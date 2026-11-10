// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using static DemaConsulting.CanvasNet.Mermaid.State.StateTokens;

namespace DemaConsulting.CanvasNet.Mermaid.State;

/// <summary>
///     The state parser: a hand-written port of Mermaid's <c>stateDiagram.jison</c> (its lexer and its grammar) that builds
///     Mermaid's statement tree and hands it to a <see cref="StateModel"/>, as the grammar's <c>setRootDoc</c> action does.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The 80 lexer rules and the start conditions of the generated jison lexer are ported one to one
///         (first matching rule wins, case-insensitive), with hand-written matchers in place of the regular expressions, so
///         ids, descriptions, notes, strings and keywords split exactly as in Mermaid, quirks included: a line that
///         contains <c>direction LR</c> anywhere is a direction statement, <c>--</c> is a concurrency divider only inside a
///         composite state, an id ends at a colon, a dash, a brace or white space, <c>%%</c> ends an id, and a name of two
///         words before a brace is Mermaid's "State name must be a single word" error.
///     </para>
///     <para>
///         <b>Grammar.</b> A recursive-descent parser with one token of lookahead accepts the language of the LALR(1)
///         grammar: the <c>stateDiagram</c> and <c>stateDiagram-v2</c> headers, states with descriptions and
///         <c>:::</c> classes, transitions with labels, <c>state "description" as id</c>, composite states (nested, with
///         their own direction and <c>--</c> regions), fork, join and choice states, notes (floating notes are read and
///         dropped), <c>classDef</c>, <c>class</c>, <c>style</c>, <c>click</c> and <c>click ... href</c>, the directions,
///         the accessibility statements, <c>hide empty description</c> and <c>scale</c> (both ignored). Statements are read
///         in loops; only composite states recurse. Each statement is checked against the lookahead set of the LALR state
///         where jison reduces it, so grammar errors come where Mermaid reports them.
///     </para>
///     <para>
///         <b>Errors.</b> A token the grammar does not accept is a located <see cref="MermaidSyntaxException"/> with code
///         <see cref="MermaidDiagnosticCodes.SyntaxError"/>, a hint, and as <see cref="MermaidSyntaxException.Expected"/>
///         the jison names of the tokens of the LALR state where jison reports the error, in jison's order. Text no rule
///         matches is a located lexical error. Composite states nested deeper than
///         <see cref="MermaidLimits.MaxNestingDepth"/> (the model counts their concurrency regions as levels too) and token
///         texts longer than <see cref="MermaidLimits.MaxLabelLength"/> are refused with
///         <see cref="MermaidDiagnosticCodes.LimitExceeded"/>.
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class StateParser
{
    /// <summary>The tokens that start a statement.</summary>
    private static readonly string[] StatementStarts =
    [
        HideEmpty, Scale, CompositState, StateDescr, Id, Fork, Join, Choice, Concurrent, Note, AccTitle, AccDescr,
        AccDescrMultilineValue, Click, ClassDef, StyleKeyword, ClassKeyword, DirectionTb, DirectionBt, DirectionRl,
        DirectionLr, EdgeState,
    ];

    /// <summary>The tokens that start a line of a document.</summary>
    private static readonly string[] LineStarts = [Space, Nl, .. StatementStarts];

    /// <summary>The tokens that may follow a statement (the lookahead set of its reduction).</summary>
    private static readonly string[] StatementFollowers = [.. LineStarts, StructStop];

    /// <summary>The tokens that may follow the id of a state where a description or transition may come.</summary>
    private static readonly string[] StateFollowers = [.. StatementFollowers, Descr, Arrow];

    /// <summary>The tokens that may follow the second state of a transition.</summary>
    private static readonly string[] TransitionFollowers = [.. StatementFollowers, Descr];

    /// <summary>The tokens that may follow an id statement in any context (its merged lookahead set).</summary>
    private static readonly string[] IdFollowers = [.. StateFollowers, Str, Href];

    /// <summary>The tokens that may follow the name of a composite state.</summary>
    private static readonly string[] CompositeFollowers = [.. StatementFollowers, StructStart];

    /// <summary>The model the statement tree is handed to.</summary>
    private readonly StateModel _model;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The lexer.</summary>
    private SourceScanner _scanner = null!;

    /// <summary>The token cursor.</summary>
    private TokenCursor _cursor = null!;

    /// <summary>The current nesting depth of composite states.</summary>
    private int _depth;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="model">The model to fill.</param>
    /// <param name="limits">The limits of the open call.</param>
    private StateParser(StateModel model, MermaidLimits limits)
    {
        _model = model;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private ScanToken Current => _cursor.Current;

    /// <summary>
    ///     Parses a state diagram.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location and Mermaid id).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model, with the statement tree applied.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, when Mermaid's database fails on a statement, or when
    ///     a limit is exceeded.
    /// </exception>
    public static StateModel Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var model = new StateModel(limits, detected.Location, detected.MermaidId);
        new StateParser(model, limits).Run(source.ParserText);
        return model;
    }

    /// <summary>
    ///     Parses the whole text (<c>start</c>): line breaks, the keyword, and the statements up to the end of the tokens,
    ///     then hands the statements to the model.
    /// </summary>
    /// <param name="text">The text.</param>
    private void Run(MappedText text)
    {
        _scanner = new SourceScanner(text, CreateRules(), Conditions);
        _cursor = Lex(() => new TokenCursor(_scanner));
        while (Is(Nl))
        {
            Advance();
        }

        if (!Is(Sd))
        {
            throw Fail([Space, Nl, Sd], "start the diagram with the stateDiagram-v2 keyword");
        }

        Advance();
        _model.SetRootDocument(ParseDocument(inStruct: false));
    }

    /// <summary>
    ///     Parses the lines of a document (<c>document</c>), up to the end of the tokens or, inside a composite state, up to
    ///     its <c>}</c>.
    /// </summary>
    /// <param name="inStruct">Whether the document is the body of a composite state.</param>
    /// <returns>The statements; empty lines and statements the grammar keeps as the text <c>nl</c> are left out, as in Mermaid.</returns>
    private List<StateStatement> ParseDocument(bool inStruct)
    {
        var document = new List<StateStatement>();
        while (true)
        {
            if (Is(Nl))
            {
                Advance();
                CheckEnd();
            }
            else if (Is(StatementStarts))
            {
                var statement = ParseStatement();
                if (statement.Stmt is not null || statement.Text != "nl")
                {
                    document.Add(statement);
                }
            }
            else if (inStruct ? Is(StructStop) : IsEnd())
            {
                return document;
            }
            else
            {
                throw Fail(inStruct ? StatementFollowers : LineStarts);
            }
        }
    }

    /// <summary>
    ///     Parses one statement.
    /// </summary>
    /// <returns>The statement.</returns>
    private StateStatement ParseStatement()
    {
        var token = Current;
        var location = Locate(token);
        switch (token.Kind)
        {
            case ClassDef:
                {
                    Advance();
                    var id = ExpectAny(ClassDefId, Default);
                    var styles = Expect(ClassDefStyleOpts);
                    CheckEnd();
                    return new StateStatement
                    {
                        Stmt = "classDef",
                        Id = Preprocessor.TrimJs(TextOf(id)),
                        Styles = Preprocessor.TrimJs(TextOf(styles)),
                        Location = location,
                    };
                }

            case StyleKeyword:
                {
                    Advance();
                    var ids = Expect(StyleIds);
                    var styles = Expect(StyleDefStyleOpts);
                    CheckEnd();
                    return new StateStatement
                    {
                        Stmt = "style",
                        Id = Preprocessor.TrimJs(TextOf(ids)),
                        StyleClass = Preprocessor.TrimJs(TextOf(styles)),
                        Location = location,
                    };
                }

            case ClassKeyword:
                {
                    Advance();
                    var ids = Expect(ClassEntityIds);
                    var name = Expect(StyleClass);
                    CheckEnd();
                    return new StateStatement
                    {
                        Stmt = "applyClass",
                        Id = Preprocessor.TrimJs(TextOf(ids)),
                        StyleClass = Preprocessor.TrimJs(TextOf(name)),
                        Location = location,
                    };
                }

            case Id or EdgeState:
                return ParseStateOrTransition();
            case HideEmpty:
                Advance();
                CheckEnd();
                return Kept(token.Text, location);
            case Scale:
                Advance();
                TextOf(Expect(Width));
                CheckEnd();
                return Kept(token.Text, location);
            case CompositState:
                return ParseCompositeState();
            case StateDescr:
                return ParseDescribedState();
            case Fork or Join or Choice:
                Advance();
                CheckEnd();
                return new StateStatement { Stmt = "state", Id = TextOf(token), Type = token.Kind.ToLowerInvariant(), Location = location };
            case Concurrent:
                Advance();
                CheckEnd();
                return new StateStatement { Stmt = "state", Id = _model.NextDividerId(), Type = StateModel.DividerType, Location = location };
            case Note:
                return ParseNote();
            case DirectionTb or DirectionBt or DirectionRl or DirectionLr:
                Advance();
                CheckEnd();
                return new StateStatement { Stmt = "dir", Value = token.Kind[^2..].ToUpperInvariant(), Location = location };
            case AccTitle:
                {
                    Advance();
                    var title = Preprocessor.TrimJs(TextOf(Expect(AccTitleValue)));
                    CheckEnd();
                    _model.SetAccessibleTitle(title);
                    return Kept(title, location);
                }

            case AccDescr:
                {
                    Advance();
                    var description = Preprocessor.TrimJs(TextOf(Expect(AccDescrValue)));
                    CheckEnd();
                    _model.SetAccessibleDescription(description);
                    return Kept(description, location);
                }

            case AccDescrMultilineValue:
                {
                    Advance();
                    CheckEnd();
                    var description = Preprocessor.TrimJs(TextOf(token));
                    _model.SetAccessibleDescription(description);
                    return Kept(description, location);
                }

            default:
                return ParseClick();
        }
    }

    /// <summary>
    ///     Parses a statement that starts with a state: the state alone, the state with a description
    ///     (<c>id : description</c>), or a transition (<c>id --&gt; id [: label]</c>).
    /// </summary>
    /// <returns>The statement.</returns>
    private StateStatement ParseStateOrTransition()
    {
        var first = ParseIdStatement();
        CheckLookahead(StateFollowers, endAllowed: true);
        if (Is(Descr))
        {
            var description = Advance();
            CheckEnd();
            first.Description = StateModel.TrimColon(TextOf(description));
            return first;
        }

        if (!Is(Arrow))
        {
            return first;
        }

        Advance();
        if (!Is(Id, EdgeState))
        {
            throw Fail([Id, EdgeState], "write the state the transition goes to: a state id or [*]");
        }

        var second = ParseIdStatement();
        CheckLookahead(TransitionFollowers, endAllowed: true);
        string? label = null;
        if (Is(Descr))
        {
            var description = Advance();
            CheckEnd();
            label = StateModel.TrimColon(TextOf(description));
        }

        return new StateStatement { Stmt = "relation", State1 = first, State2 = second, Description = label, Location = first.Location };
    }

    /// <summary>
    ///     Parses an id statement (<c>idStatement</c>): an id or <c>[*]</c>, with an optional <c>:::class</c>.
    /// </summary>
    /// <returns>The state statement.</returns>
    private StateStatement ParseIdStatement()
    {
        var token = Advance();
        var statement = new StateStatement
        {
            Stmt = "state",
            Id = Preprocessor.TrimJs(TextOf(token)),
            Type = StateModel.DefaultType,
            Description = string.Empty,
            Location = Locate(token),
        };
        if (!Is(StyleSeparator))
        {
            CheckLookahead([.. IdFollowers, StyleSeparator], endAllowed: true);
            return statement;
        }

        Advance();
        var name = Expect(Id);
        CheckLookahead(IdFollowers, endAllowed: true);
        return new StateStatement
        {
            Stmt = "state",
            Id = statement.Id,
            Type = StateModel.DefaultType,
            Description = string.Empty,
            Classes = [Preprocessor.TrimJs(TextOf(name))],
            Location = statement.Location,
        };
    }

    /// <summary>
    ///     Parses <c>state name</c>, which is kept as text and ignored, or a composite state <c>state name { ... }</c>.
    /// </summary>
    /// <returns>The statement.</returns>
    private StateStatement ParseCompositeState()
    {
        var name = Advance();
        var location = Locate(name);
        CheckLookahead(CompositeFollowers, endAllowed: true);
        if (!Is(StructStart))
        {
            return Kept(TextOf(name), location);
        }

        var document = ParseBody();
        return new StateStatement
        {
            Stmt = "state",
            Id = TextOf(name),
            Type = StateModel.DefaultType,
            Description = string.Empty,
            Doc = document,
            Location = location,
        };
    }

    /// <summary>
    ///     Parses <c>state "description" as id</c>, with an optional body that makes it a composite state titled with the
    ///     description; a colon in the id of a state without a body splits off a second description.
    /// </summary>
    /// <returns>The statement.</returns>
    private StateStatement ParseDescribedState()
    {
        var descriptionToken = Advance();
        var location = Locate(descriptionToken);
        var description = TextOf(descriptionToken);
        Expect(As);
        var id = TextOf(Expect(Id));
        CheckLookahead(CompositeFollowers, endAllowed: true);
        if (Is(StructStart))
        {
            var document = ParseBody();
            return new StateStatement { Stmt = "state", Id = id, Type = StateModel.DefaultType, Description = description, Doc = document, Location = location };
        }

        var trimmed = Preprocessor.TrimJs(description);
        if (!id.Contains(':', StringComparison.Ordinal))
        {
            return new StateStatement { Stmt = "state", Id = id, Type = StateModel.DefaultType, Description = trimmed, Location = location };
        }

        var parts = id.Split(':');
        return new StateStatement
        {
            Stmt = "state",
            Id = parts[0],
            Type = StateModel.DefaultType,
            DescriptionParts = [trimmed, parts[1]],
            Location = location,
        };
    }

    /// <summary>
    ///     Parses the body of a composite state, from its <c>{</c> to its <c>}</c>, and checks the statement end after it.
    /// </summary>
    /// <returns>The statements of the body.</returns>
    private List<StateStatement> ParseBody()
    {
        var open = Advance();
        _depth++;
        if (_depth > _limits.MaxNestingDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"composite states are nested deeper than {_limits.MaxNestingDepth} levels (MermaidLimits.MaxNestingDepth)"),
                Locate(open),
                "nest fewer composite states, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
        }

        var document = ParseDocument(inStruct: true);
        Advance();
        _depth--;
        CheckEnd();
        return document;
    }

    /// <summary>
    ///     Parses a note: <c>note left of id : text</c> or a multi-line note, or a floating note
    ///     (<c>note "text" as id</c>), which Mermaid reads and drops.
    /// </summary>
    /// <returns>The statement.</returns>
    private StateStatement ParseNote()
    {
        var keyword = Advance();
        var location = Locate(keyword);
        if (Is(LeftOf, RightOf))
        {
            var position = Advance();
            var id = Expect(Id);
            var text = Expect(NoteText);
            CheckEnd();
            return new StateStatement
            {
                Stmt = "state",
                Id = Preprocessor.TrimJs(TextOf(id)),
                Note = new StateNote { Position = Preprocessor.TrimJs(position.Text), Text = Preprocessor.TrimJs(TextOf(text)) },
                Location = location,
            };
        }

        if (!Is(NoteText))
        {
            throw Fail([NoteText, LeftOf, RightOf], "write the position of the note: note left of State : text, or note right of State : text");
        }

        TextOf(Advance());
        Expect(As);
        TextOf(Expect(Id));
        CheckEnd();
        return Kept(keyword.Text, location);
    }

    /// <summary>
    ///     Parses a click statement: <c>click id "url" "tooltip"</c> or <c>click id href "url"</c>, each ended by a line
    ///     break; the link is kept and never drawn.
    /// </summary>
    /// <returns>The statement.</returns>
    private StateStatement ParseClick()
    {
        var keyword = Advance();
        var location = Locate(keyword);
        if (!Is(Id, EdgeState))
        {
            throw Fail([Id, EdgeState], "write the state of the click: click State \"url\" \"tooltip\"");
        }

        var target = ParseIdStatement();
        string url;
        var tooltip = string.Empty;
        if (Is(Str))
        {
            url = TextOf(Advance());
            tooltip = TextOf(Expect(Str));
        }
        else if (Is(Href))
        {
            Advance();
            url = TextOf(Expect(Str));
        }
        else
        {
            throw Fail([Str, Href], "write the link of the click in double quotes: click State \"url\" or click State href \"url\"");
        }

        Expect(Nl);
        CheckEnd();
        return new StateStatement { Stmt = "click", Target = target, Url = url, Tooltip = tooltip, Location = location };
    }

    /// <summary>
    ///     Creates a statement the grammar keeps as text (its value is a string), which the database ignores.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location.</param>
    /// <returns>The statement.</returns>
    private static StateStatement Kept(string text, MermaidLocation location) => new() { Text = text, Location = location };

    /// <summary>
    ///     Checks that the lookahead may follow a whole statement (the lookahead set of its reduction).
    /// </summary>
    private void CheckEnd() => CheckLookahead(StatementFollowers, endAllowed: true);

    /// <summary>
    ///     Checks the lookahead against the set a reduction is decided on, and fails with the set otherwise, as jison
    ///     lists it.
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
    ///     Gets the text of a token that is kept, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
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
    ///     Consumes the lookahead token when it is of the kind, and fails otherwise.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The token.</returns>
    private ScanToken Expect(string kind) => Is(kind) ? Advance() : throw Fail([kind]);

    /// <summary>
    ///     Consumes the lookahead token when it is of one of the kinds, and fails otherwise with all of them expected.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>The token.</returns>
    private ScanToken ExpectAny(params string[] kinds) => Is(kinds) ? Advance() : throw Fail(kinds);

    /// <summary>
    ///     Runs a step that lexes a token, and gives a lexical error a hint for state diagrams.
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
        catch (MermaidSyntaxException ex) when (ex.Code == MermaidDiagnosticCodes.SyntaxError && ex.Expected.Count == 0 && ex.Hint != StateWordsHint)
        {
            // The scanner stops at the text no rule matches
            throw new MermaidSyntaxException(
                ex.Code,
                $"unrecognized text {SourceScanner.Describe(_scanner.Input.Text, _scanner.Position)}",
                ex.Location,
                "close a multi-line note with 'end note', end 'scale' with 'width', and end every keyword statement on its line");
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
            .OrderBy(StateTokens.Number)
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
    /// <param name="expected">The jison names of the expected tokens.</param>
    /// <returns>The hint.</returns>
    private static string HintFor(ScanToken token, List<string> expected)
    {
        if (token.Kind == DefaultClassDefId)
        {
            return "a state diagram cannot redefine the default class; give the class another name";
        }

        if (token.Kind == Invalid)
        {
            return "a state id has no dashes or spaces, and '--' divides concurrent regions only inside a composite state";
        }

        if (token.Kind == SourceScanner.EndOfInput && expected.Contains(StructStop))
        {
            return "close every composite state with '}'";
        }

        return "check the statement at this position against the state diagram syntax";
    }
}
