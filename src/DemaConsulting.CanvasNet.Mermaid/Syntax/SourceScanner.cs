// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     Tries to match a lexer rule at a position of the input.
/// </summary>
/// <param name="input">The whole input text.</param>
/// <param name="position">The position the match must start at.</param>
/// <returns>The length of the match (zero is a match of nothing), or a negative number when the rule does not match.</returns>
internal delegate int ScanMatcher(string input, int position);

/// <summary>
///     One rule of a jison-style lexer: what it matches and what it does with the match.
/// </summary>
/// <param name="Name">The token kind the rule produces when it has no action, and the rule's name in messages.</param>
/// <param name="Match">The matcher, anchored at the current position.</param>
/// <param name="Action">
///     The action, run after the match is consumed, that may change the lexer state, the token text (as jison's
///     <c>yytext</c>) or the input position (<see cref="SourceScanner.Less(int)"/>), and returns the token kind, or
///     <see langword="null"/> to skip the match (white space, comments, state changes); <see langword="null"/> for a rule
///     that always returns <see cref="Name"/>.
/// </param>
internal sealed record ScanRule(string Name, ScanMatcher Match, Func<SourceScanner, string?>? Action = null);

/// <summary>
///     A token produced by <see cref="SourceScanner"/>.
/// </summary>
/// <param name="Kind">The token kind (the name the grammar uses, such as <c>NODE_STRING</c>, or <see cref="SourceScanner.EndOfInput"/>).</param>
/// <param name="Text">The token text (jison's <c>yytext</c>, possibly changed by the rule's action).</param>
/// <param name="Start">The position of the first character in the scanner input.</param>
/// <param name="End">The position after the last character in the scanner input.</param>
internal readonly record struct ScanToken(string Kind, string Text, int Start, int End);

/// <summary>
///     The shared lexer infrastructure of the diagram parsers: a port of the lexer that jison generates for Mermaid's
///     grammars, with rules tried in order, start conditions (lexer states) on a stack, and token positions that map back
///     to the original text.
/// </summary>
/// <remarks>
///     <para>
///         <b>Semantics.</b> Mermaid's grammars are jison grammars without the <c>flex</c> option, so the generated lexer
///         takes, at each position, the <i>first</i> rule of the active start condition that matches (not the longest), runs
///         its action, and either returns a token or skips the match and continues. An empty input still runs the rules
///         once, so an <c>&lt;&lt;EOF&gt;&gt;</c> rule (a matcher of nothing at the end) can produce its token; after that, and when no
///         rule matches at the end, the scanner returns <see cref="EndOfInput"/>. Text that no rule matches is a lexical
///         error, reported as a located <see cref="MermaidSyntaxException"/>. The rules and the start-condition table are
///         ported from the generated parser of each grammar (its <c>rules</c> and <c>conditions</c>), so the lexing is the
///         same as Mermaid's.
///     </para>
///     <para>
///         <b>No regular expressions.</b> Each rule is a hand-written matcher (see <see cref="ScanPatterns"/>) that runs in
///         time linear in what it inspects, so no input can make lexing backtrack catastrophically. A rule that matches
///         nothing and produces no token must change the state; a scanner that makes no progress over many steps stops with
///         an <see cref="InvalidOperationException"/> (a defect of the grammar port, never of the input).
///     </para>
///     <para>
///         An instance lexes one input on one thread.
///     </para>
/// </remarks>
internal sealed class SourceScanner
{
    /// <summary>The token kind returned at the end of the input.</summary>
    public const string EndOfInput = "EOF";

    /// <summary>The name of the start condition a scanner starts in.</summary>
    public const string InitialState = "INITIAL";

    /// <summary>The most consecutive steps that consume nothing and return no token before the scanner reports a defect.</summary>
    private const int MaxIdleSteps = 1_024;

    /// <summary>The rules, indexed by the condition table.</summary>
    private readonly IReadOnlyList<ScanRule> _rules;

    /// <summary>The indexes of the rules active in each start condition, in the order they are tried.</summary>
    private readonly IReadOnlyDictionary<string, IReadOnlyList<int>> _conditions;

    /// <summary>The stack of start conditions; the active one is last.</summary>
    private readonly List<string> _states = [InitialState];

    /// <summary>Whether the end of the input has been reached once (jison's <c>done</c>).</summary>
    private bool _done;

    /// <summary>
    ///     Initializes a scanner.
    /// </summary>
    /// <param name="input">The text to lex, mapped to the original text.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="conditions">The rule indexes of every start condition; it must contain <see cref="InitialState"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the table has no initial condition or names a rule that does not exist.</exception>
    public SourceScanner(MappedText input, IReadOnlyList<ScanRule> rules, IReadOnlyDictionary<string, IReadOnlyList<int>> conditions)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(conditions);
        if (!conditions.ContainsKey(InitialState))
        {
            throw new ArgumentException("The condition table must contain the initial condition.", nameof(conditions));
        }

        if (conditions.Values.Any(list => list.Any(index => index < 0 || index >= rules.Count)))
        {
            throw new ArgumentException("The condition table names a rule that does not exist.", nameof(conditions));
        }

        Input = input;
        _rules = rules;
        _conditions = conditions;
    }

    /// <summary>
    ///     Gets the text that is lexed, mapped to the original text.
    /// </summary>
    public MappedText Input { get; }

    /// <summary>
    ///     Gets the position of the next character to lex.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    ///     Gets or sets the text of the current match (jison's <c>yytext</c>); an action may replace it.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Gets the position at which the current match starts.
    /// </summary>
    public int MatchStart { get; private set; }

    /// <summary>
    ///     Gets the active start condition.
    /// </summary>
    public string State => _states[^1];

    /// <summary>
    ///     Gets the number of start conditions on the stack (one when only the initial condition is active).
    /// </summary>
    public int StateDepth => _states.Count;

    /// <summary>
    ///     Pushes a start condition and makes it active (jison's <c>begin</c> and <c>pushState</c>).
    /// </summary>
    /// <param name="state">The condition; it must be in the condition table.</param>
    /// <exception cref="ArgumentException">Thrown when the condition is not in the table.</exception>
    public void Begin(string state)
    {
        if (!_conditions.ContainsKey(state))
        {
            throw new ArgumentException($"The start condition '{state}' is not defined.", nameof(state));
        }

        _states.Add(state);
    }

    /// <summary>
    ///     Pops the active start condition (jison's <c>popState</c>); the initial condition is never popped.
    /// </summary>
    /// <returns>The condition that was popped, or the initial condition when only it is on the stack.</returns>
    public string PopState()
    {
        if (_states.Count <= 1)
        {
            return _states[0];
        }

        var top = _states[^1];
        _states.RemoveAt(_states.Count - 1);
        return top;
    }

    /// <summary>
    ///     Gets a start condition from the top of the stack (jison's <c>topState</c>).
    /// </summary>
    /// <param name="depth">How far below the top: zero for the active condition.</param>
    /// <returns>The condition, or <see cref="InitialState"/> when the stack is not that deep.</returns>
    public string TopState(int depth = 0)
    {
        var index = _states.Count - 1 - Math.Abs(depth);
        return index >= 0 ? _states[index] : InitialState;
    }

    /// <summary>
    ///     Keeps only the first characters of the current match and returns the rest to the input (jison's <c>less</c>).
    /// </summary>
    /// <param name="count">The number of characters to keep, from 0 to the length of the match.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the count is outside the match.</exception>
    public void Less(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Position - MatchStart);
        Position = MatchStart + count;
        Text = Input.Text.Substring(MatchStart, count);
    }

    /// <summary>
    ///     Gets the location in the original text of a position of the input.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns>The location.</returns>
    public MermaidLocation Locate(int position) => Input.Locate(position);

    /// <summary>
    ///     Lexes the next token.
    /// </summary>
    /// <returns>The token; <see cref="EndOfInput"/> (repeatedly) once the input is exhausted.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown when no rule of the active condition matches the text at the position.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the rules make no progress (a defect of the grammar port).</exception>
    public ScanToken Next()
    {
        var idle = 0;
        while (true)
        {
            // jison's next(): after the end has been reached once, every call returns the end
            if (_done)
            {
                return new ScanToken(EndOfInput, string.Empty, Input.Length, Input.Length);
            }

            if (Position >= Input.Length)
            {
                _done = true;
            }

            // The first rule of the active condition that matches wins (no longest-match search)
            var (rule, length) = FirstMatch();
            if (rule is null)
            {
                if (Position >= Input.Length)
                {
                    return new ScanToken(EndOfInput, string.Empty, Input.Length, Input.Length);
                }

                throw LexicalError(Position);
            }

            // Consume the match, then let the action decide what it means
            MatchStart = Position;
            Position += length;
            Text = Input.Text.Substring(MatchStart, length);
            var kind = rule.Action is null ? rule.Name : rule.Action(this);
            if (Position < Input.Length)
            {
                _done = false;
            }

            if (kind is not null)
            {
                return new ScanToken(kind, Text, MatchStart, Position);
            }

            // A skipped match that consumed nothing must have changed the state; too many of them is a defect
            idle = Position > MatchStart ? 0 : idle + 1;
            if (idle > MaxIdleSteps)
            {
                throw new InvalidOperationException($"The lexer made no progress at position {Position} in state '{State}'.");
            }
        }
    }

    /// <summary>
    ///     Creates the exception for text that no rule matches.
    /// </summary>
    /// <param name="position">The position of the text.</param>
    /// <returns>The located exception, ready to throw.</returns>
    public MermaidSyntaxException LexicalError(int position)
    {
        var found = Describe(Input.Text, position);
        return new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"unrecognized text {found}",
            Locate(position),
            "remove or quote the text at this position; labels with special characters can be written in double quotes, for example A[\"text\"]");
    }

    /// <summary>
    ///     Describes the text at a position for a message: the first character, quoted, or the end of the text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position.</param>
    /// <returns>The description.</returns>
    internal static string Describe(string text, int position)
    {
        if (position >= text.Length)
        {
            return "at the end of the text";
        }

        var c = text[position];
        return c switch
        {
            '\n' => "(a line break)",
            '\t' => "(a tab)",
            _ when char.IsControl(c) => string.Create(CultureInfo.InvariantCulture, $"(the control character U+{(int)c:X4})"),
            _ => $"'{c}'",
        };
    }

    /// <summary>
    ///     Finds the first rule of the active condition that matches at the position.
    /// </summary>
    /// <returns>The rule and the length of its match, or a null rule when none matches.</returns>
    private (ScanRule? Rule, int Length) FirstMatch()
    {
        var text = Input.Text;
        foreach (var index in _conditions.TryGetValue(State, out var list) ? list : _conditions[InitialState])
        {
            var length = _rules[index].Match(text, Position);
            if (length >= 0)
            {
                return (_rules[index], Math.Min(length, text.Length - Position));
            }
        }

        return (null, 0);
    }
}
