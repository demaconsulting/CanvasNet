using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the <c>xOffset</c> and <c>yOffset</c> channels: grouped bars, horizontal grouped bars, offsets with padding, layers, concatenation, facets, and what is reported for the offsets that are not
///     drawn. The charts are laid out through the public API; the numbers are derived by hand from the Vega-Lite rules (the default step is 20 pixels for each bar, the position has the paddings 0.2 and 0.2).
/// </summary>
public class VegaLiteOffsetIntegrationTests
{
    /// <summary>The rows: three categories with two groups (the third category has one).</summary>
    private const string Data = """
        "data": { "values": [ { "c": "A", "g": "x", "v": 1 }, { "c": "A", "g": "y", "v": 2 }, { "c": "B", "g": "x", "v": 3 }, { "c": "B", "g": "y", "v": 4 }, { "c": "C", "g": "x", "v": 5 } ] }
        """;

    /// <summary>The encoding of a grouped bar chart without axes and legend, so the bars are the only content.</summary>
    private const string Grouped = """{ "x": { "field": "c", "type": "nominal", "axis": null }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "axis": null }, "color": { "field": "g", "type": "nominal", "legend": null } }""";

    /// <summary>The rows: two groups over three months of 2023.</summary>
    private const string MonthlyData = """
        "data": { "values": [ { "d": "2023-01-15", "g": "a", "v": 3 }, { "d": "2023-01-20", "g": "b", "v": 5 }, { "d": "2023-02-10", "g": "a", "v": 4 }, { "d": "2023-02-12", "g": "b", "v": 2 }, { "d": "2023-03-05", "g": "a", "v": 6 }, { "d": "2023-03-25", "g": "b", "v": 1 } ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Gets the encoding of a grouped bar chart over months, with the legend but without axes.
    /// </summary>
    /// <param name="unit">The time unit of the position.</param>
    /// <returns>The encoding object.</returns>
    private static string MonthlyEncoding(string unit) =>
        "{ \"x\": { \"field\": \"d\", \"type\": \"temporal\", \"timeUnit\": \"" + unit + "\", \"axis\": null }, \"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, "
        + "\"y\": { \"field\": \"v\", \"type\": \"quantitative\", \"aggregate\": \"sum\", \"axis\": null }, \"color\": { \"field\": \"g\", \"type\": \"nominal\" } }";

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json) => VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the rectangles of a role with their absolute positions on the canvas.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The absolute x, width, y and height of each rectangle in drawing order.</returns>
    private static List<(double X, double Width, double Y, double Height)> Rects(Scene scene, string role) =>
        [.. scene.FindAll(role).Select(e => (Math.Round(e.OffsetX + ((SceneRect)e.Item).X, 6), ((SceneRect)e.Item).Width, Math.Round(e.OffsetY + ((SceneRect)e.Item).Y, 6), ((SceneRect)e.Item).Height))];

    /// <summary>
    ///     Renders a specification with a strict policy and collects what it reports.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The unsupported features and the warnings.</returns>
    private static (List<string> Unsupported, List<string> Warnings) Report(string json)
    {
        var unsupported = new List<string>();
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            UnsupportedFeatureHandler = u => unsupported.Add(u.Feature),
            WarningHandler = w => warnings.Add(w.Message),
        };
        using var surface = VegaLiteDocument.Parse(json).Render(options, Ct);
        return (unsupported, warnings);
    }

    /// <summary>
    ///     Validates a grouped bar chart through the whole pipeline: the view is 160 pixels wide, the bars are 20 pixels wide at 10, 30, 60, 80 and 110 from the left of the view, and nothing is reported (so strict mode has
    ///     nothing to throw for).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_GroupedBarsHaveTwentyPixelBarsAndReportNothing()
    {
        // Arrange
        var json = "{ \"padding\": 0, " + Data + ", \"mark\": \"bar\", \"encoding\": " + Grouped + " }";

        // Act
        var scene = SceneOf(json);
        var bars = Rects(scene, "mark-bar");
        var (unsupported, warnings) = Report(json);

        // Assert: the canvas is the view (no padding, axes or legend)
        Assert.Equal(160, scene.Width, 6);
        Assert.Equal([10.0, 30.0, 60.0, 80.0, 110.0], bars.Select(b => b.X));
        Assert.All(bars, b => Assert.Equal(20, b.Width, 9));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
        using var strict = VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
    }

    /// <summary>
    ///     Validates the guides of a grouped bar chart: the legend lists the groups and the x axis has one tick for each category, centered in its band (the offset does not move the axis).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_AxisIsCenteredOnTheBandAndLegendListsTheGroups()
    {
        // Arrange
        var json = "{ " + Data + ", \"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"c\", \"type\": \"nominal\" }, \"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\": { \"field\": \"v\", \"type\": \"quantitative\" }, \"color\": { \"field\": \"g\", \"type\": \"nominal\" } } }";

        // Act
        var scene = SceneOf(json);
        var bars = Rects(scene, "mark-bar");
        var labels = scene.FindAll("axis-label").Where(e => ((SceneText)e.Item).Text is "A" or "B" or "C").ToDictionary(e => ((SceneText)e.Item).Text, e => e.OffsetX + ((SceneText)e.Item).X);
        var legend = scene.FindAll("legend-label").Select(e => ((SceneText)e.Item).Text).ToList();

        // Assert: the label of a category is at the center of the band that holds its bars (the first two bars are A's and the next two B's), and the legend lists the groups
        Assert.Equal(bars[0].X + bars[0].Width, labels["A"], 6);
        Assert.Equal(bars[2].X + bars[2].Width, labels["B"], 6);
        Assert.Equal(["x", "y"], legend);
    }

    /// <summary>
    ///     Validates the horizontal variant: <c>yOffset</c> with the groups as bands of 20 pixels in the bands of y.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_YOffsetDrawsHorizontalGroupedBars()
    {
        // Arrange
        const string encoding = """{ "y": { "field": "c", "type": "nominal", "axis": null }, "yOffset": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative", "axis": null }, "color": { "field": "g", "type": "nominal", "legend": null } }""";
        var json = "{ \"padding\": 0, " + Data + ", \"mark\": \"bar\", \"encoding\": " + encoding + " }";

        // Act
        var scene = SceneOf(json);
        var bars = Rects(scene, "mark-bar");

        // Assert
        Assert.Equal(160, scene.Height, 6);
        Assert.Equal([10.0, 30.0, 60.0, 80.0, 110.0], bars.Select(b => b.Y));
        Assert.All(bars, b => Assert.Equal(20, b.Height, 9));
    }

    /// <summary>
    ///     Validates the padding of the scale of the offset: the bars shrink and a gap opens inside the group.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_ScalePaddingOfTheOffsetLeavesAGapInsideTheGroup()
    {
        // Arrange
        var encoding = Grouped.Replace("\"field\": \"g\", \"type\": \"nominal\" }, \"y\"", "\"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"paddingInner\": 0.25 } }, \"y\"", StringComparison.Ordinal);
        var json = "{ \"padding\": 0, " + Data + ", \"mark\": \"bar\", \"encoding\": " + encoding + " }";

        // Act
        var scene = SceneOf(json);
        var bars = Rects(scene, "mark-bar");

        // Assert: the view is 43.75 * 3.2 = 140 wide and the bars are 15 wide, 5 pixels apart
        Assert.Equal(140, scene.Width, 6);
        Assert.All(bars, b => Assert.Equal(15, b.Width, 9));
        Assert.Equal(5.0, bars[1].X - (bars[0].X + bars[0].Width), 9);
    }

    /// <summary>
    ///     Validates that a width given as a number gives the bands the room that is left.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_FixedWidthSharesTheBandwidth()
    {
        // Arrange
        var json = "{ \"padding\": 0, \"width\": 200, " + Data + ", \"mark\": \"bar\", \"encoding\": " + Grouped + " }";

        // Act
        var bars = Rects(SceneOf(json), "mark-bar");

        // Assert
        Assert.All(bars, b => Assert.Equal(25, b.Width, 9));
        Assert.Equal(12.5, bars[0].X, 9);
    }

    /// <summary>
    ///     Validates that the offset scales of the children of a concatenation are independent by default (each chart is sized by its own groups) and that
    ///     <c>resolve.scale.xOffset: shared</c> shares the domain.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_ConcatChartsHaveIndependentOffsetScalesUnlessShared()
    {
        // Arrange: the first chart has two groups, the second one (filtered) has one
        const string first = """{ "mark": "bar", "encoding": { "x": { "field": "c", "type": "nominal", "axis": null }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        const string second = """{ "transform": [ { "filter": "datum.g == 'x'" } ], "mark": "bar", "encoding": { "x": { "field": "c", "type": "nominal", "axis": null }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        var independent = "{ \"padding\": 0, " + Data + ", \"hconcat\": [" + first + ", " + second + "] }";
        var shared = "{ \"padding\": 0, " + Data + ", \"resolve\": { \"scale\": { \"xOffset\": \"shared\" } }, \"hconcat\": [" + first + ", " + second + "] }";

        // Act
        var alone = SceneOf(independent);
        var together = SceneOf(shared);

        // Assert: the filtered chart has one group in its own offset domain (step 20 * 1 / 0.8 = 25, 80 pixels wide), and the shared domain of two groups makes it as wide as the first chart (160 pixels)
        Assert.Equal(80, together.Width - alone.Width, 6);
        Assert.All(Rects(alone, "mark-bar").Skip(5), b => Assert.Equal(20, b.Width, 9));
        Assert.All(Rects(together, "mark-bar"), b => Assert.Equal(20, b.Width, 9));
    }

    /// <summary>
    ///     Validates a faceted grouped bar chart: every cell has the same grouped bars (the offset domain is shared by the cells of a facet).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_FacetCellsShareTheOffsetDomain()
    {
        // Arrange: two cells (one for each segment) in which only some groups occur
        const string rows = """[ { "p": "u", "c": "A", "g": "x", "v": 1 }, { "p": "u", "c": "A", "g": "y", "v": 2 }, { "p": "w", "c": "A", "g": "x", "v": 3 } ]""";
        const string spec = """{ "mark": "bar", "encoding": { "x": { "field": "c", "type": "nominal", "axis": null }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        var json = "{ \"padding\": 0, \"data\": { \"values\": " + rows + " }, \"facet\": { \"column\": { \"field\": \"p\", \"type\": \"nominal\" } }, \"spec\": " + spec + " }";

        // Act
        var bars = Rects(SceneOf(json), "mark-bar");

        // Assert: the one-group cell still has bars of the width of the shared offset band (the cells share the offset domain of two groups, so the bar is 20 wide at the start of its slot)
        Assert.Equal(3, bars.Count);
        Assert.All(bars, b => Assert.Equal(20, b.Width, 9));
    }

    /// <summary>
    ///     Validates layers: a grouped bar layer and a tick layer over the same offsets share the position and the offset scales.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_LayersShareTheOffsetScale()
    {
        // Arrange
        const string bar = """{ "mark": "bar", "encoding": { "x": { "field": "c", "type": "nominal", "axis": null }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        const string text = """{ "mark": "text", "encoding": { "x": { "field": "c", "type": "nominal", "axis": null }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "axis": null }, "text": { "field": "v", "type": "quantitative" } } }""";
        var json = "{ \"padding\": 0, " + Data + ", \"layer\": [" + bar + ", " + text + "] }";

        // Act
        var scene = SceneOf(json);
        var bars = Rects(scene, "mark-bar");
        var texts = scene.FindAll("mark-text").Select(e => e.OffsetX + ((SceneText)e.Item).X).ToList();

        // Assert: the text is at the center of its bar
        Assert.Equal(bars.Select(b => Math.Round(b.X + (b.Width / 2), 6)), texts.Select(t => Math.Round(t, 6)));
    }

    /// <summary>
    ///     Validates what is reported for an offset that is not drawn, and that the marks are drawn at the position.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_NotDrawnOffsetsAreReportedAndIgnored()
    {
        // Arrange: a quantitative x, a bar with a quantitative offset (ranged), and the time unit positions that cannot hold an offset (the day of the week) or can (a month)
        const string quantitative = """{ "mark": "point", "encoding": { "x": { "field": "v", "type": "quantitative" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }""";
        const string ranged = """{ "mark": "bar", "encoding": { "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative" } } }""";

        // Act
        var (quantitativeReport, _) = Report("{ " + Data + ", " + quantitative.TrimStart('{').TrimEnd('}') + " }");
        var (rangedReport, _) = Report("{ " + Data + ", " + ranged.TrimStart('{').TrimEnd('}') + " }");
        var (dayReport, _) = Report("{ " + MonthlyData + ", \"mark\": \"bar\", \"encoding\": " + MonthlyEncoding("day") + " }");
        var (monthReport, warnings) = Report("{ " + MonthlyData + ", \"mark\": \"bar\", \"encoding\": " + MonthlyEncoding("month") + " }");

        // Assert
        Assert.Equal(["vl-offset-position"], quantitativeReport);
        Assert.Equal(["vl-offset-ranged"], rangedReport);
        Assert.Equal(["vl-offset-position"], dayReport);
        Assert.Empty(monthReport);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates a grouped bar chart over months (the temporal position has a time unit): the offset spans a share of the length of a month on the time scale, as Vega-Lite 6.5 draws it. The view is 300
    ///     pixels wide for the domain of January to March (a <c>month</c> unit puts the dates in the leap year 2012: 1 January to 1 April, 91 days), so a day is 300/91 pixels; the reference month (January 2001) has 31 days, the offset
    ///     range is [0.1, 0.9] of it (padding 0.2) and the two groups each have 0.4 of it. The bars start at the start of their month plus the offset.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_MonthlyGroupedBars()
    {
        // Arrange
        var json = "{ \"padding\": 0, \"width\": 300, " + MonthlyData + ", \"mark\": \"bar\", \"encoding\": " + MonthlyEncoding("month") + " }";

        // Act
        var scene = SceneOf(json);
        var bars = Rects(scene, "mark-bar");
        var (unsupported, warnings) = Report(json);

        // Assert: the first bars of January start at 0.1 and 0.5 of the reference month and touch, those of February are at the start of February (31 days) plus the same offsets
        var pixelsPerDay = 300.0 / 91;
        var month = 31 * pixelsPerDay;
        Assert.Equal(6, bars.Count);
        Assert.All(bars, b => Assert.Equal(0.8 * month / 2, b.Width, 4));
        double[] expected = [0.1 * month, 0.5 * month, month + (0.1 * month), month + (0.5 * month)];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], bars[i].X, 4);
        }

        Assert.Equal(bars[0].X + bars[0].Width, bars[1].X, 4);
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
        using var strict = VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
        Assert.Equal(["a", "b"], scene.FindAll("legend-label").Select(e => ((SceneText)e.Item).Text));
    }


    /// <summary>
    ///     Validates that a boxplot is drawn with an offset channel: the expansion of the composite mark keeps it, so the boxes of the groups of a category sit side by side.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Offset_BoxplotBoxesSitSideBySide()
    {
        // Arrange
        const string rows = """[ { "c": "A", "g": "x", "v": 1 }, { "c": "A", "g": "x", "v": 2 }, { "c": "A", "g": "x", "v": 4 }, { "c": "A", "g": "y", "v": 2 }, { "c": "A", "g": "y", "v": 3 }, { "c": "A", "g": "y", "v": 6 }, { "c": "B", "g": "x", "v": 3 }, { "c": "B", "g": "x", "v": 4 }, { "c": "B", "g": "x", "v": 9 } ]""";
        const string encoding = """{ "x": { "field": "c", "type": "nominal", "axis": null }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "axis": null } }""";
        var json = "{ \"padding\": 0, \"data\": { \"values\": " + rows + " }, \"mark\": \"boxplot\", \"encoding\": " + encoding + " }";

        // Act
        var boxes = Rects(SceneOf(json), "mark-bar");

        // Assert: three boxes (A x, A y, B x); a box has the size 14 and is centered in its offset band of 20, so the two boxes of A are 6 pixels apart
        Assert.Equal(3, boxes.Count);
        Assert.Equal(14, boxes[0].Width, 6);
        Assert.Equal(6, boxes[1].X - (boxes[0].X + boxes[0].Width), 6);
        Assert.Equal(boxes[0].Width, boxes[1].Width, 6);
    }
}
