using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the <c>order</c> channel as the drawing order of the marks of <see cref="DemaConsulting.CanvasNet.VegaLite.Marks.MarkBuilder"/>:
///     the marks are drawn in the order of the channel unless the marks are stacked or the order is switched off.
/// </summary>
public class MarkBuilderOrderTests
{
    /// <summary>The default color of a mark.</summary>
    private const string Blue = "#4c78a8";

    /// <summary>The second color of the tableau10 palette.</summary>
    private const string Orange = "#f58518";

    /// <summary>
    ///     Builds a 300 by 200 chart of a mark over x and y quantitative fields plus extra channels.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="channels">The JSON text of the other channels (a list of members that starts with a comma), or empty.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string channels, string values) =>
        "{ \"width\": 300, \"height\": 200, \"data\": { \"values\": " + values + " }, \"mark\": " + mark
        + ", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" }" + channels + " } }";

    /// <summary>
    ///     Validates that the <c>order</c> channel sorts the drawing order of the symbols and bars (ascending or descending), and does not reorder a stack.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_OrderChannelSortsTheDrawOrderOfAnyMark()
    {
        // Arrange: three points at x = 0, 1, 2 (x(v) = 150 v) with the order values 3, 1, 2
        const string rows = """[ { "x": 0, "y": 0, "o": 3 }, { "x": 1, "y": 0, "o": 1 }, { "x": 2, "y": 0, "o": 2 } ]""";
        List<double> Xs(string channels) =>
            [.. ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Chart("\"point\"", channels, rows), 300, 200), "mark-point").Select(s => s.X)];

        // Assert: ascending visits o = 1, 2, 3 (x = 1, 2, 0), descending o = 3, 2, 1 (x = 0, 2, 1), and without the channel the data order is kept
        Assert.Equal([150.0, 300.0, 0.0], Xs(""", "order": { "field": "o" }"""));
        Assert.Equal([0.0, 300.0, 150.0], Xs(""", "order": { "field": "o", "sort": "descending" }"""));
        Assert.Equal([0.0, 150.0, 300.0], Xs(string.Empty));

        // Assert: several order fields break ties in turn: the equal first values (all 1) are decided by the second
        const string tied = """[ { "x": 0, "y": 0, "o": 1, "p": 2 }, { "x": 1, "y": 0, "o": 1, "p": 3 }, { "x": 2, "y": 0, "o": 1, "p": 1 } ]""";
        var multi = ChartTestHelpers.Of<SceneSymbol>(
            ChartTestHelpers.Marks(Chart("\"point\"", """, "order": [ { "field": "o" }, { "field": "p", "sort": "descending" } ]""", tied), 300, 200),
            "mark-point");
        Assert.Equal([150.0, 0.0, 300.0], multi.Select(s => s.X));

        // Arrange: bars of three categories (step 100, band 90, first band at 5) with the order values 2, 3, 1; a bar stacks by default, and then the order field is the
        // order of the layers (Vega-Lite stacks even a single layer), so the stack is switched off to sort the drawing
        const string bars = """
            { "width": 300, "height": 200, "data": { "values": [ { "k": "p", "v": 1, "o": 2 }, { "k": "q", "v": 2, "o": 3 }, { "k": "r", "v": 3, "o": 1 } ] }, "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "stack": null }, "order": { "field": "o" } } }
            """;

        // Assert: the bars are drawn r, p, q; with the default stack they keep the data order
        Assert.Equal([205.0, 5.0, 105.0], ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(bars, 300, 200), "mark-bar").Select(r => r.X));
        Assert.Equal([5.0, 105.0, 205.0], ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(bars.Replace(", \"stack\": null", string.Empty, StringComparison.Ordinal), 300, 200), "mark-bar").Select(r => r.X));

        // Arrange: stacked bars: the order field orders the layers of the stack, not the drawing: the items keep the data order whatever the values
        const string stacked = """
            { "width": 300, "height": 200, "data": { "values": [ { "k": "p", "v": 1, "g": "a", "o": 2 }, { "k": "p", "v": 2, "g": "b", "o": 1 } ] }, "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" },
                            "order": { "field": "o" } } }
            """;
        Assert.Equal([Blue, Orange], ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(stacked, 300, 200), "mark-bar").Select(r => r.Fill));
    }

    /// <summary>
    ///     Validates that an <c>order</c> that is a value of null (or a mark property of false or null) keeps the data order of the marks and of the points of a path.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_OrderNullKeepsTheDataOrder()
    {
        // Arrange: the rows are not in the order of x
        const string rows = """[ { "x": 2, "y": 5 }, { "x": 0, "y": 0 }, { "x": 1, "y": 10 } ]""";

        // Assert: symbols are drawn in data order
        var symbols = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Chart("\"point\"", """, "order": { "value": null }""", rows), 300, 200), "mark-point");
        Assert.Equal([300.0, 0.0, 150.0], symbols.Select(s => s.X));

        // Assert: a line is sorted by x by default and keeps the data order with the order switched off (by the channel or by the mark property)
        Assert.Equal("M0,200 L150,0 L300,100", PathText.Of(Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"line\"", string.Empty, rows), 300, 200), "mark-line"))));
        foreach (var json in new[]
        {
            Chart("\"line\"", """, "order": { "value": null }""", rows),
            Chart("\"line\"", """, "order": { "value": false }""", rows),
            Chart("""{ "type": "line", "order": false }""", string.Empty, rows),
            Chart("""{ "type": "line", "order": null }""", string.Empty, rows),
        })
        {
            Assert.Equal("M300,100 L0,200 L150,0", PathText.Of(Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(json, 300, 200), "mark-line"))));
        }

        // Assert: the same for an area (its polygon runs through the points in data order: top line forward, baseline backward)
        var area = Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("""{ "type": "area", "order": false }""", string.Empty, rows), 300, 200), "mark-area"));
        Assert.Equal("M300,100 L0,200 L150,0 L150,200 L0,200 L300,200 Z", PathText.Of(area));
    }
}
