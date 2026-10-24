using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadLexer"/> comment handling.
/// </summary>
public class ScadLexerCommentTests
{
    /// <summary>
    ///     Validates that a line comment is skipped to the end of its line.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_LineComment_IsSkippedToEndOfLine()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("a // b c\nd");

        // Assert: only a and d remain; d is on line 2
        Assert.Equal("Identifier Identifier", ScadLexerTestSupport.Kinds("a // b c\nd"));
        Assert.Equal(new ScadSourceLocation(null, 2, 1), tokens[1].Location);
    }

    /// <summary>
    ///     Validates that a line comment at the end of the file needs no line break.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_LineCommentAtEndOfFileWithoutNewline_IsSkipped()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("a // end");

        // Assert: identifier then end-of-file
        Assert.Equal(2, tokens.Count);
        Assert.Equal(ScadTokenKind.EndOfFile, tokens[1].Kind);
    }

    /// <summary>
    ///     Validates that a line comment ends at a carriage return as well.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_LineCommentEndedByCarriageReturn_ContinuesOnNextLine()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("a // x\r\nb\r// y\rc");

        // Assert: b on line 2, c on line 4
        Assert.Equal(2, tokens[1].Location.Line);
        Assert.Equal(4, tokens[2].Location.Line);
        Assert.Equal(1, tokens[2].Location.Column);
    }

    /// <summary>
    ///     Validates that a block comment is skipped and its lines are counted.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_BlockComment_IsSkippedAndCountsLines()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("a /* x\ny */ b");

        // Assert: b follows the comment on line 2, column 6
        Assert.Equal("Identifier Identifier", ScadLexerTestSupport.Kinds("a /* x\ny */ b"));
        Assert.Equal(new ScadSourceLocation(null, 2, 6), tokens[1].Location);
    }

    /// <summary>
    ///     Validates that block comments do not nest: the first terminator closes the comment.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_BlockCommentsDoNotNest_FirstTerminatorCloses()
    {
        // Arrange / Act: "/* a /* b */" is a comment; " c */" remains
        var kinds = ScadLexerTestSupport.Kinds("/* a /* b */ c */");

        // Assert: c, then the stray "*" and "/" operators
        Assert.Equal("Identifier Star Slash", kinds);
    }

    /// <summary>
    ///     Validates that comment markers inside strings are inert.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_CommentMarkersInsideString_AreNotComments()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("\"// not\" \"/* x */\"");

        // Assert: two strings with their original text
        Assert.Equal(3, tokens.Count);
        Assert.Equal("// not", tokens[0].Text);
        Assert.Equal("/* x */", tokens[1].Text);
    }

    /// <summary>
    ///     Validates that an unterminated block comment is reported at its opening marker.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    [Theory]
    [InlineData("a\n  /* never closed", 2, 3)]
    [InlineData("/*/", 1, 1)]
    [InlineData("/*", 1, 1)]
    public void ScadLexer_Tokenize_UnterminatedBlockComment_ThrowsAtCommentStart(string source, int line, int column)
    {
        // Arrange / Act / Assert
        ScadLexerTestSupport.AssertLexError(source, line, column, "Unterminated block comment");
    }

    /// <summary>
    ///     Validates the minimal closed block comment forms.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_EmptyBlockComment_IsSkipped()
    {
        // Arrange / Act / Assert
        Assert.Equal(string.Empty, ScadLexerTestSupport.Kinds("/**/"));
        Assert.Equal("Identifier", ScadLexerTestSupport.Kinds("/***/a"));
    }
}
