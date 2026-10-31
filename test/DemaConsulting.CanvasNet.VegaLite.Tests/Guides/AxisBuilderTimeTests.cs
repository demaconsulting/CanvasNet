using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the time axes of <see cref="AxisBuilder"/>: the d3 time ticks, the default multi-format labels, the labels of a time unit, the tick count rules
///     of Vega-Lite, explicit <c>values</c>, the zone of the chart, the limits and the determinism. Every expected tick and label is derived by hand from the
///     calendar (2024-01-01 is a Monday, 2024 is a leap year, a week starts on Sunday), the d3 interval table and Vega's multi-format, never recorded from the code.
/// </summary>
public class AxisBuilderTimeTests
{
    /// <summary>A scatter of two points ten days apart (January 1 and 11, 2024), whose x axis is the time axis under test.</summary>
    private const string TenDays = """
        { "data": { "values": [ { "d": "2024-01-01", "v": 1 }, { "d": "2024-01-11", "v": 2 } ] },
          "mark": "point",
          "encoding": {
            "x": { "field": "d", "type": "temporal"AXIS },
            "y": { "field": "v", "type": "quantitative" }
          } }
        """;

    /// <summary>A bar chart over three months of 2024 (the unit <c>yearmonth</c>): the domain is January 1 to April 1, 91 days.</summary>
    private const string MonthBars = """
        { "data": { "values": [ { "date": "2024-01-15" }, { "date": "2024-02-10" }, { "date": "2024-03-20" } ] },
          "mark": "bar",
          "encoding": {
            "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth"AXIS },
            "y": { "aggregate": "count" }
          } }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds the ten day specification with an <c>axis</c> object on x.
    /// </summary>
    /// <param name="axis">The members of the axis object, or an empty text for none.</param>
    /// <returns>The specification text.</returns>
    private static string Days(string axis = "") => TenDays.Replace("AXIS", axis.Length == 0 ? string.Empty : ", \"axis\": { " + axis + " }", StringComparison.Ordinal);

    /// <summary>
    ///     Builds the month bar specification with an <c>axis</c> object on x.
    /// </summary>
    /// <param name="axis">The members of the axis object, or an empty text for none.</param>
    /// <returns>The specification text.</returns>
    private static string Months(string axis = "") => MonthBars.Replace("AXIS", axis.Length == 0 ? string.Empty : ", \"axis\": { " + axis + " }", StringComparison.Ordinal);

    /// <summary>
    ///     Gets the instant of a UTC date and time.
    /// </summary>
    /// <param name="month">The month from 1.</param>
    /// <param name="day">The day of the month.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <returns>The milliseconds since the epoch.</returns>
    private static double Jan(int month, int day, int hour = 0, int minute = 0) => TimeTestSupport.Ms(2024, month, day, hour, minute);

    /// <summary>
    ///     Gets the milliseconds of the ticks of an axis.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <returns>The instants.</returns>
    private static List<double> Instants(AxisResult axis) => [.. axis.Ticks.Select(t => t.Value.IsDate ? t.Value.Milliseconds : t.Value.Number)];

    /// <summary>
    ///     Validates the default labels of a time axis: ten days over 400 pixels have a tick a day (11 ticks, the last one included), labeled by the coarsest boundary
    ///     they sit on (Vega's multi-format), with the grid, the flush end labels and the parity overlap method of a continuous axis.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_DefaultLabelsAreMultiFormat()
    {
        // Arrange: tick count ceil(400 / 40) = 10 and a span of 10 days: the target is 1 day, which is in the d3 table, so a tick every midnight from Jan 1 to Jan 11.
        // Jan 1 is a year (and month and week) start, so its label is the year; Jan 7 is a Sunday (a week start, not a month start): "Jan 07"; the others are weekdays.
        string[] expected = ["2024", "Tue 02", "Wed 03", "Thu 04", "Fri 05", "Sat 06", "Jan 07", "Mon 08", "Tue 09", "Wed 10", "Thu 11"];

        // Act
        var axis = ChartTestHelpers.Axis(Days(), "x", 400, 200)!;

        // Assert: eleven ticks, 40 pixels apart, at the midnights of the days
        Assert.Equal(expected, axis.Ticks.Select(t => t.Label));
        Assert.Equal(Enumerable.Range(0, 11).Select(i => Jan(1, 1 + i)), Instants(axis));
        Assert.Equal(Enumerable.Range(0, 11).Select(i => 40.0 * i), axis.Ticks.Select(t => t.Position));
        Assert.All(axis.Ticks, t => Assert.True(t.Value.IsDate));

        // Assert: a time axis has a grid, flush end labels and the title of the field
        Assert.NotNull(axis.Grid);
        Assert.Equal(11, ChartTestHelpers.Of<SceneRule>(axis.Grid, "axis-grid").Count);
        var labels = ChartTestHelpers.Of<SceneText>(axis.Group, "axis-label");
        Assert.Equal("left", labels[0].Align);
        Assert.Equal("right", labels[^1].Align);
        Assert.Equal(["d"], ChartTestHelpers.Of<SceneText>(axis.Group, "axis-title").Single().Lines);

        // Assert: labels that overlap are removed by parity (which labels fit depends on the measured text widths, so only the order and the text are pinned);
        // with the overlap method off every label is drawn
        Assert.All(labels, l => Assert.Contains(l.Text, expected));
        var order = labels.Select(l => Array.IndexOf(expected, l.Text)).ToList();
        Assert.Equal(order.Order(), order);
        Assert.True(labels.Count < expected.Length);
        var allLabels = ChartTestHelpers.Axis(Days("\"labelOverlap\": false"), "x", 400, 200)!;
        Assert.Equal(expected, ChartTestHelpers.Of<SceneText>(allLabels.Group, "axis-label").Select(l => l.Text));
    }

    /// <summary>
    ///     Validates that a field with the time unit <c>yearmonth</c> is labeled with the specifier of the unit, has its title and takes the tick count of Vega-Lite's
    ///     rules: 8 for 300 pixels, lowered to 3 by the minimum step of one month (31 days, so floor(91 / 31) + 1).
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_YearMonthLabelsUseTheUnitSpecifier()
    {
        // Arrange: the target 91 / 3 = 30.33 days is nearest to the month (30 days) of the d3 table, so the ticks are the month starts Jan 1 to Apr 1 (the end is included).
        // Positions on 300 pixels: 31, 60 and 91 days of 91 are 102.198 (rounded 102), 197.802 (198) and 300.

        // Act
        var axis = ChartTestHelpers.Axis(Months(), "x", 300, 200)!;

        // Assert
        Assert.Equal(["Jan 2024", "Feb 2024", "Mar 2024", "Apr 2024"], axis.Ticks.Select(t => t.Label));
        Assert.Equal([Jan(1, 1), Jan(2, 1), Jan(3, 1), Jan(4, 1)], Instants(axis));
        Assert.Equal([0.0, 102, 198, 300], axis.Ticks.Select(t => t.Position));
        Assert.Equal(["date (year-month)"], ChartTestHelpers.Of<SceneText>(axis.Group, "axis-title").Single().Lines);
    }

    /// <summary>
    ///     Validates the <c>format</c> of a time axis: a text is a d3 time format; an object gives the levels of the multi-format (a missing level keeps Vega's
    ///     default).
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_FormatStringAndObject()
    {
        // Act
        var text = ChartTestHelpers.Axis(Days("\"format\": \"%Y-%m-%d\""), "x", 400, 200)!;
        var levels = ChartTestHelpers.Axis(Days("\"format\": { \"date\": \"%d\", \"year\": \"Y%Y\" }"), "x", 400, 200)!;

        // Assert: every tick has its date; the object replaces the day and year levels, and Jan 7 (a week start) keeps the default "%b %d"
        Assert.Equal(Enumerable.Range(1, 11).Select(d => $"2024-01-{d:00}"), text.Ticks.Select(t => t.Label));
        Assert.Equal(["Y2024", "02", "03", "04", "05", "06", "Jan 07", "08", "09", "10", "11"], levels.Ticks.Select(t => t.Label));
    }

    /// <summary>
    ///     Validates the format types: <c>number</c> formats the milliseconds, and <c>time</c> or <c>utc</c> makes the numbers of a quantitative axis times.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_FormatTypeNumberAndTime()
    {
        // Arrange: a quantitative axis of 0 to 10,000 "milliseconds" has a tick every 1,000 (count 10 over 400 pixels), labeled by the seconds field of the instant. A
        // quantitative axis has no calendar of its own: "time" uses the zone of the chart (the epoch is 05:30 at +05:30) and "utc" uses UTC.
        const string Quantitative = """
            { "data": { "values": [ { "v": 0 }, { "v": 10000 } ] }, "mark": "point",
              "encoding": { "x": { "field": "v", "type": "quantitative", "axis": { "formatType": "time", "format": "%H:%M:%S" } } } }
            """;

        // Act
        var numbers = ChartTestHelpers.Axis(Days("\"format\": \",d\", \"formatType\": \"number\", \"tickCount\": { \"interval\": \"day\" }"), "x", 400, 200)!;
        var times = ChartTestHelpers.Axis(Quantitative, "x", 400, 200, zone: TimeTestSupport.Fixed(5, 30))!;
        var utc = ChartTestHelpers.Axis(Quantitative.Replace("\"time\"", "\"utc\"", StringComparison.Ordinal), "x", 400, 200, zone: TimeTestSupport.Fixed(5, 30))!;
        var plain = ChartTestHelpers.Axis(Quantitative, "x", 400, 200)!;

        // Assert: January 1, 2024 is day 19,723 = 1,704,067,200,000 ms; the number format groups the digits
        Assert.Equal("1,704,067,200,000", numbers.Ticks[0].Label);
        Assert.Equal("1,704,931,200,000", numbers.Ticks[^1].Label);
        Assert.Equal(Enumerable.Range(0, 11).Select(s => $"05:30:{s:00}"), times.Ticks.Select(t => t.Label));
        Assert.Equal(Enumerable.Range(0, 11).Select(s => $"00:00:{s:00}"), utc.Ticks.Select(t => t.Label));
        Assert.Equal(utc.Ticks.Select(t => t.Label), plain.Ticks.Select(t => t.Label));
    }

    /// <summary>
    ///     Validates the forms of <c>tickCount</c>: a number is the approximate count, an interval name places a tick at every boundary, and
    ///     <c>{interval, step}</c> at every nth boundary.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_TickCountNumberObjectAndInterval()
    {
        // Act
        var five = ChartTestHelpers.Axis(Days("\"tickCount\": 5"), "x", 400, 200)!;
        var week = ChartTestHelpers.Axis(Days("\"tickCount\": \"week\""), "x", 400, 200)!;
        var every5 = ChartTestHelpers.Axis(Days("\"tickCount\": { \"interval\": \"day\", \"step\": 5 }"), "x", 400, 200)!;
        var month = ChartTestHelpers.Axis(Days("\"tickCount\": \"month\""), "x", 400, 200)!;

        // Assert: a count of 5 over 10 days targets 2 days, which is the d3 entry of two days: the days of the month 1, 3, 5, 7, 9 and 11
        Assert.Equal(new[] { 1, 3, 5, 7, 9, 11 }.Select(d => Jan(1, d)), Instants(five));
        Assert.Equal([0.0, 80, 160, 240, 320, 400], five.Ticks.Select(t => t.Position));

        // Assert: the only Sunday between January 1 and 11 is the 7th (a week start that is not a month start)
        Assert.Equal([Jan(1, 7)], Instants(week));
        Assert.Equal(["Jan 07"], week.Ticks.Select(t => t.Label));
        Assert.Equal([240.0], week.Ticks.Select(t => t.Position));

        // Assert: every 5th day of the month boundary: the days 1, 6 and 11; the month interval has only the first of January
        Assert.Equal([Jan(1, 1), Jan(1, 6), Jan(1, 11)], Instants(every5));
        Assert.Equal(["2024", "Sat 06", "Thu 11"], every5.Ticks.Select(t => t.Label));
        Assert.Equal([Jan(1, 1)], Instants(month));
    }

    /// <summary>
    ///     Validates that Vega-Lite leaves the default tick count to Vega (ten) for a field whose unit is exactly <c>hours</c> (and a month, a day or a quarter), instead
    ///     of one tick for every 40 pixels.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_TickCountDefaultMonthUnitUsesVegaDefault10()
    {
        // Arrange: the hours 00:00 and 23:00 of one day over 800 pixels. Vega's ten ticks over 23 hours target 2.3 hours: the table has 1 hour and 3 hours, and 2.3 / 1 is
        // not below 3 / 2.3, so every 3 hours (0, 3, ..., 21: eight ticks). One tick for every 40 pixels (20) would target 1.15 hours and give every hour (24 ticks).
        const string Hours = """
            { "data": { "values": [ { "t": "2024-05-05T00:00:00Z" }, { "t": "2024-05-05T23:00:00Z" } ] }, "mark": "point",
              "encoding": { "x": { "field": "t", "type": "temporal", "timeUnit": "UNIT" } } }
            """;

        // Act
        var hours = ChartTestHelpers.Axis(Hours.Replace("UNIT", "hours", StringComparison.Ordinal), "x", 800, 200)!;
        var hoursMinutes = ChartTestHelpers.Axis(Hours.Replace("UNIT", "hoursminutes", StringComparison.Ordinal), "x", 800, 200)!;

        // Assert: the unit specifier of the hours is "%H:00"
        Assert.Equal(["00:00", "03:00", "06:00", "09:00", "12:00", "15:00", "18:00", "21:00"], hours.Ticks.Select(t => t.Label));

        // Assert: the minutes are part of the unit, so the default is one tick for every 40 pixels (20); the minimum step of a minute does not lower it, and the target of
        // 23 / 20 = 1.15 hours is nearer to the hour (1.15 / 1 is below 3 / 1.15) than to three hours: a tick every hour, 24 of them
        Assert.Equal(24, hoursMinutes.Ticks.Count);
    }

    /// <summary>
    ///     Validates that the minimum tick step comes from the time unit and can be replaced by <c>tickMinStep</c>.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_TickMinStepFromTheUnit()
    {
        // Act: a minimum step of one millisecond keeps the 8 ticks of the width: the target 91 / 8 = 11.4 days is nearest to the week (7 days of the table)
        var small = ChartTestHelpers.Axis(Months("\"tickMinStep\": 1"), "x", 300, 200)!;

        // Assert: the Sundays from January 7 to March 31 (13 of them), labeled with the unit specifier
        Assert.Equal(13, small.Ticks.Count);
        Assert.Equal(Jan(1, 7), Instants(small)[0]);
        Assert.Equal(Jan(3, 31), Instants(small)[^1]);
        Assert.All(small.Ticks, t => Assert.Matches("^[A-Z][a-z]{2} 2024$", t.Label));
    }

    /// <summary>
    ///     Validates that <c>values</c> take timestamps, date texts and date-time objects, drop what is not a date or lies outside the scale, and keep the order of the scale.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_ValuesAcceptDatetimeObjects()
    {
        // Act: January 10 as a timestamp, the 5th as text, the 3rd as an object, and an out-of-range date and a text that is not a date, which are dropped
        var axis = ChartTestHelpers.Axis(
            Days("\"values\": [ 1704844800000, \"2024-01-05\", { \"year\": 2024, \"month\": 1, \"date\": 3 }, \"2025-01-01\", \"nonsense\" ]"),
            "x",
            400,
            200)!;

        // Assert: in scale order (the 3rd, 5th and 10th), 40 pixels a day
        Assert.Equal([Jan(1, 3), Jan(1, 5), Jan(1, 10)], Instants(axis));
        Assert.Equal([80.0, 160, 360], axis.Ticks.Select(t => t.Position));
        Assert.Equal(["Wed 03", "Fri 05", "Wed 10"], axis.Ticks.Select(t => t.Label));
    }

    /// <summary>
    ///     Validates the defaults of a time axis: flush end labels on x only, the parity overlap method (dense labels are thinned, <c>labelOverlap: false</c> keeps them), and the
    ///     <c>axisTemporal</c> configuration.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_LabelFlushAndOverlapDefaults()
    {
        // Arrange: eleven ticks 10 pixels apart on 100 pixels, whose labels cannot all fit
        var dense = Days("\"tickCount\": { \"interval\": \"day\" }");
        var vertical = TenDays.Replace("\"x\": { \"field\": \"d\", \"type\": \"temporal\"AXIS }", "\"x\": { \"field\": \"v\", \"type\": \"quantitative\" }", StringComparison.Ordinal)
            .Replace("\"y\": { \"field\": \"v\", \"type\": \"quantitative\" }", "\"y\": { \"field\": \"d\", \"type\": \"temporal\" }", StringComparison.Ordinal);

        // Act
        var thinned = ChartTestHelpers.Axis(dense, "x", 100, 200)!;
        var all = ChartTestHelpers.Axis(Days("\"tickCount\": { \"interval\": \"day\" }, \"labelOverlap\": false"), "x", 100, 200)!;
        var y = ChartTestHelpers.Axis(vertical, "y", 100, 200)!;
        var noGrid = ChartTestHelpers.Axis(Days(), "x", 400, 200, "{ \"axisTemporal\": { \"grid\": false } }")!;

        // Assert: 11 ticks either way; parity draws fewer than 11 labels, and switching the overlap off draws every one
        Assert.Equal(11, thinned.Ticks.Count);
        Assert.InRange(ChartTestHelpers.Of<SceneText>(thinned.Group, "axis-label").Count, 2, 10);
        Assert.Equal(11, ChartTestHelpers.Of<SceneText>(all.Group, "axis-label").Count);

        // Assert: a vertical time axis has no flush labels (they are centered on their tick) and the temporal axis configuration applies
        Assert.All(ChartTestHelpers.Of<SceneText>(y.Group, "axis-label"), l => Assert.Equal("middle", l.Baseline));
        Assert.Null(noGrid.Grid);
    }

    /// <summary>
    ///     Validates that the labels of an ordinal field with a time unit are not turned by default (a nominal field gets 270 degrees) and are the dates of the unit.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_NoRotationDefaultForTimeUnitOrdinal()
    {
        // Arrange
        const string Spec = """
            { "data": { "values": [ { "d": "2024-01-05" }, { "d": "2024-02-05" }, { "d": "2024-03-05" } ] }, "mark": "bar",
              "encoding": { "x": { "field": "d", "type": "TYPE", "timeUnit": "month" }, "y": { "aggregate": "count" } } }
            """;

        // Act
        var ordinal = ChartTestHelpers.Axis(Spec.Replace("TYPE", "ordinal", StringComparison.Ordinal), "x", 300, 200)!;

        // Assert: horizontal labels of the three months, one per band, with the unit specifier (month: "%b")
        Assert.All(ChartTestHelpers.Of<SceneText>(ordinal.Group, "axis-label"), l => Assert.Equal(0, l.Angle, 9));
        Assert.Equal(["Jan", "Feb", "Mar"], ordinal.Ticks.Select(t => t.Label));
    }

    /// <summary>
    ///     Validates that a time scale in a time zone has its ticks at the local midnights and its labels in local time.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_Zone_LabelsAndTicksUseLocalMidnights()
    {
        // Arrange: January 1 and 11, 2024 at 00:00Z in a zone five and a half hours ahead: the domain runs from the local 05:30 of the 1st to the local 05:30 of the 11th
        const string Spec = """
            { "data": { "values": [ { "d": "2024-01-01T00:00:00Z", "v": 1 }, { "d": "2024-01-11T00:00:00Z", "v": 2 } ] }, "mark": "point",
              "encoding": { "x": { "field": "d", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } } }
            """;
        var zone = TimeTestSupport.Fixed(5, 30);

        // Act
        var local = ChartTestHelpers.Axis(Spec, "x", 400, 200, zone: zone)!;
        var utc = ChartTestHelpers.Axis(Spec, "x", 400, 200)!;

        // Assert: the local midnights of the 2nd to the 11th, each 18:30Z of the day before; the first is 18.5 hours (30.83 pixels, rounded 31) after the start
        Assert.Equal(Enumerable.Range(0, 10).Select(i => Jan(1, 1 + i, 18, 30)), Instants(local));
        Assert.Equal(["Tue 02", "Wed 03", "Thu 04", "Fri 05", "Sat 06", "Jan 07", "Mon 08", "Tue 09", "Wed 10", "Thu 11"], local.Ticks.Select(t => t.Label));
        Assert.Equal(31.0, local.Ticks[0].Position);

        // Assert: the same data on the UTC calendar has ticks at the UTC midnights
        Assert.Equal(Jan(1, 1), Instants(utc)[0]);
        Assert.Equal(11, utc.Ticks.Count);
    }

    /// <summary>
    ///     Validates that a UTC scale ignores the zone of the chart.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_UtcScale_IgnoresZone()
    {
        // Arrange
        var spec = Days().Replace("\"type\": \"temporal\"", "\"type\": \"temporal\", \"scale\": { \"type\": \"utc\" }", StringComparison.Ordinal);

        // Act
        var zoned = ChartTestHelpers.Axis(spec, "x", 400, 200, zone: TimeTestSupport.UsLike())!;
        var plain = ChartTestHelpers.Axis(spec, "x", 400, 200)!;

        // Assert: UTC midnights and UTC labels, whatever the zone is
        Assert.Equal(plain.Ticks.Select(t => t.Label), zoned.Ticks.Select(t => t.Label));
        Assert.Equal(Instants(plain), Instants(zoned));
        Assert.Equal(Jan(1, 1), Instants(zoned)[0]);
        Assert.Equal("2024", zoned.Ticks[0].Label);
    }

    /// <summary>
    ///     Validates the labels of a discrete axis over the dates of a time unit: the format, else the unit specifier, and <c>values</c> that name a part of the unit.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_DiscreteDateLabels_UseUnitSpecifier()
    {
        // Arrange
        const string Spec = """
            { "data": { "values": [ { "d": "2024-01-05" }, { "d": "2024-02-05" }, { "d": "2024-03-05" } ] }, "mark": "bar",
              "encoding": { "x": { "field": "d", "type": "ordinal", "timeUnit": "month"AXIS }, "y": { "aggregate": "count" } } }
            """;

        // Act
        var unit = ChartTestHelpers.Axis(Spec.Replace("AXIS", string.Empty, StringComparison.Ordinal), "x", 300, 200)!;
        var named = ChartTestHelpers.Axis(Spec.Replace("AXIS", ", \"axis\": { \"format\": \"%B\" }", StringComparison.Ordinal), "x", 300, 200)!;
        var chosen = ChartTestHelpers.Axis(Spec.Replace("AXIS", ", \"axis\": { \"values\": [ \"Mar\", 1 ] }", StringComparison.Ordinal), "x", 300, 200)!;

        // Assert
        Assert.Equal(["Jan", "Feb", "Mar"], unit.Ticks.Select(t => t.Label));
        Assert.Equal(["January", "February", "March"], named.Ticks.Select(t => t.Label));
        Assert.Equal(["Jan", "Mar"], chosen.Ticks.Select(t => t.Label));
        Assert.All(unit.Ticks, t => Assert.True(t.Value.IsDate));
    }

    /// <summary>
    ///     Validates that more ticks than <see cref="VegaLiteLimits.MaxTicks"/> are a located render exception, before any label is made, for generated ticks, an
    ///     interval request and a long list of values.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_TooManyTicks_IsLocated()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxTicks = 5 };
        var millis = Days("\"tickCount\": { \"interval\": \"millisecond\" }");
        var values = Days("\"values\": [ 1704067200000, 1704153600000, 1704240000000, 1704326400000, 1704412800000, 1704499200000 ]");

        // Act
        var generated = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(Days("\"tickCount\": { \"interval\": \"day\" }"), "x", 400, 200, limits: limits));
        var interval = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(millis, "x", 400, 200, limits: limits));
        var listed = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(values, "x", 400, 200, limits: limits));

        // Assert: located at the channel, and the message names the limit
        Assert.All([generated, interval, listed], e => Assert.True(e.Location.IsKnown));
        Assert.Contains("the limit of 5", generated.Message, StringComparison.Ordinal);
        Assert.Contains("MaxTicks", interval.Message, StringComparison.Ordinal);
        Assert.Contains("MaxTicks (5)", listed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a label longer than <see cref="VegaLiteLimits.MaxStringLength"/> is a located render exception that names the limit.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_LabelTooLong_IsLocated()
    {
        // Arrange: "%c %c" is "1/1/2024, 12:00:00 AM 1/1/2024, 12:00:00 AM": 43 characters; one "%c" is 21, so a single tick (the labels of a guide are limited together) fits 30
        var limits = new VegaLiteLimits { MaxStringLength = 30 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(Days("\"format\": \"%c %c\", \"values\": [ 1704067200000 ]"), "x", 400, 200, limits: limits));
        var fits = ChartTestHelpers.Axis(Days("\"format\": \"%c\", \"values\": [ 1704067200000 ]"), "x", 400, 200, limits: limits)!;

        // Assert
        Assert.True(error.Location.IsKnown);
        Assert.Contains("the limit of 30", error.Message, StringComparison.Ordinal);
        Assert.Contains("A label", error.Message, StringComparison.Ordinal);
        Assert.Equal("1/1/2024, 12:00:00 AM", fits.Ticks[0].Label);
    }

    /// <summary>
    ///     Validates that the labels of one time axis are limited together: eleven labels of 21 or 22 characters are 233, which is over a limit of 100 although no single label is,
    ///     and the exception is located and names the limit.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_LabelsTogetherTooLong_IsLocated()
    {
        // Arrange: eleven day ticks with the labels of "%c" (21 characters for the 1st to the 9th, 22 for the 10th and 11th: 233 in all); the fifth label brings the total to 105
        var limits = new VegaLiteLimits { MaxStringLength = 100 };
        var spec = Days("\"format\": \"%c\"");

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(spec, "x", 400, 200, limits: limits));
        var fits = ChartTestHelpers.Axis(spec, "x", 400, 200, limits: new VegaLiteLimits { MaxStringLength = 240 })!;

        // Assert: the labels that fit are 233 characters in all (the 10th and 11th are "1/10/2024, 12:00:00 AM" and "1/11/2024, 12:00:00 AM", 22 each)
        Assert.True(error.Location.IsKnown);
        Assert.Contains("together longer than the limit of 100 characters", error.Message, StringComparison.Ordinal);
        Assert.Equal(11, fits.Ticks.Count);
        Assert.Equal(233, fits.Ticks.Sum(t => t.Label.Length));
    }

    /// <summary>
    ///     Validates the aggregate cap with the default limit and a hand-built hostile specification: a format of 512 month names (1,024 characters, the longest the format
    ///     allows) on 481 monthly ticks is about 1.5 million characters, which is over the default <see cref="VegaLiteLimits.MaxStringLength"/> of a guide, so the axis fails
    ///     with a located exception instead of building the labels.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_HostileFormatOnManyTicks_IsStoppedByTheAggregateCap()
    {
        // Arrange: January 1980 to January 2020 has 481 month starts; "%B" is 3 to 9 characters (6.2 on average), so 512 of them are about 3,200 characters a label
        var format = string.Concat(Enumerable.Repeat("%B", 512));
        var spec = $$"""
            { "data": { "values": [ { "d": "1980-01-01", "v": 1 }, { "d": "2020-01-01", "v": 2 } ] },
              "mark": "point",
              "encoding": {
                "x": { "field": "d", "type": "temporal", "axis": { "format": "{{format}}", "tickCount": { "interval": "month" } } },
                "y": { "field": "v", "type": "quantitative" } } }
            """;

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Axis(spec, "x", 400, 200));

        // Assert
        Assert.True(error.Location.IsKnown);
        Assert.Contains("the limit of 1048576 characters", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a time format that is too long, or expands to too many pieces, is a located specification exception at the format, from the specification
    ///     parser (for a temporal field, and for a field forced to time by its <c>formatType</c>).
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_FormatSpecifierLimits_AreSpecificationErrors()
    {
        // Arrange
        var tooLong = new string('x', TimeFormat.MaxSpecifierLength + 1);
        var manyPieces = string.Concat(Enumerable.Repeat("%c", 400));
        var forced = Days().Replace("\"type\": \"quantitative\"", "\"type\": \"quantitative\", \"axis\": { \"formatType\": \"time\", \"format\": \"" + tooLong + "\" }", StringComparison.Ordinal);

        // Act
        var length = Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Axis(Days("\"format\": \"" + tooLong + "\""), "x", 400, 200));
        var pieces = Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Axis(Days("\"format\": \"" + manyPieces + "\""), "x", 400, 200));
        var multi = Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Axis(Days("\"format\": { \"year\": \"" + tooLong + "\" }"), "x", 400, 200));
        var forcedError = Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Axis(forced, "y", 400, 200));

        // Assert: located, naming the limit
        Assert.All([length, multi, forcedError], e => Assert.Contains("the limit of 1024 characters", e.Message, StringComparison.Ordinal));
        Assert.Contains("the limit of 4096 pieces", pieces.Message, StringComparison.Ordinal);
        Assert.All([length, pieces, multi, forcedError], e => Assert.True(e.Location.IsKnown));
    }

    /// <summary>
    ///     Validates that a canceled token stops the construction of a time axis.
    /// </summary>
    /// <returns>A task that completes when the check is done.</returns>
    [Fact]
    public async Task AxisBuilder_Time_Cancellation()
    {
        // Arrange
        var parts = ChartTestHelpers.Parts(Days());
        var scale = parts.Scales["x"]!;
        var request = new AxisRequest("x", parts.Tree.Root["encoding"]?["x"], scale, scale.CreateModel(400), 400, 200, "point");
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => new AxisBuilder(new TextMeasurer(), parts.Config).Build(request, canceled.Token));
        Assert.NotNull(new AxisBuilder(new TextMeasurer(), parts.Config).Build(request, Ct));
    }

    /// <summary>
    ///     Validates that the labels, with their English month and day names and their ASCII digits, do not depend on the current culture (Turkish has a dotted capital
    ///     I, German groups with dots).
    /// </summary>
    /// <param name="culture">The name of the culture.</param>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("")]
    public void AxisBuilder_Time_Culture_Invariant(string culture)
    {
        // Act
        var labels = ExpressionTestSupport.UnderCulture(culture, () =>
        {
            var multi = ChartTestHelpers.Axis(Days(), "x", 400, 200)!.Ticks.Select(t => t.Label).ToList();
            var named = ChartTestHelpers.Axis(Days("\"format\": \"%A %B %I %p\""), "x", 400, 200)!.Ticks.Select(t => t.Label).ToList();
            var number = ChartTestHelpers.Axis(Days("\"format\": \",d\", \"formatType\": \"number\", \"tickCount\": { \"interval\": \"day\" }"), "x", 400, 200)!.Ticks.Select(t => t.Label).ToList();
            return (multi, named, number);
        });

        // Assert
        Assert.Equal(["2024", "Tue 02", "Wed 03", "Thu 04", "Fri 05", "Sat 06", "Jan 07", "Mon 08", "Tue 09", "Wed 10", "Thu 11"], labels.multi);
        Assert.Equal("Monday January 12 AM", labels.named[0]);
        Assert.Equal("1,704,067,200,000", labels.number[0]);
    }

    /// <summary>
    ///     Validates that building the same axis twice gives the same ticks, labels and positions.
    /// </summary>
    [Fact]
    public void AxisBuilder_Time_Deterministic()
    {
        // Act
        var first = ChartTestHelpers.Axis(Months(), "x", 300, 200, zone: TimeTestSupport.UsLike())!;
        var second = ChartTestHelpers.Axis(Months(), "x", 300, 200, zone: TimeTestSupport.UsLike())!;

        // Assert
        Assert.Equal(Instants(first), Instants(second));
        Assert.Equal(first.Ticks.Select(t => (t.Position, t.Label)), second.Ticks.Select(t => (t.Position, t.Label)));
        Assert.Equal(first.ItemCount, second.ItemCount);
    }
}
