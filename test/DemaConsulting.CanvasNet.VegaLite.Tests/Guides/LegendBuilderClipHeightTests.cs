using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the <c>clipHeight</c> of a symbol legend in <see cref="LegendBuilder"/> (Phase 14). The rules are those of vega-parser's <c>legend-symbol-groups.js</c> (the symbol and the label at
///     half of the height, the symbols clipped, the entry group of that height and not <c>noBound</c>), of vega-scenegraph's <c>group.js</c> and <c>boundClip.js</c> (the bounds of a clipped mark and of
///     a group) and of vega-view-transforms' <c>legendEntryLayout</c> (the width of the entry group is that of the widest entry of its column, its height the bottom edge of its bounds). The
///     vertical numbers (the pitch of the entries, the heights of the clip rectangles, the positions of the symbols and labels) are those of Vega 6.4 with Vega-Lite 6.5.0 for the same
///     specifications (the SVG of <c>vega.View.toSVG</c>: the translations of the entry groups, the positions of the symbols and the <c>clipPath</c> rectangles), read once and written out here; the
///     widths depend on the measured text (Vega's headless estimate is 0.8 of the font size per character, this package measures the bundled font), so they are worked out from the measured labels.
/// </summary>
public class LegendBuilderClipHeightTests
{
    /// <summary>A scatter with a nominal color field of three categories (the labels <c>x</c>, <c>yy</c> and <c>zzz</c>).</summary>
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
    ///     Gets the clipping groups of the symbols of a legend (one per entry, in drawing order).
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The groups.</returns>
    private static List<SceneGroup> Clips(LegendResult legend) => [.. Entry(legend).Children.OfType<SceneGroup>().Where(g => g.Role == "legend-symbol-clip")];

    /// <summary>
    ///     Gets the labels of a legend.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The labels in entry order.</returns>
    private static List<SceneText> Labels(LegendResult legend) => [.. Entry(legend).Children.OfType<SceneText>()];

    /// <summary>
    ///     Gets the symbol of a clipping group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The only child of the group.</returns>
    private static SceneSymbol SymbolOf(SceneGroup group) => Assert.IsType<SceneSymbol>(Assert.Single(group.Children));

    /// <summary>
    ///     Gets the right edge of the box of every entry (its label) relative to the origin of its entry group, as the entry layout measures it.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The right edges in entry order.</returns>
    private static List<double> RightEdges(LegendResult legend)
    {
        var clips = Clips(legend);
        return [.. Labels(legend).Select((label, i) => Measurer.GetBounds(label).X2 - clips[i].X)];
    }

    /// <summary>
    ///     Validates the default (no <c>clipHeight</c>): no clipping group, the symbols are direct children of the entry group at half of the entry size.
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_AbsentLeavesTheSymbolsUnclipped()
    {
        // Arrange / Act
        var legend = Build(Symbols, "{}");

        // Assert: the entries are 18 apart as in Vega's output (the entry groups at 0, 18 and 36, the symbols 6 below their group's origin)
        Assert.Empty(Clips(legend));
        Assert.Equal([6.0, 24.0, 42.0], Entry(legend).Children.OfType<SceneSymbol>().Select(s => s.Y));
        Assert.Equal(["legend-symbol", "legend-label", "legend-symbol", "legend-label", "legend-symbol", "legend-label"], Entry(legend).Children.Select(c => c.Role));
    }

    /// <summary>
    ///     Validates a <c>clipHeight</c> of 30 (taller than the symbols): the entries are 30 high with the rows 2 pixels apart (the pitch of 32 of Vega), the symbol is at (6, 15), the label at
    ///     the same height, each symbol is clipped to a rectangle as wide as the widest entry and 30 high, and the legend is as high as the title block plus 94.
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_TallerThanTheSymbolsSetsThePitchAndTheClipBox()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "clipHeight": 30 }""");
        var clips = Clips(legend);
        var labels = Labels(legend);
        var width = RightEdges(legend).Max();

        // Assert: the entry groups (the clipping groups) are at 0, 32 and 64, the rows of Vega's output
        Assert.Equal([0.0, 32.0, 64.0], clips.Select(g => g.Y));
        Assert.All(clips, g => Assert.Equal(0.0, g.X));
        Assert.All(clips, g => Assert.Equal(new SceneClip(0, 0, width, 30), g.Clip));
        Assert.All(clips, g => Assert.Equal((width, 30.0), (g.Width, g.Height)));
        Assert.All(clips, g => Assert.Equal((6.0, 15.0), (SymbolOf(g).X, SymbolOf(g).Y)));
        Assert.Equal([15.0, 47.0, 79.0], labels.Select(l => l.Y));
        Assert.Equal([16.0, 16.0, 16.0], labels.Select(l => l.X));
        Assert.Equal(Math.Ceiling(Entry(legend).Y + 94), legend.Height);
        Assert.Equal(Math.Ceiling(width), legend.Width);
    }

    /// <summary>
    ///     Validates a <c>clipHeight</c> of 10 (smaller than the symbols, 12 high): the entries are 10 high (pitch 12), the symbols at (6, 5), the clip rectangles 10 high, and the legend as high as the
    ///     title block plus 34 (the entry group counts in the bounds with its height).
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_SmallerThanTheSymbolsClipsThemToTheEntry()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "clipHeight": 10 }""");
        var clips = Clips(legend);
        var width = RightEdges(legend).Max();

        // Assert
        Assert.Equal([0.0, 12.0, 24.0], clips.Select(g => g.Y));
        Assert.All(clips, g => Assert.Equal(new SceneClip(0, 0, width, 10), g.Clip));
        Assert.All(clips, g => Assert.Equal(5.0, SymbolOf(g).Y));
        Assert.Equal([5.0, 17.0, 29.0], Labels(legend).Select(l => l.Y));
        Assert.Equal(Math.Ceiling(Entry(legend).Y + 34), legend.Height);
    }

    /// <summary>
    ///     Validates a <c>clipHeight</c> of 4: the label (10 high, centered at 2) reaches below the entry group, so the bounds of the entry are the label's and the clip rectangle is as
    ///     tall as the bottom edge of those bounds (the height of an entry group is <c>bounds.y2 - y</c>, 7 here), and the legend is as high as the title block plus 31.
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_ALabelTallerThanTheEntryWidensTheClipBox()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "clipHeight": 4 }""");
        var clips = Clips(legend);
        var width = RightEdges(legend).Max();

        // Assert
        Assert.Equal([0.0, 12.0, 24.0], clips.Select(g => g.Y));
        Assert.All(clips, g => Assert.Equal(new SceneClip(0, 0, width, 7), g.Clip));
        Assert.All(clips, g => Assert.Equal(2.0, SymbolOf(g).Y));
        Assert.Equal(Math.Ceiling(Entry(legend).Y + 31), legend.Height);
    }

    /// <summary>
    ///     Validates the clip box with the columns of a horizontal legend (<c>columns: 2</c>): the width of an entry group is that of the widest entry of its column (the first column holds the
    ///     entries 0 and 2, the second the entry 1), the entries are laid out at (0, 0), (column width + 10, 0) and (0, 12).
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_ColumnsShareTheWidthOfTheWidestEntry()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "clipHeight": 10, "orient": "top", "columns": 2 }""");
        var clips = Clips(legend);
        var edges = RightEdges(legend);
        var firstColumn = Math.Max(edges[0], edges[2]);

        // Assert: the third entry is the widest of the first column (zzz), and the second column starts after it, rounded up, and the column padding of 10
        Assert.Equal([(0.0, 0.0), (Math.Ceiling(firstColumn) + 10, 0.0), (0.0, 12.0)], clips.Select(g => (g.X, g.Y)));
        Assert.Equal([firstColumn, edges[1], firstColumn], clips.Select(g => g.Clip!.Value.Width));
        Assert.True(edges[2] > edges[0]);
    }

    /// <summary>
    ///     Validates the clip with a large symbol size and a row padding: <c>symbolSize: 900</c> makes the symbol 32 wide (at x = 16, the label at 36) and <c>clipHeight: 20</c> with
    ///     <c>rowPadding: 5</c> gives the pitch of 25.
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_WithASymbolSizeAndRowPaddingMatchesVega()
    {
        // Arrange / Act
        var legend = Build(Symbols, """{ "clipHeight": 20, "rowPadding": 5, "symbolSize": 900 }""");
        var clips = Clips(legend);
        var width = RightEdges(legend).Max();

        // Assert
        Assert.Equal([0.0, 25.0, 50.0], clips.Select(g => g.Y));
        Assert.All(clips, g => Assert.Equal(new SceneClip(0, 0, width, 20), g.Clip));
        Assert.All(clips, g => Assert.Equal((16.0, 10.0), (SymbolOf(g).X, SymbolOf(g).Y)));
        Assert.All(Labels(legend), l => Assert.Equal(36.0, l.X));
        Assert.Equal(Math.Ceiling(Entry(legend).Y + 70), legend.Height);
    }

    /// <summary>
    ///     Validates that a gradient legend ignores <c>clipHeight</c> (Vega-Lite gives a gradient legend the value 20 and the gradient parser never reads it): the legend equals the one without the property.
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_AGradientLegendIgnoresIt()
    {
        // Arrange / Act
        var plain = Build(Gradient, "{}");
        var clipped = Build(Gradient, """{ "clipHeight": 50 }""");

        // Assert
        Assert.Equal((plain.Width, plain.Height), (clipped.Width, clipped.Height));
        Assert.Empty(Entry(clipped).Children.OfType<SceneGroup>());
    }

    /// <summary>
    ///     Validates that a <c>clipHeight</c> of zero is not truthy (Vega's <c>height ? ... : ...</c>) and draws the legend as without it, and that the <c>config.legend</c> value is read.
    /// </summary>
    [Fact]
    public void LegendBuilder_ClipHeight_ZeroIsIgnoredAndTheConfigurationApplies()
    {
        // Arrange / Act
        var zero = Build(Symbols, """{ "clipHeight": 0 }""");
        var plain = Build(Symbols, "{}");
        var configured = ChartTestHelpers.Legend(
            Symbols.Replace("LEGEND", "{}", StringComparison.Ordinal).Replace("\"mark\": \"point\",", "\"mark\": \"point\", \"config\": { \"legend\": { \"clipHeight\": 30 } },", StringComparison.Ordinal),
            0,
            300,
            200);

        // Assert
        Assert.Empty(Clips(zero));
        Assert.Equal((plain.Width, plain.Height), (zero.Width, zero.Height));
        Assert.Equal([0.0, 32.0, 64.0], Clips(configured).Select(g => g.Y));
    }
}
