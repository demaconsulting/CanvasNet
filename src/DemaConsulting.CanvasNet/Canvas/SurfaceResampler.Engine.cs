using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;

namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The general-purpose resampling engine of the <see cref="SurfaceResampler"/> unit: separable
///     two-pass filtering with precomputed per-axis weight tables, used by
///     <see cref="Surface.Resize"/> and by every document renderer that draws a raster image.
/// </summary>
/// <remarks>
///     <para>
///     Each axis is described by a fractional source interval: output pixel <c>o</c> has its
///     center at source position <c>start + ((o + 0.5) * extent / outDim)</c> (source pixel
///     <c>i</c> covers <c>[i, i + 1)</c> so its center is <c>i + 0.5</c>). A negative extent
///     mirrors the axis. This one description serves a plain resize (the full source range) and
///     the device-space draw of an image at a fractional offset and scale.
///     </para>
///     <para>
///     Weight tables are built once per axis. For the smooth kernels the support is multiplied
///     by the reduction ratio when the axis is shrunk, which makes the kernel a true low-pass
///     prefilter (a one-pixel line is attenuated, never skipped). Taps that fall outside the
///     image are folded into the edge pixel (edge clamp) and each row of weights is normalized
///     to sum to one. All filtering is performed in premultiplied alpha so transparent pixels
///     never bleed their (meaningless) color into their neighbors.
///     </para>
///     <para>
///     Memory is bounded: the horizontal pass writes a float intermediate for a strip of output
///     columns across the source rows that the vertical pass needs, and the strip width is chosen
///     so the intermediate fits a byte budget, so the working set is the destination surface plus
///     one strip regardless of image size.
///     </para>
/// </remarks>
internal static partial class SurfaceResampler
{
    /// <summary>The default budget, in bytes, for the float intermediate of the horizontal pass (8 MiB).</summary>
    internal const int DefaultIntermediateBudgetBytes = 8 * 1024 * 1024;

    /// <summary>Floats stored per intermediate pixel (premultiplied R, G, B and alpha).</summary>
    private const int Channels = 4;

    /// <summary>The most out-of-image taps summed exactly per folded edge tail; longer tails are sampled.</summary>
    private const int MaxExactTailTaps = 1024;

    /// <summary>Per-axis weight table: for each output index a first source index, a tap count and the weights.</summary>
    /// <param name="First">First source index of each output pixel's taps.</param>
    /// <param name="Count">Number of consecutive taps of each output pixel.</param>
    /// <param name="Offset">Offset of each output pixel's weights in <paramref name="Weights"/>.</param>
    /// <param name="Weights">All normalized weights, concatenated.</param>
    /// <param name="MinIndex">Smallest source index referenced by any output pixel.</param>
    /// <param name="MaxIndex">Largest source index referenced by any output pixel.</param>
    private sealed record AxisWeights(int[] First, int[] Count, int[] Offset, float[] Weights, int MinIndex, int MaxIndex);

    /// <summary>
    ///     Resamples a fractional source window of <paramref name="source"/> to a new
    ///     <paramref name="outWidth"/> by <paramref name="outHeight"/> surface.
    /// </summary>
    /// <param name="source">The source surface (not modified).</param>
    /// <param name="startX">Source x of the left edge of the first output column.</param>
    /// <param name="extentX">Signed source width covered by all output columns.</param>
    /// <param name="startY">Source y of the top edge of the first output row.</param>
    /// <param name="extentY">Signed source height covered by all output rows.</param>
    /// <param name="outWidth">Output width in pixels (at least one).</param>
    /// <param name="outHeight">Output height in pixels (at least one).</param>
    /// <param name="mode">The filter; <see cref="ResamplingMode.Auto"/> is resolved per axis.</param>
    /// <param name="intermediateBudgetBytes">Budget for the float intermediate; at least one column is always processed.</param>
    /// <returns>A new surface owned by the caller.</returns>
    internal static Surface Resample(
        Surface source,
        double startX,
        double extentX,
        double startY,
        double extentY,
        int outWidth,
        int outHeight,
        ResamplingMode mode,
        int intermediateBudgetBytes = DefaultIntermediateBudgetBytes)
    {
        var xs = BuildAxis(source.Width, startX, extentX, outWidth, mode);
        var ys = BuildAxis(source.Height, startY, extentY, outHeight, mode);
        var result = new Surface(outWidth, outHeight);

        // Choose the strip width so the intermediate (needed rows x strip columns x 16 bytes) fits the budget
        var rows = ys.MaxIndex - ys.MinIndex + 1;
        var bytesPerColumn = (long)rows * Channels * sizeof(float);
        var strip = (int)Math.Clamp(intermediateBudgetBytes / bytesPerColumn, 1L, outWidth);
        var rowStride = checked(strip * Channels);
        var intermediate = ArrayPool<float>.Shared.Rent(checked(rows * rowStride));
        var accumulator = ArrayPool<float>.Shared.Rent(rowStride);

        // Pooled buffers are not cleared: every element read below (the strip's rows of the
        // intermediate, and the accumulator's first Multiply tap) is written first.
        try
        {
            for (var column0 = 0; column0 < outWidth; column0 += strip)
            {
                var columns = Math.Min(strip, outWidth - column0);

                // Horizontal pass: filter every needed source row into the intermediate strip
                for (var r = 0; r < rows; r++)
                {
                    HorizontalRow(source.GetRowSpan(ys.MinIndex + r), xs, column0, columns, intermediate.AsSpan(r * rowStride, columns * Channels));
                }

                // Vertical pass: each output row is a weighted sum of intermediate rows
                var acc = accumulator.AsSpan(0, columns * Channels);
                for (var oy = 0; oy < outHeight; oy++)
                {
                    var first = ys.First[oy];
                    var count = ys.Count[oy];
                    var offset = ys.Offset[oy];
                    for (var t = 0; t < count; t++)
                    {
                        var row = intermediate.AsSpan((first + t - ys.MinIndex) * rowStride, columns * Channels);
                        if (t == 0)
                        {
                            TensorPrimitives.Multiply(row, ys.Weights[offset], acc);
                        }
                        else
                        {
                            TensorPrimitives.MultiplyAdd(row, ys.Weights[offset + t], acc, acc);
                        }
                    }

                    StoreRow(acc, result.GetRowSpan(oy).Slice(column0, columns));
                }
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(intermediate);
            ArrayPool<float>.Shared.Return(accumulator);
        }

        return result;
    }

    /// <summary>
    ///     Filters one source row horizontally into premultiplied float pixels for output columns
    ///     <paramref name="column0"/> .. <paramref name="column0"/> + <paramref name="columns"/> - 1.
    /// </summary>
    private static void HorizontalRow(ReadOnlySpan<Rgba32> row, AxisWeights xs, int column0, int columns, Span<float> destination)
    {
        for (var j = 0; j < columns; j++)
        {
            var ox = column0 + j;
            var first = xs.First[ox];
            var count = xs.Count[ox];
            var offset = xs.Offset[ox];
            float sumR = 0, sumG = 0, sumB = 0, sumA = 0;
            for (var t = 0; t < count; t++)
            {
                var pixel = row[first + t];
                if (pixel.A == 0)
                {
                    continue;
                }

                // Premultiply on the fly: w * a is the pixel's alpha contribution, /255 scales colors by alpha
                var weightedAlpha = xs.Weights[offset + t] * pixel.A;
                var f = weightedAlpha / 255f;
                sumA += weightedAlpha;
                sumR += pixel.R * f;
                sumG += pixel.G * f;
                sumB += pixel.B * f;
            }

            var d = j * Channels;
            destination[d] = sumR;
            destination[d + 1] = sumG;
            destination[d + 2] = sumB;
            destination[d + 3] = sumA;
        }
    }

    /// <summary>Clamps premultiplied float pixels, un-premultiplies them and stores them as bytes.</summary>
    private static void StoreRow(ReadOnlySpan<float> source, Span<Rgba32> destination)
    {
        for (var j = 0; j < destination.Length; j++)
        {
            var d = j * Channels;

            // Clamp alpha first (kills negative kernel lobes), then color to [0, alpha]
            var alpha = Math.Clamp(source[d + 3], 0f, 255f);
            var alphaByte = (int)MathF.Round(alpha, MidpointRounding.AwayFromZero);
            if (alphaByte == 0)
            {
                destination[j] = new Rgba32(0, 0, 0, 0);
                continue;
            }

            var scale = 255f / alpha;
            destination[j] = new Rgba32(
                ToByte(Math.Clamp(source[d], 0f, alpha) * scale),
                ToByte(Math.Clamp(source[d + 1], 0f, alpha) * scale),
                ToByte(Math.Clamp(source[d + 2], 0f, alpha) * scale),
                (byte)alphaByte);
        }
    }

    /// <summary>
    ///     Builds the weight table for one axis.
    /// </summary>
    /// <param name="sourceSize">Source pixels along the axis.</param>
    /// <param name="start">Source coordinate of the first output pixel's leading edge.</param>
    /// <param name="extent">Signed source length covered by all output pixels.</param>
    /// <param name="outSize">Output pixels along the axis.</param>
    /// <param name="mode">Requested filter (<see cref="ResamplingMode.Auto"/> is resolved here).</param>
    private static AxisWeights BuildAxis(int sourceSize, double start, double extent, int outSize, ResamplingMode mode)
    {
        var step = extent / outSize;
        var scale = Math.Abs(step);
        var last = sourceSize - 1;

        // Resolve Auto per axis: area averaging when reducing, Catmull-Rom otherwise
        if (mode == ResamplingMode.Auto)
        {
            mode = scale > 1.0 ? ResamplingMode.Area : ResamplingMode.Bicubic;
        }

        // An axis that maps pixels 1:1 onto whole source pixels is an exact copy for every filter
        if (Math.Abs(scale - 1.0) < 1e-9 && Math.Abs(start - Math.Round(start)) < 1e-9)
        {
            mode = ResamplingMode.Nearest;
        }

        var radius = mode switch
        {
            ResamplingMode.Bilinear => 1.0,
            ResamplingMode.Bicubic => 2.0,
            ResamplingMode.Lanczos3 => 3.0,
            _ => 0.0,
        };

        // Pass 1: the tap interval [lo, hi] (before edge folding) of every output pixel
        var firsts = new int[outSize];
        var counts = new int[outSize];
        var offsets = new int[outSize];
        var los = new long[outSize];
        var his = new long[outSize];
        long total = 0;
        var minIndex = int.MaxValue;
        var maxIndex = int.MinValue;
        for (var o = 0; o < outSize; o++)
        {
            var c = start + ((o + 0.5) * step);
            long lo, hi;
            switch (mode)
            {
                case ResamplingMode.Nearest:
                    lo = hi = (long)Math.Floor(c);
                    break;
                case ResamplingMode.Area:
                    lo = (long)Math.Floor(c - (scale / 2));
                    hi = (long)Math.Ceiling(c + (scale / 2)) - 1;
                    break;
                default:
                    var support = radius * Math.Max(1.0, scale);
                    lo = (long)Math.Ceiling(c - support - 0.5);
                    hi = (long)Math.Floor(c + support - 0.5);
                    break;
            }

            if (hi < lo)
            {
                lo = hi = (long)Math.Floor(c);
            }

            los[o] = lo;
            his[o] = hi;
            var first = (int)Math.Clamp(lo, 0, last);
            var end = (int)Math.Clamp(hi, 0, last);
            firsts[o] = first;
            counts[o] = end - first + 1;
            offsets[o] = checked((int)total);
            total = checked(total + counts[o]);
            minIndex = Math.Min(minIndex, first);
            maxIndex = Math.Max(maxIndex, end);
        }

        // Pass 2: fill the weights, folding out-of-range taps into the edge pixel, then normalize
        var weights = new float[checked((int)total)];
        var buffer = new double[counts.Max()];
        for (var o = 0; o < outSize; o++)
        {
            var c = start + ((o + 0.5) * step);
            var count = counts[o];
            var first = firsts[o];
            var w = buffer.AsSpan(0, count);
            w.Clear();
            var inLo = Math.Max(los[o], 0);
            var inHi = Math.Min(his[o], last);
            for (var i = inLo; i <= inHi; i++)
            {
                w[(int)i - first] += Weight(mode, i + 0.5 - c, scale);
            }

            // Area drops out-of-image taps (renormalized); other filters fold them into the edge pixel.
            // The folded tails can be enormous for extreme scales, so their work is bounded.
            if (mode != ResamplingMode.Area)
            {
                if (los[o] < 0)
                {
                    w[0 - first] += TailWeight(mode, los[o], Math.Min(his[o], -1L), c, scale);
                }

                if (his[o] > last)
                {
                    w[last - first] += TailWeight(mode, Math.Max(los[o], (long)last + 1), his[o], c, scale);
                }
            }

            var sum = 0.0;
            foreach (var v in w)
            {
                sum += v;
            }

            if (!(sum > 1e-9))
            {
                // Degenerate (no overlap or cancelling lobes): fall back to the nearest source pixel
                w.Clear();
                var nearest = (int)Math.Clamp((long)Math.Floor(c), 0, last) - first;
                w[Math.Clamp(nearest, 0, count - 1)] = 1.0;
                sum = 1.0;
            }

            for (var t = 0; t < count; t++)
            {
                weights[offsets[o] + t] = (float)(w[t] / sum);
            }
        }

        return new AxisWeights(firsts, counts, offsets, weights, minIndex, maxIndex);
    }

    /// <summary>
    ///     Sums the filter weights of the taps <paramref name="from"/> .. <paramref name="to"/> (all outside
    ///     the image). Short tails are summed exactly; longer ones are sampled at a bounded number of
    ///     evenly spaced taps so the work never depends on the footprint size.
    /// </summary>
    private static double TailWeight(ResamplingMode mode, long from, long to, double c, double scale)
    {
        if (to < from)
        {
            return 0.0;
        }

        var n = to - from + 1;
        if (n <= MaxExactTailTaps)
        {
            var exact = 0.0;
            for (var i = from; i <= to; i++)
            {
                exact += Weight(mode, i + 0.5 - c, scale);
            }

            return exact;
        }

        // Midpoint rule over MaxExactTailTaps equal buckets, each bucket weighted by its tap count
        var sum = 0.0;
        for (var b = 0; b < MaxExactTailTaps; b++)
        {
            var b0 = from + (n * b / MaxExactTailTaps);
            var b1 = from + (n * (b + 1) / MaxExactTailTaps);
            if (b1 > b0)
            {
                sum += (b1 - b0) * Weight(mode, ((b0 + b1 - 1) / 2.0) + 0.5 - c, scale);
            }
        }

        return sum;
    }

    /// <summary>
    ///     Evaluates the filter weight of one source pixel.
    /// </summary>
    /// <param name="mode">The resolved (non-Auto) filter.</param>
    /// <param name="distance">Source-space distance from the output sample center to the source pixel center.</param>
    /// <param name="scale">Absolute source pixels per output pixel (the kernel is widened when above one).</param>
    private static double Weight(ResamplingMode mode, double distance, double scale)
    {
        if (mode == ResamplingMode.Nearest)
        {
            return 1.0;
        }

        if (mode == ResamplingMode.Area)
        {
            // Exact overlap of the output footprint [c - scale/2, c + scale/2] with the pixel [d - 0.5, d + 0.5]
            var half = scale / 2;
            var overlap = Math.Min(distance + 0.5, half) - Math.Max(distance - 0.5, -half);
            return Math.Max(0.0, overlap);
        }

        var x = Math.Abs(distance) / Math.Max(1.0, scale);
        return mode switch
        {
            ResamplingMode.Bilinear => x < 1.0 ? 1.0 - x : 0.0,
            ResamplingMode.Bicubic => CatmullRom(x),
            _ => x < 3.0 ? Sinc(x) * Sinc(x / 3.0) : 0.0,
        };
    }

    /// <summary>Device pixels per tile pixel above which a tile is considered magnified (smoothed).</summary>
    private const double TileMagnifiedThreshold = 1.05;

    /// <summary>Device pixels per tile pixel below which a tile is considered minified (prefiltered).</summary>
    private const double TileMinifiedThreshold = 0.95;

    /// <summary>
    ///     Classifies the device scale of one tile pixel. Magnification on either axis wins
    ///     (the whole tile is then sampled bilinearly, no prefilter); otherwise, when either axis is
    ///     minified, only the minified axes are reduced (the other keeps its size).
    /// </summary>
    /// <param name="deviceX">Device pixels per tile pixel along x.</param>
    /// <param name="deviceY">Device pixels per tile pixel along y.</param>
    /// <param name="tileWidth">Tile buffer width in pixels.</param>
    /// <param name="tileHeight">Tile buffer height in pixels.</param>
    /// <param name="magnified">Receives <see langword="true"/> when the tile should be sampled bilinearly.</param>
    /// <param name="reducedWidth">Receives the prefiltered width when minified.</param>
    /// <param name="reducedHeight">Receives the prefiltered height when minified.</param>
    /// <returns><see langword="true"/> when the tile should be prefiltered to the reduced size.</returns>
    internal static bool ClassifyTileScale(
        double deviceX,
        double deviceY,
        int tileWidth,
        int tileHeight,
        out bool magnified,
        out int reducedWidth,
        out int reducedHeight)
    {
        magnified = false;
        reducedWidth = tileWidth;
        reducedHeight = tileHeight;
        if (!double.IsFinite(deviceX) || !double.IsFinite(deviceY) || deviceX <= 0 || deviceY <= 0)
        {
            return false;
        }

        if (deviceX > TileMagnifiedThreshold || deviceY > TileMagnifiedThreshold)
        {
            magnified = true;
            return false;
        }

        if (deviceX >= TileMinifiedThreshold && deviceY >= TileMinifiedThreshold)
        {
            return false;
        }

        if (deviceX < TileMinifiedThreshold)
        {
            reducedWidth = Math.Max(1, (int)Math.Round(tileWidth * deviceX));
        }

        if (deviceY < TileMinifiedThreshold)
        {
            reducedHeight = Math.Max(1, (int)Math.Round(tileHeight * deviceY));
        }

        return true;
    }

    /// <summary>The Catmull-Rom cubic kernel (support two) at non-negative distance <paramref name="x"/>.</summary>
    private static double CatmullRom(double x)
    {
        if (x < 1.0)
        {
            return (((1.5 * x) - 2.5) * x * x) + 1.0;
        }

        return x < 2.0 ? (((((-0.5 * x) + 2.5) * x) - 4.0) * x) + 2.0 : 0.0;
    }

    /// <summary>The normalized sinc function.</summary>
    private static double Sinc(double x) => x < 1e-12 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);

    /// <summary>
    ///     Resamples the whole of <paramref name="source"/> to a new size (the engine behind
    ///     <see cref="Surface.Resize"/>); sizes must already be validated.
    /// </summary>
    internal static Surface ResizeFull(Surface source, int width, int height, ResamplingMode mode) =>
        Resample(source, 0, source.Width, 0, source.Height, width, height, mode);

    /// <summary>
    ///     Resamples <paramref name="source"/> for an axis-aligned draw: when
    ///     <paramref name="pixelToDevice"/> contains no rotation or skew, produces a surface whose
    ///     pixels are exactly the device pixels (clipped to the visible area) covered by the image,
    ///     so the caller composites it 1:1.
    /// </summary>
    /// <param name="source">The source image (not modified).</param>
    /// <param name="pixelToDevice">Affine map from source pixel coordinates to device coordinates.</param>
    /// <param name="visibleWidth">Width of the device area that is visible.</param>
    /// <param name="visibleHeight">Height of the device area that is visible.</param>
    /// <param name="mode">The filter to use.</param>
    /// <param name="window">
    ///     Receives a new surface the caller must dispose, or <see langword="null"/> when the image
    ///     covers no visible device pixel center.
    /// </param>
    /// <param name="originX">Device x of the window's left column.</param>
    /// <param name="originY">Device y of the window's top row.</param>
    /// <returns>
    ///     <see langword="false"/> when the transform is rotated, skewed, flipped to a degenerate
    ///     size or non-finite (the caller must use an inverse-mapped fallback); otherwise
    ///     <see langword="true"/>.
    /// </returns>
    internal static bool TryResampleToDevice(
        Surface source,
        Matrix3x2 pixelToDevice,
        int visibleWidth,
        int visibleHeight,
        ResamplingMode mode,
        out Surface? window,
        out int originX,
        out int originY)
    {
        window = null;
        originX = 0;
        originY = 0;
        double m11 = pixelToDevice.M11, m12 = pixelToDevice.M12, m21 = pixelToDevice.M21, m22 = pixelToDevice.M22;
        double m31 = pixelToDevice.M31, m32 = pixelToDevice.M32;
        if (!double.IsFinite(m11) || !double.IsFinite(m12) || !double.IsFinite(m21) || !double.IsFinite(m22)
            || !double.IsFinite(m31) || !double.IsFinite(m32) || m11 == 0 || m22 == 0)
        {
            return false;
        }

        // Rotation/skew is tolerated only while it moves no pixel by more than a hundredth of a device pixel
        if (Math.Abs(m12) * source.Height > 0.01 || Math.Abs(m21) * source.Width > 0.01)
        {
            return false;
        }

        // Device pixels whose centers fall inside the image's device rectangle, clipped to the visible area
        var (x0, x1) = DevicePixelRange(m31, m11 * source.Width, visibleWidth);
        var (y0, y1) = DevicePixelRange(m32, m22 * source.Height, visibleHeight);
        if (x1 <= x0 || y1 <= y0)
        {
            return true;
        }

        window = Resample(
            source,
            (x0 - m31) / m11,
            (x1 - x0) / m11,
            (y0 - m32) / m22,
            (y1 - y0) / m22,
            x1 - x0,
            y1 - y0,
            mode);
        originX = x0;
        originY = y0;
        return true;
    }

    /// <summary>
    ///     Returns the half-open range of device pixels whose centers lie within
    ///     <c>[origin, origin + length)</c> (either sign of <paramref name="length"/>), clipped to
    ///     <c>[0, visible)</c>.
    /// </summary>
    private static (int Start, int End) DevicePixelRange(double origin, double length, int visible)
    {
        var low = Math.Min(origin, origin + length);
        var high = Math.Max(origin, origin + length);
        var start = Math.Max(0.0, Math.Ceiling(low - 0.5));
        var end = Math.Min((double)visible, Math.Ceiling(high - 0.5));
        return end <= start ? (0, 0) : ((int)start, (int)end);
    }

    /// <summary>
    ///     Samples <paramref name="image"/> bilinearly at a continuous pixel position, wrapping
    ///     around both edges (for repeating tiles).
    /// </summary>
    /// <param name="image">The tile image.</param>
    /// <param name="x">Horizontal position in pixels (pixel <c>i</c> has its center at <c>i + 0.5</c>).</param>
    /// <param name="y">Vertical position in pixels.</param>
    internal static Rgba32 SampleBilinearWrapped(Surface image, double x, double y)
    {
        var fx = x - 0.5;
        var fy = y - 0.5;
        var x0f = Math.Floor(fx);
        var y0f = Math.Floor(fy);
        var tx = (float)(fx - x0f);
        var ty = (float)(fy - y0f);
        var x0 = (int)(((long)x0f % image.Width + image.Width) % image.Width);
        var y0 = (int)(((long)y0f % image.Height + image.Height) % image.Height);
        var x1 = (x0 + 1) % image.Width;
        var y1 = (y0 + 1) % image.Height;
        return Blend(image[x0, y0], image[x1, y0], image[x0, y1], image[x1, y1], tx, ty);
    }
}
