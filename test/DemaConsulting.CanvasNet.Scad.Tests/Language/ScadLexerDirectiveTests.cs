using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadLexer"/> <c>include</c> and <c>use</c> directive tokens.
/// </summary>
public class ScadLexerDirectiveTests
{
    /// <summary>
    ///     Validates the include directive token.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_IncludeDirective_ProducesPathToken()
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single("include <a/b.scad>");

        // Assert: directive kind, path text, keyword location
        Assert.Equal(ScadTokenKind.Include, token.Kind);
        Assert.Equal("a/b.scad", token.Text);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), token.Location);
    }

    /// <summary>
    ///     Validates the use directive token and the position of the token after it.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_UseDirective_ProducesPathToken()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("use <lib.scad> cube");

        // Assert: use token then the next token after the closing bracket
        Assert.Equal(ScadTokenKind.Use, tokens[0].Kind);
        Assert.Equal("lib.scad", tokens[0].Text);
        Assert.Equal(ScadTokenKind.Identifier, tokens[1].Kind);
        Assert.Equal(16, tokens[1].Location.Column);
    }

    /// <summary>
    ///     Validates that whitespace, newlines and comments may separate the keyword from the angle bracket,
    ///     and that the path is not trimmed.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_DirectiveWithWhitespaceBeforeAngle_ProducesPathToken()
    {
        // Arrange / Act
        var spaced = ScadLexerTestSupport.Single("include   /* c */ <x.scad>");
        var split = ScadLexerTestSupport.Single("use\n  <y.scad>");
        var untrimmed = ScadLexerTestSupport.Single("include < a b.scad >");

        // Assert
        Assert.Equal("x.scad", spaced.Text);
        Assert.Equal("y.scad", split.Text);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), split.Location);
        Assert.Equal(" a b.scad ", untrimmed.Text);
    }

    /// <summary>
    ///     Validates that the directive location is reported on later lines too.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_DirectiveAfterOtherTokens_CarriesKeywordLocation()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("x;\n include <a>");

        // Assert: keyword at line 2, column 2
        Assert.Equal(new ScadSourceLocation(null, 2, 2), tokens[2].Location);
    }

    /// <summary>
    ///     Validates that a directive keyword without an angle bracket is rejected at the keyword.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="word">The directive keyword named in the message.</param>
    [Theory]
    [InlineData("include foo", "include")]
    [InlineData("use", "use")]
    [InlineData("x; use \"a.scad\";", "use")]
    public void ScadLexer_Tokenize_DirectiveWithoutAngleBracket_Throws(string source, string word)
    {
        // Arrange / Act / Assert: the keyword is the error location
        var column = source.IndexOf(word, StringComparison.Ordinal) + 1;
        ScadLexerTestSupport.AssertLexError(source, 1, column, $"Expected '<' after '{word}'");
    }

    /// <summary>
    ///     Validates that a path that is not closed on its line is rejected.
    /// </summary>
    /// <param name="source">The source text.</param>
    [Theory]
    [InlineData("include <abc")]
    [InlineData("include <abc\nx>")]
    [InlineData("use <ab\r")]
    public void ScadLexer_Tokenize_DirectiveUnterminated_Throws(string source)
    {
        // Arrange / Act / Assert: reported at the keyword
        ScadLexerTestSupport.AssertLexError(source, 1, 1, "Unterminated include/use path");
    }

    /// <summary>
    ///     Validates that an empty path is rejected.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_DirectiveEmptyPath_Throws()
    {
        // Arrange / Act / Assert
        ScadLexerTestSupport.AssertLexError("include <>", 1, 1, "Empty include/use path");
    }
}
