using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of cancellation in <see cref="ScadEvaluator"/>: the token is polled while charging steps, at every loop
///     turn and while loading files, and cancellation is never wrapped.
/// </summary>
/// <remarks>
///     No test depends on a clock. The token is canceled from inside the evaluation, when the sink records a known
///     echo (<see cref="ScadEvaluationSink.EchoRecorded"/> runs on the evaluating thread), and the step or loop budget
///     is set so that an evaluation that ignored the token would end with a limit error instead of the cancellation.
/// </remarks>
public class ScadEvaluatorCancellationTests
{
    /// <summary>
    ///     The step budget of the tests that cancel an endless evaluation: far above the 1,024 steps between two
    ///     cancellation polls, so cancellation must win, yet small enough that an evaluation that ignored the token
    ///     fails with the step-limit error.
    /// </summary>
    private const long StepBudget = 1_000_000;

    /// <summary>
    ///     Validates that a token canceled before the call stops evaluation before any resolver callback.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_AlreadyCanceled_ThrowsBeforeAnyResolverCall()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
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
        // Arrange: the first echo cancels the token, then the endless recursion runs until the next poll
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var settings = new ScadEvaluationSettings
        {
            Cancellation = cts.Token,
            Limits = new ScadLimits { MaxEvaluationSteps = StepBudget },
        };
        var sink = new ScadEvaluationSink(ScadLimits.Default) { EchoRecorded = cts.Cancel };

        // Act
        var ex = Record.Exception(() => Run("echo(\"started\");\nfunction f(n) = f(n + 1);\necho(f(0));", settings, null, sink));

        // Assert: the cancellation itself, not the step-limit error an evaluation that ignored the token would reach
        Assert.IsType<OperationCanceledException>(ex);
        Assert.Equal(["ECHO: \"started\""], sink.EchoLines);
    }

    /// <summary>
    ///     Validates that cancellation interrupts a huge loop at the next loop boundary.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_DuringHugeLoop_Stops()
    {
        // Arrange: the echo of the first turn cancels the token; nothing else bounds the loop
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var settings = new ScadEvaluationSettings
        {
            Cancellation = cts.Token,
            Limits = new ScadLimits { MaxEvaluationSteps = long.MaxValue, MaxLoopIterations = long.MaxValue, MaxInstances = int.MaxValue },
        };
        var sink = new ScadEvaluationSink(ScadLimits.Default) { EchoRecorded = cts.Cancel };

        // Act
        var ex = Record.Exception(() => Run("for (i = [0 : 1e15]) echo(i);", settings, null, sink));

        // Assert: every loop turn polls the token, so the second turn never runs
        Assert.IsType<OperationCanceledException>(ex);
        Assert.Equal(["ECHO: 0"], sink.EchoLines);
    }

    /// <summary>
    ///     Validates that cancellation is observed while loading files: a resolver that cancels stops the load before the next file.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_DuringIncludeLoading_StopsBeforeNextFile()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
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
        // Arrange: the echo right before str(v) cancels the token. Formatting 100,000 elements charges 800,000 steps,
        // so a budget of the steps before it plus half of that lets only a formatting that polls end with the
        // cancellation; one that charged without polling would end with the step-limit error
        const string Setup = "v = [for (i = [0 : 99999]) i * 1.5];\n";
        var before = Run(Setup + "s = echo(\"formatting\") 0;").Steps;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var settings = new ScadEvaluationSettings
        {
            Cancellation = cts.Token,
            Limits = new ScadLimits { MaxEvaluationSteps = before + (100_000 * ScadValue.FormatStepsPerElement / 2) },
        };
        var sink = new ScadEvaluationSink(ScadLimits.Default) { EchoRecorded = cts.Cancel };

        // Act
        var ex = Record.Exception(() => Run(Setup + "s = echo(\"formatting\") str(v);\necho(len(s));", settings, null, sink));

        // Assert
        Assert.IsType<OperationCanceledException>(ex);
        Assert.Equal(["ECHO: \"formatting\""], sink.EchoLines);
    }

    /// <summary>
    ///     Validates that an uncanceled token does not disturb evaluation.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCancellation_NotCanceled_EvaluatesNormally()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var settings = new ScadEvaluationSettings { Cancellation = cts.Token };

        // Act
        var output = Run("for (i = [1 : 3000]) {}\necho(\"done\");", settings);

        // Assert
        Assert.Equal("ECHO: \"done\"", Assert.Single(output.Echo));
    }
}
