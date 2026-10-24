// cspell:ignore xmodule
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadLexer"/>: identifiers, keywords, operators and unexpected characters.
/// </summary>
public class ScadLexerTests
{
    /// <summary>
    ///     Validates that ordinary ASCII identifiers produce identifier tokens carrying their name.
    /// </summary>
    /// <param name="name">The identifier.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("abc_1")]
    [InlineData("_x")]
    [InlineData("Abc9")]
    [InlineData("__")]
    public void ScadLexer_Tokenize_Identifier_ProducesIdentifierToken(string name)
    {
        // Arrange / Act: lex the identifier alone
        var token = ScadLexerTestSupport.Single(name);

        // Assert: an identifier carrying its own name
        Assert.Equal(ScadTokenKind.Identifier, token.Kind);
        Assert.Equal(name, token.Text);
    }

    /// <summary>
    ///     Validates that <c>$</c>-prefixed names are identifiers that keep the dollar sign.
    /// </summary>
    /// <param name="name">The special variable name.</param>
    [Theory]
    [InlineData("$fn")]
    [InlineData("$t")]
    [InlineData("$1")]
    [InlineData("$_a")]
    public void ScadLexer_Tokenize_SpecialVariable_ProducesIdentifierWithDollar(string name)
    {
        // Arrange / Act: lex the special variable
        var token = ScadLexerTestSupport.Single(name);

        // Assert: identifier kind with the dollar in the text
        Assert.Equal(ScadTokenKind.Identifier, token.Kind);
        Assert.Equal(name, token.Text);
        Assert.Equal('$', token.Text![0]);
    }

    /// <summary>
    ///     Validates that a lone <c>$</c> is rejected at its own position.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="column">The expected column of the dollar sign.</param>
    [Theory]
    [InlineData("$", 1)]
    [InlineData("a = $;", 5)]
    [InlineData("$ fn", 1)]
    public void ScadLexer_Tokenize_LoneDollar_Throws(string source, int column)
    {
        // Arrange / Act / Assert: the dollar has no name
        ScadLexerTestSupport.AssertLexError(source, 1, column, "Expected a name after '$'");
    }

    /// <summary>
    ///     Validates that an identifier cannot begin with a digit: a number and an identifier are produced.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_IdentifierStartingWithDigit_SplitsNumberAndIdentifier()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("9lives");

        // Assert: number 9 then identifier "lives"
        Assert.Equal("Number Identifier", ScadLexerTestSupport.Kinds("9lives"));
        Assert.Equal(9, tokens[0].Number);
        Assert.Equal("lives", tokens[1].Text);
        Assert.Equal(2, tokens[1].Location.Column);
    }

    /// <summary>
    ///     Validates that every reserved word maps to its keyword kind.
    /// </summary>
    /// <param name="word">The keyword.</param>
    /// <param name="kind">The expected kind name.</param>
    [Theory]
    [InlineData("module", "Module")]
    [InlineData("function", "Function")]
    [InlineData("if", "If")]
    [InlineData("else", "Else")]
    [InlineData("for", "For")]
    [InlineData("intersection_for", "IntersectionFor")]
    [InlineData("let", "Let")]
    [InlineData("each", "Each")]
    [InlineData("assert", "Assert")]
    [InlineData("echo", "Echo")]
    [InlineData("true", "True")]
    [InlineData("false", "False")]
    [InlineData("undef", "Undef")]
    public void ScadLexer_Tokenize_Keyword_ProducesKeywordKind(string word, string kind)
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single(word);

        // Assert: the keyword kind with the lexeme as text
        Assert.Equal(kind, token.Kind.ToString());
        Assert.Equal(word, token.Text);
    }

    /// <summary>
    ///     Validates that identifiers merely containing, extending or differing in case from a keyword
    ///     remain identifiers.
    /// </summary>
    /// <param name="word">The identifier.</param>
    [Theory]
    [InlineData("modules")]
    [InlineData("xmodule")]
    [InlineData("If")]
    [InlineData("for_")]
    [InlineData("_for")]
    [InlineData("true1")]
    [InlineData("Undef")]
    [InlineData("intersection_for2")]
    [InlineData("included")]
    [InlineData("user")]
    [InlineData("PI")]
    [InlineData("children")]
    public void ScadLexer_Tokenize_KeywordPrefixOrSuffix_IsIdentifier(string word)
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single(word);

        // Assert: not a keyword
        Assert.Equal(ScadTokenKind.Identifier, token.Kind);
        Assert.Equal(word, token.Text);
    }

    /// <summary>
    ///     Validates every operator and punctuation lexeme.
    /// </summary>
    /// <param name="lexeme">The operator text.</param>
    /// <param name="kind">The expected kind name.</param>
    [Theory]
    [InlineData("(", "LeftParen")]
    [InlineData(")", "RightParen")]
    [InlineData("{", "LeftBrace")]
    [InlineData("}", "RightBrace")]
    [InlineData("[", "LeftBracket")]
    [InlineData("]", "RightBracket")]
    [InlineData(";", "Semicolon")]
    [InlineData(",", "Comma")]
    [InlineData(".", "Dot")]
    [InlineData(":", "Colon")]
    [InlineData("?", "Question")]
    [InlineData("=", "Assign")]
    [InlineData("==", "EqualEqual")]
    [InlineData("!=", "NotEqual")]
    [InlineData("<", "Less")]
    [InlineData("<=", "LessEqual")]
    [InlineData(">", "Greater")]
    [InlineData(">=", "GreaterEqual")]
    [InlineData("&&", "AndAnd")]
    [InlineData("||", "OrOr")]
    [InlineData("+", "Plus")]
    [InlineData("-", "Minus")]
    [InlineData("*", "Star")]
    [InlineData("/", "Slash")]
    [InlineData("%", "Percent")]
    [InlineData("^", "Caret")]
    [InlineData("!", "Bang")]
    [InlineData("#", "Hash")]
    public void ScadLexer_Tokenize_Operator_ProducesOperatorKind(string lexeme, string kind)
    {
        // Arrange / Act
        var token = ScadLexerTestSupport.Single(lexeme);

        // Assert: the operator kind with its lexeme
        Assert.Equal(kind, token.Kind.ToString());
        Assert.Equal(lexeme, token.Text);
    }

    /// <summary>
    ///     Validates that adjacent operator characters use longest match.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_AdjacentOperators_UsesLongestMatch()
    {
        // Arrange / Act / Assert: each source splits greedily from the left
        Assert.Equal("LessEqual Assign", ScadLexerTestSupport.Kinds("<=="));
        Assert.Equal("EqualEqual Assign", ScadLexerTestSupport.Kinds("==="));
        Assert.Equal("NotEqual Assign", ScadLexerTestSupport.Kinds("!=="));
        Assert.Equal("LessEqual GreaterEqual", ScadLexerTestSupport.Kinds("<=>="));
        Assert.Equal("Assign Bang", ScadLexerTestSupport.Kinds("=!"));
        Assert.Equal("AndAnd Bang", ScadLexerTestSupport.Kinds("&&!"));
        Assert.Equal("Less Less", ScadLexerTestSupport.Kinds("< <"));
    }

    /// <summary>
    ///     Validates that a minus sign is never part of a number literal.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_MinusAndNumber_ProducesSeparateTokens()
    {
        // Arrange / Act / Assert: sign handling is the parser's job
        Assert.Equal("Minus Number", ScadLexerTestSupport.Kinds("-5"));
        Assert.Equal("Identifier Minus Number", ScadLexerTestSupport.Kinds("a-5"));
        Assert.Equal("Number Minus Number", ScadLexerTestSupport.Kinds("1-5"));
    }

    /// <summary>
    ///     Validates that a lone slash is the divide operator and does not start a comment.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_SlashAlone_IsDivide()
    {
        // Arrange / Act / Assert
        Assert.Equal("Identifier Slash Identifier", ScadLexerTestSupport.Kinds("a/b"));
        Assert.Equal("Slash", ScadLexerTestSupport.Kinds("/"));
    }

    /// <summary>
    ///     Validates that a single <c>&amp;</c> or <c>|</c> is rejected with a hint at the doubled operator.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="column">The expected error column.</param>
    /// <param name="hint">The hinted operator.</param>
    [Theory]
    [InlineData("&", 1, "'&&'")]
    [InlineData("|", 1, "'||'")]
    [InlineData("a & b", 3, "'&&'")]
    [InlineData("a | b", 3, "'||'")]
    public void ScadLexer_Tokenize_SingleAmpersandOrPipe_Throws(string source, int column, string hint)
    {
        // Arrange / Act / Assert: error at the character, mentioning the doubled form
        ScadLexerTestSupport.AssertLexError(source, 1, column, "Unexpected character");
        ScadLexerTestSupport.AssertLexError(source, 1, column, hint);
    }

    /// <summary>
    ///     Validates that characters that cannot start a token are rejected with their location.
    /// </summary>
    /// <param name="text">The offending character.</param>
    [Theory]
    [InlineData("@")]
    [InlineData("`")]
    [InlineData("\\")]
    [InlineData("~")]
    [InlineData("'")]
    [InlineData("é")]
    public void ScadLexer_Tokenize_UnexpectedCharacter_ThrowsWithLocation(string text)
    {
        // Arrange: the character sits at line 2, column 3
        var source = "x;\n  " + text + " y";

        // Act / Assert
        ScadLexerTestSupport.AssertLexError(source, 2, 3, $"Unexpected character '{text}'");
    }

    /// <summary>
    ///     Validates that a null source is rejected.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_NullSource_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScadLexer.Tokenize(null!, null));
    }

    /// <summary>
    ///     Validates that an ordinary statement produces the expected kind sequence and ends with end-of-file.
    /// </summary>
    [Fact]
    public void ScadLexer_Tokenize_Statement_ProducesTokensEndingWithEndOfFile()
    {
        // Arrange / Act
        var tokens = ScadLexerTestSupport.Lex("cube([1, 2.5, 3], center = true);");

        // Assert: full kind sequence and a final end-of-file token with no text
        Assert.Equal(
            "Identifier LeftParen LeftBracket Number Comma Number Comma Number RightBracket Comma Identifier Assign True RightParen Semicolon",
            ScadLexerTestSupport.Kinds("cube([1, 2.5, 3], center = true);"));
        Assert.Equal(ScadTokenKind.EndOfFile, tokens[^1].Kind);
        Assert.Null(tokens[^1].Text);
    }
}
