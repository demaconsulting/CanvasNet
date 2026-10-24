// cspell:ignore ifor
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadParser"/> control flow: conditionals, loops, and the let, echo and assert forms.
/// </summary>
public class ScadParserControlFlowTests
{
    /// <summary>
    ///     Validates an <c>if</c> without <c>else</c>.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_If_WithoutElse_ProducesIfStatement()
    {
        // Arrange / Act
        var program = ScadParserTestSupport.Parse("if (a) b();");

        // Assert: no else branch is represented as null
        var statement = Assert.IsType<ScadIfStatement>(Assert.Single(program.Statements));
        Assert.Null(statement.Else);
        Assert.Equal("(if a (then (inst b ())))", ScadAstDumper.DumpStatements(program));
    }

    /// <summary>
    ///     Validates an <c>if</c> with <c>else</c>.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_If_Else_ProducesBothBranches()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(if a (then (inst b ())) (else (inst c ())))",
            ScadParserTestSupport.Stmts("if (a) b(); else c();"));
    }

    /// <summary>
    ///     Validates an else-if chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_If_ElseIfChain_NestsInElseBranch()
    {
        // Arrange / Act
        var dump = ScadParserTestSupport.Stmts("if (a) x(); else if (b) y(); else z();");

        // Assert
        Assert.Equal("(if a (then (inst x ())) (else (if b (then (inst y ())) (else (inst z ())))))", dump);
    }

    /// <summary>
    ///     Validates that a dangling else binds to the nearest if.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_If_DanglingElse_BindsToNearestIf()
    {
        // Arrange / Act
        var dump = ScadParserTestSupport.Stmts("if (a) if (b) x(); else y();");

        // Assert: the else belongs to the inner if
        Assert.Equal("(if a (then (if b (then (inst x ())) (else (inst y ())))))", dump);
    }

    /// <summary>
    ///     Validates empty and braced branches.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_If_EmptyBody_ProducesEmptyBranches()
    {
        // Arrange / Act
        var program = ScadParserTestSupport.Parse("if (a) ; else ;");

        // Assert: an empty else is an empty list, distinct from a missing else
        var statement = Assert.IsType<ScadIfStatement>(Assert.Single(program.Statements));
        Assert.Empty(statement.Then);
        Assert.NotNull(statement.Else);
        Assert.Empty(statement.Else);
        Assert.Equal(
            "(if a (then (inst x ()) (inst y ())) (else (inst z ())))",
            ScadParserTestSupport.Stmts("if (a) { x(); y(); } else { z(); }"));
    }

    /// <summary>
    ///     Validates a for loop with one range iterator.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_For_SingleIteratorRange_ProducesForStatement()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(for ((i (range 0 3))) (inst cube (i)))",
            ScadParserTestSupport.Stmts("for (i = [0:3]) cube(i);"));
    }

    /// <summary>
    ///     Validates multiple iterators and a trailing comma.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_For_MultipleIterators_ProducesAllIterators()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(for ((i (range 0 2)) (j (range 0 2))) (inst c ()))",
            ScadParserTestSupport.Stmts("for (i=[0:2], j=[0:2]) c();"));
        Assert.Equal("(for ((i (range 0 2))) (inst c ()))", ScadParserTestSupport.Stmts("for (i=[0:2],) c();"));
    }

    /// <summary>
    ///     Validates a vector iterator and a braced body.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_For_VectorIterator_ProducesForStatement()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(for ((v (vec (vec 1 2) (vec 3 4)))) (inst f (v)) (inst g ()))",
            ScadParserTestSupport.Stmts("for (v = [[1,2],[3,4]]) { f(v); g(); }"));
    }

    /// <summary>
    ///     Validates <c>intersection_for</c>.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_IntersectionFor_ProducesIntersectionForStatement()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(ifor ((i (range 1 3))) (inst rotate (i) (inst cube (1))))",
            ScadParserTestSupport.Stmts("intersection_for (i = [1:3]) rotate(i) cube(1);"));
    }

    /// <summary>
    ///     Validates that a C-style loop header is rejected in statement position.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_For_CStyleInStatementPosition_Throws()
    {
        // Arrange / Act / Assert: error at the first semicolon
        ScadParserTestSupport.AssertParseError(
            "for (i=0; i<3; i=i+1) a();",
            1,
            9,
            "A C-style 'for' loop is only allowed inside a list comprehension");
    }

    /// <summary>
    ///     Validates that a loop without iterators is rejected.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="found">The offending token.</param>
    [Theory]
    [InlineData("for () a();", ")")]
    [InlineData("for (;;) a();", ";")]
    [InlineData("intersection_for () a();", ")")]
    public void ScadParser_Parse_For_NoIterators_Throws(string source, string found)
    {
        // Arrange / Act
        var column = source.IndexOf('(', StringComparison.Ordinal) + 2;

        // Assert
        ScadParserTestSupport.AssertParseError(source, 1, column, $"Expected an identifier but found '{found}'");
    }

    /// <summary>
    ///     Validates a let statement.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_LetStatement_ProducesLetStatement()
    {
        // Arrange / Act / Assert
        Assert.Equal("(let ((a 1) (b 2)) (inst c (a b)))", ScadParserTestSupport.Stmts("let (a = 1, b = 2) c(a, b);"));
        Assert.Equal("(let () (inst a ()))", ScadParserTestSupport.Stmts("let () a();"));
    }

    /// <summary>
    ///     Validates an echo statement with and without a child.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_EchoStatement_ProducesEchoStatement()
    {
        // Arrange / Act / Assert
        Assert.Equal("(echo (\"hi\" x))", ScadParserTestSupport.Stmts("echo(\"hi\", x);"));
        Assert.Equal("(echo (1) (inst a ()))", ScadParserTestSupport.Stmts("echo(1) a();"));
    }

    /// <summary>
    ///     Validates an assert statement.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_AssertStatement_ProducesAssertStatement()
    {
        // Arrange / Act / Assert
        Assert.Equal("(assert ((> x 0) \"bad\"))", ScadParserTestSupport.Stmts("assert(x > 0, \"bad\");"));
        Assert.Equal("(assert ((arg condition x)) (inst a ()))", ScadParserTestSupport.Stmts("assert(condition = x) a();"));
    }

    /// <summary>
    ///     Validates an echo expression.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_EchoExpression_ProducesEchoExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(echo (1) 2)", ScadParserTestSupport.Expr("echo(1) 2"));
    }

    /// <summary>
    ///     Validates an assert expression.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_AssertExpression_ProducesAssertExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(assert (a) (+ a 1))", ScadParserTestSupport.Expr("assert(a) a + 1"));
    }

    /// <summary>
    ///     Validates a let expression.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_LetExpression_ProducesLetExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(let ((a 2) (b 3)) (* a b))", ScadParserTestSupport.Expr("let(a=2, b=3) a*b"));
    }

    /// <summary>
    ///     Validates that a let expression body extends as far right as possible.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_LetExpression_BodyExtendsRight_ConsumesRemainingExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(+ 1 (let ((x 2)) (* x 3)))", ScadParserTestSupport.Expr("1+let(x=2) x*3"));
        Assert.Equal("(let ((x 2)) (?: x 1 0))", ScadParserTestSupport.Expr("let(x=2) x ? 1 : 0"));
    }
}
