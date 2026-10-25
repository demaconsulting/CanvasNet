using System.Globalization;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of the deterministic stack safety of <see cref="ScadEvaluator"/>. The nominal recursion limit is
///     enforced by a counter, so it must behave identically whatever stack the caller has: when the caller's stack
///     runs short, the evaluation restarts on a dedicated large-stack thread and produces exactly the same result.
/// </summary>
public class ScadEvaluatorStackTests
{
    /// <summary>The stack size, in bytes, of the thread that stands in for a caller with plenty of stack.</summary>
    private const int BigCallerStackSize = 256 * 1024 * 1024;

    /// <summary>The stack size, in bytes, of a caller below the runtime guard's reserve (it must hop).</summary>
    private const int TinyCallerStackSize = 64 * 1024;

    /// <summary>How long a test waits for another thread before it gives up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     The pathological recursion shapes: each makes a program from a recursion depth, and gives the largest depth
    ///     that fits in a frame limit (the frame accounting is documented in the design and pinned by these tests).
    /// </summary>
    private static readonly Dictionary<string, (Func<int, string> Make, Func<int, int> MaxDepth)> Shapes = new()
    {
        ["function"] = (n => $"function f(n) = n == 0 ? 0 : 1 + f(n - 1);\necho(f({n}));", limit => (limit - 4) / 2),
        ["closure"] = (n => $"f = function(n) n == 0 ? 0 : 1 + f(n - 1);\necho(f({n}));", limit => (limit - 4) / 2),
        ["mutual"] = (n => $"function a(n) = n == 0 ? 0 : 1 + b(n - 1);\nfunction b(n) = n == 0 ? 0 : 1 + a(n - 1);\necho(a({n}));", limit => (limit - 4) / 2),
        ["let-echo-assert"] = (n => $"function f(n) = n == 0 ? 0 : let(a = 1) echo(\"x\") assert(true) 1 + f(n - 1);\necho(f({n}));", limit => (limit - 4) / 2),
        ["module"] = (n => $"module r(n) {{ if (n > 0) r(n - 1); }}\nr({n});", limit => (limit - 5) / 3),
        ["blocks"] = (n => $"module r(n) {{ if (n > 0) {{ {{ {{ r(n - 1); }} }} }} }}\nr({n});", limit => (limit - 5) / 5),
        ["children"] = (n => $"module w(n) {{ if (n > 0) w(n - 1) {{ children(); }} else children(); }}\nw({n}) cube(1);", limit => (limit - 9) / 5),
        ["nested-expression"] = (n => $"function f(n) = n == 0 ? 0 : {Repeat("1 + (", 100)}f(n - 1){Repeat(")", 100)};\necho(f({n}));", limit => (limit - 4) / 101),
        ["vector-nesting"] = (n => $"function f(n) = n == 0 ? 0 : [[[[f(n - 1)]]]][0][0][0][0];\necho(f({n}));", limit => (limit - 4) / 9),
    };

    /// <summary>Gets the shape names crossed with the default and the highest recursion limit, as theory data.</summary>
    public static TheoryData<string, int> ShapesAndLimits
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var shape in Shapes.Keys)
            {
                data.Add(shape, ScadLimits.Default.MaxRecursionDepth);
                data.Add(shape, 16_384);
            }

            return data;
        }
    }

    /// <summary>
    ///     Validates that every shape recursing exactly to the nominal limit succeeds on a 256 KiB caller (by hopping to
    ///     the evaluator thread) with exactly the result, diagnostics and tree it has on a big stack.
    /// </summary>
    /// <param name="shape">The shape name.</param>
    /// <param name="limit">The recursion limit.</param>
    [Theory]
    [MemberData(nameof(ShapesAndLimits))]
    public void ScadEvaluatorStack_Evaluate_AtLimit_OnSmallCallerStack_MatchesBigStackResult(string shape, int limit)
    {
        // Arrange
        var (make, maxDepth) = Shapes[shape];
        var program = make(maxDepth(limit));
        var settings = With(new ScadLimits { MaxRecursionDepth = limit });

        // Act
        var big = OnSmallStack(() => RunAndDescribe(program, settings), BigCallerStackSize);
        var small = OnSmallStack(() => RunAndDescribe(program, settings));

        // Assert
        Assert.True(big.Inline);
        Assert.False(small.Inline);
        Assert.Equal(big.Text, small.Text);
    }

    /// <summary>
    ///     Validates that every shape one level over the nominal limit fails with the recursion error on a 256 KiB
    ///     caller, with the same message and location as on a big stack.
    /// </summary>
    /// <param name="shape">The shape name.</param>
    /// <param name="limit">The recursion limit.</param>
    [Theory]
    [MemberData(nameof(ShapesAndLimits))]
    public void ScadEvaluatorStack_Evaluate_OverLimit_OnSmallCallerStack_ThrowsSameRecursionError(string shape, int limit)
    {
        // Arrange
        var (make, maxDepth) = Shapes[shape];
        var program = make(maxDepth(limit) + 1);
        var settings = With(new ScadLimits { MaxRecursionDepth = limit });

        // Act
        var big = OnSmallStack(() => RunAndCatch(program, settings), BigCallerStackSize);
        var small = OnSmallStack(() => RunAndCatch(program, settings));

        // Assert
        Assert.Contains($"Recursion too deep (limit {limit})", small.Message, StringComparison.Ordinal);
        Assert.Equal(big, small);
    }

    /// <summary>
    ///     Validates that the recursion error is identical on a 64 KiB, 256 KiB and 1 MiB caller, on the default test
    ///     thread and on a big stack: same message and same location.
    /// </summary>
    /// <param name="shape">The shape name.</param>
    [Theory]
    [InlineData("function")]
    [InlineData("module")]
    [InlineData("children")]
    public void ScadEvaluatorStack_Evaluate_OverLimit_IsIdenticalOnEveryStackSize(string shape)
    {
        // Arrange
        var (make, maxDepth) = Shapes[shape];
        var program = make(maxDepth(ScadLimits.Default.MaxRecursionDepth) + 1);

        // Act
        var results = new[]
        {
            OnSmallStack(() => RunAndCatch(program, null), TinyCallerStackSize),
            OnSmallStack(() => RunAndCatch(program, null)),
            OnSmallStack(() => RunAndCatch(program, null), 1024 * 1024),
            RunAndCatch(program, null),
            OnSmallStack(() => RunAndCatch(program, null), BigCallerStackSize),
        };

        // Assert
        Assert.All(results, r => Assert.Equal(results[0], r));
        Assert.Contains("Recursion too deep (limit 5000)", results[0].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that comprehension recursion, whose depth the value nesting limit bounds first, behaves identically
    ///     at and one over the limit on a small caller stack.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_ComprehensionNesting_AtAndOverLimit_OnSmallCallerStack()
    {
        // Arrange
        const string Function = "function f(n) = n == 0 ? [] : [for (i = [0]) f(n - 1)];\n";

        // Act
        var atLimit = OnSmallStack(() => RunAndDescribe(Function + "echo(len(f(255)));", null));
        var overLimit = OnSmallStack(() => RunAndCatch(Function + "echo(len(f(256)));", null));

        // Assert
        Assert.Equal("echo=[ECHO: 1] diag=[] tree=", atLimit.Text.Split(" steps=")[0]);
        Assert.Contains("List nesting too deep (limit 256)", overLimit.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates value operations on vectors nested to the maximum depth on a small caller stack: equality, ordering,
    ///     formatting and arithmetic recurse over 256 levels and must give the same answers as on a big stack.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_ValueNestingAtLimit_OnSmallCallerStack_MatchesBigStack()
    {
        // Arrange
        const string Source = """
            function nest(n, leaf) = n == 0 ? leaf : [nest(n - 1, leaf)];
            a = nest(255, 1);
            b = nest(255, 1);
            c = nest(255, 2);
            echo(a == b, a == c, a < c, len(str(a)), a + b == nest(255, 2), -a == nest(255, -1));
            """;

        // Act
        var big = OnSmallStack(() => RunAndDescribe(Source, null), BigCallerStackSize);
        var small = OnSmallStack(() => RunAndDescribe(Source, null));

        // Assert
        Assert.Equal("ECHO: true, false, true, 511, true, true", big.Echo[0]);
        Assert.Equal(big.Text, small.Text);
    }

    /// <summary>
    ///     Validates that a caller below the guard's reserve really hops: the evaluation completes on another thread
    ///     and the fallback is counted.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_TinyCallerStack_FallsBackToDedicatedThread()
    {
        // Arrange
        var before = ScadEvaluator.FallbackCount;
        var program = Shapes["function"].Make(1000);

        // Act
        var (output, callerThread, evaluatorThread) = OnSmallStack(
            () =>
            {
                var result = Run(program);
                return (result, Environment.CurrentManagedThreadId, ScadEvaluator.LastRunThreadId);
            },
            TinyCallerStackSize);

        // Assert
        Assert.Equal("ECHO: 1000", Assert.Single(output.Echo));
        Assert.NotEqual(callerThread, evaluatorThread);
        Assert.True(ScadEvaluator.FallbackCount > before);
    }

    /// <summary>
    ///     Validates that an ordinary program runs on the calling thread and needs no fallback.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_NormalProgram_RunsOnCallerThread()
    {
        // Arrange / Act
        var output = Run("function f(n) = n == 0 ? 0 : 1 + f(n - 1);\nfor (i = [1 : 10]) echo(f(i));");

        // Assert
        Assert.Equal(10, output.Echo.Count);
        Assert.Equal(Environment.CurrentManagedThreadId, ScadEvaluator.LastRunThreadId);
    }

    /// <summary>
    ///     Validates that an error raised on the evaluator thread keeps its type, message and original stack trace, and
    ///     that the caller's culture is applied there.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_OnTinyCallerStack_PreservesExceptionsAndCulture()
    {
        // Arrange
        var deep = Shapes["function"].Make(ScadLimits.Default.MaxRecursionDepth);

        // Act
        var (error, hopped, text) = OnSmallStack(
            () =>
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var caught = Record.Exception(() => Run(deep));
                var failedElsewhere = ScadEvaluator.LastRunThreadId != Environment.CurrentManagedThreadId;
                var echo = Run("echo(1.5, 1e-5);\n" + Shapes["function"].Make(2000)).Echo[0];
                return (caught, failedElsewhere, echo);
            },
            TinyCallerStackSize);

        // Assert
        var evaluation = Assert.IsType<ScadEvaluationException>(error);
        Assert.Contains("Recursion too deep (limit 5000)", evaluation.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ScadEvaluator), evaluation.StackTrace, StringComparison.Ordinal);
        Assert.True(hopped);
        Assert.Equal("ECHO: 1.5, 1e-05", text);
    }

    /// <summary>
    ///     Validates that a restarted evaluation records each echo line and warning once, not twice.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_Restart_RecordsEachEventOnce()
    {
        // Arrange
        const string Source = "echo(\"start\");\nx = nope;\nfunction f(n) = n == 0 ? 0 : 1 + f(n - 1);\necho(f(2000));";
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var output = OnSmallStack(() => Run(Source, null, null, sink), TinyCallerStackSize);

        // Assert
        Assert.Equal(["ECHO: \"start\"", "ECHO: 2000"], output.Echo);
        Assert.Equal(["ECHO: \"start\"", "ECHO: 2000"], sink.EchoLines);
        Assert.Single(sink.Diagnostics);
    }

    /// <summary>
    ///     Validates that warnings recorded while loading files survive a restart: they appear once.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_Restart_KeepsFileLoadingWarnings()
    {
        // Arrange
        var files = new ScadVirtualFiles();
        var source = "include <missing.scad>\n" + Shapes["function"].Make(2000);

        // Act
        var output = OnSmallStack(() => Run(source, null, files), TinyCallerStackSize);

        // Assert
        Assert.Equal("ECHO: 2000", Assert.Single(output.Echo));
        Assert.Equal("Can't open include file 'missing.scad'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that the file resolver callback runs only on the calling thread and once per file even when the
    ///     evaluation restarts on the evaluator thread.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_Restart_DoesNotRepeatOrMoveResolverCalls()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "function twice(v) = v * 2;\n"));
        var source = "use <lib.scad>\n" + Shapes["function"].Make(2000) + "\necho(twice(4));";

        // Act
        var (output, callerThread) = OnSmallStack(() => (Run(source, null, files), Environment.CurrentManagedThreadId), TinyCallerStackSize);

        // Assert
        Assert.Equal(["ECHO: 2000", "ECHO: 8"], output.Echo);
        Assert.Equal(["lib.scad"], files.Requests);
        Assert.All(files.Threads, thread => Assert.Equal(callerThread, thread));
    }

    /// <summary>
    ///     Validates that many concurrent tiny-stack callers all succeed while the number of simultaneously running
    ///     fallback threads never exceeds the bound.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_ConcurrentTinyCallerStacks_BoundFallbackThreads()
    {
        // Arrange: several times more callers than the bound allows at once
        var program = Shapes["function"].Make(1500);
        var callers = new Thread[ScadEvaluator.MaxConcurrentFallbackThreads * 4];
        var failures = new Exception?[callers.Length];
        var before = ScadEvaluator.FallbackCount;
        for (var i = 0; i < callers.Length; i++)
        {
            var index = i;
            callers[i] = new Thread(() => failures[index] = Record.Exception(() => Run(program)), TinyCallerStackSize);
        }

        // Act
        foreach (var caller in callers)
        {
            caller.Start();
        }

        foreach (var caller in callers)
        {
            caller.Join();
        }

        // Assert
        Assert.All(failures, Assert.Null);
        Assert.InRange(ScadEvaluator.PeakConcurrentFallbackThreads, 1, ScadEvaluator.MaxConcurrentFallbackThreads);
        Assert.True(ScadEvaluator.FallbackCount - before >= callers.Length);
    }

    /// <summary>
    ///     Validates that cancellation ends the wait for a fallback slot: a caller that must hop but finds every slot
    ///     taken is abandoned with an <see cref="OperationCanceledException"/> and acquires nothing.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_CancelWhileWaitingForFallbackSlot_ThrowsOperationCanceled()
    {
        // Arrange: a slot set of this test's own, every slot held, so the next hopping caller has to wait
        var slots = new ScadEvaluatorFallbackSlots(2);
        HoldAll(slots);
        try
        {
            using var cts = new CancellationTokenSource();
            var (caller, result) = StartTinyCaller(Shapes["function"].Make(1500), cts.Token, slots);
            Assert.True(SpinWait.SpinUntil(() => slots.Waiting == 1, Patience), "the caller never started waiting");

            // Act
            cts.Cancel();
            var finished = caller.Join(Patience);

            // Assert: the caller was released by the cancellation, and the slots we hold are still all held
            Assert.True(finished);
            Assert.IsAssignableFrom<OperationCanceledException>(result[0]);
            Assert.Equal(0, slots.FreeSlots);
            Assert.Equal(0, slots.Waiting);
            Assert.Equal(slots.Capacity, slots.Started);
        }
        finally
        {
            ReleaseAll(slots);
        }
    }

    /// <summary>
    ///     Validates that a caller waiting for a fallback slot proceeds, and completes correctly, when a slot is released.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_WaitingForSlot_ProceedsWhenSlotIsReleased()
    {
        // Arrange
        var slots = new ScadEvaluatorFallbackSlots(2);
        HoldAll(slots);
        var released = false;
        try
        {
            var (caller, result) = StartTinyCaller(Shapes["function"].Make(1500), CancellationToken.None, slots);
            Assert.True(SpinWait.SpinUntil(() => slots.Waiting == 1, Patience), "the caller never started waiting");

            // Act
            slots.Release();
            released = true;
            var finished = caller.Join(Patience);

            // Assert: the caller took the released slot (one more start than the slots we held) and finished cleanly
            Assert.True(finished);
            Assert.Null(result[0]);
            Assert.Equal(slots.Capacity + 1, slots.Started);
            Assert.Equal(0, slots.Waiting);
        }
        finally
        {
            // One slot was released inside the test, so release the others
            for (var i = released ? 1 : 0; i < slots.Capacity; i++)
            {
                slots.Release();
            }
        }
    }

    /// <summary>
    ///     Validates that a canceled wait leaks no slot: once the held slots are released, every slot is free again.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_CanceledWait_DoesNotLeakASlot()
    {
        // Arrange
        var slots = new ScadEvaluatorFallbackSlots(2);
        HoldAll(slots);
        try
        {
            using var cts = new CancellationTokenSource();
            var (caller, result) = StartTinyCaller(Shapes["function"].Make(1500), cts.Token, slots);
            Assert.True(SpinWait.SpinUntil(() => slots.Waiting == 1, Patience), "the caller never started waiting");
            cts.Cancel();
            Assert.True(caller.Join(Patience));
            Assert.IsAssignableFrom<OperationCanceledException>(result[0]);
        }
        finally
        {
            ReleaseAll(slots);
        }

        // Act / Assert: the slot set is private to this test, so every slot is free right away
        Assert.Equal(slots.Capacity, slots.FreeSlots);
    }

    /// <summary>
    ///     Validates that a test's own slot set observes only its own callers: another set, with callers waiting on it
    ///     at the same time, shows nothing of them (so a counter that other tests' evaluations bump cannot satisfy a
    ///     wait).
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_InjectedSlots_ObserveOnlyTheirOwnCallers()
    {
        // Arrange: two independent slot sets, all slots of both held
        var mine = new ScadEvaluatorFallbackSlots(2);
        var other = new ScadEvaluatorFallbackSlots(2);
        HoldAll(mine);
        HoldAll(other);
        using var cts = new CancellationTokenSource();
        try
        {
            var (callerOnMine, mineResult) = StartTinyCaller(Shapes["function"].Make(1500), cts.Token, mine);
            Assert.True(SpinWait.SpinUntil(() => mine.Waiting == 1, Patience), "the caller never started waiting");

            // Act: a second caller waits on the other set
            var (callerOnOther, otherResult) = StartTinyCaller(Shapes["function"].Make(1500), cts.Token, other);
            Assert.True(SpinWait.SpinUntil(() => other.Waiting == 1, Patience), "the second caller never started waiting");

            // Assert: each set counts exactly its own waiter; the shared set is untouched by both
            Assert.Equal(1, mine.Waiting);
            Assert.Equal(1, other.Waiting);
            cts.Cancel();
            Assert.True(callerOnMine.Join(Patience));
            Assert.True(callerOnOther.Join(Patience));
            Assert.IsAssignableFrom<OperationCanceledException>(mineResult[0]);
            Assert.IsAssignableFrom<OperationCanceledException>(otherResult[0]);
        }
        finally
        {
            ReleaseAll(mine);
            ReleaseAll(other);
        }
    }

    /// <summary>
    ///     Validates that the unseeded random stream gives the same numbers on a restarted evaluation as on an
    ///     inline one.
    /// </summary>
    [Fact]
    public void ScadEvaluatorStack_Evaluate_Restart_RandsWithoutSeedAreReproducible()
    {
        // Arrange
        var source = "echo(rands(0, 1, 3), rands(0, 1, 2));\n" + Shapes["function"].Make(1500);

        // Act
        var inline = OnSmallStack(() => RunAndDescribe(source, null), BigCallerStackSize);
        var hopped = OnSmallStack(() => RunAndDescribe(source, null), TinyCallerStackSize);

        // Assert
        Assert.True(inline.Inline);
        Assert.False(hopped.Inline);
        Assert.Equal(inline.Text, hopped.Text);
    }

    /// <summary>
    ///     Takes every slot of a slot set, so the next caller that has to hop on that set must wait.
    /// </summary>
    /// <param name="slots">The slot set (private to the test; nothing else competes for it).</param>
    private static void HoldAll(ScadEvaluatorFallbackSlots slots)
    {
        for (var i = 0; i < slots.Capacity; i++)
        {
            slots.Acquire(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    ///     Gives back every slot taken by <see cref="HoldAll"/>.
    /// </summary>
    /// <param name="slots">The slot set.</param>
    private static void ReleaseAll(ScadEvaluatorFallbackSlots slots)
    {
        for (var i = 0; i < slots.Capacity; i++)
        {
            slots.Release();
        }
    }

    /// <summary>
    ///     Starts a caller with a tiny stack that evaluates a program and so has to hop to a fallback thread.
    /// </summary>
    /// <param name="program">The program.</param>
    /// <param name="cancellation">The cancellation token of the evaluation.</param>
    /// <param name="slots">The slot set the caller uses for the hop (set for that thread only).</param>
    /// <returns>The started thread, and a one-element array that receives the exception it ended with (or stays null).</returns>
    private static (Thread Caller, Exception?[] Result) StartTinyCaller(string program, CancellationToken cancellation, ScadEvaluatorFallbackSlots slots)
    {
        var result = new Exception?[1];
        var settings = new ScadEvaluationSettings { Cancellation = cancellation };
        var caller = new Thread(
            () =>
            {
                ScadEvaluator.SlotsForCurrentThread = slots;
                result[0] = Record.Exception(() => Run(program, settings));
            },
            TinyCallerStackSize);
        caller.Start();
        return (caller, result);
    }

    /// <summary>
    ///     Runs a program and describes everything observable about the result.
    /// </summary>
    /// <param name="program">The program.</param>
    /// <param name="settings">The settings, or <see langword="null"/>.</param>
    /// <returns>The description and whether the evaluation completed on the calling thread.</returns>
    private static Described RunAndDescribe(string program, ScadEvaluationSettings? settings)
    {
        var output = Run(program, settings);
        var text = $"echo=[{string.Join('|', output.Echo)}] diag=[{string.Join('|', output.Diagnostics.Select(d => $"{d.Location}: {d.Message}"))}] "
            + $"tree={ScadInstanceDumper.Dump(output.Roots)} steps={output.Steps} loops={output.LoopIterations} instances={output.InstanceCount}";
        return new Described(text, [.. output.Echo], ScadEvaluator.LastRunThreadId == Environment.CurrentManagedThreadId);
    }

    /// <summary>
    ///     Runs a program that must fail and describes the failure.
    /// </summary>
    /// <param name="program">The program.</param>
    /// <param name="settings">The settings, or <see langword="null"/>.</param>
    /// <returns>The message and location of the evaluation error.</returns>
    private static Failure RunAndCatch(string program, ScadEvaluationSettings? settings)
    {
        var ex = Assert.Throws<ScadEvaluationException>(() => Run(program, settings));
        return new Failure(ex.Message, ex.Location);
    }

    /// <summary>
    ///     Repeats a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="count">The number of repetitions.</param>
    /// <returns>The repeated text.</returns>
    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    /// <summary>The observable result of a successful run.</summary>
    /// <param name="Text">The full description.</param>
    /// <param name="Echo">The echo lines.</param>
    /// <param name="Inline">Whether the run completed on the calling thread.</param>
    private sealed record Described(string Text, string[] Echo, bool Inline);

    /// <summary>The observable result of a failed run.</summary>
    /// <param name="Message">The exception message.</param>
    /// <param name="Location">The exception location.</param>
    private sealed record Failure(string Message, ScadSourceLocation Location);
}
