using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the arc mark of <see cref="MarkBuilder"/>: pies, donuts and radial charts (the angles and the radii of each slice), the constants of the mark properties, the pad angle, the corner
///     radius and the paint, the center, and the charges of the slices. The expectations are derived by hand from the scale formulas (a stacked <c>theta</c> runs over <c>[0, 2 PI]</c>, a
///     <c>radius</c> over <c>[0, min(width, height) / 2]</c>); no pixel is painted, so nothing depends on a platform.
/// </summary>
public class MarkBuilderArcTests
{
    /// <summary>The first color of the tableau10 palette, which is the default color of a color scale and of a mark.</summary>
    private const string Blue = "#4c78a8";

    /// <summary>The second color of the tableau10 palette.</summary>
    private const string Orange = "#f58518";

    /// <summary>The third color of the tableau10 palette.</summary>
    private const string Red = "#e45756";

    /// <summary>The tolerance of the comparisons of angles and radii.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The data of a pie of three slices with the values 1, 1 and 2 (a quarter, a quarter and a half).</summary>
    private const string PieRows = """[ { "k": "a", "v": 1 }, { "k": "b", "v": 1 }, { "k": "c", "v": 2 } ]""";

    /// <summary>The encoding of a pie: the value stacked on <c>theta</c> and the slices colored by their name.</summary>
    private const string PieEncoding = """{ "theta": { "field": "v", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } }""";

    /// <summary>
    ///     Builds a specification of a mark in a view of the given size.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="config">The JSON text of the <c>config</c> object, or null.</param>
    /// <param name="width">The width of the view.</param>
    /// <param name="height">The height of the view.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string encoding, string values, string? config = null, int width = 200, int height = 200) =>
        "{ \"width\": " + width + ", \"height\": " + height + ", \"data\": { \"values\": " + values + " }, \"mark\": " + mark + ", \"encoding\": " + encoding
        + (config is null ? string.Empty : ", \"config\": " + config) + " }";

    /// <summary>
    ///     Builds the arcs of a chart.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="values">The JSON text of the data values array.</param>
    /// <param name="config">The JSON text of the <c>config</c> object, or null.</param>
    /// <param name="width">The width of the view.</param>
    /// <param name="height">The height of the view.</param>
    /// <returns>The arcs in drawing order.</returns>
    private static List<SceneArc> Arcs(string mark, string encoding, string values, string? config = null, int width = 200, int height = 200) =>
        ChartTestHelpers.Of<SceneArc>(ChartTestHelpers.Marks(Chart(mark, encoding, values, config, width, height), width, height), "mark-arc");

    /// <summary>
    ///     Asserts the angles and the radii of an arc.
    /// </summary>
    /// <param name="arc">The arc.</param>
    /// <param name="start">The expected start angle.</param>
    /// <param name="end">The expected end angle.</param>
    /// <param name="inner">The expected inner radius.</param>
    /// <param name="outer">The expected outer radius.</param>
    private static void AssertSector(SceneArc arc, double start, double end, double inner, double outer)
    {
        Assert.Equal(start, arc.StartAngle, Tolerance);
        Assert.Equal(end, arc.EndAngle, Tolerance);
        Assert.Equal(inner, arc.InnerRadius, Tolerance);
        Assert.Equal(outer, arc.OuterRadius, Tolerance);
    }

    /// <summary>
    ///     Validates a pie: the values 1, 1 and 2 stacked on <c>theta</c> make the domain <c>[0, 4]</c> on the range <c>[0, 2 PI]</c>, so the slices span <c>(0, PI/2)</c>, <c>(PI/2, PI)</c> and
    ///     <c>(PI, 2 PI)</c>; the radius runs from 0 to half of the smaller side (100), the center is the middle of the view, and every slice has the color of its name.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PieSlicesHaveHandDerivedAnglesAndRadii()
    {
        // Act
        var marks = ChartTestHelpers.Marks(Chart("\"arc\"", PieEncoding, PieRows), 200, 200);
        var arcs = ChartTestHelpers.Of<SceneArc>(marks, "mark-arc");

        // Assert: one arc for each row, in the order of the data, at the middle of the 200 by 200 data rectangle
        Assert.Equal(3, arcs.Count);
        Assert.Equal(3, marks.Children.Count);
        Assert.All(arcs, a => Assert.Equal((100.0, 100.0), (a.X, a.Y)));

        // Assert: theta(v) = 2 PI v / 4, stacked in the order of the color field: a is [0, 1], b is [1, 2], c is [2, 4]
        AssertSector(arcs[0], 0, Math.PI / 2, 0, 100);
        AssertSector(arcs[1], Math.PI / 2, Math.PI, 0, 100);
        AssertSector(arcs[2], Math.PI, 2 * Math.PI, 0, 100);

        // Assert: the slices meet, and the paint is that of a filled mark (the color is the fill, no stroke, no padding, no rounding)
        Assert.Equal(arcs[0].EndAngle, arcs[1].StartAngle, Tolerance);
        Assert.Equal(arcs[1].EndAngle, arcs[2].StartAngle, Tolerance);
        Assert.Equal([Blue, Orange, Red], arcs.Select(a => a.Fill));
        Assert.All(arcs, a => Assert.Equal(((string?)null, 1.0, 0.0, 0.0), (a.Stroke, a.Opacity, a.PadAngle, a.CornerRadius)));

        // Assert: the radius follows the smaller side of the data rectangle (half of 300 by 120 is 60)
        var wide = Arcs("\"arc\"", PieEncoding, PieRows, width: 300, height: 120);
        Assert.All(wide, a => Assert.Equal((150.0, 60.0, 0.0, 60.0), (a.X, a.Y, a.InnerRadius, a.OuterRadius)));

        // Assert: a single value without a color is the whole circle
        var whole = Assert.Single(Arcs("\"arc\"", """{ "theta": { "field": "v", "type": "quantitative" } }""", """[ { "v": 5 } ]"""));
        AssertSector(whole, 0, 2 * Math.PI, 0, 100);
        Assert.Equal(Blue, whole.Fill);

        // Assert: a row whose unstacked value is not a number is not drawn; the others keep their places (the domain [0, 2] over 2 PI: 1 is half the circle, 2 the whole)
        const string unstacked = """{ "theta": { "field": "v", "type": "quantitative", "stack": false } }""";
        var gap = Arcs("\"arc\"", unstacked, """[ { "v": 1 }, { "v": null }, { "v": 2 } ]""");
        Assert.Equal(2, gap.Count);
        AssertSector(gap[0], 0, Math.PI, 0, 100);
        AssertSector(gap[1], 0, 2 * Math.PI, 0, 100);

        // Assert: an arc without any channel is a full disc, as Vega-Lite draws it
        var disc = Assert.Single(Arcs("\"arc\"", "{ }", """[ { "v": 1 } ]"""));
        AssertSector(disc, 0, 2 * Math.PI, 0, 100);
    }

    /// <summary>
    ///     Validates a donut (<c>innerRadius</c>), the constants of the mark properties <c>radius</c>, <c>radius2</c>, <c>theta</c> and <c>theta2</c> and their aliases, a radial chart whose
    ///     <c>radius</c> is a square root scale with <c>rangeMin</c> 20, stacked rings, and a band scale on <c>theta</c>.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_DonutRadialAndConstantRadiiUseTheMarkProperties()
    {
        // Assert: innerRadius 50 makes a donut of the pie (the outer radius is the default 100)
        var donut = Arcs("""{ "type": "arc", "innerRadius": 50 }""", PieEncoding, PieRows);
        AssertSector(donut[0], 0, Math.PI / 2, 50, 100);
        AssertSector(donut[2], Math.PI, 2 * Math.PI, 50, 100);

        // Assert: outerRadius and innerRadius are the aliases of radius and radius2, which win when both are given
        var aliases = Assert.Single(Arcs("""{ "type": "arc", "outerRadius": 80, "innerRadius": 30 }""", "{ }", """[ { "v": 1 } ]"""));
        AssertSector(aliases, 0, 2 * Math.PI, 30, 80);
        var explicitOnes = Assert.Single(Arcs("""{ "type": "arc", "radius": 90, "radius2": 10, "outerRadius": 80, "innerRadius": 30 }""", "{ }", """[ { "v": 1 } ]"""));
        AssertSector(explicitOnes, 0, 2 * Math.PI, 10, 90);

        // Assert: the larger radius is the outer one whichever property holds it
        var swapped = Assert.Single(Arcs("""{ "type": "arc", "radius": 20, "radius2": 70 }""", "{ }", """[ { "v": 1 } ]"""));
        AssertSector(swapped, 0, 2 * Math.PI, 20, 70);

        // Assert: theta and theta2 are the angles of a constant sector, in radians clockwise from twelve o'clock
        var sector = Assert.Single(Arcs("""{ "type": "arc", "theta": 1, "theta2": 2.5 }""", "{ }", """[ { "v": 1 } ]"""));
        AssertSector(sector, 1, 2.5, 0, 100);
        var fromConfig = Assert.Single(Arcs("\"arc\"", "{ }", """[ { "v": 1 } ]""", """{ "arc": { "radius": 60, "radius2": 20 } }"""));
        AssertSector(fromConfig, 0, 2 * Math.PI, 20, 60);

        // Assert: radius sqrt scale, zero, rangeMin 20 on a 300 by 200 view (R = 100); the values 1, 4, 9 give the domain [0, 9], so the outer radius is 20 + 80 sqrt(v / 9) = 20 + 80 / 3, 20 + 160 / 3, 100
        // and the inner one is the scaled zero (20); theta stacks the values over [0, 14]: [0, 1], [1, 5], [5, 14] of 2 PI
        const string radial = """
            { "theta": { "field": "v", "type": "quantitative", "stack": true }, "radius": { "field": "v", "type": "quantitative", "scale": { "type": "sqrt", "zero": true, "rangeMin": 20 } } }
            """;
        var rays = Arcs("\"arc\"", radial, """[ { "v": 1 }, { "v": 4 }, { "v": 9 } ]""", width: 300, height: 200);
        Assert.Equal(3, rays.Count);
        AssertSector(rays[0], 0, 2 * Math.PI / 14, 20, 20 + (80.0 / 3));
        AssertSector(rays[1], 2 * Math.PI / 14, 2 * Math.PI * 5 / 14, 20, 20 + (160.0 / 3));
        AssertSector(rays[2], 2 * Math.PI * 5 / 14, 2 * Math.PI, 20, 100);
        Assert.All(rays, a => Assert.Equal((150.0, 100.0), (a.X, a.Y)));

        // Assert: a radius stacked by color makes rings: the domain [0, 4] runs over [0, 100], so a is [0, 25], b is [25, 50], c is [50, 100] of the full circle
        const string rings = """{ "radius": { "field": "v", "type": "quantitative", "stack": true }, "color": { "field": "k", "type": "nominal" } }""";
        var circles = Arcs("\"arc\"", rings, PieRows);
        AssertSector(circles[0], 0, 2 * Math.PI, 0, 25);
        AssertSector(circles[1], 0, 2 * Math.PI, 25, 50);
        AssertSector(circles[2], 0, 2 * Math.PI, 50, 100);

        // Assert: a theta that is a band (a nominal field with a band scale of three categories) gives each category a third of the circle
        var bands = Arcs("""{ "type": "arc" }""", """{ "theta": { "field": "k", "type": "nominal", "scale": { "type": "band" } } }""", PieRows);
        Assert.Equal(3, bands.Count);
        AssertSector(bands[0], 0, 2 * Math.PI / 3, 0, 100);
        AssertSector(bands[1], 2 * Math.PI / 3, 4 * Math.PI / 3, 0, 100);
        AssertSector(bands[2], 4 * Math.PI / 3, 2 * Math.PI, 0, 100);

        // Assert: a secondary channel with a field makes a ranged arc (no stack): theta 1 to theta2 3 on the domain [0, 3] is the sector (2 PI / 3, 2 PI); radius 20 to radius2 60 on [0, 60] over [0, 100] is the ring (100 / 3, 100)
        var ranged = Assert.Single(Arcs(
            "\"arc\"",
            """
            { "theta": { "field": "a", "type": "quantitative" }, "theta2": { "field": "b", "type": "quantitative" },
              "radius": { "field": "c", "type": "quantitative" }, "radius2": { "field": "d", "type": "quantitative" } }
            """,
            """[ { "a": 1, "b": 3, "c": 20, "d": 60 } ]"""));
        AssertSector(ranged, 2 * Math.PI / 3, 2 * Math.PI, 100.0 / 3, 100);

        // Assert: an unstacked theta runs from zero to its value (the domain [0, 2] over 2 PI: 2 is the whole circle), and a theta2 value starts the sector there
        var toValue = Assert.Single(Arcs(
            "\"arc\"",
            """{ "theta": { "field": "v", "type": "quantitative", "stack": false }, "theta2": { "value": 1 } }""",
            """[ { "v": 2 } ]"""));
        AssertSector(toValue, 1, 2 * Math.PI, 0, 100);
        var upTo = Assert.Single(Arcs("\"arc\"", """{ "theta": { "field": "v", "type": "quantitative", "stack": false } }""", """[ { "v": 2 } ]"""));
        AssertSector(upTo, 0, 2 * Math.PI, 0, 100);

        // Assert: a constant value channel is the main end (the angle in radians, the radius in pixels), and the mark properties give the other end
        var constants = Assert.Single(Arcs(
            """{ "type": "arc", "theta2": 0.5, "radius2": 10 }""",
            """{ "theta": { "value": 2 }, "radius": { "value": 70 } }""",
            """[ { "v": 1 } ]"""));
        AssertSector(constants, 0.5, 2, 10, 70);
    }

    /// <summary>
    ///     Validates that <c>padAngle</c>, <c>cornerRadius</c>, the stroke, the opacity and the fill of the mark definition reach every item, that the configuration of the arc is the default of
    ///     the mark definition, and that the paint channels drive the fill.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ArcPadAngleCornerRadiusAndPaintReachTheItem()
    {
        // Act
        var arcs = Arcs(
            """{ "type": "arc", "padAngle": 0.05, "cornerRadius": 4, "stroke": "white", "strokeWidth": 2, "opacity": 0.8, "fillOpacity": 0.5, "strokeOpacity": 0.25, "strokeDash": [4, 2], "color": "green" }""",
            PieEncoding,
            PieRows);

        // Assert: the properties are on every slice; the color channel wins over the color of the mark for the fill
        Assert.All(arcs, a =>
        {
            Assert.Equal((0.05, 4.0), (a.PadAngle, a.CornerRadius));
            Assert.Equal(("white", 2.0, 0.8, 0.5, 0.25), (a.Stroke, a.StrokeWidth, a.Opacity, a.FillOpacity, a.StrokeOpacity));
            Assert.Equal([4.0, 2.0], a.StrokeDash);
        });
        Assert.Equal([Blue, Orange, Red], arcs.Select(a => a.Fill));

        // Assert: without a color channel the color of the mark is the fill
        var green = Assert.Single(Arcs("""{ "type": "arc", "color": "green" }""", "{ }", """[ { "v": 1 } ]"""));
        Assert.Equal(("green", (string?)null), (green.Fill, green.Stroke));

        // Assert: the configuration of the arc gives the pad angle and the corner radius, and the mark definition wins over it
        const string config = """{ "arc": { "padAngle": 0.1, "cornerRadius": 3 } }""";
        var configured = Assert.Single(Arcs("\"arc\"", "{ }", """[ { "v": 1 } ]""", config));
        Assert.Equal((0.1, 3.0), (configured.PadAngle, configured.CornerRadius));
        var overridden = Assert.Single(Arcs("""{ "type": "arc", "padAngle": 0.2 }""", "{ }", """[ { "v": 1 } ]""", config));
        Assert.Equal((0.2, 3.0), (overridden.PadAngle, overridden.CornerRadius));

        // Assert: a fill and a stroke channel drive the paint of each slice, an opacity value applies to all
        const string painted = """
            { "theta": { "field": "v", "type": "quantitative" }, "fill": { "field": "k", "type": "nominal" }, "stroke": { "value": "black" }, "opacity": { "value": 0.5 } }
            """;
        var byFill = Arcs("\"arc\"", painted, PieRows);
        Assert.Equal([Blue, Orange, Red], byFill.Select(a => a.Fill));
        Assert.All(byFill, a => Assert.Equal(("black", 0.5), (a.Stroke, a.Opacity)));
    }

    /// <summary>
    ///     Validates the center of an arc: the middle of the data rectangle without <c>x</c> and <c>y</c>, the position of a value of the channels, and the center of the band or point of a
    ///     discrete field; the radius still follows the smaller side of the view.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ArcCenterFollowsXAndY()
    {
        // Assert: pixel values of x and y are the center, whatever the size of the view
        var fixedCenter = Assert.Single(Arcs("\"arc\"", """{ "x": { "value": 60 }, "y": { "value": 80 } }""", """[ { "v": 1 } ]""", width: 300, height: 200));
        AssertSector(fixedCenter, 0, 2 * Math.PI, 0, 100);
        Assert.Equal((60.0, 80.0), (fixedCenter.X, fixedCenter.Y));

        // Assert: the keywords width and height are the far edges of the data rectangle
        var corner = Assert.Single(Arcs("\"arc\"", """{ "x": { "value": "width" }, "y": { "value": "height" } }""", """[ { "v": 1 } ]""", width: 300, height: 200));
        Assert.Equal((300.0, 200.0), (corner.X, corner.Y));

        // Assert: two categories on x (a point scale of 300 pixels: 75 and 225) and one on y (the middle, 100) place two arcs of the same sector
        const string byCategory = """
            { "x": { "field": "g", "type": "nominal" }, "y": { "field": "h", "type": "nominal" } }
            """;
        var placed = Arcs("\"arc\"", byCategory, """[ { "g": "u", "h": "r" }, { "g": "w", "h": "r" } ]""", width: 300, height: 200);
        Assert.Equal(2, placed.Count);
        Assert.Equal((75.0, 100.0), (placed[0].X, placed[0].Y));
        Assert.Equal((225.0, 100.0), (placed[1].X, placed[1].Y));
        Assert.All(placed, a => AssertSector(a, 0, 2 * Math.PI, 0, 100));

        // Assert: a quantitative x on its scale (domain [0, 2] over 300 pixels: 150 per unit) with a pie by color on each (the stack of x is off)
        const string scatter = """
            { "x": { "field": "x", "type": "quantitative", "stack": false }, "y": { "field": "y", "type": "quantitative", "stack": false },
              "theta": { "field": "v", "type": "quantitative", "stack": false } }
            """;
        var points = Arcs("\"arc\"", scatter, """[ { "x": 0, "y": 0, "v": 1 }, { "x": 2, "y": 10, "v": 1 } ]""", width: 300, height: 200);
        Assert.Equal(2, points.Count);
        Assert.Equal((0.0, 200.0), (points[0].X, points[0].Y));
        Assert.Equal((300.0, 0.0), (points[1].X, points[1].Y));
    }

    /// <summary>
    ///     Validates the charges of the arcs: every slice is one scene item charged before the item exists, a row that draws nothing costs nothing, the evaluation steps are charged per row, and
    ///     the token is polled.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ArcsAreChargedPerSliceAndPollCancellation()
    {
        // Arrange: the pie of three slices
        var parts = ChartTestHelpers.Parts(Chart("\"arc\"", PieEncoding, PieRows));

        // Act: the budget is exactly the marks group (1) and three slices
        var budget = new SceneBudget(4);
        var group = new MarkBuilder(parts.Config, null, budget, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 200, 200, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, budget.Count);
        Assert.Equal(3, group.Children.Count);

        // Act / Assert: one item less and the third slice is refused after its charge, before it exists
        var tight = new SceneBudget(3);
        var failure = Assert.Throws<VegaLiteRenderException>(() => new MarkBuilder(parts.Config, null, tight, "test.vl.json").Build(parts.Tree.Root, parts.Table, parts.Scales, 200, 200, TestContext.Current.CancellationToken));
        Assert.Equal("/mark", failure.Location.Pointer);
        Assert.Contains("MaxSceneItems", failure.Message, StringComparison.Ordinal);
        Assert.Equal(4, tight.Count);

        // Act / Assert: a row that is not drawn (no number) is not charged to the budget
        var gap = ChartTestHelpers.Parts(Chart("\"arc\"", """{ "theta": { "field": "v", "type": "quantitative", "stack": false } }""", """[ { "v": 1 }, { "v": null }, { "v": 2 } ]"""));
        var gapBudget = new SceneBudget(10);
        new MarkBuilder(gap.Config, null, gapBudget, "test.vl.json").Build(gap.Tree.Root, gap.Table, gap.Scales, 200, 200, TestContext.Current.CancellationToken);
        Assert.Equal(3, gapBudget.Count);

        // Arrange: 200 rows cost 200 elements, which are 3 whole evaluation steps (one for each 64 elements): a limit of 2 is exceeded, the default limit is not (no stack: the steps are the mark's)
        var rows = "[" + string.Join(", ", Enumerable.Range(0, 200).Select(i => "{ \"v\": 1 }")) + "]";
        var many = Chart("\"arc\"", """{ "theta": { "field": "v", "type": "quantitative", "stack": false } }""", rows);

        // Act / Assert: the steps are charged with the location of the mark
        var steps = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Marks(many, 200, 200, limits: new VegaLiteLimits { MaxEvaluationSteps = 2 }));
        Assert.Equal("/mark", steps.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", steps.Message, StringComparison.Ordinal);
        Assert.Equal(200, ChartTestHelpers.Of<SceneArc>(ChartTestHelpers.Marks(many, 200, 200), "mark-arc").Count);

        // Act / Assert: a token that is canceled stops the build at its first poll
        var big = ChartTestHelpers.Parts(many);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new MarkBuilder(big.Config, null, null, "test.vl.json", big.Expressions).Build(big.Tree.Root, big.Table, big.Scales, 200, 200, canceled.Token));
    }
}
