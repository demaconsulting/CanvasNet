using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of tail calls and recursion accounting in <see cref="ScadEvaluator"/>: tail calls of user functions run
///     in a loop and add no frames, while every other recursion is bounded by the frame counter.
/// </summary>
public class ScadEvaluatorTailCallTests
{
    /// <summary>The recursion limit that makes the frame accounting easy to pin.</summary>
    private const int PinnedLimit = 104;

    /// <summary>
    ///     Validates that a deep tail recursion succeeds at the default limits and charges steps for every turn.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_DeepTailRecursion_SucceedsAtDefaultLimits()
    {
        // Arrange
        const string Source = "function count(n, acc = 0) = n == 0 ? acc : count(n - 1, acc + 1);\necho(count(100000));";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: 100000", Assert.Single(output.Echo));
        Assert.True(output.Steps > 100000);
    }

    /// <summary>
    ///     Validates that tail calls add no frames: a recursion limit far below the depth does not matter.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_TailRecursion_UsesConstantFrames()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 16 });
        const string Source = "function count(n) = n == 0 ? 0 : count(n - 1);\necho(count(50000));";

        // Act
        var output = Run(Source, settings);

        // Assert
        Assert.Equal("ECHO: 0", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates the documented accounting for non-tail recursion: <c>1 + f(n - 1)</c> costs exactly two frames per
    ///     level plus four at the bottom, so a limit of 104 admits depth 50 and rejects 51.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_NonTailRecursion_CostsTwoFramesPerLevel()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = PinnedLimit });
        const string Function = "function f(n) = n == 0 ? 0 : 1 + f(n - 1);\n";

        // Act
        var atLimit = Run(Function + "echo(f(50));", settings);
        var ex = Fails(Function + "echo(f(51));", settings);

        // Assert
        Assert.Equal("ECHO: 50", Assert.Single(atLimit.Echo));
        Assert.Contains($"Recursion too deep (limit {PinnedLimit})", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that non-tail recursion at the exact default limit succeeds and one level more fails.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_NonTailRecursion_AtDefaultLimit_Succeeds_OneOverFails()
    {
        // Arrange
        const string Function = "function f(n) = n == 0 ? 0 : 1 + f(n - 1);\n";
        var maxLevels = (ScadLimits.Default.MaxRecursionDepth - 4) / 2;

        // Act
        var atLimit = Run(Function + $"echo(f({maxLevels}));");
        var ex = Fails(Function + $"echo(f({maxLevels + 1}));");

        // Assert
        Assert.Equal($"ECHO: {maxLevels}", Assert.Single(atLimit.Echo));
        Assert.Contains($"Recursion too deep (limit {ScadLimits.Default.MaxRecursionDepth})", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a tail call is recognized through the branches of a conditional and the bodies of
    ///     <c>let</c>, <c>assert</c> and <c>echo</c>.
    /// </summary>
    /// <param name="function">The recursive function.</param>
    [Theory]
    [InlineData("function f(n) = n == 0 ? 0 : f(n - 1);")]
    [InlineData("function f(n) = n != 0 ? f(n - 1) : 0;")]
    [InlineData("function f(n) = n == 0 ? 0 : let(m = n - 1) f(m);")]
    [InlineData("function f(n) = assert(n >= 0) n == 0 ? 0 : f(n - 1);")]
    [InlineData("function f(n) = n == 0 ? 0 : assert(n > 0, \"positive\") f(n - 1);")]
    [InlineData("function f(n) = n == 0 ? 0 : echo() f(n - 1);")]
    [InlineData("function f(n) = n == 0 ? 0 : let(a = 1) let(b = 2) assert(a < b) f(n - a);")]
    public void ScadEvaluatorTailCall_TailPositions_AddNoFrames(string function)
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 16 });

        // Act
        var output = Run($"{function}\nresult = f(2000);\necho(result);", settings);

        // Assert
        Assert.Equal("ECHO: 0", output.Echo[^1]);
    }

    /// <summary>
    ///     Validates tail calls through closures held in variables and through mutual recursion.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_MutualAndClosureRecursion_AddNoFrames()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 16 });
        const string Source = """
            function ping(n) = n == 0 ? "done" : pong(n - 1);
            function pong(n) = n == 0 ? "done" : ping(n - 1);
            loop = function(n) n == 0 ? "closure" : loop(n - 1);
            echo(ping(30001), loop(30000));
            """;

        // Act
        var output = Run(Source, settings);

        // Assert
        Assert.Equal("ECHO: \"done\", \"closure\"", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates that a call that is not in tail position (an operand) is not optimized.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_CallAsOperand_IsNotATailCall()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 64 });
        const string Source = "function f(n) = n == 0 ? 0 : (f(n - 1));\nfunction g(n) = n == 0 ? 0 : [g(n - 1)][0];\necho(f(1000));";

        // Act
        var output = Run(Source, settings);
        var ex = Fails("function g(n) = n == 0 ? 0 : [g(n - 1)][0];\necho(g(1000));", settings);

        // Assert
        Assert.Equal("ECHO: 0", Assert.Single(output.Echo));
        Assert.Contains("Recursion too deep (limit 64)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an infinite tail recursion ends with the step limit, never with a stack error.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_InfiniteTailRecursion_EndsWithStepLimit()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxEvaluationSteps = 10_000 });

        // Act
        var ex = Fails("function f(n) = f(n + 1);\necho(f(0));", settings);

        // Assert
        Assert.Contains("Evaluation step limit exceeded (limit 10000)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that unbounded non-tail recursion ends with the recursion limit.
    /// </summary>
    [Fact]
    public void ScadEvaluatorTailCall_InfiniteNonTailRecursion_EndsWithRecursionLimit()
    {
        // Arrange / Act
        var ex = Fails("function f(n) = 1 + f(n + 1);\necho(f(0));");

        // Assert
        Assert.Contains("Recursion too deep (limit 5000)", ex.Message, StringComparison.Ordinal);
    }
}
