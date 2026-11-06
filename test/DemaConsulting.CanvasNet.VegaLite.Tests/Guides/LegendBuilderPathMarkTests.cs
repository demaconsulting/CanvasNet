using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the legend symbols of the path, text and arc marks of <see cref="LegendBuilder"/> (Vega-Lite Phase 9): a line, a rule and a trail
///     have the <c>stroke</c> symbol (a short line), an area, an arc and a text have a circle, and the color of the legend is the paint of the symbol that the
///     mark uses (the stroke of an unfilled mark, the fill of a filled one). Every expectation is derived by hand from the defaults of Vega-Lite's legend
///     encoding, not recorded from the code.
/// </summary>
public class LegendBuilderPathMarkTests
{
    /// <summary>Three rows with the categories <c>a</c>, <c>b</c> and <c>c</c> in <c>g</c> and the numbers <c>q</c> (1, 2, 3) and <c>r</c> (10, 20, 30).</summary>
    private const string Rows = "[ { \"g\": \"a\", \"q\": 1, \"r\": 10 }, { \"g\": \"b\", \"q\": 2, \"r\": 20 }, { \"g\": \"c\", \"q\": 3, \"r\": 30 } ]";

    /// <summary>The nominal field <c>g</c> on the color channel.</summary>
    private const string Color = "\"color\": { \"field\": \"g\", \"type\": \"nominal\" }";

    /// <summary>The categorical colors of Vega's default scheme for the three categories.</summary>
    private static readonly string[] Colors = ["#4c78a8", "#f58518", "#e45756"];

    /// <summary>
    ///     Builds the specification of a mark over the position channels <c>q</c> and <c>r</c> and a color by the category.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <returns>The specification text.</returns>
    private static string Position(string mark) =>
        "{ \"data\": { \"values\": " + Rows + " }, \"mark\": " + mark + ", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"r\", \"type\": \"quantitative\" }, " + Color + " } }";

    /// <summary>
    ///     Builds the specification of a mark over the angle <c>q</c> and a color by the category.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="extra">More channels, each starting with a comma.</param>
    /// <returns>The specification text.</returns>
    private static string Polar(string mark, string extra = "") =>
        "{ \"data\": { \"values\": " + Rows + " }, \"mark\": " + mark + ", \"encoding\": { \"theta\": { \"field\": \"q\", \"type\": \"quantitative\" }, " + Color + extra + " } }";

    /// <summary>
    ///     Gets the symbols of the first legend of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The symbols in drawing order.</returns>
    private static List<SceneSymbol> Symbols(string json) =>
        [.. Assert.IsType<SceneGroup>(ChartTestHelpers.Legend(json).Children[0]).Children.OfType<SceneSymbol>()];

    /// <summary>
    ///     Validates that a line mark has a legend whose symbols are the <c>stroke</c> shape (a short line) in the color of each category: the color of an
    ///         unfilled mark is its stroke, so the symbol has the stroke and a transparent fill; the stroke width is the default of a legend symbol (1.5), a
    ///     stroke width that the mark gives wins, and a trail and a rule have the same shape.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LineMarkUsesAStrokeSymbolInTheMarkColor()
    {
        // Arrange / Act
        var symbols = Symbols(Position("\"line\""));

        // Assert: one stroke symbol for each category, painted by the color scale, with no fill of its own
        Assert.Equal(["stroke", "stroke", "stroke"], symbols.Select(s => s.Shape));
        Assert.Equal(Colors, symbols.Select(s => s.Stroke));
        // (the width is the default of a legend symbol, 1.5: Vega-Lite's legend does not read the width of Vega's line style)
        Assert.All(symbols, s => Assert.Equal(("transparent", 1.5), (s.Fill, s.StrokeWidth)));

        // Assert: a fixed stroke width of the mark is the width of the symbols
        var wide = Symbols(Position("{ \"type\": \"line\", \"strokeWidth\": 5 }"));
        Assert.All(wide, s => Assert.Equal(5.0, s.StrokeWidth));

        // Assert: the symbol type of the legend wins over the default shape of the mark
        var circles = Symbols(Position("\"line\"").Replace(Color, Color.Replace("}", ", \"legend\": { \"symbolType\": \"circle\" } }", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.All(circles, s => Assert.Equal("circle", s.Shape));

        // Assert: a rule and a trail (Vega-Lite draws a legend of a trail with the stroke even though the trail is filled) have the stroke shape in the color
        Assert.Equal(["stroke", "stroke", "stroke"], Symbols(Position("\"rule\"")).Select(s => s.Shape));
        var trail = Symbols(Position("\"trail\""));
        Assert.Equal(["stroke", "stroke", "stroke"], trail.Select(s => s.Shape));
        Assert.Equal(Colors, trail.Select(s => s.Stroke));
    }

    /// <summary>
    ///     Asserts that numbers equal the expected ones to six decimals.
    /// </summary>
    /// <param name="expected">The expected numbers.</param>
    /// <param name="actual">The actual numbers.</param>
    private static void AssertNumbers(double[] expected, IEnumerable<double?> actual)
    {
        var values = actual.ToArray();
        Assert.Equal(expected.Length, values.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], values[i]!.Value, 6);
        }
    }

    /// <summary>The numbers 1, 3, 6 and 9 of <c>s</c> over the positions <c>q</c> and <c>r</c>.</summary>
    private const string SizeRows = "[ { \"q\": 1, \"r\": 1, \"s\": 1 }, { \"q\": 2, \"r\": 3, \"s\": 3 }, { \"q\": 3, \"r\": 2, \"s\": 6 }, { \"q\": 4, \"r\": 4, \"s\": 9 } ]";

    /// <summary>
    ///     Builds the specification of a mark over <c>q</c> and <c>r</c> with the given channels (each starting with a comma) over <see cref="SizeRows"/>.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="channels">The channels after the positions.</param>
    /// <returns>The specification text.</returns>
    private static string Sized(string mark, string channels) =>
        "{ \"data\": { \"values\": " + SizeRows + " }, \"mark\": " + mark + ", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"r\", \"type\": \"quantitative\" }" + channels + " } }";

    /// <summary>
    ///     Validates that the size legend of a trail scales the stroke width of its symbols and keeps the symbol size at 100 (vega-lite@6.5.0
    ///     <c>compile/legend/parse.ts</c> <c>getLegendDefWithScale</c>: <c>mark === 'trail'</c> binds <c>size</c> to <c>strokeWidth</c>), so the symbols are
    ///     as wide as the trail is thick. The trail size range is [1, 4] over the domain [0, 9], so the entries 0, 2, 4, 6 and 8 have the widths
    ///     1 + 3 v / 9.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TrailSizeLegendScalesTheStrokeWidthNotTheSymbolSize()
    {
        // Arrange / Act
        var legend = ChartTestHelpers.Legend(Sized("\"trail\"", ", \"size\": { \"field\": \"s\", \"type\": \"quantitative\" }"));
        var entries = Assert.IsType<SceneGroup>(legend.Children[0]);
        var symbols = entries.Children.OfType<SceneSymbol>().ToList();

        // Assert: five stroke symbols of the fixed size 100 whose stroke widths follow the scale
        Assert.Equal(5, symbols.Count);
        Assert.All(symbols, s => Assert.Equal(("stroke", 100.0, "transparent", "#4c78a8"), (s.Shape, s.Size, s.Fill, s.Stroke)));
        AssertNumbers([1.0, 1 + 3 * 2 / 9.0, 1 + 3 * 4 / 9.0, 3.0, 1 + 3 * 8 / 9.0], symbols.Select(s => s.StrokeWidth));

        // Assert: the symbol column is ceil(sqrt(100) + 3.667) = 14 wide (Vega's sizeExpression), so the labels start at 14 + 4 = 18
        Assert.All(entries.Children.OfType<SceneText>().Where(t => t.Role == "legend-label"), t => Assert.Equal(18.0, t.X, 6));
    }

    /// <summary>
    ///     Validates that a nominal size of a trail gives the stroke widths of the ordinal size range (1, 2, 3, 4 for four categories) and that a
    ///     trail that has the same field in the color and the size gives one legend with the stroke from the color and the width from the size.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_TrailSizeLegendOfACategoryAndOfAMergedColorUseStrokeWidths()
    {
        // Arrange: four categories of g on the size channel
        const string rows = "[ { \"q\": 1, \"r\": 1, \"g\": \"a\" }, { \"q\": 2, \"r\": 3, \"g\": \"b\" }, { \"q\": 3, \"r\": 2, \"g\": \"c\" }, { \"q\": 4, \"r\": 4, \"g\": \"d\" } ]";
        var nominal = "{ \"data\": { \"values\": " + rows + " }, \"mark\": \"trail\", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"r\", \"type\": \"quantitative\" }, \"size\": { \"field\": \"g\", \"type\": \"nominal\" } } }";
        var merged = nominal.Replace("\"size\": { \"field\": \"g\", \"type\": \"nominal\" }", "\"size\": { \"field\": \"g\", \"type\": \"nominal\" }, \"color\": { \"field\": \"g\", \"type\": \"nominal\" }", StringComparison.Ordinal);

        // Act
        var plain = Symbols(nominal);
        var both = Symbols(merged);

        // Assert: the widths of the ordinal size range of four categories, the symbol size untouched
        AssertNumbers([1.0, 2.0, 3.0, 4.0], plain.Select(s => s.StrokeWidth));
        Assert.All(plain, s => Assert.Equal(100.0, s.Size));

        // Assert: one legend (one symbol for each category) has both the color of the stroke and the width
        Assert.Single(ChartTestHelpers.Legends(merged));
        Assert.Equal(["#4c78a8", "#f58518", "#e45756", "#72b7b2"], both.Select(s => s.Stroke));
        AssertNumbers([1.0, 2.0, 3.0, 4.0], both.Select(s => s.StrokeWidth));
        Assert.All(both, s => Assert.Equal(100.0, s.Size));

        // Assert: an explicit symbol size is the length of the symbols of a trail
        var explicitSize = Symbols(nominal.Replace("\"nominal\" } } }", "\"nominal\", \"legend\": { \"symbolSize\": 400 } } } }", StringComparison.Ordinal));
        Assert.All(explicitSize, s => Assert.Equal(400.0, s.Size));
    }

    /// <summary>
    ///     Validates that the size legend of a line is still the symbol size (the scale value is the size) with the default stroke width of 1.5: only a
    ///     trail binds the size to the stroke width.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LineSizeLegendScalesTheSymbolSize()
    {
        // Arrange / Act
        var symbols = Symbols(Sized("\"line\"", ", \"size\": { \"field\": \"s\", \"type\": \"quantitative\" }"));

        // Assert: the size range of a line is [1, 4] over the domain [0, 9]
        AssertNumbers([1.0, 1 + 3 * 2 / 9.0, 1 + 3 * 4 / 9.0, 3.0, 1 + 3 * 8 / 9.0], symbols.Select(s => (double?)s.Size));
        Assert.All(symbols, s => Assert.Equal(1.5, s.StrokeWidth));
    }

    /// <summary>
    ///     Validates that an area, an arc and a text mark have legends of filled circles in the color of each category: the mark is filled, so the
    ///     color is the fill of the symbols and the stroke is not set.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_AreaArcAndTextMarksUseFilledCircles()
    {
        // Arrange: the three marks with a color by the category (the area and the text over positions, the arc over an angle)
        var specs = new[]
        {
            Position("\"area\""),
            Polar("\"arc\""),
            Position("{ \"type\": \"text\", \"text\": \"x\" }"),
        };

        foreach (var json in specs)
        {
            // Act
            var symbols = Symbols(json);

            // Assert: circles of the default size of a legend symbol (100) with the category colors as the fill, no stroke
            Assert.Equal(["circle", "circle", "circle"], symbols.Select(s => s.Shape));
            Assert.Equal(Colors, symbols.Select(s => s.Fill));
            Assert.All(symbols, s => Assert.Equal(100.0, s.Size));
            Assert.All(symbols, s => Assert.True(s.Stroke is null || s.Stroke == "transparent", $"unexpected stroke {s.Stroke}"));
        }

        // Assert: a size channel on a text mark is a font size, and the legend shows it as the symbol size
        var sized = Symbols(Position("{ \"type\": \"text\", \"text\": \"x\" }").Replace(Color, Color + ", \"size\": { \"field\": \"g\", \"type\": \"nominal\" }", StringComparison.Ordinal));
        Assert.Equal(3, sized.Count);
        Assert.Equal(sized.Select(s => s.Size).Distinct().Count(), sized.Count);
    }
}
