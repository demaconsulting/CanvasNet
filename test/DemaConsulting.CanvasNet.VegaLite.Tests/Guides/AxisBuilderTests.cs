using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="AxisBuilder"/>: ticks, labels, grid, domain, title and the extent an axis needs, with the
///     expectations derived by hand from the d3 tick algorithm and the Vega axis layout rules (not recorded from the code).
/// </summary>
public class AxisBuilderTests
{
    /// <summary>A scatter specification whose x domain is 0 to 100 and whose y domain is 0 to 50.</summary>
    private const string LinearSpec = """
        { "data": { "values": [ { "v": 0, "w": 0 }, { "v": 100, "w": 50 } ] },
          "mark": "point",
          "encoding": {
            "x": { "field": "v", "type": "quantitative" },
            "y": { "field": "w", "type": "quantitative" }
          } }
        """;

    /// <summary>A bar specification with three categories; the band scale of 300 pixels has a step of 100.</summary>
    private const string BandSpec = """
        { "data": { "values": [ { "k": "a", "n": 1 }, { "k": "b", "n": 2 }, { "k": "c", "n": 3 } ] },
          "mark": "bar",
          "encoding": {
            "x": { "field": "k", "type": "nominal" },
            "y": { "field": "n", "type": "quantitative" }
          } }
        """;

    /// <summary>
    ///     Builds a linear specification that puts an axis object on the x channel.
    /// </summary>
    /// <param name="axis">The JSON text of the <c>axis</c> property value.</param>
    /// <returns>The specification text.</returns>
    private static string LinearWithAxis(string axis) => LinearSpec.Replace(
        "\"x\": { \"field\": \"v\", \"type\": \"quantitative\" }",
        "\"x\": { \"field\": \"v\", \"type\": \"quantitative\", \"axis\": " + axis + " }",
        StringComparison.Ordinal);

    /// <summary>
    ///     Builds a band specification that puts an axis object on the x channel.
    /// </summary>
    /// <param name="axis">The JSON text of the <c>axis</c> property value.</param>
    /// <returns>The specification text.</returns>
    private static string BandWithAxis(string axis) => BandSpec.Replace(
        "\"x\": { \"field\": \"k\", \"type\": \"nominal\" }",
        "\"x\": { \"field\": \"k\", \"type\": \"nominal\", \"axis\": " + axis + " }",
        StringComparison.Ordinal);

    /// <summary>
    ///     Validates the ticks, labels, tick marks, grid and domain of linear axes against hand-derived vectors.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LinearAxisUsesExpectedTickValuesAndLabels()
    {
        // Arrange: x is 0 to 100 over 300 px (a tick count of ceil(300 / 40) = 8) and y is 0 to 50 over 200 px (count 5).
        // d3 ticks(0, 100, 8): span / count = 12.5, power 10, error 1.25 < sqrt(2), so the step is 10: 0, 10, ..., 100.
        // d3 ticks(0, 50, 5): span / count = 10, power 10, error 1, so the step is 10: 0, 10, ..., 50.
        var expectedX = Enumerable.Range(0, 11).Select(i => i * 10.0).ToList();
        var expectedY = Enumerable.Range(0, 6).Select(i => i * 10.0).ToList();

        // Act
        var x = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200)!;
        var y = ChartTestHelpers.Axis(LinearSpec, "y", 300, 200)!;

        // Assert: the x axis sits below the data rectangle, shifted by Vega's half pixel
        Assert.Equal("bottom", x.Orient);
        Assert.Equal(0.5, x.Group.X, 9);
        Assert.Equal(200.5, x.Group.Y, 9);
        Assert.Equal(expectedX, x.Ticks.Select(t => t.Value.Number));
        Assert.Equal(["0", "10", "20", "30", "40", "50", "60", "70", "80", "90", "100"], x.Ticks.Select(t => t.Label));

        // Assert: tick positions are 3 px per unit, rounded half up; labels sit at the unrounded position
        Assert.Equal(expectedX.Select(v => 3 * v), x.Ticks.Select(t => t.Position));
        var xLabels = ChartTestHelpers.Of<SceneText>(x.Group, "axis-label");
        Assert.Equal(expectedX.Select(v => 3 * v), xLabels.Select(l => l.X));
        Assert.All(xLabels, l => Assert.Equal(7, l.Y, 9));
        Assert.All(xLabels, l => Assert.Equal("top", l.Baseline));
        Assert.All(xLabels, l => Assert.Equal(0, l.Angle, 9));

        // Assert: labelFlush (on for a continuous x axis) aligns the end labels inward; the others are centered
        Assert.Equal("left", xLabels[0].Align);
        Assert.Equal("right", xLabels[^1].Align);
        Assert.All(xLabels.Skip(1).Take(9), l => Assert.Equal("center", l.Align));

        // Assert: tick marks are 5 px long, grid lines run the 200 px of the data rectangle upward, the domain spans the width
        var ticks = ChartTestHelpers.Of<SceneRule>(x.Group, "axis-tick");
        Assert.Equal(11, ticks.Count);
        Assert.All(ticks, t => Assert.Equal((0.0, 5.0, "#888"), (t.Y, t.Y2, t.Stroke)));
        Assert.Equal(expectedX.Select(v => 3 * v), ticks.Select(t => t.X));
        var grid = ChartTestHelpers.Of<SceneRule>(x.Grid!, "axis-grid");
        Assert.Equal(11, grid.Count);
        Assert.All(grid, g => Assert.Equal((0.0, -200.0, "#ddd"), (Math.Abs(g.Y), g.Y2, g.Stroke)));
        var domain = Assert.Single(ChartTestHelpers.Of<SceneRule>(x.Group, "axis-domain"));
        Assert.Equal((0.0, 0.0, 300.0, 0.0), (domain.X, domain.Y, domain.X2, domain.Y2));

        // Assert: the grid is a group of its own (drawn before the other axis parts); the others are ticks, labels, domain, title
        var roles = x.Group.Children.Select(c => c.Role).Distinct().ToList();
        Assert.Equal(["axis-tick", "axis-label", "axis-domain", "axis-title"], roles);
        Assert.Equal(("axis-grid-group", 0.5, 200.5), (x.Grid!.Role, x.Grid.X, x.Grid.Y));
        Assert.False(x.InFront);

        // Assert: the y axis is on the left; 4 px per unit upward; labels end 7 px left of the data rectangle, centered on the tick
        Assert.Equal("left", y.Orient);
        Assert.Equal(expectedY, y.Ticks.Select(t => t.Value.Number));
        Assert.Equal(["0", "10", "20", "30", "40", "50"], y.Ticks.Select(t => t.Label));
        Assert.Equal(expectedY.Select(v => 200 - (4 * v)), y.Ticks.Select(t => t.Position));
        var yLabels = ChartTestHelpers.Of<SceneText>(y.Group, "axis-label");
        Assert.All(yLabels, l => Assert.Equal((-7.0, "right", "middle"), (l.X, l.Align, l.Baseline)));
        var yTicks = ChartTestHelpers.Of<SceneRule>(y.Group, "axis-tick");
        Assert.All(yTicks, t => Assert.Equal((0.0, -5.0), (t.X, t.X2)));
        var yDomain = Assert.Single(ChartTestHelpers.Of<SceneRule>(y.Group, "axis-domain"));
        Assert.Equal((0.0, 200.0, 0.0, 0.0), (yDomain.X, yDomain.Y, yDomain.X2, yDomain.Y2));
        var yGrid = ChartTestHelpers.Of<SceneRule>(y.Grid!, "axis-grid");
        Assert.Equal(6, yGrid.Count);
        Assert.All(yGrid, g => Assert.Equal((0.0, 300.0), (g.X, g.X2)));
        Assert.Equal(expectedY.Select(v => 200 - (4 * v)), yGrid.Select(g => g.Y));
    }

    /// <summary>
    ///     Validates the categories, positions, labels and grid of band axes against hand-derived vectors.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_BandAxisUsesExpectedCategoriesAndGrid()
    {
        // Arrange: 3 categories over 300 px with the bar padding (inner 0.1, outer 0.05): step 300 / (3 - 0.1 + 0.1) = 100,
        // band width 90, first band at (300 - 100 * 2.9) / 2 = 5. A tick is at round(start + 45 - 0.5): 49.5 -> 50 (half up).
        var plain = ChartTestHelpers.Axis(BandSpec, "x", 300, 200)!;
        var rotated0 = ChartTestHelpers.Axis(BandWithAxis("{ \"labelAngle\": 0, \"grid\": true }"), "x", 300, 200)!;
        var filtered = ChartTestHelpers.Axis(BandWithAxis("{ \"values\": [\"c\", \"a\", \"zzz\"] }"), "x", 300, 200)!;

        // Assert: one tick per category in domain order, labels are the category text, no grid for a discrete scale
        Assert.Equal(["a", "b", "c"], plain.Ticks.Select(t => t.Label));
        Assert.Equal([50.0, 150.0, 250.0], plain.Ticks.Select(t => t.Position));
        Assert.Null(plain.Grid);

        // Assert: the labels use the unrounded center (5 + 45 - 0.5 = 49.5) and, for a discrete x axis, Vega-Lite's default angle of 270
        var labels = ChartTestHelpers.Of<SceneText>(plain.Group, "axis-label");
        Assert.Equal([49.5, 149.5, 249.5], labels.Select(l => l.X));
        Assert.All(labels, l => Assert.Equal((270.0, "right", "middle", 7.0), (l.Angle, l.Align, l.Baseline, l.Y)));

        // Assert: labelAngle 0 gives centered labels that hang below the tick, and grid: true draws a line at each tick position
        var upright = ChartTestHelpers.Of<SceneText>(rotated0.Group, "axis-label");
        Assert.All(upright, l => Assert.Equal((0.0, "center", "top"), (l.Angle, l.Align, l.Baseline)));
        var grid = ChartTestHelpers.Of<SceneRule>(rotated0.Grid!, "axis-grid");
        Assert.Equal([50.0, 150.0, 250.0], grid.Select(g => g.X));

        // Assert: values keep the listed categories that exist, in range order (a before c), and drop the others
        Assert.Equal(["a", "c"], filtered.Ticks.Select(t => t.Label));
        Assert.Equal([50.0, 250.0], filtered.Ticks.Select(t => t.Position));

        // Assert: a point scale has no band offset: positions are the scale positions
        var point = ChartTestHelpers.Axis(BandSpec.Replace("\"type\": \"nominal\"", "\"type\": \"nominal\", \"scale\": { \"type\": \"point\" }", StringComparison.Ordinal), "x", 300, 200)!;
        Assert.Equal(3, point.Ticks.Count);
        Assert.Equal([50.0, 150.0, 250.0], point.Ticks.Select(t => t.Position));
    }

    /// <summary>
    ///     Validates the title position and the extent of an axis against the Vega axis layout arithmetic.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_TitleAndExtentMatchAnalyticLayout()
    {
        // Arrange: bottom axis. Tick marks are rules from 0 to 5 that grow by half their 1 px width: bottom 5.5. A 10 px
        // label hangs from y = 5 + 2 = 7 (baseline top: Vega's offset of round(0.79 * 10) = 8 cancels the box offset
        // round(0.8 * 10) = 8), so its box is 7 to 17. The extent is 17; the 11 px title hangs from 17 + 4 = 21 and ends at 32.
        var bottom = ChartTestHelpers.Axis(LinearSpec, "x", 300, 200)!;
        var extents = AxisBuilder.GetExtents([bottom], 300, 200);
        var title = Assert.Single(ChartTestHelpers.Of<SceneText>(bottom.Group, "axis-title"));

        // Assert
        Assert.Equal(["v"], title.Lines);
        Assert.Equal((150.0, 21.0, "center", "top", 0.0), (title.X, title.Y, title.Align, title.Baseline, title.Angle));
        Assert.Equal(("bold", 11.0, "#000"), (title.FontWeight, title.FontSize, title.Fill));
        Assert.Equal(232.0, bottom.Bounds.Y2, 9);
        Assert.Equal(0.5, extents.Left, 9);
        Assert.Equal(0.5, extents.Right, 9);
        Assert.Equal(0.0, extents.Top, 9);
        Assert.Equal(32.0, extents.Bottom, 9);

        // Arrange: left axis. The labels are right aligned 7 px left of the data rectangle, so the extent is 7 plus the
        // widest label ("50" is the widest of 0, 10, ..., 50); the 11 px title is rotated by -90 degrees, its box
        // [-11, 0] below the baseline becomes [-11, 0] to the left of x = -(extent + 4).
        var measurer = new TextMeasurer();
        var widest = measurer.MeasureWidth("50", new TextFont("sans-serif", 10));
        var left = ChartTestHelpers.Axis(LinearSpec, "y", 300, 200)!;
        var leftExtents = AxisBuilder.GetExtents([left], 300, 200);
        var leftTitle = Assert.Single(ChartTestHelpers.Of<SceneText>(left.Group, "axis-title"));

        // Assert
        var extent = 7 + widest;
        Assert.Equal((-(extent + 4), 100.0, -90.0, "bottom"), (leftTitle.X, leftTitle.Y, leftTitle.Angle, leftTitle.Baseline));
        Assert.Equal(extent + 4 + 11, leftExtents.Left, 6);
        Assert.Equal(5.0, leftExtents.Top, 9);
        Assert.Equal(5.0, leftExtents.Bottom, 9);
        Assert.Equal(0.0, leftExtents.Right, 9);

        // Assert: minExtent pushes the title out, offset moves the whole axis away from the data rectangle
        var padded = ChartTestHelpers.Axis(LinearWithAxis("{ \"minExtent\": 40, \"offset\": 10 }"), "x", 300, 200)!;
        var paddedTitle = Assert.Single(ChartTestHelpers.Of<SceneText>(padded.Group, "axis-title"));
        Assert.Equal(44.0, paddedTitle.Y, 9);
        Assert.Equal(210.5, padded.Group.Y, 9);
        Assert.Equal(200 + 10 + 44 + 11, padded.Bounds.Y2, 9);
    }

    /// <summary>
    ///     Validates the other orientations and the properties that switch parts off.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_OrientAndPartSwitchesChangeTheItems()
    {
        // Arrange / Act
        var top = ChartTestHelpers.Axis(LinearWithAxis("{ \"orient\": \"top\", \"title\": \"Top\" }"), "x", 300, 200)!;
        var right = ChartTestHelpers.Axis(LinearSpec.Replace("\"y\": { \"field\": \"w\", \"type\": \"quantitative\" }", "\"y\": { \"field\": \"w\", \"type\": \"quantitative\", \"axis\": { \"orient\": \"right\" } }", StringComparison.Ordinal), "y", 300, 200)!;
        var bare = ChartTestHelpers.Axis(LinearWithAxis("{ \"domain\": false, \"ticks\": false, \"labels\": false, \"grid\": false, \"title\": null }"), "x", 300, 200)!;
        var hidden = ChartTestHelpers.Axis(LinearSpec.Replace("\"x\": { \"field\": \"v\", \"type\": \"quantitative\" }", "\"x\": { \"field\": \"v\", \"type\": \"quantitative\", \"axis\": null }", StringComparison.Ordinal), "x", 300, 200);

        // Assert: a top axis sits at the top edge with ticks pointing up and labels above them; its title is above the labels
        Assert.Equal("top", top.Orient);
        Assert.Equal(0.5, top.Group.Y, 9);
        Assert.All(ChartTestHelpers.Of<SceneRule>(top.Group, "axis-tick"), t => Assert.Equal(-5.0, t.Y2, 9));
        Assert.All(ChartTestHelpers.Of<SceneText>(top.Group, "axis-label"), l => Assert.Equal((-7.0, "bottom"), (l.Y, l.Baseline)));
        var topTitle = Assert.Single(ChartTestHelpers.Of<SceneText>(top.Group, "axis-title"));
        Assert.Equal(["Top"], topTitle.Lines);
        Assert.Equal((-21.0, "bottom"), (topTitle.Y, topTitle.Baseline));

        // Assert: a right axis sits at the right edge, its labels are left aligned and its title is rotated by +90 degrees
        Assert.Equal("right", right.Orient);
        Assert.Equal(300.5, right.Group.X, 9);
        Assert.All(ChartTestHelpers.Of<SceneText>(right.Group, "axis-label"), l => Assert.Equal((7.0, "left"), (l.X, l.Align)));
        Assert.Equal(90.0, Assert.Single(ChartTestHelpers.Of<SceneText>(right.Group, "axis-title")).Angle, 9);

        // Assert: every part can be switched off; with nothing to measure the axis has no extent
        Assert.Empty(bare.Group.Children);
        Assert.Null(bare.Grid);
        Assert.Equal(0, bare.ItemCount);
        Assert.Null(hidden);
    }

    /// <summary>
    ///     Validates that overlapping labels are removed with Vega's parity method and that the other label overlap settings apply.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_OverlappingLabelsAreRemovedByParity()
    {
        // Arrange: 21 ticks, 5 px apart (tickCount 20 over 100 px), each label wider than the spacing.
        // Parity keeps every other label until nothing overlaps: 21 -> 11 (every 10) -> 6 (0, 20, ..., 100; "80" and the
        // right aligned "100" still overlap) -> 3 (0, 40, 80), which no longer overlap. The hidden last label stays hidden.
        const string json = """
            { "data": { "values": [ { "v": 0 }, { "v": 100 } ] }, "mark": "point",
              "encoding": { "x": { "field": "v", "type": "quantitative", "axis": { "tickCount": 20 } } } }
            """;

        // Act
        var parity = ChartTestHelpers.Axis(json, "x", 100, 100)!;
        var none = ChartTestHelpers.Axis(json.Replace("\"tickCount\": 20", "\"tickCount\": 20, \"labelOverlap\": false", StringComparison.Ordinal), "x", 100, 100)!;

        // Assert
        Assert.Equal(21, parity.Ticks.Count);
        Assert.Equal(["0", "40", "80"], ChartTestHelpers.Of<SceneText>(parity.Group, "axis-label").Select(l => l.Text));
        Assert.Equal(21, ChartTestHelpers.Of<SceneText>(none.Group, "axis-label").Count);
        Assert.Equal(21, ChartTestHelpers.Of<SceneRule>(parity.Group, "axis-tick").Count);
    }

    /// <summary>
    ///     Validates explicit tick values, tick counts, d3 number formats, label flush and tick rounding.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_ValuesCountFormatAndFlushAreHonored()
    {
        // Act
        var values = ChartTestHelpers.Axis(LinearWithAxis("{ \"values\": [25, 0, 50, 500, 100], \"format\": \".1f\" }"), "x", 300, 200)!;
        var count = ChartTestHelpers.Axis(LinearWithAxis("{ \"tickCount\": 2 }"), "x", 300, 200)!;
        var percent = ChartTestHelpers.Axis(LinearWithAxis("{ \"format\": \"$,.2f\" }"), "x", 300, 200)!;
        var noFlush = ChartTestHelpers.Axis(LinearWithAxis("{ \"labelFlush\": false }"), "x", 300, 200)!;
        var unrounded = ChartTestHelpers.Axis(BandWithAxis("{ \"tickRound\": false }"), "x", 300, 200)!;

        // Assert: values are kept in range order, the one outside the scale (500) is dropped, and the format applies
        Assert.Equal(["0.0", "25.0", "50.0", "100.0"], values.Ticks.Select(t => t.Label));
        Assert.Equal([0.0, 75.0, 150.0, 300.0], values.Ticks.Select(t => t.Position));

        // Assert: d3 ticks(0, 100, 2): span / count = 50, power 10, error 5 >= sqrt(10) and < sqrt(50): step 50
        Assert.Equal(["0", "50", "100"], count.Ticks.Select(t => t.Label));

        // Assert: a $ format with a comma and two decimals
        Assert.Equal("$0.00", percent.Ticks[0].Label);
        Assert.Equal("$100.00", percent.Ticks[^1].Label);

        // Assert: without flush the end labels are centered on their ticks
        Assert.All(ChartTestHelpers.Of<SceneText>(noFlush.Group, "axis-label"), l => Assert.Equal("center", l.Align));

        // Assert: tickRound false keeps the fractional position: 49.5 instead of 50
        Assert.Equal([49.5, 149.5, 249.5], unrounded.Ticks.Select(t => t.Position));
    }

    /// <summary>
    ///     Validates the precedence of the axis configuration: axis, axisX, orient and band specific objects, and the explicit axis.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_ConfigurationPrecedenceFollowsVegaLite()
    {
        // Arrange: config.axis < config.axisX < config.axisBottom, and the axis of the channel wins over all of them
        const string config = """
            { "axis": { "tickSize": 9, "domainColor": "red", "labelFontSize": 12 },
              "axisX": { "tickSize": 8, "gridColor": "blue" },
              "axisBottom": { "tickSize": 7 },
              "axisQuantitative": { "titleFontSize": 14 } }
            """;

        // Act
        var axis = ChartTestHelpers.Axis(LinearWithAxis("{ \"domainWidth\": 3 }"), "x", 300, 200, config)!;
        var explicitSize = ChartTestHelpers.Axis(LinearWithAxis("{ \"tickSize\": 4 }"), "x", 300, 200, config)!;
        var band = ChartTestHelpers.Axis(BandSpec, "x", 300, 200, "{ \"axisBand\": { \"tickSize\": 6 } }")!;

        // Assert: the most specific configuration wins; properties nobody names keep Vega's defaults
        Assert.All(ChartTestHelpers.Of<SceneRule>(axis.Group, "axis-tick"), t => Assert.Equal(7.0, t.Y2, 9));
        Assert.Equal("red", Assert.Single(ChartTestHelpers.Of<SceneRule>(axis.Group, "axis-domain")).Stroke);
        Assert.Equal(3.0, Assert.Single(ChartTestHelpers.Of<SceneRule>(axis.Group, "axis-domain")).StrokeWidth);
        Assert.All(ChartTestHelpers.Of<SceneRule>(axis.Grid!, "axis-grid"), g => Assert.Equal("blue", g.Stroke));
        Assert.All(ChartTestHelpers.Of<SceneText>(axis.Group, "axis-label"), l => Assert.Equal(12.0, l.FontSize, 9));
        Assert.Equal(14.0, Assert.Single(ChartTestHelpers.Of<SceneText>(axis.Group, "axis-title")).FontSize, 9);
        Assert.All(ChartTestHelpers.Of<SceneRule>(explicitSize.Group, "axis-tick"), t => Assert.Equal(4.0, t.Y2, 9));
        Assert.All(ChartTestHelpers.Of<SceneRule>(band.Group, "axis-tick"), t => Assert.Equal(6.0, t.Y2, 9));
    }

    /// <summary>
    ///     Validates the z-order of axes: behind the marks by default, in front of a rect mark with a discrete channel or with an explicit z-index.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_AxesOfRectMarksWithDiscreteChannelsAreInFront()
    {
        // Arrange
        const string rect = """
            { "data": { "values": [ { "a": "p", "b": "u" } ] }, "mark": "rect",
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "b", "type": "nominal" } } }
            """;

        // Act
        var rectAxis = ChartTestHelpers.Axis(rect, "x", 100, 100)!;
        var barAxis = ChartTestHelpers.Axis(BandSpec, "x", 300, 200)!;
        var raised = ChartTestHelpers.Axis(LinearWithAxis("{ \"zindex\": 1 }"), "x", 300, 200)!;
        var lowered = ChartTestHelpers.Axis(rect.Replace("\"field\": \"a\", \"type\": \"nominal\"", "\"field\": \"a\", \"type\": \"nominal\", \"axis\": { \"zindex\": 0 }", StringComparison.Ordinal), "x", 100, 100)!;

        // Assert
        Assert.True(rectAxis.InFront);
        Assert.False(barAxis.InFront);
        Assert.True(raised.InFront);
        Assert.False(lowered.InFront);
    }

    /// <summary>
    ///     Validates that invalid orients and formats fail with a location, and that tick and cancellation limits hold.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations()
    {
        // Act
        var orient = Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Axis(LinearWithAxis("{ \"orient\": \"left\" }"), "x", 300, 200));
        var format = Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Axis(LinearWithAxis("{ \"format\": \"!!\" }"), "x", 300, 200));
        var limit = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(LinearWithAxis("{ \"tickCount\": 100 }"), "x", 3000, 200, limits: new VegaLiteLimits { MaxTicks = 5 }));
        var bandLimit = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(BandSpec, "x", 300, 200, limits: new VegaLiteLimits { MaxTicks = 2 }));

        // Assert: each error points into the specification
        Assert.Equal("/encoding/x/axis/orient", orient.Location.Pointer);
        Assert.Equal("/encoding/x/axis/format", format.Location.Pointer);
        Assert.Equal("test.vl.json", format.Location.File);
        Assert.True(format.Location.Line >= 1);
        Assert.Equal("/encoding/x", limit.Location.Pointer);
        Assert.Equal("/encoding/x", bandLimit.Location.Pointer);
        Assert.Contains("MaxTicks", limit.Message, StringComparison.Ordinal);

        // Assert: cancellation is honored
        var parts = ChartTestHelpers.Parts(LinearSpec);
        var scale = parts.Scales["x"]!;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var builder = new AxisBuilder(new TextMeasurer(), parts.Config);
        Assert.Throws<OperationCanceledException>(() => builder.Build(new AxisRequest("x", parts.Tree.Root["encoding"]?["x"], scale, scale.CreateModel(300), 300, 200), canceled.Token));
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that the axis title of an aggregated, binned or counted channel is the default title of the plan (Vega-Lite's <c>Sum of v</c>,
    ///     <c>v (binned)</c>, <c>Count of Records</c>), that an explicit title still wins, and that a count (a definition with no field) has a grid.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_TitlesFollowThePlanAndACountHasAGrid()
    {
        // Arrange
        const string Rows = "\"data\":{\"values\":[{\"k\":\"a\",\"v\":1},{\"k\":\"b\",\"v\":4}]}";
        string Spec(string x, string y) => "{" + Rows + ",\"mark\":\"bar\",\"encoding\":{\"x\":" + x + ",\"y\":" + y + "}}";
        const string Category = "{\"field\":\"k\",\"type\":\"nominal\"}";

        // Act
        var sum = ChartTestHelpers.Axis(Spec(Category, "{\"field\":\"v\",\"type\":\"quantitative\",\"aggregate\":\"sum\"}"), "y", 300, 200)!;
        var count = ChartTestHelpers.Axis(Spec(Category, "{\"aggregate\":\"count\",\"type\":\"quantitative\"}"), "y", 300, 200)!;
        var binned = ChartTestHelpers.Axis(Spec("{\"field\":\"v\",\"type\":\"quantitative\",\"bin\":true}", "{\"aggregate\":\"count\",\"type\":\"quantitative\"}"), "x", 300, 200)!;
        var explicitTitle = ChartTestHelpers.Axis(Spec(Category, "{\"field\":\"v\",\"type\":\"quantitative\",\"aggregate\":\"sum\",\"title\":\"Total\"}"), "y", 300, 200)!;

        // Assert
        Assert.Equal("Sum of v", ChartTestHelpers.Of<SceneText>(sum.Group, "axis-title").Single().Text);
        Assert.Equal("Count of Records", ChartTestHelpers.Of<SceneText>(count.Group, "axis-title").Single().Text);
        Assert.Equal("v (binned)", ChartTestHelpers.Of<SceneText>(binned.Group, "axis-title").Single().Text);
        Assert.Equal("Total", ChartTestHelpers.Of<SceneText>(explicitTitle.Group, "axis-title").Single().Text);
        Assert.NotNull(count.Grid);
        Assert.NotNull(sum.Grid);
        Assert.Null(binned.Grid);
    }

    /// <summary>
    ///     Validates the default tick count of a binned axis: Vega-Lite asks for one tick per ten pixels when the encoding bins the field (200 px: 20, which d3 turns
    ///     into a step of 2 over the bins 0 to 35), an explicit <c>tickCount</c> wins, and a field that is binned already keeps the count of one tick per 40 pixels (5: a step of 5).
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_BinnedAxisAsksForATickPerTenPixels()
    {
        // Arrange: twelve weights binned into 0 to 35 by 5 (ten bins asked for)
        const string Rows = "\"data\":{\"values\":[{\"w\":1.5},{\"w\":2.5},{\"w\":3},{\"w\":7},{\"w\":8.5},{\"w\":12},{\"w\":14},{\"w\":14.5},{\"w\":18},{\"w\":22},{\"w\":25},{\"w\":31}]}";
        string Histogram(string xExtra) => "{" + Rows + ",\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"w\",\"type\":\"quantitative\",\"bin\":true" + xExtra + "},\"y\":{\"aggregate\":\"count\",\"type\":\"quantitative\"}}}";

        // Act
        var binning = ChartTestHelpers.Axis(Histogram(string.Empty), "x", 200, 100)!;
        var explicitCount = ChartTestHelpers.Axis(Histogram(",\"axis\":{\"tickCount\":5}"), "x", 200, 100)!;
        var alreadyBinned = ChartTestHelpers.Axis(
            "{\"data\":{\"values\":[{\"s\":0,\"e\":10,\"n\":4},{\"s\":10,\"e\":30,\"n\":7}]},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"s\",\"type\":\"quantitative\",\"bin\":\"binned\"},\"x2\":{\"field\":\"e\"},\"y\":{\"field\":\"n\",\"type\":\"quantitative\"}}}",
            "x",
            200,
            100)!;

        // Assert: 0, 2, ... 34 (the step 2 of the d3 algorithm for about 20 ticks of 0 to 35), 0, 5, ... 35, and 0, 5, ... 30 for the domain 0 to 30
        Assert.Equal(Enumerable.Range(0, 18).Select(i => 2.0 * i), binning.Ticks.Select(t => t.Value.Number));
        Assert.Equal(Enumerable.Range(0, 8).Select(i => 5.0 * i), explicitCount.Ticks.Select(t => t.Value.Number));
        Assert.Equal(Enumerable.Range(0, 7).Select(i => 5.0 * i), alreadyBinned.Ticks.Select(t => t.Value.Number));
    }

    /// <summary>
    ///     Validates that the axis of a normalized stack shows percentages (the <c>normalizedNumberFormat</c> of the configuration, <c>.0%</c> by default), that an
    ///     explicit axis format and the configuration win, and that an ordinary stack keeps plain numbers.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_NormalizedStackUsesTheNormalizedNumberFormat()
    {
        // Arrange: three layers in two stacks; a normalized y domain is 0 to 1, so a 100 px axis has the ticks 0, 0.5 and 1
        const string Rows = "\"data\":{\"values\":[{\"k\":\"a\",\"c\":\"p\",\"n\":3},{\"k\":\"a\",\"c\":\"q\",\"n\":5},{\"k\":\"b\",\"c\":\"p\",\"n\":4}]}";
        string Stacked(string yExtra, string config = "") => "{" + Rows + config + ",\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"n\",\"type\":\"quantitative\"" + yExtra + "},\"color\":{\"field\":\"c\",\"type\":\"nominal\"}}}";

        // Act
        var normalized = ChartTestHelpers.Axis(Stacked(",\"stack\":\"normalize\""), "y", 100, 100)!;
        var configured = ChartTestHelpers.Axis(Stacked(",\"stack\":\"normalize\"", ",\"config\":{\"normalizedNumberFormat\":\".1%\"}"), "y", 100, 100)!;
        var explicitFormat = ChartTestHelpers.Axis(Stacked(",\"stack\":\"normalize\",\"axis\":{\"format\":\".2f\"}"), "y", 100, 100)!;
        var plain = ChartTestHelpers.Axis(Stacked(string.Empty), "y", 100, 100)!;

        // Assert
        Assert.Equal(["0%", "50%", "100%"], normalized.Ticks.Select(t => t.Label));
        Assert.Equal(["0.0%", "50.0%", "100.0%"], configured.Ticks.Select(t => t.Label));
        Assert.Equal(["0.00", "0.50", "1.00"], explicitFormat.Ticks.Select(t => t.Label));
        Assert.DoesNotContain(plain.Ticks, t => t.Label.Contains('%', StringComparison.Ordinal));
        Assert.Equal(["n"], [ChartTestHelpers.Of<SceneText>(plain.Group, "axis-title").Single().Text]);
    }
}
