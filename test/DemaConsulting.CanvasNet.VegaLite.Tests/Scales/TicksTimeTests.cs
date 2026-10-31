using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the ticks and <c>nice</c> boundaries of time scales: the d3 interval table with its nearest-neighbor rule, inclusive stops, reversed
///     domains, local and UTC calendars (including days across a daylight saving change), explicit intervals, <c>nice</c> and the limits. The expected
///     ticks are listed by hand from the calendar; the instants are computed with the base class library.
/// </summary>
public class TicksTimeTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Creates the instants of dates in 2024 (UTC).
    /// </summary>
    /// <param name="month">The month from 1.</param>
    /// <param name="day">The day.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <returns>The milliseconds since the epoch.</returns>
    private static double D(int month, int day, int hour = 0, int minute = 0) => TimeTestSupport.Ms(2024, month, day, hour, minute);

    /// <summary>
    ///     Generates the ticks of a time scale over a domain.
    /// </summary>
    /// <param name="start">The start of the domain.</param>
    /// <param name="stop">The end of the domain.</param>
    /// <param name="request">The request.</param>
    /// <param name="calendar">The calendar, or null for UTC.</param>
    /// <param name="utc">Whether the scale is a UTC scale.</param>
    /// <param name="maxTicks">The tick limit.</param>
    /// <returns>The ticks.</returns>
    private static double[] TickValues(double start, double stop, TimeTickRequest request, TimeCalendar? calendar = null, bool utc = false, int maxTicks = 1_000)
    {
        var model = ScaleModel.CreateTime([start, stop], [0, 400], false, false, calendar ?? TimeCalendar.Utc, utc);
        return Ticks.TimeTicks(model, request, maxTicks, Ct);
    }

    /// <summary>
    ///     Validates the d3 table: the interval whose length is nearest the target (<c>|stop - start| / count</c>) by the ratio rule, whole milliseconds below a
    ///     second and whole years beyond a year, each checked by its kind and by the number of ticks it gives (the stop is inclusive).
    /// </summary>
    [Fact]
    public void Ticks_TimeTickInterval_FollowsD3Table()
    {
        // Arrange: (label, start, stop, count, expected interval, expected number of ticks); every row is derived by hand in the comment
        var rows = new (string Label, double Start, double Stop, double Count, TimeIntervalKind Kind, int Ticks)[]
        {
            // 10 days / 10 = 1 day: the table has 1 day (ratio 1 against 2): January 1 to 11 inclusive
            ("1 day", D(1, 1), D(1, 11), 10, TimeIntervalKind.Day, 11),

            // 366 days / 10 = 36.6 days: between 1 month (30 d) and 3 months (90 d); 36.6 / 30 = 1.22 < 90 / 36.6 = 2.46, so 1 month: 13 boundaries
            ("1 month", D(1, 1), TimeTestSupport.Ms(2025, 1, 1), 10, TimeIntervalKind.Month, 13),

            // 1000 ms / 10 = 100 ms is below a second: whole milliseconds by tickStep (100): 0, 100, ..., 1000
            ("100 ms", 0, 1000, 10, TimeIntervalKind.Millisecond, 11),

            // 100 years / 10 = 10 years is beyond the 1 year entry: whole years by tickStep over the years (10): 2000, 2010, ..., 2100
            ("10 years", TimeTestSupport.Ms(2000, 1, 1), TimeTestSupport.Ms(2100, 1, 1), 10, TimeIntervalKind.Year, 11),

            // 30 minutes / 10 = 3 min: 3 / 1 = 3 is not below 5 / 3 = 1.67, so 5 minutes: 00:00, 00:05, ..., 00:30
            ("5 minutes by ratio", D(1, 1), D(1, 1, 0, 30), 10, TimeIntervalKind.Minute, 7),

            // 100 minutes / 10 = 10 min: 10 / 5 = 2 is not below 15 / 10 = 1.5, so 15 minutes: 00:00, 00:15, ..., 01:30
            ("15 minutes by ratio", D(1, 1), D(1, 1, 1, 40), 10, TimeIntervalKind.Minute, 7),

            // 50 minutes / 10 = 5 min exactly: the 5 minute entry (ratio 1): 11 ticks
            ("5 minutes exactly", D(1, 1), D(1, 1, 0, 50), 10, TimeIntervalKind.Minute, 11),

            // 42 days / 6 = 7 days: the week entry; the Sundays January 7, 14, 21, 28, February 4 and 11
            ("1 week", D(1, 1), D(2, 12), 6, TimeIntervalKind.Week, 6),

            // 20 days / 10 = 2 days: every second day of the month, January 1, 3, ..., 21 (a local calendar counts the day of the month)
            ("2 days", D(1, 1), D(1, 21), 10, TimeIntervalKind.Day, 11),
        };

        // Act / Assert
        foreach (var row in rows)
        {
            var interval = Ticks.TimeTickInterval(row.Start, row.Stop, row.Count, TimeCalendar.Utc);
            Assert.NotNull(interval);
            Assert.True(row.Kind == interval.Kind, row.Label);
            Assert.True(row.Ticks == interval.Range(row.Start, row.Stop + 1, 1, 10_000, Ct).Count, row.Label);
        }

        // Assert: no count, no interval
        Assert.Null(Ticks.TimeTickInterval(D(1, 1), D(1, 11), 0, TimeCalendar.Utc));
        Assert.Null(Ticks.TimeTickInterval(D(1, 1), D(1, 11), -3, TimeCalendar.Utc));
        Assert.Null(Ticks.TimeTickInterval(D(1, 1), D(1, 11), double.NaN, TimeCalendar.Utc));
        Assert.Null(Ticks.TimeTickInterval(double.NaN, D(1, 11), 10, TimeCalendar.Utc));
        Assert.Throws<ArgumentNullException>(() => Ticks.TimeTickInterval(0, 1, 10, null!));
    }

    /// <summary>
    ///     Validates that the two-day ticks of a UTC scale count the days since the epoch (d3's <c>unixDay</c>: January 1, 2024 is day 19,723, odd, so the ticks are the
    ///     even days from January 2) and those of a time scale the day of the month (January 1, 3, ...).
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_TwoDays_UtcScaleCountsFromTheEpoch()
    {
        // Act
        var utc = TickValues(D(1, 1), D(1, 21), new TimeTickRequest(10), utc: true);
        var time = TickValues(D(1, 1), D(1, 21), new TimeTickRequest(10), utc: false);

        // Assert: January 2, 4, ..., 20 against January 1, 3, ..., 21
        Assert.Equal([.. Enumerable.Range(1, 10).Select(i => D(1, 2 * i))], utc);
        Assert.Equal([.. Enumerable.Range(0, 11).Select(i => D(1, 1 + (2 * i)))], time);
    }

    /// <summary>
    ///     Validates the inclusive stop: January 1 to January 11 gives the eleven midnights.
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_InclusiveStop_Utc()
    {
        // Act
        var ticks = TickValues(D(1, 1), D(1, 11), new TimeTickRequest(10));

        // Assert
        Assert.Equal([.. Enumerable.Range(1, 11).Select(day => D(1, day))], ticks);
    }

    /// <summary>
    ///     Validates that a reversed domain gives the same ticks in descending order.
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_Reversed()
    {
        // Act
        var forward = TickValues(D(1, 1), D(1, 11), new TimeTickRequest(10));
        var backward = TickValues(D(1, 11), D(1, 1), new TimeTickRequest(10));

        // Assert
        Assert.Equal(11, backward.Length);
        Assert.Equal([.. Enumerable.Reverse(forward)], backward);
    }

    /// <summary>
    ///     Validates month and day boundaries in a daylight saving zone (standard time minus five hours, daylight time from 2024-03-10T07:00Z): the ticks are
    ///     local midnights, so their instants shift by an hour, and a day across the spring-forward change is 23 hours.
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_Month_Boundaries_InAZone()
    {
        // Arrange
        var zone = TimeCalendar.Create(TimeTestSupport.UsLike());

        // Act: February 1 00:00 EST (05:00Z) to June 1 00:00 EDT (04:00Z), five ticks: 121 days less an hour, about 24.2 days each, so the month (nearer than the week)
        var months = TickValues(D(2, 1, 5), D(6, 1, 4), new TimeTickRequest(5), zone);

        // Assert: local midnights of the first of each month: EST until March 10, EDT after it
        Assert.Equal([D(2, 1, 5), D(3, 1, 5), D(4, 1, 4), D(5, 1, 4), D(6, 1, 4)], months);

        // Act: March 8 00:00 EST (05:00Z) to March 13 00:00 EDT (04:00Z), five ticks: 119 hours, 23.8 hours each, so one day (24 hours is nearer than 12)
        var days = TickValues(D(3, 8, 5), D(3, 13, 4), new TimeTickRequest(5), zone);

        // Assert: six local midnights, the day of March 10 lasting 23 hours (05:00Z to the next 04:00Z)
        Assert.Equal([D(3, 8, 5), D(3, 9, 5), D(3, 10, 5), D(3, 11, 4), D(3, 12, 4), D(3, 13, 4)], days);
        Assert.Equal(23 * 3_600_000d, days[3] - days[2]);
    }

    /// <summary>
    ///     Validates an explicit interval with a step: every second week (counting from the epoch, as d3's week filter does), the quarters of a year, and the
    ///     <c>utc</c> prefix, which uses the UTC calendar in a zone.
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_ExplicitInterval_Week_Step2()
    {
        // Arrange: the Sundays of 2024 are 2,819 weeks after December 28, 1969 on January 7 (odd), so the even weeks are January 14, 28, February 11, 25, March 10, 24
        Assert.True(TimeTickRequest.TryFromInterval("week", 2, out var everyOtherWeek));
        Assert.True(TimeTickRequest.TryFromInterval("quarter", null, out var quarters));
        Assert.True(TimeTickRequest.TryFromInterval("utcmonth", null, out var utcMonths));
        Assert.True(TimeTickRequest.TryFromInterval("month", null, out var months));

        // Act
        var weeks = TickValues(D(1, 1), D(3, 31), everyOtherWeek);
        var quarterTicks = TickValues(D(1, 1), TimeTestSupport.Ms(2025, 1, 1), quarters);
        var zone = TimeCalendar.Create(TimeTestSupport.Fixed(5, 30));
        var utcInZone = TickValues(D(1, 1), D(4, 1), utcMonths, zone);
        var localInZone = TickValues(D(1, 1), D(4, 1), months, zone);

        // Assert
        Assert.Equal([D(1, 14), D(1, 28), D(2, 11), D(2, 25), D(3, 10), D(3, 24)], weeks);
        Assert.Equal([D(1, 1), D(4, 1), D(7, 1), D(10, 1), TimeTestSupport.Ms(2025, 1, 1)], quarterTicks);
        Assert.Equal([D(1, 1), D(2, 1), D(3, 1), D(4, 1)], utcInZone);

        // Assert: local months start at 18:30Z of the day before; the one of January 1 (December 31 18:30Z) lies before the domain
        Assert.Equal([D(1, 31, 18, 30), D(2, 29, 18, 30), D(3, 31, 18, 30)], localInZone);
    }

    /// <summary>
    ///     Validates the interval request parsing: every name of the union, the quarter as three months, the step, the UTC prefix and unknown names.
    /// </summary>
    [Fact]
    public void Ticks_TimeTickRequest_TryFromInterval_ReadsNames()
    {
        // Act / Assert: the names of the documentation and of the code, case-insensitive
        foreach (var (name, kind) in new[]
        {
            ("millisecond", TimeIntervalKind.Millisecond), ("second", TimeIntervalKind.Second), ("minute", TimeIntervalKind.Minute), ("hour", TimeIntervalKind.Hour),
            ("day", TimeIntervalKind.Day), ("week", TimeIntervalKind.Week), ("month", TimeIntervalKind.Month), ("year", TimeIntervalKind.Year),
            ("hours", TimeIntervalKind.Hour), ("date", TimeIntervalKind.Day), ("MONTH", TimeIntervalKind.Month),
        })
        {
            Assert.True(TimeTickRequest.TryFromInterval(name, null, out var request), name);
            Assert.Equal(kind, request.Interval);
            Assert.Equal(0, request.Step);
            Assert.False(request.Utc);
        }

        // Assert: the step, the quarter (three months, times the step) and the UTC prefix
        Assert.True(TimeTickRequest.TryFromInterval("month", 3, out var stepped));
        Assert.Equal(3, stepped.Step);
        Assert.True(TimeTickRequest.TryFromInterval("quarter", null, out var quarter));
        Assert.Equal((TimeIntervalKind.Month, 3d), (quarter.Interval!.Value, quarter.Step));
        Assert.True(TimeTickRequest.TryFromInterval("quarter", 2, out var twoQuarters));
        Assert.Equal(6, twoQuarters.Step);
        Assert.True(TimeTickRequest.TryFromInterval("utcday", 2, out var utcDay));
        Assert.True(utcDay.Utc);
        Assert.False(TimeTickRequest.TryFromInterval("fortnight", null, out _));
        Assert.Throws<ArgumentNullException>(() => TimeTickRequest.TryFromInterval(null!, null, out _));
        Assert.Equal(10, TimeTickRequest.Default.Count);
    }

    /// <summary>
    ///     Validates that a step that gives no interval (below one, or negative: d3's <c>every</c> is null) falls back to the default of about ten ticks as Vega does,
    ///     that a count that is not positive gives no ticks, and that a degenerate domain gives its single instant.
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_DegenerateRequestsAndDomains()
    {
        // Arrange
        Assert.True(TimeTickRequest.TryFromInterval("day", 0.5, out var halfDay));
        Assert.True(TimeTickRequest.TryFromInterval("day", -2, out var negative));

        // Act: four days over ten ticks is 9.6 hours, nearer to 12 hours than to 6 (9.6 / 6 = 1.6 is not below 12 / 9.6 = 1.25): every 12 hours, nine ticks
        var fallback = TickValues(D(1, 1), D(1, 5), new TimeTickRequest(10));

        // Assert
        Assert.Equal(9, fallback.Length);
        Assert.Equal(fallback, TickValues(D(1, 1), D(1, 5), halfDay));
        Assert.Equal(fallback, TickValues(D(1, 1), D(1, 5), negative));
        Assert.Empty(TickValues(D(1, 1), D(1, 5), new TimeTickRequest(0)));

        // Assert: one instant is one tick (d3: millisecond.every(max(tickStep, 1)) of an empty span)
        Assert.Equal([D(1, 5, 6)], TickValues(D(1, 5, 6), D(1, 5, 6), new TimeTickRequest(10)));
        Assert.Empty(TickValues(double.NaN, double.NaN, new TimeTickRequest(10)));
    }

    /// <summary>
    ///     Validates the tick limit: an interval that would give more ticks than the limit (milliseconds over a year, or a huge count) is a render exception that names
    ///     the limit, and the wrong model is an argument error.
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_MaxTicksExceeded_Throws()
    {
        // Arrange
        Assert.True(TimeTickRequest.TryFromInterval("millisecond", null, out var millisecond));

        // Act
        var interval = Assert.Throws<VegaLiteRenderException>(() => TickValues(D(1, 1), TimeTestSupport.Ms(2025, 1, 1), millisecond, maxTicks: 1000));
        var count = Assert.Throws<VegaLiteRenderException>(() => TickValues(D(1, 1), TimeTestSupport.Ms(2025, 1, 1), new TimeTickRequest(1e9), maxTicks: 1000));

        // Assert: the message names the limit; the exception has no location (the axis builder adds it)
        Assert.Contains("the limit of 1000", interval.Message, StringComparison.Ordinal);
        Assert.Contains("MaxTicks", interval.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 1000", count.Message, StringComparison.Ordinal);
        Assert.False(interval.Location.IsKnown);

        // Assert: exactly the limit is accepted (eleven days against a limit of eleven)
        Assert.Equal(11, TickValues(D(1, 1), D(1, 11), new TimeTickRequest(10), maxTicks: 11).Length);
        Assert.Throws<VegaLiteRenderException>(() => TickValues(D(1, 1), D(1, 11), new TimeTickRequest(10), maxTicks: 10));

        // Assert: a model that is not a time scale is rejected
        Assert.Throws<ArgumentException>(() => Ticks.TimeTicks(ScaleModel.CreateLinear(0, 1, 0, 10), new TimeTickRequest(10), 1_000, Ct));
        Assert.Throws<ArgumentNullException>(() => Ticks.TimeTicks(null!, new TimeTickRequest(10), 1_000, Ct));
    }

    /// <summary>
    ///     Validates that the ticks poll the cancellation token.
    /// </summary>
    [Fact]
    public void Ticks_TimeTicks_PollCancellation()
    {
        // Arrange
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var model = ScaleModel.CreateTime([D(1, 1), D(1, 11)], [0, 400], false, false, TimeCalendar.Utc, false);

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => Ticks.TimeTicks(model, new TimeTickRequest(10), 1_000, canceled.Token));
    }

    /// <summary>
    ///     Validates <c>nice</c> for the count 10 over the domain January 10 13:00 to March 20 05:00 (69 days 16 hours, 6.97 days per tick: the week): the domain
    ///     extends to the Sundays January 7 and March 24; and a reversed domain keeps its direction.
    /// </summary>
    [Fact]
    public void Ticks_TimeNice_WeekExample()
    {
        // Act
        var nice = Ticks.TimeNice(D(1, 10, 13), D(3, 20, 5), TimeTickRequest.Default, TimeCalendar.Utc, false);
        var reversed = Ticks.TimeNice(D(3, 20, 5), D(1, 10, 13), TimeTickRequest.Default, TimeCalendar.Utc, false);

        // Assert
        Assert.Equal((D(1, 7), D(3, 24)), nice);
        Assert.Equal((D(3, 24), D(1, 7)), reversed);

        // Assert: a domain that is already on the boundaries stays as it is
        Assert.Equal((D(1, 7), D(3, 24)), Ticks.TimeNice(D(1, 7), D(3, 24), TimeTickRequest.Default, TimeCalendar.Utc, false));
    }

    /// <summary>
    ///     Validates <c>nice</c> with an explicit interval (a month, quarters by <c>{interval, step}</c>), a count, and the cases that leave the domain as it is.
    /// </summary>
    [Fact]
    public void Ticks_TimeNice_ExplicitMonth()
    {
        // Arrange
        Assert.True(TimeTickRequest.TryFromInterval("month", null, out var month));
        Assert.True(TimeTickRequest.TryFromInterval("month", 3, out var quarter));

        // Act / Assert: month: January 1 to April 1
        Assert.Equal((D(1, 1), D(4, 1)), Ticks.TimeNice(D(1, 10, 13), D(3, 20, 5), month, TimeCalendar.Utc, false));

        // Assert: quarters: February 10 floors to January 1 (months 0, 3, 6, 9) and May 20 ceils to July 1
        Assert.Equal((D(1, 1), D(7, 1)), Ticks.TimeNice(D(2, 10), D(5, 20), quarter, TimeCalendar.Utc, false));

        // Assert: the count 2 gives a target of 34.8 days, nearer to the month than to the quarter
        Assert.Equal((D(1, 1), D(4, 1)), Ticks.TimeNice(D(1, 10, 13), D(3, 20, 5), new TimeTickRequest(2), TimeCalendar.Utc, false));

        // Assert: no count, no interval, no change; the same for a boundary outside the years the calendar supports
        Assert.Equal((D(1, 10, 13), D(3, 20, 5)), Ticks.TimeNice(D(1, 10, 13), D(3, 20, 5), new TimeTickRequest(0), TimeCalendar.Utc, false));
        var edge = TimeTestSupport.Ms(9999, 12, 31, 12);
        Assert.Equal((D(1, 1), edge), Ticks.TimeNice(D(1, 1), edge, month, TimeCalendar.Utc, false));
        Assert.Throws<ArgumentNullException>(() => Ticks.TimeNice(0, 1, month, null!, false));
    }

    /// <summary>
    ///     Validates <c>nice</c> in a zone: the boundaries are local midnights, so the extended domain starts at the local month start.
    /// </summary>
    [Fact]
    public void Ticks_TimeNice_InAZone()
    {
        // Arrange: a month interval in the daylight saving zone
        var zone = TimeCalendar.Create(TimeTestSupport.UsLike());
        Assert.True(TimeTickRequest.TryFromInterval("month", null, out var month));

        // Act: March 15 12:00 UTC is 08:00 EDT; April 20 12:00 UTC is 08:00 EDT
        var nice = Ticks.TimeNice(D(3, 15, 12), D(4, 20, 12), month, zone, false);

        // Assert: March 1 00:00 EST is 05:00Z, May 1 00:00 EDT is 04:00Z
        Assert.Equal((D(3, 1, 5), D(5, 1, 4)), nice);
    }

    /// <summary>
    ///     Validates that a time scale does not lower its tick count by the d3 tick step (the minimum step caps the count by the span only): eight ticks over the
    ///     91 days of January to March with a minimum of 31 days give 3, where a numeric scale would go on to 2.
    /// </summary>
    [Fact]
    public void Ticks_ResolveTickCount_Temporal_SkipsTickStepLoop()
    {
        // Arrange: 91 days / 31 days = 2.935, floor 2, plus one is 3
        var start = D(1, 1);
        var stop = D(4, 1);
        const double MinStep = 31 * 86_400_000d;

        // Act
        var temporal = Ticks.ResolveTickCount(start, stop, 8, MinStep, temporal: true);
        var numeric = Ticks.ResolveTickCount(start, stop, 8, MinStep);

        // Assert: the loop of a numeric scale finds that 3 ticks give a step of 2e9 ms (below 31 days) and settles on 2 (step 5e9)
        Assert.Equal(3, temporal);
        Assert.Equal(2, numeric);
        Assert.Equal(8, Ticks.ResolveTickCount(start, stop, 8, null, temporal: true));
    }

    /// <summary>
    ///     Validates the cap of the count by the minimum step for the year-and-month example of Vega-Lite (three bars of one month each over 300 pixels: the default
    ///     eight ticks drop to three, which then choose the month).
    /// </summary>
    [Fact]
    public void Ticks_ResolveTickCount_MinStep_CapsCount()
    {
        // Arrange: January 1 to April 1, 2024 and the minimum step of the shortest month of a year-month unit (31 days)
        var count = Ticks.ResolveTickCount(D(1, 1), D(4, 1), Ticks.DefaultAxisTickCount(300), 31 * 86_400_000d, temporal: true);

        // Act: three ticks over 91 days target 30.3 days, the month entry
        var ticks = TickValues(D(1, 1), D(4, 1), new TimeTickRequest(count));

        // Assert
        Assert.Equal(3, count);
        Assert.Equal([D(1, 1), D(2, 1), D(3, 1), D(4, 1)], ticks);
    }

    /// <summary>
    ///     Validates that the ticks and the boundaries are the same under the Turkish and the German cultures (nothing reads the current culture).
    /// </summary>
    [Fact]
    public void Ticks_Time_Culture_Invariant()
    {
        // Arrange
        var baseline = TickValues(D(1, 1), D(3, 31), new TimeTickRequest(8));
        var baselineNice = Ticks.TimeNice(D(1, 10, 13), D(3, 20, 5), TimeTickRequest.Default, TimeCalendar.Utc, false);

        // Act / Assert
        foreach (var culture in new[] { "tr-TR", "de-DE" })
        {
            var (ticks, nice) = ExpressionTestSupport.UnderCulture(culture, () =>
                (TickValues(D(1, 1), D(3, 31), new TimeTickRequest(8)), Ticks.TimeNice(D(1, 10, 13), D(3, 20, 5), TimeTickRequest.Default, TimeCalendar.Utc, false)));
            Assert.Equal(baseline, ticks);
            Assert.Equal(baselineNice, nice);
        }
    }
}
