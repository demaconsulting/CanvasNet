using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Guides;

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
        ["bar-inline.vl.json", "point-inline.vl.json", "rule-tick-inline.vl.json", "titles-config.vl.json", "unsupported.vl.json"];

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
        // Arrange: scatter plot. The data rectangle is 300 by 200; the y axis is 0 to 100 (widest label "100"), the title sits above
        // the top label overhang as in the bar chart, so the origin is the same formula. x runs 6 to 20 (300 / 14 per unit).
        var ox = (int)(5 + Math.Ceiling(7 + LabelWidth("100") + 4 + 11));
        const int oy = 27;
        using var scatter = OpenFixture("point-inline.vl.json").Render(cancellationToken: Ct);

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
    ///     Validates that an unsupported mark, transform, expression and aggregate are each reported once with the pointer, line and
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
                ("vl-transform-calculate", "/transform/0", 12, 5),
                ("vl-expr-calculate", "/transform/0/calculate", 12, 20),
                ("vl-mark-line", "/mark", 14, 11),
                ("vl-encoding-aggregate", "/encoding/y/aggregate", 17, 69),
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
        var big = VegaLiteDocument.Parse("{\"mark\":\"point\",\"data\":{\"values\":[" + rows + "]},\"encoding\":{\"x\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"k\",\"type\":\"nominal\"}}}");
        var reported = new List<string>();
        var options = new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 100 }, UnsupportedFeatureHandler = u => reported.Add(u.Feature) };

        // Act
        var format = Assert.Throws<VegaLiteSpecException>(() => invalidAxis.Render(cancellationToken: Ct));
        var items = Assert.Throws<VegaLiteRenderException>(() => big.Render(options, Ct));

        // Assert: the format error points at the format value; the budget error at the mark; the legend report was still delivered
        Assert.Equal("/encoding/x/axis/format", format.Location.Pointer);
        Assert.Equal(2, format.Location.Line);
        Assert.Equal("/mark", items.Location.Pointer);
        Assert.Contains("MaxSceneItems", items.Message, StringComparison.Ordinal);
        Assert.Contains("vl-guide-legend", reported);
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
}
