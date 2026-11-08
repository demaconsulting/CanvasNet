using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Tests of <c>config.scale.invalid</c> in <see cref="DemaConsulting.CanvasNet.VegaLite.Marks.MarkBuilder"/> (Phase 14; Vega-Lite 6.5.0 <c>invalid.ts</c>, <c>compile/invalid/ScaleInvalidDataMode.ts</c> and
///     <c>compile/mark/encode/invalid.ts</c>): a channel whose scale has an output for invalid values (<c>"zero-or-min"</c> or <c>{"value": ...}</c>) draws the rows whose value is not a number, whatever the <c>invalid</c> mode
///     of the mark is. The expected numbers are those of Vega 6.4 with Vega-Lite 6.5.0 for the same specifications (the SVG of <c>vega.View.toSVG</c>), read once and written out here.
/// </summary>
public class MarkBuilderScaleInvalidTests
{
    /// <summary>Four rows with a null in the x of the third, in the color, size and opacity field of the second, and (in <see cref="WithNullY"/>) in the y of the second; a data rectangle of 200 by 100.</summary>
    private const string Rows = """
        "data": { "values": [ { "a": 1, "b": 2, "c": 5 }, { "a": 2, "b": 3, "c": null }, { "a": null, "b": 4, "c": 9 }, { "a": 4, "b": 6, "c": 2 } ] }, "width": 200, "height": 100
        """;

    /// <summary>The same rows with a null in the y of the second.</summary>
    private const string WithNullY = """
        "data": { "values": [ { "a": 1, "b": 2, "c": 5 }, { "a": 2, "b": null, "c": null }, { "a": null, "b": 4, "c": 9 }, { "a": 4, "b": 6, "c": 2 } ] }, "width": 200, "height": 100
        """;

    /// <summary>The encoding of the scatter plot: position, color, size and opacity over the three fields.</summary>
    private const string Encoding = """
        "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" }, "color": { "field": "c", "type": "quantitative" },
                      "size": { "field": "c", "type": "quantitative" }, "opacity": { "field": "c", "type": "quantitative" } }
        """;

    /// <summary>
    ///     Gets the points of a specification.
    /// </summary>
    /// <param name="data">The data member of the specification.</param>
    /// <param name="invalid">The JSON text of <c>config.scale.invalid</c>, or null for none.</param>
    /// <returns>The symbols in row order.</returns>
    private static List<SceneSymbol> Points(string data, string? invalid)
    {
        var config = invalid is null ? string.Empty : ", \"config\": { \"scale\": { \"invalid\": " + invalid + " } }";
        var json = "{ " + data + config + ", \"mark\": \"point\", " + Encoding + " }";
        return ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(json, 200, 100), "mark-point");
    }

    /// <summary>
    ///     Validates the values of the entries: a color <c>{"value": "#aaa"}</c> and an opacity <c>{"value": 0.25}</c> for the second row (no color value), a pixel position <c>{"value": 5}</c> for the third (no x), and
    ///     the output of <c>"zero-or-min"</c> for the size of the second row (the position, the stroke and the opacity of the other rows come from the scales), as Vega draws it.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ScaleInvalidValuesAreTheOutputOfTheChannel()
    {
        // Arrange / Act
        var points = Points(Rows, """{ "color": { "value": "#aaa" }, "x": { "value": 5 }, "size": "zero-or-min", "opacity": { "value": 0.25 } }""");

        // Assert: Vega: (50, 66.667) rgb(109, 174, 213) opacity 0.514; (100, 50) #aaa 0.25 radius 1; (5, 33.333) rgb(10, 74, 144) 0.8 radius 9.5; (200, 0) rgb(207, 225, 242) 0.3
        Assert.Equal(4, points.Count);
        Assert.Equal([(50.0, 66.666666667), (100.0, 50.0), (5.0, 33.333333333), (200.0, 0.0)], points.Select(p => (Math.Round(p.X, 9), Math.Round(p.Y, 9))));
        Assert.Equal(["#6daed5", "#aaa", "#0a4a90", "#cfe1f2"], points.Select(p => p.Stroke));
        Assert.Equal([0.514285714, 0.25, 0.8, 0.3], points.Select(p => Math.Round(p.Opacity, 9)));

        // The radius of a circle is half of the square root of its size (Vega's circle symbol): 7.112, 1, 9.5 and 4.564 in Vega's output
        Assert.Equal([7.112, 1.0, 9.5, 4.564], points.Select(p => Math.Round(Math.Sqrt(p.Size) / 2, 3)));
    }

    /// <summary>
    ///     Validates that a channel that <c>config.scale.invalid</c> does not name keeps the mode of the mark: the row whose y is null is not drawn (the mark's mode is <c>filter</c> for a point) while the null of the
    ///     color of the same row is not the reason, and the other rows of the configured channels are drawn.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_AChannelWithoutAnEntryStillFiltersItsInvalidRows()
    {
        // Arrange / Act
        var points = Points(WithNullY, """{ "color": { "value": "#aaa" }, "x": { "value": 5 }, "size": "zero-or-min" }""");

        // Assert: the second row has no y: Vega's filter on the y field removes it, the third has no x and is drawn at 5
        Assert.Equal([(50.0, 66.666666667), (5.0, 33.333333333), (200.0, 0.0)], points.Select(p => (Math.Round(p.X, 9), Math.Round(p.Y, 9))));
    }

    /// <summary>
    ///     Validates the contrast with no configuration: the rows with a missing value of a continuous channel are not drawn (the mode <c>filter</c> of a point), which is the output before Phase 14 and Vega-Lite's
    ///     without <c>config.scale.invalid</c>.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_WithoutScaleInvalidTheRowsAreFiltered()
    {
        // Arrange / Act
        var points = Points(Rows, null);

        // Assert: the rows with a null color (the second) and a null x (the third) are not drawn
        Assert.Equal([(50.0, 66.666666667), (200.0, 0.0)], points.Select(p => (Math.Round(p.X, 9), Math.Round(p.Y, 9))));
    }

    /// <summary>
    ///     Validates that the configured entry wins over the <c>invalid</c> mode of the mark: with <c>"filter"</c> the channel with an entry is still drawn, and a channel without an entry is filtered.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ScaleInvalidWinsOverTheInvalidModeOfTheMark()
    {
        // Arrange
        var json = "{ " + Rows + ", \"config\": { \"scale\": { \"invalid\": { \"color\": { \"value\": \"#aaa\" }, \"x\": { \"value\": 5 }, \"size\": \"zero-or-min\", \"opacity\": \"zero-or-min\" } } }, \"mark\": { \"type\": \"point\", \"invalid\": \"filter\" }, " + Encoding + " }";

        // Act
        var points = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(json, 200, 100), "mark-point");

        // Assert: every channel that has a missing value has an entry, so all four rows are drawn
        Assert.Equal(4, points.Count);
        Assert.Equal("#aaa", points[1].Stroke);
    }

    /// <summary>
    ///     Validates a line: with <c>"zero-or-min"</c> for y the point of the row without a value is at the baseline and the path is not broken (Vega: <c>M0,44.444L66.667,100L133.333,0L200,77.778</c>); without it the path breaks there.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ScaleInvalidKeepsAPathUnbroken()
    {
        // Arrange: the third row has an x here (the line needs one in every row)
        const string lineRows = """
            "data": { "values": [ { "a": 1, "c": 5 }, { "a": 2, "c": null }, { "a": 3, "c": 9 }, { "a": 4, "c": 2 } ] }, "width": 200, "height": 100
            """;
        var line = "\"mark\": \"line\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"c\", \"type\": \"quantitative\" } }";
        var configured = "{ " + lineRows + ", \"config\": { \"scale\": { \"invalid\": { \"y\": \"zero-or-min\" } } }, " + line + " }";
        var plain = "{ " + lineRows + ", " + line + " }";

        // Act
        var kept = Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(configured, 200, 100), "mark-line"));
        var broken = Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(plain, 200, 100), "mark-line"));

        // Assert: Vega draws the first path through the baseline; without the entry the path breaks at the row without a value and the point of the first row stays alone
        Assert.Equal("M0,44.444444 L66.666667,100 L133.333333,0 L200,77.777778", PathText.Of(kept));
        Assert.Equal("M0,44.444444 Z M133.333333,0 L200,77.777778", PathText.Of(broken));
    }

    /// <summary>
    ///     Validates the entries that this renderer does not read (an expression, <c>null</c>, a boolean) and the channels without a continuous scale: they are left alone, so a null is filtered as without the configuration.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_UnreadableScaleInvalidEntriesAreIgnored()
    {
        // Arrange / Act
        var points = Points(Rows, """{ "color": { "value": null }, "x": { "value": true }, "size": "other", "opacity": { "expr": "1" }, "y": { "value": 3 } }""");

        // Assert: the entry of y is read (y has no missing value here) and the other entries are not, so the rows with a null color and x are filtered as before
        Assert.Equal(2, points.Count);
    }
}
