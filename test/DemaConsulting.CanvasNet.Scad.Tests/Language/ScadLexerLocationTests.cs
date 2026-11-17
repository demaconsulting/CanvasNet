using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadLexer"/> location tracking.
/// </summary>
public class ScadLexerLocationTests
{
    /// <summary>
    ///     Validates one-based line and column numbers across lines.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_Tokens_CarryOneBasedLineAndColumn()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("a b\n  c");

        // Assert: a, b on line 1; c on line 2; end-of-file just after c
        Assert.Equal(new ScadSourceLocation(null, 1, 1), tokens[0].Location);
        Assert.Equal(new ScadSourceLocation(null, 1, 3), tokens[1].Location);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), tokens[2].Location);
        Assert.Equal(new ScadSourceLocation(null, 2, 4), tokens[3].Location);
    }

    /// <summary>
    ///     Validates that each line-end convention counts as exactly one line.
    /// </summary>
    /// <param name="newline">The line terminator.</param>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void ScadLexer_Tokenize_LineEndings_CountOneLineEach(string newline)
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex($"a{newline}b{newline}c");

        // Assert: lines 1, 2, 3, each at column 1
        Assert.Equal(new ScadSourceLocation(null, 1, 1), tokens[0].Location);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), tokens[1].Location);
        Assert.Equal(new ScadSourceLocation(null, 3, 1), tokens[2].Location);
    }

    /// <summary>
    ///     Validates that mixed and consecutive line terminators are counted individually.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_MixedLineEndings_CountEachBreak()
    {
        // Arrange / Act: CRLF, then a lone CR, then LF, then LF CR (which is two breaks)
        var tokens = ScadLexerTestSupport.Lex("a\r\n\rb\n\n\rc");

        // Assert: a=1, b=3 (CRLF + CR), c=6 (LF, LF, CR)
        Assert.Equal(1, tokens[0].Location.Line);
        Assert.Equal(3, tokens[1].Location.Line);
        Assert.Equal(6, tokens[2].Location.Line);
    }

    /// <summary>
    ///     Validates that a tab counts as one column.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_Tab_CountsAsOneColumn()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("\ta\tb");

        // Assert: a at column 2, b at column 4
        Assert.Equal(2, tokens[0].Location.Column);
        Assert.Equal(4, tokens[1].Location.Column);
    }

    /// <summary>
    ///     Validates that vertical tab and form feed are whitespace.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_VerticalTabAndFormFeed_AreWhitespace()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("a\v\fb");

        // Assert: b at column 4 on the same line
        Assert.Equal(new ScadSourceLocation(null, 1, 4), tokens[1].Location);
    }

    /// <summary>
    ///     Validates that the file name is carried by every token, including end-of-file, and by errors.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_FileName_IsPropagatedToTokenLocations()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("a b", "dir/f.scad");

        // Assert: all locations name the file
        Assert.All(tokens, t => Assert.Equal("dir/f.scad", t.Location.File));
        ScadTestHelpers.AssertSyntaxError(() => ScadLexerTestSupport.Lex("a @", "dir/f.scad"), 1, 3, "Unexpected character", "dir/f.scad");
    }

    /// <summary>
    ///     Validates the position of the end-of-file token.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("abc", 1, 4)]
    [InlineData("a\n", 2, 1)]
    [InlineData("a\r\n  ", 2, 3)]
    public void ScadLexer_Tokenize_EndOfFileToken_IsAppendedAtEndLocation(string source, int line, int column)
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex(source);

        // Assert: exactly one end-of-file token, last, with no text
        var eof = tokens[^1];
        Assert.Equal(1, tokens.Count(t => t.Kind == ScadTokenKind.EndOfFile));
        Assert.Equal(new ScadSourceLocation(null, line, column), eof.Location);
        Assert.Null(eof.Text);
    }

    /// <summary>
    ///     Validates that a leading byte-order mark is skipped without consuming a column.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_LeadingByteOrderMark_IsSkipped()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("\uFEFFa");
        var onlyBom = ScadLexerTestSupport.Lex("\uFEFF");

        // Assert: a is at column 1; a BOM alone is an empty program
        Assert.Equal(new ScadSourceLocation(null, 1, 1), tokens[0].Location);
        Assert.Single(onlyBom);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), onlyBom[0].Location);
    }

    /// <summary>
    ///     Validates that a byte-order mark anywhere else is an unexpected character.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_ByteOrderMarkInsideSource_Throws()
    {
        // Arrange / Act / Assert
        ScadLexerTestSupport.AssertLexError("a\uFEFF", 1, 2, "Unexpected character");
    }
}
