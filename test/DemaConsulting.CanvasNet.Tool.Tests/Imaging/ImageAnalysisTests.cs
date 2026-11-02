// cspell:ignore luma otsu
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageAnalysis class: dominant colors, luma statistics, and pixel samples.
/// </summary>
public class ImageAnalysisTests
{
    /// <summary>The opaque white background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Creates a surface one pixel high from a row of pixels.
    /// </summary>
    /// <param name="pixels">The pixels.</param>
    /// <returns>The surface.</returns>
    private static Surface Row(params Rgba32[] pixels)
    {
        var surface = new Surface(pixels.Length, 1);
        for (var x = 0; x < pixels.Length; x++)
        {
            surface[x, 0] = pixels[x];
        }

        return surface;
    }

    /// <summary>
    ///     Repeats a pixel.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="count">The number of copies.</param>
    /// <returns>The pixels.</returns>
    private static IEnumerable<Rgba32> Repeat(Rgba32 pixel, int count)
    {
        return Enumerable.Repeat(pixel, count);
    }

    /// <summary>
    ///     Creates a gray pixel.
    /// </summary>
    /// <param name="level">The level of all three channels.</param>
    /// <returns>The opaque pixel.</returns>
    private static Rgba32 Gray(int level)
    {
        return new Rgba32((byte)level, (byte)level, (byte)level, 255);
    }

    /// <summary>
    ///     Verifies that shades with the same top four bits per channel are counted as one color with the mean color.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_FourBitBuckets_GroupNearbyShadesIntoOneColor()
    {
        using var surface = Row(
            new Rgba32(16, 32, 48, 255),
            new Rgba32(16, 32, 48, 255),
            new Rgba32(16, 32, 48, 255),
            new Rgba32(31, 47, 63, 255));

        var statistics = ImageAnalysis.Analyze(surface, White, 8);

        Assert.Equal(4, statistics.TotalPixels);
        var color = Assert.Single(statistics.Colors);
        Assert.Equal(4, color.Pixels);
        Assert.Equal(1.0, color.Fraction);

        // Means 79 / 4 = 19.75 + rounding: (2 * 79 + 4) / 8 = 20, 143 / 4 = 35.75 -> 36, 207 / 4 = 51.75 -> 52.
        Assert.Equal(new Rgba32(20, 36, 52, 255), color.Color);
    }

    /// <summary>
    ///     Verifies the order and the fractions of two colors.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_TwoColorImage_ReportsBothInPixelOrderWithFractions()
    {
        var black = Gray(0);
        using var surface = Row(Repeat(black, 25).Concat(Repeat(White, 75)).ToArray());

        var statistics = ImageAnalysis.Analyze(surface, White, 8);

        Assert.Equal(2, statistics.Colors.Count);
        Assert.Equal(White, statistics.Colors[0].Color);
        Assert.Equal(75, statistics.Colors[0].Pixels);
        Assert.Equal(0.75, statistics.Colors[0].Fraction);
        Assert.Equal(black, statistics.Colors[1].Color);
        Assert.Equal(25, statistics.Colors[1].Pixels);
        Assert.Equal(0.25, statistics.Colors[1].Fraction);

        using var thirds = Row(Gray(0), Gray(255), Gray(255));
        Assert.Equal(0.666667, ImageAnalysis.Analyze(thirds, White, 8).Colors[0].Fraction);
    }

    /// <summary>
    ///     Verifies that colors with equal counts are ordered by their bucket index.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_EqualCounts_OrderedByBucketIndex()
    {
        var red = new Rgba32(255, 0, 0, 255);
        var green = new Rgba32(0, 255, 0, 255);
        var blue = new Rgba32(0, 0, 255, 255);
        using var surface = Row(red, green, blue, red, green, blue);

        var statistics = ImageAnalysis.Analyze(surface, White, 8);

        // Buckets: blue 0x00F, green 0x0F0, red 0xF00.
        Assert.Equal([blue, green, red], statistics.Colors.Select(c => c.Color).ToArray());
    }

    /// <summary>
    ///     Verifies that only the requested number of colors is reported, the most common first.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_CountLimit_ReportsOnlyTheMostCommon()
    {
        using var surface = Row(
            Repeat(Gray(0), 5)
                .Concat(Repeat(Gray(64), 4))
                .Concat(Repeat(Gray(128), 3))
                .Concat(Repeat(Gray(192), 2))
                .Concat(Repeat(Gray(255), 1))
                .ToArray());

        var two = ImageAnalysis.Analyze(surface, White, 2);
        var all = ImageAnalysis.Analyze(surface, White, 32);

        Assert.Equal(2, two.Colors.Count);
        Assert.Equal(Gray(0), two.Colors[0].Color);
        Assert.Equal(Gray(64), two.Colors[1].Color);
        Assert.Equal(5, all.Colors.Count);
        Assert.Equal(15, two.TotalPixels);
    }

    /// <summary>
    ///     Verifies that buckets near the leader of a cluster merge, with the mean of the members, and distant ones do not.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_Gradient_MergesWithinClusterRadiusAndAveragesMembers()
    {
        // Buckets 8, 9, and 10 are within 2 of the leader (bucket 8, the most common); bucket 13 is not.
        using var surface = Row(
            Repeat(Gray(128), 4)
                .Concat(Repeat(Gray(144), 2))
                .Concat(Repeat(Gray(160), 1))
                .Concat(Repeat(Gray(208), 1))
                .ToArray());

        var statistics = ImageAnalysis.Analyze(surface, White, 8);

        Assert.Equal(2, statistics.Colors.Count);

        // (128 * 4 + 144 * 2 + 160) / 7 = 137.14, which rounds to 137.
        Assert.Equal(Gray(137), statistics.Colors[0].Color);
        Assert.Equal(7, statistics.Colors[0].Pixels);
        Assert.Equal(0.875, statistics.Colors[0].Fraction);
        Assert.Equal(Gray(208), statistics.Colors[1].Color);
        Assert.Equal(0.125, statistics.Colors[1].Fraction);
    }

    /// <summary>
    ///     Verifies that translucent pixels are flattened over the background before they are counted.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_TransparentPixels_AreFlattenedOverBackground()
    {
        using var surface = Row(
            new Rgba32(255, 0, 0, 0),
            new Rgba32(255, 0, 0, 0),
            new Rgba32(255, 0, 0, 0),
            Gray(0));

        var overWhite = ImageAnalysis.Analyze(surface, White, 8);
        var overBlue = ImageAnalysis.Analyze(surface, new Rgba32(0, 0, 255, 255), 8);

        Assert.Equal(White, overWhite.Colors[0].Color);
        Assert.Equal(3, overWhite.Colors[0].Pixels);
        Assert.Equal(new Rgba32(0, 0, 255, 255), overBlue.Colors[0].Color);
        Assert.Equal(3, overBlue.Colors[0].Pixels);

        // Black at alpha 128 over white: (0 * 128 + 255 * 127 + 127) / 255 = 127.
        using var half = Row(new Rgba32(0, 0, 0, 128));
        Assert.Equal(Gray(127), ImageAnalysis.Analyze(half, White, 1).Colors[0].Color);
        Assert.Equal(127, ImageAnalysis.Analyze(half, White, 1).Luma.Min);
    }

    /// <summary>
    ///     Verifies the luma statistics of an image with every gray level once.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_Luma_ReportsMeanMinMaxPercentilesOtsuAndHistogram()
    {
        using var surface = new Surface(256, 1);
        for (var x = 0; x < 256; x++)
        {
            surface[x, 0] = Gray(x);
        }

        var luma = ImageAnalysis.Analyze(surface, White, 8).Luma;

        Assert.Equal(127.5, luma.Mean);
        Assert.Equal(0, luma.Min);
        Assert.Equal(255, luma.Max);

        // ceil(256 * 1 / 100) = 3 pixels -> level 2; ceil(256 * 50 / 100) = 128 -> level 127; ceil(256 * 99 / 100) = 254 -> level 253.
        Assert.Equal(2, luma.P1);
        Assert.Equal(127, luma.P50);
        Assert.Equal(253, luma.P99);
        Assert.Equal(127, luma.Otsu);
        Assert.Equal(Enumerable.Repeat(16L, 16).ToArray(), luma.Histogram.ToArray());

        using var flat = Row(Gray(200), Gray(200));
        var single = ImageAnalysis.Analyze(flat, White, 1).Luma;
        Assert.Equal(200, single.Mean);
        Assert.Equal(200, single.Min);
        Assert.Equal(200, single.Max);
        Assert.Equal(200, single.P1);
        Assert.Equal(127, single.Otsu);
        Assert.Equal(2, single.Histogram[12]);
    }

    /// <summary>
    ///     Verifies that a sample reports the pixel and its luma.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Sample_Point_ReturnsColorAndLuma()
    {
        using var surface = new Surface(5, 4);
        surface[2, 3] = new Rgba32(255, 0, 0, 255);
        surface[1, 1] = new Rgba32(0, 0, 0, 128);

        var sample = ImageAnalysis.Sample(surface, 2, 3, 0);
        var translucent = ImageAnalysis.Sample(surface, 1, 1, 0);

        Assert.Equal(2, sample.X);
        Assert.Equal(3, sample.Y);
        Assert.Equal(new Rgba32(255, 0, 0, 255), sample.Pixel);
        Assert.Equal(77, sample.Luma);
        Assert.Null(sample.Window);
        Assert.Equal(127, translucent.Luma);
    }

    /// <summary>
    ///     Verifies that a window is clipped to the image and that its mean includes alpha.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Sample_Radius_ReturnsClippedWindowAndMean()
    {
        using var surface = new Surface(6, 6);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                surface[x, y] = new Rgba32((byte)(x * 10), (byte)(y * 10), 0, (byte)(255 - x));
            }
        }

        var corner = ImageAnalysis.Sample(surface, 0, 0, 2);
        var inner = ImageAnalysis.Sample(surface, 3, 3, 1);

        var window = corner.Window!;
        Assert.Equal((0, 0, 3, 3), (window.X, window.Y, window.Width, window.Height));
        Assert.Equal(9, window.Pixels.Count);
        Assert.Equal(surface[1, 1], window.Pixels[4]);

        // Columns 0 to 2 of red are 0, 10, 20 (mean 10), rows 0 to 2 of green likewise, alpha 255, 254, 253 (mean 254).
        Assert.Equal(new Rgba32(10, 10, 0, 254), window.Mean);
        Assert.Equal((2, 2, 3, 3), (inner.Window!.X, inner.Window.Y, inner.Window.Width, inner.Window.Height));
        Assert.Equal(new Rgba32(30, 30, 0, 252), inner.Window.Mean);
    }

    /// <summary>
    ///     Verifies that a point outside the image is a usage error.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Sample_OutsideImage_Throws()
    {
        using var surface = new Surface(6, 4);

        foreach (var (x, y) in new[] { (6, 0), (0, 4), (-1, 0), (0, -1), (5000, 10) })
        {
            var exception = Assert.Throws<ToolException>(() => ImageAnalysis.Sample(surface, x, y, 0));
            Assert.Equal(ErrorCodes.UsageError, exception.Code);
            Assert.Contains("6x4", exception.Message);
        }
    }

    /// <summary>
    ///     Verifies that the same image gives the same statistics.
    /// </summary>
    [Fact]
    public void ImageAnalysis_Analyze_SameInputTwice_ReturnsIdenticalResult()
    {
        var random = new Random(99);
        using var surface = new Surface(64, 48);
        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                surface[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), (byte)(random.Next(3) == 0 ? random.Next(256) : 255));
            }
        }

        var first = ImageAnalysis.Analyze(surface, White, 32);
        var second = ImageAnalysis.Analyze(surface, White, 32);

        Assert.Equal(first.TotalPixels, second.TotalPixels);
        Assert.Equal(first.Colors, second.Colors);
        Assert.Equal(first.Luma with { Histogram = [] }, second.Luma with { Histogram = [] });
        Assert.Equal(first.Luma.Histogram, second.Luma.Histogram);
        Assert.Equal(64 * 48, first.Luma.Histogram.Sum());
    }

    /// <summary>
    ///     Verifies that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void ImageAnalysis_NullOrOutOfRangeArguments_Throw()
    {
        using var surface = new Surface(4, 4);

        Assert.Throws<ArgumentNullException>(() => ImageAnalysis.Analyze(null!, White, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageAnalysis.Analyze(surface, White, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageAnalysis.Analyze(surface, White, 33));
        Assert.Throws<ArgumentException>(() => ImageAnalysis.Analyze(surface, new Rgba32(255, 255, 255, 254), 8));
        Assert.Throws<ArgumentNullException>(() => ImageAnalysis.Sample(null!, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageAnalysis.Sample(surface, 0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageAnalysis.Sample(surface, 0, 0, 9));
    }
}
