using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the text mark of <see cref="MarkBuilder"/>: the position and the polar offset, the paint, the font properties, the formatted text of a field, the lines, and the limits
///     with which the characters and the lines of the labels are charged. The expectations are derived by hand from the scale formulas, the d3 formats and the rules of Vega's text mark; no
///     text is measured, so nothing depends on a font.
/// </summary>
public class MarkBuilderTextTests
{
    /// <summary>The first color of the tableau10 palette, which is the default color of a color scale.</summary>
    private const string Blue = "#4c78a8";

    /// <summary>The second color of the tableau10 palette.</summary>
    private const string Orange = "#f58518";

    /// <summary>A text field that is a nominal field.</summary>
    private const string TextField = """{ "field": "t", "type": "nominal" }""";

    /// <summary>The tolerance of the comparisons of positions that go through trigonometry.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    ///     Builds a specification of a mark in a 300 by 200 view.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="config">The JSON text of the <c>config</c> object, or null.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string encoding, string values, string? config = null) =>
        "{ \"width\": 300, \"height\": 200, \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": " + encoding
        + (config is null ? string.Empty : ", \"config\": " + config) + " }";

    /// <summary>
    ///     Builds the texts of a text mark whose encoding is only a <c>text</c> channel.
    /// </summary>
    /// <param name="text">The JSON text of the <c>text</c> channel definition.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="config">The JSON text of the <c>config</c> object, or null.</param>
    /// <returns>The text items in drawing order.</returns>
    private static List<SceneText> Texts(string text, string values, string mark = "\"text\"", string? config = null) =>
        ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart(mark, "{ \"text\": " + text + " }", values, config), 300, 200), "mark-text");

    /// <summary>
    ///     Gets the labels of a text mark whose encoding is only a <c>text</c> channel.
    /// </summary>
    /// <param name="text">The JSON text of the <c>text</c> channel definition.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="config">The JSON text of the <c>config</c> object, or null.</param>
    /// <returns>The text of each item.</returns>
    private static List<string> Labels(string text, string values, string mark = "\"text\"", string? config = null) =>
        [.. Texts(text, values, mark, config).Select(t => t.Text)];

    /// <summary>
    ///     Asserts the position of a text.
    /// </summary>
    /// <param name="item">The text.</param>
    /// <param name="x">The expected x coordinate.</param>
    /// <param name="y">The expected y coordinate.</param>
    private static void AssertAt(SceneText item, double x, double y)
    {
        Assert.Equal(x, item.X, Tolerance);
        Assert.Equal(y, item.Y, Tolerance);
    }

    /// <summary>
    ///     Validates the position of the items (the scale of x and y, the middle of the data rectangle for an absent channel), the role and the defaults of Vega's text mark (centered, middle
    ///     baseline, 11 pixels, sans-serif, black) and the paint from the color and opacity channels.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextHasPositionDefaultsAndPaint()
    {
        // Arrange: x domain [0, 2] (x(v) = 150 v), y domain [0, 10] (y(v) = 200 - 20 v): the labels sit at (0, 200) and (300, 0)
        const string rows = """[ { "x": 0, "y": 0, "t": "a", "g": "u" }, { "x": 2, "y": 10, "t": "b", "g": "v" } ]""";
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "text": { "field": "t", "type": "nominal" } }
            """;

        // Act
        var marks = ChartTestHelpers.Marks(Chart("\"text\"", encoding, rows), 300, 200);
        var texts = ChartTestHelpers.Of<SceneText>(marks, "mark-text");

        // Assert: one item for each row at its position, with the defaults of the text mark
        Assert.Equal(2, texts.Count);
        Assert.Equal(["a", "b"], texts.Select(t => t.Text));
        AssertAt(texts[0], 0, 200);
        AssertAt(texts[1], 300, 0);
        Assert.All(texts, t =>
        {
            Assert.Equal(("center", "middle", 11.0, "sans-serif"), (t.Align, t.Baseline, t.FontSize, t.FontFamily));
            Assert.Equal(("black", (string?)null, 1.0), (t.Fill, t.Stroke, t.Opacity));
            Assert.Equal((0.0, 0.0, 0.0, 0.0), (t.Dx, t.Dy, t.Angle, t.Limit));
            Assert.Equal(((double?)null, "\u2026", (string?)null, (string?)null), (t.LineHeight, t.Ellipsis, t.FontWeight, t.FontStyle));
        });
        Assert.Equal(2, marks.Children.Count);

        // Act / Assert: without x and y the label sits in the middle of the 300 by 200 data rectangle
        var middle = Assert.Single(Texts(TextField, """[ { "t": "only" } ]"""));
        AssertAt(middle, 150, 100);

        // Act / Assert: a color field drives the fill through its scale, a color value and an opacity value are used as they are
        const string colored = """{ "color": { "field": "g", "type": "nominal" }, "text": { "field": "t", "type": "nominal" } }""";
        var byColor = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", colored, rows), 300, 200), "mark-text");
        Assert.Equal([Blue, Orange], byColor.Select(t => t.Fill));
        const string constants = """{ "color": { "value": "red" }, "opacity": { "value": 0.5 }, "text": { "field": "t", "type": "nominal" } }""";
        var fixedPaint = Assert.Single(ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", constants, """[ { "t": "a" } ]"""), 300, 200), "mark-text"));
        Assert.Equal(("red", 0.5), (fixedPaint.Fill, fixedPaint.Opacity));
    }

    /// <summary>
    ///     Validates that <c>dx</c>, <c>dy</c>, <c>angle</c> (the property and the channel), <c>font</c>, <c>fontSize</c>, <c>fontStyle</c>, <c>fontWeight</c>, <c>limit</c>, <c>ellipsis</c>,
    ///     <c>lineHeight</c>, <c>align</c> and <c>baseline</c> of the mark reach the item, with the precedence mark definition, configuration, default.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextOffsetsAnglesAndFontPropertiesReachTheItem()
    {
        // Arrange
        const string mark = """
            { "type": "text", "dx": 3, "dy": -4, "angle": 30, "font": "Georgia", "fontSize": 20, "fontStyle": "italic", "fontWeight": "bold", "limit": 50,
              "ellipsis": "...", "lineHeight": 14, "align": "left", "baseline": "top" }
            """;

        // Act
        var item = Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", mark));

        // Assert: every property is on the item as the mark gives it (the position is not moved by dx and dy: the item keeps them apart)
        AssertAt(item, 150, 100);
        Assert.Equal((3.0, -4.0, 30.0), (item.Dx, item.Dy, item.Angle));
        Assert.Equal(("Georgia", 20.0, "italic", "bold"), (item.FontFamily, item.FontSize, item.FontStyle, item.FontWeight));
        Assert.Equal((50.0, "...", 14.0), (item.Limit, item.Ellipsis, item.LineHeight));
        Assert.Equal(("left", "top"), (item.Align, item.Baseline));

        // Assert: a numeric font weight is its text, an unknown alignment is left and an unknown baseline is alphabetic (the names of Vega's text mark)
        var odd = Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "fontWeight": 700, "align": "diagonal", "baseline": "sideways" }"""));
        Assert.Equal(("700", "left", "alphabetic"), (odd.FontWeight, odd.Align, odd.Baseline));

        // Assert: the configuration of the text mark supplies what the mark does not say, and the mark wins
        const string config = """{ "text": { "fontSize": 13, "align": "right", "baseline": "bottom", "dx": 2, "dy": 5 } }""";
        var configured = Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", "\"text\"", config));
        Assert.Equal((13.0, "right", "bottom", 2.0, 5.0), (configured.FontSize, configured.Align, configured.Baseline, configured.Dx, configured.Dy));
        var overridden = Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "align": "left", "dx": 7 }""", config));
        Assert.Equal(("left", "bottom", 7.0, 5.0), (overridden.Align, overridden.Baseline, overridden.Dx, overridden.Dy));

        // Assert: an angle channel replaces the angle of the mark
        const string angled = """{ "angle": { "value": 45 }, "text": { "field": "t", "type": "nominal" } }""";
        var rotated = Assert.Single(ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("{ \"type\": \"text\", \"angle\": 30 }", angled, """[ { "t": "a" } ]"""), 300, 200), "mark-text"));
        Assert.Equal(45.0, rotated.Angle);
    }

    /// <summary>
    ///     Validates that the <c>size</c> channel is the font size through the size scale (8 to 40 pixels), that a constant size, the <c>fontSize</c>, the <c>size</c> property and a row-bound
    ///     <c>size</c> expression give the font size in the documented order, and that the default is 11.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextSizeChannelMapsToTheFontSize()
    {
        // Arrange: the size domain is [0, 10] (zero) and the range [8, 40]: 5 is halfway (24) and 10 is the end (40)
        const string rows = """[ { "s": 5, "t": "a" }, { "s": 10, "t": "b" } ]""";
        const string encoding = """{ "size": { "field": "s", "type": "quantitative" }, "text": { "field": "t", "type": "nominal" } }""";

        // Act
        var texts = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", encoding, rows), 300, 200), "mark-text");

        // Assert
        Assert.Equal([24.0, 40.0], texts.Select(t => t.FontSize).ToArray());

        // Assert: a size value is the font size; the size channel wins over the fontSize of the mark
        const string constant = """{ "size": { "value": 15 }, "text": { "field": "t", "type": "nominal" } }""";
        Assert.Equal([15.0, 15.0], ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", constant, rows), 300, 200), "mark-text").Select(t => t.FontSize).ToArray());
        Assert.Equal([24.0, 40.0], ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("{ \"type\": \"text\", \"fontSize\": 20 }", encoding, rows), 300, 200), "mark-text").Select(t => t.FontSize).ToArray());

        // Assert: without a channel, the fontSize of the mark wins over its size, which wins over the default of 11
        Assert.Equal(20.0, Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "fontSize": 20, "size": 9 }""")).FontSize);
        Assert.Equal(9.0, Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "size": 9 }""")).FontSize);
        Assert.Equal(11.0, Assert.Single(Texts(TextField, """[ { "t": "a" } ]""")).FontSize);

        // Assert: a size expression of the mark is evaluated for every row: s + 1 gives 6 and 11
        var computed = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("{ \"type\": \"text\", \"size\": { \"expr\": \"datum.s + 1\" } }", "{ \"text\": " + TextField + " }", rows), 300, 200), "mark-text");
        Assert.Equal([6.0, 11.0], computed.Select(t => t.FontSize).ToArray());
    }

    /// <summary>
    ///     Validates the text of a field: the d3 number format, the default number format of a quantitative field, the time formats of a temporal field (the format, the unit and the
    ///     configuration), a forced time format, the range of a binned field, an aggregate, and the JavaScript text of every other value, null included.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextFormatsNumbersTimesAndNulls()
    {
        // Assert: a d3 format, the default format (12 significant digits, trimmed) and a percentage
        Assert.Equal(["1,234.5"], Labels("""{ "field": "q", "type": "quantitative", "format": ",.1f" }""", """[ { "q": 1234.5 } ]"""));
        Assert.Equal(["1234.5"], Labels("""{ "field": "q", "type": "quantitative" }""", """[ { "q": 1234.5 } ]"""));
        Assert.Equal(["26%"], Labels("""{ "field": "q", "type": "quantitative", "format": ".0%" }""", """[ { "q": 0.256 } ]"""));

        // Assert: the numberFormat of the configuration is the default of a quantitative field, and the format of the channel wins
        Assert.Equal(["3.0"], Labels("""{ "field": "q", "type": "quantitative" }""", """[ { "q": 3 } ]""", "\"text\"", """{ "numberFormat": ".1f" }"""));
        Assert.Equal(["3.00"], Labels("""{ "field": "q", "type": "quantitative", "format": ".2f" }""", """[ { "q": 3 } ]""", "\"text\"", """{ "numberFormat": ".1f" }"""));

        // Assert: a null of a quantitative field is formatted as the number zero (d3 formats +null), a null of any other field is the text null
        Assert.Equal(["0"], Labels("""{ "field": "q", "type": "quantitative" }""", """[ { "q": null } ]"""));
        Assert.Equal(["null", "x", "5", "true"], Labels("""{ "field": "n", "type": "nominal" }""", """[ { "n": null }, { "n": "x" }, { "n": 5 }, { "n": true } ]"""));

        // Assert: a temporal field uses the format, else config.timeFormat (%b %d, %Y), else the specifier of its time unit; the calendar of the chart is UTC here
        const string dates = """[ { "d": "2020-03-05" } ]""";
        Assert.Equal(["2020/03/05"], Labels("""{ "field": "d", "type": "temporal", "format": "%Y/%m/%d" }""", dates));
        Assert.Equal(["Mar 05, 2020"], Labels("""{ "field": "d", "type": "temporal" }""", dates));
        Assert.Equal(["05.03.2020"], Labels("""{ "field": "d", "type": "temporal" }""", dates, "\"text\"", """{ "timeFormat": "%d.%m.%Y" }"""));
        Assert.Equal(["Mar 2020"], Labels("""{ "field": "d", "type": "temporal", "timeUnit": "yearmonth" }""", dates));
        Assert.Equal(["null"], Labels("""{ "field": "d", "type": "temporal" }""", """[ { "d": null } ]"""));

        // Assert: formatType time and utc force a time format on a number of milliseconds (1583366400000 is 2020-03-05T00:00:00Z)
        const string instants = """[ { "n": 1583366400000 } ]""";
        Assert.Equal(["2020-03-05"], Labels("""{ "field": "n", "type": "quantitative", "format": "%Y-%m-%d", "formatType": "time" }""", instants));
        Assert.Equal(["2020-03-05"], Labels("""{ "field": "n", "type": "quantitative", "format": "%Y-%m-%d", "formatType": "utc" }""", instants));

        // Assert: a binned field is the start and the end of its bin with an en dash, through the number format
        const string values = """[ { "q": 5 }, { "q": 15 } ]""";
        Assert.Equal(["0 \u2013 10", "10 \u2013 20"], Labels("""{ "field": "q", "type": "quantitative", "bin": { "step": 10 } }""", values));
        Assert.Equal(["0.0 \u2013 10.0", "10.0 \u2013 20.0"], Labels("""{ "field": "q", "type": "quantitative", "bin": { "step": 10 }, "format": ".1f" }""", values));

        // Assert: an aggregate is the text of its measure: three rows give one count
        Assert.Equal(["3"], Labels("""{ "aggregate": "count", "type": "quantitative" }""", """[ { "q": 1 }, { "q": 2 }, { "q": 3 } ]"""));
    }

    /// <summary>
    ///     Validates the lines of a label: an array of lines as the <c>value</c> or the <c>text</c> property, a field that holds an array, the <c>lineBreak</c> of the mark (a literal
    ///     delimiter that does not apply to an array), and that an empty text draws nothing.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextArraysAndLineBreakMakeLines()
    {
        // Act: an array value is one line for each element; the line height stays the default (the font size plus 2 when it is painted)
        var lines = Assert.Single(Texts("""{ "value": ["a", "b"] }""", """[ { "q": 1 } ]"""));

        // Assert
        Assert.Equal(["a", "b"], lines.Lines.ToArray());
        Assert.Equal("a\nb", lines.Text);
        Assert.Null(lines.LineHeight);

        // Assert: numbers, booleans and null elements are their JavaScript text (null is an empty line)
        Assert.Equal(["1", "true", string.Empty, "x"], Assert.Single(Texts("""{ "value": [1, true, null, "x"] }""", """[ { "q": 1 } ]""")).Lines.ToArray());

        // Assert: a field that holds an array (made by a calculate transform; inline rows are flattened into fields) is a line for each element
        const string calculated = """
            { "width": 300, "height": 200, "data": { "values": [ { "a": "p", "b": "q" } ] }, "transform": [ { "calculate": "[datum.a, datum.b, 'r']", "as": "t" } ],
              "mark": "text", "encoding": { "text": { "field": "t", "type": "nominal" } } }
            """;
        Assert.Equal(["p", "q", "r"], Assert.Single(ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(calculated, 300, 200), "mark-text")).Lines.ToArray());

        // Assert: the lineBreak splits a text on its literal text, also at the ends; a newline character is a delimiter only when it is the lineBreak
        const string broken = """{ "type": "text", "lineBreak": "|" }""";
        Assert.Equal([["a", "b", "c"], ["", "a"], ["a", string.Empty]], Texts(TextField, """[ { "t": "a|b|c" }, { "t": "|a" }, { "t": "a|" } ]""", broken).Select(t => t.Lines.ToArray()));
        Assert.Equal(["a\nb"], Assert.Single(Texts(TextField, """[ { "t": "a\nb" } ]""")).Lines.ToArray());
        Assert.Equal(["a", "b"], Assert.Single(Texts(TextField, """[ { "t": "a\nb" } ]""", """{ "type": "text", "lineBreak": "\n" }""")).Lines.ToArray());

        // Assert: the lineBreak does not split an array, and a lineBreak of text that does not occur leaves one line
        Assert.Equal(["a|b"], Assert.Single(Texts("""{ "value": ["a|b"] }""", """[ { "q": 1 } ]""", broken)).Lines.ToArray());
        Assert.Equal(["ab"], Assert.Single(Texts(TextField, """[ { "t": "ab" } ]""", broken)).Lines.ToArray());

        // Assert: the text property of the mark can be a list of lines too
        Assert.Equal(["x", "y"], Assert.Single(ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("{ \"type\": \"text\", \"text\": [\"x\", \"y\"] }", "{}", """[ { "q": 1 } ]"""), 300, 200), "mark-text")).Lines.ToArray());

        // Assert: an empty text and an empty list draw nothing (Vega does not draw a text of length zero)
        Assert.Empty(Texts(TextField, """[ { "t": "" } ]"""));
        Assert.Empty(Texts("""{ "value": [] }""", """[ { "q": 1 } ]"""));
        Assert.Single(Texts("""{ "value": [""] }""", """[ { "q": 1 } ]"""));
    }

    /// <summary>
    ///     Validates the sources of the text: the <c>text</c> property of the mark, a <c>value</c> (a text, a number, a boolean or null), a <c>datum</c> with its format, the precedence of the
    ///     channel over the property, and that a mark without text draws nothing.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextMarkPropertyAndValueChannelAreUsed()
    {
        // Arrange
        const string rows = """[ { "t": "a" }, { "t": "b" } ]""";

        // Act / Assert: the property is the text of every row; the channel wins over it
        var property = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("{ \"type\": \"text\", \"text\": \"hello\" }", "{}", rows), 300, 200), "mark-text");
        Assert.Equal(["hello", "hello"], property.Select(t => t.Text));
        var channel = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("{ \"type\": \"text\", \"text\": \"hello\" }", "{ \"text\": " + TextField + " }", rows), 300, 200), "mark-text");
        Assert.Equal(["a", "b"], channel.Select(t => t.Text));

        // Assert: a value is its JavaScript text, null has none
        Assert.Equal(["v", "v"], Labels("""{ "value": "v" }""", rows));
        Assert.Equal(["5"], Labels("""{ "value": 5 }""", """[ { "t": "a" } ]"""));
        Assert.Equal(["true"], Labels("""{ "value": true }""", """[ { "t": "a" } ]"""));
        Assert.Empty(Labels("""{ "value": null }""", """[ { "t": "a" } ]"""));

        // Assert: a datum is formatted like a field, a text datum is itself
        Assert.Equal(["5.00"], Labels("""{ "datum": 5, "type": "quantitative", "format": ".2f" }""", """[ { "t": "a" } ]"""));
        Assert.Equal(["5"], Labels("""{ "datum": 5 }""", """[ { "t": "a" } ]"""));
        Assert.Equal(["abc"], Labels("""{ "datum": "abc" }""", """[ { "t": "a" } ]"""));

        // Assert: a mark with neither a text channel nor a text property, and a field that the data does not have, draw nothing
        Assert.Empty(ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", "{}", rows), 300, 200), "mark-text"));
        Assert.Empty(Labels("""{ "field": "missing", "type": "nominal" }""", rows));
    }

    /// <summary>
    ///     Validates that a custom <c>formatType</c> is not evaluated: the default format of the type is used and the <c>format</c> is ignored (the feature catalog reports it).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextCustomFormatTypeFallsBackToTheDefault()
    {
        // Assert: a number is formatted by the default format, not by the format of the unknown type
        Assert.Equal(["1234.5"], Labels("""{ "field": "q", "type": "quantitative", "format": ",.1f", "formatType": "mine" }""", """[ { "q": 1234.5 } ]"""));

        // Assert: a date is formatted by config.timeFormat
        Assert.Equal(["Mar 05, 2020"], Labels("""{ "field": "d", "type": "temporal", "format": "%Y", "formatType": "mine" }""", """[ { "d": "2020-03-05" } ]"""));

        // Assert: the catalog reports the custom format type at the formatType
        var tree = SpecParser.Parse(Chart("\"text\"", "{ \"text\": { \"field\": \"q\", \"type\": \"quantitative\", \"formatType\": \"mine\" } }", """[ { "q": 1 } ]"""), "t.json");
        var report = Assert.Single(FeatureCatalog.Visit(tree));
        Assert.Equal(("vl-format-type-custom", "/encoding/text/formatType"), (report.Id, report.Location.Pointer));
    }

    /// <summary>
    ///     Validates the constant and field forms of the polar offset: <c>theta</c> runs clockwise from twelve o'clock, the offset is the radius, the radius scale runs to half of the smaller side
    ///     of the data rectangle, and a row with no valid angle or radius is not drawn.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextConstantAndFieldRadiusAndThetaOffsetTheAnchor()
    {
        // Assert: a quarter turn is 3 o'clock, a half turn is 6 o'clock and no turn is 12 o'clock, from the middle (150, 100) of the data rectangle
        var quarter = Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "radius": 50, "theta": 1.5707963267948966 }"""));
        AssertAt(quarter, 200, 100);
        AssertAt(Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "radius": 50, "theta": 3.141592653589793 }""")), 150, 150);
        AssertAt(Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "radius": 50, "theta": 0 }""")), 150, 50);

        // Assert: without a radius the angle moves nothing, and dx and dy stay apart from the offset
        AssertAt(Assert.Single(Texts(TextField, """[ { "t": "a" } ]""", """{ "type": "text", "theta": 1.5707963267948966, "dx": 9 }""")), 150, 100);

        // Assert: a radius value and a theta field; the theta domain is [0, 4] over [0, 2 pi], so a = 1 is a quarter turn and a = 4 a full turn
        const string rows = """[ { "a": 1, "t": "p" }, { "a": 4, "t": "q" } ]""";
        const string theta = """{ "theta": { "field": "a", "type": "quantitative", "stack": false }, "radius": { "value": 40 }, "text": { "field": "t", "type": "nominal" } }""";
        var turned = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", theta, rows), 300, 200), "mark-text");
        AssertAt(turned[0], 190, 100);
        AssertAt(turned[1], 150, 60);

        // Assert: a radius field runs over [0, 100] (half of the smaller side, 200): the domain [0, 10] puts 5 at 50 and 10 at 100; with a quarter turn the labels are to the right
        const string radiusRows = """[ { "r": 5, "t": "p" }, { "r": 10, "t": "q" } ]""";
        const string radius = """{ "radius": { "field": "r", "type": "quantitative", "stack": false }, "text": { "field": "t", "type": "nominal" } }""";
        var spread = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("{ \"type\": \"text\", \"theta\": 1.5707963267948966 }", radius, radiusRows), 300, 200), "mark-text");
        AssertAt(spread[0], 200, 100);
        AssertAt(spread[1], 250, 100);

        // Assert: a row whose angle is null has no position and is not drawn
        const string gaps = """[ { "a": 1, "t": "p" }, { "a": null, "t": "q" }, { "a": 4, "t": "r" } ]""";
        var drawn = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", theta, gaps), 300, 200), "mark-text");
        Assert.Equal(["p", "r"], drawn.Select(t => t.Text));
    }

    /// <summary>
    ///     Validates that the characters of a label are bounded by <c>MaxStringLength</c> and those of all the labels of the mark by sixteen times that, before the label is formatted
    ///     or kept, that the characters are charged to the evaluation steps, and that the build polls the cancellation token.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextCharactersAreChargedBeforeFormatting()
    {
        // Arrange: a limit of 16 characters; the number format "0>16d" pads the value to 16 characters, so each label has exactly the limit and the mark may have 16 * 16 = 256 characters
        var limits = new VegaLiteLimits { MaxStringLength = 16 };
        const string text = """{ "field": "q", "type": "quantitative", "format": "0>16d" }""";
        static string Rows(int count) => "[" + string.Join(", ", Enumerable.Range(1, count).Select(i => "{ \"q\": " + i + " }")) + "]";

        // Act / Assert: sixteen labels of sixteen characters fit the total
        var fits = ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(Chart("\"text\"", "{ \"text\": " + text + " }", Rows(16)), 300, 200, limits: limits), "mark-text");
        Assert.Equal(16, fits.Count);
        Assert.Equal("0000000000000001", fits[0].Text);

        // Act / Assert: the seventeenth label is refused because of the total, with the location of the mark
        var total = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Marks(Chart("\"text\"", "{ \"text\": " + text + " }", Rows(17)), 300, 200, limits: limits));
        Assert.Equal("/mark", total.Location.Pointer);
        Assert.Contains("together", total.Message, StringComparison.Ordinal);
        Assert.Contains("MaxStringLength", total.Message, StringComparison.Ordinal);

        // Act / Assert: a single label of seventeen characters is refused because of its own length
        var single = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Marks(Chart("\"text\"", """{ "text": { "field": "q", "type": "quantitative", "format": "0>17d" } }""", Rows(1)), 300, 200, limits: limits));
        Assert.Equal("/mark", single.Location.Pointer);
        Assert.Contains("A label", single.Message, StringComparison.Ordinal);

        // Act / Assert: the same refusal for a time label that does not fit (the label is 22 characters, the limit is ten; every text of the specification fits it)
        var tight = new VegaLiteLimits { MaxStringLength = 10 };
        var date = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Marks(Chart("\"text\"", """{ "text": { "field": "d", "type": "nominal", "format": "%A, %B %d", "formatType": "time" } }""", """[ { "d": 1599264000000 } ]"""), 300, 200, limits: tight));
        Assert.Contains("A label", date.Message, StringComparison.Ordinal);
        Assert.Equal("/mark", date.Location.Pointer);

        // Arrange: a text of 6,400 characters costs 100 steps, more than the limit of 50 (a row costs one element, which is below one step)
        var longText = new string('a', 6400);
        var chart = Chart("\"text\"", "{ \"text\": " + TextField + " }", "[ { \"t\": \"" + longText + "\" } ]");

        // Act / Assert: the characters are charged to the evaluation steps, with the location of the mark
        var steps = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Marks(chart, 300, 200, limits: new VegaLiteLimits { MaxEvaluationSteps = 50 }));
        Assert.Equal("/mark", steps.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", steps.Message, StringComparison.Ordinal);

        // Assert: with the default limits the label is built
        Assert.Equal(6400, Assert.Single(ChartTestHelpers.Of<SceneText>(ChartTestHelpers.Marks(chart, 300, 200), "mark-text")).Text.Length);

        // Act / Assert: a token that is canceled stops the build
        var parts = ChartTestHelpers.Parts(chart);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new MarkBuilder(parts.Config, null, null, "test.vl.json", parts.Expressions).Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, canceled.Token));
    }

    /// <summary>
    ///     Validates that every line after the first of a label is charged to the scene budget as an item, before the label exists: two labels of three lines cost the marks group and two times three items.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextLinesAreChargedAsItems()
    {
        // Arrange: two rows, each a label of the three lines a, b, c
        var parts = ChartTestHelpers.Parts(Chart("\"text\"", """{ "text": { "value": ["a", "b", "c"] }, "x": { "field": "x", "type": "quantitative" } }""", """[ { "x": 1 }, { "x": 2 } ]"""));

        // Act: the budget is exactly the marks group (1) and two labels of 1 + 2 items
        var budget = new SceneBudget(7);
        var group = new MarkBuilder(parts.Config, null, budget, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(7, budget.Count);
        Assert.Equal(2, group.Children.Count);
        Assert.All(group.Children, c => Assert.Equal(3, ((SceneText)c).Lines.Count));

        // Act / Assert: one item less and the second label is refused after its charge
        var tight = new SceneBudget(6);
        var failure = Assert.Throws<VegaLiteRenderException>(() => new MarkBuilder(parts.Config, null, tight, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken));
        Assert.Equal("/mark", failure.Location.Pointer);
        Assert.Contains("MaxSceneItems", failure.Message, StringComparison.Ordinal);
        Assert.Equal(7, tight.Count);

        // Act / Assert: the first label is refused after its charge, and nothing else was counted
        var first = new SceneBudget(3);
        Assert.Throws<VegaLiteRenderException>(() => new MarkBuilder(parts.Config, null, first, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken));
        Assert.Equal(4, first.Count);
    }

    /// <summary>
    ///     Validates that a label whose font size, position, angle, offset or line height is beyond the magnitude bound of the painter is not built (32,768 scene units; a font size of 1e300
    ///     once made the renderer throw), while the values at the bound itself are kept.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TextBeyondTheMagnitudeBoundIsSkipped()
    {
        // Arrange: two rows; each mark property below is beyond the bound (or, for the control, at it)
        const string rows = """[ { "t": "a" }, { "t": "b" } ]""";

        // Act / Assert: a mark property beyond the bound leaves no label
        foreach (var property in new[] { "\"fontSize\": 1e300", "\"fontSize\": 32769", "\"fontSize\": 3e38", "\"dx\": 1e300", "\"dy\": -40000", "\"angle\": 1e7", "\"lineHeight\": 1e300" })
        {
            Assert.Empty(Texts(TextField, rows, "{ \"type\": \"text\", " + property + " }"));
        }

        // Assert: at the bound itself the labels are built
        foreach (var property in new[] { "\"fontSize\": 32768", "\"dx\": 32768", "\"dy\": -32768", "\"angle\": 32768", "\"lineHeight\": 32768" })
        {
            Assert.Equal(["a", "b"], Labels(TextField, rows, "{ \"type\": \"text\", " + property + " }"));
        }

        // Arrange: three rows on a fixed x scale (x(v) = 300 v): the third is a billion pixels to the right
        const string encoding = """{ "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 1], "nice": false } }, "text": { "field": "t", "type": "nominal" } }""";
        const string positions = """[ { "x": 0, "t": "a" }, { "x": 0.5, "t": "b" }, { "x": 1e9, "t": "c" } ]""";
        var parts = ChartTestHelpers.Parts(Chart("\"text\"", encoding, positions));
        var budget = new SceneBudget(100);

        // Act
        var group = new MarkBuilder(parts.Config, null, budget, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken);

        // Assert: the third row is skipped like an invalid one, the others are at 0 and 150, and only the labels that exist were charged (the marks group and two items)
        var texts = ChartTestHelpers.Of<SceneText>(group, "mark-text");
        Assert.Equal(["a", "b"], texts.Select(t => t.Text));
        AssertAt(texts[0], 0, 100);
        AssertAt(texts[1], 150, 100);
        Assert.Equal(3, budget.Count);
    }
}
