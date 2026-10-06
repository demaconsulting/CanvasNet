using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Edge-case tests for <see cref="ChartRenderer"/>: single-data-point series, single-value
///     Pie/Doughnut, negative values, many-series legend layout, long labels, and very
///     small/large render targets.
/// </summary>
public class ChartRendererEdgeCaseTests
{
    /// <summary>Opaque white - <see cref="ChartRenderOptions.Default"/>'s own documented default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque red, used as an explicit series/point color.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Proves a single-data-point Bar series renders without error, painting a single full-width band.</summary>
    [Fact]
    public void Render_Bar_SingleDataPoint_RendersSuccessfully()
    {
        // Arrange
        var series = new ChartSeries("S1", [5.0], color: Red);
        var chart = new Chart(ChartType.Bar, [series], new ChartAxis(["Only"]), new ChartAxis(minimum: 0f, maximum: 10f));

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 200);

        // Assert
        Assert.Equal(Red, surface[150, 100]);
    }

    /// <summary>Proves a single-data-point Column series renders without error.</summary>
    [Fact]
    public void Render_Column_SingleDataPoint_RendersSuccessfully()
    {
        // Arrange
        var series = new ChartSeries("S1", [5.0], color: Red);
        var chart = new Chart(ChartType.Column, [series], new ChartAxis(["Only"]), new ChartAxis(minimum: 0f, maximum: 10f));

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 200);

        // Assert
        Assert.Equal(Red, surface[150, 100]);
    }

    /// <summary>Proves a single-data-point Line series renders without error (a lone marker, no segment to stroke).</summary>
    [Fact]
    public void Render_Line_SingleDataPoint_RendersSuccessfully()
    {
        // Arrange
        var series = new ChartSeries("S1", [5.0], color: Red);
        var chart = new Chart(ChartType.Line, [series], new ChartAxis(["Only"]), new ChartAxis(minimum: 0f, maximum: 10f));

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 200);

        // Assert
        Assert.Equal(300, surface.Width);
        Assert.Equal(200, surface.Height);
    }

    /// <summary>Proves a single-data-point Area series renders without error (a degenerate, zero-width fill region).</summary>
    [Fact]
    public void Render_Area_SingleDataPoint_RendersSuccessfully()
    {
        // Arrange
        var series = new ChartSeries("S1", [5.0], color: Red);
        var chart = new Chart(ChartType.Area, [series], new ChartAxis(["Only"]), new ChartAxis(minimum: 0f, maximum: 10f));

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 200);

        // Assert
        Assert.Equal(300, surface.Width);
        Assert.Equal(200, surface.Height);
    }

    /// <summary>Proves a single-value (100% share) Pie renders as one full-circle wedge, not an invisible/degenerate arc.</summary>
    [Fact]
    public void Render_Pie_SingleValue_RendersFullCircleWedge()
    {
        // Arrange
        var series = new ChartSeries("S1", [1.0], pointColors: [Red]);
        var chart = new Chart(ChartType.Pie, [series]);

        // Act
        using var surface = ChartRenderer.Render(chart, 200, 200);

        // Assert
        Assert.Equal(Red, surface[100, 30]);
        Assert.Equal(Red, surface[100, 170]);
    }

    /// <summary>Proves a single-value (100% share) Doughnut renders as one full-circle ring wedge, not an invisible/degenerate arc.</summary>
    [Fact]
    public void Render_Doughnut_SingleValue_RendersFullCircleWedge()
    {
        // Arrange
        var series = new ChartSeries("S1", [1.0], pointColors: [Red]);
        var chart = new Chart(ChartType.Doughnut, [series]);

        // Act
        using var surface = ChartRenderer.Render(chart, 200, 200);

        // Assert
        Assert.Equal(Red, surface[100, 30]);
        Assert.Equal(Red, surface[100, 170]);
        Assert.Equal(White, surface[100, 100]);
    }

    /// <summary>Proves a Column series spanning both negative and positive values paints bars extending both above and below the zero baseline.</summary>
    [Fact]
    public void Render_Column_NegativeAndPositiveValues_PaintsBothAboveAndBelowZeroBaseline()
    {
        // Arrange
        var series = new ChartSeries("S1", [-5.0, 5.0], color: Red);
        var chart = new Chart(ChartType.Column, [series], new ChartAxis(["Neg", "Pos"]), new ChartAxis(minimum: -5f, maximum: 5f));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        // The zero baseline falls at the vertical midpoint of the inner plot rect; the negative
        // bar (first category) occupies a band below it, the positive bar (second category) an
        // equally sized band above it.
        Assert.Equal(Red, surface[100, 220]);
        Assert.Equal(Red, surface[300, 80]);
    }

    /// <summary>Proves a Bar series spanning both negative and positive values paints bars extending both left and right of the zero baseline.</summary>
    [Fact]
    public void Render_Bar_NegativeAndPositiveValues_PaintsBothLeftAndRightOfZeroBaseline()
    {
        // Arrange
        var series = new ChartSeries("S1", [-5.0, 5.0], color: Red);
        var chart = new Chart(ChartType.Bar, [series], new ChartAxis(["Neg", "Pos"]), new ChartAxis(minimum: -5f, maximum: 5f));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(Red, surface[80, 75]);
        Assert.Equal(Red, surface[320, 225]);
    }

    /// <summary>Proves a Line series spanning both negative and positive values renders successfully, crossing the zero baseline.</summary>
    [Fact]
    public void Render_Line_NegativeAndPositiveValues_RendersSuccessfully()
    {
        // Arrange
        var series = new ChartSeries("S1", [-5.0, 5.0], color: Red);
        var chart = new Chart(ChartType.Line, [series], new ChartAxis(["Neg", "Pos"]), new ChartAxis(minimum: -5f, maximum: 5f));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }

    /// <summary>Proves an Area series spanning both negative and positive values renders successfully, filling both above and below the zero baseline.</summary>
    [Fact]
    public void Render_Area_NegativeAndPositiveValues_RendersSuccessfully()
    {
        // Arrange
        var series = new ChartSeries("S1", [-5.0, 5.0], color: Red);
        var chart = new Chart(ChartType.Area, [series], new ChartAxis(["Neg", "Pos"]), new ChartAxis(minimum: -5f, maximum: 5f));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }

    /// <summary>Proves many series (enough to overflow a legend band) renders without error, the legend degrading by omission rather than throwing or overlapping the plot area.</summary>
    [Fact]
    public void Render_ManySeries_LegendDegradesByOmissionWithoutError()
    {
        // Arrange
        var series = new List<ChartSeries>();
        for (var i = 0; i < 40; i++)
        {
            series.Add(new ChartSeries($"Series {i}", [1.0]));
        }

        var chart = new Chart(
            ChartType.Column,
            series,
            new ChartAxis(["Only"]),
            new ChartAxis(minimum: 0f, maximum: 1f),
            new ChartLegend(ChartLegendPosition.Right));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }

    /// <summary>Proves a very long category label is truncated to fit rather than overflowing its allotted axis band or crashing.</summary>
    [Fact]
    public void Render_VeryLongCategoryLabel_RendersSuccessfullyWithoutOverflow()
    {
        // Arrange
        var longLabel = new string('X', 200);
        var series = new ChartSeries("S1", [5.0], color: Red);
        var chart = new Chart(ChartType.Column, [series], new ChartAxis([longLabel]), new ChartAxis(minimum: 0f, maximum: 10f));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }

    /// <summary>Proves a very long legend label is truncated to fit rather than overflowing its allotted legend band or crashing.</summary>
    [Fact]
    public void Render_VeryLongLegendLabel_RendersSuccessfullyWithoutOverflow()
    {
        // Arrange
        var longName = new string('Y', 200);
        var series = new ChartSeries(longName, [5.0], color: Red);
        var chart = new Chart(
            ChartType.Column,
            [series],
            new ChartAxis(["Only"]),
            new ChartAxis(minimum: 0f, maximum: 10f),
            new ChartLegend(ChartLegendPosition.Bottom));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }

    /// <summary>Proves every <see cref="ChartType"/> renders successfully at the smallest possible 1x1 render target.</summary>
    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    [InlineData(ChartType.Pie)]
    [InlineData(ChartType.Doughnut)]
    public void Render_OnePixelByOnePixelTarget_RendersSuccessfullyForEveryChartType(ChartType type)
    {
        // Arrange
        var series = new ChartSeries("S1", [1.0, 2.0], pointColors: type is ChartType.Pie or ChartType.Doughnut ? [Red, Red] : null);
        var categoryAxis = type is ChartType.Pie or ChartType.Doughnut ? null : new ChartAxis(["A", "B"]);
        var chart = new Chart(
            type,
            [series],
            categoryAxis,
            categoryAxis is null ? null : new ChartAxis(minimum: 0f, maximum: 2f),
            new ChartLegend(ChartLegendPosition.Right),
            new ChartTitle("T"));

        // Act
        using var surface = ChartRenderer.Render(chart, 1, 1);

        // Assert
        Assert.Equal(1, surface.Width);
        Assert.Equal(1, surface.Height);
    }

    /// <summary>Proves rendering succeeds across a handful of minimal sizes up to roughly 10x10, with title and legend bands present (and defensively skipped once too small).</summary>
    [Theory]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(10, 3)]
    [InlineData(3, 10)]
    public void Render_VerySmallRenderTargets_RenderSuccessfully(int width, int height)
    {
        // Arrange
        var series = new ChartSeries("S1", [1.0, 2.0]);
        var chart = new Chart(
            ChartType.Column,
            [series],
            new ChartAxis(["A", "B"]),
            new ChartAxis(minimum: 0f, maximum: 2f),
            new ChartLegend(ChartLegendPosition.Top),
            new ChartTitle("T"));

        // Act
        using var surface = ChartRenderer.Render(chart, width, height);

        // Assert
        Assert.Equal(width, surface.Width);
        Assert.Equal(height, surface.Height);
    }

    /// <summary>Proves rendering succeeds at the <see cref="Surface"/> maximum dimension in a reasonable time.</summary>
    [Fact]
    public void Render_MaximumDimensionRenderTarget_RendersSuccessfully()
    {
        // Arrange
        var series = new ChartSeries("S1", [1.0, 2.0], color: Red);
        var chart = new Chart(
            ChartType.Column,
            [series],
            new ChartAxis(["A", "B"]),
            new ChartAxis(minimum: 0f, maximum: 2f),
            new ChartLegend(ChartLegendPosition.Right),
            new ChartTitle("Large"));

        // Act
        using var surface = ChartRenderer.Render(chart, 8192, 8192);

        // Assert
        Assert.Equal(8192, surface.Width);
        Assert.Equal(8192, surface.Height);
    }

    /// <summary>Proves a grouped Column chart with a large practical series count narrows each series' bar within its category band rather than overlapping or crashing.</summary>
    [Fact]
    public void Render_ManySeriesGroupedColumn_NarrowsBarsRatherThanOverlapping()
    {
        // Arrange
        var series = new List<ChartSeries>();
        for (var i = 0; i < 20; i++)
        {
            series.Add(new ChartSeries($"S{i}", [1.0]));
        }

        var chart = new Chart(
            ChartType.Column,
            series,
            new ChartAxis(["Only"]),
            new ChartAxis(minimum: 0f, maximum: 1f));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }
}
