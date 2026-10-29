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
///         common cases: <c>\d</c>, <c>\D</c>, <c>\w</c> and <c>\W</c> are the ASCII classes, <c>$</c> without the
///         <c>m</c> flag matches only at the very end of the text, and <c>[^]</c> and <c>[]</c> have their JavaScript
///         meaning. The flags <c>g</c>, <c>i</c>, <c>m</c>, <c>s</c> and <c>u</c> are accepted; <c>g</c> is only
///         observable through <see cref="IsGlobal"/> (<c>replace</c> replaces every match). The object is immutable and
///         safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class ExpressionRegex
{
    /// <summary>The longest pattern accepted, in UTF-16 code units.</summary>
    internal const int MaxPatternLength = 1_024;

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

    /// <summary>Gets the flags in canonical order (<c>gimsu</c>).</summary>
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
        var invalid = flags.FirstOrDefault(f => "gimsu".IndexOf(f, StringComparison.Ordinal) < 0);
        if (invalid != '\0' || seen.Count != flags.Length)
        {
            throw new ExpressionRuntimeException(
                $"The regular expression flags '{flags}' are not supported or repeat a flag; the flags g, i, m, s and u are supported.");
        }

        options |= seen.Contains('i') ? RegexOptions.IgnoreCase : RegexOptions.None;
        options |= seen.Contains('m') ? RegexOptions.Multiline : RegexOptions.None;
        options |= seen.Contains('s') ? RegexOptions.Singleline : RegexOptions.None;
        var canonical = new string("gimsu".Where(seen.Contains).ToArray());

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
        switch (escaped)
        {
            case 'd':
                text.Append(inClass ? "0-9" : "[0-9]");
                break;
            case 'w':
                text.Append(inClass ? "a-zA-Z0-9_" : "[a-zA-Z0-9_]");
                break;
            case 'D' when !inClass:
                text.Append("[^0-9]");
                break;
            case 'W' when !inClass:
                text.Append("[^a-zA-Z0-9_]");
                break;
            default:
                text.Append('\\').Append(escaped);
                break;
        }
    }
}
