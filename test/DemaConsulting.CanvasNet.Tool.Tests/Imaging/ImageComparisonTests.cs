// cspell:ignore psnr rmse ssim
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageComparison class: the thresholds of a comparison and the names of their metrics. (The
///     metrics, the grid, and the pictures are computed by the core Surface class and tested there.)
/// </summary>
public class ImageComparisonTests
{
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
    ///     Creates a result whose measures are known, for the threshold tests.
    /// </summary>
    /// <returns>A result with a MAE of 5, RMSE of 7, PSNR of 30, SSIM of 0.8, differing ratio of 0.25, maximum difference of 40, and worst cell RMSE of 9.</returns>
    private static ComparisonResult KnownResult()
    {
        var metrics = new ComparisonMetrics(100, false, 5, 7, 30, 0.8, 25, 0.25, 40);
        ComparisonCell[] cells = [new(0, 0, 0, 0, 5, 10, 1, 2, 0.1), new(1, 0, 5, 0, 5, 10, 8, 9, 0.4)];
        return new ComparisonResult(10, 10, 8, metrics, 2, 1, cells);
    }

    /// <summary>
    ///     Test that null arguments are rejected by the threshold evaluation.
    /// </summary>
    [Fact]
    public void ImageComparison_Evaluate_NullArguments_ThrowArgumentNullException()
    {
        // Arrange / Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => ImageComparison.Evaluate(null!, []));
        Assert.Throws<ArgumentNullException>(() => ImageComparison.Evaluate(KnownResult(), null!));
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
        var result = a.Compare(b, 8, 2, 2);

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

        var result = a.Compare(b, 8, 2, 2);

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
    ///     Test that the defaults of the command-line options are within what the core library accepts.
    /// </summary>
    [Fact]
    public void ImageComparison_Defaults_AreAcceptedByTheCoreLibrary()
    {
        // Arrange: two small surfaces
        using var a = Solid(8, 8, Px(1, 2, 3));
        using var b = Solid(8, 8, Px(3, 2, 1));

        // Act / Assert: the default tolerance and grid are accepted by Compare, the default gain by CreateDiff, the weight by CreateOverlay
        var result = a.Compare(b, ImageComparison.DefaultTolerance, ImageComparison.DefaultGridColumns, ImageComparison.DefaultGridRows);
        Assert.Equal(ImageComparison.DefaultGridColumns * ImageComparison.DefaultGridRows, result.Cells.Count);
        using var diff = a.CreateDiff(b, DiffStyle.Heat, ImageComparison.DefaultGain);
        using var overlay = a.CreateOverlay(b, ImageComparison.DefaultOverlayWeight);
        Assert.Equal(a.Width, overlay.Width);
        Assert.Equal(a.Width, diff.Width);
    }
}
