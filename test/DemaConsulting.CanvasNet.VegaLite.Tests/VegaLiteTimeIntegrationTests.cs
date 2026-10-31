using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of time (Phase 8): temporal fields on time scales with d3 time ticks and Vega's multi-format labels, bars over the months of a
///     time unit, the <see cref="VegaLiteRenderOptions.TimeZone"/> option (local midnights, a day that is not 24 hours), custom and implicit date parsing, filters by time unit and
///     date-time object, a temporal color legend, ordinal time units, the strict mode, the limits, the cancellation and the determinism. The charts are the original fixtures
///     (<c>time-*.vl.json</c>) and small inline specifications, rendered through the public API; every number is derived by hand from the calendar of 2024 (a leap year whose
///     January 1 is a Monday) and from the layout of the chart. No expectation is taken from a real Vega output.
/// </summary>
public class VegaLiteTimeIntegrationTests
{
    /// <summary>The pixels of a day on the 300 pixels of the 91 days from January 1 to April 1, 2024.</summary>
    private const double PerDay = 300.0 / 91;

    /// <summary>The chart of three points on one date each, whose x axis has a tick every two days; it is a template for the inline charts below.</summary>
    private const string TemplateChart = """
        { "width": WIDTH, "height": 100,
          "data": { "values": [ ROWS ] },
          "mark": "point",
          "encoding": { "x": { "field": "d", "type": "temporal"EXTRA_X }, "y": { "field": "v", "type": "quantitative" } } }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Opens a fixture.
    /// </summary>
    /// <param name="name">The file name in the <c>Fixtures</c> folder.</param>
    /// <returns>The document.</returns>
    private static VegaLiteDocument Open(string name) => VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

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
    ///     Gets the texts of the items of a role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. scene.FindAll(role).Select(e => ((SceneText)e.Item).Text)];

    /// <summary>
    ///     Gets the x positions of the symbols of a mark role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role, <c>mark-point</c> or <c>mark-circle</c>.</param>
    /// <returns>The positions relative to the data rectangle.</returns>
    private static List<double> Xs(Scene scene, string role) => [.. scene.FindAll(role).Select(e => ((SceneSymbol)e.Item).X)];

    /// <summary>
    ///     Builds an inline chart of three points.
    /// </summary>
    /// <param name="dates">The date texts of the three rows (the values 1, 2 and 3 are added).</param>
    /// <param name="width">The width of the data rectangle.</param>
    /// <param name="xExtra">Members of the x definition, each starting with a comma, or an empty text.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string[] dates, int width, string xExtra = "") =>
        TemplateChart
            .Replace("WIDTH", width.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("ROWS", string.Join(", ", dates.Select((d, i) => "{ \"d\": \"" + d + "\", \"v\": " + (i + 1) + " }")), StringComparison.Ordinal)
            .Replace("EXTRA_X", xExtra, StringComparison.Ordinal);

    /// <summary>
    ///     Asserts that two colors differ by at most two in every channel and the second is opaque.
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="actual">The painted color.</param>
    private static void AssertNear(Rgba32 expected, Rgba32 actual) =>
        Assert.True(Math.Abs(actual.R - expected.R) <= 2 && Math.Abs(actual.G - expected.G) <= 2 && Math.Abs(actual.B - expected.B) <= 2 && actual.A == 255, $"expected {expected} but the pixel is {actual}");

    /// <summary>
    ///     Validates the points fixture: three points sit at their dates on a time axis whose ticks are every two days (January 1, 3, ... 11 over 300 pixels: 60 pixels apart),
    ///     labeled by Vega's multi-format (the year for the year start, <c>Jan 07</c> for the Sunday that starts a week, weekday and date for the others), with a grid line
    ///     at each tick, in strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_PointsSitAtTheirDatesOnAMultiFormatAxis()
    {
        // Arrange
        var document = Open("time-points.vl.json");
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) };
        var scene = document.BuildScene(options, Ct);
        var view = ViewOf(scene);

        // Act
        using var surface = document.Render(options, Ct);

        // Assert: nothing is reported; the points are at day 0, 5 and 10 of 10 (0, 150 and 300 pixels)
        Assert.Empty(warnings);
        Assert.Equal([0.0, 150, 300], Xs(scene, "mark-point").Select(x => Math.Round(x, 6)));

        // Assert: the x axis labels (the y axis has one-digit labels): Jan 1 is a Monday and a year start, Jan 7 a Sunday
        Assert.Equal(["2024", "Wed 03", "Fri 05", "Jan 07", "Tue 09", "Thu 11"], Texts(scene, "axis-label").Where(t => t.Length >= 4));
        Assert.Contains("day", Texts(scene, "axis-title"));

        // Assert: the grid line of the second tick (January 3, 60 pixels in) is painted in the light gray of a grid (#ddd), well above the points
        AssertNear(new Rgba32(0xdd, 0xdd, 0xdd, 255), surface[(int)Math.Floor(view.X + 60.5), (int)Math.Round(view.Y + 60)]);
    }

    /// <summary>
    ///     Validates the bars fixture: the counts of visits per month (2, 3 and 1) are bars that span their months on a scale of 91 days over 300 pixels (a pixel of spacing taken
    ///     off every pair of edges, half a pixel of axis translate), the axis names the month starts, the title says what the x field is, and the pixels are the color of a bar.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_YearMonthBarsSpanTheirMonthsAndLabelTheMonths()
    {
        // Arrange
        var document = Open("time-bars.vl.json");
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };
        var scene = document.BuildScene(options, Ct);
        var view = ViewOf(scene);
        var bars = scene.FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Act
        using var surface = document.Render(options, Ct);

        // Assert: January is 31 days, February 29 and March 31; the counts 2, 3 and 1 on the y domain 0 to 3 (50 pixels a visit) are the heights 100, 150 and 50
        Assert.Equal(3, bars.Count);
        Assert.Equal([1.0, (31 * PerDay) + 1, (60 * PerDay) + 1], bars.Select(b => b.X), new ToleranceComparer());
        Assert.Equal([(31 * PerDay) - 1, (29 * PerDay) - 1, (31 * PerDay) - 1], bars.Select(b => b.Width), new ToleranceComparer());
        Assert.Equal([100.0, 150, 50], bars.Select(b => b.Height), new ToleranceComparer());
        Assert.All(bars, b => Assert.Equal(150, b.Y + b.Height, 6));

        // Assert: the axis has the four month starts of the domain, whose title follows Vega-Lite's rule for a time unit
        Assert.Equal(["Jan 2024", "Feb 2024", "Mar 2024", "Apr 2024"], Texts(scene, "axis-label").Where(t => t.Contains("2024", StringComparison.Ordinal)));
        Assert.Contains("visit (year-month)", Texts(scene, "axis-title"));

        // Assert: the middle of the February bar, near its foot, is the color of a bar (#4c78a8)
        AssertNear(new Rgba32(0x4c, 0x78, 0xa8, 255), surface[(int)Math.Round(view.X + 150), (int)Math.Round(view.Y + 140)]);
    }

    /// <summary>
    ///     Validates the time zone option: a point at 2024-03-05T20:00Z is on March 6 for a chart in a zone 5:30 hours ahead (local 01:30) and on March 5 in UTC, so its pixel
    ///     column differs by exactly one day of the scale (400 pixels over 10 days: 40 pixels); the ticks are the same days and the label text is the same.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_TimeZoneOptionShiftsTimeUnitBoundaries()
    {
        // Arrange: the days start at the local midnights; the domain is March 1 to March 11 in both zones (10 days), the middle point is on day 4 (UTC) or day 5 (local)
        var document = Open("time-zone.vl.json");

        // Act
        var utc = document.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
        var local = document.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, TimeZone = TimeTestSupport.Fixed(5, 30) }, Ct);

        // Assert
        Assert.Equal([0.0, 160, 400], Xs(utc, "mark-point").Select(x => Math.Round(x, 6)));
        Assert.Equal([0.0, 200, 400], Xs(local, "mark-point").Select(x => Math.Round(x, 6)));
        Assert.Equal(Texts(utc, "axis-label").Where(t => t.Contains("2024", StringComparison.Ordinal)), Texts(local, "axis-label").Where(t => t.Contains("2024", StringComparison.Ordinal)));

        // Assert: the ticks are the odd days of the month (every second day boundary); the 12 character labels are 80 pixels apart, but the parity overlap method drops the
        // labels that touch, so only the order and the text of the labels that remain are pinned: the first tick (the start of the domain) is always there
        var labels = Texts(local, "axis-label").Where(t => t.Contains("2024", StringComparison.Ordinal)).ToList();
        string[] ticks = ["Mar 01, 2024", "Mar 03, 2024", "Mar 05, 2024", "Mar 07, 2024", "Mar 09, 2024", "Mar 11, 2024"];
        Assert.Equal(ticks[0], labels[0]);
        Assert.All(labels, l => Assert.Contains(l, ticks));
        Assert.Equal(labels.Select(l => Array.IndexOf(ticks, l)).Order(), labels.Select(l => Array.IndexOf(ticks, l)));
    }

    /// <summary>
    ///     Validates that a day is not always 24 hours: in a zone that follows the United States rule (clocks go forward at 02:00 on March 10, 2024), March 10 has 23 hours,
    ///     so with the days March 9, 10 and 11 across 470 pixels (47 hours: 10 pixels an hour) the middle day starts 240 pixels in, not 235 as in UTC.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_DstDayIsNot24Hours()
    {
        // Arrange: the local midnights are 05:00Z on March 9 (standard time), 05:00Z on March 10 (the start of the short day) and 04:00Z on March 11 (daylight time); the points are
        // in the afternoon of each day so that the unit decides their position
        var spec = Chart(["2024-03-09T17:00:00Z", "2024-03-10T17:00:00Z", "2024-03-11T17:00:00Z"], 470, ", \"timeUnit\": \"yearmonthdate\", \"axis\": { \"tickCount\": \"day\" }");
        var document = VegaLiteDocument.Parse(spec);

        // Act
        var dst = document.BuildScene(new VegaLiteRenderOptions { TimeZone = TimeTestSupport.UsLike() }, Ct);
        var utc = document.BuildScene(new VegaLiteRenderOptions(), Ct);

        // Assert: 05:00Z to 05:00Z is 24 hours (240 pixels), then 05:00Z to 04:00Z is 23 hours (230 pixels)
        Assert.Equal([0.0, 240, 470], Xs(dst, "mark-point").Select(x => Math.Round(x, 6)));
        Assert.Equal([0.0, 235, 470], Xs(utc, "mark-point").Select(x => Math.Round(x, 6)));

        // Assert: the labels of the days March 9, 10 and 11 have 12 characters; they are 235 pixels apart, so none is dropped
        Assert.Equal(["Mar 09, 2024", "Mar 10, 2024", "Mar 11, 2024"], Texts(dst, "axis-label").Where(t => t.Contains("2024", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Validates that a UTC time unit ignores the zone option: the chart in a zone is the chart in UTC.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_UtcScaleIgnoresTheZoneOption()
    {
        // Arrange
        var spec = Chart(["2024-03-01T12:00:00Z", "2024-03-05T20:00:00Z", "2024-03-11T12:00:00Z"], 400, ", \"timeUnit\": \"utcyearmonthdate\"");
        var document = VegaLiteDocument.Parse(spec);

        // Act
        var zoned = document.BuildScene(new VegaLiteRenderOptions { TimeZone = TimeTestSupport.Fixed(5, 30) }, Ct);
        var plain = document.BuildScene(new VegaLiteRenderOptions(), Ct);

        // Assert: the UTC dates are March 1, 5 and 11: 0, 160 and 400 pixels, in both
        Assert.Equal([0.0, 160, 400], Xs(plain, "mark-point").Select(x => Math.Round(x, 6)));
        Assert.Equal(Xs(plain, "mark-point"), Xs(zoned, "mark-point"));
        Assert.Equal(Texts(plain, "axis-label"), Texts(zoned, "axis-label"));
    }

    /// <summary>
    ///     Validates the <c>date:'%d/%m/%Y'</c> parse directive: the days of the CSV (5, 15 and 25 March 2024) are 10 days apart (0, 100 and 200 pixels); without the directive
    ///     the day first dates are not valid month first dates ("15/03/2024" is the fifteenth month), so they do not draw and the load is reported.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_CustomDateParseDirectiveReadsDayMonthYear()
    {
        // Arrange: the same chart without the parse directive
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "time-parse.vl.json");
        var withoutParse = File.ReadAllText(path).Replace("\"parse\": { \"visit\": \"date:'%d/%m/%Y'\", \"tickets\": \"number\" }", "\"parse\": { \"tickets\": \"number\" }", StringComparison.Ordinal);
        Assert.NotEqual(File.ReadAllText(path), withoutParse);
        var warnings = new List<string>();
        var plain = VegaLiteDocument.Open(path);
        var broken = VegaLiteDocument.Parse(withoutParse, new VegaLiteOpenOptions { FileResolver = name => File.OpenRead(Path.Combine(Path.GetDirectoryName(path)!, name)) });

        // Act
        var scene = plain.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) }, Ct);
        var brokenScene = broken.BuildScene(new VegaLiteRenderOptions(), Ct);
        using var rendered = broken.Render(new VegaLiteRenderOptions { WarningHandler = w => warnings.Add(w.Message) }, Ct);

        // Assert
        Assert.Equal([0.0, 100, 200], Xs(scene, "mark-point").Select(x => Math.Round(x, 6)));
        Assert.Single(Xs(brokenScene, "mark-point"));
        Assert.Contains(warnings, w => w.Contains("could not be read as", StringComparison.Ordinal) && w.Contains("'visit'", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates the implicit parse of the text dates of a temporal field: dates such as "March 5, 2024" are read like <c>Date.parse</c> without a <c>parse</c> directive.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_ImplicitParseOfTemporalStrings()
    {
        // Arrange: ten days apart
        var spec = Chart(["March 5, 2024", "March 15, 2024", "March 25, 2024"], 200);

        // Act
        var scene = VegaLiteDocument.Parse(spec).BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);

        // Assert
        Assert.Equal([0.0, 100, 200], Xs(scene, "mark-point").Select(x => Math.Round(x, 6)));
    }

    /// <summary>
    ///     Validates a filter by a time unit and by a range of date-time objects: only the points in February and March 2024 remain (March 30 is at the right end), 5 pixels a day
    ///     on 54 days.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_FilterByTimeUnitAndDatetimeObject()
    {
        // Arrange: of the eight rows, the months February or March of the year 2024 are rows 3, 4, 5 and 7 (the 5th and 25th of February, the 15th and 30th of March)
        var document = Open("time-filter.vl.json");

        // Act
        var scene = document.BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);

        // Assert: from February 5 to March 30 there are 24 + 30 = 54 days over 270 pixels: February 25 is day 20, March 15 day 39
        Assert.Equal([0.0, 100, 195, 270], Xs(scene, "mark-point").Select(x => Math.Round(x, 6)));
    }

    /// <summary>
    ///     Validates the legend of a temporal color: a gradient legend titled with the field whose labels are the two-day ticks of the domain in the format of
    ///     <c>config.timeFormat</c>, whose ramp is sampled at time ticks, and whose first and last stops are the colors of the first and the last date.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_TemporalColorLegendShowsDates()
    {
        // Arrange
        var document = Open("time-legend.vl.json");
        var warnings = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) };

        // Act
        var scene = document.BuildScene(options, Ct);
        using var surface = document.Render(options, Ct);

        // Assert: nothing is reported; one gradient legend titled with the field and labeled with the dates
        Assert.Empty(warnings);
        Assert.Equal(["sampled"], Texts(scene, "legend-title"));
        Assert.Equal(["Jan 01, 2024", "Jan 03, 2024", "Jan 05, 2024", "Jan 07, 2024", "Jan 09, 2024", "Jan 11, 2024"], Texts(scene, "legend-label"));
        var gradient = Assert.IsType<SceneRect>(Assert.Single(scene.FindAll("legend-gradient")).Item);
        Assert.NotNull(gradient.FillGradient);
        Assert.Equal(21, gradient.FillGradient.Stops.Count);

        // Assert: the circles of the first and the last date have the colors of the ends of the ramp, and the middle date a color in between
        var circles = scene.FindAll("mark-circle").Select(e => (SceneSymbol)e.Item).ToList();
        Assert.Equal(3, circles.Count);
        Assert.Equal(gradient.FillGradient.Stops[0].Color, circles[0].Fill);
        Assert.Equal(gradient.FillGradient.Stops[^1].Color, circles[2].Fill);
        Assert.Equal(3, circles.Select(c => c.Fill).Distinct().Count());
    }

    /// <summary>
    ///     Validates an ordinal field with a time unit: the months are three bands of a band scale (a band of 90 pixels in a step of 100) labeled by the unit, not turned.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_OrdinalTimeUnitUsesABandScale()
    {
        // Arrange
        const string Spec = """
            { "width": 300, "height": 100,
              "data": { "values": [ { "d": "2024-01-15" }, { "d": "2024-02-10" }, { "d": "2024-02-20" }, { "d": "2024-03-20" } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "d", "type": "ordinal", "timeUnit": "month" }, "y": { "aggregate": "count" } } }
            """;

        // Act
        var scene = VegaLiteDocument.Parse(Spec).BuildScene(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);

        // Assert: the bands start at 5, 105 and 205 (step 100 with the bar padding); February has two rows, so its bar is twice as high
        var bars = scene.FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();
        Assert.Equal([5.0, 105, 205], bars.Select(b => b.X));
        Assert.All(bars, b => Assert.Equal(90, b.Width, 6));
        Assert.Equal(bars[0].Height * 2, bars[1].Height, 6);
        Assert.Equal(["Jan", "Feb", "Mar"], Texts(scene, "axis-label").Where(t => t.Length == 3 && char.IsLetter(t[0])));
        Assert.All(scene.FindAll("axis-label").Where(e => ((SceneText)e.Item).Text is "Jan" or "Feb" or "Mar"), e => Assert.Equal(0, ((SceneText)e.Item).Angle, 9));
    }

    /// <summary>
    ///     Validates the strict mode: temporal fields, time units and date parse directives are supported and not reported, while a binned time unit and a band position other than
    ///     one half on a bar with a time unit are reported with their ids and places.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_StrictModeReportsBinnedTimeUnitAndRectBandPosition()
    {
        // Arrange
        var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };
        var binned = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "time-bars.vl.json")).Replace("\"timeUnit\": \"yearmonth\"", "\"timeUnit\": \"binnedyearmonth\"", StringComparison.Ordinal);
        var rect = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "time-bars.vl.json")).Replace("\"mark\": \"bar\"", "\"mark\": { \"type\": \"bar\", \"timeUnitBandPosition\": 0.2 }", StringComparison.Ordinal);
        Assert.Contains("binnedyearmonth", binned, StringComparison.Ordinal);
        Assert.Contains("timeUnitBandPosition", rect, StringComparison.Ordinal);

        // Act: the supported fixtures render without a report
        using var bars = Open("time-bars.vl.json").Render(strict, Ct);
        using var parsed = Open("time-parse.vl.json").Render(strict, Ct);
        var binnedError = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => VegaLiteDocument.Parse(binned).Render(strict, Ct));
        var rectError = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => VegaLiteDocument.Parse(rect).Render(strict, Ct));

        // Assert
        Assert.Equal("vl-timeunit-binned", binnedError.Feature);
        Assert.True(binnedError.Location.Line > 0);
        Assert.Equal("vl-encoding-bandposition-rect", rectError.Feature);
        Assert.True(rectError.Location.Line > 0);
    }

    /// <summary>
    ///     Validates the limits of a time chart: more ticks than <c>MaxTicks</c> and a label longer than <c>MaxStringLength</c> are located render errors that name their limits,
    ///     and a limit that allows them renders; a canceled token stops the render.
    /// </summary>
    /// <returns>A task that completes when the checks are done.</returns>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_Time_Limits_TickCountAndStringLimitsAreEnforced()
    {
        // Arrange: the fixture has 6 ticks; "%c" labels have 21 characters, and 22 on the 10th and 11th ("1/11/2024, 12:00:00 AM")
        var document = Open("time-points.vl.json");
        var longLabels = (await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "time-points.vl.json"), Ct)).Replace("\"tickCount\": 5", "\"tickCount\": 5, \"format\": \"%c\"", StringComparison.Ordinal);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        // Act
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxTicks = 5 } }, Ct));
        using var exact = document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxTicks = 6 } }, Ct);
        var tooLong = Assert.Throws<VegaLiteRenderException>(() => VegaLiteDocument.Parse(longLabels).Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxStringLength = 21 } }, Ct));
        using var fits = VegaLiteDocument.Parse(longLabels).Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxStringLength = 22 } }, Ct);

        // Assert: located at the x channel of the chart, naming the limits
        Assert.Equal("/encoding/x", tooMany.Location.Pointer);
        Assert.Contains("MaxTicks", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/x", tooLong.Location.Pointer);
        Assert.Contains("the limit of 21", tooLong.Message, StringComparison.Ordinal);
        Assert.Equal(exact.Width, fits.Width);
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: canceled.Token));
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a chart in a zone are byte-identical.
    /// </summary>
    /// <returns>A task that completes when the concurrent renders are done.</returns>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_Time_Determinism_RepeatedAndConcurrentRendersAreByteIdentical()
    {
        // Arrange
        var document = Open("time-bars.vl.json");
        var options = new VegaLiteRenderOptions { TimeZone = TimeTestSupport.UsLike() };
        using var first = document.Render(options, Ct);
        var baseline = VegaLiteTestHelpers.Snapshot(first);

        // Act
        using var second = document.Render(options, Ct);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => VegaLiteTestHelpers.Snapshot(document.Render(options, Ct)), Ct)));

        // Assert
        Assert.Equal(baseline, VegaLiteTestHelpers.Snapshot(second));
        Assert.All(concurrent, snapshot => Assert.Equal(baseline, snapshot));
    }

    /// <summary>
    ///     Validates that the current culture does not change the output: Turkish (dotted capital I) and German (dots to group digits) render the same pixels as the invariant
    ///     culture, for charts that format dates in axes and legends.
    /// </summary>
    /// <param name="culture">The name of the culture.</param>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void CanvasNetVegaLite_SystemIntegration_Time_CurrentCultureDoesNotChangeTheOutput(string culture)
    {
        // Arrange
        var fixtures = new[] { "time-points.vl.json", "time-bars.vl.json", "time-legend.vl.json" };
        var documents = fixtures.Select(Open).ToList();

        // Act
        var baseline = ExpressionTestSupport.UnderCulture(string.Empty, () => documents.Select(d => VegaLiteTestHelpers.Snapshot(d.Render(cancellationToken: Ct))).ToList());
        var localized = ExpressionTestSupport.UnderCulture(culture, () => documents.Select(d => VegaLiteTestHelpers.Snapshot(d.Render(cancellationToken: Ct))).ToList());

        // Assert
        for (var i = 0; i < fixtures.Length; i++)
        {
            Assert.Equal(baseline[i], localized[i]);
        }
    }

    /// <summary>
    ///     Validates that the host's zone does not matter: a chart without a zone and one in a custom zone with a zero offset (UTC by another name) are byte-identical.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Time_HostZoneDoesNotMatter()
    {
        // Arrange
        var document = Open("time-zone.vl.json");
        var zero = TimeZoneInfo.CreateCustomTimeZone("Test/ZeroOffset", TimeSpan.Zero, "Zero", "Zero");

        // Act
        using var none = document.Render(new VegaLiteRenderOptions(), Ct);
        using var utc = document.Render(new VegaLiteRenderOptions { TimeZone = TimeZoneInfo.Utc }, Ct);
        using var custom = document.Render(new VegaLiteRenderOptions { TimeZone = zero }, Ct);

        // Assert
        var baseline = VegaLiteTestHelpers.Snapshot(none);
        Assert.Equal(baseline, VegaLiteTestHelpers.Snapshot(utc));
        Assert.Equal(baseline, VegaLiteTestHelpers.Snapshot(custom));
    }

    /// <summary>
    ///     Compares sequences of numbers with a tolerance of a millionth, for positions that are fractions of a day.
    /// </summary>
    private sealed class ToleranceComparer : IEqualityComparer<double>
    {
        /// <inheritdoc/>
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-6;

        /// <inheritdoc/>
        public int GetHashCode(double obj) => 0;
    }
}
