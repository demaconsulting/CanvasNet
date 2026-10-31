// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     What a time axis (or a <c>nice</c>) asks of the ticks: a number of ticks, or a calendar interval with an optional step.
/// </summary>
/// <remarks>
///     These are the forms Vega accepts for <c>tickCount</c> and <c>nice</c> of a time scale: a number (the approximate count the d3 interval table
///     is searched with), an interval name such as <c>month</c>, and <c>{interval, step}</c>, such as every second week.
/// </remarks>
/// <param name="Count">The approximate number of ticks (10 when the specification gives none); ignored when <paramref name="Interval"/> is set.</param>
/// <param name="Interval">The calendar interval, or null to search the d3 interval table with <paramref name="Count"/>.</param>
/// <param name="Step">The number of intervals between two ticks; 0 or less for none (every boundary of the interval).</param>
/// <param name="Utc">Whether the interval name had the <c>utc</c> prefix, which uses the UTC calendar whatever the scale's.</param>
internal readonly record struct TimeTickRequest(double Count, TimeIntervalKind? Interval = null, double Step = 0, bool Utc = false)
{
    /// <summary>Gets the request d3 makes when no count is given: about ten ticks.</summary>
    public static TimeTickRequest Default => new(Ticks.DefaultTimeTickCount);

    /// <summary>
    ///     Makes the request for an interval name and an optional step.
    /// </summary>
    /// <param name="name">The interval name (see <see cref="TimeInterval.TryParseName"/>).</param>
    /// <param name="step">The step of <c>{interval, step}</c>, or null for a bare name.</param>
    /// <param name="request">Receives the request.</param>
    /// <returns><see langword="true"/> when the name is known.</returns>
    public static bool TryFromInterval(string name, double? step, out TimeTickRequest request)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!TimeInterval.TryParseName(name, out var kind, out var multiplier, out var forceUtc))
        {
            request = default;
            return false;
        }

        // A quarter is three months, so the step counts quarters
        double effective = 0;
        if (step is { } given)
        {
            effective = given * multiplier;
        }
        else if (multiplier > 1)
        {
            effective = multiplier;
        }

        request = new TimeTickRequest(Ticks.DefaultTimeTickCount, kind, effective, forceUtc);
        return true;
    }
}

/// <content>
///     The tick values and <c>nice</c> boundaries of time and UTC scales (a partial file of the ticks unit).
/// </content>
/// <remarks>
///     <para>
///         <see cref="TimeTickInterval"/>, <see cref="TimeTicks(ScaleModel, TimeTickRequest, int, CancellationToken)"/> and <see cref="TimeNice"/> are C# versions of d3-time 3.1's <c>tickInterval</c> and
///         <c>ticks</c> and of the <c>nice</c> method of d3-scale 4's time scale. The interval is the one of the table 1, 5, 15 and 30 seconds,
///         1, 5, 15 and 30 minutes, 1, 3, 6 and 12 hours, 1 and 2 days, 1 week, 1 and 3 months and 1 year whose length is nearest to
///         <c>|stop - start| / count</c> (the earlier of the two neighbors of the target when <c>target / previous &lt; next / target</c>, so a target
///         of 10 minutes, whose ratios are 10 / 5 = 2 and 15 / 10 = 1.5, picks 15 minutes). A target shorter than a second uses whole milliseconds by the
///         <c>tickStep</c> rule of numbers, and a target longer than a year whole years by the same rule over the years.
///     </para>
///     <para>
///         <b>Limits.</b> The ticks are bounded by the largest tick count (<see cref="VegaLiteLimits.MaxTicks"/>): the range stops at the first tick
///         beyond it and the failure is a <see cref="VegaLiteRenderException"/> that names the limit, so a request for a millisecond interval over
///         decades costs at most that many ticks. The cancellation token is polled while the ticks are generated. <b>Determinism:</b> the arithmetic is
///         that of <see cref="TimeCalendar"/> in the calendar of the scale; no wall clock or host zone is read.
///     </para>
/// </remarks>
internal static partial class Ticks
{
    /// <summary>The tick count d3 uses when none is given.</summary>
    internal const double DefaultTimeTickCount = 10;

    /// <summary>The length of one year in the d3 interval table: 365 days.</summary>
    private const double DurationYear = 365 * 86_400_000.0;

    /// <summary>The intervals d3 chooses from, with their step and approximate length in milliseconds (a month is 30 days).</summary>
    private static readonly (TimeIntervalKind Kind, double Step, double Duration)[] TimeIntervalTable =
    [
        (TimeIntervalKind.Second, 1, 1_000),
        (TimeIntervalKind.Second, 5, 5_000),
        (TimeIntervalKind.Second, 15, 15_000),
        (TimeIntervalKind.Second, 30, 30_000),
        (TimeIntervalKind.Minute, 1, 60_000),
        (TimeIntervalKind.Minute, 5, 300_000),
        (TimeIntervalKind.Minute, 15, 900_000),
        (TimeIntervalKind.Minute, 30, 1_800_000),
        (TimeIntervalKind.Hour, 1, 3_600_000),
        (TimeIntervalKind.Hour, 3, 10_800_000),
        (TimeIntervalKind.Hour, 6, 21_600_000),
        (TimeIntervalKind.Hour, 12, 43_200_000),
        (TimeIntervalKind.Day, 1, 86_400_000),
        (TimeIntervalKind.Day, 2, 172_800_000),
        (TimeIntervalKind.Week, 1, 604_800_000),
        (TimeIntervalKind.Month, 1, 2_592_000_000),
        (TimeIntervalKind.Month, 3, 7_776_000_000),
        (TimeIntervalKind.Year, 1, DurationYear),
    ];

    /// <summary>
    ///     Chooses the calendar interval for about <paramref name="count"/> ticks between two instants (d3-time's <c>tickInterval</c>).
    /// </summary>
    /// <remarks>
    ///     The target length is <c>|stop - start| / count</c>. The interval whose length follows the target in the table is found by bisection; the
    ///     one before it is chosen instead when the target is nearer to it by ratio (<c>target / previous &lt; next / target</c>). A target below
    ///     the first entry gives whole milliseconds (<c>max(tickStep(start, stop, count), 1)</c>) and one beyond the last gives whole years
    ///     (<c>tickStep</c> over the years). The result is null when the count is not positive or the numbers have no interval (NaN).
    /// </remarks>
    /// <param name="start">The first instant in milliseconds.</param>
    /// <param name="stop">The second instant.</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <param name="calendar">The calendar the interval belongs to.</param>
    /// <param name="utcScale">Whether the ticks are those of a UTC scale, whose two-day ticks count the days since the epoch (d3's <c>unixDay</c>) rather than the day of the month.</param>
    /// <returns>The interval; null when there is none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="calendar"/> is null.</exception>
    public static TimeInterval? TimeTickInterval(double start, double stop, double count, TimeCalendar calendar, bool utcScale = false)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (!(count > 0))
        {
            return null;
        }

        var target = Math.Abs(stop - start) / count;
        if (double.IsNaN(target))
        {
            return null;
        }

        // bisector.right over the lengths: the number of table entries that are not longer than the target
        var i = 0;
        while (i < TimeIntervalTable.Length && TimeIntervalTable[i].Duration <= target)
        {
            i++;
        }

        if (i == TimeIntervalTable.Length)
        {
            return calendar.Interval(TimeIntervalKind.Year).Every(TickStep(start / DurationYear, stop / DurationYear, count));
        }

        if (i == 0)
        {
            return calendar.Interval(TimeIntervalKind.Millisecond).Every(Math.Max(TickStep(start, stop, count), 1));
        }

        var previous = TimeIntervalTable[i - 1];
        var next = TimeIntervalTable[i];
        var chosen = target / previous.Duration < next.Duration / target ? previous : next;
        var interval = calendar.Interval(chosen.Kind);
        return utcScale && chosen.Kind == TimeIntervalKind.Day ? interval.EveryEpochDay(chosen.Step) : interval.Every(chosen.Step);
    }

    /// <summary>
    ///     Resolves a request to the interval that places the ticks or the <c>nice</c> boundaries.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="start">The first instant of the domain.</param>
    /// <param name="stop">The last instant of the domain.</param>
    /// <param name="calendar">The calendar of the scale.</param>
    /// <param name="utcScale">Whether the scale is a UTC scale.</param>
    /// <returns>The interval; null when there is none (a count that is not positive or finds no interval).</returns>
    public static TimeInterval? ResolveTimeInterval(TimeTickRequest request, double start, double stop, TimeCalendar calendar, bool utcScale)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (request.Interval is not { } kind)
        {
            return TimeTickInterval(start, stop, request.Count, calendar, utcScale);
        }

        // Vega: an interval name is an interval (of the scale's calendar), and a step makes it every nth boundary; a step that gives no interval
        // (below one, or negative) leaves the scale without one, and d3 then falls back to its default of about ten ticks
        var interval = (request.Utc ? TimeCalendar.Utc : calendar).Interval(kind);
        if (request.Step == 0 || double.IsNaN(request.Step))
        {
            return interval;
        }

        return interval.Every(request.Step) ?? TimeTickInterval(start, stop, DefaultTimeTickCount, calendar, utcScale);
    }

    /// <summary>
    ///     Generates the ticks of a time or UTC scale (d3-time's <c>ticks</c> of the scale's domain).
    /// </summary>
    /// <remarks>
    ///     The ticks are the boundaries of the chosen interval from the first one at or after the start of the domain up to and including the stop (d3 asks
    ///     the range for <c>stop + 1</c>); a reversed domain gives the ticks in descending order.
    /// </remarks>
    /// <param name="model">The time or UTC scale.</param>
    /// <param name="request">The request: a count or an interval.</param>
    /// <param name="maxTicks">The largest number of ticks accepted.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The ticks as milliseconds since the epoch.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the model is not a time or UTC scale.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the result would hold more than <paramref name="maxTicks"/> values.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public static double[] TimeTicks(ScaleModel model, TimeTickRequest request, int maxTicks = 1_000, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Kind is not (ScaleKind.Time or ScaleKind.Utc) || model.Calendar is not { } calendar)
        {
            throw new ArgumentException("The model must be a time or UTC scale.", nameof(model));
        }

        return TimeTicks(model.DomainStart, model.DomainEnd, request, calendar, model.Kind == ScaleKind.Utc, maxTicks, cancellationToken);
    }

    /// <summary>
    ///     Generates the ticks between two instants (d3-time's <c>ticks</c>), for a domain that is not held by a time scale model: the instants of a temporal color scale.
    /// </summary>
    /// <remarks>
    ///     The ticks are the boundaries of the chosen interval from the first one at or after the start up to and including the stop; a reversed domain gives the ticks in
    ///     descending order. The result holds at most <paramref name="maxTicks"/> values.
    /// </remarks>
    /// <param name="domainStart">The first end of the domain in milliseconds.</param>
    /// <param name="domainEnd">The second end of the domain.</param>
    /// <param name="request">The request: a count or an interval.</param>
    /// <param name="calendar">The calendar of the ticks.</param>
    /// <param name="utcScale">Whether the ticks are those of a UTC scale (its two-day ticks count the days since the epoch).</param>
    /// <param name="maxTicks">The largest number of ticks accepted.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The ticks as milliseconds since the epoch.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="calendar"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the result would hold more than <paramref name="maxTicks"/> values.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public static double[] TimeTicks(double domainStart, double domainEnd, TimeTickRequest request, TimeCalendar calendar, bool utcScale, int maxTicks = 1_000, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var reverse = domainEnd < domainStart;
        var start = reverse ? domainEnd : domainStart;
        var stop = reverse ? domainStart : domainEnd;
        var interval = ResolveTimeInterval(request, start, stop, calendar, utcScale);
        if (interval is null)
        {
            return [];
        }

        try
        {
            var ticks = new List<double>(interval.Range(start, stop + 1, 1, maxTicks, cancellationToken));
            if (reverse)
            {
                ticks.Reverse();
            }

            return [.. ticks];
        }
        catch (TimeRangeLimitException)
        {
            throw new VegaLiteRenderException($"The axis would have more than {maxTicks} ticks, exceeding the limit of {maxTicks} (MaxTicks).");
        }
    }

    /// <summary>
    ///     Extends a time domain to the boundaries of an interval (the <c>nice</c> method of d3's time scale).
    /// </summary>
    /// <remarks>
    ///     The interval is the requested one or the one that <see cref="TimeTickInterval"/> picks for the count; the start is floored and the end is
    ///     ceiled to it. A reversed domain keeps its direction. The domain is returned unchanged when there is no interval or a boundary
    ///     lies outside the years the calendar supports.
    /// </remarks>
    /// <param name="start">The first end of the domain in milliseconds.</param>
    /// <param name="stop">The second end of the domain.</param>
    /// <param name="request">The request: a count (10 for <c>nice: true</c>) or an interval.</param>
    /// <param name="calendar">The calendar of the scale.</param>
    /// <param name="utcScale">Whether the scale is a UTC scale.</param>
    /// <returns>The extended domain, in the direction of the input.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="calendar"/> is null.</exception>
    public static (double Start, double Stop) TimeNice(double start, double stop, TimeTickRequest request, TimeCalendar calendar, bool utcScale)
    {
        var interval = ResolveTimeInterval(request, start, stop, calendar, utcScale);
        if (interval is null)
        {
            return (start, stop);
        }

        var reversed = stop < start;
        var low = interval.Floor(reversed ? stop : start);
        var high = interval.Ceil(reversed ? start : stop);
        if (double.IsNaN(low) || double.IsNaN(high))
        {
            return (start, stop);
        }

        return reversed ? (high, low) : (low, high);
    }
}
