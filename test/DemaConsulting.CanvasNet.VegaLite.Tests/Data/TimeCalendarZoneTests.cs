using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of the time zone half of <see cref="TimeCalendar"/>: offsets around daylight saving changes, the
///     ECMAScript rule for wall times that are skipped or repeated, fixed-offset zones and the edges of the range. All zones are
///     built by <see cref="TimeTestSupport"/>, so no test depends on the host's zone database.
/// </summary>
public class TimeCalendarZoneTests
{
    /// <summary>Minus five hours in milliseconds.</summary>
    private const double MinusFive = -18_000_000;

    /// <summary>Minus four hours in milliseconds.</summary>
    private const double MinusFour = -14_400_000;

    /// <summary>
    ///     Validates the offsets around the 2024 changes of a United States like zone: the start at 07:00Z on March 10 and the end at 06:00Z on November 3.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_UsLike_OffsetsAroundTheTransitions()
    {
        // Arrange: the zone with the hand-derived transitions (second Sunday of March, first Sunday of November)
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: the offsets one millisecond before and at each transition
        var beforeStart = calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 3, 10, 6, 59, 59, 999));
        var atStart = calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 3, 10, 7));
        var beforeEnd = calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 11, 3, 5, 59, 59, 999));
        var atEnd = calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 11, 3, 6));

        // Assert: standard time, daylight time, daylight time, standard time
        Assert.Equal(MinusFive, beforeStart);
        Assert.Equal(MinusFour, atStart);
        Assert.Equal(MinusFour, beforeEnd);
        Assert.Equal(MinusFive, atEnd);
        Assert.False(calendar.IsUtc);
        Assert.NotNull(calendar.Zone);
    }

    /// <summary>
    ///     Validates the wall time of the instants around the March change: 06:59:59.999Z is 01:59:59.999 and 07:00Z is 03:00.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_UsLike_ToPartsAroundTheSpringChange()
    {
        // Arrange: the zone
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: the parts of the two instants
        var before = calendar.ToParts(TimeTestSupport.Ms(2024, 3, 10, 6, 59, 59, 999));
        var after = calendar.ToParts(TimeTestSupport.Ms(2024, 3, 10, 7));

        // Assert: the clock jumps from 01:59:59.999 to 03:00
        Assert.Equal((10, 1, 59, 59, 999, MinusFive), (before.Date, before.Hours, before.Minutes, before.Seconds, before.Milliseconds, before.OffsetMs));
        Assert.Equal((10, 3, 0, 0, 0, MinusFour), (after.Date, after.Hours, after.Minutes, after.Seconds, after.Milliseconds, after.OffsetMs));
    }

    /// <summary>
    ///     Validates the gap rule: 02:30 on 2024-03-10 does not exist and is read with the offset from before the change, which is the instant that shows 03:30.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_UsLike_Gap_0230BecomesThe0330Instant()
    {
        // Arrange: the zone
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: build the skipped wall time and read it back
        var instant = calendar.FromParts(2024, 2, 10, 2, 30, 0, 0);
        var parts = calendar.ToParts(instant);

        // Assert: 02:30 at minus five hours is 07:30Z, which shows as 03:30
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10, 7, 30), instant);
        Assert.Equal((3, 30), (parts.Hours, parts.Minutes));
    }

    /// <summary>
    ///     Validates the overlap rule: 01:30 on 2024-11-03 occurs twice and takes the earlier instant, 05:30Z.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_UsLike_Overlap_0130IsTheEarlierInstant()
    {
        // Arrange: the zone
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: build the repeated wall time
        var instant = calendar.FromParts(2024, 10, 3, 1, 30, 0, 0);

        // Assert: the daylight time reading (minus four hours) is the earlier one
        Assert.Equal(TimeTestSupport.Ms(2024, 11, 3, 5, 30), instant);
        Assert.Equal(MinusFour, calendar.UtcOffsetMilliseconds(instant));
    }

    /// <summary>
    ///     Validates wall times just outside the repeated and the skipped hour: 00:59 and 02:00 of November 3 and 01:59 and 03:00 of March 10.
    /// </summary>
    [Theory]
    [InlineData(2024, 10, 3, 0, 59, 2024, 11, 3, 4, 59)]
    [InlineData(2024, 10, 3, 2, 0, 2024, 11, 3, 7, 0)]
    [InlineData(2024, 2, 10, 1, 59, 2024, 3, 10, 6, 59)]
    [InlineData(2024, 2, 10, 3, 0, 2024, 3, 10, 7, 0)]
    [InlineData(2024, 6, 15, 12, 0, 2024, 7, 15, 16, 0)]
    [InlineData(2024, 0, 15, 12, 0, 2024, 1, 15, 17, 0)]
    public void TimeCalendarZone_UsLike_WallTimesAwayFromTheAmbiguity(
        int year,
        int month,
        int date,
        int hour,
        int minute,
        int utcYear,
        int utcMonth,
        int utcDay,
        int utcHour,
        int utcMinute)
    {
        // Arrange: the zone and the expected instant derived by adding the offset in force at that wall time
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var expected = TimeTestSupport.Ms(utcYear, utcMonth, utcDay, utcHour, utcMinute);

        // Act: build the instant from the wall time
        var actual = calendar.FromParts(year, month, date, hour, minute, 0, 0);

        // Assert: the unambiguous reading
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates fixed-offset zones: they round-trip and shift the wall time by their offset (+05:30, -03:00, -03:30).
    /// </summary>
    [Theory]
    [InlineData(5, 30, 5, 30)]
    [InlineData(-3, 0, 21, 0)]
    [InlineData(-3, -30, 20, 30)]
    public void TimeCalendarZone_FixedOffset_UsesTheFastPathAndRoundTrips(int offsetHours, int offsetMinutes, int expectedHour, int expectedMinute)
    {
        // Arrange: a zone without rules; the instant is 2024-06-01T00:00Z, and the wall hour on the previous day for west zones
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.Fixed(offsetHours, offsetMinutes));
        var instant = TimeTestSupport.Ms(2024, 6, 1);

        // Act: break the instant down and build it back
        var parts = calendar.ToParts(instant);
        var back = calendar.FromParts(parts.Year, parts.Month, parts.Date, parts.Hours, parts.Minutes, parts.Seconds, parts.Milliseconds);

        // Assert: the wall time is the instant shifted by the offset
        Assert.Equal((expectedHour, expectedMinute), (parts.Hours, parts.Minutes));
        Assert.Equal(instant, back);
        Assert.Equal(new TimeSpan(offsetHours, offsetMinutes, 0).TotalMilliseconds, parts.OffsetMs);
        Assert.False(calendar.IsUtc);
    }

    /// <summary>
    ///     Validates a rule that spans the new year: the southern zone is at +11 in January, +10 in July, and changes at 2024-04-06T16:00Z and 2024-10-05T16:00Z.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_SouthernRuleSpanningNewYear()
    {
        // Arrange: standard +10, daylight +11 from the first Sunday of October to the first Sunday of April
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.SouthernLike());
        const double plusTen = 36_000_000;
        const double plusEleven = 39_600_000;

        // Act / Assert: summer, winter and the four instants around the two changes
        Assert.Equal(plusEleven, calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 1, 15)));
        Assert.Equal(plusEleven, calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2023, 12, 25)));
        Assert.Equal(plusTen, calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 7, 15)));
        Assert.Equal(plusEleven, calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 4, 6, 15, 59, 59, 999)));
        Assert.Equal(plusTen, calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 4, 6, 16)));
        Assert.Equal(plusTen, calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 10, 5, 15, 59, 59, 999)));
        Assert.Equal(plusEleven, calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 10, 5, 16)));
    }

    /// <summary>
    ///     Validates the gap and the overlap of the southern zone: 02:30 on 2024-10-06 is skipped and 02:30 on 2024-04-07 repeats.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_Southern_GapAndOverlap()
    {
        // Arrange: the zone
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.SouthernLike());

        // Act: the skipped wall time (clock 02:00 to 03:00 on October 6) and the repeated one (03:00 back to 02:00 on April 7)
        var gap = calendar.FromParts(2024, 9, 6, 2, 30, 0, 0);
        var overlap = calendar.FromParts(2024, 3, 7, 2, 30, 0, 0);

        // Assert: the gap is read at +10 (16:30Z on the 5th), the overlap takes the earlier instant, read at +11 (15:30Z on the 6th)
        Assert.Equal(TimeTestSupport.Ms(2024, 10, 5, 16, 30), gap);
        Assert.Equal(TimeTestSupport.Ms(2024, 4, 6, 15, 30), overlap);
    }

    /// <summary>
    ///     Validates that the first and the last representable instants can be looked up in a zone with rules without an exception.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_ProbeAtYear1AndYear9999NeverThrows()
    {
        // Arrange: a zone with daylight saving time
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: offsets and parts at the edges of the range
        var atMin = calendar.UtcOffsetMilliseconds(DataValue.MinDateMilliseconds);
        var atMax = calendar.UtcOffsetMilliseconds(DataValue.MaxDateMilliseconds);
        var partsAtMin = calendar.ToParts(DataValue.MinDateMilliseconds);
        var firstWall = calendar.FromParts(1, 0, 1, 0, 0, 0, 0);
        var lastWall = calendar.FromParts(9999, 11, 31, 23, 0, 0, 0);

        // Assert: January is standard time at both edges, the wall time at the start is the last evening of year 0
        Assert.Equal(MinusFive, atMin);
        Assert.Equal(MinusFive, atMax);
        Assert.Equal((0, 11, 31, 19), (partsAtMin.Year, partsAtMin.Month, partsAtMin.Date, partsAtMin.Hours));
        Assert.Equal(DataValue.MinDateMilliseconds + 18_000_000, firstWall);
        Assert.True(double.IsNaN(lastWall));
    }

    /// <summary>
    ///     Validates that a zone calendar never consults the host's zone: the results are the same when the process zone differs.
    /// </summary>
    [Fact]
    public void TimeCalendarZone_Results_DoNotDependOnTheHostZone()
    {
        // Arrange: a zone and a summary of several conversions
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        string Summarize() => string.Join(
            ",",
            calendar.UtcOffsetMilliseconds(TimeTestSupport.Ms(2024, 7, 1)),
            calendar.FromParts(2024, 2, 10, 2, 30, 0, 0),
            calendar.Interval(TimeIntervalKind.Day).Floor(TimeTestSupport.Ms(2024, 3, 10, 12)));

        // Act: summarize twice (the host zone is whatever the machine uses; the code must not read it)
        var first = Summarize();
        var second = Summarize();

        // Assert: the summary is the hand-derived one: offset -4 h, 07:30Z on March 10, and local midnight 05:00Z
        Assert.Equal(first, second);
        Assert.Equal("-14400000,1710055800000,1710046800000", first);
    }
}
