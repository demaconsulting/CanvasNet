using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of conditional encodings (Phase 7): charts of the original fixture and of small inline specifications are rendered
///     through the public API and checked by the scene items and sample pixels, with every number derived by hand from the tests of the conditions and the
///     layout of the chart (the days 1 to 8 on an x axis of 0 to 8 over 200 pixels, the heights 1 to 5 on a y axis of 0 to 5 over 100 pixels).
/// </summary>
public class VegaLiteConditionalMarksIntegrationTests
{
    /// <summary>The opaque red of the dry readings.</summary>
    private static readonly Rgba32 Red = new(0xd7, 0x30, 0x27, 255);

    /// <summary>The opaque blue of the other readings.</summary>
    private static readonly Rgba32 Blue = new(0x45, 0x75, 0xb4, 255);

    /// <summary>Whether each of the eight readings of the fixture is dry (moisture 3, 12, 27, 41, 58, 66, 83 and 97: under 30 for the first three).</summary>
    private static readonly bool[] Dry = [true, true, true, false, false, false, false, false];

    /// <summary>The height of each of the eight readings of the fixture.</summary>
    private static readonly double[] Heights = [1, 3, 2, 4, 2, 5, 1, 3];

    /// <summary>Whether the day of each reading is one of the days 3 to 5, which the opacity condition keeps opaque.</summary>
    private static readonly bool[] Opaque = [false, false, true, true, true, false, false, false];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Gets the position of the view (the data rectangle) on the canvas.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The x and y position of the data rectangle.</returns>
    private static (double X, double Y) ViewOf(Scene scene)
    {
        var view = scene.Find("view") ?? throw new InvalidOperationException("The scene has no view.");
        var group = Assert.IsType<SceneGroup>(view.Item);
        return (view.OffsetX + group.X, view.OffsetY + group.Y);
    }

    /// <summary>
    ///     Gets the color a mark of an opacity gives over the white background.
    /// </summary>
    /// <param name="color">The opaque color.</param>
    /// <param name="opacity">The opacity.</param>
    /// <returns>The blended color (as the exact arithmetic gives it; the painter may differ by one in a channel).</returns>
    private static Rgba32 OverWhite(Rgba32 color, double opacity) =>
        new(
            (byte)Math.Round((opacity * color.R) + ((1 - opacity) * 255)),
            (byte)Math.Round((opacity * color.G) + ((1 - opacity) * 255)),
            (byte)Math.Round((opacity * color.B) + ((1 - opacity) * 255)),
            255);

    /// <summary>
    ///     Tells whether two colors differ by at most one in every channel and the second is opaque.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The painted color.</param>
    /// <returns><see langword="true"/> when the colors are as near as the rounding of a blend allows.</returns>
    private static bool IsNear(Rgba32 expected, Rgba32 actual) =>
        Math.Abs(actual.R - expected.R) <= 1 && Math.Abs(actual.G - expected.G) <= 1 && Math.Abs(actual.B - expected.B) <= 1 && actual.A == 255;

    /// <summary>
    ///     Validates the fixture: eight circles whose color, size and opacity follow the conditions (the dry readings are red, the plants of height 4 or more
    ///     are larger, the days 3 to 5 are opaque), every value derived by hand, in strict mode, without a legend, and confirmed by pixels at the circles.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_ConditionalMarks_CirclesTakeTheValuesOfTheirConditions()
    {
        // Arrange: strict mode would throw for any unsupported feature; a warning would be collected
        var document = VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "conditional-marks.vl.json"));
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) };
        var scene = document.BuildScene(options, Ct);
        var view = ViewOf(scene);
        var circles = scene.FindAll("mark-circle").Select(e => (SceneSymbol)e.Item).ToList();

        // Act
        using var surface = document.Render(options, Ct);

        // Assert: nothing was reported, the circles follow the axes, and no legend is drawn (conditions with values create none)
        Assert.Empty(warnings);
        Assert.Equal(8, circles.Count);
        Assert.Empty(scene.FindAll("legend-symbol"));
        Assert.Empty(scene.FindAll("legend-label"));
        for (var i = 0; i < circles.Count; i++)
        {
            Assert.Equal(25 * (i + 1), circles[i].X, 6);
            Assert.Equal(100 - (20 * Heights[i]), circles[i].Y, 6);
            Assert.Equal(Dry[i] ? "#d73027" : "#4575b4", circles[i].Fill);
            Assert.Equal(Heights[i] >= 4 ? 240.0 : 60.0, circles[i].Size);
            Assert.Equal(Opaque[i] ? 1.0 : 0.4, circles[i].Opacity);
        }

        // Assert: the pixels 1 right of and below each center (a whole pixel inside the smallest circle, of the radius sqrt(60 / pi) = 4.4, whose far corner is
        // sqrt(2 * 2^2) = 2.8 from the center) are the color of the circle blended over the white background with its opacity
        for (var i = 0; i < circles.Count; i++)
        {
            var px = (int)Math.Round(view.X + circles[i].X) + 1;
            var py = (int)Math.Round(view.Y + circles[i].Y) + 1;
            var expected = OverWhite(Dry[i] ? Red : Blue, Opaque[i] ? 1.0 : 0.4);
            Assert.True(IsNear(expected, surface[px, py]), $"circle {i}: expected {expected} but the pixel ({px},{py}) is {surface[px, py]}");
        }
    }

    /// <summary>
    ///     Validates a color condition that holds a field: the legend lists the categories of the field with the colors of its scale, the marks where the test
    ///     fails take the color of the channel's value, and the circles are confirmed by pixels.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_ConditionalMarks_FieldConditionDrawsALegendAndKeepsTheValueElsewhere()
    {
        // Arrange: the category g is a, b, c (colors 1, 2 and 3 of the palette); the first reading fails the test and is gray
        const string json = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "q": 1, "r": 10, "g": "a" }, { "q": 2, "r": 20, "g": "b" }, { "q": 3, "r": 30, "g": "c" } ] },
              "mark": { "type": "circle", "size": 100, "opacity": 1 },
              "encoding": {
                "x": { "field": "q", "type": "quantitative" }, "y": { "field": "r", "type": "quantitative" },
                "color": { "condition": { "test": "datum.q > 1", "field": "g", "type": "nominal" }, "value": "#808080" } } }
            """;
        var document = VegaLiteDocument.Parse(json);
        var scene = document.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
        var view = ViewOf(scene);
        var circles = scene.FindAll("mark-circle").Select(e => (SceneSymbol)e.Item).ToList();

        // Act
        using var surface = document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);

        // Assert: the circles are gray, then the colors of b and c; the legend lists all three categories under the title of the field
        Assert.Equal(["#808080", "#f58518", "#e45756"], circles.Select(c => c.Fill));
        Assert.Equal(["a", "b", "c"], scene.FindAll("legend-label").Select(e => ((SceneText)e.Item).Text));
        Assert.Equal(["g"], scene.FindAll("legend-title").Select(e => ((SceneText)e.Item).Text));
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], scene.FindAll("legend-symbol").Select(e => ((SceneSymbol)e.Item).Fill));

        // Assert: the pixels of the circles are opaque, in their colors
        Rgba32[] colors = [new(0x80, 0x80, 0x80, 255), new(0xf5, 0x85, 0x18, 255), new(0xe4, 0x57, 0x56, 255)];
        for (var i = 0; i < circles.Count; i++)
        {
            var px = (int)Math.Round(view.X + circles[i].X) + 1;
            var py = (int)Math.Round(view.Y + circles[i].Y) + 1;
            Assert.True(IsNear(colors[i], surface[px, py]), $"circle {i}: expected {colors[i]} but the pixel ({px},{py}) is {surface[px, py]}");
        }
    }

    /// <summary>
    ///     Validates the diagnostics: a condition on a parameter is reported with its location (strict mode turns it into an exception there) and the chart is
    ///     drawn with the channel's own value, and a test that is not an expression is a located specification error.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_ConditionalMarks_DiagnosticsAreLocated()
    {
        // Arrange
        const string parameter = """
            { "width": 100, "height": 100, "params": [ { "name": "flag", "value": true } ],
              "data": { "values": [ { "q": 1 }, { "q": 2 } ] },
              "mark": "circle",
              "encoding": { "x": { "field": "q", "type": "quantitative" },
                            "color": { "condition": { "param": "flag", "value": "red" }, "value": "#808080" } } }
            """;
        const string broken = """
            { "data": { "values": [ { "q": 1 } ] },
              "mark": "circle",
              "encoding": { "x": { "field": "q", "type": "quantitative" },
                            "color": { "condition": { "test": "datum.q >", "value": "red" }, "value": "#808080" } } }
            """;
        var reported = new List<(string Feature, string Pointer)>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => reported.Add((u.Feature, u.Location.Pointer)) };
        var document = VegaLiteDocument.Parse(parameter);

        // Act
        using var surface = document.Render(options, Ct);
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        var strict = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
        var invalid = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(broken));

        // Assert: the parameter condition is reported once, at the condition, and the circles keep the channel's value
        Assert.Equal([("vl-encoding-condition-param", "/encoding/color/condition")], reported);
        Assert.Equal("vl-encoding-condition-param", strict.Feature);
        Assert.Equal("/encoding/color/condition", strict.Location.Pointer);
        Assert.True(strict.Location.Line > 0 && strict.Location.Column > 0);
        Assert.All(scene.FindAll("mark-circle"), e => Assert.Equal("#808080", ((SceneSymbol)e.Item).Fill));

        // Assert: the syntax error is located at the test
        Assert.Equal("/encoding/color/condition/test", invalid.Location.Pointer);
        Assert.True(invalid.Location.Line > 0 && invalid.Location.Column > 0);
    }

    /// <summary>
    ///     Validates the limits, the cancellation and the determinism of conditional charts: the tests are charged to <c>MaxEvaluationSteps</c> (a render error
    ///     that names the limit), a roomy limit renders, the scene count is exact, a canceled token stops the render, and repeated and concurrent renders give
    ///     the same pixels.
    /// </summary>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_ConditionalMarks_LimitsCancellationAndDeterminismApply()
    {
        // Arrange
        var document = VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "conditional-marks.vl.json"));
        var count = document.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems();
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        // Act
        var tooFew = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxEvaluationSteps = 10 } }, Ct));
        using var roomy = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxEvaluationSteps = 100_000 } }, Ct);
        using var exact = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count } }, Ct);
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count - 1 } }, Ct));
        using var first = document.Render(cancellationToken: Ct);
        using var second = document.Render(cancellationToken: Ct);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => VegaLiteTestHelpers.Snapshot(document.Render(cancellationToken: Ct)), Ct)));

        // Assert: the failures name their limits
        Assert.Contains("MaxEvaluationSteps", tooFew.Message, StringComparison.Ordinal);
        Assert.Contains("MaxSceneItems", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal(first.Width, roomy.Width);
        Assert.Equal(first.Height, exact.Height);
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: canceled.Token));

        // Assert: the pixels are the same on every render, also when they run at the same time
        var expected = VegaLiteTestHelpers.Snapshot(first);
        Assert.Equal(expected, VegaLiteTestHelpers.Snapshot(second));
        Assert.All(concurrent, snapshot => Assert.Equal(expected, snapshot));
    }
}
