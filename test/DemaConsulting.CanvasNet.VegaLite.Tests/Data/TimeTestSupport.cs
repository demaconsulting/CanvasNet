using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Helpers shared by the time tests: time zones built in the test (so that no test reads the host's zone, clock or
///     culture), expected instants computed with the base class library, and a way to get a specification node from JSON.
///     The class holds no tests.
/// </summary>
internal static class TimeTestSupport
{
    /// <summary>The milliseconds of the anchor instant 2024-02-29T12:34:56.789Z, worked out by hand: day 19,782 plus 12:34:56.789.</summary>
    public const double AnchorMilliseconds = 1_709_210_096_789;

    /// <summary>
    ///     Gets the UTC zone.
    /// </summary>
    /// <returns>The built-in UTC zone.</returns>
    public static TimeZoneInfo Utc() => TimeZoneInfo.Utc;

    /// <summary>
    ///     Creates a zone with a fixed offset and no daylight saving time.
    /// </summary>
    /// <param name="hours">The signed whole hours.</param>
    /// <param name="minutes">The signed minutes (the same sign as the hours).</param>
    /// <returns>The zone.</returns>
    public static TimeZoneInfo Fixed(int hours, int minutes = 0) =>
        TimeZoneInfo.CreateCustomTimeZone($"Test/Fixed{hours}:{minutes}", new TimeSpan(hours, minutes, 0), "Fixed", "Fixed");

    /// <summary>
    ///     Creates a zone that follows the United States rule: standard time at the given offset (minus five hours by default),
    ///     daylight saving time (plus one hour) from the second Sunday of March at 02:00 standard time to the first Sunday of
    ///     November at 02:00 daylight time.
    /// </summary>
    /// <param name="baseOffset">The standard offset; minus five hours when null.</param>
    /// <returns>The zone. In 2024 daylight time starts at 2024-03-10T07:00Z and ends at 2024-11-03T06:00Z.</returns>
    public static TimeZoneInfo UsLike(TimeSpan? baseOffset = null) =>
        CreateRuleZone("Test/UsLike", baseOffset ?? TimeSpan.FromHours(-5), 3, 2, 11, 1, new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified));

    /// <summary>
    ///     Creates a zone whose daylight saving rule spans the new year: standard time at plus ten hours, daylight saving
    ///     time (plus one hour) from the first Sunday of October at 02:00 standard time to the first Sunday of April at 03:00
    ///     daylight time.
    /// </summary>
    /// <returns>The zone. In 2024 daylight time ends at 2024-04-06T16:00Z and starts at 2024-10-05T16:00Z.</returns>
    public static TimeZoneInfo SouthernLike() =>
        CreateRuleZone("Test/SouthernLike", TimeSpan.FromHours(10), 10, 1, 4, 1, new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), new DateTime(1, 1, 1, 3, 0, 0, DateTimeKind.Unspecified));

    /// <summary>
    ///     Gets the calendar of a zone.
    /// </summary>
    /// <param name="zone">The zone.</param>
    /// <returns>The calendar.</returns>
    public static TimeCalendar Calendar(TimeZoneInfo zone) => TimeCalendar.Create(zone);

    /// <summary>
    ///     Computes the instant of a UTC date and time with the base class library, independently of the code under test.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month from 1.</param>
    /// <param name="day">The day of the month from 1.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <param name="second">The second.</param>
    /// <param name="millisecond">The millisecond.</param>
    /// <returns>The milliseconds since the Unix epoch.</returns>
    public static double Ms(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int millisecond = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, second, millisecond, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    ///     Parses JSON into a specification node by placing it inside the <c>usermeta</c> of a minimal specification.
    /// </summary>
    /// <param name="json">The JSON value.</param>
    /// <returns>The node of the value.</returns>
    public static SpecNode Node(string json)
    {
        var tree = SpecParser.Parse("{ \"data\": { \"values\": [ { \"a\": 1 } ] }, \"mark\": \"point\", \"usermeta\": { \"v\": " + json + " } }");
        return tree.Root["usermeta"]!["v"]!;
    }

    /// <summary>
    ///     Creates a zone with one daylight saving rule that lasts for all time.
    /// </summary>
    /// <param name="id">The zone id.</param>
    /// <param name="baseOffset">The standard offset.</param>
    /// <param name="startMonth">The month the daylight saving time starts in.</param>
    /// <param name="startWeek">The week of that month (1 is the first, 5 the last).</param>
    /// <param name="endMonth">The month it ends in.</param>
    /// <param name="endWeek">The week of that month.</param>
    /// <param name="startTime">The time of day of the start, in standard time.</param>
    /// <param name="endTime">The time of day of the end, in daylight time.</param>
    /// <returns>The zone.</returns>
    private static TimeZoneInfo CreateRuleZone(string id, TimeSpan baseOffset, int startMonth, int startWeek, int endMonth, int endWeek, DateTime startTime, DateTime endTime)
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(startTime, startMonth, startWeek, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(endTime, endMonth, endWeek, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone(id, baseOffset, id, id + " standard", id + " daylight", [rule]);
    }
}
