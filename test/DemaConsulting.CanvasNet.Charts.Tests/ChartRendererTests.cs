using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartRenderer"/>'s public entry points: argument validation, the DPI
///     overload, and a per-<see cref="ChartType"/> success path.
/// </summary>
public class ChartRendererTests
{
    /// <summary>Opaque white - <see cref="ChartRenderOptions.Default"/>'s own documented default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Opaque red, used throughout as an explicit, unambiguous series/point color.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>Opaque blue, used as a second explicit series/point color.</summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>
    ///     Scans the inclusive pixel rectangle [<paramref name="x0"/>, <paramref name="x1"/>] x
    ///     [<paramref name="y0"/>, <paramref name="y1"/>] of <paramref name="surface"/> for any
    ///     pixel exactly equal to <paramref name="color"/>.
    /// </summary>
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

    /// <summary>Proves a null chart is rejected by the pixel-size overload.</summary>
    [Fact]
    public void Render_PixelOverloadNullChart_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ChartRenderer.Render(null!, 100, 100));
    }

    /// <summary>Proves a null chart is rejected by the DPI overload.</summary>
    [Fact]
    public void Render_DpiOverloadNullChart_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ChartRenderer.Render(null!, 2f, 1f, 100f));
    }

    /// <summary>Proves an invalid pixel width/height propagates <see cref="Surface"/>'s own validation directly.</summary>
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(8193, 100)]
    public void Render_PixelOverloadInvalidSize_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        // Arrange
        var chart = new Chart(ChartType.Bar, [new ChartSeries("S1", [1.0])]);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRenderer.Render(chart, width, height));
    }

    /// <summary>Proves a non-finite or non-positive <c>widthInches</c>/<c>heightInches</c>/<c>dpi</c> is rejected.</summary>
    [Theory]
    [InlineData(0f, 1f, 100f)]
    [InlineData(-1f, 1f, 100f)]
    [InlineData(float.NaN, 1f, 100f)]
    [InlineData(float.PositiveInfinity, 1f, 100f)]
    [InlineData(1f, 0f, 100f)]
    [InlineData(1f, -1f, 100f)]
    [InlineData(1f, float.NaN, 100f)]
    [InlineData(1f, 1f, 0f)]
    [InlineData(1f, 1f, -1f)]
    [InlineData(1f, 1f, float.NaN)]
    public void Render_DpiOverloadInvalidInputs_ThrowsArgumentOutOfRangeException(float widthInches, float heightInches, float dpi)
    {
        // Arrange
        var chart = new Chart(ChartType.Bar, [new ChartSeries("S1", [1.0])]);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRenderer.Render(chart, widthInches, heightInches, dpi));
    }

    /// <summary>Proves computed pixel dimensions exceeding <see cref="Surface.MaxDimension"/> are rejected.</summary>
    [Fact]
    public void Render_DpiOverloadExceedsMaxDimension_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var chart = new Chart(ChartType.Bar, [new ChartSeries("S1", [1.0])]);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartRenderer.Render(chart, 1000f, 1f, 100f));
    }

    /// <summary>Proves the DPI overload computes pixel dimensions as <c>round(inches * dpi)</c>.</summary>
    [Fact]
    public void Render_DpiOverload_ComputesExpectedPixelDimensions()
    {
        // Arrange
        var chart = new Chart(ChartType.Bar, [new ChartSeries("S1", [1.0])]);

        // Act
        using var surface = ChartRenderer.Render(chart, 2f, 1.5f, 100f);

        // Assert
        Assert.Equal(200, surface.Width);
        Assert.Equal(150, surface.Height);
    }

    /// <summary>Proves every defined <see cref="ChartType"/> renders successfully at a typical size.</summary>
    [Theory]
    [InlineData(ChartType.Bar)]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Area)]
    [InlineData(ChartType.Pie)]
    [InlineData(ChartType.Doughnut)]
    public void Render_EveryChartType_ProducesCorrectlySizedSurface(ChartType type)
    {
        // Arrange
        var series = new ChartSeries("S1", [3.0, 7.0], color: Red);
        var categoryAxis = new ChartAxis(["A", "B"]);
        var chart = new Chart(type, [series], type == ChartType.Pie || type == ChartType.Doughnut ? null : categoryAxis);

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 200);

        // Assert
        Assert.Equal(300, surface.Width);
        Assert.Equal(200, surface.Height);

        // The far corner is always outside every chart type's painted content for this chart
        // (no title/legend reserved, and no painter ever reaches a render target's absolute
        // top-left corner) - a deliberately type-agnostic smoke assertion that something sane
        // (the cleared background) survives untouched there.
        Assert.Equal(White, surface[1, 1]);
    }

    /// <summary>Proves a Bar (horizontal) chart's full-scale bar fills the plot area's interior.</summary>
    [Fact]
    public void Render_Bar_FullScaleBarPaintsInteriorPixel()
    {
        // Arrange
        var series = new ChartSeries("S1", [10.0], color: Red);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var chart = new Chart(ChartType.Bar, [series], new ChartAxis(["A"]), valueAxis);

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(Red, surface[200, 150]);

        // The bar's vertical extent never reaches the render target's top edge (it is inset by
        // 10% of its category band on each side), so the top-right corner remains background
        // regardless of how wide the (small, single-letter-label) left category-axis margin is.
        Assert.Equal(White, surface[398, 2]);
    }

    /// <summary>Proves a Column (vertical) chart's full-scale bar fills the plot area's interior.</summary>
    [Fact]
    public void Render_Column_FullScaleBarPaintsInteriorPixel()
    {
        // Arrange
        var series = new ChartSeries("S1", [10.0], color: Red);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var chart = new Chart(ChartType.Column, [series], new ChartAxis(["A"]), valueAxis);

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(Red, surface[200, 150]);

        // The bar's horizontal extent never reaches the render target's left edge (inset by 10%
        // of its category band, in addition to the reserved left value-axis margin), so the
        // top-left corner remains background.
        Assert.Equal(White, surface[2, 2]);
    }

    /// <summary>Proves a Line chart strokes a visible segment between two category points of differing value.</summary>
    [Fact]
    public void Render_Line_StrokesVisibleSegmentBetweenPoints()
    {
        // Arrange
        var series = new ChartSeries("S1", [0.0, 10.0], color: Blue);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var chart = new Chart(ChartType.Line, [series], new ChartAxis(["A", "B"]), valueAxis);

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        // The ascending diagonal (bottom-left to top-right) necessarily crosses the central
        // region of the plot; scanning a generous central window (rather than one exact pixel)
        // tolerates the left/bottom axis-margin sizes, which depend on bundled-font tick-label
        // metrics not replicated here.
        Assert.True(RegionContainsColor(surface, Blue, 120, 60, 280, 220));

        // Far corners the ascending line never reaches, regardless of margin size.
        Assert.Equal(White, surface[2, 2]);
        Assert.Equal(White, surface[398, 298]);
    }

    /// <summary>Proves an Area chart fills a translucent region beneath its line and strokes an opaque top edge.</summary>
    [Fact]
    public void Render_Area_FillsRegionAndStrokesTopEdge()
    {
        // Arrange
        var series = new ChartSeries("S1", [0.0, 10.0], color: new Rgba32(0, 255, 0, 255));
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var chart = new Chart(ChartType.Area, [series], new ChartAxis(["A", "B"]), valueAxis);

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        // The filled region blends translucent green over white, landing on neither endpoint;
        // assert only that it differs from the untouched background, without depending on the
        // exact alpha-blended byte values.
        var belowDiagonalMidpoint = surface[260, 260];
        Assert.NotEqual(White, belowDiagonalMidpoint);

        // The opaque top-edge stroke is pure, full-alpha green and is found somewhere along the
        // ascending diagonal's generous central window.
        Assert.True(RegionContainsColor(surface, new Rgba32(0, 255, 0, 255), 120, 60, 280, 220));
    }

    /// <summary>Proves a single-category Pie fills (almost) the entire wedge circle with that category's color.</summary>
    /// <remarks>
    ///     This is the key regression test for the full-circle degenerate-arc fix: a naive,
    ///     unsplit 360-degree <c>ArcTo</c> whose start and end points coincide would silently
    ///     vanish (see <c>ChartRenderer.PieChart.cs</c>'s own <c>AppendForwardArc</c> remarks), so
    ///     a single-category Pie would otherwise paint nothing at all.
    /// </remarks>
    [Fact]
    public void Render_Pie_SingleCategory_FillsEntireCircleWithoutDegenerateArc()
    {
        // Arrange
        var series = new ChartSeries("S1", [5.0], pointColors: [Red]);
        var chart = new Chart(ChartType.Pie, [series]);

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 300);

        // Assert
        // Directly above, below, left of, and right of center - every quadrant of the single
        // wedge - all resolve to the one category's color.
        Assert.Equal(Red, surface[150, 80]);
        Assert.Equal(Red, surface[150, 220]);
        Assert.Equal(Red, surface[80, 150]);
        Assert.Equal(Red, surface[220, 150]);

        // The far corner, outside the wedge circle's outer radius, remains background.
        Assert.Equal(White, surface[5, 5]);
    }

    /// <summary>Proves a two-category Pie splits into a right wedge and a left wedge, in category order.</summary>
    [Fact]
    public void Render_Pie_TwoEqualCategories_SplitsIntoRightAndLeftWedges()
    {
        // Arrange
        var series = new ChartSeries("S1", [1.0, 1.0], pointColors: [Red, Blue]);
        var chart = new Chart(ChartType.Pie, [series]);

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 300);

        // Assert
        // Wedge 0 sweeps from 12 o'clock to 6 o'clock clockwise through 3 o'clock (the right
        // half); wedge 1 continues from 6 o'clock back to 12 o'clock through 9 o'clock (the left
        // half) - see ChartRenderer.PieChart.cs's own angle-convention remarks.
        Assert.Equal(Red, surface[220, 150]);
        Assert.Equal(Blue, surface[80, 150]);
    }

    /// <summary>Proves a single-category Doughnut fills its ring but leaves the central hole as background.</summary>
    [Fact]
    public void Render_Doughnut_SingleCategory_FillsRingAndLeavesHoleUnpainted()
    {
        // Arrange
        var series = new ChartSeries("S1", [5.0], pointColors: [Red]);
        var chart = new Chart(ChartType.Doughnut, [series]);

        // Act
        using var surface = ChartRenderer.Render(chart, 300, 300);

        // Assert
        // The ring, roughly midway between the inner and outer radius (71 and 142 for a 300x300
        // target), is painted; the exact center (the hole) is not.
        Assert.Equal(Red, surface[150, 44]);
        Assert.Equal(White, surface[150, 150]);
    }

    /// <summary>Proves a custom <see cref="ChartRenderOptions.BackgroundColor"/> is used to clear the surface.</summary>
    [Fact]
    public void Render_CustomBackgroundColor_ClearsToThatColor()
    {
        // Arrange
        var chart = new Chart(ChartType.Bar, [new ChartSeries("S1", [1.0])]);
        var background = new Rgba32(10, 20, 30, 255);
        var options = new ChartRenderOptions { BackgroundColor = background };

        // Act
        using var surface = ChartRenderer.Render(chart, 100, 100, options);

        // Assert
        Assert.Equal(background, surface[1, 1]);
    }

    /// <summary>Proves a fully transparent <see cref="ChartRenderOptions.BackgroundColor"/> clears to transparent.</summary>
    [Fact]
    public void Render_TransparentBackgroundColor_ClearsToTransparent()
    {
        // Arrange
        var chart = new Chart(ChartType.Bar, [new ChartSeries("S1", [1.0])]);
        var options = new ChartRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };

        // Act
        using var surface = ChartRenderer.Render(chart, 100, 100, options);

        // Assert
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[1, 1]);
    }

    /// <summary>Proves series without an explicit color resolve, in series order, to <see cref="ChartColorPalette.Default"/>.</summary>
    [Fact]
    public void Render_SeriesWithoutExplicitColor_ResolvesToDefaultPaletteInOrder()
    {
        // Arrange
        var seriesA = new ChartSeries("A", [10.0]);
        var seriesB = new ChartSeries("B", [10.0]);
        var categoryAxis = new ChartAxis(["Only"]);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var chart = new Chart(ChartType.Column, [seriesA, seriesB], categoryAxis, valueAxis);

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        // Two series sharing one category are painted as two side-by-side bars within the same
        // category band; scan each half of the plot's central row for the corresponding default
        // palette entry.
        Assert.True(RegionContainsColor(surface, ChartColorPalette.Default[0], 40, 100, 195, 270));
        Assert.True(RegionContainsColor(surface, ChartColorPalette.Default[1], 205, 100, 395, 270));
    }

    /// <summary>Proves a chart-level color palette overrides <see cref="ChartColorPalette.Default"/>.</summary>
    [Fact]
    public void Render_ChartColorPaletteOverride_TakesPriorityOverDefaultPalette()
    {
        // Arrange
        var series = new ChartSeries("S1", [10.0]);
        var categoryAxis = new ChartAxis(["A"]);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var palette = new[] { new Rgba32(1, 2, 3, 255) };
        var chart = new Chart(ChartType.Column, [series], categoryAxis, valueAxis, colorPalette: palette);

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300);

        // Assert
        Assert.Equal(new Rgba32(1, 2, 3, 255), surface[200, 150]);
    }

    /// <summary>Proves an options-level color palette is used when the chart itself supplies none.</summary>
    [Fact]
    public void Render_OptionsColorPaletteOverride_UsedWhenChartSuppliesNone()
    {
        // Arrange
        var series = new ChartSeries("S1", [10.0]);
        var categoryAxis = new ChartAxis(["A"]);
        var valueAxis = new ChartAxis(minimum: 0f, maximum: 10f);
        var chart = new Chart(ChartType.Column, [series], categoryAxis, valueAxis);
        var options = new ChartRenderOptions { ColorPalette = [new Rgba32(4, 5, 6, 255)] };

        // Act
        using var surface = ChartRenderer.Render(chart, 400, 300, options);

        // Assert
        Assert.Equal(new Rgba32(4, 5, 6, 255), surface[200, 150]);
    }
}
