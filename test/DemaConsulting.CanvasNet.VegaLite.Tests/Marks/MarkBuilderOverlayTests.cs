using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the path overlays of <see cref="MarkBuilder"/>: the <c>point</c> overlay of a line, area, trail or rule and the <c>line</c> overlay of an area,
///     with the expectations derived by hand from the scales of a 300 by 200 chart (x(v) = 150 v and y(v) = 200 - 20 v for the points (0, 0), (1, 10), (2, 5)).
/// </summary>
public class MarkBuilderOverlayTests
{
    /// <summary>The default color of a mark.</summary>
    private const string Blue = "#4c78a8";

    /// <summary>The second color of the tableau10 palette.</summary>
    private const string Orange = "#f58518";

    /// <summary>Two quantitative position channels.</summary>
    private const string XY = """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } }""";

    /// <summary>Three points: x 0, 1, 2 and y 0, 10, 5.</summary>
    private const string Basic = """[ { "x": 0, "y": 0 }, { "x": 1, "y": 10 }, { "x": 2, "y": 5 } ]""";

    /// <summary>
    ///     Builds a specification of a mark in a 300 by 200 view.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="config">The JSON text of the <c>config</c> object, or null.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string encoding, string values, string? config = null) =>
        "{ \"width\": 300, \"height\": 200, \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": " + encoding
        + (config is null ? string.Empty : ", \"config\": " + config) + " }";

    /// <summary>
    ///     Validates the point overlay of a line, area, trail and rule: one symbol for every row after the base items, with the definition of a filled point mark (and
    ///     the overlay object replacing its properties), not inheriting the color of the base mark.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PointOverlayDrawsSymbolsWithTheOverlayDefinition()
    {
        // Arrange: the line with its point overlay, charged to an explicit budget
        var parts = ChartTestHelpers.Parts(Chart("""{ "type": "line", "color": "green", "point": true }""", XY, Basic));
        var budget = new SceneBudget(1000);

        // Act
        var group = new MarkBuilder(parts.Config, null, budget, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken);

        // Assert: the line comes first, in the color of the mark; then three filled points in the default color (the color of the mark is not inherited)
        Assert.Equal(["mark-line", "mark-point", "mark-point", "mark-point"], group.Children.Select(c => c.Role));
        Assert.Equal(("green", "M0,200 L150,0 L300,100"), (group.Children[0].Stroke, PathText.Of((ScenePath)group.Children[0])));
        var points = ChartTestHelpers.Of<SceneSymbol>(group, "mark-point");
        Assert.Equal([(0.0, 200.0), (150.0, 0.0), (300.0, 100.0)], points.Select(p => (p.X, p.Y)));
        Assert.All(points, p => Assert.Equal((Blue, (string?)null, 1.0, 30.0, "circle"), (p.Fill, p.Stroke, p.Opacity, p.Size, p.Shape)));

        // Assert: the marks group, the path (1 + 3 + 1) and one item for every symbol were charged
        Assert.Equal(1 + 5 + 3, budget.Count);

        // Act: an overlay object replaces the properties of the point
        var custom = ChartTestHelpers.Of<SceneSymbol>(
            ChartTestHelpers.Marks(Chart("""{ "type": "line", "point": { "color": "red", "size": 100, "shape": "square", "opacity": 0.5 } }""", XY, Basic), 300, 200),
            "mark-point");

        // Assert
        Assert.Equal(3, custom.Count);
        Assert.All(custom, p => Assert.Equal(("red", 100.0, "square", 0.5), (p.Fill, p.Size, p.Shape, p.Opacity)));

        // Assert: a point overlay of false or null is no overlay
        Assert.Single(ChartTestHelpers.Marks(Chart("""{ "type": "line", "point": false }""", XY, Basic), 300, 200).Children);
        Assert.Single(ChartTestHelpers.Marks(Chart("""{ "type": "line", "point": null }""", XY, Basic), 300, 200).Children);

        // Assert: an overlay point is drawn for every row that has a position (a row without a y has no point, as for the point mark)
        const string gap = """[ { "x": 0, "y": 0 }, { "x": 1, "y": null }, { "x": 2, "y": 5 } ]""";
        Assert.Equal(2, ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Chart("""{ "type": "line", "point": true }""", XY, gap), 300, 200), "mark-point").Count);

        // Act: the rule and the trail have point overlays too; a rule with only x has its points at the middle of the height
        const string ruleChart = """{ "x": { "field": "x", "type": "quantitative" } }""";
        var ruled = ChartTestHelpers.Marks(Chart("""{ "type": "rule", "point": true }""", ruleChart, """[ { "x": 1 }, { "x": 2 }, { "x": 3 } ]"""), 300, 200);
        Assert.Equal(["mark-rule", "mark-rule", "mark-rule", "mark-point", "mark-point", "mark-point"], ruled.Children.Select(c => c.Role));
        Assert.Equal([(100.0, 100.0), (200.0, 100.0), (300.0, 100.0)], ChartTestHelpers.Of<SceneSymbol>(ruled, "mark-point").Select(p => (p.X, p.Y)));
        var trailed = ChartTestHelpers.Marks(Chart("""{ "type": "trail", "point": true }""", XY, Basic), 300, 200);
        Assert.Equal(["mark-trail", "mark-point", "mark-point", "mark-point"], trailed.Children.Select(c => c.Role));
    }

    /// <summary>
    ///     Validates the line overlay of an area (the top edge of the area as a line with the interpolation of the area) and the default opacity 0.7 of an area that has an overlay.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_AreaLineOverlayAndDefaultOpacity()
    {
        // Act
        var group = ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": true }""", XY, Basic), 300, 200);

        // Assert: the area first (with the reduced opacity), then the line along its top edge in the default color and the width of the line style
        Assert.Equal(["mark-area", "mark-line"], group.Children.Select(c => c.Role));
        var area = (ScenePath)group.Children[0];
        var line = (ScenePath)group.Children[1];
        Assert.Equal("M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z", PathText.Of(area));
        Assert.Equal((Blue, 0.7), (area.Fill, area.Opacity));
        Assert.Equal("M0,200 L150,0 L300,100", PathText.Of(line));
        Assert.Equal((Blue, (string?)null, 2.0, 1.0), (line.Stroke, line.Fill, line.StrokeWidth, line.Opacity));

        // Assert: the overlay object replaces the properties of the line
        var red = (ScenePath)ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": { "color": "red", "strokeWidth": 4 } }""", XY, Basic), 300, 200).Children[1];
        Assert.Equal(("red", 4.0), (red.Stroke, red.StrokeWidth));

        // Assert: the line takes the interpolation of the area (the step curve at the middle between two points)
        var step = (ScenePath)ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": true, "interpolate": "step" }""", XY, Basic), 300, 200).Children[1];
        Assert.Equal("M0,200 L75,200 L75,0 L225,0 L225,100 L300,100", PathText.Of(step));

        // Assert: an opacity (or a fill opacity) of the mark keeps the area as it says; the point overlay reduces the opacity of an area too
        Assert.Equal(0.5, ((ScenePath)ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": true, "opacity": 0.5 }""", XY, Basic), 300, 200).Children[0]).Opacity);
        var filled = (ScenePath)ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": true, "fillOpacity": 0.4 }""", XY, Basic), 300, 200).Children[0];
        Assert.Equal((1.0, 0.4), (filled.Opacity, filled.FillOpacity));
        Assert.Equal(0.7, ((ScenePath)ChartTestHelpers.Marks(Chart("""{ "type": "area", "point": true }""", XY, Basic), 300, 200).Children[0]).Opacity);

        // Assert: without an overlay (or with it switched off) the area is opaque
        Assert.Equal(1.0, ((ScenePath)ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": false, "point": false }""", XY, Basic), 300, 200).Children[0]).Opacity);

        // Assert: a clip of the mark clips the overlay too (it is one marks group)
        Assert.NotNull(ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": true, "clip": true }""", XY, Basic), 300, 200).Clip);

        // Arrange: a stack of two areas: the line of each layer is at the stacked end of its layer: the lower layer b ends at 1 (y(v) = 200 - 100 v), the upper layer a at 2
        const string rows = """[ { "x": 0, "y": 1, "g": "a" }, { "x": 1, "y": 1, "g": "a" }, { "x": 0, "y": 1, "g": "b" }, { "x": 1, "y": 1, "g": "b" } ]""";
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }
            """;

        // Act
        var stacked = ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": true }""", encoding, rows), 300, 200);
        var lines = ChartTestHelpers.Of<ScenePath>(stacked, "mark-line");

        // Assert: the lines follow the groups of the areas (a, then b) and run along the upper end of their layer
        Assert.Equal(["M0,0 L300,0", "M0,100 L300,100"], lines.Select(PathText.Of));
        Assert.Equal([Blue, Orange], lines.Select(l => l.Stroke));
        Assert.Equal(2, ChartTestHelpers.Of<ScenePath>(stacked, "mark-area").Count);
    }

    /// <summary>
    ///     Validates the overlays that the configuration enables (<c>config.&lt;mark&gt;.point</c> and <c>.line</c>), the <c>"transparent"</c> points and the switch-off by the mark.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_OverlayFromConfigAndTransparentPoints()
    {
        // Act: the configuration of the area enables both overlays; the point object of the configuration colors the points
        var group = ChartTestHelpers.Marks(Chart("\"area\"", XY, Basic, """{ "area": { "line": true, "point": { "color": "black" } } }"""), 300, 200);

        // Assert: the area, its line and three black points
        Assert.Equal(["mark-area", "mark-line", "mark-point", "mark-point", "mark-point"], group.Children.Select(c => c.Role));
        Assert.All(ChartTestHelpers.Of<SceneSymbol>(group, "mark-point"), p => Assert.Equal("black", p.Fill));
        Assert.Equal(0.7, ((ScenePath)group.Children[0]).Opacity);

        // Assert: the configuration of the line mark enables the points (true: the default point)
        var line = ChartTestHelpers.Marks(Chart("\"line\"", XY, Basic, """{ "line": { "point": true } }"""), 300, 200);
        Assert.Equal(["mark-line", "mark-point", "mark-point", "mark-point"], line.Children.Select(c => c.Role));

        // Assert: the mark definition switches the overlay of the configuration off
        Assert.Single(ChartTestHelpers.Marks(Chart("""{ "type": "line", "point": false }""", XY, Basic, """{ "line": { "point": true } }"""), 300, 200).Children);
        Assert.Single(ChartTestHelpers.Marks(Chart("""{ "type": "area", "line": false, "point": false }""", XY, Basic, """{ "area": { "line": true, "point": true } }"""), 300, 200).Children);

        // Assert: "transparent" draws the points with an opacity of 0 (they exist, as they would catch the pointer of a tooltip), the other properties are those of the point overlay
        var transparent = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Chart("""{ "type": "line", "point": "transparent" }""", XY, Basic), 300, 200), "mark-point");
        Assert.Equal(3, transparent.Count);
        Assert.All(transparent, p => Assert.Equal((Blue, 0.0), (p.Fill, p.Opacity)));

        // Assert: the overlay of a color field has the color of the group of each point
        const string rows = """[ { "x": 0, "y": 1, "g": "a" }, { "x": 1, "y": 2, "g": "b" } ]""";
        const string colored = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }
            """;
        var points = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Chart("""{ "type": "line", "point": true }""", colored, rows), 300, 200), "mark-point");
        Assert.Equal([Blue, Orange], points.Select(p => p.Fill));
    }
}
