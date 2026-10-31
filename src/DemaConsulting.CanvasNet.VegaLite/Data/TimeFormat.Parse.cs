// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The fields a d3 time parser has read so far (d3's <c>d</c> object): the date and time parts with their defaults, and
///     the optional ones that only exist when their directive was seen.
/// </summary>
internal sealed class ParsedTimeFields
{
    /// <summary>Gets or sets the year (1900 by default).</summary>
    public double Year { get; set; } = 1900;

    /// <summary>Gets or sets the month from 0, or null when no month was read.</summary>
    public int? Month { get; set; }

    /// <summary>Gets or sets the day of the month (1 by default).</summary>
    public double Day { get; set; } = 1;

    /// <summary>Gets or sets the hour.</summary>
    public double Hour { get; set; }

    /// <summary>Gets or sets the minute.</summary>
    public double Minute { get; set; }

    /// <summary>Gets or sets the second.</summary>
    public double Second { get; set; }

    /// <summary>Gets or sets the millisecond.</summary>
    public double Millisecond { get; set; }

    /// <summary>Gets or sets the milliseconds of a <c>%Q</c> timestamp, or null.</summary>
    public double? UnixMilliseconds { get; set; }

    /// <summary>Gets or sets the seconds of a <c>%s</c> timestamp, or null.</summary>
    public double? UnixSeconds { get; set; }

    /// <summary>Gets or sets d3's zone value (the negated <c>±hhmm</c> number), or null when no zone was read.</summary>
    public int? Zone { get; set; }

    /// <summary>Gets or sets the day period, 0 (AM) or 1 (PM), or null.</summary>
    public int? Period { get; set; }

    /// <summary>Gets or sets the first month of the quarter read by <c>%q</c>, or null.</summary>
    public int? Quarter { get; set; }

    /// <summary>Gets or sets the weekday from Sunday (0), set by <c>%w</c>, <c>%a</c> and <c>%A</c> alike, or null.</summary>
    public int? WeekdaySunday { get; set; }

    /// <summary>Gets or sets the weekday number of <c>%u</c> (Monday is 1), or null.</summary>
    public int? WeekdayMonday { get; set; }

    /// <summary>Gets or sets the Sunday-based week number of <c>%U</c>, or null.</summary>
    public int? WeekSunday { get; set; }

    /// <summary>Gets or sets the Monday-based week number of <c>%W</c>, or null.</summary>
    public int? WeekMonday { get; set; }

    /// <summary>Gets or sets the ISO week number of <c>%V</c>, or null.</summary>
    public int? WeekIso { get; set; }
}

/// <summary>
///     The parser of format specifiers.
/// </summary>
internal static partial class TimeFormat
{
    /// <summary>
    ///     Parses text like d3's <c>timeParse(specifier)</c> (for a zone calendar) or <c>utcParse(specifier)</c> (for the UTC calendar).
    /// </summary>
    /// <param name="specifier">The specifier, with the directives of <see cref="Compile"/>.</param>
    /// <param name="text">The text; at most <see cref="MaxParseTextLength"/> characters, longer text fails at once.</param>
    /// <param name="calendar">The calendar the fields are read in; a zone or UTC.</param>
    /// <param name="ms">The instant, or NaN when the parse fails.</param>
    /// <returns>True when the whole text matched the specifier and the result is inside the range of the calendar.</returns>
    /// <remarks>
    ///     <para>
    ///         The parser follows d3: a literal must match exactly, names match ignoring ASCII case, a number directive reads
    ///         at most its width in characters (leading white space inside that window is allowed, a sign is not), and there is
    ///         no range validation (<c>2024-13-01</c> with <c>%Y-%m-%d</c> is 2025-01-01, hour 25 rolls into the next day). The
    ///         defaults are the year 1900, January and the first, time zero; <c>%p</c> turns the hour into a 12-hour value; a
    ///         <c>%Z</c> reads <c>Z</c> or <c>±hh[:]mm</c> and makes the other fields UTC; <c>%Q</c> and <c>%s</c> give a timestamp
    ///         directly. The text must be consumed completely.
    ///     </para>
    ///     <para>
    ///         The method never throws: any text, any specifier. The cost is linear in the text and the specifier.
    ///     </para>
    /// </remarks>
    public static bool TryParse(string specifier, string text, TimeCalendar calendar, out double ms)
    {
        ms = double.NaN;
        if (text.Length > MaxParseTextLength)
        {
            return false;
        }

        var fields = new ParsedTimeFields();
        if (ParseSpecifier(fields, specifier, text, 0) != text.Length)
        {
            return false;
        }

        ms = ToInstant(fields, calendar);
        return !double.IsNaN(ms);
    }

    /// <summary>
    ///     Walks a specifier against the text from a position (d3's <c>parseSpecifier</c>).
    /// </summary>
    /// <param name="fields">The fields that receive the values.</param>
    /// <param name="specifier">The specifier.</param>
    /// <param name="text">The text.</param>
    /// <param name="start">The position in the text.</param>
    /// <returns>The position after the match, or -1.</returns>
    private static int ParseSpecifier(ParsedTimeFields fields, string specifier, string text, int start)
    {
        var j = start;
        var i = 0;
        while (i < specifier.Length)
        {
            // Like d3, the text must have a character left for every piece of the specifier
            if (j >= text.Length)
            {
                return -1;
            }

            var c = specifier[i++];
            if (c == '%')
            {
                var directive = i < specifier.Length ? specifier[i++] : '\0';
                if (IsPadFlag(directive, out _))
                {
                    directive = i < specifier.Length ? specifier[i++] : '\0';
                }

                j = ParseDirective(fields, directive, text, j);
                if (j < 0)
                {
                    return -1;
                }
            }
            else if (c != text[j++])
            {
                return -1;
            }
        }

        return j;
    }

    /// <summary>
    ///     Parses one directive at a position.
    /// </summary>
    /// <param name="fields">The fields that receive the value.</param>
    /// <param name="directive">The directive letter (<c>\0</c> when there is none).</param>
    /// <param name="text">The text.</param>
    /// <param name="i">The position in the text.</param>
    /// <returns>The position after the directive, or -1 when it does not match or is unknown.</returns>
    private static int ParseDirective(ParsedTimeFields fields, char directive, string text, int i)
    {
        switch (directive)
        {
            case 'a':
                return ParseName(text, i, ShortDays, v => fields.WeekdaySunday = v);
            case 'A':
                return ParseName(text, i, Days, v => fields.WeekdaySunday = v);
            case 'b':
                return ParseName(text, i, ShortMonths, v => fields.Month = v);
            case 'B':
                return ParseName(text, i, Months, v => fields.Month = v);
            case 'p':
                return ParseName(text, i, Periods, v => fields.Period = v);
            case 'c':
                return ParseSpecifier(fields, DateTimeSpecifier, text, i);
            case 'x':
                return ParseSpecifier(fields, DateSpecifier, text, i);
            case 'X':
                return ParseSpecifier(fields, TimeSpecifier, text, i);
            case 'Z':
                return ParseZone(fields, text, i);
            case '%':
                return i < text.Length && text[i] == '%' ? i + 1 : -1;
            default:
                return ParseNumberDirective(fields, directive, text, i);
        }
    }

    /// <summary>
    ///     Parses the directives that read a number.
    /// </summary>
    /// <param name="fields">The fields that receive the value.</param>
    /// <param name="directive">The directive letter.</param>
    /// <param name="text">The text.</param>
    /// <param name="i">The position in the text.</param>
    /// <returns>The position after the number, or -1.</returns>
    private static int ParseNumberDirective(ParsedTimeFields fields, char directive, string text, int i)
    {
        // The window is the largest number of characters the directive may read (white space counts)
        var window = directive switch
        {
            'q' or 'u' or 'w' => 1,
            'd' or 'e' or 'g' or 'y' or 'H' or 'I' or 'm' or 'M' or 'S' or 'U' or 'V' or 'W' => 2,
            'j' or 'L' => 3,
            'G' or 'Y' => 4,
            'f' => 6,
            'Q' or 's' => int.MaxValue,
            _ => 0,
        };
        if (window == 0)
        {
            return -1;
        }

        var end = ReadNumber(text, i, window, out var value);
        if (end < 0)
        {
            return -1;
        }

        Store(fields, directive, value);
        return end;
    }

    /// <summary>
    ///     Stores a number read by a directive into the fields.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <param name="directive">The directive letter.</param>
    /// <param name="value">The number.</param>
    private static void Store(ParsedTimeFields fields, char directive, double value)
    {
        switch (directive)
        {
            case 'd' or 'e':
                fields.Day = value;
                break;
            case 'f':
                fields.Millisecond = Math.Floor(value / 1000.0);
                break;
            case 'g' or 'y':
                fields.Year = value + (value > 68 ? 1900 : 2000);
                break;
            case 'G' or 'Y':
                fields.Year = value;
                break;
            case 'H' or 'I':
                fields.Hour = value;
                break;
            case 'j':
                fields.Month = 0;
                fields.Day = value;
                break;
            case 'L':
                fields.Millisecond = value;
                break;
            case 'm':
                fields.Month = (int)value - 1;
                break;
            case 'M':
                fields.Minute = value;
                break;
            case 'S':
                fields.Second = value;
                break;
            case 'q':
                fields.Quarter = (int)(value * 3) - 3;
                break;
            case 'Q':
                fields.UnixMilliseconds = value;
                break;
            case 's':
                fields.UnixSeconds = value;
                break;
            case 'u':
                fields.WeekdayMonday = (int)value;
                break;
            case 'w':
                fields.WeekdaySunday = (int)value;
                break;
            case 'U':
                fields.WeekSunday = (int)value;
                break;
            case 'V':
                fields.WeekIso = (int)value;
                break;
            default:
                fields.WeekMonday = (int)value;
                break;
        }
    }

    /// <summary>
    ///     Reads the number at the start of a window of the text: optional white space, then digits (d3's <c>/^\s*\d+/</c> on a slice).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first position of the window.</param>
    /// <param name="window">The width of the window in characters.</param>
    /// <param name="value">The number; large digit strings give large (possibly infinite) doubles.</param>
    /// <returns>The position after the digits, or -1 when the window holds no number at its start.</returns>
    private static int ReadNumber(string text, int start, int window, out double value)
    {
        value = 0;
        var end = (int)Math.Min((long)text.Length, (long)start + window);
        var i = start;
        while (i < end && IsJsWhiteSpace(text[i]))
        {
            i++;
        }

        var first = i;
        while (i < end && IsAsciiDigit(text[i]))
        {
            value = (value * 10) + (text[i] - '0');
            i++;
        }

        return i == first ? -1 : i;
    }

    /// <summary>
    ///     Matches one of a list of names at a position, ignoring ASCII case, and reports its index.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The position.</param>
    /// <param name="names">The names.</param>
    /// <param name="store">The action that receives the index of the name that matched.</param>
    /// <returns>The position after the name, or -1.</returns>
    private static int ParseName(string text, int i, string[] names, Action<int> store)
    {
        for (var k = 0; k < names.Length; k++)
        {
            if (MatchesName(text, i, names[k]))
            {
                store(k);
                return i + names[k].Length;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Parses a <c>%Z</c> zone as d3 does: the regular expression <c>/^(Z)|([+-]\d\d)(?::?(\d\d))?/</c> on the next six characters.
    /// </summary>
    /// <param name="fields">The fields that receive the zone.</param>
    /// <param name="text">The text.</param>
    /// <param name="i">The position.</param>
    /// <returns>The position after the match, or -1.</returns>
    /// <remarks>
    ///     The second alternative of d3's expression is not anchored, so it can match later in the six characters, and the
    ///     position advances by the length of the match rather than to its end. This is reproduced (it only matters for
    ///     text that has something before the zone, which then almost never consumes all of the text).
    /// </remarks>
    private static int ParseZone(ParsedTimeFields fields, string text, int i)
    {
        var end = (int)Math.Min((long)text.Length, (long)i + 6);
        for (var k = i; k < end; k++)
        {
            if (k == i && text[k] == 'Z')
            {
                fields.Zone = 0;
                return i + 1;
            }

            var length = MatchOffset(text, k, end, out var offset);
            if (length > 0)
            {
                fields.Zone = -offset;
                return i + length;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Matches <c>[+-]\d\d(?::?\d\d)?</c> at a position inside a window.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="k">The position.</param>
    /// <param name="end">The end of the window.</param>
    /// <param name="offset">The signed <c>hhmm</c> number (for example 200 for <c>+02</c> and -530 for <c>-05:30</c>).</param>
    /// <returns>The length of the match, or 0.</returns>
    private static int MatchOffset(string text, int k, int end, out int offset)
    {
        offset = 0;
        if ((text[k] != '+' && text[k] != '-') || k + 2 >= end || !IsAsciiDigit(text[k + 1]) || !IsAsciiDigit(text[k + 2]))
        {
            return 0;
        }

        var hours = ((text[k + 1] - '0') * 10) + (text[k + 2] - '0');
        var minutes = 0;
        var length = 3;
        var m = k + 3;
        if (m < end && text[m] == ':')
        {
            m++;
        }

        if (m + 1 < end && IsAsciiDigit(text[m]) && IsAsciiDigit(text[m + 1]))
        {
            minutes = ((text[m] - '0') * 10) + (text[m + 1] - '0');
            length = m + 2 - k;
        }

        offset = (text[k] == '-' ? -1 : 1) * ((hours * 100) + minutes);
        return length;
    }

    /// <summary>
    ///     Turns the parsed fields into an instant (the second half of d3's <c>newParse</c>).
    /// </summary>
    /// <param name="f">The fields.</param>
    /// <param name="calendar">The calendar of local fields.</param>
    /// <returns>The instant, or NaN when it is outside the range of the calendar or a week number is invalid.</returns>
    private static double ToInstant(ParsedTimeFields f, TimeCalendar calendar)
    {
        // A timestamp is returned as it is
        if (f.UnixMilliseconds is { } timestamp)
        {
            return Checked(timestamp);
        }

        if (f.UnixSeconds is { } seconds)
        {
            return Checked((seconds * 1000) + f.Millisecond);
        }

        // 12-hour clock, then the month from the quarter
        var hour = f.Period is { } period ? (f.Hour % 12) + (period * 12) : f.Hour;
        var year = f.Year;
        double month = f.Month ?? f.Quarter ?? 0;
        var day = f.Day;
        if (f.WeekIso is { } isoWeek)
        {
            if (isoWeek < 1 || isoWeek > 53)
            {
                return double.NaN;
            }

            IsoWeekToDate(f, ref year, out month, out day);
        }
        else if (f.WeekMonday is not null || f.WeekSunday is not null)
        {
            month = 0;
            day = WeekToDay(f, year);
        }

        // With a zone the fields are UTC and the offset is applied to the hours and minutes
        if (f.Zone is { } zone)
        {
            return MakeDate(TimeCalendar.Utc, year, month, day, hour + (zone / 100), f.Minute + (zone % 100), f.Second, f.Millisecond);
        }

        return MakeDate(calendar, year, month, day, hour, f.Minute, f.Second, f.Millisecond);
    }

    /// <summary>
    ///     Checks a timestamp against the range of the calendar.
    /// </summary>
    /// <param name="ms">The timestamp.</param>
    /// <returns>The timestamp, or NaN.</returns>
    private static double Checked(double ms) => TimeCalendar.IsValid(ms) ? Math.Truncate(ms) : double.NaN;

    /// <summary>
    ///     Computes the day of the month for the <c>%U</c> and <c>%W</c> week-of-year directives (January is implied).
    /// </summary>
    /// <param name="f">The fields.</param>
    /// <param name="year">The year.</param>
    /// <returns>The day, which may be zero or negative or past the end of January (it rolls over).</returns>
    private static double WeekToDay(ParsedTimeFields f, double year)
    {
        // d.w is the weekday from Sunday: the %w number, else the %u number modulo 7, else Monday for %W and Sunday for %U
        var weekday = f.WeekdaySunday ?? (f.WeekdayMonday is { } u ? u % 7 : 0);
        if (f.WeekdaySunday is null && f.WeekdayMonday is null && f.WeekMonday is not null)
        {
            weekday = 1;
        }
        var first = TimeCalendar.FirstWeekdayOfYear(year);
        return f.WeekMonday is { } w
            ? ((weekday + 6) % 7) + (w * 7) - ((first + 5) % 7)
            : weekday + ((f.WeekSunday ?? 0) * 7) - ((first + 6) % 7);
    }

    /// <summary>
    ///     Converts the ISO week number (and weekday) of <c>%V</c> to a year, month and day.
    /// </summary>
    /// <param name="f">The fields.</param>
    /// <param name="year">The year read; replaced by the year of the result.</param>
    /// <param name="month">The month of the result, from 0.</param>
    /// <param name="day">The day of the month of the result.</param>
    private static void IsoWeekToDate(ParsedTimeFields f, ref double year, out double month, out double day)
    {
        // Week 1 contains the first Thursday: the Monday on or after January 1 for Friday to Sunday, else the Monday before
        var first = TimeCalendar.FirstWeekdayOfYear(year);
        var january1 = TimeCalendar.DaysFromCivil((long)year, 1, 1);
        var monday = first is > 4 or 0 ? january1 + ((8 - first) % 7) : january1 - ((first + 6) % 7);
        TimeCalendar.CivilFromDays(monday + (((f.WeekIso ?? 1) - 1) * 7L), out var resultYear, out var resultMonth, out var resultDay);
        var weekday = f.WeekdaySunday ?? 1;
        year = resultYear;
        month = resultMonth - 1;
        day = resultDay + ((weekday + 6) % 7);
    }

    /// <summary>
    ///     Builds an instant from fields (d3's <c>localDate</c> and <c>utcDate</c>), including their rule for years 0 to 99.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month from 0; any whole number rolls over.</param>
    /// <param name="day">The day of the month.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <param name="second">The second.</param>
    /// <param name="millisecond">The millisecond.</param>
    /// <returns>The instant, or NaN when out of range.</returns>
    private static double MakeDate(TimeCalendar calendar, double year, double month, double day, double hour, double minute, double second, double millisecond)
    {
        if (!(year >= 0 && year < 100))
        {
            return calendar.FromParts(year, month, day, hour, minute, second, millisecond);
        }

        // d3 builds the date in the year -1 so that JavaScript does not add 1900, then sets the year: the month, the day and
        // the time come from that first date, which matters for a day that rolls over (and a leap day)
        var monthNumber = (long)month;
        var carriedYear = -1 + TimeCalendar.FloorDiv(monthNumber, 12);
        var days = TimeCalendar.DaysFromCivil(carriedYear, (int)TimeCalendar.FloorMod(monthNumber, 12) + 1, (long)day);
        var time = ((long)hour * 3_600_000L) + ((long)minute * 60_000L) + ((long)second * 1000L) + (long)millisecond;
        var wall = (days * 86_400_000L) + time;
        TimeCalendar.CivilFromDays(TimeCalendar.FloorDiv(wall, 86_400_000L), out _, out var rolledMonth, out var rolledDay);
        return calendar.FromParts(year, rolledMonth - 1, rolledDay, 0, 0, 0, TimeCalendar.FloorMod(wall, 86_400_000L));
    }
}
