using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the brush of an interval selection in its initial state (Phase 12, Vega-Lite's <c>interval.ts</c> <c>marks</c>): charts are laid out through the public
///     API and the geometry, paint, clip, order and owner of the brush groups are checked against positions worked out by hand from the scales.
/// </summary>
/// <remarks>
///     The charts are built so that the scales are exact: a quantitative x field from 0 to 10 over 300 pixels maps a value v to 30 v, and a y field from 0 to 5 over 200 pixels maps v to 200 - 40 v (the
///     extremes are in the data, so the zero rule and <c>nice</c> leave the domains alone).
/// </remarks>
public class VegaLiteBrushIntegrationTests
{
    /// <summary>The rows of the scatter plots: the extremes decide the domains, x from 0 to 10 and y from 0 to 5.</summary>
    private const string Rows = """ "data": { "values": [ { "a": 0, "b": 0, "k": "a" }, { "a": 10, "b": 5, "k": "c" }, { "a": 4, "b": 2, "k": "b" } ] } """;

    /// <summary>The position encoding of the scatter plots.</summary>
    private const string Positions = """ "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } """;

    /// <summary>The roles of the brush groups and of the marks groups, which the tests list in drawing order.</summary>
    private static readonly string[] GroupRoles = ["selection-brush-background", "marks", "selection-brush"];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a scatter plot of the three rows with a parameter and the given extra top-level properties.
    /// </summary>
    /// <param name="parameter">The parameter object (written out), or an empty text for none.</param>
    /// <param name="extra">More top-level properties, each ending with a comma (such as a <c>config</c>).</param>
    /// <returns>The specification text.</returns>
    private static string Scatter(string parameter, string extra = "") =>
        "{ " + extra + (parameter.Length == 0 ? string.Empty : "\"params\": [ " + parameter + " ], ") + "\"width\": 300, \"height\": 200, " + Rows + ", \"mark\": \"point\", " + Positions + " }";

    /// <summary>
    ///     Lays a chart out.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="options">The render options, or null for the defaults.</param>
    /// <returns>The scene.</returns>
    private static Scene Build(string json, VegaLiteRenderOptions? options = null) =>
        VegaLiteDocument.Parse(json).BuildScene(options ?? new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Lists the brush groups and the marks groups of a scene in drawing order, with the offset of their coordinate system.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The groups.</returns>
    private static List<(SceneGroup Group, double OffsetX)> Groups(Scene scene) =>
        [.. scene.Enumerate().Where(e => e.Item is SceneGroup && GroupRoles.Contains(e.Item.Role)).Select(e => ((SceneGroup)e.Item, e.OffsetX))];

    /// <summary>
    ///     Gets the rectangle that a brush group holds.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The rectangle.</returns>
    private static SceneRect RectOf(SceneGroup group) => Assert.IsType<SceneRect>(Assert.Single(group.Children));

    /// <summary>
    ///     Validates the brush of an interval selection over x and y: the shade and the outline both cover the rectangle from the first to the second end of the range of each channel (x(2) = 60,
    ///     x(6) = 180, y(3) = 80, y(1) = 160), are clipped to the data rectangle and are drawn around the marks.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_CoversTheRangeOfBothChannels()
    {
        // Arrange
        var json = Scatter("""{ "name": "brush", "select": "interval", "value": { "x": [2, 6], "y": [1, 3] } }""");

        // Act
        var groups = Groups(Build(json));

        // Assert: shade, marks, outline, in that order
        Assert.Equal(GroupRoles, groups.Select(g => g.Group.Role));
        var shade = RectOf(groups[0].Group);
        var outline = RectOf(groups[2].Group);
        foreach (var rect in new[] { shade, outline })
        {
            Assert.Equal((60.0, 80.0, 120.0, 80.0), (rect.X, rect.Y, rect.Width, rect.Height));
        }

        // The shade is the fill of the default mark of the interval selection, without a stroke; the outline is transparent with a white stroke
        Assert.Equal(("#333", 0.125, null), (shade.Fill, shade.FillOpacity, shade.Stroke));
        Assert.Equal(("transparent", "white", null, 1.0), (outline.Fill, outline.Stroke, outline.StrokeWidth, outline.StrokeOpacity));
        foreach (var group in new[] { groups[0].Group, groups[2].Group })
        {
            Assert.Equal(new SceneClip(0, 0, 300, 200), group.Clip);
        }
    }

    /// <summary>
    ///     Validates that a selection projected over x only spans the data rectangle on y: 0 to 200.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_AChannelThatIsNotProjectedSpansTheDataRectangle()
    {
        // Arrange
        var json = Scatter("""{ "name": "brush", "select": "interval", "value": { "x": [2, 6] } }""");

        // Act
        var rect = RectOf(Groups(Build(json))[0].Group);

        // Assert
        Assert.Equal((60.0, 0.0, 120.0, 200.0), (rect.X, rect.Y, rect.Width, rect.Height));
    }

    /// <summary>
    ///     Validates that a selection projected over y only spans the data rectangle on x: 0 to 300.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_AYOnlySelectionSpansTheWidth()
    {
        // Arrange
        var json = Scatter("""{ "name": "brush", "select": "interval", "value": { "y": [1, 3] } }""");

        // Act
        var rect = RectOf(Groups(Build(json))[0].Group);

        // Assert
        Assert.Equal((0.0, 80.0, 300.0, 80.0), (rect.X, rect.Y, rect.Width, rect.Height));
    }

    /// <summary>
    ///     Validates that the ends of a range may come in either order: [6, 2] draws the rectangle of [2, 6].
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_ReversedEndsDrawTheSameRectangle()
    {
        // Arrange
        var json = Scatter("""{ "name": "brush", "select": "interval", "value": { "x": [6, 2], "y": [3, 1] } }""");

        // Act
        var rect = RectOf(Groups(Build(json))[0].Group);

        // Assert
        Assert.Equal((60.0, 80.0, 120.0, 80.0), (rect.X, rect.Y, rect.Width, rect.Height));
    }

    /// <summary>
    ///     Validates the brush over a temporal axis: the ends are date-time objects (read in the time zone of the chart, UTC by default) and the data runs from the first to the eleventh of January, so
    ///     the third maps to 60 and the ninth to 240 of 300 pixels.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_DatesMapThroughTheTimeScale()
    {
        // Arrange
        var json = """
            { "width": 300, "height": 200,
              "params": [ { "name": "brush", "select": "interval",
                            "value": { "x": [ { "year": 2020, "month": "jan", "date": 3 }, { "year": 2020, "month": "jan", "date": 9 } ] } } ],
              "data": { "values": [ { "d": "2020-01-01" }, { "d": "2020-01-11" } ] },
              "mark": "point",
              "encoding": { "x": { "field": "d", "type": "temporal" } } }
            """;

        // Act
        var rect = RectOf(Groups(Build(json))[0].Group);

        // Assert
        Assert.Equal((60.0, 180.0), (rect.X, rect.Width));
    }

    /// <summary>
    ///     Validates the brush over a band scale: each end is the <b>start</b> of the band of its value (three bands of 100 pixels with no padding start at 0, 100 and 200), so the brush from "a" to
    ///     "c" runs from 0 to 200, not to the end of the band of "c".
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_BandEndsAreTheStartsOfTheirBands()
    {
        // Arrange
        var json = """
            { "width": 300, "height": 200,
              "params": [ { "name": "brush", "select": "interval", "value": { "x": ["a", "c"] } } ],
              "data": { "values": [ { "k": "a", "b": 1 }, { "k": "b", "b": 2 }, { "k": "c", "b": 3 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal", "scale": { "paddingInner": 0, "paddingOuter": 0 } }, "y": { "field": "b", "type": "quantitative" } } }
            """;

        // Act
        var rect = RectOf(Groups(Build(json))[0].Group);

        // Assert: y is not projected
        Assert.Equal((0.0, 0.0, 200.0, 200.0), (rect.X, rect.Y, rect.Width, rect.Height));
    }

    /// <summary>
    ///     Validates that a range without an extent on a projected axis paints nothing, so no item is made: x [3, 3] has no width.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_ARangeWithoutExtentIsNotDrawn()
    {
        // Arrange
        var json = Scatter("""{ "name": "brush", "select": "interval", "value": { "x": [3, 3], "y": [1, 3] } }""");

        // Act
        var groups = Groups(Build(json));

        // Assert: only the marks
        Assert.Equal(["marks"], groups.Select(g => g.Group.Role));
    }

    /// <summary>
    ///     Validates that an interval selection without an initial value draws no brush.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_NoInitialValueDrawsNothing()
    {
        // Arrange
        var json = Scatter("""{ "name": "brush", "select": "interval" }""");

        // Act
        var groups = Groups(Build(json));

        // Assert
        Assert.Equal(["marks"], groups.Select(g => g.Group.Role));
    }

    /// <summary>
    ///     Validates that a point selection draws no brush, and an interval selection bound to the scales draws none either.
    /// </summary>
    /// <param name="parameter">The parameter object.</param>
    [Theory]
    [InlineData("""{ "name": "brush", "select": { "type": "point", "fields": ["k"] }, "value": [ { "k": "a" } ] }""")]
    [InlineData("""{ "name": "brush", "select": "interval", "bind": "scales", "value": { "x": [2, 6] } }""")]
    public void CanvasNetVegaLite_SystemIntegration_Brush_OnlyAnUnboundIntervalSelectionDrawsOne(string parameter)
    {
        // Arrange / Act
        var groups = Groups(Build(Scatter(parameter)));

        // Assert
        Assert.Equal(["marks"], groups.Select(g => g.Group.Role));
    }

    /// <summary>
    ///     Validates the <c>mark</c> of the selection: its <c>fill</c> and <c>fillOpacity</c> paint the shade, every other key is the stroke of the outline (the <c>cursor</c> is not), and the keys of
    ///     the default mark that the selection does not repeat stay.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_TheMarkOfTheSelectionPaintsTheBrush()
    {
        // Arrange: the stroke is overridden, the dash and the width are added; the default fill stays
        var json = Scatter("""
            { "name": "brush", "select": { "type": "interval", "mark": { "fill": "red", "fillOpacity": 0.3, "stroke": "blue", "strokeWidth": 2, "strokeDash": [4, 2], "strokeDashOffset": 1, "strokeOpacity": 0.5, "cursor": "crosshair" } },
              "value": { "x": [2, 6] } }
            """);

        // Act
        var groups = Groups(Build(json));
        var shade = RectOf(groups[0].Group);
        var outline = RectOf(groups[2].Group);

        // Assert
        Assert.Equal(("red", 0.3, null), (shade.Fill, shade.FillOpacity, shade.Stroke));
        Assert.Equal(("transparent", "blue", 2.0, 0.5, 1.0), (outline.Fill, outline.Stroke, outline.StrokeWidth, outline.StrokeOpacity, outline.StrokeDashOffset));
        Assert.Equal([4.0, 2.0], outline.StrokeDash);
    }

    /// <summary>
    ///     Validates that the other stroke keys of the <c>mark</c> of the selection that a rectangle has (the cap, the join and the miter limit, and the opacity) reach the outline too: Vega-Lite
    ///     copies every key but the fill, the fill opacity and the cursor to the rectangle, whatever its schema lists.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_TheStrokeCapJoinAndMiterLimitReachTheOutline()
    {
        // Arrange
        var json = Scatter("""
            { "name": "brush", "select": { "type": "interval", "mark": { "strokeCap": "round", "strokeJoin": "bevel", "strokeMiterLimit": 2, "opacity": 0.5 } },
              "value": { "x": [2, 6] } }
            """);

        // Act
        var groups = Groups(Build(json));
        var shade = RectOf(groups[0].Group);
        var outline = RectOf(groups[2].Group);

        // Assert: the outline has them and the shade does not
        Assert.Equal(("round", "bevel", 2.0, 0.5), (outline.StrokeCap, outline.StrokeJoin, outline.StrokeMiterLimit, outline.Opacity));
        Assert.Equal((null, null, null, 1.0), (shade.StrokeCap, shade.StrokeJoin, shade.StrokeMiterLimit, shade.Opacity));
    }

    /// <summary>
    ///     Validates that the <c>mark</c> of the selection is merged over the default <c>mark</c> of the configuration: a selection that gives only a stroke keeps the default fill.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_TheSelectionMarkIsMergedOverTheDefaults()
    {
        // Arrange
        var json = Scatter("""{ "name": "brush", "select": { "type": "interval", "mark": { "stroke": "green" } }, "value": { "x": [2, 6] } }""");

        // Act
        var groups = Groups(Build(json));
        var shade = RectOf(groups[0].Group);
        var outline = RectOf(groups[2].Group);

        // Assert
        Assert.Equal(("#333", 0.125), (shade.Fill, shade.FillOpacity));
        Assert.Equal("green", outline.Stroke);
    }

    /// <summary>
    ///     Validates that <c>config.selection.interval.mark</c> of the specification and of the caller replaces the default mark of every interval selection, the caller's winning.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_TheConfigurationOverridesTheDefaultMark()
    {
        // Arrange: the specification asks for an orange stroke and a 0.2 fill opacity; the caller for a purple stroke
        var parameter = """{ "name": "brush", "select": "interval", "value": { "x": [2, 6] } }""";
        var configured = Scatter(parameter, """ "config": { "selection": { "interval": { "mark": { "stroke": "orange", "fillOpacity": 0.2 } } } }, """);

        // Act
        var fromSpecification = Groups(Build(configured));
        var fromCaller = Groups(Build(configured, new VegaLiteRenderOptions { Config = """{ "selection": { "interval": { "mark": { "stroke": "purple" } } } }""" }));

        // Assert
        Assert.Equal(("orange", 0.2), (RectOf(fromSpecification[2].Group).Stroke, RectOf(fromSpecification[0].Group).FillOpacity));
        Assert.Equal(("purple", 0.2, "#333"), (RectOf(fromCaller[2].Group).Stroke, RectOf(fromCaller[0].Group).FillOpacity, RectOf(fromCaller[0].Group).Fill));
    }

    /// <summary>
    ///     Validates the override of the caller: its value replaces the initial value, and null clears it (no brush).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_TheCallerReplacesOrClearsTheInitialValue()
    {
        // Arrange: the specification's brush is [2, 6]; the caller's is [4, 8]
        var json = Scatter("""{ "name": "brush", "select": "interval", "value": { "x": [2, 6] } }""");
        var replaced = new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["brush"] = new Dictionary<string, object?> { ["x"] = new object[] { 4, 8 } } } };
        var cleared = new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["brush"] = null } };

        // Act
        var replacedRect = RectOf(Groups(Build(json, replaced))[0].Group);
        var clearedGroups = Groups(Build(json, cleared));

        // Assert: x(4) = 120 and x(8) = 240
        Assert.Equal((120.0, 120.0), (replacedRect.X, replacedRect.Width));
        Assert.Equal(["marks"], clearedGroups.Select(g => g.Group.Role));
    }

    /// <summary>
    ///     Validates that a selection that is resolved globally keeps one interval, the one of the unit that declared it first: of two views side by side only the first draws the brush; with
    ///     <c>resolve: union</c> each unit draws its own, through its own scale (x(2) = 60 over a domain to 10, and 30 over a domain to 20).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_AGlobalSelectionIsDrawnByItsFirstUnitOnly()
    {
        // Arrange
        string Concat(string resolve) => $$"""
            { "params": [ { "name": "brush", "select": { "type": "interval", "resolve": "{{resolve}}", "encodings": ["x"] }, "value": { "x": [2, 6] } } ],
              "hconcat": [
                { "width": 300, "height": 100, "data": { "values": [ { "a": 0 }, { "a": 10 } ] }, "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } },
                { "width": 300, "height": 100, "data": { "values": [ { "a": 0 }, { "a": 20 } ] }, "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } } ] }
            """;

        // Act
        var global = Groups(Build(Concat("global")));
        var union = Groups(Build(Concat("union")));

        // Assert: global: shade, marks, outline of the first view, then the marks of the second
        Assert.Equal(["selection-brush-background", "marks", "selection-brush", "marks"], global.Select(g => g.Group.Role));

        // Union: both views draw theirs
        Assert.Equal(["selection-brush-background", "marks", "selection-brush", "selection-brush-background", "marks", "selection-brush"], union.Select(g => g.Group.Role));
        var shades = union.Where(g => g.Group.Role == "selection-brush-background").Select(g => RectOf(g.Group)).ToList();
        Assert.Equal((60.0, 120.0), (shades[0].X, shades[0].Width));
        Assert.Equal((30.0, 60.0), (shades[1].X, shades[1].Width));
    }

    /// <summary>
    ///     Validates that no cell of a facet draws the brush of a globally resolved selection (the name of the unit in the store never matches the name of a cell), while with <c>resolve: union</c>
    ///     every cell draws its own.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_NoCellOfAFacetDrawsAGlobalSelection()
    {
        // Arrange: two cells, a and b
        string Faceted(string resolve) => $$"""
            { "params": [ { "name": "brush", "select": { "type": "interval", "resolve": "{{resolve}}", "encodings": ["x"] }, "value": { "x": [2, 6] } } ],
              "data": { "values": [ { "a": 0, "c": "a" }, { "a": 10, "c": "a" }, { "a": 0, "c": "b" }, { "a": 10, "c": "b" } ] },
              "facet": { "field": "c", "type": "nominal" },
              "spec": { "width": 300, "height": 100, "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } } }
            """;

        // Act
        var global = Groups(Build(Faceted("global")));
        var union = Groups(Build(Faceted("union")));

        // Assert
        Assert.Equal(["marks", "marks"], global.Select(g => g.Group.Role));
        Assert.Equal(2, union.Count(g => g.Group.Role == "selection-brush"));
        Assert.All(union.Where(g => g.Group.Role == "selection-brush-background"), g => Assert.Equal((60.0, 120.0), (RectOf(g.Group).X, RectOf(g.Group).Width)));
    }

    /// <summary>
    ///     Validates the order of the brushes in a layered view (Vega-Lite's <c>assembleLayerSelectionMarks</c>): the shade of every selection is drawn first (the last unit's first), then the marks of
    ///     every layer, then the outlines (the first unit's first), so the brush of one layer is behind and in front of all the layers.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_ALayeredViewWrapsAllItsLayers()
    {
        // Arrange: each layer declares an interval selection in its params, with its own colors
        var json = """
            { "width": 300, "height": 200,
              "data": { "values": [ { "a": 0, "b": 0 }, { "a": 10, "b": 5 } ] },
              "layer": [
                { "params": [ { "name": "first", "select": { "type": "interval", "mark": { "fill": "red", "stroke": "red" } }, "value": { "x": [1, 2] } } ],
                  "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } },
                { "params": [ { "name": "second", "select": { "type": "interval", "mark": { "fill": "blue", "stroke": "blue" } }, "value": { "x": [3, 4] } } ],
                  "mark": "line", "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } } ] }
            """;

        // Act
        var groups = Groups(Build(json));

        // Assert: [bg second, bg first, marks, marks, brush first, brush second]
        Assert.Equal(
            ["selection-brush-background", "selection-brush-background", "marks", "marks", "selection-brush", "selection-brush"],
            groups.Select(g => g.Group.Role));
        Assert.Equal(["blue", "red"], groups.Take(2).Select(g => RectOf(g.Group).Fill));
        Assert.Equal(["red", "blue"], groups.Skip(4).Select(g => RectOf(g.Group).Stroke));

        // The first selection [1, 2] and the second [3, 4] map through the shared x scale (0 to 10 over 300): 30 to 60 and 90 to 120
        Assert.Equal((90.0, 30.0), (RectOf(groups[0].Group).X, RectOf(groups[0].Group).Width));
        Assert.Equal((30.0, 30.0), (RectOf(groups[1].Group).X, RectOf(groups[1].Group).Width));
    }

    /// <summary>
    ///     Validates that the selections of one unit wrap each other in the order they are declared: the second selection's shade is outside the first's.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_TheSelectionsOfAUnitWrapEachOther()
    {
        // Arrange
        var json = """
            { "width": 300, "height": 200, "data": { "values": [ { "a": 0 }, { "a": 10 } ] },
              "params": [ { "name": "first", "select": { "type": "interval", "mark": { "fill": "red" } }, "value": { "x": [1, 2] } },
                          { "name": "second", "select": { "type": "interval", "mark": { "fill": "blue" } }, "value": { "x": [3, 4] } } ],
              "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }
            """;

        // Act
        var groups = Groups(Build(json));

        // Assert: [bg second, bg first, marks, brush first, brush second]
        Assert.Equal(["selection-brush-background", "selection-brush-background", "marks", "selection-brush", "selection-brush"], groups.Select(g => g.Group.Role));
        Assert.Equal(["blue", "red"], groups.Take(2).Select(g => RectOf(g.Group).Fill));
    }

    /// <summary>
    ///     Validates that the brush is charged to the scene budget: the smallest budget that lays the chart out is four items larger with a brush (two groups and two rectangles).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_IsChargedToTheSceneBudget()
    {
        // Arrange
        var plain = Scatter("""{ "name": "brush", "select": "interval" }""");
        var brushed = Scatter("""{ "name": "brush", "select": "interval", "value": { "x": [2, 6] } }""");

        // Act
        var smallestPlain = SmallestBudget(plain);
        var smallestBrushed = SmallestBudget(brushed);

        // Assert
        Assert.Equal(smallestPlain + 4, smallestBrushed);
    }

    /// <summary>
    ///     Lays a chart out and collects the unsupported-feature reports of the call.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="config">The configuration of the caller, or null.</param>
    /// <returns>The groups of the brush and the reports, each as the feature id, a colon and the message.</returns>
    private static (List<(SceneGroup Group, double OffsetX)> Groups, List<string> Reports) BuildReporting(string json, string? config = null)
    {
        var reports = new List<string>();
        var options = new VegaLiteRenderOptions { Config = config, UnsupportedFeatureHandler = u => reports.Add(u.Feature + ": " + u.Message) };

        // The reports are delivered by measuring; the scene is laid out the same way
        var document = VegaLiteDocument.Parse(json);
        document.Measure(options, Ct);
        return (Groups(document.BuildScene(options, Ct)), reports);
    }

    /// <summary>
    ///     Validates that Vega-Lite's schema (6.5.0), which allows no expression reference in <c>BrushConfig</c> (its <c>fill</c> and <c>stroke</c> are a color, the opacities and widths
    ///     numbers), is honored for <c>config.selection.interval.mark</c>: a fill, a stroke and an opacity that are expressions (one driven by a variable parameter) are reported as
    ///     <c>vl-expr-ref</c> at their pointers and are not evaluated, and the brush keeps the default shade (#333 at 0.125) and the white outline.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_AnExpressionInTheConfigurationMarkIsReportedAndTheDefaultsStay()
    {
        // Arrange: the fill is driven by a variable parameter, the stroke is a constant expression and the opacity reads the parameter
        var json = Scatter(
            """{ "name": "shade", "value": "red" }, { "name": "level", "value": 0.5 }, { "name": "brush", "select": "interval", "value": { "x": [2, 6] } }""",
            """ "config": { "selection": { "interval": { "mark": { "fill": { "expr": "shade" }, "stroke": { "expr": "'blue'" }, "fillOpacity": { "expr": "level" }, "strokeWidth": { "expr": "3" } } } } }, """);

        // Act
        var (groups, reports) = BuildReporting(json);
        var shade = RectOf(groups[0].Group);
        var outline = RectOf(groups[2].Group);

        // Assert: four references reported, the brush painted with the defaults
        Assert.Equal(4, reports.Count(r => r.StartsWith("vl-expr-ref:", StringComparison.Ordinal) && r.Contains("selection", StringComparison.Ordinal)));
        Assert.Equal(("#333", 0.125), (shade.Fill, shade.FillOpacity));
        Assert.Equal(("transparent", "white"), (outline.Fill, outline.Stroke));
        Assert.NotEqual(3.0, outline.StrokeWidth);
    }

    /// <summary>
    ///     Validates the same for <c>params[].select.mark</c> and for a reference that reads the datum: the references are reported (each at its own pointer), the value that the selection would
    ///     have replaced is kept, and a sibling that is a plain value still applies. A reference in the selection leaves the value of the configuration in place.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_AnExpressionInTheSelectionMarkIsReportedAndTheConfiguredValueStays()
    {
        // Arrange: the configuration gives orange and 0.2; the selection's fill reads a variable parameter, its stroke the datum, and its opacity is a plain 0.4 beside a reference to the stroke width
        var json = Scatter(
            """{ "name": "shade", "value": "red" }, { "name": "brush", "select": { "type": "interval", "mark": { "fill": { "expr": "shade" }, "stroke": { "expr": "datum.k" }, "fillOpacity": 0.4, "strokeWidth": { "expr": "2" } } }, "value": { "x": [2, 6] } }""",
            """ "config": { "selection": { "interval": { "mark": { "fill": "orange", "stroke": "purple", "strokeWidth": 4 } } } }, """);

        // Act
        var (groups, reports) = BuildReporting(json);
        var shade = RectOf(groups[0].Group);
        var outline = RectOf(groups[2].Group);

        // Assert
        Assert.Equal(3, reports.Count(r => r.StartsWith("vl-expr-ref:", StringComparison.Ordinal) && r.Contains("selection", StringComparison.Ordinal)));
        Assert.Equal(("orange", 0.4), (shade.Fill, shade.FillOpacity));
        Assert.Equal(("purple", 4.0), (outline.Stroke, outline.StrokeWidth));
    }

    /// <summary>
    ///     Validates that a reference in the <c>Config</c> of the render options and in an initial value or an event of the selection is reported, and that the brush is drawn as if it were absent.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Brush_AnExpressionInTheCallerConfigurationOrTheProjectionIsReported()
    {
        // Arrange: a reference at the stroke in the caller's configuration, one in the events of the selection
        var json = Scatter("""{ "name": "brush", "select": { "type": "interval", "on": { "expr": "'mousedown'" } }, "value": { "x": [2, 6] } }""");

        // Act
        var (groups, reports) = BuildReporting(json, """{ "selection": { "interval": { "mark": { "stroke": { "expr": "'blue'" } } } } }""");
        var outline = RectOf(groups[2].Group);

        // Assert: one report for the caller's text and one for the events; the stroke is the default
        Assert.Equal(2, reports.Count(r => r.StartsWith("vl-expr-ref:", StringComparison.Ordinal)));
        Assert.Equal("white", outline.Stroke);
    }

    /// <summary>
    ///     Finds the smallest <see cref="VegaLiteLimits.MaxSceneItems"/> with which a chart can be laid out.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The smallest budget.</returns>
    private static long SmallestBudget(string json)
    {
        for (var budget = 1L; budget < 500; budget++)
        {
            try
            {
                Build(json, new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = budget } });
                return budget;
            }
            catch (VegaLiteRenderException)
            {
                // Too small: try one more
            }
        }

        throw new InvalidOperationException("The chart needs more than 500 scene items.");
    }
}
