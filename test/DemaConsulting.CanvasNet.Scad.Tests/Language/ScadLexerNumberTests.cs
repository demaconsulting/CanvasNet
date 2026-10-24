using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadLexer"/> numeric literals.
/// </summary>
public class ScadLexerNumberTests
{
    /// <summary>
    ///     Validates integer and decimal forms.
    /// </summary>
    /// <param name="source">The literal.</param>
    /// <param name="expected">The expected value.</param>
    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("42", 42.0)]
    [InlineData("007", 7.0)]
    [InlineData("3.25", 3.25)]
    [InlineData("1.5", 1.5)]
    public void ScadLexer_Tokenize_DecimalNumber_ProducesNumberTokenWithValue(string source, double expected)
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single(source);

        // Assert: number kind, value, and lexeme text
        Assert.Equal(ScadTokenKind.Number, token.Kind);
        Assert.Equal(expected, token.Number);
        Assert.Equal(source, token.Text);
    }

    /// <summary>
    ///     Validates a literal that starts with a dot.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_LeadingDotNumber_ProducesNumberToken()
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single(".5");

        // Assert
        Assert.Equal(ScadTokenKind.Number, token.Kind);
        Assert.Equal(0.5, token.Number);
    }

    /// <summary>
    ///     Validates a literal that ends with a dot.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_TrailingDotNumber_ProducesNumberToken()
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single("5.");

        // Assert: the dot is part of the number
        Assert.Equal(ScadTokenKind.Number, token.Kind);
        Assert.Equal(5.0, token.Number);
    }

    /// <summary>
    ///     Validates exponent forms.
    /// </summary>
    /// <param name="source">The literal.</param>
    /// <param name="expected">The expected value.</param>
    [Theory]
    [InlineData("1e3", 1000.0)]
    [InlineData("2.5E-2", 0.025)]
    [InlineData("1e+2", 100.0)]
    [InlineData(".5e1", 5.0)]
    [InlineData("5.e1", 50.0)]
    [InlineData("1E0", 1.0)]
    public void ScadLexer_Tokenize_ExponentNumber_ProducesNumberTokenWithValue(string source, double expected)
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single(source);

        // Assert
        Assert.Equal(ScadTokenKind.Number, token.Kind);
        Assert.Equal(expected, token.Number, 12);
    }

    /// <summary>
    ///     Validates that an exponent marker without digits is not part of the number.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_ExponentWithoutDigits_SplitsNumberAndIdentifier()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("2e");

        // Assert: number 2 then identifier "e"
        Assert.Equal("Number Identifier", ScadLexerTestSupport.Kinds("2e"));
        Assert.Equal(2.0, tokens[0].Number);
        Assert.Equal("e", tokens[1].Text);
        Assert.Equal("Number Identifier Plus", ScadLexerTestSupport.Kinds("2e+"));
        Assert.Equal("Number Identifier Minus Identifier", ScadLexerTestSupport.Kinds("2e-x"));
    }

    /// <summary>
    ///     Validates that an overflowing exponent yields infinity rather than an error.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_ExponentOverflow_ProducesInfinity()
    {
        // Arrange / Act
        var big = ScadLexerTestSupport.Single("1e999");
        var small = ScadLexerTestSupport.Single("1e-999");

        // Assert: positive infinity and zero (underflow)
        Assert.True(double.IsPositiveInfinity(big.Number));
        Assert.Equal(0.0, small.Number);
    }

    /// <summary>
    ///     Validates that letters directly after a number start a new identifier token.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_NumberFollowedByLetters_SplitsIntoNumberAndIdentifier()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("2cm");

        // Assert
        Assert.Equal("Number Identifier", ScadLexerTestSupport.Kinds("2cm"));
        Assert.Equal("cm", tokens[1].Text);
        Assert.Equal(2, tokens[1].Location.Column);
    }

    /// <summary>
    ///     Validates that a dot after an identifier is member access, while a dot before a digit starts a number.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_DotBeforeDigitOrLetter_DistinguishesNumberFromMember()
    {
        // Arrange / Act / Assert
        Assert.Equal("Identifier Dot Identifier", ScadLexerTestSupport.Kinds("a.x"));
        Assert.Equal("Identifier Number", ScadLexerTestSupport.Kinds("a.5"));
        Assert.Equal("Number Number", ScadLexerTestSupport.Kinds("1.2.3"));
    }
}
