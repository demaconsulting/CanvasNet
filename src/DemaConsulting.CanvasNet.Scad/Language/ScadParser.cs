using System.Runtime.CompilerServices;

namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     Parses a token list into an immutable <see cref="ScadProgram"/> syntax tree.
/// </summary>
/// <remarks>
///     <para>
///         Statements are parsed by recursive descent and expressions by precedence climbing over the
///         pre-tokenized list, so any lookahead is a simple index. The parser is name-agnostic: every
///         <c>name(args) children</c> is a module instantiation and built-in names are never special, so
///         unknown or unsupported modules never produce a syntax error. It is fail-fast: the first grammatical
///         error aborts with a <see cref="ScadSyntaxException"/> (lexical errors are raised earlier, during
///         tokenization).
///     </para>
///     <para>
///         Hostile input is bounded in two complementary ways. A recursion counter limits how deeply the parser
///         itself nests (statements, expressions, prefix operators, exponent chains and comprehension
///         elements) and is backed by <see cref="RuntimeHelpers.EnsureSufficientExecutionStack"/>; after parsing,
///         an iterative check limits the height of the resulting tree, which also covers left-associative
///         operator and postfix chains that the parser builds without recursing. Together they guarantee that
///         every later recursive consumer sees a tree of bounded depth. Input size is bounded by
///         <see cref="MaxSourceLength"/>.
///     </para>
///     <para>
///         This file holds the entry points, the token cursor, error construction and the limit mechanics; the
///         grammar lives in <c>ScadParser.Statements.cs</c> and <c>ScadParser.Expressions.cs</c>. Instances are
///         single-use and not thread-safe; the static <see cref="Parse(string, string?)"/> entry points are safe
///         for concurrent use.
///     </para>
/// </remarks>
internal sealed partial class ScadParser
{
    /// <summary>The maximum nesting depth, applied both to parser recursion and to the height of the finished tree.</summary>
    internal const int MaxNestingDepth = 256;

    /// <summary>The maximum accepted source length in UTF-16 characters.</summary>
    internal const int MaxSourceLength = 4_194_304;

    /// <summary>The message names of the token kinds the grammar requires with <c>Expect</c>.</summary>
    private static readonly Dictionary<ScadTokenKind, string> ExpectedNames = new()
    {
        [ScadTokenKind.Identifier] = "an identifier",
        [ScadTokenKind.LeftParen] = "'('",
        [ScadTokenKind.RightParen] = "')'",
        [ScadTokenKind.RightBrace] = "'}'",
        [ScadTokenKind.RightBracket] = "']'",
        [ScadTokenKind.Semicolon] = "';'",
        [ScadTokenKind.Colon] = "':'",
        [ScadTokenKind.Assign] = "'='",
    };

    /// <summary>The tokens being parsed; the last one is always the end-of-file token.</summary>
    private readonly IReadOnlyList<ScadToken> _tokens;

    /// <summary>The index of the current token.</summary>
    private int _index;

    /// <summary>The current recursion depth.</summary>
    private int _depth;

    /// <summary>
    ///     Initializes a parser over a token list.
    /// </summary>
    /// <param name="tokens">The tokens, ending with an end-of-file token.</param>
    private ScadParser(IReadOnlyList<ScadToken> tokens)
    {
        _tokens = tokens;
    }

    /// <summary>
    ///     Tokenizes and parses a source text.
    /// </summary>
    /// <param name="source">The source text. Must not be null.</param>
    /// <param name="file">The source name placed in locations; may be null.</param>
    /// <returns>The syntax tree.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when the source exceeds <see cref="MaxSourceLength"/> characters.</exception>
    /// <exception cref="ScadSyntaxException">Thrown at the first lexical or grammatical error, or when nesting is too deep.</exception>
    public static ScadProgram Parse(string source, string? file)
    {
        ArgumentNullException.ThrowIfNull(source);

        // The size limit is checked before any work so that hostile input cannot consume memory
        if (source.Length > MaxSourceLength)
        {
            throw CreateSourceTooLongException();
        }

        return Parse(ScadLexer.Tokenize(source, file));
    }


    /// <summary>
    ///     Parses an already tokenized source.
    /// </summary>
    /// <param name="tokens">The tokens produced by <see cref="ScadLexer.Tokenize"/>, ending with an end-of-file token.</param>
    /// <returns>The syntax tree.</returns>
    /// <exception cref="ScadSyntaxException">Thrown at the first grammatical error, or when nesting is too deep.</exception>
    public static ScadProgram Parse(IReadOnlyList<ScadToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return new ScadParser(tokens).ParseProgramGuarded();
    }

    /// <summary>
    ///     Creates the exception reported for source text that exceeds <see cref="MaxSourceLength"/>.
    /// </summary>
    /// <returns>A plain <see cref="InvalidDataException"/> (no location is meaningful for an oversize source).</returns>
    internal static InvalidDataException CreateSourceTooLongException() =>
        new($"Source exceeds the maximum length of {MaxSourceLength} characters");

    /// <summary>
    ///     Parses the whole program, converting stack exhaustion into a syntax error and verifying the tree height.
    /// </summary>
    /// <returns>The syntax tree.</returns>
    private ScadProgram ParseProgramGuarded()
    {
        ScadProgram program;
        try
        {
            program = ParseProgram();
        }
        catch (InsufficientExecutionStackException)
        {
            // The runtime stack guard fired before the depth counter did; report it the same way
            throw Error(Current, $"Nesting too deep (limit {MaxNestingDepth})");
        }

        // Left-associative chains do not recurse in the parser, so the finished tree is measured as well
        ScadTreeDepth.Verify(program, MaxNestingDepth);
        return program;
    }

    /// <summary>
    ///     Parses <c>{ top-level-statement } EOF</c>.
    /// </summary>
    /// <returns>The program.</returns>
    private ScadProgram ParseProgram()
    {
        var file = _tokens[^1].Location.File;
        var statements = new List<ScadStatement>();
        while (!At(ScadTokenKind.EndOfFile))
        {
            var statement = ParseTopLevelStatement();
            if (statement is not null)
            {
                statements.Add(statement);
            }
        }

        return new ScadProgram(statements.AsReadOnly()) { Location = new ScadSourceLocation(file, 1, 1) };
    }

    /// <summary>
    ///     Gets the current token.
    /// </summary>
    private ScadToken Current => _tokens[_index];

    /// <summary>
    ///     Looks ahead without consuming; clamps to the end-of-file token.
    /// </summary>
    /// <param name="offset">The distance from the current token.</param>
    /// <returns>The token.</returns>
    private ScadToken PeekAt(int offset) => _tokens[Math.Min(_index + offset, _tokens.Count - 1)];

    /// <summary>
    ///     Determines whether the current token has a kind.
    /// </summary>
    /// <param name="kind">The kind to test.</param>
    /// <returns><see langword="true"/> when the current token has that kind.</returns>
    private bool At(ScadTokenKind kind) => _tokens[_index].Kind == kind;

    /// <summary>
    ///     Consumes the current token (the end-of-file token is never consumed).
    /// </summary>
    /// <returns>The consumed token.</returns>
    private ScadToken Advance()
    {
        // The index never moves past the end-of-file token, so lookahead can never run off the list
        var token = _tokens[_index];
        _index = Math.Min(_index + 1, _tokens.Count - 1);
        return token;
    }

    /// <summary>
    ///     Consumes the current token when it has the given kind.
    /// </summary>
    /// <param name="kind">The kind to accept.</param>
    /// <returns><see langword="true"/> when a token was consumed.</returns>
    private bool Accept(ScadTokenKind kind)
    {
        if (!At(kind))
        {
            return false;
        }

        _index++;
        return true;
    }

    /// <summary>
    ///     Consumes a token of the required kind.
    /// </summary>
    /// <param name="kind">The required kind.</param>
    /// <returns>The consumed token.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the current token has a different kind.</exception>
    private ScadToken Expect(ScadTokenKind kind) =>
        At(kind) ? Advance() : throw ErrorExpected(ExpectedNames[kind]);

    /// <summary>
    ///     Enters a recursive construct, enforcing the depth limit and the runtime stack guard.
    /// </summary>
    /// <exception cref="ScadSyntaxException">Thrown when the depth limit is exceeded.</exception>
    private void Enter()
    {
        _depth++;
        if (_depth > MaxNestingDepth)
        {
            throw Error(Current, $"Nesting too deep (limit {MaxNestingDepth})");
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
    }

    /// <summary>
    ///     Leaves a recursive construct entered with <see cref="Enter"/>.
    /// </summary>
    private void Leave() => _depth--;

    /// <summary>
    ///     Creates a syntax error located at a token.
    /// </summary>
    /// <param name="token">The offending token.</param>
    /// <param name="description">The description without location prefix.</param>
    /// <returns>The exception to throw.</returns>
    private static ScadSyntaxException Error(ScadToken token, string description) => new(description, token.Location);

    /// <summary>
    ///     Creates the "expected X" error for the current token: an end-of-input message at the end of the file,
    ///     otherwise an "expected X but found Y" message.
    /// </summary>
    /// <param name="expected">The description of what was expected, including quotes where appropriate.</param>
    /// <returns>The exception to throw.</returns>
    private ScadSyntaxException ErrorExpected(string expected)
    {
        var token = Current;
        return token.Kind == ScadTokenKind.EndOfFile
            ? Error(token, $"Unexpected end of input; expected {expected}")
            : Error(token, $"Expected {expected} but found {Describe(token)}");
    }

    /// <summary>
    ///     Creates the "unexpected token" error for a token that cannot start a statement.
    /// </summary>
    /// <param name="token">The offending token; never the end-of-file token, which the statement loops check first.</param>
    /// <returns>The exception to throw.</returns>
    private static ScadSyntaxException ErrorUnexpected(ScadToken token) =>
        Error(token, $"Unexpected token {Describe(token)}");

    /// <summary>
    ///     Describes a token for use in messages.
    /// </summary>
    /// <param name="token">The token; never the end-of-file token, which callers report separately.</param>
    /// <returns>The quoted lexeme (strings in double quotes, directives with their path).</returns>
    private static string Describe(ScadToken token) => token.Kind switch
    {
        ScadTokenKind.String => $"\"{token.Text}\"",
        ScadTokenKind.Include => $"'include <{token.Text}>'",
        ScadTokenKind.Use => $"'use <{token.Text}>'",
        _ => $"'{token.Text}'",
    };

    /// <summary>
    ///     Wraps a list as a read-only collection.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list, which must not be modified afterwards.</param>
    /// <returns>A read-only view of the list.</returns>
    private static IReadOnlyList<T> Freeze<T>(List<T> list) => list.AsReadOnly();
}
