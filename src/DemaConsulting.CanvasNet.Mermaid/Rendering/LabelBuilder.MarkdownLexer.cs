// Portions derived from marked (MIT); see third-party/README.md
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The markdown lexer: the block and inline tokens of <c>marked</c> that Mermaid's markdown labels use.
/// </content>
internal sealed partial class LabelBuilder
{
    /// <summary>
    ///     The names of the HTML elements that start an HTML block (CommonMark's block conditions 6).
    /// </summary>
    private static readonly HashSet<string> HtmlBlockNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "base", "basefont", "blockquote", "body", "caption", "center", "col", "colgroup",
        "dd", "details", "dialog", "dir", "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "frame",
        "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hr", "html", "iframe", "legend", "li", "link",
        "main", "menu", "menuitem", "meta", "nav", "noframes", "ol", "optgroup", "option", "p", "param", "search",
        "section", "summary", "table", "tbody", "td", "tfoot", "th", "thead", "title", "tr", "track", "ul",
    };

    /// <summary>
    ///     Splits markdown into block tokens: paragraphs (with their inline tokens), blank lines, HTML blocks and the
    ///     blocks Mermaid does not support (kept as written).
    /// </summary>
    /// <param name="markdown">The markdown.</param>
    /// <returns>The block tokens.</returns>
    private static List<MarkdownToken> LexBlocks(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var tokens = new List<MarkdownToken>();
        var i = 0;
        while (i < lines.Length)
        {
            var start = i;
            var line = lines[i];
            TokenKind kind;
            if (IsBlank(line))
            {
                while (i < lines.Length && IsBlank(lines[i]))
                {
                    i++;
                }

                // A blank line at the very end is the end of the text, not a token
                if (i == lines.Length && start == lines.Length - 1)
                {
                    break;
                }

                kind = TokenKind.Space;
            }
            else if (FenceOf(line) is { } fence)
            {
                i++;
                while (i < lines.Length && !lines[i].TrimStart(' ').StartsWith(fence, StringComparison.Ordinal))
                {
                    i++;
                }

                i = Math.Min(i + 1, lines.Length);
                kind = TokenKind.Other;
            }
            else if (IsIndentedCode(line))
            {
                while (i < lines.Length && (IsIndentedCode(lines[i]) || IsBlank(lines[i])))
                {
                    i++;
                }

                kind = TokenKind.Other;
            }
            else if (IsHeading(line) || IsRule(line))
            {
                i++;
                kind = TokenKind.Other;
            }
            else if (IsQuote(line) || IsListItem(line, interrupting: false) || IsHtmlBlockStart(line))
            {
                kind = IsHtmlBlockStart(line) ? TokenKind.Html : TokenKind.Other;
                while (i < lines.Length && !IsBlank(lines[i]))
                {
                    i++;
                }
            }
            else
            {
                // A paragraph runs to a blank line or to a block that may interrupt it
                i++;
                while (i < lines.Length && !IsBlank(lines[i]) && !InterruptsParagraph(lines[i]))
                {
                    i++;
                }

                kind = TokenKind.Paragraph;
            }

            var raw = string.Join('\n', lines[start..i]) + (i < lines.Length ? "\n" : string.Empty);
            if (kind == TokenKind.Paragraph)
            {
                var text = string.Join('\n', lines[start..i]);
                tokens.Add(new MarkdownToken(kind, raw, text, LexInline(text)));
            }
            else
            {
                tokens.Add(MarkdownToken.Leaf(kind, raw, raw.TrimEnd('\n')));
            }
        }

        return tokens;
    }

    /// <summary>Gets whether a line holds only spaces and tabs.</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it is blank.</returns>
    private static bool IsBlank(string line) => line.All(c => c is ' ' or '\t');

    /// <summary>Gets the number of leading spaces of a line.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The count.</returns>
    private static int Indent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    /// <summary>Gets the opening fence of a fenced code block (three or more backticks or tildes), or null.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The fence.</returns>
    private static string? FenceOf(string line)
    {
        var indent = Indent(line);
        if (indent > 3 || indent + 3 > line.Length || line[indent] is not '`' and not '~')
        {
            return null;
        }

        var c = line[indent];
        var end = indent;
        while (end < line.Length && line[end] == c)
        {
            end++;
        }

        return end - indent >= 3 ? new string(c, end - indent) : null;
    }

    /// <summary>Gets whether a line is indented code (four spaces or a tab).</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsIndentedCode(string line) => !IsBlank(line) && (Indent(line) >= 4 || line.StartsWith('\t'));

    /// <summary>Gets whether a line is an ATX heading (one to six <c>#</c> followed by a space or the end).</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsHeading(string line)
    {
        var indent = Indent(line);
        if (indent > 3)
        {
            return false;
        }

        var end = indent;
        while (end < line.Length && line[end] == '#')
        {
            end++;
        }

        return end - indent is >= 1 and <= 6 && (end == line.Length || line[end] is ' ' or '\t');
    }

    /// <summary>Gets whether a line is a thematic break (three or more <c>-</c>, <c>*</c> or <c>_</c>).</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsRule(string line)
    {
        var indent = Indent(line);
        if (indent > 3 || indent >= line.Length || line[indent] is not '-' and not '*' and not '_')
        {
            return false;
        }

        var c = line[indent];
        var count = 0;
        foreach (var ch in line[indent..])
        {
            if (ch == c)
            {
                count++;
            }
            else if (ch is not ' ' and not '\t')
            {
                return false;
            }
        }

        return count >= 3;
    }

    /// <summary>Gets whether a line starts a block quote.</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsQuote(string line)
    {
        var indent = Indent(line);
        return indent <= 3 && indent < line.Length && line[indent] == '>';
    }

    /// <summary>Gets whether a line starts a list item.</summary>
    /// <param name="line">The line.</param>
    /// <param name="interrupting">Whether the item would interrupt a paragraph (then it must not be empty, and an ordered one must start at 1).</param>
    /// <returns>Whether it does.</returns>
    private static bool IsListItem(string line, bool interrupting)
    {
        var indent = Indent(line);
        if (indent > 3 || indent >= line.Length)
        {
            return false;
        }

        var end = indent;
        if (line[end] is '-' or '*' or '+')
        {
            end++;
        }
        else
        {
            while (end < line.Length && end - indent < 9 && char.IsAsciiDigit(line[end]))
            {
                end++;
            }

            if (end == indent || end >= line.Length || line[end] is not '.' and not ')')
            {
                return false;
            }

            if (interrupting && line[indent..end] != "1")
            {
                return false;
            }

            end++;
        }

        if (end == line.Length)
        {
            return !interrupting;
        }

        return line[end] is ' ' or '\t' && (!interrupting || !IsBlank(line[end..]));
    }

    /// <summary>Gets whether a line starts an HTML block (a block-level element or a comment).</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it does.</returns>
    private static bool IsHtmlBlockStart(string line)
    {
        var indent = Indent(line);
        if (indent > 3 || indent >= line.Length || line[indent] != '<')
        {
            return false;
        }

        var rest = line[(indent + 1)..];
        if (rest.StartsWith("!--", StringComparison.Ordinal))
        {
            return true;
        }

        if (rest.StartsWith('/'))
        {
            rest = rest[1..];
        }

        var nameEnd = 0;
        while (nameEnd < rest.Length && char.IsAsciiLetterOrDigit(rest[nameEnd]))
        {
            nameEnd++;
        }

        return nameEnd > 0
            && HtmlBlockNames.Contains(rest[..nameEnd])
            && (nameEnd == rest.Length || rest[nameEnd] is ' ' or '\t' or '>' or '/');
    }

    /// <summary>Gets whether a line ends a paragraph that it follows.</summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it does.</returns>
    private static bool InterruptsParagraph(string line) =>
        FenceOf(line) is not null || IsHeading(line) || IsRule(line) || IsQuote(line) || IsListItem(line, interrupting: true) || IsHtmlBlockStart(line);

    /// <summary>
    ///     Splits the text of a paragraph into inline tokens: text, backslash escapes, inline HTML, code spans (kept as
    ///     written) and emphasis.
    /// </summary>
    /// <param name="text">The paragraph text.</param>
    /// <returns>The inline tokens.</returns>
    private static List<MarkdownToken> LexInline(string text)
    {
        var nodes = new List<InlineNode>();
        var pending = new StringBuilder();
        void FlushText()
        {
            if (pending.Length > 0)
            {
                nodes.Add(InlineNode.Of(MarkdownToken.Leaf(TokenKind.Text, pending.ToString(), pending.ToString())));
                pending.Clear();
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length && IsAsciiPunctuation(text[i + 1]))
            {
                FlushText();
                nodes.Add(InlineNode.Of(MarkdownToken.Leaf(TokenKind.Escape, text.Substring(i, 2), text[i + 1].ToString())));
                i += 2;
                continue;
            }

            if (c == '<' && MatchInlineHtml(text, i) is > 0 and var htmlLength)
            {
                FlushText();
                var tag = text.Substring(i, htmlLength);
                nodes.Add(InlineNode.Of(MarkdownToken.Leaf(TokenKind.Html, tag, tag)));
                i += htmlLength;
                continue;
            }

            if (c == '`' && MatchCodeSpan(text, i) is > 0 and var codeLength)
            {
                FlushText();
                var code = text.Substring(i, codeLength);
                nodes.Add(InlineNode.Of(MarkdownToken.Leaf(TokenKind.Other, code, code)));
                i += codeLength;
                continue;
            }

            if (c is '*' or '_')
            {
                FlushText();
                var end = i;
                while (end < text.Length && text[end] == c)
                {
                    end++;
                }

                nodes.Add(InlineNode.Delimiter(c, end - i, i > 0 ? text[i - 1] : '\n', end < text.Length ? text[end] : '\n'));
                i = end;
                continue;
            }

            pending.Append(c);
            i++;
        }

        FlushText();
        ProcessEmphasis(nodes);
        return ToTokens(nodes);
    }

    /// <summary>Gets whether a character is ASCII punctuation (what a backslash escapes).</summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsAsciiPunctuation(char c) => c is (>= '!' and <= '/') or (>= ':' and <= '@') or (>= '[' and <= '`') or (>= '{' and <= '~');

    /// <summary>
    ///     Matches inline HTML at a <c>&lt;</c>: an open or closing tag, or a comment.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The position of the <c>&lt;</c>.</param>
    /// <returns>The length, or zero.</returns>
    private static int MatchInlineHtml(string text, int i)
    {
        if (string.CompareOrdinal(text, i, "<!--", 0, 4) == 0)
        {
            var close = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
            return close < 0 ? 0 : close + 3 - i;
        }

        var j = i + 1;
        if (j < text.Length && text[j] == '/')
        {
            j++;
        }

        if (j >= text.Length || !char.IsAsciiLetter(text[j]))
        {
            return 0;
        }

        // The tag runs to the next '>' outside quotes
        char? quote = null;
        for (var k = j; k < text.Length; k++)
        {
            var c = text[k];
            if (quote is { } q)
            {
                if (c == q)
                {
                    quote = null;
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '<')
            {
                return 0;
            }
            else if (c == '>')
            {
                return k + 1 - i;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Matches a code span at a backtick: a run of backticks and the next run of the same length.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The position.</param>
    /// <returns>The length, or zero when the run is not closed.</returns>
    private static int MatchCodeSpan(string text, int i)
    {
        var end = i;
        while (end < text.Length && text[end] == '`')
        {
            end++;
        }

        var length = end - i;
        var k = end;
        while (k < text.Length)
        {
            if (text[k] != '`')
            {
                k++;
                continue;
            }

            var runEnd = k;
            while (runEnd < text.Length && text[runEnd] == '`')
            {
                runEnd++;
            }

            if (runEnd - k == length)
            {
                return runEnd - i;
            }

            k = runEnd;
        }

        return 0;
    }

    /// <summary>
    ///     Resolves the emphasis delimiters with CommonMark's algorithm (<c>process_emphasis</c>): each closer is matched
    ///     with the nearest possible opener (the rule of three applies), two delimiters make strong emphasis and one makes
    ///     emphasis, and the delimiters that match nothing stay text.
    /// </summary>
    /// <param name="nodes">The inline nodes, changed in place.</param>
    private static void ProcessEmphasis(List<InlineNode> nodes)
    {
        var index = 0;
        while (index < nodes.Count)
        {
            var closer = nodes[index];
            if (!closer.IsDelimiter || !closer.CanClose || closer.Count == 0)
            {
                index++;
                continue;
            }

            // The nearest opener of the same character that the rule of three allows
            var openerIndex = -1;
            for (var k = index - 1; k >= 0; k--)
            {
                var opener = nodes[k];
                if (!opener.IsDelimiter || opener.Count == 0 || opener.Char != closer.Char || !opener.CanOpen)
                {
                    continue;
                }

                var oddMatch = (closer.CanOpen || opener.CanClose)
                    && closer.Original % 3 != 0
                    && (opener.Original + closer.Original) % 3 == 0;
                if (!oddMatch)
                {
                    openerIndex = k;
                    break;
                }
            }

            if (openerIndex < 0)
            {
                // A closer that cannot also open no longer takes part
                if (!closer.CanOpen)
                {
                    closer.CanClose = false;
                }

                index++;
                continue;
            }

            var match = nodes[openerIndex];
            var use = closer.Count >= 2 && match.Count >= 2 ? 2 : 1;
            match.Count -= use;
            closer.Count -= use;

            // The nodes between the two become the children of the emphasis
            var children = nodes.GetRange(openerIndex + 1, index - openerIndex - 1);
            nodes.RemoveRange(openerIndex + 1, index - openerIndex - 1);
            var emphasis = new MarkdownToken(use == 2 ? TokenKind.Strong : TokenKind.Em, string.Empty, string.Empty, ToTokens(children));
            nodes.Insert(openerIndex + 1, InlineNode.Of(emphasis));
            index = openerIndex + 2;

            // A used-up opener goes; the closer is looked at again while it has delimiters left
            if (match.Count == 0)
            {
                nodes.RemoveAt(openerIndex);
                index--;
            }

            if (closer.Count == 0)
            {
                nodes.RemoveAt(index);
            }
        }
    }

    /// <summary>
    ///     Converts inline nodes to tokens: the delimiters left over become text and adjacent text merges.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns>The tokens.</returns>
    private static List<MarkdownToken> ToTokens(List<InlineNode> nodes)
    {
        var tokens = new List<MarkdownToken>();
        var pending = new StringBuilder();
        foreach (var node in nodes)
        {
            if (node.IsDelimiter)
            {
                pending.Append(node.Char, node.Count);
            }
            else if (node.Token!.Kind == TokenKind.Text)
            {
                pending.Append(node.Token.Text);
            }
            else
            {
                if (pending.Length > 0)
                {
                    tokens.Add(MarkdownToken.Leaf(TokenKind.Text, pending.ToString(), pending.ToString()));
                    pending.Clear();
                }

                tokens.Add(node.Token);
            }
        }

        if (pending.Length > 0)
        {
            tokens.Add(MarkdownToken.Leaf(TokenKind.Text, pending.ToString(), pending.ToString()));
        }

        return tokens;
    }

    /// <summary>
    ///     A node of the inline lexer: a token, or a run of emphasis delimiters.
    /// </summary>
    private sealed class InlineNode
    {
        /// <summary>Gets the token, or null for a delimiter run.</summary>
        public MarkdownToken? Token { get; private init; }

        /// <summary>Gets the delimiter character (<c>*</c> or <c>_</c>).</summary>
        public char Char { get; private init; }

        /// <summary>Gets or sets the number of delimiters left.</summary>
        public int Count { get; set; }

        /// <summary>Gets the original number of delimiters (for the rule of three).</summary>
        public int Original { get; private init; }

        /// <summary>Gets a value indicating whether the run may open emphasis.</summary>
        public bool CanOpen { get; private init; }

        /// <summary>Gets or sets a value indicating whether the run may close emphasis.</summary>
        public bool CanClose { get; set; }

        /// <summary>Gets a value indicating whether the node is a delimiter run.</summary>
        public bool IsDelimiter => Token is null;

        /// <summary>Creates the node of a token.</summary>
        /// <param name="token">The token.</param>
        /// <returns>The node.</returns>
        public static InlineNode Of(MarkdownToken token) => new() { Token = token };

        /// <summary>
        ///     Creates the node of a delimiter run with CommonMark's flanking rules.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <param name="count">The length of the run.</param>
        /// <param name="before">The character before the run (a new line at the start).</param>
        /// <param name="after">The character after the run (a new line at the end).</param>
        /// <returns>The node.</returns>
        public static InlineNode Delimiter(char c, int count, char before, char after)
        {
            var afterSpace = char.IsWhiteSpace(after);
            var beforeSpace = char.IsWhiteSpace(before);
            var afterPunctuation = IsMarkdownPunctuation(after);
            var beforePunctuation = IsMarkdownPunctuation(before);
            var left = !afterSpace && (!afterPunctuation || beforeSpace || beforePunctuation);
            var right = !beforeSpace && (!beforePunctuation || afterSpace || afterPunctuation);
            var canOpen = c == '*' ? left : left && (!right || beforePunctuation);
            var canClose = c == '*' ? right : right && (!left || afterPunctuation);
            return new InlineNode { Char = c, Count = count, Original = count, CanOpen = canOpen, CanClose = canClose };
        }

        /// <summary>Gets whether a character is punctuation for the flanking rules (Unicode punctuation and symbols).</summary>
        /// <param name="c">The character.</param>
        /// <returns>Whether it is.</returns>
        private static bool IsMarkdownPunctuation(char c) =>
            char.GetUnicodeCategory(c) is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation
                or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation
                or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation or UnicodeCategory.MathSymbol
                or UnicodeCategory.CurrencySymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol;
    }
}
