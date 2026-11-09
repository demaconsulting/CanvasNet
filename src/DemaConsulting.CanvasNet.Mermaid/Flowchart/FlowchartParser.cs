// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <summary>
///     The flowchart parser: a hand-written port of Mermaid's <c>flow.jison</c> (its lexer and its grammar), filling a
///     <see cref="FlowchartModel"/> with the same calls, in the same order, as Mermaid's parser makes on its database.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The 122 lexer rules and 21 start conditions of the generated jison lexer are ported one to one
///         (first matching rule wins), with hand-written matchers in place of the regular expressions; the rules that
///         backtrack or look far ahead (link ids, <c>direction</c> lines, links) are matched as the expressions would
///         match them, with caches that keep lexing linear.
///     </para>
///     <para>
///         <b>Grammar.</b> A recursive-descent parser with one token of lookahead accepts the language of the LALR(1)
///         grammar: the header (<c>graph</c>/<c>flowchart</c> and a direction), statements separated by new lines or
///         semicolons, vertices with every bracket shape and <c>@{ ... }</c> shape data, <c>&amp;</c> chains, links of
///         every stroke, head and length with their text and ids, subgraphs (with titles, nesting and
///         <c>direction</c>), <c>style</c>, <c>classDef</c>, <c>class</c>, <c>:::</c>, <c>linkStyle</c>,
///         <c>click</c>/<c>call</c>/<c>href</c> and the accessibility statements. The database calls are made where
///         jison reduces the rules, so the vertex counter, the edge ids and the subgraph members are Mermaid's.
///     </para>
///     <para>
///         <b>Errors.</b> A token the grammar does not accept is a located <see cref="MermaidSyntaxException"/> with code
///         <see cref="MermaidDiagnosticCodes.SyntaxError"/>, a hint, and as <see cref="MermaidSyntaxException.Expected"/>
///         the jison names of the tokens that would have been accepted at that point, in jison's order. Text no rule
///         matches is a located lexical error. Subgraphs nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/>
///         are refused with <see cref="MermaidDiagnosticCodes.LimitExceeded"/>.
///     </para>
///     <para>
///         An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class FlowchartParser
{
    /// <summary>The tokens that start or continue an id (<c>idStringToken</c>).</summary>
    private static readonly string[] IdTokens =
    [
        FlowchartTokens.Amp, FlowchartTokens.Colon, FlowchartTokens.Down, FlowchartTokens.Default, FlowchartTokens.Num,
        FlowchartTokens.Comma, FlowchartTokens.NodeString, FlowchartTokens.Brkt, FlowchartTokens.Minus,
        FlowchartTokens.Mult, FlowchartTokens.UnicodeText,
    ];

    /// <summary>The tokens that end a statement (<c>separator</c>).</summary>
    private static readonly string[] Separators = [FlowchartTokens.Newline, FlowchartTokens.Semi, FlowchartTokens.Eof];

    /// <summary>
    ///     The tokens that may follow a label: the closing brackets of every shape, the pipe of a link text and more label
    ///     text (the lookahead set of jison's <c>text</c> rule).
    /// </summary>
    private static readonly string[] TextFollowers =
    [
        FlowchartTokens.Sqe, FlowchartTokens.DoubleCircleEnd, FlowchartTokens.Pe, FlowchartTokens.EllipseEnd,
        FlowchartTokens.StadiumEnd, FlowchartTokens.SubroutineEnd, FlowchartTokens.Pipe, FlowchartTokens.CylinderEnd,
        FlowchartTokens.DiamondStop, FlowchartTokens.TagEnd, FlowchartTokens.TrapEnd, FlowchartTokens.InvTrapEnd,
        FlowchartTokens.UnicodeText, FlowchartTokens.Text, FlowchartTokens.TagStart,
    ];

    /// <summary>The model the parser fills.</summary>
    private readonly FlowchartModel _model;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The token kinds that would have been accepted since the last token was consumed.</summary>
    private readonly HashSet<string> _expected = new(StringComparer.Ordinal);

    /// <summary>The token cursor.</summary>
    private TokenCursor _cursor = null!;

    /// <summary>The current subgraph nesting depth.</summary>
    private int _depth;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="model">The model to fill.</param>
    /// <param name="limits">The limits of the open call.</param>
    private FlowchartParser(FlowchartModel model, MermaidLimits limits)
    {
        _model = model;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private ScanToken Current => _cursor.Current;

    /// <summary>
    ///     Parses a flowchart.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text does not follow the grammar or exceeds a limit.</exception>
    public static FlowchartModel Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var model = new FlowchartModel(limits, detected.Location);
        new FlowchartParser(model, limits).Run(TrimSpaceAfterBraces(source.ParserText));
        return model;
    }

    /// <summary>
    ///     Removes the white space between a closing brace and the line feed that ends its line, as Mermaid's flowchart
    ///     parser does before it parses (<c>src.replace(/}\s*\n/g, '}\n')</c>).
    /// </summary>
    /// <param name="text">The parser text.</param>
    /// <returns>The text without that white space, still mapped to the original.</returns>
    internal static MappedText TrimSpaceAfterBraces(MappedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new MappedText.Builder(text);
        var s = text.Text;
        var i = 0;
        while (i < s.Length)
        {
            var brace = s.IndexOf('}', i);
            if (brace < 0)
            {
                builder.Append(i, s.Length - i);
                break;
            }

            // Keep the text up to the brace; drop the white space up to the last line feed of the run after it
            builder.Append(i, brace + 1 - i);
            var end = SkipSpaces(s, brace + 1);
            var lineFeed = s.LastIndexOf('\n', end - 1, end - brace - 1);
            if (end > brace + 1 && lineFeed > brace)
            {
                builder.Append(lineFeed, 1);
                i = lineFeed + 1;
            }
            else
            {
                i = brace + 1;
            }
        }

        return builder.Build(text.OriginOf(text.Length));
    }

    /// <summary>
    ///     Parses the whole text: the header, then the statements up to the end.
    /// </summary>
    /// <param name="text">The text.</param>
    private void Run(MappedText text)
    {
        _cursor = new TokenCursor(CreateScanner(text));
        ParseHeader();
        ParseDocument(inSubgraph: false);
    }

    /// <summary>
    ///     Parses the header (<c>graphConfig</c>): white space and line breaks, the keyword, and either no direction or a
    ///     direction and the first separator.
    /// </summary>
    private void ParseHeader()
    {
        while (Is(FlowchartTokens.Space, FlowchartTokens.Newline))
        {
            Advance();
        }

        Expect(FlowchartTokens.Graph);
        if (Is(FlowchartTokens.NoDir))
        {
            Advance();
            _model.SetDirection("TB");
            return;
        }

        if (!Is(FlowchartTokens.Dir))
        {
            throw Fail("write a direction after the keyword (TB, TD, BT, LR or RL) and end the line, for example 'flowchart LR'");
        }

        var direction = Advance().Text;

        // FirstStmtSeparator: a semicolon, a line break, or spaces and a line break
        if (Is(FlowchartTokens.Semi, FlowchartTokens.Newline))
        {
            Advance();
        }
        else if (Is(FlowchartTokens.Space))
        {
            SkipSpaceList();
            Expect(FlowchartTokens.Newline);
        }
        else
        {
            throw Fail("end the header line after the direction");
        }

        _model.SetDirection(direction);
    }

    /// <summary>
    ///     Parses statements (<c>document</c>) up to the end of the text, or up to the <c>end</c> of a subgraph.
    /// </summary>
    /// <param name="inSubgraph">Whether the statements are inside a subgraph.</param>
    /// <returns>The values of the statements, for the subgraph's members.</returns>
    private List<DocumentItem> ParseDocument(bool inSubgraph)
    {
        var items = new List<DocumentItem>();
        var afterLine = false;
        while (true)
        {
            // Empty lines: their token text is a value too (only blank ones are dropped later)
            if (Is(FlowchartTokens.Semi, FlowchartTokens.Newline, FlowchartTokens.Space, FlowchartTokens.Eof))
            {
                items.Add(new DocumentItem(Advance().Text, null));
                afterLine = true;
                continue;
            }

            if (inSubgraph ? Is(FlowchartTokens.End) : IsEnd())
            {
                return items;
            }

            if (TryParseStatement(items))
            {
                afterLine = true;
            }
            else
            {
                // After a line, jison's state is shared with the statements of subgraphs, so it lists 'end' too
                if (afterLine && Current.Kind != FlowchartTokens.End)
                {
                    _expected.Add(FlowchartTokens.End);
                }

                throw Fail(inSubgraph && Current.Kind == SourceScanner.EndOfInput
                    ? "close the subgraph with 'end' on a line of its own"
                    : null);
            }
        }
    }

    /// <summary>
    ///     Parses one statement when the lookahead starts one.
    /// </summary>
    /// <param name="items">The statement values, to which the statement's value is added.</param>
    /// <returns>Whether a statement was parsed.</returns>
    private bool TryParseStatement(List<DocumentItem> items)
    {
        var start = Current;
        var location = _cursor.Scanner.Locate(start.Start);
        if (Is(FlowchartTokens.Subgraph))
        {
            items.Add(new DocumentItem(ParseSubgraph(), null));
        }
        else if (Is(FlowchartTokens.AccTitle))
        {
            Advance();
            var text = Preprocessor.TrimJs(Expect(FlowchartTokens.AccTitleValue).Text);
            _model.SetAccessibleTitle(text, location);
            items.Add(new DocumentItem(text, null));
        }
        else if (Is(FlowchartTokens.AccDescr))
        {
            Advance();
            var text = Preprocessor.TrimJs(Expect(FlowchartTokens.AccDescrValue).Text);
            _model.SetAccessibleDescription(text, location);
            items.Add(new DocumentItem(text, null));
        }
        else if (Is(FlowchartTokens.AccDescrMultilineValue))
        {
            var text = Preprocessor.TrimJs(Advance().Text);
            _model.SetAccessibleDescription(text, location);
            items.Add(new DocumentItem(text, null));
        }
        else if (Is(FlowchartTokens.Style))
        {
            ParseStyleStatement(location);
        }
        else if (Is(FlowchartTokens.LinkStyle))
        {
            ParseLinkStyleStatement(location);
        }
        else if (Is(FlowchartTokens.ClassDef))
        {
            ParseClassDefStatement();
        }
        else if (Is(FlowchartTokens.Class))
        {
            ParseClassStatement();
        }
        else if (Is(FlowchartTokens.Click))
        {
            ParseClickStatement();
        }
        else if (Is(FlowchartTokens.DirectionTb, FlowchartTokens.DirectionBt, FlowchartTokens.DirectionRl, FlowchartTokens.DirectionLr, FlowchartTokens.DirectionTd))
        {
            items.Add(new DocumentItem(null, Advance().Kind[^2..].ToUpperInvariant()));
        }
        else if (Is(IdTokens))
        {
            var nodes = ParseVertexStatement();
            ExpectSeparator();
            items.AddRange(nodes.Select(id => new DocumentItem(id, null)));
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Parses a subgraph: <c>subgraph</c>, an optional id and title, a separator, the statements and <c>end</c>.
    /// </summary>
    /// <returns>The subgraph id.</returns>
    private string ParseSubgraph()
    {
        var keyword = Advance();
        var location = _cursor.Scanner.Locate(keyword.Start);
        _depth++;
        if (_depth > _limits.MaxNestingDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"subgraphs are nested deeper than {_limits.MaxNestingDepth} levels (MermaidLimits.MaxNestingDepth)"),
                location,
                "nest fewer subgraphs, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
        }

        FlowText? id = null;
        FlowText? title = null;
        var titleIsId = false;
        if (Is(FlowchartTokens.Space))
        {
            Advance();
            id = ParseTextNoTags();
            if (Is(FlowchartTokens.Sqs))
            {
                Advance();
                title = ParseText();
                Expect(FlowchartTokens.Sqe);
            }
            else
            {
                title = id;
                titleIsId = true;
            }
        }

        ExpectSeparator();
        var items = ParseDocument(inSubgraph: true);
        Expect(FlowchartTokens.End);
        _depth--;
        return _model.AddSubgraph(id, items, title, titleIsId, location);
    }

    /// <summary>
    ///     Parses a subgraph title without brackets (<c>textNoTags</c>): a quoted or markdown string, or words, keywords
    ///     and spaces, possibly followed by more of them.
    /// </summary>
    /// <returns>The text; a quoted string has the type <c>text</c>, as in Mermaid.</returns>
    private FlowText ParseTextNoTags()
    {
        var builder = new StringBuilder();
        string type;
        if (Is(FlowchartTokens.MdStr))
        {
            builder.Append(Advance().Text);
            type = "markdown";
        }
        else if (Is(FlowchartTokens.Str) || IsTextNoTagsToken())
        {
            builder.Append(Advance().Text);
            type = "text";
        }
        else
        {
            throw Fail("write the subgraph id or title after the keyword");
        }

        while (IsTextNoTagsToken())
        {
            builder.Append(Advance().Text);
        }

        return new FlowText(builder.ToString(), type);
    }

    /// <summary>
    ///     Gets a value indicating whether the lookahead is a <c>textNoTagsToken</c>: a word, a number, a keyword, a space
    ///     or one of a few punctuation tokens.
    /// </summary>
    /// <returns>Whether it is.</returns>
    private bool IsTextNoTagsToken() => Is(
        FlowchartTokens.Num, FlowchartTokens.NodeString, FlowchartTokens.Space, FlowchartTokens.Minus, FlowchartTokens.Amp,
        FlowchartTokens.UnicodeText, FlowchartTokens.Colon, FlowchartTokens.Mult, FlowchartTokens.Brkt,
        FlowchartTokens.StartLink, FlowchartTokens.Style, FlowchartTokens.LinkStyle, FlowchartTokens.ClassDef,
        FlowchartTokens.Class, FlowchartTokens.Click, FlowchartTokens.Graph, FlowchartTokens.Dir, FlowchartTokens.Subgraph,
        FlowchartTokens.End, FlowchartTokens.Down, FlowchartTokens.Up);

    /// <summary>
    ///     Parses a label (<c>text</c>): a quoted or markdown string, or label text, possibly followed by more label text.
    /// </summary>
    /// <returns>The text with its type (<c>string</c>, <c>markdown</c> or <c>text</c>).</returns>
    private FlowText ParseText()
    {
        var builder = new StringBuilder();
        string type;
        if (Is(FlowchartTokens.Str))
        {
            builder.Append(Advance().Text);
            type = "string";
        }
        else if (Is(FlowchartTokens.MdStr))
        {
            builder.Append(Advance().Text);
            type = "markdown";
        }
        else if (IsTextToken())
        {
            builder.Append(Advance().Text);
            type = "text";
        }
        else
        {
            throw Fail("write the label text; quote text that contains brackets or quotes, for example A[\"a [b] c\"]");
        }

        while (IsTextToken())
        {
            builder.Append(Advance().Text);
        }

        // jison's state after a label is shared by every shape, so a token that closes none lists all their closers
        if (!TextFollowers.Contains(Current.Kind))
        {
            _expected.UnionWith(TextFollowers);
        }

        return new FlowText(builder.ToString(), type);
    }

    /// <summary>
    ///     Gets a value indicating whether the lookahead is a <c>textToken</c>.
    /// </summary>
    /// <returns>Whether it is.</returns>
    private bool IsTextToken() =>
        Is(FlowchartTokens.TagEnd, FlowchartTokens.UnicodeText, FlowchartTokens.Text, FlowchartTokens.TagStart);

    /// <summary>
    ///     Consumes a statement separator: a line break, a semicolon or the end of the text.
    /// </summary>
    private void ExpectSeparator()
    {
        if (!Is(Separators))
        {
            throw Fail("end the statement with a line break or a semicolon");
        }

        Advance();
    }

    /// <summary>
    ///     Consumes one or more <c>SPACE</c> tokens (<c>spaceList</c>).
    /// </summary>
    private void SkipSpaceList()
    {
        // jison reduces a space list without listing SPACE as expected, so the loop does not record it
        Expect(FlowchartTokens.Space);
        while (Current.Kind == FlowchartTokens.Space)
        {
            Advance();
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the token stream has ended (jison's <c>$end</c>); the end is never listed as
    ///     expected.
    /// </summary>
    /// <returns>Whether it has.</returns>
    private bool IsEnd() => Current.Kind == SourceScanner.EndOfInput;

    /// <summary>
    ///     Gets a value indicating whether the lookahead is of one of the kinds, and records the kinds as expected when it
    ///     is not.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>Whether it is.</returns>
    private bool Is(params string[] kinds)
    {
        if (Array.IndexOf(kinds, Current.Kind) >= 0)
        {
            return true;
        }

        _expected.UnionWith(kinds);
        return false;
    }

    /// <summary>
    ///     Consumes the lookahead token.
    /// </summary>
    /// <returns>The token.</returns>
    private ScanToken Advance()
    {
        _expected.Clear();
        return _cursor.Advance();
    }

    /// <summary>
    ///     Consumes the lookahead token when it is of one of the kinds, and fails otherwise.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>The token.</returns>
    private ScanToken Expect(params string[] kinds) => Is(kinds) ? Advance() : throw Fail();

    /// <summary>
    ///     Creates the exception for a lookahead token the grammar does not accept, with the expected tokens recorded since
    ///     the last token was consumed.
    /// </summary>
    /// <param name="hint">The suggested fix, or null for a hint from the context.</param>
    /// <returns>The located exception.</returns>
    private MermaidSyntaxException Fail(string? hint = null)
    {
        var expected = _expected
            .OrderBy(FlowchartTokens.Number)
            .ThenBy(kind => kind, StringComparer.Ordinal)
            .Select(FlowchartTokens.JisonName)
            .ToList();
        var token = Current;
        var found = token.Kind == SourceScanner.EndOfInput
            ? "the end of the text"
            : $"'{FlowchartTokens.JisonName(token.Kind)}' ({Describe(token.Text)})";
        return new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"expected {TokenCursor.JoinAlternatives([.. expected.Select(e => $"'{e}'")])}, found {found}",
            _cursor.Scanner.Locate(token.Start),
            hint ?? HintFor(token, expected),
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
        return shown.Length == 0 ? "no text" : "'" + shown.Replace("\n", "\\n", StringComparison.Ordinal) + "'";
    }

    /// <summary>
    ///     Suggests a fix for an unexpected token from what was expected.
    /// </summary>
    /// <param name="token">The unexpected token.</param>
    /// <param name="expected">The jison names of the expected tokens.</param>
    /// <returns>The hint.</returns>
    private static string HintFor(ScanToken token, List<string> expected)
    {
        string[] closers = ["SQE", "PE", "DIAMOND_STOP", "STADIUMEND", "SUBROUTINEEND", "CYLINDEREND", "DOUBLECIRCLEEND", "TRAPEND", "INVTRAPEND", "-)"];
        if (expected.Exists(e => closers.Contains(e)))
        {
            return "close the label with the bracket that matches its opening bracket, or quote text that contains brackets: A[\"a [b] c\"]";
        }

        if (expected.Contains("LINK"))
        {
            return "finish the link text with an arrow that matches its start, for example A-- text -->B";
        }

        if (token.Kind == SourceScanner.EndOfInput)
        {
            return "the text ends in the middle of a statement; complete it";
        }

        return "check the statement at this position against the flowchart syntax; quote labels that contain special characters";
    }
}
