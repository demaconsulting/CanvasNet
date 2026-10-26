// cspell:ignore psnr rmse ssim luma
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageComparison class: metrics, structural similarity, the grid, thresholds, and the pictures.
/// </summary>
public class ImageComparisonTests
{
    /// <summary>
    ///     The stabilizing constant of the luminance term of SSIM.
    /// </summary>
    private const double C1 = 6.5025;

    /// <summary>
    ///     The stabilizing constant of the contrast term of SSIM.
    /// </summary>
    private const double C2 = 58.5225;

    /// <summary>
    ///     Gets the metric names, as theory data.
    /// </summary>
    public static TheoryData<string> MetricNames => ["mae", "rmse", "psnr", "ssim", "diff-ratio", "max-diff", "cell-rmse"];

    /// <summary>
    ///     Creates an opaque pixel.
    /// </summary>
    /// <param name="r">The red channel.</param>
    /// <param name="g">The green channel.</param>
    /// <param name="b">The blue channel.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Px(int r, int g, int b)
    {
        return new Rgba32((byte)r, (byte)g, (byte)b, 255);
    }

    /// <summary>
    ///     Creates a surface of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The surface.</returns>
    private static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <summary>
    ///     Creates a surface from the pixels of one row.
    /// </summary>
    /// <param name="pixels">The pixels, left to right.</param>
    /// <returns>A surface one pixel high.</returns>
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
    ///     Computes the integer luma of a pixel, as the unit defines it.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns>The luma.</returns>
    private static int Luma(Rgba32 pixel)
    {
        return ((77 * pixel.R) + (150 * pixel.G) + (29 * pixel.B) + 128) >> 8;
    }

    /// <summary>
    ///     Computes the SSIM of one window by the textbook formula in floating point.
    /// </summary>
    /// <param name="a">The first image.</param>
    /// <param name="b">The second image.</param>
    /// <param name="left">The left edge of the window.</param>
    /// <param name="top">The top edge of the window.</param>
    /// <param name="width">The width of the window.</param>
    /// <param name="height">The height of the window.</param>
    /// <returns>The SSIM of the window.</returns>
    private static double ReferenceWindow(Surface a, Surface b, int left, int top, int width, int height)
    {
        var n = width * height;
        double meanA = 0;
        double meanB = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                meanA += Luma(a[x, y]);
                meanB += Luma(b[x, y]);
            }
        }

        meanA /= n;
        meanB /= n;
        double varA = 0;
        double varB = 0;
        double covariance = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var da = Luma(a[x, y]) - meanA;
                var db = Luma(b[x, y]) - meanB;
                varA += da * da;
                varB += db * db;
                covariance += da * db;
            }
        }

        varA /= n;
        varB /= n;
        covariance /= n;
        return ((2 * meanA * meanB) + C1) * ((2 * covariance) + C2) / (((meanA * meanA) + (meanB * meanB) + C1) * (varA + varB + C2));
    }

    /// <summary>
    ///     Creates a result whose measures are known, for the threshold tests.
    /// </summary>
    /// <returns>A result with a MAE of 5, RMSE of 7, PSNR of 30, SSIM of 0.8, differing ratio of 0.25, maximum difference of 40, and worst cell RMSE of 9.</returns>
    private static ComparisonResult KnownResult()
    {
        var metrics = new ComparisonMetrics(100, false, 5, 7, 30, 0.8, 25, 0.25, 40);
        GridCell[] cells = [new(0, 0, 0, 0, 5, 10, 1, 2, 0.1), new(1, 0, 5, 0, 5, 10, 8, 9, 0.4)];
        return new ComparisonResult(10, 10, 8, metrics, 2, 1, cells);
    }

    /// <summary>
    ///     Test that identical images have no error, are identical, have an infinite PSNR and an SSIM of one.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_IdenticalImages_ReportsZeroErrorIdenticalAndInfinitePsnr()
    {
        // Arrange: two separate copies of the same image
        using var a = FixtureFactory.CreateGradient();
        using var b = FixtureFactory.CreateGradient();

        // Act
        var result = ImageComparison.Compare(a, b, 8, 4, 4);

        // Assert: every measure says identical
        Assert.Equal((12, 8, 8), (result.Width, result.Height, result.Tolerance));
        Assert.Equal(96, result.Metrics.PixelCount);
        Assert.True(result.Metrics.Identical);
        Assert.Equal(0, result.Metrics.Mae);
        Assert.Equal(0, result.Metrics.Rmse);
        Assert.Null(result.Metrics.PsnrDb);
        Assert.Equal(1, result.Metrics.Ssim);
        Assert.Equal(0, result.Metrics.DifferingPixels);
        Assert.Equal(0, result.Metrics.DifferingPixelRatio);
        Assert.Equal(0, result.Metrics.MaxChannelDifference);
        Assert.Equal(16, result.Cells.Count);
        Assert.All(result.Cells, cell => Assert.Equal((0.0, 0.0, 0.0), (cell.Mae, cell.Rmse, cell.DifferingPixelRatio)));
        Assert.Equal(0, result.MaxCellRmse);
        Assert.Empty(result.WorstCells(3));
    }

    /// <summary>
    ///     Test the MAE, RMSE, and PSNR of an image pair whose differences are known exactly.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_KnownDifference_ComputesMaeRmseAndPsnr()
    {
        // Arrange: two pixels; the second differs by 10 in every channel
        using var a = Row(Px(100, 100, 100), Px(100, 100, 100));
        using var b = Row(Px(100, 100, 100), Px(110, 110, 110));

        // Act
        var metrics = ImageComparison.Compare(a, b, 8, 1, 1).Metrics;

        // Assert: sum 30 over 6 channel values is 5; sum of squares 300 over 6 is 50
        Assert.Equal(5, metrics.Mae);
        Assert.Equal(Math.Sqrt(50), metrics.Rmse);
        Assert.Equal(20 * Math.Log10(255 / Math.Sqrt(50)), metrics.PsnrDb);
        Assert.False(metrics.Identical);
        Assert.Equal(10, metrics.MaxChannelDifference);
        Assert.Equal(1, metrics.DifferingPixels);
        Assert.Equal(0.5, metrics.DifferingPixelRatio);
    }

    /// <summary>
    ///     Test that a difference in one channel is averaged over all three channels of all pixels.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_SingleChannelDifference_AveragesOverThreeChannels()
    {
        // Arrange: four pixels, one of which differs by 30 in red only
        using var a = Solid(2, 2, Px(50, 60, 70));
        using var b = Solid(2, 2, Px(50, 60, 70));
        b[1, 1] = Px(80, 60, 70);

        // Act
        var metrics = ImageComparison.Compare(a, b, 8, 1, 1).Metrics;

        // Assert: 30 over 12 channel values, and 900 over 12
        Assert.Equal(2.5, metrics.Mae);
        Assert.Equal(Math.Sqrt(75), metrics.Rmse);
        Assert.Equal(30, metrics.MaxChannelDifference);
    }

    /// <summary>
    ///     Test that only a pixel whose difference is above the tolerance counts as differing.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_Tolerance_CountsOnlyPixelsAboveTolerance()
    {
        // Arrange: four pixels whose green differs by 0, 5, 8, and 9
        using var a = Row(Px(0, 0, 0), Px(0, 0, 0), Px(0, 0, 0), Px(0, 0, 0));
        using var b = Row(Px(0, 0, 0), Px(0, 5, 0), Px(0, 8, 0), Px(0, 9, 0));

        // Act: with several tolerances
        var at8 = ImageComparison.Compare(a, b, 8, 1, 1).Metrics;
        var at0 = ImageComparison.Compare(a, b, 0, 1, 1).Metrics;
        var at4 = ImageComparison.Compare(a, b, 4, 1, 1).Metrics;
        var at255 = ImageComparison.Compare(a, b, 255, 1, 1).Metrics;

        // Assert: a difference equal to the tolerance is not counted; one above is; the error measures do not change
        Assert.Equal(1, at8.DifferingPixels);
        Assert.Equal(0.25, at8.DifferingPixelRatio);
        Assert.Equal(3, at0.DifferingPixels);
        Assert.Equal(3, at4.DifferingPixels);
        Assert.Equal(0, at255.DifferingPixels);
        Assert.Equal(at8.Mae, at0.Mae);
        Assert.Equal(at8.Rmse, at255.Rmse);
        Assert.False(at255.Identical);
    }

    /// <summary>
    ///     Test that the maximum channel difference is the largest delta of any channel of any pixel.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_MaxChannelDifference_IsLargestChannelDelta()
    {
        // Arrange: one pixel that differs by 3, 40, and 7, and one that differs by 12 in blue
        using var a = Row(Px(10, 100, 50), Px(0, 0, 0));
        using var b = Row(Px(13, 60, 57), Px(0, 0, 12));

        // Act
        var metrics = ImageComparison.Compare(a, b, 8, 1, 1).Metrics;

        // Assert: 40, of the first pixel's green; both pixels are above the tolerance of 8
        Assert.Equal(40, metrics.MaxChannelDifference);
        Assert.Equal(2, metrics.DifferingPixels);
    }

    /// <summary>
    ///     Test that the alpha channel plays no part in the comparison.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_AlphaChannel_IsIgnored()
    {
        // Arrange: the same color with different opacity
        using var a = Solid(10, 10, new Rgba32(30, 60, 90, 255));
        using var b = Solid(10, 10, new Rgba32(30, 60, 90, 17));

        // Act
        var metrics = ImageComparison.Compare(a, b, 0, 2, 2).Metrics;

        // Assert: identical
        Assert.True(metrics.Identical);
        Assert.Equal(1, metrics.Ssim);
        Assert.Equal(0, metrics.DifferingPixels);
    }

    /// <summary>
    ///     Test that images of different sizes cannot be compared.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_SizeMismatch_ThrowsArgumentException()
    {
        // Arrange: two sizes
        using var a = Solid(4, 3, Px(0, 0, 0));
        using var b = Solid(3, 4, Px(0, 0, 0));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => ImageComparison.Compare(a, b, 8, 1, 1));

        // Assert: the message names both sizes
        Assert.Contains("4x3", exception.Message);
        Assert.Contains("3x4", exception.Message);
    }

    /// <summary>
    ///     Test that a tolerance outside 0 to 255 and a grid outside 1 to 64 or larger than the image are rejected.
    /// </summary>
    /// <param name="size">The width and height of the images.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <param name="columns">The grid columns.</param>
    /// <param name="rows">The grid rows.</param>
    [Theory]
    [InlineData(70, -1, 1, 1)]
    [InlineData(70, 256, 1, 1)]
    [InlineData(70, 8, 0, 1)]
    [InlineData(70, 8, 1, 0)]
    [InlineData(70, 8, 65, 1)]
    [InlineData(70, 8, 1, 65)]
    [InlineData(4, 8, 5, 1)]
    [InlineData(4, 8, 1, 5)]
    public void ImageComparison_Compare_InvalidToleranceOrGrid_ThrowsArgumentOutOfRange(int size, int tolerance, int columns, int rows)
    {
        // Arrange: two equal images
        using var a = Solid(size, size, Px(1, 2, 3));
        using var b = Solid(size, size, Px(1, 2, 3));

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.Compare(a, b, tolerance, columns, rows));
    }

    /// <summary>
    ///     Test that null images are rejected by every method that takes images.
    /// </summary>
    [Fact]
    public void ImageComparison_Methods_NullArguments_ThrowArgumentNullException()
    {
        // Arrange / Act / Assert: each null argument throws
        using var surface = Solid(2, 2, Px(0, 0, 0));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.Compare(null!, surface, 8, 1, 1));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.Compare(surface, null!, 8, 1, 1));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.Ssim(null!, surface));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.CreateDiff(null!, surface, DiffStyle.Heat, 4));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.CreateOverlay(surface, null!, 0.5));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.CreateSideBySide(surface, surface, null!));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.Evaluate(null!, []));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.Evaluate(KnownResult(), null!));
    }

    /// <summary>
    ///     Test that comparing the same inputs twice gives equal results.
    /// </summary>
    [Fact]
    public void ImageComparison_Compare_SameInputTwice_ReturnsEqualResults()
    {
        // Arrange: two different images
        using var a = FixtureFactory.CreateGradient(37, 23);
        using var b = FixtureFactory.CreatePalette(37, 23);

        // Act
        var first = ImageComparison.Compare(a, b, 8, 4, 3);
        var second = ImageComparison.Compare(a, b, 8, 4, 3);

        // Assert: the same metrics and cells, bit for bit
        Assert.Equal(first.Metrics, second.Metrics);
        Assert.Equal(first.Cells, second.Cells);
        Assert.Equal(first.MaxCellRmse, second.MaxCellRmse);
    }

    /// <summary>
    ///     Test that the SSIM of identical images is exactly one.
    /// </summary>
    [Fact]
    public void ImageComparison_Ssim_IdenticalImages_IsOne()
    {
        // Arrange: a textured image and a copy, of a size that needs edge windows
        using var a = FixtureFactory.CreateGradient(21, 19);
        using var b = FixtureFactory.CreateGradient(21, 19);

        // Act / Assert
        Assert.Equal(1, ImageComparison.Ssim(a, b));
    }

    /// <summary>
    ///     Test that a constant brightness shift gives the closed-form SSIM of the luminance term.
    /// </summary>
    [Fact]
    public void ImageComparison_Ssim_ConstantBrightnessShift_MatchesClosedForm()
    {
        // Arrange: flat grays 100 and 110 (the luma of a gray is the gray itself)
        using var a = Solid(20, 17, Px(100, 100, 100));
        using var b = Solid(20, 17, Px(110, 110, 110));

        // Act
        var ssim = ImageComparison.Ssim(a, b);

        // Assert: with no variance the contrast term is 1, leaving (2 ma mb + C1) / (ma^2 + mb^2 + C1)
        var expected = ((2 * 100 * 110) + C1) / ((100 * 100) + (110 * 110) + C1);
        Assert.Equal(expected, ssim, 12);
    }

    /// <summary>
    ///     Test that an image and its inverse have a low (negative) similarity.
    /// </summary>
    [Fact]
    public void ImageComparison_Ssim_InvertedGradient_IsLow()
    {
        // Arrange: a horizontal ramp and its inverse
        using var a = new Surface(16, 16);
        using var b = new Surface(16, 16);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                a[x, y] = Px(x * 16, x * 16, x * 16);
                b[x, y] = Px(255 - (x * 16), 255 - (x * 16), 255 - (x * 16));
            }
        }

        // Act
        var ssim = ImageComparison.Ssim(a, b);

        // Assert: anti-correlated structure is below zero
        Assert.True(ssim < 0, $"SSIM is {ssim}");
    }

    /// <summary>
    ///     Test that an image smaller than the window is compared as one window of its own size.
    /// </summary>
    [Fact]
    public void ImageComparison_Ssim_ImageSmallerThanWindow_UsesOneWindow()
    {
        // Arrange: two 3x3 images that differ
        using var a = FixtureFactory.CreateGradient(3, 3);
        using var b = FixtureFactory.CreatePalette(3, 3);

        // Act
        var ssim = ImageComparison.Ssim(a, b);

        // Assert: the textbook formula over the whole image
        Assert.Equal(ReferenceWindow(a, b, 0, 0, 3, 3), ssim, 9);
    }

    /// <summary>
    ///     Test that windows flush with the right and bottom edges catch a difference in the last column and row.
    /// </summary>
    [Fact]
    public void ImageComparison_Ssim_DifferenceOnlyInLastColumnAndRow_IsDetectedByEdgeWindows()
    {
        // Arrange: a 10x10 texture, and a copy that differs only in the last pixel (the stride of 4 alone would end at 8)
        using var a = FixtureFactory.CreateGradient(10, 10);
        using var b = FixtureFactory.CreateGradient(10, 10);
        b[9, 9] = Px(255 - b[9, 9].R, 255 - b[9, 9].G, 255 - b[9, 9].B);

        // Act
        var ssim = ImageComparison.Ssim(a, b);

        // Assert: below one, and equal to the mean of the four 8x8 windows at origins 0 and 2 in row-major order
        Assert.True(ssim < 1, $"SSIM is {ssim}");
        var expected = (ReferenceWindow(a, b, 0, 0, 8, 8) + ReferenceWindow(a, b, 2, 0, 8, 8)
                        + ReferenceWindow(a, b, 0, 2, 8, 8) + ReferenceWindow(a, b, 2, 2, 8, 8)) / 4;
        Assert.Equal(expected, ssim, 9);
    }

    /// <summary>
    ///     Test that the grid cells partition the image exactly with integer boundaries.
    /// </summary>
    [Fact]
    public void ImageComparison_Grid_Boundaries_PartitionTheImageExactly()
    {
        // Arrange: a 10x7 image divided into 3 columns and 2 rows
        using var a = Solid(10, 7, Px(0, 0, 0));
        using var b = Solid(10, 7, Px(0, 0, 0));

        // Act
        var result = ImageComparison.Compare(a, b, 8, 3, 2);

        // Assert: columns start at 0, 3, 6 (floor(i * 10 / 3)) and rows at 0, 3 (floor(j * 7 / 2)); the areas add up
        Assert.Equal(6, result.Cells.Count);
        Assert.Equal(
            [(0, 0, 0, 0, 3, 3), (1, 0, 3, 0, 3, 3), (2, 0, 6, 0, 4, 3), (0, 1, 0, 3, 3, 4), (1, 1, 3, 3, 3, 4), (2, 1, 6, 3, 4, 4)],
            result.Cells.Select(c => (c.Column, c.Row, c.X, c.Y, c.Width, c.Height)));
        Assert.Equal(70, result.Cells.Sum(c => c.Width * c.Height));
    }

    /// <summary>
    ///     Test that a difference confined to one quadrant is reported in that cell only.
    /// </summary>
    [Fact]
    public void ImageComparison_Grid_DifferenceInOneQuadrant_IsLocatedInThatCell()
    {
        // Arrange: black images, the second white in the bottom-right quadrant
        using var a = Solid(8, 8, Px(0, 0, 0));
        using var b = Solid(8, 8, Px(0, 0, 0));
        for (var y = 4; y < 8; y++)
        {
            for (var x = 4; x < 8; x++)
            {
                b[x, y] = Px(255, 255, 255);
            }
        }

        // Act
        var result = ImageComparison.Compare(a, b, 8, 2, 2);

        // Assert: only cell (1, 1) differs, by the maximum; the whole image is a quarter as bad
        Assert.Equal([0.0, 0.0, 0.0, 255.0], result.Cells.Select(c => c.Rmse));
        Assert.Equal([0.0, 0.0, 0.0, 255.0], result.Cells.Select(c => c.Mae));
        Assert.Equal([0.0, 0.0, 0.0, 1.0], result.Cells.Select(c => c.DifferingPixelRatio));
        Assert.Equal(255, result.MaxCellRmse);
        Assert.Equal(63.75, result.Metrics.Mae);
        Assert.Equal(0.25, result.Metrics.DifferingPixelRatio);
    }

    /// <summary>
    ///     Test that the worst cells are the ones with the largest error, ties in row-major order, and only cells that differ.
    /// </summary>
    [Fact]
    public void ImageComparison_Grid_WorstCells_AreOrderedByRmseThenPosition()
    {
        // Arrange: a 6x2 image in a 3x2 grid (cells of 2x1 pixels); differences of 10, 30, and 30 in three cells
        using var a = Solid(6, 2, Px(100, 100, 100));
        using var b = Solid(6, 2, Px(100, 100, 100));
        b[0, 0] = Px(110, 110, 110);
        b[1, 0] = Px(110, 110, 110);
        b[4, 0] = Px(130, 130, 130);
        b[5, 0] = Px(130, 130, 130);
        b[2, 1] = Px(130, 130, 130);
        b[3, 1] = Px(130, 130, 130);

        // Act
        var result = ImageComparison.Compare(a, b, 8, 3, 2);

        // Assert: (2, 0) before (1, 1) because of the row-major tie-break, then (0, 0); the unchanged cells are left out
        Assert.Equal([(2, 0), (1, 1), (0, 0)], result.WorstCells(10).Select(c => (c.Column, c.Row)));
        Assert.Equal([(2, 0), (1, 1)], result.WorstCells(2).Select(c => (c.Column, c.Row)));
        Assert.Empty(result.WorstCells(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.WorstCells(-1));
    }

    /// <summary>
    ///     Test that every error metric passes at its own value and below fails when the limit is lower.
    /// </summary>
    /// <param name="name">The metric name.</param>
    [Theory]
    [InlineData("mae")]
    [InlineData("rmse")]
    [InlineData("diff-ratio")]
    [InlineData("max-diff")]
    [InlineData("cell-rmse")]
    public void ImageComparison_Evaluate_ErrorMetrics_PassWhenAtMostLimit(string name)
    {
        // Arrange: a result with known values, and the value of the metric under test
        var result = KnownResult();
        Assert.True(ImageComparison.TryParseMetric(name, out var metric));
        var value = ImageComparison.Evaluate(result, [new(metric, 0)])[0].Value;

        // Act: limits at, above, and just below the value
        var at = ImageComparison.Evaluate(result, [new(metric, value)])[0];
        var above = ImageComparison.Evaluate(result, [new(metric, value + 1)])[0];
        var below = ImageComparison.Evaluate(result, [new(metric, value - 0.001)])[0];

        // Assert: a maximum, so the value at the limit passes, and a lower limit fails
        Assert.False(at.IsMinimum);
        Assert.True(at.Passed);
        Assert.True(above.Passed);
        Assert.False(below.Passed);
        Assert.Equal((metric, value), (below.Metric, below.Value));
    }

    /// <summary>
    ///     Test that the similarity metrics pass when at least the limit.
    /// </summary>
    /// <param name="name">The metric name.</param>
    [Theory]
    [InlineData("psnr")]
    [InlineData("ssim")]
    public void ImageComparison_Evaluate_SimilarityMetrics_PassWhenAtLeastLimit(string name)
    {
        // Arrange: a result with known values
        var result = KnownResult();
        Assert.True(ImageComparison.TryParseMetric(name, out var metric));
        var value = ImageComparison.Evaluate(result, [new(metric, 0)])[0].Value;

        // Act: limits at, below, and just above the value
        var at = ImageComparison.Evaluate(result, [new(metric, value)])[0];
        var below = ImageComparison.Evaluate(result, [new(metric, value - 0.1)])[0];
        var above = ImageComparison.Evaluate(result, [new(metric, value + 0.001)])[0];

        // Assert: a minimum, so the value at the limit passes, and a higher limit fails
        Assert.True(at.IsMinimum);
        Assert.True(at.Passed);
        Assert.True(below.Passed);
        Assert.False(above.Passed);
    }

    /// <summary>
    ///     Test that the infinite PSNR of identical images passes every minimum, and that the checks keep the order given.
    /// </summary>
    [Fact]
    public void ImageComparison_Evaluate_IdenticalImagesPsnr_PassesAnyMinimum()
    {
        // Arrange: a comparison of identical images
        using var a = Solid(8, 8, Px(9, 9, 9));
        using var b = Solid(8, 8, Px(9, 9, 9));
        var result = ImageComparison.Compare(a, b, 8, 2, 2);

        // Act
        var checks = ImageComparison.Evaluate(
            result, [new(ThresholdMetric.Psnr, 1000), new(ThresholdMetric.Rmse, 0), new(ThresholdMetric.Ssim, 1)]);

        // Assert: all pass; the PSNR value is infinite
        Assert.All(checks, check => Assert.True(check.Passed));
        Assert.Equal(double.PositiveInfinity, checks[0].Value);
        Assert.Equal([ThresholdMetric.Psnr, ThresholdMetric.Rmse, ThresholdMetric.Ssim], checks.Select(c => c.Metric));
    }

    /// <summary>
    ///     Test that the worst-cell threshold uses the cell with the largest error.
    /// </summary>
    [Fact]
    public void ImageComparison_Evaluate_CellRmse_UsesTheWorstCell()
    {
        // Arrange: a comparison where one quadrant differs by the maximum
        using var a = Solid(8, 8, Px(0, 0, 0));
        using var b = Solid(8, 8, Px(0, 0, 0));
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                b[x, y] = Px(255, 255, 255);
            }
        }

        var result = ImageComparison.Compare(a, b, 8, 2, 2);

        // Act
        var checks = ImageComparison.Evaluate(
            result, [new(ThresholdMetric.CellRmse, 255), new(ThresholdMetric.CellRmse, 254), new(ThresholdMetric.Rmse, 128)]);

        // Assert: the cell's error is 255 although the whole image's is about 127.5
        Assert.Equal([true, false, true], checks.Select(c => c.Passed));
        Assert.Equal(255, checks[0].Value);
        Assert.True(checks[2].Value < 128);
    }

    /// <summary>
    ///     Test that names, ranges, and directions of the threshold metrics are consistent.
    /// </summary>
    [Fact]
    public void ImageComparison_ThresholdMetrics_NamesRangesAndDirections_AreConsistent()
    {
        // Arrange: every metric
        var metrics = Enum.GetValues<ThresholdMetric>();

        // Act / Assert: the names are unique, parse back in any case, and the documented ranges and directions hold
        Assert.Equal(7, metrics.Length);
        Assert.Equal(7, metrics.Select(ImageComparison.MetricName).Distinct().Count());
        foreach (var metric in metrics)
        {
            var name = ImageComparison.MetricName(metric);
            Assert.True(ImageComparison.TryParseMetric(name, out var parsed));
            Assert.Equal(metric, parsed);
            Assert.True(ImageComparison.TryParseMetric(name.ToUpperInvariant(), out parsed));
            Assert.Equal(metric, parsed);
        }

        Assert.Equal(["mae", "rmse", "psnr", "ssim", "diff-ratio", "max-diff", "cell-rmse"], metrics.Select(ImageComparison.MetricName));
        Assert.Equal([ThresholdMetric.Psnr, ThresholdMetric.Ssim], metrics.Where(ImageComparison.IsMinimum));
        Assert.Equal((0.0, 255.0), ImageComparison.Range(ThresholdMetric.Mae));
        Assert.Equal((0.0, 255.0), ImageComparison.Range(ThresholdMetric.Rmse));
        Assert.Equal((0.0, 255.0), ImageComparison.Range(ThresholdMetric.MaxDiff));
        Assert.Equal((0.0, 255.0), ImageComparison.Range(ThresholdMetric.CellRmse));
        Assert.Equal((0.0, 1.0), ImageComparison.Range(ThresholdMetric.DiffRatio));
        Assert.Equal((0.0, 1000.0), ImageComparison.Range(ThresholdMetric.Psnr));
        Assert.Equal((-1.0, 1.0), ImageComparison.Range(ThresholdMetric.Ssim));
        Assert.False(ImageComparison.TryParseMetric("bogus", out _));
        Assert.False(ImageComparison.TryParseMetric(null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.MetricName((ThresholdMetric)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.Range((ThresholdMetric)99));
    }

    /// <summary>
    ///     Test that every metric name of the theory data is a known metric.
    /// </summary>
    /// <param name="name">The metric name.</param>
    [Theory]
    [MemberData(nameof(MetricNames))]
    public void ImageComparison_TryParseMetric_KnownName_RoundTrips(string name)
    {
        // Arrange / Act
        var found = ImageComparison.TryParseMetric(name, out var metric);

        // Assert: found, and named the same
        Assert.True(found);
        Assert.Equal(name, ImageComparison.MetricName(metric));
    }

    /// <summary>
    ///     Test that the heat style maps the difference through the hot ramp: black, red, yellow, white.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateDiff_Heat_MapsDeltaThroughHotRamp()
    {
        // Arrange: pixels whose largest channel differs by 0, 85, 170, and 255, and one whose differences are 10, 85, 20
        using var a = Row(Px(0, 0, 0), Px(0, 0, 0), Px(0, 0, 0), Px(0, 0, 0), Px(0, 0, 0));
        using var b = Row(Px(0, 0, 0), Px(85, 0, 0), Px(0, 170, 0), Px(0, 0, 255), Px(10, 85, 20));

        // Act: with a gain of 1 and with the default gain
        using var diff = ImageComparison.CreateDiff(a, b, DiffStyle.Heat, 1);
        using var amplified = ImageComparison.CreateDiff(a, b, DiffStyle.Heat, ImageComparison.DefaultGain);

        // Assert: v 0 is black, 85 is red, 170 is yellow, 255 is white, and the largest channel decides
        Assert.Equal(Px(0, 0, 0), diff[0, 0]);
        Assert.Equal(Px(255, 0, 0), diff[1, 0]);
        Assert.Equal(Px(255, 255, 0), diff[2, 0]);
        Assert.Equal(Px(255, 255, 255), diff[3, 0]);
        Assert.Equal(Px(255, 0, 0), diff[4, 0]);

        // Gain 4: a delta of 64 or more is clamped to v 255 (white); identical stays black
        Assert.Equal(Px(0, 0, 0), amplified[0, 0]);
        Assert.Equal(Px(255, 255, 255), amplified[3, 0]);
        Assert.Equal(Px(255, 255, 255), amplified[2, 0]);
        Assert.Equal(Px(255, 255, 255), amplified[4, 0]);

        // The gain range is 0.1 to 255
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.CreateDiff(a, b, DiffStyle.Heat, 0.05));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.CreateDiff(a, b, DiffStyle.Heat, 256));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.CreateDiff(a, b, DiffStyle.Heat, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.CreateDiff(a, b, (DiffStyle)9, 1));
    }

    /// <summary>
    ///     Test that the hot ramp of a small amplified difference is a dark red.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateDiff_HeatSmallDelta_IsDarkRed()
    {
        // Arrange: a difference of 21 with gain 4 is v 84, and a difference of 1 with gain 0.5 rounds to v 1 (half away from zero)
        using var a = Row(Px(0, 0, 0), Px(0, 0, 0));
        using var b = Row(Px(21, 0, 0), Px(1, 0, 0));

        // Act
        using var diff = ImageComparison.CreateDiff(a, b, DiffStyle.Heat, 4);
        using var half = ImageComparison.CreateDiff(a, b, DiffStyle.Heat, 0.5);

        // Assert: R = 3v
        Assert.Equal(Px(252, 0, 0), diff[0, 0]);
        Assert.Equal(Px(12, 0, 0), diff[1, 0]);
        Assert.Equal(Px(3, 0, 0), half[1, 0]);
    }

    /// <summary>
    ///     Test that the absolute style amplifies each channel by itself and clamps at 255.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateDiff_Abs_AmplifiesEachChannelAndClamps()
    {
        // Arrange: differences of 10, 20, 30 in one pixel (in either direction), and of 100 in another
        using var a = Row(Px(10, 20, 30), Px(0, 0, 100));
        using var b = Row(Px(0, 40, 0), Px(0, 0, 0));

        // Act
        using var diff = ImageComparison.CreateDiff(a, b, DiffStyle.Abs, 4);

        // Assert: 40, 80, 120 and an opaque result; 100 * 4 is clamped to 255
        Assert.Equal(Px(40, 80, 120), diff[0, 0]);
        Assert.Equal(Px(0, 0, 255), diff[1, 0]);
    }

    /// <summary>
    ///     Test the overlay blend with the integer rule for several weights.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateOverlay_Weight_BlendsWithIntegerRule()
    {
        // Arrange: two pixels
        using var a = Row(Px(0, 100, 255));
        using var b = Row(Px(255, 200, 0));

        // Act
        using var zero = ImageComparison.CreateOverlay(a, b, 0);
        using var half = ImageComparison.CreateOverlay(a, b, 0.5);
        using var one = ImageComparison.CreateOverlay(a, b, 1);

        // Assert: weight 0 is the first image, 1 the second; 0.5 is bw = 128 and (a * 127 + b * 128 + 127) / 255
        Assert.Equal(Px(0, 100, 255), zero[0, 0]);
        Assert.Equal(Px(255, 200, 0), one[0, 0]);
        Assert.Equal(Px(128, 150, 127), half[0, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.CreateOverlay(a, b, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.CreateOverlay(a, b, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageComparison.CreateOverlay(a, b, double.NaN));
    }

    /// <summary>
    ///     Test that the side-by-side image places the three panels in order, separated by gray gaps.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateSideBySide_PlacesPanelsInOrderSeparatedByGaps()
    {
        // Arrange: three flat panels of 4x3 pixels
        using var a = Solid(4, 3, Px(255, 0, 0));
        using var b = Solid(4, 3, Px(0, 0, 255));
        using var diff = Solid(4, 3, Px(0, 255, 0));

        // Act
        using var sheet = ImageComparison.CreateSideBySide(a, b, diff);

        // Assert: 3 * 4 + 2 * 8 = 28 wide; panels at 0, 12, and 24; gray gaps between
        Assert.Equal((28, 3), (sheet.Width, sheet.Height));
        var gray = Px(128, 128, 128);
        foreach (var y in new[] { 0, 2 })
        {
            Assert.Equal(Px(255, 0, 0), sheet[0, y]);
            Assert.Equal(Px(255, 0, 0), sheet[3, y]);
            Assert.Equal(gray, sheet[4, y]);
            Assert.Equal(gray, sheet[11, y]);
            Assert.Equal(Px(0, 0, 255), sheet[12, y]);
            Assert.Equal(Px(0, 0, 255), sheet[15, y]);
            Assert.Equal(gray, sheet[16, y]);
            Assert.Equal(gray, sheet[23, y]);
            Assert.Equal(Px(0, 255, 0), sheet[24, y]);
            Assert.Equal(Px(0, 255, 0), sheet[27, y]);
        }
    }

    /// <summary>
    ///     Test that panels that would make the sheet too wide are scaled down to fit the maximum size.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateSideBySide_TooWide_ScalesPanelsToFitMaximum()
    {
        // Arrange: three panels of 3000x2 pixels, which would need 9016 pixels
        using var a = Solid(3000, 2, Px(255, 0, 0));
        using var b = Solid(3000, 2, Px(0, 0, 255));
        using var diff = Solid(3000, 2, Px(0, 255, 0));

        // Act
        using var sheet = ImageComparison.CreateSideBySide(a, b, diff);

        // Assert: the scale is (8192 - 16) / 9000, so the panels are 2725x1 and the sheet is 8191 wide
        Assert.Equal((8191, 1), (sheet.Width, sheet.Height));
        Assert.Equal(Px(255, 0, 0), sheet[0, 0]);
        Assert.Equal(Px(255, 0, 0), sheet[2724, 0]);
        Assert.Equal(Px(128, 128, 128), sheet[2725, 0]);
        Assert.Equal(Px(0, 0, 255), sheet[2733, 0]);
        Assert.Equal(Px(0, 255, 0), sheet[8190, 0]);
    }

    /// <summary>
    ///     Test that the pictures of images that differ in size from each other are refused.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateImages_SizeMismatch_ThrowsArgumentException()
    {
        // Arrange: two sizes
        using var wide = Solid(4, 3, Px(0, 0, 0));
        using var tall = Solid(3, 4, Px(0, 0, 0));

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ImageComparison.CreateDiff(wide, tall, DiffStyle.Heat, 4));
        Assert.Throws<ArgumentException>(() => ImageComparison.CreateOverlay(wide, tall, 0.5));
        Assert.Throws<ArgumentException>(() => ImageComparison.CreateSideBySide(wide, wide, tall));
        Assert.Throws<ArgumentException>(() => ImageComparison.Ssim(wide, tall));
    }

    /// <summary>
    ///     Test that creating the pictures twice from the same inputs gives identical pixels.
    /// </summary>
    [Fact]
    public void ImageComparison_CreateImages_SameInputTwice_ProduceIdenticalPixels()
    {
        // Arrange: two different images
        using var a = FixtureFactory.CreateGradient(31, 17);
        using var b = FixtureFactory.CreatePalette(31, 17);

        // Act: create each picture twice
        using var diff1 = ImageComparison.CreateDiff(a, b, DiffStyle.Heat, 4);
        using var diff2 = ImageComparison.CreateDiff(a, b, DiffStyle.Heat, 4);
        using var overlay1 = ImageComparison.CreateOverlay(a, b, 0.3);
        using var overlay2 = ImageComparison.CreateOverlay(a, b, 0.3);
        using var sheet1 = ImageComparison.CreateSideBySide(a, b, diff1);
        using var sheet2 = ImageComparison.CreateSideBySide(a, b, diff2);

        // Assert: identical pixels
        SurfaceAssert.Equal(diff1, diff2);
        SurfaceAssert.Equal(overlay1, overlay2);
        SurfaceAssert.Equal(sheet1, sheet2);
    }
}
