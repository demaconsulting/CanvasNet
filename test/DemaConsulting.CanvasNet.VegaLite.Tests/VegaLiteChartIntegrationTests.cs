using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the first charts: bars, points, rules and ticks with axes and titles, rendered through
///     the public API and checked by sample pixels, with every position derived by hand from the scale and layout formulas.
/// </summary>
public class VegaLiteChartIntegrationTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The names of the fixture specifications.</summary>
    private static readonly string[] FixtureNames =
        ["bar-inline.vl.json", "point-inline.vl.json", "rule-tick-inline.vl.json", "titles-config.vl.json", "unsupported.vl.json", "data-files.vl.json"];

    /// <summary>The default color of a mark.</summary>
    private static readonly Rgba32 Blue = new(0x4c, 0x78, 0xa8, 255);

    /// <summary>
    ///     Reads a fixture specification.
    /// </summary>
    /// <param name="name">The file name in the <c>Fixtures</c> folder.</param>
    /// <returns>The specification text.</returns>
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>
    ///     Opens a fixture from its path, so that locations carry the file name.
    /// </summary>
    /// <param name="name">The file name in the <c>Fixtures</c> folder.</param>
    /// <returns>The document.</returns>
    private static VegaLiteDocument OpenFixture(string name) => VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>
    ///     Measures the width of a text at the guide label size, so that layout offsets can be derived without a recorded constant.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The width in pixels.</returns>
    private static double LabelWidth(string text) => new TextMeasurer().MeasureWidth(text, new TextFont("sans-serif", 10));

    /// <summary>
    ///     Asserts that a pixel has a color, within a tolerance per channel (anti-aliasing and rounding of the blend).
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="red">The expected red.</param>
    /// <param name="green">The expected green.</param>
    /// <param name="blue">The expected blue.</param>
    /// <param name="tolerance">The allowed difference per channel.</param>
    private static void AssertPixel(Surface surface, int x, int y, double red, double green, double blue, double tolerance)
    {
        var pixel = surface[x, y];
        Assert.True(
            Math.Abs(pixel.R - red) <= tolerance && Math.Abs(pixel.G - green) <= tolerance && Math.Abs(pixel.B - blue) <= tolerance && pixel.A == 255,
            $"Pixel ({x}, {y}) is {pixel.R},{pixel.G},{pixel.B},{pixel.A}; expected about {red},{green},{blue}.");
    }

    /// <summary>
    ///     Validates the bar chart: the canvas size, bar colors at the bar centers, the axis lines, ticks and grid lines, and the
    ///     title, at pixel positions derived from the layout formulas.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_InlineBarHasExpectedSamplePixels()
    {
        // Arrange: the data rectangle is 240 by 200. Left of it the y axis needs 7 px (tick and label padding) plus the widest label
        // ("100"), then 4 px of title padding and an 11 px title; 5 px of padding is added. Above it the title (13 px) sits 4 px
        // above the top label overhang (5 px). Below it the x axis needs 32 px (17 for ticks and labels, 4 + 11 for its title).
        // Right of it the 5 px of padding is all (the end of the domain line is the widest guide).
        var origin = (X: 5 + Math.Ceiling(7 + LabelWidth("100") + 4 + 11), Y: 5.0 + 5 + 4 + 13);
        var document = OpenFixture("bar-inline.vl.json");

        // Act
        var size = document.Measure(cancellationToken: Ct);
        using var surface = document.Render(cancellationToken: Ct);
        var ox = (int)origin.X;
        var oy = (int)origin.Y;

        // Assert: canvas = origin + 240 wide + 5, origin + 200 high + 32 + 5
        Assert.Equal((ox + 240 + 5, oy + 200 + 32 + 5), (surface.Width, surface.Height));
        Assert.Equal(new VegaLiteSize(surface.Width, surface.Height), size);

        // Assert: bar centers are the mark color (bands of 54 px starting at 3, 63, 123, 183; the bars for 43, 28, 55 and 91 rise
        // to y = 114, 144, 90 and 18)
        Assert.Equal(Blue, surface[ox + 30, oy + 160]);
        Assert.Equal(Blue, surface[ox + 90, oy + 172]);
        Assert.Equal(Blue, surface[ox + 150, oy + 145]);
        Assert.Equal(Blue, surface[ox + 210, oy + 110]);

        // Assert: above a bar and in a gap between bars the plot is the (transparent) view over the white background
        Assert.Equal(VegaLiteTestHelpers.White, surface[ox + 90, oy + 100]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[ox + 60, oy + 190]);

        // Assert: a grid line (at 160, 120, 80, 40 and 0) is #ddd in a gap; the domain line along y = 200 and the tick below the East
        // band center (x = 30) are #888; a y tick left of the data rectangle is #888
        AssertPixel(surface, ox + 60, oy + 160, 221, 221, 221, 1);
        AssertPixel(surface, ox + 100, oy + 200, 136, 136, 136, 1);
        AssertPixel(surface, ox + 30, oy + 203, 136, 136, 136, 1);
        AssertPixel(surface, ox - 3, oy + 120, 136, 136, 136, 1);

        // Assert: the padding is untouched and the title and the axis texts left ink in their bands
        Assert.True(VegaLiteTestHelpers.RegionIs(surface, 0, 0, 3, surface.Height, VegaLiteTestHelpers.White));
        var titleBand = VegaLiteTestHelpers.InkBounds(CopyRows(surface, 0, oy - 5), VegaLiteTestHelpers.White);
        Assert.NotNull(titleBand);
        Assert.InRange(titleBand.Value.Top, 3, oy - 9);
        var labels = VegaLiteTestHelpers.InkBounds(CopyRows(surface, oy + 207, oy + 222), VegaLiteTestHelpers.White);
        Assert.NotNull(labels);
    }

    /// <summary>
    ///     Copies a band of rows of a surface into a new surface, to measure the ink in that band.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="top">The first row.</param>
    /// <param name="bottom">The row after the last.</param>
    /// <returns>A new surface with the rows.</returns>
    private static Surface CopyRows(Surface source, int top, int bottom)
    {
        var copy = new Surface(source.Width, bottom - top);
        for (var y = top; y < bottom; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                copy[x, y - top] = source[x, y];
            }
        }

        return copy;
    }

    /// <summary>
    ///     Validates points, rules and ticks: sample pixels of the scatter plot (blended mark colors), of rules (opaque stems) and of
    ///     ticks (blended bars) at positions derived from the scale formulas.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_PointRuleTickHaveExpectedSamplePixels()
    {
        // Arrange: scatter plot without its legends (the legend geometry is the subject of the legend integration tests). The data rectangle
        // is 300 by 200; the y axis is 0 to 100 (widest label "100"), the title sits above the top label overhang as in the bar chart,
        // so the origin is the same formula. x runs 6 to 20 (300 / 14 per unit).
        var ox = (int)(5 + Math.Ceiling(7 + LabelWidth("100") + 4 + 11));
        const int oy = 27;
        var withoutLegends = Fixture("point-inline.vl.json")
            .Replace("\"color\": { \"field\": \"glaze\", \"type\": \"nominal\" }", "\"color\": { \"field\": \"glaze\", \"type\": \"nominal\", \"legend\": null }", StringComparison.Ordinal)
            .Replace("\"size\": { \"field\": \"load\", \"type\": \"quantitative\" }", "\"size\": { \"field\": \"load\", \"type\": \"quantitative\", \"legend\": null }", StringComparison.Ordinal);
        using var scatter = VegaLiteDocument.Parse(withoutLegends).Render(cancellationToken: Ct);

        // Assert: the canvas has the 5 px of padding and 1 px for the half tick at the right end; the points are at
        // x = 300 (h - 6) / 14, y = 200 - 2 yield: (12, 78) -> (128.6, 44) gloss (blue), (14, 74) -> (171.4, 52) matte (orange)
        Assert.Equal((ox + 300 + 1 + 5, oy + 200 + 32 + 5), (scatter.Width, scatter.Height));

        // Assert: the points are drawn with 0.7 opacity over white: blue (76, 120, 168) -> (129.7, 160.5, 194.1),
        // orange (245, 133, 24) -> (248, 169.6, 93.3)
        AssertPixel(scatter, ox + 128, oy + 44, 129.7, 160.5, 194.1, 2);
        AssertPixel(scatter, ox + 172, oy + 52, 248, 169.6, 93.3, 2);

        // Assert: the row without a y value is not drawn, and the plot is clear next to a point (white at 20 px right of (12, 78))
        Assert.Equal(VegaLiteTestHelpers.White, scatter[ox + 128 + 25, oy + 44]);

        // Arrange: rules. The y axis labels "One", "Two", "Three" are right aligned 7 px left of the data rectangle, there is no y title;
        // the title sits 4 px above the data rectangle top (the y axis starts at y = 0) and is 13 px high: origin y = 5 + 4 + 13
        var ruleSpec = Fixture("rule-tick-inline.vl.json").Replace("\"mark\": \"tick\"", "\"mark\": { \"type\": \"rule\", \"strokeWidth\": 2 }", StringComparison.Ordinal);
        var rx = (int)(5 + Math.Ceiling(7 + LabelWidth("Three")));
        var ry = 5 + 4 + 13;
        using var rules = VegaLiteDocument.Parse(ruleSpec).Render(cancellationToken: Ct);

        // Assert: x runs 0 to 45 over 300 px; the stems are drawn from x(v) back to x(0) at the band centers y = 20, 60, 100, 2 px thick
        // so they cover rows 19 and 20 completely; they are opaque black (rules are not made translucent)
        Assert.Equal(new Rgba32(0, 0, 0, 255), rules[rx + 40, ry + 19]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), rules[rx + 40, ry + 20]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), rules[rx + 150, ry + 59]);
        Assert.Equal(VegaLiteTestHelpers.White, rules[rx + 40, ry + 40]);

        // Arrange: ticks 2 px thick: the tick of the first reading (12 minutes at x = 80) spans the band of "One" (tick bands have
        // inner padding 0.25 over 3 bands of 40 px: 30 px high, starting at 5)
        var tickSpec = Fixture("rule-tick-inline.vl.json").Replace("\"mark\": \"tick\"", "\"mark\": { \"type\": \"tick\", \"thickness\": 2 }", StringComparison.Ordinal);
        using var ticks = VegaLiteDocument.Parse(tickSpec).Render(cancellationToken: Ct);

        // Assert: blue at 0.7 opacity inside the tick, white just outside it
        AssertPixel(ticks, rx + 79, ry + 20, 129.7, 160.5, 194.1, 2);
        AssertPixel(ticks, rx + 80, ry + 6, 129.7, 160.5, 194.1, 2);
        Assert.Equal(VegaLiteTestHelpers.White, ticks[rx + 83, ry + 20]);
        Assert.Equal(VegaLiteTestHelpers.White, ticks[rx + 80, ry + 3]);
    }

    /// <summary>
    ///     Validates that an unsupported mark, transform and aggregate are each reported once with the pointer, line and
    ///     column of the construct, in document order, and that the chart is still rendered without them.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Unsupported_MarkAndTransformCarryPointerLineColumn()
    {
        // Arrange
        var document = OpenFixture("unsupported.vl.json");
        var reports = new List<VegaLiteUnsupportedFeature>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = reports.Add };

        // Act
        using var surface = document.Render(options, Ct);

        // Assert: exactly one report per construct with its stable identifier, pointer and one-based line and column of its value
        Assert.Equal(
            [
                ("vl-transform-regression", "/transform/0", 12, 5),
                ("vl-mark-line", "/mark", 14, 11),
                ("vl-encoding-bandposition", "/encoding/y/bandPosition", 17, 71),
            ],
            reports.Select(r => (r.Feature, r.Location.Pointer, r.Location.Line, r.Location.Column)));
        Assert.All(reports, r => Assert.EndsWith("unsupported.vl.json", r.Location.File, StringComparison.Ordinal));

        // Assert: a line mark is not drawn, so no data, scale or axis work is done for it: the view is empty and has the default size
        Assert.Equal((310, 310), (surface.Width, surface.Height));
        Assert.Equal(VegaLiteTestHelpers.White, surface[150, 150]);
    }

    /// <summary>
    ///     Validates that measuring gives the size of the surface that rendering produces, with and without a scale.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Measure_MatchesNaturalRenderedSize()
    {
        foreach (var name in FixtureNames)
        {
            // Arrange
            var document = OpenFixture(name);

            // Act
            var natural = document.Measure(cancellationToken: Ct);
            var doubled = document.Measure(new VegaLiteRenderOptions { Scale = 2 }, Ct);
            using var surface = document.Render(cancellationToken: Ct);
            using var scaled = document.Render(new VegaLiteRenderOptions { Scale = 2 }, Ct);

            // Assert: the sizes agree, the scale multiplies them and rounds to whole pixels
            Assert.Equal(new VegaLiteSize(surface.Width, surface.Height), natural);
            Assert.Equal(new VegaLiteSize(scaled.Width, scaled.Height), doubled);
            Assert.Equal(new VegaLiteSize(natural.Width * 2, natural.Height * 2), doubled);
        }

        // Assert: the fixtures have different natural sizes, so the sizes do depend on the chart
        Assert.Equal(new VegaLiteSize(289, 264), OpenFixture("bar-inline.vl.json").Measure(cancellationToken: Ct));

        // Assert: a chart whose rows come from a file beside the specification has the size of the inline chart
        Assert.Equal(new VegaLiteSize(289, 264), OpenFixture("data-files.vl.json").Measure(cancellationToken: Ct));
    }

    /// <summary>
    ///     Validates that a chart drawn from a CSV file beside its specification is pixel-identical to the same chart with
    ///     inline rows, and that the JSON fixture with nested rows draws the same bars through a data path and
    ///     <c>format.property</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_FileBackedChartsMatchTheInlineChart()
    {
        // Arrange: the inline fixture is the reference; the JSON document is read with a property path and a data path
        using var reference = OpenFixture("bar-inline.vl.json").Render(cancellationToken: Ct);
        var fromJson = Fixture("data-files.vl.json").Replace(
            "\"data\": { \"url\": \"data-files.csv\" }",
            "\"data\": { \"url\": \"data-values.json\", \"format\": { \"property\": \"report.rows\" } }",
            StringComparison.Ordinal);

        // Act
        using var fromCsv = OpenFixture("data-files.vl.json").Render(cancellationToken: Ct);
        using var viaProperty = VegaLiteDocument
            .Parse(fromJson, new VegaLiteOpenOptions { DataPaths = [Path.Combine(AppContext.BaseDirectory, "Fixtures")] })
            .Render(cancellationToken: Ct);

        // Assert
        Assert.Equal(VegaLiteTestHelpers.Snapshot(reference), VegaLiteTestHelpers.Snapshot(fromCsv));
        Assert.Equal(VegaLiteTestHelpers.Snapshot(reference), VegaLiteTestHelpers.Snapshot(viaProperty));
    }

    /// <summary>
    ///     Validates that an explicit size scales the chart uniformly to fit and centers it over the background.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_WidthHeightUniformlyFitsAndCenters()
    {
        // Arrange: the natural chart is 289 by 264; in 600 by 300 the height limits the scale to 300 / 264 (the chart is 328.4 wide
        // and centered: 135.8 px of margin on each side); in 300 by 600 the width limits the scale to 300 / 289
        var document = OpenFixture("bar-inline.vl.json");
        var background = new Rgba32(250, 240, 230, 255);
        var options = new VegaLiteRenderOptions { Background = background };

        // Act
        using var wide = document.Render(600, 300, options, Ct);
        using var tall = document.Render(300, 600, options, Ct);

        // Assert: the sizes are exact and the margins are the background
        Assert.Equal((600, 300), (wide.Width, wide.Height));
        Assert.Equal((300, 600), (tall.Width, tall.Height));
        Assert.True(VegaLiteTestHelpers.RegionIs(wide, 0, 0, 130, 300, background));
        Assert.True(VegaLiteTestHelpers.RegionIs(wide, 470, 0, 600, 300, background));

        // Assert: the ink is centered in both directions, within the few pixels of padding asymmetry of the chart
        var wideInk = VegaLiteTestHelpers.InkBounds(wide, background)!.Value;
        Assert.InRange(((wideInk.Left + wideInk.Right) / 2.0) - 299.5, -4, 4);
        var tallInk = VegaLiteTestHelpers.InkBounds(tall, background)!.Value;
        Assert.InRange(((tallInk.Top + tallInk.Bottom) / 2.0) - 299.5, -6, 6);
        Assert.True(tallInk.Top > 100 && tallInk.Bottom < 500);

        // Assert: the bars are scaled: the East bar center (30, 160) in chart pixels of the natural layout is at 1.1364 times that
        // plus the offset, and it is still the mark color
        var scale = 300.0 / 264;
        var left = (600 - (289 * scale)) / 2;
        var x = (int)Math.Round(left + (scale * (44 + 30)));
        var y = (int)Math.Round(scale * (27 + 160));
        Assert.Equal(Blue, wide[x, y]);
    }

    /// <summary>
    ///     Validates the failures: an invalid axis format is a specification error with a location, and a chart with too many items or
    ///     ticks is a render error with a location; reports found before the failure are still delivered.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_ChartFailuresCarryLocations()
    {
        // Arrange: a bad format, and a point chart with many rows that cannot fit the item budget
        var invalidAxis = VegaLiteDocument.Parse("""
            { "mark": "point", "data": { "values": [ { "a": 1 } ] },
              "encoding": { "x": { "field": "a", "type": "quantitative", "axis": { "format": "!!" } } } }
            """);
        var rows = string.Join(',', Enumerable.Range(0, 5000).Select(i => "{\"k\":\"c" + i + "\",\"v\":" + i + "}"));
        var big = VegaLiteDocument.Parse("{\"mark\":\"point\",\"data\":{\"values\":[" + rows + "]},\"encoding\":{\"x\":{\"field\":\"v\",\"type\":\"quantitative\",\"bandPosition\":0.5},\"color\":{\"field\":\"k\",\"type\":\"nominal\",\"legend\":null}}}");
        var reported = new List<string>();
        var options = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 100 }, UnsupportedFeatureHandler = u => reported.Add(u.Feature) };

        // Act
        var format = Assert.Throws<VegaLiteSpecException>(() => invalidAxis.Render(cancellationToken: Ct));
        var items = Assert.Throws<VegaLiteRenderException>(() => big.Render(options, Ct));

        // Assert: the format error points at the format value; the budget error at the mark; the report found before the failure was still delivered
        Assert.Equal("/encoding/x/axis/format", format.Location.Pointer);
        Assert.Equal(2, format.Location.Line);
        Assert.Equal("/mark", items.Location.Pointer);
        Assert.Contains("MaxSceneItems", items.Message, StringComparison.Ordinal);
        Assert.Contains("vl-encoding-bandposition", reported);
    }

    /// <summary>
    ///     Validates that the scene budget counts what is created: thousands of rows that draw nothing fit a small budget.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_RowsThatDrawNothingCostNoBudget()
    {
        // Arrange: 5000 rows whose x is null, so no mark exists; the axis and the marks group need far fewer than 100 items
        var rows = string.Join(',', Enumerable.Range(0, 5000).Select(_ => "{\"v\":null}"));
        var document = VegaLiteDocument.Parse("{\"mark\":\"point\",\"data\":{\"values\":[" + rows + "]},\"encoding\":{\"x\":{\"field\":\"v\",\"type\":\"quantitative\"}}}");

        // Act
        using var surface = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 100 } }, Ct);

        // Assert
        Assert.True(surface.Width > 0);
    }

    /// <summary>
    ///     Validates the exact boundary of the scene item limit for every kind of item (root and view groups, title, axes with
    ///     grid, ticks, labels, domain and title, the marks group and the marks): a limit of N allows a chart of N items and
    ///     every smaller limit fails with a located render error naming the limit, so no item is created past the limit.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="expectedItems">The number of scene items of the chart, counted by hand.</param>
    [Theory]
    [InlineData("""{ "title": "T" }""", 4)] // root, view, title group, title text
    [InlineData("""{ "title": { "text": "T", "subtitle": "S" } }""", 5)] // plus the subtitle text
    [InlineData("""{ "mark": "point", "data": { "values": [ { "a": 1 } ] } }""", 4)] // root, view, marks group and the one point that an unencoded row draws at the center
    [InlineData("""{ "mark": "point", "data": { "values": [ { "a": 1 }, { "a": 2 } ] }, "encoding": { "x": { "field": "a", "type": "quantitative", "axis": null } } }""", 5)] // root, view, marks group, two points
    [InlineData(
        """{ "mark": "tick", "data": { "values": [ { "a": 0 }, { "a": 5 }, { "a": 10 } ] }, "width": 200, "encoding": { "x": { "field": "a", "type": "quantitative", "axis": { "grid": true, "tickCount": 2, "title": null } } } }""",
        18)] // root, view, axis group, grid group, 3 grid lines, 3 ticks, 3 labels, domain, marks group, 3 ticks marks (no title, no y channel)
    public void CanvasNetVegaLite_SystemIntegration_Limits_SceneItemLimitIsExactAtTheBoundary(string json, int expectedItems)
    {
        // Arrange
        var document = VegaLiteDocument.Parse(json);

        // Act: the scene that the pipeline builds, counted with no limit in the way
        var count = document.BuildScene(new VegaLiteRenderOptions(), Ct).CountItems();

        // Assert: the hand count is right; a limit of N accepts it and N - 1 and every smaller limit reject it
        Assert.Equal(expectedItems, count);
        for (var limit = 1; limit <= count; limit++)
        {
            var options = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = limit } };
            if (limit == count)
            {
                Assert.Equal(count, document.BuildScene(options, Ct).CountItems());
                _ = document.Measure(options, Ct);
            }
            else
            {
                var ex = Assert.Throws<VegaLiteRenderException>(() => document.Measure(options, Ct));
                Assert.Contains("MaxSceneItems", ex.Message, StringComparison.Ordinal);
                Assert.Contains($"more than {limit} scene items", ex.Message, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    ///     Validates the boundary on a chart with two axes, a grid, a title and many marks: the count equals the items of the scene, a
    ///     limit equal to it renders, one below it fails, and the failure of a limit inside each part is located at that part.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_SceneItemLimitCountsEveryItemOfTheScene()
    {
        // Arrange: a bar chart with a title, a band x axis (with a title), a continuous y axis (grid, title) and four bars
        var document = VegaLiteDocument.Parse("""
            { "title": "Counts", "mark": "bar",
              "data": { "values": [ { "k": "a", "v": 1 }, { "k": "b", "v": 3 }, { "k": "c", "v": 2 }, { "k": "d", "v": 4 } ] },
              "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }
            """);

        // Act
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        var count = scene.CountItems();

        // Assert: by hand: root and view (2), title group and text (2), x axis = group + 4 ticks + 4 labels + domain + title
        // (11), y axis = group + grid group + the grid lines, ticks and labels of every tick + domain + title, marks group + 4 bars (5)
        var yTicks = scene.FindAll("axis-tick").Count(e => e.Item is SceneRule) - 4;
        Assert.Equal(2 + 2 + 11 + (2 + (3 * yTicks) + 2) + 5, count);
        Assert.Equal(yTicks, scene.FindAll("axis-grid").Count());
        Assert.Equal(4, scene.FindAll("mark-bar").Count());

        // Assert: the limit that equals the count renders and one fewer does not, naming the last item that was charged
        var exact = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count } };
        using var surface = document.Render(exact, Ct);
        Assert.True(surface.Width > 0);
        var below = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count - 1 } };
        var last = Assert.Throws<VegaLiteRenderException>(() => document.Render(below, Ct));
        Assert.Equal("/mark", last.Location.Pointer);

        // Assert: a limit that is spent by the title is located at the title; one spent by an axis, at its channel
        var inTitle = Assert.Throws<VegaLiteRenderException>(() =>
            document.Measure(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 3 } }, Ct));
        Assert.Equal("/title", inTitle.Location.Pointer);
        var inXAxis = Assert.Throws<VegaLiteRenderException>(() =>
            document.Measure(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 6 } }, Ct));
        Assert.Equal("/encoding/x", inXAxis.Location.Pointer);
        var inYAxis = Assert.Throws<VegaLiteRenderException>(() =>
            document.Measure(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 4 + 11 + 3 } }, Ct));
        Assert.Equal("/encoding/y", inYAxis.Location.Pointer);
    }

    /// <summary>
    ///     Validates that labels removed for overlapping cost no budget: a chart whose labels overlap needs only as many items as
    ///     the scene holds.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_LabelsRemovedByOverlapCostNoBudget()
    {
        // Arrange: a narrow axis with 11 ticks whose labels cannot all fit
        var document = VegaLiteDocument.Parse("""
            { "mark": "tick", "width": 60, "data": { "values": [ { "a": 0 }, { "a": 1000000 } ] },
              "encoding": { "x": { "field": "a", "type": "quantitative", "axis": { "tickCount": 11, "title": null } } } }
            """);
        var scene = document.BuildScene(new VegaLiteRenderOptions(), Ct);
        var count = scene.CountItems();

        // Assert: some label was removed, yet the limit that equals the scene renders
        Assert.True(scene.FindAll("axis-label").Count() < scene.FindAll("axis-tick").Count());
        var options = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = count } };
        Assert.Equal(count, document.BuildScene(options, Ct).CountItems());
    }

    /// <summary>
    ///     Validates the axis builder's own budget: it charges the axis group, the grid group, every rule, label and the
    ///     title before it creates them, fails at the channel with the item that passes the limit, and counts exactly what the
    ///     axis holds.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_AxisChargesEveryItemBeforeCreatingIt()
    {
        // Arrange: a y axis with grid, ticks, labels, domain and title
        const string json = """
            { "mark": "point", "data": { "values": [ { "v": 0 }, { "v": 10 } ] },
              "encoding": { "y": { "field": "v", "type": "quantitative", "axis": { "grid": true, "tickCount": 2 } } } }
            """;
        var parts = ChartTestHelpers.Parts(json);
        var scale = parts.Scales["y"]!;
        var model = scale.CreateModel(100);
        var channelDef = parts.Tree.Root["encoding"]!["y"];

        // Act: build with a large budget to count what the axis creates
        var big = new SceneBudget(1000);
        var axis = new AxisBuilder(new TextMeasurer(), parts.Config, null, "t.vl.json")
            .Build(new AxisRequest("y", channelDef, scale, model, 100, 100, parts.Scales.MarkType, big), Ct)!;

        // Assert: the group, the grid group and the items are what was charged, no more
        Assert.Equal(axis.ItemCount + 2, big.Count);

        // Assert: exactly that many fits, one fewer fails at the channel
        var exact = new SceneBudget(axis.ItemCount + 2);
        _ = new AxisBuilder(new TextMeasurer(), parts.Config, null, "t.vl.json")
            .Build(new AxisRequest("y", channelDef, scale, model, 100, 100, parts.Scales.MarkType, exact), Ct);
        Assert.Equal(axis.ItemCount + 2, exact.Count);
        var below = new SceneBudget(axis.ItemCount + 1);
        var ex = Assert.Throws<VegaLiteRenderException>(() => new AxisBuilder(new TextMeasurer(), parts.Config, null, "t.vl.json")
            .Build(new AxisRequest("y", channelDef, scale, model, 100, 100, parts.Scales.MarkType, below), Ct));
        Assert.Equal("/encoding/y", ex.Location.Pointer);

        // Assert: an axis without its own budget uses MaxSceneItems of the limits it was built with
        var limited = new AxisBuilder(new TextMeasurer(), parts.Config, new VegaLiteLimits { MaxSceneItems = 2 }, "t.vl.json");
        Assert.Throws<VegaLiteRenderException>(() => limited.Build(new AxisRequest("y", channelDef, scale, model, 100, 100, parts.Scales.MarkType), Ct));
    }

    /// <summary>
    ///     Validates that a title charges its group and text before they are created.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_TitleChargesGroupAndTextsBeforeCreatingThem()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "title": { "text": "T", "subtitle": "S" } }""", "t.vl.json");
        var config = ConfigResolver.Resolve(tree, null);
        var builder = new TitleBuilder(new TextMeasurer());

        // Act / Assert: the group, the title and the subtitle make three
        var exact = new SceneBudget(3);
        Assert.NotNull(builder.Build(tree.Root["title"], config, true, exact, tree.SourceName));
        Assert.Equal(3, exact.Count);

        // Assert: two fail at the subtitle, one at the group; both are located at the title
        var two = Assert.Throws<VegaLiteRenderException>(() => builder.Build(tree.Root["title"], config, true, new SceneBudget(2), tree.SourceName));
        Assert.Equal("/title", two.Location.Pointer);
        Assert.Equal("t.vl.json", two.Location.File);
        Assert.Throws<VegaLiteRenderException>(() => builder.Build(tree.Root["title"], config, true, new SceneBudget(1), tree.SourceName));

        // Assert: no title costs nothing
        var untouched = new SceneBudget(1);
        Assert.Null(builder.Build(null, config, true, untouched, tree.SourceName));
        Assert.Equal(0, untouched.Count);
    }

    /// <summary>
    ///     Validates that a fork counts on its own and that only an adopted fork changes the count of its origin.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_BudgetForkIsAdoptedOnlyWhenKept()
    {
        // Arrange
        var budget = new SceneBudget(10);
        budget.Add(4, default);

        // Act
        var discarded = budget.Fork();
        discarded.Add(5, default);
        var kept = budget.Fork();
        kept.Add(2, default);

        // Assert: the origin did not see the discarded pass and takes the kept one; a fork keeps the limit
        Assert.Equal(4, budget.Count);
        budget.Adopt(kept);
        Assert.Equal(6, budget.Count);
        Assert.Throws<VegaLiteRenderException>(() => kept.Add(5, default));
        Assert.Throws<ArgumentNullException>(() => budget.Adopt(null!));
    }

    /// <summary>
    ///     Validates that a caller configuration changes the drawn chart: a zero tick size removes the tick marks.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_CallerConfigOverridesSpecificationConfig()
    {
        // Arrange: titles-config.vl.json sets the tick color to #335533 in its own config; the caller turns ticks off
        var document = OpenFixture("titles-config.vl.json");

        // Act
        var with = document.Measure(cancellationToken: Ct);
        var without = document.Measure(new VegaLiteRenderOptions { Config = "{ \"axis\": { \"ticks\": false, \"labels\": false } }" }, Ct);

        // Assert: without tick marks and labels the guides need less room
        Assert.True(without.Width < with.Width);
        Assert.True(without.Height < with.Height);
    }

    /// <summary>
    ///     Validates that a container width gives a canvas of exactly the container width: the data rectangle is what the padding and
    ///     the axes leave, and the chart is laid out again for that rectangle.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_ContainerWidthFillsTheContainer()
    {
        // Arrange: 400 px container; the left guides need 38.68 px (rounded up to 39 on the canvas), the padding 2 * 5, so the data
        // rectangle is 400 - 10 - 38.68 = 351.32 px wide and the canvas 5 + 39 + 351.32 + 5 = 400.32 px, which rounds to 400
        var spec = Fixture("bar-inline.vl.json").Replace("\"width\": 240", "\"width\": \"container\"", StringComparison.Ordinal);
        var document = VegaLiteDocument.Parse(spec);
        var options = new VegaLiteRenderOptions { ContainerWidth = 400 };

        // Act
        var size = document.Measure(options, Ct);
        using var surface = document.Render(options, Ct);

        // Assert: the size is the container width, and the rendering agrees
        Assert.Equal(400, size.Width);
        Assert.Equal(new VegaLiteSize(surface.Width, surface.Height), size);

        // Assert: four bands share the data rectangle: the East bar spans 351.32 / 4 * 0.9 = 79 px of the 87.8 px step
        var step = (400 - 10 - (7 + LabelWidth("100") + 4 + 11)) / 4;
        var barLeft = (int)Math.Ceiling(5 + Math.Ceiling(7 + LabelWidth("100") + 4 + 11) + (step * 0.05) + 1);
        var barRight = (int)Math.Floor(5 + Math.Ceiling(7 + LabelWidth("100") + 4 + 11) + (step * 0.95) - 1);
        Assert.Equal(Blue, surface[barLeft, 27 + 190]);
        Assert.Equal(Blue, surface[barRight, 27 + 190]);
    }

    /// <summary>The red of the third category of the default palette (#e45756).</summary>
    private static readonly Rgba32 Red3 = new(0xe4, 0x57, 0x56, 255);

    /// <summary>The orange of the second category of the default palette (#f58518).</summary>
    private static readonly Rgba32 Orange2 = new(0xf5, 0x85, 0x18, 255);

    /// <summary>The mark of the dot charts: an opaque filled circle of area 200 (a radius of about 7.98 pixels).</summary>
    private const string DotMark = "{\"type\":\"point\",\"filled\":true,\"opacity\":1,\"size\":200}";

    /// <summary>The scale of a position channel of the dot charts: a domain of 0 to 10 over 100 pixels, no nice rounding and no axis.</summary>
    private const string Measure = "\"type\":\"quantitative\",\"axis\":null,\"scale\":{\"domain\":[0,10],\"nice\":false,\"zero\":false}";

    /// <summary>
    ///     Builds a 100 by 100 chart without padding, axes and title, so that the data position (x, y) of a dot with the scale
    ///     domain 0 to 10 is the pixel (10 x, 100 - 10 y) and every dot center can be checked by one pixel.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <param name="encoding">The encoding members.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="extra">Extra top-level members, each followed by a comma, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string Dots(string rows, string transforms, string encoding, string mark = DotMark, string extra = "") =>
        "{" + extra + "\"width\":100,\"height\":100,\"padding\":0,\"data\":{\"values\":" + rows + "},\"transform\":" + transforms
        + ",\"mark\":" + mark + ",\"encoding\":{" + encoding + "}}";

    /// <summary>
    ///     Renders a chart and checks the 100 by 100 size.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="options">The render options, or null.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    private static Surface RenderDots(string json, VegaLiteRenderOptions? options = null)
    {
        var surface = VegaLiteDocument.Parse(json).Render(options, Ct);
        Assert.Equal((100, 100), (surface.Width, surface.Height));
        return surface;
    }

    /// <summary>
    ///     Validates that a calculate and a filter decide which dots are drawn: the calculated field is scaled, the dots of the
    ///     filtered rows are absent and the others sit at the pixel positions the scales give.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Transforms_FilterAndCalculateSelectTheDrawnDots()
    {
        // Arrange: x = 1 to 5, y = 2 x; the filter keeps 4 < y < 10, that is x = 3 and 4 with y = 6 and 8
        var rows = "[{\"x\":1},{\"x\":2},{\"x\":3},{\"x\":4},{\"x\":5}]";
        var json = Dots(
            rows,
            "[{\"calculate\":\"datum.x * 2\",\"as\":\"y\"},{\"filter\":\"datum.y > 4 && datum.y < 10\"}]",
            "\"x\":{\"field\":\"x\"," + Measure + "},\"y\":{\"field\":\"y\"," + Measure + "}");

        // Act
        using var surface = RenderDots(json);

        // Assert: the kept dots are at (30, 40) and (40, 20); the dots of x = 1, 2 and 5 are not drawn
        Assert.Equal(Blue, surface[30, 40]);
        Assert.Equal(Blue, surface[40, 20]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[10, 80]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[20, 60]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[50, 4]);
    }

    /// <summary>
    ///     Validates fold and flatten: the folded fields become key and value rows (a nominal key gives point positions at
    ///     25 and 75 of 100 pixels) and the arrays become one row per element.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Transforms_FoldAndFlattenReshapeTheRows()
    {
        // Arrange: a point scale of two categories over 100 pixels with padding 0.5 has a step of 50, so a is at 25 and b at 75
        var key = "\"y\":{\"field\":\"key\",\"type\":\"nominal\",\"axis\":null}";
        var folded = Dots("[{\"id\":\"r\",\"a\":2,\"b\":6}]", "[{\"fold\":[\"a\",\"b\"]}]", "\"x\":{\"field\":\"value\"," + Measure + "}," + key);
        var flattened = Dots(
            "[{\"k\":\"p\",\"v\":[1,3]},{\"k\":\"q\",\"v\":[5]}]",
            "[{\"flatten\":[\"v\"]}]",
            "\"x\":{\"field\":\"v\"," + Measure + "},\"y\":{\"field\":\"k\",\"type\":\"nominal\",\"axis\":null}");

        // Act
        using var fold = RenderDots(folded);
        using var flatten = RenderDots(flattened);

        // Assert: (a, 2) and (b, 6) only
        Assert.Equal(Blue, fold[20, 25]);
        Assert.Equal(Blue, fold[60, 75]);
        Assert.Equal(VegaLiteTestHelpers.White, fold[60, 25]);
        Assert.Equal(VegaLiteTestHelpers.White, fold[20, 75]);

        // Assert: (p, 1), (p, 3) and (q, 5) only
        Assert.Equal(Blue, flatten[10, 25]);
        Assert.Equal(Blue, flatten[30, 25]);
        Assert.Equal(Blue, flatten[50, 75]);
        Assert.Equal(VegaLiteTestHelpers.White, flatten[10, 75]);
        Assert.Equal(VegaLiteTestHelpers.White, flatten[30, 75]);
        Assert.Equal(VegaLiteTestHelpers.White, flatten[50, 25]);
    }

    /// <summary>
    ///     Validates pivot (the columns of the pivoted values, zero for a missing combination) and lookup (the fields of the
    ///     first matching row of a second inline source) as the source of the scaled fields.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Transforms_PivotAndLookupBuildTheScaledFields()
    {
        // Arrange: u has s = 2 and t = 4, w has s = 6 and no t, which pivots to 0
        var pivot = Dots(
            "[{\"g\":\"u\",\"k\":\"s\",\"v\":2},{\"g\":\"u\",\"k\":\"t\",\"v\":4},{\"g\":\"w\",\"k\":\"s\",\"v\":6}]",
            "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"]}]",
            "\"x\":{\"field\":\"s\"," + Measure + "},\"y\":{\"field\":\"t\"," + Measure + "}");
        var lookup = Dots(
            "[{\"id\":1,\"v\":2},{\"id\":2,\"v\":5}]",
            "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":[{\"k\":1,\"w\":8},{\"k\":2,\"w\":3},{\"k\":1,\"w\":9}]},\"key\":\"k\",\"fields\":[\"w\"]}}]",
            "\"x\":{\"field\":\"v\"," + Measure + "},\"y\":{\"field\":\"w\"," + Measure + "}");

        // Act
        using var pivoted = RenderDots(pivot);
        using var looked = RenderDots(lookup);

        // Assert: (2, 4) -> (20, 60), and (6, 0) -> (60, 100), of which the upper half is inside the chart
        Assert.Equal(Blue, pivoted[20, 60]);
        Assert.Equal(Blue, pivoted[60, 94]);
        Assert.Equal(VegaLiteTestHelpers.White, pivoted[60, 60]);

        // Assert: the first match wins, so (2, 8) -> (20, 20) and (5, 3) -> (50, 70); the later row for k = 1 (w = 9) is not used
        Assert.Equal(Blue, looked[20, 20]);
        Assert.Equal(Blue, looked[50, 70]);
        Assert.Equal(VegaLiteTestHelpers.White, looked[20, 10]);
    }

    /// <summary>
    ///     Validates the deterministic sample (the dots of the rows 2, 3, 6 and 9 for the pointer of the first transform), and
    ///     the stable sort of a discrete channel by an aggregate of a field (ascending and descending).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Transforms_SampleAndSortAreDeterministic()
    {
        // Arrange: ten rows (i, i) on the diagonal and a sample of four, which the hand-computed reservoir puts at 2, 3, 6 and 9
        var rows = "[" + string.Join(",", Enumerable.Range(0, 10).Select(i => "{\"i\":" + i + "}")) + "]";
        var sample = Dots(rows, "[{\"sample\":4}]", "\"x\":{\"field\":\"i\"," + Measure + "},\"y\":{\"field\":\"i\"," + Measure + "}");

        // Arrange: the categories a (2 + 3), b (10) and c (1) of a point scale of three positions (16.67, 50 and 83.33 pixels), each
        // in its palette color (a blue, b orange, c red), sorted by the sum of w
        const string Weighted = "[{\"k\":\"a\",\"w\":2},{\"k\":\"b\",\"w\":10},{\"k\":\"a\",\"w\":3},{\"k\":\"c\",\"w\":1}]";
        string Sorted(string order) => Dots(
            Weighted,
            "[]",
            "\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"axis\":null,\"sort\":{\"op\":\"sum\",\"field\":\"w\"" + order + "}},\"y\":{\"value\":50},"
            + "\"color\":{\"field\":\"k\",\"type\":\"nominal\",\"legend\":null}");

        // Act
        using var sampled = RenderDots(sample);
        using var again = RenderDots(sample);
        using var ascending = RenderDots(Sorted(string.Empty));
        using var descending = RenderDots(Sorted(",\"order\":\"descending\""));

        // Assert: the dots (i, i) of the sample are at (10 i, 100 - 10 i) and the others are not drawn
        foreach (var i in new[] { 2, 3, 6, 9 })
        {
            Assert.Equal(Blue, sampled[10 * i, 100 - (10 * i)]);
        }

        foreach (var i in new[] { 1, 4, 5, 7, 8 })
        {
            Assert.Equal(VegaLiteTestHelpers.White, sampled[10 * i, 100 - (10 * i)]);
        }

        Assert.Equal(VegaLiteTestHelpers.Snapshot(sampled), VegaLiteTestHelpers.Snapshot(again));

        // Assert: ascending by the sums 1, 5 and 10 puts c, a, b from left to right; descending reverses them
        Assert.Equal(Red3, ascending[17, 50]);
        Assert.Equal(Blue, ascending[50, 50]);
        Assert.Equal(Orange2, ascending[83, 50]);
        Assert.Equal(Orange2, descending[17, 50]);
        Assert.Equal(Blue, descending[50, 50]);
        Assert.Equal(Red3, descending[83, 50]);
    }

    /// <summary>
    ///     Validates the static parameters through the public API: the default value, a parameter that is an expression of
    ///     another, the override of the caller (of a value and of an expression), and a filter on a variable parameter.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Parameters_DefaultsOverridesAndFilterParam()
    {
        // Arrange: x = 1 to 5 with y = x; the filter keeps x greater than cut + 1 (cut is 3 by default, so x = 5)
        const string Params = "\"params\":[{\"name\":\"limit\",\"expr\":\"cut + 1\"},{\"name\":\"cut\",\"value\":3},{\"name\":\"show\",\"value\":true}],";
        var rows = "[{\"x\":1},{\"x\":2},{\"x\":3},{\"x\":4},{\"x\":5}]";
        var encoding = "\"x\":{\"field\":\"x\"," + Measure + "},\"y\":{\"field\":\"x\"," + Measure + "}";
        var json = Dots(rows, "[{\"filter\":\"datum.x > limit\"},{\"filter\":{\"param\":\"show\"}}]", encoding, extra: Params);
        var document = VegaLiteDocument.Parse(json);

        // Act
        using var byDefault = document.Render(new VegaLiteRenderOptions(), Ct);
        using var lowered = document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["cut"] = 1 } }, Ct);
        using var replaced = document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["limit"] = 3 } }, Ct);
        using var hidden = document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["show"] = false } }, Ct);

        // Assert: by default only x = 5 is drawn, at (50, 50)
        Assert.Equal(Blue, byDefault[50, 50]);
        Assert.Equal(VegaLiteTestHelpers.White, byDefault[40, 60]);

        // Assert: cut = 1 makes the limit 2, so x = 3, 4 and 5 are drawn; limit = 3 replaces the expression, so x = 4 and 5
        Assert.Equal(Blue, lowered[30, 70]);
        Assert.Equal(VegaLiteTestHelpers.White, lowered[20, 80]);
        Assert.Equal(Blue, replaced[40, 60]);
        Assert.Equal(VegaLiteTestHelpers.White, replaced[30, 70]);

        // Assert: show = false filters every row, so nothing but the white background remains inside the chart
        Assert.True(VegaLiteTestHelpers.RegionIs(hidden, 5, 5, 95, 95, VegaLiteTestHelpers.White));
        Assert.Equal(["limit", "cut", "show"], document.ParameterNames);
    }

    /// <summary>
    ///     Validates the row-bound expression references of the mark: the paint properties of the mark definition (fill and
    ///     opacity) and the values of the color and size channels are evaluated against the transformed row of each dot.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Expressions_RowBoundMarkValuesAreEvaluatedPerRow()
    {
        // Arrange: dots at (2, 2) and (5, 5); the fill is blue up to x = 3 and red above, half transparent for the first
        var rows = "[{\"x\":2},{\"x\":5}]";
        var encoding = "\"x\":{\"field\":\"x\"," + Measure + "},\"y\":{\"field\":\"x\"," + Measure + "}";
        var paint = Dots(
            rows,
            "[]",
            encoding,
            "{\"type\":\"point\",\"filled\":true,\"size\":200,\"fill\":{\"expr\":\"datum.x > 3 ? 'red' : 'blue'\"},\"opacity\":{\"expr\":\"datum.x > 3 ? 1 : 0.5\"}}");

        // Arrange: the same rows with the color and the size given by channel values; the size is 100 x, so radius 5.6 and 12.6
        var channels = Dots(
            rows,
            "[{\"calculate\":\"datum.x * 100\",\"as\":\"area\"}]",
            encoding + ",\"color\":{\"value\":{\"expr\":\"datum.x > 3 ? '#00ff00' : '#0000ff'\"}},\"size\":{\"value\":{\"expr\":\"datum.area\"}}");

        // Act
        using var painted = RenderDots(paint);
        using var valued = RenderDots(channels, null);

        // Assert: blue at half opacity over white is (127.5, 127.5, 255); red is opaque
        AssertPixel(painted, 20, 80, 127.5, 127.5, 255, 2);
        Assert.Equal(new Rgba32(255, 0, 0, 255), painted[50, 50]);

        // Assert: the channel values give blue and green, and the areas 200 and 500: the circle at (20, 80) ends before 10 pixels,
        // and the one at (50, 50) still covers a pixel 10 pixels from its center
        Assert.Equal(new Rgba32(0, 0, 255, 255), valued[20, 80]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), valued[50, 50]);
        Assert.Equal(VegaLiteTestHelpers.White, valued[30, 80]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), valued[60, 50]);
    }

    /// <summary>
    ///     Validates that a continuous color and a shape channel are drawn through the whole pipeline without a report, in strict mode:
    ///     the ramp color of each dot and the symbol of each category are the pixels at the derived positions.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Colors_RampColorAndShapeChannelsAreDrawnInStrictMode()
    {
        // Arrange: dots at (2, 2) and (5, 5) with the values 0 and 10 on a black to red ramp; the categories a and b are a circle and a square
        const string rows = "[{\"x\":2,\"v\":0,\"g\":\"a\"},{\"x\":5,\"v\":10,\"g\":\"b\"}]";
        var encoding = "\"x\":{\"field\":\"x\"," + Measure + "},\"y\":{\"field\":\"x\"," + Measure + "},"
            + "\"color\":{\"field\":\"v\",\"type\":\"quantitative\",\"legend\":null,\"scale\":{\"range\":[\"#000000\",\"#ff0000\"]}},"
            + "\"shape\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}";
        var reported = new List<string>();
        var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = u => reported.Add(u.Feature) };

        // Act
        using var surface = RenderDots(Dots(rows, "[]", encoding), strict);

        // Assert: nothing was reported, the first dot is black (the start of the ramp) and the second red (its end)
        Assert.Empty(reported);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[20, 80]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 50]);

        // Assert: 6.5 pixels from the center along both axes is inside the square (half side 7.07) and outside the circle (radius 7.98)
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[56, 56]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[26, 86]);
    }

    /// <summary>
    ///     Validates the diagnostics of the Phase 7 channels: an unknown color scheme is a located specification error, a custom path shape
    ///     is reported once and drawn as a circle, and a scale type that the field cannot use is a warning that keeps the default scale.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Colors_DiagnosticsAreLocated()
    {
        // Arrange
        const string unknown = """
            { "data": { "values": [ { "a": 1 } ] }, "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative" },
                            "color": { "field": "a", "type": "quantitative", "legend": null, "scale": { "scheme": "no-such-scheme" } } } }
            """;
        const string custom = """
            { "data": { "values": [ { "a": 1 } ] }, "mark": { "type": "point", "shape": "M0,0L1,1Z" },
              "encoding": { "x": { "field": "a", "type": "quantitative", "scale": { "type": "band" } } } }
            """;
        var unsupported = new List<string>();
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            UnsupportedFeatureHandler = u => unsupported.Add(u.Feature + " " + u.Location.Pointer),
            WarningHandler = w => warnings.Add(w.Message[..w.Message.IndexOf(':', StringComparison.Ordinal)] + " " + w.Location.Pointer),
        };

        // Act
        var error = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(unknown).Render(null, Ct));
        using var surface = VegaLiteDocument.Parse(custom).Render(options, Ct);

        // Assert: the scheme error points at the scheme; the path shape and the ignored scale type are reported at their values
        Assert.Equal("/encoding/color/scale/scheme", error.Location.Pointer);
        Assert.Equal(["vl-shape-path /mark/shape"], unsupported);
        Assert.Equal(["vl-scale-type-ignored /encoding/x/scale/type"], warnings);
    }

    /// <summary>
    ///     Validates the diagnostics of expressions: an unsupported function is reported at the expression text, a regular
    ///     expression result is a runtime warning delivered after the reports of the specification, and strict mode stops at the
    ///     first report with nothing after it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Diagnostics_ExpressionReportsAndStrictMode()
    {
        // Arrange: line 3 calls a function of a later phase, line 4 stores a regular expression
        const string Json = """
            { "data": { "values": [ { "a": 1 } ] },
              "transform": [ { "calculate": "geoArea(1)", "as": "d" },
                             { "calculate": "regexp('a')", "as": "r" } ],
              "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }
            """;
        var document = VegaLiteDocument.Parse(Json);
        var log = new List<string>();
        var lenient = new VegaLiteRenderOptions
        {
            WarningHandler = w => log.Add($"W {w.Message[..w.Message.IndexOf(':', StringComparison.Ordinal)]} {w.Location.Pointer} {w.Location.Line}:{w.Location.Column}"),
            UnsupportedFeatureHandler = u => log.Add($"U {u.Feature} {u.Location.Pointer} {u.Location.Line}:{u.Location.Column}"),
        };

        // Act
        using var surface = document.Render(lenient, Ct);
        var lenientLog = log.ToList();
        log.Clear();
        var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = lenient.WarningHandler, UnsupportedFeatureHandler = lenient.UnsupportedFeatureHandler };
        var ex = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(strict, Ct));

        // Assert: the chart renders without the unsupported call; the report is at the string value of the expression, and the
        // warning of the data stage follows it
        Assert.Equal(
            ["U vl-expr-function-geoarea /transform/0/calculate 2:33", "W vl-expr-runtime /transform/1/calculate 3:33"],
            lenientLog);

        // Assert: strict mode throws for the first report, delivers only it, and the warning that comes later is never delivered
        Assert.Equal(("vl-expr-function-geoarea", "/transform/0/calculate"), (ex.Feature, ex.Location.Pointer));
        Assert.Equal(["U vl-expr-function-geoarea /transform/0/calculate 2:33"], log);
    }

    /// <summary>
    ///     Validates that the features that Phase 5 does not support are still reported and never silently executed:
    ///     conditional encodings, selection parameters, the transforms of later phases and the date and format functions.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Deferred_FeaturesAreStillReported()
    {
        // Arrange
        const string Json = """
            { "params": [ { "name": "sel", "select": "interval" } ],
              "data": { "values": [ { "a": 1, "d": "2020-01-01" } ] },
              "transform": [
                { "regression": "a", "on": "d" },
                { "loess": "a", "on": "d" },
                { "density": "a" },
                { "quantile": "a" },
                { "timeUnit": "year", "field": "d", "as": "y" },
                { "calculate": "now() + format(1, ',')", "as": "t" }
              ],
              "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative", "condition": { "param": "sel", "value": 1 } } } }
            """;
        var reported = new List<string>();
        var options = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => reported.Add(u.Feature) };

        // Act
        using var surface = VegaLiteDocument.Parse(Json).Render(options, Ct);

        // Assert: each one is reported once, in document order, and the chart is still drawn
        Assert.Equal(
            [
                "vl-param-selection", "vl-transform-regression", "vl-transform-loess", "vl-transform-density", "vl-transform-quantile",
                "vl-transform-timeunit", "vl-expr-function-now", "vl-expr-function-format", "vl-encoding-condition",
            ],
            reported);
        Assert.True(surface.Width > 0);
    }
}
