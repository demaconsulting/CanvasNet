// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The time formatting and parsing of d3-time-format 4.1.0 with its default English (<c>en-US</c>) locale, plus Vega's
///     multi-format (the default axis labels of a time scale).
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Compile"/> turns a specifier such as <c>%Y-%m-%d</c> into a <see cref="TimeFormatter"/>;
///         <see cref="Multi"/> builds the formatter that picks a specifier from the coarsest calendar boundary the instant
///         sits on; <see cref="TryParse"/> is d3's <c>timeParse</c> and <c>utcParse</c> in one method. The class is a set of
///         pure functions of its arguments: the only calendar used is the <see cref="TimeCalendar"/> that is passed in, the
///         names are the English d3 names, the digits are invariant ASCII digits, and the host's culture, zone and clock are
///         never read. There is no static cache (a compiled formatter is an object the caller keeps), so concurrent use
///         needs no synchronization.
///     </para>
///     <para>
///         Deliberate differences from the JavaScript library: an instant outside the range of the calendar (years 1 to
///         9999) or a NaN formats as <c>Invalid Date</c> (d3 would print <c>NaN</c> and <c>undefined</c> pieces) and parses
///         to NaN, the parser works on a bounded text with a hand scanner instead of regular expressions, and the week
///         numbers are computed with the civil arithmetic that gives the same result as d3's interval counts. Everything
///         else (the directives, the padding flags, the unknown-directive behavior, the lack of range validation in the
///         parser) follows the source. See <c>TimeFormat.Format.cs</c>, <c>TimeFormat.Parse.cs</c> and
///         <c>TimeFormat.Multi.cs</c>.
///     </para>
/// </remarks>
internal static partial class TimeFormat
{
    /// <summary>The longest text <see cref="TryParse"/> examines; longer text fails at once.</summary>
    internal const int MaxParseTextLength = 256;

    /// <summary>The text of an instant that is NaN or outside the range of the calendar.</summary>
    internal const string InvalidDateText = "Invalid Date";

    /// <summary>The locale's date-and-time specifier, the meaning of <c>%c</c>.</summary>
    internal const string DateTimeSpecifier = "%x, %X";

    /// <summary>The locale's date specifier, the meaning of <c>%x</c>.</summary>
    internal const string DateSpecifier = "%-m/%-d/%Y";

    /// <summary>The locale's time specifier, the meaning of <c>%X</c>.</summary>
    internal const string TimeSpecifier = "%-I:%M:%S %p";

    /// <summary>The weekday names, Sunday first.</summary>
    internal static readonly string[] Days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    /// <summary>The abbreviated weekday names, Sunday first.</summary>
    internal static readonly string[] ShortDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    /// <summary>The month names, January first.</summary>
    internal static readonly string[] Months =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>The abbreviated month names, January first.</summary>
    internal static readonly string[] ShortMonths = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>The day periods, before noon first.</summary>
    internal static readonly string[] Periods = ["AM", "PM"];

    /// <summary>
    ///     Tells whether a character is a padding flag of a directive (<c>-</c>, <c>_</c> or <c>0</c>).
    /// </summary>
    /// <param name="c">The character after the percent sign.</param>
    /// <param name="fill">The fill character: <c>\0</c> for no padding (<c>-</c>), a space (<c>_</c>) or a zero (<c>0</c>).</param>
    /// <returns>True when <paramref name="c"/> is a flag.</returns>
    internal static bool IsPadFlag(char c, out char fill)
    {
        switch (c)
        {
            case '-':
                fill = '\0';
                return true;
            case '_':
                fill = ' ';
                return true;
            case '0':
                fill = '0';
                return true;
            default:
                fill = '0';
                return false;
        }
    }

    /// <summary>
    ///     Writes a number as d3's <c>pad(value, fill, width)</c> does: a minus sign for a negative value, then the digits
    ///     preceded by as many fill characters as the width needs (none for the no-padding flag).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="fill">The fill character, or <c>\0</c> for none.</param>
    /// <param name="width">The minimum width of the digits (the sign is not counted).</param>
    /// <returns>The invariant ASCII text.</returns>
    internal static string Pad(long value, char fill, int width)
    {
        // The magnitude is taken without negating the value so that the smallest long cannot overflow
        var negative = value < 0;
        var magnitude = negative ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
        var digits = magnitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var padding = fill != '\0' && digits.Length < width ? new string(fill, width - digits.Length) : string.Empty;
        return (negative ? "-" : string.Empty) + padding + digits;
    }

    /// <summary>
    ///     Tells whether a character is JavaScript white space (the set of the regular expression class <c>\s</c>).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True for the white space characters of ECMAScript.</returns>
    internal static bool IsJsWhiteSpace(char c) =>
        c is '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\u00a0' or '\u1680' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000' or '\ufeff'
        || (c >= '\u2000' && c <= '\u200a');

    /// <summary>
    ///     Tells whether a character is an ASCII digit.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True for <c>0</c> to <c>9</c> only (not for other scripts' digits).</returns>
    internal static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    /// <summary>
    ///     Converts an ASCII letter to lower case without consulting any culture.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The lower-case letter for <c>A</c> to <c>Z</c>; the character itself otherwise.</returns>
    internal static char AsciiLower(char c) => c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;

    /// <summary>
    ///     Compares a name with the text at a position, ignoring the case of ASCII letters only.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position in the text.</param>
    /// <param name="name">The name to look for.</param>
    /// <returns>True when the text continues with the name.</returns>
    internal static bool MatchesName(string text, int position, string name)
    {
        if (position + name.Length > text.Length)
        {
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            if (AsciiLower(text[position + i]) != AsciiLower(name[i]))
            {
                return false;
            }
        }

        return true;
    }
}
