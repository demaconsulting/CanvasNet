using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the path marks of <see cref="MarkBuilder"/> (<c>line</c>, <c>area</c>, <c>trail</c>): the segments, the grouping, the sort, the invalid data modes,
///     the curves, the stroke properties and the limits, with the expectations derived by hand from the linear scale formulas and the d3-shape outlines.
/// </summary>
public class MarkBuilderPathTests
{
    /// <summary>The default color of a mark, which is also the first color of the tableau10 palette.</summary>
    private const string Blue = "#4c78a8";

    /// <summary>The second color of the tableau10 palette.</summary>
    private const string Orange = "#f58518";

    /// <summary>Two quantitative position channels.</summary>
    private const string XY = """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } }""";

    /// <summary>Two quantitative position channels; y is not stacked (an area stacks its measure from zero by default, which turns a missing value into zero).</summary>
    private const string Unstacked = """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative", "stack": null } }""";

    /// <summary>Three points: x 0, 1, 2 and y 0, 10, 5.</summary>
    private const string Basic = """[ { "x": 0, "y": 0 }, { "x": 1, "y": 10 }, { "x": 2, "y": 5 } ]""";

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
    ///     Builds a 100 by 100 chart whose x and y scales are the identity from data to pixels (y runs upwards).
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <returns>The specification text.</returns>
    private static string Square(string mark, string values) =>
        "{ \"width\": 100, \"height\": 100, \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": { "
        + "\"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"scale\": { \"domain\": [0, 100], \"nice\": false } }, "
        + "\"y\": { \"field\": \"y\", \"type\": \"quantitative\", \"scale\": { \"domain\": [0, 100], \"nice\": false } } } }";

    /// <summary>
    ///     Gets the only path of a mark group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="role">The role of the path.</param>
    /// <returns>The path.</returns>
    private static ScenePath OnlyPath(SceneGroup group, string role) => Assert.Single(ChartTestHelpers.Of<ScenePath>(group, role));

    /// <summary>
    ///     Validates the segments and the paint of a line with the numbers derived by hand: x runs 0 to 2 over 300 pixels, y runs 0 to 10 over 200 pixels.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LineHasHandDerivedSegmentsAndPaint()
    {
        // Arrange: x domain [0, 2] (zero, nice) so x(v) = 150 v; y domain [0, 10] so y(v) = 200 - 20 v: the points are (0, 200), (150, 0), (300, 100)
        var marks = ChartTestHelpers.Marks(Chart("\"line\"", XY, Basic), 300, 200);

        // Act
        var line = OnlyPath(marks, "mark-line");

        // Assert: one subpath through the three points, stroked in the default color with the width of Vega's line style, not filled
        Assert.Equal("M0,200 L150,0 L300,100", PathText.Of(line));
        Assert.Equal((Blue, (string?)null, 2.0), (line.Stroke, line.Fill, line.StrokeWidth));
        Assert.Equal((1.0, 1.0, 1.0), (line.Opacity, line.FillOpacity, line.StrokeOpacity));
        Assert.Null(line.StrokeDash);
        Assert.Empty(line.Points);
        Assert.Null(marks.Clip);
    }

    /// <summary>
    ///     Validates that the rows of a line are grouped by the color and detail fields, one path per group in order of first appearance, each with the color of its group.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LineGroupsByColorAndDetailInFirstSeenOrder()
    {
        // Arrange: the rows of the series a and b alternate; x domain [0, 1] (x(v) = 300 v), y domain [0, 6] (y(v) = 200 - 100/3 v)
        const string rows = """
            [ { "x": 0, "y": 1, "g": "a" }, { "x": 0, "y": 5, "g": "b" }, { "x": 1, "y": 2, "g": "a" }, { "x": 1, "y": 6, "g": "b" } ]
            """;
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }
            """;

        // Act
        var lines = ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"line\"", encoding, rows), 300, 200), "mark-line");

        // Assert: a comes first, then b; the color scale gives a the first and b the second color
        Assert.Equal(["M0,166.666667 L300,133.333333", "M0,33.333333 L300,0"], lines.Select(PathText.Of));
        Assert.Equal([Blue, Orange], lines.Select(l => l.Stroke));

        // Arrange: a detail field splits one series into two paths without changing the color; y domain [0, 4] (y(v) = 200 - 50 v)
        const string detailRows = """
            [ { "x": 0, "y": 1, "d": "p" }, { "x": 0, "y": 3, "d": "q" }, { "x": 1, "y": 2, "d": "p" }, { "x": 1, "y": 4, "d": "q" } ]
            """;
        const string detail = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "detail": { "field": "d" } }
            """;
        var split = ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"line\"", detail, detailRows), 300, 200), "mark-line");
        Assert.Equal(["M0,150 L300,100", "M0,50 L300,0"], split.Select(PathText.Of));
        Assert.All(split, l => Assert.Equal(Blue, l.Stroke));

        // Assert: without a grouping field all rows are one path
        Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"line\"", XY, detailRows), 300, 200), "mark-line"));
    }

    /// <summary>
    ///     Validates the sort of the points of a line: by the pixel x by default, by the <c>order</c> field (ascending or descending) when the encoding has one.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LineSortsByPixelXAndByTheOrderChannel()
    {
        // Arrange: the rows are not in the order of x; the line visits them from the smallest x to the largest
        const string shuffled = """[ { "x": 2, "y": 5 }, { "x": 0, "y": 0 }, { "x": 1, "y": 10 } ]""";

        // Act / Assert: sorted by x, the same line as the sorted data
        Assert.Equal("M0,200 L150,0 L300,100", PathText.Of(OnlyPath(ChartTestHelpers.Marks(Chart("\"line\"", XY, shuffled), 300, 200), "mark-line")));

        // Arrange: the order field decides the sequence instead (it does not group a line): o = 1, 3, 2 for x = 0, 1, 2
        const string ordered = """[ { "x": 0, "y": 0, "o": 1 }, { "x": 1, "y": 10, "o": 3 }, { "x": 2, "y": 5, "o": 2 } ]""";
        string WithOrder(string order) =>
            Chart("\"line\"", "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" }, \"order\": " + order + " }", ordered);

        // Assert: ascending by o visits x = 0, 2, 1; descending visits x = 1, 2, 0
        Assert.Equal("M0,200 L300,100 L150,0", PathText.Of(OnlyPath(ChartTestHelpers.Marks(WithOrder("""{ "field": "o" }"""), 300, 200), "mark-line")));
        Assert.Equal("M150,0 L300,100 L0,200", PathText.Of(OnlyPath(ChartTestHelpers.Marks(WithOrder("""{ "field": "o", "sort": "descending" }"""), 300, 200), "mark-line")));

        // Assert: a horizontal line (the measure is x, the dimension y) is sorted by the pixel y: y(t) = 200 - 100 t for t = 0, 1, 2
        const string rotated = """
            { "width": 300, "height": 200, "data": { "values": [ { "t": 0, "v": 0 }, { "t": 2, "v": 10 }, { "t": 1, "v": 5 } ] }, "mark": { "type": "line", "orient": "horizontal" },
              "encoding": { "x": { "field": "v", "type": "quantitative" }, "y": { "field": "t", "type": "quantitative" } } }
            """;
        Assert.Equal("M300,0 L150,100 L0,200", PathText.Of(OnlyPath(ChartTestHelpers.Marks(rotated, 300, 200), "mark-line")));
    }

    /// <summary>
    ///     Validates that the <c>size</c> of a line is its stroke width, that it splits the lines (one path for every size), and that the mark property gives a constant width.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_LineSizeIsTheStrokeWidthOfTheFirstRow()
    {
        // Arrange: size maps the domain [0, 4] to the widths [2, 10]: the size 1 is 4 wide and the size 3 is 8 wide
        const string rows = """
            [ { "x": 0, "y": 0, "s": 1 }, { "x": 1, "y": 10, "s": 1 }, { "x": 0, "y": 5, "s": 3 }, { "x": 1, "y": 6, "s": 3 } ]
            """;
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" },
              "size": { "field": "s", "type": "quantitative", "legend": null, "scale": { "domain": [0, 4], "range": [2, 10] } } }
            """;

        // Act
        var lines = ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"line\"", encoding, rows), 300, 200), "mark-line");

        // Assert: the rows of one size are one path and carry its width
        Assert.Equal([4.0, 8.0], lines.Select(l => l.StrokeWidth!.Value));
        Assert.Equal(2, lines.Count);

        // Assert: the size of the mark is the stroke width of a line, and the strokeWidth property is the default width
        Assert.Equal(5.0, OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "line", "size": 5 }""", XY, Basic), 300, 200), "mark-line").StrokeWidth);
        Assert.Equal(3.0, OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "line", "strokeWidth": 3 }""", XY, Basic), 300, 200), "mark-line").StrokeWidth);

        // Assert: a constant size value of the channel and a row-bound expression give the width too
        const string valued = """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "size": { "value": 7 } }""";
        Assert.Equal(7.0, OnlyPath(ChartTestHelpers.Marks(Chart("\"line\"", valued, Basic), 300, 200), "mark-line").StrokeWidth);
        Assert.Equal(6.0, OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "line", "size": { "expr": "2 * 3" } }""", XY, Basic), 300, 200), "mark-line").StrokeWidth);
    }

    /// <summary>
    ///     Validates the outline of an area between the value and the baseline at zero: the top line forward, the baseline backward, closed.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_AreaPolygonRunsBetweenTheValueAndTheBaseline()
    {
        // Arrange: the scales of the line chart; the baseline is y(0) = 200
        var marks = ChartTestHelpers.Marks(Chart("\"area\"", XY, Basic), 300, 200);

        // Act
        var area = OnlyPath(marks, "mark-area");

        // Assert: the area is filled in the default color without a stroke
        Assert.Equal("M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z", PathText.Of(area));
        Assert.Equal((Blue, (string?)null, 1.0), (area.Fill, area.Stroke, area.Opacity));

        // Assert: a baseline that is not zero when the domain does not hold it and the area is not stacked (an area stacks from zero by default): the baseline is
        // the first domain value, y(4) = 200
        const string high = """[ { "x": 0, "y": 6 }, { "x": 1, "y": 10 }, { "x": 2, "y": 8 } ]""";
        const string floating = """{ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative", "stack": null, "scale": { "zero": false, "domain": [4, 10], "nice": false } } }""";
        var shifted = OnlyPath(ChartTestHelpers.Marks(Chart("\"area\"", floating, high), 300, 200), "mark-area");
        Assert.Equal("M0,133.333333 L150,0 L300,66.666667 L300,200 L150,200 L0,200 Z", PathText.Of(shifted));

        // Assert: a discrete dimension puts the vertices at the centers of its points (3 points over 300 pixels, step 100, padding 0.5: 50, 150, 250)
        const string named = """
            { "x": { "field": "k", "type": "nominal" }, "y": { "field": "y", "type": "quantitative" } }
            """;
        const string namedRows = """[ { "k": "p", "y": 0 }, { "k": "q", "y": 10 }, { "k": "r", "y": 5 } ]""";
        var discrete = OnlyPath(ChartTestHelpers.Marks(Chart("\"area\"", named, namedRows), 300, 200), "mark-area");
        Assert.Equal("M50,200 L150,0 L250,100 L250,200 L150,200 L50,200 Z", PathText.Of(discrete));
    }

    /// <summary>
    ///     Validates a stack of areas: the layers meet, and the missing keys of a layer are imputed with zero so that it has a vertex at every key (and not when the impute is off).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_StackedAreaLayersMeetAndImputeMissingKeys()
    {
        // Arrange: a has the keys 0, 1, 2 with the value 2; b has the keys 0 and 2 with the value 1; x(v) = 150 v, y domain [0, 3] so y(v) = 200 - 200/3 v
        const string rows = """
            [ { "x": 0, "y": 2, "g": "a" }, { "x": 1, "y": 2, "g": "a" }, { "x": 2, "y": 2, "g": "a" }, { "x": 0, "y": 1, "g": "b" }, { "x": 2, "y": 1, "g": "b" } ]
            """;
        string Encoding(string measure) => "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": " + measure + ", \"color\": { \"field\": \"g\", \"type\": \"nominal\" } }";
        const string imputed = """{ "field": "y", "type": "quantitative" }""";

        // Act
        var layers = ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"area\"", Encoding(imputed), rows), 300, 200), "mark-area");
        var a = layers.Single(l => l.Fill == Blue);
        var b = layers.Single(l => l.Fill == Orange);

        // Assert: the layers of a stack of y are stacked in descending order of the color, so b is at the bottom (0 to its value, the imputed key 1 has the value 0) and a on top of it
        Assert.Equal("M0,133.333333 L150,200 L300,133.333333 L300,200 L150,200 L0,200 Z", PathText.Of(b));
        Assert.Equal("M0,0 L150,66.666667 L300,0 L300,133.333333 L150,200 L0,133.333333 Z", PathText.Of(a));

        // Assert: the layers meet: the baseline of a is the top line of b, vertex for vertex
        Assert.Equal(7, b.Segments.Count);
        Assert.Equal(7, a.Segments.Count);

        // Act: without the impute (impute: null) b has the vertices of its own rows only
        const string plain = """{ "field": "y", "type": "quantitative", "impute": null }""";
        var withoutImpute = ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"area\"", Encoding(plain), rows), 300, 200), "mark-area");

        // Assert: two vertices on the top line and two on the baseline, closed: M, L, L, L, Z
        Assert.Equal(5, withoutImpute.Single(l => l.Fill == Orange).Segments.Count);
        Assert.Equal("M0,133.333333 L300,133.333333 L300,200 L0,200 Z", PathText.Of(withoutImpute.Single(l => l.Fill == Orange)));
    }

    /// <summary>
    ///     Validates a horizontal area (the value runs along x, the dimension along y) and a range area (<c>y2</c> is the baseline).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_HorizontalAreaAndRangeAreaFollowOrient()
    {
        // Arrange: the measure is x (domain [0, 10], x(v) = 30 v), the dimension is y (domain [0, 2], y(t) = 200 - 100 t); the orient says horizontal
        const string rows = """[ { "t": 0, "v": 0 }, { "t": 1, "v": 10 }, { "t": 2, "v": 5 } ]""";
        const string encoding = """{ "x": { "field": "v", "type": "quantitative" }, "y": { "field": "t", "type": "quantitative" } }""";

        // Act
        var horizontal = OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "area", "orient": "horizontal" }""", encoding, rows), 300, 200), "mark-area");

        // Assert: the rows are sorted by the pixel y (t = 2, 1, 0); the top line is the value x, the baseline x(0) = 0 runs back
        Assert.Equal("M150,0 L300,100 L0,200 L0,200 L0,100 L0,0 Z", PathText.Of(horizontal));

        // Arrange: a range area: y2 is the baseline (y domain [0, 10], y(v) = 200 - 20 v), the orientation is inferred as vertical
        const string ranged = """[ { "x": 0, "y": 4, "q": 1 }, { "x": 1, "y": 10, "q": 2 }, { "x": 2, "y": 6, "q": 3 } ]""";
        const string range = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "y2": { "field": "q" } }
            """;
        var band = OnlyPath(ChartTestHelpers.Marks(Chart("\"area\"", range, ranged), 300, 200), "mark-area");

        // Assert: the band between y and y2
        Assert.Equal("M0,120 L150,0 L300,80 L300,140 L150,160 L0,180 Z", PathText.Of(band));

        // Assert: an area without its measure channel has nothing to span (a nominal x makes the orientation vertical, so the measure is the missing y)
        Assert.Empty(ChartTestHelpers.Marks(Chart("\"area\"", """{ "x": { "field": "k", "type": "nominal" } }""", """[ { "k": "a" }, { "k": "b" } ]"""), 300, 200).Children);
    }

    /// <summary>
    ///     Validates that the points of a trail carry the size of their row, and that the trail is one item with one run.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TrailPointsCarryTheSizeChannel()
    {
        // Arrange: the size scale is the identity from [4, 12] to [4, 12]
        const string rows = """[ { "x": 0, "y": 0, "s": 4 }, { "x": 1, "y": 10, "s": 8 }, { "x": 2, "y": 5, "s": 12 } ]""";
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" },
              "size": { "field": "s", "type": "quantitative", "legend": null, "scale": { "domain": [4, 12], "range": [4, 12], "zero": false } } }
            """;

        // Act
        var trail = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Chart("\"trail\"", encoding, rows), 300, 200), "mark-trail"));

        // Assert: one run with the pixel positions and the sizes of the rows; a trail is filled and its size does not split it
        var run = Assert.Single(trail.Runs);
        Assert.Equal([(0.0, 200.0, 4.0), (150.0, 0.0, 8.0), (300.0, 100.0, 12.0)], run.Select(p => (p.X, p.Y, p.Size)));
        Assert.Equal((Blue, (string?)null), (trail.Fill, trail.Stroke));

        // Assert: without a size channel every point has the size of the Vega style of the trail (2), and the mark property replaces it
        var plain = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Chart("\"trail\"", XY, Basic), 300, 200), "mark-trail"));
        Assert.All(Assert.Single(plain.Runs), p => Assert.Equal(2.0, p.Size));
        var sized = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Chart("""{ "type": "trail", "size": 9 }""", XY, Basic), 300, 200), "mark-trail"));
        Assert.All(Assert.Single(sized.Runs), p => Assert.Equal(9.0, p.Size));
        var computed = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Chart("""{ "type": "trail", "size": { "expr": "1 + 4" } }""", XY, Basic), 300, 200), "mark-trail"));
        Assert.All(Assert.Single(computed.Runs), p => Assert.Equal(5.0, p.Size));
    }

    /// <summary>
    ///     Validates that an invalid row ends a run of the trail, and that a run of one point draws nothing.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_TrailBreaksAtInvalidRows()
    {
        // Arrange: x 0..4 (x(v) = 75 v), y domain [0, 4] (y(v) = 200 - 50 v); the middle row has no y
        const string rows = """[ { "x": 0, "y": 1 }, { "x": 1, "y": 2 }, { "x": 2, "y": null }, { "x": 3, "y": 3 }, { "x": 4, "y": 4 } ]""";

        // Act
        var trail = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Chart("\"trail\"", XY, rows), 300, 200), "mark-trail"));

        // Assert: two runs of two points
        Assert.Equal(
            [[(0.0, 150.0), (75.0, 100.0)], [(225.0, 50.0), (300.0, 0.0)]],
            trail.Runs.Select(r => r.Select(p => (p.X, p.Y)).ToList()));

        // Arrange: the second row is invalid: a run of one point (the first row) and a run of two
        const string single = """[ { "x": 0, "y": 1 }, { "x": 1, "y": null }, { "x": 2, "y": 2 }, { "x": 3, "y": 3 } ]""";
        var partial = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Chart("\"trail\"", XY, single), 300, 200), "mark-trail"));

        // Assert: only the run of two points is kept
        Assert.Single(partial.Runs);
        Assert.Equal(2, partial.Runs[0].Count);

        // Assert: nothing but single points means no trail at all
        Assert.Empty(ChartTestHelpers.Marks(Chart("\"trail\"", XY, """[ { "x": 0, "y": 1 }, { "x": 1, "y": null }, { "x": 2, "y": 2 } ]"""), 300, 200).Children);
    }

    /// <summary>
    ///     Validates that <c>interpolate</c> and <c>tension</c> choose the curve, with outlines derived by hand for the step curves and from the control point rule of the cardinal curve.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InterpolateAndTensionSelectTheCurve()
    {
        // Arrange: the identity scales of a 100 by 100 chart; the points are (0, 0), (50, 50), (100, 25) in pixels, that is y = 100, 50, 75
        const string rows = """[ { "x": 0, "y": 100 }, { "x": 50, "y": 50 }, { "x": 100, "y": 75 } ]""";
        string Outline(string mark) => PathText.Of(OnlyPath(ChartTestHelpers.Marks(Square(mark, rows), 100, 100), "mark-line"));

        // Assert: the default is the linear curve
        Assert.Equal("M0,0 L50,50 L100,25", Outline("\"line\""));
        Assert.Equal("M0,0 L50,50 L100,25", Outline("""{ "type": "line", "interpolate": "linear" }"""));

        // Assert: the step curves, with the flush of the last point (t = 0.5 turns at the middle between two points)
        Assert.Equal("M0,0 L25,0 L25,50 L75,50 L75,25 L100,25", Outline("""{ "type": "line", "interpolate": "step" }"""));
        Assert.Equal("M0,0 L50,0 L50,50 L100,50 L100,25", Outline("""{ "type": "line", "interpolate": "step-after" }"""));
        Assert.Equal("M0,0 L0,50 L50,50 L50,25 L100,25", Outline("""{ "type": "line", "interpolate": "step-before" }"""));

        // Assert: a cardinal curve with the tension 1 has k = 0, so every control point is an end point of its piece: C P0 P1 P1 and C P1 P2 P2
        var flat = Outline("""{ "type": "line", "interpolate": "cardinal", "tension": 1 }""");
        Assert.Equal("M0,0 C0,0 50,50 50,50 C50,50 100,25 100,25", flat);

        // Assert: the default tension 0 is another curve (k = 1/6), and a curve through the same points
        var cardinal = Outline("""{ "type": "line", "interpolate": "cardinal" }""");
        Assert.NotEqual(flat, cardinal);
        Assert.StartsWith("M0,0 C", cardinal, StringComparison.Ordinal);
        Assert.EndsWith("100,25", cardinal, StringComparison.Ordinal);

        // Assert: the monotone curve of a line is MonotoneX and is the d3 curve of the same points
        var expected = new List<ScenePathSegment>();
        CurveBuilder.Line(CurveKind.MonotoneX, null, [new CurvePoint(0, 0, true), new CurvePoint(50, 50, true), new CurvePoint(100, 25, true)], expected, TestContext.Current.CancellationToken);
        Assert.Equal(PathText.Of(new ScenePath("expected", expected)), Outline("""{ "type": "line", "interpolate": "monotone" }"""));

        // Assert: a trail has no curve (interpolate is ignored): it is one run of the three points
        var trail = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Square("""{ "type": "trail", "interpolate": "basis" }""", rows), 100, 100), "mark-trail"));
        Assert.Equal(3, Assert.Single(trail.Runs).Count);
    }

    /// <summary>
    ///     Validates that an area with the <c>bundle</c> interpolation (which d3-shape cannot draw as an area) is drawn with the linear interpolation.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_AreaBundleFallsBackToLinear()
    {
        // Arrange / Act
        var bundle = OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "area", "interpolate": "bundle", "tension": 0.5 }""", XY, Basic), 300, 200), "mark-area");

        // Assert: the linear area of the same data
        Assert.Equal("M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z", PathText.Of(bundle));

        // Assert: the bundle curve of a line is a curve of its own (it is not linear)
        var line = OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "line", "interpolate": "bundle" }""", XY, Basic), 300, 200), "mark-line");
        Assert.Contains("C", PathText.Of(line), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the default invalid mode breaks the path at a row without a number, and that the scales still contain the whole data.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_DefaultInvalidModeBreaksThePath()
    {
        // Arrange: x 0..4 (x(v) = 75 v), y domain [0, 4] (y(v) = 200 - 50 v); the third row has no y
        const string rows = """[ { "x": 0, "y": 1 }, { "x": 1, "y": 2 }, { "x": 2, "y": null }, { "x": 3, "y": 3 }, { "x": 4, "y": 4 } ]""";

        // Act
        var line = OnlyPath(ChartTestHelpers.Marks(Chart("\"line\"", XY, rows), 300, 200), "mark-line");

        // Assert: two subpaths
        Assert.Equal("M0,150 L75,100 M225,50 L300,0", PathText.Of(line));

        // Assert: the area breaks the same way: two closed polygons (the baseline is y(0) = 200); the stack of an area turns a null into 0, so the area is not stacked here
        var area = OnlyPath(ChartTestHelpers.Marks(Chart("\"area\"", Unstacked, rows), 300, 200), "mark-area");
        Assert.Equal("M0,150 L75,100 L75,200 L0,200 Z M225,50 L300,0 L300,200 L225,200 Z", PathText.Of(area));

        // Assert: the other break-paths modes break the path as well, and a row that is not a number (text) is invalid like a null
        foreach (var mode in new[] { "break-paths-filter-domains", "break-paths-show-domains", "break-paths-show-path-domains" })
        {
            var broken = OnlyPath(ChartTestHelpers.Marks(Chart("{ \"type\": \"line\", \"invalid\": \"" + mode + "\" }", XY, rows), 300, 200), "mark-line");
            Assert.Equal("M0,150 L75,100 M225,50 L300,0", PathText.Of(broken));
        }

        var text = OnlyPath(ChartTestHelpers.Marks(Chart("\"line\"", XY, rows.Replace("null", "\"n/a\"", StringComparison.Ordinal)), 300, 200), "mark-line");
        Assert.Equal("M0,150 L75,100 M225,50 L300,0", PathText.Of(text));
    }

    /// <summary>
    ///     Validates that the <c>filter</c> invalid mode drops the invalid rows and connects their neighbors.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InvalidFilterConnectsTheNeighbors()
    {
        // Arrange: the same data as the default mode; the scales do not change (they include the filtered rows)
        const string rows = """[ { "x": 0, "y": 1 }, { "x": 1, "y": 2 }, { "x": 2, "y": null }, { "x": 3, "y": 3 }, { "x": 4, "y": 4 } ]""";

        // Act
        var line = OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "line", "invalid": "filter" }""", XY, rows), 300, 200), "mark-line");

        // Assert: one connected path over the valid rows
        Assert.Equal("M0,150 L75,100 L225,50 L300,0", PathText.Of(line));

        // Assert: the configuration sets the mode for the mark (config.line.invalid), and the mark definition wins over it
        var configured = OnlyPath(ChartTestHelpers.Marks(Chart("\"line\"", XY, rows, """{ "line": { "invalid": "filter" } }"""), 300, 200), "mark-line");
        Assert.Equal("M0,150 L75,100 L225,50 L300,0", PathText.Of(configured));
        var overridden = OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "line", "invalid": "break-paths-show-domains" }""", XY, rows, """{ "line": { "invalid": "filter" } }"""), 300, 200), "mark-line");
        Assert.Equal("M0,150 L75,100 M225,50 L300,0", PathText.Of(overridden));

        // Assert: an area connects its neighbors too
        var area = OnlyPath(ChartTestHelpers.Marks(Chart("""{ "type": "area", "invalid": "filter" }""", Unstacked, rows), 300, 200), "mark-area");
        Assert.Equal("M0,150 L75,100 L225,50 L300,0 L300,200 L225,200 L75,200 L0,200 Z", PathText.Of(area));

        // Assert: a mark other than a path does not draw an invalid row, whatever the mode
        Assert.Equal(4, ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Chart("\"point\"", XY, rows), 300, 200), "mark-point").Count);
    }

    /// <summary>
    ///     Validates that <c>invalid: "show"</c> (and null) behaves as the default mode and that the feature catalog reports it as unsupported at the value.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InvalidShowIsTheDefaultModeAndReported()
    {
        // Arrange
        const string rows = """[ { "x": 0, "y": 1 }, { "x": 1, "y": 2 }, { "x": 2, "y": null }, { "x": 3, "y": 3 }, { "x": 4, "y": 4 } ]""";

        foreach (var invalid in new[] { "\"show\"", "null" })
        {
            var json = Chart("{ \"type\": \"line\", \"invalid\": " + invalid + " }", XY, rows);

            // Act
            var line = OnlyPath(ChartTestHelpers.Marks(json, 300, 200), "mark-line");
            var reports = FeatureCatalog.Visit(SpecParser.Parse(json)).Where(r => r.Id.StartsWith("vl-mark-", StringComparison.Ordinal)).ToList();

            // Assert: the path is broken as in the default mode, and the mode is reported at the value
            Assert.Equal("M0,150 L75,100 M225,50 L300,0", PathText.Of(line));
            var report = Assert.Single(reports);
            Assert.Equal(("vl-mark-invalid-show", "/mark/invalid", FeatureReportKind.UnsupportedFeature), (report.Id, report.Location.Pointer, report.Kind));
        }

        // Assert: a configured mode that the renderer does not know is ignored (the default applies)
        var ignored = OnlyPath(ChartTestHelpers.Marks(Chart("\"line\"", XY, rows, """{ "line": { "invalid": "bogus" } }"""), 300, 200), "mark-line");
        Assert.Equal("M0,150 L75,100 M225,50 L300,0", PathText.Of(ignored));
    }

    /// <summary>
    ///     Validates that the dash pattern, the dash offset, the cap, the join and the miter limit of the mark reach the items of every mark type.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_StrokeDashCapJoinMiterAndOffsetReachEveryItemType()
    {
        foreach (var type in new[] { "line", "area", "trail", "bar", "rect", "point", "circle", "square", "rule", "tick" })
        {
            // Arrange
            var mark = "{ \"type\": \"" + type + "\", \"strokeDash\": [4, 2], \"strokeDashOffset\": 3, \"strokeCap\": \"round\", \"strokeJoin\": \"bevel\", \"strokeMiterLimit\": 2 }";

            // Act
            var item = ChartTestHelpers.Marks(Chart(mark, XY, Basic), 300, 200).Children[0];

            // Assert
            Assert.Equal(new[] { 4.0, 2.0 }, item.StrokeDash!.ToArray());
            Assert.Equal((3.0, "round", "bevel", 2.0), (item.StrokeDashOffset, item.StrokeCap, item.StrokeJoin, item.StrokeMiterLimit));
        }

        // Assert: without the properties the items have the defaults
        var plain = ChartTestHelpers.Marks(Chart("\"line\"", XY, Basic), 300, 200).Children[0];
        Assert.Equal((0.0, (string?)null, (string?)null, (double?)null), (plain.StrokeDashOffset, plain.StrokeCap, plain.StrokeJoin, plain.StrokeMiterLimit));
    }

    /// <summary>
    ///     Validates that the segments of a path are charged to the scene budget as an upper bound before the path exists, and that the exact count is within the charge.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PathSegmentsAreChargedBeforeCreation()
    {
        // Arrange: three points in one run: the bound of a linear line is 3 + 1 = 4 segments (the move of the run), charged with the item: 1 + 4, plus the marks group
        var parts = ChartTestHelpers.Parts(Chart("\"line\"", XY, Basic));
        var budget = new SceneBudget(6);

        // Act
        var group = new MarkBuilder(parts.Config, null, budget, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken);

        // Assert: exactly the budget was needed, and the real count (three segments) is within the charge
        Assert.Equal(6, budget.Count);
        Assert.True(OnlyPath(group, "mark-line").Segments.Count <= 4);

        // Act / Assert: one item less in the budget and the path is refused, with the location of the mark, after the charge and before any segment exists
        var tight = new SceneBudget(5);
        var failure = Assert.Throws<VegaLiteRenderException>(() => new MarkBuilder(parts.Config, null, tight, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken));
        Assert.Equal("/mark", failure.Location.Pointer);
        Assert.Contains("MaxSceneItems", failure.Message, StringComparison.Ordinal);
        Assert.Equal(6, tight.Count);

        // Arrange: an area writes every point twice: the bound is 2 * 3 + 1 = 7, so the area costs 1 + 7 + 1
        var area = ChartTestHelpers.Parts(Chart("\"area\"", XY, Basic));
        var areaBudget = new SceneBudget(9);
        new MarkBuilder(area.Config, null, areaBudget, "test.vl.json").Build(area.Tree.Root, area.Table, area.Scales, 300, 200, TestContext.Current.CancellationToken);
        Assert.Equal(9, areaBudget.Count);

        // Act / Assert: a long line is refused by a small limit whatever its length (the limit is the same for the data and the segments)
        var rows = "[" + string.Join(", ", Enumerable.Range(0, 2000).Select(i => "{ \"x\": " + i + ", \"y\": " + (i % 7) + " }")) + "]";
        var limits = new VegaLiteLimits { MaxSceneItems = 500 };
        var many = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Marks(Chart("\"line\"", XY, rows), 300, 200, limits: limits));
        Assert.Contains("MaxSceneItems", many.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that building the paths polls the cancellation token and charges the evaluation steps (one for every 64 rows and for every 64 comparisons of a sort).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PathBuildPollsCancellationAndChargesSteps()
    {
        // Arrange
        var parts = ChartTestHelpers.Parts(Chart("\"line\"", XY, Basic));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert: a token that is canceled stops the build (the group loop and the curve poll it)
        Assert.Throws<OperationCanceledException>(() => new MarkBuilder(parts.Config, null, null, "test.vl.json", parts.Expressions).Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, canceled.Token));

        // Arrange: 640 rows cost 10 steps to read, grouped (no grouping field: 640 elements, 10 steps) and sorted (640 * 10 levels): more than the limit of 5 steps
        var rows = "[" + string.Join(", ", Enumerable.Range(0, 640).Select(i => "{ \"x\": " + (639 - i) + ", \"y\": " + (i % 5) + " }")) + "]";
        var limits = new VegaLiteLimits { MaxEvaluationSteps = 5 };

        // Act / Assert: the steps are exhausted by the marks (the data stage of this chart uses none): a located render exception names the limit
        var failure = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Marks(Chart("\"line\"", XY, rows), 300, 200, limits: limits));
        Assert.Equal("/mark", failure.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", failure.Message, StringComparison.Ordinal);

        // Assert: with the default limit the same chart is built
        Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(Chart("\"line\"", XY, rows), 300, 200), "mark-line"));
    }

    /// <summary>
    ///     Validates that a group that has fewer than two points draws nothing and costs nothing of the scene budget.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_GroupsWithoutTwoPointsCostNothing()
    {
        // Arrange: a has three points; b has one point; c has two rows without a y (no defined point)
        const string rows = """
            [ { "x": 0, "y": 1, "g": "a" }, { "x": 1, "y": 2, "g": "a" }, { "x": 2, "y": 3, "g": "a" }, { "x": 1, "y": 4, "g": "b" },
              { "x": 0, "y": null, "g": "c" }, { "x": 1, "y": null, "g": "c" } ]
            """;
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }
            """;
        var parts = ChartTestHelpers.Parts(Chart("\"line\"", encoding, rows));
        var budget = new SceneBudget(1000);

        // Act
        var group = new MarkBuilder(parts.Config, null, budget, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 300, 200, TestContext.Current.CancellationToken);

        // Assert: only a is drawn; the marks group and the path of a (1 + 3 + 1) are all that was charged
        Assert.Single(group.Children);
        Assert.Equal(6, budget.Count);

        // Assert: the same for a trail (1 + the three points of a)
        var trail = ChartTestHelpers.Parts(Chart("\"trail\"", encoding, rows));
        var trailBudget = new SceneBudget(1000);
        var trails = new MarkBuilder(trail.Config, null, trailBudget, "test.vl.json").Build(trail.Tree.Root, trail.Table, trail.Scales, 300, 200, TestContext.Current.CancellationToken);
        Assert.Single(trails.Children);
        Assert.Equal(5, trailBudget.Count);
    }

    /// <summary>
    ///     Validates that a row whose position is beyond the magnitude bound of the painter (one million scene units) is undefined like a row without a value: it breaks the path of a line, an
    ///     area and a trail, instead of reaching the painter, and the segment bound that is charged still holds because the same rule counts the defined points.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PointsBeyondTheMagnitudeBoundBreakThePath()
    {
        // Arrange: a 100 by 100 chart with identity scales (x(v) = v, y(v) = 100 - v); the third row (sorted by x) has a y of 1e9, a billion pixels above the chart
        const string rows = """[ { "x": 0, "y": 0 }, { "x": 10, "y": 10 }, { "x": 15, "y": 1e9 }, { "x": 20, "y": 20 }, { "x": 30, "y": 10 } ]""";

        // Act / Assert: the line is two subpaths, as at a null (a run of two points does not close)
        Assert.Equal("M0,100 L10,90 M20,80 L30,90", PathText.Of(OnlyPath(ChartTestHelpers.Marks(Square("\"line\"", rows), 100, 100), "mark-line")));

        // Act / Assert: so is a basis curve (the bound still covers it: 4 defined points in 2 runs, at most (4 - 2) / 2 = 1 of them long: 4 + 2 + 1 = 7 segments, and the real count is 2 + 2)
        var parts = ChartTestHelpers.Parts(Square("{ \"type\": \"line\", \"interpolate\": \"basis\" }", rows));
        var budget = new SceneBudget(100);
        var group = new MarkBuilder(parts.Config, null, budget, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 100, 100, TestContext.Current.CancellationToken);
        Assert.Equal(4, OnlyPath(group, "mark-line").Segments.Count);
        Assert.Equal(1 + 1 + 7, budget.Count);

        // Act / Assert: the trail has two runs of two points, in the order of x
        var trail = Assert.Single(ChartTestHelpers.Of<SceneTrail>(ChartTestHelpers.Marks(Square("\"trail\"", rows), 100, 100), "mark-trail"));
        Assert.Equal("0,100 10,90 | 20,80 30,90", string.Join(" | ", trail.Runs.Select(r => string.Join(' ', r.Select(p => $"{p.X},{p.Y}")))));

        // Act / Assert: an area whose baseline is beyond the bound breaks too (the y2 of the third row is a billion pixels away)
        const string rangeRows = """[ { "x": 0, "y": 0, "y2": 5 }, { "x": 10, "y": 10, "y2": 5 }, { "x": 15, "y": 10, "y2": 1e9 }, { "x": 20, "y": 20, "y2": 5 }, { "x": 30, "y": 10, "y2": 5 } ]""";
        const string rangeEncoding = """
            { "x": { "field": "x", "type": "quantitative", "scale": { "domain": [0, 100], "nice": false } },
              "y": { "field": "y", "type": "quantitative", "scale": { "domain": [0, 100], "nice": false } },
              "y2": { "field": "y2" } }
            """;
        var area = ChartTestHelpers.Marks("{ \"width\": 100, \"height\": 100, \"data\": { \"values\": " + rangeRows + " }, \"mark\": \"area\", \"encoding\": " + rangeEncoding + " }", 100, 100);
        Assert.Equal("M0,100 L10,90 L10,95 L0,95 Z M20,80 L30,90 L30,95 L20,95 Z", PathText.Of(OnlyPath(area, "mark-area")));
    }
}
