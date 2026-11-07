using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the parameters of a specification (Phase 12, evaluation and expression references): charts of small inline specifications are measured and laid out
///     through the public API, and the scene items are checked against numbers derived by hand from the scale definitions (a linear scale of the domain [d0, d1] over a data rectangle of 300
///     pixels puts the value v at 300 * (v - d0) / (d1 - d0)).
/// </summary>
public class VegaLiteParamsIntegrationTests
{
    /// <summary>The three rows of the charts: x is 1, 4 and 7.</summary>
    private const string Rows = """ "data": { "values": [ { "a": 1, "b": 1 }, { "a": 4, "b": 2 }, { "a": 7, "b": 3 } ] } """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a circle chart of the three rows with a data rectangle of 300 by 200 pixels.
    /// </summary>
    /// <param name="parameters">The <c>params</c> array (written out), or an empty text for none.</param>
    /// <param name="xScale">The <c>scale</c> of the x channel (written out), or an empty text for none.</param>
    /// <param name="extra">More top-level properties, each ending with a comma.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string parameters = "", string xScale = "", string extra = "")
    {
        var scale = xScale.Length == 0 ? string.Empty : ", \"scale\": " + xScale;
        var declared = parameters.Length == 0 ? string.Empty : "\"params\": " + parameters + ",";
        return "{ " + extra + declared + " \"width\": 300, \"height\": 200, " + Rows + ", \"mark\": \"circle\", "
            + "\"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\"" + scale + " }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\" } } }";
    }

    /// <summary>
    ///     Lays out a chart and returns the scene and the reports of the call.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="parameters">The values of the caller, or null.</param>
    /// <param name="reports">Receives the ids of the unsupported features and warnings, in the order delivered.</param>
    /// <returns>The scene.</returns>
    private static Scene Layout(string json, IReadOnlyDictionary<string, object?>? parameters, List<string> reports)
    {
        var options = new VegaLiteRenderOptions
        {
            Parameters = parameters,
            WarningHandler = w => reports.Add(w.Message),
            UnsupportedFeatureHandler = u => reports.Add(u.Feature + ": " + u.Message),
        };
        var document = VegaLiteDocument.Parse(json);

        // The reports are delivered by measuring; the scene is laid out the same way
        document.Measure(options, Ct);
        return document.BuildScene(options, Ct);
    }

    /// <summary>
    ///     Gets the x positions of the circles of a scene, in row order.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The x positions in the data rectangle.</returns>
    private static List<double> Xs(Scene scene) => [.. scene.FindAll("mark-circle").Select(e => ((SceneSymbol)e.Item).X)];

    /// <summary>
    ///     Checks positions against the hand-derived values to a billionth of a pixel (the scale multiplies by the range before it divides).
    /// </summary>
    /// <param name="expected">The expected positions.</param>
    /// <param name="actual">The positions of the scene.</param>
    private static void AssertPositions(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], actual[i], 9);
        }
    }

    /// <summary>
    ///     Validates that the expression references of the title, the padding and the background are evaluated with the parameters (the expression of a parameter wins over its value and a
    ///     parameter reads another): the chart is the one that the same literals give, in strict mode and without a report, and the padding of 10 makes it 10 pixels larger in both
    ///     directions than the default padding of 5.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_ExpressionReferencesDriveTitleBackgroundAndPadding()
    {
        // Arrange: size 9, the label 'Total 18', a padding of 10 and a background; the same chart with the literals; and the chart with the default padding
        const string paramsJson = """[ { "name": "size", "value": 9 }, { "name": "label", "value": "unused", "expr": "'Total ' + size * 2" } ]""";
        var driven = Chart(paramsJson, extra: """ "title": { "text": { "expr": "label" }, "fontSize": { "expr": "size + 9" } }, "padding": { "expr": "size + 1" }, "background": { "expr": "'#ffeecc'" }, """);
        var literal = Chart(extra: """ "title": { "text": "Total 18", "fontSize": 18 }, "padding": 10, "background": "#ffeecc", """);
        var plain = Chart(extra: """ "title": { "text": "Total 18", "fontSize": 18 }, "background": "#ffeecc", """);
        var reports = new List<string>();

        // Act
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => reports.Add(w.Message) };
        var document = VegaLiteDocument.Parse(driven);
        document.Measure(options, Ct);
        var scene = document.BuildScene(options, Ct);
        var expected = VegaLiteDocument.Parse(literal).BuildScene(new VegaLiteRenderOptions(), Ct);
        var defaultPadding = VegaLiteDocument.Parse(plain).BuildScene(new VegaLiteRenderOptions(), Ct);

        // Assert: nothing was reported, and the chart is the chart of the literals
        Assert.Empty(reports);
        var title = Assert.IsType<SceneText>(scene.Find("title-text")!.Value.Item);
        Assert.Equal("Total 18", title.Text);
        Assert.Equal(18, title.FontSize);
        Assert.Equal(expected.Background, scene.Background);
        Assert.Equal((expected.Width, expected.Height), (scene.Width, scene.Height));
        Assert.Equal(((SceneText)expected.Find("title-text")!.Value.Item).Text, title.Text);

        // Assert: the padding of 10 against the default of 5 is 5 more on each of the four sides
        Assert.Equal(defaultPadding.Width + 10, scene.Width);
        Assert.Equal(defaultPadding.Height + 10, scene.Height);
    }

    /// <summary>
    ///     Validates that an expression reference at a property of an axis, a scale or the configuration is a value for the stages that read it: the label size of the axes, the
    ///     <c>zero</c> of a scale (the domain of 1 to 7 instead of 0 to 7 puts the first circle at 0, not at 300 * 1 / 7), and the label size of the configuration.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_ExpressionReferencesReachAxesScalesAndConfiguration()
    {
        // Arrange
        const string paramsJson = """[ { "name": "px", "value": 14 }, { "name": "keepZero", "value": false } ]""";
        var axis = Chart(paramsJson).Replace("\"type\": \"quantitative\"", "\"type\": \"quantitative\", \"axis\": { \"labelFontSize\": { \"expr\": \"px\" } }", StringComparison.Ordinal);
        var config = Chart(paramsJson, extra: """ "config": { "axis": { "labelFontSize": { "expr": "px + 1" } } }, """);
        var reports = new List<string>();

        // Act
        var axisScene = Layout(axis, null, reports);
        var configScene = Layout(config, null, reports);
        var withoutZero = Layout(Chart(paramsJson, """{ "zero": { "expr": "keepZero" } }"""), null, reports);
        var zeroed = Layout(Chart(paramsJson, """{ "zero": { "expr": "!keepZero" } }"""), null, reports);

        // Assert: the labels of the axes have the size of the parameter, and those of the configuration one more
        var axisSizes = axisScene.FindAll("axis-label").Select(e => ((SceneText)e.Item).FontSize).Distinct().Order().ToList();
        Assert.Contains(14.0, axisSizes);
        Assert.Contains(15.0, configScene.FindAll("axis-label").Select(e => ((SceneText)e.Item).FontSize));

        // Assert: the domain 1 to 7 puts 1 at the start of the range, 4 at 300 * 3 / 6 and 7 at the end; the domain 0 to 7 puts them at 300 * v / 7
        Assert.Equal([0.0, 150.0, 300.0], Xs(withoutZero));
        Assert.Equal([300.0 / 7, 1200.0 / 7, 300.0], Xs(zeroed));
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the domain of a scale that a variable parameter gives (<c>domain: {"param": ...}</c>): the circles are placed by the raw domain [2, 6] (300 * (v - 2) / 4: -75, 150 and 375),
    ///     the marks are clipped to the data rectangle, and nothing adjusts the domain (the zero rule, <c>nice</c>, <c>padding</c> and <c>domainMin</c> are not applied to a raw domain).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_DomainOfAParameterIsRawAndClipsTheMarks()
    {
        // Arrange: the same domain without and with the adjustments that Vega's configureDomain skips for a raw domain
        const string paramsJson = """[ { "name": "xr", "value": [2, 6] } ]""";
        var reports = new List<string>();

        // Act
        var plain = Layout(Chart(paramsJson, """{ "domain": { "param": "xr" } }"""), null, reports);
        var adjusted = Layout(Chart(paramsJson, """{ "domain": { "param": "xr" }, "zero": true, "nice": true, "padding": 30, "domainMin": 0, "domainMax": 100 }"""), null, reports);
        var explicitDomain = Layout(Chart("", """{ "domain": [2, 6] }"""), null, reports);

        // Assert: the raw domain places the circles by 300 * (v - 2) / 4, adjusted or not
        Assert.Equal([-75.0, 150.0, 375.0], Xs(plain));
        Assert.Equal([-75.0, 150.0, 375.0], Xs(adjusted));

        // Assert: the marks are clipped to the data rectangle, which an explicit domain of the same numbers does not do
        var clip = Assert.IsType<SceneGroup>(plain.Find("marks")!.Value.Item).Clip;
        Assert.Equal(new SceneClip(0, 0, 300, 200), clip);
        Assert.Null(Assert.IsType<SceneGroup>(explicitDomain.Find("marks")!.Value.Item).Clip);
        Assert.Equal([-75.0, 150.0, 375.0], Xs(explicitDomain));
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that the caller's value replaces the default of the parameter that gives a domain, also an array: [0, 8] puts the circles at 300 * v / 8 (37.5, 150 and 262.5); a
    ///     <see langword="null"/> clears it (the scale keeps the domain of its data), as a parameter that never had a value.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_TheCallerOverridesTheDomainOfAParameter()
    {
        // Arrange
        const string paramsJson = """[ { "name": "xr", "value": [2, 6] } ]""";
        var chart = Chart(paramsJson, """{ "domain": { "param": "xr" } }""");
        var none = Chart("""[ { "name": "xr" } ]""", """{ "domain": { "param": "xr" } }""");
        var reports = new List<string>();

        // Act
        var replaced = Layout(chart, new Dictionary<string, object?> { ["xr"] = new object[] { 0, 8 } }, reports);
        var cleared = Layout(chart, new Dictionary<string, object?> { ["xr"] = null }, reports);
        var neverSet = Layout(none, null, reports);
        var noDomain = Layout(Chart(), null, reports);

        // Assert
        Assert.Equal([37.5, 150.0, 262.5], Xs(replaced));
        Assert.Equal(Xs(noDomain), Xs(cleared));
        Assert.Equal(Xs(noDomain), Xs(neverSet));
        Assert.Empty(reports);

        // Assert: the domain of a parameter clips the marks even when the parameter has no value (Vega-Lite's scaleClip looks for the extent, not its value)
        Assert.NotNull(Assert.IsType<SceneGroup>(cleared.Find("marks")!.Value.Item).Clip);
    }

    /// <summary>
    ///     Validates the <c>domainRaw</c> of a scale: [0.5, 9.5] places the circles at 300 * (v - 0.5) / 9; it wins over the domain a parameter gives, and, unlike a parameter, it does not clip.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_DomainRawIsTheDomain()
    {
        // Arrange
        var reports = new List<string>();

        // Act
        var raw = Layout(Chart(xScale: """{ "domainRaw": [0.5, 9.5], "domain": [100, 200] }"""), null, reports);
        var both = Layout(Chart("""[ { "name": "xr", "value": [2, 6] } ]""", """{ "domainRaw": [0.5, 9.5], "domain": { "param": "xr" } }"""), null, reports);

        // Assert
        double[] expected = [300 * 0.5 / 9, 300 * 3.5 / 9, 300 * 6.5 / 9];
        AssertPositions(expected, Xs(raw));
        AssertPositions(expected, Xs(both));
        Assert.Null(Assert.IsType<SceneGroup>(raw.Find("marks")!.Value.Item).Clip);
        Assert.NotNull(Assert.IsType<SceneGroup>(both.Find("marks")!.Value.Item).Clip);
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a raw domain that no scale can use (a text, an array of one number, an empty array, a non-numeric entry) is reported as <c>vl-scale-domain-raw</c> and the scale keeps
    ///     the domain of its data.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_AnUnusableRawDomainIsReportedAndIgnored()
    {
        // Arrange
        var chart = Chart("""[ { "name": "xr", "value": [2, 6] } ]""", """{ "domain": { "param": "xr" } }""");
        var expected = Xs(Layout(Chart(), null, []));
        object?[] unusable = ["abc", new object[] { 3 }, new object[] { }, new object[] { 1, "x" }, 5.0, true];

        foreach (var value in unusable)
        {
            // Act
            var reports = new List<string>();
            var scene = Layout(chart, new Dictionary<string, object?> { ["xr"] = value }, reports);

            // Assert: one warning that names the scale, and the chart of the data's domain
            var report = Assert.Single(reports);
            Assert.Contains("must be an array of at least two numbers or dates", report, StringComparison.Ordinal);
            Assert.Equal(expected, Xs(scene));
        }
    }

    /// <summary>
    ///     Validates the domain of a discrete scale that a parameter gives: the categories are the value of the parameter in its order (<c>["c", "a"]</c>), so the bar of c is left of the bar of a.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_TheDomainOfADiscreteScaleIsTheValueOfTheParameter()
    {
        // Arrange: two bars, a then c in the data
        const string json = """
            {
              "params": [ { "name": "order", "value": ["c", "a"] } ],
              "width": 300, "height": 100,
              "data": { "values": [ { "k": "a", "v": 1 }, { "k": "c", "v": 2 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal", "scale": { "domain": { "param": "order" } } }, "y": { "field": "v", "type": "quantitative" } }
            }
            """;
        var sorted = json.Replace("""{ "domain": { "param": "order" } }""", """{ "domain": ["a", "c"] }""", StringComparison.Ordinal);

        // Act
        var bars = VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();
        var expected = VegaLiteDocument.Parse(sorted).BuildScene(new VegaLiteRenderOptions(), Ct).FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Assert: the domain [c, a] mirrors the domain [a, c]: the bar of c takes the first band, the bar of a the second, with the same bands
        Assert.Equal(expected[0].X, bars[1].X, 6);
        Assert.Equal(expected[1].X, bars[0].X, 6);
        Assert.True(bars[1].X < bars[0].X);
        Assert.Equal(bars[0].Width, bars[1].Width, 6);
    }

    /// <summary>
    ///     Validates the raw domain of the layers of a view: the layers share the x scale, the domain a parameter gives to one of them is the domain of all, and the marks of every layer are clipped.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_ALayerThatTakesItsDomainFromAParameterSetsTheSharedScale()
    {
        // Arrange: a circle layer without a domain and a square layer that takes it from the parameter (the top-level parameter is visible in both)
        const string json = """
            {
              "params": [ { "name": "xr", "value": [2, 6] } ],
              "width": 300, "height": 200,
              "data": { "values": [ { "a": 1, "b": 1 }, { "a": 4, "b": 2 }, { "a": 7, "b": 3 } ] },
              "layer": [
                { "mark": "circle", "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } },
                { "mark": "square", "encoding": { "x": { "field": "a", "type": "quantitative", "scale": { "domain": { "param": "xr" }, "nice": true } }, "y": { "field": "b", "type": "quantitative" } } }
              ]
            }
            """;

        // Act
        var scene = VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

        // Assert: both layers use the domain [2, 6], unadjusted
        Assert.Equal([-75.0, 150.0, 375.0], scene.FindAll("mark-circle").Select(e => ((SceneSymbol)e.Item).X));
        Assert.Equal([-75.0, 150.0, 375.0], scene.FindAll("mark-square").Select(e => ((SceneSymbol)e.Item).X));
        Assert.All(scene.FindAll("marks"), e => Assert.Equal(new SceneClip(0, 0, 300, 200), Assert.IsType<SceneGroup>(e.Item).Clip));
    }

    /// <summary>
    ///     Validates the variable parameters of a nested view: Vega-Lite compiles no signal for them, so they are reported as <c>vl-param-variable</c> with that explanation and an
    ///     expression that reads one sees null (the title keeps its default size, not the 30 of the nested parameter).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_AVariableParameterOfANestedViewIsNotASignal()
    {
        // Arrange
        const string nested = """
            {
              "hconcat": [
                { "params": [ { "name": "q", "value": 30 } ], "title": { "text": "T", "fontSize": { "expr": "q" } }, "width": 100, "height": 100,
                  "data": { "values": [ { "a": 1 } ] }, "mark": "circle", "encoding": { "x": { "field": "a", "type": "quantitative" } } }
              ]
            }
            """;
        var without = nested.Replace(""", "fontSize": { "expr": "q" }""", string.Empty, StringComparison.Ordinal);
        var reports = new List<string>();

        // Act
        var scene = Layout(nested, null, reports);
        var expected = Layout(without, null, []);

        // Assert: the report names the rule, and the title has the default size
        var report = Assert.Single(reports, r => r.StartsWith("vl-param-variable", StringComparison.Ordinal));
        Assert.Contains("top-level view", report, StringComparison.Ordinal);
        Assert.Equal(((SceneText)expected.Find("title-text")!.Value.Item).FontSize, ((SceneText)scene.Find("title-text")!.Value.Item).FontSize);
    }

    /// <summary>
    ///     Validates the reports of a static expression reference: one that reads <c>datum</c> is reported as <c>vl-expr-ref</c> (a static site has no row) and the property keeps its default; a date
    ///     at a property that takes none is a warning of the call.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_UnfoldableReferencesAreReported()
    {
        // Arrange
        var datum = Chart(extra: """ "title": { "text": "T", "fontSize": { "expr": "datum.a" } }, """);
        var date = Chart(extra: """ "title": { "text": "T", "fontSize": { "expr": "datetime(2020, 0, 1)" } }, """);
        var reports = new List<string>();

        // Act
        var scene = Layout(datum, null, reports);
        var dateReports = new List<string>();
        var dateScene = Layout(date, null, dateReports);

        // Assert
        Assert.Contains(reports, r => r.StartsWith("vl-expr-ref", StringComparison.Ordinal) && r.Contains("datum", StringComparison.Ordinal));
        Assert.Contains(dateReports, r => r.StartsWith("vl-expr-ref", StringComparison.Ordinal) && r.Contains("date", StringComparison.Ordinal));
        Assert.Equal(((SceneText)scene.Find("title-text")!.Value.Item).FontSize, ((SceneText)dateScene.Find("title-text")!.Value.Item).FontSize);
    }

    /// <summary>
    ///     Validates that the caller can give the domain of a time scale as dates: the dates of the caller are the instants of the domain (2020-01-01 to 2020-01-03 over 300 pixels puts
    ///     2020-01-02 at 150 and 2020-01-03 at 300).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_TheCallerGivesTheDomainOfATimeScaleAsDates()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "span" } ],
              "width": 300, "height": 200,
              "data": { "values": [ { "t": "2020-01-02", "b": 1 }, { "t": "2020-01-03", "b": 2 } ] },
              "mark": "circle",
              "encoding": { "x": { "field": "t", "type": "temporal", "scale": { "domain": { "param": "span" } } }, "y": { "field": "b", "type": "quantitative" } }
            }
            """;
        var span = new object[] { new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTimeOffset(2020, 1, 3, 0, 0, 0, TimeSpan.Zero) };

        // Act
        var scene = Layout(json, new Dictionary<string, object?> { ["span"] = span }, []);

        // Assert
        AssertPositions([150.0, 300.0], Xs(scene));
    }

    /// <summary>
    ///     Validates that an expression reference in the <c>Config</c> of the render options is reported as <c>vl-expr-ref</c> with its pointer in the text (nothing evaluates it, so the
    ///     property would silently keep its default), in document order, and that the <c>config</c> of the specification, which is evaluated, is not reported.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_AnExpressionReferenceInTheCallerConfigIsReported()
    {
        // Arrange: one reference in an object and one in an array of the caller's configuration, and one in the specification's own
        var document = VegaLiteDocument.Parse(Chart(extra: """ "config": { "axis": { "labelFontSize": { "expr": "12" } } }, """));
        var reports = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            Config = """{ "axis": { "titleFontSize": { "expr": "12" } }, "legend": { "x": [ 1, { "expr": "1" } ] }, "view": { "stroke": "red" } }""",
            UnsupportedFeatureHandler = u => reports.Add(u.Feature + ": " + u.Message),
        };

        // Act
        document.Measure(options, Ct);

        // Assert: exactly the two references of the caller's text, at their pointers, in document order
        Assert.Equal(2, reports.Count);
        Assert.StartsWith("vl-expr-ref:", reports[0], StringComparison.Ordinal);
        Assert.Contains("/axis/titleFontSize", reports[0], StringComparison.Ordinal);
        Assert.Contains("/legend/x/1", reports[1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the value of an expression reference is the same for every call and every degree of parallelism, and that the document is not changed by a call (it is immutable).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Params_FoldingIsDeterministicAndLeavesTheDocumentAlone()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Chart("""[ { "name": "px", "value": 12 } ]""", extra: """ "title": { "text": { "expr": "'p' + px" }, "fontSize": { "expr": "px" } }, """));
        var overrides = new Dictionary<string, object?> { ["px"] = 20 };

        // Act: concurrent calls with and without an override
        var scenes = Enumerable.Range(0, 8).AsParallel().Select(i => document.BuildScene(i % 2 == 0 ? new VegaLiteRenderOptions() : new VegaLiteRenderOptions { Parameters = overrides }, Ct)).ToList();

        // Assert: the even calls draw 'p12' at 12, the odd ones 'p20' at 20
        var texts = scenes.Select(s => ((SceneText)s.Find("title-text")!.Value.Item)).Select(t => (t.Text, t.FontSize)).Distinct().Order().ToList();
        Assert.Equal([("p12", 12.0), ("p20", 20.0)], texts);
    }
}
