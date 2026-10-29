using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of <see cref="MarkBuilder"/>: the geometry and the paint of the scene items of each supported mark, with
///     the expectations derived by hand from the band, point and linear scale formulas and the Vega-Lite mark rules.
/// </summary>
public class MarkBuilderTests
{
    /// <summary>The default color of a mark, which is also the first color of the tableau10 palette.</summary>
    private const string Blue = "#4c78a8";

    /// <summary>A vertical bar chart: 4 categories (East, North, South, West after the ascending sort) in 240 by 200 pixels.</summary>
    private const string BarSpec = """
        { "width": 240, "height": 200,
          "data": { "values": [ { "k": "East", "n": 43 }, { "k": "North", "n": 28 }, { "k": "South", "n": 55 }, { "k": "West", "n": 91 } ] },
          "mark": "bar",
          "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "n", "type": "quantitative" } } }
        """;

    /// <summary>
    ///     Builds a specification of a mark in a 100 by 100 view.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string encoding, string values) =>
        "{ \"width\": 100, \"height\": 100, \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + " }";

    /// <summary>
    ///     Rounds numbers to nine decimals, so that a hand-derived value is not compared with the last bit of a floating point result.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The rounded numbers.</returns>
    private static List<double> R(IEnumerable<double> values) => [.. values.Select(v => Math.Round(v, 9))];

    /// <summary>
    ///     Rounds four numbers to nine decimals.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <param name="c">The third number.</param>
    /// <param name="d">The fourth number.</param>
    /// <returns>The rounded numbers.</returns>
    private static (double, double, double, double) Q(double a, double b, double c, double d) =>
        (Math.Round(a, 9), Math.Round(b, 9), Math.Round(c, 9), Math.Round(d, 9));

    /// <summary>
    ///     Validates the positions of bars and rectangles for discrete, quantitative, negative and absent channels.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_BarAndRectPositionsMatchExpectedSceneItems()
    {
        // Arrange: x is a band scale of 4 over 240 px: step 240 / (4 - 0.1 + 0.1) = 60, band width 54, first band at
        // (240 - 60 * 3.9) / 2 = 3. y is linear 0 to 91, extended by nice to 0 to 100, over 200 px: y(v) = 200 - 2v.
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(BarSpec, 240, 200), "mark-bar");

        // Assert: x = 3, 63, 123, 183 (width 54); the bar runs from y(v) down to y(0) = 200
        Assert.Equal(4, bars.Count);
        Assert.Equal([3.0, 63.0, 123.0, 183.0], R(bars.Select(b => b.X)));
        Assert.Equal([54.0, 54.0, 54.0, 54.0], R(bars.Select(b => b.Width)));
        Assert.Equal([114.0, 144.0, 90.0, 18.0], R(bars.Select(b => b.Y)));
        Assert.Equal([86.0, 56.0, 110.0, 182.0], R(bars.Select(b => b.Height)));
        Assert.All(bars, b => Assert.Equal((Blue, (string?)null, 1.0, 0.0), (b.Fill, b.Stroke, b.Opacity, b.CornerRadius)));

        // Arrange: a horizontal bar chart: y is a band scale of 4 over 200 px: step 50, band width 45, first band at 2.5.
        // x is linear 0 to 100 over 240 px: x(v) = 2.4 v, so the bar for 43 is 103.2 wide and starts at x(0) = 0.
        const string horizontal = """
            { "width": 240, "height": 200,
              "data": { "values": [ { "k": "East", "n": 43 }, { "k": "North", "n": 28 }, { "k": "South", "n": 55 }, { "k": "West", "n": 91 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "n", "type": "quantitative" }, "y": { "field": "k", "type": "nominal" } } }
            """;
        var rows = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(horizontal, 240, 200), "mark-bar");
        Assert.Equal([2.5, 52.5, 102.5, 152.5], R(rows.Select(b => b.Y)));
        Assert.Equal([45.0, 45.0, 45.0, 45.0], R(rows.Select(b => b.Height)));
        Assert.Equal([0.0, 0.0, 0.0, 0.0], R(rows.Select(b => b.X)));
        Assert.Equal([103.2, 67.2, 132.0, 218.4], R(rows.Select(b => b.Width)));

        // Arrange: negative values: y is -10 to 30 over 200 px, y(v) = 5 (30 - v): the bar of -10 hangs from y(0) = 150 to y(-10) = 200
        const string negative = """
            { "width": 100, "height": 200,
              "data": { "values": [ { "k": "a", "n": -10 }, { "k": "b", "n": 30 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "n", "type": "quantitative" } } }
            """;
        var signed = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(negative, 100, 200), "mark-bar");
        Assert.Equal([150.0, 0.0], R(signed.Select(b => b.Y)));
        Assert.Equal([50.0, 150.0], R(signed.Select(b => b.Height)));

        // Arrange: a bar on a measure axis whose domain does not contain zero runs to the first domain value (y(20) = 100)
        const string offset = """
            { "width": 100, "height": 100,
              "data": { "values": [ { "k": "a", "n": 60 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "n", "type": "quantitative", "scale": { "domain": [20, 100] } } } }
            """;
        var shifted = Assert.Single(ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(offset, 100, 100), "mark-bar"));
        Assert.Equal([50.0, 50.0], R([shifted.Y, shifted.Height]));

        // Arrange: two quantitative channels: the bar is 5 px (continuousBandSize) wide and centered on its x value
        var both = Spec("\"bar\"", "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }", "[ { \"x\": 10, \"y\": 20 }, { \"x\": 30, \"y\": 40 } ]");
        var xModel = ChartTestHelpers.Parts(both).Scales["x"]!.CreateModel(100);
        var continuous = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(both, 100, 100), "mark-bar");
        Assert.Equal(2, continuous.Count);
        Assert.Equal([5.0, 5.0], R(continuous.Select(b => b.Width)));
        Assert.Equal(R([xModel.Map(10) - 2.5, xModel.Map(30) - 2.5]), R(continuous.Select(b => b.X)));

        // Arrange: a bar without a y channel spans the whole height; without an x channel it is centered and 0.9 of the width
        var noY = Spec("\"bar\"", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" } }", "[ { \"k\": \"a\" }, { \"k\": \"b\" } ]");
        var tall = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(noY, 100, 100), "mark-bar");
        Assert.All(tall, b => Assert.Equal((0.0, 100.0), (b.Y, b.Height)));
        var noX = Spec("\"bar\"", "{ \"y\": { \"field\": \"n\", \"type\": \"quantitative\" } }", "[ { \"n\": 10 } ]");
        var wide = Assert.Single(ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(noX, 100, 100), "mark-bar"));
        Assert.Equal([5.0, 90.0], R([wide.X, wide.Width]));

        // Arrange: an explicit size is centered on the band: the band of East is 3 to 57, its center 30
        var sized = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(BarSpec.Replace("\"mark\": \"bar\"", "\"mark\": { \"type\": \"bar\", \"size\": 20, \"cornerRadius\": 3 }", StringComparison.Ordinal), 240, 200), "mark-bar");
        Assert.Equal([20.0, 20.0, 3.0], R([sized[0].X, sized[0].Width, sized[0].CornerRadius]));

        // Arrange: a rect mark with two discrete channels fills the cells: bands of 50 with no padding
        var cells = Spec("\"rect\"", "{ \"x\": { \"field\": \"a\", \"type\": \"nominal\" }, \"y\": { \"field\": \"b\", \"type\": \"nominal\" } }", "[ { \"a\": \"p\", \"b\": \"u\" }, { \"a\": \"q\", \"b\": \"v\" } ]");
        var rects = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(cells, 100, 100), "mark-rect");
        Assert.Equal([Q(0, 0, 50, 50), Q(50, 50, 50, 50)], rects.Select(r => Q(r.X, r.Y, r.Width, r.Height)));
        Assert.All(rects, r => Assert.Equal(Blue, r.Fill));

        // Assert: marks are clipped to the data rectangle only when the mark says so
        Assert.Null(ChartTestHelpers.Marks(BarSpec, 240, 200).Clip);
        var clipped = ChartTestHelpers.Marks(BarSpec.Replace("\"mark\": \"bar\"", "\"mark\": { \"type\": \"bar\", \"clip\": true }", StringComparison.Ordinal), 240, 200);
        Assert.Equal(new SceneClip(0, 0, 240, 200), clipped.Clip);
    }

    /// <summary>
    ///     Validates the symbols, rules and ticks: positions, sizes, fills, strokes, opacities and orientations.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PointCircleSquareRuleAndTickMatchExpectedSceneItems()
    {
        // Arrange: x is 0 to 100 over 300 px (3 per unit), y is 0 to 50 over 200 px (200 - 4v); the third row has no y value
        // and is dropped (Vega-Lite's default invalid mode for these marks is filter)
        const string rows = "[ { \"x\": 0, \"y\": 0 }, { \"x\": 100, \"y\": 50 }, { \"x\": 50, \"y\": null } ]";
        const string xy = "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }";
        static string Wide(string mark, string encoding, string values) => Spec(mark, encoding, values).Replace("\"width\": 100, \"height\": 100", "\"width\": 300, \"height\": 200", StringComparison.Ordinal);

        // Act
        var points = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Wide("\"point\"", xy, rows), 300, 200), "mark-point");
        var circles = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Wide("\"circle\"", xy, rows), 300, 200), "mark-circle");
        var squares = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Wide("\"square\"", xy, rows), 300, 200), "mark-square");
        var filled = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Wide("{ \"type\": \"point\", \"filled\": true }", xy, rows), 300, 200), "mark-point");

        // Assert: a point is an outline: a transparent fill, the mark color as a 2 px stroke, size 30, opacity 0.7
        Assert.Equal([0.0, 300.0], R(points.Select(p => p.X)));
        Assert.Equal([200.0, 0.0], R(points.Select(p => p.Y)));
        Assert.All(points, p => Assert.Equal(("circle", 30.0, 0.0, "transparent", Blue, (double?)2.0, 0.7), (p.Shape, p.Size, p.Angle, p.Fill, p.Stroke, p.StrokeWidth, p.Opacity)));

        // Assert: circles and squares are filled and have no stroke; the opacity is reduced too; a filled point is like a circle
        Assert.All(circles, c => Assert.Equal(("circle", Blue, (string?)null, 0.7), (c.Shape, c.Fill, c.Stroke, c.Opacity)));
        Assert.All(squares, s => Assert.Equal(("square", Blue, (string?)null, 0.7), (s.Shape, s.Fill, s.Stroke, s.Opacity)));
        Assert.All(filled, p => Assert.Equal((Blue, (string?)null), (p.Fill, p.Stroke)));
        Assert.Equal(2, circles.Count);

        // Assert: a missing channel puts the symbol in the middle of the data rectangle; an explicit opacity and size win
        var middleSpec = Wide("{ \"type\": \"point\", \"opacity\": 1, \"size\": 80, \"angle\": 45 }", "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" } }", "[ { \"x\": 100 } ]");
        var middle = Assert.Single(ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(middleSpec, 300, 200), "mark-point"));
        Assert.Equal([300.0, 100.0, 1.0, 80.0, 45.0], R([middle.X, middle.Y, middle.Opacity, middle.Size, middle.Angle]));

        // Assert: a point scale puts symbols at 50, 150, 250 (step 300 / (3 - 1 + 2 * 0.5) = 100, first at (300 - 200) / 2)
        var categories = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Wide("\"point\"", "{ \"x\": { \"field\": \"c\", \"type\": \"nominal\" } }", "[ { \"c\": \"A\" }, { \"c\": \"B\" }, { \"c\": \"C\" } ]"), 300, 200), "mark-point");
        Assert.Equal([50.0, 150.0, 250.0], R(categories.Select(s => s.X)));
        Assert.Equal([100.0, 100.0, 100.0], R(categories.Select(s => s.Y)));

        // Arrange: rules in a 100 by 100 view; x is 0 to 60 so x(v) = 100 v / 60 (33.33 and 100)
        const string values = "[ { \"x\": 20, \"k\": \"a\" }, { \"x\": 60, \"k\": \"b\" } ]";
        const double third = 100.0 * 20 / 60;
        var vertical = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("\"rule\"", "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" } }", values), 100, 100), "mark-rule");
        var horizontal = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("\"rule\"", "{ \"y\": { \"field\": \"x\", \"type\": \"quantitative\" } }", values), 100, 100), "mark-rule");
        var stems = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("{ \"type\": \"rule\", \"strokeWidth\": 3 }", "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"k\", \"type\": \"nominal\" } }", values), 100, 100), "mark-rule");

        // Assert: only x: vertical rules across the whole height; only y: horizontal rules across the whole width, which Vega-Lite
        // draws from x = width back to x2 = 0 (y(v) = 100 - 100 v / 60)
        Assert.Equal([Q(third, 0, third, 100), Q(100, 0, 100, 100)], vertical.Select(r => Q(r.X, r.Y, r.X2, r.Y2)));
        Assert.All(vertical, r => Assert.Equal(("black", (double?)null, 1.0), (r.Stroke, r.StrokeWidth, r.Opacity)));
        Assert.Equal([Q(100, 100 - third, 0, 100 - third), Q(100, 0, 0, 0)], horizontal.Select(r => Q(r.X, r.Y, r.X2, r.Y2)));

        // Assert: x measure and y bands: horizontal stems at the band centers (rule bands have no padding: step 50) from x(v) back to x(0)
        Assert.Equal([Q(third, 25, 0, 25), Q(100, 75, 0, 75)], stems.Select(r => Q(r.X, r.Y, r.X2, r.Y2)));
        Assert.All(stems, r => Assert.Equal((double?)3.0, r.StrokeWidth));

        // Arrange: ticks: x measure and y bands: vertical ticks. The tick bands have inner padding 0.25 and outer 0.125:
        // step 100 / (2 - 0.25 + 0.25) = 50, band width 37.5, first band at 6.25; a tick is 1 px thick and centered on x(v)
        var vTicks = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"tick\"", "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"k\", \"type\": \"nominal\" } }", values), 100, 100), "mark-tick");
        var hTicks = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("{ \"type\": \"tick\", \"thickness\": 3 }", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"x\", \"type\": \"quantitative\" } }", values), 100, 100), "mark-tick");
        var qTicks = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"tick\"", "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"x\", \"type\": \"quantitative\" } }", values), 100, 100), "mark-tick");

        // Assert
        Assert.Equal([Q(third - 0.5, 6.25, 1, 37.5), Q(99.5, 56.25, 1, 37.5)], vTicks.Select(t => Q(t.X, t.Y, t.Width, t.Height)));
        Assert.All(vTicks, t => Assert.Equal((Blue, 0.7), (t.Fill, t.Opacity)));

        // Assert: a horizontal tick spans its band (the x bands of tick marks start at 6.25) and is 3 px thick around y(v) = 100 - 100 v / 60
        Assert.Equal([Q(6.25, 100 - third - 1.5, 37.5, 3), Q(56.25, -1.5, 37.5, 3)], hTicks.Select(t => Q(t.X, t.Y, t.Width, t.Height)));

        // Assert: two measures: a vertical tick that is continuousBandSize (5) long, centered on y(v), and 1 px thick
        Assert.All(qTicks, t => Assert.Equal((5.0, 1.0), (t.Height, t.Width)));
    }

    /// <summary>
    ///     Validates that color, size and opacity fields and values set the paint of the items.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ColorSizeAndOpacityUseExpectedProperties()
    {
        // Arrange: g is nominal (a, b), q is quantitative 10..30 (size domain starts at zero: 0 to 30), o is quantitative 2..4
        // (opacity has no zero rule: 2 to 4 maps to 0.3 to 0.8). The point size range is 4 to (0.95 * 20)^2 = 361.
        const string values = "[ { \"g\": \"a\", \"q\": 10, \"o\": 2 }, { \"g\": \"b\", \"q\": 20, \"o\": 3 }, { \"g\": \"a\", \"q\": 30, \"o\": 4 } ]";
        const string encoding = """
            { "x": { "field": "q", "type": "quantitative" }, "y": { "field": "o", "type": "quantitative" },
              "color": { "field": "g", "type": "nominal" },
              "size": { "field": "q", "type": "quantitative" },
              "opacity": { "field": "o", "type": "quantitative" } }
            """;

        // Act
        var symbols = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("{ \"type\": \"point\", \"filled\": true }", encoding, values), 100, 100), "mark-point");
        var outlines = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("\"point\"", encoding, values), 100, 100), "mark-point");

        // Assert: tableau10 by sorted category: a is #4c78a8, b is #f58518; the filled point takes the color as its fill
        Assert.Equal([Blue, "#f58518", Blue], symbols.Select(s => s.Fill));
        Assert.All(symbols, s => Assert.Null(s.Stroke));

        // Assert: size = 4 + 357 q / 30, opacity = 0.3 + 0.5 (o - 2) / 2
        Assert.Equal([123.0, 242.0, 361.0], R(symbols.Select(s => s.Size)));
        Assert.Equal([0.3, 0.55, 0.8], R(symbols.Select(s => s.Opacity)));

        // Assert: an unfilled point takes the color as its stroke and keeps a transparent fill
        Assert.Equal([Blue, "#f58518", Blue], outlines.Select(s => s.Stroke));
        Assert.All(outlines, s => Assert.Equal("transparent", s.Fill));

        // Assert: value definitions are used as given, and the fill channel wins over the color channel
        const string constants = "{ \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"color\": { \"value\": \"crimson\" }, \"fill\": { \"value\": \"#102030\" }, \"opacity\": { \"value\": 0.25 }, \"size\": { \"value\": 99 } }";
        var fixedItems = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("{ \"type\": \"circle\" }", constants, values), 100, 100), "mark-circle");
        Assert.All(fixedItems, s => Assert.Equal(("#102030", 0.25, 99.0), (s.Fill, s.Opacity, s.Size)));

        // Assert: explicit mark properties and configuration: the mark definition wins over config.point, which wins over config.mark
        const string xOnly = "{ \"x\": { \"field\": \"q\", \"type\": \"quantitative\" } }";
        var configured = ChartTestHelpers.Marks(Spec("{ \"type\": \"point\", \"stroke\": \"teal\", \"strokeWidth\": 3 }", xOnly, "[ { \"q\": 1 } ]"), 100, 100, "{ \"point\": { \"size\": 50, \"strokeWidth\": 9, \"fillOpacity\": 0.5 }, \"mark\": { \"color\": \"gold\" } }");
        var item = Assert.Single(ChartTestHelpers.Of<SceneSymbol>(configured, "mark-point"));
        Assert.Equal(("teal", (double?)3.0, 50.0, 0.5), (item.Stroke, item.StrokeWidth, item.Size, item.FillOpacity));

        // Assert: the opacity is 1 for bars by default, and the configured opacity replaces the reduced default of points
        var plainBar = Assert.Single(ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", "{ \"y\": { \"field\": \"q\", \"type\": \"quantitative\" } }", "[ { \"q\": 1 } ]"), 100, 100), "mark-bar"));
        Assert.Equal(1.0, plainBar.Opacity, 9);
        var configuredOpacity = Assert.Single(ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("\"point\"", xOnly, "[ { \"q\": 1 } ]"), 100, 100, "{ \"mark\": { \"opacity\": 0.9 } }"), "mark-point"));
        Assert.Equal(0.9, configuredOpacity.Opacity, 9);

        // Assert: a row whose size value is not a number is dropped
        var invalid = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("\"point\"", "{ \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"size\": { \"field\": \"s\", \"type\": \"quantitative\" } }", "[ { \"q\": 1, \"s\": 1 }, { \"q\": 2, \"s\": \"n/a\" }, { \"q\": 3, \"s\": 3 } ]"), 100, 100), "mark-point");
        Assert.Equal(2, invalid.Count);
    }

    /// <summary>
    ///     Validates that the budget counts the items that are created: rows that draw nothing cost nothing, and the marks group
    ///     and the axes are counted without an item too many.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_BudgetCountsOnlyTheItemsThatAreCreated()
    {
        // Arrange: five rows of which three have no valid x, so only two symbols exist
        var parts = ChartTestHelpers.Parts(Spec(
            "\"point\"",
            "{ \"x\": { \"field\": \"q\", \"type\": \"quantitative\" } }",
            "[ { \"q\": 1 }, { \"q\": null }, { \"q\": 2 }, { \"q\": \"n/a\" }, { \"q\": \"none\" } ]"));
        var budget = new SceneBudget(3);

        // Act
        var group = new MarkBuilder(parts.Config, budget: budget).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, TestContext.Current.CancellationToken);

        // Assert: the marks group and two items fit a budget of three, and exactly three were counted
        Assert.Equal(2, group.Children.Count);
        Assert.Equal(3, budget.Count);

        // Assert: a budget of two fails on the second symbol, with the location of the mark; the marks group counted first
        var tight = new SceneBudget(2);
        var ex = Assert.Throws<VegaLiteRenderException>(() =>
            new MarkBuilder(parts.Config, budget: tight, sourceName: "t.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, TestContext.Current.CancellationToken));
        Assert.Equal("/mark", ex.Location.Pointer);
        Assert.Equal(3, tight.Count);
    }

    /// <summary>
    ///     Validates the limits, cancellation and the marks that are not drawn.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks()
    {
        // Arrange
        var parts = ChartTestHelpers.Parts(BarSpec);
        var limits = new VegaLiteLimits { MaxSceneItems = 3 };
        var builder = new MarkBuilder(parts.Config, limits, sourceName: "test.vl.json");

        // Act / Assert: more items than the budget fail with the location of the mark
        var ex = Assert.Throws<VegaLiteRenderException>(() => builder.Build(parts.Tree.Root, parts.Table, parts.Scales, 240, 200, TestContext.Current.CancellationToken));
        Assert.Equal("/mark", ex.Location.Pointer);
        Assert.Contains("MaxSceneItems", ex.Message, StringComparison.Ordinal);

        // Act / Assert: cancellation is honored before the first batch
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new MarkBuilder(parts.Config).Build(parts.Tree.Root, parts.Table, parts.Scales, 240, 200, canceled.Token));

        // Assert: marks that are not drawn give an empty group, and unsupported mark types are named by IsSupported
        var line = ChartTestHelpers.Marks(BarSpec.Replace("\"mark\": \"bar\"", "\"mark\": \"line\"", StringComparison.Ordinal), 240, 200);
        Assert.Empty(line.Children);
        Assert.True(MarkBuilder.IsSupported("tick"));
        Assert.False(MarkBuilder.IsSupported("line"));
        Assert.False(MarkBuilder.IsSupported(null));

        // Assert: a rule without x and y draws nothing, and null arguments are rejected
        Assert.Empty(ChartTestHelpers.Marks(Spec("\"rule\"", "{}", "[ { \"a\": 1 } ]"), 100, 100).Children);
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, parts.Table, parts.Scales, 1, 1, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => new MarkBuilder(null!));
    }
}
