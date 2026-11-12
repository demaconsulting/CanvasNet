// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     The YAML frontmatter of a diagram, reduced to what Mermaid keeps of it.
/// </summary>
/// <param name="Title">The <c>title</c> as text (Mermaid's <c>String(title)</c>), or null when it is missing or falsy.</param>
/// <param name="DisplayMode">The <c>displayMode</c> as text, or null when it is missing or falsy (Mermaid moves it to <c>gantt.displayMode</c>).</param>
/// <param name="Config">The <c>config</c> value as written (normally a mapping), or null when it is missing or falsy.</param>
/// <param name="Location">The location of the opening <c>---</c>.</param>
internal sealed record FrontmatterData(string? Title, string? DisplayMode, YamlNode? Config, MermaidLocation Location);

/// <summary>
///     A <c>%%{type: arguments}%%</c> directive found in the text, as Mermaid's <c>detectDirective</c> finds it.
/// </summary>
/// <param name="Type">The directive type as written (for example <c>init</c>, <c>initialize</c> or <c>wrap</c>).</param>
/// <param name="Arguments">
///     The arguments, trimmed, after Mermaid's replacement of every <c>'</c> by <c>"</c>, or null when there are none.
/// </param>
/// <param name="ArgumentsAreJson">
///     Whether the arguments are a JSON text that Mermaid parses with <c>JSON.parse</c> (they are a single word, used as
///     text, otherwise).
/// </param>
/// <param name="Location">The location of the opening <c>%%{</c>.</param>
internal sealed record DirectiveText(string Type, string? Arguments, bool ArgumentsAreJson, MermaidLocation Location);

/// <summary>
///     The result of preprocessing a diagram text: what Mermaid's <c>preprocessDiagram</c> and the start of
///     <c>Diagram.fromText</c> produce, with every character mapped back to the original text.
/// </summary>
/// <param name="Lines">The line table of the original text.</param>
/// <param name="Code">The diagram code (Mermaid's <c>processed.code</c>): the text without the frontmatter, the directives and the comment lines; the detector reads it and <see cref="MermaidLimits.MaxTextSize"/> applies to it.</param>
/// <param name="ParserText">The text the diagram parser reads: <see cref="Code"/> with Mermaid's entity encoding and a final line feed.</param>
/// <param name="Frontmatter">The frontmatter, or null when the text has none.</param>
/// <param name="Directives">The directives in text order.</param>
internal sealed record PreprocessedSource(
    SourceLines Lines,
    MappedText Code,
    MappedText ParserText,
    FrontmatterData? Frontmatter,
    IReadOnlyList<DirectiveText> Directives);

/// <summary>
///     Prepares a diagram text for detection and parsing exactly as Mermaid does before it parses a diagram: line endings,
///     HTML attribute quotes, the YAML frontmatter, <c>%%{...}%%</c> directives, <c>%%</c> comment lines and the
///     <c>#entity;</c> encoding, with a location map back to the original text.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> The steps and their order are those of Mermaid's <c>preprocessDiagram</c> (<c>cleanupText</c>,
///         <c>extractFrontMatter</c>, <c>processDirectives</c>, <c>cleanupComments</c>) followed by the
///         <c>encodeEntities</c> of <c>Diagram.fromText</c>. Each regular expression of those functions is replaced by a
///         hand-written scanner with the same matches (including the backtracking cases, such as an empty frontmatter
///         block), so preprocessing is linear in the length of the text. The <see cref="DirectiveText"/> list is what
///         <c>detectDirective</c> finds; interpreting the directives is the configuration stage's business.
///     </para>
///     <para>
///         <b>Locations.</b> Every intermediate text is a <see cref="MappedText"/>, so a position in <see cref="PreprocessedSource.Code"/>
///         or <see cref="PreprocessedSource.ParserText"/> has a location in the original text, frontmatter lines included.
///     </para>
///     <para>
///         <b>Limits.</b> The original text may be at most <see cref="SourceLengthFactor"/> times
///         <see cref="MermaidLimits.MaxTextSize"/> long, and the code at most <see cref="MermaidLimits.MaxTextSize"/> (as
///         Mermaid's <c>maxTextSize</c> check, which measures the code); either is an <see cref="InvalidDataException"/>.
///         The frontmatter YAML is bounded by <see cref="MermaidLimits.MaxYamlDepth"/>.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class Preprocessor
{
    /// <summary>How many times <see cref="MermaidLimits.MaxTextSize"/> the original text may be, frontmatter, directives and comments included.</summary>
    internal const int SourceLengthFactor = 4;

    /// <summary>The prefix Mermaid's <c>encodeEntities</c> gives a named or hexadecimal entity (<c>ﬂ°</c>).</summary>
    private const string EntityPrefix = "\uFB02\u00B0";

    /// <summary>The prefix Mermaid's <c>encodeEntities</c> gives a decimal entity (<c>ﬂ°°</c>).</summary>
    private const string NumericEntityPrefix = "\uFB02\u00B0\u00B0";

    /// <summary>The suffix Mermaid's <c>encodeEntities</c> gives an entity (<c>¶ß</c>).</summary>
    private const string EntitySuffix = "\u00B6\u00DF";

    /// <summary>
    ///     Preprocesses a diagram text.
    /// </summary>
    /// <param name="source">The original text.</param>
    /// <param name="sourceName">The source name placed in every location, or null.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The preprocessed source.</returns>
    /// <exception cref="InvalidDataException">Thrown when the text or its code is longer than the limits allow.</exception>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the frontmatter is not valid YAML or uses a refused construct.</exception>
    public static PreprocessedSource Run(string source, string? sourceName, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);

        // Refuse a hostile text before any work
        if (source.Length > (long)limits.MaxTextSize * SourceLengthFactor)
        {
            throw new InvalidDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"The Mermaid text is longer than {SourceLengthFactor} times the limit of {limits.MaxTextSize} characters (MermaidLimits.MaxTextSize)."));
        }

        var lines = new SourceLines(source, sourceName);
        var cleaned = CleanupText(MappedText.Identity(lines));

        // The frontmatter is read from the start of the cleaned text and removed
        FrontmatterData? frontmatter = null;
        var rest = cleaned;
        var match = MatchFrontmatter(cleaned.Text);
        if (match is { } fm)
        {
            frontmatter = ReadFrontmatter(cleaned, fm, limits);
            rest = cleaned.Slice(fm.End, cleaned.Length - fm.End);
        }

        // Directives are found, then removed; then the comment lines are removed and the start is trimmed
        var directives = DetectDirectives(rest);
        var code = TrimStart(CleanupComments(RemoveDirectives(rest)));

        // Mermaid's maxTextSize check measures the code
        if (code.Length > limits.MaxTextSize)
        {
            throw new InvalidDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"The Mermaid diagram is longer than the limit of {limits.MaxTextSize} characters (MermaidLimits.MaxTextSize) after its frontmatter, directives and comment lines are removed."));
        }

        // The parser reads the encoded code with a final line feed (Diagram.fromText)
        var encoded = EncodeEntities(code);
        var builder = new MappedText.Builder(encoded);
        builder.Append(0, encoded.Length);
        builder.AppendInserted("\n", encoded.OriginOf(encoded.Length));
        var parserText = builder.Build(encoded.OriginOf(encoded.Length));

        return new PreprocessedSource(lines, code, parserText, frontmatter, directives);
    }

    /// <summary>
    ///     Mermaid's <c>cleanupText</c>: turns <c>\r\n</c> and <c>\r</c> into <c>\n</c>, and the double quotes of HTML
    ///     attributes (<c>&lt;a href="x"&gt;</c>) into single quotes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The cleaned text.</returns>
    internal static MappedText CleanupText(MappedText text)
    {
        // Line endings: /\r\n?/g -> \n (the \n of a pair keeps the origin of the \r)
        var builder = new MappedText.Builder(text);
        var s = text.Text;
        var i = 0;
        while (i < s.Length)
        {
            if (s[i] == '\r')
            {
                builder.AppendInserted("\n", text.OriginOf(i));
                i += i + 1 < s.Length && s[i + 1] == '\n' ? 2 : 1;
            }
            else
            {
                builder.Append(i, 1);
                i++;
            }
        }

        var normalized = builder.Build(text.OriginOf(s.Length));
        return QuoteHtmlAttributes(normalized);
    }

    /// <summary>
    ///     Finds the frontmatter block at the start of a text, as Mermaid's <c>frontMatterRegex</c>
    ///     (<c>^([^\S\n\r]*)-{3}\s*[\n\r](.*?)[\n\r]\1-{3}\s*[\n\r]+</c> with the <c>s</c> flag) matches it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The match, or null when the text does not start with a frontmatter block.</returns>
    internal static FrontmatterMatch? MatchFrontmatter(string text)
    {
        // The indent: white space other than line breaks, then exactly ---
        var indent = 0;
        while (indent < text.Length && ScanPatterns.IsJsWhitespace(text[indent]) && text[indent] is not '\n' and not '\r')
        {
            indent++;
        }

        if (string.CompareOrdinal(text, indent, "---", 0, 3) != 0)
        {
            return null;
        }

        // \s*[\n\r] after the opening dashes: the greedy \s* backtracks to a line break of its run, the last one first
        var run = indent + 3;
        var runEnd = run;
        var breaks = new List<int>();
        while (runEnd < text.Length && ScanPatterns.IsJsWhitespace(text[runEnd]))
        {
            if (text[runEnd] is '\n' or '\r')
            {
                breaks.Add(runEnd);
            }

            runEnd++;
        }

        if (breaks.Count == 0)
        {
            return null;
        }

        // (.*?) is lazy: the first closing line after the body start wins
        var bodyStart = breaks[^1] + 1;
        for (var e = bodyStart; e < text.Length; e++)
        {
            if (TryMatchClosing(text, e, indent, out var end))
            {
                return new FrontmatterMatch(indent, bodyStart, e, end);
            }
        }

        // Backtracking to the previous line break of the opening run lets an empty body close at the last one
        if (breaks.Count >= 2 && TryMatchClosing(text, breaks[^1], indent, out var emptyEnd))
        {
            return new FrontmatterMatch(indent, breaks[^2] + 1, breaks[^1], emptyEnd);
        }

        return null;
    }

    /// <summary>
    ///     Finds the directives of a text as Mermaid's <c>detectDirective</c> does: after removing the lines that start
    ///     with <c>%%}%%</c> and replacing every <c>'</c> by <c>"</c>.
    /// </summary>
    /// <param name="text">The text without the frontmatter.</param>
    /// <returns>The directives in order.</returns>
    internal static IReadOnlyList<DirectiveText> DetectDirectives(MappedText text)
    {
        // commentWithoutDirectives: %%(?=}%%).*\n is removed (the rest of the line and its line feed)
        var s = text.Text;
        var builder = new MappedText.Builder(text);
        var position = 0;
        var failsBefore = -1;
        while (position < s.Length)
        {
            if (position >= failsBefore && string.CompareOrdinal(s, position, "%%}%%", 0, 5) == 0)
            {
                var lineEnd = position;
                while (lineEnd < s.Length && !ScanPatterns.IsJsLineTerminator(s[lineEnd]))
                {
                    lineEnd++;
                }

                if (lineEnd < s.Length && s[lineEnd] == '\n')
                {
                    position = lineEnd + 1;
                    continue;
                }

                // Every later occurrence on this line ends at the same terminator and fails the same way
                failsBefore = lineEnd;
            }

            builder.Append(position, 1);
            position++;
        }

        var detection = builder.Build(text.OriginOf(s.Length));
        var quoted = detection.Text.Replace('\'', '"');

        // directiveRegex, applied repeatedly
        var directives = new List<DirectiveText>();
        position = 0;
        while (position < quoted.Length)
        {
            if (TryMatchDirective(quoted, position, out var directive))
            {
                directives.Add(new DirectiveText(directive.Type, directive.Arguments, directive.ArgumentsAreJson, detection.Locate(position)));
                position = directive.End;
            }
            else
            {
                position++;
            }
        }

        return directives;
    }

    /// <summary>
    ///     Mermaid's <c>removeDirectives</c>: removes every match of <c>directiveRegex</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without directives.</returns>
    internal static MappedText RemoveDirectives(MappedText text)
    {
        var s = text.Text;
        var builder = new MappedText.Builder(text);
        var position = 0;
        while (position < s.Length)
        {
            if (TryMatchDirective(s, position, out var directive))
            {
                position = directive.End;
            }
            else
            {
                builder.Append(position, 1);
                position++;
            }
        }

        return builder.Build(text.OriginOf(s.Length));
    }

    /// <summary>
    ///     Mermaid's <c>cleanupComments</c> without the final trim: removes the comment lines
    ///     (<c>/^\s*%%(?!{)[^\n]+\n?/gm</c>), together with the blank lines before them.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without comment lines.</returns>
    internal static MappedText CleanupComments(MappedText text)
    {
        var s = text.Text;
        var builder = new MappedText.Builder(text);
        var position = 0;
        while (position < s.Length)
        {
            // Only a line start can begin a match (the m flag's ^)
            if (position > 0 && !ScanPatterns.IsJsLineTerminator(s[position - 1]))
            {
                builder.Append(position, 1);
                position++;
                continue;
            }

            // \s* spans line breaks; it must end at %% not followed by { and by at least one character of the line
            var j = position;
            while (j < s.Length && ScanPatterns.IsJsWhitespace(s[j]))
            {
                j++;
            }

            if (string.CompareOrdinal(s, j, "%%", 0, 2) == 0 && j + 2 < s.Length && s[j + 2] is not '{' and not '\n')
            {
                var end = j + 2;
                while (end < s.Length && s[end] != '\n')
                {
                    end++;
                }

                position = end < s.Length ? end + 1 : end;
                continue;
            }

            // Every line start up to the end of this run fails the same way, so the run is kept as it is
            var keep = Math.Min(j + 1, s.Length);
            builder.Append(position, keep - position);
            position = keep;
        }

        return builder.Build(text.OriginOf(s.Length));
    }

    /// <summary>
    ///     Mermaid's <c>encodeEntities</c>: drops the last <c>;</c> of <c>style</c> and <c>classDef</c> lines that contain a
    ///     color, then encodes <c>#name;</c> and <c>#123;</c> entities with Mermaid's private markers.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The encoded text.</returns>
    internal static MappedText EncodeEntities(MappedText text)
    {
        var withoutStyleSemicolons = DropStyleSemicolons(DropStyleSemicolons(text, "style"), "classDef");
        var s = withoutStyleSemicolons.Text;
        var builder = new MappedText.Builder(withoutStyleSemicolons);
        var position = 0;
        while (position < s.Length)
        {
            // #\w+; becomes the prefix, the word and the suffix
            if (s[position] == '#')
            {
                var end = position + 1;
                while (end < s.Length && ScanPatterns.IsJsWordChar(s[end]))
                {
                    end++;
                }

                if (end > position + 1 && end < s.Length && s[end] == ';')
                {
                    var word = s.AsSpan(position + 1, end - position - 1);
                    var numeric = word.IndexOfAnyExceptInRange('0', '9') < 0;
                    builder.AppendInserted(numeric ? NumericEntityPrefix : EntityPrefix, withoutStyleSemicolons.OriginOf(position));
                    builder.Append(position + 1, end - position - 1);
                    builder.AppendInserted(EntitySuffix, withoutStyleSemicolons.OriginOf(end));
                    position = end + 1;
                    continue;
                }
            }

            builder.Append(position, 1);
            position++;
        }

        return builder.Build(withoutStyleSemicolons.OriginOf(s.Length));
    }

    /// <summary>
    ///     Removes the leading white space of a text (JavaScript's <c>trimStart</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    internal static MappedText TrimStart(MappedText text)
    {
        var start = 0;
        while (start < text.Length && ScanPatterns.IsJsWhitespace(text.Text[start]))
        {
            start++;
        }

        return start == 0 ? text : text.Slice(start, text.Length - start);
    }

    /// <summary>
    ///     Matches Mermaid's <c>directiveRegex</c>
    ///     (<c>%{2}{\s*(?:(\w+)\s*:|(\w+))\s*(?:(\w+)|((?:(?!}%{2}).|\r?\n)*))?\s*(?:}%{2})?</c>) at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position.</param>
    /// <param name="match">The match.</param>
    /// <returns>Whether a directive starts at the position.</returns>
    internal static bool TryMatchDirective(string text, int position, out DirectiveMatch match)
    {
        match = default;
        if (string.CompareOrdinal(text, position, "%%{", 0, 3) != 0)
        {
            return false;
        }

        // The type: a word, optionally followed by ':'
        var i = SkipWhitespace(text, position + 3);
        var typeStart = i;
        while (i < text.Length && ScanPatterns.IsJsWordChar(text[i]))
        {
            i++;
        }

        if (i == typeStart)
        {
            return false;
        }

        var type = text[typeStart..i];
        var afterType = SkipWhitespace(text, i);
        if (afterType < text.Length && text[afterType] == ':')
        {
            i = afterType + 1;
        }

        // The arguments: a word, or anything up to }%% that stays on \n-separated lines
        i = SkipWhitespace(text, i);
        string? arguments;
        var json = false;
        if (i < text.Length && ScanPatterns.IsJsWordChar(text[i]))
        {
            var start = i;
            while (i < text.Length && ScanPatterns.IsJsWordChar(text[i]))
            {
                i++;
            }

            arguments = text[start..i];
        }
        else
        {
            var start = i;
            i = ScanDirectiveArguments(text, i);
            var raw = text[start..i];
            arguments = raw.Length == 0 ? null : TrimJs(raw);
            json = arguments is not null;
        }

        // Optional white space and the closing }%%
        i = SkipWhitespace(text, i);
        if (string.CompareOrdinal(text, i, "}%%", 0, 3) == 0)
        {
            i += 3;
        }

        match = new DirectiveMatch(i, type, arguments, json);
        return true;
    }

    /// <summary>
    ///     Removes JavaScript white space from both ends of a text (JavaScript's <c>trim</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The trimmed text.</returns>
    internal static string TrimJs(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && ScanPatterns.IsJsWhitespace(text[start]))
        {
            start++;
        }

        while (end > start && ScanPatterns.IsJsWhitespace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    /// <summary>
    ///     Skips JavaScript white space (<c>\s*</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position.</param>
    /// <returns>The position after the white space.</returns>
    private static int SkipWhitespace(string text, int position)
    {
        while (position < text.Length && ScanPatterns.IsJsWhitespace(text[position]))
        {
            position++;
        }

        return position;
    }

    /// <summary>
    ///     Scans the free-form arguments of a directive: <c>((?:(?!}%{2}).|\r?\n)*)</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The start.</param>
    /// <returns>The end of the arguments.</returns>
    private static int ScanDirectiveArguments(string text, int position)
    {
        while (position < text.Length)
        {
            if (string.CompareOrdinal(text, position, "}%%", 0, 3) == 0)
            {
                break;
            }

            var c = text[position];
            if (c == '\n')
            {
                position++;
            }
            else if (c == '\r' && position + 1 < text.Length && text[position + 1] == '\n')
            {
                position += 2;
            }
            else if (ScanPatterns.IsJsLineTerminator(c))
            {
                break;
            }
            else
            {
                position++;
            }
        }

        return position;
    }

    /// <summary>
    ///     Checks for the closing line of a frontmatter block: <c>[\n\r]\1-{3}\s*[\n\r]+</c> at a position.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position of the line break before the closing dashes.</param>
    /// <param name="indent">The length of the indent (the opening line's, at the start of the text).</param>
    /// <param name="end">The end of the match: after the last line break of the white space that follows the dashes.</param>
    /// <returns>Whether the closing line matches.</returns>
    private static bool TryMatchClosing(string text, int position, int indent, out int end)
    {
        end = 0;
        if (text[position] is not '\n' and not '\r')
        {
            return false;
        }

        // The same indent, then ---
        var p = position + 1;
        if (p + indent > text.Length || string.CompareOrdinal(text, p, text, 0, indent) != 0)
        {
            return false;
        }

        p += indent;
        if (string.CompareOrdinal(text, p, "---", 0, 3) != 0)
        {
            return false;
        }

        // \s*[\n\r]+: the white space after the dashes must contain a line break; the match ends after the last one
        var lastBreak = -1;
        for (var q = p + 3; q < text.Length && ScanPatterns.IsJsWhitespace(text[q]); q++)
        {
            if (text[q] is '\n' or '\r')
            {
                lastBreak = q;
            }
        }

        if (lastBreak < 0)
        {
            return false;
        }

        end = lastBreak + 1;
        return true;
    }

    /// <summary>
    ///     Reads the frontmatter YAML and keeps what Mermaid's <c>extractFrontMatter</c> keeps.
    /// </summary>
    /// <param name="text">The cleaned text.</param>
    /// <param name="match">The frontmatter match.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The frontmatter.</returns>
    private static FrontmatterData ReadFrontmatter(MappedText text, FrontmatterMatch match, MermaidLimits limits)
    {
        // The indent of the opening line is removed from the body (js-yaml refuses tab indentation)
        var body = text.Slice(match.BodyStart, match.BodyEnd - match.BodyStart);
        if (match.Indent > 0)
        {
            body = RemoveIndent(body, text.Text[..match.Indent]);
        }

        var root = YamlSubsetParser.Parse(body.Text, body.Locate, limits.MaxYamlDepth);

        // Only a mapping counts; title, displayMode and config are kept when they are truthy
        string? title = null;
        string? displayMode = null;
        YamlNode? config = null;
        if (root is YamlMapping mapping)
        {
            var titleNode = mapping.Get("title");
            title = titleNode is { IsTruthy: true } ? titleNode.ToJsString() : null;
            var modeNode = mapping.Get("displayMode");
            displayMode = modeNode is { IsTruthy: true } ? modeNode.ToJsString() : null;
            var configNode = mapping.Get("config");
            config = configNode is { IsTruthy: true } ? configNode : null;
        }

        return new FrontmatterData(title, displayMode, config, text.Locate(match.Indent));
    }

    /// <summary>
    ///     Removes an indent from the start of every line that has it.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="indent">The indent.</param>
    /// <returns>The body without the indent.</returns>
    private static MappedText RemoveIndent(MappedText body, string indent)
    {
        var s = body.Text;
        var builder = new MappedText.Builder(body);
        var lineStart = 0;
        while (lineStart <= s.Length)
        {
            var lineEnd = s.IndexOf('\n', lineStart);
            var end = lineEnd < 0 ? s.Length : lineEnd;
            var skip = string.CompareOrdinal(s, lineStart, indent, 0, indent.Length) == 0 && lineStart + indent.Length <= end ? indent.Length : 0;
            builder.Append(lineStart + skip, end - lineStart - skip);
            if (lineEnd < 0)
            {
                break;
            }

            builder.Append(lineEnd, 1);
            lineStart = lineEnd + 1;
        }

        return builder.Build(body.OriginOf(s.Length));
    }

    /// <summary>
    ///     Replaces the double quotes of HTML attributes: <c>/&lt;(\w+)([^&gt;]*)&gt;/g</c>, and in its attributes
    ///     <c>/="([^"]*)"/g</c> becomes <c>='$1'</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with single-quoted attributes (the same length, so the origins do not move).</returns>
    private static MappedText QuoteHtmlAttributes(MappedText text)
    {
        var s = text.Text;
        StringBuilder? changed = null;
        var position = 0;
        while (position < s.Length)
        {
            // A tag: < then a word, then anything up to the first >
            if (s[position] != '<' || position + 1 >= s.Length || !ScanPatterns.IsJsWordChar(s[position + 1]))
            {
                position++;
                continue;
            }

            // Without a > further on, no later tag can close either
            var close = s.IndexOf('>', position + 1);
            if (close < 0)
            {
                break;
            }

            // In the attributes, ="..." becomes ='...'
            var wordEnd = position + 1;
            while (wordEnd < close && ScanPatterns.IsJsWordChar(s[wordEnd]))
            {
                wordEnd++;
            }

            var a = wordEnd;
            while (a + 1 < close)
            {
                if (s[a] != '=' || s[a + 1] != '"')
                {
                    a++;
                    continue;
                }

                var endQuote = s.IndexOf('"', a + 2, close - a - 2);
                if (endQuote < 0)
                {
                    break;
                }

                changed ??= new StringBuilder(s);
                changed[a + 1] = '\'';
                changed[endQuote] = '\'';
                a = endQuote + 1;
            }

            position = close + 1;
        }

        if (changed is null)
        {
            return text;
        }

        // The same characters at the same positions except the quotes, so the origins are kept one for one
        var replaced = changed.ToString();
        var result = new MappedText.Builder(text);
        for (var i = 0; i < replaced.Length; i++)
        {
            if (replaced[i] == s[i])
            {
                result.Append(i, 1);
            }
            else
            {
                result.AppendInserted(replaced[i].ToString(), text.OriginOf(i));
            }
        }

        return result.Build(text.OriginOf(s.Length));
    }

    /// <summary>
    ///     Drops the last <c>;</c> of every line that matches <c>/{keyword}.*:\S*#.*;/g</c> (the first pass of Mermaid's
    ///     <c>encodeEntities</c>, so that a color such as <c>#f9f;</c> is not taken for an entity).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="keyword">The keyword: <c>style</c> or <c>classDef</c>.</param>
    /// <returns>The text with those semicolons removed.</returns>
    private static MappedText DropStyleSemicolons(MappedText text, string keyword)
    {
        var s = text.Text;
        var drops = new List<int>();
        var position = 0;
        while (true)
        {
            var at = s.IndexOf(keyword, position, StringComparison.Ordinal);
            if (at < 0)
            {
                break;
            }

            // The match stays on the line ('.' and \S never cross a line terminator)
            var lineEnd = at + keyword.Length;
            while (lineEnd < s.Length && !ScanPatterns.IsJsLineTerminator(s[lineEnd]))
            {
                lineEnd++;
            }

            var lastSemicolon = s.LastIndexOf(';', lineEnd - 1, lineEnd - at - keyword.Length);
            if (lastSemicolon >= 0 && HasColorBefore(s, at + keyword.Length, lastSemicolon))
            {
                drops.Add(lastSemicolon);
            }

            // A later occurrence on the same line can only fail the same way or was consumed by the match
            position = lineEnd;
        }

        if (drops.Count == 0)
        {
            return text;
        }

        var builder = new MappedText.Builder(text);
        var from = 0;
        foreach (var drop in drops)
        {
            builder.Append(from, drop - from);
            from = drop + 1;
        }

        builder.Append(from, s.Length - from);
        return builder.Build(text.OriginOf(s.Length));
    }

    /// <summary>
    ///     Checks for <c>:\S*#</c> between two positions of a line: a colon followed, without white space, by a <c>#</c>
    ///     that lies before the end.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first position a colon may have.</param>
    /// <param name="end">The position the <c>#</c> must lie before.</param>
    /// <returns>Whether there is such a colon and hash.</returns>
    private static bool HasColorBefore(string text, int start, int end)
    {
        // Scan right to left, remembering whether a # lies ahead within the current run of non-space characters
        var hashAhead = false;
        for (var i = end - 1; i >= start; i--)
        {
            var c = text[i];
            if (ScanPatterns.IsJsWhitespace(c))
            {
                hashAhead = false;
            }
            else if (c == ':' && hashAhead)
            {
                return true;
            }
            else if (c == '#')
            {
                hashAhead = true;
            }
        }

        return false;
    }
}

/// <summary>
///     The parts of a frontmatter match, as positions of the cleaned text.
/// </summary>
/// <param name="Indent">The length of the indent of the opening line.</param>
/// <param name="BodyStart">The first position of the YAML body.</param>
/// <param name="BodyEnd">The position after the YAML body.</param>
/// <param name="End">The position after the whole block.</param>
internal readonly record struct FrontmatterMatch(int Indent, int BodyStart, int BodyEnd, int End);

/// <summary>
///     A match of Mermaid's directive expression.
/// </summary>
/// <param name="End">The position after the match.</param>
/// <param name="Type">The directive type.</param>
/// <param name="Arguments">The trimmed arguments, or null.</param>
/// <param name="ArgumentsAreJson">Whether the arguments are free-form (JSON) rather than a word.</param>
internal readonly record struct DirectiveMatch(int End, string Type, string? Arguments, bool ArgumentsAreJson);
