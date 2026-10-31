// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
using System.Text;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The exception that <see cref="TimeFormatter.Format"/> throws when the formatted text would be longer than the
///     caller allows.
/// </summary>
/// <remarks>
///     Callers map it to a located render exception that contains the limit they enforce (<c>the limit of</c>) or to an
///     expression runtime error; it never reaches a public caller.
/// </remarks>
internal sealed class TimeFormatTooLongException : InvalidOperationException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeFormatTooLongException"/> class.
    /// </summary>
    /// <param name="maxLength">The largest length that was allowed.</param>
    public TimeFormatTooLongException(int maxLength)
        : base($"The formatted time is longer than {maxLength} characters.")
    {
        MaxLength = maxLength;
    }

    /// <summary>Gets the largest length that was allowed.</summary>
    public int MaxLength { get; }
}

/// <summary>
///     The exception that <see cref="TimeFormat.Compile"/> throws when a specifier is longer than
///     <see cref="TimeFormat.MaxSpecifierLength"/> or expands to more than <see cref="TimeFormat.MaxTokenCount"/> pieces.
/// </summary>
/// <remarks>
///     Callers map it to a located specification or render exception (guides) or to an expression runtime error (the
///     expression functions); it never reaches a public caller.
/// </remarks>
internal sealed class TimeFormatSpecifierException : ArgumentException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeFormatSpecifierException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public TimeFormatSpecifierException(string message)
        : base(message)
    {
    }
}

/// <summary>
///     One piece of a compiled specifier: either literal text or one directive with its fill character.
/// </summary>
/// <param name="Directive">The directive letter, or <c>\0</c> for a literal.</param>
/// <param name="Fill">The fill character of a numeric directive (<c>\0</c> for none).</param>
/// <param name="Literal">The literal text; null for a directive.</param>
internal readonly record struct FormatToken(char Directive, char Fill, string? Literal);

/// <summary>
///     A compiled time format: d3's <c>timeFormat(specifier)</c>, or Vega's multi-format made by
///     <see cref="TimeFormat.Multi"/>.
/// </summary>
/// <remarks>
///     The formatter is immutable and safe for concurrent use. The output is bounded: the formatter stops as soon as the
///     text would exceed the length the caller passes, so a huge specifier cannot allocate more than that.
/// </remarks>
internal sealed class TimeFormatter
{
    /// <summary>The pieces of a plain formatter; empty for a multi-format.</summary>
    private readonly FormatToken[] _tokens;

    /// <summary>The function that picks the formatter of an instant in a multi-format; null for a plain formatter.</summary>
    private readonly Func<double, TimeFormatter>? _selector;

    /// <summary>The calendar a multi-format is bound to; null for a plain formatter.</summary>
    private readonly TimeCalendar? _boundCalendar;

    /// <summary>
    ///     Initializes a plain formatter.
    /// </summary>
    /// <param name="tokens">The compiled pieces.</param>
    internal TimeFormatter(FormatToken[] tokens)
    {
        _tokens = tokens;
    }

    /// <summary>
    ///     Initializes a multi-format formatter.
    /// </summary>
    /// <param name="calendar">The calendar the formats are bound to.</param>
    /// <param name="selector">The function that chooses the plain formatter for an instant.</param>
    internal TimeFormatter(TimeCalendar calendar, Func<double, TimeFormatter> selector)
    {
        _tokens = [];
        _boundCalendar = calendar;
        _selector = selector;
    }

    /// <summary>
    ///     Formats an instant.
    /// </summary>
    /// <param name="ms">The instant in milliseconds since the Unix epoch; a fraction is truncated.</param>
    /// <param name="calendar">
    ///     The calendar whose wall time is formatted (UTC gives <c>utcFormat</c>, a zone gives <c>timeFormat</c>). A
    ///     multi-format formatter is bound to the calendar given to <see cref="TimeFormat.Multi"/> and ignores this one.
    /// </param>
    /// <param name="maxLength">The longest text allowed.</param>
    /// <returns>The text, or <see cref="TimeFormat.InvalidDateText"/> for an instant that is NaN or outside the calendar's range.</returns>
    /// <exception cref="TimeFormatTooLongException">The text would be longer than <paramref name="maxLength"/>.</exception>
    public string Format(double ms, TimeCalendar calendar, int maxLength)
    {
        var limit = Math.Max(maxLength, 0);
        var output = new StringBuilder();
        var cal = _boundCalendar ?? calendar;
        if (!TimeCalendar.IsValid(ms))
        {
            Append(output, TimeFormat.InvalidDateText, limit);
            return output.ToString();
        }

        var instant = Math.Truncate(ms);
        var formatter = _selector is null ? this : _selector(instant);
        formatter.WriteTokens(output, instant, cal, limit);
        return output.ToString();
    }

    /// <summary>
    ///     Appends text to the output unless that would exceed the limit.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="text">The text.</param>
    /// <param name="limit">The largest allowed length of the output.</param>
    /// <exception cref="TimeFormatTooLongException">The result would be longer than the limit.</exception>
    private static void Append(StringBuilder output, string text, int limit)
    {
        if (output.Length + text.Length > limit)
        {
            throw new TimeFormatTooLongException(limit);
        }

        output.Append(text);
    }

    /// <summary>
    ///     Gets the text of one directive for an instant.
    /// </summary>
    /// <param name="directive">The directive letter.</param>
    /// <param name="fill">The fill character of numeric directives.</param>
    /// <param name="p">The wall-clock parts of the instant.</param>
    /// <param name="ms">The instant.</param>
    /// <returns>The text.</returns>
    private static string DirectiveText(char directive, char fill, in DateParts p, double ms) => directive switch
    {
        'a' => TimeFormat.ShortDays[p.Day],
        'A' => TimeFormat.Days[p.Day],
        'b' => TimeFormat.ShortMonths[p.Month],
        'B' => TimeFormat.Months[p.Month],
        'd' or 'e' => TimeFormat.Pad(p.Date, fill, 2),
        'f' => TimeFormat.Pad(p.Milliseconds, fill, 3) + "000",
        'H' => TimeFormat.Pad(p.Hours, fill, 2),
        'I' => TimeFormat.Pad(p.Hours % 12 == 0 ? 12 : p.Hours % 12, fill, 2),
        'j' => TimeFormat.Pad(p.DayOfYear, fill, 3),
        'L' => TimeFormat.Pad(p.Milliseconds, fill, 3),
        'm' => TimeFormat.Pad(p.Month + 1, fill, 2),
        'M' => TimeFormat.Pad(p.Minutes, fill, 2),
        'p' => TimeFormat.Periods[p.Hours >= 12 ? 1 : 0],
        'q' => TimeFormat.Pad((p.Month / 3) + 1, '\0', 1),
        'Q' => TimeFormat.Pad((long)ms, '\0', 1),
        's' => TimeFormat.Pad((long)Math.Floor(ms / 1000.0), '\0', 1),
        'S' => TimeFormat.Pad(p.Seconds, fill, 2),
        'u' => TimeFormat.Pad(p.Day == 0 ? 7 : p.Day, '\0', 1),
        'w' => TimeFormat.Pad(p.Day, '\0', 1),
        'U' => TimeFormat.Pad((p.DayOfYear - 1 + 7 - p.Day) / 7, fill, 2),
        'W' => TimeFormat.Pad((p.DayOfYear - 1 + 7 - ((p.Day + 6) % 7)) / 7, fill, 2),
        'y' => TimeFormat.Pad(p.Year % 100, fill, 2),
        'Y' => TimeFormat.Pad(p.Year % 10000, fill, 4),
        'V' or 'g' or 'G' => IsoText(directive, fill, p),
        'Z' => ZoneText(p.OffsetMs),
        _ => "%",
    };

    /// <summary>
    ///     Gets the text of the ISO 8601 week directives: <c>%V</c> (week number), <c>%G</c> (week-based year) and <c>%g</c> (its last two digits).
    /// </summary>
    /// <param name="directive"><c>V</c>, <c>G</c> or <c>g</c>.</param>
    /// <param name="fill">The fill character.</param>
    /// <param name="p">The wall-clock parts.</param>
    /// <returns>The text.</returns>
    private static string IsoText(char directive, char fill, in DateParts p)
    {
        // The ISO week belongs to the year of its Thursday: d3 moves the date to that Thursday, then counts Thursdays
        var days = TimeCalendar.DaysFromCivil(p.Year, p.Month + 1, p.Date);
        var thursday = p.Day is >= 4 or 0 ? days - ((p.Day + 3) % 7) : days + (4 - p.Day);
        TimeCalendar.CivilFromDays(thursday, out var year, out _, out _);
        if (directive == 'V')
        {
            var firstDay = TimeCalendar.FirstWeekdayOfYear(year);
            var offset = thursday - TimeCalendar.DaysFromCivil(year, 1, 1);
            var count = (offset + ((firstDay + 3) % 7)) / 7;
            return TimeFormat.Pad(count + (firstDay == 4 ? 1 : 0), fill, 2);
        }

        return directive == 'G' ? TimeFormat.Pad(year % 10000, fill, 4) : TimeFormat.Pad(year % 100, fill, 2);
    }

    /// <summary>
    ///     Gets the text of the <c>%Z</c> directive: the offset as <c>+hhmm</c> or <c>-hhmm</c>.
    /// </summary>
    /// <param name="offsetMs">The offset from UTC in milliseconds; whole minutes are shown (the seconds of historic offsets are dropped).</param>
    /// <returns>The text; <c>+0000</c> for UTC.</returns>
    private static string ZoneText(double offsetMs)
    {
        var minutes = (long)Math.Truncate(offsetMs / 60_000.0);
        var absolute = Math.Abs(minutes);
        return (minutes < 0 ? "-" : "+") + TimeFormat.Pad(absolute / 60, '0', 2) + TimeFormat.Pad(absolute % 60, '0', 2);
    }

    /// <summary>
    ///     Writes the pieces of a plain formatter for an instant.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="ms">The valid, whole-millisecond instant.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="limit">The largest allowed length of the output.</param>
    private void WriteTokens(StringBuilder output, double ms, TimeCalendar calendar, int limit)
    {
        var parts = calendar.ToParts(ms);
        foreach (var token in _tokens)
        {
            Append(output, token.Literal ?? DirectiveText(token.Directive, token.Fill, parts, ms), limit);
        }
    }
}

/// <summary>
///     The compiler of format specifiers.
/// </summary>
internal static partial class TimeFormat
{
    /// <summary>The directive letters that produce text (the locale directives <c>c</c>, <c>x</c> and <c>X</c> and the percent sign are handled separately).</summary>
    private static readonly char[] KnownDirectives =
    [
        'a', 'A', 'b', 'B', 'd', 'e', 'f', 'g', 'G', 'H', 'I', 'j', 'L', 'm', 'M', 'p', 'q', 'Q', 's', 'S', 'u', 'U', 'V', 'w', 'W', 'y', 'Y', 'Z',
    ];

    /// <summary>
    ///     Compiles a specifier like d3's <c>timeFormat(specifier)</c>.
    /// </summary>
    /// <param name="specifier">
    ///     The specifier: literal text and directives <c>%a %A %b %B %c %d %e %f %g %G %H %I %j %L %m %M %p %q %Q %s %S %u
    ///     %U %V %w %W %x %X %y %Y %Z %%</c>, each optionally with a padding flag (<c>-</c> none, <c>_</c> space, <c>0</c>
    ///     zero) between the percent sign and the letter.
    /// </param>
    /// <returns>The formatter.</returns>
    /// <remarks>
    ///     The cost is linear in the length of the specifier, which is limited (<see cref="MaxSpecifierLength"/> characters and
    ///     <see cref="MaxTokenCount"/> pieces after the locale directives are expanded, so a specifier of <c>%c</c> directives
    ///     cannot grow into a huge token list). Like d3, an unknown directive is not
    ///     an error: the percent sign is dropped and the letter is copied (<c>%k</c> gives <c>k</c>), and a percent sign
    ///     at the end of the specifier gives nothing. The locale directives <c>%c</c>, <c>%x</c> and <c>%X</c> expand to
    ///     <c>%x, %X</c>, <c>%-m/%-d/%Y</c> and <c>%-I:%M:%S %p</c>, and ignore a padding flag.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="specifier"/> is null.</exception>
    /// <exception cref="TimeFormatSpecifierException">Thrown when the specifier is longer than <see cref="MaxSpecifierLength"/> or has more than <see cref="MaxTokenCount"/> pieces.</exception>
    public static TimeFormatter Compile(string specifier)
    {
        ArgumentNullException.ThrowIfNull(specifier);
        CheckSpecifierLength(specifier);
        var tokens = new List<FormatToken>();
        Tokenize(specifier, tokens, 0);
        CheckTokenCount(tokens);
        return new TimeFormatter([.. tokens]);
    }

    /// <summary>
    ///     Checks that a token list is not longer than <see cref="MaxTokenCount"/>.
    /// </summary>
    /// <param name="tokens">The tokens.</param>
    /// <exception cref="TimeFormatSpecifierException">Thrown when the list is too long.</exception>
    private static void CheckTokenCount(List<FormatToken> tokens)
    {
        if (tokens.Count > MaxTokenCount)
        {
            throw new TimeFormatSpecifierException($"The time format expands to more than the limit of {MaxTokenCount} pieces.");
        }
    }

    /// <summary>
    ///     Checks that a specifier is not longer than <see cref="MaxSpecifierLength"/>.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <exception cref="TimeFormatSpecifierException">Thrown when the specifier is too long.</exception>
    internal static void CheckSpecifierLength(string specifier)
    {
        if (specifier.Length > MaxSpecifierLength)
        {
            throw new TimeFormatSpecifierException($"The time format is longer than the limit of {MaxSpecifierLength} characters.");
        }
    }

    /// <summary>
    ///     Splits a specifier into literal and directive tokens.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="tokens">The list that receives the tokens.</param>
    /// <param name="depth">The nesting depth of locale expansions (the locale specifiers never nest more than twice).</param>
    /// <exception cref="TimeFormatSpecifierException">Thrown when the tokens are more than <see cref="MaxTokenCount"/>.</exception>
    private static void Tokenize(string specifier, List<FormatToken> tokens, int depth)
    {
        var literal = new StringBuilder();
        var i = 0;
        while (i < specifier.Length)
        {
            // A locale directive adds a dozen tokens at most, so the list never outgrows the limit by more than that
            CheckTokenCount(tokens);

            if (specifier[i] != '%')
            {
                literal.Append(specifier[i++]);
                continue;
            }

            // d3: c is the character after the percent sign, or after a padding flag; past the end of the text it is empty (-1)
            i++;
            var c = i < specifier.Length ? specifier[i] : -1;
            i++;
            char fill;
            if (c >= 0 && IsPadFlag((char)c, out fill))
            {
                c = i < specifier.Length ? specifier[i] : -1;
                i++;
            }
            else
            {
                fill = c == 'e' ? ' ' : '0';
            }
            AddDirective(c, fill, literal, tokens, depth);
        }

        Flush(literal, tokens);
    }

    /// <summary>
    ///     Adds the token of one directive (or the copied character of an unknown one).
    /// </summary>
    /// <param name="c">The directive letter, or -1 when the specifier ended before the letter (the letter is empty).</param>
    /// <param name="fill">The fill character.</param>
    /// <param name="literal">The literal text collected so far.</param>
    /// <param name="tokens">The token list.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void AddDirective(int c, char fill, StringBuilder literal, List<FormatToken> tokens, int depth)
    {
        if (c < 0)
        {
            return;
        }

        if (c == '%')
        {
            literal.Append('%');
        }
        else if (c is 'c' or 'x' or 'X')
        {
            // The locale specifiers are pasted in (their pads do not matter); the depth guard is never reached with this locale
            Flush(literal, tokens);
            if (depth < 4)
            {
                var expansion = c == 'c' ? DateTimeSpecifier : TimeSpecifier;
                Tokenize(c == 'x' ? DateSpecifier : expansion, tokens, depth + 1);
            }
        }
        else if (Array.IndexOf(KnownDirectives, (char)c) >= 0)
        {
            Flush(literal, tokens);
            tokens.Add(new FormatToken((char)c, fill, null));
        }
        else
        {
            literal.Append((char)c);
        }
    }

    /// <summary>
    ///     Moves the collected literal text into the token list.
    /// </summary>
    /// <param name="literal">The collected text, cleared by this call.</param>
    /// <param name="tokens">The token list.</param>
    private static void Flush(StringBuilder literal, List<FormatToken> tokens)
    {
        if (literal.Length > 0)
        {
            tokens.Add(new FormatToken('\0', '\0', literal.ToString()));
            literal.Clear();
        }
    }
}
