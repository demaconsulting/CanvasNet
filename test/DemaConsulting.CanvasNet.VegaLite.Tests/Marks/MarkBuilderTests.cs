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

        // Arrange: a bar on a measure axis whose domain does not contain zero starts at the stack start 0, as Vega-Lite draws it: y(60) = 50 down to y(0) = 125.
        // That is beyond the data rectangle, and the marks are NOT clipped: Vega-Lite's scaleClip applies to interval-selection extents only, so an explicit
        // domain does not clip (the geometry is the unclipped overflow); only an explicit "clip": true does
        const string offset = """
            { "width": 100, "height": 100,
              "data": { "values": [ { "k": "a", "n": 60 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "n", "type": "quantitative", "scale": { "domain": [20, 100] } } } }
            """;
        var shiftedMarks = ChartTestHelpers.Marks(offset, 100, 100);
        var shifted = Assert.Single(ChartTestHelpers.Of<SceneRect>(shiftedMarks, "mark-bar"));
        Assert.Equal([50.0, 75.0], R([shifted.Y, shifted.Height]));
        Assert.Null(shiftedMarks.Clip);

        // Arrange: without the stack (stack: null) the bar runs to the first domain value as before: y(20) = 100
        var unstacked = Assert.Single(ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(offset.Replace("\"type\": \"quantitative\", \"scale\"", "\"type\": \"quantitative\", \"stack\": null, \"scale\"", StringComparison.Ordinal), 100, 100), "mark-bar"));
        Assert.Equal([50.0, 50.0], R([unstacked.Y, unstacked.Height]));

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

    /// <summary>
    ///     Validates that the row-bound expression references of the mark are evaluated per row when the builder has the compiled
    ///     expressions and the evaluator, that a result of the wrong kind keeps the default, and that without them the references
    ///     are left unevaluated (the mark keeps its defaults).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_RowBoundExpressionReferencesAreEvaluatedPerRow()
    {
        // Arrange: the fill is red above 3, a text that is not a color value stays text; the opacity is 0.25 above 3 and a text
        // (not a number) below; the stroke width is the row value; the size channel value is the row value times 10
        const string mark = "{ \"type\": \"point\", \"fill\": { \"expr\": \"datum.q > 3 ? 'red' : 7\" }, \"opacity\": { \"expr\": \"datum.q > 3 ? 0.25 : 'x'\" }, \"strokeWidth\": { \"expr\": \"datum.q\" } }";
        const string encoding = "{ \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"size\": { \"value\": { \"expr\": \"datum.q * 10\" } } }";
        var json = Spec(mark, encoding, "[ { \"q\": 2 }, { \"q\": 5 } ]");
        var parts = ChartTestHelpers.Parts(json);
        var values = new Dictionary<string, DemaConsulting.CanvasNet.VegaLite.Data.DataValue>();
        var evaluator = new DemaConsulting.CanvasNet.VegaLite.Expressions.ExpressionEvaluator(null, TestContext.Current.CancellationToken, values);
        var expressions = new DemaConsulting.CanvasNet.VegaLite.Transforms.RowExpressions(parts.Tree.Expressions, evaluator);

        // Act
        var evaluated = new MarkBuilder(parts.Config, expressions: expressions).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, TestContext.Current.CancellationToken);
        var plain = new MarkBuilder(parts.Config).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, TestContext.Current.CancellationToken);
        var items = ChartTestHelpers.Of<SceneSymbol>(evaluated, "mark-point");
        var defaults = ChartTestHelpers.Of<SceneSymbol>(plain, "mark-point");

        // Assert: row 0 keeps the default fill (the expression gave a number) and the default opacity (0.7 of a point), row 1 has its values
        Assert.Equal(2, items.Count);
        Assert.Equal(["#4c78a8", "red"], [items[0].Fill is null ? "#4c78a8" : items[0].Fill, items[1].Fill]);
        Assert.Equal([0.7, 0.25], R(items.Select(i => i.Opacity)));
        Assert.Equal([2.0, 5.0], R(items.Select(i => i.StrokeWidth ?? double.NaN)));
        Assert.Equal([20.0, 50.0], R(items.Select(i => i.Size)));

        // Assert: without the evaluator the references change nothing (the size falls back to the area of a point, 30)
        Assert.All(defaults, d => Assert.Equal(0.7, d.Opacity));
        Assert.All(defaults, d => Assert.NotEqual("red", d.Fill));
    }

    /// <summary>Three rows in two stacks: region a has the layers p (3) and q (5), region b only p (4).</summary>
    private const string Layers = "[ { \"k\": \"a\", \"c\": \"p\", \"n\": 3 }, { \"k\": \"a\", \"c\": \"q\", \"n\": 5 }, { \"k\": \"b\", \"c\": \"p\", \"n\": 4 } ]";

    /// <summary>The four-step bins of the rows 1, 4, 12, 18 and 25 (three bins of ten: 0 to 30).</summary>
    private const string BinValues = "[ { \"v\": 1 }, { \"v\": 4 }, { \"v\": 12 }, { \"v\": 18 }, { \"v\": 25 } ]";

    /// <summary>
    ///     Builds a specification of a mark of a given size.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="width">The width of the view.</param>
    /// <param name="height">The height of the view.</param>
    /// <returns>The specification text.</returns>
    private static string Sized(string mark, string encoding, string values, int width, int height) =>
        "{ \"width\": " + width + ", \"height\": " + height + ", \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + " }";

    /// <summary>
    ///     Gets the rectangle items of a mark.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="height">The height of the data rectangle.</param>
    /// <param name="role">The role of the items.</param>
    /// <returns>The rectangles in drawing order.</returns>
    private static List<SceneRect> Rectangles(string json, double width, double height, string role = "mark-bar") =>
        ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(json, width, height), role);

    /// <summary>
    ///     Validates that the layers of a stack are drawn from the start to the end of their stack (vertical and horizontal, zero, center and normalize),
    ///     that <c>stack: null</c> brings back the overlapping bars from zero, and that a symbol sits at the upper end of its stack.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_StackedLayersSpanFromTheStackStartToItsEnd()
    {
        // Arrange: two bands of 45 px start at 2.5 and 52.5. The stacks of a vertical bar chart are ordered by the color, descending (the first layer
        // is the bottom one): q is 0 to 5 and p 5 to 8 in a; p is 0 to 4 in b. The y domain is 0 to 8, so y(v) = 100 - 12.5 v
        const string color = ", \"color\": { \"field\": \"c\", \"type\": \"nominal\" }";
        string Vertical(string stack) => Sized("\"bar\"", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"n\", \"type\": \"quantitative\"" + stack + " }" + color + " }", Layers, 100, 100);

        // Act
        var zero = Rectangles(Vertical(string.Empty), 100, 100);

        // Assert: the rows are in data order: a/p is the upper layer (5 to 8), a/q the lower (0 to 5), b/p the whole stack of b (0 to 4)
        Assert.Equal([Q(2.5, 0, 45, 37.5), Q(2.5, 37.5, 45, 62.5), Q(52.5, 50, 45, 50)], zero.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Assert: center puts the stack of b (4 of the largest 8) in the middle: 2 to 6, so y(6) = 25 and y(2) = 75; a is as before
        var center = Rectangles(Vertical(", \"stack\": \"center\""), 100, 100);
        Assert.Equal([Q(2.5, 0, 45, 37.5), Q(2.5, 37.5, 45, 62.5), Q(52.5, 25, 45, 50)], center.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Assert: normalize divides by the stack total (8 and 4): a is q 0 to 0.625 and p 0.625 to 1, b is 0 to 1 over the domain 0 to 1: y(v) = 100 - 100 v
        var normalized = Rectangles(Vertical(", \"stack\": \"normalize\""), 100, 100);
        Assert.Equal([Q(2.5, 0, 45, 37.5), Q(2.5, 37.5, 45, 62.5), Q(52.5, 0, 45, 100)], normalized.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Assert: without the stack every bar runs from zero and the domain is 0 to 5 (y(v) = 100 - 20 v): the bars overlap
        var overlapping = Rectangles(Vertical(", \"stack\": null"), 100, 100);
        Assert.Equal([Q(2.5, 40, 45, 60), Q(2.5, 0, 45, 100), Q(52.5, 20, 45, 80)], overlapping.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Arrange: a horizontal chart stacks x ascending by the color: p is 0 to 3 and q 3 to 8 in a, p is 0 to 4 in b; x(v) = 12.5 v
        var horizontal = Sized("\"bar\"", "{ \"y\": { \"field\": \"k\", \"type\": \"nominal\" }, \"x\": { \"field\": \"n\", \"type\": \"quantitative\" }" + color + " }", Layers, 100, 100);

        // Assert
        Assert.Equal(
            [Q(0, 2.5, 37.5, 45), Q(37.5, 2.5, 62.5, 45), Q(0, 52.5, 50, 45)],
            Rectangles(horizontal, 100, 100).Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Arrange: a circle with an explicit stack sits at the upper end of its layer (x points at 25 and 75)
        var circles = Sized("\"circle\"", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"n\", \"type\": \"quantitative\", \"stack\": \"zero\" }" + color + " }", Layers, 100, 100);

        // Assert: the ends 8, 5 and 4 are at y = 0, 37.5 and 50
        var symbols = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(circles, 100, 100), "mark-circle");
        Assert.Equal([0.0, 37.5, 50.0], R(symbols.Select(s => s.Y)));

        // Assert: the result is deterministic
        Assert.Equal(zero.Select(b => (b.X, b.Y, b.Width, b.Height)), Rectangles(Vertical(string.Empty), 100, 100).Select(b => (b.X, b.Y, b.Width, b.Height)));
    }

    /// <summary>
    ///     Validates that signed stacks hang below the zero baseline in both orientations: positives stack up (right) from zero and negatives stack down
    ///     (left) from zero, separately; an all-negative stack hangs entirely below zero; center and normalize use the absolute values. The rectangles are
    ///     derived by hand from the domains below, with the band of a single category 90 px wide at 5 and of two categories 45 px wide at 2.5 and 52.5.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_SignedStacksHangBelowZeroInBothOrientations()
    {
        // Arrange: one category a with p = 3, q = 2, r = -4, s = -1. A vertical stack runs descending by the color: q (0, 2) then p (2, 5) above
        // zero, s (0, -1) then r (-1, -5) below. The domain is -5 to 5, so y(v) = 50 - 10 v
        const string mixed = "[ { \"k\": \"a\", \"c\": \"p\", \"n\": 3 }, { \"k\": \"a\", \"c\": \"q\", \"n\": 2 }, { \"k\": \"a\", \"c\": \"r\", \"n\": -4 }, { \"k\": \"a\", \"c\": \"s\", \"n\": -1 } ]";
        const string color = ", \"color\": { \"field\": \"c\", \"type\": \"nominal\" }";
        var vertical = Sized("\"bar\"", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"n\", \"type\": \"quantitative\" }" + color + " }", mixed, 100, 100);

        // Act
        var up = Rectangles(vertical, 100, 100);

        // Assert: the rows are in data order: p is 2 to 5 (y 0 to 30), q is 0 to 2 (30 to 50), r is -1 to -5 (60 to 100), s is 0 to -1 (50 to 60)
        Assert.Equal([Q(5, 0, 90, 30), Q(5, 30, 90, 20), Q(5, 60, 90, 40), Q(5, 50, 90, 10)], up.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Arrange: the horizontal chart stacks ascending by the color: p (0, 3) then q (3, 5) to the right, r (0, -4) then s (-4, -5) to the left; x(v) = 50 + 10 v
        var horizontal = Sized("\"bar\"", "{ \"y\": { \"field\": \"k\", \"type\": \"nominal\" }, \"x\": { \"field\": \"n\", \"type\": \"quantitative\" }" + color + " }", mixed, 100, 100);

        // Assert: p is x 50 wide 30, q is 80 wide 20, r is 10 wide 40 (from -4 to 0), s is 0 wide 10 (from -5 to -4)
        Assert.Equal(
            [Q(50, 5, 30, 90), Q(80, 5, 20, 90), Q(10, 5, 40, 90), Q(0, 5, 10, 90)],
            Rectangles(horizontal, 100, 100).Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Arrange: an all-negative stack: p = -3 and q = -2 stack down to -5 (q first, then p). The domain is -5 to 0, so y(v) = -20 v
        const string negative = "[ { \"k\": \"a\", \"c\": \"p\", \"n\": -3 }, { \"k\": \"a\", \"c\": \"q\", \"n\": -2 } ]";
        var down = Sized("\"bar\"", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"n\", \"type\": \"quantitative\" }" + color + " }", negative, 100, 100);

        // Assert: p is -2 to -5 (y 40 to 100) and q is 0 to -2 (y 0 to 40): nothing is drawn above the baseline at the top edge
        Assert.Equal([Q(5, 40, 90, 60), Q(5, 0, 90, 40)], Rectangles(down, 100, 100).Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Arrange: a with p = 3 and q = -1, b with p = -2 (the data order is a/p, a/q, b/p)
        const string signed = "[ { \"k\": \"a\", \"c\": \"p\", \"n\": 3 }, { \"k\": \"a\", \"c\": \"q\", \"n\": -1 }, { \"k\": \"b\", \"c\": \"p\", \"n\": -2 } ]";
        string Offset(string stack) => Sized("\"bar\"", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"n\", \"type\": \"quantitative\", \"stack\": \"" + stack + "\" }" + color + " }", signed, 100, 100);

        // Assert: center adds the absolute values: a is q 0 to 1 and p 1 to 4 (the widest, starting at 0), b is p from (4 - 2) / 2 = 1 to 3; the domain is 0 to 4, y(v) = 100 - 25 v
        Assert.Equal(
            [Q(2.5, 0, 45, 75), Q(2.5, 75, 45, 25), Q(52.5, 25, 45, 50)],
            Rectangles(Offset("center"), 100, 100).Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Assert: normalize divides by the sum of the absolute values (4 and 2): a is q 0 to 0.25 and p 0.25 to 1, b is p 0 to 1; y(v) = 100 - 100 v
        Assert.Equal(
            [Q(2.5, 0, 45, 75), Q(2.5, 75, 45, 25), Q(52.5, 0, 45, 100)],
            Rectangles(Offset("normalize"), 100, 100).Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Assert: the result is deterministic
        Assert.Equal(up.Select(b => (b.X, b.Y, b.Width, b.Height)), Rectangles(vertical, 100, 100).Select(b => (b.X, b.Y, b.Width, b.Height)));
    }

    /// <summary>
    ///     Validates the geometry of bars and rectangles on a binned field: the bin from its start to its end less the bin spacing (1 for a bar, 0 for a
    ///     rect) and shifted by the half pixel of the axis translate; the reversed scale; <c>minBandSize</c>; an explicit <c>binSpacing</c>; a size that
    ///     centers the bar on the middle of the bin; and a bin step on a field that is binned already.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_BinnedBarsSpanTheirBinsLessTheSpacing()
    {
        // Arrange: the bins 0 to 30 by 10 hold 2, 2 and 1 rows. The view is 90 wide, so x(v) = 3 v and the bin edges are at 0, 30, 60 and 90.
        // The count domain is 0 to 2 (y(v) = 100 - 50 v)
        const string x = "{ \"field\": \"v\", \"type\": \"quantitative\", \"bin\": { \"step\": 10 } }";
        const string count = "{ \"aggregate\": \"count\", \"type\": \"quantitative\" }";
        string Histogram(string mark, string scale = "") =>
            Sized(mark, "{ \"x\": { \"field\": \"v\", \"type\": \"quantitative\", \"bin\": { \"step\": 10 }" + scale + " }, \"y\": " + count + " }", BinValues, 90, 100);

        // Act
        var bars = Rectangles(Histogram("\"bar\""), 90, 100);

        // Assert: bin spacing 1 takes half a pixel off each edge and the translate puts back half a pixel: x = 0 + 0.5 + 0.5, width 30 - 1
        Assert.Equal([Q(1, 0, 29, 100), Q(31, 0, 29, 100), Q(61, 50, 29, 50)], bars.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Assert: the bin spacing of the mark: 0 (the translate stays), and 4 (two pixels off each edge)
        var flush = Rectangles(Histogram("{ \"type\": \"bar\", \"binSpacing\": 0 }"), 90, 100);
        Assert.Equal([0.5, 30.5, 60.5], R(flush.Select(b => b.X)));
        Assert.Equal([30.0, 30.0, 30.0], R(flush.Select(b => b.Width)));
        var spaced = Rectangles(Histogram("{ \"type\": \"bar\", \"binSpacing\": 4 }"), 90, 100);
        Assert.Equal([2.5, 32.5, 62.5], R(spaced.Select(b => b.X)));
        Assert.Equal([26.0, 26.0, 26.0], R(spaced.Select(b => b.Width)));

        // Assert: a reversed scale (x(v) = 90 - 3 v) draws the same bins mirrored: the first bin (90 to 60) starts at 60 + 0.5 + 0.5
        Assert.Equal([61.0, 31.0, 1.0], R(Rectangles(Histogram("\"bar\"", ", \"scale\": { \"reverse\": true }"), 90, 100).Select(b => b.X)));

        // Assert: a size centers the bar on the middle of its bin (x(5) = 15, x(15) = 45, x(25) = 75) without spacing or translate
        var sized = Rectangles(Histogram("{ \"type\": \"bar\", \"size\": 10 }"), 90, 100);
        Assert.Equal([Q(10, 0, 10, 100), Q(40, 0, 10, 100), Q(70, 50, 10, 50)], sized.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Arrange: a rect on the binned field has no bin spacing: the rows are drawn one by one, each over its bin, the full height without a y channel
        var rects = Rectangles(Sized("\"rect\"", "{ \"x\": " + x + " }", BinValues, 90, 100), 90, 100, "mark-rect");

        // Assert
        Assert.Equal([0.5, 0.5, 30.5, 30.5, 60.5], R(rects.Select(r => r.X)));
        Assert.All(rects, r => Assert.Equal((30.0, 0.0, 100.0), (Math.Round(r.Width, 9), r.Y, r.Height)));

        // Arrange: each bin is 3 px wide in a view of 9 px, thinner than the minBandSize of 5: it is widened around its middle by 1 px on each side
        var thin = Sized("{ \"type\": \"bar\", \"minBandSize\": 5 }", "{ \"x\": " + x + ", \"y\": " + count + " }", BinValues, 9, 100);

        // Assert: -1 to 4, 2 to 7, 5 to 10, all shifted by the half pixel
        var widened = Rectangles(thin, 9, 100);
        Assert.Equal([-0.5, 2.5, 5.5], R(widened.Select(b => b.X)));
        Assert.Equal([5.0, 5.0, 5.0], R(widened.Select(b => b.Width)));

        // Arrange: data that is binned already and has a step instead of an end field: the bins 0 to 10, 10 to 20 and 20 to 30 (the domain is 0 to 30)
        var stepped = Sized("\"bar\"", "{ \"x\": { \"field\": \"s\", \"type\": \"quantitative\", \"bin\": { \"binned\": true, \"step\": 10 } }, \"y\": { \"field\": \"n\", \"type\": \"quantitative\" } }", "[ { \"s\": 0, \"n\": 1 }, { \"s\": 10, \"n\": 1 }, { \"s\": 20, \"n\": 1 } ]", 90, 100);

        // Assert
        Assert.Equal([1.0, 31.0, 61.0], R(Rectangles(stepped, 90, 100).Select(b => b.X)));
        Assert.Equal([29.0, 29.0, 29.0], R(Rectangles(stepped, 90, 100).Select(b => b.Width)));

        // Assert: a circle on a binned field sits at the middle of its bin: 15, 45 and 75 (counts 2, 2 and 1 are at y = 0, 0 and 50)
        var circles = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Histogram("\"circle\""), 90, 100), "mark-circle");
        Assert.Equal([15.0, 45.0, 75.0], R(circles.Select(c => c.X)));
        Assert.Equal([0.0, 0.0, 50.0], R(circles.Select(c => c.Y)));
    }

    /// <summary>
    ///     Validates that data that is binned already (<c>bin: "binned"</c> with <c>x2</c>) is drawn over its ranges: bars with a pixel of spacing, rects
    ///     without, circles in the middle of their ranges.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PreBinnedRangesSpanFromTheStartToTheEndField()
    {
        // Arrange: the ranges 0 to 10 and 10 to 25 give the domain 0 to 25 (no zero rule, no nice), so x(v) = 4 v over 100 px: the edges are at 0, 40 and 100.
        // The tickets (4 and 7) have the domain 0 to 7, so y(v) = 100 - 100 v / 7
        const string values = "[ { \"s\": 0, \"e\": 10, \"n\": 4 }, { \"s\": 10, \"e\": 25, \"n\": 7 } ]";
        const string x = "{ \"field\": \"s\", \"type\": \"quantitative\", \"bin\": \"binned\" }";
        const string y = "{ \"field\": \"n\", \"type\": \"quantitative\" }";
        string Spec(string mark) => Sized(mark, "{ \"x\": " + x + ", \"x2\": { \"field\": \"e\" }, \"y\": " + y + " }", values, 100, 100);

        // Act
        var bars = Rectangles(Spec("\"bar\""), 100, 100);
        var rects = Rectangles(Spec("\"rect\""), 100, 100, "mark-rect");
        var circles = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec("\"circle\""), 100, 100), "mark-circle");

        // Assert
        Assert.Equal([Q(1, 100 - (400.0 / 7), 39, 400.0 / 7), Q(41, 0, 59, 100)], bars.Select(b => Q(b.X, b.Y, b.Width, b.Height)));
        Assert.Equal([0.5, 40.5], R(rects.Select(r => r.X)));
        Assert.Equal([40.0, 60.0], R(rects.Select(r => r.Width)));
        Assert.Equal([20.0, 70.0], R(circles.Select(c => c.X)));
    }

    /// <summary>
    ///     Validates the secondary channels: a bar between two fields, between a field and a constant pixel value and a datum, and a rule along the
    ///     range of one channel or between two points; a row with an invalid end is not drawn.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_SecondaryChannelsSpanARange()
    {
        // Arrange: a horizontal range bar between lo and hi: the domain 2 to 10 includes zero, so x(v) = 10 v; the bands are at 2.5 and 52.5
        const string ranges = "[ { \"k\": \"a\", \"lo\": 2, \"hi\": 6 }, { \"k\": \"b\", \"lo\": 4, \"hi\": 10 } ]";
        const string lo = "{ \"field\": \"lo\", \"type\": \"quantitative\" }";
        var bars = Rectangles(Sized("\"bar\"", "{ \"y\": { \"field\": \"k\", \"type\": \"nominal\" }, \"x\": " + lo + ", \"x2\": { \"field\": \"hi\" } }", ranges, 100, 100), 100, 100);

        // Assert
        Assert.Equal([Q(20, 2.5, 40, 45), Q(40, 52.5, 60, 45)], bars.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Arrange: the second end is a constant pixel position (100 px, a keyword would be "width") or a datum (1 on the domain 0 to 2: 50 px)
        const string single = "[ { \"k\": \"a\", \"lo\": 2 } ]";
        var byValue = Rectangles(Sized("\"bar\"", "{ \"y\": { \"field\": \"k\", \"type\": \"nominal\" }, \"x\": " + lo + ", \"x2\": { \"value\": 50 } }", single, 100, 100), 100, 100);
        var byDatum = Rectangles(Sized("\"bar\"", "{ \"y\": { \"field\": \"k\", \"type\": \"nominal\" }, \"x\": " + lo + ", \"x2\": { \"datum\": 1 } }", single, 100, 100), 100, 100);

        // Assert: x(2) = 100 is one end, 50 the other
        Assert.Equal([Q(50, 5, 50, 90)], byValue.Select(b => Q(b.X, b.Y, b.Width, b.Height)));
        Assert.Equal([Q(50, 5, 50, 90)], byDatum.Select(b => Q(b.X, b.Y, b.Width, b.Height)));

        // Assert: a row whose end is null is not drawn
        var rows = "[ { \"k\": \"a\", \"lo\": 2, \"hi\": 6 }, { \"k\": \"b\", \"lo\": 4, \"hi\": null }, { \"k\": \"c\", \"lo\": 3, \"hi\": 8 } ]";
        Assert.Equal(2, Rectangles(Sized("\"bar\"", "{ \"y\": { \"field\": \"k\", \"type\": \"nominal\" }, \"x\": " + lo + ", \"x2\": { \"field\": \"hi\" } }", rows, 100, 100), 100, 100).Count);

        // Arrange: rules: with y and y2 the rule is vertical from y to y2 at the band centers (25 and 75, the rule bands have no padding): y(v) = 100 - 10 v
        const string vertical = "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"lo\", \"type\": \"quantitative\" }, \"y2\": { \"field\": \"hi\" } }";
        const string horizontal = "{ \"y\": { \"field\": \"k\", \"type\": \"nominal\" }, \"x\": { \"field\": \"lo\", \"type\": \"quantitative\" }, \"x2\": { \"field\": \"hi\" } }";
        var upright = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Sized("\"rule\"", vertical, ranges, 100, 100), 100, 100), "mark-rule");
        var lying = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Sized("\"rule\"", horizontal, ranges, 100, 100), 100, 100), "mark-rule");

        // Assert: from the first field to the second; with x and x2 the rule lies at the band centers from x(lo) to x(hi), x(v) = 10 v
        Assert.Equal([Q(25, 80, 25, 40), Q(75, 60, 75, 0)], upright.Select(r => Q(r.X, r.Y, r.X2, r.Y2)));
        Assert.Equal([Q(20, 25, 60, 25), Q(40, 75, 100, 75)], lying.Select(r => Q(r.X, r.Y, r.X2, r.Y2)));

        // Arrange: a range on both channels is the segment between the points (1, 2) and (5, 8): x(v) = 20 v on 0 to 5, y(v) = 100 - 12.5 v on 0 to 8
        const string both = "{ \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"x2\": { \"field\": \"b\" }, \"y\": { \"field\": \"c\", \"type\": \"quantitative\" }, \"y2\": { \"field\": \"d\" } }";
        var segment = Assert.Single(ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Sized("\"rule\"", both, "[ { \"a\": 1, \"b\": 5, \"c\": 2, \"d\": 8 } ]", 100, 100), 100, 100), "mark-rule"));

        // Assert
        Assert.Equal(Q(20, 75, 100, 0), Q(segment.X, segment.Y, segment.X2, segment.Y2));
    }

    /// <summary>
    ///     Validates that every mark of a stack or a bin is charged to the scene budget before it exists, that a canceled token stops the work, and that
    ///     the fixture of a chart with one row per category is drawn as before (the bars run from zero).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_StackedAndBinnedMarksRespectTheBudgetAndCancellation()
    {
        // Arrange: three bars and the group of the marks need four scene items
        var json = Sized("\"bar\"", "{ \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"n\", \"type\": \"quantitative\" }, \"color\": { \"field\": \"c\", \"type\": \"nominal\" } }", Layers, 100, 100);
        var parts = ChartTestHelpers.Parts(json);

        // Act
        var token = TestContext.Current.CancellationToken;
        var exact = new MarkBuilder(parts.Config, new VegaLiteLimits { MaxSceneItems = 4 }).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, token);
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => new MarkBuilder(parts.Config, new VegaLiteLimits { MaxSceneItems = 3 }).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, token));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Assert
        Assert.Equal(3, exact.Children.Count);
        Assert.Contains("MaxSceneItems", tooMany.Message, StringComparison.Ordinal);
        Assert.Throws<OperationCanceledException>(() => new MarkBuilder(parts.Config).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, canceled.Token));

        // Assert: one row per category stacks to the plain bars from zero (start 0, end the value)
        var plain = Rectangles(BarSpec, 240, 200);
        Assert.Equal([114.0, 144.0, 90.0, 18.0], R(plain.Select(b => b.Y)));
        Assert.Equal([86.0, 56.0, 110.0, 182.0], R(plain.Select(b => b.Height)));
    }

    /// <summary>
    ///     Validates the bars of a log y axis: the domain 28 to 91 is rounded out to 10 to 100 (one decade, which is the 200 pixels of the data rectangle), the
    ///     bars are not stacked and start at the bottom of the data rectangle (the start of the domain, as Vega-Lite's <c>zeroOrMin</c> gives for a log
    ///     scale), and a row whose value the log cannot map (zero) is left out instead of drawing a bar at a position that is not a number.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LogScaleBarsStartAtTheBaselineAndSkipValuesTheLogCannotMap()
    {
        // Arrange: y(v) = 200 - 200 * (log10(v) - 1) on the domain 10 to 100 over 200 px
        var log = BarSpec.Replace("\"y\": { \"field\": \"n\", \"type\": \"quantitative\" }", "\"y\": { \"field\": \"n\", \"type\": \"quantitative\", \"scale\": { \"type\": \"log\" } }", StringComparison.Ordinal);
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(log, 240, 200), "mark-bar");

        // Assert: the tops follow the logarithm and every bar ends at the bottom
        double[] values = [43, 28, 55, 91];
        Assert.Equal(4, bars.Count);
        Assert.Equal(R(values.Select(v => 200 - (200 * (Math.Log10(v) - 1)))), R(bars.Select(b => b.Y)));
        Assert.All(bars, b => Assert.Equal(200, Math.Round(b.Y + b.Height, 9)));

        // Arrange: a fifth category whose value is zero, on an explicit domain 10 to 100 (a zero in the data would otherwise be a domain error)
        var withZero = log
            .Replace("{ \"k\": \"West\", \"n\": 91 }", "{ \"k\": \"West\", \"n\": 91 }, { \"k\": \"None\", \"n\": 0 }", StringComparison.Ordinal)
            .Replace("\"scale\": { \"type\": \"log\" }", "\"scale\": { \"type\": \"log\", \"domain\": [10, 100] }", StringComparison.Ordinal);
        var drawn = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(withZero, 240, 200), "mark-bar");

        // Assert: the zero row has no bar, and the others are where they were
        Assert.Equal(4, drawn.Count);
        Assert.Equal(R(values.Select(v => 200 - (200 * (Math.Log10(v) - 1)))), R(drawn.Select(b => b.Y)));
    }
}
