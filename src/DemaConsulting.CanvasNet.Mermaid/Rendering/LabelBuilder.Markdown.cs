// Portions derived from mermaid (MIT) and marked (MIT); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     Markdown and plain strings: Mermaid's <c>handle-markdown-text.ts</c> (<c>markdownToHTML</c>,
///     <c>markdownToLines</c>, <c>nonMarkdownToHTML</c>, <c>nonMarkdownToLines</c>) over the part of the <c>marked</c>
///     lexer those functions use: paragraphs, <c>**strong**</c>/<c>__strong__</c>, <c>*em*</c>/<c>_em_</c> (with
///     CommonMark's delimiter rules), backslash escapes and inline HTML; every other block (headings, lists, quotes, code,
///     rules, HTML blocks) is kept as written, as Mermaid does with what it does not support.
/// </content>
internal sealed partial class LabelBuilder
{
    /// <summary>
    ///     The style of a word of an SVG label (Mermaid's <c>MarkdownWordType</c>).
    /// </summary>
    internal enum WordType
    {
        /// <summary>Plain.</summary>
        Normal,

        /// <summary>Bold (<c>strong</c>).</summary>
        Strong,

        /// <summary>Italic (<c>em</c>).</summary>
        Em,
    }

    /// <summary>
    ///     The kind of a token of the markdown lexer.
    /// </summary>
    private enum TokenKind
    {
        /// <summary>A paragraph (block) with inline children.</summary>
        Paragraph,

        /// <summary>Blank lines (block).</summary>
        Space,

        /// <summary>An HTML block or inline HTML.</summary>
        Html,

        /// <summary>A block or inline token that Mermaid does not support; its raw text is used.</summary>
        Other,

        /// <summary>Inline text.</summary>
        Text,

        /// <summary>Inline strong emphasis with children.</summary>
        Strong,

        /// <summary>Inline emphasis with children.</summary>
        Em,

        /// <summary>An inline backslash escape.</summary>
        Escape,
    }

    /// <summary>
    ///     Converts a markdown string to the HTML of an HTML label (Mermaid's <c>markdownToHTML</c>).
    /// </summary>
    /// <param name="markdown">The markdown.</param>
    /// <param name="autoWrap">Whether text may wrap at spaces (<c>markdownAutoWrap</c>); when not, spaces become no-break spaces.</param>
    /// <returns>The HTML.</returns>
    internal static string MarkdownToHtml(string markdown, bool autoWrap)
    {
        var builder = new StringBuilder();
        foreach (var token in LexBlocks(markdown))
        {
            AppendHtml(builder, token, autoWrap);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Converts a markdown string to the lines of words of an SVG label (Mermaid's <c>markdownToLines</c>).
    /// </summary>
    /// <param name="markdown">The markdown.</param>
    /// <returns>The lines.</returns>
    internal static List<List<(string Content, WordType Type)>> MarkdownToLines(string markdown)
    {
        // preprocessMarkdown: <br/> to new lines, several new lines to one, then dedent
        var text = markdown.Replace("<br/>", "\n", StringComparison.Ordinal);
        text = CollapseNewLines(text);
        text = Dedent(text);

        var lines = new List<List<(string, WordType)>> { new() };
        foreach (var token in LexBlocks(text))
        {
            switch (token.Kind)
            {
                case TokenKind.Paragraph:
                    foreach (var child in token.Children)
                    {
                        AppendLineWords(lines, child, WordType.Normal);
                    }

                    break;
                case TokenKind.Html:
                    lines[^1].Add((token.Text, WordType.Normal));
                    break;
                default:
                    lines[^1].Add((token.Raw, WordType.Normal));
                    break;
            }
        }

        return lines;
    }

    /// <summary>
    ///     Converts a plain string to the HTML of an HTML label (Mermaid's <c>nonMarkdownToHTML</c>): a paragraph whose
    ///     new lines (and <c>\n</c> sequences) are line breaks.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The HTML; empty for an empty text.</returns>
    internal static string NonMarkdownToHtml(string text)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        return "<p>" + text.Replace("\\n", "<br />", StringComparison.Ordinal).Replace("\n", "<br />", StringComparison.Ordinal) + "</p>";
    }

    /// <summary>
    ///     Converts a plain string to the lines of words of an SVG label (Mermaid's <c>nonMarkdownToLines</c>): lines at
    ///     new lines, <c>\n</c> and <c>&lt;br&gt;</c>, words at white space, tags kept as words of their own.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines.</returns>
    internal static List<List<(string Content, WordType Type)>> NonMarkdownToLines(string text)
    {
        var lines = new List<List<(string, WordType)>>();
        foreach (var line in SplitPlainLines(text))
        {
            var words = new List<(string, WordType)>();
            var trimmed = Preprocessor.TrimJs(line);
            var i = 0;
            while (i < trimmed.Length)
            {
                var c = trimmed[i];
                if (c == '<')
                {
                    // A tag: up to the next '>', with at least one character in between
                    var end = trimmed.IndexOf('>', i + 1);
                    if (end > i + 1)
                    {
                        words.Add((trimmed[i..(end + 1)], WordType.Normal));
                        i = end + 1;
                        continue;
                    }

                    i++;
                    continue;
                }

                if (c == '>' || ScanPatterns.IsJsWhitespace(c))
                {
                    i++;
                    continue;
                }

                var start = i;
                while (i < trimmed.Length && trimmed[i] is not '<' and not '>' && !ScanPatterns.IsJsWhitespace(trimmed[i]))
                {
                    i++;
                }

                words.Add((trimmed[start..i], WordType.Normal));
            }

            lines.Add(words);
        }

        return lines;
    }

    /// <summary>
    ///     Splits a plain string at new lines, <c>\n</c> sequences and <c>&lt;br&gt;</c> tags (any case, with optional
    ///     spaces and slash), as <c>split(/\\n|\n|&lt;br\s*\/?&gt;/gi)</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The pieces.</returns>
    private static List<string> SplitPlainLines(string text)
    {
        var pieces = new List<string>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var length = 0;
            if (text[i] == '\n')
            {
                length = 1;
            }
            else if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == 'n')
            {
                length = 2;
            }
            else
            {
                length = MatchBreakTag(text, i);
            }

            if (length > 0)
            {
                pieces.Add(text[start..i]);
                i += length;
                start = i;
            }
            else
            {
                i++;
            }
        }

        pieces.Add(text[start..]);
        return pieces;
    }

    /// <summary>
    ///     Matches a <c>&lt;br&gt;</c> tag (<c>&lt;br\s*\/?&gt;</c>, any case) at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The position.</param>
    /// <returns>The length of the tag, or zero.</returns>
    internal static int MatchBreakTag(string text, int i)
    {
        if (i + 3 >= text.Length || text[i] != '<' || char.ToLowerInvariant(text[i + 1]) != 'b' || char.ToLowerInvariant(text[i + 2]) != 'r')
        {
            return 0;
        }

        var j = i + 3;
        while (j < text.Length && ScanPatterns.IsJsWhitespace(text[j]))
        {
            j++;
        }

        if (j < text.Length && text[j] == '/')
        {
            j++;
        }

        return j < text.Length && text[j] == '>' ? j + 1 - i : 0;
    }

    /// <summary>
    ///     Appends the HTML of a token (the <c>output</c> function of <c>markdownToHTML</c>).
    /// </summary>
    /// <param name="builder">The HTML.</param>
    /// <param name="token">The token.</param>
    /// <param name="autoWrap">Whether text may wrap at spaces.</param>
    private static void AppendHtml(StringBuilder builder, MarkdownToken token, bool autoWrap)
    {
        switch (token.Kind)
        {
            case TokenKind.Text:
                var text = ReplaceNewLineRuns(token.Text, "<br/>");
                builder.Append(autoWrap ? text : text.Replace(" ", "&nbsp;", StringComparison.Ordinal));
                break;
            case TokenKind.Strong:
                builder.Append("<strong>");
                token.Children.ForEach(child => AppendHtml(builder, child, autoWrap));
                builder.Append("</strong>");
                break;
            case TokenKind.Em:
                builder.Append("<em>");
                token.Children.ForEach(child => AppendHtml(builder, child, autoWrap));
                builder.Append("</em>");
                break;
            case TokenKind.Paragraph:
                builder.Append("<p>");
                token.Children.ForEach(child => AppendHtml(builder, child, autoWrap));
                builder.Append("</p>");
                break;
            case TokenKind.Space:
                break;
            case TokenKind.Html:
            case TokenKind.Escape:
                builder.Append(token.Text);
                break;
            default:
                builder.Append(token.Raw);
                break;
        }
    }

    /// <summary>
    ///     Adds the words of an inline token to the lines (the <c>processNode</c> function of <c>markdownToLines</c>).
    /// </summary>
    /// <param name="lines">The lines.</param>
    /// <param name="token">The token.</param>
    /// <param name="parentType">The style of the enclosing emphasis.</param>
    private static void AppendLineWords(List<List<(string, WordType)>> lines, MarkdownToken token, WordType parentType)
    {
        switch (token.Kind)
        {
            case TokenKind.Text:
                var textLines = token.Text.Split('\n');
                for (var index = 0; index < textLines.Length; index++)
                {
                    if (index != 0)
                    {
                        lines.Add([]);
                    }

                    foreach (var word in textLines[index].Split(' '))
                    {
                        var content = word.Replace("&#39;", "'", StringComparison.Ordinal);
                        if (content.Length > 0)
                        {
                            lines[^1].Add((content, parentType));
                        }
                    }
                }

                break;
            case TokenKind.Strong:
                token.Children.ForEach(child => AppendLineWords(lines, child, WordType.Strong));
                break;
            case TokenKind.Em:
                token.Children.ForEach(child => AppendLineWords(lines, child, WordType.Em));
                break;
            case TokenKind.Html:
                lines[^1].Add((token.Text, WordType.Normal));
                break;
            default:
                // Escapes and the other inline tokens are not drawn by Mermaid's SVG labels
                break;
        }
    }

    /// <summary>
    ///     Replaces every new line and the spaces after it (<c>/\n */g</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>The text.</returns>
    private static string ReplaceNewLineRuns(string text, string replacement)
    {
        if (!text.Contains('\n', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 8);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                builder.Append(replacement);
                i++;
                while (i < text.Length && text[i] == ' ')
                {
                    i++;
                }

                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Replaces runs of two or more new lines by one (<c>/\n{2,}/g</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    private static string CollapseNewLines(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == '\n' && builder.Length > 0 && builder[^1] == '\n')
            {
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Removes the common indentation of the lines, as <c>ts-dedent</c> does: the trailing white-space line is removed,
    ///     the smallest indentation of the lines after the first is removed from them, the text is trimmed and
    ///     <c>\n</c> sequences become new lines.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    internal static string Dedent(string text)
    {
        // 1. A last line of only spaces and tabs goes, with its line break
        var last = text.LastIndexOf('\n');
        if (last >= 0 && text[(last + 1)..].All(c => c is ' ' or '\t'))
        {
            var cut = last > 0 && text[last - 1] == '\r' ? last - 1 : last;
            text = text[..cut];
        }

        // 2. The smallest indentation of the lines that follow a line break and are not empty
        var lines = text.Split('\n');
        var indent = int.MaxValue;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || (line[0] is not ' ' and not '\t' && char.IsWhiteSpace(line[0])))
            {
                continue;
            }

            var count = 0;
            while (count < line.Length && line[count] is ' ' or '\t')
            {
                count++;
            }

            // A line of only white space other than spaces and tabs does not count
            if (count == 0 && char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            indent = Math.Min(indent, count);
        }

        // 3. The indentation goes from every line after a line break
        if (indent is > 0 and < int.MaxValue)
        {
            for (var i = 1; i < lines.Length; i++)
            {
                var count = 0;
                while (count < lines[i].Length && count < indent && lines[i][count] is ' ' or '\t')
                {
                    count++;
                }

                if (count == indent)
                {
                    lines[i] = lines[i][indent..];
                }
            }

            text = string.Join('\n', lines);
        }

        // 4. and 5. Trim, then escaped new lines become new lines
        return Preprocessor.TrimJs(text).Replace("\\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>
    ///     A token of the markdown lexer.
    /// </summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Raw">The text as written.</param>
    /// <param name="Text">The text of the token (the content of text, HTML and escape tokens).</param>
    /// <param name="Children">The inline children of a paragraph, strong or em token.</param>
    private sealed record MarkdownToken(TokenKind Kind, string Raw, string Text, List<MarkdownToken> Children)
    {
        /// <summary>Creates a token without children.</summary>
        /// <param name="kind">The kind.</param>
        /// <param name="raw">The raw text.</param>
        /// <param name="text">The text.</param>
        /// <returns>The token.</returns>
        public static MarkdownToken Leaf(TokenKind kind, string raw, string text) => new(kind, raw, text, []);
    }
}
