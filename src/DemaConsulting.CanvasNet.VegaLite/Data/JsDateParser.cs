// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The date parsing of JavaScript's <c>Date.parse</c>, which Vega uses for <c>parse: "date"</c> and for date type
///     inference: the ECMAScript date-time string format plus a documented subset of the legacy forms.
/// </summary>
/// <remarks>
///     <para>
///         The ECMAScript format is <c>YYYY</c>, <c>YYYY-MM</c> or <c>YYYY-MM-DD</c> (also <c>±YYYYYY</c> years), optionally
///         followed by <c>T</c> or a space and <c>HH:mm</c>, <c>HH:mm:ss</c> or <c>HH:mm:ss.fff</c> (any number of fraction
///         digits, truncated to milliseconds) and an offset (<c>Z</c>, <c>±HH:mm</c> or <c>±HHmm</c>). A date without a time is UTC; a date
///         with a time but without an offset is in the zone of the calendar passed in, exactly the mixed rule of
///         JavaScript; an offset is honored. The time <c>24:00</c> is the end of the day, other hours above 23 are
///         rejected, and a day that does not exist (<c>2023-02-29</c>) is rejected.
///     </para>
///     <para>
///         The legacy subset (all local unless a zone is given; month and weekday names in any case, full or at least three
///         letters; a parenthesized comment is ignored): <c>Jan 2, 2006</c>, <c>January 2, 2006 10:00</c>, <c>2 Jan 2006</c>,
///         <c>Mon Jan 02 2006 15:04:05 GMT+0200</c>, <c>Tue, 02 Jan 2006 15:04:05 GMT</c>, <c>2006/01/02</c> and <c>1/2/2006</c>
///         (month, day, year), with an optional <c>h:mm[:ss[.fff]]</c> time and <c>AM</c> or <c>PM</c>, and a zone
///         <c>UTC</c>, <c>GMT</c>, <c>UT</c>, <c>Z</c>, optionally followed by an offset, or just an offset <c>±hh[:]mm</c>. Years in
///         these forms have four digits. Anything else is not a date; V8 accepts more (two-digit years, a nonexistent day that
///         rolls over, and so on), which is a documented deviation.
///     </para>
///     <para>
///         The parser is a hand scanner (no <c>DateTime.Parse</c>, no regular expression, no culture), never throws, and
///         rejects text longer than <see cref="MaxLength"/> characters before looking at it, so a huge cell costs no more
///         than a short one. The result is inside the range of <see cref="TimeCalendar"/> (years 1 to 9999) or the parse fails.
///     </para>
/// </remarks>
internal static partial class JsDateParser
{
    /// <summary>The longest text that is examined.</summary>
    internal const int MaxLength = 128;

    /// <summary>
    ///     Parses a date the way <c>Date.parse</c> does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="calendar">The calendar whose zone is used for a date-time without an offset.</param>
    /// <param name="ms">The instant in milliseconds since the Unix epoch, or NaN when the text is not a date.</param>
    /// <returns>True when the text is a date inside the range of the calendar.</returns>
    public static bool TryParse(string text, TimeCalendar calendar, out double ms)
    {
        ms = double.NaN;
        if (text.Length == 0 || text.Length > MaxLength)
        {
            return false;
        }

        var iso = TryParseIso(text, calendar, out var value);
        if (iso == IsoResult.NoMatch)
        {
            value = ParseLegacy(text, calendar);
        }

        ms = value;
        return !double.IsNaN(ms);
    }

    /// <summary>
    ///     Tells how many days a month has.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <returns>The number of days.</returns>
    internal static int DaysInMonth(long year, int month)
    {
        if (month == 2)
        {
            var leap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
            return leap ? 29 : 28;
        }

        return month is 4 or 6 or 9 or 11 ? 30 : 31;
    }

    /// <summary>
    ///     Turns the digits after a decimal point into milliseconds (the first three digits, padded with zeros).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position of the first fraction digit.</param>
    /// <param name="length">The number of fraction digits.</param>
    /// <returns>The milliseconds, 0 to 999.</returns>
    internal static int FractionToMilliseconds(string text, int start, int length)
    {
        var value = 0;
        for (var i = 0; i < 3; i++)
        {
            value = (value * 10) + (i < length ? text[start + i] - '0' : 0);
        }

        return value;
    }

    /// <summary>
    ///     Reads exactly <paramref name="count"/> ASCII digits.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position; advanced past the digits on success.</param>
    /// <param name="count">The number of digits.</param>
    /// <param name="value">The number.</param>
    /// <returns>True when the digits were there.</returns>
    private static bool ReadDigits(string text, ref int position, int count, out int value)
    {
        value = 0;
        if (position + count > text.Length)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            var c = text[position + i];
            if (!TimeFormat.IsAsciiDigit(c))
            {
                return false;
            }

            value = (value * 10) + (c - '0');
        }

        position += count;
        return true;
    }

    /// <summary>
    ///     Reads the date part of the ECMAScript format: the year, optionally with month and day.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position; advanced past the date.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month from 1 (1 when absent).</param>
    /// <param name="day">The day from 1 (1 when absent).</param>
    /// <returns>The result: no match, or invalid values, or OK.</returns>
    private static IsoResult ReadIsoDate(string text, ref int position, out int year, out int month, out int day)
    {
        month = 1;
        day = 1;
        var sign = 0;
        if (text[0] == '+' || text[0] == '-')
        {
            sign = text[0] == '-' ? -1 : 1;
            position = 1;
        }

        if (!ReadDigits(text, ref position, sign == 0 ? 4 : 6, out year))
        {
            return IsoResult.NoMatch;
        }

        if (sign < 0)
        {
            // The year -000000 is not allowed
            year = -year;
            if (year == 0)
            {
                return IsoResult.Invalid;
            }
        }

        if (position < text.Length && text[position] == '-')
        {
            position++;
            if (!ReadDigits(text, ref position, 2, out month))
            {
                return IsoResult.NoMatch;
            }

            if (position < text.Length && text[position] == '-')
            {
                position++;
                if (!ReadDigits(text, ref position, 2, out day))
                {
                    return IsoResult.NoMatch;
                }
            }
        }

        return month is >= 1 and <= 12 && day >= 1 && day <= DaysInMonth(year, month) ? IsoResult.Ok : IsoResult.Invalid;
    }

    /// <summary>
    ///     Reads the time of day of the ECMAScript format after the separator.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position; advanced past the time.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <param name="second">The second.</param>
    /// <param name="millisecond">The millisecond.</param>
    /// <returns>The result.</returns>
    private static IsoResult ReadIsoTime(string text, ref int position, out int hour, out int minute, out int second, out int millisecond)
    {
        second = 0;
        millisecond = 0;
        minute = 0;
        if (!ReadDigits(text, ref position, 2, out hour) || position >= text.Length || text[position] != ':')
        {
            return IsoResult.NoMatch;
        }

        position++;
        if (!ReadDigits(text, ref position, 2, out minute))
        {
            return IsoResult.NoMatch;
        }

        if (position < text.Length && text[position] == ':')
        {
            position++;
            if (!ReadDigits(text, ref position, 2, out second))
            {
                return IsoResult.NoMatch;
            }

            if (position < text.Length && text[position] == '.')
            {
                var start = ++position;
                while (position < text.Length && TimeFormat.IsAsciiDigit(text[position]))
                {
                    position++;
                }

                if (position == start)
                {
                    return IsoResult.NoMatch;
                }

                millisecond = FractionToMilliseconds(text, start, position - start);
            }
        }

        // 24:00:00 is the end of the day; no other hour above 23 exists
        var valid = hour < 24 && minute < 60 && second < 60;
        if (hour == 24)
        {
            valid = minute == 0 && second == 0 && millisecond == 0;
        }

        return valid ? IsoResult.Ok : IsoResult.Invalid;
    }

    /// <summary>
    ///     Reads the optional zone designator of the ECMAScript format: <c>Z</c>, <c>±HH:mm</c>, <c>±HHmm</c> or <c>±HH</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position; advanced past the zone.</param>
    /// <param name="hasZone">Whether a zone was present.</param>
    /// <param name="offsetMinutes">The offset in minutes east of UTC.</param>
    /// <returns>The result; NoMatch when other text follows.</returns>
    private static IsoResult ReadIsoZone(string text, ref int position, out bool hasZone, out int offsetMinutes)
    {
        hasZone = false;
        offsetMinutes = 0;
        if (position >= text.Length)
        {
            return IsoResult.Ok;
        }

        var c = text[position];
        if (c == 'Z')
        {
            hasZone = true;
            position++;
            return position == text.Length ? IsoResult.Ok : IsoResult.NoMatch;
        }

        if (c != '+' && c != '-')
        {
            return IsoResult.NoMatch;
        }

        position++;
        var minutes = 0;
        if (!ReadDigits(text, ref position, 2, out var hours))
        {
            return IsoResult.NoMatch;
        }

        if (position < text.Length)
        {
            if (text[position] == ':')
            {
                position++;
            }

            if (!ReadDigits(text, ref position, 2, out minutes))
            {
                return IsoResult.NoMatch;
            }
        }

        if (position != text.Length)
        {
            return IsoResult.NoMatch;
        }

        hasZone = true;
        offsetMinutes = (c == '-' ? -1 : 1) * ((hours * 60) + minutes);
        return hours < 24 && minutes < 60 ? IsoResult.Ok : IsoResult.Invalid;
    }

    /// <summary>
    ///     Tries the ECMAScript date-time string format.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="calendar">The calendar for a time without an offset.</param>
    /// <param name="ms">The instant, or NaN.</param>
    /// <returns>No match (try the legacy forms), invalid (a date-shaped text with impossible values) or OK.</returns>
    private static IsoResult TryParseIso(string text, TimeCalendar calendar, out double ms)
    {
        ms = double.NaN;
        var position = 0;
        var result = ReadIsoDate(text, ref position, out var year, out var month, out var day);
        if (result != IsoResult.Ok)
        {
            return result;
        }

        // A date without a time is UTC
        if (position == text.Length)
        {
            ms = TimeCalendar.Utc.FromParts(year, month - 1, day, 0, 0, 0, 0);
            return IsoResult.Ok;
        }

        if (text[position] is not ('T' or 't' or ' '))
        {
            return IsoResult.NoMatch;
        }

        position++;
        result = ReadIsoTime(text, ref position, out var hour, out var minute, out var second, out var millisecond);
        if (result == IsoResult.Ok)
        {
            result = ReadIsoZone(text, ref position, out var hasZone, out var offset);
            if (result == IsoResult.Ok)
            {
                ms = hasZone
                    ? TimeCalendar.Utc.FromParts(year, month - 1, day, hour, minute - offset, second, millisecond)
                    : calendar.FromParts(year, month - 1, day, hour, minute, second, millisecond);
            }
        }

        return result;
    }

    /// <summary>The outcome of looking at the ECMAScript format.</summary>
    private enum IsoResult
    {
        /// <summary>The text is not in the format (the legacy forms may still apply).</summary>
        NoMatch,

        /// <summary>The text has the shape of the format but impossible values (it is not a date).</summary>
        Invalid,

        /// <summary>The text was read.</summary>
        Ok,
    }
}
