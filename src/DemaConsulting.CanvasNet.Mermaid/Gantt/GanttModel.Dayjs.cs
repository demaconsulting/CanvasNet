// Portions derived from mermaid (MIT) and dayjs (MIT); see third-party/README.md
using System.Globalization;
using System.Numerics;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     The part of dayjs 1.11.21 (the version Mermaid bundles) that Mermaid's gantt chart uses, on <see cref="JsDate"/>
///     values whose local time zone is UTC: the core's <c>add</c>, <c>startOf</c>/<c>endOf('day')</c>,
///     <c>diff('year')</c> and <c>format</c> (with the <c>advancedFormat</c> tokens), the <c>isoWeek</c> plugin's
///     <c>isoWeekday</c>, <c>isoWeek</c> and <c>isoWeekYear</c>, and the <c>customParseFormat</c> plugin's strict parse; with
///     JavaScript's <c>Number(text)</c> and the date texts <c>new Date(text)</c> reads in the ECMAScript format.
/// </summary>
/// <remarks>
///     <para>
///         <b>Locale.</b> The English locale of the core: it has weekday and month names and the ordinal function, but no
///         short names, no meridiem function and no long-date formats, so short names are the first three letters, the
///         meridiem is <c>AM</c>/<c>PM</c>, and any <c>L</c> or <c>l</c> token makes a parse fail (the plugin reads the
///         missing formats).
///     </para>
///     <para>
///         <b>Missing plugins.</b> Mermaid loads <c>isoWeek</c>, <c>customParseFormat</c>, <c>advancedFormat</c> and
///         <c>duration</c> only, so formatting a valid date with <c>w</c>, <c>ww</c>, <c>wo</c>, <c>gggg</c>, <c>z</c> or
///         <c>zzz</c> throws (<see cref="NotSupportedException"/>), and a parsed week number makes a parse fail.
///     </para>
///     <para>
///         <b>Strict parse.</b> The plugin splits the format into tokens (characters that are no token are dropped), takes
///         each token's value with its regular expression from the text where the previous literal ended (the first match
///         anywhere after it), removes the first occurrence of that value from the text, builds the date from the values
///         with today for what is missing, and finally makes the date invalid unless formatting it gives the text back.
///     </para>
///     <para>
///         <b>Cost.</b> Formatting is linear in the format: the search for the end of a text in brackets resumes at the
///         closing bracket it found last (in a parse format it stops at the next opening bracket), so no part of the format
///         is searched twice. The strict parse reads the text and the format a few times each, and the rest of the text
///         twice for each token with a value (the search with the token's expression, which passes a run of digits whole,
///         and the removal of the value, found in linear time), so its work is at most the product of the two lengths.
///         With a work meter, <see cref="Format"/> and <see cref="ParseStrict"/> charge that work to it before they do
///         it, so a render bounds the parse of long texts with long formats.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class Dayjs
{
    /// <summary>The default format of <c>format()</c>.</summary>
    private const string DefaultFormat = "YYYY-MM-DDTHH:mm:ssZ";

    /// <summary>The weekday names of the English locale, from Sunday.</summary>
    private static readonly string[] Weekdays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    /// <summary>The month names of the English locale.</summary>
    private static readonly string[] Months =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>The ordinal suffixes of the English locale.</summary>
    private static readonly string[] Ordinals = ["th", "st", "nd", "rd"];

    /// <summary>The tokens of the core format, in the order of its regular expression's alternatives.</summary>
    private static readonly string[] CoreTokens =
        ["YYYY", "YY", "MMMM", "MMM", "MM", "M", "DD", "D", "dddd", "ddd", "dd", "d", "HH", "H", "hh", "h", "a", "A", "mm", "m", "ss", "s", "ZZ", "Z", "SSS"];

    /// <summary>The tokens of the advanced format, in the order of its regular expression's alternatives.</summary>
    private static readonly string[] AdvancedTokens =
        ["Q", "wo", "ww", "w", "WW", "W", "zzz", "z", "gggg", "GGGG", "Do", "X", "x", "kk", "k", "S"];

    /// <summary>The tokens of the parse format, in the order of its regular expression's alternatives.</summary>
    private static readonly string[] ParseTokens =
        ["A", "a", "Q", "YYYY", "YY", "Y", "ww", "w", "MMMM", "MMM", "MM", "M", "Do", "DD", "D", "hh", "h", "HH", "H", "mm", "m", "ss", "s", "SSS", "SS", "S", "z", "ZZ", "Z"];

    /// <summary>
    ///     Adds an amount of a unit to a date (<c>add(value, unit)</c> for the units of Mermaid's durations): months and
    ///     years through the month or year (the day kept, or the last day of a shorter month), days and weeks through the
    ///     day of the month by the rounded number of days, and the rest in milliseconds.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="value">The amount.</param>
    /// <param name="unit">The unit: <c>ms</c>, <c>s</c>, <c>m</c>, <c>h</c>, <c>d</c>, <c>w</c>, <c>M</c> or <c>y</c>.</param>
    /// <returns>The date, invalid when the result is out of range.</returns>
    public static JsDate Add(JsDate date, double value, string unit) => unit switch
    {
        "M" => SetMonthOrYear(date, month: true, date.Month + value),
        "y" => SetMonthOrYear(date, month: false, date.FullYear + value),
        "d" => date.SetDate(date.Date + TimeAxis.JsRound(value)),
        "w" => date.SetDate(date.Date + TimeAxis.JsRound(7 * value)),
        "m" => JsDate.FromTime(date.Time + (value * JsDate.MsPerMinute)),
        "h" => JsDate.FromTime(date.Time + (value * JsDate.MsPerHour)),
        "s" => JsDate.FromTime(date.Time + (value * JsDate.MsPerSecond)),
        _ => JsDate.FromTime(date.Time + value),
    };

    /// <summary>
    ///     Gets the start of the day of a date (<c>startOf('day')</c>).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>Midnight of its day.</returns>
    public static JsDate StartOfDay(JsDate date) => date.SetHours(0, 0, 0, 0);

    /// <summary>
    ///     Gets the end of the day of a date (<c>endOf('day')</c>).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The last millisecond of its day.</returns>
    public static JsDate EndOfDay(JsDate date) => date.SetHours(23, 59, 59, 999);

    /// <summary>
    ///     Gets the ISO day of the week (<c>isoWeekday()</c>): 1 for Monday to 7 for Sunday (7 for an invalid date too).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The day.</returns>
    public static double IsoWeekday(JsDate date) => date.Day is var day && day > 0 ? day : 7;

    /// <summary>
    ///     Gets the whole years from one date to another (<c>later.diff(earlier, 'year')</c>): dayjs's month difference
    ///     divided by 12, truncated.
    /// </summary>
    /// <param name="later">The date the difference is taken of.</param>
    /// <param name="earlier">The date it is taken from.</param>
    /// <returns>The years (NaN for an invalid date).</returns>
    public static double DiffYears(JsDate later, JsDate earlier)
    {
        var years = MonthDiff(later, earlier) / 12;
        return years < 0 ? Math.Ceiling(years) + 0.0 : Math.Floor(years);
    }

    /// <summary>
    ///     Formats a date (<c>format(text)</c>): the <c>advancedFormat</c> tokens first, then the core tokens; text in
    ///     brackets is kept as it is.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="format">The format; an empty format is the default <c>YYYY-MM-DDTHH:mm:ssZ</c>.</param>
    /// <param name="now">The time of <c>dayjs()</c>, which the ISO week number reads.</param>
    /// <param name="meter">The work meter charged for the scans of the format, or null.</param>
    /// <returns>The text, or <c>Invalid Date</c> for an invalid date.</returns>
    /// <exception cref="NotSupportedException">Thrown when the format needs a plugin Mermaid does not load.</exception>
    /// <exception cref="MermaidRenderException">Thrown when the work exceeds the limit of the meter.</exception>
    public static string Format(JsDate date, string format, JsDate now, LayoutWorkMeter? meter = null)
    {
        meter?.Charge(1);
        if (!date.IsValid)
        {
            return "Invalid Date";
        }

        // Each pass is linear in what it scans; the advanced pass gives at most a short number for each token
        var used = format.Length == 0 ? DefaultFormat : format;
        meter?.Charge(used.Length);
        var advanced = AdvancedFormat(date, used, now);
        meter?.Charge(advanced.Length);
        return CoreFormat(date, advanced);
    }

    /// <summary>
    ///     Parses a text strictly with a format (<c>dayjs(text, format, true)</c> with the <c>customParseFormat</c> plugin).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <param name="now">The time of <c>new Date()</c>, which fills what the format does not give.</param>
    /// <param name="usedNow">Set when the date took a part of <paramref name="now"/>.</param>
    /// <param name="meter">The work meter charged for the scans of the text and the format, or null.</param>
    /// <returns>The date, or the invalid date when the text does not match the format.</returns>
    /// <exception cref="NotSupportedException">Thrown when the strict check formats with a plugin Mermaid does not load.</exception>
    /// <exception cref="MermaidRenderException">Thrown when the work exceeds the limit of the meter.</exception>
    public static JsDate ParseStrict(string text, string format, JsDate now, ref bool usedNow, LayoutWorkMeter? meter = null)
    {
        // The two splits of the format, the number of a timestamp and the final comparison each read the texts once
        meter?.Charge(1 + text.Length + (2L * format.Length));
        JsDate date;
        var tookNow = false;
        try
        {
            var unit = format == "X" ? 1000 : 1;
            date = format is "x" or "X"
                ? JsDate.FromTime(unit * ToNumber(text))
                : ParseFormatted(text, format, now, ref tookNow, meter);
        }
        catch (FormatException)
        {
            date = JsDate.Invalid;
        }

        // The strict check: the date must format back to the text
        if (text != Format(date, format, now, meter))
        {
            return JsDate.Invalid;
        }

        usedNow |= tookNow;
        return date;
    }

    /// <summary>
    ///     JavaScript's <c>Number(text)</c>: white space around the number is ignored, an empty text is zero, and the
    ///     decimal, hexadecimal, octal, binary and infinite literals are numbers; anything else is NaN.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    public static double ToNumber(string text)
    {
        var s = TrimJs(text);
        if (s.Length == 0)
        {
            return 0;
        }

        switch (s)
        {
            case "Infinity" or "+Infinity":
                return double.PositiveInfinity;
            case "-Infinity":
                return double.NegativeInfinity;
        }

        if (s.Length > 2 && s[0] == '0' && (s[1] | 0x20) is 'x' or 'o' or 'b')
        {
            var radix = (s[1] | 0x20) switch { 'x' => 16, 'o' => 8, _ => 2 };
            var digits = s.AsSpan(2);
            foreach (var c in digits)
            {
                if (DigitValue(c) >= radix)
                {
                    return double.NaN;
                }
            }

            // Leading zeros add nothing, and a value of 2^1024 or more is infinite as a double, so the big integer
            // never grows past 1,024 bits (each step is then bounded, and the conversion linear in the text)
            digits = digits.TrimStart('0');
            var bitsPerDigit = radix switch { 16 => 4, 8 => 3, _ => 1 };
            if ((long)(digits.Length - 1) * bitsPerDigit >= 1024)
            {
                return double.PositiveInfinity;
            }

            var value = BigInteger.Zero;
            foreach (var c in digits)
            {
                value = (value * radix) + DigitValue(c);
            }

            // The decimal text rounds to the nearest double, as JavaScript does (the explicit conversion of a big
            // integer can round down near the largest double)
            return double.Parse(value.ToString(CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        return IsDecimalLiteral(s) ? double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture) : double.NaN;
    }

    /// <summary>
    ///     Reads a date text as <c>new Date(text)</c> does for the ECMAScript date time string format: a four-digit year
    ///     (or a signed six-digit one), an optional month and day, an optional time (<c>THH:mm</c>, seconds and a fraction),
    ///     and with a time an optional <c>Z</c> or offset. A date alone is UTC and a time without an offset is local time,
    ///     which is UTC here. A day past the end of its month carries into the next month, as in V8.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The date, or the invalid date for any other text (V8's own legacy forms are not read).</returns>
    public static JsDate ParseDateString(string text)
    {
        var p = 0;
        double year;
        if (p < text.Length && text[p] is '+' or '-')
        {
            if (!Digits(text, p + 1, 6, out year) || (text[p] == '-' && year == 0))
            {
                return JsDate.Invalid;
            }

            year = text[p] == '-' ? -year : year;
            p += 7;
        }
        else if (Digits(text, p, 4, out year))
        {
            p += 4;
        }
        else
        {
            return JsDate.Invalid;
        }

        double month = 1, day = 1, hours = 0, minutes = 0, seconds = 0, ms = 0, offset = 0;
        if (p < text.Length && text[p] == '-')
        {
            if (!Digits(text, p + 1, 2, out month) || month is < 1 or > 12)
            {
                return JsDate.Invalid;
            }

            p += 3;
            if (p < text.Length && text[p] == '-')
            {
                if (!Digits(text, p + 1, 2, out day) || day is < 1 or > 31)
                {
                    return JsDate.Invalid;
                }

                p += 3;
            }
        }

        if (p < text.Length && text[p] == 'T')
        {
            if (!Digits(text, p + 1, 2, out hours) || p + 3 >= text.Length || text[p + 3] != ':' || !Digits(text, p + 4, 2, out minutes))
            {
                return JsDate.Invalid;
            }

            p += 6;
            if (p < text.Length && text[p] == ':')
            {
                if (!Digits(text, p + 1, 2, out seconds))
                {
                    return JsDate.Invalid;
                }

                p += 3;
                if (p < text.Length && text[p] == '.')
                {
                    var end = p + 1;
                    while (end < text.Length && char.IsAsciiDigit(text[end]))
                    {
                        end++;
                    }

                    if (end == p + 1)
                    {
                        return JsDate.Invalid;
                    }

                    ms = double.Parse(text.AsSpan(p + 1, Math.Min(3, end - p - 1)), NumberStyles.None, CultureInfo.InvariantCulture)
                        * Math.Pow(10, 3 - Math.Min(3, end - p - 1));
                    p = end;
                }
            }

            if (hours > 24 || minutes > 59 || seconds > 59 || (hours >= 24 && (minutes > 0 || seconds > 0 || ms > 0)))
            {
                return JsDate.Invalid;
            }

            if (p < text.Length && text[p] == 'Z')
            {
                p++;
            }
            else if (p < text.Length && text[p] is '+' or '-')
            {
                if (!Digits(text, p + 1, 2, out var offsetHours) || p + 3 >= text.Length || text[p + 3] != ':' || !Digits(text, p + 4, 2, out var offsetMinutes)
                    || offsetHours > 23 || offsetMinutes > 59)
                {
                    return JsDate.Invalid;
                }

                offset = (text[p] == '-' ? -1 : 1) * ((offsetHours * 60) + offsetMinutes);
                p += 6;
            }
        }

        if (p != text.Length)
        {
            return JsDate.Invalid;
        }

        var time = JsDate.MakeDate(JsDate.MakeDay(year, month - 1, day), JsDate.MakeTime(hours, minutes, seconds, ms));
        return JsDate.FromTime(time - (offset * JsDate.MsPerMinute));
    }

    /// <summary>
    ///     Gets the ISO week number of a date (<c>isoWeek()</c>).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="now">The time of <c>dayjs()</c>, whose year is set to the ISO week year.</param>
    /// <returns>The week number.</returns>
    internal static double IsoWeek(JsDate date, JsDate now)
    {
        var thursday = ThursdayOf(date);
        var yearStart = StartOfYear(SetMonthOrYear(now, month: false, thursday.FullYear));
        var offset = 4 - IsoWeekday(yearStart);
        if (IsoWeekday(yearStart) > 4)
        {
            offset += 7;
        }

        var weeks = (thursday.Time - Add(yearStart, offset, "d").Time) / (7 * JsDate.MsPerDay);
        return (weeks < 0 ? Math.Ceiling(weeks) + 0.0 : Math.Floor(weeks)) + 1;
    }

    /// <summary>
    ///     Gets the Thursday of a date's ISO week (<c>add(4 - isoWeekday(), 'day')</c>), whose year is the ISO week year.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The Thursday.</returns>
    private static JsDate ThursdayOf(JsDate date) => Add(date, 4 - IsoWeekday(date), "d");

    /// <summary>
    ///     Gets the start of the year of a date (<c>startOf('year')</c>: <c>new Date(year, 0, 1)</c>, which maps the years 0
    ///     to 99 to the twentieth century).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The first moment of its year.</returns>
    private static JsDate StartOfYear(JsDate date) => JsDate.FromComponents(date.FullYear, 0, 1);

    /// <summary>
    ///     Sets the month or the year of a date as dayjs's <c>$set</c> does: from the first of the month, then the day back,
    ///     but at most the last day of the new month.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="month">Whether the month is set (else the year).</param>
    /// <param name="value">The new month or year.</param>
    /// <returns>The date.</returns>
    private static JsDate SetMonthOrYear(JsDate date, bool month, double value)
    {
        var first = date.SetDate(1);
        first = month ? first.SetMonth(value) : first.SetFullYear(value);
        return first.SetDate(Math.Min(date.Date, DaysInMonth(first)));
    }

    /// <summary>
    ///     Gets the number of days of the month of a date (<c>daysInMonth()</c>: the day of <c>new Date(year, month + 1,
    ///     0)</c>, which maps the years 0 to 99 to the twentieth century).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The days.</returns>
    private static double DaysInMonth(JsDate date) => JsDate.FromComponents(date.FullYear, date.Month + 1, 0).Date;

    /// <summary>
    ///     dayjs's month difference (<c>monthDiff</c>): the whole months from one date to another and the part of the
    ///     next month, negated.
    /// </summary>
    /// <param name="a">The date the difference is taken of.</param>
    /// <param name="b">The date it is taken from.</param>
    /// <returns>The months.</returns>
    private static double MonthDiff(JsDate a, JsDate b)
    {
        if (a.Date < b.Date)
        {
            var (first, second) = (b, a);
            return -MonthDiff(first, second);
        }

        var whole = (12 * (b.FullYear - a.FullYear)) + (b.Month - a.Month);
        var anchor = Add(a, whole, "M");
        var before = b.Time - anchor.Time < 0;
        var other = Add(a, whole + (before ? -1 : 1), "M");
        var result = -(whole + ((b.Time - anchor.Time) / (before ? anchor.Time - other.Time : other.Time - anchor.Time)));
        return result is 0 || double.IsNaN(result) ? 0 : result;
    }

    /// <summary>
    ///     Replaces the <c>advancedFormat</c> tokens of a format (the plugin's regular expression, its brackets kept).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="format">The format.</param>
    /// <param name="now">The time of <c>dayjs()</c>.</param>
    /// <returns>The format with the tokens replaced.</returns>
    private static string AdvancedFormat(JsDate date, string format, JsDate now)
    {
        var builder = new StringBuilder(format.Length);
        var close = 0;
        var i = 0;
        while (i < format.Length)
        {
            var bracket = BracketEnd(format, i, ref close);
            if (bracket > 0)
            {
                builder.Append(format, i, bracket - i);
                i = bracket;
                continue;
            }

            var token = MatchToken(format, i, AdvancedTokens);
            if (token is null)
            {
                builder.Append(format[i]);
                i++;
                continue;
            }

            builder.Append(token switch
            {
                "Q" => Number(Math.Ceiling((date.Month + 1) / 3)),
                "Do" => Ordinal(date.Date),
                "gggg" => throw new NotSupportedException("t.weekYear is not a function"),
                "GGGG" => Number(ThursdayOf(date).FullYear),
                "wo" or "ww" or "w" => throw new NotSupportedException("t.week is not a function"),
                "W" or "WW" => Pad(Number(IsoWeek(date, now)), token.Length),
                "k" or "kk" => Pad(Number(date.Hours == 0 ? 24 : date.Hours), token.Length),
                "X" => Number(Math.Floor(date.Time / 1000)),
                "x" => Number(date.Time),
                "z" or "zzz" => throw new NotSupportedException("t.offsetName is not a function"),
                _ => token,
            });
            i += token.Length;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Replaces the core tokens of a format (the core's regular expression; the text in brackets is kept without its
    ///     brackets).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="format">The format.</param>
    /// <returns>The text.</returns>
    private static string CoreFormat(JsDate date, string format)
    {
        var builder = new StringBuilder(format.Length);
        var close = 0;
        var i = 0;
        while (i < format.Length)
        {
            var bracket = BracketEnd(format, i, ref close);
            if (bracket > 0)
            {
                builder.Append(format, i + 1, bracket - i - 2);
                i = bracket;
                continue;
            }

            var token = MatchToken(format, i, CoreTokens);
            if (token is null)
            {
                builder.Append(format[i]);
                i++;
                continue;
            }

            var hours = date.Hours;
            var hours12 = hours % 12 == 0 ? 12 : hours % 12;
            builder.Append(token switch
            {
                "YY" => Number(date.FullYear)[^Math.Min(2, Number(date.FullYear).Length)..],
                "YYYY" => Pad(Number(date.FullYear), 4),
                "M" => Number(date.Month + 1),
                "MM" => Pad(Number(date.Month + 1), 2),
                "MMM" => Months[(int)date.Month][..3],
                "MMMM" => Months[(int)date.Month],
                "D" => Number(date.Date),
                "DD" => Pad(Number(date.Date), 2),
                "d" => Number(date.Day),
                "dd" => Weekdays[(int)date.Day][..2],
                "ddd" => Weekdays[(int)date.Day][..3],
                "dddd" => Weekdays[(int)date.Day],
                "H" => Number(hours),
                "HH" => Pad(Number(hours), 2),
                "h" => Pad(Number(hours12), 1),
                "hh" => Pad(Number(hours12), 2),
                "a" => hours < 12 ? "am" : "pm",
                "A" => hours < 12 ? "AM" : "PM",
                "m" => Number(date.Minutes),
                "mm" => Pad(Number(date.Minutes), 2),
                "s" => Number(date.Seconds),
                "ss" => Pad(Number(date.Seconds), 2),
                "SSS" => Pad(Number(date.Milliseconds), 3),
                "Z" => "+00:00",
                _ => "+0000",
            });
            i += token.Length;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses a text with a format (the plugin's <c>makeParser</c> and the date it builds), without the strict check.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <param name="now">The time of <c>new Date()</c>.</param>
    /// <param name="usedNow">Set when the date took a part of <paramref name="now"/>.</param>
    /// <param name="meter">The work meter, or null.</param>
    /// <returns>The date (invalid when it is out of range).</returns>
    /// <exception cref="FormatException">Thrown where the plugin fails (a JavaScript error it catches).</exception>
    private static JsDate ParseFormatted(string text, string format, JsDate now, ref bool usedNow, LayoutWorkMeter? meter)
    {
        var parsed = new ParsedDate();
        var input = text;
        var position = 0;
        foreach (var token in ParseTokensOf(format))
        {
            if (!IsParserToken(token))
            {
                // A literal (a separator, a text in brackets or a token without a parser) is skipped by its length
                var literal = token.StartsWith('[') ? token[1..] : token;
                literal = literal.EndsWith(']') ? literal[..^1] : literal;
                position += literal.Length;
                continue;
            }

            // The search of the rest of the text and the removal of the value each read the text once
            meter?.Charge(1 + (2 * (long)input.Length));
            var rest = position < input.Length ? input[position..] : string.Empty;
            var value = Exec(token, rest) ?? throw new FormatException(token);
            Apply(parsed, token, value);
            var at = FirstIndexOf(input, value);
            input = at < 0 ? input : input.Remove(at, value.Length);
        }

        // The meridiem moves the hours
        if (parsed.Afternoon is { } afternoon)
        {
            if (afternoon && parsed.Hours < 12)
            {
                parsed.Hours += 12;
            }
            else if (!afternoon && parsed.Hours is 12)
            {
                parsed.Hours = 0;
            }
        }

        // The date: what the format does not give is taken from today
        double day;
        if (Truthy(parsed.Day) || parsed.DayText is not null)
        {
            day = parsed.Day ?? 0;
        }
        else
        {
            day = Truthy(parsed.Year) || Truthy(parsed.Month) ? 1 : TakeNow(now.Date, ref usedNow);
        }
        var year = Truthy(parsed.Year) ? parsed.Year!.Value : TakeNow(now.FullYear, ref usedNow);
        var month = 0.0;
        if (!(Truthy(parsed.Year) && !Truthy(parsed.Month)))
        {
            month = parsed.Month > 0 ? parsed.Month.Value - 1 : TakeNow(now.Month, ref usedNow);
        }

        var hours = Or0(parsed.Hours);
        var minutes = Or0(parsed.Minutes);
        var seconds = Or0(parsed.Seconds);
        var ms = Or0(parsed.Milliseconds);
        if (parsed.Zone is { } offset)
        {
            return JsDate.FromComponents(year, month, day, hours, minutes, seconds, ms + (60 * offset * 1000));
        }

        // A week number needs the weekOfYear plugin
        return Truthy(parsed.Week) ? throw new FormatException("week") : JsDate.FromComponents(year, month, day, hours, minutes, seconds, ms);
    }

    /// <summary>
    ///     Splits a parse format into its tokens (the plugin's regular expression), after failing on the long-date tokens
    ///     the English locale lacks.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <returns>The tokens: texts in brackets, runs of separators and format tokens.</returns>
    /// <exception cref="FormatException">Thrown on an <c>L</c> or <c>l</c> token, or when the format has no token.</exception>
    private static List<string> ParseTokensOf(string format)
    {
        // An L or l token outside brackets reads the locale's missing formats
        var search = 0;
        var i = 0;
        while (i < format.Length)
        {
            var bracket = BracketEnd(format, i, ref search);
            if (bracket > 0)
            {
                i = bracket;
                continue;
            }

            if (format[i] is 'L' or 'l')
            {
                throw new FormatException(format);
            }

            i++;
        }

        var tokens = new List<string>();
        var p = 0;
        while (p < format.Length)
        {
            // A text in brackets: up to the last ']' before the next '[' (both scans stop at the next '[', so no part of
            // the format is scanned twice)
            if (format[p] == '[')
            {
                var next = format.IndexOf('[', p + 1);
                var limit = next < 0 ? format.Length : next;
                var close = format.LastIndexOf(']', limit - 1, limit - 1 - p);
                if (close > p)
                {
                    tokens.Add(format[p..(close + 1)]);
                    p = close + 1;
                    continue;
                }
            }

            if (IsSeparator(format[p]))
            {
                var end = p;
                while (end < format.Length && IsSeparator(format[end]))
                {
                    end++;
                }

                tokens.Add(format[p..end]);
                p = end;
                continue;
            }

            var token = MatchToken(format, p, ParseTokens);
            if (token is not null)
            {
                tokens.Add(token);
                p += token.Length;
            }
            else
            {
                p++;
            }
        }

        return tokens.Count > 0 ? tokens : throw new FormatException(format);
    }

    /// <summary>
    ///     Gets whether a parse token has a parser (any other token is skipped as a literal).
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>Whether it has.</returns>
    private static bool IsParserToken(string token) => token is
        "A" or "a" or "Q" or "S" or "SS" or "SSS" or "s" or "ss" or "m" or "mm" or "H" or "h" or "HH" or "hh" or "D" or "DD"
        or "Do" or "w" or "ww" or "M" or "MM" or "MMM" or "MMMM" or "Y" or "YY" or "YYYY" or "Z" or "ZZ";

    /// <summary>
    ///     Finds the value of a parse token in a text with the token's regular expression (its first match anywhere).
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="text">The text from the current position.</param>
    /// <returns>The value, or null when the expression does not match.</returns>
    private static string? Exec(string token, string text) => token switch
    {
        "Q" or "S" => Search(text, (s, p) => p < s.Length && char.IsAsciiDigit(s[p]) ? 1 : -1),
        "SS" or "DD" or "ww" or "MM" or "YY" => Search(text, (s, p) => DigitRun(s, p, 2, 2)),
        "SSS" => Search(text, (s, p) => DigitRun(s, p, 3, 3)),
        "YYYY" => Search(text, (s, p) => DigitRun(s, p, 4, 4)),
        "Y" => Search(text, (s, p) =>
        {
            var start = p < s.Length && s[p] is '+' or '-' ? p + 1 : p;
            var run = DigitRun(s, start, 1, int.MaxValue);
            return run < 0 ? -1 : start - p + run;
        }),
        "A" or "a" or "Do" or "MMM" or "MMMM" => SearchWord(text),
        "Z" or "ZZ" => Search(text, (s, p) =>
        {
            if (p < s.Length && s[p] == 'Z')
            {
                return 1;
            }

            if (p < s.Length && s[p] is '+' or '-' && DigitRun(s, p + 1, 2, 2) == 2)
            {
                var end = p + 3;
                end += end < s.Length && s[end] == ':' ? 1 : 0;
                end += DigitRun(s, end, 2, 2) == 2 ? 2 : 0;
                return end - p;
            }

            return -1;
        }),
        _ => Search(text, (s, p) => DigitRun(s, p, 1, 2)),
    };

    /// <summary>
    ///     Applies a parse token's value (the plugin's parser of the token).
    /// </summary>
    /// <param name="parsed">The parsed values.</param>
    /// <param name="token">The token.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="FormatException">Thrown when a name or an ordinal is not one of the locale's.</exception>
    private static void Apply(ParsedDate parsed, string token, string value)
    {
        var number = ToNumber(value);
        switch (token)
        {
            case "A":
                parsed.Afternoon = value == "PM";
                break;
            case "a":
                parsed.Afternoon = value == "pm";
                break;
            case "Q":
                parsed.Month = (3 * (number - 1)) + 1;
                break;
            case "S":
                parsed.Milliseconds = 100 * number;
                break;
            case "SS":
                parsed.Milliseconds = 10 * number;
                break;
            case "SSS":
                parsed.Milliseconds = number;
                break;
            case "s" or "ss":
                parsed.Seconds = number;
                break;
            case "m" or "mm":
                parsed.Minutes = number;
                break;
            case "H" or "h" or "HH" or "hh":
                parsed.Hours = number;
                break;
            case "D" or "DD":
                parsed.Day = number;
                parsed.DayText = null;
                break;
            case "Do":
                {
                    // The digits of the ordinal (a text, so even "0" is set), or the day whose ordinal it is
                    var start = 0;
                    while (start < value.Length && !char.IsAsciiDigit(value[start]))
                    {
                        start++;
                    }

                    var end = start;
                    while (end < value.Length && char.IsAsciiDigit(value[end]))
                    {
                        end++;
                    }

                    if (end == start)
                    {
                        throw new FormatException(value);
                    }

                    parsed.DayText = value[start..end];
                    parsed.Day = ToNumber(parsed.DayText);
                    for (var r = 1; r <= 31; r++)
                    {
                        if (Ordinal(r)[1..^1] == value)
                        {
                            parsed.Day = r;
                        }
                    }

                    break;
                }

            case "w" or "ww":
                parsed.Week = number;
                break;
            case "M" or "MM":
                parsed.Month = number;
                break;
            case "MMM" or "MMMM":
                {
                    var index = Array.FindIndex(Months, m => (token == "MMM" ? m[..3] : m) == value) + 1;
                    parsed.Month = index >= 1 ? index : throw new FormatException(value);
                    break;
                }

            case "Y" or "YYYY":
                parsed.Year = number;
                break;
            case "YY":
                parsed.Year = number + (number > 68 ? 1900 : 2000);
                break;
            default:
                parsed.Zone = ZoneOffset(value);
                break;
        }
    }

    /// <summary>
    ///     Reads a zone offset (<c>Z</c>, <c>+HH:mm</c> or <c>+HHmm</c>) in minutes, as the plugin's <c>offsetFromString</c>
    ///     does: the minutes to add to the local time (negative east of UTC).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The offset.</returns>
    private static double ZoneOffset(string value)
    {
        if (value == "Z")
        {
            return 0;
        }

        // The sign and the two-digit groups (/([+-]|\d\d)/g)
        var parts = new List<string>();
        var i = 0;
        while (i < value.Length)
        {
            if (value[i] is '+' or '-')
            {
                parts.Add(value[i..(i + 1)]);
                i++;
            }
            else if (DigitRun(value, i, 2, 2) == 2)
            {
                parts.Add(value[i..(i + 2)]);
                i += 2;
            }
            else
            {
                i++;
            }
        }

        var minutes = (60 * ToNumber(parts[1])) + (parts.Count > 2 && ToNumber(parts[2]) is var m && Truthy(m) ? m : 0);
        if (minutes is 0)
        {
            return 0;
        }

        return parts[0] == "+" ? -minutes : minutes;
    }

    /// <summary>
    ///     Finds the leftmost match of an anchored matcher in a text; the matcher must look at a bounded number of
    ///     characters when it fails, so the search is linear.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="match">The matcher: the length of a match at a position, or -1.</param>
    /// <returns>The match, or null.</returns>
    private static string? Search(string text, Func<string, int, int> match)
    {
        for (var p = 0; p < text.Length; p++)
        {
            var length = match(text, p);
            if (length > 0)
            {
                return text.Substring(p, length);
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds the leftmost match of the plugin's word expression <c>\d*[^-_:/,()\s\d]+</c> in a text, in one pass.
    /// </summary>
    /// <remarks>
    ///     A start in a run of digits fails only when the character after the run is no word character (the digits
    ///     cannot be given back to the word), and then every start in the run fails at that same character, as does a
    ///     start at it; so the search goes on after it instead of trying each start, with the same result.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <returns>The match, or null.</returns>
    private static string? SearchWord(string text)
    {
        var p = 0;
        while (p < text.Length)
        {
            var q = p;
            while (q < text.Length && char.IsAsciiDigit(text[q]))
            {
                q++;
            }

            var end = q;
            while (end < text.Length && !char.IsAsciiDigit(text[end]) && !IsWordSeparator(text[end]))
            {
                end++;
            }

            if (end > q)
            {
                return text[p..end];
            }

            p = q + 1;
        }

        return null;
    }

    /// <summary>
    ///     Finds the first occurrence of a value in a text (JavaScript's <c>indexOf</c>) in time linear in both
    ///     (Knuth-Morris-Pratt), however the value repeats its own beginning.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value; not empty.</param>
    /// <returns>The index, or -1.</returns>
    private static int FirstIndexOf(string text, string value)
    {
        // The length of the longest proper prefix of each prefix of the value that is also its suffix
        var border = new int[value.Length];
        for (int i = 1, k = 0; i < value.Length; i++)
        {
            while (k > 0 && value[i] != value[k])
            {
                k = border[k - 1];
            }

            k += value[i] == value[k] ? 1 : 0;
            border[i] = k;
        }

        for (int i = 0, k = 0; i < text.Length; i++)
        {
            while (k > 0 && text[i] != value[k])
            {
                k = border[k - 1];
            }

            k += text[i] == value[k] ? 1 : 0;
            if (k == value.Length)
            {
                return i - k + 1;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Matches a run of ASCII digits at a position, greedy up to a most.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="p">The position.</param>
    /// <param name="min">The fewest digits.</param>
    /// <param name="max">The most digits.</param>
    /// <returns>The length, or -1.</returns>
    private static int DigitRun(string s, int p, int min, int max)
    {
        var end = p;
        while (end < s.Length && end - p < max && char.IsAsciiDigit(s[end]))
        {
            end++;
        }

        return end - p >= min ? end - p : -1;
    }

    /// <summary>
    ///     Gets the end of a text in brackets at a position (<c>\[([^\]]+)]</c>), for positions that only grow: the first
    ///     <c>]</c> after the previous search is kept, so the format is searched once whatever the number of brackets.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="p">The position; not less than at the previous call with the same state.</param>
    /// <param name="close">
    ///     The state of the search: zero at first, then the first <c>]</c> found, or -1 when none is left.
    /// </param>
    /// <returns>The position after the closing bracket, or -1.</returns>
    private static int BracketEnd(string s, int p, ref int close)
    {
        if (s[p] != '[')
        {
            return -1;
        }

        // A ']' found after an earlier position is the first one after this one too when it lies past it
        if (close >= 0 && close <= p)
        {
            close = s.IndexOf(']', p + 1);
        }

        return close > p + 1 ? close + 1 : -1;
    }

    /// <summary>
    ///     Matches the first token of a list (the alternatives of a regular expression, in order) at a position.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="p">The position.</param>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The token, or null.</returns>
    private static string? MatchToken(string s, int p, string[] tokens)
    {
        // Every token starts with an ASCII letter, and the first character rules out most tokens without a comparison
        var c = s[p];
        if (!char.IsAsciiLetter(c))
        {
            return null;
        }

        foreach (var token in tokens)
        {
            if (token[0] == c && p + token.Length <= s.Length && string.CompareOrdinal(s, p, token, 0, token.Length) == 0)
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    ///     Gets whether a character is a separator of a parse format (<c>[-_:/.,()\s]</c>).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsSeparator(char c) => c is '-' or '_' or ':' or '/' or '.' or ',' or '(' or ')' || ScanPatterns.IsJsWhitespace(c);

    /// <summary>
    ///     Gets whether a character ends a word of the parse expression <c>\d*[^-_:/,()\s\d]+</c> (the dot does not).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsWordSeparator(char c) => c is '-' or '_' or ':' or '/' or ',' or '(' or ')' || ScanPatterns.IsJsWhitespace(c);

    /// <summary>
    ///     Gets the value of a digit or letter in a radix up to 36 (99 for any other character).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The value.</returns>
    private static int DigitValue(char c)
    {
        if (char.IsAsciiDigit(c))
        {
            return c - '0';
        }

        return char.IsAsciiLetter(c) ? (c | 0x20) - 'a' + 10 : 99;
    }

    /// <summary>
    ///     Gets the English ordinal of a number in brackets (the locale's <c>ordinal</c>), such as <c>[21st]</c>.
    /// </summary>
    /// <param name="n">The number.</param>
    /// <returns>The ordinal.</returns>
    private static string Ordinal(double n)
    {
        var rest = n % 100;
        var tens = (rest - 20) % 10;
        var suffix = Ordinals[0];
        if (tens is >= 0 and <= 3)
        {
            suffix = Ordinals[(int)tens];
        }
        else if (rest is >= 0 and <= 3)
        {
            suffix = Ordinals[(int)rest];
        }
        return "[" + Number(n) + suffix + "]";
    }

    /// <summary>
    ///     Pads a text on the left with zeros to a length (dayjs's <c>padStart</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The length.</param>
    /// <returns>The text.</returns>
    private static string Pad(string text, int length) =>
        text.Length == 0 || text.Length >= length ? text : new string('0', length - text.Length) + text;

    /// <summary>
    ///     Writes a number as JavaScript's <c>String(number)</c> does.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Number(double value) => YamlSubsetParser.FormatJsNumber(value);

    /// <summary>
    ///     Reads a run of exactly a number of ASCII digits.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="p">The position.</param>
    /// <param name="count">The number of digits.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the digits are there.</returns>
    private static bool Digits(string s, int p, int count, out double value)
    {
        value = 0;
        if (p < 0 || p + count > s.Length)
        {
            return false;
        }

        for (var i = p; i < p + count; i++)
        {
            if (!char.IsAsciiDigit(s[i]))
            {
                return false;
            }

            value = (value * 10) + (s[i] - '0');
        }

        return true;
    }

    /// <summary>
    ///     Gets whether a text is a JavaScript decimal literal (<c>StrDecimalLiteral</c> without <c>Infinity</c>).
    /// </summary>
    /// <param name="s">The trimmed text.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsDecimalLiteral(string s)
    {
        var i = s[0] is '+' or '-' ? 1 : 0;
        var start = i;
        while (i < s.Length && char.IsAsciiDigit(s[i]))
        {
            i++;
        }

        var integer = i - start;
        var fraction = 0;
        if (i < s.Length && s[i] == '.')
        {
            i++;
            var fractionStart = i;
            while (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                i++;
            }

            fraction = i - fractionStart;
        }

        if (integer + fraction == 0)
        {
            return false;
        }

        if (i < s.Length && (s[i] | 0x20) == 'e')
        {
            i++;
            i += i < s.Length && s[i] is '+' or '-' ? 1 : 0;
            var exponentStart = i;
            while (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                i++;
            }

            if (i == exponentStart)
            {
                return false;
            }
        }

        return i == s.Length;
    }

    /// <summary>
    ///     Trims JavaScript white space and line terminators.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    private static string TrimJs(string text) => Preprocessor.TrimJs(text);

    /// <summary>
    ///     Gets whether a number is truthy in JavaScript (defined, not zero and not NaN).
    /// </summary>
    /// <param name="value">The number, or null for undefined.</param>
    /// <returns>Whether it is.</returns>
    private static bool Truthy(double? value) => value is { } v && v != 0 && !double.IsNaN(v);

    /// <summary>
    ///     Applies JavaScript's <c>value || 0</c>.
    /// </summary>
    /// <param name="value">The number, or null for undefined.</param>
    /// <returns>The value, or zero.</returns>
    private static double Or0(double? value) => Truthy(value) ? value!.Value : 0;

    /// <summary>
    ///     Takes a part of today's date and notes that it was taken.
    /// </summary>
    /// <param name="value">The part.</param>
    /// <param name="usedNow">Set.</param>
    /// <returns>The part.</returns>
    private static double TakeNow(double value, ref bool usedNow)
    {
        usedNow = true;
        return value;
    }

    /// <summary>
    ///     The values a strict parse reads (the plugin's <c>time</c> object); undefined values are null.
    /// </summary>
    private sealed class ParsedDate
    {
        /// <summary>Gets or sets the year.</summary>
        public double? Year { get; set; }

        /// <summary>Gets or sets the month, from 1.</summary>
        public double? Month { get; set; }

        /// <summary>Gets or sets the day of the month.</summary>
        public double? Day { get; set; }

        /// <summary>Gets or sets the digits of an ordinal day, a text that is set even when it is zero.</summary>
        public string? DayText { get; set; }

        /// <summary>Gets or sets the hours.</summary>
        public double? Hours { get; set; }

        /// <summary>Gets or sets the minutes.</summary>
        public double? Minutes { get; set; }

        /// <summary>Gets or sets the seconds.</summary>
        public double? Seconds { get; set; }

        /// <summary>Gets or sets the milliseconds.</summary>
        public double? Milliseconds { get; set; }

        /// <summary>Gets or sets the week number.</summary>
        public double? Week { get; set; }

        /// <summary>Gets or sets the zone offset in minutes, or null when no zone was read.</summary>
        public double? Zone { get; set; }

        /// <summary>Gets or sets the meridiem: true for the afternoon, or null when none was read.</summary>
        public bool? Afternoon { get; set; }
    }
}
