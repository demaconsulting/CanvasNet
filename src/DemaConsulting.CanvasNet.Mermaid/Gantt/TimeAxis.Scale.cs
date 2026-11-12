// Portions derived from d3 (ISC); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     The d3 time functions a gantt chart is drawn with: d3-time's local intervals and tick generator, d3-array's
///     <c>tickStep</c>, <c>min</c> and <c>max</c>, and d3-scale's time scale with d3-interpolate's
///     <c>interpolateRound</c>; the local time zone is UTC (see <see cref="JsDate"/>).
/// </summary>
/// <remarks>
///     <para>
///         The intervals are d3's <c>timeMillisecond</c>, <c>timeSecond</c>, <c>timeMinute</c>, <c>timeHour</c>,
///         <c>timeDay</c>, <c>timeSunday</c> to <c>timeSaturday</c>, <c>timeMonth</c> and <c>timeYear</c>, with the
///         special <c>every</c> of the millisecond and year intervals. <see cref="Ticks(double, double, double, Action?)"/> is
///         d3-time's <c>timeTicks</c> (the 18-row interval table, the bisection and the year and millisecond fallbacks),
///         <see cref="TickStep"/> is d3-array's (the thresholds √50, √10 and √2), and <see cref="TimeScale"/> is a
///         <c>scaleTime</c> with <c>rangeRound</c>: Mermaid's gantt chart never calls <c>nice</c> or <c>clamp</c>, so
///         neither is ported.
///     </para>
///     <para>
///         The functions are pure and the intervals immutable, so the class is safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class TimeAxis
{
    /// <summary>The milliseconds of a week.</summary>
    public const double DurationWeek = JsDate.MsPerDay * 7;

    /// <summary>The milliseconds of d3-time's month (30 days).</summary>
    public const double DurationMonth = JsDate.MsPerDay * 30;

    /// <summary>The milliseconds of d3-time's year (365 days).</summary>
    public const double DurationYear = JsDate.MsPerDay * 365;

    /// <summary>d3-array's threshold for a step of ten (√50).</summary>
    private static readonly double E10 = Math.Sqrt(50);

    /// <summary>d3-array's threshold for a step of five (√10).</summary>
    private static readonly double E5 = Math.Sqrt(10);

    /// <summary>d3-array's threshold for a step of two (√2).</summary>
    private static readonly double E2 = Math.Sqrt(2);

    /// <summary>
    ///     Gets d3's <c>timeMillisecond</c>.
    /// </summary>
    public static TimeInterval Millisecond { get; } = new(
        date => date,
        (date, step) => JsDate.FromTime(date.Time + step),
        (start, end) => end.Time - start.Time,
        every: MillisecondEvery);

    /// <summary>
    ///     Gets d3's <c>timeSecond</c>.
    /// </summary>
    public static TimeInterval Second { get; } = new(
        date => JsDate.FromTime(date.Time - date.Milliseconds),
        (date, step) => JsDate.FromTime(date.Time + step * JsDate.MsPerSecond),
        (start, end) => (end.Time - start.Time) / JsDate.MsPerSecond,
        date => date.Seconds);

    /// <summary>
    ///     Gets d3's <c>timeMinute</c>.
    /// </summary>
    public static TimeInterval Minute { get; } = new(
        date => JsDate.FromTime(date.Time - date.Milliseconds - date.Seconds * JsDate.MsPerSecond),
        (date, step) => JsDate.FromTime(date.Time + step * JsDate.MsPerMinute),
        (start, end) => (end.Time - start.Time) / JsDate.MsPerMinute,
        date => date.Minutes);

    /// <summary>
    ///     Gets d3's <c>timeHour</c>.
    /// </summary>
    public static TimeInterval Hour { get; } = new(
        date => JsDate.FromTime(date.Time - date.Milliseconds - date.Seconds * JsDate.MsPerSecond - date.Minutes * JsDate.MsPerMinute),
        (date, step) => JsDate.FromTime(date.Time + step * JsDate.MsPerHour),
        (start, end) => (end.Time - start.Time) / JsDate.MsPerHour,
        date => date.Hours);

    /// <summary>
    ///     Gets d3's <c>timeDay</c>.
    /// </summary>
    public static TimeInterval Day { get; } = new(
        date => date.SetHours(0, 0, 0, 0),
        (date, step) => date.SetDate(date.Date + step),
        (start, end) => (end.Time - start.Time - (end.TimezoneOffset - start.TimezoneOffset) * JsDate.MsPerMinute) / JsDate.MsPerDay,
        date => date.Date - 1);

    /// <summary>
    ///     Gets d3's <c>timeSunday</c> (d3's <c>timeWeek</c>).
    /// </summary>
    public static TimeInterval Sunday { get; } = Weekday(0);

    /// <summary>
    ///     Gets d3's <c>timeMonday</c>.
    /// </summary>
    public static TimeInterval Monday { get; } = Weekday(1);

    /// <summary>
    ///     Gets d3's <c>timeTuesday</c>.
    /// </summary>
    public static TimeInterval Tuesday { get; } = Weekday(2);

    /// <summary>
    ///     Gets d3's <c>timeWednesday</c>.
    /// </summary>
    public static TimeInterval Wednesday { get; } = Weekday(3);

    /// <summary>
    ///     Gets d3's <c>timeThursday</c>.
    /// </summary>
    public static TimeInterval Thursday { get; } = Weekday(4);

    /// <summary>
    ///     Gets d3's <c>timeFriday</c>.
    /// </summary>
    public static TimeInterval Friday { get; } = Weekday(5);

    /// <summary>
    ///     Gets d3's <c>timeSaturday</c>.
    /// </summary>
    public static TimeInterval Saturday { get; } = Weekday(6);

    /// <summary>
    ///     Gets d3's <c>timeMonth</c>.
    /// </summary>
    public static TimeInterval Month { get; } = new(
        date => date.SetDate(1).SetHours(0, 0, 0, 0),
        (date, step) => date.SetMonth(date.Month + step),
        (start, end) => end.Month - start.Month + (end.FullYear - start.FullYear) * 12,
        date => date.Month);

    /// <summary>
    ///     Gets d3's <c>timeYear</c>.
    /// </summary>
    public static TimeInterval Year { get; } = new(
        date => date.SetMonth(0, 1).SetHours(0, 0, 0, 0),
        (date, step) => date.SetFullYear(date.FullYear + step),
        (start, end) => end.FullYear - start.FullYear,
        date => date.FullYear,
        YearEvery);

    /// <summary>
    ///     Gets d3-time's tick intervals (<c>tickIntervals</c> of <c>ticker</c>): the interval, its step and the
    ///     approximate duration of that step, in ascending duration.
    /// </summary>
    private static (TimeInterval Interval, double Step, double Duration)[] TickIntervals { get; } =
    [
        (Second, 1, JsDate.MsPerSecond),
        (Second, 5, 5 * JsDate.MsPerSecond),
        (Second, 15, 15 * JsDate.MsPerSecond),
        (Second, 30, 30 * JsDate.MsPerSecond),
        (Minute, 1, JsDate.MsPerMinute),
        (Minute, 5, 5 * JsDate.MsPerMinute),
        (Minute, 15, 15 * JsDate.MsPerMinute),
        (Minute, 30, 30 * JsDate.MsPerMinute),
        (Hour, 1, JsDate.MsPerHour),
        (Hour, 3, 3 * JsDate.MsPerHour),
        (Hour, 6, 6 * JsDate.MsPerHour),
        (Hour, 12, 12 * JsDate.MsPerHour),
        (Day, 1, JsDate.MsPerDay),
        (Day, 2, 2 * JsDate.MsPerDay),
        (Sunday, 1, DurationWeek),
        (Month, 1, DurationMonth),
        (Month, 3, 3 * DurationMonth),
        (Year, 1, DurationYear),
    ];

    /// <summary>
    ///     Gets the ticks of a time domain, as d3-time's <c>timeTicks(start, stop, count)</c>: the boundaries of the
    ///     interval chosen by <see cref="TickInterval"/> from the start through the stop, reversed for a descending
    ///     domain.
    /// </summary>
    /// <param name="start">The start time value.</param>
    /// <param name="stop">The stop time value (included).</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <param name="onStep">Called per generated tick and per walk step, or null.</param>
    /// <returns>The ticks; empty when no interval fits (an invalid domain).</returns>
    public static List<JsDate> Ticks(double start, double stop, double count, Action? onStep = null)
    {
        var reverse = stop < start;
        if (reverse)
        {
            (start, stop) = (stop, start);
        }

        var interval = TickInterval(start, stop, count);
        var ticks = interval is not null ? interval.Range(start, stop + 1, 1, onStep) : [];
        if (reverse)
        {
            ticks.Reverse();
        }

        return ticks;
    }

    /// <summary>
    ///     Gets the ticks of a time domain at the boundaries of a given interval, as d3-time's
    ///     <c>timeTicks(start, stop, interval)</c>.
    /// </summary>
    /// <param name="start">The start time value.</param>
    /// <param name="stop">The stop time value (included).</param>
    /// <param name="interval">The interval.</param>
    /// <param name="onStep">Called per generated tick and per walk step, or null.</param>
    /// <returns>The ticks.</returns>
    public static List<JsDate> Ticks(double start, double stop, TimeInterval interval, Action? onStep = null)
    {
        ArgumentNullException.ThrowIfNull(interval);
        var reverse = stop < start;
        if (reverse)
        {
            (start, stop) = (stop, start);
        }

        var ticks = interval.Range(start, stop + 1, 1, onStep);
        if (reverse)
        {
            ticks.Reverse();
        }

        return ticks;
    }

    /// <summary>
    ///     Chooses the tick interval of a time domain, as d3-time's <c>timeTickInterval</c>: the table row whose
    ///     duration is nearest (by ratio) to the domain over the count, a year multiple beyond the table, or a
    ///     millisecond multiple below it.
    /// </summary>
    /// <param name="start">The start time value.</param>
    /// <param name="stop">The stop time value.</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <returns>The interval, or null when no step fits (NaN values).</returns>
    public static TimeInterval? TickInterval(double start, double stop, double count)
    {
        var target = Math.Abs(stop - start) / count;
        var i = BisectRight(target);
        if (i == TickIntervals.Length)
        {
            return Year.Every(TickStep(start / DurationYear, stop / DurationYear, count));
        }

        if (i == 0)
        {
            var step = TickStep(start, stop, count);
            return Millisecond.Every(double.IsNaN(step) ? double.NaN : Math.Max(step, 1));
        }

        var row = target / TickIntervals[i - 1].Duration < TickIntervals[i].Duration / target ? TickIntervals[i - 1] : TickIntervals[i];
        return row.Interval.Every(row.Step);
    }

    /// <summary>
    ///     Gets d3-array's <c>tickStep(start, stop, count)</c>: the step of about <paramref name="count"/> nice ticks (a
    ///     power of ten times 1, 2 or 5), negative for a descending range.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="stop">The stop.</param>
    /// <param name="count">The approximate number of ticks.</param>
    /// <returns>The step, or NaN.</returns>
    public static double TickStep(double start, double stop, double count)
    {
        var reverse = stop < start;
        var low = reverse ? stop : start;
        var high = reverse ? start : stop;
        var inc = TickSpec(low, high, count).Increment;
        return (reverse ? -1 : 1) * (inc < 0 ? 1 / -inc : inc);
    }

    /// <summary>
    ///     Gets the earliest valid date of a list, as d3-array's <c>min</c> (invalid dates are skipped).
    /// </summary>
    /// <param name="dates">The dates.</param>
    /// <returns>The earliest, or null (<c>undefined</c>) when there is no valid date.</returns>
    public static JsDate? Min(IEnumerable<JsDate> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        JsDate? min = null;
        foreach (var date in dates)
        {
            if (date.IsValid && (min is null || min.Value > date))
            {
                min = date;
            }
        }

        return min;
    }

    /// <summary>
    ///     Gets the latest valid date of a list, as d3-array's <c>max</c> (invalid dates are skipped).
    /// </summary>
    /// <param name="dates">The dates.</param>
    /// <returns>The latest, or null (<c>undefined</c>) when there is no valid date.</returns>
    public static JsDate? Max(IEnumerable<JsDate> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        JsDate? max = null;
        foreach (var date in dates)
        {
            if (date.IsValid && (max is null || max.Value < date))
            {
                max = date;
            }
        }

        return max;
    }

    /// <summary>
    ///     Rounds as JavaScript's <c>Math.round</c> does: to the nearest integer, halves toward positive infinity.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The rounded value; NaN and infinities unchanged.</returns>
    public static double JsRound(double value)
    {
        if (!double.IsFinite(value))
        {
            return value;
        }

        var floor = Math.Floor(value);
        return value - floor >= 0.5 ? floor + 1 : floor;
    }

    /// <summary>
    ///     d3-array's <c>tickSpec</c>: the first and last tick indexes and the increment (negative for the inverse of a
    ///     fractional step).
    /// </summary>
    /// <param name="start">The start (not after the stop).</param>
    /// <param name="stop">The stop.</param>
    /// <param name="count">The approximate number of ticks.</param>
    /// <returns>The indexes and increment.</returns>
    private static (double I1, double I2, double Increment) TickSpec(double start, double stop, double count)
    {
        var step = (stop - start) / Math.Max(0, count);
        var power = Math.Floor(Math.Log10(step));
        var error = step / Math.Pow(10, power);
        var factor = Factor(error);
        double i1;
        double i2;
        double inc;
        if (power < 0)
        {
            inc = Math.Pow(10, -power) / factor;
            i1 = JsRound(start * inc);
            i2 = JsRound(stop * inc);
            if (i1 / inc < start)
            {
                ++i1;
            }

            if (i2 / inc > stop)
            {
                --i2;
            }

            inc = -inc;
        }
        else
        {
            inc = Math.Pow(10, power) * factor;
            i1 = JsRound(start / inc);
            i2 = JsRound(stop / inc);
            if (i1 * inc < start)
            {
                ++i1;
            }

            if (i2 * inc > stop)
            {
                --i2;
            }
        }

        if (i2 < i1 && count >= 0.5 && count < 2)
        {
            return TickSpec(start, stop, count * 2);
        }

        return (i1, i2, inc);
    }

    /// <summary>
    ///     Gets the nice factor of a step's mantissa: 10, 5, 2 or 1 by d3-array's thresholds.
    /// </summary>
    /// <param name="error">The mantissa, from 1 to 10 (NaN gives 1).</param>
    /// <returns>The factor.</returns>
    private static double Factor(double error)
    {
        if (error >= E10)
        {
            return 10;
        }

        if (error >= E5)
        {
            return 5;
        }

        return error >= E2 ? 2 : 1;
    }

    /// <summary>
    ///     d3-array's <c>bisector(row =&gt; row.duration).right</c> over the tick interval table.
    /// </summary>
    /// <param name="target">The target duration.</param>
    /// <returns>The index after the last row not longer than the target; the table length for NaN.</returns>
    private static int BisectRight(double target)
    {
        var lo = 0;
        var hi = TickIntervals.Length;
        if (double.IsNaN(target))
        {
            return hi;
        }

        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (TickIntervals[mid].Duration <= target)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>
    ///     d3-time's <c>timeWeekday(i)</c>: the weeks that start on a day of the week.
    /// </summary>
    /// <param name="weekday">The first day of the week, 0 for Sunday.</param>
    /// <returns>The interval.</returns>
    private static TimeInterval Weekday(int weekday) => new(
        date => date.SetDate(date.Date - (date.Day + 7 - weekday) % 7).SetHours(0, 0, 0, 0),
        (date, step) => date.SetDate(date.Date + step * 7),
        (start, end) => (end.Time - start.Time - (end.TimezoneOffset - start.TimezoneOffset) * JsDate.MsPerMinute) / DurationWeek);

    /// <summary>
    ///     d3-time's optimized <c>timeMillisecond.every(k)</c>.
    /// </summary>
    /// <param name="k">The step; floored.</param>
    /// <returns>The interval, the millisecond interval for one, or null for a step that is not positive or finite.</returns>
    private static TimeInterval? MillisecondEvery(double k)
    {
        k = Math.Floor(k);
        if (!double.IsFinite(k) || !(k > 0))
        {
            return null;
        }

        if (!(k > 1))
        {
            return Millisecond;
        }

        return new TimeInterval(
            date => JsDate.FromTime(Math.Floor(date.Time / k) * k),
            (date, step) => JsDate.FromTime(date.Time + step * k),
            (start, end) => (end.Time - start.Time) / k);
    }

    /// <summary>
    ///     d3-time's optimized <c>timeYear.every(k)</c> (an interval without count, even for one).
    /// </summary>
    /// <param name="k">The step; floored.</param>
    /// <returns>The interval, or null for a step that is not positive or finite.</returns>
    private static TimeInterval? YearEvery(double k)
    {
        k = Math.Floor(k);
        if (!double.IsFinite(k) || !(k > 0))
        {
            return null;
        }

        return new TimeInterval(
            date => date.SetFullYear(Math.Floor(date.FullYear / k) * k).SetMonth(0, 1).SetHours(0, 0, 0, 0),
            (date, step) => date.SetFullYear(date.FullYear + step * k));
    }
}

/// <summary>
///     d3-scale's <c>scaleTime()</c> with a two-value domain and <c>rangeRound</c>: a linear map from time values to
///     whole pixels, and its ticks.
/// </summary>
/// <remarks>
///     The map is d3-scale's <c>bimap</c> (a descending domain is handled by swapping both ends), its normalization (a
///     zero-length domain maps to the middle of the range) and d3-interpolate's <c>interpolateRound</c> (JavaScript's
///     <c>Math.round</c> of the interpolated value). There is no clamping. The scale is immutable and safe for
///     concurrent use.
/// </remarks>
internal sealed class TimeScale
{
    /// <summary>
    ///     Initializes a scale, as <c>scaleTime().domain([domain0, domain1]).rangeRound([range0, range1])</c>.
    /// </summary>
    /// <param name="domain0">The first domain time value (NaN for an <c>undefined</c> end).</param>
    /// <param name="domain1">The second domain time value.</param>
    /// <param name="range0">The first range value.</param>
    /// <param name="range1">The second range value.</param>
    public TimeScale(double domain0, double domain1, double range0, double range1)
    {
        Domain0 = domain0;
        Domain1 = domain1;
        Range0 = range0;
        Range1 = range1;
    }

    /// <summary>
    ///     Gets the first domain time value.
    /// </summary>
    public double Domain0 { get; }

    /// <summary>
    ///     Gets the second domain time value.
    /// </summary>
    public double Domain1 { get; }

    /// <summary>
    ///     Gets the first range value.
    /// </summary>
    public double Range0 { get; }

    /// <summary>
    ///     Gets the second range value.
    /// </summary>
    public double Range1 { get; }

    /// <summary>
    ///     Maps a time value to the range (<c>scale(x)</c>).
    /// </summary>
    /// <param name="time">The time value.</param>
    /// <returns>The rounded range value, or NaN for NaN (d3's <c>unknown</c>) or an invalid domain.</returns>
    public double Map(double time)
    {
        if (double.IsNaN(time))
        {
            return double.NaN;
        }

        // bimap: a descending domain swaps both ends
        var (a, b, r0, r1) = Domain1 < Domain0 ? (Domain1, Domain0, Range1, Range0) : (Domain0, Domain1, Range0, Range1);

        // normalize: the parameter of the value in the domain, the middle for a zero-length domain
        var span = b - a;
        double t;
        if (double.IsNaN(span))
        {
            t = double.NaN;
        }
        else
        {
            t = span is 0 ? 0.5 : (time - a) / span;
        }

        // interpolateRound
        return TimeAxis.JsRound(r0 * (1 - t) + r1 * t);
    }

    /// <summary>
    ///     Gets the ticks of the domain (<c>scale.ticks(count)</c>).
    /// </summary>
    /// <param name="count">The approximate number of ticks; d3's default is 10.</param>
    /// <param name="onStep">Called per generated tick and per walk step, or null.</param>
    /// <returns>The ticks.</returns>
    public List<JsDate> Ticks(double count = 10, Action? onStep = null) => TimeAxis.Ticks(Domain0, Domain1, count, onStep);

    /// <summary>
    ///     Gets the ticks of the domain at an interval's boundaries (<c>scale.ticks(interval)</c>).
    /// </summary>
    /// <param name="interval">The interval.</param>
    /// <param name="onStep">Called per generated tick and per walk step, or null.</param>
    /// <returns>The ticks.</returns>
    public List<JsDate> Ticks(TimeInterval interval, Action? onStep = null) => TimeAxis.Ticks(Domain0, Domain1, interval, onStep);
}
