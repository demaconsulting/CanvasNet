using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the path marks (Vega-Lite Phase 9): lines, areas and trails drawn through the public API, with the
///     interpolations, the point and line overlays, the invalid data modes, the impute, the draw order, the limits and the cancellation. The charts are
///     small inline specifications with made-up numbers; every expectation is derived by hand from the scale formulas (a quantitative axis that
///     starts at zero, a 300 by 200 data rectangle) and from the path rules, and the pixel assertions look only at the color of regions that the
///     derivation puts inside or outside of a shape. No expectation is taken from a real Vega output, and no test depends on a host font, time zone,
///     culture or clock.
/// </summary>
public class VegaLiteLineAreaIntegrationTests
{
    /// <summary>The first color of Vega's categorical scheme.</summary>
    private static readonly Rgba32 Blue = new(0x4c, 0x78, 0xa8, 255);

    /// <summary>The second color of Vega's categorical scheme.</summary>
    private static readonly Rgba32 Orange = new(0xf5, 0x85, 0x18, 255);

    /// <summary>The third color of Vega's categorical scheme.</summary>
    private static readonly Rgba32 Red = new(0xe4, 0x57, 0x56, 255);

    /// <summary>The x channel of the charts: a quantitative field without an axis, so that the data rectangle is at the padding of the chart.</summary>
    private const string X = "{ \"field\": \"x\", \"type\": \"quantitative\", \"axis\": null }";

    /// <summary>The y channel of the charts: a quantitative field without an axis.</summary>
    private const string Y = "{ \"field\": \"y\", \"type\": \"quantitative\", \"axis\": null }";

    /// <summary>Three rows: x is 0, 1 and 2 and y is 0, 10 and 5 (the x domain is 0 to 2 over 300 pixels and the y domain is 0 to 10 over 200 pixels).</summary>
    private const string Three = "[ { \"x\": 0, \"y\": 0 }, { \"x\": 1, \"y\": 10 }, { \"x\": 2, \"y\": 5 } ]";

    /// <summary>The names of the fifteen interpolations of Vega-Lite.</summary>
    private static readonly string[] Interpolations =
    [
        "basis", "basis-open", "basis-closed", "bundle", "cardinal", "cardinal-open", "cardinal-closed", "catmull-rom",
        "linear", "linear-closed", "monotone", "natural", "step", "step-before", "step-after",
    ];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a unit chart of 300 by 200 pixels.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="data">The JSON text of the rows.</param>
    /// <param name="encoding">The JSON text of the encoding.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string data, string encoding) =>
        "{ \"width\": 300, \"height\": 200, \"data\": { \"values\": " + data + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + " }";

    /// <summary>
    ///     Builds the encoding of the usual x and y channels with more channels after them.
    /// </summary>
    /// <param name="more">More members, each starting with a comma, or an empty text.</param>
    /// <returns>The JSON text of the encoding.</returns>
    private static string Encoding(string more = "") => "{ \"x\": " + X + ", \"y\": " + Y + more + " }";

    /// <summary>
    ///     Gets the position of the view (the data rectangle) on the canvas.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The x and y position of the data rectangle.</returns>
    private static (double X, double Y) ViewOf(Scene scene)
    {
        var view = scene.Find("view") ?? throw new InvalidOperationException("The scene has no view.");
        var group = Assert.IsType<SceneGroup>(view.Item);
        return (view.OffsetX + group.X, view.OffsetY + group.Y);
    }

    /// <summary>
    ///     Gets the items of a role of a scene, cast to a type.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items in drawing order.</returns>
    private static List<T> Items<T>(Scene scene, string role)
        where T : SceneItem =>
        [.. scene.FindAll(role).Select(e => Assert.IsType<T>(e.Item))];

    /// <summary>
    ///     Describes the segments of a path as text in the notation of SVG path data, with the coordinates rounded to four decimals.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The text, for example <c>M0,200 L150,0 Z</c>.</returns>
    private static string Describe(ScenePath path)
    {
        static string N(double v) => Math.Round(v + 0.0, 4, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
        return string.Join(
            ' ',
            path.Segments.Select(s => s.Kind switch
            {
                ScenePathSegmentKind.MoveTo => $"M{N(s.X)},{N(s.Y)}",
                ScenePathSegmentKind.LineTo => $"L{N(s.X)},{N(s.Y)}",
                ScenePathSegmentKind.CubicTo => $"C{N(s.X1)},{N(s.Y1)} {N(s.X2)},{N(s.Y2)} {N(s.X)},{N(s.Y)}",
                _ => "Z",
            }));
    }

    /// <summary>
    ///     Draws a document in strict mode and collects the warnings.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="scene">The scene that the document builds.</param>
    /// <param name="warnings">The warnings that were delivered.</param>
    /// <returns>The painted surface.</returns>
    private static Surface Draw(string json, out Scene scene, out List<string> warnings)
    {
        var document = VegaLiteDocument.Parse(json);
        var delivered = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => delivered.Add(w.Message) };
        scene = document.BuildScene(options, Ct);
        warnings = delivered;
        return document.Render(options, Ct);
    }

    /// <summary>
    ///     Renders a document and tells whether a surface was produced.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The render options.</param>
    /// <returns><see langword="true"/> when the surface has pixels.</returns>
    private static bool Renders(VegaLiteDocument document, VegaLiteRenderOptions options)
    {
        using var surface = document.Render(options, Ct);
        return surface.Width > 0 && surface.Height > 0;
    }

    /// <summary>
    ///     Evaluates a cubic Bezier curve.
    /// </summary>
    /// <param name="start">The start point.</param>
    /// <param name="segment">The cubic segment, whose end point is the end of the curve.</param>
    /// <param name="t">The parameter from 0 to 1.</param>
    /// <returns>The point of the curve.</returns>
    private static (double X, double Y) Bezier((double X, double Y) start, ScenePathSegment segment, double t)
    {
        var u = 1 - t;
        double Mix(double p0, double p1, double p2, double p3) => (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
        return (Mix(start.X, segment.X1, segment.X2, segment.X), Mix(start.Y, segment.Y1, segment.Y2, segment.Y));
    }

    /// <summary>
    ///     Validates a polyline: the three points (0, 0), (1, 10) and (2, 5) of the 300 by 200 chart are the pixels (0, 200), (150, 0) and (300, 100), the path is
    ///     the three commands <c>M L L</c> in the blue of the first color with the width 2 of a line and no fill, nothing is reported in strict mode, the pixels at the middle of
    ///     both segments are blue and the pixels far from the path are white, and a horizontal line at y = 5 (pixel row 100) is two pixels wide.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Lines_PolylineHasTheDocumentedSegmentsAndPixels()
    {
        // Arrange / Act
        using var surface = Draw(Chart("\"line\"", Three, Encoding()), out var scene, out var warnings);
        var view = ViewOf(scene);
        var path = Assert.Single(Items<ScenePath>(scene, "mark-line"));

        // Assert: the documented segments, paints and nothing reported
        Assert.Empty(warnings);
        Assert.Equal("M0,200 L150,0 L300,100", Describe(path));
        Assert.Equal(("#4c78a8", 2.0, null), (path.Stroke, path.StrokeWidth, path.Fill));

        // Assert: the middle of the first segment is (75, 100) and the middle of the second is (225, 50); the points far from the line are the background
        Assert.True(VegaLiteTestHelpers.HasInk(surface, view.X + 75, view.Y + 100, Blue, 2));
        Assert.True(VegaLiteTestHelpers.HasInk(surface, view.X + 225, view.Y + 50, Blue, 2));
        Assert.Equal(VegaLiteTestHelpers.White, surface[(int)(view.X + 75), (int)(view.Y + 40)]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[(int)(view.X + 225), (int)(view.Y + 150)]);

        // Arrange / Act: y is 5 at every point of the domain 0 to 10: the line is the pixel row 100 of the data rectangle
        const string flat = "[ { \"x\": 0, \"y\": 5 }, { \"x\": 1, \"y\": 5 }, { \"x\": 2, \"y\": 5 } ]";
        var flatEncoding = "{ \"x\": " + X + ", \"y\": { \"field\": \"y\", \"type\": \"quantitative\", \"axis\": null, \"scale\": { \"domain\": [0, 10] } } }";
        using var flatSurface = Draw(Chart("\"line\"", flat, flatEncoding), out var flatScene, out _);
        var flatView = ViewOf(flatScene);
        Assert.Equal("M0,100 L150,100 L300,100", Describe(Assert.Single(Items<ScenePath>(flatScene, "mark-line"))));

        // Assert: the stroke is two pixels wide, centered on the row boundary 100: the rows 99 and 100 are blue, the rows 98 and 101 are not
        var column = (int)Math.Round(flatView.X + 150, MidpointRounding.AwayFromZero);
        var row = (int)Math.Round(flatView.Y + 100, MidpointRounding.AwayFromZero);
        Assert.True(VegaLiteTestHelpers.Near(Blue, flatSurface[column, row - 1]));
        Assert.True(VegaLiteTestHelpers.Near(Blue, flatSurface[column, row]));
        Assert.Equal(VegaLiteTestHelpers.White, flatSurface[column, row - 2]);
        Assert.Equal(VegaLiteTestHelpers.White, flatSurface[column, row + 1]);
    }

    /// <summary>
    ///     Validates that a monotone interpolation draws a cubic curve through the data: for the rising points (0, 0), (1, 4), (2, 6) and (3, 10) of the 300 by 200
    ///     chart (pixels (0, 200), (100, 120), (200, 80) and (300, 0)) the path starts at the first point, has a cubic segment ending at each following point, never
    ///     rises on the screen (the data rise and the monotone curve keeps each segment between its end values), and every sampled point of the curves is covered by ink.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Lines_InterpolatedLineCoversItsCurvePixels()
    {
        // Arrange / Act
        const string data = "[ { \"x\": 0, \"y\": 0 }, { \"x\": 1, \"y\": 4 }, { \"x\": 2, \"y\": 6 }, { \"x\": 3, \"y\": 10 } ]";
        using var surface = Draw(Chart("{ \"type\": \"line\", \"interpolate\": \"monotone\" }", data, Encoding()), out var scene, out var warnings);
        var view = ViewOf(scene);
        var path = Assert.Single(Items<ScenePath>(scene, "mark-line"));

        // Assert: one move and three cubic curves, ending at the data points
        Assert.Empty(warnings);
        var segments = path.Segments;
        Assert.Equal([ScenePathSegmentKind.MoveTo, ScenePathSegmentKind.CubicTo, ScenePathSegmentKind.CubicTo, ScenePathSegmentKind.CubicTo], segments.Select(s => s.Kind));
        Assert.Equal([(0.0, 200.0), (100.0, 120.0), (200.0, 80.0), (300.0, 0.0)], segments.Select(s => (Math.Round(s.X, 6), Math.Round(s.Y, 6))));

        // Assert: along every cubic the curve is between the two end points (the data rise, so the pixels fall) and the ink covers the samples
        var start = (segments[0].X, segments[0].Y);
        for (var i = 1; i < segments.Count; i++)
        {
            var previous = i == 1 ? start : (segments[i - 1].X, segments[i - 1].Y);
            foreach (var t in new[] { 0.25, 0.5, 0.75 })
            {
                var point = Bezier(previous, segments[i], t);
                Assert.InRange(point.Y, segments[i].Y - 1e-6, previous.Item2 + 1e-6);
                Assert.InRange(point.X, previous.Item1 - 1e-6, segments[i].X + 1e-6);
                Assert.True(VegaLiteTestHelpers.HasInk(surface, view.X + point.X, view.Y + point.Y, Blue, 2), $"no ink at t = {t} of the segment {i}");
            }
        }

        // Assert: the pixel above the left end of the chart, far from the curve, is the background
        Assert.Equal(VegaLiteTestHelpers.White, surface[(int)(view.X + 20), (int)view.Y + 20]);
    }

    /// <summary>
    ///     Validates the geometry of areas: a single area of the points (0, 0), (1, 10) and (2, 5) has its top outline forward and the baseline y = 0 (pixel 200) backward
    ///     (<c>M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z</c>) and is filled with the blue and not stroked; two stacked areas of ten at every x share the 20 of the domain,
    ///     with the first series on top (pixels 0 to 100) and the second below (100 to 200), each in its color, and the pixels of the layers have their colors.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Areas_StackedAreasFillBetweenTheirLayers()
    {
        // Arrange / Act: a single area
        using var single = Draw(Chart("\"area\"", Three, Encoding()), out var singleScene, out var singleWarnings);
        var singlePath = Assert.Single(Items<ScenePath>(singleScene, "mark-area"));
        var singleView = ViewOf(singleScene);

        // Assert
        Assert.Empty(singleWarnings);
        Assert.Equal("M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z", Describe(singlePath));
        Assert.Equal(("#4c78a8", null), (singlePath.Fill, singlePath.Stroke));
        Assert.True(VegaLiteTestHelpers.Near(Blue, single[(int)(singleView.X + 150), (int)(singleView.Y + 150)]));
        Assert.Equal(VegaLiteTestHelpers.White, single[(int)(singleView.X + 75), (int)(singleView.Y + 50)]);

        // Arrange / Act: two series of ten at x = 0, 1 and 2, stacked on the y axis
        const string data = """
            [ { "x": 0, "y": 10, "g": "a" }, { "x": 1, "y": 10, "g": "a" }, { "x": 2, "y": 10, "g": "a" },
              { "x": 0, "y": 10, "g": "b" }, { "x": 1, "y": 10, "g": "b" }, { "x": 2, "y": 10, "g": "b" } ]
            """;
        using var stacked = Draw(Chart("\"area\"", data, Encoding(", \"color\": { \"field\": \"g\", \"type\": \"nominal\" }")), out var scene, out var warnings);
        var view = ViewOf(scene);
        var areas = Items<ScenePath>(scene, "mark-area");

        // Assert: one area for each series, in the order of the first appearance; a is stacked above b (Vega-Lite stacks the first category on top of a vertical stack)
        Assert.Empty(warnings);
        Assert.Equal(2, areas.Count);
        Assert.Equal(["#4c78a8", "#f58518"], areas.Select(a => a.Fill));
        Assert.Equal("M0,0 L150,0 L300,0 L300,100 L150,100 L0,100 Z", Describe(areas[0]));
        Assert.Equal("M0,100 L150,100 L300,100 L300,200 L150,200 L0,200 Z", Describe(areas[1]));

        // Assert: the pixels in the middle of the layers have the colors of the layers
        Assert.True(VegaLiteTestHelpers.Near(Blue, stacked[(int)(view.X + 150), (int)(view.Y + 50)]));
        Assert.True(VegaLiteTestHelpers.Near(Orange, stacked[(int)(view.X + 150), (int)(view.Y + 150)]));
        Assert.True(VegaLiteTestHelpers.Near(Blue, stacked[(int)(view.X + 20), (int)(view.Y + 20)]));
        Assert.True(VegaLiteTestHelpers.Near(Orange, stacked[(int)(view.X + 280), (int)(view.Y + 180)]));
    }

    /// <summary>
    ///     Validates that a trail has the width of its size: the sizes 4, 8 and 12 (the identity scale) at the pixels x = 30, 150 and 270 on the row 100 are the diameters of the
    ///     circles that the capsules follow, so the half widths are 2, 4 and 6 and the columns of the pixels there are covered by 4, 8 and 12 rows of ink.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Trails_WidthFollowsTheSizeChannel()
    {
        // Arrange: x from 0 to 10 over 300 pixels (3 pixels per unit) and y = 5 on the domain 0 to 10 (the row 100); the size scale is the identity on 4 to 12
        const string data = "[ { \"x\": 1, \"y\": 5, \"s\": 4 }, { \"x\": 5, \"y\": 5, \"s\": 8 }, { \"x\": 9, \"y\": 5, \"s\": 12 } ]";
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative", "axis": null, "scale": { "domain": [0, 10] } },
              "y": { "field": "y", "type": "quantitative", "axis": null, "scale": { "domain": [0, 10] } },
              "size": { "field": "s", "type": "quantitative", "legend": null, "scale": { "domain": [4, 12], "range": [4, 12] } } }
            """;

        // Act
        using var surface = Draw(Chart("\"trail\"", data, encoding), out var scene, out var warnings);
        var view = ViewOf(scene);
        var trail = Assert.Single(Items<SceneTrail>(scene, "mark-trail"));

        // Assert: one run of three points with the sizes as diameters, filled with the blue and not stroked
        Assert.Empty(warnings);
        var run = Assert.Single(trail.Runs);
        Assert.Equal([(30.0, 100.0, 4.0), (150.0, 100.0, 8.0), (270.0, 100.0, 12.0)], run.Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(p.Size, 6))));
        Assert.Equal(("#4c78a8", null), (trail.Fill, trail.Stroke));

        // Assert: the rows of ink at the three columns; a row is counted when the whole pixel is inside of the half width (the rows with a boundary pixel are ignored)
        foreach (var (x, halfWidth) in new[] { (30, 2.0), (150, 4.0), (270, 6.0) })
        {
            var column = (int)Math.Round(view.X + x, MidpointRounding.AwayFromZero);
            var top = view.Y + 100 - halfWidth;
            var bottom = view.Y + 100 + halfWidth;
            var expectedFirst = (int)Math.Ceiling(top + 0.02);
            var expectedLast = (int)Math.Floor(bottom - 0.02) - 1;
            for (var y = (int)Math.Floor(view.Y) + 80; y < (int)Math.Floor(view.Y) + 120; y++)
            {
                var ink = VegaLiteTestHelpers.Near(Blue, surface[column, y]);
                if (y >= expectedFirst && y <= expectedLast)
                {
                    Assert.True(ink, $"no ink at the row {y} of the column {x}");
                }
                else if (y < expectedFirst - 1 || y > expectedLast + 1)
                {
                    Assert.False(ink, $"ink at the row {y} of the column {x}");
                }
            }
        }
    }

    /// <summary>
    ///     Validates that all fifteen interpolations of Vega-Lite render a line and an area of five points without a report (the area of the bundle interpolation is reported and
    ///     drawn with the linear one), with finite coordinates near the chart, straight segments only for the linear and step interpolations, curves for the others, the closing
    ///     command for the closed ones, and the first point of the data as the start of every interpolation that passes through it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Curves_AllFifteenInterpolationsRenderAndAreFinite()
    {
        // Arrange: five points of the 300 by 200 chart: x is 0 to 4 (75 pixels apart) and y is 0, 10, 4, 8, 2 on the domain 0 to 10
        const string data = "[ { \"x\": 0, \"y\": 0 }, { \"x\": 1, \"y\": 10 }, { \"x\": 2, \"y\": 4 }, { \"x\": 3, \"y\": 8 }, { \"x\": 4, \"y\": 2 } ]";
        var straight = new HashSet<string> { "linear", "linear-closed", "step", "step-before", "step-after" };
        var closedCurves = new HashSet<string> { "basis-closed", "cardinal-closed" };
        var startsAtTheFirstPoint = new HashSet<string> { "basis", "bundle", "cardinal", "catmull-rom", "linear", "linear-closed", "monotone", "natural", "step", "step-before", "step-after" };
        Assert.Equal(15, Interpolations.Length);

        foreach (var name in Interpolations)
        {
            // Act: the line in strict mode
            using var line = Draw(Chart("{ \"type\": \"line\", \"interpolate\": \"" + name + "\" }", data, Encoding()), out var lineScene, out var lineWarnings);
            var path = Assert.Single(Items<ScenePath>(lineScene, "mark-line"));
            var view = ViewOf(lineScene);

            // Assert: finite coordinates near the chart, the kind of segments, the first point and some ink; only the linear-closed interpolation ends with the closing command (d3-shape
            // draws the closed basis and cardinal curves back to their start point without it) and a closed curve ends where it started
            Assert.Empty(lineWarnings);
            Assert.All(path.Segments, s => Assert.True(s.IsFinite(), name));
            Assert.All(path.Segments, s => Assert.InRange(s.X, -300, 600));
            Assert.All(path.Segments, s => Assert.InRange(s.Y, -300, 500));
            Assert.Equal(straight.Contains(name), path.Segments.All(s => s.Kind != ScenePathSegmentKind.CubicTo));
            Assert.Equal(name == "linear-closed", path.Segments[^1].Kind == ScenePathSegmentKind.Close);
            if (closedCurves.Contains(name))
            {
                Assert.Equal(path.Segments[0].X, path.Segments[^1].X, 6);
                Assert.Equal(path.Segments[0].Y, path.Segments[^1].Y, 6);
            }

            if (startsAtTheFirstPoint.Contains(name))
            {
                Assert.Equal("M0,200", Describe(path).Split(' ')[0]);
            }

            Assert.NotNull(VegaLiteTestHelpers.InkBounds(line, VegaLiteTestHelpers.White));
            Assert.True(VegaLiteTestHelpers.CountNear(line, Blue, (int)view.X - 5, (int)view.Y - 5, (int)view.X + 305, (int)view.Y + 205) > 100, name);

            // Act: the area; the interpolation of the bundle is not an area interpolation of d3-shape
            var reported = new List<string>();
            var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => reported.Add(u.Feature + " " + u.Location.Pointer) };
            var document = VegaLiteDocument.Parse(Chart("{ \"type\": \"area\", \"interpolate\": \"" + name + "\" }", data, Encoding()));
            var areaScene = document.BuildScene(options, Ct);
            using var areaSurface = document.Render(options, Ct);
            var area = Assert.Single(Items<ScenePath>(areaScene, "mark-area"));

            // Assert
            Assert.Equal(name == "bundle" ? ["vl-mark-interpolate-bundle-area /mark/interpolate"] : [], reported);
            Assert.All(area.Segments, s => Assert.True(s.IsFinite(), name));
            Assert.All(area.Segments, s => Assert.InRange(s.Y, -300, 500));
            // d3-shape draws the area of a closed basis or cardinal curve as two closed loops (the top and the baseline) without the closing command
            Assert.True((area.Segments[^1].Kind == ScenePathSegmentKind.Close) != closedCurves.Contains(name), name + " " + Describe(area));
            Assert.Equal(closedCurves.Contains(name) || name == "linear-closed" ? 2 : 1, area.Segments.Count(s => s.Kind == ScenePathSegmentKind.MoveTo));
            Assert.Equal(!straight.Contains(name) && name != "bundle", area.Segments.Any(s => s.Kind == ScenePathSegmentKind.CubicTo));
        }
    }

    /// <summary>
    ///     Validates the overlays: a line with <c>point: true</c> draws the line and a filled symbol at each of the three points (0, 200), (150, 0) and (300, 100); an area with
    ///     <c>line: true</c> and <c>point: true</c> draws the area (with the opacity 0.7 of an area with an overlay), a line along its top outline (three segments, without the baseline) and the
    ///     symbols; a transparent point overlay draws invisible symbols; and the overlay of a color encoding follows the color of each series.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Overlays_LinePointsAndAreaLineAreDrawn()
    {
        // Arrange / Act: a line with points
        using var withPoints = Draw(Chart("{ \"type\": \"line\", \"point\": true }", Three, Encoding()), out var scene, out var warnings);
        var view = ViewOf(scene);
        var lines = Items<ScenePath>(scene, "mark-line");
        var points = Items<SceneSymbol>(scene, "mark-point");

        // Assert: one line and three symbols at the data points, filled with the blue and opaque
        Assert.Empty(warnings);
        Assert.Equal("M0,200 L150,0 L300,100", Describe(Assert.Single(lines)));
        Assert.Equal([(0.0, 200.0), (150.0, 0.0), (300.0, 100.0)], points.Select(p => (Math.Round(p.X, 6), Math.Round(p.Y, 6))));
        Assert.All(points, p => Assert.Equal(("#4c78a8", 1.0), (p.Fill, p.Opacity)));
        Assert.True(VegaLiteTestHelpers.HasInk(withPoints, view.X + 300, view.Y + 100, Blue));

        // Arrange / Act: an area with a line and points
        using var overlaid = Draw(Chart("{ \"type\": \"area\", \"line\": true, \"point\": true }", Three, Encoding()), out var areaScene, out var areaWarnings);
        var area = Assert.Single(Items<ScenePath>(areaScene, "mark-area"));
        var outline = Assert.Single(Items<ScenePath>(areaScene, "mark-line"));

        // Assert: the area with its baseline and the default opacity of an area with an overlay, the line along the top, the three symbols, drawn in this order
        Assert.Empty(areaWarnings);
        Assert.Equal("M0,200 L150,0 L300,100 L300,200 L150,200 L0,200 Z", Describe(area));
        Assert.Equal(0.7, area.Opacity, 9);
        Assert.Equal("M0,200 L150,0 L300,100", Describe(outline));
        Assert.Equal("#4c78a8", outline.Stroke);
        Assert.Equal(3, Items<SceneSymbol>(areaScene, "mark-point").Count);
        var roles = areaScene.Enumerate().Select(e => e.Item.Role).Where(r => r is "mark-area" or "mark-line" or "mark-point").Distinct().ToList();
        Assert.Equal(["mark-area", "mark-line", "mark-point"], roles);

        // Arrange / Act: the transparent overlay and an overlay with an explicit color
        using var transparent = Draw(Chart("{ \"type\": \"line\", \"point\": \"transparent\" }", Three, Encoding()), out var transparentScene, out _);
        Assert.All(Items<SceneSymbol>(transparentScene, "mark-point"), p => Assert.Equal(0.0, p.Opacity));

        // Arrange / Act: two series: each series has its line and its points in its color
        const string data = """
            [ { "x": 0, "y": 1, "g": "a" }, { "x": 1, "y": 2, "g": "a" }, { "x": 0, "y": 3, "g": "b" }, { "x": 1, "y": 4, "g": "b" } ]
            """;
        using var series = Draw(Chart("{ \"type\": \"line\", \"point\": true }", data, Encoding(", \"color\": { \"field\": \"g\", \"type\": \"nominal\", \"legend\": null }")), out var seriesScene, out var seriesWarnings);

        // Assert
        Assert.Empty(seriesWarnings);
        Assert.Equal(["#4c78a8", "#f58518"], Items<ScenePath>(seriesScene, "mark-line").Select(l => l.Stroke));
        Assert.Equal(["#4c78a8", "#4c78a8", "#f58518", "#f58518"], Items<SceneSymbol>(seriesScene, "mark-point").Select(p => p.Fill));
    }

    /// <summary>
    ///     Validates the invalid data modes: a null y in the middle of five points breaks the default line into two subpaths (two moves), the <c>filter</c> mode connects the points on both
    ///     sides, the filter mode warns that the scale domains include the filtered rows, and the modes <c>show</c> and <c>null</c> are reported as unsupported (and thrown in strict mode).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_InvalidData_NullsBreakAFilterConnectsAndShowIsReported()
    {
        // Arrange: x is 0 to 4 over 300 pixels (75 each); y is 0, 10, null, 5, 8 on the domain 0 to 10
        const string data = "[ { \"x\": 0, \"y\": 0 }, { \"x\": 1, \"y\": 10 }, { \"x\": 2, \"y\": null }, { \"x\": 3, \"y\": 5 }, { \"x\": 4, \"y\": 8 } ]";

        // Act / Assert: the default breaks the path at the invalid row: two subpaths
        using var broken = Draw(Chart("\"line\"", data, Encoding()), out var brokenScene, out var brokenWarnings);
        Assert.Empty(brokenWarnings);
        Assert.Equal("M0,200 L75,0 M225,100 L300,40", Describe(Assert.Single(Items<ScenePath>(brokenScene, "mark-line"))));

        // Act / Assert: the filter mode drops the row and connects its neighbors; its warning is delivered through the warning handler
        var document = VegaLiteDocument.Parse(Chart("{ \"type\": \"line\", \"invalid\": \"filter\" }", data, Encoding()));
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) };
        var scene = document.BuildScene(options, Ct);
        using var filtered = document.Render(options, Ct);
        Assert.Equal("M0,200 L75,0 L225,100 L300,40", Describe(Assert.Single(Items<ScenePath>(scene, "mark-line"))));
        Assert.Contains(warnings, w => w.StartsWith("vl-mark-invalid-domains", StringComparison.Ordinal));

        // Act / Assert: the explicit default mode is silent
        using var explicitDefault = Draw(Chart("{ \"type\": \"line\", \"invalid\": \"break-paths-show-path-domains\" }", data, Encoding()), out var explicitScene, out var explicitWarnings);
        Assert.Empty(explicitWarnings);
        Assert.Equal("M0,200 L75,0 M225,100 L300,40", Describe(Assert.Single(Items<ScenePath>(explicitScene, "mark-line"))));

        // Act / Assert: show and null are reported as unsupported with the location of the property, and strict mode throws for them
        foreach (var mode in new[] { "\"show\"", "null" })
        {
            var spec = VegaLiteDocument.Parse(Chart("{ \"type\": \"line\", \"invalid\": " + mode + " }", data, Encoding()));
            var reported = new List<string>();
            using var surface = spec.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => reported.Add(u.Feature + " " + u.Location.Pointer) }, Ct);
            Assert.Equal(["vl-mark-invalid-show /mark/invalid"], reported);
            var failure = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => spec.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
            Assert.Equal("vl-mark-invalid-show", failure.Feature);
        }
    }

    /// <summary>
    ///     Validates the two imputes: the <c>impute</c> of a y channel adds the missing key of the second series of a line (a at x = 0, 1, 2 and b at x = 0 and 2: b gets the value 0 at x = 1, so
    ///     its line has three points and without the impute two), and the stack of areas imputes zeros implicitly (a at x = 1, 2, 3 and b at x = 1 and 3: b has three vertices with a zero thickness at x = 2
    ///     and the area of a dips to the baseline of b), unless the y channel switches it off with <c>impute: null</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Impute_EncodingAndStackImputeFillMissingKeys()
    {
        // Arrange: x is 0 to 2 over 300 pixels; y is on the domain 0 to 6 (200 / 6 pixels a unit)
        const string lineData = """
            [ { "x": 0, "y": 6, "g": "a" }, { "x": 1, "y": 6, "g": "a" }, { "x": 2, "y": 6, "g": "a" }, { "x": 0, "y": 4, "g": "b" }, { "x": 2, "y": 4, "g": "b" } ]
            """;
        const string color = ", \"color\": { \"field\": \"g\", \"type\": \"nominal\", \"legend\": null }";
        var plainY = "{ \"field\": \"y\", \"type\": \"quantitative\", \"axis\": null, \"scale\": { \"domain\": [0, 6] } }";
        string ImputedY(string impute) => "{ \"field\": \"y\", \"type\": \"quantitative\", \"axis\": null, \"scale\": { \"domain\": [0, 6] }, \"impute\": " + impute + " }";
        string Lines(string y) => Chart("\"line\"", lineData, "{ \"x\": " + X + ", \"y\": " + y + color + " }");

        // Act / Assert: without the impute b has two points; with the value 0 it has the third point at (150, 200)
        using var without = Draw(Lines(plainY), out var withoutScene, out var withoutWarnings);
        Assert.Empty(withoutWarnings);
        var plain = Items<ScenePath>(withoutScene, "mark-line");
        Assert.Equal(["M0,0 L150,0 L300,0", "M0,66.6667 L300,66.6667"], plain.Select(Describe));

        using var imputed = Draw(Lines(ImputedY("{ \"value\": 0 }")), out var imputedScene, out var imputedWarnings);
        Assert.Empty(imputedWarnings);
        Assert.Equal(["M0,0 L150,0 L300,0", "M0,66.6667 L150,200 L300,66.6667"], Items<ScenePath>(imputedScene, "mark-line").Select(Describe));

        // Arrange: stacked areas of a (x = 1, 2, 3; one each) and b (x = 1 and 3; one each); x is 1 to 3 over 300 pixels and the stack domain is 0 to 2 (100 pixels a unit)
        const string areaData = """
            [ { "x": 1, "y": 1, "g": "a" }, { "x": 2, "y": 1, "g": "a" }, { "x": 3, "y": 1, "g": "a" }, { "x": 1, "y": 1, "g": "b" }, { "x": 3, "y": 1, "g": "b" } ]
            """;
        string Areas(string y) => Chart("\"area\"", areaData, "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"axis\": null, \"scale\": { \"domain\": [1, 3] } }, \"y\": " + y + color + " }");
        const string stackedY = "{ \"field\": \"y\", \"type\": \"quantitative\", \"axis\": null }";
        const string withoutImputeY = "{ \"field\": \"y\", \"type\": \"quantitative\", \"axis\": null, \"impute\": null }";

        // Act: the implicit impute of the stack
        using var stacked = Draw(Areas(stackedY), out var stackedScene, out var stackedWarnings);
        var layers = Items<ScenePath>(stackedScene, "mark-area");

        // Assert: b is at the bottom: its top is at 100, 200 (zero thick) and 100 and its baseline on 200; a is above b: 0 to 100 at x = 1 and 3, and 100 to 200 at the middle
        Assert.Empty(stackedWarnings);
        Assert.Equal(
            ["M0,0 L150,100 L300,0 L300,100 L150,200 L0,100 Z", "M0,100 L150,200 L300,100 L300,200 L150,200 L0,200 Z"],
            layers.Select(Describe));

        // Act / Assert: without the impute b has only its two rows
        using var raw = Draw(Areas(withoutImputeY), out var rawScene, out var rawWarnings);
        Assert.Empty(rawWarnings);
        Assert.Equal(
            ["M0,0 L150,100 L300,0 L300,100 L150,200 L0,100 Z", "M0,100 L300,100 L300,200 L0,200 Z"],
            Items<ScenePath>(rawScene, "mark-area").Select(Describe));
    }

    /// <summary>
    ///     Validates the draw order: a line is sorted by its x position (the data order 1, 0, 2 draws the path 0, 1, 2), the <c>order</c> channel gives the order of the path (the sequence 0, 2, 1 draws
    ///     the points in the order of the field), a mark <c>order: false</c> keeps the order of the data, and the <c>order</c> channel of symbols is their draw order: of three overlapping squares
    ///     the last one in the order covers the others, ascending or descending.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Order_OrderChannelControlsOverlapAndPathSort()
    {
        // Arrange: the rows are not in the order of x; the sequence s puts x = 2 second
        const string data = "[ { \"x\": 1, \"y\": 10, \"s\": 2 }, { \"x\": 0, \"y\": 0, \"s\": 0 }, { \"x\": 2, \"y\": 5, \"s\": 1 } ]";
        const string sequence = ", \"order\": { \"field\": \"s\", \"type\": \"quantitative\" }";

        // Act / Assert: the path follows x, the field of the order channel, or the data
        using var byX = Draw(Chart("\"line\"", data, Encoding()), out var byXScene, out _);
        Assert.Equal("M0,200 L150,0 L300,100", Describe(Assert.Single(Items<ScenePath>(byXScene, "mark-line"))));
        using var bySequence = Draw(Chart("\"line\"", data, Encoding(sequence)), out var sequenceScene, out _);
        Assert.Equal("M0,200 L300,100 L150,0", Describe(Assert.Single(Items<ScenePath>(sequenceScene, "mark-line"))));
        using var byData = Draw(Chart("{ \"type\": \"line\", \"order\": false }", data, Encoding()), out var dataScene, out _);
        Assert.Equal("M150,0 L0,200 L300,100", Describe(Assert.Single(Items<ScenePath>(dataScene, "mark-line"))));

        // Arrange: three squares of 63 pixels at the same point (the middle of the data rectangle), in the colors of their categories and drawn opaque; the field z is 2, 3 and 1
        const string squares = "[ { \"g\": \"a\", \"z\": 2 }, { \"g\": \"b\", \"z\": 3 }, { \"g\": \"c\", \"z\": 1 } ]";
        string Overlap(string order) => Chart(
            "{ \"type\": \"square\", \"size\": 4000, \"opacity\": 1 }",
            squares,
            "{ \"x\": { \"datum\": 1, \"type\": \"quantitative\", \"axis\": null, \"scale\": { \"domain\": [0, 2] } }, \"y\": { \"datum\": 1, \"type\": \"quantitative\", \"axis\": null, \"scale\": { \"domain\": [0, 2] } },"
            + " \"color\": { \"field\": \"g\", \"type\": \"nominal\", \"legend\": null }, \"order\": " + order + " }");

        // Act / Assert: ascending z draws c (1), a (2), b (3), so b is on top; descending draws b, a, c, so c is on top
        using var ascending = Draw(Overlap("{ \"field\": \"z\", \"type\": \"quantitative\" }"), out var ascendingScene, out _);
        Assert.Equal(["#e45756", "#4c78a8", "#f58518"], Items<SceneSymbol>(ascendingScene, "mark-square").Select(s => s.Fill));
        var ascendingView = ViewOf(ascendingScene);
        Assert.True(VegaLiteTestHelpers.Near(Orange, ascending[(int)(ascendingView.X + 150), (int)(ascendingView.Y + 100)]));

        using var descending = Draw(Overlap("{ \"field\": \"z\", \"type\": \"quantitative\", \"sort\": \"descending\" }"), out var descendingScene, out _);
        Assert.Equal(["#f58518", "#4c78a8", "#e45756"], Items<SceneSymbol>(descendingScene, "mark-square").Select(s => s.Fill));
        var descendingView = ViewOf(descendingScene);
        Assert.True(VegaLiteTestHelpers.Near(Red, descending[(int)(descendingView.X + 150), (int)(descendingView.Y + 100)]));
    }

    /// <summary>
    ///     Validates that the limits fail closed with located render exceptions for the new marks: a line of many points above the scene item limit (the segments are charged before the path
    ///     exists), the point overlay symbols, the arcs and the text lines above the limit, a label above <c>MaxStringLength</c> and the labels above sixteen times that, while the same charts
    ///     render with the default limits.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_PathLimits_SegmentsTextAndItemsFailClosedBeforeAllocation()
    {
        // Arrange: a line of 2,000 points
        var rows = "[" + string.Join(", ", Enumerable.Range(0, 2000).Select(i => "{ \"x\": " + i + ", \"y\": " + (i % 7) + " }")) + "]";
        var tight = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 500 } };

        // Act / Assert: the path is refused with the location of the mark and the name of the limit, for a line and for an area, and the same charts render without the limit
        foreach (var mark in new[] { "line", "area", "trail" })
        {
            var document = VegaLiteDocument.Parse(Chart("\"" + mark + "\"", rows, Encoding()));
            var failure = Assert.Throws<VegaLiteRenderException>(() => document.Render(tight, Ct));
            Assert.Equal("/mark", failure.Location.Pointer);
            Assert.Contains("MaxSceneItems", failure.Message, StringComparison.Ordinal);
            using var drawn = document.Render(cancellationToken: Ct);
            Assert.True(drawn.Width > 0);
        }

        // Act / Assert: the point overlay costs one item for every point (the line of 300 points costs 307 items and fits 500 items, its 300 symbols do not)
        var few = "[" + string.Join(", ", Enumerable.Range(0, 300).Select(i => "{ \"x\": " + i + ", \"y\": " + (i % 5) + " }")) + "]";
        var overlay = VegaLiteDocument.Parse(Chart("{ \"type\": \"line\", \"point\": true }", few, Encoding()));
        var overlayFailure = Assert.Throws<VegaLiteRenderException>(() => overlay.Render(tight, Ct));
        Assert.Contains("MaxSceneItems", overlayFailure.Message, StringComparison.Ordinal);
        Assert.True(Renders(VegaLiteDocument.Parse(Chart("\"line\"", few, Encoding())), tight));

        // Act / Assert: arcs are charged one by one; 600 slices do not fit 500 items
        var slices = "[" + string.Join(", ", Enumerable.Range(1, 600).Select(i => "{ \"v\": " + i + " }")) + "]";
        var pie = VegaLiteDocument.Parse("{ \"width\": 200, \"height\": 200, \"data\": { \"values\": " + slices + " }, \"mark\": \"arc\", \"encoding\": { \"theta\": { \"field\": \"v\", \"type\": \"quantitative\" } } }");
        var pieFailure = Assert.Throws<VegaLiteRenderException>(() => pie.Render(tight, Ct));
        Assert.Contains("MaxSceneItems", pieFailure.Message, StringComparison.Ordinal);
        Assert.Equal("/mark", pieFailure.Location.Pointer);

        // Act / Assert: labels: the text above the limit of a label, the labels above sixteen times it, and the lines of a label above the item limit
        var label = new string('a', 40);
        var texts = "[" + string.Join(", ", Enumerable.Range(0, 3).Select(i => "{ \"x\": " + i + ", \"t\": \"" + label + "\" }")) + "]";
        string Text(string data, string text) => Chart("\"text\"", data, "{ \"x\": " + X + ", \"text\": " + text + " }");
        const string field = "{ \"field\": \"t\", \"type\": \"nominal\" }";
        var shortLabels = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxStringLength = 64 } };
        Assert.True(Renders(VegaLiteDocument.Parse(Text(texts, field)), shortLabels));

        var narrow = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxStringLength = 39 } };
        var single = Assert.Throws<VegaLiteRenderException>(() => VegaLiteDocument.Parse(Text(texts, field)).Render(narrow, Ct));
        Assert.Contains("MaxStringLength", single.Message, StringComparison.Ordinal);

        var many = "[" + string.Join(", ", Enumerable.Range(0, 20).Select(i => "{ \"x\": " + i + ", \"t\": \"" + label + "\" }")) + "]";
        var together = Assert.Throws<VegaLiteRenderException>(() => VegaLiteDocument.Parse(Text(many, field)).Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxStringLength = 40 } }, Ct));
        Assert.Contains("MaxStringLength", together.Message, StringComparison.Ordinal);
        Assert.Equal("/mark", together.Location.Pointer);

        // Assert: a label of 40 characters is accepted at the limit of 40 (twenty of them are 800 characters, above sixteen times 40 = 640, and refused as shown above, sixteen are accepted)
        var sixteen = "[" + string.Join(", ", Enumerable.Range(0, 16).Select(i => "{ \"x\": " + i + ", \"t\": \"" + label + "\" }")) + "]";
        Assert.True(Renders(VegaLiteDocument.Parse(Text(sixteen, field)), new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxStringLength = 40 } }));

        var lines = VegaLiteDocument.Parse(Text("[ { \"x\": 0 }, { \"x\": 1 } ]", "{ \"value\": [\"a\", \"b\", \"c\", \"d\"] }"));
        Assert.True(Renders(lines, new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 1000 } }));
    }

    /// <summary>
    ///     Validates that a canceled token stops the rendering and the measuring of every new mark before a result exists, and that the same documents render with a token that is not canceled.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_PathLimits_CancellationIsHonoredWhileBuildingPaths()
    {
        // Arrange: a document for each of the five marks
        var rows = "[" + string.Join(", ", Enumerable.Range(0, 200).Select(i => "{ \"x\": " + i + ", \"y\": " + (i % 9) + ", \"t\": \"p" + i + "\" }")) + "]";
        var documents = new[]
        {
            VegaLiteDocument.Parse(Chart("\"line\"", rows, Encoding())),
            VegaLiteDocument.Parse(Chart("\"area\"", rows, Encoding())),
            VegaLiteDocument.Parse(Chart("\"trail\"", rows, Encoding())),
            VegaLiteDocument.Parse(Chart("\"text\"", rows, Encoding(", \"text\": { \"field\": \"t\", \"type\": \"nominal\" }"))),
            VegaLiteDocument.Parse(Chart("\"arc\"", rows, "{ \"theta\": { \"field\": \"y\", \"type\": \"quantitative\" } }")),
        };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var events = 0;
        var options = new VegaLiteRenderOptions { WarningHandler = _ => events++, UnsupportedFeatureHandler = _ => events++ };

        foreach (var document in documents)
        {
            // Act / Assert: every entry point throws and delivers nothing
            Assert.Throws<OperationCanceledException>(() => document.Render(options, canceled.Token));
            Assert.Throws<OperationCanceledException>(() => document.Render(100, 100, options, canceled.Token));
            Assert.Throws<OperationCanceledException>(() => document.Measure(options, canceled.Token));

            // Assert: the same document renders with a live token
            using var surface = document.Render(options, Ct);
            Assert.True(surface.Width > 0);
        }

        Assert.Equal(0, events);
    }
}
