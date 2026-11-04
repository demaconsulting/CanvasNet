using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the offset channels of <see cref="MarkBuilder"/>: <c>xOffset</c> and <c>yOffset</c> place a mark inside the band of its position scale, as Vega-Lite does (a grouped bar chart has bars of the width of the
///     offset band). The numbers are derived by hand: the default step of a grouped chart of 3 categories and 2 groups is 20 * 2 / 0.8 = 50, so the view is 160 pixels wide, a category band starts at 10 + 50 * i and is 40 wide,
///     and each bar is 20 wide.
/// </summary>
public class MarkBuilderOffsetTests
{
    /// <summary>The rows: categories A, B and C with the groups x and y (C has only x, so its second slot stays empty).</summary>
    private const string Rows = """
        [ { "c": "A", "g": "x", "v": 1 }, { "c": "A", "g": "y", "v": 2 }, { "c": "B", "g": "x", "v": 3 }, { "c": "B", "g": "y", "v": 4 }, { "c": "C", "g": "x", "v": 5 } ]
        """;

    /// <summary>The encoding of a grouped bar chart.</summary>
    private const string Grouped = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }""";

    /// <summary>
    ///     Builds a specification over the rows.
    /// </summary>
    /// <param name="mark">The mark JSON.</param>
    /// <param name="encoding">The encoding JSON.</param>
    /// <param name="rows">The data rows JSON.</param>
    /// <param name="top">More top-level properties starting with a comma.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string encoding, string rows = Rows, string top = "") =>
        "{ \"data\": { \"values\": " + rows + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + top + " }";

    /// <summary>
    ///     Rounds numbers to nine decimals.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The rounded numbers.</returns>
    private static List<double> R(IEnumerable<double> values) => [.. values.Select(v => Math.Round(v, 9))];

    /// <summary>
    ///     Validates the grouped bar chart with the default step: bars of 20 pixels, no gap inside a group, 0.2 steps (10 pixels) between groups and at the ends, and an empty slot for the missing group.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_GroupedBarsHaveTheOffsetBandWidthAtTheOffsetPosition()
    {
        // Act: the data rectangle is the natural 160 pixels wide and 100 high (y is nice: 0 to 5, 20 px per unit)
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", Grouped), 160, 100), "mark-bar");

        // Assert: A at 10 (x then y), B at 60, C at 110 with only x
        Assert.Equal([10.0, 30.0, 60.0, 80.0, 110.0], R(bars.Select(b => b.X)));
        Assert.All(bars, b => Assert.Equal(20.0, b.Width, 9));
        Assert.Equal([80.0, 60.0, 40.0, 20.0, 0.0], R(bars.Select(b => b.Y)));
        Assert.Equal([20.0, 40.0, 60.0, 80.0, 100.0], R(bars.Select(b => b.Height)));
    }

    /// <summary>
    ///     Validates a fixed width: 200 pixels give the step 62.5, the band width 50 and bars of 25.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_GroupedBarsInAFixedWidthFillTheBandwidth()
    {
        // Act
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", Grouped, top: ", \"width\": 200"), 200, 100), "mark-bar");

        // Assert: A starts at 0.2 * 62.5 = 12.5, B at 75, C at 137.5; each bar is 25 wide
        Assert.Equal([12.5, 37.5, 75.0, 100.0, 137.5], R(bars.Select(b => b.X)));
        Assert.All(bars, b => Assert.Equal(25.0, b.Width, 9));
    }

    /// <summary>
    ///     Validates the horizontal variant with yOffset: bars of 20 pixels in the bands of y, the first group on top.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_HorizontalGroupedBarsUseYOffset()
    {
        // Arrange
        const string horizontal = """{ "y": { "field": "c", "type": "nominal" }, "yOffset": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }""";

        // Act: the height is the natural 160 pixels, x is 0 to 5 over 100 pixels
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", horizontal), 100, 160), "mark-bar");

        // Assert
        Assert.Equal([10.0, 30.0, 60.0, 80.0, 110.0], R(bars.Select(b => b.Y)));
        Assert.All(bars, b => Assert.Equal(20.0, b.Height, 9));
        Assert.Equal([20.0, 40.0, 60.0, 80.0, 100.0], R(bars.Select(b => b.Width)));
        Assert.All(bars, b => Assert.Equal(0.0, b.X, 9));
    }

    /// <summary>
    ///     Validates that the padding of the offset scale narrows the bars and leaves a gap inside the group: an inner padding of 0.25 gives the step 43.75, the band 35 and bars of 15 at 20 pixels apart.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_OffsetPaddingNarrowsTheBarsAndLeavesAGap()
    {
        // Arrange
        var encoding = Grouped.Replace("\"field\": \"g\", \"type\": \"nominal\" }, \"y\"", "\"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"paddingInner\": 0.25 } }, \"y\"", StringComparison.Ordinal);

        // Act: the natural width is 43.75 * 3.2 = 140
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", encoding), 140, 100), "mark-bar");

        // Assert: A starts at 0.2 * 43.75 = 8.75
        Assert.Equal([8.75, 28.75, 52.5, 72.5, 96.25], R(bars.Select(b => b.X)));
        Assert.All(bars, b => Assert.Equal(15.0, b.Width, 9));
    }

    /// <summary>
    ///     Validates that a bar with an explicit size is centered in its offset band.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_BarWithASizeIsCenteredInItsOffsetBand()
    {
        // Act
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("{ \"type\": \"bar\", \"size\": 10 }", Grouped), 160, 100), "mark-bar");

        // Assert: the offset band of the first bar is 10 to 30, so a bar of 10 starts at 15
        Assert.Equal([15.0, 35.0, 65.0, 85.0, 115.0], R(bars.Select(b => b.X)));
        Assert.All(bars, b => Assert.Equal(10.0, b.Width, 9));
    }

    /// <summary>
    ///     Validates that the points of a point mark sit at the start of their band plus the center of their offset (the offset scale is a point scale with half a step of outer padding).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PointsSitAtTheBandStartPlusTheOffsetPoint()
    {
        // Act: the step is 50, the band 40 wide at 10, 60 and 110, and the offset points are at 10 and 30 inside it
        var points = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("\"point\"", Grouped), 160, 100), "mark-point");

        // Assert
        Assert.Equal([20.0, 40.0, 70.0, 90.0, 120.0], R(points.Select(p => p.X)));
    }

    /// <summary>
    ///     Validates the marks that are placed by their center: text and rule follow the same positions as the points.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextAndRuleFollowTheOffsetCenter()
    {
        // Arrange
        const string text = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "text": { "field": "v", "type": "quantitative" } }""";
        const string rule = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""";

        // Act
        var texts = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Spec("\"text\"", text), 160, 100), "mark-text");
        var rules = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("\"rule\"", rule), 160, 100), "mark-rule");

        // Assert: text is at the center of the offset point; a rule is a mark of a band and takes the offset band (its center)
        Assert.Equal([20.0, 40.0, 70.0, 90.0, 120.0], R(texts.Select(t => t.X)));
        Assert.Equal(5, rules.Count);
        Assert.Equal([20.0, 40.0, 70.0, 90.0, 120.0], R(rules.Select(r => r.X)));
    }

    /// <summary>
    ///     Validates that a line over the offset positions connects the points of the offset.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LineMarkUsesTheOffsetPositions()
    {
        // Arrange: one series along x with the group as the offset (a line needs one row for each x and offset)
        const string rows = """[ { "c": "A", "g": "x", "v": 1 }, { "c": "A", "g": "y", "v": 2 }, { "c": "B", "g": "x", "v": 3 } ]""";
        const string encoding = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""";

        // Act: the natural width of 2 bands with the step 50 is 50 * (2 - 0.2 + 0.4) = 110
        var marks = ChartTestHelpers.Marks(Spec("\"line\"", encoding, rows), 110, 100);
        var path = Assert.Single(marks.Children.OfType<ScenePath>());

        // Assert: the bands start at 10 and 60 and the offset points are 10 and 30 inside them
        var xs = path.Points.Count > 0 ? path.Points.Select(p => p.X) : path.Segments.Select(s => s.X);
        Assert.Equal([20.0, 40.0, 70.0], R(xs));
    }

    /// <summary>
    ///     Validates the continuous offset of a point mark (a strip plot): the offset scale runs from 0 to the bandwidth over the data, so the points jitter inside the band.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_QuantitativeOffsetOfPointsIsAStripInsideTheBand()
    {
        // Arrange: the step stays on the position (the offset is not discrete), so x has the step 20, width 64 and bandwidth 16; the offset domain is 1 to 5 over 16 pixels
        const string strip = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative" } }""";

        // Act
        var points = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("\"point\"", strip), 64, 100), "mark-point");

        // Assert: A starts at 0.2 * 20 = 4: v = 1 and 2 are at 4 + 0 and 4 + 4; B at 24: 24 + 8 and 24 + 12; C at 44 + 16
        Assert.Equal([4.0, 8.0, 32.0, 36.0, 60.0], R(points.Select(p => p.X)));
    }

    /// <summary>
    ///     Validates that a row whose offset value is not valid is not drawn.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_RowWithInvalidOffsetIsNotDrawn()
    {
        // Arrange
        const string rows = """[ { "c": "A", "o": 1, "v": 1 }, { "c": "A", "o": null, "v": 2 }, { "c": "B", "o": 5, "v": 3 } ]""";
        const string strip = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "o", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative" } }""";

        // Act
        var points = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("\"point\"", strip, rows), 100, 100), "mark-point");

        // Assert
        Assert.Equal(2, points.Count);
    }

    /// <summary>
    ///     Validates stacked grouped bars: the segments of the color field stack inside each group (the offset field splits the stacks), and each group starts at zero.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_StackedGroupedBarsStackInsideEachGroup()
    {
        // Arrange: group x of category A has two segments (2 and 3), group y one (1)
        const string rows = """[ { "c": "A", "g": "x", "s": "p", "v": 2 }, { "c": "A", "g": "x", "s": "q", "v": 3 }, { "c": "A", "g": "y", "s": "p", "v": 1 } ]""";
        const string encoding = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "s", "type": "nominal" } }""";

        // Act: y is 0 to 5 over 100 pixels (20 per unit)
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", encoding, rows), 100, 100), "mark-bar");

        // Assert: the layers of a stack are ordered by the color field descending (q first, then p), so group x has q from 0 to 3 and p from 3 to 5; its stack is 5, not 6 or 7
        Assert.Equal(3, bars.Count);
        var byX = bars.GroupBy(b => b.X).OrderBy(g => g.Key).ToList();
        Assert.Equal(2, byX.Count);
        var first = byX[0].OrderBy(b => b.Y).ToList();
        Assert.Equal([0.0, 40.0], R(first.Select(b => b.Y)));
        Assert.Equal([40.0, 60.0], R(first.Select(b => b.Height)));
        Assert.Equal(20.0, Assert.Single(byX[1]).Height, 9);
    }

    /// <summary>
    ///     Validates that an aggregated grouped bar chart whose color is the offset field is not stacked: each bar starts at the baseline.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_AggregatedGroupedBarsAreNotStacked()
    {
        // Arrange
        const string encoding = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "aggregate": "mean", "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }""";

        // Act: the means are 1, 2, 3, 4 and 5; y is 0 to 5 over 100 pixels
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", encoding), 160, 100), "mark-bar");

        // Assert
        Assert.Equal([20.0, 40.0, 60.0, 80.0, 100.0], R(bars.Select(b => b.Height)));
        Assert.Equal([10.0, 30.0, 60.0, 80.0, 110.0], R(bars.Select(b => b.X)));
    }
}
