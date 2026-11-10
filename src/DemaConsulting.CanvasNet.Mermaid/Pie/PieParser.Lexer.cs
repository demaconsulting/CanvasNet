// Portions derived from @mermaid-js/parser (MIT) and Langium (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Pie;

/// <summary>
///     A token of the pie lexer: its kind (the Langium token name), and where its text is in the parser text.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Start">The index of the first character.</param>
/// <param name="End">The index after the last character.</param>
internal readonly record struct PieToken(string Kind, int Start, int End);

/// <content>
///     The lexer: the token types Langium builds from <c>pie.langium</c> and <c>common.langium</c> (with the token builder
///     of <c>@mermaid-js/parser</c>), tried in chevrotain's order at each position, the first that matches winning.
/// </content>
internal sealed partial class PieParser
{
    /// <summary>The <c>showData</c> keyword.</summary>
    internal const string ShowData = "showData";

    /// <summary>The <c>pie</c> keyword.</summary>
    internal const string PieKeyword = "pie";

    /// <summary>The <c>:</c> keyword.</summary>
    internal const string Colon = ":";

    /// <summary>A section value (<c>FLOAT_PIE</c> or <c>INT_PIE</c>).</summary>
    internal const string NumberPie = "NUMBER_PIE";

    /// <summary>An accessible description, single-line or in braces.</summary>
    internal const string AccDescr = "ACC_DESCR";

    /// <summary>An accessible title.</summary>
    internal const string AccTitle = "ACC_TITLE";

    /// <summary>A title.</summary>
    internal const string Title = "TITLE";

    /// <summary>A quoted string.</summary>
    internal const string StringToken = "STRING";

    /// <summary>A line break.</summary>
    internal const string Newline = "NEWLINE";

    /// <summary>The end of the tokens (chevrotain's <c>EOF</c>).</summary>
    internal const string Eof = "EOF";

    /// <summary>The token matchers in chevrotain's order; a null kind is a hidden token, which the parser never sees.</summary>
    private static readonly (string? Kind, Func<string, int, int> Match)[] Rules =
    [
        // The keywords, longest first, each followed by %% or not by a non-space (the mermaid token builder's suffix)
        (ShowData, (text, p) => MatchKeyword(text, p, ShowData)),
        (PieKeyword, (text, p) => MatchKeyword(text, p, PieKeyword)),
        (Colon, (text, p) => p < text.Length && text[p] == ':' ? p + 1 : -1),

        // The terminals in the order of the grammar
        (NumberPie, MatchNumber),
        (AccDescr, MatchAccDescr),
        (AccTitle, MatchAccTitle),
        (Title, MatchTitle),
        (StringToken, MatchString),
        (Newline, MatchNewline),

        // The hidden terminals: white space (skipped), YAML, directives and comments
        (null, (text, p) => SkipSpaces(text, p) is var e && e > p ? e : -1),
        (null, MatchYaml),
        (null, MatchDirective),
        (null, MatchComment),
    ];

    /// <summary>
    ///     Splits the parser text into the tokens the grammar reads, hidden tokens left out.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The tokens, ending with one <see cref="Eof"/> token.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, at the first character no token matches.</exception>
    private List<PieToken> Tokenize(string text)
    {
        var tokens = new List<PieToken>();
        var position = 0;
        while (position < text.Length)
        {
            var matched = false;
            foreach (var (kind, match) in Rules)
            {
                var end = match(text, position);
                if (end <= position)
                {
                    continue;
                }

                if (kind is not null)
                {
                    tokens.Add(new PieToken(kind, position, end));
                }

                position = end;
                matched = true;
                break;
            }

            if (!matched)
            {
                throw new MermaidSyntaxException(
                    MermaidDiagnosticCodes.SyntaxError,
                    $"unrecognized text {SourceScanner.Describe(text, position)}",
                    _text.Locate(position),
                    "write each slice as a quoted label, a colon and a number (\"Dogs\" : 42), and the title, accTitle and accDescr statements on lines of their own");
            }
        }

        tokens.Add(new PieToken(Eof, text.Length, text.Length));
        return tokens;
    }

    /// <summary>
    ///     Matches a keyword followed by <c>%%</c> or by no character other than white space (<c>(?:(?=%%)|(?!\S))</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchKeyword(string text, int p, string keyword)
    {
        if (string.CompareOrdinal(text, p, keyword, 0, keyword.Length) != 0)
        {
            return -1;
        }

        var end = p + keyword.Length;
        return end >= text.Length || ScanPatterns.IsJsWhitespace(text[end]) || IsAt(text, end, "%%") ? end : -1;
    }

    /// <summary>
    ///     Matches <c>NUMBER_PIE</c>: <c>FLOAT_PIE</c> (<c>-?[0-9]+\.[0-9]+(?!\.)</c>), else <c>INT_PIE</c>
    ///     (<c>-?(0|[1-9][0-9]*)(?!\.)</c>), with the backtracking of the regular expressions.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchNumber(string text, int p)
    {
        var s = p < text.Length && text[p] == '-' ? p + 1 : p;
        var e1 = SkipDigits(text, s);
        if (e1 == s)
        {
            return -1;
        }

        // FLOAT_PIE: the integer digits cannot give back a dot, so only the fraction digits may shrink
        if (e1 < text.Length && text[e1] == '.')
        {
            var e2 = SkipDigits(text, e1 + 1);
            var fraction = e2 - (e1 + 1);
            if (fraction >= 1 && !IsAt(text, e2, "."))
            {
                return e2;
            }

            if (fraction >= 2)
            {
                return e2 - 1;
            }
        }

        // INT_PIE: a zero alone, or digits without a leading zero
        if (text[s] == '0')
        {
            return IsAt(text, s + 1, ".") ? -1 : s + 1;
        }

        if (!IsAt(text, e1, "."))
        {
            return e1;
        }

        return e1 - s >= 2 ? e1 - 1 : -1;
    }

    /// <summary>
    ///     Matches <c>ACC_DESCR</c>:
    ///     <c>[\t ]*accDescr(?:[\t ]*:([^\n\r]*?(?=%%)|[^\n\r]*)|\s*{([^}]*)})</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchAccDescr(string text, int p)
    {
        var k = SkipSpaces(text, p);
        if (!IsAt(text, k, "accDescr"))
        {
            return -1;
        }

        k += "accDescr".Length;
        var colon = SkipSpaces(text, k);
        if (IsAt(text, colon, ":"))
        {
            return RestOfLineBeforeComment(text, colon + 1);
        }

        var brace = k;
        while (brace < text.Length && ScanPatterns.IsJsWhitespace(text[brace]))
        {
            brace++;
        }

        if (!IsAt(text, brace, "{"))
        {
            return -1;
        }

        var close = text.IndexOf('}', brace + 1);
        return close < 0 ? -1 : close + 1;
    }

    /// <summary>
    ///     Matches <c>ACC_TITLE</c>: <c>[\t ]*accTitle[\t ]*:(?:[^\n\r]*?(?=%%)|[^\n\r]*)</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchAccTitle(string text, int p)
    {
        var k = SkipSpaces(text, p);
        if (!IsAt(text, k, "accTitle"))
        {
            return -1;
        }

        var colon = SkipSpaces(text, k + "accTitle".Length);
        return IsAt(text, colon, ":") ? RestOfLineBeforeComment(text, colon + 1) : -1;
    }

    /// <summary>
    ///     Matches <c>TITLE</c>: <c>[\t ]*title(?:[\t ][^\n\r]*?(?=%%)|[\t ][^\n\r]*|)</c> (the last alternative is empty,
    ///     so <c>title</c> matches before any character).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchTitle(string text, int p)
    {
        var k = SkipSpaces(text, p);
        if (!IsAt(text, k, "title"))
        {
            return -1;
        }

        k += "title".Length;
        return k < text.Length && text[k] is '\t' or ' ' ? RestOfLineBeforeComment(text, k + 1) : k;
    }

    /// <summary>
    ///     Matches <c>STRING</c>: <c>"([^"\\]|\\.)*"|'([^'\\]|\\.)*'</c> (a backslash escapes any character but a line
    ///     terminator).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchString(string text, int p)
    {
        if (p >= text.Length || text[p] is not ('"' or '\''))
        {
            return -1;
        }

        var quote = text[p];
        var i = p + 1;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == quote)
            {
                return i + 1;
            }

            // A backslash escapes the next character, unless it ends a line
            if (c == '\\' && (i + 1 >= text.Length || ScanPatterns.IsJsLineTerminator(text[i + 1])))
            {
                return -1;
            }

            i += c == '\\' ? 2 : 1;
        }

        return -1;
    }

    /// <summary>
    ///     Matches <c>NEWLINE</c>: <c>\r?\n</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchNewline(string text, int p)
    {
        var q = IsAt(text, p, "\r") ? p + 1 : p;
        return IsAt(text, q, "\n") ? q + 1 : -1;
    }

    /// <summary>
    ///     Matches the hidden <c>YAML</c>: <c>---[\t ]*\r?\n(?:[\S\s]*?\r?\n)?---(?:\r?\n|(?!\S))</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchYaml(string text, int p)
    {
        if (!IsAt(text, p, "---"))
        {
            return -1;
        }

        var open = MatchNewline(text, SkipSpaces(text, p + 3));
        if (open < 0)
        {
            return -1;
        }

        // The closing --- after the shortest run that ends with a line break (the optional group is greedy, so it is tried
        // first), else at the start of the body; followed by a line break or by no character other than white space
        for (var close = open + 1; close <= text.Length - 3; close++)
        {
            if (text[close - 1] == '\n' && ClosesYaml(text, close) is var end && end >= 0)
            {
                return end;
            }
        }

        return ClosesYaml(text, open);
    }

    /// <summary>
    ///     Matches the closing <c>---(?:\r?\n|(?!\S))</c> of a YAML block.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int ClosesYaml(string text, int p)
    {
        if (!IsAt(text, p, "---"))
        {
            return -1;
        }

        var after = p + 3;
        var line = MatchNewline(text, after);
        if (line >= 0)
        {
            return line;
        }

        return after >= text.Length || ScanPatterns.IsJsWhitespace(text[after]) ? after : -1;
    }

    /// <summary>
    ///     Matches the hidden <c>DIRECTIVE</c>: <c>[\t ]*%%{[\S\s]*?}%%(?:\r?\n|(?!\S))</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchDirective(string text, int p)
    {
        var k = SkipSpaces(text, p);
        if (!IsAt(text, k, "%%{"))
        {
            return -1;
        }

        // The shortest body whose closing }%% is followed by a line break or by no character other than white space
        for (var close = text.IndexOf("}%%", k + 3, StringComparison.Ordinal); close >= 0; close = text.IndexOf("}%%", close + 1, StringComparison.Ordinal))
        {
            var after = close + 3;
            var line = MatchNewline(text, after);
            if (line >= 0)
            {
                return line;
            }

            if (after >= text.Length || ScanPatterns.IsJsWhitespace(text[after]))
            {
                return after;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Matches the hidden <c>SINGLE_LINE_COMMENT</c>: <c>[\t ]*%%[^\n\r]*</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchComment(string text, int p)
    {
        var k = SkipSpaces(text, p);
        if (!IsAt(text, k, "%%"))
        {
            return -1;
        }

        var end = k + 2;
        while (end < text.Length && text[end] is not ('\n' or '\r'))
        {
            end++;
        }

        return end;
    }

    /// <summary>
    ///     Matches <c>[^\n\r]*?(?=%%)|[^\n\r]*</c>: the rest of the line before its first <c>%%</c>, or the whole rest of
    ///     the line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match.</returns>
    private static int RestOfLineBeforeComment(string text, int p)
    {
        var end = p;
        while (end < text.Length && text[end] is not ('\n' or '\r'))
        {
            if (IsAt(text, end, "%%"))
            {
                return end;
            }

            end++;
        }

        return end;
    }

    /// <summary>
    ///     Skips tabs and spaces (<c>[\t ]*</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position after them.</returns>
    private static int SkipSpaces(string text, int p)
    {
        while (p < text.Length && text[p] is '\t' or ' ')
        {
            p++;
        }

        return p;
    }

    /// <summary>
    ///     Skips ASCII digits (<c>[0-9]*</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position after them.</returns>
    private static int SkipDigits(string text, int p)
    {
        while (p < text.Length && char.IsAsciiDigit(text[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>
    ///     Gets whether a text continues with a literal at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <param name="literal">The literal.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsAt(string text, int p, string literal) =>
        p >= 0 && p + literal.Length <= text.Length && string.CompareOrdinal(text, p, literal, 0, literal.Length) == 0;
}
