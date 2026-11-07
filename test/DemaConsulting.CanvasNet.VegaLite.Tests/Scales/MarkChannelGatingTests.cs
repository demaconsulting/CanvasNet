using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of which marks draw which channels (Vega-Lite's <c>supportMark</c> table for <c>theta</c>, <c>radius</c>, <c>size</c>, <c>strokeDash</c>, <c>angle</c> and <c>shape</c>), the reports
///     of the feature catalog that follow from it, and the marks whose <c>size</c> conditions are evaluated.
/// </summary>
public class MarkChannelGatingTests
{
    /// <summary>The marks of Vega-Lite that carry the channels of the table.</summary>
    private static readonly string[] Marks =
        ["point", "circle", "square", "rect", "bar", "tick", "rule", "line", "area", "trail", "text", "arc", "image"];

    /// <summary>
    ///     Validates the support table: for every channel the exact set of marks that draw it.
    /// </summary>
    [Fact]
    public void ScaleResolver_ChannelIsDrawn_FollowsTheSupportTable()
    {
        // Arrange: the marks that draw each channel, from Vega-Lite's channel support (every mark draws the channels that are not listed)
        var table = new Dictionary<string, string[]>
        {
            ["theta"] = ["text", "arc"],
            ["radius"] = ["text", "arc"],
            ["theta2"] = ["arc"],
            ["radius2"] = ["arc"],
            ["text"] = ["text"],
            ["size"] = ["point", "circle", "square", "bar", "tick", "rule", "line", "trail", "text"],
            ["strokeDash"] = ["point", "circle", "square", "bar", "tick", "rule", "line", "image"],
            ["angle"] = ["point", "circle", "square", "text"],
            ["shape"] = ["point"],
            ["color"] = Marks,
            ["fill"] = Marks,
            ["stroke"] = Marks,
            ["opacity"] = Marks,
            ["fillOpacity"] = Marks,
            ["strokeOpacity"] = Marks,
            ["strokeWidth"] = Marks,
            ["x"] = Marks,
            ["y"] = Marks,
        };

        // Act / Assert: all 13 marks for each channel
        foreach (var (channel, drawers) in table)
        {
            foreach (var mark in Marks)
            {
                Assert.True(
                    drawers.Contains(mark) == ScaleResolver.ChannelIsDrawn(channel, mark),
                    $"The channel '{channel}' on the '{mark}' mark: expected {drawers.Contains(mark)}.");
            }
        }
    }

    /// <summary>
    ///     Validates that a channel which the mark does not draw has no scale: a size on an area and an arc, a stroke dash on a trail, a text and an area, an angle on a bar.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ChannelsThatTheMarkDoesNotDrawHaveNoScale()
    {
        // Arrange: (mark, channel, field type) with a field that every mark could read
        var vectors = new (string Mark, string Channel, string Type, bool HasScale)[]
        {
            ("area", "size", "quantitative", false),
            ("arc", "size", "quantitative", false),
            ("line", "size", "quantitative", true),
            ("trail", "size", "quantitative", true),
            ("text", "size", "quantitative", true),
            ("area", "strokeDash", "nominal", false),
            ("trail", "strokeDash", "nominal", false),
            ("text", "strokeDash", "nominal", false),
            ("arc", "strokeDash", "nominal", false),
            ("line", "strokeDash", "nominal", true),
            ("text", "angle", "quantitative", true),
            ("line", "angle", "quantitative", false),
            ("arc", "shape", "nominal", false),
        };

        // Act / Assert
        foreach (var (mark, channel, type, hasScale) in vectors)
        {
            var tree = SpecParser.Parse(
                "{\"data\": {\"values\": [{\"f\": 1, \"g\": \"a\"}, {\"f\": 2, \"g\": \"b\"}]}, \"mark\": \"" + mark + "\", \"encoding\": {\""
                + channel + "\": {\"field\": \"" + (type == "nominal" ? "g" : "f") + "\", \"type\": \"" + type + "\"}}}");
            var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, TestContext.Current.CancellationToken);
            var scales = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), tree.SourceName, null, TestContext.Current.CancellationToken);
            Assert.True(hasScale == (scales[channel] is not null), $"The '{channel}' scale of the '{mark}' mark: expected {hasScale}.");
        }
    }

    /// <summary>
    ///     Validates the reports of the feature catalog for the channels that the mark does not draw: a size on an area or an arc, a stroke dash on a trail, text, area or arc,
    ///     a shape on a line; and that a size on a rect (a long-standing exception) and the channels that are drawn are not reported.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ReportsChannelsThatTheMarkDoesNotDraw()
    {
        // Arrange / Act / Assert: the mark itself is reported too while it is not drawn; only the channel reports are listed
        Assert.Equal(["vl-channel-size"], ChannelReports("area", """ "size": {"field":"v","type":"quantitative"} """));
        Assert.Equal(["vl-channel-size"], ChannelReports("arc", """ "size": {"value": 5} """));
        Assert.Equal(["vl-channel-strokedash"], ChannelReports("trail", """ "strokeDash": {"field":"v","type":"nominal"} """));
        Assert.Equal(["vl-channel-strokedash"], ChannelReports("text", """ "strokeDash": {"value": [2, 2]} """));
        Assert.Equal(["vl-channel-strokedash"], ChannelReports("area", """ "strokeDash": {"field":"v","type":"nominal"} """));
        Assert.Equal(["vl-channel-shape"], ChannelReports("line", """ "shape": {"field":"v","type":"nominal"} """, overlay: false));
        Assert.Equal(["vl-channel-angle"], ChannelReports("line", """ "angle": {"field":"v","type":"quantitative"} """));

        // Assert: the drawn channels are not reported
        Assert.Empty(ChannelReports("line", """ "size": {"field":"v","type":"quantitative"}, "strokeDash": {"field":"v","type":"nominal"} """));
        Assert.Empty(ChannelReports("text", """ "size": {"field":"v","type":"quantitative"}, "angle": {"field":"v","type":"quantitative"} """));
        Assert.Empty(ChannelReports("trail", """ "size": {"field":"v","type":"quantitative"} """));
        Assert.Empty(ChannelReports("bar", """ "size": {"value": 4}, "strokeDash": {"value": [1, 1]} """));
        Assert.Empty(ChannelReports("rect", """ "size": {"value": 4} """));
    }

    /// <summary>
    ///     Validates which marks evaluate the conditions of a <c>size</c> channel: the ones that use a size (symbols, bar, rect, tick, rule, line, trail and text), shown by an invalid
    ///     expression that is a specification error only where the condition is evaluated; and that <c>theta</c> and <c>radius</c> take no condition.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_EvaluatesSizeConditionsOfMarksThatUseASize()
    {
        // Arrange: a condition whose value is an expression that does not compile
        const string broken = """ "size": { "condition": { "test": "datum.v > 1", "value": {"expr": "datum.v >"} } } """;

        // Act / Assert: compiled (and so rejected) for the marks that evaluate their size conditions
        foreach (var mark in new[] { "point", "circle", "square", "bar", "rect", "tick", "rule", "line", "trail", "text" })
        {
            Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec(mark, broken)));
        }

        // Assert: not evaluated, so not compiled, for the marks that have no size
        foreach (var mark in new[] { "area", "arc" })
        {
            Assert.NotNull(SpecParser.Parse(Spec(mark, broken)));
        }

        // Assert: the polar channels take no condition (Vega-Lite defines none), the others of the table do
        Assert.False(ChannelDefinitions.CanCondition("theta"));
        Assert.False(ChannelDefinitions.CanCondition("radius"));
        Assert.False(ChannelDefinitions.CanCondition("theta2"));
        Assert.False(ChannelDefinitions.CanCondition("radius2"));
        Assert.True(ChannelDefinitions.CanCondition("size"));
        Assert.True(ChannelDefinitions.CanCondition("angle"));
    }

    /// <summary>
    ///     Validates that the point overlay of a path mark makes the scales of the channels that its points draw (Vega-Lite gives the overlay layer every channel but the second ends
    ///     of the positions): a shape of a line or an area, an angle of a line, a size of an area (with the range of a point) and a dash of a trail, and that without an overlay none of them has a scale.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PointOverlayBuildsTheScalesOfThePointChannels()
    {
        // Arrange: (mark, channel, field type, has a scale)
        var vectors = new (string Mark, string Channel, string Type, bool HasScale)[]
        {
            ("""{"type":"line","point":true}""", "shape", "nominal", true),
            ("""{"type":"line","point":{"size":80}}""", "shape", "nominal", true),
            ("""{"type":"line","point":"transparent"}""", "shape", "nominal", true),
            ("\"line\"", "shape", "nominal", true),
            ("\"trail\"", "shape", "nominal", true),
            ("""{"type":"area","line":true}""", "shape", "nominal", true),
            ("""{"type":"area","point":true}""", "shape", "nominal", true),
            ("""{"type":"line","point":false}""", "shape", "nominal", false),
            ("""{"type":"line","point":null}""", "shape", "nominal", false),
            ("""{"type":"area","point":false}""", "shape", "nominal", false),
            ("""{"type":"arc","point":true}""", "shape", "nominal", false),
            ("""{"type":"line","point":true}""", "angle", "quantitative", true),
            ("""{"type":"line","point":false}""", "angle", "quantitative", false),
            ("""{"type":"area","point":true}""", "size", "quantitative", true),
            ("""{"type":"area","point":false}""", "size", "quantitative", false),
            ("""{"type":"trail","point":true}""", "strokeDash", "nominal", true),
            ("""{"type":"area","point":true}""", "strokeDash", "nominal", true),
            ("""{"type":"area","point":true}""", "text", "nominal", false),
            ("""{"type":"area","line":true,"point":false}""", "size", "quantitative", true),
            ("""{"type":"area","line":true,"point":false}""", "strokeDash", "nominal", true),
            ("""{"type":"area","line":{"color":"red"},"point":false}""", "size", "quantitative", true),
            ("""{"type":"area","line":true,"point":false}""", "shape", "nominal", false),
            ("""{"type":"area","line":true,"point":false}""", "angle", "quantitative", false),
            ("""{"type":"area","line":false,"point":false}""", "size", "quantitative", false),
            ("""{"type":"area","line":null,"point":false}""", "strokeDash", "nominal", false),
            ("""{"type":"trail","line":true,"point":false}""", "strokeDash", "nominal", false),
        };

        // Act / Assert
        foreach (var (mark, channel, type, hasScale) in vectors)
        {
            var tree = SpecParser.Parse(
                "{\"data\": {\"values\": [{\"f\": 1, \"g\": \"a\"}, {\"f\": 2, \"g\": \"b\"}]}, \"mark\": " + mark + ", \"encoding\": {\""
                + channel + "\": {\"field\": \"" + (type == "nominal" ? "g" : "f") + "\", \"type\": \"" + type + "\"}}}");
            var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, TestContext.Current.CancellationToken);
            var scales = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), tree.SourceName, null, TestContext.Current.CancellationToken);
            Assert.True(hasScale == (scales[channel] is not null), $"The '{channel}' scale of {mark}: expected {hasScale}.");
        }

        // Assert: the shape scale of a line with an overlay has the groups of the field as its domain; a falsy configuration of the point leaves the decision to the shape
        var configured = SpecParser.Parse("{\"data\": {\"values\": [{\"g\": \"a\"}, {\"g\": \"b\"}]}, \"mark\": \"line\", \"encoding\": {\"shape\": {\"field\": \"g\", \"type\": \"nominal\"}}, \"config\": {\"line\": {\"point\": false}}}");
        var configuredTable = DataLoader.Load(configured.Root["data"], configured.SourceName, null, TestContext.Current.CancellationToken);
        var shaped = ScaleResolver.Resolve(configured.Root, configuredTable, ConfigResolver.Resolve(configured, null), configured.SourceName, null, TestContext.Current.CancellationToken);
        Assert.Equal(["a", "b"], shaped["shape"]!.DiscreteDomain.Select(d => d.Text));

        // Assert: the line overlay of the configuration builds the scales of the line too; the size of an area then has the range of the stroke widths of its line (not that of a symbol)
        var area = SpecParser.Parse("{\"data\": {\"values\": [{\"f\": 1}, {\"f\": 2}]}, \"mark\": {\"type\": \"area\", \"point\": false}, \"encoding\": {\"size\": {\"field\": \"f\", \"type\": \"quantitative\"}}, \"config\": {\"area\": {\"line\": true}}}");
        var areaTable = DataLoader.Load(area.Root["data"], area.SourceName, null, TestContext.Current.CancellationToken);
        var lineSize = ScaleResolver.Resolve(area.Root, areaTable, ConfigResolver.Resolve(area, null), area.SourceName, null, TestContext.Current.CancellationToken)["size"];
        Assert.NotNull(lineSize);
        Assert.Equal(4.0, lineSize.CreateModel(0).Map(2.0));
    }

    /// <summary>
    ///     Validates the reports of the feature catalog for the channels of a path mark with a point overlay: a shape is not reported (Vega-Lite drops it from the path layer), while a
    ///     size of an area, a dash of an area or a trail and an angle of a line are (the path layer warns about them), with a message that says that the points draw them; and that
    ///     a path mark without an overlay (<c>point: false</c>) still reports the shape.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ShapeOfAPathMarkWithAPointOverlayIsNotReported()
    {
        // Assert: no report for the shape, whatever way the overlay is switched on
        const string shape = """ "shape": {"field":"v","type":"nominal"} """;
        Assert.Empty(ChannelReports("""{"type":"line","point":true}""", shape));
        Assert.Empty(ChannelReports("""{"type":"line","point":{"color":"red"}}""", shape));
        Assert.Empty(ChannelReports("""{"type":"line","point":"transparent"}""", shape));
        Assert.Empty(ChannelReports("\"line\"", shape));
        Assert.Empty(ChannelReports("""{"type":"trail"}""", shape));
        Assert.Empty(ChannelReports("""{"type":"rule"}""", shape));
        Assert.Empty(ChannelReports("""{"type":"area","point":true}""", shape));
        Assert.Empty(ChannelReports("""{"type":"area","line":true}""", shape));

        // Assert: the shape is still reported without an overlay, and on the marks that have none
        Assert.Equal(["vl-channel-shape"], ChannelReports("""{"type":"line","point":false}""", shape));
        Assert.Equal(["vl-channel-shape"], ChannelReports("""{"type":"line","point":null}""", shape));
        Assert.Equal(["vl-channel-shape"], ChannelReports("""{"type":"area","point":false}""", shape));
        Assert.Equal(["vl-channel-shape"], ChannelReports("\"bar\"", shape));
        Assert.Equal(["vl-channel-shape"], ChannelReports("\"arc\"", shape));

        // Assert: the overlay of the configuration of the specification counts as well
        var configured = FeatureCatalog.Visit(SpecParser.Parse("""
            { "data": { "values": [ { "v": "a" } ] }, "mark": { "type": "line", "point": false }, "config": { "line": { "point": true } },
              "encoding": { "shape": { "field": "v", "type": "nominal" } } }
            """));
        Assert.Contains(configured, r => r.Id == "vl-channel-shape");
        var enabled = FeatureCatalog.Visit(SpecParser.Parse("""
            { "data": { "values": [ { "v": "a" } ] }, "mark": "line", "config": { "line": { "point": true } },
              "encoding": { "shape": { "field": "v", "type": "nominal" } } }
            """));
        Assert.DoesNotContain(enabled, r => r.Id == "vl-channel-shape");

        // Assert: the channels that the path layer keeps are still reported, but they say that the points draw them
        var angle = FeatureCatalog.Visit(SpecParser.Parse(Spec("""{"type":"line","point":true}""", """ "angle": {"field":"v","type":"quantitative"} """))).Single(r => r.Id == "vl-channel-angle");
        Assert.Contains("point overlay", angle.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored", angle.Message, StringComparison.Ordinal);
        Assert.Equal(["vl-channel-size"], ChannelReports("""{"type":"area","point":true}""", """ "size": {"field":"v","type":"quantitative"} """));
        Assert.Equal(["vl-channel-strokedash"], ChannelReports("""{"type":"trail","point":true}""", """ "strokeDash": {"field":"v","type":"nominal"} """));
        Assert.Contains("ignored", FeatureCatalog.Visit(SpecParser.Parse(Spec("\"line\"", """ "angle": {"field":"v","type":"quantitative"} """))).Single(r => r.Id == "vl-channel-angle").Message, StringComparison.Ordinal);

        // Assert: the line overlay of an area counts the same way: its size and its dash are drawn by the line, the message names the overlay that draws them
        const string size = """ "size": {"field":"v","type":"quantitative"} """;
        const string dash = """ "strokeDash": {"field":"v","type":"nominal"} """;
        string Message(string mark, string channels, string id) =>
            FeatureCatalog.Visit(SpecParser.Parse(Spec(mark, channels))).Single(r => r.Id == id).Message;
        var lineOnly = Message("""{"type":"area","line":true,"point":false}""", size, "vl-channel-size");
        Assert.Contains("line overlay", lineOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("point", lineOnly, StringComparison.Ordinal);
        Assert.Contains("point and line overlays", Message("""{"type":"area","line":true,"point":true}""", dash, "vl-channel-strokedash"), StringComparison.Ordinal);
        Assert.Contains("ignored", Message("""{"type":"area","line":false,"point":false}""", size, "vl-channel-size"), StringComparison.Ordinal);
        Assert.Contains("ignored", Message("""{"type":"trail","line":true,"point":false}""", dash, "vl-channel-strokedash"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the reports follow the configuration of the rendering when there is one: the angle of a line is drawn by the overlay points that <c>config.line.point</c> of the
    ///     caller enables (the message says so), and the size of an area by the line overlay that <c>config.area.line</c> enables, while the same specification visited without that
    ///     configuration reports them as ignored.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ReportsFollowTheEffectiveConfiguration()
    {
        // Arrange
        var angle = SpecParser.Parse(Spec("\"line\"", """ "angle": {"field":"v","type":"quantitative"} """));
        var size = SpecParser.Parse(Spec("""{"type":"area","point":false}""", """ "size": {"field":"v","type":"quantitative"} """));

        // Act / Assert: the specification alone has no overlay
        Assert.Contains("ignored", FeatureCatalog.Visit(angle).Single(r => r.Id == "vl-channel-angle").Message, StringComparison.Ordinal);
        Assert.Contains("ignored", FeatureCatalog.Visit(size).Single(r => r.Id == "vl-channel-size").Message, StringComparison.Ordinal);

        // Act / Assert: the effective configuration (with the caller's) enables them
        var points = FeatureCatalog.Visit(angle, ConfigResolver.Resolve(angle, """{ "line": { "point": true } }""")).Single(r => r.Id == "vl-channel-angle");
        Assert.Contains("point overlay", points.Message, StringComparison.Ordinal);
        var line = FeatureCatalog.Visit(size, ConfigResolver.Resolve(size, """{ "area": { "line": true } }""")).Single(r => r.Id == "vl-channel-size");
        Assert.Contains("line overlay", line.Message, StringComparison.Ordinal);

        // Assert: a shape is not reported either way (it switches the overlay on)
        var shape = SpecParser.Parse(Spec("\"line\"", """ "shape": {"field":"v","type":"nominal"} """));
        Assert.Empty(FeatureCatalog.Visit(shape));
        Assert.Empty(FeatureCatalog.Visit(shape, ConfigResolver.Resolve(shape, """{ "line": { "point": true } }""")));
    }

    /// <summary>
    ///     Validates that the definition of a channel that an overlay draws is still visited: a shape of a line with a point overlay reports no <c>vl-channel-shape</c> but still reports its
    ///     own invalid shape path, and an angle that only the overlay points draw reports the channel and its own unsupported <c>bin</c> too.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_VisitsTheDefinitionOfAChannelThatAnOverlayDraws()
    {
        // Act
        var shape = FeatureCatalog.Visit(SpecParser.Parse(Spec("""{"type":"line","point":true}""", """ "shape": {"field":"v","type":"nominal","scale":{"range":["circle","M0,0L"]}} """))).Select(r => r.Id).ToList();
        var shapeValue = FeatureCatalog.Visit(SpecParser.Parse(Spec("\"line\"", """ "shape": {"value":"M0,0L"} """))).Select(r => r.Id).ToList();
        var angle = FeatureCatalog.Visit(SpecParser.Parse(Spec("""{"type":"line","point":true}""", """ "angle": {"field":"v","type":"quantitative","bin":"binned"} """))).Select(r => r.Id).ToList();
        var off = FeatureCatalog.Visit(SpecParser.Parse(Spec("""{"type":"line","point":false}""", """ "angle": {"field":"v","type":"quantitative","bin":"binned"} """))).Select(r => r.Id).ToList();

        // Assert: the path-specific report is suppressed for the shape only, the definition's own reports stay
        Assert.Equal(["vl-shape-path-invalid"], shape);
        Assert.Equal(["vl-shape-path-invalid"], shapeValue);
        Assert.Equal(["vl-channel-angle", "vl-encoding-bin"], angle);

        // Assert: a channel that nothing draws is reported as ignored and its definition is not visited
        Assert.Equal(["vl-channel-angle"], off);
    }

    /// <summary>
    ///     Validates the legends of a line with a point overlay: a shape and a color of the same field are one legend whose symbols have the shapes and the colors of the groups, and a shape of
    ///     another field is a legend of its own.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_ShapeOfALineWithAPointOverlayHasALegendMergedWithTheColorOfTheSameField()
    {
        // Arrange
        const string rows = """[ { "x": 0, "y": 1, "g": "a", "h": "p" }, { "x": 1, "y": 2, "g": "b", "h": "p" }, { "x": 2, "y": 3, "g": "c", "h": "q" } ]""";
        static string Json(string channels) =>
            "{ \"data\": { \"values\": " + rows + " }, \"mark\": { \"type\": \"line\", \"point\": true }, \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" }, "
            + channels + " } }";

        // Act: the same field in both channels
        var json = Json("""
            "color": { "field": "g", "type": "nominal" }, "shape": { "field": "g", "type": "nominal" }
            """);
        var merged = ChartTestHelpers.Legends(json);

        // Assert: one legend that holds the color and the shape
        var legend = Assert.Single(merged);
        Assert.Equal(["color", "shape"], legend.Members.Select(m => m.Channel));

        // Assert: the symbols of the built legend have the shapes of the shape scale (a circle, a square, a triangle) and the colors of the groups
        var symbols = Assert.IsType<SceneGroup>(ChartTestHelpers.Legend(json).Children[0]).Children.OfType<SceneSymbol>().ToList();
        Assert.Equal(["circle", "square", "triangle-up"], symbols.Select(s => s.Shape));

        // Assert: the symbols are filled with the colors of the groups, as the filled points of the overlay are (the stroke of the line does not make them hollow)
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], symbols.Select(s => s.Fill));
        Assert.All(symbols, s => Assert.NotEqual(s.Fill, s.Stroke));

        // Assert: an overlay object that says so keeps the points, and so the symbols, unfilled; an area with a point overlay is filled like a line
        var hollowJson = json.Replace("\"point\": true", "\"point\": { \"filled\": false }", StringComparison.Ordinal);
        var hollow = Assert.IsType<SceneGroup>(ChartTestHelpers.Legend(hollowJson).Children[0]).Children.OfType<SceneSymbol>().ToList();
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], hollow.Select(s => s.Stroke));
        var areaJson = json.Replace("\"type\": \"line\"", "\"type\": \"area\"", StringComparison.Ordinal);
        var areaSymbols = Assert.IsType<SceneGroup>(ChartTestHelpers.Legend(areaJson).Children[0]).Children.OfType<SceneSymbol>().ToList();
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], areaSymbols.Select(s => s.Fill));
        Assert.Equal(["circle", "square", "triangle-up"], areaSymbols.Select(s => s.Shape));

        // Act / Assert: another field for the shape is a legend of its own
        var apart = ChartTestHelpers.Legends(Json("""
            "color": { "field": "g", "type": "nominal" }, "shape": { "field": "h", "type": "nominal" }
            """));
        Assert.Equal(["color", "shape"], apart.Select(l => l.Members.Single().Channel));

        // Act / Assert: without an overlay (point: false) the shape has no scale and so no legend
        var off = ChartTestHelpers.Legends(json.Replace("\"point\": true", "\"point\": false", StringComparison.Ordinal));
        Assert.Equal(["color"], Assert.Single(off).Members.Select(m => m.Channel));
    }

    /// <summary>
    ///     Builds a specification of one mark over a tiny data set.
    /// </summary>
    /// <param name="mark">The JSON text of the mark (a name in quotes or a mark definition).</param>
    /// <param name="channels">The properties of the encoding object, without the braces.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string channels) =>
        "{\"data\": {\"values\": [{\"v\": 1}, {\"v\": 2}]}, \"mark\": " + (mark.StartsWith('{') || mark.StartsWith('"') ? mark : "\"" + mark + "\"") + ", \"encoding\": {" + channels + "}}";

    /// <summary>
    ///     Lists the identifiers of the channel reports (<c>vl-channel-*</c>) of a one-mark specification.
    /// </summary>
    /// <param name="mark">The JSON text of the mark (a name, or a name in quotes, or a mark definition).</param>
    /// <param name="channels">The properties of the encoding object, without the braces.</param>
    /// <param name="overlay"><see langword="false"/> to switch the point overlay off (<c>"point": false</c>), which a <c>shape</c> channel would otherwise enable on a path mark; only for a mark given by its name.</param>
    /// <returns>The identifiers in the order of the catalog.</returns>
    private static string[] ChannelReports(string mark, string channels, bool overlay = true) =>
        [.. FeatureCatalog.Visit(SpecParser.Parse(Spec(overlay ? mark : "{\"type\": \"" + mark + "\", \"point\": false}", channels))).Where(r => r.Id.StartsWith("vl-channel-", StringComparison.Ordinal)).Select(r => r.Id)];
}
