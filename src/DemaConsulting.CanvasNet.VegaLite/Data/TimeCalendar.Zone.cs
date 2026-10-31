// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The time zone half of <see cref="TimeCalendar"/>: the offset lookup and the conversion of a wall time to an instant.
/// </summary>
internal sealed partial class TimeCalendar
{
    /// <summary>The number of ticks (100 ns) from 0001-01-01 to the Unix epoch.</summary>
    private const long UnixEpochTicks = 621_355_968_000_000_000L;

    /// <summary>The earliest instant whose offset is asked of the zone; one day inside the <see cref="DateTime"/> range.</summary>
    private const double MinProbeMilliseconds = DataValue.MinDateMilliseconds + MillisecondsPerDay;

    /// <summary>The latest instant whose offset is asked of the zone; one day inside the <see cref="DateTime"/> range.</summary>
    private const double MaxProbeMilliseconds = DataValue.MaxDateMilliseconds - MillisecondsPerDay;

    /// <summary>
    ///     Gets the offset of the wall time from UTC at an instant.
    /// </summary>
    /// <param name="utcMs">The milliseconds since the Unix epoch.</param>
    /// <returns>
    ///     The offset in milliseconds (positive east of Greenwich). Instants beyond the range of
    ///     <see cref="DateTime"/> are asked about the nearest representable instant, so the call never throws; NaN gives NaN
    ///     for a zone with daylight saving time and the constant offset for UTC and fixed zones.
    /// </returns>
    public double UtcOffsetMilliseconds(double utcMs)
    {
        // UTC and fixed-offset zones never need the zone
        if (_isFixed)
        {
            return _fixedOffsetMs;
        }

        if (double.IsNaN(utcMs))
        {
            return double.NaN;
        }

        // The probe is clamped so that the DateTime can always be built and the zone never sees its own edges
        var probe = Math.Clamp(Math.Truncate(utcMs), MinProbeMilliseconds, MaxProbeMilliseconds);
        var ticks = UnixEpochTicks + ((long)probe * TimeSpan.TicksPerMillisecond);
        return Zone!.GetUtcOffset(new DateTime(ticks, DateTimeKind.Utc)).TotalMilliseconds;
    }

    /// <summary>
    ///     Converts a wall time to an instant, resolving the hour that repeats and the hour that is skipped like ECMAScript.
    /// </summary>
    /// <param name="wall">The wall-clock time as a millisecond count (the wall time read as if it were UTC).</param>
    /// <returns>The instant; not range checked.</returns>
    /// <remarks>
    ///     The offsets one day before and one day after the wall time (read as an instant) bracket the transition that
    ///     can be nearby. A candidate instant is the wall time minus one of those offsets, and it is a real solution when
    ///     the zone reports that same offset at the candidate. Two solutions (the clock repeats) give the earlier one; no
    ///     solution (the clock jumps forward) gives the wall time minus the offset from before the jump.
    /// </remarks>
    private double WallToUtc(double wall)
    {
        if (_isFixed)
        {
            return wall - _fixedOffsetMs;
        }

        // Without a transition nearby both probes agree and the answer is unique (offsets are whole milliseconds)
        var before = (long)UtcOffsetMilliseconds(wall - MillisecondsPerDay);
        var after = (long)UtcOffsetMilliseconds(wall + MillisecondsPerDay);
        if (before == after)
        {
            return wall - before;
        }

        // A transition lies within a day of the wall time: test each offset as a solution
        var early = wall - before;
        var late = wall - after;
        var earlyHolds = (long)UtcOffsetMilliseconds(early) == before;
        var lateHolds = (long)UtcOffsetMilliseconds(late) == after;
        if (earlyHolds && lateHolds)
        {
            return Math.Min(early, late);
        }

        if (lateHolds)
        {
            return late;
        }

        // Either the earlier offset holds or the wall time was skipped; both read it with the earlier offset
        return early;
    }
}
