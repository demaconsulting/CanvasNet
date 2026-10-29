using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The bounded parser of the Vega expression language: it turns expression text into a
///     <see cref="CompiledExpression"/> in one pass without building a syntax tree.
/// </summary>
/// <remarks>
///     <para>
///         The grammar is the closed subset of JavaScript that the engine runs: the literals <c>null</c>, <c>true</c>,
///         <c>false</c>, numbers (decimal with fraction and exponent, hexadecimal), strings with JavaScript escapes,
///         regular expressions, arrays and objects; the constants <c>PI</c>, <c>E</c>, <c>NaN</c> and <c>Infinity</c>;
///         <c>datum</c> with dotted and bracketed access (<c>datum.x</c>, <c>datum['a b']</c>); member and index access
///         and calls of named functions; the unary operators <c>+ - ! ~</c>; the binary operators <c>+ - * / %</c>,
///         <c>=== !== == != &lt; &lt;= &gt; &gt;=</c>, <c>&amp;&amp;</c> and <c>||</c> (which return an operand and
///         short-circuit); the conditional <c>?:</c>; and parentheses. Any other identifier is a reference to a named
///         parameter. Assignments, increments, <c>new</c>, function expressions, template strings, comma sequences,
///         method calls (<c>a.b()</c>) and the other JavaScript operators are syntax errors.
///     </para>
///     <para>
///         Three groups of constructs parse but are not executed and are collected in
///         <see cref="CompiledExpression.UnsupportedConstructs"/>: calls of functions that are not in the closed function
///         table (<c>vl-expr-function-{name}</c>), view state (the identifiers <c>signal</c>, <c>event</c> and <c>item</c>,
///         <c>vl-expr-view-state</c>), and deferred operators (the bitwise and shift operators and <c>in</c>,
///         <c>vl-expr-construct-{name}</c>); each evaluates to null.
///     </para>
///     <para>
///         The parser is a precedence-climbing recursive descent over a token list with a cursor. Recursion happens only
///         where the text nests (groups, brackets, call arguments, array and object literals, conditional branches and
///         operands of unary operators), and every such level is counted: nesting deeper than
///         <see cref="VegaLiteLimits.MaxExpressionDepth"/> is rejected before the next level is entered, so the stack use
///         is bounded by the limit and never by the length of the text. A chain such as <c>a + b + c + ...</c> loops
///         instead of recursing and, because the parser emits a postfix program directly, never builds a deep structure
///         that a later pass would have to walk recursively. The text length is checked against
///         <see cref="VegaLiteLimits.MaxExpressionLength"/> before anything else is done. The parser is stateless
///         between calls and thread-safe; the result is immutable.
///     </para>
///     <para>
///         Every error is a <see cref="VegaLiteSpecException"/> that carries the location of the expression text (the
///         JSON pointer and the line and column of the string value that the caller supplies) and whose message
///         ends with the zero-based <c>expression offset N</c> of the first problem, in the fixed form
///         <c>Invalid expression: {description} at expression offset {N}.</c>.
///     </para>
/// </remarks>
internal sealed partial class ExpressionParser
{
    /// <summary>The identifiers that name view state.</summary>
    private static readonly HashSet<string> ViewStateNames = new(StringComparer.Ordinal) { "signal", "event", "item" };

    /// <summary>The words that are reserved in JavaScript; they are never parameter names.</summary>
    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
    {
        "new", "function", "var", "let", "const", "if", "else", "for", "while", "do", "return", "this", "class", "typeof", "void",
        "delete", "instanceof", "switch", "case", "default", "break", "continue", "throw", "try", "catch", "finally", "with",
        "yield", "await", "async", "import", "export", "super", "enum", "debugger", "extends", "in",
    };

    /// <summary>The binary operators with their precedence, opcode and (for deferred operators) construct name.</summary>
    private static readonly Dictionary<string, (int Precedence, ExpressionOpcode Op, string? Deferred)> BinaryOperators = new(StringComparer.Ordinal)
    {
        ["||"] = (1, ExpressionOpcode.JumpIfTruthyKeep, null),
        ["&&"] = (2, ExpressionOpcode.JumpIfFalsyKeep, null),
        ["|"] = (3, ExpressionOpcode.Unsupported, "bitwise-or"),
        ["^"] = (4, ExpressionOpcode.Unsupported, "bitwise-xor"),
        ["&"] = (5, ExpressionOpcode.Unsupported, "bitwise-and"),
        ["=="] = (6, ExpressionOpcode.LooseEqual, null),
        ["!="] = (6, ExpressionOpcode.LooseNotEqual, null),
        ["==="] = (6, ExpressionOpcode.StrictEqual, null),
        ["!=="] = (6, ExpressionOpcode.StrictNotEqual, null),
        ["<"] = (7, ExpressionOpcode.Less, null),
        ["<="] = (7, ExpressionOpcode.LessEqual, null),
        [">"] = (7, ExpressionOpcode.Greater, null),
        [">="] = (7, ExpressionOpcode.GreaterEqual, null),
        ["in"] = (7, ExpressionOpcode.Unsupported, "in"),
        ["<<"] = (8, ExpressionOpcode.Unsupported, "shift-left"),
        [">>"] = (8, ExpressionOpcode.Unsupported, "shift-right"),
        [">>>"] = (8, ExpressionOpcode.Unsupported, "shift-right-unsigned"),
        ["+"] = (9, ExpressionOpcode.Add, null),
        ["-"] = (9, ExpressionOpcode.Subtract, null),
        ["*"] = (10, ExpressionOpcode.Multiply, null),
        ["/"] = (10, ExpressionOpcode.Divide, null),
        ["%"] = (10, ExpressionOpcode.Modulo, null),
    };

    /// <summary>The expression text.</summary>
    private readonly string _source;

    /// <summary>The location of the expression text in the specification.</summary>
    private readonly VegaLiteLocation _location;

    /// <summary>The deepest nesting allowed.</summary>
    private readonly int _maxDepth;

    /// <summary>The instructions emitted so far.</summary>
    private readonly List<ExpressionInstruction> _program = [];

    /// <summary>The constants of the program.</summary>
    private readonly List<DataValue> _constants = [];

    /// <summary>The names of the program.</summary>
    private readonly List<string> _names = [];

    /// <summary>The position of every name in <see cref="_names"/>.</summary>
    private readonly Dictionary<string, int> _nameIndex = new(StringComparer.Ordinal);

    /// <summary>The functions the program calls.</summary>
    private readonly List<ExpressionFunction> _functions = [];

    /// <summary>The position of every function in <see cref="_functions"/> by name.</summary>
    private readonly Dictionary<string, int> _functionIndex = new(StringComparer.Ordinal);

    /// <summary>The unsupported constructs found so far.</summary>
    private readonly List<ExpressionConstruct> _constructs = [];

    /// <summary>The parameter references found so far.</summary>
    private readonly List<ExpressionReference> _parameters = [];

    /// <summary>The tokens of the text.</summary>
    private List<ExpressionToken> _tokens = [];

    /// <summary>The position of the current token.</summary>
    private int _position;

    /// <summary>Whether the expression reads the datum.</summary>
    private bool _referencesDatum;

    /// <summary>The current nesting depth.</summary>
    private int _depth;

    /// <summary>The deepest nesting reached.</summary>
    private int _deepest;

    /// <summary>The stack height at the current point of the program.</summary>
    private int _stack;

    /// <summary>The largest stack height of the program.</summary>
    private int _maxStack;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionParser"/> class for one expression.
    /// </summary>
    /// <param name="source">The expression text.</param>
    /// <param name="location">The location of the text.</param>
    /// <param name="limits">The limits.</param>
    private ExpressionParser(string source, VegaLiteLocation location, VegaLiteLimits limits)
    {
        _source = source;
        _location = location;
        _maxDepth = limits.MaxExpressionDepth;
    }

    /// <summary>What a postfix expression has read so far but not yet emitted.</summary>
    private enum Pending
    {
        /// <summary>Everything has been emitted.</summary>
        None,

        /// <summary>The identifier <c>datum</c> was read; a following member or index access reads a field directly.</summary>
        Datum,

        /// <summary>An identifier was read; a following parenthesis makes it a function call.</summary>
        Name,
    }

    /// <summary>
    ///     Parses an expression.
    /// </summary>
    /// <param name="source">The expression text.</param>
    /// <param name="location">
    ///     The location of the string value that holds the expression: the JSON pointer and the line and column of the
    ///     value. Errors carry it unchanged; the offset inside the text is part of the message.
    /// </param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <returns>The compiled expression.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="VegaLiteSpecException">
    ///     Thrown when the text is longer than <see cref="VegaLiteLimits.MaxExpressionLength"/>, is nested deeper than
    ///     <see cref="VegaLiteLimits.MaxExpressionDepth"/>, or is not valid; the message names the expression offset of
    ///     the first problem.
    /// </exception>
    public static CompiledExpression Parse(string source, VegaLiteLocation location, VegaLiteLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        limits ??= VegaLiteLimits.Default;
        var parser = new ExpressionParser(source, location, limits);

        // The length is checked before the text is tokenized, so a hostile text costs nothing but this comparison
        if (source.Length > limits.MaxExpressionLength)
        {
            throw parser.Fail(
                limits.MaxExpressionLength,
                $"the expression has {source.Length} characters, more than the limit of {limits.MaxExpressionLength} (MaxExpressionLength)");
        }

        return parser.Compile();
    }

    /// <summary>
    ///     Creates the exception for the first problem of the expression.
    /// </summary>
    /// <param name="offset">The offset of the problem within the expression text.</param>
    /// <param name="description">The description of the problem.</param>
    /// <returns>The exception to throw.</returns>
    private VegaLiteSpecException Fail(int offset, string description) =>
        new($"Invalid expression: {description} at expression offset {offset}.", _location);

    /// <summary>
    ///     Compiles the whole expression.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    private CompiledExpression Compile()
    {
        _tokens = Tokenize();
        ParseTernary();
        if (Current.Kind != ExpressionTokenKind.End)
        {
            throw Fail(Current.Offset, $"unexpected {Describe(Current)}");
        }

        var tables = new ExpressionTables([.. _constants], [.. _names], [.. _functions]);
        var findings = new ExpressionFindings([.. _constructs], [.. _parameters], _referencesDatum, _deepest, Math.Max(_maxStack, 1));
        return new CompiledExpression(_source, _location, [.. _program], tables, findings);
    }

    /// <summary>Gets the current token.</summary>
    private ExpressionToken Current => _tokens[_position];

    /// <summary>
    ///     Describes a token for an error message.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The description.</returns>
    private string Describe(ExpressionToken token)
    {
        if (token.Kind == ExpressionTokenKind.End)
        {
            return "end of expression";
        }

        var text = _source.Substring(token.Offset, Math.Min(token.Length, 24));
        return token.Length > 24 ? $"'{text}...'" : $"'{text}'";
    }

    /// <summary>
    ///     Tells whether the current token is a given punctuator.
    /// </summary>
    /// <param name="text">The punctuator.</param>
    /// <returns><see langword="true"/> when it is the current token.</returns>
    private bool IsPunctuator(string text) =>
        Current.Kind == ExpressionTokenKind.Punctuator && string.Equals(Current.Text, text, StringComparison.Ordinal);

    /// <summary>
    ///     Moves to the next token (the end token is never left).
    /// </summary>
    private void Advance()
    {
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }
    }

    /// <summary>
    ///     Consumes a punctuator or fails.
    /// </summary>
    /// <param name="text">The punctuator.</param>
    private void Expect(string text)
    {
        if (!IsPunctuator(text))
        {
            throw Fail(Current.Offset, $"expected '{text}' but found {Describe(Current)}");
        }

        Advance();
    }

    /// <summary>
    ///     Enters one level of nesting.
    /// </summary>
    /// <param name="offset">The offset of the construct that nests.</param>
    private void Enter(int offset)
    {
        _depth++;
        if (_depth > _maxDepth)
        {
            throw Fail(offset, $"the expression is nested more than {_maxDepth} levels deep (MaxExpressionDepth)");
        }

        if (_depth > _deepest)
        {
            _deepest = _depth;
        }
    }

    /// <summary>
    ///     Leaves one level of nesting.
    /// </summary>
    private void Leave() => _depth--;

    /// <summary>
    ///     Appends an instruction.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="operand">The operand.</param>
    /// <param name="argument">The argument count.</param>
    /// <param name="offset">The offset of the construct in the text.</param>
    /// <param name="stackDelta">The change of the stack height on the path that falls through.</param>
    /// <returns>The index of the instruction.</returns>
    private int Emit(ExpressionOpcode op, int operand, int argument, int offset, int stackDelta)
    {
        _program.Add(new ExpressionInstruction(op, operand, argument, offset));
        _stack += stackDelta;
        _maxStack = Math.Max(_maxStack, _stack);
        return _program.Count - 1;
    }

    /// <summary>
    ///     Points a jump instruction at the next instruction to be emitted.
    /// </summary>
    /// <param name="jump">The index of the jump.</param>
    private void PatchToHere(int jump) => _program[jump] = _program[jump] with { Operand = _program.Count };

    /// <summary>
    ///     Emits an instruction that pushes a constant.
    /// </summary>
    /// <param name="value">The constant.</param>
    /// <param name="offset">The offset of the construct in the text.</param>
    private void EmitConstant(DataValue value, int offset)
    {
        _constants.Add(value);
        Emit(ExpressionOpcode.PushConstant, _constants.Count - 1, 0, offset, 1);
    }

    /// <summary>
    ///     Adds a name to the name table.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The index of the name.</returns>
    private int AddName(string name)
    {
        if (!_nameIndex.TryGetValue(name, out var index))
        {
            index = _names.Count;
            _names.Add(name);
            _nameIndex[name] = index;
        }

        return index;
    }

    /// <summary>
    ///     Records an unsupported construct.
    /// </summary>
    /// <param name="kind">The kind of the construct.</param>
    /// <param name="name">The name.</param>
    /// <param name="offset">The offset in the text.</param>
    private void RecordConstruct(ExpressionConstructKind kind, string name, int offset) =>
        _constructs.Add(new ExpressionConstruct(kind, name, offset));

    /// <summary>
    ///     Parses a conditional expression: <c>test ? consequent : alternate</c>.
    /// </summary>
    private void ParseTernary()
    {
        ParseBinary(1);
        if (!IsPunctuator("?"))
        {
            return;
        }

        // The test jumps to the alternate when it is falsy; the consequent jumps over the alternate
        var question = Current.Offset;
        Advance();
        var toAlternate = Emit(ExpressionOpcode.JumpIfFalsy, 0, 0, question, -1);
        var baseStack = _stack;
        Enter(question);
        ParseTernary();
        Leave();
        var colon = Current.Offset;
        Expect(":");
        var toEnd = Emit(ExpressionOpcode.Jump, 0, 0, colon, 0);
        PatchToHere(toAlternate);
        _stack = baseStack;
        Enter(colon);
        ParseTernary();
        Leave();
        PatchToHere(toEnd);
    }

    /// <summary>
    ///     Parses a chain of binary operators by precedence climbing.
    /// </summary>
    /// <param name="minimum">The lowest precedence this call may consume.</param>
    private void ParseBinary(int minimum)
    {
        ParseUnary();
        while (TryGetBinaryOperator(Current, out var info) && info.Precedence >= minimum)
        {
            var token = Current;
            Advance();
            if (info.Op is ExpressionOpcode.JumpIfFalsyKeep or ExpressionOpcode.JumpIfTruthyKeep)
            {
                // Short-circuit: the jump keeps the left operand when it decides the result
                var jump = Emit(info.Op, 0, 0, token.Offset, -1);
                ParseBinary(info.Precedence + 1);
                PatchToHere(jump);
                continue;
            }

            ParseBinary(info.Precedence + 1);
            if (info.Deferred is not null)
            {
                RecordConstruct(ExpressionConstructKind.Deferred, info.Deferred, token.Offset);
                Emit(ExpressionOpcode.Unsupported, 0, 2, token.Offset, -1);
            }
            else
            {
                Emit(info.Op, 0, 0, token.Offset, -1);
            }
        }
    }

    /// <summary>
    ///     Looks up the binary operator a token stands for.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="info">Receives the precedence, opcode and deferred name.</param>
    /// <returns><see langword="true"/> when the token is a binary operator.</returns>
    private static bool TryGetBinaryOperator(ExpressionToken token, out (int Precedence, ExpressionOpcode Op, string? Deferred) info)
    {
        info = default;
        return token.Kind is ExpressionTokenKind.Punctuator or ExpressionTokenKind.Identifier
            && BinaryOperators.TryGetValue(token.Text, out info)
            && (token.Kind == ExpressionTokenKind.Punctuator || token.Text == "in");
    }

    /// <summary>
    ///     Parses a unary operator and its operand.
    /// </summary>
    private void ParseUnary()
    {
        var token = Current;
        if (token.Kind == ExpressionTokenKind.Punctuator && token.Text is "+" or "-" or "!" or "~")
        {
            Advance();
            Enter(token.Offset);
            ParseUnary();
            Leave();
            if (token.Text == "~")
            {
                // Bitwise not is deferred like the binary bitwise operators: it is reported and evaluates to null
                RecordConstruct(ExpressionConstructKind.Deferred, "bitwise-not", token.Offset);
                Emit(ExpressionOpcode.Unsupported, 0, 1, token.Offset, 0);
                return;
            }

            var op = token.Text switch
            {
                "+" => ExpressionOpcode.Plus,
                "-" => ExpressionOpcode.Negate,
                _ => ExpressionOpcode.Not,
            };
            Emit(op, 0, 0, token.Offset, 0);
            return;
        }

        ParsePostfix();
    }
}
