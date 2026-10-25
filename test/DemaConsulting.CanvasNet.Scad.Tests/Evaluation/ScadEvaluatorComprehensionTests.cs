using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of vector literals and list comprehensions in <see cref="DemaConsulting.CanvasNet.Scad.Evaluation.ScadEvaluator"/>.
/// </summary>
public class ScadEvaluatorComprehensionTests
{
    /// <summary>
    ///     Validates the result of each comprehension form.
    /// </summary>
    /// <param name="expression">The vector expression.</param>
    /// <param name="expected">The expected echo text.</param>
    [Theory]
    [InlineData("[for (i = [1 : 3]) i * 2]", "[2, 4, 6]")]
    [InlineData("[for (i = [1 : 2]) for (j = [1 : 2]) i * 10 + j]", "[11, 12, 21, 22]")]
    [InlineData("[for (i = [1 : 2], j = [1 : 2]) i * 10 + j]", "[11, 12, 21, 22]")]
    [InlineData("[for (i = [1 : 6]) if (i % 2 == 0) i]", "[2, 4, 6]")]
    [InlineData("[for (i = [1 : 4]) if (i < 3) i else -i]", "[1, 2, -3, -4]")]
    [InlineData("[for (i = [1 : 3]) let (sq = i * i) sq + 1]", "[2, 5, 10]")]
    [InlineData("[each [1, 2], each [3 : 5], each 7]", "[1, 2, 3, 4, 5, 7]")]
    [InlineData("[for (v = [[1, 2], [3]]) each v]", "[1, 2, 3]")]
    [InlineData("[for (i = [0 : 1]) [i, i * 2]]", "[[0, 0], [1, 2]]")]
    [InlineData("[(for (i = [0 : 2]) i), (if (true) 9)]", "[0, 1, 2, 9]")]
    [InlineData("[for (i = [0 : 1]) (for (j = [0 : 1]) i + j)]", "[0, 1, 1, 2]")]
    [InlineData("[for (i = [1 : 3]) if (i > 1) if (i < 3) i]", "[2]")]
    [InlineData("[let (a = 2) for (i = [1 : a]) i]", "[1, 2]")]
    [InlineData("[for (c = \"ab\") c]", "[\"ab\"]")]
    [InlineData("[for (i = []) i]", "[]")]
    [InlineData("[0, for (i = [1 : 2]) i, 9]", "[0, 1, 2, 9]")]
    [InlineData("[each undef]", "[undef]")]
    public void ScadEvaluatorComprehension_Forms_ProduceExpectedVector(string expression, string expected)
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
    ///     Validates C-style loops: the increments are evaluated simultaneously from the old state, and variables that
    ///     are not incremented keep their value.
    /// </summary>
    /// <param name="expression">The vector expression.</param>
    /// <param name="expected">The expected echo text.</param>
    [Theory]
    [InlineData("[for (i = 0; i < 4; i = i + 1) i * i]", "[0, 1, 4, 9]")]
    [InlineData("[for (i = 0, s = 0; i < 4; i = i + 1, s = s + i) s]", "[0, 0, 1, 3]")]
    [InlineData("[for (a = 1, b = 2; a < 4; a = b, b = a + b) a]", "[1, 2, 3]")]
    [InlineData("[for (a = 2, i = 0; i < 3; i = i + 1) a * i]", "[0, 2, 4]")]
    [InlineData("[for (; false;) 1]", "[]")]
    [InlineData("[for (i = 0; i < 3; i = i + 1) if (i != 1) i]", "[0, 2]")]
    [InlineData("[for (x = [1, 1]; len(x) < 4; x = concat(x, [len(x)])) len(x)]", "[2, 3]")]
    public void ScadEvaluatorComprehension_CStyleFor_UpdatesStateSimultaneously(string expression, string expected)
    {
        // Arrange
        var source = $"echo({expression});";

        // Act
        var text = EchoText(source);

        // Assert
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Validates that closures created in a C-style loop body keep the values of their own turn.
    /// </summary>
    [Fact]
    public void ScadEvaluatorComprehension_CStyleFor_ClosuresKeepTheirTurnsValues()
    {
        // Arrange
        const string Source = "fs = [for (i = 0; i < 3; i = i + 1) function() i * 10];\necho(fs[0](), fs[1](), fs[2]());";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("0, 10, 20", text);
    }

    /// <summary>
    ///     Validates that iterating undef inside a comprehension warns and yields nothing.
    /// </summary>
    [Fact]
    public void ScadEvaluatorComprehension_ForOverUndef_WarnsAndYieldsNothing()
    {
        // Arrange
        const string Source = "echo([for (i = undef) i]);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: []", Assert.Single(output.Echo));
        Assert.Equal("Cannot iterate over undef", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that a comprehension can call recursive functions and that comprehensions nest inside recursion.
    /// </summary>
    [Fact]
    public void ScadEvaluatorComprehension_InsideRecursion_Works()
    {
        // Arrange
        const string Source = "function tree(n) = n == 0 ? [] : [for (i = [1 : 2]) tree(n - 1)];\necho(tree(3));";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("[[[[], []], [[], []]], [[[], []], [[], []]]]", text);
    }

    /// <summary>
    ///     Validates the list length limit inside a comprehension: exactly at the limit succeeds, one over fails.
    /// </summary>
    [Fact]
    public void ScadEvaluatorComprehension_ListLimit_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxListLength = 5 });

        // Act
        var atLimit = Run("echo(len([for (i = [1 : 5]) i]));", settings);
        var ex = Fails("echo([for (i = [1 : 6]) i]);", settings);

        // Assert
        Assert.Equal("ECHO: 5", Assert.Single(atLimit.Echo));
        Assert.Contains("List too long (limit 5)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <c>each</c> over a huge range is rejected before any element is produced.
    /// </summary>
    [Fact]
    public void ScadEvaluatorComprehension_EachOverHugeRange_IsRejectedUpFront()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxLoopIterations = 100 });

        // Act
        var ex = Fails("echo([each [0 : 1e12]]);", settings);

        // Assert
        Assert.Contains("Loop iteration limit exceeded (limit 100)", ex.Message, StringComparison.Ordinal);
    }
}
