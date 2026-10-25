using System.Diagnostics;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests that <see cref="ScadEvaluator"/> bounds operators on deeply shared nested vectors: the logical element
///     count of an operand (which can be astronomically larger than the memory the shared vectors use) is counted
///     with a cap, charged against the step budget, and limited by a fixed aggregate cap when the budget is
///     effectively unlimited.
/// </summary>
public class ScadEvaluatorAggregateLimitTests
{
    /// <summary>The longest an evaluation that must be refused may take before the test gives up.</summary>
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     Source that builds seven levels of vectors, each holding a thousand references to the previous level:
    ///     every list is short, yet the last one has about 10^21 logical nested elements.
    /// </summary>
    private const string SharedLevels =
        "a0 = [1];\n"
        + "a1 = [for (i = [0 : 999]) a0];\n"
        + "a2 = [for (i = [0 : 999]) a1];\n"
        + "a3 = [for (i = [0 : 999]) a2];\n"
        + "a4 = [for (i = [0 : 999]) a3];\n"
        + "a5 = [for (i = [0 : 999]) a4];\n"
        + "a6 = [for (i = [0 : 999]) a5];\n"
        + "a7 = [for (i = [0 : 999]) a6];\n";

    /// <summary>
    ///     Runs source that must be refused, failing the test when it takes longer than <see cref="Generous"/>.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="elapsed">Receives the time the evaluation took.</param>
    /// <returns>The evaluation exception.</returns>
    private static ScadEvaluationException FailsQuickly(string source, ScadEvaluationSettings settings, out TimeSpan elapsed)
    {
        // The evaluation runs on a pool thread so that a runaway walk fails the assertion instead of hanging the run
        var watch = Stopwatch.StartNew();
        var task = Task.Run(() => Record.Exception(() => Run(source, settings)), TestContext.Current.CancellationToken);
        var finished = task.Wait(Generous, TestContext.Current.CancellationToken);
        watch.Stop();
        elapsed = watch.Elapsed;
        Assert.True(finished, $"The evaluation was still running after {Generous}");
        return Assert.IsType<ScadEvaluationException>(task.Result);
    }

    /// <summary>
    ///     Validates that an operator on a deeply shared operand fails with the step-limit error under the default
    ///     limits instead of walking or allocating the logical nested elements.
    /// </summary>
    /// <param name="expression">The expression that applies an operator to <c>a7</c>.</param>
    [Theory]
    [InlineData("-a7")]
    [InlineData("a7 + a7")]
    [InlineData("a7 * 2")]
    [InlineData("a7 / 2")]
    [InlineData("a7 == a7")]
    [InlineData("a7 < a7")]
    public void ScadEvaluatorAggregateLimit_SharedNestedVectors_DefaultLimits_FailsWithStepLimit(string expression)
    {
        // Arrange
        var source = SharedLevels + $"w = {expression};";

        // Act
        var ex = FailsQuickly(source, new ScadEvaluationSettings(), out var elapsed);

        // Assert
        Assert.Contains("Evaluation step limit exceeded (limit 50000000)", ex.Message, StringComparison.Ordinal);
        Assert.True(elapsed < Generous, $"elapsed={elapsed}");
    }

    /// <summary>
    ///     Validates that, with an effectively unlimited step budget, an operator on a deeply shared operand is
    ///     refused with the aggregate-size diagnostic (the logical element count would overflow a 64-bit counter).
    /// </summary>
    /// <param name="expression">The expression that applies an operator to <c>a7</c>.</param>
    [Theory]
    [InlineData("-a7")]
    [InlineData("a7 + a7")]
    [InlineData("a7 * 2")]
    [InlineData("a7 / 2")]
    [InlineData("a7 == a7")]
    [InlineData("a7 < a7")]
    public void ScadEvaluatorAggregateLimit_SharedNestedVectors_UnlimitedSteps_FailsWithAggregateLimit(string expression)
    {
        // Arrange
        var source = SharedLevels + $"w = {expression};";
        var settings = With(new ScadLimits { MaxEvaluationSteps = long.MaxValue });

        // Act
        var ex = FailsQuickly(source, settings, out var elapsed);

        // Assert: the same deterministic diagnostic, at the operator
        Assert.Contains("Operand too large (limit 67108864 nested elements)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(9, ex.Location.Line);
        Assert.True(elapsed < Generous, $"elapsed={elapsed}");
    }

    /// <summary>
    ///     Validates that the step-limit diagnostic is kept when the remaining step budget is below the aggregate cap,
    ///     and that the aggregate diagnostic applies when the budget is above it.
    /// </summary>
    [Fact]
    public void ScadEvaluatorAggregateLimit_SharedNestedVectors_BudgetNearCap_ChoosesTheSmallerLimit()
    {
        // Arrange: a budget of one billion steps is above the cap, one million is below it
        var above = With(new ScadLimits { MaxEvaluationSteps = 1_000_000_000 });
        var below = With(new ScadLimits { MaxEvaluationSteps = 1_000_000 });
        var source = SharedLevels + "w = -a7;";

        // Act
        var aboveFailure = FailsQuickly(source, above, out _);
        var belowFailure = FailsQuickly(source, below, out _);

        // Assert
        Assert.Contains("Operand too large", aboveFailure.Message, StringComparison.Ordinal);
        Assert.Contains("Evaluation step limit exceeded (limit 1000000)", belowFailure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an operand whose logical element count is within the cap is still accepted and charged by
    ///     its count, so the cap changes nothing for ordinary models.
    /// </summary>
    [Fact]
    public void ScadEvaluatorAggregateLimit_NestedOperand_WithinCap_IsChargedByElementCount()
    {
        // Arrange: two levels of thousand-fold sharing hold 2,001,000 slots (three would hold about two billion)
        var settings = With(new ScadLimits { MaxEvaluationSteps = long.MaxValue });
        const string Source = "a0 = [1];\na1 = [for (i = [0 : 999]) a0];\na2 = [for (i = [0 : 999]) a1];\nw = -a2;\necho(len(w), len(w[0]), w[0][0]);";

        // Act
        var output = Run(Source, settings);

        // Assert
        Assert.Equal("ECHO: 1000, 1000, [-1]", Assert.Single(output.Echo));
        Assert.True(output.Steps > 2_001_000, $"steps={output.Steps}");
    }
}
