using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadParser"/> operator precedence and associativity.
/// </summary>
public class ScadParserPrecedenceTests
{
    /// <summary>
    ///     Validates the binary precedence levels and the oracle cases of the grammar table.
    /// </summary>
    /// <param name="source">The expression source.</param>
    /// <param name="expected">The expected dump.</param>
    [Theory]
    [InlineData("1+2*3", "(+ 1 (* 2 3))")]
    [InlineData("1*2+3", "(+ (* 1 2) 3)")]
    [InlineData("a||b&&c", "(|| a (&& b c))")]
    [InlineData("a&&b||c&&d", "(|| (&& a b) (&& c d))")]
    [InlineData("a==b<c", "(== a (< b c))")]
    [InlineData("a<b==c<d", "(== (< a b) (< c d))")]
    [InlineData("a==b&&c!=d", "(&& (== a b) (!= c d))")]
    [InlineData("a+b<c+d", "(< (+ a b) (+ c d))")]
    [InlineData("a<=b>=c", "(>= (<= a b) c)")]
    [InlineData("-a*b", "(* (neg a) b)")]
    [InlineData("!a&&b", "(&& (! a) b)")]
    [InlineData("!a==b", "(== (! a) b)")]
    [InlineData("2^3*4", "(* (^ 2 3) 4)")]
    [InlineData("a.x^2", "(^ (member a x) 2)")]
    [InlineData("a||b?c:d", "(?: (|| a b) c d)")]
    [InlineData("a?b:c||d", "(?: a b (|| c d))")]
    public void ScadParser_Parse_Precedence_BinaryLevels_GroupAsTable(string source, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, ScadParserTestSupport.Expr(source));
    }

    /// <summary>
    ///     Validates that all left-associative operators group from the left.
    /// </summary>
    /// <param name="source">The expression source.</param>
    /// <param name="expected">The expected dump.</param>
    [Theory]
    [InlineData("1-2-3", "(- (- 1 2) 3)")]
    [InlineData("a/b/c", "(/ (/ a b) c)")]
    [InlineData("a%b*c", "(* (% a b) c)")]
    [InlineData("a*b%c/d", "(/ (% (* a b) c) d)")]
    [InlineData("a-b+c", "(+ (- a b) c)")]
    [InlineData("a<b<c", "(< (< a b) c)")]
    [InlineData("a==b!=c", "(!= (== a b) c)")]
    [InlineData("a&&b&&c", "(&& (&& a b) c)")]
    [InlineData("a||b||c", "(|| (|| a b) c)")]
    public void ScadParser_Parse_Associativity_LeftAssociativeOperators_GroupLeft(string source, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, ScadParserTestSupport.Expr(source));
    }

    /// <summary>
    ///     Validates that the exponent operator is right-associative.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Power_IsRightAssociative()
    {
        // Arrange / Act / Assert
        Assert.Equal("(^ 2 (^ 3 2))", ScadParserTestSupport.Expr("2^3^2"));
        Assert.Equal("(^ 2 (^ 3 (^ 4 5)))", ScadParserTestSupport.Expr("2^3^4^5"));
    }

    /// <summary>
    ///     Validates that the exponent binds tighter than unary minus.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Power_BindsTighterThanUnaryMinus()
    {
        // Arrange / Act / Assert
        Assert.Equal("(neg (^ 2 2))", ScadParserTestSupport.Expr("-2^2"));
        Assert.Equal("(! (^ a b))", ScadParserTestSupport.Expr("!a^b"));
        Assert.Equal("(neg (^ a (^ b c)))", ScadParserTestSupport.Expr("-a^b^c"));
    }

    /// <summary>
    ///     Validates that the exponent operand may carry a prefix operator.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Power_AllowsUnaryExponent()
    {
        // Arrange / Act / Assert
        Assert.Equal("(^ 2 (neg 1))", ScadParserTestSupport.Expr("2^-1"));
        Assert.Equal("(^ 2 (! (neg 1)))", ScadParserTestSupport.Expr("2^!-1"));
        Assert.Equal("(^ 2 (neg (^ 3 2)))", ScadParserTestSupport.Expr("2^-3^2"));
    }

    /// <summary>
    ///     Validates that the conditional operator is right-associative.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Ternary_IsRightAssociative()
    {
        // Arrange / Act / Assert: the else branch absorbs a following conditional; the then branch is a full expression
        Assert.Equal("(?: a b (?: c d e))", ScadParserTestSupport.Expr("a?b:c?d:e"));
        Assert.Equal("(?: a (?: b c d) e)", ScadParserTestSupport.Expr("a?b?c:d:e"));
    }

    /// <summary>
    ///     Validates that the condition of a conditional is a logical-or expression.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Ternary_ConditionIsLogicalOr()
    {
        // Arrange / Act / Assert
        Assert.Equal("(?: (|| a b) (&& c d) e)", ScadParserTestSupport.Expr("a||b ? c&&d : e"));
    }

    /// <summary>
    ///     Validates prefix operator chains.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Unary_Chains_NestRightward()
    {
        // Arrange / Act / Assert
        Assert.Equal("(! (neg (pos a)))", ScadParserTestSupport.Expr("!-+a"));
        Assert.Equal("(neg (neg a))", ScadParserTestSupport.Expr("- -a"));
        Assert.Equal("(! (! a))", ScadParserTestSupport.Expr("!!a"));
        Assert.Equal("(neg (call f 1))", ScadParserTestSupport.Expr("-f(1)"));
    }

    /// <summary>
    ///     Validates that parentheses override precedence and leave no node of their own.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Parentheses_OverridePrecedence()
    {
        // Arrange / Act / Assert
        Assert.Equal("(* (+ 1 2) 3)", ScadParserTestSupport.Expr("(1+2)*3"));
        Assert.Equal("(neg (+ 1 2))", ScadParserTestSupport.Expr("-(1+2)"));
        Assert.Equal("a", ScadParserTestSupport.Expr("((a))"));
        Assert.Equal("(^ (^ 2 3) 2)", ScadParserTestSupport.Expr("(2^3)^2"));
    }

    /// <summary>
    ///     Validates that a minus sign is never folded into a literal.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Unary_MinusLiteral_IsNotConstantFolded()
    {
        // Arrange / Act
        var node = Assert.IsType<ScadUnaryExpression>(ScadParserTestSupport.ExprNode("-5"));

        // Assert
        Assert.Equal(ScadUnaryOperator.Negate, node.Operator);
        Assert.Equal(5.0, Assert.IsType<ScadNumberLiteral>(node.Operand).Value);
    }

    /// <summary>
    ///     Validates that each binary operator token maps to its operator.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Binary_EveryOperator_MapsToOperatorKind()
    {
        // Arrange
        var expected = new Dictionary<string, ScadBinaryOperator>
        {
            ["+"] = ScadBinaryOperator.Add,
            ["-"] = ScadBinaryOperator.Subtract,
            ["*"] = ScadBinaryOperator.Multiply,
            ["/"] = ScadBinaryOperator.Divide,
            ["%"] = ScadBinaryOperator.Modulo,
            ["^"] = ScadBinaryOperator.Power,
            ["<"] = ScadBinaryOperator.Less,
            ["<="] = ScadBinaryOperator.LessEqual,
            [">"] = ScadBinaryOperator.Greater,
            [">="] = ScadBinaryOperator.GreaterEqual,
            ["=="] = ScadBinaryOperator.Equal,
            ["!="] = ScadBinaryOperator.NotEqual,
            ["&&"] = ScadBinaryOperator.And,
            ["||"] = ScadBinaryOperator.Or,
        };

        // Act / Assert
        foreach (var (symbol, op) in expected)
        {
            var node = Assert.IsType<ScadBinaryExpression>(ScadParserTestSupport.ExprNode($"a {symbol} b"));
            Assert.Equal(op, node.Operator);
        }
    }
}
