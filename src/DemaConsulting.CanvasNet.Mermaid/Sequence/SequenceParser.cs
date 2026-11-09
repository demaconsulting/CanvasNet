// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     The sequence parser: a hand-written port of Mermaid's <c>sequenceDiagram.jison</c> (its lexer and its grammar),
///     producing the statements that Mermaid's grammar hands to its database and applying them to a
///     <see cref="SequenceModel"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The 94 lexer rules and 8 start conditions of the generated jison lexer are ported one to one
///         (first matching rule wins, ASCII letters in any case), with hand-written matchers in place of the regular
///         expressions, so names, texts, comments and keywords split exactly as in Mermaid.
///     </para>
///     <para>
///         <b>Grammar.</b> A recursive-descent parser with one token of lookahead accepts the language of the LALR(1)
///         grammar: the header, participant statements (with <c>@{ ... }</c> metadata and aliases), <c>create</c>,
///         <c>destroy</c>, boxes, signals with every arrow and the <c>+</c>, <c>-</c> and <c>()</c> modifiers, notes,
///         activations, <c>autonumber</c>, <c>links</c>, <c>link</c>, <c>properties</c>, <c>details</c>, the title and
///         accessibility statements, and the blocks (<c>loop</c>, <c>rect</c>, <c>opt</c>, <c>alt</c>/<c>else</c>,
///         <c>par</c>/<c>and</c>, <c>par_over</c>, <c>critical</c>/<c>option</c>, <c>break</c>). Title and
///         accessibility texts are set as they are parsed; every other statement is listed and applied to the model once
///         the whole text has parsed, as Mermaid's <c>yy.apply</c>, so grammar errors come before database errors.
///     </para>
///     <para>
///         <b>Errors.</b> A token the grammar does not accept is a located <see cref="MermaidSyntaxException"/> with code
///         <see cref="MermaidDiagnosticCodes.SyntaxError"/>, a hint, and as <see cref="MermaidSyntaxException.Expected"/>
///         the jison names of the tokens jison lists at that point, in jison's order. Where jison's LALR states merge the
///         lookahead sets of several contexts (after a participant name, an arrow or a whole statement), the parser checks
///         the lookahead against the merged set first, so the error is reported in the same state as jison's. Text no rule
///         matches is a located lexical error. Blocks and boxes nested deeper than
///         <see cref="MermaidLimits.MaxNestingDepth"/> and texts longer than <see cref="MermaidLimits.MaxLabelLength"/> are
///         refused with <see cref="MermaidDiagnosticCodes.LimitExceeded"/>.
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class SequenceParser
{
    /// <summary>The tokens before the keyword (<c>start</c>).</summary>
    private static readonly string[] StartTokens = [SequenceTokens.Space, SequenceTokens.Newline, SequenceTokens.Sd];

    /// <summary>The tokens that start a line of a document (<c>line</c>).</summary>
    private static readonly string[] LineStarts =
    [
        SequenceTokens.Space, SequenceTokens.Newline, SequenceTokens.Invalid, SequenceTokens.Create, SequenceTokens.Box,
        SequenceTokens.Autonumber, SequenceTokens.Activate, SequenceTokens.Deactivate, SequenceTokens.Title,
        SequenceTokens.LegacyTitle, SequenceTokens.AccTitle, SequenceTokens.AccDescr,
        SequenceTokens.AccDescrMultilineValue, SequenceTokens.Loop, SequenceTokens.Rect, SequenceTokens.Opt,
        SequenceTokens.Alt, SequenceTokens.Par, SequenceTokens.ParOver, SequenceTokens.Critical, SequenceTokens.Break,
        SequenceTokens.Participant, SequenceTokens.ParticipantActor, SequenceTokens.Destroy, SequenceTokens.Note,
        SequenceTokens.Links, SequenceTokens.Link, SequenceTokens.Properties, SequenceTokens.Details, SequenceTokens.Actor,
    ];

    /// <summary>
    ///     The tokens that may follow a whole line in any document (the merged lookahead of the states that end a
    ///     statement): a line start, or a keyword that ends or continues a block; the end of the tokens is accepted too.
    /// </summary>
    private static readonly string[] LineFollowers =
        [.. LineStarts, SequenceTokens.End, SequenceTokens.Else, SequenceTokens.And, SequenceTokens.Option];

    /// <summary>The arrow token kinds.</summary>
    private static readonly string[] Arrows = [.. SequenceTokens.ArrowRules.Select(rule => rule.Kind)];

    /// <summary>The tokens that may follow a participant name in a statement (the merged lookahead of <c>actor</c>).</summary>
    private static readonly string[] ActorFollowers =
        [SequenceTokens.Newline, SequenceTokens.Comma, SequenceTokens.Central, .. Arrows, SequenceTokens.Txt];

    /// <summary>The tokens after a participant name that may start a signal.</summary>
    private static readonly string[] SignalStarts = [SequenceTokens.Central, .. Arrows];

    /// <summary>The tokens that may follow an arrow (the merged lookahead of <c>signaltype</c>).</summary>
    private static readonly string[] ArrowFollowers =
        [SequenceTokens.Plus, SequenceTokens.Minus, SequenceTokens.Central, SequenceTokens.Actor];

    /// <summary>The tokens after the name of a <c>participant</c> or <c>actor</c> statement.</summary>
    private static readonly string[] DeclaredNameFollowers = [SequenceTokens.Newline, SequenceTokens.As, SequenceTokens.ConfigStart];

    /// <summary>The tokens after a declared participant: an alias, or the end of the statement.</summary>
    private static readonly string[] AliasOrNewline = [SequenceTokens.Newline, SequenceTokens.As];

    /// <summary>The tokens of the lines of a box (<c>box_line</c>) and its <c>end</c>.</summary>
    private static readonly string[] BoxLineTokens =
    [
        SequenceTokens.Space, SequenceTokens.Newline, SequenceTokens.End, SequenceTokens.Participant,
        SequenceTokens.ParticipantActor, SequenceTokens.Destroy,
    ];

    /// <summary>The keywords of a participant statement.</summary>
    private static readonly string[] ParticipantKeywords = [SequenceTokens.Participant, SequenceTokens.ParticipantActor, SequenceTokens.Destroy];

    /// <summary>
    ///     The blocks: the line types of their start, of their sections and of their end, and the keyword of their
    ///     sections (null for a block without sections).
    /// </summary>
    private static readonly Dictionary<string, (int Start, string? Section, int SectionType, int End)> Blocks = new(StringComparer.Ordinal)
    {
        [SequenceTokens.Loop] = (SequenceLineType.LoopStart, null, 0, SequenceLineType.LoopEnd),
        [SequenceTokens.Rect] = (SequenceLineType.RectStart, null, 0, SequenceLineType.RectEnd),
        [SequenceTokens.Opt] = (SequenceLineType.OptStart, null, 0, SequenceLineType.OptEnd),
        [SequenceTokens.Break] = (SequenceLineType.BreakStart, null, 0, SequenceLineType.BreakEnd),
        [SequenceTokens.Alt] = (SequenceLineType.AltStart, SequenceTokens.Else, SequenceLineType.AltElse, SequenceLineType.AltEnd),
        [SequenceTokens.Par] = (SequenceLineType.ParStart, SequenceTokens.And, SequenceLineType.ParAnd, SequenceLineType.ParEnd),
        [SequenceTokens.ParOver] = (SequenceLineType.ParOverStart, SequenceTokens.And, SequenceLineType.ParAnd, SequenceLineType.ParEnd),
        [SequenceTokens.Critical] = (SequenceLineType.CriticalStart, SequenceTokens.Option, SequenceLineType.CriticalOption, SequenceLineType.CriticalEnd),
    };

    /// <summary>The model the statements are applied to.</summary>
    private readonly SequenceModel _model;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The statements for the database, in Mermaid's order.</summary>
    private readonly List<SequenceStatement> _statements = [];

    /// <summary>The lexer.</summary>
    private SourceScanner _scanner = null!;

    /// <summary>The token cursor.</summary>
    private TokenCursor _cursor = null!;

    /// <summary>The current nesting depth of blocks and boxes.</summary>
    private int _depth;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="model">The model to fill.</param>
    /// <param name="limits">The limits of the open call.</param>
    private SequenceParser(SequenceModel model, MermaidLimits limits)
    {
        _model = model;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private ScanToken Current => _cursor.Current;

    /// <summary>
    ///     Parses a sequence diagram.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model, with every statement applied.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, when Mermaid's database refuses a statement, or when
    ///     a limit is exceeded.
    /// </exception>
    public static SequenceModel Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var model = new SequenceModel(limits, detected.Location);
        var parser = new SequenceParser(model, limits);
        parser.Run(source.ParserText);
        model.Apply(parser._statements);
        return model;
    }

    /// <summary>
    ///     Parses the whole text (<c>start</c>): line breaks, the keyword, then the statements up to the end.
    /// </summary>
    /// <param name="text">The text.</param>
    private void Run(MappedText text)
    {
        _scanner = CreateScanner(text);
        _cursor = Lex(() => new TokenCursor(_scanner));
        while (Is(SequenceTokens.Newline))
        {
            Advance();
        }

        if (!Is(SequenceTokens.Sd))
        {
            throw Fail(StartTokens, "start the diagram with the sequenceDiagram keyword");
        }

        Advance();
        ParseDocument([]);
    }

    /// <summary>
    ///     Parses lines (<c>document</c>) up to the end of the tokens, or up to a keyword that ends or continues the block
    ///     around them.
    /// </summary>
    /// <param name="stops">The keywords that end the document; none for the whole diagram, which ends with the tokens.</param>
    private void ParseDocument(string[] stops)
    {
        while (true)
        {
            if (Is(LineStarts))
            {
                ParseLine();
                CheckLookahead(LineFollowers, endAllowed: true);
            }
            else if (Is(stops) || (stops.Length == 0 && IsEnd()))
            {
                return;
            }
            else
            {
                throw Fail([.. LineStarts, .. stops]);
            }
        }
    }

    /// <summary>
    ///     Parses one line: a line break or invalid text (both ignored), or a statement.
    /// </summary>
    private void ParseLine()
    {
        if (Is(SequenceTokens.Newline, SequenceTokens.Invalid))
        {
            Advance();
            return;
        }

        ParseStatement();
    }

    /// <summary>
    ///     Checks the lookahead in a state that reduces on a merged lookahead set: a token outside the set fails there, with
    ///     the set as jison lists it.
    /// </summary>
    /// <param name="accepted">The set.</param>
    /// <param name="endAllowed">Whether the end of the tokens is in the set (jison never lists it).</param>
    private void CheckLookahead(string[] accepted, bool endAllowed = false)
    {
        if (!Is(accepted) && !(endAllowed && IsEnd()))
        {
            throw Fail(accepted);
        }
    }

    /// <summary>
    ///     Enters a block or box, and checks <see cref="MermaidLimits.MaxNestingDepth"/>.
    /// </summary>
    /// <param name="location">The location of its keyword.</param>
    private void Enter(MermaidLocation location)
    {
        _depth++;
        if (_depth > _limits.MaxNestingDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"blocks are nested deeper than {_limits.MaxNestingDepth} levels (MermaidLimits.MaxNestingDepth)"),
                location,
                "nest fewer blocks, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
        }
    }

    /// <summary>
    ///     Leaves a block or box.
    /// </summary>
    private void Leave() => _depth--;

    /// <summary>
    ///     Gets the text of a token that is a label or a name, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string TextOf(ScanToken token)
    {
        if (token.Text.Length > _limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {token.Text.Length} characters long, more than the limit of {_limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                Locate(token),
                "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }

        return token.Text;
    }

    /// <summary>
    ///     Gets the location of a token in the original text.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The location.</returns>
    private MermaidLocation Locate(ScanToken token) => _scanner.Locate(token.Start);

    /// <summary>
    ///     Gets a value indicating whether the token stream has ended (jison's <c>$end</c>).
    /// </summary>
    /// <returns>Whether it has.</returns>
    private bool IsEnd() => Current.Kind == SourceScanner.EndOfInput;

    /// <summary>
    ///     Gets a value indicating whether the lookahead is of one of the kinds.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>Whether it is.</returns>
    private bool Is(params string[] kinds) => Array.IndexOf(kinds, Current.Kind) >= 0;

    /// <summary>
    ///     Consumes the lookahead token.
    /// </summary>
    /// <returns>The token.</returns>
    private ScanToken Advance() => Lex(_cursor.Advance);

    /// <summary>
    ///     Consumes the lookahead token when it is of a kind, and fails otherwise.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The token.</returns>
    private ScanToken Expect(string kind) => Is(kind) ? Advance() : throw Fail([kind]);

    /// <summary>
    ///     Runs a step that lexes a token, and gives a lexical error a hint for sequence diagrams.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="step">The step.</param>
    /// <returns>The result of the step.</returns>
    private T Lex<T>(Func<T> step)
    {
        try
        {
            return step();
        }
        catch (MermaidSyntaxException ex) when (ex.Code == MermaidDiagnosticCodes.SyntaxError && ex.Expected.Count == 0)
        {
            // The scanner stops at the text no rule matches
            throw new MermaidSyntaxException(
                ex.Code,
                $"unrecognized text {SourceScanner.Describe(_scanner.Input.Text, _scanner.Position)}",
                ex.Location,
                "write a participant name after participant, actor, destroy, activate and deactivate, and close the @{ ... } of participant metadata");
        }
    }

    /// <summary>
    ///     Creates the exception for a lookahead token the grammar does not accept.
    /// </summary>
    /// <param name="expectedKinds">The tokens jison lists in the state of the error.</param>
    /// <param name="hint">The suggested fix, or null for a hint from the context.</param>
    /// <returns>The located exception.</returns>
    private MermaidSyntaxException Fail(IEnumerable<string> expectedKinds, string? hint = null)
    {
        var expected = expectedKinds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(SequenceTokens.Number)
            .ThenBy(kind => kind, StringComparer.Ordinal)
            .ToList();
        var token = Current;
        var found = token.Kind == SourceScanner.EndOfInput ? "the end of the text" : $"'{token.Kind}' ({Describe(token.Text)})";
        return new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"expected {TokenCursor.JoinAlternatives([.. expected.Select(e => $"'{e}'")])}, found {found}",
            Locate(token),
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
    /// <param name="expected">The expected token kinds.</param>
    /// <returns>The hint.</returns>
    private static string HintFor(ScanToken token, List<string> expected)
    {
        if (token.Kind == SourceScanner.EndOfInput && expected.Contains(SequenceTokens.End))
        {
            return "close every loop, alt, opt, par, critical, break, rect and box with 'end' on a line of its own";
        }

        if (token.Kind is SequenceTokens.End or SequenceTokens.Else or SequenceTokens.And or SequenceTokens.Option)
        {
            return "'else' continues an alt block, 'and' a par block and 'option' a critical block, and 'end' closes an open block";
        }

        if (expected.Contains("SOLID_ARROW"))
        {
            return "connect two participants with an arrow and end with a text, for example Alice->>Bob: Hello";
        }

        if (expected.Contains(SequenceTokens.Txt))
        {
            return "write the text after a colon, for example Alice->>Bob: Hello or Note over Alice: Thinking";
        }

        if (expected.Contains(SequenceTokens.Actor))
        {
            return "write a participant name; a name cannot contain <, >, :, commas or semicolons";
        }

        return "check the statement at this position against the sequence diagram syntax";
    }
}
