using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of expression evaluation in <see cref="DemaConsulting.CanvasNet.Scad.Evaluation.ScadEvaluator"/>: operators, indexing, undef
///     propagation and short-circuiting. Each case echoes one expression and compares its echo form.
/// </summary>
public class ScadEvaluatorExpressionTests
{
    /// <summary>
    ///     Validates the result of scalar arithmetic and precedence.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The echo text of its value.</param>
    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("7 % 3", "1")]
    [InlineData("-7 % 3", "-1")]
    [InlineData("2 ^ 10", "1024")]
    [InlineData("2 ^ 3 ^ 2", "512")]
    [InlineData("-(3)", "-3")]
    [InlineData("+4", "4")]
    [InlineData("0.1 + 0.2", "0.3")]
    [InlineData("1e3", "1000")]
    public void ScadEvaluatorExpression_Scalars_ProduceExpectedNumber(string expression, string expected)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var text = EchoText(source);

        // Assert
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Validates division by zero follows IEEE rules without any diagnostic.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The echo text.</param>
    [Theory]
    [InlineData("1 / 0", "inf")]
    [InlineData("-1 / 0", "-inf")]
    [InlineData("0 / 0", "nan")]
    [InlineData("5 % 0", "nan")]
    public void ScadEvaluatorExpression_DivisionByZero_FollowsIeeeWithoutWarning(string expression, string expected)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal($"ECHO: {expected}", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
        Assert.Equal(0, output.InstanceCount);
    }

    /// <summary>
    ///     Validates vector and matrix arithmetic.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The echo text.</param>
    [Theory]
    [InlineData("[1, 2, 3] + [4, 5, 6]", "[5, 7, 9]")]
    [InlineData("[5, 7, 9] - [4, 5, 6]", "[1, 2, 3]")]
    [InlineData("[1, 2, 3] * 2", "[2, 4, 6]")]
    [InlineData("2 * [1, 2, 3]", "[2, 4, 6]")]
    [InlineData("[2, 4] / 2", "[1, 2]")]
    [InlineData("[1, 2, 3] * [4, 5, 6]", "32")]
    [InlineData("[[1, 0], [0, 1]] * [3, 4]", "[3, 4]")]
    [InlineData("[3, 4] * [[1, 0], [0, 1]]", "[3, 4]")]
    [InlineData("[[1, 2], [3, 4]] * [[0, 1], [1, 0]]", "[[2, 1], [4, 3]]")]
    [InlineData("-[1, -2]", "[-1, 2]")]
    [InlineData("[[1, 2], [3, 4]] + [[1, 1], [1, 1]]", "[[2, 3], [4, 5]]")]
    public void ScadEvaluatorExpression_VectorAndMatrixArithmetic_ProducesExpectedValue(string expression, string expected)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var echo = Echo(source);

        // Assert
        Assert.Equal([$"ECHO: {expected}"], echo);
    }

    /// <summary>
    ///     Validates mismatched vector shapes yield undef elements or undef with a warning rather than failing.
    /// </summary>
    [Fact]
    public void ScadEvaluatorExpression_MismatchedVectors_YieldUndefWithWarning()
    {
        // Arrange
        const string Source = "echo([1, 2] + [1, 2, 3]);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: undef", Assert.Single(output.Echo));
        Assert.Single(output.Diagnostics);
    }

    /// <summary>
    ///     Validates indexing and member access, including every out-of-range edge.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The echo text.</param>
    [Theory]
    [InlineData("[10, 20, 30][1]", "20")]
    [InlineData("[10, 20, 30][0]", "10")]
    [InlineData("[10, 20, 30][3]", "undef")]
    [InlineData("[10, 20, 30][-1]", "undef")]
    [InlineData("[10, 20, 30][1.9]", "20")]
    [InlineData("[10, 20, 30][\"a\"]", "undef")]
    [InlineData("[10, 20, 30][undef]", "undef")]
    [InlineData("\"abc\"[1]", "\"b\"")]
    [InlineData("\"abc\"[3]", "undef")]
    [InlineData("5[0]", "undef")]
    [InlineData("undef[0]", "undef")]
    [InlineData("[[1, 2], [3, 4]][1][0]", "3")]
    [InlineData("[7, 8, 9].x", "7")]
    [InlineData("[7, 8, 9].y", "8")]
    [InlineData("[7, 8, 9].z", "9")]
    [InlineData("[7, 8].z", "undef")]
    [InlineData("[7, 8].w", "undef")]
    [InlineData("[0 : 5][1]", "undef")]
    public void ScadEvaluatorExpression_IndexingAndMembers_ReturnExpectedValue(string expression, string expected)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal($"ECHO: {expected}", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates comparison, equality, logical operators and truthiness.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The echo text.</param>
    [Theory]
    [InlineData("1 < 2", "true")]
    [InlineData("2 <= 2", "true")]
    [InlineData("3 > 4", "false")]
    [InlineData("[1, 2] < [1, 3]", "true")]
    [InlineData("\"a\" < \"b\"", "true")]
    [InlineData("1 == 1", "true")]
    [InlineData("1 != 1", "false")]
    [InlineData("\"1\" == 1", "false")]
    [InlineData("undef == undef", "true")]
    [InlineData("[1, [2]] == [1, [2]]", "true")]
    [InlineData("(0 / 0) == (0 / 0)", "false")]
    [InlineData("true && false", "false")]
    [InlineData("false || true", "true")]
    [InlineData("!0", "true")]
    [InlineData("!\"x\"", "false")]
    [InlineData("1 && \"x\"", "true")]
    [InlineData("0 || \"\"", "false")]
    [InlineData("[] ? 1 : 2", "2")]
    [InlineData("[0] ? 1 : 2", "1")]
    [InlineData("\"\" ? 1 : 2", "2")]
    [InlineData("undef ? 1 : 2", "2")]
    [InlineData("(0 / 0) ? 1 : 2", "1")]
    [InlineData("true ? 1 : 2", "1")]
    public void ScadEvaluatorExpression_ComparisonAndLogic_ProduceExpectedValue(string expression, string expected)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal(expected, output.Echo[0]["ECHO: ".Length..]);
        Assert.Single(output.Echo);
    }

    /// <summary>
    ///     Validates that the logical operators short-circuit: the skipped operand's side effect never happens.
    /// </summary>
    [Fact]
    public void ScadEvaluatorExpression_LogicalOperators_ShortCircuit()
    {
        // Arrange
        const string Source = """
            function noisy(tag, v) = echo(tag) v;
            a = false && noisy("and-right", true);
            b = true || noisy("or-right", true);
            c = true && noisy("and-evaluated", true);
            d = false || noisy("or-evaluated", false);
            """;

        // Act
        var echo = EchoText(Source);

        // Assert
        Assert.Equal("\"and-evaluated\"|\"or-evaluated\"", echo);
    }

    /// <summary>
    ///     Validates that undef operands propagate as undef and warn.
    /// </summary>
    /// <param name="expression">The expression.</param>
    [Theory]
    [InlineData("undef + 1")]
    [InlineData("1 - undef")]
    [InlineData("undef * 2")]
    [InlineData("-undef")]
    [InlineData("\"a\" + 1")]
    [InlineData("[1] * \"x\"")]
    public void ScadEvaluatorExpression_InvalidOperands_YieldUndefAndWarn(string expression)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal("ECHO: undef", Assert.Single(output.Echo));
        var warning = Assert.Single(output.Diagnostics);
        Assert.StartsWith("Cannot apply operator", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the exact warning text and location of an invalid operand pair.
    /// </summary>
    [Fact]
    public void ScadEvaluatorExpression_InvalidOperands_WarningNamesOperatorAndKinds()
    {
        // Arrange
        const string Source = "x = 1 + \"a\";";

        // Act
        var warnings = Warnings(Source);

        // Assert
        Assert.Equal(["1:7: Cannot apply operator '+' to a number and a string"], warnings);
    }

    /// <summary>
    ///     Validates the textual forms of values in echo output.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="expected">The echo text.</param>
    [Theory]
    [InlineData("\"a\\\"b\"", "\"a\\\"b\"")]
    [InlineData("\"tab\\there\"", "\"tab\\there\"")]
    [InlineData("[1, \"s\", [true, undef]]", "[1, \"s\", [true, undef]]")]
    [InlineData("[0 : 2 : 6]", "[0 : 2 : 6]")]
    [InlineData("[3 : 5]", "[3 : 1 : 5]")]
    [InlineData("function(a, b) a", "function(a, b)")]
    [InlineData("1 / 3", "0.333333")]
    [InlineData("1234567", "1.23457e+06")]
    [InlineData("PI", "3.14159")]
    public void ScadEvaluatorExpression_EchoForms_AreStable(string expression, string expected)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal($"ECHO: {expected}", output.Echo.Single());
        Assert.Empty(output.UnsupportedFeatures);
    }

    /// <summary>
    ///     Validates that echo prints named arguments as <c>name = value</c> and joins arguments with a comma.
    /// </summary>
    [Fact]
    public void ScadEvaluatorExpression_Echo_NamedAndPositionalArguments_AreJoined()
    {
        // Arrange
        const string Source = "echo(1, \"two\", n = 3);";

        // Act
        var echo = Echo(Source);

        // Assert
        Assert.Equal(["ECHO: 1, \"two\", n = 3"], echo);
    }

    /// <summary>
    ///     Validates that a range with non-numeric parts is undef with a warning.
    /// </summary>
    [Fact]
    public void ScadEvaluatorExpression_RangeWithNonNumber_IsUndefWithWarning()
    {
        // Arrange
        const string Source = "echo([0 : \"a\"]);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: undef", Assert.Single(output.Echo));
        Assert.Equal("Range bounds and step must be numbers", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that a list comprehension element outside a vector (reachable only as a parenthesized let) is not an error.
    /// </summary>
    [Fact]
    public void ScadEvaluatorExpression_ParenthesizedLetAsOperand_Evaluates()
    {
        // Arrange
        const string Source = "echo((let(a = [4, 5]) a)[1]);";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("5", text);
    }
}
