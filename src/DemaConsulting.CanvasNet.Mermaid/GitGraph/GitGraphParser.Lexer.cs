// Portions derived from @mermaid-js/parser (MIT) and Langium (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.GitGraph;

/// <summary>
///     A token of the git graph lexer: its kind (the Langium token name, or the keyword), and where its text is in the
///     parser text.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Start">The index of the first character.</param>
/// <param name="End">The index after the last character.</param>
internal readonly record struct GitGraphToken(string Kind, int Start, int End);

/// <content>
///     The lexer: the token types Langium builds from <c>gitGraph.langium</c> and <c>common.langium</c> (with the token
///     builder of <c>@mermaid-js/parser</c>), tried in chevrotain's order at each position, the first that matches winning,
///     except that a keyword gives way to a longer <c>REFERENCE</c> (its <c>LONGER_ALT</c>).
/// </content>
internal sealed partial class GitGraphParser
{
    /// <summary>The <c>gitGraph</c> keyword.</summary>
    internal const string GitGraphKeyword = "gitGraph";

    /// <summary>The <c>gitGraph:</c> keyword.</summary>
    internal const string GitGraphColonKeyword = "gitGraph:";

    /// <summary>The <c>commit</c> keyword.</summary>
    internal const string CommitKeyword = "commit";

    /// <summary>The <c>branch</c> keyword.</summary>
    internal const string BranchKeyword = "branch";

    /// <summary>The <c>merge</c> keyword.</summary>
    internal const string MergeKeyword = "merge";

    /// <summary>The <c>checkout</c> keyword.</summary>
    internal const string CheckoutKeyword = "checkout";

    /// <summary>The <c>switch</c> keyword.</summary>
    internal const string SwitchKeyword = "switch";

    /// <summary>The <c>cherry-pick</c> keyword.</summary>
    internal const string CherryPickKeyword = "cherry-pick";

    /// <summary>The <c>id:</c> keyword.</summary>
    internal const string IdKeyword = "id:";

    /// <summary>The <c>msg:</c> keyword.</summary>
    internal const string MsgKeyword = "msg:";

    /// <summary>The <c>tag:</c> keyword.</summary>
    internal const string TagKeyword = "tag:";

    /// <summary>The <c>type:</c> keyword.</summary>
    internal const string TypeKeyword = "type:";

    /// <summary>The <c>order:</c> keyword.</summary>
    internal const string OrderKeyword = "order:";

    /// <summary>The <c>parent:</c> keyword.</summary>
    internal const string ParentKeyword = "parent:";

    /// <summary>The <c>NORMAL</c> commit type.</summary>
    internal const string NormalKeyword = "NORMAL";

    /// <summary>The <c>REVERSE</c> commit type.</summary>
    internal const string ReverseKeyword = "REVERSE";

    /// <summary>The <c>HIGHLIGHT</c> commit type.</summary>
    internal const string HighlightKeyword = "HIGHLIGHT";

    /// <summary>The <c>LR</c> direction.</summary>
    internal const string LeftToRight = "LR";

    /// <summary>The <c>TB</c> direction.</summary>
    internal const string TopToBottom = "TB";

    /// <summary>The <c>BT</c> direction.</summary>
    internal const string BottomToTop = "BT";

    /// <summary>The <c>:</c> keyword.</summary>
    internal const string Colon = ":";

    /// <summary>An accessible description, single-line or in braces.</summary>
    internal const string AccDescr = "ACC_DESCR";

    /// <summary>An accessible title.</summary>
    internal const string AccTitle = "ACC_TITLE";

    /// <summary>A title.</summary>
    internal const string Title = "TITLE";

    /// <summary>An integer (a branch order).</summary>
    internal const string Int = "INT";

    /// <summary>A quoted string.</summary>
    internal const string StringToken = "STRING";

    /// <summary>A line break.</summary>
    internal const string Newline = "NEWLINE";

    /// <summary>A branch name written without quotes.</summary>
    internal const string Reference = "REFERENCE";

    /// <summary>The end of the tokens (chevrotain's <c>EOF</c>).</summary>
    internal const string Eof = "EOF";

    /// <summary>
    ///     The keywords of the grammar in chevrotain's order (longest first, stably); only <c>gitGraph</c> has the suffix
    ///     of the mermaid token builder (followed by <c>%%</c> or by no character other than white space).
    /// </summary>
    private static readonly string[] Keywords =
    [
        CherryPickKeyword, GitGraphColonKeyword, HighlightKeyword, GitGraphKeyword, CheckoutKeyword, ReverseKeyword,
        ParentKeyword, CommitKeyword, NormalKeyword, BranchKeyword, OrderKeyword, SwitchKeyword, TypeKeyword,
        MergeKeyword, MsgKeyword, TagKeyword, IdKeyword, LeftToRight, TopToBottom, BottomToTop, Colon,
    ];

    /// <summary>
    ///     The terminal matchers in chevrotain's order, after the keywords; a null kind is a hidden token, which the parser
    ///     never sees, and the null matcher is the directive's (<see cref="MatchDirective"/>), which also reads the last
    ///     closing of the text. The grammar reaches only these terminals (<c>REFERENCE</c> last, as Langium orders the
    ///     terminals of the grammar after the hidden ones of <c>common.langium</c>).
    /// </summary>
    private static readonly (string? Kind, Func<string, int, int>? Match)[] Terminals =
    [
        (AccDescr, MatchAccDescr),
        (AccTitle, MatchAccTitle),
        (Title, MatchTitle),
        (Int, MatchInt),
        (StringToken, MatchString),
        (Newline, MatchNewline),

        // The hidden terminals: white space (skipped), YAML, directives and comments
        (null, (text, p) => SkipSpaces(text, p) is var e && e > p ? e : -1),
        (null, MatchYaml),
        (null, null),
        (null, MatchComment),

        (Reference, MatchReference),
    ];

    /// <summary>
    ///     Splits the parser text into the tokens the grammar reads, hidden tokens left out.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The tokens, ending with one <see cref="Eof"/> token.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, at the first character no token matches.</exception>
    private List<GitGraphToken> Tokenize(string text)
    {
        var tokens = new List<GitGraphToken>();
        var lastClose = LastDirectiveClose(text);
        var position = 0;
        while (position < text.Length)
        {
            if (MatchKeywordToken(text, position) is { } keyword)
            {
                tokens.Add(keyword);
                position = keyword.End;
                continue;
            }

            var matched = false;
            foreach (var (kind, match) in Terminals)
            {
                var end = match is null ? MatchDirective(text, position, lastClose) : match(text, position);
                if (end <= position)
                {
                    continue;
                }

                if (kind is not null)
                {
                    tokens.Add(new GitGraphToken(kind, position, end));
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
                    "write one statement per line: commit, branch <name>, checkout <name>, merge <name> or cherry-pick id: \"<id>\" with quoted values, and branch names of letters, digits, '_', '-', '.' and '/'");
            }
        }

        tokens.Add(new GitGraphToken(Eof, text.Length, text.Length));
        return tokens;
    }

    /// <summary>
    ///     Matches the first keyword at a position, which gives way to a <c>REFERENCE</c> that matches more characters
    ///     there (chevrotain's <c>LONGER_ALT</c>, which Langium gives every keyword that a <c>REFERENCE</c> can start).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The token, or null when no keyword matches.</returns>
    private static GitGraphToken? MatchKeywordToken(string text, int p)
    {
        foreach (var keyword in Keywords)
        {
            var end = -1;
            if (keyword == GitGraphKeyword)
            {
                end = MatchSuffixedKeyword(text, p, keyword);
            }
            else if (IsAt(text, p, keyword))
            {
                end = p + keyword.Length;
            }

            if (end < 0)
            {
                continue;
            }

            var reference = keyword.All(IsReferenceCharacter) ? MatchReference(text, p) : -1;
            return reference > end ? new GitGraphToken(Reference, p, reference) : new GitGraphToken(keyword, p, end);
        }

        return null;
    }

    /// <summary>
    ///     Gets whether a character can be in a <c>REFERENCE</c> (<c>[-./\w]</c>, with ASCII word characters).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it can.</returns>
    private static bool IsReferenceCharacter(char c) => IsWordCharacter(c) || c is '-' or '.' or '/';

    /// <summary>
    ///     Gets whether a character is an ASCII word character (<c>\w</c>).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsWordCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    /// <summary>
    ///     Matches <c>REFERENCE</c>: <c>\w([-\./\w]*[-\w])?</c>, the run given back to its last character that is not a dot
    ///     or a slash.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchReference(string text, int p)
    {
        if (p >= text.Length || !IsWordCharacter(text[p]))
        {
            return -1;
        }

        var end = p + 1;
        while (end < text.Length && IsReferenceCharacter(text[end]))
        {
            end++;
        }

        while (end > p + 1 && text[end - 1] is '.' or '/')
        {
            end--;
        }

        return end;
    }

    /// <summary>
    ///     Matches a keyword followed by <c>%%</c> or by no character other than white space (<c>(?:(?=%%)|(?!\S))</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchSuffixedKeyword(string text, int p, string keyword)
    {
        if (!IsAt(text, p, keyword))
        {
            return -1;
        }

        var end = p + keyword.Length;
        return end >= text.Length || ScanPatterns.IsJsWhitespace(text[end]) || IsAt(text, end, "%%") ? end : -1;
    }

    /// <summary>
    ///     Matches <c>INT</c>: <c>0|[1-9][0-9]*(?!\.)</c>, with the backtracking of the regular expression (a zero alone
    ///     matches whatever follows it; the digits of a longer number give back their last digit before a dot).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchInt(string text, int p)
    {
        if (p >= text.Length || !char.IsAsciiDigit(text[p]))
        {
            return -1;
        }

        if (text[p] == '0')
        {
            return p + 1;
        }

        // [1-9][0-9]* followed by no dot: only the whole run can be followed by a dot, so one digit is given back
        var end = p;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        if (!IsAt(text, end, "."))
        {
            return end;
        }

        return end - p >= 2 ? end - 1 : -1;
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
    /// <param name="lastClose">The last closing <c>}%%</c> of the text that can end a directive, or -1.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int MatchDirective(string text, int p, int lastClose)
    {
        // A directive that opens after the last closing never closes, so the text after it is not searched again for
        // each such opening
        var k = SkipSpaces(text, p);
        if (!IsAt(text, k, "%%{") || lastClose < k + 3)
        {
            return -1;
        }

        // The shortest body whose closing }%% is followed by a line break or by no character other than white space
        for (var close = text.IndexOf("}%%", k + 3, StringComparison.Ordinal); close >= 0; close = text.IndexOf("}%%", close + 1, StringComparison.Ordinal))
        {
            if (ClosesDirective(text, close) is var end && end >= 0)
            {
                return end;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Finds the last closing <c>}%%</c> of a text that can end a directive, in one pass.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The position of the closing, or -1.</returns>
    private static int LastDirectiveClose(string text)
    {
        var last = -1;
        for (var close = text.IndexOf("}%%", StringComparison.Ordinal); close >= 0; close = text.IndexOf("}%%", close + 1, StringComparison.Ordinal))
        {
            last = ClosesDirective(text, close) >= 0 ? close : last;
        }

        return last;
    }

    /// <summary>
    ///     Matches the end of a directive at a <c>}%%</c>: <c>}%%(?:\r?\n|(?!\S))</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="close">The position of the <c>}%%</c>.</param>
    /// <returns>The end of the match, or -1.</returns>
    private static int ClosesDirective(string text, int close)
    {
        var after = close + 3;
        var line = MatchNewline(text, after);
        if (line >= 0)
        {
            return line;
        }

        return after >= text.Length || ScanPatterns.IsJsWhitespace(text[after]) ? after : -1;
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
    ///     Gets whether a text continues with a literal at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="p">The position.</param>
    /// <param name="literal">The literal.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsAt(string text, int p, string literal) =>
        p >= 0 && p + literal.Length <= text.Length && string.CompareOrdinal(text, p, literal, 0, literal.Length) == 0;
}
