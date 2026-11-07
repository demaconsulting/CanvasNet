using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the <c>titleOrient</c> of <see cref="LegendBuilder"/> (Phase 13): the title on the left, right, top and bottom of the entries of a symbol legend and of a vertical and a
///     horizontal gradient legend. The rules are those of vega-view-transforms' <c>legendGroupLayout</c> and <c>legendTitleOffset</c> and vega-parser's <c>legend-title.js</c>; the expectations are
///     written as the arithmetic of those functions over the measured boxes of the title and the entries (the text is measured with the bundled font), and the vertical positions of the symbol
///     legend (the title of three entries on the left at 25, the right title at the width of the entries plus 5, the bottom title at 55) were observed on Vega 6.4 with the same padding of 0.
/// </summary>
public class LegendBuilderTitleOrientTests
{
    /// <summary>A scatter with a nominal color field of three categories.</summary>
    private const string Symbols = """
        { "data": { "values": [ { "a": "A", "b": 1, "c": "x" }, { "a": "B", "b": 3, "c": "yy" }, { "a": "C", "b": 2, "c": "zzz" } ] },
          "mark": "point",
          "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "b", "type": "quantitative" },
                        "color": { "field": "c", "type": "nominal", "legend": LEGEND } } }
        """;

    /// <summary>A scatter with a quantitative color field (a gradient legend).</summary>
    private const string Gradient = """
        { "data": { "values": [ { "a": "A", "b": 1, "c": 10 }, { "a": "B", "b": 3, "c": 50 }, { "a": "C", "b": 2, "c": 90 } ] },
          "mark": "point",
          "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "b", "type": "quantitative" },
                        "color": { "field": "c", "type": "quantitative", "legend": LEGEND } } }
        """;

    /// <summary>The measurer of the text.</summary>
    private static readonly TextMeasurer Measurer = new();

    /// <summary>
    ///     Builds the first legend of a specification.
    /// </summary>
    /// <param name="template">The specification with the <c>LEGEND</c> placeholder.</param>
    /// <param name="legend">The JSON text of the legend object.</param>
    /// <returns>The legend.</returns>
    private static LegendResult Build(string template, string legend) =>
        ChartTestHelpers.Legend(template.Replace("LEGEND", legend, StringComparison.Ordinal), 0, 300, 200);

    /// <summary>
    ///     Gets the entry group of a legend.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The group.</returns>
    private static SceneGroup Entry(LegendResult legend) => Assert.IsType<SceneGroup>(legend.Children[0]);

    /// <summary>
    ///     Gets the title of a legend.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The title text.</returns>
    private static SceneText Title(LegendResult legend) => Assert.IsType<SceneText>(legend.Children[1]);

    /// <summary>
    ///     Gets the bounds of the entries in the coordinates of the entry group (the symbols with their stroke, the labels and the gradient).
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The bounds.</returns>
    private static SceneBounds EntryBounds(LegendResult legend)
    {
        var bounds = SceneBounds.Empty;
        foreach (var child in Entry(legend).Children)
        {
            bounds = bounds.Union(child switch
            {
                SceneSymbol symbol => LegendBuilder.SymbolBounds(symbol),
                SceneText text => Measurer.GetBounds(text),
                SceneRect rect => new SceneBounds(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height),
                _ => SceneBounds.Empty,
            });
        }

        return bounds;
    }

    /// <summary>
    ///     Gets the bounds of the gradient bar.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The bounds of the bar in the coordinates of the entry group.</returns>
    private static SceneBounds BarBounds(LegendResult legend)
    {
        var bar = Assert.IsType<SceneRect>(Entry(legend).Children.Single(c => c.Role == "legend-gradient"));
        return new SceneBounds(bar.X, bar.Y, bar.X + bar.Width, bar.Y + bar.Height);
    }

    /// <summary>
    ///     Gets the box of a text without its position: the size of the title as drawn.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>The bounds at its position.</returns>
    private static SceneBounds Box(SceneText title) => Measurer.GetBounds(title);

    /// <summary>
    ///     Validates the default (<c>top</c>): the entries sit below the title and its padding of 5, the title is at the origin, aligned left from the top, and the legend is as high as the
    ///     two together.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleOrientTopPutsTheEntriesBelowTheTitle()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "title": "Category" }""");
        var title = Title(legend);
        var box = Box(title);
        var entry = EntryBounds(legend);

        // Assert
        Assert.Equal((0.0, 0.0), (title.X, title.Y));
        Assert.Equal(("left", "top", 0.0), (title.Align, title.Baseline, title.Angle));
        Assert.Equal((0.0, box.Height + 5), (Entry(legend).X, Entry(legend).Y));
        Assert.Equal(Math.Ceiling(Math.Max(box.X2, entry.X2)), legend.Width);
        Assert.Equal(Math.Ceiling(box.Height + 5 + entry.Y2), legend.Height);
    }

    /// <summary>
    ///     Validates <c>left</c>: the entries move right by the width of the title and 5 (the title's width rounded up), the title is at the left edge, centered on the entries' height (middle
    ///     anchor, rounded half up) with the baseline middle, and the legend is as wide as the two together.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleOrientLeftPutsTheTitleBesideTheEntries()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "title": "Category", "titleOrient": "left" }""");
        var title = Title(legend);
        var box = Box(title);
        var entry = EntryBounds(legend);

        // Assert: the entries are right of the title; the title is centered on them: round(0.5 * 50) = 25 for three symbols of 18 pixels
        Assert.Equal((Math.Ceiling(box.Width) + 5, 0.0), (Entry(legend).X, Entry(legend).Y));
        Assert.Equal((0.0, TextMeasurer.RoundHalfUp(0.5 * entry.Y2)), (title.X, title.Y));
        Assert.Equal(25.0, title.Y);
        Assert.Equal(("left", "middle", 0.0), (title.Align, title.Baseline, title.Angle));

        // Assert: as wide as the title, the gap and the entries; as high as the entries
        Assert.Equal(Math.Ceiling(Entry(legend).X + entry.X2), legend.Width);
        Assert.Equal(Math.Ceiling(entry.Y2), legend.Height);
    }

    /// <summary>
    ///     Validates <c>right</c>: the title is past the widest entry (rounded) and 5 more, centered on the height of the entries, and the legend is as wide as the entries and the title.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleOrientRightPutsTheTitlePastTheEntries()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "title": "Category", "titleOrient": "right" }""");
        var title = Title(legend);
        var box = Box(title);
        var entry = EntryBounds(legend);

        // Assert
        Assert.Equal((0.0, 0.0), (Entry(legend).X, Entry(legend).Y));
        Assert.Equal((TextMeasurer.RoundHalfUp(entry.X2) + 5, 25.0), (title.X, title.Y));
        Assert.Equal(("left", "middle", 0.0), (title.Align, title.Baseline, title.Angle));
        Assert.Equal(Math.Ceiling(box.X2), legend.Width);
        Assert.Equal(Math.Ceiling(entry.Y2), legend.Height);
    }

    /// <summary>
    ///     Validates <c>bottom</c>: the title is below the entries (their bottom rounded, plus 5), aligned left from the top, and the legend is as high as the two together.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleOrientBottomPutsTheTitleBelowTheEntries()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "title": "Category", "titleOrient": "bottom" }""");
        var title = Title(legend);
        var box = Box(title);
        var entry = EntryBounds(legend);

        // Assert: the bottom of the three symbols with their stroke is 50, so the title is at 55
        Assert.Equal((0.0, 0.0), (Entry(legend).X, Entry(legend).Y));
        Assert.Equal((0.0, TextMeasurer.RoundHalfUp(entry.Y2) + 5), (title.X, title.Y));
        Assert.Equal(55.0, title.Y);
        Assert.Equal(("left", "top", 0.0), (title.Align, title.Baseline, title.Angle));
        Assert.Equal(Math.Ceiling(box.Y2), legend.Height);
    }

    /// <summary>
    ///     Validates <c>titleAnchor</c> with a side title: <c>start</c> puts the title at the top of the entries (offset 0), <c>end</c> at their bottom less the multi-line allowance, and the
    ///     default of the sides is <c>middle</c>; the baseline follows the anchor (top, bottom, middle) for a title that is not turned.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleAnchorPlacesASideTitleAlongTheEntries()
    {
        // Arrange / Act
        var start = Build(Symbols, """{ "title": "Category", "titleOrient": "left", "titleAnchor": "start" }""");
        var end = Build(Symbols, """{ "title": "Category", "titleOrient": "left", "titleAnchor": "end" }""");
        var entry = EntryBounds(end);

        // Assert
        Assert.Equal((0.0, "top"), (Title(start).Y, Title(start).Baseline));
        Assert.Equal((TextMeasurer.RoundHalfUp(entry.Y2), "bottom"), (Title(end).Y, Title(end).Baseline));
    }

    /// <summary>
    ///     Validates the title of a multi-line title on the left: the allowance of the extra lines (one line height each) is taken from the end and the middle anchor positions.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleOfSeveralLinesLeavesRoomAtTheEnd()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "title": ["Crop", "kind"], "titleOrient": "left" }""");
        var title = Title(legend);
        var entry = EntryBounds(legend);
        var lineHeight = TextMeasurer.LineHeight(11, null);

        // Assert: round(0.5 * (50 - one line height))
        Assert.Equal(TextMeasurer.RoundHalfUp(0.5 * (entry.Y2 - lineHeight)), title.Y);
    }

    /// <summary>
    ///     Validates a vertical gradient with the title on the left and on the right: the title is turned (-90 and 90 degrees), centered along the gradient bar (round(0.5 * bar bottom)),
    ///     aligned center with the baseline top on the left and bottom on the right, and the entries make room for the turned title.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleOfAVerticalGradientIsTurnedAtTheSides()
    {
        // Arrange / Act
        var left = Build(Gradient, """{ "title": "Score", "titleOrient": "left" }""");
        var right = Build(Gradient, """{ "title": "Score", "titleOrient": "right" }""");
        var leftTitle = Title(left);
        var rightTitle = Title(right);
        var bar = BarBounds(left);

        // Assert: left: turned -90, the entries are right of the rotated box (its width is the height of the text) and the title is centered along the bar
        Assert.Equal((-90.0, "center", "top"), (leftTitle.Angle, leftTitle.Align, leftTitle.Baseline));
        Assert.Equal(Math.Ceiling(Box(leftTitle).Width) + 5, Entry(left).X);
        Assert.Equal((0.0, TextMeasurer.RoundHalfUp(0.5 * bar.Y2)), (leftTitle.X, leftTitle.Y));

        // Assert: right: turned 90 degrees, past the entries (their right edge, rounded, plus 5), centered along the bar
        Assert.Equal((90.0, "center", "bottom"), (rightTitle.Angle, rightTitle.Align, rightTitle.Baseline));
        Assert.Equal((0.0, 0.0), (Entry(right).X, Entry(right).Y));
        Assert.Equal((TextMeasurer.RoundHalfUp(EntryBounds(right).X2) + 5, TextMeasurer.RoundHalfUp(0.5 * BarBounds(right).Y2)), (rightTitle.X, rightTitle.Y));
    }

    /// <summary>
    ///     Validates a horizontal gradient with the title on the left and the right: the title is not turned (only a vertical gradient turns it), left aligned with the baseline middle, and centered on the
    ///     bar (its height, 16, so 8) rather than on the labels below it; on the right it is past the entries.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleOfAHorizontalGradientIsCenteredOnTheBar()
    {
        // Arrange / Act
        var left = Build(Gradient, """{ "title": "Score", "titleOrient": "left", "orient": "bottom", "direction": "horizontal" }""");
        var right = Build(Gradient, """{ "title": "Score", "titleOrient": "right", "orient": "bottom", "direction": "horizontal" }""");
        var bar = BarBounds(left);

        // Assert: the bar is 16 high, so the title of the left legend is at round(0.5 * 16) = 8 and is not turned
        Assert.Equal(16.0, bar.Y2);
        Assert.Equal((0.0, 8.0, 0.0), (Title(left).X, Title(left).Y, Title(left).Angle));
        Assert.Equal(("left", "middle"), (Title(left).Align, Title(left).Baseline));
        Assert.Equal(Math.Ceiling(Box(Title(left)).Width) + 5, Entry(left).X);

        // Assert: right: past the entries, at 8, left aligned
        Assert.Equal((TextMeasurer.RoundHalfUp(EntryBounds(right).X2) + 5, 8.0), (Title(right).X, Title(right).Y));
        Assert.Equal(("left", "middle", 0.0), (Title(right).Align, Title(right).Baseline, Title(right).Angle));
    }

    /// <summary>
    ///     Validates that <c>titleAlign</c> and <c>titleBaseline</c> win over the alignment and the baseline that the orient gives the title.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TitleAlignAndBaselineOverrideTheOrientDefaults()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "title": "Category", "titleOrient": "left", "titleAlign": "center", "titleBaseline": "bottom" }""");

        // Assert
        Assert.Equal(("center", "bottom"), (Title(legend).Align, Title(legend).Baseline));
    }
}
