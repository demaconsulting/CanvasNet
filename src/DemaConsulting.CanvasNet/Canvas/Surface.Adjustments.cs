// cspell:ignore luma otsu grayscale gamma percentile percentiles midtone midtones
using System.Runtime.CompilerServices;

namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The tone and color adjustments of <see cref="Surface"/>: luma, grayscale, invert, threshold (fixed and Otsu),
///     levels, brightness, contrast, and auto-levels.
/// </summary>
/// <remarks>
///     <para>
///         Every adjustment that changes pixels works <b>in place</b>, like <see cref="Clear"/> and
///         <see cref="PremultiplyAlpha"/>, so that adjusting a large surface allocates no second full-size surface; call
///         <see cref="Clone"/> first to keep the original. Surfaces hold straight (non-premultiplied) alpha.
///     </para>
///     <para>
///         Nothing here uses floating point to decide a pixel except the three table builders (gamma power, contrast
///         factor), which are computed once per call into a 256-entry table; luma, thresholds, histograms, and the Otsu
///         search are exact integer arithmetic. Rows are processed in parallel with disjoint output, and the histogram
///         is summed in fixed 64-row blocks merged in block order, so the results do not depend on the number of cores.
///         Nothing allocates per pixel. A surface must not be adjusted while another thread uses it.
///     </para>
/// </remarks>
public sealed partial class Surface
{
    /// <summary>The smallest gamma that <see cref="Levels"/> accepts.</summary>
    public const double MinGamma = 0.1;

    /// <summary>The largest gamma that <see cref="Levels"/> accepts.</summary>
    public const double MaxGamma = 10.0;

    /// <summary>The percentage of the pixels ignored at each end by <see cref="AutoLevels"/> when none is given.</summary>
    public const double DefaultAutoLevelsClipPercent = 1.0;

    /// <summary>The largest percentage of the pixels per end that <see cref="AutoLevels"/> accepts.</summary>
    public const double MaxAutoLevelsClipPercent = 20.0;

    /// <summary>The number of rows summed into one block of a histogram; the blocks are merged in order.</summary>
    private const int HistogramBlockRows = 64;

    /// <summary>The threshold Otsu's method returns when every pixel has the same luma.</summary>
    private const int OtsuFallback = 127;

    /// <summary>The number of luma levels, and the length of every table and histogram.</summary>
    private const int LevelCount = 256;

    /// <summary>
    ///     Gets the luma of a pixel, flattened over white.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns>
    ///     <c>Y = (77 R + 150 G + 29 B + 128) &gt;&gt; 8</c> (BT.601 in fixed point, 0 to 255) when the pixel is opaque; for a
    ///     pixel with alpha <c>A</c> below 255 the luma of the pixel flattened over white,
    ///     <c>(Y * A + 255 * (255 - A) + 127) / 255</c> with integer division, so a fully transparent pixel has the luma
    ///     255.
    /// </returns>
    /// <remarks>
    ///     Flattening over white treats transparent paper as the white it will look like, so every adjustment that
    ///     decides by brightness (<see cref="Threshold"/>, <see cref="LumaHistogram"/>) agrees on one brightness of a
    ///     pixel. Fixed-point weights give the same number on every machine.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Luma(Rgba32 pixel)
    {
        var y = OpaqueLuma(pixel);
        var a = pixel.A;
        return a == 255 ? y : ((y * a) + (255 * (255 - a)) + 127) / 255;
    }

    /// <summary>
    ///     Replaces the color of every pixel with its luma, in place.
    /// </summary>
    /// <remarks>
    ///     Red, green, and blue become the luma of the pixel's own color (not flattened over white); alpha is unchanged.
    ///     The result is still an RGBA surface, and an already gray pixel is unchanged.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public void Grayscale()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Parallel.For(0, Height, y =>
        {
            foreach (ref var pixel in GetRowSpan(y))
            {
                var gray = (byte)OpaqueLuma(pixel);
                pixel.R = gray;
                pixel.G = gray;
                pixel.B = gray;
            }
        });
    }

    /// <summary>
    ///     Replaces the red, green, and blue of every pixel with their complement, in place.
    /// </summary>
    /// <remarks>Each color channel <c>c</c> becomes <c>255 - c</c>; alpha is unchanged, so inverting twice restores the surface.</remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public void Invert()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Parallel.For(0, Height, y =>
        {
            foreach (ref var pixel in GetRowSpan(y))
            {
                pixel.R = (byte)(255 - pixel.R);
                pixel.G = (byte)(255 - pixel.G);
                pixel.B = (byte)(255 - pixel.B);
            }
        });
    }

    /// <summary>
    ///     Turns every pixel into opaque black or opaque white by its luma, in place.
    /// </summary>
    /// <param name="level">The threshold, 0 to 255.</param>
    /// <remarks>
    ///     A pixel becomes opaque white (255, 255, 255, 255) when its <see cref="Luma"/> (over white) is greater than
    ///     <paramref name="level"/> and opaque black otherwise: level 255 gives an all-black result and level 0 makes only
    ///     luma 0 black. Use <see cref="OtsuThreshold"/> to choose a level from the image itself.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is not 0 to 255.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public void Threshold(int level)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 255);

        var white = new Rgba32(255, 255, 255, 255);
        var black = new Rgba32(0, 0, 0, 255);
        Parallel.For(0, Height, y =>
        {
            foreach (ref var pixel in GetRowSpan(y))
            {
                pixel = Luma(pixel) > level ? white : black;
            }
        });
    }

    /// <summary>
    ///     Counts the pixels of each luma level.
    /// </summary>
    /// <returns>A new array of 256 counts, the number of pixels with each <see cref="Luma"/> (over white) from 0 to 255.</returns>
    /// <remarks>
    ///     The surface is not modified. The rows are summed in blocks of 64 rows, each block into its own array in
    ///     parallel, and the block arrays are added in block order; the counts are exact integers, so the result is the
    ///     same on any number of cores.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public long[] LumaHistogram()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var blocks = (Height + HistogramBlockRows - 1) / HistogramBlockRows;
        var partial = new long[blocks][];
        Parallel.For(0, blocks, block =>
        {
            var counts = new long[LevelCount];
            var end = Math.Min(Height, (block + 1) * HistogramBlockRows);
            for (var y = block * HistogramBlockRows; y < end; y++)
            {
                foreach (var pixel in GetRowSpan(y))
                {
                    counts[Luma(pixel)]++;
                }
            }

            partial[block] = counts;
        });

        var histogram = new long[LevelCount];
        foreach (var counts in partial)
        {
            for (var level = 0; level < LevelCount; level++)
            {
                histogram[level] += counts[level];
            }
        }

        return histogram;
    }

    /// <summary>
    ///     Finds the threshold that best separates the two classes of a luma histogram (Otsu's method).
    /// </summary>
    /// <param name="histogram">The 256 counts of the luma levels, for example from <see cref="LumaHistogram"/>.</param>
    /// <returns>
    ///     The threshold <c>T</c> from 0 to 254 for <see cref="Threshold"/> (pixels with luma <c>&gt; T</c> are the bright
    ///     class). For each candidate <c>t</c> from 0 to 254 with pixels on both sides (<c>w0</c> pixels with a level up to
    ///     <c>t</c> and sum <c>S0</c> of their levels, <c>w1</c> and <c>S1</c> for the rest) the score is
    ///     <c>(w1 * S0 - w0 * S1)^2 / (w0 * w1)</c>, the between-class variance up to a constant factor; the sums are exact
    ///     64-bit integers and only the final division is floating point. The candidate with the largest score wins; the
    ///     candidates that tie (a gap of empty levels between two modes gives bit-identical scores) are resolved to the
    ///     middle of the first and last of them, <c>(first + last) / 2</c> with integer division. When no candidate has
    ///     pixels on both sides (all pixels share one level, or there are none) the result is 127.
    /// </returns>
    /// <remarks>
    ///     Otsu's method picks a threshold from the image itself, so a photograph needs no trial and error; the exact sums
    ///     and the rule for ties make the chosen level the same on every machine.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="histogram"/> does not have 256 entries or has a negative count.</exception>
    public static int OtsuThreshold(ReadOnlySpan<long> histogram)
    {
        if (histogram.Length != LevelCount)
        {
            throw new ArgumentException("The histogram must have 256 entries.", nameof(histogram));
        }

        long total = 0;
        long sum = 0;
        for (var level = 0; level < LevelCount; level++)
        {
            if (histogram[level] < 0)
            {
                throw new ArgumentException("A histogram count cannot be negative.", nameof(histogram));
            }

            total += histogram[level];
            sum += level * histogram[level];
        }

        long w0 = 0;
        long s0 = 0;
        var bestScore = -1.0;
        var first = -1;
        var last = -1;
        for (var t = 0; t < LevelCount - 1; t++)
        {
            w0 += histogram[t];
            s0 += t * histogram[t];
            var w1 = total - w0;
            if (w0 == 0 || w1 == 0)
            {
                continue;
            }

            var s1 = sum - s0;
            var numerator = (double)((w1 * s0) - (w0 * s1));
            var score = numerator * numerator / ((double)w0 * w1);
            if (score > bestScore)
            {
                bestScore = score;
                first = t;
                last = t;
            }
            else if (score.CompareTo(bestScore) == 0)
            {
                last = t;
            }
        }

        return first < 0 ? OtsuFallback : (first + last) / 2;
    }

    /// <summary>
    ///     Maps the levels of every pixel through a black point, a white point, and a gamma, in place.
    /// </summary>
    /// <param name="black">The input level that becomes 0, 0 to 254.</param>
    /// <param name="white">The input level that becomes 255, 1 to 255; greater than <paramref name="black"/>.</param>
    /// <param name="gamma">The midtone exponent, <see cref="MinGamma"/> to <see cref="MaxGamma"/>; 1 (the default) is linear and a value above 1 brightens the midtones.</param>
    /// <remarks>
    ///     The red, green, and blue of every pixel go through the table of <c>BuildLevelsTable</c>: with
    ///     <c>t = clamp((v - black) / (white - black), 0, 1)</c> the new level for <c>v</c> is
    ///     <c>Round(255 * t^(1 / gamma))</c> (half away from zero). Alpha is unchanged, and the same table serves the three
    ///     channels, so a gray pixel stays gray.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is outside its range.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="black"/> is not less than <paramref name="white"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public void Levels(int black, int white, double gamma = 1.0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ApplyTable(BuildLevelsTable(black, white, gamma));
    }

    /// <summary>
    ///     Adds an amount to the red, green, and blue of every pixel, in place.
    /// </summary>
    /// <param name="amount">The value added to each level, -255 to 255.</param>
    /// <remarks>Each color channel <c>v</c> becomes <c>clamp(v + amount, 0, 255)</c>; alpha is unchanged.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not -255 to 255.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public void Brightness(int amount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ApplyTable(BuildBrightnessTable(amount));
    }

    /// <summary>
    ///     Changes the contrast of the red, green, and blue of every pixel around mid-gray, in place.
    /// </summary>
    /// <param name="contrast">The contrast, -100 to 100; 0 changes nothing.</param>
    /// <remarks>
    ///     With <c>C = contrast * 255 / 100</c> and <c>f = 259 * (C + 255) / (255 * (259 - C))</c> each color channel
    ///     <c>v</c> becomes <c>clamp(Round(f * (v - 128) + 128), 0, 255)</c> (half away from zero), so levels pivot around
    ///     128. Alpha is unchanged.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="contrast"/> is not a number from -100 to 100.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public void Contrast(double contrast)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ApplyTable(BuildContrastTable(contrast));
    }

    /// <summary>
    ///     Stretches the contrast so that the black and white points of the luma histogram become 0 and 255, in place.
    /// </summary>
    /// <param name="clipPercent">The percentage of the pixels ignored at each end, 0 to <see cref="MaxAutoLevelsClipPercent"/>; the default is <see cref="DefaultAutoLevelsClipPercent"/>.</param>
    /// <returns>
    ///     The black and white points that were applied, or <see langword="null"/> when the image is flat (it has points
    ///     less than one level apart) and was left unchanged. With <c>N</c> pixels, <c>k = max(1, ceil(N * p / 100))</c>
    ///     (a tolerance of 1e-9 absorbs floating-point noise in the product), black is the smallest level whose cumulative
    ///     <see cref="LumaHistogram"/> count from 0 is at least <c>k</c>, and white is the largest level whose cumulative
    ///     count from 255 is at least <c>k</c>.
    /// </returns>
    /// <remarks>
    ///     A photograph taken in poor light uses a narrow band of levels; a percentile stretch ignores a few specks at
    ///     either end and leaves an image with no contrast alone instead of amplifying noise. The points are applied to
    ///     red, green, and blue as a <see cref="Levels"/> adjustment with gamma 1, so the stretch follows luma and the hues
    ///     are preserved (a color cast is not removed).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="clipPercent"/> is not a number from 0 to 20.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public (int Black, int White)? AutoLevels(double clipPercent = DefaultAutoLevelsClipPercent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var range = AutoLevelsRange(LumaHistogram(), clipPercent);
        if (range is not { } points)
        {
            return null;
        }

        ApplyTable(BuildLevelsTable(points.Black, points.White, 1.0));
        return points;
    }

    /// <summary>
    ///     Builds the 256-entry table of a levels adjustment.
    /// </summary>
    /// <param name="black">The input level that becomes 0, 0 to 254.</param>
    /// <param name="white">The input level that becomes 255, 1 to 255; greater than <paramref name="black"/>.</param>
    /// <param name="gamma">The midtone exponent, <see cref="MinGamma"/> to <see cref="MaxGamma"/>.</param>
    /// <returns>
    ///     The table: with <c>t = clamp((v - black) / (white - black), 0, 1)</c> the entry for level <c>v</c> is
    ///     <c>Round(255 * t^(1 / gamma))</c> (half away from zero); <c>Math.Pow</c> is not called when the gamma is exactly 1.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is outside its range.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="black"/> is not less than <paramref name="white"/>.</exception>
    internal static byte[] BuildLevelsTable(int black, int white, double gamma)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(black);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(black, 254);
        ArgumentOutOfRangeException.ThrowIfLessThan(white, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(white, 255);
        if (double.IsNaN(gamma) || gamma < MinGamma || gamma > MaxGamma)
        {
            throw new ArgumentOutOfRangeException(nameof(gamma), gamma, "The gamma must be 0.1 to 10.");
        }

        if (black >= white)
        {
            throw new ArgumentException("The black level must be less than the white level.", nameof(black));
        }

        var table = new byte[LevelCount];
        var range = (double)(white - black);
        var exponent = 1.0 / gamma;
        var linear = gamma.CompareTo(1.0) == 0;
        for (var v = 0; v < LevelCount; v++)
        {
            var t = Math.Clamp((v - black) / range, 0.0, 1.0);
            var scaled = linear ? 255.0 * t : 255.0 * Math.Pow(t, exponent);
            table[v] = (byte)Math.Clamp(Math.Round(scaled, MidpointRounding.AwayFromZero), 0, 255);
        }

        return table;
    }

    /// <summary>
    ///     Builds the 256-entry table of a brightness adjustment.
    /// </summary>
    /// <param name="amount">The value added to each level, -255 to 255.</param>
    /// <returns>The table: the entry for level <c>v</c> is <c>clamp(v + amount, 0, 255)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not -255 to 255.</exception>
    internal static byte[] BuildBrightnessTable(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, -255);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amount, 255);

        var table = new byte[LevelCount];
        for (var v = 0; v < LevelCount; v++)
        {
            table[v] = (byte)Math.Clamp(v + amount, 0, 255);
        }

        return table;
    }

    /// <summary>
    ///     Builds the 256-entry table of a contrast adjustment.
    /// </summary>
    /// <param name="contrast">The contrast, -100 to 100; 0 changes nothing.</param>
    /// <returns>
    ///     The table: with <c>C = contrast * 255 / 100</c> and <c>f = 259 * (C + 255) / (255 * (259 - C))</c> the entry for
    ///     level <c>v</c> is <c>clamp(Round(f * (v - 128) + 128), 0, 255)</c> (half away from zero), so levels pivot around
    ///     mid-gray 128.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="contrast"/> is not a number from -100 to 100.</exception>
    internal static byte[] BuildContrastTable(double contrast)
    {
        if (double.IsNaN(contrast) || contrast < -100 || contrast > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(contrast), contrast, "The contrast must be -100 to 100.");
        }

        var c = contrast * 255.0 / 100.0;
        var factor = 259.0 * (c + 255.0) / (255.0 * (259.0 - c));
        var table = new byte[LevelCount];
        for (var v = 0; v < LevelCount; v++)
        {
            var mapped = (factor * (v - 128)) + 128;
            table[v] = (byte)Math.Clamp(Math.Round(mapped, MidpointRounding.AwayFromZero), 0, 255);
        }

        return table;
    }

    /// <summary>
    ///     Maps the red, green, and blue of every pixel through a table, in place.
    /// </summary>
    /// <param name="table">The 256 output levels, indexed by the input level.</param>
    /// <remarks>Alpha is unchanged. The same table is used for all three channels, so a gray pixel stays gray.</remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="table"/> does not have 256 entries.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    internal void ApplyTable(ReadOnlySpan<byte> table)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (table.Length != LevelCount)
        {
            throw new ArgumentException("The table must have 256 entries.", nameof(table));
        }

        var map = table.ToArray();
        Parallel.For(0, Height, y =>
        {
            foreach (ref var pixel in GetRowSpan(y))
            {
                pixel.R = map[pixel.R];
                pixel.G = map[pixel.G];
                pixel.B = map[pixel.B];
            }
        });
    }

    /// <summary>
    ///     Finds the black and white points of an auto-levels stretch from a luma histogram.
    /// </summary>
    /// <param name="histogram">The 256 counts of the luma levels.</param>
    /// <param name="clipPercent">The percentage of the pixels ignored at each end, 0 to <see cref="MaxAutoLevelsClipPercent"/>.</param>
    /// <returns>
    ///     The black and white points, or <see langword="null"/> when the image is flat (it has no pixels, or the points are
    ///     less than one level apart) and nothing should change.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="histogram"/> does not have 256 entries or has a negative count.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="clipPercent"/> is not a number from 0 to 20.</exception>
    internal static (int Black, int White)? AutoLevelsRange(ReadOnlySpan<long> histogram, double clipPercent)
    {
        if (histogram.Length != LevelCount)
        {
            throw new ArgumentException("The histogram must have 256 entries.", nameof(histogram));
        }

        if (double.IsNaN(clipPercent) || clipPercent < 0 || clipPercent > MaxAutoLevelsClipPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(clipPercent), clipPercent, "The clip must be 0 to 20 percent.");
        }

        long total = 0;
        foreach (var count in histogram)
        {
            if (count < 0)
            {
                throw new ArgumentException("A histogram count cannot be negative.", nameof(histogram));
            }

            total += count;
        }

        if (total == 0)
        {
            return null;
        }

        var k = Math.Min(total, Math.Max(1L, (long)Math.Ceiling((total * clipPercent / 100.0) - 1e-9)));

        var black = 0;
        long cumulative = 0;
        for (var v = 0; v < LevelCount; v++)
        {
            cumulative += histogram[v];
            if (cumulative >= k)
            {
                black = v;
                break;
            }
        }

        var white = LevelCount - 1;
        cumulative = 0;
        for (var v = LevelCount - 1; v >= 0; v--)
        {
            cumulative += histogram[v];
            if (cumulative >= k)
            {
                white = v;
                break;
            }
        }

        return white - black < 1 ? null : (black, white);
    }

    /// <summary>
    ///     Gets the luma of the color of a pixel, ignoring its alpha.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns><c>(77 R + 150 G + 29 B + 128) &gt;&gt; 8</c>, 0 to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OpaqueLuma(Rgba32 pixel)
    {
        return ((77 * pixel.R) + (150 * pixel.G) + (29 * pixel.B) + 128) >> 8;
    }
}
