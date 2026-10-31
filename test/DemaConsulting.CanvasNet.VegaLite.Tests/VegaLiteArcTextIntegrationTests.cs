using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the arc and text marks and of the whole path mark family (Vega-Lite Phase 9): pie and donut charts with their wedges and labels, text
///     labels with number formats, the constructs that are reported as unsupported, the strict mode on the supported subset, the determinism of repeated and concurrent renders,
///     the scale option and the independence from the culture of the host. The charts are small inline specifications with made-up numbers; every expectation is derived by hand
///     (the angles of the slices of a pie, the position of a label on its slice, the areas of the wedges, the stacking of the lines) and the pixel assertions look only at the color
///     of regions that the derivation puts inside or outside of a shape. No expectation is taken from a real Vega output, and no test depends on a host font, time zone or clock.
/// </summary>
public class VegaLiteArcTextIntegrationTests
{
    /// <summary>The first color of Vega's categorical scheme.</summary>
    private static readonly Rgba32 Blue = new(0x4c, 0x78, 0xa8, 255);

    /// <summary>The second color of Vega's categorical scheme.</summary>
    private static readonly Rgba32 Orange = new(0xf5, 0x85, 0x18, 255);

    /// <summary>The third color of Vega's categorical scheme.</summary>
    private static readonly Rgba32 Red = new(0xe4, 0x57, 0x56, 255);

    /// <summary>Three slices of the weights 1, 1 and 2 of the categories a, b and c: the angles of the slices are 0 to a quarter, a quarter to a half and a half to a whole turn.</summary>
    private const string Slices = "[ { \"g\": \"a\", \"v\": 1 }, { \"g\": \"b\", \"v\": 1 }, { \"g\": \"c\", \"v\": 2 } ]";

    /// <summary>The encoding of a pie: the weights are the angle and the categories are the colors.</summary>
    private const string PieEncoding = "{ \"theta\": { \"field\": \"v\", \"type\": \"quantitative\" }, \"color\": { \"field\": \"g\", \"type\": \"nominal\", \"legend\": null } }";

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a chart of 200 by 200 pixels.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="data">The JSON text of the rows.</param>
    /// <param name="encoding">The JSON text of the encoding.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string data, string encoding) =>
        "{ \"width\": 200, \"height\": 200, \"data\": { \"values\": " + data + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + " }";

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
    ///     Gets the point of a pie at an angle and a distance from its center, as the canvas position of the data rectangle.
    /// </summary>
    /// <param name="view">The position of the data rectangle.</param>
    /// <param name="angle">The angle in radians, from the top and clockwise.</param>
    /// <param name="radius">The distance from the center in pixels.</param>
    /// <returns>The canvas position.</returns>
    private static (double X, double Y) OnPie((double X, double Y) view, double angle, double radius) =>
        (view.X + 100 + (radius * Math.Sin(angle)), view.Y + 100 - (radius * Math.Cos(angle)));

    /// <summary>
    ///     Counts the pixels of a color in the data rectangle of a 200 by 200 chart.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="view">The position of the data rectangle.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of pixels that are close to the color.</returns>
    private static int Covered(Surface surface, (double X, double Y) view, Rgba32 color) =>
        VegaLiteTestHelpers.CountNear(surface, color, (int)Math.Floor(view.X) - 1, (int)Math.Floor(view.Y) - 1, (int)Math.Ceiling(view.X) + 201, (int)Math.Ceiling(view.Y) + 201);

    /// <summary>
    ///     Validates a pie and a donut: the slices of the weights 1, 1 and 2 are the arcs from 0 to a quarter turn, a quarter to a half and a half to a whole turn around the middle (100, 100)
    ///     of the 200 by 200 data rectangle with the outer radius 100 (the inner radius 0, or 50 for the donut); a point in the middle of each slice has its color, the number of the pixels
    ///     of each color is the area of its wedge (a quarter or a half of 3.1416 times 100 squared, and the same less the hole for the donut), and the hole is white.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Arcs_PieAndDonutCoverTheirWedges()
    {
        // Arrange / Act: the pie
        using var pie = Draw(Chart("\"arc\"", Slices, PieEncoding), out var pieScene, out var pieWarnings);
        var view = ViewOf(pieScene);
        var arcs = Items<SceneArc>(pieScene, "mark-arc");

        // Assert: three arcs around the middle, outer radius 100, from the top and clockwise
        Assert.Empty(pieWarnings);
        Assert.Equal(3, arcs.Count);
        Assert.All(arcs, a => Assert.Equal((100.0, 100.0, 0.0, 100.0), (Math.Round(a.X, 6), Math.Round(a.Y, 6), a.InnerRadius, Math.Round(a.OuterRadius, 6))));
        double[] starts = [0.0, Math.PI / 2, Math.PI];
        double[] ends = [Math.PI / 2, Math.PI, 2 * Math.PI];
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(starts[i], arcs[i].StartAngle, 9);
            Assert.Equal(ends[i], arcs[i].EndAngle, 9);
        }
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], arcs.Select(a => a.Fill));

        // Assert: a point in the middle of each slice (at half the radius) has the color of the slice: the middles are 45, 135 and 270 degrees
        var inA = OnPie(view, Math.PI / 4, 50);
        var inB = OnPie(view, 3 * Math.PI / 4, 50);
        var inC = OnPie(view, 3 * Math.PI / 2, 50);
        Assert.True(VegaLiteTestHelpers.HasInk(pie, inA.X, inA.Y, Blue, 0));
        Assert.True(VegaLiteTestHelpers.HasInk(pie, inB.X, inB.Y, Orange, 0));
        Assert.True(VegaLiteTestHelpers.HasInk(pie, inC.X, inC.Y, Red, 0));

        // Assert: the areas of the wedges (the pixels of the rim are partly covered, so the counts are up to seven percent below the area and never above it)
        var quarter = Math.PI * 100 * 100 / 4;
        Assert.InRange(Covered(pie, view, Blue), quarter * 0.93, quarter + 1);
        Assert.InRange(Covered(pie, view, Orange), quarter * 0.93, quarter + 1);
        Assert.InRange(Covered(pie, view, Red), 2 * quarter * 0.93, (2 * quarter) + 1);

        // Arrange / Act: the donut
        using var donut = Draw(Chart("{ \"type\": \"arc\", \"innerRadius\": 50 }", Slices, PieEncoding), out var donutScene, out var donutWarnings);
        var ring = Items<SceneArc>(donutScene, "mark-arc");

        // Assert: the inner radius is 50 for each slice, the hole is white and the ring has the colors
        Assert.Empty(donutWarnings);
        Assert.All(ring, a => Assert.Equal((50.0, 100.0), (a.InnerRadius, Math.Round(a.OuterRadius, 6))));
        var hole = OnPie(view, Math.PI / 4, 25);
        Assert.Equal(VegaLiteTestHelpers.White, donut[(int)hole.X, (int)hole.Y]);
        Assert.Equal(VegaLiteTestHelpers.White, donut[(int)(view.X + 100), (int)(view.Y + 100)]);
        var ringPoint = OnPie(view, Math.PI / 4, 75);
        Assert.True(VegaLiteTestHelpers.HasInk(donut, ringPoint.X, ringPoint.Y, Blue, 0));

        // Assert: the areas of the ring segments
        var quarterRing = Math.PI * ((100 * 100) - (50 * 50)) / 4;
        Assert.InRange(Covered(donut, view, Blue), quarterRing * 0.93, quarterRing + 1);
        Assert.InRange(Covered(donut, view, Red), 2 * quarterRing * 0.93, (2 * quarterRing) + 1);
    }

    /// <summary>
    ///     Validates the labels of a pie: a text mark with the same angle and color encoding and the radius 60 puts each label at the middle of its slice, 60 pixels from the middle
    ///     (100, 100) of the 200 by 200 data rectangle: the slice a (the angles 0 to 90 degrees) has its label at (142.4264, 57.5736), the slice b at (142.4264, 142.4264) and
    ///     the slice c (180 to 360 degrees) at (40, 100); the labels are centered, colored by their category and painted around those points.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Arcs_PieLabelsAreAtTheSliceMiddles()
    {
        // Arrange / Act: big labels (40 pixels) so that the strokes of the letters have whole pixels of ink; a text mark is stacked only when the channel says so
        const string encoding = """
            { "theta": { "field": "v", "type": "quantitative", "stack": true }, "color": { "field": "g", "type": "nominal", "legend": null },
              "text": { "field": "g", "type": "nominal" } }
            """;
        using var surface = Draw(Chart("{ \"type\": \"text\", \"radius\": 60, \"fontSize\": 40 }", Slices, encoding), out var scene, out var warnings);
        var view = ViewOf(scene);
        var labels = Items<SceneText>(scene, "mark-text");

        // Assert: the three labels at the middles of the slices, centered
        Assert.Empty(warnings);
        Assert.Equal(["a", "b", "c"], labels.Select(l => l.Text));
        Assert.Equal([142.4264, 142.4264, 40.0], labels.Select(l => Math.Round(l.X, 4)));
        Assert.Equal([57.5736, 142.4264, 100.0], labels.Select(l => Math.Round(l.Y, 4)));
        Assert.All(labels, l => Assert.Equal(("center", "middle", 40.0), (l.Align, l.Baseline, l.FontSize)));
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], labels.Select(l => l.Fill));

        // Assert: the ink of each label has its color, around its point, and the middle of the chart has none
        var colors = new[] { Blue, Orange, Red };
        for (var i = 0; i < 3; i++)
        {
            var left = (int)Math.Round(view.X + labels[i].X) - 20;
            var top = (int)Math.Round(view.Y + labels[i].Y) - 20;
            Assert.True(VegaLiteTestHelpers.CountNear(surface, colors[i], left, top, left + 40, top + 40) >= 30, $"no ink of the label {labels[i].Text}");
        }

        Assert.Equal(VegaLiteTestHelpers.White, surface[(int)(view.X + 100), (int)(view.Y + 100)]);
    }

    /// <summary>
    ///     Validates the text mark: the labels of the values 1234.5, 7 and 0.26 with the number format <c>,.1f</c> are <c>1,234.5</c>, <c>7.0</c> and <c>0.3</c>, centered
    ///     on the pixels x = 100, 200 and 300 and y = 133.33, 66.67 and 0 of the 300 by 200 chart, with the font size and the red color of the mark, and painted in red around the
    ///     points; a left aligned label with an offset sits at its point plus the offset, and a text value with several lines has the lines.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Text_LabelsAreFormattedPositionedAndPainted()
    {
        // Arrange: x is 1, 2 and 3 on the domain 0 to 3 (100 pixels a unit) and y is 10, 20 and 30 on the domain 0 to 30 (200 / 30 pixels a unit)
        const string data = "[ { \"x\": 1, \"y\": 10, \"v\": 1234.5 }, { \"x\": 2, \"y\": 20, \"v\": 7 }, { \"x\": 3, \"y\": 30, \"v\": 0.26 } ]";
        const string encoding = """
            { "x": { "field": "x", "type": "quantitative", "axis": null, "scale": { "domain": [0, 3] } },
              "y": { "field": "y", "type": "quantitative", "axis": null, "scale": { "domain": [0, 30] } },
              "text": { "field": "v", "type": "quantitative", "format": ",.1f" } }
            """;
        string Spec(string mark) => "{ \"width\": 300, \"height\": 200, \"data\": { \"values\": " + data + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + " }";

        // Act
        using var surface = Draw(Spec("{ \"type\": \"text\", \"fontSize\": 24, \"color\": \"#ff0000\" }"), out var scene, out var warnings);
        var view = ViewOf(scene);
        var labels = Items<SceneText>(scene, "mark-text");

        // Assert: the texts, the anchors and the properties
        Assert.Empty(warnings);
        Assert.Equal(["1,234.5", "7.0", "0.3"], labels.Select(l => l.Text));
        Assert.Equal([100.0, 200.0, 300.0], labels.Select(l => Math.Round(l.X, 4)));
        Assert.Equal([133.3333, 66.6667, 0.0], labels.Select(l => Math.Round(l.Y, 4)));
        Assert.All(labels, l => Assert.Equal(("center", "middle", 24.0, "#ff0000", 0.0, 0.0), (l.Align, l.Baseline, l.FontSize, l.Fill, l.Dx, l.Dy)));

        // Assert: red ink around the first two points (the third is at the top edge and has only its lower half in the data rectangle), and none in the empty corner
        var red = new Rgba32(255, 0, 0, 255);
        foreach (var label in labels.Take(2))
        {
            var left = (int)Math.Round(view.X + label.X) - 25;
            var top = (int)Math.Round(view.Y + label.Y) - 12;
            Assert.True(VegaLiteTestHelpers.CountNear(surface, red, left, top, left + 50, top + 24) >= 20, $"no ink of the label {label.Text}");
        }

        Assert.Equal(0, VegaLiteTestHelpers.CountNear(surface, red, (int)view.X + 230, (int)view.Y + 120, (int)view.X + 300, (int)view.Y + 200));

        // Arrange / Act: a left aligned label with an offset (the properties of the mark)
        var offset = VegaLiteDocument.Parse(Spec("{ \"type\": \"text\", \"align\": \"left\", \"baseline\": \"bottom\", \"dx\": 5, \"dy\": -3 }"));
        var moved = Items<SceneText>(offset.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct), "mark-text");

        // Assert: the anchor does not change and the offset and the alignment are the properties of the items
        Assert.Equal([100.0, 200.0, 300.0], moved.Select(l => Math.Round(l.X, 4)));
        Assert.All(moved, l => Assert.Equal(("left", "bottom", 5.0, -3.0), (l.Align, l.Baseline, l.Dx, l.Dy)));

        // Arrange / Act: a text value of several lines is one label of three lines, once for every row
        var lines = VegaLiteDocument.Parse(
            "{ \"width\": 100, \"height\": 100, \"data\": { \"values\": [ { \"x\": 1 } ] }, \"mark\": \"text\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"axis\": null }, \"text\": { \"value\": [\"one\", \"two\", \"three\"] } } }");
        var multi = Assert.Single(Items<SceneText>(lines.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct), "mark-text"));
        Assert.Equal(["one", "two", "three"], multi.Lines);
    }

    /// <summary>
    ///     Validates that the constructs of the new marks that cannot be drawn are reported with their identifiers and pointers once, in order, and that the chart is still drawn: a geoshape
    ///     and an image mark, the <c>show</c> invalid mode, the bundle interpolation of an area, the sizes of an area, the angle of a bar and the dash of an area; strict mode stops at the first.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Unsupported_GeoshapeImageAndSpecialInvalidModesAreReported()
    {
        // Arrange: each specification with the reports that it must deliver
        const string Rows = "[ { \"x\": 1, \"y\": 2, \"z\": 3 }, { \"x\": 2, \"y\": 3, \"z\": 4 } ]";
        const string Xy = "\"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" }";
        var cases = new (string Mark, string Encoding, string[] Expected)[]
        {
            ("\"geoshape\"", "{ " + Xy + " }", ["vl-mark-geoshape /mark"]),
            ("\"image\"", "{ " + Xy + " }", ["vl-mark-image /mark"]),
            ("{ \"type\": \"line\", \"invalid\": \"show\" }", "{ " + Xy + " }", ["vl-mark-invalid-show /mark/invalid"]),
            ("{ \"type\": \"area\", \"interpolate\": \"bundle\" }", "{ " + Xy + " }", ["vl-mark-interpolate-bundle-area /mark/interpolate"]),
            ("\"area\"", "{ " + Xy + ", \"size\": { \"field\": \"z\", \"type\": \"quantitative\" } }", ["vl-channel-size /encoding/size"]),
            ("\"bar\"", "{ " + Xy + ", \"theta\": { \"field\": \"z\", \"type\": \"quantitative\" } }", ["vl-channel-theta /encoding/theta"]),
            ("\"point\"", "{ " + Xy + ", \"text\": { \"field\": \"z\", \"type\": \"quantitative\" } }", ["vl-channel-text /encoding/text"]),
            ("\"area\"", "{ " + Xy + ", \"strokeDash\": { \"field\": \"z\", \"type\": \"ordinal\" } }", ["vl-channel-strokedash /encoding/strokeDash"]),
        };

        foreach (var (mark, encoding, expected) in cases)
        {
            var document = VegaLiteDocument.Parse(Chart(mark, Rows, encoding));
            var reported = new List<string>();
            var threads = new HashSet<int>();
            var options = new VegaLiteRenderOptions
            {
                UnsupportedFeatureHandler = u =>
                {
                    threads.Add(Environment.CurrentManagedThreadId);
                    reported.Add(u.Feature + " " + u.Location.Pointer);
                },
            };

            // Act: the chart is still drawn
            using var surface = document.Render(options, Ct);

            // Assert: the reports, once and in order, on the calling thread
            Assert.Equal(expected, reported);
            Assert.Equal([Environment.CurrentManagedThreadId], threads);
            Assert.True(surface.Width >= 210);

            // Assert: strict mode stops at the first report
            var failure = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
            Assert.Equal(expected[0], failure.Feature + " " + failure.Location.Pointer);
        }

        // Assert: a geoshape mark has no marks and no arcs: the chart is the empty view
        using var empty = VegaLiteDocument.Parse(Chart("\"geoshape\"", Rows, "{ " + Xy + " }")).Render(cancellationToken: Ct);
        Assert.NotNull(empty);
    }

    /// <summary>
    ///     Gets the charts of the supported subset: a pie, a donut, a radial chart, a multi-series line with a point overlay, a stacked area, an area with a line overlay, a trail,
    ///     text labels and an imputed line. Each is an original chart with made-up numbers.
    /// </summary>
    /// <returns>The specification texts with their names.</returns>
    private static (string Name, string Json)[] SupportedCharts()
    {
        const string series = """
            [ { "x": 0, "y": 3, "g": "a" }, { "x": 1, "y": 5, "g": "a" }, { "x": 2, "y": 4, "g": "a" }, { "x": 0, "y": 2, "g": "b" }, { "x": 2, "y": 6, "g": "b" } ]
            """;
        const string xy = "\"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"color\": { \"field\": \"g\", \"type\": \"nominal\" }";
        const string labels = "[ { \"x\": 1, \"y\": 10, \"v\": 1234.5 }, { \"x\": 2, \"y\": 20, \"v\": 7 }, { \"x\": 3, \"y\": 30, \"v\": 0.26 } ]";
        const string radial = """
            { "theta": { "field": "v", "type": "quantitative", "stack": true },
              "radius": { "field": "v", "type": "quantitative", "scale": { "type": "sqrt", "zero": true, "rangeMin": 20 } },
              "color": { "field": "g", "type": "nominal" } }
            """;
        return
        [
            ("pie", Chart("\"arc\"", Slices, PieEncoding)),
            ("donut", Chart("{ \"type\": \"arc\", \"innerRadius\": 50, \"padAngle\": 0.02, \"cornerRadius\": 3 }", Slices, PieEncoding)),
            ("radial", Chart("{ \"type\": \"arc\", \"innerRadius\": 20, \"stroke\": \"#fff\" }", Slices, radial)),
            ("line", Chart("{ \"type\": \"line\", \"point\": true, \"interpolate\": \"monotone\" }", series, "{ " + xy + ", \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }")),
            ("stacked-area", Chart("{ \"type\": \"area\", \"line\": true }", series, "{ " + xy + ", \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }")),
            ("trail", Chart("\"trail\"", series, "{ " + xy + ", \"y\": { \"field\": \"y\", \"type\": \"quantitative\" }, \"size\": { \"field\": \"y\", \"type\": \"quantitative\" } }")),
            ("text", Chart("{ \"type\": \"text\", \"fontSize\": 14 }", labels, "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" }, \"text\": { \"field\": \"v\", \"type\": \"quantitative\", \"format\": \",.1f\" } }")),
            ("impute", Chart("\"line\"", series, "{ " + xy + ", \"y\": { \"field\": \"y\", \"type\": \"quantitative\", \"impute\": { \"value\": 0 } } }")),
        ];
    }

    /// <summary>
    ///     Validates that the whole supported subset (the pie, the donut, the radial chart, a multi-series line with points, a stacked area, a trail, text labels and an imputed line)
    ///     renders in strict mode without any warning or unsupported report, and that every one of them paints something.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_PathMarks_SupportedSubsetDeliversNoDiagnosticsInStrictMode()
    {
        foreach (var (name, json) in SupportedCharts())
        {
            // Arrange
            var document = VegaLiteDocument.Parse(json);
            var delivered = new List<string>();
            var options = new VegaLiteRenderOptions
            {
                ThrowOnUnsupportedFeature = true,
                WarningHandler = w => delivered.Add("W " + w.Message),
                UnsupportedFeatureHandler = u => delivered.Add("U " + u),
            };

            // Act: throws if anything unsupported is found
            using var surface = document.Render(options, Ct);
            using var measured = document.Render(100, 100, options, Ct);

            // Assert
            Assert.True(delivered.Count == 0, name + ": " + string.Join("; ", delivered));
            Assert.NotNull(VegaLiteTestHelpers.InkBounds(surface, VegaLiteTestHelpers.White));
            Assert.Equal((100, 100), (measured.Width, measured.Height));
            Assert.Equal(document.Measure(options, Ct).Width, surface.Width);
        }
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of every supported chart are byte-identical, and that the scenes built from the same text have the same items.
    /// </summary>
    /// <returns>A task that completes when the concurrent renders are done.</returns>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_PathMarks_RepeatedAndConcurrentRendersAreIdentical()
    {
        var token = Ct;
        foreach (var (name, json) in SupportedCharts())
        {
            // Arrange
            var document = VegaLiteDocument.Parse(json);
            using var first = document.Render(cancellationToken: token);
            var baseline = VegaLiteTestHelpers.Snapshot(first);

            // Act: again on this thread, from a document parsed again, and from several threads through the same document
            using var second = document.Render(cancellationToken: token);
            using var parsed = VegaLiteDocument.Parse(json).Render(cancellationToken: token);
            var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(
                () =>
                {
                    using var surface = document.Render(cancellationToken: token);
                    return VegaLiteTestHelpers.Snapshot(surface);
                },
                token)));

            // Assert
            Assert.True(baseline.AsSpan().SequenceEqual(VegaLiteTestHelpers.Snapshot(second)), name);
            Assert.True(baseline.AsSpan().SequenceEqual(VegaLiteTestHelpers.Snapshot(parsed)), name);
            Assert.All(concurrent, snapshot => Assert.True(baseline.AsSpan().SequenceEqual(snapshot), name));

            // Assert: the scenes agree item by item (the roles and the numbers that identify the geometry)
            var options = new VegaLiteRenderOptions();
            Assert.Equal(DescribeScene(document.BuildScene(options, token)), DescribeScene(VegaLiteDocument.Parse(json).BuildScene(options, token)));
        }
    }

    /// <summary>
    ///     Describes the items of a scene as text with the roles and the numbers that identify their geometry, in the invariant culture.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>One text for the whole scene.</returns>
    private static string DescribeScene(Scene scene)
    {
        static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        return string.Join(
            "|",
            scene.Enumerate().Select(e => e.Item switch
            {
                ScenePath p => p.Role + ":" + string.Join(',', p.Segments.Select(s => $"{s.Kind}{N(s.X)}/{N(s.Y)}/{N(s.X1)}/{N(s.Y1)}/{N(s.X2)}/{N(s.Y2)}")),
                SceneArc a => $"{a.Role}:{N(a.X)},{N(a.Y)},{N(a.InnerRadius)},{N(a.OuterRadius)},{N(a.StartAngle)},{N(a.EndAngle)}",
                SceneTrail t => t.Role + ":" + string.Join(',', t.Runs.SelectMany(r => r).Select(p => $"{N(p.X)}/{N(p.Y)}/{N(p.Size)}")),
                SceneText x => $"{x.Role}:{x.Text}@{N(x.X)},{N(x.Y)}",
                SceneSymbol y => $"{y.Role}:{N(y.X)},{N(y.Y)},{N(y.Size)}",
                _ => e.Item.Role,
            }));
    }

    /// <summary>
    ///     Validates the <see cref="VegaLiteRenderOptions.Scale"/> option for the new marks: the surface of a chart at the scale 2 is twice as large, the scene is the same, and a point
    ///     that the derivation puts inside a wedge of the pie (at 45, 135 and 270 degrees, 50 pixels from the middle) has the same color at the doubled position.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_PathMarks_ScaleKeepsTheGeometryAndMultipliesThePixels()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Chart("\"arc\"", Slices, PieEncoding));
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };
        var doubled = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, Scale = 2 };

        // Act
        using var one = document.Render(options, Ct);
        using var two = document.Render(doubled, Ct);
        var scene = document.BuildScene(options, Ct);
        var view = ViewOf(scene);

        // Assert: twice the size (the chart is 200 pixels and its padding is 5 on every side)
        Assert.Equal((210, 210), (one.Width, one.Height));
        Assert.Equal((420, 420), (two.Width, two.Height));

        // Assert: the wedges keep their colors at the doubled positions, and the corner is the background
        var colors = new[] { (Math.PI / 4, Blue), (3 * Math.PI / 4, Orange), (3 * Math.PI / 2, Red) };
        foreach (var (angle, color) in colors)
        {
            var p = OnPie(view, angle, 50);
            Assert.True(VegaLiteTestHelpers.HasInk(one, p.X, p.Y, color, 0));
            Assert.True(VegaLiteTestHelpers.HasInk(two, 2 * p.X, 2 * p.Y, color, 0));
        }

        Assert.Equal(VegaLiteTestHelpers.White, two[(int)(2 * view.X) + 4, (int)(2 * view.Y) + 4]);

        // Assert: the area of a wedge grows with the square of the scale (the blue wedge covers four times as many pixels)
        var area = VegaLiteTestHelpers.CountNear(one, Blue, 0, 0, one.Width, one.Height);
        var doubledArea = VegaLiteTestHelpers.CountNear(two, Blue, 0, 0, two.Width, two.Height);
        Assert.InRange(doubledArea, area * 3.9, area * 4.3);
    }

    /// <summary>
    ///     Validates that the host's culture does not change the result: with the current culture of a language that has another decimal separator, another digit grouping and
    ///     the Turkish dotted and dotless <c>i</c>, the labels, the scene and the pixels are the same as with the invariant culture.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_PathMarks_CultureDoesNotChangeTheScenesOrThePixels()
    {
        // Arrange
        var charts = SupportedCharts().Where(c => c.Name is "text" or "line" or "radial" or "pie").ToList();
        var baseline = new Dictionary<string, byte[]>();
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            foreach (var (name, json) in charts)
            {
                using var surface = VegaLiteDocument.Parse(json).Render(cancellationToken: Ct);
                baseline[name] = VegaLiteTestHelpers.Snapshot(surface);
            }

            foreach (var cultureName in new[] { "de-DE", "tr-TR", "fr-FR", "ar-SA" })
            {
                CultureInfo culture;
                try
                {
                    culture = new CultureInfo(cultureName);
                }
                catch (CultureNotFoundException)
                {
                    // A host without the culture data (the invariant globalization mode) cannot change the result, so it has nothing to check
                    continue;
                }

                // Act
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                foreach (var (name, json) in charts)
                {
                    var document = VegaLiteDocument.Parse(json);
                    using var surface = document.Render(cancellationToken: Ct);

                    // Assert
                    Assert.True(baseline[name].AsSpan().SequenceEqual(VegaLiteTestHelpers.Snapshot(surface)), name + " in " + cultureName);
                    if (name == "text")
                    {
                        var labels = Items<SceneText>(document.BuildScene(new VegaLiteRenderOptions(), Ct), "mark-text");
                        Assert.Equal(["1,234.5", "7.0", "0.3"], labels.Select(l => l.Text));
                    }
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    /// <summary>
    ///     Validates that an <c>invalid</c> mode and an <c>interpolate</c> name that come from the configuration are reported like the ones in a mark definition, at the value in the
    ///     configuration (the <c>show</c> mode, the bundle interpolation of an area and a name that Vega-Lite does not know), that the chart is still drawn, and that strict mode stops at the report.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Unsupported_ConfigValuesOfInvalidAndInterpolateAreReportedAtTheConfig()
    {
        // Arrange: a mark with no property of its own and a configuration that gives it the value, with the report that the value must cause
        const string Rows = "[ { \"x\": 1, \"y\": 2 }, { \"x\": 2, \"y\": 3 } ]";
        const string Xy = "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }";
        var cases = new (string Mark, string Config, string Expected)[]
        {
            ("\"line\"", "{ \"line\": { \"invalid\": \"show\" } }", "vl-mark-invalid-show /config/line/invalid"),
            ("\"area\"", "{ \"mark\": { \"interpolate\": \"bundle\" } }", "vl-mark-interpolate-bundle-area /config/mark/interpolate"),
            ("\"line\"", "{ \"line\": { \"interpolate\": \"zigzag\" } }", "vl-mark-interpolate-unknown /config/line/interpolate"),
            ("{ \"type\": \"line\", \"style\": \"loud\" }", "{ \"style\": { \"loud\": { \"invalid\": null } } }", "vl-mark-invalid-show /config/style/loud/invalid"),
        };

        foreach (var (mark, config, expected) in cases)
        {
            var chart = Chart(mark, Rows, Xy);
            var document = VegaLiteDocument.Parse(chart[..chart.LastIndexOf('}')] + ", \"config\": " + config + " }");
            var reported = new List<string>();

            // Act: the chart is still drawn
            using var surface = document.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => reported.Add(u.Feature + " " + u.Location.Pointer) }, Ct);

            // Assert: the report, once, at the configuration; strict mode throws it
            Assert.Equal([expected], reported);
            Assert.True(surface.Width >= 210);
            var failure = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
            Assert.Equal(expected, failure.Feature + " " + failure.Location.Pointer);
        }
    }

    /// <summary>
    ///     Validates that a text with a font size that no font can have (1e300, which is finite as a double and infinity as a single; a limit does not help) renders without an exception
    ///     and draws no label, while the same chart with an ordinary font size has its label.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_ExtremeValues_AHugeFontSizeDrawsNoLabelAndNeverThrows()
    {
        // Arrange: one label; the marks differ in the font size and in the limit only
        const string Rows = "[ { \"x\": 1, \"t\": \"Hello\" } ]";
        const string Encoding = "{ \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"axis\": null }, \"text\": { \"field\": \"t\", \"type\": \"nominal\" } }";

        foreach (var mark in new[]
        {
            "{ \"type\": \"text\", \"fontSize\": 1e300 }",
            "{ \"type\": \"text\", \"fontSize\": 1e300, \"limit\": 100 }",
            "{ \"type\": \"text\", \"fontSize\": 3e38, \"limit\": 100 }",
            "{ \"type\": \"text\", \"fontSize\": 1e-300 }",
        })
        {
            var document = VegaLiteDocument.Parse(Chart(mark, Rows, Encoding));

            // Act: no exception from the layout, the scene or the painter
            var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
            using var surface = document.Render(new VegaLiteRenderOptions(), Ct);

            // Assert: no label in the scene (or none that the painter can draw), and a surface
            Assert.True(surface.Width > 0);
            Assert.All(Items<SceneText>(scene, "mark-text"), t => Assert.True(t.FontSize < 1e-100));
        }

        // Act / Assert: the same chart with an ordinary size has its label
        var ordinary = VegaLiteDocument.Parse(Chart("{ \"type\": \"text\", \"fontSize\": 14, \"limit\": 100 }", Rows, Encoding));
        var label = Assert.Single(Items<SceneText>(ordinary.BuildScene(new VegaLiteRenderOptions(), Ct), "mark-text"));
        Assert.Equal(("Hello", 14.0), (label.Text, label.FontSize));
    }
}
