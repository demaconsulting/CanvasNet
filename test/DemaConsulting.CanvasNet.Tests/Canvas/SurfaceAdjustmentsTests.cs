// cspell:ignore luma otsu grayscale gamma midtone midtones
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for the tone and color adjustments of the Surface class: luma, grayscale, invert, thresholds, levels,
///     brightness, contrast, and auto-levels.
/// </summary>
public class SurfaceAdjustmentsTests
{
    /// <summary>
    ///     Creates a surface from a row of pixels.
    /// </summary>
    /// <param name="pixels">The pixels of the single row.</param>
    /// <returns>The surface, as wide as the number of pixels and one pixel high.</returns>
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
    ///     Creates an opaque surface 256 pixels wide whose pixel <c>x</c> is the gray level <c>x</c>.
    /// </summary>
    /// <returns>The surface.</returns>
    private static Surface GrayRamp()
    {
        var surface = new Surface(256, 1);
        for (var x = 0; x < 256; x++)
        {
            surface[x, 0] = new Rgba32((byte)x, (byte)x, (byte)x, 255);
        }

        return surface;
    }

    /// <summary>
    ///     Creates a surface of pseudo-random pixels, alpha included, from a fixed seed.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The surface.</returns>
    private static Surface RandomSurface(int width, int height)
    {
        var random = new Random(12345);
        var surface = new Surface(width, height);
        var bytes = new byte[4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                random.NextBytes(bytes);
                surface[x, y] = new Rgba32(bytes[0], bytes[1], bytes[2], random.Next(4) == 0 ? bytes[3] : (byte)255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Creates a histogram with the given counts at the given levels.
    /// </summary>
    /// <param name="counts">The pairs of level and count.</param>
    /// <returns>The 256-entry histogram.</returns>
    private static long[] Histogram(params (int Level, long Count)[] counts)
    {
        var histogram = new long[256];
        foreach (var (level, count) in counts)
        {
            histogram[level] = count;
        }

        return histogram;
    }

    /// <summary>
    ///     Verifies that the luma uses the fixed-point weights and flattens translucent pixels over white.
    /// </summary>
    [Fact]
    public void Surface_Luma_OpaqueAndTranslucentPixels_UsesFixedPointWeightsOverWhite()
    {
        Assert.Equal(0, Surface.Luma(new Rgba32(0, 0, 0, 255)));
        Assert.Equal(255, Surface.Luma(new Rgba32(255, 255, 255, 255)));
        Assert.Equal(77, Surface.Luma(new Rgba32(255, 0, 0, 255)));
        Assert.Equal(149, Surface.Luma(new Rgba32(0, 255, 0, 255)));
        Assert.Equal(29, Surface.Luma(new Rgba32(0, 0, 255, 255)));

        // Over white: (Y * A + 255 * (255 - A) + 127) / 255.
        Assert.Equal(127, Surface.Luma(new Rgba32(0, 0, 0, 128)));
        Assert.Equal(166, Surface.Luma(new Rgba32(255, 0, 0, 128)));
        Assert.Equal(255, Surface.Luma(new Rgba32(0, 0, 0, 0)));
        Assert.Equal(255, Surface.Luma(new Rgba32(10, 20, 30, 0)));
    }

    /// <summary>
    ///     Verifies that grayscale makes the channels equal to the luma of the color and keeps alpha.
    /// </summary>
    [Fact]
    public void Surface_Grayscale_ColorPixels_SetsEqualChannelsAndKeepsAlpha()
    {
        using var surface = Row(new Rgba32(255, 0, 0, 100), new Rgba32(0, 255, 0, 255), new Rgba32(0, 0, 255, 0));

        surface.Grayscale();

        Assert.Equal(new Rgba32(77, 77, 77, 100), surface[0, 0]);
        Assert.Equal(new Rgba32(149, 149, 149, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(29, 29, 29, 0), surface[2, 0]);
    }

    /// <summary>
    ///     Verifies that grayscale leaves gray pixels as they are.
    /// </summary>
    [Fact]
    public void Surface_Grayscale_AlreadyGray_IsUnchanged()
    {
        using var surface = GrayRamp();
        using var expected = surface.Clone();

        surface.Grayscale();

        SurfaceAssert.Equal(expected, surface);
    }

    /// <summary>
    ///     Verifies that invert complements the color channels and keeps alpha.
    /// </summary>
    [Fact]
    public void Surface_Invert_Pixels_ComplementsColorAndKeepsAlpha()
    {
        using var surface = Row(new Rgba32(0, 100, 255, 77), new Rgba32(255, 255, 255, 0));

        surface.Invert();

        Assert.Equal(new Rgba32(255, 155, 0, 77), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[1, 0]);
    }

    /// <summary>
    ///     Verifies that inverting twice restores the original pixels.
    /// </summary>
    [Fact]
    public void Surface_Invert_Twice_RestoresOriginal()
    {
        using var surface = RandomSurface(40, 30);
        using var expected = surface.Clone();

        surface.Invert();
        surface.Invert();

        SurfaceAssert.Equal(expected, surface);
    }

    /// <summary>
    ///     Verifies that a fixed threshold makes a pixel white only when its luma is above the level.
    /// </summary>
    [Fact]
    public void Surface_Threshold_FixedLevel_WhiteOnlyAboveLevel()
    {
        using var surface = GrayRamp();

        surface.Threshold(100);

        for (var x = 0; x < 256; x++)
        {
            var expected = x > 100 ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
            Assert.Equal(expected, surface[x, 0]);
        }

        using var all = GrayRamp();
        all.Threshold(255);
        for (var x = 0; x < 256; x++)
        {
            Assert.Equal(new Rgba32(0, 0, 0, 255), all[x, 0]);
        }

        using var nearlyAll = GrayRamp();
        nearlyAll.Threshold(0);
        Assert.Equal(new Rgba32(0, 0, 0, 255), nearlyAll[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), nearlyAll[1, 0]);
    }

    /// <summary>
    ///     Verifies that a translucent pixel is judged by its luma over white and the result is opaque.
    /// </summary>
    [Fact]
    public void Surface_Threshold_TranslucentPixel_UsesLumaOverWhiteAndIsOpaque()
    {
        // Black at alpha 128 has the luma 127 over white.
        using var low = Row(new Rgba32(0, 0, 0, 128));
        low.Threshold(100);
        Assert.Equal(new Rgba32(255, 255, 255, 255), low[0, 0]);

        using var high = Row(new Rgba32(0, 0, 0, 128));
        high.Threshold(127);
        Assert.Equal(new Rgba32(0, 0, 0, 255), high[0, 0]);

        using var clear = Row(new Rgba32(0, 0, 0, 0));
        clear.Threshold(254);
        Assert.Equal(new Rgba32(255, 255, 255, 255), clear[0, 0]);
    }

    /// <summary>
    ///     Verifies that Otsu's method puts the threshold in the middle of the gap between two levels.
    /// </summary>
    [Fact]
    public void Surface_OtsuThreshold_TwoLevels_ReturnsMiddleOfGap()
    {
        var histogram = Histogram((20, 100), (200, 100));

        // Every t from 20 to 199 ties, so the result is (20 + 199) / 2.
        Assert.Equal(109, Surface.OtsuThreshold(histogram));
    }

    /// <summary>
    ///     Verifies that Otsu's method returns 127 when every pixel has the same luma or there are no pixels.
    /// </summary>
    [Fact]
    public void Surface_OtsuThreshold_SingleLevel_ReturnsFallback127()
    {
        Assert.Equal(127, Surface.OtsuThreshold(Histogram((77, 1000))));
        Assert.Equal(127, Surface.OtsuThreshold(new long[256]));
        Assert.Equal(127, Surface.OtsuThreshold(Histogram((255, 5))));
    }

    /// <summary>
    ///     Verifies that Otsu's method splits classes of very different sizes between the modes.
    /// </summary>
    [Fact]
    public void Surface_OtsuThreshold_UnevenClasses_SeparatesModes()
    {
        // The best split is between the two dark levels and the bright one: t = 40 to 219, so T = (40 + 219) / 2.
        var histogram = Histogram((30, 900), (40, 100), (220, 50));

        Assert.Equal(129, Surface.OtsuThreshold(histogram));
    }

    /// <summary>
    ///     Verifies that a levels table maps the endpoints and the midtone and that gamma moves the midtone.
    /// </summary>
    [Fact]
    public void Surface_BuildLevelsTable_BlackWhiteGamma_MapsEndpointsAndMidtone()
    {
        var linear = Surface.BuildLevelsTable(50, 200, 1.0);

        Assert.Equal(256, linear.Length);
        Assert.Equal(0, linear[0]);
        Assert.Equal(0, linear[50]);
        Assert.Equal(128, linear[125]);
        Assert.Equal(255, linear[200]);
        Assert.Equal(255, linear[255]);

        // Gamma 2 brightens the midtones: 255 * sqrt(0.5) = 180.3.
        var bright = Surface.BuildLevelsTable(50, 200, 2.0);
        Assert.Equal(180, bright[125]);
        Assert.Equal(0, bright[50]);
        Assert.Equal(255, bright[200]);

        // Gamma 0.5 darkens them: 255 * 0.25 = 63.75.
        var dark = Surface.BuildLevelsTable(50, 200, 0.5);
        Assert.Equal(64, dark[125]);

        Assert.Equal(1, Surface.BuildLevelsTable(0, 255, 1.0)[1]);
        Assert.Equal(255, Surface.BuildLevelsTable(254, 255, 1.0)[255]);
    }

    /// <summary>
    ///     Verifies that a levels table rejects arguments outside their ranges and a black level that is not below white.
    /// </summary>
    [Fact]
    public void Surface_BuildLevelsTable_InvalidRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.BuildLevelsTable(-1, 100, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.BuildLevelsTable(255, 255, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.BuildLevelsTable(0, 0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.BuildLevelsTable(0, 256, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.BuildLevelsTable(0, 255, 0.05));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.BuildLevelsTable(0, 255, 10.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.BuildLevelsTable(0, 255, double.NaN));
        Assert.Throws<ArgumentException>(() => Surface.BuildLevelsTable(100, 100, 1.0));
        Assert.Throws<ArgumentException>(() => Surface.BuildLevelsTable(200, 100, 1.0));
    }

    /// <summary>
    ///     Verifies that the brightness table adds a value and clamps.
    /// </summary>
    [Fact]
    public void Surface_BuildBrightnessTable_ShiftsAndClamps()
    {
        var up = Surface.BuildBrightnessTable(50);
        Assert.Equal(50, up[0]);
        Assert.Equal(150, up[100]);
        Assert.Equal(255, up[205]);
        Assert.Equal(255, up[255]);

        var down = Surface.BuildBrightnessTable(-50);
        Assert.Equal(0, down[0]);
        Assert.Equal(0, down[50]);
        Assert.Equal(50, down[100]);
        Assert.Equal(205, down[255]);

        Assert.Equal(255, Surface.BuildBrightnessTable(255)[1]);
        Assert.Equal(0, Surface.BuildBrightnessTable(-255)[254]);
    }

    /// <summary>
    ///     Verifies that the contrast table pivots at mid-gray, spreads levels for a positive value, and is the identity at 0.
    /// </summary>
    [Fact]
    public void Surface_BuildContrastTable_PivotsAtMidGray()
    {
        var identity = Surface.BuildContrastTable(0);
        for (var v = 0; v < 256; v++)
        {
            Assert.Equal(v, identity[v]);
        }

        var more = Surface.BuildContrastTable(50);
        Assert.Equal(128, more[128]);
        Assert.True(more[100] < 100);
        Assert.True(more[160] > 160);
        Assert.Equal(158, more[138]);

        var less = Surface.BuildContrastTable(-50);
        Assert.Equal(128, less[128]);
        Assert.True(less[100] > 100);
        Assert.True(less[160] < 160);

        // At 100 the factor is 129.5, so one level either side of the pivot saturates.
        var extreme = Surface.BuildContrastTable(100);
        Assert.Equal(0, extreme[127]);
        Assert.Equal(255, extreme[129]);
    }

    /// <summary>
    ///     Verifies that a table maps red, green, and blue and leaves alpha alone.
    /// </summary>
    [Fact]
    public void Surface_ApplyTable_Pixels_MapsRgbAndKeepsAlpha()
    {
        using var surface = Row(new Rgba32(10, 20, 30, 77), new Rgba32(250, 0, 128, 0));

        surface.ApplyTable(Surface.BuildBrightnessTable(10));

        Assert.Equal(new Rgba32(20, 30, 40, 77), surface[0, 0]);
        Assert.Equal(new Rgba32(255, 10, 138, 0), surface[1, 0]);
    }

    /// <summary>
    ///     Verifies that the public levels, brightness, and contrast adjustments apply their tables to red, green, and blue.
    /// </summary>
    [Fact]
    public void Surface_LevelsBrightnessContrast_Pixels_ApplyTheirTablesAndKeepAlpha()
    {
        // Levels 50..200 with gamma 1 maps 125 to 128 and clips the ends
        using var levels = Row(new Rgba32(125, 50, 255, 77));
        levels.Levels(50, 200);
        Assert.Equal(new Rgba32(128, 0, 255, 77), levels[0, 0]);

        // Levels with a gamma of 2 maps 125 to 180
        using var gamma = Row(new Rgba32(125, 125, 125, 255));
        gamma.Levels(50, 200, 2.0);
        Assert.Equal(new Rgba32(180, 180, 180, 255), gamma[0, 0]);

        // Brightness adds and clamps
        using var brightness = Row(new Rgba32(10, 250, 128, 5));
        brightness.Brightness(10);
        Assert.Equal(new Rgba32(20, 255, 138, 5), brightness[0, 0]);

        // Contrast of 50 keeps mid-gray and maps 138 to 158
        using var contrast = Row(new Rgba32(128, 138, 138, 9));
        contrast.Contrast(50);
        Assert.Equal(new Rgba32(128, 158, 158, 9), contrast[0, 0]);
    }

    /// <summary>
    ///     Verifies that the auto-levels range cuts the clip percentage from each end of the histogram.
    /// </summary>
    [Fact]
    public void Surface_AutoLevelsRange_ClipPercent_PicksPercentilePoints()
    {
        // 1000 pixels: 5 black, 495 at 50, 495 at 150, 5 white.
        var histogram = Histogram((0, 5), (50, 495), (150, 495), (255, 5));

        // 1 percent: k = 10, so the five extreme pixels at each end are ignored.
        Assert.Equal((50, 150), Surface.AutoLevelsRange(histogram, 1.0));

        // 0.5 percent: k = 5, which the extreme levels alone satisfy.
        Assert.Equal((0, 255), Surface.AutoLevelsRange(histogram, 0.5));

        // No clip: k = 1.
        Assert.Equal((0, 255), Surface.AutoLevelsRange(histogram, 0));

        // A large clip still leaves two distinct points here.
        Assert.Equal((50, 150), Surface.AutoLevelsRange(histogram, 20));
    }

    /// <summary>
    ///     Verifies that a flat image, or one without pixels, has no auto-levels range.
    /// </summary>
    [Fact]
    public void Surface_AutoLevelsRange_FlatImage_ReturnsNull()
    {
        Assert.Null(Surface.AutoLevelsRange(Histogram((77, 100)), 1.0));
        Assert.Null(Surface.AutoLevelsRange(new long[256], 1.0));

        using var flat = Row(new Rgba32(90, 90, 90, 255), new Rgba32(90, 90, 90, 255));
        using var expected = flat.Clone();
        Assert.Null(flat.AutoLevels(1.0));
        SurfaceAssert.Equal(expected, flat);
    }

    /// <summary>
    ///     Verifies that auto-levels stretches a low-contrast image to the full range.
    /// </summary>
    [Fact]
    public void Surface_AutoLevels_LowContrastImage_StretchesToFullRange()
    {
        // Gray levels 100, 103, ... 145, sixteen pixels each.
        using var surface = new Surface(16, 16);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var level = (byte)(100 + (x * 3));
                surface[x, y] = new Rgba32(level, level, level, 255);
            }
        }

        var points = surface.AutoLevels(1.0);

        Assert.Equal((100, 145), points);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[0, 5]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[15, 5]);
        Assert.True(surface[8, 5].R is > 100 and < 160);
    }

    /// <summary>
    ///     Verifies that running the same operations on the same pixels twice gives identical pixels.
    /// </summary>
    [Fact]
    public void Surface_Adjustments_SameInputTwice_ProduceIdenticalPixels()
    {
        using var source = RandomSurface(150, 130);
        using var first = source.Clone();
        using var second = source.Clone();

        foreach (var surface in new[] { first, second })
        {
            surface.AutoLevels();
            surface.Contrast(30);
            surface.Grayscale();
            surface.Threshold(Surface.OtsuThreshold(surface.LumaHistogram()));
        }

        SurfaceAssert.Equal(first, second);
    }

    /// <summary>
    ///     Verifies that the block-parallel histogram equals a sequential count, including a partial last block.
    /// </summary>
    [Fact]
    public void Surface_LumaHistogram_LargeImage_MatchesSequentialReference()
    {
        // 200 rows are three full blocks of 64 rows and a partial block of 8.
        using var surface = RandomSurface(300, 200);
        var expected = new long[256];
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                expected[Surface.Luma(surface[x, y])]++;
            }
        }

        var actual = surface.LumaHistogram();

        Assert.Equal(expected, actual);
        Assert.Equal(300L * 200, actual.Sum());
    }

    /// <summary>
    ///     Verifies that the parallel table application equals mapping each pixel one by one.
    /// </summary>
    [Fact]
    public void Surface_ApplyTable_LargeImage_MatchesPerPixelReference()
    {
        using var surface = RandomSurface(300, 200);
        using var expected = surface.Clone();
        var table = Surface.BuildLevelsTable(30, 220, 1.4);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var p = expected[x, y];
                expected[x, y] = new Rgba32(table[p.R], table[p.G], table[p.B], p.A);
            }
        }

        surface.Levels(30, 220, 1.4);

        SurfaceAssert.Equal(expected, surface);
    }

    /// <summary>
    ///     Verifies that out-of-range arguments are rejected.
    /// </summary>
    [Fact]
    public void Surface_Adjustments_OutOfRangeArguments_Throw()
    {
        using var surface = Row(new Rgba32(1, 2, 3, 4));

        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Threshold(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Threshold(256));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Levels(-1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Levels(0, 255, 10.5));
        Assert.Throws<ArgumentException>(() => surface.Levels(100, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Brightness(256));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Brightness(-256));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Contrast(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Contrast(-100.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Contrast(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.AutoLevels(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.AutoLevels(20.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Surface.AutoLevelsRange(new long[256], double.NaN));

        Assert.Throws<ArgumentException>(() => surface.ApplyTable(new byte[255]));
        Assert.Throws<ArgumentException>(() => Surface.OtsuThreshold(new long[255]));
        Assert.Throws<ArgumentException>(() => Surface.AutoLevelsRange(new long[257], 1.0));
        Assert.Throws<ArgumentException>(() => Surface.OtsuThreshold(Histogram((3, -1))));
        Assert.Throws<ArgumentException>(() => Surface.AutoLevelsRange(Histogram((3, -1)), 1.0));
    }

    /// <summary>
    ///     Verifies that every adjustment of a disposed surface throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public void Surface_Adjustments_AfterDispose_ThrowObjectDisposedException()
    {
        var surface = new Surface(2, 2);
        surface.Dispose();

        Assert.Throws<ObjectDisposedException>(() => surface.Grayscale());
        Assert.Throws<ObjectDisposedException>(() => surface.Invert());
        Assert.Throws<ObjectDisposedException>(() => surface.Threshold(5));
        Assert.Throws<ObjectDisposedException>(() => surface.Levels(0, 255));
        Assert.Throws<ObjectDisposedException>(() => surface.Brightness(5));
        Assert.Throws<ObjectDisposedException>(() => surface.Contrast(5));
        Assert.Throws<ObjectDisposedException>(() => surface.AutoLevels());
        Assert.Throws<ObjectDisposedException>(() => surface.LumaHistogram());
    }
}
