using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     One value of the front matter of a help card: a scalar, a list, or a map of scalars.
/// </summary>
/// <param name="Line">The 1-based line of the card file the value starts on, for messages.</param>
/// <param name="Scalar">The text of a scalar, or <see langword="null"/> for a list or a map.</param>
/// <param name="Items">The items of a list, or <see langword="null"/> for a scalar or a map.</param>
/// <param name="Map">The entries of a map, in order, or <see langword="null"/> for a scalar or a list.</param>
internal sealed record FrontMatterValue(
    int Line,
    string? Scalar,
    IReadOnlyList<FrontMatterValue>? Items,
    IReadOnlyList<KeyValuePair<string, FrontMatterValue>>? Map);

/// <summary>
///     A help card file split into its front matter and its Markdown body.
/// </summary>
/// <param name="Properties">The top-level keys of the front matter and their values, in order.</param>
/// <param name="Body">
///     The body: the text after the closing <c>---</c> line, with LF line endings, without one leading blank line, and
///     without trailing line feeds.
/// </param>
internal sealed record FrontMatterDocument(
    IReadOnlyList<KeyValuePair<string, FrontMatterValue>> Properties,
    string Body);

/// <summary>
///     The front-matter parser of the help cards.
/// </summary>
/// <remarks>
///     <para>
///         A card starts with YAML front matter between two <c>---</c> lines. The parser reads only the small subset
///         of YAML the cards use, and it reports anything else as an error instead of guessing:
///     </para>
///     <list type="bullet">
///         <item><c>key: value</c> at column 0, where a key is lower case letters and dashes;</item>
///         <item><c>key:</c> alone, followed by a block list of items <c>  - value</c> (two spaces);</item>
///         <item>
///             a list item that is a map: <c>  - key: value</c> followed by <c>    key: value</c> lines (four spaces);
///         </item>
///         <item>
///             plain, single-quoted (<c>''</c> escapes a quote), and double-quoted (<c>\\</c> and <c>\"</c> only)
///             scalars;
///         </item>
///         <item>blank lines and full-line <c>#</c> comments.</item>
///     </list>
///     <para>
///         Tabs, flow collections, block scalars, anchors, aliases, tags, trailing comments, deeper nesting, and
///         duplicate keys are errors. Every error is an <see cref="InvalidOperationException"/> whose message names the
///         card file and the line, so a broken card fails the unit tests with a message that says where to look.
///     </para>
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The line that opens and closes the front matter.
    /// </summary>
    private const string FrontMatterFence = "---";

    /// <summary>
    ///     Matches a <c>key: value</c> or <c>key:</c> entry: group 1 is the key, group 2 the value (possibly empty).
    /// </summary>
    private static readonly Regex KeyValuePattern = new(
        @"^([a-z][a-z-]*):(?:$| (.*)$)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Splits a help card file into its front matter and its body.
    /// </summary>
    /// <param name="text">The text of the card file.</param>
    /// <param name="fileName">The name of the card file, for messages (for example <c>mermaid/gantt.md</c>).</param>
    /// <returns>The front matter and the body.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the front matter is missing or uses YAML outside the supported subset; the message names the
    ///     file and the line.
    /// </exception>
    internal static FrontMatterDocument ParseFrontMatter(string text, string fileName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fileName);

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines[0] != FrontMatterFence)
        {
            throw FrontMatterError(fileName, 1, "the card must start with a '---' line that opens the front matter");
        }

        var close = Array.IndexOf(lines, FrontMatterFence, 1);
        if (close < 0)
        {
            throw FrontMatterError(fileName, lines.Length, "the front matter has no closing '---' line");
        }

        var properties = ParseProperties(lines, close, fileName);
        return new FrontMatterDocument(properties, ExtractBody(lines, close + 1));
    }

    /// <summary>
    ///     Creates the exception for an error of a card file.
    /// </summary>
    /// <param name="fileName">The name of the card file.</param>
    /// <param name="line">The 1-based line.</param>
    /// <param name="reason">What is wrong, without a final period.</param>
    /// <returns>The exception.</returns>
    internal static InvalidOperationException FrontMatterError(string fileName, int line, string reason)
    {
        return new InvalidOperationException($"Help card '{fileName}', line {line}: {reason}.");
    }

    /// <summary>
    ///     Parses the front matter lines between the two fences.
    /// </summary>
    /// <param name="lines">Every line of the card file.</param>
    /// <param name="close">The index of the closing fence.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <returns>The top-level keys and their values, in order.</returns>
    /// <exception cref="InvalidOperationException">Thrown for anything outside the supported subset.</exception>
    private static List<KeyValuePair<string, FrontMatterValue>> ParseProperties(string[] lines, int close, string fileName)
    {
        var properties = new List<KeyValuePair<string, FrontMatterValue>>();
        string? listKey = null;
        var listKeyLine = 0;
        List<FrontMatterValue>? items = null;
        List<KeyValuePair<string, FrontMatterValue>>? map = null;
        var mapLine = 0;

        // Ends the map item in progress, if any
        void EndMap()
        {
            if (map != null)
            {
                items!.Add(new FrontMatterValue(mapLine, null, null, map));
                map = null;
            }
        }

        // Ends the list in progress, if any; a key that opens a list must have items
        void EndList()
        {
            EndMap();
            if (listKey == null)
            {
                return;
            }

            if (items!.Count == 0)
            {
                throw FrontMatterError(fileName, listKeyLine, $"the key '{listKey}' has neither a value nor list items");
            }

            properties.Add(new(listKey, new FrontMatterValue(listKeyLine, null, items, null)));
            listKey = null;
            items = null;
        }

        for (var index = 1; index < close; index++)
        {
            var line = lines[index];
            var number = index + 1;
            if (line.Contains('\t', StringComparison.Ordinal))
            {
                throw FrontMatterError(fileName, number, "a tab is not allowed; indent with spaces");
            }

            var trimmed = line.TrimStart(' ');
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var indent = line.Length - trimmed.Length;
            if (indent == 0)
            {
                // A new top-level key ends the list in progress
                EndList();
                var (key, value) = SplitKeyValue(trimmed, fileName, number);
                if (properties.Any(p => p.Key == key))
                {
                    throw FrontMatterError(fileName, number, $"the key '{key}' is repeated");
                }

                if (value.Length == 0)
                {
                    (listKey, listKeyLine, items) = (key, number, []);
                    continue;
                }

                properties.Add(new(key, new FrontMatterValue(number, ParseScalar(value, fileName, number), null, null)));
                continue;
            }

            if (indent == 2 && trimmed.StartsWith("- ", StringComparison.Ordinal) && listKey != null)
            {
                // A list item: a scalar, or the first entry of a map
                EndMap();
                var content = trimmed[2..].Trim(' ');
                if (KeyValuePattern.IsMatch(content))
                {
                    var (key, value) = SplitKeyValue(content, fileName, number);
                    (map, mapLine) = ([new(key, ParseMapValue(value, fileName, number))], number);
                    continue;
                }

                items!.Add(new FrontMatterValue(number, ParseScalar(content, fileName, number), null, null));
                continue;
            }

            if (indent == 4 && map != null)
            {
                // A further entry of the map item in progress
                var (key, value) = SplitKeyValue(trimmed, fileName, number);
                if (map.Any(p => p.Key == key))
                {
                    throw FrontMatterError(fileName, number, $"the key '{key}' is repeated");
                }

                map.Add(new(key, ParseMapValue(value, fileName, number)));
                continue;
            }

            throw FrontMatterError(
                fileName,
                number,
                "bad indentation: use 'key: value' at column 0, '  - item' for a list item, and '    key: value' for the "
                + "further keys of a list item; nothing nests deeper");
        }

        EndList();
        return properties;
    }

    /// <summary>
    ///     Splits a <c>key: value</c> entry.
    /// </summary>
    /// <param name="entry">The entry, without indentation.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="line">The 1-based line, for messages.</param>
    /// <returns>The key and the raw value (empty when the entry opens a list).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entry is not a key and a value.</exception>
    private static (string Key, string Value) SplitKeyValue(string entry, string fileName, int line)
    {
        var match = KeyValuePattern.Match(entry);
        if (!match.Success)
        {
            throw FrontMatterError(
                fileName,
                line,
                "expected 'key: value', where a key is lower case letters and dashes and a space follows the colon");
        }

        return (match.Groups[1].Value, match.Groups[2].Value.TrimEnd(' '));
    }

    /// <summary>
    ///     Parses the value of an entry of a map item, which must be a scalar.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="line">The 1-based line, for messages.</param>
    /// <returns>The scalar value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is empty or not a supported scalar.</exception>
    private static FrontMatterValue ParseMapValue(string value, string fileName, int line)
    {
        if (value.Length == 0)
        {
            throw FrontMatterError(fileName, line, "a key of a list item needs a value; nothing nests deeper");
        }

        return new FrontMatterValue(line, ParseScalar(value, fileName, line), null, null);
    }

    /// <summary>
    ///     Parses a scalar: plain, single-quoted, or double-quoted.
    /// </summary>
    /// <param name="value">The raw value, without leading spaces and without trailing spaces.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="line">The 1-based line, for messages.</param>
    /// <returns>The text of the scalar.</returns>
    /// <exception cref="InvalidOperationException">Thrown for a construct outside the supported subset.</exception>
    private static string ParseScalar(string value, string fileName, int line)
    {
        if (value.Length == 0)
        {
            throw FrontMatterError(fileName, line, "the value is empty");
        }

        switch (value[0])
        {
            case '\'':
                return ParseQuoted(value, '\'', fileName, line);
            case '"':
                return ParseQuoted(value, '"', fileName, line);
            case '[':
                throw FrontMatterError(fileName, line, "a flow sequence ('[') is not supported; write a block list");
            case '{':
                throw FrontMatterError(fileName, line, "a flow mapping ('{') is not supported; write a block list");
            case '|':
            case '>':
                throw FrontMatterError(fileName, line, "a block scalar ('|' or '>') is not supported; write one line");
            case '&':
                throw FrontMatterError(fileName, line, "an anchor ('&') is not supported");
            case '*':
                throw FrontMatterError(fileName, line, "an alias ('*') is not supported");
            case '!':
                throw FrontMatterError(fileName, line, "a tag ('!') is not supported");
            case '#':
                throw FrontMatterError(fileName, line, "a comment is not a value");
            case '%':
            case '@':
            case '`':
                throw FrontMatterError(fileName, line, $"a plain value cannot start with '{value[0]}'; quote it");
        }

        if (value.Contains(" #", StringComparison.Ordinal))
        {
            throw FrontMatterError(fileName, line, "a trailing comment is not supported; quote a value that holds ' #'");
        }

        if (value.Contains(": ", StringComparison.Ordinal) || value.EndsWith(':'))
        {
            throw FrontMatterError(fileName, line, "a plain value cannot hold ': '; quote it");
        }

        return value;
    }

    /// <summary>
    ///     Parses a quoted scalar.
    /// </summary>
    /// <param name="value">The raw value, starting with the quote.</param>
    /// <param name="quote">The quote character: <c>'</c> or <c>"</c>.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="line">The 1-based line, for messages.</param>
    /// <returns>The text between the quotes, unescaped.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the quote is not closed, when text follows the closing quote, or for an unsupported escape.
    /// </exception>
    private static string ParseQuoted(string value, char quote, string fileName, int line)
    {
        var builder = new StringBuilder();
        var i = 1;
        while (i < value.Length)
        {
            var c = value[i];
            var next = i + 1 < value.Length ? value[i + 1] : '\0';
            if (quote == '\'' && c == '\'')
            {
                // '' is a quote; a single ' closes the value
                if (next != '\'')
                {
                    return RequireEnd(value, i, builder, fileName, line);
                }

                builder.Append('\'');
                i += 2;
                continue;
            }

            if (quote == '"' && c == '\\')
            {
                // Only \\ and \" are escapes
                if (next is not ('\\' or '"'))
                {
                    throw FrontMatterError(fileName, line, "only the escapes '\\\\' and '\\\"' are supported in a double-quoted value");
                }

                builder.Append(next);
                i += 2;
                continue;
            }

            if (quote == '"' && c == '"')
            {
                return RequireEnd(value, i, builder, fileName, line);
            }

            builder.Append(c);
            i++;
        }

        throw FrontMatterError(fileName, line, "the quoted value is not closed on its line");
    }

    /// <summary>
    ///     Requires the closing quote to end the value.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <param name="closeIndex">The index of the closing quote.</param>
    /// <param name="builder">The unescaped text.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="line">The 1-based line, for messages.</param>
    /// <returns>The unescaped text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when text follows the closing quote.</exception>
    private static string RequireEnd(string value, int closeIndex, StringBuilder builder, string fileName, int line)
    {
        if (closeIndex != value.Length - 1)
        {
            throw FrontMatterError(fileName, line, "text follows the closing quote (a trailing comment is not supported)");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Extracts the body of a card: the lines after the front matter.
    /// </summary>
    /// <param name="lines">Every line of the card file.</param>
    /// <param name="start">The index of the first line after the closing fence.</param>
    /// <returns>The body, without one leading blank line and without trailing line feeds.</returns>
    private static string ExtractBody(string[] lines, int start)
    {
        if (start < lines.Length && lines[start].Length == 0)
        {
            start++;
        }

        return string.Join('\n', lines.Skip(start)).TrimEnd('\n', ' ');
    }
}
