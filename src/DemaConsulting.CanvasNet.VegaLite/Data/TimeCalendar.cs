// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The broken-down wall-clock time of an instant in one calendar: the result of <see cref="TimeCalendar.ToParts"/>.
/// </summary>
/// <remarks>
///     The fields follow the JavaScript <c>Date</c> getters (months and weekdays are zero based, the day of the year is
///     one based), so the ported d3 and Vega code reads like the original. A value for an instant outside the valid range
///     (or a non-finite one) has <see cref="IsValid"/> false and all other fields zero.
/// </remarks>
/// <param name="Year">The year (the proleptic Gregorian year; 0 and 10000 can appear at the very edge of the range).</param>
/// <param name="Month">The month, 0 (January) to 11.</param>
/// <param name="Date">The day of the month, 1 to 31.</param>
/// <param name="Day">The day of the week, 0 (Sunday) to 6.</param>
/// <param name="DayOfYear">The day of the year, 1 to 366.</param>
/// <param name="Hours">The hour, 0 to 23.</param>
/// <param name="Minutes">The minute, 0 to 59.</param>
/// <param name="Seconds">The second, 0 to 59.</param>
/// <param name="Milliseconds">The millisecond, 0 to 999.</param>
/// <param name="OffsetMs">The offset of the wall time from UTC in milliseconds at that instant.</param>
/// <param name="IsValid">Whether the instant was valid; when false the other fields carry no meaning.</param>
internal readonly record struct DateParts(
    int Year,
    int Month,
    int Date,
    int Day,
    int DayOfYear,
    int Hours,
    int Minutes,
    int Seconds,
    int Milliseconds,
    double OffsetMs,
    bool IsValid);

/// <summary>
///     The calendar of one chart: the conversions between an instant (milliseconds since the Unix epoch, as a
///     <see cref="double"/>) and its wall-clock parts in UTC or in one caller-supplied time zone.
/// </summary>
/// <remarks>
///     <para>
///         Vega and d3 do their calendar arithmetic through the JavaScript <c>Date</c> object, whose local time is the
///         host's. A renderer that must give the same picture on every machine cannot do that, so this type replaces the
///         host zone with the <see cref="TimeZoneInfo"/> passed to <see cref="Create"/> (null means UTC) and never reads
///         the host zone, the clock or the culture. The year, month, day and time-of-day arithmetic is integer based
///         (days from civil and civil from days), so it needs no <see cref="DateTime"/> and cannot overflow or throw.
///     </para>
///     <para>
///         The valid range is exactly the range of <see cref="DataValue.FromDate"/> (years 1 to 9999): any result outside
///         it, any non-finite input and any invalid parts give <see cref="double.NaN"/>, instead of JavaScript's wider range
///         and <c>Invalid Date</c>. Instants are whole milliseconds, like a JavaScript <c>Date</c> (a fraction is truncated
///         toward zero). Every method is a total function of its <see cref="double"/> arguments.
///     </para>
///     <para>
///         Converting a wall time to an instant follows ECMAScript: an hour that occurs twice (the clock goes back) takes
///         the earlier instant, an hour that does not occur (the clock jumps forward) is read with the offset from before
///         the jump, so 02:30 in a spring-forward gap becomes the instant that shows 03:30. The rule assumes at most one
///         offset change within 48 hours, which holds for every real zone. See <see cref="WallToUtc"/>.
///     </para>
///     <para>
///         The type is immutable (it only caches its interval objects) and safe for concurrent use; a
///         <see cref="TimeZoneInfo"/> is read-only as well.
///     </para>
/// </remarks>
internal sealed partial class TimeCalendar
{
    /// <summary>The number of milliseconds in a day.</summary>
    internal const double MillisecondsPerDay = 86_400_000.0;

    /// <summary>The number of days between 0000-03-01 and 1970-01-01 used by the civil-date algorithms.</summary>
    private const long CivilEpochShift = 719_468;

    /// <summary>The number of days in a 400-year Gregorian cycle.</summary>
    private const long DaysPer400Years = 146_097;

    /// <summary>The largest absolute year or month count accepted before a result is declared out of range.</summary>
    private const double MaxMagnitude = 1e7;

    /// <summary>The largest absolute day count accepted before a result is declared out of range.</summary>
    private const double MaxDayMagnitude = 1e15;

    /// <summary>The smallest wall time (as a millisecond count) that can still map to a valid instant.</summary>
    private const double MinWallMilliseconds = DataValue.MinDateMilliseconds - (2 * MillisecondsPerDay);

    /// <summary>The largest wall time (as a millisecond count) that can still map to a valid instant.</summary>
    private const double MaxWallMilliseconds = DataValue.MaxDateMilliseconds + (2 * MillisecondsPerDay);

    /// <summary>The cached interval objects, one per kind.</summary>
    private readonly TimeInterval?[] _intervals = new TimeInterval?[Enum.GetValues<TimeIntervalKind>().Length];

    /// <summary>Whether the offset from UTC never changes (UTC itself and fixed-offset zones).</summary>
    private readonly bool _isFixed;

    /// <summary>The constant offset in milliseconds when <see cref="_isFixed"/> is true.</summary>
    private readonly double _fixedOffsetMs;

    /// <summary>
    ///     Initializes a calendar.
    /// </summary>
    /// <param name="zone">The zone, or null for UTC.</param>
    /// <param name="isFixed">Whether the offset is constant.</param>
    /// <param name="fixedOffsetMs">The constant offset in milliseconds (used when <paramref name="isFixed"/>).</param>
    private TimeCalendar(TimeZoneInfo? zone, bool isFixed, double fixedOffsetMs)
    {
        Zone = zone;
        _isFixed = isFixed;
        _fixedOffsetMs = fixedOffsetMs;
    }

    /// <summary>Gets the UTC calendar (a singleton); its <see cref="IsUtc"/> is true.</summary>
    public static TimeCalendar Utc { get; } = new(null, true, 0.0);

    /// <summary>Gets a value indicating whether the wall time is UTC (the offset is always zero).</summary>
    public bool IsUtc => _isFixed && _fixedOffsetMs == 0.0;

    /// <summary>Gets the zone behind this calendar, for diagnostics only; null for UTC.</summary>
    public TimeZoneInfo? Zone { get; }

    /// <summary>
    ///     Creates the calendar of a time zone.
    /// </summary>
    /// <param name="zone">The zone, or null for UTC.</param>
    /// <returns>
    ///     The UTC singleton for null and for zones that are always at offset zero; a fast calendar for fixed-offset
    ///     zones (no adjustment rules); otherwise a calendar that asks the zone for its offsets.
    /// </returns>
    /// <remarks>
    ///     The zone is only read (<see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/> on UTC times), never converted through
    ///     the host's local zone. A zone with adjustment rules is never treated as fixed, even when it has no daylight
    ///     saving time, because its base offset may have changed over the years.
    /// </remarks>
    public static TimeCalendar Create(TimeZoneInfo? zone)
    {
        if (zone is null)
        {
            return Utc;
        }

        // A zone without any adjustment rule has one offset for all time, so the lookups can be skipped
        if (!zone.SupportsDaylightSavingTime && zone.GetAdjustmentRules().Length == 0)
        {
            var offset = zone.BaseUtcOffset.TotalMilliseconds;
            return offset == 0.0 ? Utc : new TimeCalendar(zone, true, offset);
        }

        return new TimeCalendar(zone, false, 0.0);
    }

    /// <summary>
    ///     Tells whether a number is an instant this calendar can represent.
    /// </summary>
    /// <param name="utcMs">The milliseconds since the Unix epoch.</param>
    /// <returns>True when the number is finite and inside the range of <see cref="DataValue.FromDate"/>.</returns>
    public static bool IsValid(double utcMs) =>
        double.IsFinite(utcMs) && utcMs >= DataValue.MinDateMilliseconds && utcMs <= DataValue.MaxDateMilliseconds;

    /// <summary>
    ///     Breaks an instant into its wall-clock parts in this calendar.
    /// </summary>
    /// <param name="utcMs">The milliseconds since the Unix epoch; a fraction is truncated.</param>
    /// <returns>The parts, or a value with <see cref="DateParts.IsValid"/> false for an invalid instant.</returns>
    public DateParts ToParts(double utcMs)
    {
        if (!IsValid(utcMs))
        {
            return default;
        }

        // The wall time is the instant shifted by the offset in force at that instant
        var instant = Math.Truncate(utcMs);
        var offset = UtcOffsetMilliseconds(instant);
        return PartsFromWall(instant + offset, offset);
    }

    /// <summary>
    ///     Builds the instant of a wall time, following ECMAScript <c>MakeDay</c>, <c>MakeTime</c> and <c>MakeDate</c>.
    /// </summary>
    /// <param name="year">The year, taken as given (the 1900 shift of years 0 to 99 is the caller's job).</param>
    /// <param name="month">The month from 0; values outside 0 to 11 roll the year, as in JavaScript.</param>
    /// <param name="date">The day of the month from 1; 0 and negative values roll back, large values roll forward.</param>
    /// <param name="hours">The hours; overflow rolls into the day.</param>
    /// <param name="minutes">The minutes; overflow rolls into the hour.</param>
    /// <param name="seconds">The seconds; overflow rolls into the minute.</param>
    /// <param name="milliseconds">The milliseconds; overflow rolls into the second.</param>
    /// <returns>
    ///     The instant, or NaN when an argument is not finite or the result is outside the valid range. Fractions of
    ///     every argument are truncated toward zero.
    /// </returns>
    public double FromParts(double year, double month, double date, double hours, double minutes, double seconds, double milliseconds)
    {
        if (!double.IsFinite(year) || !double.IsFinite(month) || !double.IsFinite(date)
            || !double.IsFinite(hours) || !double.IsFinite(minutes) || !double.IsFinite(seconds) || !double.IsFinite(milliseconds))
        {
            return double.NaN;
        }

        // MakeDay: the month carries into the year, the date is an offset from the first of that month
        var day = MakeDay(Math.Truncate(year), Math.Truncate(month), Math.Truncate(date));
        if (double.IsNaN(day))
        {
            return double.NaN;
        }

        // MakeTime and MakeDate: plain double arithmetic, exactly like the JavaScript operations
        var time = (Math.Truncate(hours) * 3_600_000.0) + (Math.Truncate(minutes) * 60_000.0)
            + (Math.Truncate(seconds) * 1000.0) + Math.Truncate(milliseconds);
        var wall = (day * MillisecondsPerDay) + time;

        // Walls outside the margin cannot map to a valid instant, and this check keeps the zone lookups in range
        if (!(wall >= MinWallMilliseconds && wall <= MaxWallMilliseconds))
        {
            return double.NaN;
        }

        var utc = WallToUtc(wall);
        return IsValid(utc) ? utc : double.NaN;
    }

    /// <summary>
    ///     Gets the weekday of the first of January of a year, independent of any zone.
    /// </summary>
    /// <param name="year">The year; values that no calendar can represent give 0.</param>
    /// <returns>The weekday, 0 (Sunday) to 6.</returns>
    internal static int FirstWeekdayOfYear(double year)
    {
        if (!double.IsFinite(year) || Math.Abs(year) > MaxMagnitude)
        {
            return 0;
        }

        return (int)FloorMod(DaysFromCivil((long)Math.Truncate(year), 1, 1) + 4, 7);
    }

    /// <summary>
    ///     Gets the number of days from 1970-01-01 to a civil date (days from civil).
    /// </summary>
    /// <param name="year">The proleptic Gregorian year.</param>
    /// <param name="month">The month from 1 to 12.</param>
    /// <param name="day">The day of the month from 1; larger or smaller values simply add days.</param>
    /// <returns>The day number; negative before 1970.</returns>
    internal static long DaysFromCivil(long year, int month, long day)
    {
        // The year starts in March so the leap day is the last day of the year
        var y = month <= 2 ? year - 1 : year;
        var era = FloorDiv(y, 400);
        var yearOfEra = y - (era * 400);
        var dayOfYear = (((153 * (month + (month > 2 ? -3 : 9))) + 2) / 5) + day - 1;
        var dayOfEra = (yearOfEra * 365) + (yearOfEra / 4) - (yearOfEra / 100) + dayOfYear;
        return (era * DaysPer400Years) + dayOfEra - CivilEpochShift;
    }

    /// <summary>
    ///     Converts a day number to a civil date (civil from days).
    /// </summary>
    /// <param name="days">The days from 1970-01-01.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month from 1 to 12.</param>
    /// <param name="day">The day of the month from 1.</param>
    internal static void CivilFromDays(long days, out long year, out int month, out int day)
    {
        var z = days + CivilEpochShift;
        var era = FloorDiv(z, DaysPer400Years);
        var dayOfEra = z - (era * DaysPer400Years);
        var yearOfEra = (dayOfEra - (dayOfEra / 1460) + (dayOfEra / 36524) - (dayOfEra / 146096)) / 365;
        var dayOfYear = dayOfEra - ((365 * yearOfEra) + (yearOfEra / 4) - (yearOfEra / 100));
        var shiftedMonth = ((5 * dayOfYear) + 2) / 153;
        day = (int)(dayOfYear - (((153 * shiftedMonth) + 2) / 5) + 1);
        month = (int)(shiftedMonth < 10 ? shiftedMonth + 3 : shiftedMonth - 9);
        year = yearOfEra + (era * 400) + (month <= 2 ? 1 : 0);
    }

    /// <summary>
    ///     Divides rounding toward negative infinity.
    /// </summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>The floor of the quotient.</returns>
    internal static long FloorDiv(long value, long divisor)
    {
        var quotient = value / divisor;
        return (value % divisor < 0) ? quotient - 1 : quotient;
    }

    /// <summary>
    ///     Takes the remainder with the sign of the divisor (always non-negative for a positive divisor).
    /// </summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>A value from 0 up to the divisor.</returns>
    internal static long FloorMod(long value, long divisor) => value - (FloorDiv(value, divisor) * divisor);

    /// <summary>
    ///     Breaks a wall time (a millisecond count on the wall clock) into parts.
    /// </summary>
    /// <param name="wall">The whole-millisecond wall time; must be within the margin around the valid range.</param>
    /// <param name="offset">The offset that produced the wall time, copied into the result.</param>
    /// <returns>The parts of a valid result.</returns>
    private static DateParts PartsFromWall(double wall, double offset)
    {
        var milliseconds = (long)wall;
        var days = FloorDiv(milliseconds, 86_400_000L);
        var msOfDay = (int)(milliseconds - (days * 86_400_000L));
        CivilFromDays(days, out var year, out var month, out var day);
        var dayOfYear = (int)(days - DaysFromCivil(year, 1, 1)) + 1;
        return new DateParts(
            (int)year,
            month - 1,
            day,
            (int)FloorMod(days + 4, 7),
            dayOfYear,
            msOfDay / 3_600_000,
            msOfDay / 60_000 % 60,
            msOfDay / 1000 % 60,
            msOfDay % 1000,
            offset,
            true);
    }

    /// <summary>
    ///     The ECMAScript <c>MakeDay</c> operation on whole numbers.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, any whole number.</param>
    /// <param name="date">The day of the month, any whole number.</param>
    /// <returns>The day number from 1970-01-01, or NaN when the arguments are too large to matter.</returns>
    private static double MakeDay(double year, double month, double date)
    {
        // The month carries into the year; the magnitudes are limited so that the integer arithmetic cannot overflow
        var carriedYear = year + Math.Floor(month / 12.0);
        if (Math.Abs(carriedYear) > MaxMagnitude || Math.Abs(date) > MaxDayMagnitude)
        {
            return double.NaN;
        }

        var monthIndex = (int)(month - (12.0 * Math.Floor(month / 12.0)));
        return DaysFromCivil((long)carriedYear, monthIndex + 1, (long)date);
    }
}
