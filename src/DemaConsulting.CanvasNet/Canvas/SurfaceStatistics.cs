// cspell:ignore luma otsu percentiles
namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     One dominant color of an image.
/// </summary>
/// <param name="Color">The opaque color: the mean of the flattened pixels of the cluster.</param>
/// <param name="Pixels">The number of pixels of the cluster.</param>
/// <param name="Fraction">The share of all pixels, <c>Pixels / total</c> rounded to 6 decimals.</param>
public sealed record DominantColor(Rgba32 Color, long Pixels, double Fraction);

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
public sealed record LumaStatistics(double Mean, int Min, int Max, int P1, int P50, int P99, int Otsu, IReadOnlyList<long> Histogram);

/// <summary>
///     The result of analyzing the colors of an image with <see cref="Surface.Analyze"/>.
/// </summary>
/// <param name="TotalPixels">The number of pixels analyzed.</param>
/// <param name="Colors">The dominant colors, the most common first.</param>
/// <param name="Luma">The luma statistics.</param>
public sealed record SurfaceStatistics(long TotalPixels, IReadOnlyList<DominantColor> Colors, LumaStatistics Luma)
{
    /// <summary>The smallest number of dominant colors that <see cref="Surface.Analyze"/> accepts.</summary>
    public const int MinColors = 1;

    /// <summary>The largest number of dominant colors that <see cref="Surface.Analyze"/> accepts.</summary>
    public const int MaxColors = 32;
}

/// <summary>
///     The window of pixels around a sampled point.
/// </summary>
/// <param name="X">The x position of the left edge of the window, clipped to the image.</param>
/// <param name="Y">The y position of the top edge of the window, clipped to the image.</param>
/// <param name="Width">The width of the window.</param>
/// <param name="Height">The height of the window.</param>
/// <param name="Mean">The mean of the window, each channel (alpha included) rounded.</param>
/// <param name="Pixels">The pixels of the window in row-major order.</param>
public sealed record PixelWindow(int X, int Y, int Width, int Height, Rgba32 Mean, IReadOnlyList<Rgba32> Pixels);

/// <summary>
///     A sample of the pixel at a point, from <see cref="Surface.Sample"/>.
/// </summary>
/// <param name="X">The x position of the point.</param>
/// <param name="Y">The y position of the point.</param>
/// <param name="Pixel">The pixel at the point.</param>
/// <param name="Luma">The <see cref="Surface.Luma"/> of the pixel.</param>
/// <param name="Window">The window around the point when a radius was asked for; otherwise <see langword="null"/>.</param>
public sealed record PixelSample(int X, int Y, Rgba32 Pixel, int Luma, PixelWindow? Window)
{
    /// <summary>The largest radius of a sampled window that <see cref="Surface.Sample"/> accepts.</summary>
    public const int MaxRadius = 8;
}
