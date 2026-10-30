using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the Phase 7 channels of <see cref="MarkBuilder"/>: ramp and discretized colors, shape, stroke width, stroke dash, fill and
///     stroke opacity, angle and size on discretizing scales, with every expectation derived by hand from the scale formulas.
/// </summary>
public class MarkBuilderChannelTests
{
    /// <summary>Four rows with the quantitative fields <c>q</c> (10, 20, 30, 40) and <c>v</c> (20, 60, 100, 100) and the category <c>g</c>.</summary>
    private const string Rows = "[ { \"q\": 10, \"v\": 20, \"g\": \"a\" }, { \"q\": 20, \"v\": 60, \"g\": \"b\" }, { \"q\": 30, \"v\": 100, \"g\": \"c\" }, { \"q\": 40, \"v\": 100, \"g\": \"a\" } ]";

    /// <summary>The x channel of the rows.</summary>
    private const string X = "\"x\": { \"field\": \"q\", \"type\": \"quantitative\" }";

    /// <summary>
    ///     Builds a specification of a mark in a 100 by 100 view.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="channels">The JSON text of the channels after x.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string channels, string values = Rows) =>
        "{ \"width\": 100, \"height\": 100, \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": { " + X + ", " + channels + " } }";

    /// <summary>
    ///     Builds the symbols of a mark.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="channels">The JSON text of the channels after x.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <returns>The symbols.</returns>
    private static List<SceneSymbol> Symbols(string mark, string channels, string values = Rows)
    {
        var type = new[] { "point", "circle", "square" }.First(t => mark.Contains(t, StringComparison.Ordinal));
        return ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec(mark, channels, values), 100, 100), "mark-" + type);
    }

    /// <summary>
    ///     Writes a dash pattern as text.
    /// </summary>
    /// <param name="dash">The pattern, or null.</param>
    /// <returns>The numbers separated by commas; <c>solid</c> for null.</returns>
    private static string D(IReadOnlyList<double>? dash) => dash is null ? "solid" : string.Join(",", dash);

    /// <summary>
    ///     Rounds numbers to nine decimals.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The rounded numbers.</returns>
    private static List<double> R(IEnumerable<double> values) => [.. values.Select(v => Math.Round(v, 9))];

    /// <summary>
    ///     Validates a sequential color scale with an explicit range: the domain is the extent of the data (a color has no zero rule), and the
    ///     colors are the interpolation between the two range colors, as a fill for a filled mark and as a stroke for an unfilled one.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_SequentialColorInterpolatesTheRange()
    {
        // Arrange: the domain is 20 to 100, so v = 20, 60, 100, 100 sit at 0, 0.5, 1, 1 of the black to white ramp (127.5 rounds up to 128)
        const string color = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"range\": [\"#000000\", \"#ffffff\"] } }";

        // Act
        var filled = Symbols("{ \"type\": \"point\", \"filled\": true }", color);
        var outlined = Symbols("\"point\"", color);

        // Assert
        Assert.Equal(["#000000", "#808080", "#ffffff", "#ffffff"], filled.Select(s => s.Fill));
        Assert.All(filled, s => Assert.Null(s.Stroke));
        Assert.Equal(["#000000", "#808080", "#ffffff", "#ffffff"], outlined.Select(s => s.Stroke));
        Assert.All(outlined, s => Assert.Equal("transparent", s.Fill));
    }

    /// <summary>
    ///     Validates the default continuous color range (<c>ramp</c>, the blues scheme) and the domain options: <c>reverse</c> swaps the ends,
    ///     <c>domainMid</c> makes the scale diverging, and a value that is not a number drops the row.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_DefaultRampReverseAndDivergingColors()
    {
        // Arrange
        const string plain = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\" }";
        const string reversed = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"reverse\": true } }";
        const string diverging = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"domainMid\": 60, \"range\": [\"#ff0000\", \"#ffffff\", \"#0000ff\"] } }";

        // Act
        var ramp = Symbols("{ \"type\": \"circle\" }", plain, "[ { \"q\": 1, \"v\": 0 }, { \"q\": 2, \"v\": 10 } ]");
        var back = Symbols("{ \"type\": \"circle\" }", reversed, "[ { \"q\": 1, \"v\": 0 }, { \"q\": 2, \"v\": 10 } ]");
        var split = Symbols("{ \"type\": \"circle\" }", diverging);
        var dropped = Symbols("{ \"type\": \"circle\" }", plain, "[ { \"q\": 1, \"v\": 0 }, { \"q\": 2, \"v\": \"n/a\" }, { \"q\": 3, \"v\": null }, { \"q\": 4, \"v\": 10 } ]");

        // Assert: the ends of the blues ramp of Vega (first color, last color), the lightest first
        Assert.Equal(["#cfe1f2", "#0a4a90"], ramp.Select(s => s.Fill));
        Assert.Equal(["#0a4a90", "#cfe1f2"], back.Select(s => s.Fill));

        // Assert: the domain 20 to 100 with the middle 60 gives red, white (the middle), then blue at the end; 100 and 100 are blue
        Assert.Equal(["#ff0000", "#ffffff", "#0000ff", "#0000ff"], split.Select(s => s.Fill));

        // Assert: the rows without a number are not drawn
        Assert.Equal(2, dropped.Count);
        Assert.Equal(["#cfe1f2", "#0a4a90"], dropped.Select(s => s.Fill));
    }

    /// <summary>
    ///     Validates the discretizing color scales: quantize cuts the domain into equal parts, quantile into parts of equal counts, threshold at
    ///     the given values; each row gets the color of its part, and a nominal color keeps the category palette.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_DiscretizedColorsGiveTheColorOfTheClass()
    {
        // Arrange: three classes red, green, blue
        const string range = "\"range\": [\"#ff0000\", \"#00ff00\", \"#0000ff\"]";
        const string values = "[ { \"q\": 1, \"v\": 0 }, { \"q\": 2, \"v\": 29 }, { \"q\": 3, \"v\": 31 }, { \"q\": 4, \"v\": 59 }, { \"q\": 5, \"v\": 61 }, { \"q\": 6, \"v\": 90 } ]";
        var quantize = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"type\": \"quantize\", \"domain\": [0, 90], " + range + " } }";
        var threshold = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"type\": \"threshold\", \"domain\": [30, 60], " + range + " } }";

        // The quantile of the eight values 1..8 into two classes has the single threshold 4.5 (the median)
        const string eight = "[ { \"q\": 1, \"v\": 1 }, { \"q\": 2, \"v\": 2 }, { \"q\": 3, \"v\": 3 }, { \"q\": 4, \"v\": 4 }, { \"q\": 5, \"v\": 5 }, { \"q\": 6, \"v\": 6 }, { \"q\": 7, \"v\": 7 }, { \"q\": 8, \"v\": 8 } ]";
        const string quantile = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"type\": \"quantile\", \"range\": [\"#111111\", \"#eeeeee\"] } }";

        // Act
        var cut = Symbols("{ \"type\": \"circle\" }", quantize, values);
        var thresholds = Symbols("{ \"type\": \"circle\" }", threshold, values);
        var halves = Symbols("{ \"type\": \"circle\" }", quantile, eight);

        // Assert: quantize thresholds at 30 and 60, so 0 and 29 are red, 31 and 59 green, 61 and 90 blue
        Assert.Equal(["#ff0000", "#ff0000", "#00ff00", "#00ff00", "#0000ff", "#0000ff"], cut.Select(s => s.Fill));

        // Assert: threshold uses the same cut points
        Assert.Equal(cut.Select(s => s.Fill), thresholds.Select(s => s.Fill));

        // Assert: a value on a threshold belongs to the class above it
        var edge = Symbols("{ \"type\": \"circle\" }", threshold, "[ { \"q\": 1, \"v\": 30 }, { \"q\": 2, \"v\": 60 } ]");
        Assert.Equal(["#00ff00", "#0000ff"], edge.Select(s => s.Fill));

        // Assert: quantile halves
        Assert.Equal(["#111111", "#111111", "#111111", "#111111", "#eeeeee", "#eeeeee", "#eeeeee", "#eeeeee"], halves.Select(s => s.Fill));
    }

    /// <summary>
    ///     Validates the symbol shapes: the default sequence of the shape scale, a custom range, a constant value, a shape that is a custom path
    ///     (kept as given; the painter draws a circle), and the mark property.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ShapeChannelChoosesTheSymbolOfEachRow()
    {
        // Arrange
        const string field = "\"shape\": { \"field\": \"g\", \"type\": \"nominal\" }";
        const string custom = "\"shape\": { \"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"range\": [\"diamond\", \"cross\", \"M0,0L1,1\"] } }";
        const string constant = "\"shape\": { \"value\": \"triangle-down\" }";

        // Act
        var byField = Symbols("\"point\"", field);
        var byRange = Symbols("\"point\"", custom);
        var fixedShape = Symbols("\"point\"", constant);
        var fromMark = Symbols("{ \"type\": \"point\", \"shape\": \"wedge\" }", "\"opacity\": { \"value\": 1 }");
        var circles = Symbols("\"circle\"", field);

        // Assert: the categories a, b, c take circle, square, triangle-up (Vega's symbol range) and the repeated a takes circle again
        Assert.Equal(["circle", "square", "triangle-up", "circle"], byField.Select(s => s.Shape));
        Assert.Equal(["diamond", "cross", "M0,0L1,1", "diamond"], byRange.Select(s => s.Shape));
        Assert.All(fixedShape, s => Assert.Equal("triangle-down", s.Shape));
        Assert.All(fromMark, s => Assert.Equal("wedge", s.Shape));

        // Assert: a circle mark has no shape channel
        Assert.All(circles, s => Assert.Equal("circle", s.Shape));
    }

    /// <summary>
    ///     Validates that every symbol name that Vega draws is passed to the symbol as given.
    /// </summary>
    /// <param name="shape">The shape name.</param>
    [Theory]
    [InlineData("circle")]
    [InlineData("square")]
    [InlineData("cross")]
    [InlineData("diamond")]
    [InlineData("triangle-up")]
    [InlineData("triangle-down")]
    [InlineData("triangle-left")]
    [InlineData("triangle-right")]
    [InlineData("arrow")]
    [InlineData("wedge")]
    [InlineData("triangle")]
    [InlineData("stroke")]
    public void MarkBuilder_Build_EveryShapeNameReachesTheSymbol(string shape)
    {
        // Act
        var symbols = Symbols("\"point\"", "\"shape\": { \"value\": \"" + shape + "\" }");

        // Assert
        Assert.All(symbols, s => Assert.Equal(shape, s.Shape));
        Assert.True(ScenePainter.IsSymbolShape(shape));
    }

    /// <summary>
    ///     Validates the stroke width channel: a scaled field (the range 1 to 4 of Vega-Lite's minimum and maximum stroke width, from zero), a
    ///     constant value and the mark property.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_StrokeWidthChannelScalesAndOverridesTheMark()
    {
        // Act: only a size scale starts at zero (Vega-Lite's zero rule), so the domain of q = 10, 20, 30, 40 is 10 to 40 and the range 1 to 4
        // gives 1, 2, 3, 4
        var scaled = Symbols("\"point\"", "\"strokeWidth\": { \"field\": \"q\", \"type\": \"quantitative\" }");
        var constant = Symbols("{ \"type\": \"point\", \"strokeWidth\": 7 }", "\"strokeWidth\": { \"value\": 5 }");
        var fromMark = Symbols("{ \"type\": \"point\", \"strokeWidth\": 7 }", "\"opacity\": { \"value\": 1 }");
        var discrete = Symbols("\"point\"", "\"strokeWidth\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"type\": \"quantize\", \"domain\": [0, 120] } }");

        // Assert
        Assert.Equal([1.0, 2.0, 3.0, 4.0], R(scaled.Select(s => s.StrokeWidth!.Value)));
        Assert.All(constant, s => Assert.Equal(5.0, s.StrokeWidth));
        Assert.All(fromMark, s => Assert.Equal(7.0, s.StrokeWidth));

        // Assert: with the default two-entry range of 1 to 4 a quantize scale has two classes, cut at 60 (20 is below, 60 is on the threshold)
        Assert.Equal([1.0, 4.0, 4.0, 4.0], R(discrete.Select(s => s.StrokeWidth!.Value)));
    }

    /// <summary>
    ///     Validates the stroke dash channel: the five patterns of the scale by category, a custom range, a constant pattern and the mark's
    ///     pattern; on a rule and a bar too, while a rect does not draw it.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_StrokeDashChannelChoosesThePatternOfEachRow()
    {
        // Arrange
        const string field = "\"strokeDash\": { \"field\": \"g\", \"type\": \"nominal\" }";
        const string custom = "\"strokeDash\": { \"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"range\": [[8, 2], [3], [1, 1, 1]] } }";
        const string constant = "\"strokeDash\": { \"value\": [3, 1] }";

        // Act
        var byField = Symbols("\"point\"", field);
        var byRange = Symbols("\"point\"", custom);
        var fixedDash = Symbols("\"point\"", constant);
        var fromMark = Symbols("{ \"type\": \"point\", \"strokeDash\": [5, 5] }", "\"opacity\": { \"value\": 1 }");
        var plain = Symbols("\"point\"", "\"opacity\": { \"value\": 1 }");
        var rules = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("\"rule\"", field), 100, 100), "mark-rule");
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", field), 100, 100), "mark-bar");
        var rects = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"rect\"", field), 100, 100), "mark-rect");

        // Assert: the categories a, b, c take the first three of Vega-Lite's five patterns
        Assert.Equal(["1,0", "4,2", "2,1", "1,0"], byField.Select(s => D(s.StrokeDash)));
        Assert.Equal(["8,2", "3", "1,1,1", "8,2"], byRange.Select(s => D(s.StrokeDash)));
        Assert.All(fixedDash, s => Assert.Equal("3,1", D(s.StrokeDash)));
        Assert.All(fromMark, s => Assert.Equal("5,5", D(s.StrokeDash)));
        Assert.All(plain, s => Assert.Equal("solid", D(s.StrokeDash)));
        Assert.Equal(["1,0", "4,2", "2,1", "1,0"], rules.Select(s => D(s.StrokeDash)));
        Assert.Equal(["1,0", "4,2", "2,1", "1,0"], bars.Select(s => D(s.StrokeDash)));

        // Assert: a rect does not draw a dash channel, so it keeps the solid line
        Assert.NotEmpty(rects);
        Assert.All(rects, s => Assert.Equal("solid", D(s.StrokeDash)));
    }

    /// <summary>
    ///     Validates the fill opacity and stroke opacity channels: scaled fields (0.3 to 0.8 from the extent, no zero rule), constant values and
    ///     the mark properties, independent of the opacity.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_FillAndStrokeOpacityChannelsScaleIndependently()
    {
        // Act: v = 20, 60, 100, 100 on the extent 20 to 100 gives 0.3 + 0.5 (v - 20) / 80
        var fill = Symbols("\"point\"", "\"fillOpacity\": { \"field\": \"v\", \"type\": \"quantitative\" }");
        var stroke = Symbols("\"point\"", "\"strokeOpacity\": { \"field\": \"v\", \"type\": \"quantitative\" }");
        var constants = Symbols("\"point\"", "\"fillOpacity\": { \"value\": 0.4 }, \"strokeOpacity\": { \"value\": 0.6 }");
        var fromMark = Symbols("{ \"type\": \"point\", \"fillOpacity\": 0.2, \"strokeOpacity\": 0.9 }", "\"size\": { \"value\": 30 }");

        // Assert
        Assert.Equal([0.3, 0.55, 0.8, 0.8], R(fill.Select(s => s.FillOpacity)));
        Assert.All(fill, s => Assert.Equal(1.0, s.StrokeOpacity));
        Assert.Equal([0.3, 0.55, 0.8, 0.8], R(stroke.Select(s => s.StrokeOpacity)));
        Assert.All(stroke, s => Assert.Equal(1.0, s.FillOpacity));
        Assert.All(constants, s => Assert.Equal((0.4, 0.6), (s.FillOpacity, s.StrokeOpacity)));
        Assert.All(fromMark, s => Assert.Equal((0.2, 0.9), (s.FillOpacity, s.StrokeOpacity)));

        // Assert: a rule carries the stroke opacity and no fill
        var rules = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("\"rule\"", "\"strokeOpacity\": { \"value\": 0.25 }"), 100, 100), "mark-rule");
        Assert.All(rules, r => Assert.Equal(0.25, r.StrokeOpacity));
    }

    /// <summary>
    ///     Validates the angle channel: degrees from a scaled field (0 to 360 over the domain), a constant value, and the mark property; the
    ///     channel is ignored by a mark that has no angle.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_AngleChannelRotatesTheSymbols()
    {
        // Act: the domain 0 to 100 maps to 0 to 360 degrees; v = 20, 60, 100, 100 give 72, 216, 360, 360
        var scaled = Symbols("\"point\"", "\"angle\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"domain\": [0, 100] } }");
        var constant = Symbols("\"point\"", "\"angle\": { \"value\": 45 }");
        var fromMark = Symbols("{ \"type\": \"square\", \"angle\": 30 }", "\"opacity\": { \"value\": 1 }");
        var ticks = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"tick\"", "\"angle\": { \"value\": 45 }"), 100, 100), "mark-tick");
        var invalid = Symbols("\"point\"", "\"angle\": { \"field\": \"v\", \"type\": \"quantitative\" }", "[ { \"q\": 1, \"v\": 0 }, { \"q\": 2, \"v\": \"n/a\" } ]");

        // Assert
        Assert.Equal([72.0, 216.0, 360.0, 360.0], R(scaled.Select(s => s.Angle)));
        Assert.All(constant, s => Assert.Equal(45.0, s.Angle));
        Assert.All(fromMark, s => Assert.Equal(30.0, s.Angle));
        Assert.Equal(4, ticks.Count);
        Assert.Single(invalid);
    }

    /// <summary>
    ///     Validates the size channel on the discretizing scales: quantize and threshold spread the size range evenly over their classes, and an
    ///     ordinal scale does as well.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_SizeOnDiscretizingScalesUsesTheClassOfTheValue()
    {
        // Arrange: the point size range is 4 to 361; four classes give 4, 123, 242 and 361
        const string values = "[ { \"q\": 1, \"v\": 10, \"g\": \"a\" }, { \"q\": 2, \"v\": 30, \"g\": \"b\" }, { \"q\": 3, \"v\": 60, \"g\": \"c\" }, { \"q\": 4, \"v\": 90, \"g\": \"a\" } ]";
        const string quantize = "\"size\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"type\": \"quantize\", \"domain\": [0, 100] } }";
        const string threshold = "\"size\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"type\": \"threshold\", \"domain\": [20, 50] } }";
        const string ordinal = "\"size\": { \"field\": \"g\", \"type\": \"ordinal\" }";

        // Act
        var quantized = Symbols("\"point\"", quantize, values);
        var byThreshold = Symbols("\"point\"", threshold, values);
        var ordered = Symbols("\"point\"", ordinal, values);

        // Assert: thresholds 25, 50 and 75 give the classes 0, 1, 2, 3
        Assert.Equal([4.0, 123.0, 242.0, 361.0], R(quantized.Select(s => s.Size)));

        // Assert: three classes (two thresholds) spread 4 to 361 as 4, 182.5, 361
        Assert.Equal([4.0, 182.5, 361.0, 361.0], R(byThreshold.Select(s => s.Size)));

        // Assert: the ordinal scale gives its range positions: three categories, two entries (4 and 361), repeated
        Assert.Equal(quantized.Count, ordered.Count);
        Assert.Equal(ordered[0].Size, ordered[3].Size);
    }

    /// <summary>
    ///     Validates the colors on the other marks: a bar and a rect take a ramp as the fill, a rule and a tick the stroke.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_RampColorsPaintBarsRectsRulesAndTicks()
    {
        // Arrange
        const string color = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"range\": [\"#000000\", \"#ffffff\"] } }";

        // Act
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Spec("\"bar\"", color), 100, 100), "mark-bar");
        var rules = ChartTestHelpers.Of<SceneRule>(ChartTestHelpers.Marks(Spec("\"rule\"", color), 100, 100), "mark-rule");

        // Assert
        Assert.Equal(["#000000", "#808080", "#ffffff", "#ffffff"], bars.Select(b => b.Fill));
        Assert.Equal(["#000000", "#808080", "#ffffff", "#ffffff"], rules.Select(r => r.Stroke));
    }

    /// <summary>
    ///     Validates that a row with an invalid value for any channel that has a continuous domain is not drawn, while a category that is not in the
    ///     domain of an ordinal scale does not drop the row.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InvalidValuesOfContinuousChannelsDropTheRow()
    {
        // Arrange
        const string values = "[ { \"q\": 1, \"v\": 1, \"w\": 1 }, { \"q\": 2, \"v\": \"x\", \"w\": 1 }, { \"q\": 3, \"v\": 3, \"w\": \"y\" }, { \"q\": 4, \"v\": 4, \"w\": 4 } ]";
        const string channels = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\" }, \"strokeWidth\": { \"field\": \"w\", \"type\": \"quantitative\" }";

        // Act
        var symbols = Symbols("\"point\"", channels, values);

        // Assert: rows 2 and 3 each have one invalid channel
        Assert.Equal(2, symbols.Count);
    }

    /// <summary>
    ///     Validates the limit on the length of a dash pattern: 64 entries are drawn, more are a located render error, for a constant pattern of
    ///     the mark, of a channel and of a scale range.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LongDashPatternsExceedTheLimit()
    {
        // Arrange
        var ok = "[" + string.Join(",", Enumerable.Repeat("1", 64)) + "]";
        var tooLong = "[" + string.Join(",", Enumerable.Repeat("1", 65)) + "]";

        // Act
        var accepted = Symbols("{ \"type\": \"point\", \"strokeDash\": " + ok + " }", "\"opacity\": { \"value\": 1 }");
        var mark = Assert.Throws<VegaLiteRenderException>(() => Symbols("{ \"type\": \"point\", \"strokeDash\": " + tooLong + " }", "\"opacity\": { \"value\": 1 }"));
        var channel = Assert.Throws<VegaLiteRenderException>(() => Symbols("\"point\"", "\"strokeDash\": { \"value\": " + tooLong + " }"));
        var range = Assert.Throws<VegaLiteRenderException>(() => Symbols("\"point\"", "\"strokeDash\": { \"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"range\": [" + tooLong + "] } }"));

        // Assert
        Assert.All(accepted, s => Assert.Equal(64, s.StrokeDash!.Count));
        Assert.All([mark, channel, range], ex => Assert.Contains("the limit of 64", ex.Message, StringComparison.Ordinal));
        Assert.All([mark, channel, range], ex => Assert.Equal("/mark", ex.Location.Pointer));
    }

    /// <summary>
    ///     Validates that the build polls the cancellation token for rows that use the new channels and that the scene budget is charged before
    ///     each item is created.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_NewChannelsHonorCancellationAndTheSceneBudget()
    {
        // Arrange
        var json = Spec("\"point\"", "\"shape\": { \"field\": \"g\", \"type\": \"nominal\" }, \"color\": { \"field\": \"v\", \"type\": \"quantitative\" }");
        var parts = ChartTestHelpers.Parts(json);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var tight = new SceneBudget(2);

        // Act and assert
        Assert.Throws<OperationCanceledException>(() => new MarkBuilder(parts.Config).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, canceled.Token));
        var ex = Assert.Throws<VegaLiteRenderException>(() => new MarkBuilder(parts.Config, budget: tight).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, TestContext.Current.CancellationToken));
        Assert.Contains("MaxSceneItems", ex.Message, StringComparison.Ordinal);
        Assert.Equal(3, tight.Count);
    }

    /// <summary>The channels of the determinism test: every new channel at once.</summary>
    private const string Everything =
        "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"scale\": { \"type\": \"quantile\", \"scheme\": \"viridis\" } }, \"shape\": { \"field\": \"g\", \"type\": \"nominal\" }, \"strokeDash\": { \"field\": \"g\", \"type\": \"nominal\" }, \"angle\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"strokeWidth\": { \"field\": \"v\", \"type\": \"quantitative\" }";

    /// <summary>
    ///     Validates that building the same chart twice gives the same items, in the same order, with the same paint.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_NewChannelsAreDeterministic()
    {
        // Act
        var first = Symbols("\"point\"", Everything);
        var second = Symbols("\"point\"", Everything);

        // Assert
        string Describe(SceneSymbol s) => $"{s.Fill}|{s.Stroke}|{s.Shape}|{D(s.StrokeDash)}|{s.Angle}|{s.StrokeWidth}|{s.X}|{s.Y}";
        Assert.Equal(first.Select(Describe), second.Select(Describe));
        Assert.Equal(4, first.Count);
        Assert.Equal(3, first.Select(s => s.Shape).Distinct().Count());
    }

    /// <summary>Eight rows with the numbers <c>v</c> from 3 to 97 (the bins 0-20 to 80-100 hold 2, 1, 2, 1 and 2 of them) and <c>q</c> from 1 to 8.</summary>
    private const string RowsBins = "[ { \"q\": 1, \"v\": 3 }, { \"q\": 2, \"v\": 12 }, { \"q\": 3, \"v\": 27 }, { \"q\": 4, \"v\": 41 }, { \"q\": 5, \"v\": 58 }, { \"q\": 6, \"v\": 66 }, { \"q\": 7, \"v\": 83 }, { \"q\": 8, \"v\": 97 } ]";

    /// <summary>
    ///     Validates the marks of a binned color: a bin-ordinal scale gives each mark the color of the bin of its value (the first two rows share the first
    ///     bin, the fourth and fifth the third, the last two the fifth), as a fill for a filled mark and a stroke for an unfilled one.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_BinnedColorGivesEachMarkTheColorOfItsBin()
    {
        // Arrange: the values 3 to 97 have the six-bin default step 20 (edges 0 to 100), five colors of the range
        const string color = "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"bin\": true, \"scale\": { \"range\": [\"#ff0000\", \"#ffa500\", \"#ffff00\", \"#00ff00\", \"#0000ff\"] } }";

        // Act
        var filled = Symbols("{ \"type\": \"circle\" }", color, RowsBins);
        var outlined = Symbols("\"point\"", color, RowsBins);

        // Assert
        string[] expected = ["#ff0000", "#ff0000", "#ffa500", "#ffff00", "#ffff00", "#00ff00", "#0000ff", "#0000ff"];
        Assert.Equal(expected, filled.Select(s => s.Fill));
        Assert.Equal(expected, outlined.Select(s => s.Stroke));
        Assert.All(outlined, s => Assert.Equal("transparent", s.Fill));
    }

    /// <summary>
    ///     Validates the size of a binned field: the marks use the start of the bin as the value of a linear scale over the bins (0 to 100, with the zero rule
    ///     of a size), so the values 3 and 12 both give the size at 0, 27 the size at 20 and so on, over the size range 4 to 361 of a point.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_BinnedSizeUsesTheStartOfTheBin()
    {
        // Act
        var points = Symbols("\"point\"", "\"size\": { \"field\": \"v\", \"type\": \"quantitative\", \"bin\": true }", RowsBins);

        // Assert: size = 4 + 357 * start / 100 for the starts 0, 0, 20, 40, 40, 60, 80, 80
        Assert.Equal(R(new[] { 0.0, 0, 20, 40, 40, 60, 80, 80 }.Select(start => 4 + (357 * start / 100))), R(points.Select(s => s.Size)));
    }

    /// <summary>
    ///     Validates a histogram whose bars are colored by the bin they count: the bin of <c>x</c> and the bin of <c>color</c> are the same, so the five bars
    ///     take the five colors in bin order.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_HistogramBarsTakeTheColorOfTheirBin()
    {
        // Arrange
        const string encoding = "\"x\": { \"field\": \"v\", \"type\": \"quantitative\", \"bin\": { \"maxbins\": 5 } }, \"y\": { \"aggregate\": \"count\", \"type\": \"quantitative\" }, \"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"bin\": { \"maxbins\": 5 }, \"scale\": { \"range\": [\"#ff0000\", \"#ffa500\", \"#ffff00\", \"#00ff00\", \"#0000ff\"] } }";
        var json = "{ \"width\": 100, \"height\": 100, \"data\": { \"values\": " + RowsBins + " }, \"mark\": \"bar\", \"encoding\": { " + encoding + " } }";

        // Act
        var bars = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(json, 100, 100), "mark-bar");

        // Assert: five bars (the five bins), in bin order, filled with the colors of the bins; the counts 2, 1, 2, 1, 2 give the heights on the domain 0 to 2, and each
        // bar spans its bin of 20 pixels less the bin spacing of 1 (the bar starts one pixel in and ends 20 - 1 = 19 pixels wide)
        Assert.Equal(["#ff0000", "#ffa500", "#ffff00", "#00ff00", "#0000ff"], bars.Select(b => b.Fill));
        Assert.Equal(R([100.0, 50, 100, 50, 100]), R(bars.Select(b => b.Height)));
        Assert.Equal(R([1.0, 21, 41, 61, 81]), R(bars.Select(b => b.X)));
        Assert.Equal(R([19.0, 19, 19, 19, 19]), R(bars.Select(b => b.Width)));
    }

    /// <summary>
    ///     Validates that the marks of a binned color are charged to the scene budget before they are created and that a canceled token stops the build.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_BinnedColorMarksAreChargedAndCancellable()
    {
        // Arrange
        var json = Spec("\"point\"", "\"color\": { \"field\": \"v\", \"type\": \"quantitative\", \"bin\": true }", RowsBins);
        var parts = ChartTestHelpers.Parts(json);
        var builder = new MarkBuilder(parts.Config, budget: new SceneBudget(4), sourceName: parts.Tree.SourceName);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert: one item for the group and eight marks exceed a budget of four; a canceled token stops the marks
        var ex = Assert.Throws<VegaLiteRenderException>(() => builder.Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, TestContext.Current.CancellationToken));
        Assert.Contains("MaxSceneItems", ex.Message, StringComparison.Ordinal);
        Assert.ThrowsAny<OperationCanceledException>(() =>
            new MarkBuilder(parts.Config, sourceName: parts.Tree.SourceName).Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, canceled.Token));
    }
}
