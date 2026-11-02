using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of layered views (Phase 10): <c>layer</c> specifications are drawn layer by layer over the data and the transforms they inherit, share
///     (or keep apart, with <c>resolve</c>) their scales, axes and legends, and size the view from the combined discrete scale. The charts are rendered through the public API
///     and checked by the positions of the marks and the guides, with every number derived by hand from the scale formulas and the layout of the chart (a 300 pixel data
///     rectangle in the continuous direction, a step of 20 pixels for each category of a discrete direction).
/// </summary>
public class VegaLiteLayerIntegrationTests
{
    /// <summary>Three categories with a value <c>v</c> of 4, 8 and 6 and a second measure <c>w</c> of 100, 400 and 1000.</summary>
    private const string CategoryData = """
        "data": { "values": [ { "a": "A", "v": 4, "w": 100 }, { "a": "B", "v": 8, "w": 400 }, { "a": "C", "v": 6, "w": 1000 } ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json) => VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the items of a role in a scene as a given kind.
    /// </summary>
    /// <typeparam name="T">The kind of item.</typeparam>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items in drawing order.</returns>
    private static List<T> Items<T>(Scene scene, string role)
        where T : SceneItem => [.. scene.FindAll(role).Select(e => (T)e.Item)];

    /// <summary>
    ///     Gets the texts of the items of a role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. Items<SceneText>(scene, role).Select(t => t.Text)];

    /// <summary>
    ///     Gets the number of items that a marks group of a scene holds, one entry for each layer.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The item counts of the marks groups in drawing order.</returns>
    private static List<int> MarkCounts(Scene scene) => [.. Items<SceneGroup>(scene, "marks").Select(g => g.Children.Count)];

    /// <summary>
    ///     Renders a specification and collects what it reports.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The warnings and the unsupported features, each as <c>identifier: message</c>.</returns>
    private static (List<string> Warnings, List<string> Unsupported) Report(string json, VegaLiteLimits? limits = null)
    {
        var warnings = new List<string>();
        var unsupported = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w => warnings.Add(w.Message),
            UnsupportedFeatureHandler = u => unsupported.Add($"{u.Feature}: {u.Message}"),
            Limits = limits ?? VegaLiteLimits.Default,
        };
        using var surface = VegaLiteDocument.Parse(json).Render(options, Ct);
        return (warnings, unsupported);
    }

    /// <summary>
    ///     Validates the reported bug: a top-level <c>transform</c> with a <c>calculate</c> over a <c>layer</c> is applied, nothing is reported for it, and the calculated field
    ///     is drawn by the layer's encoding (the bars are as high as the calculated values, not the original ones).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_TopLevelCalculateIsAppliedAndNotReported()
    {
        // Arrange: w = 10 * v gives 40, 80 and 60 over the domain 0 to 80, so the bars are 150, 300 and 225 pixels high (a data rectangle of 300 pixels)
        const string json = """
            {
              "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] },
              "transform": [ { "calculate": "datum.v * 10", "as": "w" } ],
              "layer": [ { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "w", "type": "quantitative" } } } ]
            }
            """;
        var document = VegaLiteDocument.Parse(json);
        var unsupported = new List<VegaLiteUnsupportedFeature>();
        var warnings = new List<VegaLiteDiagnostic>();

        // Act: strict mode would throw for the old report; the render must succeed
        using var surface = document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = unsupported.Add, WarningHandler = warnings.Add }, Ct);
        var bars = Items<SceneRect>(SceneOf(json), "mark-bar");

        // Assert: no report at all, and the calculated field is what the bars show
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
        Assert.Equal([150.0, 300.0, 225.0], bars.Select(b => b.Height));
        Assert.Equal([150.0, 0.0, 75.0], bars.Select(b => b.Y));
        Assert.Equal(["0", "10", "20", "30", "40", "50", "60", "70", "80"], Texts(SceneOf(json), "axis-label").Where(t => char.IsDigit(t[0])));
    }

    /// <summary>
    ///     Validates that the transforms apply in order: the top-level transform first, then the transform of each layer on the table that the top level produced, and that
    ///     the layers do not see each other's results.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_TransformsApplyTopLevelFirstThenPerLayer()
    {
        // Arrange: w = 10 v is 40, 80, 60; the bars keep the rows with w above 50 (B and C); the rule is the mean of w over all the rows (60), unaffected by the bars' filter
        const string json = """
            {
              "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] },
              "transform": [ { "calculate": "datum.v * 10", "as": "w" } ],
              "layer": [
                { "mark": "bar", "transform": [ { "filter": "datum.w > 50" } ],
                  "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "w", "type": "quantitative" } } },
                { "mark": "rule", "transform": [ { "aggregate": [ { "op": "mean", "field": "w", "as": "m" } ] } ],
                  "encoding": { "y": { "field": "m", "type": "quantitative" }, "color": { "value": "red" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = Items<SceneRect>(scene, "mark-bar");
        var rules = Items<SceneRule>(scene, "mark-rule");

        // Assert: two bars (B and C) on the domain 0 to 80, and one rule at the mean 60 of the three rows
        Assert.Equal(2, bars.Count);
        Assert.Equal([300.0, 225.0], bars.Select(b => b.Height));
        var rule = Assert.Single(rules);
        Assert.Equal(75.0, rule.Y, 6);
        Assert.Equal(75.0, rule.Y2, 6);
        Assert.Equal(["w, m"], Texts(scene, "axis-title").Where(t => t.Contains(',', StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Validates that a line and its points share one pair of axes: the encoding of the top-level view is inherited by both layers, each layer has its own marks group,
    ///     and the layers draw in order (the points over the line).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_LineAndPointShareOneAxisPair()
    {
        // Arrange: four points on a continuous x and y
        const string json = """
            {
              "data": { "values": [ { "x": 1, "y": 3 }, { "x": 2, "y": 1 }, { "x": 3, "y": 4 }, { "x": 4, "y": 2 } ] },
              "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } },
              "layer": [ { "mark": "line" }, { "mark": "point" } ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var points = Items<SceneSymbol>(scene, "mark-point");

        // Assert: one x axis and one y axis (the titles are the fields), a marks group per layer, the line first
        Assert.Equal(2, scene.FindAll("axis-domain").Count());
        Assert.Equal(["x", "y"], Texts(scene, "axis-title"));
        Assert.Equal([1, 4], MarkCounts(scene));
        Assert.Single(scene.FindAll("mark-line"));

        // Assert: the domains are 0 to 4 for both (nice), so the points are at x = 75 * x - 75... (x from 1 to 4 on 0 to 4) and y = 300 - 75 * y
        Assert.Equal([75.0, 150.0, 225.0, 300.0], points.Select(p => p.X));
        Assert.Equal([75.0, 225.0, 0.0, 150.0], points.Select(p => p.Y));
    }

    /// <summary>
    ///     Validates a bar layer with a rule at the mean: the aggregate layer shares the y scale (the domain is the union of the bars' and of the mean), the rule spans the
    ///     whole width at the mean, and the y axis title joins the two measures.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_BarsWithMeanRuleShareTheYScale()
    {
        // Arrange: the values 4, 8, 6 have the mean 6; the domain is 0 to 8
        var json = $$"""
            {
              {{CategoryData}},
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                { "mark": "rule", "encoding": { "y": { "aggregate": "mean", "field": "v", "type": "quantitative" }, "color": { "value": "red" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = Items<SceneRect>(scene, "mark-bar");
        var rule = Assert.Single(Items<SceneRule>(scene, "mark-rule"));

        // Assert: the bars rise to 150, 300 and 225 pixels, the rule is at 6 / 8 of the height from the bottom (y = 75) and as wide as the three categories (60)
        Assert.Equal([150.0, 300.0, 225.0], bars.Select(b => b.Height));
        Assert.Equal(60.0, Math.Abs(rule.X2 - rule.X), 6);
        Assert.Equal((75.0, 75.0), (rule.Y, rule.Y2));
        Assert.Equal(["a", "v, Mean of v"], Texts(scene, "axis-title"));
        Assert.Equal(["0", "1", "2", "3", "4", "5", "6", "7", "8"], Texts(scene, "axis-label").Where(t => t.Length == 1 && char.IsDigit(t[0])));
    }

    /// <summary>
    ///     Validates that the axis title of layers that share an axis follows Vega-Lite's explicit-over-implicit merge: a written title (<c>title</c> of the channel definition or
    ///     <c>axis.title</c>) wins over a title derived from the field in either layer order, derived titles join as <c>a, b</c>, different written titles join too, equal ones are one,
    ///     and a written <c>null</c> switches the title off.
    /// </summary>
    /// <param name="first">The y channel definition of the first layer (bars).</param>
    /// <param name="second">The y channel definition of the second layer (rule).</param>
    /// <param name="expected">The expected y axis title; null for no title.</param>
    [Theory]
    [InlineData("""{ "field": "v", "type": "quantitative", "title": "Crates" }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative" }""", "Crates")]
    [InlineData("""{ "aggregate": "mean", "field": "v", "type": "quantitative" }""", """{ "field": "v", "type": "quantitative", "title": "Crates" }""", "Crates")]
    [InlineData("""{ "field": "v", "type": "quantitative" }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative" }""", "v, Mean of v")]
    [InlineData("""{ "field": "v", "type": "quantitative", "title": "Crates" }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative", "title": "Mean crates" }""", "Crates, Mean crates")]
    [InlineData("""{ "field": "v", "type": "quantitative", "title": "Crates" }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative", "title": "Crates" }""", "Crates")]
    [InlineData("""{ "field": "v", "type": "quantitative", "title": "Crates" }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative", "axis": { "title": "Mine" } }""", "Crates, Mine")]
    [InlineData("""{ "field": "v", "type": "quantitative", "axis": { "title": "Mine", "grid": false } }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative" }""", "Mine")]
    [InlineData("""{ "field": "v", "type": "quantitative", "title": "Crates", "axis": { "title": "Mine" } }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative" }""", "Mine")]
    [InlineData("""{ "field": "v", "type": "quantitative", "title": null }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative" }""", null)]
    [InlineData("""{ "field": "v", "type": "quantitative", "title": ["Crates", "sold"] }""", """{ "aggregate": "mean", "field": "v", "type": "quantitative" }""", "Crates|sold")]
    public void CanvasNetVegaLite_SystemIntegration_Layer_WrittenAxisTitleWinsOverDerivedAndOthersJoin(string first, string second, string? expected)
    {
        // Arrange
        var json = $$"""
            {
              {{CategoryData}},
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": {{first}} } },
                { "mark": "rule", "encoding": { "y": {{second}} } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert: the x axis is the field of the bars; the y title is the merge, shown as one line or several
        var titles = Items<SceneText>(scene, "axis-title");
        Assert.Equal("a", titles[0].Text);
        Assert.Equal(expected is null ? 1 : 2, titles.Count);
        if (expected is not null)
        {
            Assert.Equal(expected.Split('|'), titles[1].Lines);
        }
    }

    /// <summary>
    ///     Validates the reported bug on a bar layer with a mean rule where the bars write their titles: the shared y axis is titled <c>Crates</c> (not <c>Crates, Mean of crates</c>) and
    ///     the x axis <c>Stall</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_BarsWithWrittenTitlesAndMeanRuleKeepTheWrittenTitles()
    {
        // Arrange
        const string json = """
            {
              "data": { "values": [ { "stall": "Apples", "crates": 28 }, { "stall": "Pears", "crates": 55 }, { "stall": "Plums", "crates": 43 } ] },
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "stall", "type": "nominal", "title": "Stall" }, "y": { "field": "crates", "type": "quantitative", "title": "Crates" } } },
                { "mark": { "type": "rule", "color": "firebrick" }, "encoding": { "y": { "field": "crates", "type": "quantitative", "aggregate": "mean" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Equal(["Stall", "Crates"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates the reported bug on an error band with a mean line: the band writes the titles of both axes and the line derives its own (<c>week</c>, <c>Mean of yield</c>); the
    ///     shared axes keep the written <c>Week</c> and <c>Yield</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_ErrorBandWithWrittenTitlesAndMeanLineKeepTheWrittenTitles()
    {
        // Arrange
        const string json = """
            {
              "data": { "values": [ { "week": 1, "yield": 10 }, { "week": 1, "yield": 14 }, { "week": 2, "yield": 13 }, { "week": 2, "yield": 19 }, { "week": 3, "yield": 15 }, { "week": 3, "yield": 21 } ] },
              "layer": [
                { "mark": { "type": "errorband", "extent": "stdev", "borders": true },
                  "encoding": { "x": { "field": "week", "type": "quantitative", "title": "Week" }, "y": { "field": "yield", "type": "quantitative", "title": "Yield" } } },
                { "mark": { "type": "line", "color": "black" },
                  "encoding": { "x": { "field": "week", "type": "quantitative" }, "y": { "field": "yield", "type": "quantitative", "aggregate": "mean" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Equal(["Week", "Yield"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates that an error band without written titles derives them from its field (as Vega-Lite's composite marks write them), so the line's <c>Mean of yield</c> does not join
    ///     the band's <c>yield</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_ErrorBandWithoutTitlesWritesItsFieldAsTheTitle()
    {
        // Arrange
        const string json = """
            {
              "data": { "values": [ { "week": 1, "yield": 10 }, { "week": 1, "yield": 14 }, { "week": 2, "yield": 13 }, { "week": 2, "yield": 19 } ] },
              "layer": [
                { "mark": "errorband", "encoding": { "x": { "field": "week", "type": "quantitative" }, "y": { "field": "yield", "type": "quantitative" } } },
                { "mark": "line", "encoding": { "x": { "field": "week", "type": "quantitative" }, "y": { "field": "yield", "type": "quantitative", "aggregate": "mean" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Equal(["week", "yield"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates text on bars: the encoding of the view is inherited, the labels sit at the bar tops on the center of the bands.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_TextLabelsSitOnTheBars()
    {
        // Arrange: the encoding of x and y is written once for both layers
        var json = $$"""
            {
              {{CategoryData}},
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } },
              "layer": [ { "mark": "bar" }, { "mark": { "type": "text", "dy": -5 }, "encoding": { "text": { "field": "v", "type": "quantitative" } } } ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = Items<SceneRect>(scene, "mark-bar");
        var labels = Items<SceneText>(scene, "mark-text");

        // Assert: the text is at the center of its band and at the top of its bar
        Assert.Equal(3, labels.Count);
        Assert.Equal(["4", "8", "6"], labels.Select(t => t.Text));
        Assert.Equal(bars.Select(b => b.X + (b.Width / 2)), labels.Select(t => t.X));
        Assert.Equal(bars.Select(b => b.Y), labels.Select(t => t.Y));
        Assert.Equal([3, 3], MarkCounts(scene));
    }

    /// <summary>
    ///     Validates <c>resolve</c> with independent y scales: each layer maps its own domain, the second axis is on the opposite side without a grid, and each layer's marks
    ///     sit where its own scale puts them.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_IndependentYScalesDrawTwoAxes()
    {
        // Arrange: bars of v on 0 to 8, points of w on 0 to 1000
        var json = $$"""
            {
              {{CategoryData}},
              "resolve": { "scale": { "y": "independent" } },
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                { "mark": "point", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "w", "type": "quantitative" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = Items<SceneRect>(scene, "mark-bar");
        var points = Items<SceneSymbol>(scene, "mark-point");
        var domains = scene.FindAll("axis-domain").ToList();
        var ticks = scene.FindAll("axis-tick").Select(e => (SceneRule)e.Item).ToList();

        // Assert: each layer is on its own scale (the bar of 8 reaches the top, the point of 1000 reaches the top)
        Assert.Equal([150.0, 0.0, 75.0], bars.Select(b => b.Y));
        Assert.Equal([270.0, 180.0, 0.0], points.Select(p => p.Y));

        // Assert: one x axis, a y axis on the left and a second y axis on the right (the width of the data rectangle, 60, to the right), with ticks pointing outwards
        Assert.Equal(3, domains.Count);
        Assert.Equal(60.0, domains[2].OffsetX - domains[1].OffsetX, 6);
        Assert.Contains(ticks, t => Math.Abs(t.X2 + 5) < 1e-9);
        Assert.Contains(ticks, t => Math.Abs(t.X2 - 5) < 1e-9);
        Assert.Equal(["0", "1", "2", "3", "4", "5", "6", "7", "8", "0", "100", "200", "300", "400", "500", "600", "700", "800", "900", "1,000"], Texts(scene, "axis-label").Where(t => t.Length > 0 && char.IsDigit(t[0])));
        Assert.Equal(["a", "v", "w"], Texts(scene, "axis-title"));

        // Assert: only the first y axis has a grid (the band scale of the x axis has none)
        Assert.Single(scene.FindAll("axis-grid-group"));
    }

    /// <summary>
    ///     Validates <c>resolve.axis</c> alone: one shared scale over both layers with an axis for each, the second on the opposite side.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_IndependentAxesOfASharedScaleBothShowTheSameTicks()
    {
        // Arrange
        var json = $$"""
            {
              {{CategoryData}},
              "resolve": { "axis": { "y": "independent" } },
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                { "mark": "point", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var domains = scene.FindAll("axis-domain").ToList();

        // Assert: the two y axes show the same labels; the scale is shared so the point sits on the top of its bar
        Assert.Equal(3, domains.Count);
        Assert.Equal(60.0, domains[2].OffsetX - domains[1].OffsetX, 6);
        var labels = Texts(scene, "axis-label").Where(t => t.Length == 1 && char.IsDigit(t[0])).ToList();
        Assert.Equal(18, labels.Count);
        Assert.Equal(labels.Take(9), labels.Skip(9));
        Assert.Equal(Items<SceneRect>(scene, "mark-bar").Select(b => b.Y), Items<SceneSymbol>(scene, "mark-point").Select(p => p.Y));
    }

    /// <summary>
    ///     Validates nested layers: the encoding of the outer view and of the inner layered view is inherited by the layers inside, and each leaf has its own marks group.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_NestedLayersInheritTheEncodingOfEveryLevel()
    {
        // Arrange: y from the top level, x from the inner layered view, a rule whose y is a constant on the same scale (and that has no x)
        var json = $$"""
            {
              {{CategoryData}},
              "encoding": { "y": { "field": "v", "type": "quantitative" } },
              "layer": [
                { "encoding": { "x": { "field": "a", "type": "nominal" } },
                  "layer": [ { "mark": "bar" }, { "mark": "point", "encoding": { "color": { "value": "red" } } } ] },
                { "mark": "rule", "encoding": { "y": { "datum": 5 } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = Items<SceneRect>(scene, "mark-bar");
        var points = Items<SceneSymbol>(scene, "mark-point");
        var rules = Items<SceneRule>(scene, "mark-rule");

        // Assert: three marks groups, the points are at the top center of the bars; the rule layer draws a rule for each of the three rows, at 5 on the shared 0 to 8 scale (300 - 300 * 5 / 8 = 112.5)
        Assert.Equal([3, 3, 3], MarkCounts(scene));
        Assert.Equal(bars.Select(b => b.X + (b.Width / 2)), points.Select(p => p.X));
        Assert.Equal(bars.Select(b => b.Y), points.Select(p => p.Y));
        Assert.Equal([112.5, 112.5, 112.5], rules.Select(r => r.Y));
    }

    /// <summary>
    ///     Validates that a layer with its own <c>data</c> replaces the inherited data, and that the scales are still shared: the combined domain covers both tables.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_OwnDataReplacesTheInheritedDataAndSharesTheScale()
    {
        // Arrange: the bars of the top-level data reach 8; the layer's own table has one row of 10 in category A
        var json = $$"""
            {
              {{CategoryData}},
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } },
              "layer": [
                { "mark": "bar" },
                { "mark": "point", "data": { "values": [ { "a": "A", "v": 10 } ] } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = Items<SceneRect>(scene, "mark-bar");
        var point = Assert.Single(Items<SceneSymbol>(scene, "mark-point"));

        // Assert: the union of the domains is 0 to 10, so the bar of 4 is 120 pixels high, the point of 10 is at the top, centered on category A (x = 10)
        Assert.Equal([120.0, 240.0, 180.0], bars.Select(b => b.Height));
        Assert.Equal(0.0, point.Y, 6);
        Assert.Equal(10.0, point.X, 6);
    }

    /// <summary>
    ///     Validates step sizing through layers: the width follows the union of the categories of the layers times the step of the view.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_StepSizeUsesTheMergedDiscreteScale()
    {
        // Arrange: A and B in the first layer, C in the second; a step of 30 gives 3 * 30 = 90 pixels
        const string json = """
            {
              "width": { "step": 30 },
              "data": { "values": [ { "a": "A", "v": 1 }, { "a": "B", "v": 2 } ] },
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } },
              "layer": [
                { "mark": "bar" },
                { "mark": "bar", "data": { "values": [ { "a": "C", "v": 2 } ] } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = Items<SceneRect>(scene, "mark-bar");
        var domain = (SceneRule)scene.FindAll("axis-domain").First().Item;

        // Assert: the x axis spans 90 pixels and the bars sit on the centers of the three bands (15, 45 and 75)
        Assert.Equal(90.0, domain.X2 - domain.X, 6);
        Assert.Equal([15.0, 45.0, 75.0], bars.Select(b => b.X + (b.Width / 2)));
        Assert.Equal(["A", "B", "C"], Texts(scene, "axis-label").Where(t => t is "A" or "B" or "C"));
    }

    /// <summary>
    ///     Validates that the size that <c>Measure</c> reports is the size of the surface that <c>Render</c> returns for a layered chart, and that rendering is deterministic.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_MeasureMatchesRenderAndRenderIsDeterministic()
    {
        // Arrange
        var json = $$"""
            {
              {{CategoryData}},
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "a", "type": "nominal" } },
              "layer": [ { "mark": "bar" }, { "mark": "point" } ]
            }
            """;
        var document = VegaLiteDocument.Parse(json);

        // Act
        var size = document.Measure(cancellationToken: Ct);
        using var first = document.Render(cancellationToken: Ct);
        using var second = document.Render(cancellationToken: Ct);

        // Assert
        Assert.Equal((size.Width, size.Height), (first.Width, first.Height));
        Assert.Equal(VegaLiteTestHelpers.Snapshot(first), VegaLiteTestHelpers.Snapshot(second));
    }

    /// <summary>
    ///     Validates that layers with a color field of the same name share one legend, titled once, and that the legend is drawn once.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_SharedColorFieldGivesOneLegend()
    {
        // Arrange: the color is inherited by the bars and the points
        var json = $$"""
            {
              {{CategoryData}},
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "a", "type": "nominal" } },
              "layer": [ { "mark": "bar" }, { "mark": "point" } ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert: one legend with three entries and one title
        Assert.Single(scene.FindAll("legend"));
        Assert.Equal(["A", "B", "C"], Texts(scene, "legend-label"));
        Assert.Equal(["a"], Texts(scene, "legend-title"));
    }

    /// <summary>
    ///     Validates that layers whose color fields differ but share the color scale have one legend whose title joins the fields.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_ColorFieldsOfDifferentNamesJoinTheLegendTitle()
    {
        // Arrange: the bars color by a, the points by b (the same categories under another name)
        const string json = """
            {
              "data": { "values": [ { "a": "A", "b": "A", "v": 4 }, { "a": "B", "b": "B", "v": 8 } ] },
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } },
              "layer": [
                { "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" } } },
                { "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Single(scene.FindAll("legend"));
        Assert.Equal(["a, b"], Texts(scene, "legend-title"));
        Assert.Equal(["A", "B"], Texts(scene, "legend-label"));
    }

    /// <summary>
    ///     Validates that an independent legend gives each layer its own legend.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_IndependentLegendsGiveOneLegendPerLayer()
    {
        // Arrange: both layers color by a, but the legends are independent
        var json = $$"""
            {
              {{CategoryData}},
              "resolve": { "legend": { "color": "independent" } },
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "a", "type": "nominal" } },
              "layer": [ { "mark": "bar" }, { "mark": "point" } ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Equal(2, scene.FindAll("legend").Count());
        Assert.Equal(["a", "a"], Texts(scene, "legend-title"));
    }

    /// <summary>
    ///     Validates that layers whose scales cannot be merged are reported as a warning, keep their own scale and are still drawn.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_ScaleConflictWarnsAndStillDraws()
    {
        // Arrange: x is a band scale in the bars and a linear scale in the line
        var json = $$"""
            {
              {{CategoryData}},
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                { "mark": "line", "encoding": { "x": { "field": "w", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative" } } }
              ]
            }
            """;

        // Act
        var (warnings, unsupported) = Report(json);
        var scene = SceneOf(json);

        // Assert
        Assert.Empty(unsupported);
        var warning = Assert.Single(warnings);
        Assert.StartsWith("vl-layer-scale-conflict:", warning, StringComparison.Ordinal);
        Assert.Equal(3, Items<SceneRect>(scene, "mark-bar").Count);
        Assert.Single(scene.FindAll("mark-line"));
    }

    /// <summary>
    ///     Validates that the properties of a layer that only the top-level view decides are warned about and ignored.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_PropertiesOfTheTopLevelViewAreIgnoredInALayer()
    {
        // Arrange: the layer asks for a width of 500; the top-level view has none
        var json = $$"""
            {
              {{CategoryData}},
              "layer": [ { "mark": "bar", "width": 500, "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } } ]
            }
            """;

        // Act
        var (warnings, unsupported) = Report(json);
        var domain = (SceneRule)SceneOf(json).FindAll("axis-domain").First().Item;

        // Assert: one warning and the default step of 20 pixels for each of the three categories
        Assert.Empty(unsupported);
        Assert.Equal(["vl-layer-property-ignored"], warnings.Select(w => w[..w.IndexOf(':', StringComparison.Ordinal)]));
        Assert.Equal(60.0, domain.X2 - domain.X, 6);
    }

    /// <summary>
    ///     Validates that a layer with a mark that is not drawn is reported and the other layers are drawn.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_UnsupportedLayerMarkIsReportedAndOthersAreDrawn()
    {
        // Arrange
        var json = $$"""
            {
              {{CategoryData}},
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } },
              "layer": [ { "mark": "bar" }, { "mark": "geoshape" } ]
            }
            """;

        // Act
        var (_, unsupported) = Report(json);
        var scene = SceneOf(json);

        // Assert
        Assert.Equal(["vl-mark-geoshape"], unsupported.Select(u => u[..u.IndexOf(':', StringComparison.Ordinal)]));
        Assert.Equal(3, Items<SceneRect>(scene, "mark-bar").Count);
    }

    /// <summary>
    ///     Validates that the transform of a view in a concatenation, which is still not drawn, is reported with a message that names the concatenation.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_TransformInAConcatenationIsReportedAsNotApplied()
    {
        // Arrange: the layered view is inside an hconcat, which is not drawn
        const string json = """
            {
              "hconcat": [ { "transform": [ { "calculate": "datum.v * 10", "as": "w" } ], "layer": [ { "mark": "bar" } ] } ]
            }
            """;

        // Act
        var (_, unsupported) = Report(json);

        // Assert: the identifier is unchanged and the message names the composition
        Assert.Contains(
            "vl-transform-calculate: The 'calculate' transform is not applied: the view is a 'hconcat' composition, which is not supported yet.",
            unsupported);
        Assert.DoesNotContain(unsupported, u => u.Contains("vl-composition-layer", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates the limits on layers: the number of views and the depth of nested layers are enforced with an error at the offending layer.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_ViewAndDepthLimitsAreEnforced()
    {
        // Arrange: three layers (four views with the layered view) and layers nested two levels deep
        const string three = """{ "layer": [ { "mark": "bar" }, { "mark": "point" }, { "mark": "line" } ] }""";
        const string nested = """{ "layer": [ { "layer": [ { "mark": "bar" } ] } ] }""";

        // Act
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => Report(three, new VegaLiteLimits { MaxViews = 3 }));
        var tooDeep = Assert.Throws<VegaLiteRenderException>(() => Report(nested, new VegaLiteLimits { MaxCompositionDepth = 1 }));

        // Assert
        Assert.Contains("MaxViews", tooMany.Message, StringComparison.Ordinal);
        Assert.Contains("MaxCompositionDepth", tooDeep.Message, StringComparison.Ordinal);
        Assert.Equal("/layer/0/layer", tooDeep.Location.Pointer);
        Assert.Empty(Report(three, new VegaLiteLimits { MaxViews = 4 }).Warnings);
        Assert.Empty(Report(nested, new VegaLiteLimits { MaxCompositionDepth = 2 }).Warnings);
    }

    /// <summary>
    ///     Validates that a layered view with an empty <c>layer</c> array draws only its title and background, like a view without a mark.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_EmptyLayerListDrawsOnlyTheBackground()
    {
        // Arrange / Act
        using var surface = VegaLiteDocument.Parse("""{ "layer": [] }""").Render(cancellationToken: Ct);
        using var empty = VegaLiteDocument.Parse("{}").Render(cancellationToken: Ct);

        // Assert: the same pixels and size as a view without a mark
        Assert.Equal(VegaLiteTestHelpers.Snapshot(empty), VegaLiteTestHelpers.Snapshot(surface));
        Assert.Equal(new VegaLiteSize(310, 310), VegaLiteDocument.Parse("""{ "layer": [] }""").Measure(cancellationToken: Ct));
    }

    /// <summary>
    ///     Validates that a variable parameter declared inside a layer is still reported (it is not computed yet) while the layer is drawn.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Layer_VariableParameterInALayerIsStillReported()
    {
        // Arrange
        var json = $$"""
            {
              {{CategoryData}},
              "layer": [ { "params": [ { "name": "p", "value": 1 } ], "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } } ]
            }
            """;

        // Act
        var (_, unsupported) = Report(json);

        // Assert
        Assert.Equal(["vl-param-variable"], unsupported.Select(u => u[..u.IndexOf(':', StringComparison.Ordinal)]));
    }
}
