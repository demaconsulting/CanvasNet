// Portions derived from mermaid (MIT), @mermaid-js/parser (MIT) and Langium (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Pie;

/// <summary>
///     The pie parser: a hand-written port of the Langium grammar of <c>@mermaid-js/parser</c> (<c>pie.langium</c> with
///     <c>common.langium</c>, its token builder and value converters) and of Mermaid's pie database (<c>pieDb.ts</c> with
///     <c>populateCommonDb</c> and <c>commonDb.ts</c>), producing a <see cref="PieDiagram"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The token types are tried at each position in the order Langium gives chevrotain (the keywords
///         <c>showData</c>, <c>pie</c> and <c>:</c>, then the terminals <c>NUMBER_PIE</c>, <c>ACC_DESCR</c>,
///         <c>ACC_TITLE</c>, <c>TITLE</c>, <c>STRING</c> and <c>NEWLINE</c>, then the hidden white space, YAML, directives
///         and comments), and the first that matches wins, as in chevrotain. The keywords must be followed by <c>%%</c> or
///         by white space. The texts are matched by hand-written scanners with the matches of Langium's regular expressions,
///         so lexing is linear in the text.
///     </para>
///     <para>
///         <b>Grammar.</b> <c>NEWLINE* "pie" showData? (TitleAndAccessibilities | PieSection | NEWLINE)* EOF</c> is parsed
///         with one token of lookahead (the grammar is LL(1)). Errors come where Mermaid reports its first one: a lexical
///         error at the first character no token matches, before any grammar error (Mermaid lists the lexer's errors
///         first), else the first token the grammar does not accept, with the token names chevrotain lists as
///         <see cref="MermaidSyntaxException.Expected"/>.
///     </para>
///     <para>
///         <b>Values.</b> A title, <c>accTitle</c> or <c>accDescr</c> is converted with the regular expressions of the
///         mermaid value converter (trimmed, runs of spaces collapsed; the lines of a multi-line description trimmed), a
///         string with Langium's <c>convertString</c> (the quotes removed; <c>\b \f \n \r \t \v \0</c> escapes, and any
///         other escaped character taken as itself), and a number with JavaScript's <c>Number</c>. The last of each
///         statement wins; an empty one is not set (<c>populateCommonDb</c> tests it for truth).
///     </para>
///     <para>
///         <b>Database.</b> As Mermaid's <c>addSection</c>, a negative value fails (a located
///         <see cref="MermaidSyntaxException"/> with Mermaid's message) and a label seen before is ignored. The title,
///         <c>accTitle</c> and <c>accDescr</c> are sanitized as <c>commonDb</c> sanitizes them.
///     </para>
///     <para>
///         <b>Limits.</b> Every section statement counts against <see cref="MermaidLimits.MaxNodes"/>; every kept token
///         text, every converted value, every sanitized text and, with <c>showData</c>, every legend text
///         (<c>label [value]</c>) is checked against <see cref="MermaidLimits.MaxLabelLength"/>
///         (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class PieParser
{
    /// <summary>The tokens that start an iteration of the statement loop.</summary>
    private static readonly string[] StatementStarts = [AccDescr, AccTitle, Title, StringToken, Newline];

    /// <summary>The parser text.</summary>
    private readonly MappedText _text;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The tokens.</summary>
    private List<PieToken> _tokens = [];

    /// <summary>The index of the lookahead token.</summary>
    private int _index;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="text">The parser text.</param>
    /// <param name="limits">The limits of the open call.</param>
    private PieParser(MappedText text, MermaidLimits limits)
    {
        _text = text;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private PieToken Current => _tokens[_index];

    /// <summary>
    ///     Parses a pie chart.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The diagram.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, when a value is negative or when a limit is exceeded.
    /// </exception>
    public static PieDiagram Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        return new PieParser(source.ParserText, limits).Run(detected.Location);
    }

    /// <summary>
    ///     Converts the text of a <c>STRING</c> token as Langium's <c>convertString</c> does: the quotes removed, the
    ///     escapes <c>\b \f \n \r \t \v \0</c> replaced, and any other escaped character kept without its backslash.
    /// </summary>
    /// <param name="input">The token text, quotes included.</param>
    /// <returns>The value.</returns>
    internal static string ConvertString(string input)
    {
        var builder = new StringBuilder(input.Length);
        var i = 1;
        while (i < input.Length - 1)
        {
            var c = input[i++];
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            // charAt past the end is the empty string
            if (i < input.Length)
            {
                builder.Append(input[i++] switch
                {
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'v' => '\v',
                    '0' => '\0',
                    var other => other,
                });
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Converts the text of a <c>TITLE</c>, <c>ACC_TITLE</c> or single-line <c>ACC_DESCR</c> token as the mermaid value
    ///     converter does: the text after the keyword (and its colon) trimmed, with each run of two or more tabs and
    ///     spaces made one space.
    /// </summary>
    /// <param name="value">The text after the keyword (and its colon).</param>
    /// <returns>The value.</returns>
    internal static string ConvertSingleLine(string value) => CollapseSpaces(Preprocessor.TrimJs(value));

    /// <summary>
    ///     Converts the text in the braces of a multi-line <c>ACC_DESCR</c> as the mermaid value converter does: the white
    ///     space at the start of each line removed (<c>/^\s*/gm</c>), the white space before each line end removed
    ///     (<c>/\s+$/gm</c>), runs of tabs and spaces made one space and runs of line breaks one line feed.
    /// </summary>
    /// <param name="value">The text in the braces.</param>
    /// <returns>The value.</returns>
    internal static string ConvertMultiLine(string value)
    {
        // /^\s*/gm: a white-space run that starts a line is removed, line breaks in it included
        var builder = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length)
        {
            if (i == 0 || ScanPatterns.IsJsLineTerminator(value[i - 1]))
            {
                var start = i;
                while (i < value.Length && ScanPatterns.IsJsWhitespace(value[i]))
                {
                    i++;
                }

                if (i > start)
                {
                    continue;
                }
            }

            builder.Append(value[i]);
            i++;
        }

        // /\s+$/gm: a white-space run is removed up to its last line terminator, or wholly at the end of the text
        var text = builder.ToString();
        builder.Clear();
        i = 0;
        while (i < text.Length)
        {
            if (!ScanPatterns.IsJsWhitespace(text[i]))
            {
                builder.Append(text[i]);
                i++;
                continue;
            }

            var end = i;
            while (end < text.Length && ScanPatterns.IsJsWhitespace(text[end]))
            {
                end++;
            }

            // The run is kept from its last line terminator after its first character, or wholly without one
            var keep = i;
            if (end < text.Length)
            {
                for (var t = end - 1; t > i; t--)
                {
                    if (ScanPatterns.IsJsLineTerminator(text[t]))
                    {
                        keep = t;
                        break;
                    }
                }

                builder.Append(text, keep, end - keep);
            }

            i = end;
        }

        // [\t ]{2,} becomes one space, [\n\r]{2,} one line feed
        return CollapseRuns(CollapseSpaces(builder.ToString()), c => c is '\n' or '\r', '\n');
    }

    /// <summary>
    ///     Makes each run of two or more tabs and spaces one space (<c>/[\t ]{2,}/gm</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    private static string CollapseSpaces(string text) => CollapseRuns(text, c => c is '\t' or ' ', ' ');

    /// <summary>
    ///     Replaces each run of two or more characters of a class by one character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="inRun">Whether a character is of the class.</param>
    /// <param name="replacement">The character that replaces a run.</param>
    /// <returns>The text.</returns>
    private static string CollapseRuns(string text, Func<char, bool> inRun, char replacement)
    {
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var end = i;
            while (end < text.Length && inRun(text[end]))
            {
                end++;
            }

            if (end - i >= 2)
            {
                builder.Append(replacement);
                i = end;
                continue;
            }

            builder.Append(text[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses the whole text (<c>Pie</c>) and fills the database.
    /// </summary>
    /// <param name="keyword">The location of the diagram keyword.</param>
    /// <returns>The diagram.</returns>
    private PieDiagram Run(MermaidLocation keyword)
    {
        var text = _text.Text;
        _tokens = Tokenize(text);

        // NEWLINE* "pie" showData?
        while (Is(Newline))
        {
            _index++;
        }

        if (!Is(PieKeyword))
        {
            throw Fail([PieKeyword], "start the diagram with the pie keyword");
        }

        _index++;
        var showData = false;
        if (Is(ShowData))
        {
            _index++;
            showData = true;
        }

        // (TitleAndAccessibilities | PieSection | NEWLINE)*, the AST fields keeping the last value
        string? title = null;
        string? accTitle = null;
        string? accDescr = null;
        var sections = new List<(string Label, double Value, MermaidLocation Location)>();
        while (Is(StatementStarts))
        {
            var token = Current;
            switch (token.Kind)
            {
                case Newline:
                    _index++;
                    break;

                case StringToken:
                    {
                        if (sections.Count >= _limits.MaxNodes)
                        {
                            throw new MermaidSyntaxException(
                                MermaidDiagnosticCodes.LimitExceeded,
                                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} sections (MermaidLimits.MaxNodes)"),
                                Locate(token),
                                "split the pie chart, or raise MermaidLimits.MaxNodes when the source is trusted");
                        }

                        var label = Checked(ConvertString(TextOf(token)), token);
                        _index++;
                        Expect(Colon);
                        var number = Expect(NumberPie);
                        var value = double.Parse(TextOf(number), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                        EndOfLine();
                        sections.Add((label, value, Locate(token)));
                        break;
                    }

                default:
                    {
                        // TitleAndAccessibilities: one or more of the three, each ended by a line end
                        do
                        {
                            var statement = Current;
                            var raw = TextOf(statement);
                            _index++;
                            switch (statement.Kind)
                            {
                                case AccDescr:
                                    accDescr = ConvertAccDescr(raw, statement);
                                    break;
                                case AccTitle:
                                    accTitle = Checked(ConvertSingleLine(raw[(raw.IndexOf(':', StringComparison.Ordinal) + 1)..]), statement);
                                    break;
                                default:
                                    title = Checked(ConvertSingleLine(raw[(raw.IndexOf("title", StringComparison.Ordinal) + "title".Length)..]), statement);
                                    break;
                            }

                            EndOfLine();
                        }
                        while (Is(AccDescr, AccTitle, Title));
                        break;
                    }
            }
        }

        // The entry rule must end at the end of the tokens
        if (!Is(Eof))
        {
            throw Fail([Eof], "write each slice, title, accTitle and accDescr on a line of its own, after the pie line");
        }

        return Populate(keyword, showData, title, accTitle, accDescr, sections);
    }

    /// <summary>
    ///     Fills the database from the parsed values, as Mermaid's <c>populateDb</c> does.
    /// </summary>
    /// <param name="keyword">The location of the diagram keyword.</param>
    /// <param name="showData">Whether <c>showData</c> was given.</param>
    /// <param name="title">The title, or null.</param>
    /// <param name="accTitle">The accessible title, or null.</param>
    /// <param name="accDescr">The accessible description, or null.</param>
    /// <param name="parsed">The sections in order.</param>
    /// <returns>The diagram.</returns>
    private PieDiagram Populate(
        MermaidLocation keyword,
        bool showData,
        string? title,
        string? accTitle,
        string? accDescr,
        List<(string Label, double Value, MermaidLocation Location)> parsed)
    {
        // populateCommonDb sets the texts that are not empty
        var description = string.IsNullOrEmpty(accDescr) ? string.Empty : CheckDerived(PieDiagram.SanitizeDescription(accDescr), keyword);
        var accessibleTitle = string.IsNullOrEmpty(accTitle) ? string.Empty : CheckDerived(PieDiagram.SanitizeAccTitle(accTitle), keyword);
        var diagramTitle = string.IsNullOrEmpty(title) ? string.Empty : CheckDerived(PieDiagram.Sanitize(title), keyword);

        // addSection: a negative value fails, a label seen before is ignored
        var sections = new List<PieSection>();
        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (label, value, location) in parsed)
        {
            if (value < 0)
            {
                throw new MermaidSyntaxException(
                    MermaidDiagnosticCodes.SyntaxError,
                    $"\"{label}\" has invalid value: {YamlSubsetParser.FormatJsNumber(value)}. Negative values are not allowed in pie charts. All slice values must be >= 0.",
                    location,
                    "give every slice a value of zero or more");
            }

            if (labels.Add(label))
            {
                var section = new PieSection(label, value, location);
                if (showData)
                {
                    CheckDerived(section.LegendText(showData), location);
                }

                sections.Add(section);
            }
        }

        return new PieDiagram(keyword, showData, diagramTitle, accessibleTitle, description, sections);
    }

    /// <summary>
    ///     Converts the text of an <c>ACC_DESCR</c> token: the single-line form after its colon, or the multi-line form in its
    ///     braces.
    /// </summary>
    /// <param name="raw">The token text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The value.</returns>
    private string ConvertAccDescr(string raw, PieToken token)
    {
        var after = raw.IndexOf("accDescr", StringComparison.Ordinal) + "accDescr".Length;
        var colon = after;
        while (colon < raw.Length && raw[colon] is '\t' or ' ')
        {
            colon++;
        }

        var value = colon < raw.Length && raw[colon] == ':'
            ? ConvertSingleLine(raw[(colon + 1)..])
            : ConvertMultiLine(raw[(raw.IndexOf('{', after) + 1)..^1]);
        return Checked(value, token);
    }

    /// <summary>
    ///     Parses a line end (<c>EOL</c>: <c>NEWLINE+ | EOF</c>).
    /// </summary>
    private void EndOfLine()
    {
        if (Is(Eof))
        {
            return;
        }

        if (!Is(Newline))
        {
            throw Fail([Newline, Eof], "end the statement with a line break; write one statement per line");
        }

        while (Is(Newline))
        {
            _index++;
        }
    }

    /// <summary>
    ///     Gets whether the lookahead is of one of the kinds.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>Whether it is.</returns>
    private bool Is(params string[] kinds) => Array.IndexOf(kinds, Current.Kind) >= 0;

    /// <summary>
    ///     Consumes the lookahead token when it is of the kind, and fails otherwise.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The token.</returns>
    private PieToken Expect(string kind)
    {
        if (!Is(kind))
        {
            throw Fail([kind], kind == Colon
                ? "separate the label and the value with a colon: \"Dogs\" : 42"
                : "give the slice a value: digits with an optional '-' and decimal part, such as 42 or 12.5");
        }

        return _tokens[_index++];
    }

    /// <summary>
    ///     Gets the text of a token that is kept, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string TextOf(PieToken token) => Checked(_text.Text[token.Start..token.End], token);

    /// <summary>
    ///     Checks a text derived from a token against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string Checked(string text, PieToken token) => CheckDerived(text, Locate(token));

    /// <summary>
    ///     Checks a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location of the text.</param>
    /// <returns>The text.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    private string CheckDerived(string text, MermaidLocation location)
    {
        if (text.Length > _limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {text.Length} characters long, more than the limit of {_limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }

        return text;
    }

    /// <summary>
    ///     Gets the location of a token in the original text.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The location.</returns>
    private MermaidLocation Locate(PieToken token) => _text.Locate(token.Start);

    /// <summary>
    ///     Creates the exception for a lookahead token the grammar does not accept.
    /// </summary>
    /// <param name="expected">The token names chevrotain lists.</param>
    /// <param name="hint">The suggested fix.</param>
    /// <returns>The located exception.</returns>
    private MermaidSyntaxException Fail(string[] expected, string hint)
    {
        var token = Current;
        var found = token.Kind == Eof ? "the end of the text" : $"'{token.Kind}' ({Describe(_text.Text[token.Start..token.End])})";
        return new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"expected {TokenCursor.JoinAlternatives([.. expected.Select(e => $"'{e}'")])}, found {found}",
            Locate(token),
            hint,
            expected);
    }

    /// <summary>
    ///     Describes the text of a token for a message.
    /// </summary>
    /// <param name="text">The token text.</param>
    /// <returns>The quoted text, shortened and with line breaks shown.</returns>
    private static string Describe(string text)
    {
        var shown = text.Length > 24 ? text[..24] + "..." : text;
        return "'" + shown.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "'";
    }
}

/// <summary>
///     A section of a pie chart (an entry of Mermaid's <c>sections</c> map): its label, its value and where it is.
/// </summary>
/// <param name="Label">The label, as the string converter gives it.</param>
/// <param name="Value">The value: zero or more, possibly infinite.</param>
/// <param name="Location">The location of the section statement.</param>
internal sealed record PieSection(string Label, double Value, MermaidLocation Location)
{
    /// <summary>
    ///     Gets the text of the section's legend entry: its label, followed with <c>showData</c> by its value in brackets
    ///     as JavaScript writes the number.
    /// </summary>
    /// <param name="showData">Whether <c>showData</c> is set.</param>
    /// <returns>The text.</returns>
    public string LegendText(bool showData) =>
        showData ? $"{Label} [{YamlSubsetParser.FormatJsNumber(Value)}]" : Label;
}

/// <summary>
///     A parsed pie chart: the state of Mermaid's pie database (<c>pieDb.ts</c> with the title and accessibility texts of
///     <c>commonDb.ts</c>) and the metadata of the document; a render lays it out with <see cref="PieRenderer"/>.
/// </summary>
/// <remarks>
///     The diagram is never changed after parsing and is safe for concurrent use.
/// </remarks>
internal sealed class PieDiagram : IMermaidDiagram
{
    /// <summary>
    ///     Initializes a diagram.
    /// </summary>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    /// <param name="showData">Whether <c>showData</c> is set.</param>
    /// <param name="diagramTitle">The sanitized title, or empty.</param>
    /// <param name="accessibleTitle">The sanitized accessible title, or empty.</param>
    /// <param name="accessibleDescription">The sanitized accessible description, or empty.</param>
    /// <param name="sections">The sections in order, each label once.</param>
    public PieDiagram(
        MermaidLocation keywordLocation,
        bool showData,
        string diagramTitle,
        string accessibleTitle,
        string accessibleDescription,
        IReadOnlyList<PieSection> sections)
    {
        KeywordLocation = keywordLocation;
        ShowData = showData;
        DiagramTitle = diagramTitle;
        AccessibleTitle = accessibleTitle;
        AccessibleDescription = accessibleDescription;
        Sections = sections;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets a value indicating whether the legend shows the values (<c>showData</c>).</summary>
    public bool ShowData { get; }

    /// <summary>Gets the title of the <c>title</c> statement, sanitized (empty when there is none).</summary>
    public string DiagramTitle { get; }

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; }

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; }

    /// <summary>Gets the sections in the order they were added (Mermaid's <c>getSections</c>).</summary>
    public IReadOnlyList<PieSection> Sections { get; }

    /// <summary>Gets the title the diagram declares, or null (the frontmatter title is the document's fallback).</summary>
    public string? Title => DiagramTitle.Length > 0 ? DiagramTitle : null;

    /// <summary>Gets the accessible title, or null.</summary>
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <summary>Gets the direction: a pie chart has none.</summary>
    public string? Direction => null;

    /// <summary>Gets the number of sections (the legend entries).</summary>
    public int NodeCount => Sections.Count;

    /// <summary>Gets the number of edges: a pie chart has none.</summary>
    public int EdgeCount => 0;

    /// <summary>Gets the number of subgraphs: a pie chart has none.</summary>
    public int SubgraphCount => 0;

    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new PieRenderer(this, context).Draw();
    }

    /// <summary>
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does (an empty text is kept): <c>setDiagramTitle</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    internal static string Sanitize(string text) => text.Length == 0 ? text : HtmlSanitizer.Sanitize(text);

    /// <summary>
    ///     Sanitizes an accessible title as Mermaid's <c>setAccTitle</c> does: sanitized, the leading white space removed.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The accessible title.</returns>
    internal static string SanitizeAccTitle(string text)
    {
        var sanitized = Sanitize(text);
        var start = 0;
        while (start < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[start]))
        {
            start++;
        }

        return sanitized[start..];
    }

    /// <summary>
    ///     Sanitizes an accessible description as Mermaid's <c>setAccDescription</c> does: sanitized, the white space after
    ///     each line feed removed (<c>/\n\s+/g</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The accessible description.</returns>
    internal static string SanitizeDescription(string text)
    {
        var sanitized = Sanitize(text);
        var builder = new StringBuilder(sanitized.Length);
        var i = 0;
        while (i < sanitized.Length)
        {
            builder.Append(sanitized[i]);
            i++;
            if (sanitized[i - 1] == '\n')
            {
                while (i < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[i]))
                {
                    i++;
                }
            }
        }

        return builder.ToString();
    }
}
