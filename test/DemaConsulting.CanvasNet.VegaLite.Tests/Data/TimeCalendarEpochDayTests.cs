using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Tests of the day interval that counts days from the epoch (d3's <c>unixDay.every</c>, used by the two-day ticks of a UTC scale), worked out by hand:
///     January 1, 2024 is day 19,723 since 1970-01-01, an odd number.
/// </summary>
public class TimeCalendarEpochDayTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that every second epoch day is the even day numbers (January 2, 4, ...) where every second day of the month is January 1, 3, ...
    /// </summary>
    [Fact]
    public void TimeCalendar_EveryEpochDay_KeepsEvenDaysSinceTheEpoch()
    {
        // Arrange
        var day = TimeCalendar.Utc.Interval(TimeIntervalKind.Day);
        var epoch = day.EveryEpochDay(2);
        var monthDays = day.Every(2);

        // Act
        var stop = TimeTestSupport.Ms(2024, 1, 11) + 1;
        var epochDays = epoch!.Range(TimeTestSupport.Ms(2024, 1, 1), stop, 1, 100, Ct);
        var oddDays = monthDays!.Range(TimeTestSupport.Ms(2024, 1, 1), stop, 1, 100, Ct);

        // Assert: January 2, 4, 6, 8, 10 against January 1, 3, 5, 7, 9, 11
        Assert.Equal([.. new[] { 2, 4, 6, 8, 10 }.Select(d => TimeTestSupport.Ms(2024, 1, d))], epochDays);
        Assert.Equal([.. new[] { 1, 3, 5, 7, 9, 11 }.Select(d => TimeTestSupport.Ms(2024, 1, d))], oddDays);
    }

    /// <summary>
    ///     Validates floor, ceil and offset of the epoch-day filter, across a month end and before the epoch.
    /// </summary>
    [Fact]
    public void TimeCalendar_EveryEpochDay_FloorCeilOffset()
    {
        // Arrange
        var epoch = TimeCalendar.Utc.Interval(TimeIntervalKind.Day).EveryEpochDay(2)!;

        // Act / Assert: January 3 (day 19,725, odd) floors to January 2 and ceils to January 4
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 2), epoch.Floor(TimeTestSupport.Ms(2024, 1, 3, 12)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 4), epoch.Ceil(TimeTestSupport.Ms(2024, 1, 3, 12)));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 2), epoch.Floor(TimeTestSupport.Ms(2024, 1, 2)));

        // Assert: the days run on across a month end (January 30 is day 19,752, February 1 is 19,754)
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 1), epoch.Offset(TimeTestSupport.Ms(2024, 1, 30), 1));

        // Assert: day 0 is even, and negative days follow the sign of JavaScript's remainder: the day -1 (December 31, 1969) is not kept, day -2 is
        Assert.Equal(TimeTestSupport.Ms(1969, 12, 30), epoch.Floor(TimeTestSupport.Ms(1969, 12, 31)));
        Assert.Equal(0, epoch.Floor(TimeTestSupport.Ms(1970, 1, 1, 23)));
    }

    /// <summary>
    ///     Validates the cases with no interval: a step of one is the plain day, other steps that are not positive whole numbers, above 64, a calendar that is not UTC and
    ///     intervals that are not days.
    /// </summary>
    [Fact]
    public void TimeCalendar_EveryEpochDay_RejectsWhatItCannotDo()
    {
        // Arrange
        var utcDay = TimeCalendar.Utc.Interval(TimeIntervalKind.Day);
        var zoneDay = TimeCalendar.Create(TimeTestSupport.UsLike()).Interval(TimeIntervalKind.Day);

        // Act / Assert: a step of 1 (or a fraction that rounds to it) is the day itself
        Assert.Same(utcDay, utcDay.EveryEpochDay(1));
        Assert.Same(utcDay, utcDay.EveryEpochDay(1.5));

        // Assert: nothing for a step that is not positive, not finite or beyond the bound
        Assert.Null(utcDay.EveryEpochDay(0));
        Assert.Null(utcDay.EveryEpochDay(0.9));
        Assert.Null(utcDay.EveryEpochDay(-2));
        Assert.Null(utcDay.EveryEpochDay(double.NaN));
        Assert.Null(utcDay.EveryEpochDay(double.PositiveInfinity));
        Assert.Null(utcDay.EveryEpochDay(65));
        Assert.NotNull(utcDay.EveryEpochDay(64));

        // Assert: only the UTC calendar and the plain day interval
        Assert.Null(zoneDay.EveryEpochDay(2));
        Assert.Null(TimeCalendar.Utc.Interval(TimeIntervalKind.Hour).EveryEpochDay(2));
        Assert.Null(utcDay.Every(2)!.EveryEpochDay(2));
        Assert.Null(utcDay.EveryEpochDay(2)!.EveryEpochDay(2));
    }
}
