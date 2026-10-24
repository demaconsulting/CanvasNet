using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadParser"/> error reporting: first-error-only, exact locations and messages.
/// </summary>
public class ScadParserErrorTests
{
    /// <summary>
    ///     Validates that a missing semicolon is reported at the next token.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_MissingSemicolon_ReportsNextToken()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("a = 1\nb = 2;", 2, 1, "Expected ';' but found 'b'");
        ScadParserTestSupport.AssertParseError("x = 1 y = 2;", 1, 7, "Expected ';' but found 'y'");
    }

    /// <summary>
    ///     Validates that an unclosed brace is reported at the end of input.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_UnclosedBrace_ReportsEndOfInput()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("{ a();\n", 2, 1, "Unexpected end of input; expected '}'");
        ScadParserTestSupport.AssertParseError("module m() {\n  x();", 2, 7, "Unexpected end of input; expected '}'");
    }

    /// <summary>
    ///     Validates unclosed and mismatched parentheses.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_UnclosedParenthesis_ReportsExpectedParenthesis()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("a(1, 2", 1, 7, "Unexpected end of input; expected ')'");
        ScadParserTestSupport.AssertParseError("a(1 2);", 1, 5, "Expected ')' but found '2'");
        ScadParserTestSupport.AssertParseError("x = (1 + 2;", 1, 11, "Expected ')' but found ';'");
    }

    /// <summary>
    ///     Validates unclosed and mismatched brackets.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_UnclosedBracket_ReportsExpectedBracket()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = [1, 2", 1, 10, "Unexpected end of input; expected ']'");
        ScadParserTestSupport.AssertParseError("x = [1 2];", 1, 8, "Expected ']' but found '2'");
        ScadParserTestSupport.AssertParseError("x = a[1;", 1, 8, "Expected ']' but found ';'");
        ScadParserTestSupport.AssertParseError("x = a[];", 1, 7, "Expected an expression but found ']'");
    }

    /// <summary>
    ///     Validates that a stray closing brace is reported at the brace.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_UnexpectedClosingBrace_ReportsToken()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("a(); }", 1, 6, "Unexpected token '}'");
    }

    /// <summary>
    ///     Validates missing expressions.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_ExpressionExpected_ReportsToken()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = ;", 1, 5, "Expected an expression but found ';'");
        ScadParserTestSupport.AssertParseError("x = ", 1, 5, "Unexpected end of input; expected an expression");
        ScadParserTestSupport.AssertParseError("x = 1 + ;", 1, 9, "Expected an expression but found ';'");
        ScadParserTestSupport.AssertParseError("x = * 2;", 1, 5, "Expected an expression but found '*'");
        ScadParserTestSupport.AssertParseError("f(,);", 1, 3, "Expected an expression but found ','");
    }

    /// <summary>
    ///     Validates tokens that cannot start a statement.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="message">The expected message fragment.</param>
    [Theory]
    [InlineData("42;", "Unexpected token '42'")]
    [InlineData("else a();", "Unexpected token 'else'")]
    [InlineData("\"s\";", "Unexpected token \"s\"")]
    [InlineData(")", "Unexpected token ')'")]
    [InlineData("]", "Unexpected token ']'")]
    [InlineData("= 1;", "Unexpected token '='")]
    [InlineData("true;", "Unexpected token 'true'")]
    public void ScadParser_Parse_StatementStart_InvalidToken_ReportsUnexpectedToken(string source, string message)
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError(source, 1, 1, message);
    }

    /// <summary>
    ///     Validates an identifier that is followed by neither <c>=</c> nor <c>(</c>.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_IdentifierWithoutAssignOrCall_ReportsFollowingToken()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("foo;", 1, 4, "Expected '=' or '(' after 'foo' but found ';'");
        ScadParserTestSupport.AssertParseError("foo", 1, 4, "Unexpected end of input; expected '=' or '(' after 'foo'");
        ScadParserTestSupport.AssertParseError("x = 1;\n  b c();", 2, 5, "Expected '=' or '(' after 'b' but found 'c'");
    }

    /// <summary>
    ///     Validates that an assignment cannot be an unbraced child.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_AssignmentAsSingleChild_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("a() b = 1;", 1, 5, "Use braces around a declaration");
        ScadParserTestSupport.AssertParseError("if (x) y = 1;", 1, 8, "Use braces around a declaration");
    }

    /// <summary>
    ///     Validates that a module or function definition cannot be an unbraced child.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_DefinitionAsSingleChild_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("a() module m() {}", 1, 5, "Use braces around a declaration");
        ScadParserTestSupport.AssertParseError("if (x) function f() = 1;", 1, 8, "Use braces around a declaration");
    }

    /// <summary>
    ///     Validates the error for a child that is neither an instantiation nor a block.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_ChildMissing_ReportsExpectedChild()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("a()", 1, 4, "Unexpected end of input; expected ';'");
        ScadParserTestSupport.AssertParseError("a() }", 1, 5, "Expected ';', '{' or a module instantiation but found '}'");
        ScadParserTestSupport.AssertParseError("a() 5;", 1, 5, "Expected ';', '{' or a module instantiation but found '5'");
        ScadParserTestSupport.AssertParseError("a() b;", 1, 5, "Expected '(' after 'b'");
    }

    /// <summary>
    ///     Validates that <c>function</c> at statement start requires a name.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_FunctionKeywordWithoutName_AtStatementStart_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("function (x) = x;", 1, 10, "Expected a function name after 'function' but found '('");
        ScadParserTestSupport.AssertParseError("function", 1, 9, "Unexpected end of input; expected a function name after 'function'");
    }

    /// <summary>
    ///     Validates that only the first of several errors is reported.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_MultipleErrors_ReportsFirstOnly()
    {
        // Arrange / Act
        var ex = ScadParserTestSupport.AssertParseError("x = ;\ny = ;\nz = ;", 1, 5, "Expected an expression");

        // Assert: nothing about the later lines
        Assert.DoesNotContain(":2:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that messages carry file, line and column.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_ErrorMessage_ContainsFileLineColumn()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadParserTestSupport.Parse("a = 1;\nx = ;", "dir/f.scad"));

        // Assert
        Assert.Equal("dir/f.scad:2:5: Expected an expression but found ';'", ex.Message);
        Assert.Equal(new ScadSourceLocation("dir/f.scad", 2, 5), ex.Location);
    }

    /// <summary>
    ///     Validates assorted malformed constructs and their locations.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_MalformedConstructs_ReportExpectedLocations()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("module (x) {}", 1, 8, "Expected an identifier but found '('");
        ScadParserTestSupport.AssertParseError("if a b();", 1, 4, "Expected '(' but found 'a'");
        ScadParserTestSupport.AssertParseError("echo 1;", 1, 6, "Expected '(' but found '1'");
        ScadParserTestSupport.AssertParseError("x = a.;", 1, 7, "Expected an identifier but found ';'");
        ScadParserTestSupport.AssertParseError("x = a.5;", 1, 6, "Expected ';' but found '.5'");
        ScadParserTestSupport.AssertParseError("x = 2cm;", 1, 6, "Expected ';' but found 'cm'");
        ScadParserTestSupport.AssertParseError("f(1 = 2);", 1, 5, "Expected ')' but found '='");
        ScadParserTestSupport.AssertParseError("x = function;", 1, 13, "Expected '(' but found ';'");
        ScadParserTestSupport.AssertParseError("for (i 5) a();", 1, 8, "Expected '=' but found '5'");
        ScadParserTestSupport.AssertParseError("function f(a) 1;", 1, 15, "Expected '=' but found '1'");
        ScadParserTestSupport.AssertParseError("x = a ? b;", 1, 10, "Expected ':' but found ';'");
        ScadParserTestSupport.AssertParseError("x = 1 +", 1, 8, "Unexpected end of input; expected an expression");
    }

    /// <summary>
    ///     Validates that a null source is rejected.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_NullSource_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScadParser.Parse((string)null!, null));
        Assert.Throws<ArgumentNullException>(() => ScadParser.Parse((IReadOnlyList<ScadToken>)null!));
    }

    /// <summary>
    ///     Validates that an empty or comment-only source is an empty program.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_EmptySource_ProducesEmptyProgram()
    {
        // Arrange / Act
        var program = ScadParserTestSupport.Parse("  // nothing\n/* here */ ", "f.scad");

        // Assert: no statements and a program location at the start of the file
        Assert.Empty(program.Statements);
        Assert.Equal(new ScadSourceLocation("f.scad", 1, 1), program.Location);
    }
}
