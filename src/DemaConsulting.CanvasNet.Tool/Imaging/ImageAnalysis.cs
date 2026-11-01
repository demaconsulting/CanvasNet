// cspell:ignore luma otsu percentiles
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     One dominant color of an image.
/// </summary>
/// <param name="Color">The opaque color: the mean of the flattened pixels of the cluster.</param>
/// <param name="Pixels">The number of pixels of the cluster.</param>
/// <param name="Fraction">The share of all pixels, <c>Pixels / total</c> rounded to 6 decimals.</param>
internal sealed record DominantColor(Rgba32 Color, long Pixels, double Fraction);

/// <summary>
///     The statistics of the luma of an image.
/// </summary>
/// <param name="Mean">The mean luma, rounded to 2 decimals.</param>
/// <param name="Min">The smallest luma that occurs.</param>
/// <param name="Max">The largest luma that occurs.</param>
/// <param name="P1">The 1st percentile: the smallest level whose cumulative count is at least 1 percent of the pixels.</param>
/// <param name="P50">The 50th percentile (the median level).</param>
/// <param name="P99">The 99th percentile.</param>
/// <param name="Otsu">The threshold <see cref="Surface.OtsuThreshold"/> chooses for the histogram.</param>
/// <param name="Histogram">Sixteen counts: the number of pixels with the luma <c>level &gt;&gt; 4</c> equal to the index.</param>
internal sealed record LumaStatistics(double Mean, int Min, int Max, int P1, int P50, int P99, int Otsu, IReadOnlyList<long> Histogram);

/// <summary>
///     The result of analyzing the colors of an image.
/// </summary>
/// <param name="TotalPixels">The number of pixels analyzed.</param>
/// <param name="Colors">The dominant colors, the most common first.</param>
/// <param name="Luma">The luma statistics.</param>
internal sealed record ImageStatistics(long TotalPixels, IReadOnlyList<DominantColor> Colors, LumaStatistics Luma);

/// <summary>
///     The window of pixels around a sampled point.
/// </summary>
/// <param name="X">The x position of the left edge of the window, clipped to the image.</param>
/// <param name="Y">The y position of the top edge of the window, clipped to the image.</param>
/// <param name="Width">The width of the window.</param>
/// <param name="Height">The height of the window.</param>
/// <param name="Mean">The mean of the window, each channel (alpha included) rounded.</param>
/// <param name="Pixels">The pixels of the window in row-major order.</param>
internal sealed record PixelWindow(int X, int Y, int Width, int Height, Rgba32 Mean, IReadOnlyList<Rgba32> Pixels);

/// <summary>
///     A sample of the pixel at a point.
/// </summary>
/// <param name="X">The x position of the point.</param>
/// <param name="Y">The y position of the point.</param>
/// <param name="Pixel">The pixel at the point.</param>
/// <param name="Luma">The <see cref="Surface.Luma"/> of the pixel.</param>
/// <param name="Window">The window around the point when a radius was asked for; otherwise <see langword="null"/>.</param>
internal sealed record PixelSample(int X, int Y, Rgba32 Pixel, int Luma, PixelWindow? Window);

/// <summary>
///     Reads what an image contains: its dominant colors, its luma statistics, and the pixels around a point.
/// </summary>
/// <remarks>
///     <para>
///         The analysis works on surfaces only and never changes them. Colors are counted in 4096 buckets of 4 bits per
///         channel and clustered greedily so that a gradient of paper shades is one color; the luma statistics come from the
///         same single pass. All arithmetic is exact integer arithmetic (the only floating point is the rounding of the
///         reported fractions and mean), the pass is sequential, and nothing allocates per pixel, so the result is the same
///         on every run.
///     </para>
/// </remarks>
internal static class ImageAnalysis
{
    /// <summary>The smallest number of dominant colors that may be asked for.</summary>
    public const int MinColors = 1;

    /// <summary>The largest number of dominant colors that may be asked for.</summary>
    public const int MaxColors = 32;

    /// <summary>The default number of dominant colors.</summary>
    public const int DefaultColors = 8;

    /// <summary>The largest radius of a sampled window.</summary>
    public const int MaxRadius = 8;

    /// <summary>The largest distance, in bucket units on any channel, at which a bucket joins an existing cluster.</summary>
    public const int ClusterRadius = 2;

    /// <summary>The number of color buckets: 16 levels per channel.</summary>
    private const int Buckets = 4096;

    /// <summary>The number of luma levels.</summary>
    private const int Levels = 256;

    /// <summary>
    ///     Finds the dominant colors and the luma statistics of an image.
    /// </summary>
    /// <param name="surface">The surface; it is not modified.</param>
    /// <param name="background">The opaque color a translucent pixel is flattened over.</param>
    /// <param name="count">The number of dominant colors to report, <see cref="MinColors"/> to <see cref="MaxColors"/>.</param>
    /// <returns>The statistics.</returns>
    /// <remarks>
    ///     <para>
    ///         Each pixel is flattened over <paramref name="background"/> (<c>(c * a + bg * (255 - a) + 127) / 255</c>; an
    ///         opaque pixel is unchanged). Its bucket is <c>((R &gt;&gt; 4) &lt;&lt; 8) | ((G &gt;&gt; 4) &lt;&lt; 4) | (B &gt;&gt; 4)</c> of
    ///         the flattened color; the buckets count their pixels and sum their flattened channels. The occupied buckets are
    ///         ordered by count (largest first, ties by bucket index) and clustered greedily in that order: a bucket joins the
    ///         first cluster, in creation order, whose leader bucket is within <see cref="ClusterRadius"/> bucket units on
    ///         every channel, otherwise it leads a new cluster. A cluster has the pixels of its buckets and the mean color of
    ///         those pixels (rounded half up). The clusters are ordered by pixels (largest first, ties by the leader bucket)
    ///         and the first <paramref name="count"/> are reported.
    ///     </para>
    ///     <para>
    ///         The luma statistics use the luma <c>(77 R + 150 G + 29 B + 128) &gt;&gt; 8</c> of the flattened pixel: the mean
    ///         (rounded to 2 decimals), the smallest and largest level that occur, the percentiles 1, 50, and 99 (the smallest
    ///         level whose cumulative count is at least <c>ceil(N * p / 100)</c>), the Otsu threshold of the 256-bin
    ///         histogram, and a 16-bin histogram of <c>level &gt;&gt; 4</c>.
    ///     </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="background"/> is not opaque.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is out of range.</exception>
    public static ImageStatistics Analyze(Surface surface, Rgba32 background, int count)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, MinColors);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxColors);
        if (background.A != 255)
        {
            throw new ArgumentException("The background must be opaque.", nameof(background));
        }

        var counts = new long[Buckets];
        var sumR = new long[Buckets];
        var sumG = new long[Buckets];
        var sumB = new long[Buckets];
        var histogram = new long[Levels];
        long total = 0;

        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var source in surface.GetRowSpan(y))
            {
                var r = Flatten(source.R, background.R, source.A);
                var g = Flatten(source.G, background.G, source.A);
                var b = Flatten(source.B, background.B, source.A);
                var bucket = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
                counts[bucket]++;
                sumR[bucket] += r;
                sumG[bucket] += g;
                sumB[bucket] += b;
                histogram[((77 * r) + (150 * g) + (29 * b) + 128) >> 8]++;
                total++;
            }
        }

        var colors = Cluster(counts, sumR, sumG, sumB, total, count);
        return new ImageStatistics(total, colors, Statistics(histogram, total));
    }

    /// <summary>
    ///     Reads the pixel at a point and, optionally, the window of pixels around it.
    /// </summary>
    /// <param name="surface">The surface; it is not modified.</param>
    /// <param name="x">The x position of the point.</param>
    /// <param name="y">The y position of the point.</param>
    /// <param name="radius">The radius of the window, 0 (no window) to <see cref="MaxRadius"/>.</param>
    /// <returns>
    ///     The pixel, its luma, and, when the radius is above 0, the window <c>[x - radius, x + radius] x [y - radius, y + radius]</c>
    ///     clipped to the image with its actual rectangle, all of its pixels, and the mean of each channel (alpha included),
    ///     rounded half up.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is out of range.</exception>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the point is outside the image.</exception>
    public static PixelSample Sample(Surface surface, int x, int y, int radius)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(radius, MaxRadius);

        if (x < 0 || y < 0 || x >= surface.Width || y >= surface.Height)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The point {x},{y} is outside the {surface.Width}x{surface.Height} image.",
                $"Use a point from 0,0 to {surface.Width - 1},{surface.Height - 1}.");
        }

        var pixel = surface[x, y];
        if (radius == 0)
        {
            return new PixelSample(x, y, pixel, Surface.Luma(pixel), null);
        }

        var left = Math.Max(0, x - radius);
        var top = Math.Max(0, y - radius);
        var right = Math.Min(surface.Width - 1, x + radius);
        var bottom = Math.Min(surface.Height - 1, y + radius);
        var width = right - left + 1;
        var height = bottom - top + 1;
        var pixels = new Rgba32[width * height];
        long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
        var index = 0;
        for (var row = top; row <= bottom; row++)
        {
            var span = surface.GetRowSpan(row);
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
        var mean = new Rgba32(Mean(sumR, n), Mean(sumG, n), Mean(sumB, n), Mean(sumA, n));
        return new PixelSample(x, y, pixel, Surface.Luma(pixel), new PixelWindow(left, top, width, height, mean, pixels));
    }

    /// <summary>
    ///     Flattens one channel of a pixel over the background.
    /// </summary>
    /// <param name="channel">The channel of the pixel.</param>
    /// <param name="background">The same channel of the background.</param>
    /// <param name="alpha">The alpha of the pixel.</param>
    /// <returns>The channel of the flattened pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Flatten(int channel, int background, int alpha)
    {
        return alpha == 255 ? channel : ((channel * alpha) + (background * (255 - alpha)) + 127) / 255;
    }

    /// <summary>
    ///     Calculates the rounded mean of a sum.
    /// </summary>
    /// <param name="sum">The sum.</param>
    /// <param name="n">The number of terms.</param>
    /// <returns>The mean, rounded half up, from 0 to 255.</returns>
    private static byte Mean(long sum, long n)
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
    private static List<DominantColor> Cluster(long[] counts, long[] sumR, long[] sumG, long[] sumB, long total, int count)
    {
        var occupied = new List<int>();
        for (var bucket = 0; bucket < Buckets; bucket++)
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
            var color = new Rgba32(Mean(cluster.SumR, cluster.Pixels), Mean(cluster.SumG, cluster.Pixels), Mean(cluster.SumB, cluster.Pixels), 255);
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
    private static LumaStatistics Statistics(long[] histogram, long total)
    {
        long sum = 0;
        var min = -1;
        var max = 0;
        var coarse = new long[16];
        for (var level = 0; level < Levels; level++)
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
            Percentile(histogram, total, 1),
            Percentile(histogram, total, 50),
            Percentile(histogram, total, 99),
            Surface.OtsuThreshold(histogram),
            coarse);
    }

    /// <summary>
    ///     Finds a percentile level of a histogram.
    /// </summary>
    /// <param name="histogram">The pixel count of each luma level.</param>
    /// <param name="total">The number of pixels.</param>
    /// <param name="percent">The percentile, 1 to 99.</param>
    /// <returns>The smallest level whose cumulative count is at least <c>ceil(total * percent / 100)</c>.</returns>
    private static int Percentile(long[] histogram, long total, int percent)
    {
        var needed = Math.Max(1, ((total * percent) + 99) / 100);
        long cumulative = 0;
        for (var level = 0; level < Levels; level++)
        {
            cumulative += histogram[level];
            if (cumulative >= needed)
            {
                return level;
            }
        }

        return Levels - 1;
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
