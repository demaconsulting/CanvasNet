using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="LegendBuilder"/>: symbol, gradient and discrete gradient legends, their entries, labels, titles and geometry,
///     with every expectation derived by hand from Vega's legend rules and the measured width of the bundled font (not recorded from the code).
/// </summary>
public class LegendBuilderTests
{
    /// <summary>Three rows with the categories <c>a</c>, <c>b</c> and <c>c</c> in <c>g</c> and the numbers <c>q</c> (1, 2, 3) and <c>r</c> (10, 20, 30).</summary>
    private const string Rows3 = "[ { \"g\": \"a\", \"q\": 1, \"r\": 10 }, { \"g\": \"b\", \"q\": 2, \"r\": 20 }, { \"g\": \"c\", \"q\": 3, \"r\": 30 } ]";

    /// <summary>Four rows with the categories <c>a</c> to <c>d</c> in <c>g</c> and the numbers <c>q</c> (1 to 4) and <c>r</c> (10 to 40).</summary>
    private const string Rows4 = "[ { \"g\": \"a\", \"q\": 1, \"r\": 10 }, { \"g\": \"b\", \"q\": 2, \"r\": 20 }, { \"g\": \"c\", \"q\": 3, \"r\": 30 }, { \"g\": \"d\", \"q\": 4, \"r\": 40 } ]";

    /// <summary>The nominal field <c>g</c>.</summary>
    private const string Nominal = "\"field\": \"g\", \"type\": \"nominal\"";

    /// <summary>The quantitative field <c>q</c>.</summary>
    private const string Quantitative = "\"field\": \"q\", \"type\": \"quantitative\"";

    /// <summary>
    ///     Builds a specification of a mark with the encoding channels after x and y.
    /// </summary>
    /// <param name="channels">The JSON text of the channels (without the enclosing braces).</param>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="rows">The JSON text of the data values.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string channels, string mark = "\"point\"", string rows = Rows3) =>
        "{ \"data\": { \"values\": " + rows + " }, \"mark\": " + mark + ", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"r\", \"type\": \"quantitative\" }, " + channels + " } }";

    /// <summary>
    ///     Measures the width of one line of label text at 10 pixels, or of bold title text at 11 pixels.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="title">Whether the text is title text (11 pixels, bold).</param>
    /// <returns>The advance width.</returns>
    private static double Width(string text, bool title = false) =>
        new TextMeasurer().MeasureWidth(text, new TextFont("sans-serif", title ? 11 : 10, title ? "bold" : null));

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
    ///     Gets the symbols of a legend.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The symbols in drawing order.</returns>
    private static List<SceneSymbol> Symbols(LegendResult legend) => [.. Entry(legend).Children.OfType<SceneSymbol>()];

    /// <summary>
    ///     Gets the labels of a legend.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The labels in drawing order.</returns>
    private static List<SceneText> Labels(LegendResult legend) => [.. Entry(legend).Children.OfType<SceneText>()];

    /// <summary>
    ///     Rounds numbers to nine decimals.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The rounded numbers.</returns>
    private static List<double> R(IEnumerable<double> values) => [.. values.Select(v => Math.Round(v, 9))];

    /// <summary>
    ///     Describes the items of a legend as text, so that two legends can be compared.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>One line per item.</returns>
    private static string Describe(LegendResult legend)
    {
        var scene = new Scene(500, 500, null, new SceneGroup("root", 0, 0) { Children = [legend.CreateGroup(0, 0)] });
        return string.Join("\n", scene.Enumerate().Select(e => e.Item switch
        {
            SceneText t => $"{t.Role} {e.OffsetX},{e.OffsetY} {t.X},{t.Y} {t.Text} {t.Align} {t.Baseline}",
            SceneSymbol s => $"{s.Role} {e.OffsetX},{e.OffsetY} {s.X},{s.Y} {s.Shape} {s.Size} {s.Fill} {s.Stroke}",
            SceneRect r => $"{r.Role} {e.OffsetX},{e.OffsetY} {r.X},{r.Y},{r.Width},{r.Height} {r.Fill} {r.FillGradient?.Stops.Count}",
            _ => $"{e.Item.Role} {e.OffsetX},{e.OffsetY}",
        }));
    }

    /// <summary>
    ///     Validates a symbol legend of a nominal color on a point mark: stroked circles of the category colors in one column, the labels to the
    ///     right, the title above, and the rows spaced by Vega's grid layout.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_NominalColorOnAPointDrawsStrokedCirclesInOneColumn()
    {
        // Arrange / Act
        var legend = ChartTestHelpers.Legend(Spec("\"color\": { " + Nominal + " }"));
        var entry = Entry(legend);
        var title = Title(legend);
        var symbols = Symbols(legend);
        var labels = Labels(legend);

        // Assert: the title is bold, 11 pixels, at the padding (0), and the entries start below it: 11 pixels plus the title padding of 5
        Assert.Equal(("g", "left", "top", 11.0, "bold"), (title.Text, title.Align, title.Baseline, title.FontSize, title.FontWeight));
        Assert.Equal((0.0, 0.0), (title.X, title.Y));
        Assert.Equal((0.0, 16.0), (entry.X, entry.Y));

        // Assert: the size of an entry is max(ceil(sqrt(100) + 1.5), 10) = 12, so the symbol is centered at (6, 6) of a 12 pixel entry; the
        // symbol box reaches 5 + 2 * 1.5 = 8 around it (a stroked symbol is bounded with the miter allowance), so a row is 2 + 2 + 14 = 18 pixels apart
        Assert.Equal([6.0, 6.0, 6.0], symbols.Select(s => s.X));
        Assert.Equal([6.0, 24.0, 42.0], symbols.Select(s => s.Y));
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], symbols.Select(s => s.Stroke));
        Assert.All(symbols, s => Assert.Equal(("circle", 100.0, "transparent", 1.5, 0.7), (s.Shape, s.Size, s.Fill, s.StrokeWidth, s.Opacity)));

        // Assert: the labels are 12 + 4 pixels to the right of the entry, vertically centered on the symbols
        Assert.Equal(["a", "b", "c"], labels.Select(l => l.Text));
        Assert.Equal([16.0, 16.0, 16.0], labels.Select(l => l.X));
        Assert.Equal([6.0, 24.0, 42.0], labels.Select(l => l.Y));
        Assert.All(labels, l => Assert.Equal(("left", "middle", 160.0, 10.0), (l.Align, l.Baseline, l.Limit, l.FontSize)));

        // Assert: the box is as wide as the widest label ends (rounded up) and as tall as the last row (36 + 14) plus the title (16)
        Assert.Equal(Math.Ceiling(16 + Width("a")), legend.Width);
        Assert.Equal(66.0, legend.Height);
        Assert.Equal(("right", LegendKind.Symbol, 9), (legend.Orient, legend.Kind, legend.ItemCount));
    }

    /// <summary>
    ///     Validates a symbol legend of a quantitative size: the values are the ticks of the scale (five by default), the labels use the tick
    ///     format, and the sizes are the scaled values.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_QuantitativeSizeUsesTheTicksOfTheScale()
    {
        // Arrange: the size domain is 0 to 3 (the zero rule), and d3 ticks(0, 3, 5) has a step of 0.5
        var json = Spec("\"size\": { " + Quantitative + " }");
        var model = ChartTestHelpers.Parts(json).Scales["size"]!.CreateModel(0);

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var symbols = Symbols(legend);

        // Assert: seven entries with one decimal (the tick step is 0.5), whose sizes are the scaled values
        var ticks = new[] { 0, 0.5, 1, 1.5, 2, 2.5, 3 };
        Assert.Equal(["0.0", "0.5", "1.0", "1.5", "2.0", "2.5", "3.0"], Labels(legend).Select(l => l.Text));
        Assert.Equal(R(ticks.Select(v => model.Map(DataValue.FromNumber(v))!.Value)), R(symbols.Select(s => s.Size)));

        // Assert: the mark is an unfilled point, so the symbols keep its blue stroke and its transparent fill
        Assert.All(symbols, s => Assert.Equal(("#4c78a8", "transparent", "circle"), (s.Stroke, s.Fill, s.Shape)));
        Assert.Equal("q", Title(legend).Text);
    }

    /// <summary>
    ///     Validates the symbol limit: more entries than <c>symbolLimit</c> keep the first ones and end with an entry that counts the rest.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_SymbolLimitAddsAnEllipsisEntry()
    {
        // Arrange: four categories with a limit of three
        var json = Spec("\"color\": { " + Nominal + ", \"legend\": { \"symbolLimit\": 3 } }", rows: Rows4);

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var symbols = Symbols(legend);

        // Assert: limit - 1 = 2 entries, then "…2 entries" (four values minus the two shown) for the third value (c, the third color)
        Assert.Equal(["a", "b", "\u20262 entries"], Labels(legend).Select(l => l.Text));
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], symbols.Select(s => s.Stroke));
    }

    /// <summary>
    ///     Validates the range labels of a discretizing scale: <c>&lt; hi</c> for the first class, <c>lo – hi</c> between, <c>≥ lo</c> for the last.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_DiscretizingScaleLabelsUseRanges()
    {
        // Arrange: a quantize color scale over 0 to 90 with three classes has the thresholds 30 and 60; the symbol kind is forced
        var json = Spec("\"color\": { " + Quantitative + ", \"scale\": { \"type\": \"quantize\", \"domain\": [0, 90], \"range\": [\"#ff0000\", \"#00ff00\", \"#0000ff\"] }, \"legend\": { \"type\": \"symbol\" } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);

        // Assert: the entries are minus infinity and the thresholds, and each symbol has the color of its class
        Assert.Equal(["< 30", "30 \u2013 60", "\u2265 60"], Labels(legend).Select(l => l.Text));
        Assert.Equal(["#ff0000", "#00ff00", "#0000ff"], Symbols(legend).Select(s => s.Stroke));
    }

    /// <summary>
    ///     Validates that channels that share a field draw one legend whose symbols combine the shape, the color and the size of the channels.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_MergedChannelsCombineTheirProperties()
    {
        // Arrange: shape, color and size of the same two categories
        const string rows = "[ { \"g\": \"a\", \"q\": 1, \"r\": 10 }, { \"g\": \"b\", \"q\": 2, \"r\": 20 } ]";
        var json = Spec("\"shape\": { " + Nominal + " }, \"color\": { " + Nominal + " }, \"size\": { " + Nominal + " }", rows: rows);
        var sizes = ChartTestHelpers.Parts(json).Scales["size"]!.CreateModel(0);

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var symbols = Symbols(legend);

        // Assert: the first two entries of the symbol range, the first two colors and the sizes of the point scale
        Assert.Equal(["circle", "square"], symbols.Select(s => s.Shape));
        Assert.Equal(["#4c78a8", "#f58518"], symbols.Select(s => s.Stroke));
        Assert.Equal(R([sizes.Map(DataValue.FromString("a"))!.Value, sizes.Map(DataValue.FromString("b"))!.Value]), R(symbols.Select(s => s.Size)));
        Assert.Equal("g", Title(legend).Text);
    }

    /// <summary>
    ///     Validates the base paints of a legend: a size legend of a filled circle shows the mark's fill with the base stroke, and a size
    ///     legend next to a color field shows the base fill and stroke instead of the mark's paints.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_FixedAndBasePaintsComeFromTheMarkAndTheConfiguration()
    {
        // Arrange
        var size = "\"size\": { " + Quantitative + " }";

        // Act
        var filled = Symbols(ChartTestHelpers.Legend(Spec(size, "{ \"type\": \"circle\", \"color\": \"red\" }")));
        var driven = Symbols(ChartTestHelpers.Legend(Spec(size + ", \"color\": { " + Nominal + " }"), index: 1));
        var configured = Symbols(ChartTestHelpers.Legend(Spec(size), 0, 300, 300, "{ \"legend\": { \"symbolStrokeColor\": \"green\", \"symbolSize\": 200 } }"));
        var explicitSymbols = Symbols(ChartTestHelpers.Legend(Spec(size + ", \"opacity\": { \"value\": 0.25 }", "\"circle\""), 0, 300, 300, null));

        // Assert: a filled circle shows its color and the grey base stroke (the legend has no fill scale)
        Assert.All(filled, s => Assert.Equal(("red", "#888"), (s.Fill, s.Stroke)));

        // Assert: when a color field drives the stroke, the size legend shows the base paints
        Assert.All(driven, s => Assert.Equal(("transparent", "#888"), (s.Fill, s.Stroke)));

        // Assert: a configured symbolStrokeColor replaces the mark's stroke
        Assert.All(configured, s => Assert.Equal("green", s.Stroke));

        // Assert: the opacity channel's constant value is the opacity of the symbols
        Assert.All(explicitSymbols, s => Assert.Equal(0.25, s.Opacity));
    }

    /// <summary>
    ///     Validates the opacity, stroke width and stroke dash legends: the entries show the scaled value of each channel.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_OpacityStrokeWidthAndDashChannelsShowTheirValues()
    {
        // Arrange
        var opacity = Spec("\"opacity\": { " + Quantitative + " }");
        var width = Spec("\"strokeWidth\": { " + Quantitative + " }");
        var dash = Spec("\"strokeDash\": { " + Nominal + " }");
        var opacityModel = ChartTestHelpers.Parts(opacity).Scales["opacity"]!.CreateModel(0);
        var widthModel = ChartTestHelpers.Parts(width).Scales["strokeWidth"]!.CreateModel(0);
        var dashScale = ChartTestHelpers.Parts(dash).Scales["strokeDash"]!;

        // Act
        var opacities = Symbols(ChartTestHelpers.Legend(opacity));
        var widths = Symbols(ChartTestHelpers.Legend(width));
        var dashes = Symbols(ChartTestHelpers.Legend(dash));

        // Assert: the ticks of each scale's domain (1 to 3 for both: neither channel has a zero rule), scaled by each channel
        var opacityTicks = new[] { 1, 1.5, 2, 2.5, 3 };
        var widthTicks = opacityTicks;
        Assert.Equal(R(opacityTicks.Select(t => opacityModel.Map(DataValue.FromNumber(t))!.Value)), R(opacities.Select(s => s.Opacity)));
        Assert.Equal(R(widthTicks.Select(t => widthModel.Map(DataValue.FromNumber(t))!.Value)), R(widths.Select(s => s.StrokeWidth!.Value)));

        // Assert: one dash pattern per category, the first patterns of the scale
        Assert.Equal(3, dashes.Count);
        Assert.Equal(dashScale.DashRange.Take(3).Select(d => string.Join(",", d)), dashes.Select(s => string.Join(",", s.StrokeDash!)));
    }

    /// <summary>
    ///     Validates the grid of a symbol legend with columns: a vertical legend fills each column from top to bottom, columns start after the
    ///     widest label of the previous one plus the column padding, and rows are spaced as in a single column.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_ColumnsFillTopToBottomWithColumnPadding()
    {
        // Arrange: four entries in two columns, so the first column holds a and b and the second c and d
        var json = Spec("\"color\": { " + Nominal + ", \"legend\": { \"columns\": 2 } }", rows: Rows4);

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var symbols = Symbols(legend);
        var labels = Labels(legend);

        // Assert: the second column starts at 10 (column padding) + 2 (the symbol reaches 2 pixels left of its entry) + the widest box of
        // the first column (the labels end at 16 + 5.56, rounded up to 22): 34 pixels
        var second = 10 + 2 + Math.Ceiling(16 + Width("a"));
        Assert.Equal(["a", "c", "b", "d"], labels.Select(l => l.Text));
        Assert.Equal([6.0, 6.0 + second, 6.0, 6.0 + second], symbols.Select(s => s.X));
        Assert.Equal([6.0, 6.0, 24.0, 24.0], symbols.Select(s => s.Y));
    }

    /// <summary>
    ///     Validates a horizontal symbol legend: the entries form one row, and each symbol is centered on its own size.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_HorizontalDirectionLaysTheEntriesOutInOneRow()
    {
        // Arrange
        var json = Spec("\"color\": { " + Nominal + ", \"legend\": { \"orient\": \"top\" } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var symbols = Symbols(legend);

        // Assert: all entries are 12 pixels, a column starts after 10 + 2 + the extent of the previous one (22)
        var step = 10 + 2 + Math.Ceiling(16 + Width("a"));
        Assert.Equal([6.0, 6.0 + step, 6.0 + (2 * step)], symbols.Select(s => s.X));
        Assert.Equal([6.0, 6.0, 6.0], symbols.Select(s => s.Y));
        Assert.Equal("top", legend.Orient);
    }

    /// <summary>
    ///     Validates the explicit values, the label format and the entry properties of a symbol legend.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_ValuesFormatAndSymbolPropertiesApply()
    {
        // Arrange: the explicit values, a percent format, a square symbol of size 200, no padding between the symbol and the label
        var json = Spec("\"size\": { " + Quantitative + ", \"legend\": { \"values\": [1, 2], \"format\": \".0%\", \"symbolType\": \"square\", \"symbolSize\": 200, \"symbolOffset\": 3, \"labelOffset\": 1, \"title\": null } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var symbols = Symbols(legend);
        var labels = Labels(legend);

        // Assert: the labels are the formatted values; the symbol size comes from the size scale and the shape from symbolType
        Assert.Equal(["100%", "200%"], labels.Select(l => l.Text));
        Assert.All(symbols, s => Assert.Equal("square", s.Shape));

        // Assert: without a title the entries sit at the padding; the first symbol is at (offset / 2) + 3, its label at offset + 3 + 1, where
        // the offset is the largest entry size
        var sizes = symbols.Select(s => Math.Max(Math.Ceiling(Math.Sqrt(s.Size) + 1.5), 10)).ToList();
        var offset = sizes.Max();
        Assert.Single(legend.Children);
        Assert.Equal((offset * 0.5) + 3, symbols[0].X);
        Assert.Equal(offset + 3 + 1, labels[0].X);
    }

    /// <summary>
    ///     Validates the gradient legend of a continuous color scale: a 16 by 200 bar filled with the ramp (21 stops, the ends and every 0.1 of
    ///     the domain 1 to 3), labels at the ticks to its right, and the title above.
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_ContinuousColorDrawsAVerticalBarWithTickLabels()
    {
        // Arrange: the domain of the color is 1 to 3 (no zero rule for color); the default ramp is blues
        var json = Spec("\"color\": { " + Quantitative + " }");

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var entry = Entry(legend);
        var bar = Assert.IsType<SceneRect>(entry.Children[0]);
        var labels = Labels(legend);

        // Assert: the bar is the thickness wide and the length (the view height, between 64 and 200) tall; its gradient runs from the bottom up
        Assert.Equal(("legend-gradient", 0.0, 0.0, 16.0, 200.0), (bar.Role, bar.X, bar.Y, bar.Width, bar.Height));
        var gradient = bar.FillGradient!;
        Assert.Equal((0.0, 1.0, 0.0, 0.0), (gradient.X1, gradient.Y1, gradient.X2, gradient.Y2));

        // Assert: Vega's scaleGradient: the ticks of the domain for a count of 15 (a step of 0.1: 21 stops), first and last the ends of the ramp
        Assert.Equal(21, gradient.Stops.Count);
        Assert.Equal(R(Enumerable.Range(0, 21).Select(i => i / 20.0)), R(gradient.Stops.Select(s => s.Offset)));
        Assert.Equal(("#cfe1f2", "#0a4a90"), (gradient.Stops[0].Color, gradient.Stops[20].Color));
        Assert.Null(bar.Fill);
        Assert.Equal(0.0, bar.StrokeWidth);

        // Assert: four ticks are asked for (max(2, 2 * floor(200 / 100))), so d3 gives a step of 0.5: five labels, 18 pixels from the bar's start
        Assert.Equal(["1.0", "1.5", "2.0", "2.5", "3.0"], labels.Select(l => l.Text));
        Assert.All(labels, l => Assert.Equal(18.0, l.X));
        Assert.Equal([200.0, 150.0, 100.0, 50.0, 0.0], labels.Select(l => l.Y));
        Assert.Equal(["bottom", "middle", "middle", "middle", "top"], labels.Select(l => l.Baseline));
        Assert.All(labels, l => Assert.Equal("left", l.Align));

        // Assert: the box is as wide as the labels end (18 + the widest label), as tall as the bar plus the title (11 + 5)
        Assert.Equal(Math.Ceiling(18 + Width("1.0")), legend.Width);
        Assert.Equal(216.0, legend.Height);
        Assert.Equal(LegendKind.Gradient, legend.Kind);
    }

    /// <summary>
    ///     Validates that the gradient length follows the data rectangle: the height between 64 and 200 for a vertical bar, and the width
    ///     between 100 and 200 for a horizontal bar of a top legend, whose labels sit below it.
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_LengthFollowsTheDataRectangleAndDirection()
    {
        // Arrange
        var json = Spec("\"color\": { " + Quantitative + " }");
        var top = Spec("\"color\": { " + Quantitative + ", \"legend\": { \"orient\": \"top\" } }");

        // Act
        var small = Assert.IsType<SceneRect>(Entry(ChartTestHelpers.Legend(json, height: 40)).Children[0]);
        var medium = Assert.IsType<SceneRect>(Entry(ChartTestHelpers.Legend(json, height: 120)).Children[0]);
        var horizontal = ChartTestHelpers.Legend(top, width: 150);
        var bar = Assert.IsType<SceneRect>(Entry(horizontal).Children[0]);
        var labels = Labels(horizontal);

        // Assert: a short view still gets a 64 pixel bar; a medium one the view height
        Assert.Equal(64.0, small.Height);
        Assert.Equal(120.0, medium.Height);

        // Assert: the horizontal bar is 150 by 16 and runs left to right; with max(2, 2 * floor(150 / 100)) = 2 ticks d3 gives a step of 1
        Assert.Equal((150.0, 16.0), (bar.Width, bar.Height));
        Assert.Equal((0.0, 0.0, 1.0, 0.0), (bar.FillGradient!.X1, bar.FillGradient.Y1, bar.FillGradient.X2, bar.FillGradient.Y2));
        Assert.Equal(["1", "2", "3"], labels.Select(l => l.Text));
        Assert.Equal([0.0, 75.0, 150.0], labels.Select(l => l.X));
        Assert.All(labels, l => Assert.Equal(18.0, l.Y));
        Assert.All(labels, l => Assert.Equal("top", l.Baseline));
        Assert.Equal(["left", "center", "right"], labels.Select(l => l.Align));
    }

    /// <summary>
    ///     Validates that a gradient legend with fewer than three tick labels labels the two ends of the domain instead.
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_FewerThanThreeTicksUseTheEndsOfTheDomain()
    {
        // Arrange: the domain 1 to 2.9 over a 100 pixel view asks for two ticks, which d3 gives as 1 and 2 only
        const string rows = "[ { \"g\": \"a\", \"q\": 1, \"r\": 10 }, { \"g\": \"b\", \"q\": 2.9, \"r\": 20 } ]";

        // Act
        var legend = ChartTestHelpers.Legend(Spec("\"color\": { " + Quantitative + " }", rows: rows), height: 100);
        var labels = Labels(legend);

        // Assert: labels for both ends, formatted with the step 1 of the two ticks: 1 and 3 (2.9 rounds to 3)
        Assert.Equal(["1", "3"], labels.Select(l => l.Text));
        Assert.Equal([100.0, 0.0], labels.Select(l => l.Y));
    }

    /// <summary>
    ///     Validates the explicit gradient properties: length, thickness, tick count, label offset and limit, and the stroke of the bar.
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_ExplicitLengthThicknessTicksAndLabelProperties()
    {
        // Arrange
        var json = Spec("\"color\": { " + Quantitative + ", \"legend\": { \"gradientLength\": 120, \"gradientThickness\": 10, \"tickCount\": 2, \"labelOffset\": 5, \"labelLimit\": 8, \"gradientStrokeWidth\": 2, \"gradientStrokeColor\": \"black\" } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var bar = Assert.IsType<SceneRect>(Entry(legend).Children[0]);
        var labels = Labels(legend);

        // Assert
        Assert.Equal((10.0, 120.0, "black", 2.0), (bar.Width, bar.Height, bar.Stroke, bar.StrokeWidth));
        Assert.Equal(["1", "2", "3"], labels.Select(l => l.Text));
        Assert.All(labels, l => Assert.Equal((15.0, 8.0), (l.X, l.Limit)));
    }

    /// <summary>
    ///     Validates the gradient of a domain without span (one value): the ramp from 0 to 1 and a single label in the middle.
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_DomainWithoutSpanUsesTheUnitRamp()
    {
        // Arrange: every row has the same number
        const string rows = "[ { \"g\": \"a\", \"q\": 5, \"r\": 10 }, { \"g\": \"b\", \"q\": 5, \"r\": 20 } ]";

        // Act
        var legend = ChartTestHelpers.Legend(Spec("\"color\": { " + Quantitative + " }", rows: rows));
        var bar = Assert.IsType<SceneRect>(Entry(legend).Children[0]);

        // Assert: Vega replaces the domain by 0 to 1: d3 ticks(0, 1, 15) has a step of 0.05 (21 stops), the whole ramp; the single tick is the middle of the bar
        Assert.Equal(21, bar.FillGradient!.Stops.Count);
        Assert.Equal(("#cfe1f2", "#0a4a90"), (bar.FillGradient.Stops[0].Color, bar.FillGradient.Stops[20].Color));
        Assert.Single(Labels(legend));
        Assert.Equal([100.0], Labels(legend).Select(l => l.Y));
    }

    /// <summary>
    ///     Validates the discrete gradient legend of a quantize color scale: one band per class, the labels at the class boundaries, none for
    ///     the first class.
    /// </summary>
    [Fact]
    public void LegendBuilder_Discrete_QuantizeColorDrawsOneBandPerClass()
    {
        // Arrange: the domain 0 to 90 in three classes of 30
        var json = Spec("\"color\": { " + Quantitative + ", \"scale\": { \"type\": \"quantize\", \"domain\": [0, 90], \"range\": [\"#ff0000\", \"#00ff00\", \"#0000ff\"] } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var entry = Entry(legend);
        var bands = entry.Children.OfType<SceneRect>().ToList();
        var labels = Labels(legend);

        // Assert: bands from the top (the last class) down; each is 16 wide and a third of the 200 pixel bar tall
        Assert.Equal(LegendKind.DiscreteGradient, legend.Kind);
        Assert.Equal(["#ff0000", "#00ff00", "#0000ff"], bands.Select(b => b.Fill));
        Assert.Equal(R([200.0 * 2 / 3, 200.0 / 3, 0]), R(bands.Select(b => b.Y)));
        Assert.All(bands, b => Assert.Equal((0.0, 16.0), (b.X, b.Width)));
        Assert.Equal(R([200.0 / 3, 200.0 / 3, 200.0 / 3]), R(bands.Select(b => b.Height)));

        // Assert: labels at the two boundaries, 30 and 60, with their baseline in the middle
        Assert.Equal(["30", "60"], labels.Select(l => l.Text));
        Assert.Equal(R([200.0 * 2 / 3, 200.0 / 3]), R(labels.Select(l => l.Y)));
        Assert.All(labels, l => Assert.Equal(("middle", 18.0), (l.Baseline, l.X)));
    }

    /// <summary>
    ///     Validates the discrete gradient of a threshold scale: the scale is widened by one class at both ends.
    /// </summary>
    [Fact]
    public void LegendBuilder_Discrete_ThresholdScaleIsWidenedByOneClassAtEachEnd()
    {
        // Arrange: the thresholds 10 and 30 give three classes
        var json = Spec("\"color\": { " + Quantitative + ", \"scale\": { \"type\": \"threshold\", \"domain\": [10, 30], \"range\": [\"#ff0000\", \"#00ff00\", \"#0000ff\"] }, \"legend\": { \"gradientLength\": 100 } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var bands = Entry(legend).Children.OfType<SceneRect>().ToList();
        var labels = Labels(legend);

        // Assert: the domain 10 to 30 grows by 20 / 1 on both sides, to -10 to 50: the thresholds are at 1/3 and 2/3 of the 100 pixel bar
        Assert.Equal(R([100.0 * 2 / 3, 100.0 / 3, 0]), R(bands.Select(b => b.Y)));
        Assert.Equal(["10", "30"], labels.Select(l => l.Text));
        Assert.Equal(R([100.0 * 2 / 3, 100.0 / 3]), R(labels.Select(l => l.Y)));
    }

    /// <summary>
    ///     Validates the title rules of the box: the padding around the content, a title centered over the entries and pushed in when it
    ///     is wider than the entries, and the box paint.
    /// </summary>
    [Fact]
    public void LegendBuilder_Title_PaddingAnchorAndBoxPaint()
    {
        // Arrange: a padding of 4, a centered title that is wider than the entries, and a filled, stroked, rounded box
        var json = Spec(
            "\"color\": { " + Nominal + ", \"legend\": { \"title\": \"A long legend title\", \"titleAnchor\": \"middle\", \"padding\": 4, \"fillColor\": \"#eee\", \"strokeColor\": \"#999\", \"cornerRadius\": 3 } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var entry = Entry(legend);
        var title = Title(legend);

        // Assert: the title is centered on the entries' width (the labels end at 16 + 5.56): its center is round(0.5 * that) from the padding
        var entryWidth = 16 + Width("a");
        var center = 4 + Math.Floor((0.5 * entryWidth) + 0.5);
        Assert.Equal("center", title.Align);

        // Assert: the title is wider than the entries, so its left edge (center - half its width) would pass the padding, and all is pushed right
        var half = Width("A long legend title", title: true) / 2;
        var overhang = Math.Floor(((center - half) - 4) + 0.5);
        Assert.True(overhang < 0);
        Assert.Equal(center - overhang, title.X);
        Assert.Equal(4 - overhang, entry.X);
        Assert.Equal(4.0, title.Y);
        Assert.Equal(4 + 11 + 5, entry.Y);

        // Assert: the box paint and the size of the box: the padding is part of it on all sides
        Assert.Equal(("#eee", "#999", 3.0), (legend.Frame.Fill, legend.Frame.Stroke, legend.Frame.CornerRadius));
        var group = legend.CreateGroup(10, 20);
        Assert.Equal(("legend", 10.0, 20.0, "#eee", "#999"), (group.Role, group.X, group.Y, group.Fill, group.Stroke));
        Assert.Equal((legend.Width, legend.Height), (group.Width, group.Height));
        Assert.Equal(new SceneBounds(-0.5, -0.5, legend.Width + 0.5, legend.Height + 0.5), legend.Bounds);
    }

    /// <summary>
    ///     Validates that a legend without a title and without padding has its entries at the origin.
    /// </summary>
    [Fact]
    public void LegendBuilder_Title_NullTitleLeavesTheEntriesAtThePadding()
    {
        // Arrange
        var json = Spec("\"color\": { " + Nominal + ", \"legend\": { \"title\": null } }");

        // Act
        var legend = ChartTestHelpers.Legend(json);

        // Assert: one child (the entries), at the origin, and the box is the entries alone (3 rows: 36 + 14)
        Assert.Single(legend.Children);
        Assert.Equal((0.0, 0.0), (Entry(legend).X, Entry(legend).Y));
        Assert.Equal(50.0, legend.Height);
        Assert.Equal(8, legend.ItemCount);
    }

    /// <summary>
    ///     Validates the placement facts a legend reports: the orient, the offset, and the position of a legend with the orient none.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_ReportsOrientOffsetAndPositionForTheLayout()
    {
        // Arrange
        var none = Spec("\"color\": { " + Nominal + ", \"legend\": { \"orient\": \"none\", \"legendX\": 12, \"legendY\": 7, \"offset\": 30 } }");

        // Act
        var legend = ChartTestHelpers.Legend(none);

        // Assert
        Assert.Equal(("none", 30.0, 12.0, 7.0), (legend.Orient, legend.Offset, legend.X, legend.Y));
        Assert.Null(ChartTestHelpers.Legend(Spec("\"color\": { " + Nominal + " }")).Offset);
    }

    /// <summary>
    ///     Validates the limits: the number of explicit values, the ticks of a continuous scale, the length of a label and the scene items are
    ///     bounded and fail with a located error that names the limit.
    /// </summary>
    [Fact]
    public void LegendBuilder_Limits_AreEnforcedBeforeAnythingIsCreated()
    {
        // Arrange
        var values = Spec("\"color\": { " + Nominal + ", \"legend\": { \"values\": [\"a\", \"b\", \"c\", \"d\"] } }");
        var ticks = Spec("\"size\": { " + Quantitative + " }");
        var longText = Spec("\"color\": { " + Nominal + ", \"legend\": { \"values\": [1.2345678901234e21] } }");
        var items = Spec("\"color\": { " + Nominal + " }");

        // Act
        var tooManyValues = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Legend(values, limits: new VegaLiteLimits { MaxScaleDomainSize = 3 }));
        var tooManyTicks = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Legend(ticks, limits: new VegaLiteLimits { MaxTicks = 3 }));
        var tooLong = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Legend(longText, limits: new VegaLiteLimits { MaxStringLength = 12 }));
        var tooManyItems = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Legend(items, limits: new VegaLiteLimits { MaxSceneItems = 8 }, budget: new SceneBudget(8)));
        var budget = new SceneBudget(9);
        var exact = ChartTestHelpers.Legend(items, budget: budget);

        // Assert: each failure names its limit; the values failure points at the property
        Assert.Contains("the limit of 3", tooManyValues.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/color/legend/values", tooManyValues.Location.Pointer);
        Assert.Contains("MaxTicks", tooManyTicks.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/size", tooManyTicks.Location.Pointer);
        Assert.Contains("the limit of 12", tooLong.Message, StringComparison.Ordinal);
        Assert.Contains("MaxSceneItems", tooManyItems.Message, StringComparison.Ordinal);

        // Assert: nine items are charged exactly: the legend, the title, the entry group, and a symbol and a label for each of three entries
        Assert.Equal(9, budget.Count);
        Assert.Equal(9, exact.ItemCount);
    }

    /// <summary>
    ///     Validates that the build polls the cancellation token and that an invalid format is a located specification error.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_CancellationAndInvalidFormat()
    {
        // Arrange
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var bad = Spec("\"size\": { " + Quantitative + ", \"legend\": { \"format\": \"~~\" } }");

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => ChartTestHelpers.Legend(Spec("\"color\": { " + Nominal + " }"), cancellationToken: cancelled.Token));
        var error = Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Legend(bad));
        Assert.Equal("/encoding/size/legend/format", error.Location.Pointer);
    }

    /// <summary>
    ///     Validates that building the same legend twice gives the same items.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_IsDeterministic()
    {
        // Arrange
        var json = Spec("\"color\": { " + Quantitative + " }, \"size\": { " + Quantitative + " }");

        // Act
        var first = Describe(ChartTestHelpers.Legend(json));
        var second = Describe(ChartTestHelpers.Legend(json));

        // Assert
        Assert.Equal(first, second);
        Assert.Contains("legend-symbol", first, StringComparison.Ordinal);
    }

    /// <summary>Four rows with the numbers <c>q</c> (1, 10, 100, 1000), three decades, and <c>r</c> (10 to 40).</summary>
    private const string RowsDecades = "[ { \"g\": \"a\", \"q\": 1, \"r\": 10 }, { \"g\": \"b\", \"q\": 10, \"r\": 20 }, { \"g\": \"c\", \"q\": 100, \"r\": 30 }, { \"g\": \"d\", \"q\": 1000, \"r\": 40 } ]";

    /// <summary>
    ///     Validates the gradient legend of a log color scale: the labels are the ticks that Vega labels (the powers: with a count of 4 for 28
    ///     log ticks the rule <c>k = 10 * 4 / 28 = 1.43</c> keeps the digit 1 only), the variable-precision format, the positions along the
    ///     logarithm (a third of the bar per decade, derived by hand), and the stops of the bar at the log ticks with the color of the scale there.
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_LogScaleLabelsThePowersAlongTheLogarithm()
    {
        // Arrange: the log color domain is 1 to 1000 (color has no nice and no zero)
        var json = Spec("\"color\": { " + Quantitative + ", \"scale\": { \"type\": \"log\" } }", rows: RowsDecades);
        var model = ChartTestHelpers.Parts(json).Scales["color"]!.CreateModel(0);

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var bar = Assert.IsType<SceneRect>(Entry(legend).Children[0]);
        var labels = Labels(legend);

        // Assert: the labels are the powers, a third of the 200 pixel bar apart (the maximum is at the top), the ends aligned inward
        Assert.Equal(LegendKind.Gradient, legend.Kind);
        Assert.Equal(["1", "10", "100", "1,000"], labels.Select(l => l.Text));
        Assert.Equal(R([200.0, 200 * (1 - (1.0 / 3)), 200 * (1 - (2.0 / 3)), 0.0]), R(labels.Select(l => l.Y)));
        Assert.Equal(["bottom", "middle", "middle", "top"], labels.Select(l => l.Baseline));

        // Assert: the bar has a stop at each of the 28 log ticks (1..9, 10..90, 100..900, 1000), placed along the logarithm; the stop of 10 is a third of
        // the way and has the color the scale gives to 10, the stop of 2 is ln 2 / ln 1000 of the way
        var stops = bar.FillGradient!.Stops;
        Assert.Equal(28, stops.Count);
        Assert.Equal((0.0, 1.0), (stops[0].Offset, stops[27].Offset));
        Assert.Equal(Math.Round(Math.Log(2) / Math.Log(1000), 9), Math.Round(stops[1].Offset, 9));
        Assert.Equal(Math.Round(1.0 / 3, 9), Math.Round(stops[9].Offset, 9));
        Assert.Equal(model.MapColor(10)!.Value.ToCss(), stops[9].Color);
        Assert.Equal(model.MapColor(1000)!.Value.ToCss(), stops[27].Color);
    }

    /// <summary>
    ///     Validates the symbol legend of a log size scale: the entries are the labeled log ticks (the powers 1 to 1000), all labeled, and the
    ///     sizes grow by the same amount per decade because the scale is linear in the logarithm.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_LogSizeShowsThePowersWithEvenSizeSteps()
    {
        // Arrange
        var json = Spec("\"size\": { " + Quantitative + ", \"scale\": { \"type\": \"log\" } }", rows: RowsDecades);
        var model = ChartTestHelpers.Parts(json).Scales["size"]!.CreateModel(0);

        // Act
        var legend = ChartTestHelpers.Legend(json);
        var sizes = Symbols(legend).Select(s => s.Size).ToList();

        // Assert: four entries (count 5 over 28 ticks: k = 10 * 5 / 28 = 1.79, only the digit 1), the sizes are the scaled values
        Assert.Equal(["1", "10", "100", "1,000"], Labels(legend).Select(l => l.Text));
        Assert.Equal(R(new[] { 1.0, 10, 100, 1000 }.Select(v => model.Map(DataValue.FromNumber(v))!.Value)), R(sizes));

        // Assert: a decade is the same step of size anywhere on the scale
        Assert.Equal(Math.Round(sizes[1] - sizes[0], 6), Math.Round(sizes[2] - sizes[1], 6));
        Assert.Equal(Math.Round(sizes[2] - sizes[1], 6), Math.Round(sizes[3] - sizes[2], 6));
        Assert.True(sizes[1] > sizes[0]);
    }

    /// <summary>
    ///     Validates the gradient legend of a square root color scale: the ticks are the ordinary ticks of the domain, and the labels sit at the
    ///     position along the square root (the stop of 4 in 1..4 is at <c>(sqrt 4 - 1) / (2 - 1)</c> of the way).
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_SqrtScalePlacesTheLabelsAlongTheRoot()
    {
        // Arrange / Act: the domain of q is 1 to 3 with the rows of Rows4 extended to 4: ticks 1, 2, 3, 4 (a count of 4 over 3 gives the step 1)
        var legend = ChartTestHelpers.Legend(Spec("\"color\": { " + Quantitative + ", \"scale\": { \"type\": \"sqrt\" } }", rows: Rows4));
        var labels = Labels(legend);

        // Assert: along the root of 1..4 (T = 1..2): 2 is 0.4142, 3 is 0.7321, so the labels are at (1 - fraction) of 200
        Assert.Equal(["1", "2", "3", "4"], labels.Select(l => l.Text));
        Assert.Equal(
            R([200.0, 200 * (1 - (Math.Sqrt(2) - 1)), 200 * (1 - (Math.Sqrt(3) - 1)), 0.0]),
            R(labels.Select(l => l.Y)));
    }

    /// <summary>
    ///     Validates the limits and the cancellation of a log legend: the log ticks are bounded by <see cref="VegaLiteLimits.MaxTicks"/> with
    ///     a located error that names the limit, and a canceled token stops the build.
    /// </summary>
    [Fact]
    public void LegendBuilder_Gradient_LogTicksAreLimitedAndCancellable()
    {
        // Arrange
        var json = Spec("\"color\": { " + Quantitative + ", \"scale\": { \"type\": \"log\" } }", rows: RowsDecades);

        // Act: 28 ticks for the stops of the bar against a limit of 5
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Legend(json, limits: new VegaLiteLimits { MaxTicks = 5 }));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // Assert
        Assert.Contains("the limit of 5", tooMany.Message, StringComparison.Ordinal);
        Assert.True(tooMany.Location.IsKnown);
        Assert.Throws<OperationCanceledException>(() => ChartTestHelpers.Legend(json, cancellationToken: cancelled.Token));
    }
}
