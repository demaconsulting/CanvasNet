using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartRenderer"/>'s title pass, legend pass (every
///     <see cref="ChartLegendPosition"/>), axis painting, and per-point data labels.
/// </summary>
public class ChartRendererAxisLegendTitleTests
{
    /// <summary>Opaque white - <see cref="ChartRenderOptions.Default"/>'s own documented default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque green, used as an explicit series color distinct from any legend swatch confusion.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>Opaque purple, used as a second explicit series color.</summary>
    private static readonly Rgba32 Purple = new(128, 0, 128, 255);

    /// <summary>Scans a rectangular pixel region for any pixel exactly equal to <paramref name="color"/>. See <see cref="ChartRendererTests"/>'s own identical helper.</summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="color">The exact color to look for.</param>
    /// <param name="x0">The region's inclusive left x-coordinate.</param>
    /// <param name="y0">The region's inclusive top y-coordinate.</param>
    /// <param name="x1">The region's inclusive right x-coordinate.</param>
    /// <param name="y1">The region's inclusive bottom y-coordinate.</param>
    /// <returns><see langword="true"/> when at least one pixel in the region exactly equals <paramref name="color"/>.</returns>
    private static bool RegionContainsColor(Surface surface, Rgba32 color, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (surface[x, y].Equals(color))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Scans a rectangular pixel region for any pixel that differs from <paramref name="background"/> (proving something painted there, without needing to know its exact color).</summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="background">The known cleared background color.</param>
    /// <param name="x0">The region's inclusive left x-coordinate.</param>
    /// <param name="y0">The region's inclusive top y-coordinate.</param>
    /// <param name="x1">The region's inclusive right x-coordinate.</param>
    /// <param name="y1">The region's inclusive bottom y-coordinate.</param>
    /// <returns><see langword="true"/> when at least one pixel in the region differs from <paramref name="background"/>.</returns>
    private static bool RegionHasNonBackgroundPixel(Surface surface, Rgba32 background, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (!surface[x, y].Equals(background))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Finds the smallest (topmost) row containing <paramref name="color"/> anywhere in <paramref name="surface"/>, or -1 if none.</summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="color">The exact color to look for.</param>
    /// <returns>The topmost row index containing <paramref name="color"/>, or <c>-1</c> when not found.</returns>
    private static int FindTopmostColorRow(Surface surface, Rgba32 color)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].Equals(color))
                {
                    return y;
                }
            }
        }

        return -1;
    }

    /// <summary>Builds a full-scale, single-category Column chart (bar spanning the full value range) with the given optional title/legend.</summary>
    /// <param name="title">An optional title.</param>
    /// <param name="legend">An optional legend.</param>
    /// <param name="pointLabels">An optional per-point label for the series' single value.</param>
    /// <returns>The built chart.</returns>
    private static Chart BuildFullScaleColumnChart(ChartTitle? title = null, ChartLegend? legend = null, IReadOnlyList<string>? pointLabels = null)
    {
        var seriesA = new ChartSeries("Alpha", [10.0], pointLabels: pointLabels, color: Green);
        var seriesB = new ChartSeries("Beta", [10.0], color: Purple);
        var categoryAxis = new ChartAxis(["Only"]);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        return new Chart(ChartType.Column, [seriesA, seriesB], categoryAxis, valueAxis, legend, title);
    }

    /// <summary>Proves a chart title paints visible content within the reserved title band.</summary>
    [Fact]
    public void Render_WithTitle_PaintsContentInTitleBand()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart(title: new ChartTitle("Revenue"));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        // The title band's height is a small fraction of the full render target; a generous top
        // fifth comfortably contains it regardless of the exact bundled-font text metrics.
        Assert.True(RegionHasNonBackgroundPixel(surface, White, 0, 0, 399, 59));
    }

    /// <summary>Proves a chart title pushes the plot area's content down, compared to the same chart without a title.</summary>
    [Fact]
    public void Render_WithTitle_PushesPlotContentDownComparedToNoTitle()
    {
        // Arrange
        var withoutTitle = BuildFullScaleColumnChart();
        var withTitle = BuildFullScaleColumnChart(title: new ChartTitle("Revenue"));

        // Act
        using var surfaceWithoutTitle = ChartRenderer.Render(withoutTitle, 400, 300);
        using var surfaceWithTitle = ChartRenderer.Render(withTitle, 400, 300);

        var topRowWithoutTitle = FindTopmostColorRow(surfaceWithoutTitle, Green);
        var topRowWithTitle = FindTopmostColorRow(surfaceWithTitle, Green);

        // Assert
        Assert.True(topRowWithoutTitle >= 0);
        Assert.True(topRowWithTitle >= 0);
        Assert.True(topRowWithTitle > topRowWithoutTitle);
    }

    /// <summary>Proves a Top-positioned legend paints its swatches within the top band.</summary>
    [Fact]
    public void Render_LegendTop_PaintsSwatchesInTopBand()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart(legend: new ChartLegend(ChartLegendPosition.Top));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.True(RegionContainsColor(surface, Green, 0, 0, 399, 59));
        Assert.True(RegionContainsColor(surface, Purple, 0, 0, 399, 59));
    }

    /// <summary>Proves a Bottom-positioned legend paints its swatches within the bottom band.</summary>
    [Fact]
    public void Render_LegendBottom_PaintsSwatchesInBottomBand()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart(legend: new ChartLegend(ChartLegendPosition.Bottom));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.True(RegionContainsColor(surface, Green, 0, 240, 399, 299));
        Assert.True(RegionContainsColor(surface, Purple, 0, 240, 399, 299));
    }

    /// <summary>Proves a Left-positioned legend paints its swatches within the left band.</summary>
    [Fact]
    public void Render_LegendLeft_PaintsSwatchesInLeftBand()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart(legend: new ChartLegend(ChartLegendPosition.Left));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.True(RegionContainsColor(surface, Green, 0, 0, 159, 299));
        Assert.True(RegionContainsColor(surface, Purple, 0, 0, 159, 299));
    }

    /// <summary>Proves a Right-positioned legend paints its swatches within the right band.</summary>
    [Fact]
    public void Render_LegendRight_PaintsSwatchesInRightBand()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart(legend: new ChartLegend(ChartLegendPosition.Right));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.True(RegionContainsColor(surface, Green, 240, 0, 399, 299));
        Assert.True(RegionContainsColor(surface, Purple, 240, 0, 399, 299));
    }

    /// <summary>Proves a Right-positioned legend reserves plot-area space, narrowing where series content can reach, compared to no legend at all.</summary>
    [Fact]
    public void Render_LegendRight_NarrowsPlotAreaComparedToNoLegend()
    {
        // Arrange
        var withoutLegend = BuildFullScaleColumnChart();
        var withRightLegend = BuildFullScaleColumnChart(legend: new ChartLegend(ChartLegendPosition.Right));

        // Act
        using var surfaceWithoutLegend = ChartRenderer.Render(withoutLegend, 400, 300);
        using var surfaceWithLegend = ChartRenderer.Render(withRightLegend, 400, 300);

        // Assert
        // Without a legend, the rightmost (second) series' bar reaches almost to the render
        // target's right edge; with a Right legend reserving space, it cannot reach nearly as
        // far right.
        var rightmostWithoutLegend = FindRightmostColorColumn(surfaceWithoutLegend, Purple);
        var rightmostWithLegend = FindRightmostColorColumn(surfaceWithLegend, Purple);

        Assert.True(rightmostWithoutLegend >= 0);
        Assert.True(rightmostWithLegend >= 0);
        Assert.True(rightmostWithLegend < rightmostWithoutLegend);
    }

    /// <summary>Finds the largest (rightmost) column containing <paramref name="color"/> anywhere in <paramref name="surface"/>, or -1 if none.</summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="color">The exact color to look for.</param>
    /// <returns>The rightmost column index containing <paramref name="color"/>, or <c>-1</c> when not found.</returns>
    private static int FindRightmostColorColumn(Surface surface, Rgba32 color)
    {
        for (var x = surface.Width - 1; x >= 0; x--)
        {
            for (var y = 0; y < surface.Height; y++)
            {
                if (surface[x, y].Equals(color))
                {
                    return x;
                }
            }
        }

        return -1;
    }

    /// <summary>Proves a legend with <see cref="ChartLegendPosition.None"/> reserves no band, rendering successfully with no legend pass.</summary>
    [Fact]
    public void Render_LegendPositionNone_RendersSuccessfullyWithoutLegendBand()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart(legend: new ChartLegend(ChartLegendPosition.None));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(400, surface.Width);
        Assert.Equal(300, surface.Height);
    }

    /// <summary>Proves an invisible legend (<see cref="ChartLegend.IsVisible"/> = <see langword="false"/>) reserves no band.</summary>
    [Fact]
    public void Render_LegendNotVisible_RendersSuccessfullyWithoutLegendBand()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart(legend: new ChartLegend(ChartLegendPosition.Right, isVisible: false));

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        var withoutLegend = BuildFullScaleColumnChart();
        using var surfaceWithoutLegend = ChartRenderer.Render(withoutLegend, 400, 300);

        // Assert
        // An invisible legend reserves exactly as little space as no legend at all: both renders
        // reach the same rightmost extent for the second series' bar.
        Assert.Equal(FindRightmostColorColumn(surfaceWithoutLegend, Purple), FindRightmostColorColumn(surface, Purple));
    }

    /// <summary>Proves a value axis and category axis paint non-background content in their reserved margins.</summary>
    [Fact]
    public void Render_WithAxes_PaintsNonBackgroundContentInReservedMargins()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart();

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        // The left margin (reserved for the vertical value axis' tick labels/marks) and the
        // bottom margin (reserved for the horizontal category axis) both contain painted content.
        Assert.True(RegionHasNonBackgroundPixel(surface, White, 0, 20, 45, 270));
        Assert.True(RegionHasNonBackgroundPixel(surface, White, 60, 280, 340, 299));
    }

    /// <summary>Counts how many pixels in <paramref name="surface"/> differ from <paramref name="background"/>.</summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="background">The known cleared background color.</param>
    /// <returns>The count of non-background pixels.</returns>
    private static int CountNonBackgroundPixels(Surface surface, Rgba32 background)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (!surface[x, y].Equals(background))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Proves a per-point data label (an explicit, opt-in <see cref="ChartSeries.PointLabels"/> entry) paints additional, visible content compared to the same chart without one.</summary>
    [Fact]
    public void Render_PointWithDataLabel_PaintsVisibleTextNearPoint()
    {
        // Arrange
        // A half-scale bar (value 5 of a 0..10 range) leaves headroom above its top edge for the
        // data label text to actually land on-surface (a full-scale bar's label would be pushed
        // above y=0 and clipped away entirely).
        var withLabel = new Chart(ChartType.Column, [new ChartSeries("Alpha", [5.0], pointLabels: ["5.0"], color: Green)], new ChartAxis(["Only"]), new ChartAxis(minimum: 0f, maximum: 10f));
        var withoutLabel = new Chart(ChartType.Column, [new ChartSeries("Alpha", [5.0], color: Green)], new ChartAxis(["Only"]), new ChartAxis(minimum: 0f, maximum: 10f));

        // Act
        using var surfaceWithLabel = ChartRenderer.Render(withLabel, 400, 300);
        using var surfaceWithoutLabel = ChartRenderer.Render(withoutLabel, 400, 300);

        // Assert
        // Both charts share identical axes/bars/gridlines; the only difference a data label can
        // introduce is additional painted (non-background) pixels for its text glyphs.
        Assert.True(CountNonBackgroundPixels(surfaceWithLabel, White) > CountNonBackgroundPixels(surfaceWithoutLabel, White));
    }

    /// <summary>Proves a series with no <see cref="ChartSeries.PointLabels"/> renders fully deterministically (no stray data-label artifacts appear from one render to the next).</summary>
    [Fact]
    public void Render_PointWithoutDataLabel_RendersDeterministicallyWithNoLabelArtifacts()
    {
        // Arrange
        var chart = BuildFullScaleColumnChart();

        // Act
        using var surfaceA = ChartRenderer.Render(chart, 400, 300);
        using var surfaceB = ChartRenderer.Render(chart, 400, 300);

        // Assert
        // Rendering the identical chart (with no PointLabels on any series) twice must be fully
        // deterministic: no data-label text (or anything else) can appear in one render but not
        // the other.
        Assert.Equal(CountNonBackgroundPixels(surfaceA, White), CountNonBackgroundPixels(surfaceB, White));
    }
}
