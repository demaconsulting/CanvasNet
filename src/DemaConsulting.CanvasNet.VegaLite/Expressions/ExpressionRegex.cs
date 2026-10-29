using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     A regular expression of an expression: the JavaScript source and flags and the safe .NET regular expression
///     that implements them.
/// </summary>
/// <remarks>
///     <para>
///         The .NET expression is built with <see cref="RegexOptions.NonBacktracking"/>, which guarantees matching in
///         time linear in the input and so removes catastrophic backtracking, plus <see cref="RegexOptions.CultureInvariant"/>
///         so the current culture never changes a match. Patterns that the non-backtracking engine cannot run (look-around,
///         back-references, atomic groups) fall back to the backtracking engine with a short match timeout, and a match
///         that exceeds it fails closed with an <see cref="ExpressionRuntimeException"/>; the same timeout is set on the
///         non-backtracking engine as a backstop. The pattern length is bounded by <see cref="MaxPatternLength"/>.
///     </para>
///     <para>
///         The JavaScript pattern is translated in a small number of places where .NET and JavaScript differ for the
///         common cases: <c>\d</c>, <c>\D</c>, <c>\w</c> and <c>\W</c> are the ASCII classes (also inside a character class,
///         negated or not), <c>\s</c> and <c>\S</c> are the JavaScript white space, <c>$</c> without the
///         <c>m</c> flag matches only at the very end of the text, and <c>[^]</c> and <c>[]</c> have their JavaScript
///         meaning. The flags <c>g</c>, <c>i</c>, <c>m</c> and <c>s</c> are accepted; <c>g</c> is only
///         observable through <see cref="IsGlobal"/> (<c>replace</c> replaces every match). The flags <c>u</c>, <c>v</c>, <c>y</c>
///         and <c>d</c> are rejected: the engine matches UTF-16 code units and has no sticky matching or match indices, so
///         accepting them would silently change a result (<c>/./u</c> matches a surrogate pair as one character in JavaScript).
///         The object is immutable and
///         safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class ExpressionRegex
{
    /// <summary>The longest pattern accepted, in UTF-16 code units.</summary>
    internal const int MaxPatternLength = 1_024;

    /// <summary>The flags that are honored, in canonical order.</summary>
    private static readonly char[] SupportedFlags = ['g', 'i', 'm', 's'];

    /// <summary>The match timeout of the non-backtracking engine, a backstop that linear matching never reaches.</summary>
    private static readonly TimeSpan LinearTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The short match timeout of the backtracking fallback.</summary>
    private static readonly TimeSpan BacktrackingTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionRegex"/> class.
    /// </summary>
    /// <param name="source">The JavaScript pattern text.</param>
    /// <param name="flags">The JavaScript flags in canonical order.</param>
    /// <param name="regex">The .NET regular expression.</param>
    /// <param name="isGlobal">Whether the <c>g</c> flag is set.</param>
    private ExpressionRegex(string source, string flags, Regex regex, bool isGlobal)
    {
        Source = source;
        Flags = flags;
        Regex = regex;
        IsGlobal = isGlobal;
    }

    /// <summary>Gets the JavaScript pattern text.</summary>
    public string Source { get; }

    /// <summary>Gets the flags in canonical order (<c>g</c>, <c>i</c>, <c>m</c>, <c>s</c>).</summary>
    public string Flags { get; }

    /// <summary>Gets a value indicating whether the <c>g</c> flag is set.</summary>
    public bool IsGlobal { get; }

    /// <summary>Gets the .NET regular expression.</summary>
    public Regex Regex { get; }

    /// <summary>
    ///     Creates a regular expression.
    /// </summary>
    /// <param name="source">The JavaScript pattern text.</param>
    /// <param name="flags">The JavaScript flags.</param>
    /// <returns>The regular expression.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the pattern is too long or invalid or a flag is unknown or repeated.</exception>
    public static ExpressionRegex Create(string source, string flags)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(flags);
        if (source.Length > MaxPatternLength)
        {
            throw new ExpressionRuntimeException(
                $"The regular expression pattern has {source.Length} characters, more than the limit of {MaxPatternLength}.");
        }

        // Validate the flags and map them to the options of the .NET engine
        var options = RegexOptions.CultureInvariant;
        var seen = new HashSet<char>(flags);
        var invalid = flags.FirstOrDefault(f => !SupportedFlags.Contains(f));
        if (invalid != '\0' || seen.Count != flags.Length)
        {
            throw new ExpressionRuntimeException(
                $"The regular expression flags '{flags}' are not supported or repeat a flag; the flags g, i, m and s are supported (the flags u, v, y and d are not).");
        }

        options |= seen.Contains('i') ? RegexOptions.IgnoreCase : RegexOptions.None;
        options |= seen.Contains('m') ? RegexOptions.Multiline : RegexOptions.None;
        options |= seen.Contains('s') ? RegexOptions.Singleline : RegexOptions.None;
        var canonical = new string(SupportedFlags.Where(seen.Contains).ToArray());

        // Prefer the linear-time engine; patterns it cannot run use the backtracking engine under a short timeout
        var pattern = Translate(source, seen.Contains('m'));
        Regex regex;
        try
        {
            try
            {
                regex = new Regex(pattern, options | RegexOptions.NonBacktracking, LinearTimeout);
            }
            catch (NotSupportedException)
            {
                regex = new Regex(pattern, options, BacktrackingTimeout);
            }
        }
        catch (ArgumentException ex)
        {
            throw new ExpressionRuntimeException($"The regular expression /{source}/ is not valid: {ex.Message}");
        }

        return new ExpressionRegex(source, canonical, regex, seen.Contains('g'));
    }

    /// <summary>
    ///     Formats the regular expression the way JavaScript does: <c>/source/flags</c>.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString() => "/" + Source + "/" + Flags;

    /// <summary>
    ///     Translates the places where a JavaScript pattern differs from the .NET meaning for common cases.
    /// </summary>
    /// <param name="source">The JavaScript pattern.</param>
    /// <param name="multiline">Whether the multiline flag is set.</param>
    /// <returns>The .NET pattern.</returns>
    private static string Translate(string source, bool multiline)
    {
        var text = new StringBuilder(source.Length + 8);
        var inClass = false;
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '\\' && i + 1 < source.Length)
            {
                AppendEscape(text, source[i + 1], inClass);
                i += 2;
                continue;
            }

            if (c == '[' && !inClass && string.CompareOrdinal(source, i, "[^]", 0, 3) == 0)
            {
                // "[^]" matches any character in JavaScript
                text.Append("[\\s\\S]");
                i += 3;
                continue;
            }

            if (c == '[' && !inClass && string.CompareOrdinal(source, i, "[]", 0, 2) == 0)
            {
                // "[]" matches nothing in JavaScript
                text.Append("(?!)");
                i += 2;
                continue;
            }

            if (c == '[')
            {
                inClass = true;
            }
            else if (c == ']')
            {
                inClass = false;
            }

            if (c == '$' && !inClass && !multiline)
            {
                text.Append("\\z");
            }
            else
            {
                text.Append(c);
            }

            i++;
        }

        return text.ToString();
    }

    /// <summary>
    ///     Appends an escape sequence, replacing the Unicode-aware shorthand classes by their ASCII meaning.
    /// </summary>
    /// <param name="text">The pattern being built.</param>
    /// <param name="escaped">The character after the backslash.</param>
    /// <param name="inClass">Whether the escape is inside a character class.</param>
    private static void AppendEscape(StringBuilder text, char escaped, bool inClass)
    {
        // A negated shorthand inside a class is written as the ranges of its complement, which a class can hold whether it is
        // negated or not (.NET has no nested negation); outside a class it is a class of its own
        var items = escaped switch
        {
            'd' => DigitItems,
            'D' => NonDigitItems,
            'w' => WordItems,
            'W' => NonWordItems,
            's' => SpaceItems,
            'S' => NonSpaceItems,
            _ => null,
        };
        if (items is null)
        {
            text.Append('\\').Append(escaped);
        }
        else if (inClass)
        {
            text.Append(items);
        }
        else
        {
            text.Append('[').Append(items).Append(']');
        }
    }

    /// <summary>The items of the class of the ASCII digits.</summary>
    private const string DigitItems = "0-9";

    /// <summary>The items of the class of everything but the ASCII digits (UTF-16 code units).</summary>
    private const string NonDigitItems = "\\u0000-\\u002F\\u003A-\\uFFFF";

    /// <summary>The items of the class of the ASCII word characters.</summary>
    private const string WordItems = "a-zA-Z0-9_";

    /// <summary>The items of the class of everything but the ASCII word characters (UTF-16 code units).</summary>
    private const string NonWordItems = "\\u0000-\\u002F\\u003A-\\u0040\\u005B-\\u005E\\u0060\\u007B-\\uFFFF";

    /// <summary>The items of the class of the JavaScript white space and line terminators (not the .NET set, which has U+0085).</summary>
    private const string SpaceItems = "\\u0009-\\u000D\\u0020\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000\\uFEFF";

    /// <summary>The items of the class of everything but the JavaScript white space (UTF-16 code units).</summary>
    private const string NonSpaceItems =
        "\\u0000-\\u0008\\u000E-\\u001F\\u0021-\\u009F\\u00A1-\\u167F\\u1681-\\u1FFF\\u200B-\\u2027\\u202A-\\u202E\\u2030-\\u205E"
        + "\\u2060-\\u2FFF\\u3001-\\uFEFE\\uFF00-\\uFFFF";
}
