using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for the hostile-input limits of <see cref="ScadParser"/>: nesting depth, tree height and source size.
/// </summary>
/// <remarks>
///     Depth accounting: a statement and each expression entry cost one level, so a file-scope assignment
///     <c>x = ...;</c> already uses two levels before any nesting; every nested parenthesis, prefix operator,
///     <c>^</c>, ternary branch, child or block adds one more. The height of the finished tree is also limited
///     to the same value, which is what bounds left-associative operator and postfix chains.
/// </remarks>
public class ScadParserLimitTests
{
    /// <summary>The nesting limit under test.</summary>
    private const int Limit = ScadParser.MaxNestingDepth;

    /// <summary>
    ///     Parses an assignment of the given expression and returns the result for assertions.
    /// </summary>
    /// <param name="expression">The expression source.</param>
    /// <returns>The parsed program.</returns>
    private static ScadProgram ParseValue(string expression) => ScadParserTestSupport.Parse($"x = {expression};");

    /// <summary>
    ///     Asserts that parsing fails with the nesting error.
    /// </summary>
    /// <param name="source">The source.</param>
    private static void AssertTooDeep(string source)
    {
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadParserTestSupport.Parse(source));
        Assert.Contains($"Nesting too deep (limit {Limit})", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, ex.Location.Line);
        Assert.True(ex.Location.Column > 0);
    }

    /// <summary>
    ///     Validates that parenthesis nesting exactly at the limit is accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_Parentheses_AtLimit_Succeeds()
    {
        // Arrange: the assignment and the outer expression use two levels
        var n = Limit - 2;

        // Act
        var program = ParseValue(ScadParserTestSupport.Repeat("(", n) + "1" + ScadParserTestSupport.Repeat(")", n));

        // Assert: the parentheses leave no nodes, so the value is the bare literal
        Assert.IsType<ScadNumberLiteral>(Assert.IsType<ScadAssignmentStatement>(program.Statements[0]).Value);
    }

    /// <summary>
    ///     Validates that one more parenthesis level is rejected.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_Parentheses_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        var n = Limit - 1;
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("(", n)}1{ScadParserTestSupport.Repeat(")", n)};");
    }

    /// <summary>
    ///     Validates that deeply nested vectors are rejected and moderately nested ones accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_Vectors_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        var ok = ScadParserTestSupport.Repeat("[", 60) + "1" + ScadParserTestSupport.Repeat("]", 60);
        Assert.NotNull(ParseValue(ok));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("[", 300)}1{ScadParserTestSupport.Repeat("]", 300)};");
    }

    /// <summary>
    ///     Validates a prefix operator chain at and over the limit.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_UnaryChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert: 254 prefix operators plus two levels is exactly the limit
        Assert.NotNull(ParseValue(ScadParserTestSupport.Repeat("-", Limit - 2) + "1"));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("-", Limit - 1)}1;");
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("!", 100000)}1;");
    }

    /// <summary>
    ///     Validates a right-nested conditional chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_TernaryChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue(ScadParserTestSupport.Repeat("a?b:", 50) + "c"));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("a?b:", 300)}c;");
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("a?", 300)}b{ScadParserTestSupport.Repeat(":c", 300)};");
    }

    /// <summary>
    ///     Validates a right-associative exponent chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_PowerChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue(ScadParserTestSupport.Repeat("2^", 100) + "2"));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("2^", 300)}2;");
    }

    /// <summary>
    ///     Validates nested block statements.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_BlockStatements_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ScadParserTestSupport.Parse(ScadParserTestSupport.Repeat("{", 100) + ScadParserTestSupport.Repeat("}", 100)));
        AssertTooDeep(ScadParserTestSupport.Repeat("{", 300) + ScadParserTestSupport.Repeat("}", 300));
    }

    /// <summary>
    ///     Validates a chain of unbraced single children.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_SingleChildChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ScadParserTestSupport.Parse(ScadParserTestSupport.Repeat("a() ", 100) + ";"));
        AssertTooDeep(ScadParserTestSupport.Repeat("a() ", 300) + ";");
    }

    /// <summary>
    ///     Validates a long else-if chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_ElseIfChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ScadParserTestSupport.Parse(ScadParserTestSupport.Repeat("if (a) b(); else ", 50) + "c();"));
        AssertTooDeep(ScadParserTestSupport.Repeat("if (a) b(); else ", 300) + "c();");
    }

    /// <summary>
    ///     Validates nested comprehension elements.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_ComprehensionChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue("[" + ScadParserTestSupport.Repeat("for (i=a) ", 50) + "1]"));
        AssertTooDeep($"x = [{ScadParserTestSupport.Repeat("for (i=a) ", 300)}1];");
    }

    /// <summary>
    ///     Validates that a left-associative operator chain is limited by the height of the finished tree.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_LeftAssociativeChain_OverLimit_Throws()
    {
        // Arrange: a chain of n operands has tree depth n + 1 below the program (assignment + operators)
        static string Chain(int operands) => "1" + ScadParserTestSupport.Repeat("+1", operands - 1);

        // Act / Assert: 255 operands reach exactly the limit; 256 and a hostile length are rejected
        Assert.NotNull(ParseValue(Chain(Limit - 1)));
        AssertTooDeep($"x = {Chain(Limit)};");
        AssertTooDeep($"x = {Chain(100000)};");
    }

    /// <summary>
    ///     Validates that a long left-associative chain well under the limit is accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_LeftAssociativeChain_UnderLimit_Succeeds()
    {
        // Arrange / Act
        var program = ParseValue("1" + ScadParserTestSupport.Repeat("*2", 199));

        // Assert
        Assert.Single(program.Statements);
    }

    /// <summary>
    ///     Validates that long postfix chains are limited by the height of the finished tree.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_PostfixChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue("a" + ScadParserTestSupport.Repeat("[0]", 100)));
        AssertTooDeep($"x = a{ScadParserTestSupport.Repeat("[0]", 300)};");
        AssertTooDeep($"x = a{ScadParserTestSupport.Repeat(".x", 300)};");
        AssertTooDeep($"x = a{ScadParserTestSupport.Repeat("(1)", 300)};");
    }

    /// <summary>
    ///     Validates that left-leaning chains stacked through parentheses are limited by tree height even
    ///     though the parser itself recurses only a few levels per chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_InteriorLeftChainsAcrossLevels_OverLimit_Throws()
    {
        // Arrange: each level wraps the previous expression as the first operand of a 10-operator chain
        static string Build(int levels)
        {
            var expression = "1";
            for (var i = 0; i < levels; i++)
            {
                expression = "(" + expression + ")" + ScadParserTestSupport.Repeat("+1", 10);
            }

            return expression;
        }

        // Act / Assert: the parser recursion depth is only the number of levels, but the tree height is ~11 per level
        Assert.NotNull(ParseValue(Build(10)));
        AssertTooDeep($"x = {Build(40)};");
    }

    /// <summary>
    ///     Validates the exact failure location: the deepest offending node or token is on the first line.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_MultiLineInput_ReportsLineOfOffendingConstruct()
    {
        // Arrange: the deep chain starts on line 3
        var source = "a = 1;\nb = 2;\nx = " + ScadParserTestSupport.Repeat("-", 400) + "1;";

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadParserTestSupport.Parse(source));

        // Assert
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that hostile input on a small stack fails closed instead of overflowing the stack.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_OnSmallStackThread_DoesNotOverflow()
    {
        // Arrange: several hostile shapes, each far beyond the limit
        var inputs = new[]
        {
            $"x = {ScadParserTestSupport.Repeat("(", 100000)}1;",
            $"x = {ScadParserTestSupport.Repeat("[", 100000)}1;",
            $"x = {ScadParserTestSupport.Repeat("-", 100000)}1;",
            $"x = {ScadParserTestSupport.Repeat("a?", 100000)}b;",
            ScadParserTestSupport.Repeat("{", 100000),
            ScadParserTestSupport.Repeat("a() ", 100000),
            $"x = {ScadParserTestSupport.Repeat("1+", 100000)}1;",
        };
        var errors = new Exception?[inputs.Length];

        // Act: run each parse on a thread with a 1 MiB stack
        var thread = new Thread(
            () =>
            {
                for (var i = 0; i < inputs.Length; i++)
                {
                    errors[i] = Record.Exception(() => ScadParserTestSupport.Parse(inputs[i]));
                }
            },
            1024 * 1024);
        thread.Start();
        thread.Join();

        // Assert: every input produced the typed syntax exception (reaching here means no stack overflow)
        Assert.All(errors, e => Assert.IsType<ScadSyntaxException>(e));
    }

    /// <summary>
    ///     Validates that a source of exactly the maximum length is accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_SourceLength_AtLimit_Parses()
    {
        // Arrange: a valid statement padded with blanks to the limit
        var source = "x=1;" + new string(' ', ScadParser.MaxSourceLength - 4);

        // Act
        var program = ScadParserTestSupport.Parse(source);

        // Assert
        Assert.Equal(ScadParser.MaxSourceLength, source.Length);
        Assert.Single(program.Statements);
    }

    /// <summary>
    ///     Validates that a longer source is rejected with a plain <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_SourceLength_OverLimit_ThrowsInvalidDataExceptionNotSyntaxException()
    {
        // Arrange
        var source = "x=1;" + new string(' ', ScadParser.MaxSourceLength - 3);

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => ScadParserTestSupport.Parse(source));

        // Assert: the exact type, no location involved
        Assert.IsType<InvalidDataException>(ex);
        Assert.Contains("4194304", ex.Message, StringComparison.Ordinal);
    }
}
