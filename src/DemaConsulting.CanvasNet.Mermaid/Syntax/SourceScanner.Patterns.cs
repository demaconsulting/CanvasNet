namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     The building blocks of the lexer rules of <see cref="SourceScanner"/>: small matchers that replace the regular
///     expressions of Mermaid's jison grammars, and the JavaScript character classes those expressions use.
/// </summary>
/// <remarks>
///     <para>
///         Every matcher is anchored at the position it is given, is greedy and does not backtrack, so it runs in time
///         linear in what it inspects. A jison expression whose meaning depends on backtracking is written as a dedicated
///         matcher by the grammar that needs it. The character classes are those of JavaScript regular expressions without
///         the <c>u</c> flag, which is what Mermaid's grammars and preprocessing use: <c>\s</c> is
///         <see cref="IsJsWhitespace(char)"/>, <c>\w</c> is <see cref="IsJsWordChar(char)"/>, and <c>.</c> is any character
///         that is not <see cref="IsJsLineTerminator(char)"/>.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use; the matchers it returns are too.
///     </para>
/// </remarks>
internal static class ScanPatterns
{
    /// <summary>
    ///     Gets a matcher that matches only at the end of the input, consuming nothing (jison's <c>&lt;&lt;EOF&gt;&gt;</c>).
    /// </summary>
    public static ScanMatcher EndOfInput { get; } = static (input, position) => position >= input.Length ? 0 : -1;

    /// <summary>
    ///     Gets a value indicating whether a character is white space for a JavaScript regular expression (<c>\s</c>).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether the character is in the class.</returns>
    public static bool IsJsWhitespace(char c) => c switch
    {
        '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or '\u00a0' or '\u1680' or '\u2028' or '\u2029' or '\u202f' or '\u205f' or '\u3000' or '\ufeff' => true,
        _ => c >= '\u2000' && c <= '\u200a',
    };

    /// <summary>
    ///     Gets a value indicating whether a character ends a line for a JavaScript regular expression (the characters
    ///     that <c>.</c> does not match).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether the character is a line terminator.</returns>
    public static bool IsJsLineTerminator(char c) => c is '\n' or '\r' or '\u2028' or '\u2029';

    /// <summary>
    ///     Gets a value indicating whether a character is a word character for a JavaScript regular expression
    ///     (<c>\w</c>: ASCII letters, digits and the underscore).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether the character is in the class.</returns>
    public static bool IsJsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    /// <summary>
    ///     Creates a matcher of a fixed text.
    /// </summary>
    /// <param name="text">The text; not empty.</param>
    /// <param name="ignoreCase">Whether ASCII letters match regardless of case (the <c>case-insensitive</c> option of a grammar).</param>
    /// <returns>The matcher.</returns>
    public static ScanMatcher Literal(string text, bool ignoreCase = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return (input, position) =>
            position + text.Length <= input.Length && string.Compare(input, position, text, 0, text.Length, comparison) == 0
                ? text.Length
                : -1;
    }

    /// <summary>
    ///     Creates a matcher of a run of characters of a class (<c>[...]+</c>, or <c>[...]*</c> with a minimum of zero).
    /// </summary>
    /// <param name="predicate">The class.</param>
    /// <param name="minimum">The fewest characters the run must have.</param>
    /// <returns>The matcher.</returns>
    public static ScanMatcher Run(Func<char, bool> predicate, int minimum = 1)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return (input, position) =>
        {
            var end = position;
            while (end < input.Length && predicate(input[end]))
            {
                end++;
            }

            return end - position >= minimum ? end - position : -1;
        };
    }

    /// <summary>
    ///     Creates a matcher of parts that must match one after the other.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The matcher; it fails when any part fails at its position.</returns>
    public static ScanMatcher Sequence(params ScanMatcher[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return (input, position) =>
        {
            var end = position;
            foreach (var part in parts)
            {
                var length = part(input, end);
                if (length < 0)
                {
                    return -1;
                }

                end += length;
            }

            return end - position;
        };
    }

    /// <summary>
    ///     Creates a matcher of the first alternative that matches (<c>a|b</c>, tried in order).
    /// </summary>
    /// <param name="alternatives">The alternatives.</param>
    /// <returns>The matcher.</returns>
    public static ScanMatcher Choice(params ScanMatcher[] alternatives)
    {
        ArgumentNullException.ThrowIfNull(alternatives);
        return (input, position) =>
        {
            foreach (var alternative in alternatives)
            {
                var length = alternative(input, position);
                if (length >= 0)
                {
                    return length;
                }
            }

            return -1;
        };
    }

    /// <summary>
    ///     Creates a matcher that matches a part or nothing (<c>(...)?</c>).
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>The matcher; it never fails.</returns>
    public static ScanMatcher Optional(ScanMatcher part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return (input, position) => Math.Max(part(input, position), 0);
    }

    /// <summary>
    ///     Creates a matcher that matches a part only when another does not match right after it (<c>a(?!b)</c>).
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="next">The part that must not follow.</param>
    /// <returns>The matcher.</returns>
    public static ScanMatcher NotFollowedBy(ScanMatcher part, ScanMatcher next)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(next);
        return (input, position) =>
        {
            var length = part(input, position);
            return length >= 0 && next(input, position + length) < 0 ? length : -1;
        };
    }
}
