// Portions derived from d3 (ISC); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     A JavaScript <c>Date</c> whose local time zone is UTC: a time value in milliseconds since 1970-01-01T00:00Z, or
///     NaN for an invalid date, with the getters and setters of the ECMAScript specification.
/// </summary>
/// <remarks>
///     <para>
///         Mermaid computes gantt dates with JavaScript <c>Date</c> objects in the viewer's local time zone; the port
///         fixes that zone to UTC (no daylight saving time), so every local getter and setter equals its UTC
///         counterpart and <see cref="TimezoneOffset"/> is zero. The range is the ECMAScript range of ±8.64e15
///         milliseconds (about ±275,760 years), wider than <see cref="DateTime"/>; a result outside it is invalid
///         (<c>TimeClip</c>). <c>MakeDay</c> also refuses the years and months V8 refuses (beyond ±1,000,000 years and
///         ±10,000,000 months), which only turns values that would be clipped anyway into NaN earlier.
///     </para>
///     <para>
///         JavaScript dates are mutable; this value is immutable, so each setter returns the changed date (the caller
///         assigns it back where the JavaScript code mutates). The getters of an invalid date are NaN, as in JavaScript.
///         The type is a value and safe for concurrent use.
///     </para>
/// </remarks>
internal readonly struct JsDate : IEquatable<JsDate>
{
    /// <summary>The milliseconds of a day.</summary>
    public const double MsPerDay = 86_400_000;

    /// <summary>The milliseconds of an hour.</summary>
    public const double MsPerHour = 3_600_000;

    /// <summary>The milliseconds of a minute.</summary>
    public const double MsPerMinute = 60_000;

    /// <summary>The milliseconds of a second.</summary>
    public const double MsPerSecond = 1_000;

    /// <summary>The largest time value of a valid date (ECMAScript's <c>TimeClip</c> bound).</summary>
    private const double MaxTime = 8.64e15;

    /// <summary>The largest year magnitude V8's <c>MakeDay</c> accepts.</summary>
    private const double MaxYear = 1_000_000;

    /// <summary>The largest month magnitude V8's <c>MakeDay</c> accepts.</summary>
    private const double MaxMonth = 10_000_000;

    /// <summary>
    ///     Initializes a date from a time value that is already clipped (use <see cref="FromTime"/> otherwise).
    /// </summary>
    /// <param name="time">The time value, or NaN.</param>
    private JsDate(double time) => Time = time;

    /// <summary>
    ///     Gets the invalid date (<c>new Date(NaN)</c>).
    /// </summary>
    public static JsDate Invalid => new(double.NaN);

    /// <summary>
    ///     Gets the time value in milliseconds since 1970-01-01T00:00Z (<c>valueOf</c>), or NaN for an invalid date.
    /// </summary>
    public double Time { get; }

    /// <summary>
    ///     Gets whether the date is valid (its time value is not NaN).
    /// </summary>
    public bool IsValid => !double.IsNaN(Time);

    /// <summary>
    ///     Gets the year (<c>getFullYear</c>), or NaN.
    /// </summary>
    public double FullYear => IsValid ? CivilFromDays(DayOf(Time)).Year : double.NaN;

    /// <summary>
    ///     Gets the zero-based month (<c>getMonth</c>), or NaN.
    /// </summary>
    public double Month => IsValid ? CivilFromDays(DayOf(Time)).Month - 1 : double.NaN;

    /// <summary>
    ///     Gets the day of the month, from 1 (<c>getDate</c>), or NaN.
    /// </summary>
    public double Date => IsValid ? CivilFromDays(DayOf(Time)).Day : double.NaN;

    /// <summary>
    ///     Gets the day of the week, 0 for Sunday (<c>getDay</c>), or NaN.
    /// </summary>
    public double Day => IsValid ? PositiveModulo(DayOf(Time) + 4, 7) : double.NaN;

    /// <summary>
    ///     Gets the hours (<c>getHours</c>), or NaN.
    /// </summary>
    public double Hours => IsValid ? Math.Floor(TimeWithinDay(Time) / MsPerHour) : double.NaN;

    /// <summary>
    ///     Gets the minutes (<c>getMinutes</c>), or NaN.
    /// </summary>
    public double Minutes => IsValid ? PositiveModulo(Math.Floor(Time / MsPerMinute), 60) : double.NaN;

    /// <summary>
    ///     Gets the seconds (<c>getSeconds</c>), or NaN.
    /// </summary>
    public double Seconds => IsValid ? PositiveModulo(Math.Floor(Time / MsPerSecond), 60) : double.NaN;

    /// <summary>
    ///     Gets the milliseconds (<c>getMilliseconds</c>), or NaN.
    /// </summary>
    public double Milliseconds => IsValid ? PositiveModulo(Time, 1000) : double.NaN;

    /// <summary>
    ///     Gets the time zone offset in minutes (<c>getTimezoneOffset</c>): zero, or NaN for an invalid date.
    /// </summary>
    public double TimezoneOffset => IsValid ? 0 : double.NaN;

    /// <summary>
    ///     Creates a date from a time value, as <c>new Date(number)</c> does.
    /// </summary>
    /// <param name="time">The time value; NaN, infinities and values beyond ±8.64e15 give the invalid date.</param>
    /// <returns>The date, with the time value truncated to an integer.</returns>
    public static JsDate FromTime(double time) => new(TimeClip(time));

    /// <summary>
    ///     Creates a date from its local (here UTC) components, as <c>new Date(year, month, date, hours, minutes,
    ///     seconds, ms)</c> does: a year from 0 to 99 means 1900 to 1999, and overflowing components carry.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The zero-based month.</param>
    /// <param name="date">The day of the month.</param>
    /// <param name="hours">The hours.</param>
    /// <param name="minutes">The minutes.</param>
    /// <param name="seconds">The seconds.</param>
    /// <param name="ms">The milliseconds.</param>
    /// <returns>The date, invalid when a component is not finite or the result is out of range.</returns>
    public static JsDate FromComponents(double year, double month, double date = 1, double hours = 0, double minutes = 0, double seconds = 0, double ms = 0)
    {
        // The constructor maps the two-digit years to the twentieth century
        var y = year;
        if (!double.IsNaN(y))
        {
            var integer = ToInteger(y);
            if (integer is >= 0 and <= 99)
            {
                y = 1900 + integer;
            }
        }

        return FromTime(MakeDate(MakeDay(y, month, date), MakeTime(hours, minutes, seconds, ms)));
    }

    /// <summary>
    ///     Gets the date with another year and optionally month and day (<c>setFullYear</c>); an invalid date counts as
    ///     time zero here, as in JavaScript.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The zero-based month, or null to keep it.</param>
    /// <param name="date">The day of the month, or null to keep it.</param>
    /// <returns>The date.</returns>
    public JsDate SetFullYear(double year, double? month = null, double? date = null)
    {
        var t = IsValid ? Time : 0;
        var civil = CivilFromDays(DayOf(t));
        var m = month ?? civil.Month - 1;
        var dt = date ?? civil.Day;
        return FromTime(MakeDate(MakeDay(year, m, dt), TimeWithinDay(t)));
    }

    /// <summary>
    ///     Gets the date with another month and optionally day (<c>setMonth</c>); an invalid date stays invalid.
    /// </summary>
    /// <param name="month">The zero-based month.</param>
    /// <param name="date">The day of the month, or null to keep it.</param>
    /// <returns>The date.</returns>
    public JsDate SetMonth(double month, double? date = null)
    {
        if (!IsValid)
        {
            return Invalid;
        }

        var civil = CivilFromDays(DayOf(Time));
        return FromTime(MakeDate(MakeDay(civil.Year, month, date ?? civil.Day), TimeWithinDay(Time)));
    }

    /// <summary>
    ///     Gets the date with another day of the month (<c>setDate</c>); an invalid date stays invalid.
    /// </summary>
    /// <param name="date">The day of the month; values outside the month carry.</param>
    /// <returns>The date.</returns>
    public JsDate SetDate(double date)
    {
        if (!IsValid)
        {
            return Invalid;
        }

        var civil = CivilFromDays(DayOf(Time));
        return FromTime(MakeDate(MakeDay(civil.Year, civil.Month - 1, date), TimeWithinDay(Time)));
    }

    /// <summary>
    ///     Gets the date with another time of day (<c>setHours</c>); an invalid date stays invalid.
    /// </summary>
    /// <param name="hours">The hours.</param>
    /// <param name="minutes">The minutes, or null to keep them.</param>
    /// <param name="seconds">The seconds, or null to keep them.</param>
    /// <param name="ms">The milliseconds, or null to keep them.</param>
    /// <returns>The date.</returns>
    public JsDate SetHours(double hours, double? minutes = null, double? seconds = null, double? ms = null)
    {
        if (!IsValid)
        {
            return Invalid;
        }

        var time = MakeTime(hours, minutes ?? Minutes, seconds ?? Seconds, ms ?? Milliseconds);
        return FromTime(MakeDate(DayOf(Time), time));
    }

    /// <summary>
    ///     Gets the date with other minutes (<c>setMinutes</c>); an invalid date stays invalid.
    /// </summary>
    /// <param name="minutes">The minutes.</param>
    /// <param name="seconds">The seconds, or null to keep them.</param>
    /// <param name="ms">The milliseconds, or null to keep them.</param>
    /// <returns>The date.</returns>
    public JsDate SetMinutes(double minutes, double? seconds = null, double? ms = null)
    {
        if (!IsValid)
        {
            return Invalid;
        }

        var time = MakeTime(Hours, minutes, seconds ?? Seconds, ms ?? Milliseconds);
        return FromTime(MakeDate(DayOf(Time), time));
    }

    /// <summary>
    ///     Gets the date with other seconds (<c>setSeconds</c>); an invalid date stays invalid.
    /// </summary>
    /// <param name="seconds">The seconds.</param>
    /// <param name="ms">The milliseconds, or null to keep them.</param>
    /// <returns>The date.</returns>
    public JsDate SetSeconds(double seconds, double? ms = null)
    {
        if (!IsValid)
        {
            return Invalid;
        }

        var time = MakeTime(Hours, Minutes, seconds, ms ?? Milliseconds);
        return FromTime(MakeDate(DayOf(Time), time));
    }

    /// <summary>
    ///     Gets the date with other milliseconds (<c>setMilliseconds</c>); an invalid date stays invalid.
    /// </summary>
    /// <param name="ms">The milliseconds.</param>
    /// <returns>The date.</returns>
    public JsDate SetMilliseconds(double ms)
    {
        if (!IsValid)
        {
            return Invalid;
        }

        return FromTime(MakeDate(DayOf(Time), MakeTime(Hours, Minutes, Seconds, ms)));
    }

    /// <summary>
    ///     Gets the date as an ISO 8601 text (<c>toISOString</c>, with a six-digit signed year outside 0 to 9999), for
    ///     messages and tests.
    /// </summary>
    /// <returns>The text, or <c>Invalid Date</c>.</returns>
    public override string ToString()
    {
        if (!IsValid)
        {
            return "Invalid Date";
        }

        var civil = CivilFromDays(DayOf(Time));
        string year;
        if (civil.Year is >= 0 and <= 9999)
        {
            year = civil.Year.ToString("0000", CultureInfo.InvariantCulture);
        }
        else
        {
            var sign = civil.Year < 0 ? "-" : "+";
            year = sign + Math.Abs(civil.Year).ToString("000000", CultureInfo.InvariantCulture);
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{year}-{civil.Month:00}-{civil.Day:00}T{Hours:00}:{Minutes:00}:{Seconds:00}.{Milliseconds:000}Z");
    }

    /// <inheritdoc/>
    public bool Equals(JsDate other) => BitConverter.DoubleToInt64Bits(Time) == BitConverter.DoubleToInt64Bits(other.Time);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is JsDate other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Time.GetHashCode();

    /// <summary>Compares two dates by time value (equal invalid dates are equal here, unlike JavaScript's ==).</summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether they are equal.</returns>
    public static bool operator ==(JsDate left, JsDate right) => left.Equals(right);

    /// <summary>Compares two dates by time value.</summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether they differ.</returns>
    public static bool operator !=(JsDate left, JsDate right) => !left.Equals(right);

    /// <summary>Compares two dates as JavaScript's <c>&lt;</c> does (false when either is invalid).</summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether the first is earlier.</returns>
    public static bool operator <(JsDate left, JsDate right) => left.Time < right.Time;

    /// <summary>Compares two dates as JavaScript's <c>&gt;</c> does (false when either is invalid).</summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether the first is later.</returns>
    public static bool operator >(JsDate left, JsDate right) => left.Time > right.Time;

    /// <summary>Compares two dates as JavaScript's <c>&lt;=</c> does (false when either is invalid).</summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether the first is not later.</returns>
    public static bool operator <=(JsDate left, JsDate right) => left.Time <= right.Time;

    /// <summary>Compares two dates as JavaScript's <c>&gt;=</c> does (false when either is invalid).</summary>
    /// <param name="left">The first date.</param>
    /// <param name="right">The second date.</param>
    /// <returns>Whether the first is not earlier.</returns>
    public static bool operator >=(JsDate left, JsDate right) => left.Time >= right.Time;

    /// <summary>
    ///     ECMAScript's <c>MakeDay</c>: the day number of a year, month and day, with the month carried into the year.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The zero-based month.</param>
    /// <param name="date">The day of the month.</param>
    /// <returns>The day number, or NaN.</returns>
    internal static double MakeDay(double year, double month, double date)
    {
        if (!double.IsFinite(year) || !double.IsFinite(month) || !double.IsFinite(date))
        {
            return double.NaN;
        }

        // V8 refuses years and months far beyond the date range before computing
        var y = ToInteger(year);
        var m = ToInteger(month);
        if (Math.Abs(y) > MaxYear || Math.Abs(m) > MaxMonth)
        {
            return double.NaN;
        }

        // Carry the month into the year, then count the days of the first of that month
        var ym = (long)y + (long)Math.Floor(m / 12);
        var mn = (int)PositiveModulo(m, 12);
        return DaysFromCivil(ym, mn + 1, 1) + ToInteger(date) - 1;
    }

    /// <summary>
    ///     ECMAScript's <c>MakeTime</c>: the milliseconds of a time of day.
    /// </summary>
    /// <param name="hours">The hours.</param>
    /// <param name="minutes">The minutes.</param>
    /// <param name="seconds">The seconds.</param>
    /// <param name="ms">The milliseconds.</param>
    /// <returns>The milliseconds, or NaN.</returns>
    internal static double MakeTime(double hours, double minutes, double seconds, double ms)
    {
        if (!double.IsFinite(hours) || !double.IsFinite(minutes) || !double.IsFinite(seconds) || !double.IsFinite(ms))
        {
            return double.NaN;
        }

        return (ToInteger(hours) * MsPerHour + ToInteger(minutes) * MsPerMinute) + ToInteger(seconds) * MsPerSecond + ToInteger(ms);
    }

    /// <summary>
    ///     ECMAScript's <c>MakeDate</c>: the time value of a day number and a time of day.
    /// </summary>
    /// <param name="day">The day number.</param>
    /// <param name="time">The milliseconds of the day.</param>
    /// <returns>The time value, or NaN.</returns>
    internal static double MakeDate(double day, double time)
    {
        if (!double.IsFinite(day) || !double.IsFinite(time))
        {
            return double.NaN;
        }

        var value = day * MsPerDay + time;
        return double.IsFinite(value) ? value : double.NaN;
    }

    /// <summary>
    ///     ECMAScript's <c>TimeClip</c>: NaN beyond ±8.64e15, else the value truncated toward zero.
    /// </summary>
    /// <param name="time">The time value.</param>
    /// <returns>The clipped time value.</returns>
    internal static double TimeClip(double time) =>
        !double.IsFinite(time) || Math.Abs(time) > MaxTime ? double.NaN : ToInteger(time);

    /// <summary>
    ///     ECMAScript's <c>ToIntegerOrInfinity</c> for finite values: truncation toward zero, with -0 as 0.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The integer.</returns>
    internal static double ToInteger(double value) => double.IsNaN(value) ? 0 : Math.Truncate(value) + 0.0;

    /// <summary>
    ///     The day number of a time value (<c>Day(t)</c>).
    /// </summary>
    /// <param name="time">The time value.</param>
    /// <returns>The day number.</returns>
    private static double DayOf(double time) => Math.Floor(time / MsPerDay);

    /// <summary>
    ///     The milliseconds since midnight of a time value (<c>TimeWithinDay(t)</c>).
    /// </summary>
    /// <param name="time">The time value.</param>
    /// <returns>The milliseconds.</returns>
    private static double TimeWithinDay(double time) => PositiveModulo(time, MsPerDay);

    /// <summary>
    ///     The mathematical modulo (the sign of the divisor), as ECMAScript's <c>modulo</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>The remainder, from zero to the divisor.</returns>
    private static double PositiveModulo(double value, double divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder + 0.0;
    }

    /// <summary>
    ///     The day number of a proleptic Gregorian date (Howard Hinnant's <c>days_from_civil</c>).
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, from 1.</param>
    /// <param name="day">The day of the month, from 1.</param>
    /// <returns>The days since 1970-01-01.</returns>
    private static double DaysFromCivil(long year, int month, int day)
    {
        var y = month <= 2 ? year - 1 : year;
        var era = (y >= 0 ? y : y - 399) / 400;
        var yoe = y - era * 400;
        var doy = (153 * (month + (month > 2 ? -3 : 9)) + 2) / 5 + day - 1;
        var doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
        return era * 146097 + doe - 719468;
    }

    /// <summary>
    ///     The proleptic Gregorian date of a day number (Howard Hinnant's <c>civil_from_days</c>).
    /// </summary>
    /// <param name="days">The days since 1970-01-01; an integer.</param>
    /// <returns>The year, the month from 1 and the day from 1.</returns>
    private static (long Year, int Month, int Day) CivilFromDays(double days)
    {
        var z = (long)days + 719468;
        var era = (z >= 0 ? z : z - 146096) / 146097;
        var doe = z - era * 146097;
        var yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        var doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        var mp = (5 * doy + 2) / 153;
        var day = (int)(doy - (153 * mp + 2) / 5 + 1);
        var month = (int)(mp < 10 ? mp + 3 : mp - 9);
        var year = yoe + era * 400 + (month <= 2 ? 1 : 0);
        return (year, month, day);
    }
}

/// <summary>
///     A d3-time interval (<c>timeInterval</c>): a calendar unit with its floor, offset, ceiling, range, count and
///     <c>every</c> functions, on <see cref="JsDate"/> values whose local time zone is UTC.
/// </summary>
/// <remarks>
///     <para>
///         d3 mutates its dates; the delegates here return the changed date instead. A filtered interval (from
///         <see cref="Every"/> or <see cref="Filter"/>) walks the base interval until its test passes; where d3 would
///         loop for ever on an invalid date (the walk stepped outside the date range), the port stops and returns the
///         invalid date. The optional step callback of <see cref="Range"/>, <see cref="Floor"/> and
///         <see cref="Ceil"/> is called once per step of those walks, so the caller can meter the work.
///     </para>
///     <para>
///         An interval is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class TimeInterval
{
    /// <summary>The floor function, with the step callback.</summary>
    private readonly Func<JsDate, Action?, JsDate> _floor;

    /// <summary>The offset function (the step is an integer), with the step callback.</summary>
    private readonly Func<JsDate, double, Action?, JsDate> _offset;

    /// <summary>The count function between two floored dates, or null when the interval has none.</summary>
    private readonly Func<JsDate, JsDate, double>? _count;

    /// <summary>The field function of <c>every</c>, or null to count from the epoch.</summary>
    private readonly Func<JsDate, double>? _field;

    /// <summary>The replacement of <c>every</c> (millisecond and year), or null for the generic one.</summary>
    private readonly Func<double, TimeInterval?>? _every;

    /// <summary>
    ///     Initializes an interval, as d3's <c>timeInterval(floor, offset, count, field)</c>.
    /// </summary>
    /// <param name="floor">The floor function.</param>
    /// <param name="offset">The offset function.</param>
    /// <param name="count">The count function, or null for an interval without <c>count</c> and <c>every</c>.</param>
    /// <param name="field">The field function of <c>every</c>, or null.</param>
    /// <param name="every">A replacement of <c>every</c>, or null.</param>
    public TimeInterval(
        Func<JsDate, JsDate> floor,
        Func<JsDate, double, JsDate> offset,
        Func<JsDate, JsDate, double>? count = null,
        Func<JsDate, double>? field = null,
        Func<double, TimeInterval?>? every = null)
        : this((date, _) => floor(date), (date, step, _) => offset(date, step), count, field, every)
    {
    }

    /// <summary>
    ///     Initializes an interval from functions that take the step callback.
    /// </summary>
    /// <param name="floor">The floor function.</param>
    /// <param name="offset">The offset function.</param>
    /// <param name="count">The count function, or null.</param>
    /// <param name="field">The field function, or null.</param>
    /// <param name="every">A replacement of <c>every</c>, or null.</param>
    private TimeInterval(
        Func<JsDate, Action?, JsDate> floor,
        Func<JsDate, double, Action?, JsDate> offset,
        Func<JsDate, JsDate, double>? count,
        Func<JsDate, double>? field,
        Func<double, TimeInterval?>? every)
    {
        _floor = floor;
        _offset = offset;
        _count = count;
        _field = field;
        _every = every;
    }

    /// <summary>
    ///     Gets whether the interval has <c>count</c> and <c>every</c> (d3's filtered and special intervals do not).
    /// </summary>
    public bool HasCount => _count is not null;

    /// <summary>
    ///     Gets the latest boundary at or before a date (<c>interval.floor</c>).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="onStep">Called per walk step of a filtered interval, or null.</param>
    /// <returns>The boundary.</returns>
    public JsDate Floor(JsDate date, Action? onStep = null) => _floor(date, onStep);

    /// <summary>
    ///     Gets the earliest boundary at or after a date (<c>interval.ceil</c>).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="onStep">Called per walk step of a filtered interval, or null.</param>
    /// <returns>The boundary.</returns>
    public JsDate Ceil(JsDate date, Action? onStep = null) => CeilOf(date.Time, onStep);

    /// <summary>
    ///     Gets the earliest boundary at or after a time value, as d3's <c>ceil</c> does for a number: the floor of the
    ///     value minus one millisecond, moved by one interval and floored again.
    /// </summary>
    /// <param name="value">The time value.</param>
    /// <param name="onStep">Called per walk step of a filtered interval, or null.</param>
    /// <returns>The boundary.</returns>
    private JsDate CeilOf(double value, Action? onStep)
    {
        var d = _floor(JsDate.FromTime(value - 1), onStep);
        d = _offset(d, 1, onStep);
        return _floor(d, onStep);
    }

    /// <summary>
    ///     Gets a date moved by whole intervals (<c>interval.offset</c>), without flooring it.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="step">The number of intervals; floored.</param>
    /// <returns>The moved date.</returns>
    public JsDate Offset(JsDate date, double step = 1) => _offset(date, Math.Floor(step), null);

    /// <summary>
    ///     Gets the boundaries from the ceiling of a start up to, not including, a stop (<c>interval.range</c>).
    /// </summary>
    /// <param name="start">The start time value.</param>
    /// <param name="stop">The stop time value.</param>
    /// <param name="step">Every how many boundaries; floored; empty when not positive.</param>
    /// <param name="onStep">Called per boundary and per walk step of a filtered interval, or null.</param>
    /// <returns>The boundaries.</returns>
    public List<JsDate> Range(double start, double stop, double step = 1, Action? onStep = null)
    {
        var range = new List<JsDate>();
        var current = CeilOf(start, onStep);
        step = Math.Floor(step);
        if (!(current.Time < stop) || !(step > 0))
        {
            return range;
        }

        // Step from boundary to boundary while time advances and stays before the stop
        JsDate previous;
        do
        {
            onStep?.Invoke();
            previous = current;
            range.Add(previous);
            current = _offset(current, step, onStep);
            current = _floor(current, onStep);
        }
        while (previous < current && current.Time < stop);

        return range;
    }

    /// <summary>
    ///     Gets the number of boundaries after a start and up to an end (<c>interval.count</c>).
    /// </summary>
    /// <param name="start">The start time value.</param>
    /// <param name="end">The end time value.</param>
    /// <returns>The count, floored; NaN for an invalid date.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the interval has no count.</exception>
    public double Count(double start, double end)
    {
        if (_count is null)
        {
            throw new InvalidOperationException("The interval has no count.");
        }

        var t0 = _floor(JsDate.FromTime(start), null);
        var t1 = _floor(JsDate.FromTime(end), null);
        return Math.Floor(_count(t0, t1));
    }

    /// <summary>
    ///     Gets the interval of every so many boundaries (<c>interval.every</c>): those whose field (or count from the
    ///     epoch) is a multiple of the step.
    /// </summary>
    /// <param name="step">The step; floored.</param>
    /// <returns>The interval, this one for a step of one, or null for a step that is not positive or finite.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the interval has no count.</exception>
    public TimeInterval? Every(double step)
    {
        if (_every is not null)
        {
            return _every(step);
        }

        if (_count is null)
        {
            throw new InvalidOperationException("The interval has no every.");
        }

        step = Math.Floor(step);
        if (!double.IsFinite(step) || !(step > 0))
        {
            return null;
        }

        if (!(step > 1))
        {
            return this;
        }

        var field = _field;
        return field is not null
            ? Filter(d => field(d) % step == 0)
            : Filter(d => Count(0, d.Time) % step == 0);
    }

    /// <summary>
    ///     Gets the interval of the boundaries that pass a test (<c>interval.filter</c>).
    /// </summary>
    /// <param name="test">The test.</param>
    /// <returns>The filtered interval, without count.</returns>
    public TimeInterval Filter(Func<JsDate, bool> test)
    {
        var floor = _floor;
        var offset = _offset;
        return new TimeInterval(
            (date, onStep) =>
            {
                // Floor, then step back one millisecond at a time until a boundary passes
                if (!date.IsValid)
                {
                    return date;
                }

                while (true)
                {
                    date = floor(date, onStep);
                    if (!date.IsValid || test(date))
                    {
                        return date;
                    }

                    onStep?.Invoke();
                    date = JsDate.FromTime(date.Time - 1);
                }
            },
            (date, step, onStep) =>
            {
                // Move one base interval at a time until a boundary passes, once per step
                if (!date.IsValid)
                {
                    return date;
                }

                var direction = step < 0 ? -1 : 1;
                for (var remaining = Math.Abs(step); remaining > 0; remaining--)
                {
                    do
                    {
                        onStep?.Invoke();
                        date = offset(date, direction, onStep);
                    }
                    while (date.IsValid && !test(date));

                    if (!date.IsValid)
                    {
                        return date;
                    }
                }

                return date;
            },
            null,
            null,
            null);
    }
}
