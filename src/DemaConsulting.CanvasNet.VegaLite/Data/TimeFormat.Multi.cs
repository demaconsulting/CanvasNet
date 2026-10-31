// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     Vega's multi-format: the default labels of a time axis.
/// </summary>
internal static partial class TimeFormat
{
    /// <summary>
    ///     Builds Vega's <c>timeMultiFormat</c> (or <c>utcMultiFormat</c> for the UTC calendar): a formatter that labels an
    ///     instant by the coarsest calendar boundary it sits on.
    /// </summary>
    /// <param name="calendar">The calendar whose intervals decide the boundary and whose wall time is formatted.</param>
    /// <param name="overrides">
    ///     The specifiers that replace the defaults, keyed by <c>milliseconds</c>, <c>seconds</c>, <c>minutes</c>,
    ///     <c>hours</c>, <c>date</c> (or <c>day</c>), <c>week</c>, <c>month</c>, <c>quarter</c> and <c>year</c>; null or a missing
    ///     or empty entry keeps the default.
    /// </param>
    /// <returns>The formatter, bound to <paramref name="calendar"/>.</returns>
    /// <remarks>
    ///     <para>
    ///         The defaults are <c>.%L</c> (not on a whole second), <c>:%S</c> (second, not a whole minute), <c>%I:%M</c>
    ///         (minute), <c>%I %p</c> (hour), <c>%a %d</c> (a day that is not a week start), <c>%b %d</c> (a week start that
    ///         is not a month start), <c>%B</c> (a month or quarter start that is not a year start) and <c>%Y</c> (a year
    ///         start). The test is Vega's: the instant is on a boundary when the interval's floor equals the instant, so a
    ///         label is as coarse as the largest boundary the instant sits on, and an invalid instant gets the text
    ///         <see cref="InvalidDateText"/>.
    ///     </para>
    ///     <para>
    ///         The cost of the call is linear in the total length of the specifiers (the caller charges it).
    ///     </para>
    /// </remarks>
    public static TimeFormatter Multi(TimeCalendar calendar, IReadOnlyDictionary<string, string>? overrides)
    {
        var millisecond = Compile(Pick(overrides, ".%L", "milliseconds"));
        var second = Compile(Pick(overrides, ":%S", "seconds"));
        var minute = Compile(Pick(overrides, "%I:%M", "minutes"));
        var hour = Compile(Pick(overrides, "%I %p", "hours"));
        var day = Compile(Pick(overrides, "%a %d", "date", "day"));
        var week = Compile(Pick(overrides, "%b %d", "week"));
        var month = Compile(Pick(overrides, "%B", "month"));
        var quarter = Compile(Pick(overrides, "%B", "quarter"));
        var year = Compile(Pick(overrides, "%Y", "year"));

        var secondInterval = calendar.Interval(TimeIntervalKind.Second);
        var minuteInterval = calendar.Interval(TimeIntervalKind.Minute);
        var hourInterval = calendar.Interval(TimeIntervalKind.Hour);
        var dayInterval = calendar.Interval(TimeIntervalKind.Day);
        var weekInterval = calendar.Interval(TimeIntervalKind.Week);
        var monthInterval = calendar.Interval(TimeIntervalKind.Month);
        var quarterInterval = monthInterval.Every(3) ?? monthInterval;
        var yearInterval = calendar.Interval(TimeIntervalKind.Year);

        return new TimeFormatter(calendar, ms =>
        {
            if (secondInterval.Floor(ms) < ms)
            {
                return millisecond;
            }

            if (minuteInterval.Floor(ms) < ms)
            {
                return second;
            }

            if (hourInterval.Floor(ms) < ms)
            {
                return minute;
            }

            if (dayInterval.Floor(ms) < ms)
            {
                return hour;
            }

            if (monthInterval.Floor(ms) < ms)
            {
                return weekInterval.Floor(ms) < ms ? day : week;
            }

            if (yearInterval.Floor(ms) < ms)
            {
                return quarterInterval.Floor(ms) < ms ? month : quarter;
            }

            return year;
        });
    }

    /// <summary>
    ///     Picks the specifier of one level: the first non-empty override under the given keys, else the default.
    /// </summary>
    /// <param name="overrides">The overrides, or null.</param>
    /// <param name="fallback">The default specifier.</param>
    /// <param name="keys">The keys to try in order (JavaScript's <c>spec[a] || spec[b] || default</c>).</param>
    /// <returns>The specifier.</returns>
    private static string Pick(IReadOnlyDictionary<string, string>? overrides, string fallback, params string[] keys)
    {
        if (overrides is not null)
        {
            foreach (var key in keys)
            {
                if (overrides.TryGetValue(key, out var value) && value.Length > 0)
                {
                    return value;
                }
            }
        }

        return fallback;
    }
}
