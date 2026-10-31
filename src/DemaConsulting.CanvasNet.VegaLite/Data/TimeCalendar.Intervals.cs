// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The calendar intervals of d3-time, from the millisecond to the year.
/// </summary>
internal enum TimeIntervalKind
{
    /// <summary>One millisecond.</summary>
    Millisecond,

    /// <summary>One second.</summary>
    Second,

    /// <summary>One minute.</summary>
    Minute,

    /// <summary>One hour.</summary>
    Hour,

    /// <summary>One calendar day (23, 24 or 25 hours across a daylight saving change).</summary>
    Day,

    /// <summary>One week, starting on Sunday.</summary>
    Week,

    /// <summary>One calendar month.</summary>
    Month,

    /// <summary>One calendar year.</summary>
    Year,
}

/// <summary>
///     The exception that <see cref="TimeInterval.Range"/> throws when a range would hold more items than the caller allows.
/// </summary>
/// <remarks>
///     Callers map it to a located render exception that names the limit they enforce (the tick limit or the scale domain
///     limit); it never reaches a public caller.
/// </remarks>
internal sealed class TimeRangeLimitException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeRangeLimitException"/> class.
    /// </summary>
    /// <param name="count">The largest number of items that was allowed.</param>
    public TimeRangeLimitException(int count)
        : base($"The range holds more than {count} items.")
    {
        Count = count;
    }

    /// <summary>Gets the largest number of items that was allowed.</summary>
    public int Count { get; }
}

/// <summary>
///     The interval objects of a <see cref="TimeCalendar"/>.
/// </summary>
internal sealed partial class TimeCalendar
{
    /// <summary>
    ///     Gets the d3-time interval of a kind in this calendar: <c>timeMillisecond</c> to <c>timeYear</c> for a zone, the
    ///     <c>utc</c> intervals for the UTC calendar.
    /// </summary>
    /// <param name="kind">The kind of interval.</param>
    /// <returns>The interval; the same object for repeated calls.</returns>
    public TimeInterval Interval(TimeIntervalKind kind) =>
        _intervals[(int)kind] ??= new TimeInterval(this, kind);
}

/// <summary>
///     A d3-time interval: <c>floor</c>, <c>ceil</c>, <c>round</c>, <c>offset</c>, <c>count</c>, <c>every</c> and
///     <c>range</c> for one kind of calendar boundary in one <see cref="TimeCalendar"/>.
/// </summary>
/// <remarks>
///     <para>
///         The semantics are those of d3-time 3.1.0. The second, minute and hour intervals floor by subtracting the local
///         time-of-day components and offset in plain time, so an hour is always 3,600,000 ms and a half-hour zone
///         floors to its own hour. The day, week, month and year intervals work on the civil date and re-resolve the wall
///         time, so a day is 23, 24 or 25 hours across a daylight saving change. The week starts on Sunday.
///     </para>
///     <para>
///         <see cref="Every"/> gives d3's two flavors: a filter interval (every third hour, every second day, ...) that
///         keeps the boundaries whose field is a multiple of the step, and the closed forms of the millisecond and the
///         year. A filter interval scans at most a bounded number of underlying boundaries per result (the step is clamped
///         to the range of the field, which keeps the same boundaries), and an offset of more than
///         <see cref="MaxFilterOffset"/> steps of a filter interval is NaN, so no call costs more than a small fixed amount.
///         Only a plain interval has <see cref="Every"/> and <see cref="Count"/>, like d3.
///     </para>
///     <para>
///         Every method is a total function: a NaN or out-of-range instant gives NaN and nothing throws except
///         <see cref="Range"/> (limit and cancellation). Instances are immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class TimeInterval
{
    /// <summary>The largest number of steps a filter interval offsets in one call; larger counts give NaN.</summary>
    internal const int MaxFilterOffset = 64;

    /// <summary>The largest number of underlying boundaries one filter operation visits before giving up.</summary>
    private const int MaxScan = 128;

    /// <summary>The largest step of a week filter (the week count has no natural period).</summary>
    private const double MaxWeekStep = 64;

    /// <summary>The number of items between two cancellation polls in <see cref="Range"/>.</summary>
    private const int PollInterval = 1024;

    /// <summary>The calendar the interval belongs to.</summary>
    private readonly TimeCalendar _calendar;

    /// <summary>The kind of boundary.</summary>
    private readonly TimeIntervalKind _kind;

    /// <summary>The flavor: plain, filter or closed form.</summary>
    private readonly Mode _mode;

    /// <summary>The step of a filter or closed-form interval; 1 for a plain interval.</summary>
    private readonly double _step;

    /// <summary>
    ///     Initializes a plain interval.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="kind">The kind of boundary.</param>
    internal TimeInterval(TimeCalendar calendar, TimeIntervalKind kind)
        : this(calendar, kind, Mode.Plain, 1)
    {
    }

    /// <summary>
    ///     Initializes an interval of any flavor.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="kind">The kind of boundary.</param>
    /// <param name="mode">The flavor.</param>
    /// <param name="step">The step (at least 1 and whole).</param>
    private TimeInterval(TimeCalendar calendar, TimeIntervalKind kind, Mode mode, double step)
    {
        _calendar = calendar;
        _kind = kind;
        _mode = mode;
        _step = step;
    }

    /// <summary>The flavors of interval.</summary>
    private enum Mode
    {
        /// <summary>A plain d3 interval.</summary>
        Plain,

        /// <summary>The result of <c>every(step)</c>: the boundaries whose field is a multiple of the step.</summary>
        Filter,

        /// <summary>The closed form of <c>millisecond.every(step)</c> or <c>year.every(step)</c>.</summary>
        Closed,
    }

    /// <summary>Gets the kind of boundary.</summary>
    public TimeIntervalKind Kind => _kind;

    /// <summary>Gets the calendar the interval belongs to.</summary>
    public TimeCalendar Calendar => _calendar;

    /// <summary>
    ///     Reads the name of an interval as it appears in a Vega-Lite or Vega specification.
    /// </summary>
    /// <param name="name">
    ///     The name: <c>millisecond</c>, <c>second</c>, <c>minute</c>, <c>hour</c>, <c>day</c>, <c>week</c>, <c>month</c>,
    ///     <c>year</c> (the d3 names of the documentation), the Vega time-unit names the code uses (<c>milliseconds</c>,
    ///     <c>seconds</c>, <c>minutes</c>, <c>hours</c>, <c>date</c>, <c>dayofyear</c>, <c>quarter</c>), each optionally
    ///     prefixed with <c>utc</c>; the match ignores case.
    /// </param>
    /// <param name="kind">The kind of boundary.</param>
    /// <param name="stepMultiplier">3 for <c>quarter</c> (three months), otherwise 1.</param>
    /// <param name="forceUtc">True when the name has the <c>utc</c> prefix.</param>
    /// <returns>True when the name is known.</returns>
    public static bool TryParseName(string name, out TimeIntervalKind kind, out double stepMultiplier, out bool forceUtc)
    {
        kind = TimeIntervalKind.Millisecond;
        stepMultiplier = 1;
        forceUtc = false;
        var text = name.ToLowerInvariant();
        if (text.StartsWith("utc", StringComparison.Ordinal))
        {
            forceUtc = true;
            text = text[3..];
        }

        // Both spellings of the documentation and of the code are accepted
        switch (text)
        {
            case "millisecond" or "milliseconds":
                kind = TimeIntervalKind.Millisecond;
                return true;
            case "second" or "seconds":
                kind = TimeIntervalKind.Second;
                return true;
            case "minute" or "minutes":
                kind = TimeIntervalKind.Minute;
                return true;
            case "hour" or "hours":
                kind = TimeIntervalKind.Hour;
                return true;
            case "day" or "date" or "dayofyear":
                kind = TimeIntervalKind.Day;
                return true;
            case "week":
                kind = TimeIntervalKind.Week;
                return true;
            case "month":
                kind = TimeIntervalKind.Month;
                return true;
            case "quarter":
                kind = TimeIntervalKind.Month;
                stepMultiplier = 3;
                return true;
            case "year":
                kind = TimeIntervalKind.Year;
                return true;
            default:
                forceUtc = false;
                return false;
        }
    }

    /// <summary>
    ///     Gets the latest boundary at or before an instant (d3 <c>interval.floor</c>).
    /// </summary>
    /// <param name="ms">The instant in milliseconds since the Unix epoch.</param>
    /// <returns>The boundary, or NaN when the instant or the result is out of range.</returns>
    public double Floor(double ms)
    {
        var t = Clip(ms);
        return _mode switch
        {
            Mode.Plain => PlainFloor(t),
            Mode.Filter => FilterFloor(t),
            _ => ClosedFloor(t),
        };
    }

    /// <summary>
    ///     Gets the earliest boundary at or after an instant (d3 <c>interval.ceil</c>).
    /// </summary>
    /// <param name="ms">The instant in milliseconds since the Unix epoch.</param>
    /// <returns>The boundary, or NaN when the instant or the result is out of range.</returns>
    public double Ceil(double ms)
    {
        // d3: floor(date - 1), offset by one, floor again
        var t = Clip(ms);
        return Floor(Offset(Floor(t - 1), 1));
    }

    /// <summary>
    ///     Gets the nearer of <see cref="Floor"/> and <see cref="Ceil"/> (d3 <c>interval.round</c>).
    /// </summary>
    /// <param name="ms">The instant in milliseconds since the Unix epoch.</param>
    /// <returns>The nearer boundary; the later one when both are equally near.</returns>
    public double Round(double ms)
    {
        var t = Clip(ms);
        var lower = Floor(t);
        var upper = Ceil(t);
        return t - lower < upper - t ? lower : upper;
    }

    /// <summary>
    ///     Moves an instant by a number of intervals (d3 <c>interval.offset</c>).
    /// </summary>
    /// <param name="ms">The instant in milliseconds since the Unix epoch.</param>
    /// <param name="step">The number of intervals; a fraction is rounded down (<c>Math.floor</c>), negative values go back.</param>
    /// <returns>
    ///     The new instant, keeping the time of day for the day, week, month and year intervals; NaN when out of range, and
    ///     for a filter interval when the step is larger than <see cref="MaxFilterOffset"/>.
    /// </returns>
    public double Offset(double ms, double step)
    {
        var t = Clip(ms);
        var n = Math.Floor(step);
        if (double.IsNaN(t) || !double.IsFinite(n))
        {
            return double.NaN;
        }

        return _mode switch
        {
            Mode.Plain => PlainOffset(t, n),
            Mode.Filter => FilterOffset(t, n),
            _ => ClosedOffset(t, n),
        };
    }

    /// <summary>
    ///     Counts the boundaries after the first instant up to and including the one of the second (d3 <c>interval.count</c>).
    /// </summary>
    /// <param name="startMs">The first instant.</param>
    /// <param name="endMs">The second instant.</param>
    /// <returns>
    ///     The whole number of intervals between the floors of the two instants, or NaN when an instant is out of range or
    ///     the interval has no count (a filter interval or the year closed form).
    /// </returns>
    public double Count(double startMs, double endMs)
    {
        if (_mode == Mode.Filter || (_mode == Mode.Closed && _kind == TimeIntervalKind.Year))
        {
            return double.NaN;
        }

        var start = Floor(startMs);
        var end = Floor(endMs);
        if (double.IsNaN(start) || double.IsNaN(end))
        {
            return double.NaN;
        }

        // The closed millisecond form counts in steps of its own size
        if (_mode == Mode.Closed)
        {
            return Math.Floor((end - start) / _step);
        }

        return Math.Floor(PlainDistance(start, end));
    }

    /// <summary>
    ///     Gets the interval that keeps every nth boundary (d3 <c>interval.every</c>).
    /// </summary>
    /// <param name="step">The step; a fraction is rounded down.</param>
    /// <returns>
    ///     This interval for a step of 1 or less; null for a step that is not finite or not positive and for an interval
    ///     that is already a filter or closed form (d3 has no <c>every</c> there); null for a week step above 64 (a
    ///     deliberate bound); otherwise the filter or closed-form interval.
    /// </returns>
    public TimeInterval? Every(double step)
    {
        var k = Math.Floor(step);
        if (!double.IsFinite(k) || !(k > 0))
        {
            return null;
        }

        if (!(k > 1))
        {
            return this;
        }

        if (_mode != Mode.Plain)
        {
            return null;
        }

        // The millisecond and the year have closed forms; the others filter their boundaries by a field
        switch (_kind)
        {
            case TimeIntervalKind.Millisecond:
            case TimeIntervalKind.Year:
                return new TimeInterval(_calendar, _kind, Mode.Closed, k);
            case TimeIntervalKind.Week:
                return k > MaxWeekStep ? null : new TimeInterval(_calendar, _kind, Mode.Filter, k);
            default:
                // Steps beyond the range of the field select only its first value, so clamping keeps the same boundaries
                return new TimeInterval(_calendar, _kind, Mode.Filter, Math.Min(k, FieldRange(_kind)));
        }
    }

    /// <summary>
    ///     Lists the boundaries from the first one at or after a start up to but excluding a stop (d3 <c>interval.range</c>).
    /// </summary>
    /// <param name="start">The first instant.</param>
    /// <param name="stop">The exclusive end.</param>
    /// <param name="step">The number of intervals between the boundaries; a fraction is rounded down, and a step below 1 gives an empty list.</param>
    /// <param name="maxCount">The largest number of items allowed in the result.</param>
    /// <param name="cancellationToken">The token polled while the list grows.</param>
    /// <returns>The boundaries in increasing order; empty when the start is not before the stop or an argument is NaN.</returns>
    /// <exception cref="TimeRangeLimitException">The list would hold more than <paramref name="maxCount"/> items.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public IReadOnlyList<double> Range(double start, double stop, double step, int maxCount, CancellationToken cancellationToken)
    {
        var list = new List<double>();
        var current = Ceil(start);
        var n = Math.Floor(step);
        if (!(current < stop) || !(n > 0))
        {
            return list;
        }

        // d3: push, offset by the step, floor, until the sequence stops growing or reaches the stop
        double previous;
        do
        {
            if (list.Count >= maxCount)
            {
                throw new TimeRangeLimitException(maxCount);
            }

            if (list.Count % PollInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            previous = current;
            list.Add(previous);
            current = Floor(Offset(current, n));
        }
        while (previous < current && current < stop);

        return list;
    }

    /// <summary>
    ///     Truncates an instant to whole milliseconds and rejects the ones outside the valid range.
    /// </summary>
    /// <param name="ms">The instant.</param>
    /// <returns>The truncated instant, or NaN.</returns>
    private static double Clip(double ms) => TimeCalendar.IsValid(ms) ? Math.Truncate(ms) + 0.0 : double.NaN;

    /// <summary>
    ///     Turns an instant that left the valid range into NaN.
    /// </summary>
    /// <param name="ms">The instant.</param>
    /// <returns>The instant when valid, otherwise NaN.</returns>
    private static double Checked(double ms) => TimeCalendar.IsValid(ms) ? ms : double.NaN;

    /// <summary>
    ///     Gets the number of values of the field a filter interval tests (seconds 60, hours 24, ...).
    /// </summary>
    /// <param name="kind">The kind of boundary.</param>
    /// <returns>The number of distinct field values.</returns>
    private static double FieldRange(TimeIntervalKind kind) => kind switch
    {
        TimeIntervalKind.Second or TimeIntervalKind.Minute => 60,
        TimeIntervalKind.Hour => 24,
        TimeIntervalKind.Day => 31,
        _ => 12,
    };

    /// <summary>
    ///     Gets the duration of a fixed-length kind in milliseconds.
    /// </summary>
    /// <param name="kind">The kind of boundary.</param>
    /// <returns>The length, or 0 for the calendar-based kinds.</returns>
    private static double FixedLength(TimeIntervalKind kind) => kind switch
    {
        TimeIntervalKind.Millisecond => 1,
        TimeIntervalKind.Second => 1000,
        TimeIntervalKind.Minute => 60_000,
        TimeIntervalKind.Hour => 3_600_000,
        _ => 0,
    };

    /// <summary>
    ///     Floors a plain interval.
    /// </summary>
    /// <param name="t">A valid instant, or NaN.</param>
    /// <returns>The boundary or NaN.</returns>
    private double PlainFloor(double t)
    {
        if (double.IsNaN(t))
        {
            return t;
        }

        var p = _calendar.ToParts(t);
        if (!p.IsValid)
        {
            return double.NaN;
        }

        // The time-of-day kinds subtract the local components (the result can leave the range); the others rebuild the wall time at the start of the unit
        return _kind switch
        {
            TimeIntervalKind.Millisecond => t,
            TimeIntervalKind.Second => Checked(t - p.Milliseconds),
            TimeIntervalKind.Minute => Checked(t - p.Milliseconds - (p.Seconds * 1000.0)),
            TimeIntervalKind.Hour => Checked(t - p.Milliseconds - (p.Seconds * 1000.0) - (p.Minutes * 60_000.0)),
            TimeIntervalKind.Day => _calendar.FromParts(p.Year, p.Month, p.Date, 0, 0, 0, 0),
            TimeIntervalKind.Week => _calendar.FromParts(p.Year, p.Month, p.Date - p.Day, 0, 0, 0, 0),
            TimeIntervalKind.Month => _calendar.FromParts(p.Year, p.Month, 1, 0, 0, 0, 0),
            _ => _calendar.FromParts(p.Year, 0, 1, 0, 0, 0, 0),
        };
    }

    /// <summary>
    ///     Offsets a plain interval by whole steps.
    /// </summary>
    /// <param name="t">A valid instant.</param>
    /// <param name="n">The finite whole number of steps.</param>
    /// <returns>The new instant or NaN.</returns>
    private double PlainOffset(double t, double n)
    {
        var length = FixedLength(_kind);
        if (length > 0)
        {
            var moved = t + (n * length);
            return TimeCalendar.IsValid(moved) ? moved : double.NaN;
        }

        // The calendar kinds change one civil field and keep the rest of the wall time, as the JavaScript setters do
        var p = _calendar.ToParts(t);
        if (!p.IsValid)
        {
            return double.NaN;
        }

        return _kind switch
        {
            TimeIntervalKind.Day => _calendar.FromParts(p.Year, p.Month, p.Date + n, p.Hours, p.Minutes, p.Seconds, p.Milliseconds),
            TimeIntervalKind.Week => _calendar.FromParts(p.Year, p.Month, p.Date + (n * 7), p.Hours, p.Minutes, p.Seconds, p.Milliseconds),
            TimeIntervalKind.Month => _calendar.FromParts(p.Year, p.Month + n, p.Date, p.Hours, p.Minutes, p.Seconds, p.Milliseconds),
            _ => _calendar.FromParts(p.Year + n, p.Month, p.Date, p.Hours, p.Minutes, p.Seconds, p.Milliseconds),
        };
    }

    /// <summary>
    ///     Measures the distance between two plain boundaries in intervals (the <c>count</c> function of the d3 intervals).
    /// </summary>
    /// <param name="start">The floored start.</param>
    /// <param name="end">The floored end.</param>
    /// <returns>The distance, possibly fractional before the caller floors it.</returns>
    private double PlainDistance(double start, double end)
    {
        var length = FixedLength(_kind);
        if (length > 0)
        {
            return (end - start) / length;
        }

        // Days and weeks correct for a change of offset between the two boundaries
        if (_kind == TimeIntervalKind.Day || _kind == TimeIntervalKind.Week)
        {
            var span = _kind == TimeIntervalKind.Day ? TimeCalendar.MillisecondsPerDay : 7 * TimeCalendar.MillisecondsPerDay;
            var shift = _calendar.UtcOffsetMilliseconds(end) - _calendar.UtcOffsetMilliseconds(start);
            return (end - start + shift) / span;
        }

        var from = _calendar.ToParts(start);
        var to = _calendar.ToParts(end);
        return _kind == TimeIntervalKind.Month
            ? to.Month - from.Month + ((to.Year - from.Year) * 12.0)
            : to.Year - from.Year;
    }

    /// <summary>
    ///     Floors a filter interval: the latest boundary whose field is a multiple of the step.
    /// </summary>
    /// <param name="t">A valid instant, or NaN.</param>
    /// <returns>The boundary or NaN.</returns>
    private double FilterFloor(double t)
    {
        // d3: while (floor(date), !test(date)) date -= 1 ms
        var current = t;
        for (var i = 0; i < MaxScan && !double.IsNaN(current); i++)
        {
            current = PlainFloor(current);
            if (double.IsNaN(current) || Test(current))
            {
                return current;
            }

            current -= 1;
        }

        return double.NaN;
    }

    /// <summary>
    ///     Offsets a filter interval by a number of steps.
    /// </summary>
    /// <param name="t">A valid instant.</param>
    /// <param name="n">The finite whole number of steps.</param>
    /// <returns>The new instant, or NaN when the step is beyond <see cref="MaxFilterOffset"/> or the result is out of range.</returns>
    private double FilterOffset(double t, double n)
    {
        if (Math.Abs(n) > MaxFilterOffset)
        {
            return double.NaN;
        }

        // Each step moves by one underlying interval until the test passes, in either direction
        var direction = n < 0 ? -1 : 1;
        var current = t;
        for (var remaining = Math.Abs(n); remaining > 0 && !double.IsNaN(current); remaining--)
        {
            var scanned = 0;
            do
            {
                current = PlainOffset(current, direction);
                scanned++;
            }
            while (!double.IsNaN(current) && !Test(current) && scanned < MaxScan);

            if (scanned >= MaxScan && !double.IsNaN(current) && !Test(current))
            {
                return double.NaN;
            }
        }

        return current;
    }

    /// <summary>
    ///     Tests the field of a floored instant against the step of a filter interval.
    /// </summary>
    /// <param name="t">A valid, floored instant.</param>
    /// <returns>True when the boundary belongs to the filtered interval.</returns>
    private bool Test(double t)
    {
        if (_kind == TimeIntervalKind.Week)
        {
            // d3 has no field for weeks: the number of weeks since the epoch must be a multiple of the step
            return PlainDistance(PlainFloor(0), t) % _step == 0;
        }

        double field;
        if (_kind == TimeIntervalKind.Second)
        {
            // d3 reads the UTC second, which is the same in every zone
            field = TimeCalendar.FloorMod((long)Math.Floor(t / 1000.0), 60);
        }
        else
        {
            var p = _calendar.ToParts(t);
            field = _kind switch
            {
                TimeIntervalKind.Minute => p.Minutes,
                TimeIntervalKind.Hour => p.Hours,
                TimeIntervalKind.Day => p.Date - 1,
                _ => p.Month,
            };
        }

        return field % _step == 0;
    }

    /// <summary>
    ///     Floors a closed-form interval (<c>millisecond.every(k)</c> or <c>year.every(k)</c>).
    /// </summary>
    /// <param name="t">A valid instant, or NaN.</param>
    /// <returns>The boundary or NaN.</returns>
    private double ClosedFloor(double t)
    {
        if (double.IsNaN(t))
        {
            return t;
        }

        if (_kind == TimeIntervalKind.Millisecond)
        {
            var floored = Math.Floor(t / _step) * _step;
            return TimeCalendar.IsValid(floored) ? floored : double.NaN;
        }

        var p = _calendar.ToParts(t);
        return p.IsValid ? _calendar.FromParts(Math.Floor(p.Year / _step) * _step, 0, 1, 0, 0, 0, 0) : double.NaN;
    }

    /// <summary>
    ///     Offsets a closed-form interval.
    /// </summary>
    /// <param name="t">A valid instant.</param>
    /// <param name="n">The finite whole number of steps.</param>
    /// <returns>The new instant or NaN.</returns>
    private double ClosedOffset(double t, double n)
    {
        if (_kind == TimeIntervalKind.Millisecond)
        {
            var moved = t + (n * _step);
            return TimeCalendar.IsValid(moved) ? moved : double.NaN;
        }

        var p = _calendar.ToParts(t);
        return p.IsValid
            ? _calendar.FromParts(p.Year + (n * _step), p.Month, p.Date, p.Hours, p.Minutes, p.Seconds, p.Milliseconds)
            : double.NaN;
    }
}
