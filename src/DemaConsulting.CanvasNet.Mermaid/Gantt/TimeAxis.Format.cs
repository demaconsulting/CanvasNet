// Portions derived from d3 (ISC); see third-party/README.md
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     d3-time-format's <c>timeFormat</c> with d3's default en-US locale, for the gantt axis labels.
/// </summary>
internal static partial class TimeAxis
{
    /// <summary>The en-US day names (<c>days</c>).</summary>
    private static readonly string[] DayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    /// <summary>The en-US short day names (<c>shortDays</c>).</summary>
    private static readonly string[] ShortDayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    /// <summary>The en-US month names (<c>months</c>).</summary>
    private static readonly string[] MonthNames =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>The en-US short month names (<c>shortMonths</c>).</summary>
    private static readonly string[] ShortMonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>
    ///     Formats a date as d3-time-format's <c>timeFormat(specifier)(date)</c> does with the en-US locale.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each <c>%</c> directive (<c>%a %A %b %B %c %d %e %f %g %G %H %I %j %L %m %M %p %q %Q %s %S %u %U %V %w %W
    ///         %x %X %y %Y %Z %%</c>) may carry a pad modifier: <c>-</c> (none), <c>_</c> (space) or <c>0</c> (zero); the
    ///         default pad is zero, a space for <c>%e</c>. An unknown directive gives its own character, and a <c>%</c>
    ///         at the end gives nothing. <c>%c</c>, <c>%x</c> and <c>%X</c> are the locale's <c>%x, %X</c>,
    ///         <c>%-m/%-d/%Y</c> and <c>%-I:%M:%S %p</c>; <c>%Z</c> is <c>+0000</c>, as the local zone is UTC.
    ///     </para>
    ///     <para>
    ///         An invalid date formats as JavaScript formats it: numbers become <c>NaN</c> (padded), names become empty,
    ///         <c>%I</c> gives 12, <c>%p</c> gives AM and <c>%q</c> gives 1.
    ///     </para>
    /// </remarks>
    /// <param name="specifier">The format specifier.</param>
    /// <param name="date">The date.</param>
    /// <returns>The text.</returns>
    public static string Format(string specifier, JsDate date)
    {
        ArgumentNullException.ThrowIfNull(specifier);
        var builder = new StringBuilder(specifier.Length * 2);
        AppendFormat(builder, specifier, date);
        return builder.ToString();
    }

    /// <summary>
    ///     Appends the text of a specifier (<c>newFormat</c>): the literal text between the directives and the text of
    ///     each directive.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="specifier">The specifier.</param>
    /// <param name="date">The date.</param>
    private static void AppendFormat(StringBuilder builder, string specifier, JsDate date)
    {
        var n = specifier.Length;
        var j = 0;
        var i = -1;
        while (++i < n)
        {
            if (specifier[i] != '%')
            {
                continue;
            }

            // The literal text before the directive
            builder.Append(specifier, j, i - j);

            // An optional pad modifier, then the directive character (empty past the end)
            var c = CharAt(specifier, ++i);
            string pad;
            if (PadOf(c) is { } modifier)
            {
                pad = modifier;
                c = CharAt(specifier, ++i);
            }
            else
            {
                pad = c == "e" ? " " : "0";
            }

            builder.Append(FormatDirective(c, date, pad));
            j = i + 1;
        }

        if (j < n)
        {
            builder.Append(specifier, j, n - j);
        }
    }

    /// <summary>
    ///     Gets the text of one directive (<c>formats[c](date, pad)</c>), or the character itself when it is not a
    ///     directive.
    /// </summary>
    /// <param name="c">The directive character, or an empty string past the end.</param>
    /// <param name="d">The date.</param>
    /// <param name="p">The pad.</param>
    /// <returns>The text.</returns>
    private static string FormatDirective(string c, JsDate d, string p) => c switch
    {
        "a" => Name(ShortDayNames, d.Day),
        "A" => Name(DayNames, d.Day),
        "b" => Name(ShortMonthNames, d.Month),
        "B" => Name(MonthNames, d.Month),
        "c" => Format("%x, %X", d),
        "d" or "e" => Pad(d.Date, p, 2),
        "f" => Pad(d.Milliseconds, p, 3) + "000",
        "g" => Pad(IsoThursday(d).FullYear % 100, p, 2),
        "G" => Pad(IsoThursday(d).FullYear % 10000, p, 4),
        "H" => Pad(d.Hours, p, 2),
        "I" => Pad(Hour12(d.Hours), p, 2),
        "j" => Pad(1 + Day.Count(Year.Floor(d).Time, d.Time), p, 3),
        "L" => Pad(d.Milliseconds, p, 3),
        "m" => Pad(d.Month + 1, p, 2),
        "M" => Pad(d.Minutes, p, 2),
        "p" => d.Hours >= 12 ? "PM" : "AM",
        "q" => NumberText(1 + (double.IsNaN(d.Month) ? 0 : Math.Truncate(d.Month / 3))),
        "Q" => NumberText(d.Time),
        "s" => NumberText(Math.Floor(d.Time / 1000)),
        "S" => Pad(d.Seconds, p, 2),
        "u" => NumberText(d.Day == 0 ? 7 : d.Day),
        "U" => Pad(Sunday.Count(Year.Floor(d).Time - 1, d.Time), p, 2),
        "V" => Pad(IsoWeek(d), p, 2),
        "w" => NumberText(d.Day),
        "W" => Pad(Monday.Count(Year.Floor(d).Time - 1, d.Time), p, 2),
        "x" => Format("%-m/%-d/%Y", d),
        "X" => Format("%-I:%M:%S %p", d),
        "y" => Pad(d.FullYear % 100, p, 2),
        "Y" => Pad(d.FullYear % 10000, p, 4),
        "Z" => FormatZone(d),
        "%" => "%",
        _ => c,
    };

    /// <summary>
    ///     Gets the pad of a modifier character (<c>pads</c>), or null when the character is not a modifier.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The pad, or null.</returns>
    private static string? PadOf(string c) => c switch
    {
        "-" => string.Empty,
        "_" => " ",
        "0" => "0",
        _ => null,
    };

    /// <summary>
    ///     Gets the character at an index as a string (<c>charAt</c>): empty past the end.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <returns>The character, or an empty string.</returns>
    private static string CharAt(string text, int index) => index < text.Length ? text[index].ToString() : string.Empty;

    /// <summary>
    ///     Gets a locale name by index; an invalid index (NaN) gives the empty text JavaScript's <c>join</c> gives for
    ///     <c>undefined</c>.
    /// </summary>
    /// <param name="names">The names.</param>
    /// <param name="index">The index, or NaN.</param>
    /// <returns>The name.</returns>
    private static string Name(string[] names, double index) => double.IsNaN(index) ? string.Empty : names[(int)index];

    /// <summary>
    ///     Gets the 12-hour clock hour (<c>getHours() % 12 || 12</c>): NaN, like zero, gives 12.
    /// </summary>
    /// <param name="hours">The hours, or NaN.</param>
    /// <returns>The hour from 1 to 12.</returns>
    private static double Hour12(double hours)
    {
        var hour = hours % 12;
        return hour is 0 || double.IsNaN(hour) ? 12 : hour;
    }

    /// <summary>
    ///     Gets the Thursday of a date's ISO week (<c>dISO</c>): the date's own Thursday or the next one.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <returns>The Thursday.</returns>
    private static JsDate IsoThursday(JsDate d)
    {
        var day = d.Day;
        return day >= 4 || day == 0 ? Thursday.Floor(d) : Thursday.Ceil(d);
    }

    /// <summary>
    ///     Gets the ISO week number of a date (<c>formatWeekNumberISO</c>).
    /// </summary>
    /// <param name="d">The date.</param>
    /// <returns>The week number, or NaN.</returns>
    private static double IsoWeek(JsDate d)
    {
        d = IsoThursday(d);
        var year = Year.Floor(d);
        return Thursday.Count(year.Time, d.Time) + (year.Day is 4 ? 1 : 0);
    }

    /// <summary>
    ///     Gets the zone text (<c>formatZone</c>) of the zero offset: <c>+0000</c>, or <c>+00NaN</c> for an invalid
    ///     date, as JavaScript computes it.
    /// </summary>
    /// <param name="d">The date.</param>
    /// <returns>The zone text.</returns>
    private static string FormatZone(JsDate d)
    {
        var z = d.TimezoneOffset;
        var sign = z > 0 ? "-" : "+";
        if (!(z > 0))
        {
            z = -z;
        }

        var hours = double.IsNaN(z) ? 0 : Math.Truncate(z / 60);
        return sign + Pad(hours, "0", 2) + Pad(z % 60, "0", 2);
    }

    /// <summary>
    ///     Pads a number (<c>pad(value, fill, width)</c>): the sign, then the fill repeated up to the width, then the
    ///     digits of the magnitude.
    /// </summary>
    /// <param name="value">The number, an integer or NaN.</param>
    /// <param name="fill">The fill, possibly empty.</param>
    /// <param name="width">The width.</param>
    /// <returns>The padded text.</returns>
    private static string Pad(double value, string fill, int width)
    {
        var sign = value < 0 ? "-" : string.Empty;
        var digits = NumberText(value < 0 ? -value : value);
        return sign + (digits.Length < width ? string.Concat(Enumerable.Repeat(fill, width - digits.Length)) + digits : digits);
    }

    /// <summary>
    ///     Gets JavaScript's text of an integer number (<c>String(n)</c>), or <c>NaN</c>.
    /// </summary>
    /// <param name="value">The integer value or NaN.</param>
    /// <returns>The text.</returns>
    private static string NumberText(double value) =>
        double.IsNaN(value) ? "NaN" : ((long)value).ToString(CultureInfo.InvariantCulture);
}
