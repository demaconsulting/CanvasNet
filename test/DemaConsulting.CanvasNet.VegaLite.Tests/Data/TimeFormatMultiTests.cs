using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of Vega's multi-format (<see cref="TimeFormat.Multi"/>), the default labels of a time axis. The anchors are
///     hand-derived: 2024-03-05 is a Tuesday (day 19,787, 1,709,596,800,000 ms), March 3 is the Sunday before it, 2024-03-01 is
///     1,709,251,200,000 ms and 2024-04-01 is 1,711,929,600,000 ms (31 days later).
/// </summary>
public class TimeFormatMultiTests
{
    /// <summary>The instant 2024-03-05T00:00Z.</summary>
    private const double March5 = 1_709_596_800_000;

    /// <summary>The number of milliseconds in a day.</summary>
    private const double Day = 86_400_000;

    /// <summary>The limit used when a test does not care about it.</summary>
    private const int Limit = 1000;

    /// <summary>
    ///     Validates that the default format is chosen by the coarsest boundary the instant sits on.
    /// </summary>
    /// <param name="offsetFromMarch5">The milliseconds after 2024-03-05T00:00Z.</param>
    /// <param name="expected">The expected label.</param>
    [Theory]
    [InlineData(0.0, "Tue 05")]
    [InlineData(-2 * Day, "Mar 03")]
    [InlineData(-4 * Day, "March")]
    [InlineData(27 * Day, "April")]
    [InlineData(13 * 3_600_000.0, "01 PM")]
    [InlineData((13 * 3_600_000.0) + (45 * 60_000.0), "01:45")]
    [InlineData((13 * 3_600_000.0) + (45 * 60_000.0) + 30_000, ":30")]
    [InlineData((13 * 3_600_000.0) + (45 * 60_000.0) + 30_250, ".250")]
    [InlineData(1 * Day, "Wed 06")]
    [InlineData(-33 * Day, "February")]
    [InlineData(-64 * Day, "2024")]
    [InlineData(118 * Day, "July")]
    [InlineData(-5 * Day, "Thu 29")]
    public void TimeFormatMulti_Default_PicksTheCoarsestExactUnit(double offsetFromMarch5, string expected)
    {
        // Arrange: the default multi-format in UTC
        var formatter = TimeFormat.Multi(TimeCalendar.Utc, null);

        // Act: format the instant
        var actual = formatter.Format(March5 + offsetFromMarch5, TimeCalendar.Utc, Limit);

        // Assert: the label of the hand-derived unit
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that an instant before the epoch uses the same rules: -1 ms is 1969-12-31T23:59:59.999, a fraction of a second.
    /// </summary>
    [Fact]
    public void TimeFormatMulti_Default_BeforeTheEpoch()
    {
        // Arrange / Act: format -1 ms and the epoch
        var formatter = TimeFormat.Multi(TimeCalendar.Utc, null);
        var before = formatter.Format(-1, TimeCalendar.Utc, Limit);
        var epoch = formatter.Format(0, TimeCalendar.Utc, Limit);

        // Assert: milliseconds, then the start of the year 1970
        Assert.Equal(".999", before);
        Assert.Equal("1970", epoch);
    }

    /// <summary>
    ///     Validates that every override replaces its own level and nothing else.
    /// </summary>
    /// <param name="offsetFromMarch5">The milliseconds after 2024-03-05T00:00Z.</param>
    /// <param name="expected">The expected label.</param>
    [Theory]
    [InlineData(13 * 3_600_000.0 + 45 * 60_000.0 + 30_250, "ms:250")]
    [InlineData(13 * 3_600_000.0 + 45 * 60_000.0 + 30_000, "s:30")]
    [InlineData(13 * 3_600_000.0 + 45 * 60_000.0, "m:45")]
    [InlineData(13 * 3_600_000.0, "h:13")]
    [InlineData(0.0, "d:05")]
    [InlineData(-2 * Day, "w:09")]
    [InlineData(-4 * Day, "mo:03")]
    [InlineData(27 * Day, "q:2")]
    [InlineData(-64 * Day, "y:2024")]
    public void TimeFormatMulti_Overrides_ReplaceEachLevel(double offsetFromMarch5, string expected)
    {
        // Arrange: an override for each key
        var overrides = new Dictionary<string, string>
        {
            ["milliseconds"] = "ms:%L",
            ["seconds"] = "s:%S",
            ["minutes"] = "m:%M",
            ["hours"] = "h:%H",
            ["date"] = "d:%d",
            ["week"] = "w:%U",
            ["month"] = "mo:%m",
            ["quarter"] = "q:%q",
            ["year"] = "y:%Y",
        };
        var formatter = TimeFormat.Multi(TimeCalendar.Utc, overrides);

        // Act: format the instant
        var actual = formatter.Format(March5 + offsetFromMarch5, TimeCalendar.Utc, Limit);

        // Assert: the override of the level
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates the fallbacks: <c>day</c> stands in for <c>date</c>, <c>date</c> wins over <c>day</c>, and an empty override keeps the default.
    /// </summary>
    [Fact]
    public void TimeFormatMulti_Overrides_DayKeyAndEmptyValues()
    {
        // Arrange: three override sets
        var dayOnly = TimeFormat.Multi(TimeCalendar.Utc, new Dictionary<string, string> { ["day"] = "D%d" });
        var both = TimeFormat.Multi(TimeCalendar.Utc, new Dictionary<string, string> { ["day"] = "D%d", ["date"] = "d%d" });
        var empty = TimeFormat.Multi(TimeCalendar.Utc, new Dictionary<string, string> { ["date"] = string.Empty, ["year"] = string.Empty, ["unknown"] = "?" });

        // Act: format March 5 and the start of 2024
        var dayText = dayOnly.Format(March5, TimeCalendar.Utc, Limit);
        var bothText = both.Format(March5, TimeCalendar.Utc, Limit);
        var emptyDay = empty.Format(March5, TimeCalendar.Utc, Limit);
        var emptyYear = empty.Format(March5 - (64 * Day), TimeCalendar.Utc, Limit);

        // Assert: the pinned rules
        Assert.Equal("D05", dayText);
        Assert.Equal("d05", bothText);
        Assert.Equal("Tue 05", emptyDay);
        Assert.Equal("2024", emptyYear);
    }

    /// <summary>
    ///     Validates that the boundaries are local to the zone: in a +05:30 zone local midnight is 18:30Z the day before, and
    ///     00:00Z is 05:30 local, a minute label.
    /// </summary>
    [Fact]
    public void TimeFormatMulti_Zone_UsesLocalBoundaries()
    {
        // Arrange: a +05:30 zone
        var zone = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));
        var formatter = TimeFormat.Multi(zone, null);

        // Act: format local midnight of March 5, 00:00Z, local 19:00 and the local new year
        var midnight = formatter.Format(TimeTestSupport.Ms(2024, 3, 4, 18, 30), zone, Limit);
        var utcMidnight = formatter.Format(March5, zone, Limit);
        var evening = formatter.Format(TimeTestSupport.Ms(2024, 3, 5, 13, 30), zone, Limit);
        var newYear = formatter.Format(TimeTestSupport.Ms(2023, 12, 31, 18, 30), zone, Limit);

        // Assert: boundaries of the local clock
        Assert.Equal("Tue 05", midnight);
        Assert.Equal("05:30", utcMidnight);
        Assert.Equal("07 PM", evening);
        Assert.Equal("2024", newYear);
    }

    /// <summary>
    ///     Validates that a multi-format built on the UTC calendar ignores the zone given to <c>Format</c>, and that a
    ///     zone-bound one ignores a UTC calendar given to <c>Format</c>.
    /// </summary>
    [Fact]
    public void TimeFormatMulti_IsBoundToItsCalendar()
    {
        // Arrange: formatters bound to UTC and to a +05:30 zone
        var zone = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));
        var utc = TimeFormat.Multi(TimeCalendar.Utc, null);
        var local = TimeFormat.Multi(zone, null);

        // Act: format 00:00Z with the other calendar
        var utcText = utc.Format(March5, zone, Limit);
        var localText = local.Format(March5, TimeCalendar.Utc, Limit);

        // Assert: the bound calendar decides
        Assert.Equal("Tue 05", utcText);
        Assert.Equal("05:30", localText);
    }

    /// <summary>
    ///     Validates that an invalid instant gives <c>Invalid Date</c> and that the output limit applies.
    /// </summary>
    [Fact]
    public void TimeFormatMulti_InvalidDateAndLimit()
    {
        // Arrange: the default multi-format
        var formatter = TimeFormat.Multi(TimeCalendar.Utc, null);

        // Act / Assert: NaN is invalid, and "Tue 05" does not fit in five characters
        Assert.Equal("Invalid Date", formatter.Format(double.NaN, TimeCalendar.Utc, Limit));
        Assert.Throws<TimeFormatTooLongException>(() => formatter.Format(March5, TimeCalendar.Utc, 5));
        Assert.Equal("Tue 05", formatter.Format(March5, TimeCalendar.Utc, 6));
    }
}
