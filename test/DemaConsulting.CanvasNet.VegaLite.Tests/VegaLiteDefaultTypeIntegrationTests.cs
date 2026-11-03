using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the type that a field definition gets when it has no <c>type</c>: Vega-Lite 5 and later make the type optional and infer it
///     (<c>defaultType</c> of the channel definition source), so every stage of the renderer (scales, legends, text, time, bins, stacks, composite marks) has to agree on it. Each
///     test draws a chart without the types in strict mode and requires the same scene and pixels as the chart that writes the inferred types; the user's own case, a text
///     channel over a string field, also requires the text itself.
/// </summary>
public class VegaLiteDefaultTypeIntegrationTests
{
    /// <summary>Three points of three categories.</summary>
    private const string Categories = """[ { "x": 1, "y": 2, "g": "a" }, { "x": 2, "y": 3, "g": "b" }, { "x": 3, "y": 1, "g": "c" } ]""";

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a chart of 200 by 100 pixels.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="data">The JSON text of the rows.</param>
    /// <param name="encoding">The JSON text of the encoding.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string data, string encoding) =>
        "{ \"width\": 200, \"height\": 100, \"data\": { \"values\": " + data + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + " }";

    /// <summary>
    ///     Draws a document in strict mode (a missing type is valid Vega-Lite, so nothing may be reported or thrown).
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="scene">The scene that the document builds.</param>
    /// <returns>The pixels of the picture.</returns>
    private static byte[] Draw(string json, out Scene scene)
    {
        var document = VegaLiteDocument.Parse(json);
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) };
        scene = document.BuildScene(options, Ct);
        using var surface = document.Render(options, Ct);
        Assert.Empty(warnings);
        return VegaLiteTestHelpers.Snapshot(surface);
    }

    /// <summary>
    ///     Requires that two specifications draw the same scene and the same pixels.
    /// </summary>
    /// <param name="inferred">The specification without the types.</param>
    /// <param name="written">The specification with the types that Vega-Lite infers.</param>
    /// <returns>The scene of the specification without the types.</returns>
    private static Scene AssertSamePicture(string inferred, string written)
    {
        var pixels = Draw(inferred, out var scene);
        var expected = Draw(written, out var writtenScene);
        Assert.Equal(writtenScene.Width, scene.Width);
        Assert.Equal(writtenScene.Height, scene.Height);
        Assert.True(expected.AsSpan().SequenceEqual(pixels), "The picture without the types differs from the picture with the inferred types.");
        return scene;
    }

    /// <summary>
    ///     Validates the report of the user: a <c>text</c> channel over a string field without a type shows the strings (Vega-Lite shows <c>Successful</c>, the package showed
    ///     <c>NaN</c> because it read the field as quantitative), and the same chart with the type <c>nominal</c> shows the same text.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_TextOverStringsShowsTheStrings()
    {
        // Arrange
        const string data = """[ { "k": "Successful" } ]""";

        // Act
        Draw(Chart("\"text\"", data, """{ "text": { "field": "k" } }"""), out var inferred);
        Draw(Chart("\"text\"", data, """{ "text": { "field": "k", "type": "nominal" } }"""), out var nominal);

        // Assert: the text is the string, with and without the type
        Assert.Equal(["Successful"], inferred.FindAll("mark-text").Select(e => ((SceneText)e.Item).Text));
        Assert.Equal(["Successful"], nominal.FindAll("mark-text").Select(e => ((SceneText)e.Item).Text));
    }

    /// <summary>
    ///     Validates the text channel for the other inferred types: a count aggregate and a numeric aggregate are quantitative (a number is formatted as a number), a time unit is
    ///     temporal, and a number datum is quantitative.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_TextFollowsTheInferredType()
    {
        // Arrange
        const string data = """[ { "k": "a", "v": 1.5, "d": "2024-03-05" }, { "k": "a", "v": 2.5, "d": "2024-03-06" } ]""";

        // Act
        var aggregated = Draw(Chart("\"text\"", data, """{ "text": { "aggregate": "sum", "field": "v" } }"""), out var sum);
        var counted = Draw(Chart("\"text\"", data, """{ "text": { "aggregate": "count" }, "detail": { "field": "k" } }"""), out var count);
        var unit = Draw(Chart("\"text\"", data, """{ "text": { "field": "d", "timeUnit": "yearmonth" } }"""), out var months);

        // Assert: the sum is a number, the count is the number of rows and a time unit is shown as a date
        Assert.Equal(["4"], sum.FindAll("mark-text").Select(e => ((SceneText)e.Item).Text));
        Assert.Equal(["2"], count.FindAll("mark-text").Select(e => ((SceneText)e.Item).Text));
        Assert.Equal(["Mar 2024", "Mar 2024"], months.FindAll("mark-text").Select(e => ((SceneText)e.Item).Text));
        Assert.NotEmpty(aggregated);
        Assert.NotEmpty(counted);
        Assert.NotEmpty(unit);
    }

    /// <summary>
    ///     Validates that a <c>color</c> field without a type is nominal: the categorical palette and a symbol legend with one entry per category, like the nominal type.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_ColorWithoutTypeIsNominal()
    {
        // Arrange / Act
        var scene = AssertSamePicture(
            Chart("\"point\"", Categories, """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "g" } }"""),
            Chart("\"point\"", Categories, """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }"""));

        // Assert: one symbol legend of the three categories in the colors of the categorical scheme (not a gradient)
        Assert.Single(scene.FindAll("legend"));
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], scene.FindAll("legend-symbol").Select(e => ((SceneSymbol)e.Item).Stroke));
        Assert.Empty(scene.FindAll("legend-gradient"));
    }

    /// <summary>
    ///     Validates that a <c>shape</c> field without a type is nominal: a legend of three shapes, like the nominal type.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_ShapeWithoutTypeIsNominal()
    {
        // Arrange / Act
        var scene = AssertSamePicture(
            Chart("\"point\"", Categories, """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "shape": { "field": "g" } }"""),
            Chart("\"point\"", Categories, """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "shape": { "field": "g", "type": "nominal" } }"""));

        // Assert: three different shapes in the legend
        Assert.Equal(3, scene.FindAll("legend-symbol").Select(e => ((SceneSymbol)e.Item).Shape).Distinct().Count());
    }

    /// <summary>
    ///     Validates the position channels without a type: a plain field is nominal (a band scale for a bar, as Vega-Lite's <c>defaultType</c> gives), a field with an aggregate is
    ///     quantitative (a linear scale).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_PositionsWithoutTypeFollowDefaultType()
    {
        // Arrange / Act
        AssertSamePicture(
            Chart("\"bar\"", Categories, """{ "x": { "field": "g" }, "y": { "aggregate": "sum", "field": "y" } }"""),
            Chart("\"bar\"", Categories, """{ "x": { "field": "g", "type": "nominal" }, "y": { "aggregate": "sum", "field": "y", "type": "quantitative" } }"""));
        AssertSamePicture(
            Chart("\"point\"", Categories, """{ "x": { "field": "g" }, "y": { "field": "y", "type": "quantitative" } }"""),
            Chart("\"point\"", Categories, """{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "y", "type": "quantitative" } }"""));
    }

    /// <summary>
    ///     Validates that a <c>timeUnit</c> without a type is temporal: the dates are parsed and the axis is a time axis, like the type <c>temporal</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_TimeUnitWithoutTypeIsTemporal()
    {
        // Arrange
        const string data = """[ { "d": "2024-01-15", "v": 1 }, { "d": "2024-02-15", "v": 3 }, { "d": "2024-03-15", "v": 2 } ]""";

        // Act and Assert
        AssertSamePicture(
            Chart("\"bar\"", data, """{ "x": { "field": "d", "timeUnit": "month" }, "y": { "field": "v", "type": "quantitative" } }"""),
            Chart("\"bar\"", data, """{ "x": { "field": "d", "type": "temporal", "timeUnit": "month" }, "y": { "field": "v", "type": "quantitative" } }"""));
    }

    /// <summary>
    ///     Validates that a <c>bin</c> without a type is quantitative: the bins are computed and drawn as a linear scale, like the type <c>quantitative</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_BinWithoutTypeIsQuantitative()
    {
        // Arrange
        const string data = """[ { "v": 1 }, { "v": 2 }, { "v": 2 }, { "v": 7 }, { "v": 9 } ]""";

        // Act and Assert
        AssertSamePicture(
            Chart("\"bar\"", data, """{ "x": { "field": "v", "bin": true }, "y": { "aggregate": "count" } }"""),
            Chart("\"bar\"", data, """{ "x": { "field": "v", "type": "quantitative", "bin": true }, "y": { "aggregate": "count", "type": "quantitative" } }"""));
    }

    /// <summary>
    ///     Validates that the counting aggregates without a type are quantitative: the field of a <c>distinct</c>, <c>valid</c> or <c>missing</c> aggregate is a string, yet the result is a count.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_CountingAggregatesWithoutTypeAreQuantitative()
    {
        // Arrange / Act and Assert
        foreach (var aggregate in new[] { "distinct", "valid", "missing" })
        {
            AssertSamePicture(
                Chart("\"bar\"", Categories, "{ \"x\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\": { \"aggregate\": \"" + aggregate + "\", \"field\": \"g\" } }"),
                Chart("\"bar\"", Categories, "{ \"x\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\": { \"aggregate\": \"" + aggregate + "\", \"field\": \"g\", \"type\": \"quantitative\" } }"));
        }
    }

    /// <summary>
    ///     Validates that a secondary channel follows its main channel: the end of a bar of dates without a type on <c>x2</c> is read as dates, as the type of <c>x</c> says.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_SecondaryChannelFollowsItsMainChannel()
    {
        // Arrange
        const string data = """[ { "s": "2024-01-01", "e": "2024-02-01", "n": "a" }, { "s": "2024-03-01", "e": "2024-05-01", "n": "b" } ]""";

        // Act and Assert: with the type written on x only, and with the time unit inferring the type of x
        AssertSamePicture(
            Chart("\"bar\"", data, """{ "x": { "field": "s", "type": "temporal" }, "x2": { "field": "e" }, "y": { "field": "n" } }"""),
            Chart("\"bar\"", data, """{ "x": { "field": "s", "type": "temporal" }, "x2": { "field": "e", "type": "temporal" }, "y": { "field": "n", "type": "nominal" } }"""));
        AssertSamePicture(
            Chart("\"bar\"", data, """{ "x": { "field": "s", "timeUnit": "yearmonth" }, "x2": { "field": "e", "timeUnit": "yearmonth" }, "y": { "field": "n" } }"""),
            Chart("\"bar\"", data, """{ "x": { "field": "s", "type": "temporal", "timeUnit": "yearmonth" }, "x2": { "field": "e", "type": "temporal", "timeUnit": "yearmonth" }, "y": { "field": "n", "type": "nominal" } }"""));
    }

    /// <summary>
    ///     Validates a stacked bar without types: the measure is the aggregate (quantitative), the stack and the category colors are those of the written types.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_DefaultType_StackedBarWithoutTypes()
    {
        // Arrange
        const string data = """[ { "c": "p", "g": "a", "v": 1 }, { "c": "p", "g": "b", "v": 2 }, { "c": "q", "g": "a", "v": 3 } ]""";

        // Act and Assert
        AssertSamePicture(
            Chart("\"bar\"", data, """{ "x": { "field": "c" }, "y": { "aggregate": "sum", "field": "v" }, "color": { "field": "g" } }"""),
            Chart("\"bar\"", data, """{ "x": { "field": "c", "type": "nominal" }, "y": { "aggregate": "sum", "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }"""));
    }
}
