using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of <see cref="TimeInterval"/>: the d3-time floor, ceil, round, offset, count, every and range of the
///     millisecond to the year in UTC and in zones. The expected instants are worked out by hand from the definition of each interval.
/// </summary>
public class TimeCalendarIntervalTests
{
    /// <summary>The number of milliseconds in an hour.</summary>
    private const double Hour = 3_600_000;

    /// <summary>The number of milliseconds in a day.</summary>
    private const double Day = 86_400_000;

    /// <summary>
    ///     Gets an interval of the UTC calendar.
    /// </summary>
    /// <param name="kind">The kind of interval.</param>
    /// <returns>The interval.</returns>
    private static TimeInterval Utc(TimeIntervalKind kind) => TimeCalendar.Utc.Interval(kind);

    /// <summary>
    ///     Gets an interval of a zone calendar.
    /// </summary>
    /// <param name="zone">The zone.</param>
    /// <param name="kind">The kind of interval.</param>
    /// <returns>The interval.</returns>
    private static TimeInterval Zoned(TimeZoneInfo zone, TimeIntervalKind kind) => TimeTestSupport.Calendar(zone).Interval(kind);

    /// <summary>
    ///     Validates that a day is 23 hours across the spring change: local midnight March 10 is 05:00Z and the next local midnight is 04:00Z.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Day_AcrossSpringForwardIs23Hours()
    {
        // Arrange: the day interval of a United States like zone
        var day = Zoned(TimeTestSupport.UsLike(), TimeIntervalKind.Day);

        // Act: the start of the day of noon on March 10 and the start of the next day
        var start = day.Floor(TimeTestSupport.Ms(2024, 3, 10, 12));
        var end = day.Offset(start, 1);

        // Assert: 05:00Z to 04:00Z the next day
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10, 5), start);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 11, 4), end);
        Assert.Equal(23 * Hour, end - start);
    }

    /// <summary>
    ///     Validates that a day is 25 hours across the autumn change: local midnight November 3 is 04:00Z and the next is 05:00Z.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Day_FallBackIs25Hours()
    {
        // Arrange: the day interval of a United States like zone
        var day = Zoned(TimeTestSupport.UsLike(), TimeIntervalKind.Day);

        // Act: the start of the day of noon on November 3 and the start of the next day
        var start = day.Floor(TimeTestSupport.Ms(2024, 11, 3, 12));
        var end = day.Offset(start, 1);

        // Assert: 04:00Z to 05:00Z the next day
        Assert.Equal(TimeTestSupport.Ms(2024, 11, 3, 4), start);
        Assert.Equal(TimeTestSupport.Ms(2024, 11, 4, 5), end);
        Assert.Equal(25 * Hour, end - start);
    }

    /// <summary>
    ///     Validates that counting days across the spring change counts calendar days: noon March 9 to noon March 12 is 71 hours of elapsed time but 3 days.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Day_CountAcrossSpringForwardCountsCalendarDays()
    {
        // Arrange: the day interval of a United States like zone
        var day = Zoned(TimeTestSupport.UsLike(), TimeIntervalKind.Day);

        // Act: count the days between two noons
        var count = day.Count(TimeTestSupport.Ms(2024, 3, 9, 17), TimeTestSupport.Ms(2024, 3, 12, 16));

        // Assert: three calendar days
        Assert.Equal(3.0, count);
    }

    /// <summary>
    ///     Validates the hour of a half-hour zone: 10:20Z is local 15:50 at +05:30, so the hour starts at local 15:00, which is 09:30Z.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Hour_FloorIsLocalHourInAHalfHourZone()
    {
        // Arrange: the hour interval of a +05:30 zone
        var hour = Zoned(TimeTestSupport.Fixed(5, 30), TimeIntervalKind.Hour);
        var instant = TimeTestSupport.Ms(2024, 5, 1, 10, 20);

        // Act: floor, ceil and offset
        var floor = hour.Floor(instant);
        var ceil = hour.Ceil(instant);
        var next = hour.Offset(floor, 1);

        // Assert: the boundaries are at half past the UTC hour
        Assert.Equal(TimeTestSupport.Ms(2024, 5, 1, 9, 30), floor);
        Assert.Equal(TimeTestSupport.Ms(2024, 5, 1, 10, 30), ceil);
        Assert.Equal(ceil, next);
    }

    /// <summary>
    ///     Validates the time-of-day intervals in UTC: floor of 10:20:30.400.
    /// </summary>
    [Theory]
    [InlineData("Millisecond", 10, 20, 30, 400)]
    [InlineData("Second", 10, 20, 30, 0)]
    [InlineData("Minute", 10, 20, 0, 0)]
    [InlineData("Hour", 10, 0, 0, 0)]
    [InlineData("Day", 0, 0, 0, 0)]
    public void TimeCalendarInterval_TimeOfDay_FloorInUtc(string kindName, int hour, int minute, int second, int millisecond)
    {
        // Arrange: an instant with every time component set
        var instant = TimeTestSupport.Ms(2024, 5, 1, 10, 20, 30, 400);

        // Act: floor it
        var floor = Utc(Enum.Parse<TimeIntervalKind>(kindName)).Floor(instant);

        // Assert: the components below the interval are cleared
        Assert.Equal(TimeTestSupport.Ms(2024, 5, 1, hour, minute, second, millisecond), floor);
    }

    /// <summary>
    ///     Validates that the week starts on Sunday: the week of Wednesday March 6 2024 starts on Sunday March 3, and weeks are counted from the epoch week.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Week_IsSundayBasedAndCountsSundays()
    {
        // Arrange: the week interval in UTC
        var week = Utc(TimeIntervalKind.Week);

        // Act / Assert: floors around the start of a week
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 3), week.Floor(TimeTestSupport.Ms(2024, 3, 6, 12)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 3), week.Floor(TimeTestSupport.Ms(2024, 3, 3)));
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 25), week.Floor(TimeTestSupport.Ms(2024, 3, 2, 23, 59, 59, 999)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10), week.Ceil(TimeTestSupport.Ms(2024, 3, 6, 12)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 13, 12), week.Offset(TimeTestSupport.Ms(2024, 3, 6, 12), 1));

        // Assert: counts of Sundays; January 4 1970 is the first Sunday after the epoch
        Assert.Equal(2.0, week.Count(TimeTestSupport.Ms(2024, 3, 3), TimeTestSupport.Ms(2024, 3, 17, 5)));
        Assert.Equal(1.0, week.Count(0, TimeTestSupport.Ms(1970, 1, 4)));
    }

    /// <summary>
    ///     Validates the month interval: offsets keep the day (and roll over, January 31 plus a month is March 2 in 2024), roll the year, and count month differences.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Month_OffsetClampsNothingAndRollsYear()
    {
        // Arrange: the month interval in UTC
        var month = Utc(TimeIntervalKind.Month);

        // Act / Assert: roll-over, year change in both directions, floor, ceil, round and count
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 2), month.Offset(TimeTestSupport.Ms(2024, 1, 31), 1));
        Assert.Equal(TimeTestSupport.Ms(2025, 1, 15, 6), month.Offset(TimeTestSupport.Ms(2024, 12, 15, 6), 1));
        Assert.Equal(TimeTestSupport.Ms(2023, 12, 15), month.Offset(TimeTestSupport.Ms(2024, 1, 15), -1));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 1), month.Floor(TimeTestSupport.Ms(2024, 3, 17, 10, 20)));
        Assert.Equal(TimeTestSupport.Ms(2024, 4, 1), month.Ceil(TimeTestSupport.Ms(2024, 3, 17, 10, 20)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 1), month.Round(TimeTestSupport.Ms(2024, 3, 10)));
        Assert.Equal(TimeTestSupport.Ms(2024, 4, 1), month.Round(TimeTestSupport.Ms(2024, 3, 25)));
        Assert.Equal(4.0, month.Count(TimeTestSupport.Ms(2023, 11, 20), TimeTestSupport.Ms(2024, 3, 2)));
    }

    /// <summary>
    ///     Validates the year interval and its closed form: <c>every(10)</c> floors 2024 to 2020, offsets by decades and has neither count nor every.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Year_EveryKFloorsToMultiples()
    {
        // Arrange: the year interval and its decade form
        var year = Utc(TimeIntervalKind.Year);
        var decade = year.Every(10)!;
        var instant = TimeTestSupport.Ms(2024, 5, 5);

        // Act / Assert: plain year
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), year.Floor(instant));
        Assert.Equal(TimeTestSupport.Ms(2025, 3, 1), year.Offset(TimeTestSupport.Ms(2024, 2, 29), 1));
        Assert.Equal(3.0, year.Count(TimeTestSupport.Ms(2021, 6, 1), TimeTestSupport.Ms(2024, 1, 1)));

        // Assert: decade form
        Assert.Equal(TimeTestSupport.Ms(2020, 1, 1), decade.Floor(instant));
        Assert.Equal(TimeTestSupport.Ms(2030, 1, 1), decade.Ceil(instant));
        Assert.Equal(TimeTestSupport.Ms(2030, 1, 1), decade.Offset(TimeTestSupport.Ms(2020, 1, 1), 1));
        Assert.Equal(TimeTestSupport.Ms(2010, 1, 1), decade.Offset(TimeTestSupport.Ms(2020, 1, 1), -1));
        Assert.True(double.IsNaN(decade.Count(0, instant)));
        Assert.Null(decade.Every(2));
    }

    /// <summary>
    ///     Validates the millisecond closed form: <c>every(100)</c> floors to hundreds, offsets by hundreds and counts hundreds.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Millisecond_EveryKFloorsToMultiples()
    {
        // Arrange: the 100 ms form
        var hundreds = Utc(TimeIntervalKind.Millisecond).Every(100)!;

        // Act / Assert: 1,709,210,096,789 floors to ...,700 and ceils to ...,800
        Assert.Equal(1_709_210_096_700.0, hundreds.Floor(TimeTestSupport.AnchorMilliseconds));
        Assert.Equal(1_709_210_096_800.0, hundreds.Ceil(TimeTestSupport.AnchorMilliseconds));
        Assert.Equal(1_709_210_096_900.0, hundreds.Offset(1_709_210_096_700.0, 2));
        Assert.Equal(10.0, hundreds.Count(0, 1000));
        Assert.Equal(-100.0, hundreds.Floor(-1));
    }

    /// <summary>
    ///     Validates that steps that are not finite or not above zero give no interval, and a step of 1 or a fraction below 2 gives the interval itself.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Every_NonPositiveOrNaNIsNull()
    {
        // Arrange: the hour interval
        var hour = Utc(TimeIntervalKind.Hour);

        // Act / Assert: no interval for 0, negative, NaN, infinite and fractions below 1
        Assert.Null(hour.Every(0));
        Assert.Null(hour.Every(-3));
        Assert.Null(hour.Every(double.NaN));
        Assert.Null(hour.Every(double.PositiveInfinity));
        Assert.Null(hour.Every(0.5));
        Assert.Same(hour, hour.Every(1));
        Assert.Same(hour, hour.Every(1.9));
    }

    /// <summary>
    ///     Validates the filter intervals against their definition (the boundaries whose field is a multiple of the step): hour.every(3).
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Every_HoursByThree()
    {
        // Arrange: hour.every(3) keeps the hours 0, 3, 6, ...
        var every = Utc(TimeIntervalKind.Hour).Every(3)!;

        // Act / Assert: floor, ceil, offsets in both directions and round
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 9), every.Floor(TimeTestSupport.Ms(2024, 1, 1, 10, 30)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 12), every.Ceil(TimeTestSupport.Ms(2024, 1, 1, 10, 30)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 9), every.Ceil(TimeTestSupport.Ms(2024, 1, 1, 9)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), every.Floor(TimeTestSupport.Ms(2024, 1, 1, 1)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 12), every.Offset(TimeTestSupport.Ms(2024, 1, 1, 9), 1));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 6), every.Offset(TimeTestSupport.Ms(2024, 1, 1, 9), -1));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 18), every.Offset(TimeTestSupport.Ms(2024, 1, 1, 9), 3));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 2), every.Offset(TimeTestSupport.Ms(2024, 1, 1, 22), 1));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 12), every.Round(TimeTestSupport.Ms(2024, 1, 1, 10, 30)));
    }

    /// <summary>
    ///     Validates the minute and second filter intervals: minute.every(15) and second.every(15).
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Every_MinutesAndSecondsByFifteen()
    {
        // Arrange: the two filter intervals
        var minutes = Utc(TimeIntervalKind.Minute).Every(15)!;
        var seconds = Utc(TimeIntervalKind.Second).Every(15)!;

        // Act / Assert: minutes
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 10), minutes.Floor(TimeTestSupport.Ms(2024, 1, 1, 10, 7, 30)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 10, 15), minutes.Ceil(TimeTestSupport.Ms(2024, 1, 1, 10, 7, 30)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 11), minutes.Offset(TimeTestSupport.Ms(2024, 1, 1, 10, 45), 1));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 10, 30), minutes.Offset(TimeTestSupport.Ms(2024, 1, 1, 10), 2));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 9, 45), minutes.Offset(TimeTestSupport.Ms(2024, 1, 1, 10), -1));

        // Assert: seconds
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 10, 0, 45), seconds.Floor(TimeTestSupport.Ms(2024, 1, 1, 10, 0, 47, 500)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 10, 1), seconds.Ceil(TimeTestSupport.Ms(2024, 1, 1, 10, 0, 47, 500)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 10, 1), seconds.Offset(TimeTestSupport.Ms(2024, 1, 1, 10, 0, 45), 1));
    }

    /// <summary>
    ///     Validates the day and month filter intervals: day.every(2) keeps the 1st, 3rd, 5th, ... and month.every(3) keeps January, April, July and October.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Every_DaysByTwoAndMonthsByThree()
    {
        // Arrange: the two filter intervals
        var days = Utc(TimeIntervalKind.Day).Every(2)!;
        var months = Utc(TimeIntervalKind.Month).Every(3)!;

        // Act / Assert: days (the field is the date minus one, so the odd dates are kept)
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 3), days.Floor(TimeTestSupport.Ms(2024, 1, 4, 12)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 5), days.Ceil(TimeTestSupport.Ms(2024, 1, 4, 12)));
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 1), days.Offset(TimeTestSupport.Ms(2024, 1, 31), 1));
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 3), days.Offset(TimeTestSupport.Ms(2024, 2, 1), 1));
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 1), days.Floor(TimeTestSupport.Ms(2024, 2, 2)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), days.Offset(TimeTestSupport.Ms(2024, 1, 3), -1));
        Assert.Equal(TimeTestSupport.Ms(2023, 12, 31), days.Offset(TimeTestSupport.Ms(2024, 1, 1), -1));

        // Assert: months
        Assert.Equal(TimeTestSupport.Ms(2024, 4, 1), months.Floor(TimeTestSupport.Ms(2024, 5, 15)));
        Assert.Equal(TimeTestSupport.Ms(2024, 7, 1), months.Ceil(TimeTestSupport.Ms(2024, 5, 15)));
        Assert.Equal(TimeTestSupport.Ms(2024, 7, 1), months.Offset(TimeTestSupport.Ms(2024, 4, 1), 1));
        Assert.Equal(TimeTestSupport.Ms(2023, 10, 1), months.Offset(TimeTestSupport.Ms(2024, 1, 1), -1));
        Assert.Equal(TimeTestSupport.Ms(2025, 1, 1), months.Offset(TimeTestSupport.Ms(2024, 10, 1), 1));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), months.Floor(TimeTestSupport.Ms(2024, 3, 31)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), months.Ceil(TimeTestSupport.Ms(2024, 1, 1)));
    }

    /// <summary>
    ///     Validates that week.every(2) keeps every other week counted from the epoch: the floor is a Sunday midnight, an even number of weeks after the epoch week, and within two weeks of the instant.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Every_WeeksByTwoKeepsEveryOtherEpochWeek()
    {
        // Arrange: the plain week interval (for counting) and the filtered one
        var week = Utc(TimeIntervalKind.Week);
        var every = week.Every(2)!;

        for (var k = 0; k < 30; k++)
        {
            // Act: floor a Wednesday noon, k weeks after January 3 2024
            var instant = TimeTestSupport.Ms(2024, 1, 3, 12) + (k * 7 * Day);
            var floor = every.Floor(instant);

            // Assert: a week boundary, not after the instant, less than two weeks before it, on an even week count
            Assert.Equal(floor, week.Floor(floor));
            Assert.True(floor <= instant && instant - floor < 14 * Day);
            Assert.Equal(0.0, week.Count(0, floor) % 2);
        }
    }

    /// <summary>
    ///     Validates the bounds on filter intervals: a week step above 64 has no interval, an offset of more than 64 steps is NaN, and a step beyond the field is clamped to the field.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Every_IsBounded()
    {
        // Arrange: the intervals
        var weeks = Utc(TimeIntervalKind.Week);
        var hours = Utc(TimeIntervalKind.Hour);
        var instant = TimeTestSupport.Ms(2024, 1, 1, 10);

        // Act / Assert: week steps up to 64 are accepted, 65 is not
        Assert.NotNull(weeks.Every(64));
        Assert.Null(weeks.Every(65));

        // Assert: every(1000) hours keeps only hour 0 (a step beyond the 24 values of the field is the same as 24)
        var huge = hours.Every(1000)!;
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), huge.Floor(instant));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 2), huge.Ceil(instant));

        // Assert: 64 steps are fine, 65 are NaN
        var threes = hours.Every(3)!;
        Assert.False(double.IsNaN(threes.Offset(instant, 64)));
        Assert.True(double.IsNaN(threes.Offset(instant, 65)));
        Assert.True(double.IsNaN(threes.Offset(instant, -65)));
    }

    /// <summary>
    ///     Validates that the offset rounds a fractional step down (JavaScript's floor) and that zero steps change nothing.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Offset_FloorsFractionalSteps()
    {
        // Arrange: the hour interval and an instant
        var hour = Utc(TimeIntervalKind.Hour);
        var instant = TimeTestSupport.Ms(2024, 1, 1, 10, 20);

        // Act / Assert: 1.9 is one hour, -0.5 is minus one hour, 0 is nothing
        Assert.Equal(instant + Hour, hour.Offset(instant, 1.9));
        Assert.Equal(instant - Hour, hour.Offset(instant, -0.5));
        Assert.Equal(instant, hour.Offset(instant, 0));
        Assert.True(double.IsNaN(hour.Offset(instant, double.NaN)));
        Assert.True(double.IsNaN(hour.Offset(instant, double.PositiveInfinity)));
    }

    /// <summary>
    ///     Validates round: the nearer boundary wins and the later one wins a tie.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Round_PicksTheNearerBoundary()
    {
        // Arrange: the hour interval
        var hour = Utc(TimeIntervalKind.Hour);

        // Act / Assert: 10:29 rounds down, 10:30 and 10:31 round up
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 10), hour.Round(TimeTestSupport.Ms(2024, 1, 1, 10, 29)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 11), hour.Round(TimeTestSupport.Ms(2024, 1, 1, 10, 30)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 11), hour.Round(TimeTestSupport.Ms(2024, 1, 1, 10, 31)));
    }

    /// <summary>
    ///     Validates that a range runs from the first boundary at or after the start up to but excluding the stop, like the d3 documentation example (days from January 1 to January 4 are three dates).
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Range_IsCeilStartToExclusiveStop()
    {
        // Arrange: the day interval
        var day = Utc(TimeIntervalKind.Day);

        // Act: four ranges
        var three = day.Range(TimeTestSupport.Ms(2024, 1, 1), TimeTestSupport.Ms(2024, 1, 4), 1, 100, CancellationToken.None);
        var fromNoon = day.Range(TimeTestSupport.Ms(2024, 1, 1, 12), TimeTestSupport.Ms(2024, 1, 4), 1, 100, CancellationToken.None);
        var byTwo = day.Range(TimeTestSupport.Ms(2024, 1, 1), TimeTestSupport.Ms(2024, 1, 8), 2, 100, CancellationToken.None);
        var months = Utc(TimeIntervalKind.Month).Range(TimeTestSupport.Ms(2024, 1, 15), TimeTestSupport.Ms(2025, 1, 1), 1, 100, CancellationToken.None);

        // Assert: three days, two days starting January 2, four days, eleven months
        Assert.Equal([TimeTestSupport.Ms(2024, 1, 1), TimeTestSupport.Ms(2024, 1, 2), TimeTestSupport.Ms(2024, 1, 3)], three);
        Assert.Equal([TimeTestSupport.Ms(2024, 1, 2), TimeTestSupport.Ms(2024, 1, 3)], fromNoon);
        Assert.Equal([TimeTestSupport.Ms(2024, 1, 1), TimeTestSupport.Ms(2024, 1, 3), TimeTestSupport.Ms(2024, 1, 5), TimeTestSupport.Ms(2024, 1, 7)], byTwo);
        Assert.Equal(11, months.Count);
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 1), months[0]);
        Assert.Equal(TimeTestSupport.Ms(2024, 12, 1), months[^1]);
    }

    /// <summary>
    ///     Validates the empty ranges: start not before stop, a step below one, and NaN arguments.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Range_EmptyCases()
    {
        // Arrange: the day interval and two instants
        var day = Utc(TimeIntervalKind.Day);
        var early = TimeTestSupport.Ms(2024, 1, 1);
        var late = TimeTestSupport.Ms(2024, 1, 4);

        // Act / Assert: nothing for each degenerate request
        Assert.Empty(day.Range(late, early, 1, 100, CancellationToken.None));
        Assert.Empty(day.Range(early, early, 1, 100, CancellationToken.None));
        Assert.Empty(day.Range(early, late, 0, 100, CancellationToken.None));
        Assert.Empty(day.Range(early, late, 0.5, 100, CancellationToken.None));
        Assert.Empty(day.Range(double.NaN, late, 1, 100, CancellationToken.None));
        Assert.Empty(day.Range(early, double.NaN, 1, 100, CancellationToken.None));
    }

    /// <summary>
    ///     Validates that a day range in a zone follows the local midnights: 05:00Z, 05:00Z and 04:00Z across the spring change.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Range_DaysInAZoneFollowLocalMidnight()
    {
        // Arrange: the day interval of a United States like zone
        var day = Zoned(TimeTestSupport.UsLike(), TimeIntervalKind.Day);

        // Act: the local midnights from March 9 00:00 (05:00Z) up to excluding March 12 00:00 (04:00Z)
        var range = day.Range(TimeTestSupport.Ms(2024, 3, 9, 5), TimeTestSupport.Ms(2024, 3, 12, 4), 1, 100, CancellationToken.None);

        // Assert: three midnights
        Assert.Equal([TimeTestSupport.Ms(2024, 3, 9, 5), TimeTestSupport.Ms(2024, 3, 10, 5), TimeTestSupport.Ms(2024, 3, 11, 4)], range);
    }

    /// <summary>
    ///     Validates the cap on the result: a range that fits exactly succeeds, one item more throws with the limit.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Range_ThrowsAtMaxCount()
    {
        // Arrange: the day interval and a three-day range
        var day = Utc(TimeIntervalKind.Day);
        var start = TimeTestSupport.Ms(2024, 1, 1);
        var stop = TimeTestSupport.Ms(2024, 1, 4);

        // Act / Assert: three items fit in three, not in two
        Assert.Equal(3, day.Range(start, stop, 1, 3, CancellationToken.None).Count);
        var exception = Assert.Throws<TimeRangeLimitException>(() => day.Range(start, stop, 1, 2, CancellationToken.None));
        Assert.Equal(2, exception.Count);

        // Assert: a huge request stops at the cap instead of allocating
        var hours = Utc(TimeIntervalKind.Hour);
        var huge = Assert.Throws<TimeRangeLimitException>(() => hours.Range(0, DataValue.MaxDateMilliseconds, 1, 1000, CancellationToken.None));
        Assert.Equal(1000, huge.Count);
    }

    /// <summary>
    ///     Validates that a range polls the cancellation token while it grows.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Range_PollsCancellation()
    {
        // Arrange: a token that is already cancelled and a range of a million milliseconds
        using var source = new CancellationTokenSource();
        source.Cancel();
        var milliseconds = Utc(TimeIntervalKind.Millisecond);

        // Act / Assert: the range stops with a cancellation, not with the limit
        Assert.Throws<OperationCanceledException>(() => milliseconds.Range(0, 1_000_000, 1, 2_000_000, source.Token));
    }

    /// <summary>
    ///     Validates that the interval objects of a zone calendar that is in winter agree with the UTC ones: a zone with a zero base offset and daylight saving rules, from January 1 to January 20.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Utc_And_Zone_Intervals_Agree_ForAZoneAtOffsetZero()
    {
        // Arrange: a zone at base offset zero whose daylight saving time does not start before March, and no operation reaches that far
        var zone = TimeTestSupport.Calendar(TimeTestSupport.UsLike(TimeSpan.Zero));
        Assert.False(zone.IsUtc);

        for (var hours = 0; hours < 20 * 24; hours += 7)
        {
            var instant = TimeTestSupport.Ms(2024, 1, 1) + (hours * Hour) + 123_456;
            foreach (var kind in Enum.GetValues<TimeIntervalKind>())
            {
                // Act: the same operations in both calendars
                var utc = TimeCalendar.Utc.Interval(kind);
                var zoned = zone.Interval(kind);

                // Assert: the same answers
                Assert.Equal(utc.Floor(instant), zoned.Floor(instant));
                Assert.Equal(utc.Ceil(instant), zoned.Ceil(instant));
                Assert.Equal(utc.Offset(instant, 1), zoned.Offset(instant, 1));
                Assert.Equal(utc.Count(instant, instant + (10 * Day)), zoned.Count(instant, instant + (10 * Day)));
            }
        }
    }

    /// <summary>
    ///     Validates that a calendar hands out one interval object per kind.
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_Interval_IsCachedPerKind()
    {
        // Arrange / Act: ask twice
        var first = TimeCalendar.Utc.Interval(TimeIntervalKind.Week);
        var second = TimeCalendar.Utc.Interval(TimeIntervalKind.Week);

        // Assert: the same object, with its kind and calendar
        Assert.Same(first, second);
        Assert.Equal(TimeIntervalKind.Week, first.Kind);
        Assert.Same(TimeCalendar.Utc, first.Calendar);
    }

    /// <summary>
    ///     Validates the accepted interval names: the d3 names, the Vega time-unit names, the <c>utc</c> forms and the case.
    /// </summary>
    [Theory]
    [InlineData("millisecond", "Millisecond", 1, false)]
    [InlineData("milliseconds", "Millisecond", 1, false)]
    [InlineData("second", "Second", 1, false)]
    [InlineData("seconds", "Second", 1, false)]
    [InlineData("minute", "Minute", 1, false)]
    [InlineData("minutes", "Minute", 1, false)]
    [InlineData("hour", "Hour", 1, false)]
    [InlineData("hours", "Hour", 1, false)]
    [InlineData("day", "Day", 1, false)]
    [InlineData("date", "Day", 1, false)]
    [InlineData("dayofyear", "Day", 1, false)]
    [InlineData("week", "Week", 1, false)]
    [InlineData("month", "Month", 1, false)]
    [InlineData("quarter", "Month", 3, false)]
    [InlineData("year", "Year", 1, false)]
    [InlineData("utcDay", "Day", 1, true)]
    [InlineData("utcQuarter", "Month", 3, true)]
    [InlineData("UTCYear", "Year", 1, true)]
    [InlineData("MINUTE", "Minute", 1, false)]
    public void TimeCalendarInterval_TryParseName_AcceptsTheDocumentedAndTheCodedSpellings(string name, string kindName, double multiplier, bool utc)
    {
        // Arrange / Act: read the name
        var ok = TimeInterval.TryParseName(name, out var actualKind, out var actualMultiplier, out var actualUtc);

        // Assert: the kind, the multiplier and the utc flag
        Assert.True(ok);
        Assert.Equal((Enum.Parse<TimeIntervalKind>(kindName), multiplier, utc), (actualKind, actualMultiplier, actualUtc));
    }

    /// <summary>
    ///     Validates that unknown interval names are rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("utc")]
    [InlineData("fortnight")]
    [InlineData("isoWeek")]
    [InlineData("days")]
    public void TimeCalendarInterval_TryParseName_RejectsUnknownNames(string name)
    {
        // Arrange / Act: read the name
        var ok = TimeInterval.TryParseName(name, out _, out _, out var utc);

        // Assert: rejected, with no utc flag
        Assert.False(ok);
        Assert.False(utc);
    }

    /// <summary>
    ///     Validates that interval names are read the same under a Turkish and a German culture (the Turkish upper-case I must not break "MINUTE").
    /// </summary>
    [Fact]
    public void TimeCalendarInterval_TryParseName_IsCultureIndependent()
    {
        // Arrange: read a name that contains the letter I
        static string Read()
        {
            var ok = TimeInterval.TryParseName("MINUTES", out var kind, out _, out _);
            return $"{ok}:{kind}:{Utc(TimeIntervalKind.Minute).Floor(TimeTestSupport.Ms(2024, 1, 1, 10, 20, 30))}";
        }

        // Act: under the three cultures
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, Read);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", Read);
        var german = ExpressionTestSupport.UnderCulture("de-DE", Read);

        // Assert: the same text, and the name was accepted
        Assert.StartsWith("True:Minute:", invariant, StringComparison.Ordinal);
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }
}
