using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the extent that a parameter gives a scale or a bin (Phase 12): <c>"bind": "scales"</c>, <c>scale.domain</c> of the form <c>{"param": ...}</c> and
///     <c>bin.extent</c> of that form, for the selection parameters and the variable parameters. The charts are laid out through the public API and the positions are worked out by hand: a
///     domain [lo, hi] over 300 pixels maps v to (v - lo) * 300 / (hi - lo), and over 200 pixels upward in y.
/// </summary>
public class VegaLiteSelectionExtentIntegrationTests
{
    /// <summary>The rows of the scatter plots: the extremes decide the default domains, x from 0 to 10 and y from 0 to 5.</summary>
    private const string Rows = """ "data": { "values": [ { "a": 0, "b": 0 }, { "a": 10, "b": 5 }, { "a": 4, "b": 2 } ] } """;

    /// <summary>The position encoding of the scatter plots.</summary>
    private const string Positions = """ "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays a chart out.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="reports">Receives the messages of the warnings and of the unsupported features, or null.</param>
    /// <returns>The scene.</returns>
    private static Scene Build(string json, List<string>? reports = null)
    {
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w => reports?.Add(w.Message),
            UnsupportedFeatureHandler = u => reports?.Add(u.Feature + ": " + u.Message),
        };
        var document = VegaLiteDocument.Parse(json);
        document.Measure(options, Ct);
        return document.BuildScene(options, Ct);
    }

    /// <summary>
    ///     Gets the position of the points of a scene, in drawing order.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The x and y of each point.</returns>
    private static List<(double X, double Y)> Points(Scene scene) => [.. scene.FindAll("mark-point").Select(e => ((SceneSymbol)e.Item).X).Zip(scene.FindAll("mark-point").Select(e => ((SceneSymbol)e.Item).Y))];

    /// <summary>
    ///     Gets the marks groups of a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The groups in drawing order.</returns>
    private static List<SceneGroup> MarkGroups(Scene scene) => [.. scene.FindAll("marks").Select(e => (SceneGroup)e.Item)];

    /// <summary>
    ///     Validates an interval selection bound to the scales of a single view: its initial range is the domain of the x scale ([2, 6]: a value v is at (v - 2) * 75), the marks are clipped to the data
    ///     rectangle, the y scale is not bound and no brush is drawn.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BindScales_TheInitialRangeIsTheDomain()
    {
        // Arrange
        var json = "{ \"width\": 300, \"height\": 200, \"params\": [ { \"name\": \"zoom\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"bind\": \"scales\", \"value\": { \"x\": [2, 6] } } ], "
            + Rows + ", \"mark\": \"point\", " + Positions + " }";

        // Act
        var scene = Build(json);

        // Assert: a is 0, 10 and 4; b is 0, 5 and 2 on the unchanged y scale (200 - 40 b)
        Assert.Equal([(-150.0, 200.0), (600.0, 0.0), (150.0, 120.0)], Points(scene));
        Assert.Equal(new SceneClip(0, 0, 300, 200), Assert.Single(MarkGroups(scene)).Clip);
        Assert.Empty(scene.FindAll("selection-brush"));
    }

    /// <summary>
    ///     Validates that the scales of a unit that is not the whole view are not bound in a static chart (Vega-Lite pushes their state to the top level): the x domain of the first view stays
    ///     [0, 10], while the marks are still clipped, because the scale has the extent of a selection.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BindScales_AreIgnoredInAConcatenation()
    {
        // Arrange: a selection of the root, pushed down to both views
        var unit = "{ \"width\": 300, \"height\": 200, " + Rows + ", \"mark\": \"point\", " + Positions + " }";
        var json = "{ \"params\": [ { \"name\": \"zoom\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"bind\": \"scales\", \"value\": { \"x\": [2, 6] } } ], \"hconcat\": [ " + unit + ", " + unit + " ] }";

        // Act
        var scene = Build(json);

        // Assert: x = 30 a over [0, 10]
        Assert.Equal([(0.0, 200.0), (300.0, 0.0), (120.0, 120.0)], Points(scene).Take(3));
        Assert.All(MarkGroups(scene), g => Assert.Equal(new SceneClip(0, 0, 300, 200), g.Clip));
        Assert.Empty(scene.FindAll("selection-brush"));
    }

    /// <summary>
    ///     Validates that the layers of a view that is layered at the top level are bound too: the layers share the scale, so both are drawn on the domain [2, 6] and both are clipped.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BindScales_ApplyToTheLayersOfTheWholeView()
    {
        // Arrange: the selection is declared by the first layer
        var layer = "\"mark\": \"point\", " + Positions;
        var json = "{ \"width\": 300, \"height\": 200, " + Rows + ", \"layer\": [ { \"params\": [ { \"name\": \"zoom\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"bind\": \"scales\", \"value\": { \"x\": [2, 6] } } ], "
            + layer + " }, { " + layer + " } ] }";

        // Act
        var scene = Build(json);

        // Assert: six points, the same three twice
        Assert.Equal([-150.0, 600.0, 150.0, -150.0, 600.0, 150.0], Points(scene).Select(p => p.X));
        Assert.All(MarkGroups(scene), g => Assert.Equal(new SceneClip(0, 0, 300, 200), g.Clip));
        Assert.Equal(2, MarkGroups(scene).Count);
    }

    /// <summary>
    ///     Validates that a selection bound to the scales warns when the channel it projects has no continuous scale (it cannot be bound), and draws a brush for nothing.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BindScales_WarnWhenTheScaleIsNotContinuous()
    {
        // Arrange: x is nominal
        var json = "{ \"width\": 300, \"height\": 200, \"params\": [ { \"name\": \"zoom\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"bind\": \"scales\", \"value\": { \"x\": [\"a\", \"c\"] } } ], "
            + "\"data\": { \"values\": [ { \"k\": \"a\", \"b\": 1 }, { \"k\": \"b\", \"b\": 2 }, { \"k\": \"c\", \"b\": 3 } ] }, \"mark\": \"bar\", "
            + "\"encoding\": { \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\" } } }";
        var reports = new List<string>();

        // Act
        Build(json, reports);

        // Assert
        Assert.Contains(reports, r => r.Contains("bound to the scales", StringComparison.Ordinal) && r.Contains("'x'", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates the domain of a scale that is a selection (<c>scale.domain</c> of the form <c>{"param": ...}</c>): with an <c>encoding</c> it is the range of the field the selection projects
    ///     over that channel (the y scale takes the range of x, [2, 6]: y = 200 - 50 (b - 2)), with a <c>field</c> the range of that field (b: [1, 3], y = 200 - 100 (b - 1)); the marks are clipped.
    /// </summary>
    /// <param name="domain">The <c>domain</c> of the y scale.</param>
    /// <param name="y0">The y of b = 0.</param>
    /// <param name="y5">The y of b = 5.</param>
    /// <param name="y2">The y of b = 2.</param>
    [Theory]
    [InlineData("""{ "param": "brush", "encoding": "x" }""", 300.0, 50.0, 200.0)]
    [InlineData("""{ "param": "brush", "field": "b" }""", 300.0, -200.0, 100.0)]
    public void CanvasNetVegaLite_SystemIntegration_Domain_ASelectionGivesTheRangeOfAField(string domain, double y0, double y5, double y2)
    {
        // Arrange
        var json = "{ \"width\": 300, \"height\": 200, \"params\": [ { \"name\": \"brush\", \"select\": \"interval\", \"value\": { \"x\": [2, 6], \"y\": [1, 3] } } ], " + Rows
            + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\", \"scale\": { \"domain\": " + domain + " } } } }";

        // Act
        var scene = Build(json);

        // Assert: the x scale keeps the domain of the data (30 a)
        Assert.Equal([(0.0, y0), (300.0, y5), (120.0, y2)], Points(scene));
        Assert.Equal(new SceneClip(0, 0, 300, 200), Assert.Single(MarkGroups(scene)).Clip);
    }

    /// <summary>
    ///     Validates that a selection used as a scale domain without a field or an encoding takes its first projected field (x, range [2, 6]) and warns when it projects several, and that an
    ///     encoding that does not name exactly one field does the same.
    /// </summary>
    /// <param name="domain">The <c>domain</c> of the y scale.</param>
    [Theory]
    [InlineData("""{ "param": "brush" }""")]
    [InlineData("""{ "param": "brush", "encoding": "color" }""")]
    public void CanvasNetVegaLite_SystemIntegration_Domain_TheFirstFieldIsUsedWithAWarning(string domain)
    {
        // Arrange
        var json = "{ \"width\": 300, \"height\": 200, \"params\": [ { \"name\": \"brush\", \"select\": \"interval\", \"value\": { \"x\": [2, 6], \"y\": [1, 3] } } ], " + Rows
            + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\", \"scale\": { \"domain\": " + domain + " } } } }";
        var reports = new List<string>();

        // Act
        var scene = Build(json, reports);

        // Assert: y = 200 - 50 (b - 2)
        Assert.Equal([300.0, 50.0, 200.0], Points(scene).Select(p => p.Y));
        Assert.Contains(reports, r => r.Contains("the first field, 'a', is used", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a selection that holds nothing leaves the domain of the scale as it is (a normal chart) while the marks are clipped.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Domain_AnEmptySelectionKeepsTheDomainOfTheData()
    {
        // Arrange
        var json = "{ \"width\": 300, \"height\": 200, \"params\": [ { \"name\": \"brush\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] } } ], " + Rows
            + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\", \"scale\": { \"domain\": { \"param\": \"brush\" } } }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\" } } }";

        // Act
        var scene = Build(json);

        // Assert
        Assert.Equal([0.0, 300.0, 120.0], Points(scene).Select(p => p.X));
        Assert.Equal(new SceneClip(0, 0, 300, 200), Assert.Single(MarkGroups(scene)).Clip);
    }

    /// <summary>
    ///     Builds the histogram of Vega-Lite's linked-view example: a view with an interval selection on x, and a view below it whose bins take their extent from the selection.
    /// </summary>
    /// <param name="selection">The <c>params</c> array of the first view (written out).</param>
    /// <returns>The specification text.</returns>
    private static string Histogram(string selection) =>
        "{ \"data\": { \"values\": [ { \"a\": 1 }, { \"a\": 2.05 }, { \"a\": 8 } ] }, \"vconcat\": [ "
        + "{ \"width\": 300, \"height\": 50, \"params\": " + selection + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } }, "
        + "{ \"width\": 300, \"height\": 100, \"mark\": \"rect\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\", \"bin\": { \"maxbins\": 20, \"extent\": { \"param\": \"br\" } } }, "
        + "\"y\": { \"aggregate\": \"count\", \"type\": \"quantitative\" } } } ] }";

    /// <summary>
    ///     Gets the rectangles of the marks of a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The rectangles in drawing order.</returns>
    private static List<SceneRect> Rects(Scene scene) => [.. scene.FindAll("mark-rect").Select(e => (SceneRect)e.Item)];

    /// <summary>
    ///     Validates a <c>bin.extent</c> that is a selection: the span of the selected range [2, 5] (3) chooses the step, the bins still start at the extent of the data (1), and the x scale takes
    ///     the range as its domain, with the marks clipped. For the data from 1 to 8 and at most 20 bins: the step starts at 10^(round(log10 3) - 2) = 0.01, grows by 10 until the 3 / step bins fit
    ///     (1: three bins), and the divisors 5 and 2 take it to 0.2 (15 bins fit, 30 do not). On the domain [2, 5] a unit is 100 pixels: a bin is 20 pixels wide, and the bins that start at 1, 2 and 7.8 are at
    ///     -100, 0 and 580 (plus the half pixel Vega-Lite shifts a binned rectangle by).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinExtent_TheSpanOfASelectionChoosesTheStep()
    {
        // Arrange: the selection is named br
        var json = Histogram("""[ { "name": "br", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [2, 5] } } ]""");

        // Act
        var scene = Build(json);
        var rects = Rects(scene);

        // Assert: three bins with a row each
        AssertPositions([-99.5, 0.5, 580.5], rects);
        Assert.All(rects, r => Assert.Equal(20.0, r.Width, 9));
        Assert.Equal(new SceneClip(0, 0, 300, 100), MarkGroups(scene)[1].Clip);
    }

    /// <summary>
    ///     Checks the x of some rectangles to a tolerance (a position is a product of the step, which is not exact).
    /// </summary>
    /// <param name="expected">The expected x of each rectangle.</param>
    /// <param name="rects">The rectangles.</param>
    private static void AssertPositions(double[] expected, List<SceneRect> rects)
    {
        Assert.Equal(expected.Length, rects.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], rects[i].X, 9);
        }
    }

    /// <summary>
    ///     Validates that a selection that holds nothing changes nothing: the bins are those of the data alone (the span of 1 to 8 is 7: the step is 0.5, 14 bins fit and 35 at 0.2 do not), and the x scale
    ///     has the domain of the bins, 1 to 8 (a bin is 300 / 14 pixels wide).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinExtent_AnEmptySelectionLeavesTheBinsOfTheData()
    {
        // Arrange
        var json = Histogram("""[ { "name": "br", "select": { "type": "interval", "encodings": ["x"] } } ]""");

        // Act
        var rects = Rects(Build(json));

        // Assert: half a unit wide on the domain 1 to 8
        Assert.All(rects, r => Assert.Equal(0.5 * 300 / 7, r.Width, 9));
    }

    /// <summary>
    ///     Validates that a <c>bin.extent</c> that is a variable parameter gives its span too: the value [2, 5] is the span 3, as the selection's range is.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_BinExtent_AVariableParameterGivesItsSpan()
    {
        // Arrange: the parameter is declared at the top level
        var json = Histogram("[]").Replace("{ \"data\":", "{ \"params\": [ { \"name\": \"br\", \"value\": [2, 5] } ], \"data\":", StringComparison.Ordinal);
        var reports = new List<string>();

        // Act
        var rects = Rects(Build(json, reports));

        // Assert: the same bins as the selection's, on the domain [2, 5]
        AssertPositions([-99.5, 0.5, 580.5], rects);
        Assert.All(rects, r => Assert.Equal(20.0, r.Width, 9));
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the <c>extent</c> of the <c>bin</c> transform: the span chooses the step (a transform asks for at most 10 bins: with the span 3 the step is 0.5, with the data's span of 7 it is 1), the bins
    ///     still cover the data (1 to 8), and the transform does not touch a scale.
    /// </summary>
    /// <param name="parameters">The <c>params</c> property of the root, ending with a comma, or empty.</param>
    /// <param name="start">The start of the bin of the value 8: 7.5 (the last bin of the step 0.5) or 7 (the last bin of the step 1).</param>
    [Theory]
    [InlineData("\"params\": [ { \"name\": \"ex\", \"value\": [2, 5] } ], ", 7.5)]
    [InlineData("", 7.0)]
    public void CanvasNetVegaLite_SystemIntegration_BinExtent_TheTransformFollowsTheSpan(string parameters, double start)
    {
        // Arrange: the bin of a (1, 2.05 and 8) is written to b; x = 30 b on a fixed domain
        var extent = parameters.Length == 0 ? string.Empty : "\"extent\": { \"param\": \"ex\" }, ";
        var json = "{ " + parameters + "\"width\": 300, \"height\": 200, \"data\": { \"values\": [ { \"a\": 1 }, { \"a\": 2.05 }, { \"a\": 8 } ] }, "
            + "\"transform\": [ { \"bin\": { " + extent + "\"maxbins\": 10 }, \"field\": \"a\", \"as\": \"b\" } ], \"mark\": \"point\", "
            + "\"encoding\": { \"x\": { \"field\": \"b\", \"type\": \"quantitative\", \"scale\": { \"domain\": [0, 10], \"nice\": false } } } }";
        var reports = new List<string>();

        // Act
        var points = Points(Build(json, reports));

        // Assert: the bins of 1 and 2.05 start at 1 and 2 either way
        Assert.Equal([30.0, 60.0, 30 * start], points.Select(p => p.X));
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the example of the package documentation (<c>NamespaceDoc</c>): a brush on the first view of a vertical concatenation colors its circles, and the bars of the second view bin by the span
    ///     of the brush. With the value [2, 5] of the specification the circles at 2.5 and 4 are red; with the [3, 6] of the host only the circle at 4 is. Either way the span is 3, so the step is 0.5 (the
    ///     step 0.1 gives 30 bins, more than the 10 asked for; 1 gives 3; the divisor 2 then gives 0.5, and 5 would give 0.2, with 15), and the four values 1, 2.5, 4 and 8 fall in four bins. Nothing is reported.
    /// </summary>
    /// <param name="override_">Whether the host gives its own range.</param>
    /// <param name="red">The circles that are red, in row order.</param>
    [Theory]
    [InlineData(false, new[] { false, true, true, false })]
    [InlineData(true, new[] { false, false, true, false })]
    public void CanvasNetVegaLite_SystemIntegration_BinExtent_TheDocumentationExampleRenders(bool override_, bool[] red)
    {
        // Arrange: the text of the example
        var json = """
            {
              "data": { "values": [ { "a": 1 }, { "a": 2.5 }, { "a": 4 }, { "a": 8 } ] },
              "vconcat": [
                {
                  "params": [ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [2, 5] } } ],
                  "mark": "circle",
                  "encoding": {
                    "x": { "field": "a", "type": "quantitative" },
                    "color": { "condition": { "param": "brush", "value": "firebrick" }, "value": "steelblue" }
                  }
                },
                {
                  "mark": "bar",
                  "encoding": {
                    "x": { "field": "a", "type": "quantitative", "bin": { "extent": { "param": "brush" } } },
                    "y": { "aggregate": "count" }
                  }
                }
              ]
            }
            """;
        var reports = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            Parameters = override_
                ? new Dictionary<string, object?> { ["brush"] = new Dictionary<string, object?> { ["x"] = new object[] { 3, 6 } } }
                : null,
            WarningHandler = w => reports.Add(w.Message),
            UnsupportedFeatureHandler = u => reports.Add(u.Feature + ": " + u.Message),
        };

        // Act
        var document = VegaLiteDocument.Parse(json);
        document.Measure(options, Ct);
        var scene = document.BuildScene(options, Ct);

        // Assert
        Assert.Equal(red, scene.FindAll("mark-circle").Select(e => ((SceneSymbol)e.Item).Fill == "firebrick"));
        Assert.Equal(4, scene.FindAll("mark-bar").Count());
        Assert.Empty(reports);
    }
}
