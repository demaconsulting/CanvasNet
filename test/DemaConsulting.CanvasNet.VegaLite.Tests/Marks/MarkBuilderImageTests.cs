using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the image mark of <see cref="DemaConsulting.CanvasNet.VegaLite.Marks.MarkBuilder"/>: the position and the span on each axis by the rules of Vega-Lite's
///     <c>compile/mark/image.ts</c> and <c>position-rect.ts</c> (a point with <c>align</c> and <c>baseline</c> for a position without a size, a span for a size, a band or a range,
///     the whole data rectangle for an absent channel), the link from the <c>url</c> channel or property, the look properties and the limits. The marks are built in a 300 by 200 data
///     rectangle whose scales have explicit domains of 0 to 10, so a value <c>v</c> is at <c>30 v</c> pixels across and at <c>200 - 20 v</c> pixels down; every expectation is that arithmetic.
/// </summary>
public class MarkBuilderImageTests
{
    /// <summary>The tolerance of the comparisons of positions.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>One row at (5, 2): 150 pixels across and 160 down.</summary>
    private const string OneRow = """[ { "x": 5, "y": 2, "u": "a.png" } ]""";

    /// <summary>The position channels on the scales of 0 to 10.</summary>
    private const string Position = """
        "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] } },
        "y": { "field": "y", "type": "quantitative", "scale": { "domain": [0, 10] } }
        """;

    /// <summary>The link of the rows as a field.</summary>
    private const string UrlField = """ "url": { "field": "u", "type": "nominal" } """;

    /// <summary>
    ///     Builds a specification of a mark in a 300 by 200 view.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The members of the encoding object, as JSON text.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="config">The JSON text of the <c>config</c> object, or null.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string encoding, string values = OneRow, string? config = null) =>
        "{ \"width\": 300, \"height\": 200, \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": { " + encoding + " }"
        + (config is null ? string.Empty : ", \"config\": " + config) + " }";

    /// <summary>
    ///     Builds the picture items of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The items in drawing order.</returns>
    private static List<SceneImage> Images(string json, VegaLiteLimits? limits = null) =>
        ChartTestHelpers.Of<SceneImage>(ChartTestHelpers.Marks(json, 300, 200, limits: limits), "mark-image");

    /// <summary>
    ///     Builds the only picture item of a chart that has the encoding of the position and the link.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The members of the encoding.</param>
    /// <param name="values">The rows.</param>
    /// <returns>The item.</returns>
    private static SceneImage OneImage(string mark, string encoding, string values = OneRow) => Assert.Single(Images(Chart(mark, encoding, values)));

    /// <summary>
    ///     Test that a position without a size is a point: the picture is anchored at the scaled value by the <c>align</c> and <c>baseline</c> of the mark (center and middle by default)
    ///     and has no width or height of its own, because only the painter knows the size of the picture.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImagePositionWithoutSize_IsAPointAnchoredCenterMiddle()
    {
        // Act
        var item = OneImage("\"image\"", Position + "," + UrlField);

        // Assert
        Assert.Equal(("mark-image", "a.png", "center", "middle", true, true, 1d), (item.Role, item.Url, item.Align, item.Baseline, item.Aspect, item.Smooth, item.Opacity));
        Assert.Equal(150, item.X, Tolerance);
        Assert.Equal(160, item.Y, Tolerance);
        Assert.Null(item.Width);
        Assert.Null(item.Height);
    }

    /// <summary>
    ///     Test that the <c>align</c> and <c>baseline</c> of the mark anchor a point (they are the only use Vega-Lite's image mark has for them).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageAlignAndBaseline_AnchorThePoint()
    {
        // Act
        var item = OneImage("""{ "type": "image", "align": "left", "baseline": "bottom" }""", Position + "," + UrlField);

        // Assert
        Assert.Equal(("left", "bottom"), (item.Align, item.Baseline));
        Assert.Equal((150d, 160d), (item.X, item.Y));
    }

    /// <summary>
    ///     Test that a <c>width</c> and <c>height</c> of the mark make a span that is aligned by the <c>align</c> and <c>baseline</c> (center and middle by default).
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="left">The expected left edge.</param>
    /// <param name="top">The expected top edge.</param>
    [Theory]
    [InlineData("""{ "type": "image", "width": 20, "height": 10 }""", 140d, 155d)]
    [InlineData("""{ "type": "image", "width": 20, "height": 10, "align": "left", "baseline": "top" }""", 150d, 160d)]
    [InlineData("""{ "type": "image", "width": 20, "height": 10, "align": "right", "baseline": "bottom" }""", 130d, 150d)]
    public void MarkBuilder_Build_ImageWidthAndHeight_AreSpansAlignedAtThePosition(string mark, double left, double top)
    {
        // Act
        var item = OneImage(mark, Position + "," + UrlField);

        // Assert: a span has the edges of the box, so the painter anchors it at the left and top
        Assert.Equal(left, item.X, Tolerance);
        Assert.Equal(top, item.Y, Tolerance);
        Assert.Equal((20d, 10d, "left", "top"), (item.Width, item.Height, item.Align, item.Baseline));
    }

    /// <summary>
    ///     Test that only one axis can have a size: the other axis is a point again.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageWidthOnly_LeavesTheHeightNatural()
    {
        // Act
        var item = OneImage("""{ "type": "image", "width": 20 }""", Position + "," + UrlField);

        // Assert
        Assert.Equal((140d, 20d, "left"), (item.X, item.Width, item.Align));
        Assert.Equal(160, item.Y, Tolerance);
        Assert.Null(item.Height);
        Assert.Equal("middle", item.Baseline);
    }

    /// <summary>
    ///     Test that an axis without a channel spans the whole data rectangle (Vega-Lite's zero-or-max default of both ends), and that with a size the picture is anchored at the right edge
    ///     (x) and the top (y).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageWithoutPosition_SpansTheDataRectangleOrAnchorsAtItsCorner()
    {
        // Act
        var whole = OneImage("\"image\"", UrlField);
        var sized = OneImage("""{ "type": "image", "width": 20, "height": 10 }""", UrlField);

        // Assert: the whole rectangle
        Assert.Equal((0d, 0d, 300d, 200d), (whole.X, whole.Y, whole.Width, whole.Height));

        // Assert: x defaults to the width of the view and y to zero, and the default alignment is center and middle about them
        Assert.Equal((290d, -5d, 20d, 10d), (sized.X, sized.Y, sized.Width, sized.Height));
    }

    /// <summary>
    ///     Test that a position that the mark gives (<c>x</c>, <c>y</c>, <c>x2</c>, <c>y2</c> and the keyword <c>width</c>) takes the place of the default of an absent channel.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImagePositionOfTheMark_ReplacesTheDefaultOfAnAbsentChannel()
    {
        // Act
        var anchored = OneImage("""{ "type": "image", "x": 100, "y": 50, "width": 20, "height": 10 }""", UrlField);
        var ranged = OneImage("""{ "type": "image", "x": 100, "x2": "width", "y": 50, "y2": 70 }""", UrlField);

        // Assert
        Assert.Equal((90d, 45d, 20d, 10d), (anchored.X, anchored.Y, anchored.Width, anchored.Height));
        Assert.Equal((100d, 50d, 200d, 20d), (ranged.X, ranged.Y, ranged.Width, ranged.Height));
    }

    /// <summary>
    ///     Test that an <c>x2</c> and a <c>y2</c> make a range from the first to the second end, in either order.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageSecondEnd_IsARange()
    {
        // Arrange: x from 6 to 2 and y from 2 to 8
        const string rows = """[ { "x": 6, "x2": 2, "y": 2, "y2": 8, "u": "a.png" } ]""";
        const string encoding = """
            "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] } }, "x2": { "field": "x2" },
            "y": { "field": "y", "type": "quantitative", "scale": { "domain": [0, 10] } }, "y2": { "field": "y2" },
            "url": { "field": "u", "type": "nominal" }
            """;

        // Act
        var item = OneImage("\"image\"", encoding, rows);

        // Assert: x runs from 60 to 180, y from 40 (value 8) to 160 (value 2)
        Assert.Equal(60, item.X, Tolerance);
        Assert.Equal(120, item.Width!.Value, Tolerance);
        Assert.Equal(40, item.Y, Tolerance);
        Assert.Equal(120, item.Height!.Value, Tolerance);
    }

    /// <summary>
    ///     Test that a position on a continuous scale with a size from the encoding (a <c>size</c> channel) and none from the mark is a range to the zero of the scale, as Vega-Lite compiles it.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageSizeChannelWithoutWidth_RangesToZero()
    {
        // Arrange: a size channel counts as a size, so the x axis is a range from the value to zero
        const string encoding = """
            "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 10] } }, "size": { "value": 20 }, "url": { "field": "u", "type": "nominal" }
            """;

        // Act
        var item = OneImage("\"image\"", encoding);

        // Assert: from 0 to 150 across, the whole height
        Assert.Equal((0d, 150d), (item.X, item.Width));
        Assert.Equal((0d, 200d), (item.Y, item.Height));
    }

    /// <summary>
    ///     Test that a discrete position without a size is a point at the center of its band, and that a size spans the band (no width of the mark), or is centered in it (a width).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageOnBandScale_IsAPointOrSpansTheBand()
    {
        // Arrange
        const string rows = """[ { "c": "a", "u": "a.png" }, { "c": "b", "u": "b.png" }, { "c": "c", "u": "c.png" } ]""";
        const string encoding = """ "x": { "field": "c", "type": "nominal" }, "url": { "field": "u", "type": "nominal" } """;
        var model = ChartTestHelpers.Parts(Chart("\"image\"", encoding, rows)).Scales["x"]!.CreateModel(300);
        var centerOfB = model.MapCenter(DataValue.FromString("b"))!.Value;
        var startOfB = model.Map(DataValue.FromString("b"))!.Value;

        // Act
        var points = Images(Chart("\"image\"", encoding, rows));
        var spans = Images(Chart("""{ "type": "image", "size": 5 }""", encoding, rows));
        var centered = Images(Chart("""{ "type": "image", "width": 12 }""", encoding, rows));

        // Assert: a point at the center of the band, anchored at its center
        Assert.Equal(3, points.Count);
        Assert.Equal(centerOfB, points[1].X, Tolerance);
        Assert.Equal(("center", null), (points[1].Align, points[1].Width));

        // Assert: the band itself
        Assert.Equal(startOfB, spans[1].X, Tolerance);
        Assert.Equal(model.Bandwidth, spans[1].Width!.Value, Tolerance);

        // Assert: a width of 12 centered in the band
        Assert.Equal(centerOfB - 6, centered[1].X, Tolerance);
        Assert.Equal(12, centered[1].Width!.Value, Tolerance);
    }

    /// <summary>
    ///     Test that a field that is binned spans its bin when no size is given (shifted by the half pixel that the axis is translated by).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageOnBinnedField_SpansTheBin()
    {
        // Arrange: bins of width 5 on a scale of 0 to 10: the bin of 1 runs from 0 to 5, that is from 0 to 150 pixels
        const string rows = """[ { "x": 1, "u": "a.png" }, { "x": 7, "u": "b.png" } ]""";
        const string encoding = """
            "x": { "field": "x", "type": "quantitative", "bin": { "step": 5 }, "scale": { "domain": [0, 10] } }, "url": { "field": "u", "type": "nominal" }
            """;

        // Act
        var items = Images(Chart("\"image\"", encoding, rows));

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal(0.5, items[0].X, Tolerance);
        Assert.Equal(150, items[0].Width!.Value, Tolerance);
        Assert.Equal(150.5, items[1].X, Tolerance);
    }

    /// <summary>
    ///     Test the sources of the link: a field, a <c>datum</c>, a <c>value</c> and the <c>url</c> property of the mark, the channel winning over the property, a number as its JavaScript text.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="url">The channel, or an empty text for none.</param>
    /// <param name="expected">The expected link of the only row.</param>
    [Theory]
    [InlineData("\"image\"", """ "url": { "field": "u", "type": "nominal" } """, "a.png")]
    [InlineData("\"image\"", """ "url": { "datum": "icons/b.png" } """, "icons/b.png")]
    [InlineData("\"image\"", """ "url": { "value": "c.png" } """, "c.png")]
    [InlineData("""{ "type": "image", "url": "d.png" }""", "", "d.png")]
    [InlineData("""{ "type": "image", "url": "d.png" }""", """ "url": { "value": "e.png" } """, "e.png")]
    [InlineData("\"image\"", """ "url": { "value": 42 } """, "42")]
    public void MarkBuilder_Build_ImageLink_ComesFromTheChannelOrTheProperty(string mark, string url, string expected)
    {
        // Act
        var item = OneImage(mark, Position + (url.Length == 0 ? string.Empty : "," + url));

        // Assert
        Assert.Equal(expected, item.Url);
    }

    /// <summary>
    ///     Test that a row whose link is null or empty, or has no position, draws no picture, and that a mark without any link draws nothing.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageRowsWithoutLinkOrPosition_AreNotDrawn()
    {
        // Arrange: four rows: a link, a null link, an empty link and a link without a position
        const string rows = """[ { "x": 1, "y": 1, "u": "a.png" }, { "x": 2, "y": 2, "u": null }, { "x": 3, "y": 3, "u": "" }, { "x": null, "y": 4, "u": "d.png" } ]""";

        // Act
        var items = Images(Chart("\"image\"", Position + "," + UrlField, rows));
        var noLink = Images(Chart("\"image\"", Position));
        var noField = Images(Chart("\"image\"", Position + """, "url": { "field": "missing", "type": "nominal" }""", rows));

        // Assert
        Assert.Equal(["a.png"], items.Select(i => i.Url));
        Assert.Empty(noLink);
        Assert.Empty(noField);
    }

    /// <summary>
    ///     Test that the <c>aspect</c> and <c>smooth</c> of the mark are carried by the item and that both are on by default.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageAspectAndSmooth_AreCarried()
    {
        // Act
        var item = OneImage("""{ "type": "image", "aspect": false, "smooth": false }""", Position + "," + UrlField);

        // Assert
        Assert.False(item.Aspect);
        Assert.False(item.Smooth);
    }

    /// <summary>
    ///     Test the opacity: 1 by default (an image is not one of the marks that Vega-Lite draws less opaque), the <c>opacity</c> of the mark, and the opacity channel.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageOpacity_ComesFromTheMarkOrTheChannel()
    {
        // Act
        var byDefault = OneImage("\"image\"", Position + "," + UrlField);
        var byMark = OneImage("""{ "type": "image", "opacity": 0.4 }""", Position + "," + UrlField);
        var byChannel = OneImage("\"image\"", Position + "," + UrlField + """, "opacity": { "value": 0.25 }""");

        // Assert
        Assert.Equal(1, byDefault.Opacity);
        Assert.Equal(0.4, byMark.Opacity);
        Assert.Equal(0.25, byChannel.Opacity);
    }

    /// <summary>
    ///     Test that the item carries the location of the mark for the report of an unresolved picture.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageCarriesTheMarkLocation()
    {
        // Act
        var item = OneImage("\"image\"", Position + "," + UrlField);

        // Assert
        Assert.Equal("/mark", item.Source.Pointer);
    }

    /// <summary>
    ///     Test that the scene budget is charged for every picture and that a link beyond the string limit fails at the mark.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageLimits_FailAtTheMark()
    {
        // Arrange: three rows, a budget for the marks group and two items; and a link that is the 13 digits of a number against a limit of 12 characters (the longest string of the specification)
        const string rows = """[ { "x": 1, "y": 1, "u": 1234567890123 }, { "x": 2, "y": 2, "u": 2 }, { "x": 3, "y": 3, "u": 3 } ]""";
        var budget = Chart("\"image\"", Position + "," + UrlField, rows);

        // Act and assert
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => Images(budget, new VegaLiteLimits { MaxSceneItems = 3 }));
        var tooLong = Assert.Throws<VegaLiteRenderException>(() => Images(budget, new VegaLiteLimits { MaxStringLength = 12 }));
        Assert.Equal("/mark", tooMany.Location.Pointer);
        Assert.Equal("/mark", tooLong.Location.Pointer);
        Assert.Contains("MaxStringLength", tooLong.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the picture is drawn in the order of the rows and that the <c>order</c> channel sorts them, as for the other marks.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ImageRows_KeepTheDrawingOrder()
    {
        // Arrange
        const string rows = """[ { "x": 1, "y": 1, "u": "a.png" }, { "x": 2, "y": 2, "u": "b.png" } ]""";

        // Act
        var natural = Images(Chart("\"image\"", Position + "," + UrlField, rows));
        var ordered = Images(Chart("\"image\"", Position + "," + UrlField + """, "order": { "field": "x", "type": "quantitative", "sort": "descending" }""", rows));

        // Assert
        Assert.Equal(["a.png", "b.png"], natural.Select(i => i.Url));
        Assert.Equal(["b.png", "a.png"], ordered.Select(i => i.Url));
    }
}
