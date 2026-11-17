using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadLexer"/> string literals and escape sequences.
/// </summary>
public class ScadLexerStringTests
{
    /// <summary>
    ///     Validates that a string literal yields a string token carrying its value.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_String_ProducesStringTokenWithValue()
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single("\"abc def\"");

        // Assert
        Assert.Equal(ScadTokenKind.String, token.Kind);
        Assert.Equal("abc def", token.Text);
    }

    /// <summary>
    ///     Validates that an empty string is accepted.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_EmptyString_ProducesEmptyValue()
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single("\"\"");

        // Assert
        Assert.Equal(ScadTokenKind.String, token.Kind);
        Assert.Equal(string.Empty, token.Text);
    }

    /// <summary>
    ///     Validates that raw line breaks inside strings are kept and counted.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_StringContainingNewline_TracksLineAndColumn()
    {
        // Arrange / Act: the string spans two lines and is followed by an identifier
        var tokens = ScadLexerTestSupport.Lex("\"a\nb\" x");
        var crlf = ScadLexerTestSupport.Single("\"a\r\nb\"");

        // Assert: value keeps the break; the next token is on line 2 after the closing quote
        Assert.Equal("a\nb", tokens[0].Text);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), tokens[0].Location);
        Assert.Equal(new ScadSourceLocation(null, 2, 4), tokens[1].Location);
        Assert.Equal("a\r\nb", crlf.Text);
    }

    /// <summary>
    ///     Validates that an unterminated string is reported at its opening quote.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    [Theory]
    [InlineData("x \"abc", 1, 3)]
    [InlineData("\"abc\\", 1, 1)]
    [InlineData("\n  \"abc\n", 2, 3)]
    [InlineData("\"", 1, 1)]
    public void ScadLexer_Tokenize_UnterminatedString_ThrowsAtOpeningQuote(string source, int line, int column)
    {
        // Arrange / Act / Assert
        ScadLexerTestSupport.AssertLexError(source, line, column, "Unterminated string literal");
    }

    /// <summary>
    ///     Validates the single-character escapes.
    /// </summary>
    /// <param name="literal">The string body as written in source.</param>
    /// <param name="expected">The decoded value.</param>
    [Theory]
    [InlineData("\\n", "\n")]
    [InlineData("\\t", "\t")]
    [InlineData("\\r", "\r")]
    [InlineData("\\\"", "\"")]
    [InlineData("\\\\", "\\")]
    public void ScadLexer_Tokenize_SimpleEscapes_ProducesControlCharacters(string literal, string expected)
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single("\"" + literal + "\"");

        // Assert
        Assert.Equal(expected, token.Text);
    }

    /// <summary>
    ///     Validates the two-digit hexadecimal escape.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_HexEscape_ProducesLatin1Character()
    {
        // Arrange / Act / Assert: ASCII, the top of the range, and NUL
        Assert.Equal("A", ScadLexerTestSupport.Single("\"\\x41\"").Text);
        Assert.Equal("\u00FF", ScadLexerTestSupport.Single("\"\\xff\"").Text);
        Assert.Equal("\0", ScadLexerTestSupport.Single("\"\\x00\"").Text);
    }

    /// <summary>
    ///     Validates the four-digit Unicode escape.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_ShortUnicodeEscape_ProducesBmpCharacter()
    {
        // Arrange / Act / Assert: includes the code points adjacent to the surrogate range
        Assert.Equal("A", ScadLexerTestSupport.Single("\"\\u0041\"").Text);
        Assert.Equal("\u20AC", ScadLexerTestSupport.Single("\"\\u20AC\"").Text);
        Assert.Equal("\uD7FF", ScadLexerTestSupport.Single("\"\\uD7FF\"").Text);
        Assert.Equal("\uE000", ScadLexerTestSupport.Single("\"\\uE000\"").Text);
    }

    /// <summary>
    ///     Validates the six-digit Unicode escape, including a surrogate pair above U+FFFF.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_LongUnicodeEscape_ProducesSurrogatePair()
    {
        // Arrange / Act
        var emoji = ScadLexerTestSupport.Single("\"\\U01F600\"").Text;
        var ascii = ScadLexerTestSupport.Single("\"\\U000041\"").Text;
        var max = ScadLexerTestSupport.Single("\"\\U10FFFF\"").Text;

        // Assert: U+1F600 needs two UTF-16 units; the maximum scalar is accepted
        Assert.Equal("\uD83D\uDE00", emoji);
        Assert.Equal("A", ascii);
        Assert.Equal("\uDBFF\uDFFF", max);
    }

    /// <summary>
    ///     Validates that an unknown escape is rejected at the backslash.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_UnknownEscape_ThrowsAtBackslash()
    {
        // Arrange / Act / Assert: the backslash is at column 4
        ScadLexerTestSupport.AssertLexError("\"ab\\qcd\"", 1, 4, "Invalid escape sequence '\\q'");
    }

    /// <summary>
    ///     Validates that missing or non-hexadecimal escape digits are rejected at the backslash.
    /// </summary>
    /// <param name="source">The source text.</param>
    [Theory]
    [InlineData("\"\\x\"")]
    [InlineData("\"\\x4\"")]
    [InlineData("\"\\xZZ\"")]
    [InlineData("\"\\u00\"")]
    [InlineData("\"\\u12G4\"")]
    [InlineData("\"\\U00041\"")]
    [InlineData("\"\\Uzzzzzz\"")]
    [InlineData("\"\\x4")]
    public void ScadLexer_Tokenize_TruncatedHexEscape_Throws(string source)
    {
        // Arrange / Act / Assert: the backslash is at column 2
        ScadLexerTestSupport.AssertLexError(source, 1, 2, "hexadecimal digits");
    }

    /// <summary>
    ///     Validates that surrogate and out-of-range code points are rejected.
    /// </summary>
    /// <param name="source">The source text.</param>
    [Theory]
    [InlineData("\"\\uD800\"")]
    [InlineData("\"\\uDFFF\"")]
    [InlineData("\"\\U00D800\"")]
    [InlineData("\"\\U110000\"")]
    [InlineData("\"\\UFFFFFF\"")]
    public void ScadLexer_Tokenize_SurrogateOrOutOfRangeCodePoint_Throws(string source)
    {
        // Arrange / Act / Assert
        ScadLexerTestSupport.AssertLexError(source, 1, 2, "not a valid Unicode scalar value");
    }

    /// <summary>
    ///     Validates that an escaped quote does not terminate the string.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_EscapedQuote_DoesNotTerminateString()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("\"a\\\"b\" c");

        // Assert: one string and one identifier
        Assert.Equal("a\"b", tokens[0].Text);
        Assert.Equal("c", tokens[1].Text);
    }
}
