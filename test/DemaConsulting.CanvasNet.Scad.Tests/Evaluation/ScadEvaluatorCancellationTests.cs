using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of cancellation in <see cref="ScadEvaluator"/>: the token is polled while charging steps, at every loop
///     turn and while loading files, and cancellation is never wrapped.
/// </summary>
public class ScadEvaluatorCancellationTests
{
    /// <summary>The longest a canceled evaluation may take before the test gives up.</summary>
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     Validates that a token canceled before the call stops evaluation before any resolver callback.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_AlreadyCanceled_ThrowsBeforeAnyResolverCall()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var files = new ScadVirtualFiles(("lib.scad", "x = 1;\n"));
        var settings = new ScadEvaluationSettings { Cancellation = cts.Token };

        // Act
        Assert.Throws<OperationCanceledException>(() => Run("include <lib.scad>\necho(1);", settings, files));

        // Assert
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that cancellation interrupts an endless tail recursion, keeps the events produced so far, and is
    ///     not wrapped in an evaluation exception.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_DuringEndlessTailRecursion_StopsAndKeepsEarlierEvents()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));
        var settings = new ScadEvaluationSettings
        {
            Cancellation = cts.Token,
            Limits = new ScadLimits { MaxEvaluationSteps = long.MaxValue },
        };
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var started = DateTime.UtcNow;

        // Act
        var ex = Record.Exception(() => Run("echo(\"started\");\nfunction f(n) = f(n + 1);\necho(f(0));", settings, null, sink));

        // Assert
        Assert.IsType<OperationCanceledException>(ex);
        Assert.True(DateTime.UtcNow - started < Generous);
        Assert.Equal(["ECHO: \"started\""], sink.EchoLines);
    }

    /// <summary>
    ///     Validates that cancellation interrupts a huge loop at a loop boundary.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_DuringHugeLoop_Stops()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));
        var settings = new ScadEvaluationSettings
        {
            Cancellation = cts.Token,
            Limits = new ScadLimits { MaxEvaluationSteps = long.MaxValue, MaxLoopIterations = long.MaxValue, MaxInstances = int.MaxValue },
        };
        var started = DateTime.UtcNow;

        // Act
        var ex = Record.Exception(() => Run("for (i = [0 : 1e15]) {}", settings));

        // Assert
        Assert.IsType<OperationCanceledException>(ex);
        Assert.True(DateTime.UtcNow - started < Generous);
    }

    /// <summary>
    ///     Validates that cancellation is observed while loading files: a resolver that cancels stops the load before the next file.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_DuringIncludeLoading_StopsBeforeNextFile()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var files = new ScadVirtualFiles(("a.scad", "include <b.scad>\n"), ("b.scad", "x = 1;\n"))
        {
            OnRequest = path =>
            {
                if (path == "a.scad")
                {
                    cts.Cancel();
                }
            },
        };
        var settings = new ScadEvaluationSettings { Cancellation = cts.Token };

        // Act
        Assert.Throws<OperationCanceledException>(() => Run("include <a.scad>\necho(1);", settings, files));

        // Assert
        Assert.Equal(["a.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that cancellation interrupts the formatting of a huge value, which is charged and polled while
    ///     it formats rather than afterwards.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_DuringLargeFormatting_Stops()
    {
        // Arrange: each str of 100,000 numbers takes tens of milliseconds; the step limit is out of the way
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var settings = new ScadEvaluationSettings
        {
            Cancellation = cts.Token,
            Limits = new ScadLimits { MaxEvaluationSteps = long.MaxValue },
        };
        const string Source = "v = [for (i = [0 : 99999]) i * 1.5];\na = [for (k = [1 : 1000000]) len(str(v))];";
        var started = DateTime.UtcNow;
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        // Act
        var ex = Record.Exception(() => Run(Source, settings));

        // Assert
        Assert.IsType<OperationCanceledException>(ex);
        Assert.True(DateTime.UtcNow - started < Generous);
    }

    /// <summary>
    ///     Validates that an uncanceled token does not disturb evaluation.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_NotCanceled_EvaluatesNormally()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var settings = new ScadEvaluationSettings { Cancellation = cts.Token };

        // Act
        var output = Run("for (i = [1 : 3000]) {}\necho(\"done\");", settings);

        // Assert
        Assert.Equal("ECHO: \"done\"", Assert.Single(output.Echo));
    }
}
