// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using static DemaConsulting.CanvasNet.Mermaid.Gantt.GanttTokens;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     The gantt parser: a hand-written port of Mermaid's <c>gantt.jison</c> (its lexer and its grammar) that fills a
///     <see cref="GanttModel"/> through the calls the grammar's actions make on Mermaid's gantt database.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The 53 lexer rules and the start conditions of the generated jison lexer are ported one to one
///         (first matching rule wins, case-insensitive), with hand-written matchers in place of the regular expressions, so
///         statements, tasks and comments split exactly as in Mermaid, quirks included: a statement's value runs to the end
///         of the line or to <c>;</c> (and <c>#</c> for most), a keyword may be followed by a line feed as its one white-space
///         character, any character but <c>}</c> followed by <c>%</c> starts a comment (a line feed included), a single
///         <c>%</c> starts a comment, a line that starts with <c>click</c>, <c>call</c> or <c>href "</c> is a click part
///         wherever it is, a task name runs to the first colon, and a date, <c>accDescription</c> or a lone colon at the
///         start of a statement is a token the grammar does not accept.
///     </para>
///     <para>
///         <b>Grammar.</b> A recursive-descent parser with one token of lookahead accepts the language of the LALR(1)
///         grammar: <c>gantt</c>, then statements (each one or two tokens, or a click statement of the seven forms), each
///         passed to the database as the grammar's action passes it, with the action's <c>substr</c> offsets (trailing white
///         space kept). After each token the lookahead is checked against the actions of the LALR state jison is in, so
///         grammar errors come where Mermaid reports them, with its expected tokens.
///     </para>
///     <para>
///         <b>Mermaid's own failures.</b> The <c>topAxis</c> statement calls a database function that does not exist, and a
///         task whose data holds only tags reads past the end of its list; Mermaid fails at both with a JavaScript error, and
///         so does the parser, with a located <see cref="MermaidSyntaxException"/>.
///     </para>
///     <para>
///         <b>Errors and limits.</b> A token the grammar does not accept is a located <see cref="MermaidSyntaxException"/>
///         with code <see cref="MermaidDiagnosticCodes.SyntaxError"/>, a hint, and as
///         <see cref="MermaidSyntaxException.Expected"/> the jison names of the tokens of the LALR state where jison reports
///         the error, in jison's order. Every task and section statement counts against <see cref="MermaidLimits.MaxNodes"/>,
///         and every kept token text (and the class list and link the model derives from them) is checked against
///         <see cref="MermaidLimits.MaxLabelLength"/> (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class GanttParser
{
    /// <summary>The tokens that start a line of the document (jison's state after <c>gantt document</c>).</summary>
    private static readonly string[] LineStarts =
    [
        Eof, Space, Newline, WeekdayMonday, WeekdayTuesday, WeekdayWednesday, WeekdayThursday, WeekdayFriday, WeekdaySaturday,
        WeekdaySunday, WeekendFriday, WeekendSaturday, DateFormat, InclusiveEndDates, TopAxis, AxisFormat, TickInterval, Excludes,
        Includes, TodayMarker, Title, AccTitle, AccDescr, AccDescrMultilineValue, Section, TaskTxt, Click,
    ];

    /// <summary>The model the statements fill.</summary>
    private readonly GanttModel _model;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The lexer.</summary>
    private SourceScanner _scanner = null!;

    /// <summary>The token cursor.</summary>
    private TokenCursor _cursor = null!;

    /// <summary>The number of task and section statements so far.</summary>
    private int _nodes;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="model">The model to fill.</param>
    /// <param name="limits">The limits of the open call.</param>
    private GanttParser(GanttModel model, MermaidLimits limits)
    {
        _model = model;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private ScanToken Current => _cursor.Current;

    /// <summary>
    ///     Parses a gantt chart.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, where Mermaid's parser fails, or when a limit is
    ///     exceeded.
    /// </exception>
    public static GanttModel Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var model = new GanttModel(limits, detected.Location);
        new GanttParser(model, limits).Run(source.ParserText);
        return model;
    }

    /// <summary>
    ///     Parses the whole text (<c>start</c>): the keyword, then the lines up to the grammar's <c>EOF</c> and the end of
    ///     the tokens.
    /// </summary>
    /// <param name="text">The text.</param>
    private void Run(MappedText text)
    {
        _scanner = new SourceScanner(text, Rules, Conditions);
        _cursor = Lex(() => new TokenCursor(_scanner));
        if (!Is(GanttTokens.Gantt))
        {
            throw Fail([GanttTokens.Gantt], "start the diagram with the gantt keyword");
        }

        Advance();
        while (true)
        {
            if (Is(Newline))
            {
                Advance();
            }
            else if (Is(Eof))
            {
                // The grammar's EOF is followed by the end of the tokens
                Advance();
                if (IsEnd())
                {
                    return;
                }
            }
            else if (Is(LineStarts))
            {
                ParseStatement();
            }
            else
            {
                throw Fail(LineStarts);
            }
        }
    }

    /// <summary>
    ///     Parses one statement and makes its database call.
    /// </summary>
    private void ParseStatement()
    {
        var token = Current;
        var location = Locate(token);
        switch (token.Kind)
        {
            case DateFormat:
                Advance();
                _model.SetDateFormat(TextOf(token)[11..]);
                return;
            case InclusiveEndDates:
                Advance();
                _model.EnableInclusiveEndDates();
                return;
            case TopAxis:
                // The action calls yy.TopAxis(), which Mermaid's gantt database does not have
                throw new MermaidSyntaxException(
                    MermaidDiagnosticCodes.SyntaxError,
                    "Mermaid fails on the topAxis statement (its gantt parser calls TopAxis, which its database does not have)",
                    location,
                    "remove the topAxis statement and set gantt.topAxis to true in the configuration (frontmatter config or an init directive)");
            case AxisFormat:
                Advance();
                _model.SetAxisFormat(TextOf(token)[11..]);
                return;
            case TickInterval:
                Advance();
                _model.SetTickInterval(TextOf(token)[13..]);
                return;
            case Excludes:
                Advance();
                _model.SetExcludes(TextOf(token)[9..]);
                return;
            case Includes:
                Advance();
                _model.SetIncludes(TextOf(token)[9..]);
                return;
            case TodayMarker:
                Advance();
                _model.SetTodayMarker(TextOf(token)[12..], location);
                return;
            case WeekdayMonday or WeekdayTuesday or WeekdayWednesday or WeekdayThursday or WeekdayFriday or WeekdaySaturday or WeekdaySunday:
                Advance();
                _model.SetWeekday(token.Kind["weekday_".Length..]);
                return;
            case WeekendFriday or WeekendSaturday:
                Advance();
                _model.SetWeekend(token.Kind["weekend_".Length..]);
                return;
            case Title:
                Advance();
                _model.SetDiagramTitle(TextOf(token)[6..], location);
                return;
            case AccTitle:
                Advance();
                _model.SetAccessibleTitle(Preprocessor.TrimJs(TextOf(Expect(AccTitleValue))), location);
                return;
            case AccDescr:
                Advance();
                _model.SetAccessibleDescription(Preprocessor.TrimJs(TextOf(Expect(AccDescrValue))), location);
                return;
            case AccDescrMultilineValue:
                Advance();
                _model.SetAccessibleDescription(Preprocessor.TrimJs(TextOf(token)), location);
                return;
            case Section:
                Advance();
                CountNode(location, "sections and tasks");
                _model.AddSection(TextOf(token)[8..]);
                return;
            case Click:
                ParseClick();
                return;
            default:
                {
                    // taskTxt taskData
                    Advance();
                    var data = Expect(TaskData);
                    CountNode(location, "sections and tasks");
                    _model.AddTask(TextOf(token), TextOf(data), location);
                    return;
                }
        }
    }

    /// <summary>
    ///     Parses a click statement (<c>clickStatement</c>): the ids, then a call and a link in one of the seven forms of the
    ///     grammar, with the database calls of each form in its order.
    /// </summary>
    private void ParseClick()
    {
        var click = Advance();
        var ids = TextOf(click);
        var location = Locate(click);
        if (Is(CallbackName))
        {
            var name = TextOf(Advance());
            if (Is(CallbackArgs))
            {
                var args = TextOf(Advance());
                if (Is(Href))
                {
                    var href = TextOf(Advance());
                    _model.SetClickEvent(ids, name, args, location);
                    _model.SetLink(ids, href, location);
                    return;
                }

                CheckLookahead([.. LineStarts, Href]);
                _model.SetClickEvent(ids, name, args, location);
                return;
            }

            if (Is(Href))
            {
                var href = TextOf(Advance());
                _model.SetClickEvent(ids, name, null, location);
                _model.SetLink(ids, href, location);
                return;
            }

            CheckLookahead([.. LineStarts, CallbackArgs, Href]);
            _model.SetClickEvent(ids, name, null, location);
            return;
        }

        if (Is(Href))
        {
            var href = TextOf(Advance());
            if (Is(CallbackName))
            {
                var name = TextOf(Advance());
                if (Is(CallbackArgs))
                {
                    var args = TextOf(Advance());
                    _model.SetClickEvent(ids, name, args, location);
                    _model.SetLink(ids, href, location);
                    return;
                }

                CheckLookahead([.. LineStarts, CallbackArgs]);
                _model.SetClickEvent(ids, name, null, location);
                _model.SetLink(ids, href, location);
                return;
            }

            CheckLookahead([.. LineStarts, CallbackName]);
            _model.SetLink(ids, href, location);
            return;
        }

        throw Fail([CallbackName, Href], "follow click and the task ids with href \"url\" or call function(arguments)");
    }

    /// <summary>
    ///     Counts a task or section statement against <see cref="MermaidLimits.MaxNodes"/>.
    /// </summary>
    /// <param name="location">The location of the statement.</param>
    /// <param name="what">What is counted, for the message.</param>
    private void CountNode(MermaidLocation location, string what)
    {
        if (++_nodes > _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} {what} (MermaidLimits.MaxNodes)"),
                location,
                "split the gantt chart, or raise MermaidLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Checks the lookahead against the actions of the LALR state the parser is in, and fails with them otherwise, as
    ///     jison lists them.
    /// </summary>
    /// <param name="accepted">The tokens the state accepts.</param>
    private void CheckLookahead(string[] accepted)
    {
        if (!Is(accepted))
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
        GanttModel.CheckLength(token.Text.Length, _limits, Locate(token));
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
    private bool Is(params string[] kinds) => !IsEnd() && Array.IndexOf(kinds, Current.Kind) >= 0;

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
    ///     Runs a step that lexes a token, and gives a lexical error a hint for gantt charts.
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
            // The scanner stops at text no rule matches (only after a %%{ that the preprocessing left)
            throw new MermaidSyntaxException(
                ex.Code,
                $"unrecognized text {SourceScanner.Describe(_scanner.Input.Text, _scanner.Position)}",
                ex.Location,
                "remove the '%%{' that does not start a directive");
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
            .OrderBy(GanttTokens.Number)
            .ThenBy(kind => kind, StringComparer.Ordinal)
            .Select(JisonName)
            .ToList();
        var token = Current;
        var found = IsEnd() ? "the end of the text" : $"'{JisonName(token.Kind)}' ({Describe(token.Text)})";
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
    private string HintFor(ScanToken token, List<string> expected)
    {
        if (expected.Count == 1 && expected[0] == TaskData)
        {
            return "give the task its data after a colon, such as 'Write the plan :a1, 2024-01-01, 3d', with no '#' or ';' before it";
        }

        if (expected.Contains(CallbackName) || expected.Contains(Href) || expected.Contains(CallbackArgs))
        {
            return "write a click statement as click ids href \"url\", click ids call function(arguments), or both";
        }

        return token.Kind switch
        {
            Date => "a date cannot start a statement; write the task name, a colon and then the dates",
            AccDescription => "use accDescr: text or accDescr { text } for the accessible description",
            Colon => "write the task name before its colon",
            OpenDirective => "remove the '%%{' that does not start a directive",
            _ => IsEnd()
                ? "end the click statement with href \"url\" or call function()"
                : "check the statement at this position against the gantt syntax: one statement or task per line",
        };
    }
}
