using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of the UTC arithmetic of <see cref="TimeCalendar"/>: instants and parts, the ECMAScript roll-over of
///     <see cref="TimeCalendar.FromParts"/> and the behavior at the edges of the valid range. The expected values are worked out
///     by hand from the definitions of the Gregorian calendar.
/// </summary>
public class TimeCalendarTests
{
    /// <summary>
    ///     Validates the anchor instant: 2024-02-29T12:34:56.789Z is day 19,782 (a Thursday, the 60th day of the year) plus 45,296,789 ms.
    /// </summary>
    [Fact]
    public void TimeCalendar_ToParts_Utc_Anchor_20240229T123456789()
    {
        // Arrange: the UTC calendar and the hand-derived anchor
        var calendar = TimeCalendar.Utc;

        // Act: break the instant into parts and build it back
        var parts = calendar.ToParts(TimeTestSupport.AnchorMilliseconds);
        var back = calendar.FromParts(2024, 1, 29, 12, 34, 56, 789);

        // Assert: every part and the round trip
        Assert.True(parts.IsValid);
        Assert.Equal((2024, 1, 29, 4, 60), (parts.Year, parts.Month, parts.Date, parts.Day, parts.DayOfYear));
        Assert.Equal((12, 34, 56, 789), (parts.Hours, parts.Minutes, parts.Seconds, parts.Milliseconds));
        Assert.Equal(0.0, parts.OffsetMs);
        Assert.Equal(TimeTestSupport.AnchorMilliseconds, back);
    }

    /// <summary>
    ///     Validates that the epoch is Thursday 1970-01-01, the first day of the year.
    /// </summary>
    [Fact]
    public void TimeCalendar_ToParts_Utc_Epoch_Is1970_01_01Thursday()
    {
        // Arrange / Act: the parts of instant zero
        var parts = TimeCalendar.Utc.ToParts(0);

        // Assert: Thursday is weekday 4
        Assert.Equal((1970, 0, 1, 4, 1), (parts.Year, parts.Month, parts.Date, parts.Day, parts.DayOfYear));
        Assert.Equal((0, 0, 0, 0), (parts.Hours, parts.Minutes, parts.Seconds, parts.Milliseconds));
    }

    /// <summary>
    ///     Validates that an instant before the epoch breaks into the right parts: -1 ms is 1969-12-31T23:59:59.999 (a Wednesday, day 365).
    /// </summary>
    [Fact]
    public void TimeCalendar_ToParts_Utc_OneMillisecondBeforeTheEpoch()
    {
        // Arrange / Act: the parts of -1 ms
        var parts = TimeCalendar.Utc.ToParts(-1);

        // Assert: floor division keeps the time of day positive
        Assert.Equal((1969, 11, 31, 3, 365), (parts.Year, parts.Month, parts.Date, parts.Day, parts.DayOfYear));
        Assert.Equal((23, 59, 59, 999), (parts.Hours, parts.Minutes, parts.Seconds, parts.Milliseconds));
    }

    /// <summary>
    ///     Validates that a fraction of a millisecond is truncated toward zero, like a JavaScript date.
    /// </summary>
    [Theory]
    [InlineData(1.9, 1)]
    [InlineData(-1.5, -1)]
    [InlineData(0.5, 0)]
    public void TimeCalendar_ToParts_Utc_TruncatesFractions(double instant, double expectedMilliseconds)
    {
        // Arrange / Act: read the parts and build them back
        var calendar = TimeCalendar.Utc;
        var parts = calendar.ToParts(instant);
        var back = calendar.FromParts(parts.Year, parts.Month, parts.Date, parts.Hours, parts.Minutes, parts.Seconds, parts.Milliseconds);

        // Assert: the round trip gives the whole millisecond
        Assert.Equal(expectedMilliseconds, back);
    }

    /// <summary>
    ///     Validates the months and days that roll over like the JavaScript constructor: month 12, date 0, date 32 and a negative month.
    /// </summary>
    [Theory]
    [InlineData(2024, 12, 1, 0, 0, 0, 0, 2025, 1, 1, 0)]
    [InlineData(2024, 0, 0, 0, 0, 0, 0, 2023, 12, 31, 0)]
    [InlineData(2024, 0, 32, 0, 0, 0, 0, 2024, 2, 1, 0)]
    [InlineData(2024, -1, 1, 0, 0, 0, 0, 2023, 12, 1, 0)]
    [InlineData(2024, 1, 30, 0, 0, 0, 0, 2024, 3, 1, 0)]
    [InlineData(2023, 1, 29, 0, 0, 0, 0, 2023, 3, 1, 0)]
    [InlineData(2024, 5, 1, 24, 0, 0, 0, 2024, 6, 2, 0)]
    [InlineData(2024, 5, 1, 0, 59, 60, 1000, 2024, 6, 1, 1, 0, 1)]
    [InlineData(2024, 5, 1, -1, 0, 0, 0, 2024, 5, 31, 23)]
    [InlineData(2024, 5.9, 1.9, 0, 0, 0, 0, 2024, 6, 1, 0)]
    public void TimeCalendar_FromParts_Utc_RollsMonthsAndDaysLikeJavaScript(
        double year,
        double month,
        double date,
        double hours,
        double minutes,
        double seconds,
        double milliseconds,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour,
        int expectedMinute = 0,
        int expectedSecond = 0)
    {
        // Arrange: the expected instant from the base class library (month is one based there)
        var expected = TimeTestSupport.Ms(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute, expectedSecond);

        // Act: build the instant from out-of-range parts
        var actual = TimeCalendar.Utc.FromParts(year, month, date, hours, minutes, seconds, milliseconds);

        // Assert: the rolled-over instant
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that non-finite parts and years outside 1 to 9999 give NaN while the boundary years are accepted.
    /// </summary>
    [Fact]
    public void TimeCalendar_FromParts_Utc_RejectsNonFiniteAndOutOfRange()
    {
        // Arrange: the UTC calendar
        var calendar = TimeCalendar.Utc;

        // Act / Assert: non-finite arguments and years 0 and 10000 are NaN
        Assert.True(double.IsNaN(calendar.FromParts(double.NaN, 0, 1, 0, 0, 0, 0)));
        Assert.True(double.IsNaN(calendar.FromParts(2024, double.PositiveInfinity, 1, 0, 0, 0, 0)));
        Assert.True(double.IsNaN(calendar.FromParts(2024, 0, double.NegativeInfinity, 0, 0, 0, 0)));
        Assert.True(double.IsNaN(calendar.FromParts(2024, 0, 1, 0, 0, double.NaN, 0)));
        Assert.True(double.IsNaN(calendar.FromParts(0, 0, 1, 0, 0, 0, 0)));
        Assert.True(double.IsNaN(calendar.FromParts(10000, 0, 1, 0, 0, 0, 0)));

        // Assert: the first and the last representable millisecond are accepted
        Assert.Equal(DataValue.MinDateMilliseconds, calendar.FromParts(1, 0, 1, 0, 0, 0, 0));
        Assert.Equal(DataValue.MaxDateMilliseconds, calendar.FromParts(9999, 11, 31, 23, 59, 59, 999));
        Assert.True(double.IsNaN(calendar.FromParts(1, 0, 1, 0, 0, 0, -1)));
        Assert.True(double.IsNaN(calendar.FromParts(9999, 11, 31, 23, 59, 59, 1000)));
    }

    /// <summary>
    ///     Validates the range test: the first and last representable instants are valid, one millisecond beyond is not.
    /// </summary>
    [Fact]
    public void TimeCalendar_IsValid_RangeBoundaries()
    {
        // Arrange / Act / Assert: the range of years 1 to 9999
        Assert.True(TimeCalendar.IsValid(DataValue.MinDateMilliseconds));
        Assert.True(TimeCalendar.IsValid(DataValue.MaxDateMilliseconds));
        Assert.False(TimeCalendar.IsValid(DataValue.MinDateMilliseconds - 1));
        Assert.False(TimeCalendar.IsValid(DataValue.MaxDateMilliseconds + 1));
        Assert.False(TimeCalendar.IsValid(double.NaN));
        Assert.False(TimeCalendar.IsValid(double.PositiveInfinity));
    }

    /// <summary>
    ///     Validates that no input, however extreme, makes a calendar call throw: every call returns a value or NaN.
    /// </summary>
    [Theory]
    [InlineData(1e300)]
    [InlineData(-1e300)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-62135596800001.0)]
    [InlineData(253402300800000.0)]
    [InlineData(-62135596800000.0)]
    [InlineData(253402300799999.0)]
    public void TimeCalendar_Extremes_NeverThrow(double extreme)
    {
        // Arrange: the UTC calendar and a zone with daylight saving time
        var calendars = new[] { TimeCalendar.Utc, TimeTestSupport.Calendar(TimeTestSupport.UsLike()), TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30)) };

        foreach (var calendar in calendars)
        {
            // Act: every entry point with the extreme value in every position
            _ = calendar.ToParts(extreme);
            _ = calendar.UtcOffsetMilliseconds(extreme);
            _ = calendar.FromParts(extreme, 0, 1, 0, 0, 0, 0);
            _ = calendar.FromParts(2024, extreme, 1, 0, 0, 0, 0);
            _ = calendar.FromParts(2024, 0, extreme, 0, 0, 0, 0);
            _ = calendar.FromParts(2024, 0, 1, extreme, extreme, extreme, extreme);
            foreach (var kind in Enum.GetValues<TimeIntervalKind>())
            {
                var interval = calendar.Interval(kind);
                var floor = interval.Floor(extreme);
                var ceil = interval.Ceil(extreme);
                var offset = interval.Offset(extreme, 1);
                var offsetBy = interval.Offset(0, extreme);
                var count = interval.Count(extreme, 0);
                _ = interval.Round(extreme);
                _ = interval.Every(extreme);

                // Assert: a result is either NaN or a valid instant
                foreach (var result in new[] { floor, ceil, offset, offsetBy })
                {
                    Assert.True(double.IsNaN(result) || TimeCalendar.IsValid(result), $"{kind} in {calendar.Zone?.Id ?? "UTC"}: floor={floor} ceil={ceil} offset={offset} offsetBy={offsetBy}");
                }

                Assert.True(double.IsNaN(count) || double.IsFinite(count));
            }
        }
    }

    /// <summary>
    ///     Validates the conversion against the base class library over the whole range: one instant every 61 days and 7 ms from year 1 to 9999 (about 60,000 instants).
    /// </summary>
    [Fact]
    public void TimeCalendar_ToParts_Utc_MatchesTheBaseClassLibraryAcrossTheRange()
    {
        // Arrange: a step that is not a multiple of a day, so that every time of day shows up
        var calendar = TimeCalendar.Utc;
        const double step = (61 * 86_400_000.0) + 7;
        var checkedCount = 0;

        // Act / Assert: compare each instant with DateTimeOffset
        for (var instant = DataValue.MinDateMilliseconds; instant <= DataValue.MaxDateMilliseconds; instant += step)
        {
            var expected = DateTimeOffset.FromUnixTimeMilliseconds((long)instant);
            var parts = calendar.ToParts(instant);
            Assert.Equal(
                (expected.Year, expected.Month - 1, expected.Day, (int)expected.DayOfWeek, expected.DayOfYear, expected.Hour, expected.Minute, expected.Second, expected.Millisecond),
                (parts.Year, parts.Month, parts.Date, parts.Day, parts.DayOfYear, parts.Hours, parts.Minutes, parts.Seconds, parts.Milliseconds));
            Assert.Equal(instant, calendar.FromParts(parts.Year, parts.Month, parts.Date, parts.Hours, parts.Minutes, parts.Seconds, parts.Milliseconds));
            checkedCount++;
        }

        Assert.True(checkedCount > 50_000);
    }

    /// <summary>
    ///     Validates that <see cref="TimeCalendar.Create"/> returns the UTC singleton for null and for zones that are always at offset zero.
    /// </summary>
    [Fact]
    public void TimeCalendar_Create_NullAndZeroOffsetZones_GiveUtc()
    {
        // Arrange / Act: the calendars of null, the UTC zone and a fixed zone at +00:00
        var fromNull = TimeCalendar.Create(null);
        var fromUtc = TimeCalendar.Create(TimeZoneInfo.Utc);
        var fromZero = TimeCalendar.Create(TimeTestSupport.Fixed(0));

        // Assert: all are the singleton
        Assert.Same(TimeCalendar.Utc, fromNull);
        Assert.Same(TimeCalendar.Utc, fromUtc);
        Assert.Same(TimeCalendar.Utc, fromZero);
        Assert.True(TimeCalendar.Utc.IsUtc);
        Assert.Null(TimeCalendar.Utc.Zone);
    }

    /// <summary>
    ///     Validates that the calendar gives the same answers under a Turkish and a German culture as under the invariant one.
    /// </summary>
    [Fact]
    public void TimeCalendar_ToParts_And_FromParts_AreCultureIndependent()
    {
        // Arrange: summarize a few conversions as text
        static string Summarize()
        {
            var parts = TimeCalendar.Utc.ToParts(TimeTestSupport.AnchorMilliseconds);
            return $"{parts.Year}-{parts.Month}-{parts.Date}T{parts.Hours}:{parts.Minutes}:{parts.Seconds}.{parts.Milliseconds}={TimeCalendar.Utc.FromParts(2012, 1, 29, 0, 0, 0, 0)}";
        }

        // Act: run under the invariant, Turkish and German cultures
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, Summarize);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", Summarize);
        var german = ExpressionTestSupport.UnderCulture("de-DE", Summarize);

        // Assert: identical text
        Assert.Equal("2024-1-29T12:34:56.789=1330473600000", invariant);
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }
}
