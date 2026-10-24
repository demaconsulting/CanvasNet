using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadParser"/> expressions: literals, vectors, ranges, postfix forms and function literals.
/// </summary>
public class ScadParserExpressionTests
{
    /// <summary>
    ///     Validates literal and identifier expressions.
    /// </summary>
    /// <param name="source">The expression source.</param>
    /// <param name="expected">The expected dump.</param>
    [Theory]
    [InlineData("42", "42")]
    [InlineData("2.5", "2.5")]
    [InlineData("1e3", "1000")]
    [InlineData(".5", "0.5")]
    [InlineData("\"s\"", "\"s\"")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("undef", "undef")]
    [InlineData("PI", "PI")]
    [InlineData("$fn", "$fn")]
    [InlineData("[]", "(vec)")]
    [InlineData("[1,2,3]", "(vec 1 2 3)")]
    public void ScadParser_Parse_Literals_ProduceLiteralNodes(string source, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, ScadParserTestSupport.Expr(source));
    }

    /// <summary>
    ///     Validates that the literal nodes carry their values.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Literals_CarryTypedValues()
    {
        // Arrange / Act / Assert
        Assert.Equal(7.5, Assert.IsType<ScadNumberLiteral>(ScadParserTestSupport.ExprNode("7.5")).Value);
        Assert.Equal("a\tb", Assert.IsType<ScadStringLiteral>(ScadParserTestSupport.ExprNode("\"a\\tb\"")).Value);
        Assert.True(Assert.IsType<ScadBooleanLiteral>(ScadParserTestSupport.ExprNode("true")).Value);
        Assert.False(Assert.IsType<ScadBooleanLiteral>(ScadParserTestSupport.ExprNode("false")).Value);
        Assert.IsType<ScadUndefLiteral>(ScadParserTestSupport.ExprNode("undef"));
        Assert.Equal("$fn", Assert.IsType<ScadIdentifierExpression>(ScadParserTestSupport.ExprNode("$fn")).Name);
    }

    /// <summary>
    ///     Validates that a single trailing comma is accepted in vectors.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Vector_TrailingComma_IsAccepted()
    {
        // Arrange / Act / Assert
        Assert.Equal("(vec 1 2)", ScadParserTestSupport.Expr("[1,2,]"));
        Assert.Equal("(vec 1)", ScadParserTestSupport.Expr("[1,]"));
    }

    /// <summary>
    ///     Validates nested vectors.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Vector_Nested_ProducesNestedVectors()
    {
        // Arrange / Act / Assert
        Assert.Equal("(vec (vec 1 2) (vec 3 (vec 4)))", ScadParserTestSupport.Expr("[[1,2],[3,[4]]]"));
    }

    /// <summary>
    ///     Validates that empty vector elements are rejected at the offending comma.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Vector_EmptyElement_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = [1,,2];", 1, 8, "Expected an expression but found ','");
        ScadParserTestSupport.AssertParseError("x = [,];", 1, 6, "Expected an expression but found ','");
    }

    /// <summary>
    ///     Validates a two-part range.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Range_TwoParts_ProducesRangeWithoutStep()
    {
        // Arrange / Act
        var node = Assert.IsType<ScadRangeExpression>(ScadParserTestSupport.ExprNode("[0:5]"));

        // Assert
        Assert.Null(node.Step);
        Assert.Equal("(range 0 5)", ScadParserTestSupport.Expr("[0:5]"));
    }

    /// <summary>
    ///     Validates a three-part range in written order, including a negative step.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Range_ThreeParts_ProducesRangeWithStepInWrittenOrder()
    {
        // Arrange / Act / Assert: start:step:end
        Assert.Equal("(range 0 2 10)", ScadParserTestSupport.Expr("[0:2:10]"));
        Assert.Equal("(range 10 (neg 2) 0)", ScadParserTestSupport.Expr("[10:-2:0]"));
    }

    /// <summary>
    ///     Validates arbitrary expressions as range bounds.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Range_ExpressionBounds_ProducesBoundExpressions()
    {
        // Arrange / Act / Assert
        Assert.Equal("(range (+ a 1) (* b 2))", ScadParserTestSupport.Expr("[a+1 : b*2]"));
        Assert.Equal("(range 0 (call f 3))", ScadParserTestSupport.Expr("[0 : f(3)]"));
    }

    /// <summary>
    ///     Validates that a ternary start consumes its own colon before the range colon.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Range_TernaryStart_ConsumesTernaryColonFirst()
    {
        // Arrange / Act / Assert
        Assert.Equal("(range (?: a 1 2) 9)", ScadParserTestSupport.Expr("[a?1:2 : 9]"));
    }

    /// <summary>
    ///     Validates that commas in a range are rejected.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Range_WithComma_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = [1:2,3];", 1, 9, "Expected ']' but found ','");
    }

    /// <summary>
    ///     Validates that a four-part range is rejected.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Range_FourParts_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = [1:2:3:4];", 1, 11, "Expected ']' but found ':'");
    }

    /// <summary>
    ///     Validates that a list comprehension cannot start a range.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Range_ComprehensionStart_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("x = [for (i=a) i : 3];", 1, 18, "Expected ',' or ']' but found ':'");
    }

    /// <summary>
    ///     Validates index expressions.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Index_ProducesIndexExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(index a 0)", ScadParserTestSupport.Expr("a[0]"));
        Assert.Equal("(index a (+ i 1))", ScadParserTestSupport.Expr("a[i+1]"));
        Assert.Equal("(index (vec 1 2) 0)", ScadParserTestSupport.Expr("[1,2][0]"));
    }

    /// <summary>
    ///     Validates member access with the conventional component names and with any identifier.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Member_XYZAndGenericIdentifier_ProducesMemberExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(member p x)", ScadParserTestSupport.Expr("p.x"));
        Assert.Equal("(member p y)", ScadParserTestSupport.Expr("p.y"));
        Assert.Equal("(member p z)", ScadParserTestSupport.Expr("p.z"));
        Assert.Equal("(member p foo)", ScadParserTestSupport.Expr("p.foo"));
    }

    /// <summary>
    ///     Validates a call on an identifier with positional and named arguments.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Call_OnIdentifier_ProducesCallExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(call f 1 (arg n 2))", ScadParserTestSupport.Expr("f(1, n=2)"));
        Assert.Equal("(call f)", ScadParserTestSupport.Expr("f()"));
        Assert.Equal("(call f 1 2)", ScadParserTestSupport.Expr("f(1, 2,)"));
    }

    /// <summary>
    ///     Validates a call on a parenthesized expression.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Call_OnParenthesizedExpression_ProducesCallExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(call f 1)", ScadParserTestSupport.Expr("(f)(1)"));
        Assert.Equal("(call (?: c f g) 1)", ScadParserTestSupport.Expr("(c ? f : g)(1)"));
    }

    /// <summary>
    ///     Validates an immediate call of a function literal.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Call_OnFunctionLiteral_ProducesCallExpression()
    {
        // Arrange / Act / Assert
        Assert.Equal("(call (fn (x) (* x 2)) 4)", ScadParserTestSupport.Expr("(function(x) x*2)(4)"));
    }

    /// <summary>
    ///     Validates that postfix operators apply left to right.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_PostfixChain_LeftToRight_NestsLeftward()
    {
        // Arrange / Act / Assert
        Assert.Equal("(member (index (index a 0) 1) x)", ScadParserTestSupport.Expr("a[0][1].x"));
        Assert.Equal("(member (index (call (call f 1) 2) 3) x)", ScadParserTestSupport.Expr("f(1)(2)[3].x"));
    }

    /// <summary>
    ///     Validates that a call result can be indexed and member-accessed.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Call_ResultIndexedAndMember_ProducesPostfixChain()
    {
        // Arrange / Act / Assert
        Assert.Equal("(member (index (call g 1) 2) x)", ScadParserTestSupport.Expr("g(1)[2].x"));
    }

    /// <summary>
    ///     Validates a function literal with parameters and a body.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_FunctionLiteral_ProducesFunctionLiteral()
    {
        // Arrange / Act / Assert
        Assert.Equal("(fn (x) (* x 2))", ScadParserTestSupport.Expr("function(x) x*2"));
        Assert.Equal("(fn (x (= y 1)) (+ x y))", ScadParserTestSupport.Expr("function(x, y=1) x+y"));
        Assert.Equal("(fn () 1)", ScadParserTestSupport.Expr("function() 1"));
    }

    /// <summary>
    ///     Validates that a function literal body extends as far right as possible.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_FunctionLiteral_BodyExtendsRight_ConsumesOperators()
    {
        // Arrange / Act / Assert
        Assert.Equal("(fn (x) (+ x 1))", ScadParserTestSupport.Expr("function(x) x + 1"));
        Assert.Equal("(fn (x) (?: x 1 2))", ScadParserTestSupport.Expr("function(x) x ? 1 : 2"));
    }

    /// <summary>
    ///     Validates function literals as arguments, which end at the next comma.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_FunctionLiteral_AsArgument_StopsAtComma()
    {
        // Arrange / Act / Assert
        Assert.Equal("(inst a ((fn (v) (* v 2)) 3))", ScadParserTestSupport.Stmts("a(function(v) v*2, 3);"));
    }

    /// <summary>
    ///     Validates nested function literals.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_FunctionLiteral_Nested_ProducesCurriedLiteral()
    {
        // Arrange / Act / Assert
        Assert.Equal("(fn (a) (fn (b) (+ a b)))", ScadParserTestSupport.Expr("function(a) function(b) a+b"));
    }
}
