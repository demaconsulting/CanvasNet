using System.Diagnostics;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests that <see cref="ScadEvaluator"/> bounds matrix products: the result element count against the list
///     limit, the work against the step budget, and cancellation while a product runs.
/// </summary>
public class ScadEvaluatorProductLimitTests
{
    /// <summary>The longest a canceled or limited evaluation may take before the test gives up.</summary>
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     Builds source that defines an n by m matrix of ones as the variable with the given name.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="rows">The row count.</param>
    /// <param name="columns">The column count.</param>
    /// <returns>The source line.</returns>
    private static string MatrixSource(string name, int rows, int columns) =>
        $"{name} = [for (i = [1 : {rows}]) [for (j = [1 : {columns}]) 1]];\n";

    /// <summary>
    ///     Validates that a product whose result has exactly the list-limit elements succeeds, one more fails, and the
    ///     error is reported at the operator.
    /// </summary>
    [Fact]
    public void ScadEvaluatorProductLimit_MatrixTimesMatrix_ResultAtListLimit_Succeeds_OneOverFails()
    {
        // Arrange: 3 x 2 times 2 x 4 has 12 elements, 3 x 2 times 2 x 5 has 15
        var settings = With(new ScadLimits { MaxListLength = 12 });
        const string Left = "a = [[1, 1], [1, 1], [1, 1]];\n";
        var four = Left + "b = [[1, 1, 1, 1], [1, 1, 1, 1]];\nw = a * b;\necho(len(w));";
        var five = Left + "b = [[1, 1, 1, 1, 1], [1, 1, 1, 1, 1]];\nw = a * b;\necho(len(w));";

        // Act
        var atLimit = Run(four, settings);
        var ex = Fails(five, settings);

        // Assert
        Assert.Equal("ECHO: 3", Assert.Single(atLimit.Echo));
        Assert.Contains("List too long (limit 12)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(3, ex.Location.Line);
        Assert.Equal(7, ex.Location.Column);
    }

    /// <summary>
    ///     Validates that a product whose result is far over the list limit fails before allocating it.
    /// </summary>
    [Fact]
    public void ScadEvaluatorProductLimit_MatrixTimesMatrix_HugeResult_FailsBeforeAllocating()
    {
        // Arrange: 2000 x 2 times 2 x 2000 would have four million elements
        var settings = With(new ScadLimits { MaxListLength = 2000 });
        var source = MatrixSource("a", 2000, 2) + MatrixSource("b", 2, 2000) + "w = a * b;";
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();

        // Act
        var ex = Fails(source, settings);
        watch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: four million results would take about 100 MB; the operands take well under 20 MB
        Assert.Contains("List too long (limit 2000)", ex.Message, StringComparison.Ordinal);
        Assert.True(watch.Elapsed < Generous, $"elapsed={watch.Elapsed}");
        Assert.True(allocated < 20_000_000, $"allocated={allocated}");
    }

    /// <summary>
    ///     Validates that the multiply-add work of a product is charged as soon as the product starts.
    /// </summary>
    [Fact]
    public void ScadEvaluatorProductLimit_MatrixTimesMatrix_WorkIsChargedUpFront()
    {
        // Arrange: 40 x 40 times 40 x 40 is 64,000 multiply-adds
        const int N = 40;
        var setup = MatrixSource("a", N, N) + MatrixSource("b", N, N);
        var baseline = Run(setup + "w = [a, b];").Steps;
        var multiplied = Run(setup + "w = a * b;").Steps;

        // Act: allow only the baseline plus the result element count
        var settings = With(new ScadLimits { MaxEvaluationSteps = baseline + (N * N) });
        var ex = Fails(setup + "w = a * b;", settings);

        // Assert
        Assert.True(multiplied - baseline >= (N * N * N) / ScadValue.MultiplyAddsPerStep, $"multiplied={multiplied} baseline={baseline}");
        Assert.Contains("Evaluation step limit exceeded", ex.Message, StringComparison.Ordinal);
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that a product that would cost more than the default step budget fails quickly instead of running.
    /// </summary>
    [Fact]
    public void ScadEvaluatorProductLimit_MatrixTimesMatrix_DefaultLimits_LargeProductFailsFast()
    {
        // Arrange: two 1000 x 1000 matrices, each within the default list limit. The rows are one shared value, so the
        // matrices cost a thousand steps to build instead of a million, which keeps the test independent of how busy
        // the machine is: what is measured is that the product is refused, not how fast a large literal is evaluated.
        const string Source = "row = [for (j = [1 : 1000]) 1];\nm = [for (i = [1 : 1000]) row];\nw = [for (k = [1 : 50]) m * m];";
        var watch = Stopwatch.StartNew();

        // Act
        var ex = Fails(Source);
        watch.Stop();

        // Assert
        Assert.Contains("Evaluation step limit exceeded", ex.Message, StringComparison.Ordinal);
        Assert.True(watch.Elapsed < Generous, $"elapsed={watch.Elapsed}");
    }

    /// <summary>
    ///     Validates that cancellation interrupts a series of large products.
    /// </summary>
    [Fact]
    public void ScadEvaluatorProductLimit_MatrixTimesMatrix_DuringProduct_CanBeCanceled()
    {
        // Arrange: 21 products of 27 million multiply-adds each, far longer than the cancellation delay
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var settings = new ScadEvaluationSettings
        {
            Cancellation = cts.Token,
            Limits = new ScadLimits { MaxEvaluationSteps = long.MaxValue },
        };
        var source = MatrixSource("m", 300, 300) + "a = [for (k = [0 : 20]) m * m];";
        var started = DateTime.UtcNow;
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        // Act
        var ex = Record.Exception(() => Run(source, settings));

        // Assert
        Assert.IsType<OperationCanceledException>(ex);
        Assert.True(DateTime.UtcNow - started < Generous);
    }

    /// <summary>
    ///     Validates the unchanged matrix-times-vector product, including at the list limit.
    /// </summary>
    [Fact]
    public void ScadEvaluatorProductLimit_MatrixTimesVector_StillComputesAndCharges()
    {
        // Arrange: the 2-element result needs a list limit of only 2
        var settings = With(new ScadLimits { MaxListLength = 2 });
        const string Source = "a = [[1, 2], [3, 4]];\necho(a * [1, 1]);\necho([1, 1] * a);\necho([1, 2] * [3, 4]);";

        // Act
        var output = Run(Source, settings);

        // Assert
        Assert.Equal(["ECHO: [3, 7]", "ECHO: [4, 6]", "ECHO: 11"], output.Echo);
    }

    /// <summary>
    ///     Validates that scaling a vector is charged before it is allocated: a step budget below the element count fails.
    /// </summary>
    [Fact]
    public void ScadEvaluatorProductLimit_VectorTimesScalar_ChargedBeforeAllocation()
    {
        // Arrange
        const string Setup = "v = [for (i = [0 : 999]) i];\n";
        var baseline = Run(Setup + "w = v;").Steps;
        var settings = With(new ScadLimits { MaxEvaluationSteps = baseline + 500 });

        // Act
        var ex = Fails(Setup + "w = v * 2;", settings);

        // Assert
        Assert.Contains($"Evaluation step limit exceeded (limit {baseline + 500})", ex.Message, StringComparison.Ordinal);
    }
}
