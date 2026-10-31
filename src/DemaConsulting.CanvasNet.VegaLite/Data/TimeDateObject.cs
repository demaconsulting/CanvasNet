// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     A Vega-Lite <c>DateTime</c> object (<c>{"year": 2020, "month": "Mar", "date": 5}</c>): a point in time given by
///     calendar parts, read in the zone of the chart or in UTC.
/// </summary>
/// <remarks>
///     <para>
///         The keys are <c>year</c>, <c>quarter</c>, <c>month</c>, <c>date</c>, <c>day</c>, <c>hours</c>, <c>minutes</c>,
///         <c>seconds</c>, <c>milliseconds</c> and <c>utc</c>. A month is <c>1</c> to <c>12</c> or a month name (full or the
///         first three letters, any case); a quarter is <c>1</c> to <c>4</c> and stands for its first month (the month wins
///         when both are given); a day is <c>1</c> (Monday) to <c>7</c> or <c>0</c> (Sunday) or a day name and stands for that
///         day of January 2012, a Sunday-starting year. Like Vega-Lite, the object drops its <c>day</c> when it has any other
///         key (the <c>utc</c> flag included), because a weekday only makes sense on its own. A missing year is 2012, a
///         missing month January, a missing date the first, a missing time part zero. Parts that are too large roll
///         over (month 13 is January of the next year, date 32 the first of the next month) as in the JavaScript
///         <c>Date</c> constructor.
///     </para>
///     <para>
///         <see cref="ToMilliseconds"/> evaluates the object like Vega's <c>datetime(...)</c> (local) or <c>utc(...)</c>.
///         <see cref="FromScalar(double, TimeUnitSpec?, TimeCalendar)"/> and <see cref="FromScalar(string, TimeUnitSpec?, TimeCalendar, Func{string, double}?)"/> are
///         Vega-Lite's rule for a bare number or string compared with a field that has a time unit. The type is
///         immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class DateTimeObject
{
    /// <summary>The full month names in lower case.</summary>
    private static readonly string[] Months =
        ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];

    /// <summary>The full day names in lower case, Sunday first.</summary>
    private static readonly string[] Days = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    /// <summary>The keys of a <c>DateTime</c> object.</summary>
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal)
    {
        "year", "quarter", "month", "date", "day", "hours", "minutes", "seconds", "milliseconds", "utc",
    };

    /// <summary>The year, or the reference year 2012 when the object has none.</summary>
    private readonly double _year;

    /// <summary>The month from 0 (from <c>month</c> or three times the zero-based quarter).</summary>
    private readonly double _month;

    /// <summary>The day of the month (from <c>date</c>, or the weekday plus one).</summary>
    private readonly double _date;

    /// <summary>The hours.</summary>
    private readonly double _hours;

    /// <summary>The minutes.</summary>
    private readonly double _minutes;

    /// <summary>The seconds.</summary>
    private readonly double _seconds;

    /// <summary>The milliseconds.</summary>
    private readonly double _milliseconds;

    /// <summary>
    ///     Initializes an object from normalized parts.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month from 0.</param>
    /// <param name="date">The day of the month.</param>
    /// <param name="time">The hours, minutes, seconds and milliseconds.</param>
    /// <param name="utc">Whether the object is in UTC.</param>
    /// <param name="dayDropped">Whether a <c>day</c> was dropped.</param>
    /// <param name="quarterOutOfRange">Whether the quarter was above 4.</param>
    private DateTimeObject(double year, double month, double date, (double Hours, double Minutes, double Seconds, double Milliseconds) time, bool utc, bool dayDropped, bool quarterOutOfRange)
    {
        _year = year;
        _month = month;
        _date = date;
        _hours = time.Hours;
        _minutes = time.Minutes;
        _seconds = time.Seconds;
        _milliseconds = time.Milliseconds;
        Utc = utc;
        DayDropped = dayDropped;
        QuarterOutOfRange = quarterOutOfRange;
    }

    /// <summary>Gets a value indicating whether the object is in UTC (<c>"utc": true</c>) rather than in the zone of the chart.</summary>
    public bool Utc { get; }

    /// <summary>Gets a value indicating whether the object had a <c>day</c> together with other keys, which Vega-Lite drops (and warns about).</summary>
    public bool DayDropped { get; }

    /// <summary>Gets a value indicating whether the quarter was above 4 (Vega-Lite warns and goes on).</summary>
    public bool QuarterOutOfRange { get; }

    /// <summary>
    ///     Reads a <c>DateTime</c> object.
    /// </summary>
    /// <param name="node">The object node.</param>
    /// <param name="value">The object.</param>
    /// <param name="error">The reason when the node is not a valid <c>DateTime</c> object.</param>
    /// <param name="at">The node the error is about (the member, or the object itself).</param>
    /// <returns>True when the node is a valid <c>DateTime</c> object.</returns>
    public static bool TryRead(SpecNode node, [NotNullWhen(true)] out DateTimeObject? value, out string? error, out SpecNode? at)
    {
        value = null;
        error = null;
        at = node;
        if (!node.IsObject)
        {
            error = "A datetime must be an object.";
            return false;
        }

        // Every member must be known and have the right kind of value
        foreach (var property in node.Properties)
        {
            if (!ValidateMember(property, out error))
            {
                at = property;
                return false;
            }
        }

        // Vega-Lite drops the day when anything else is present
        var dayDropped = node["day"] is not null && node.Properties.Count > 1;
        double? quarter = null;
        double? month = null;
        double? day = null;
        if (node["month"] is { } monthNode && !TryNormalizeMonth(monthNode, out month))
        {
            error = $"'{Describe(monthNode)}' is not a month.";
            at = monthNode;
            return false;
        }

        if (node["quarter"] is { } quarterNode && !TryNormalizeNumber(quarterNode, out quarter))
        {
            error = $"'{Describe(quarterNode)}' is not a quarter.";
            at = quarterNode;
            return false;
        }

        if (!dayDropped && node["day"] is { } dayNode && !TryNormalizeDay(dayNode, out day))
        {
            error = $"'{Describe(dayNode)}' is not a day.";
            at = dayNode;
            return false;
        }

        value = Create(node, month, quarter, day, dayDropped);
        return true;
    }

    /// <summary>
    ///     Evaluates the object like Vega's <c>datetime(...)</c> or <c>utc(...)</c> function.
    /// </summary>
    /// <param name="zoneCalendar">The calendar of local times; ignored when the object is in UTC.</param>
    /// <returns>The instant in milliseconds since the Unix epoch, or NaN when out of range.</returns>
    public double ToMilliseconds(TimeCalendar zoneCalendar)
    {
        var calendar = Utc ? TimeCalendar.Utc : zoneCalendar;
        return calendar.FromParts(
            FullYear(_year),
            _month,
            _date,
            _hours,
            _minutes,
            _seconds,
            _milliseconds);
    }

    /// <summary>
    ///     Reads a number compared with a field that has a time unit (Vega-Lite's <c>valueExpr</c>).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="unit">The time unit of the field, or null for a temporal field without one.</param>
    /// <param name="zoneCalendar">The calendar of local times.</param>
    /// <returns>
    ///     For a single local unit and a number below 10000 the instant of that part (the number 3 against <c>month</c> is
    ///     March 2012, the number 5 against <c>hours</c> is 05:00 on 2012-01-01); otherwise the number itself as milliseconds
    ///     since the epoch, or NaN when it is out of range.
    /// </returns>
    public static double FromScalar(double value, TimeUnitSpec? unit, TimeCalendar zoneCalendar)
    {
        if (unit is { IsSingleLocalUnit: true } && value < 10000)
        {
            return FromUnitPart(unit.Parts[0], value, null, zoneCalendar);
        }

        return TimeCalendar.IsValid(value) ? Math.Truncate(value) + 0.0 : double.NaN;
    }

    /// <summary>
    ///     Reads a string compared with a field that has a time unit (Vega-Lite's <c>valueExpr</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="unit">The time unit of the field, or null for a temporal field without one.</param>
    /// <param name="zoneCalendar">The calendar of local times.</param>
    /// <param name="parseDate">
    ///     The function that reads a date from text (JavaScript's <c>Date.parse</c>), returning NaN for text that is not a
    ///     date; null when the caller has none, which makes every text a unit part or an error.
    /// </param>
    /// <returns>
    ///     The parsed date when the text is a date; otherwise, for a single local unit, the instant of that part (the text
    ///     <c>Mar</c> against <c>month</c> is March 2012); otherwise NaN.
    /// </returns>
    public static double FromScalar(string text, TimeUnitSpec? unit, TimeCalendar zoneCalendar, Func<string, double>? parseDate)
    {
        var parsed = parseDate?.Invoke(text) ?? double.NaN;
        if (!double.IsNaN(parsed) || unit is not { IsSingleLocalUnit: true })
        {
            return parsed;
        }

        return FromUnitPart(unit.Parts[0], double.NaN, text, zoneCalendar);
    }

    /// <summary>
    ///     Maps a JavaScript <c>Date</c> constructor year to the real year: 0 to 99 mean 1900 to 1999.
    /// </summary>
    /// <param name="year">The year as given.</param>
    /// <returns>The year to use.</returns>
    internal static double FullYear(double year)
    {
        var whole = Math.Truncate(year);
        return whole is >= 0 and <= 99 ? 1900 + whole : year;
    }

    /// <summary>
    ///     Builds the object from validated members.
    /// </summary>
    /// <param name="node">The object node.</param>
    /// <param name="month">The normalized zero-based month, or null.</param>
    /// <param name="quarter">The one-based quarter, or null.</param>
    /// <param name="day">The normalized weekday, or null.</param>
    /// <param name="dayDropped">Whether the day is dropped.</param>
    /// <returns>The object.</returns>
    private static DateTimeObject Create(SpecNode node, double? month, double? quarter, double? day, bool dayDropped)
    {
        // The month wins over the quarter; the date wins over the weekday, which lands in the first week of 2012
        double monthValue = month ?? (quarter is { } q ? (q - 1) * 3 : 0);
        double date = node["date"]?.NumberValue ?? (day is { } d ? d + 1 : 1);
        return new DateTimeObject(
            node["year"]?.NumberValue ?? 2012,
            monthValue,
            date,
            (node["hours"]?.NumberValue ?? 0, node["minutes"]?.NumberValue ?? 0, node["seconds"]?.NumberValue ?? 0, node["milliseconds"]?.NumberValue ?? 0),
            node["utc"]?.BoolValue ?? false,
            dayDropped,
            quarter is > 4);
    }

    /// <summary>
    ///     Checks the name and the kind of value of one member.
    /// </summary>
    /// <param name="property">The member.</param>
    /// <param name="error">The reason when invalid.</param>
    /// <returns>True when valid.</returns>
    private static bool ValidateMember(SpecNode property, out string? error)
    {
        error = null;
        var name = property.Name!;
        if (!Keys.Contains(name))
        {
            error = $"'{name}' is not a property of a datetime.";
            return false;
        }

        // Month, quarter and day may also be names or numeric text; utc is a flag; the rest are numbers
        var valid = name switch
        {
            "month" or "quarter" or "day" => property.IsNumber || property.IsString,
            "utc" => property.IsBoolean,
            _ => property.IsNumber,
        };
        if (!valid)
        {
            error = name == "utc" ? "The 'utc' of a datetime must be true or false." : $"The '{name}' of a datetime has the wrong type.";
        }

        return valid;
    }

    /// <summary>
    ///     Describes a member value for an error message.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The text of a string, otherwise the number as JavaScript prints it.</returns>
    private static string Describe(SpecNode node) => node.IsString ? node.StringValue! : JsNumber.ToString(node.NumberValue);

    /// <summary>
    ///     Reads a whole number that may be written as a number or as numeric text.
    /// </summary>
    /// <param name="node">The member value.</param>
    /// <param name="value">The number.</param>
    /// <returns>True when the member is a number or numeric text.</returns>
    private static bool TryNormalizeNumber(SpecNode node, [NotNullWhen(true)] out double? value)
    {
        value = null;
        if (node.IsNumber)
        {
            value = node.NumberValue;
            return true;
        }

        if (TryReadNumericText(node.StringValue!, out var number))
        {
            value = number;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Reads a month (Vega-Lite's <c>normalizeMonth</c>).
    /// </summary>
    /// <param name="node">The member value.</param>
    /// <param name="value">The month from 0.</param>
    /// <returns>True when the member is a month number or a month name.</returns>
    private static bool TryNormalizeMonth(SpecNode node, [NotNullWhen(true)] out double? value)
    {
        if (TryNormalizeNumber(node, out var number))
        {
            value = number - 1;
            return true;
        }

        return TryFindName(node.StringValue!, Months, out value);
    }

    /// <summary>
    ///     Reads a day of the week (Vega-Lite's <c>normalizeDay</c>).
    /// </summary>
    /// <param name="node">The member value.</param>
    /// <param name="value">The weekday, 0 for Sunday.</param>
    /// <returns>True when the member is a day number or a day name.</returns>
    private static bool TryNormalizeDay(SpecNode node, [NotNullWhen(true)] out double? value)
    {
        if (TryNormalizeNumber(node, out var number))
        {
            // The remainder makes both 0 and 7 a Sunday
            value = number % 7;
            return true;
        }

        return TryFindName(node.StringValue!, Days, out value);
    }

    /// <summary>
    ///     Finds a name by its full lower-case form or by its first three letters.
    /// </summary>
    /// <param name="text">The text, in any case.</param>
    /// <param name="names">The full lower-case names.</param>
    /// <param name="index">The index of the name.</param>
    /// <returns>True when a name matches.</returns>
    private static bool TryFindName(string text, string[] names, [NotNullWhen(true)] out double? index)
    {
        var lower = text.ToLowerInvariant();
        var found = Array.IndexOf(names, lower);
        if (found < 0)
        {
            // Vega-Lite compares the first three letters of the text with the short names
            var shortName = lower.Length > 3 ? lower[..3] : lower;
            found = Array.FindIndex(names, name => name[..3] == shortName);
        }

        index = found >= 0 ? found : null;
        return found >= 0;
    }

    /// <summary>
    ///     Reads numeric text the way JavaScript's number conversion does for a finite number.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="number">The number.</param>
    /// <returns>True when the text is non-empty and a finite number.</returns>
    private static bool TryReadNumericText(string text, out double number)
    {
        number = 0;
        return text.Trim().Length > 0 && JsNumber.TryParse(text, out number) && double.IsFinite(number);
    }

    /// <summary>
    ///     Evaluates a single part of a time unit with the defaults for the others (Vega-Lite's <c>dateTimeToExpr({[unit]: value})</c>).
    /// </summary>
    /// <param name="part">The part: <c>year</c>, <c>quarter</c>, <c>month</c>, <c>week</c>, <c>day</c>, <c>dayofyear</c>, <c>date</c>, <c>hours</c>, <c>minutes</c>, <c>seconds</c> or <c>milliseconds</c>.</param>
    /// <param name="number">The number, or NaN when the value is the text.</param>
    /// <param name="text">The text when the value is text; otherwise null.</param>
    /// <param name="calendar">The calendar of local times.</param>
    /// <returns>The instant, or NaN when the value is not valid for the part. The parts <c>week</c> and <c>dayofyear</c> have no key in a <c>DateTime</c>, so they give the default date, as in Vega-Lite.</returns>
    private static double FromUnitPart(string part, double number, string? text, TimeCalendar calendar)
    {
        double year = 2012;
        double month = 0;
        double date = 1;
        double hours = 0;
        double minutes = 0;
        double seconds = 0;
        double milliseconds = 0;
        if (!TryReadPart(part, number, text, out var value))
        {
            return double.NaN;
        }

        switch (part)
        {
            case "year":
                year = value;
                break;
            case "quarter":
                month = (value - 1) * 3;
                break;
            case "month":
                month = value;
                break;
            case "day":
                date = value + 1;
                break;
            case "date":
                date = value;
                break;
            case "hours":
                hours = value;
                break;
            case "minutes":
                minutes = value;
                break;
            case "seconds":
                seconds = value;
                break;
            case "milliseconds":
                milliseconds = value;
                break;
            default:
                // week and dayofyear cannot be written in a datetime object
                break;
        }

        return calendar.FromParts(FullYear(year), month, date, hours, minutes, seconds, milliseconds);
    }

    /// <summary>
    ///     Reads the value of a part from a number or from text, normalized like the same key of a <c>DateTime</c> object.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="number">The number, or NaN when the value is text.</param>
    /// <param name="text">The text, or null when the value is a number.</param>
    /// <param name="value">The normalized value: a zero-based month, a weekday from 0, or the number itself.</param>
    /// <returns>True when the value is valid for the part.</returns>
    private static bool TryReadPart(string part, double number, string? text, out double value)
    {
        value = number;
        if (text is null)
        {
            // Month and quarter are one-based in an object, the day is taken modulo 7
            value = part switch
            {
                "month" => number - 1,
                "day" => number % 7,
                _ => number,
            };
            return double.IsFinite(value);
        }

        // Text stands for a name (month, day) or for numeric text
        double? name;
        bool found;
        switch (part)
        {
            case "month":
                found = TryFindName(text, Months, out name);
                break;
            case "day":
                found = TryFindName(text, Days, out name);
                break;
            default:
                found = false;
                name = null;
                break;
        }

        if (found)
        {
            value = name!.Value;
            return true;
        }

        if (!TryReadNumericText(text, out var parsed))
        {
            return false;
        }

        value = part switch
        {
            "month" => parsed - 1,
            "day" => parsed % 7,
            _ => parsed,
        };
        return true;
    }
}
