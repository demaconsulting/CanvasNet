// cspell:ignore luma otsu percentiles
using System.Runtime.CompilerServices;

namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     The analysis of what <see cref="Surface"/> contains: its dominant colors, its luma statistics, and the pixels
///     around a point.
/// </summary>
/// <remarks>
///     <para>
///         The analysis never changes the surface. Colors are counted in 4096 buckets of 4 bits per channel and
///         clustered greedily so that a gradient of paper shades is one color; the luma statistics come from the same
///         single pass. All arithmetic is exact integer arithmetic (the only floating point is the rounding of the
///         reported fractions and mean), the pass is sequential, and nothing allocates per pixel, so the result is the
///         same on every run.
///     </para>
/// </remarks>
public sealed partial class Surface
{
    /// <summary>The largest distance, in bucket units on any channel, at which a bucket joins an existing cluster.</summary>
    internal const int ClusterRadius = 2;

    /// <summary>The number of color buckets: 16 levels per channel.</summary>
    private const int ColorBuckets = 4096;

    /// <summary>
    ///     Finds the dominant colors and the luma statistics of this surface.
    /// </summary>
    /// <param name="background">The opaque color a translucent pixel is flattened over.</param>
    /// <param name="colorCount">The number of dominant colors to report, <see cref="SurfaceStatistics.MinColors"/> to <see cref="SurfaceStatistics.MaxColors"/>.</param>
    /// <returns>The statistics.</returns>
    /// <remarks>
    ///     <para>
    ///         Each pixel is flattened over <paramref name="background"/> (<c>(c * a + bg * (255 - a) + 127) / 255</c>; an
    ///         opaque pixel is unchanged). Its bucket is <c>((R &gt;&gt; 4) &lt;&lt; 8) | ((G &gt;&gt; 4) &lt;&lt; 4) | (B &gt;&gt; 4)</c> of
    ///         the flattened color; the buckets count their pixels and sum their flattened channels. The occupied buckets are
    ///         ordered by count (largest first, ties by bucket index) and clustered greedily in that order: a bucket joins the
    ///         first cluster, in creation order, whose leader bucket is within 2 bucket units on
    ///         every channel, otherwise it leads a new cluster. A cluster has the pixels of its buckets and the mean color of
    ///         those pixels (rounded half up). The clusters are ordered by pixels (largest first, ties by the leader bucket)
    ///         and the first <paramref name="colorCount"/> are reported.
    ///     </para>
    ///     <para>
    ///         The luma statistics use the luma <c>(77 R + 150 G + 29 B + 128) &gt;&gt; 8</c> of the flattened pixel: the mean
    ///         (rounded to 2 decimals), the smallest and largest level that occur, the percentiles 1, 50, and 99 (the smallest
    ///         level whose cumulative count is at least <c>ceil(N * p / 100)</c>), the Otsu threshold
    ///         (<see cref="OtsuThreshold"/>) of the 256-bin histogram, and a 16-bin histogram of <c>level &gt;&gt; 4</c>.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="background"/> is not opaque.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorCount"/> is out of range.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public SurfaceStatistics Analyze(Rgba32 background, int colorCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(colorCount, SurfaceStatistics.MinColors);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(colorCount, SurfaceStatistics.MaxColors);
        if (background.A != 255)
        {
            throw new ArgumentException("The background must be opaque.", nameof(background));
        }

        var counts = new long[ColorBuckets];
        var sumR = new long[ColorBuckets];
        var sumG = new long[ColorBuckets];
        var sumB = new long[ColorBuckets];
        var histogram = new long[LevelCount];
        long total = 0;

        for (var y = 0; y < Height; y++)
        {
            foreach (var source in GetRowSpan(y))
            {
                var r = FlattenChannel(source.R, background.R, source.A);
                var g = FlattenChannel(source.G, background.G, source.A);
                var b = FlattenChannel(source.B, background.B, source.A);
                var bucket = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
                counts[bucket]++;
                sumR[bucket] += r;
                sumG[bucket] += g;
                sumB[bucket] += b;
                histogram[((77 * r) + (150 * g) + (29 * b) + 128) >> 8]++;
                total++;
            }
        }

        var colors = ClusterColors(counts, sumR, sumG, sumB, total, colorCount);
        return new SurfaceStatistics(total, colors, ComputeLumaStatistics(histogram, total));
    }

    /// <summary>
    ///     Reads the pixel at a point and, optionally, the window of pixels around it.
    /// </summary>
    /// <param name="x">The x position of the point, 0 to <c>Width - 1</c>.</param>
    /// <param name="y">The y position of the point, 0 to <c>Height - 1</c>.</param>
    /// <param name="radius">The radius of the window, 0 (no window) to <see cref="PixelSample.MaxRadius"/>.</param>
    /// <returns>
    ///     The pixel, its luma, and, when the radius is above 0, the window <c>[x - radius, x + radius] x [y - radius, y + radius]</c>
    ///     clipped to the image with its actual rectangle, all of its pixels, and the mean of each channel (alpha included),
    ///     rounded half up.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the point is outside the surface or <paramref name="radius"/> is out of range.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    public PixelSample Sample(int x, int y, int radius = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(radius, PixelSample.MaxRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        var pixel = this[x, y];
        if (radius == 0)
        {
            return new PixelSample(x, y, pixel, Luma(pixel), null);
        }

        var left = Math.Max(0, x - radius);
        var top = Math.Max(0, y - radius);
        var right = Math.Min(Width - 1, x + radius);
        var bottom = Math.Min(Height - 1, y + radius);
        var width = right - left + 1;
        var height = bottom - top + 1;
        var pixels = new Rgba32[width * height];
        long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
        var index = 0;
        for (var row = top; row <= bottom; row++)
        {
            var span = GetRowSpan(row);
            for (var column = left; column <= right; column++)
            {
                var value = span[column];
                pixels[index++] = value;
                sumR += value.R;
                sumG += value.G;
                sumB += value.B;
                sumA += value.A;
            }
        }

        long n = pixels.Length;
        var mean = new Rgba32(RoundedMean(sumR, n), RoundedMean(sumG, n), RoundedMean(sumB, n), RoundedMean(sumA, n));
        return new PixelSample(x, y, pixel, Luma(pixel), new PixelWindow(left, top, width, height, mean, pixels));
    }

    /// <summary>
    ///     Flattens one channel of a pixel over the background.
    /// </summary>
    /// <param name="channel">The channel of the pixel.</param>
    /// <param name="background">The same channel of the background.</param>
    /// <param name="alpha">The alpha of the pixel.</param>
    /// <returns>The channel of the flattened pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FlattenChannel(int channel, int background, int alpha)
    {
        return alpha == 255 ? channel : ((channel * alpha) + (background * (255 - alpha)) + 127) / 255;
    }

    /// <summary>
    ///     Calculates the rounded mean of a sum.
    /// </summary>
    /// <param name="sum">The sum.</param>
    /// <param name="n">The number of terms.</param>
    /// <returns>The mean, rounded half up, from 0 to 255.</returns>
    private static byte RoundedMean(long sum, long n)
    {
        return (byte)(((2 * sum) + n) / (2 * n));
    }

    /// <summary>
    ///     Groups the occupied color buckets into clusters and reports the most common ones.
    /// </summary>
    /// <param name="counts">The pixel count of each bucket.</param>
    /// <param name="sumR">The sum of the flattened red of each bucket.</param>
    /// <param name="sumG">The sum of the flattened green of each bucket.</param>
    /// <param name="sumB">The sum of the flattened blue of each bucket.</param>
    /// <param name="total">The number of pixels.</param>
    /// <param name="count">The number of colors to report.</param>
    /// <returns>The dominant colors.</returns>
    private static List<DominantColor> ClusterColors(long[] counts, long[] sumR, long[] sumG, long[] sumB, long total, int count)
    {
        var occupied = new List<int>();
        for (var bucket = 0; bucket < ColorBuckets; bucket++)
        {
            if (counts[bucket] > 0)
            {
                occupied.Add(bucket);
            }
        }

        occupied.Sort((a, b) => counts[a] != counts[b] ? counts[b].CompareTo(counts[a]) : a.CompareTo(b));

        var clusters = new List<ColorCluster>();
        foreach (var bucket in occupied)
        {
            var r = bucket >> 8;
            var g = (bucket >> 4) & 15;
            var b = bucket & 15;
            var home = clusters.Find(cluster =>
                Math.Abs(cluster.R - r) <= ClusterRadius && Math.Abs(cluster.G - g) <= ClusterRadius && Math.Abs(cluster.B - b) <= ClusterRadius);
            if (home == null)
            {
                home = new ColorCluster(bucket, r, g, b);
                clusters.Add(home);
            }

            home.Pixels += counts[bucket];
            home.SumR += sumR[bucket];
            home.SumG += sumG[bucket];
            home.SumB += sumB[bucket];
        }

        clusters.Sort((a, b) => a.Pixels != b.Pixels ? b.Pixels.CompareTo(a.Pixels) : a.Leader.CompareTo(b.Leader));

        var colors = new List<DominantColor>();
        foreach (var cluster in clusters.Take(count))
        {
            var color = new Rgba32(RoundedMean(cluster.SumR, cluster.Pixels), RoundedMean(cluster.SumG, cluster.Pixels), RoundedMean(cluster.SumB, cluster.Pixels), 255);
            var fraction = Math.Round(cluster.Pixels / (double)total, 6, MidpointRounding.AwayFromZero);
            colors.Add(new DominantColor(color, cluster.Pixels, fraction));
        }

        return colors;
    }

    /// <summary>
    ///     Calculates the luma statistics of a 256-bin histogram.
    /// </summary>
    /// <param name="histogram">The pixel count of each luma level.</param>
    /// <param name="total">The number of pixels.</param>
    /// <returns>The statistics.</returns>
    private static LumaStatistics ComputeLumaStatistics(long[] histogram, long total)
    {
        long sum = 0;
        var min = -1;
        var max = 0;
        var coarse = new long[16];
        for (var level = 0; level < LevelCount; level++)
        {
            if (histogram[level] > 0)
            {
                min = min < 0 ? level : min;
                max = level;
            }

            sum += level * histogram[level];
            coarse[level >> 4] += histogram[level];
        }

        var mean = Math.Round(sum / (double)total, 2, MidpointRounding.AwayFromZero);
        return new LumaStatistics(
            mean,
            Math.Max(min, 0),
            max,
            PercentileLevel(histogram, total, 1),
            PercentileLevel(histogram, total, 50),
            PercentileLevel(histogram, total, 99),
            OtsuThreshold(histogram),
            coarse);
    }

    /// <summary>
    ///     Finds a percentile level of a histogram.
    /// </summary>
    /// <param name="histogram">The pixel count of each luma level.</param>
    /// <param name="total">The number of pixels.</param>
    /// <param name="percent">The percentile, 1 to 99.</param>
    /// <returns>The smallest level whose cumulative count is at least <c>ceil(total * percent / 100)</c>.</returns>
    private static int PercentileLevel(long[] histogram, long total, int percent)
    {
        var needed = Math.Max(1, ((total * percent) + 99) / 100);
        long cumulative = 0;
        for (var level = 0; level < LevelCount; level++)
        {
            cumulative += histogram[level];
            if (cumulative >= needed)
            {
                return level;
            }
        }

        return LevelCount - 1;
    }

    /// <summary>
    ///     A cluster of color buckets while the dominant colors are being found.
    /// </summary>
    private sealed class ColorCluster
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ColorCluster"/> class.
        /// </summary>
        /// <param name="leader">The bucket that started the cluster.</param>
        /// <param name="r">The red bucket coordinate of the leader.</param>
        /// <param name="g">The green bucket coordinate of the leader.</param>
        /// <param name="b">The blue bucket coordinate of the leader.</param>
        public ColorCluster(int leader, int r, int g, int b)
        {
            Leader = leader;
            R = r;
            G = g;
            B = b;
        }

        /// <summary>Gets the bucket index of the leader.</summary>
        public int Leader { get; }

        /// <summary>Gets the red bucket coordinate of the leader.</summary>
        public int R { get; }

        /// <summary>Gets the green bucket coordinate of the leader.</summary>
        public int G { get; }

        /// <summary>Gets the blue bucket coordinate of the leader.</summary>
        public int B { get; }

        /// <summary>Gets or sets the number of pixels.</summary>
        public long Pixels { get; set; }

        /// <summary>Gets or sets the sum of the flattened red.</summary>
        public long SumR { get; set; }

        /// <summary>Gets or sets the sum of the flattened green.</summary>
        public long SumG { get; set; }

        /// <summary>Gets or sets the sum of the flattened blue.</summary>
        public long SumB { get; set; }
    }
}
