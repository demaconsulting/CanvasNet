using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Helpers shared by the <see cref="ScadLexer"/> unit tests.
/// </summary>
internal static class ScadLexerTestSupport
{
    /// <summary>
    ///     Tokenizes a source text.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="file">The optional source name.</param>
    /// <returns>The tokens, including the end-of-file token.</returns>
    public static IReadOnlyList<ScadToken> Lex(string source, string? file = null) => ScadLexer.Tokenize(source, file);

    /// <summary>
    ///     Tokenizes a source text that must contain exactly one token plus the end-of-file token.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <returns>The single token.</returns>
    public static ScadToken Single(string source)
    {
        var tokens = Lex(source);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(ScadTokenKind.EndOfFile, tokens[1].Kind);
        return tokens[0];
    }

    /// <summary>
    ///     Tokenizes a source text and renders the token kinds as a space-separated string
    ///     (the end-of-file token is omitted).
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <returns>The kinds, for example <c>Identifier Assign Number Semicolon</c>.</returns>
    public static string Kinds(string source) =>
        string.Join(' ', Lex(source).Where(t => t.Kind != ScadTokenKind.EndOfFile).Select(t => t.Kind));

    /// <summary>
    ///     Asserts that tokenizing fails at an exact location.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    /// <param name="fragment">Text the message must contain.</param>
    /// <returns>The thrown exception.</returns>
    public static ScadSyntaxException AssertLexError(string source, int line, int column, string fragment) =>
        ScadTestHelpers.AssertSyntaxError(() => Lex(source), line, column, fragment);
}
