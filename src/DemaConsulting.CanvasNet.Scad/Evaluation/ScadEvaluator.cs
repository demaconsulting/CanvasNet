using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Evaluates a parsed OpenSCAD-language program into a geometry-agnostic instantiation tree.
/// </summary>
/// <remarks>
///     <para>
///         Evaluation has two phases. Phase A resolves <c>include</c> and <c>use</c> (<see cref="ScadFileResolver"/>) on
///         the calling thread: it is the only place that touches the file system or the caller's resolver callback and
///         produces immutable parsed units. Phase B, this class, is pure computation over those units. It records
///         every echo line, warning and unsupported-feature report in a <see cref="ScadEvaluationSink"/> and never
///         calls user code, so a pass can be discarded and re-run without any observable effect.
///     </para>
///     <para>
///         Stack safety mirrors <see cref="ScadParser"/>. Every evaluation <em>frame</em> (an expression, a statement, a
///         comprehension element, a <c>children()</c> expansion, a user-module body) increments a counter that is
///         compared with <see cref="ScadLimits.MaxRecursionDepth"/>; the counter alone defines the nominal limit, so
///         the limit behaves identically on every platform and stack size. The pass first runs inline on the caller's
///         thread, guarded by <see cref="RuntimeHelpers.EnsureSufficientExecutionStack"/> at every frame (and in the
///         recursive value operations). Only when that guard reports a short stack is the pass discarded and restarted
///         on a dedicated thread whose stack (<see cref="EvaluatorStackSize"/>) is sized for the worst case at the
///         ceiling of <see cref="ScadLimits.MaxRecursionDepth"/>, with the number of such threads bounded
///         (<see cref="MaxConcurrentFallbackThreads"/>). Tail calls of user functions run in a loop and add no frame.
///     </para>
///     <para>
///         Work is bounded by <see cref="ScadLimits.MaxEvaluationSteps"/>: every expression, statement, loop turn and
///         tail-call turn charges one step, vector work and long scope walks charge more, and the cancellation token
///         is polled while charging. Instances are single-use and not thread-safe; the static entry points are safe
///         for concurrent use.
///     </para>
///     <para>
///         This file holds the entry points, the restart mechanics and the accounting; the language is in
///         <c>ScadEvaluator.Scopes.cs</c>, <c>.Expressions.cs</c>, <c>.Calls.cs</c>, <c>.Statements.cs</c>,
///         <c>.Modules.cs</c> and <c>.Comprehensions.cs</c>, and the built-in function host in <c>.Host.cs</c>.
///     </para>
/// </remarks>
internal sealed partial class ScadEvaluator
{
    /// <summary>
    ///     The stack size in bytes of the dedicated evaluator thread (384 MiB).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Derived by measurement. For every pathological shape at the ceiling of 16,384 frames (non-tail recursion
    ///         of functions, closures and mutually recursive functions, tail-position <c>let</c>/<c>echo</c>/<c>assert</c>
    ///         chains, ternary chains, nested expressions inside recursion, vector literals inside recursion, module
    ///         recursion, blocks, <c>children()</c> chains and comprehensions), the smallest caller stack on which the
    ///         pass completes inline with the runtime guard active was found by bisection. The worst case is function
    ///         recursion: about 37 MiB in an unoptimized Debug build (roughly 2.4 KiB per accounted frame, including the
    ///         64 to 128 KiB the guard reserves) and about 17 MiB in a Release build (roughly 1.1 KiB per frame). The
    ///         other shapes need less than half of that. 384 MiB is therefore a margin of about 10 times in Debug
    ///         and 22 times in Release; it also absorbs larger frames on other architectures and runtimes. The memory is
    ///         reserved address space that is committed only as it is touched. Per-frame cost stays far below the
    ///         8 KiB at which the ceiling of <see cref="ScadLimits.MaxRecursionDepth"/> would have to be lowered.
    ///     </para>
    /// </remarks>
    internal const int EvaluatorStackSize = 384 * 1024 * 1024;

    /// <summary>The name of the dedicated fallback thread.</summary>
    internal const string FallbackThreadName = "ScadEvaluator";

    /// <summary>
    ///     The most fallback threads that may run at once. Each reserves <see cref="EvaluatorStackSize"/> of address
    ///     space, so the bound keeps the reservation of a burst of concurrent small-stack callers finite.
    /// </summary>
    internal static readonly int MaxConcurrentFallbackThreads = Math.Max(2, Environment.ProcessorCount);

    /// <summary>The number of steps between two cancellation polls (a power of two).</summary>
    private const int PollInterval = 1024;

    /// <summary>Limits the number of concurrently running fallback threads; callers beyond the limit wait for a slot.</summary>
    private static readonly SemaphoreSlim FallbackGate = new(MaxConcurrentFallbackThreads, MaxConcurrentFallbackThreads);

    /// <summary>The number of passes restarted on a fallback thread (updated with <see cref="Interlocked"/>).</summary>
    private static long _fallbackCount;

    /// <summary>The number of fallback threads currently running (updated with <see cref="Interlocked"/>).</summary>
    private static int _activeFallbacks;

    /// <summary>The largest number of fallback threads that ran at once (updated with <see cref="Interlocked"/>).</summary>
    private static int _peakFallbacks;

    /// <summary>The managed thread id that completed the most recent evaluation started by the current thread.</summary>
    [ThreadStatic]
    private static int _lastRunThreadId;

    /// <summary>The source unit being evaluated.</summary>
    private readonly ScadSourceUnit _unit;

    /// <summary>The evaluation settings.</summary>
    private readonly ScadEvaluationSettings _settings;

    /// <summary>The resource limits.</summary>
    private readonly ScadLimits _limits;

    /// <summary>The buffer for echo, warnings and unsupported-feature reports.</summary>
    private readonly ScadEvaluationSink _sink;

    /// <summary>The current frame depth.</summary>
    private int _depth;

    /// <summary>The steps charged so far.</summary>
    private long _steps;

    /// <summary>The step count at the last cancellation poll.</summary>
    private long _lastPoll;

    /// <summary>The loop iterations charged so far.</summary>
    private long _loopIterations;

    /// <summary>The instances created so far.</summary>
    private int _instances;

    /// <summary>The location of the most recently entered frame (for the defense-in-depth error).</summary>
    private ScadSourceLocation _lastFrameLocation;

    /// <summary>
    ///     Initializes a single-use evaluator.
    /// </summary>
    /// <param name="unit">The include-expanded main unit.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="sink">The sink.</param>
    private ScadEvaluator(ScadSourceUnit unit, ScadEvaluationSettings settings, ScadEvaluationSink sink)
    {
        _unit = unit;
        _settings = settings;
        _limits = settings.Limits;
        _sink = sink;
        _random = new ScadRandom(ScadRandom.DefaultState);
        _defaults = BuildDefaultsScope();
    }

    /// <summary>Gets the number of passes that had to be restarted on a fallback thread since process start (a test hook).</summary>
    internal static long FallbackCount => Interlocked.Read(ref _fallbackCount);

    /// <summary>Gets the largest number of fallback threads that ever ran at once (a test hook).</summary>
    internal static int PeakConcurrentFallbackThreads => Volatile.Read(ref _peakFallbacks);

    /// <summary>
    ///     Gets the managed thread id that completed the most recent evaluation started by the calling thread: the
    ///     calling thread itself unless the pass fell back to a dedicated thread (a test hook; per calling thread).
    /// </summary>
    internal static int LastRunThreadId => _lastRunThreadId;

    /// <summary>
    ///     Evaluates a document: resolves <c>include</c>/<c>use</c> on the calling thread, then evaluates.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="sink">The sink that receives echo, warnings and unsupported-feature reports. It also holds them when evaluation fails.</param>
    /// <returns>The evaluation output.</returns>
    /// <exception cref="ScadEvaluationException">Thrown for a failed assertion, an exceeded limit, an include problem and similar errors.</exception>
    /// <exception cref="ScadSyntaxException">Thrown when an included or used file has a syntax error.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the settings' cancellation token is canceled.</exception>
    internal static ScadEvaluationOutput Evaluate(ScadDocument document, ScadEvaluationSettings settings, ScadEvaluationSink sink)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sink);

        // Cancellation wins over everything, including the first resolver callback
        settings.Cancellation.ThrowIfCancellationRequested();
        var resolver = new ScadFileResolver(document.Options, settings.Limits, sink, settings.Cancellation);
        var unit = resolver.Load(document.Program, document.SourceName);
        return Evaluate(unit, settings, sink);
    }

    /// <summary>
    ///     Evaluates an already resolved unit. The pass runs on the calling thread and, only when the caller's stack
    ///     runs short, is restarted on a dedicated large-stack thread.
    /// </summary>
    /// <remarks>
    ///     The inline attempt costs nothing extra for ordinary programs. The runtime stack guard tripping means the
    ///     caller's stack is too small for the depth reached so far, which is a property of the caller and not of the
    ///     program, so the pass is repeated from the start (it is pure: everything it reports is buffered in the sink,
    ///     which is reset first) on a thread sized for the recursion ceiling. The nominal frame counter never triggers
    ///     the restart: it reports the recursion error identically on every platform and stack size.
    /// </remarks>
    /// <param name="unit">The include-expanded main unit.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>The evaluation output.</returns>
    internal static ScadEvaluationOutput Evaluate(ScadSourceUnit unit, ScadEvaluationSettings settings, ScadEvaluationSink sink)
    {
        var executedOn = Environment.CurrentManagedThreadId;

        // Warnings recorded while the files were loaded belong to no pass, so a restart must keep them
        var loadWarnings = sink.Diagnostics.ToArray();
        try
        {
            try
            {
                return new ScadEvaluator(unit, settings, sink).RunPass(stackShortageIsRecursionError: false);
            }
            catch (InsufficientExecutionStackException)
            {
                // The caller's stack is too small; discard everything the pass recorded and restart
                sink.Reset();
                foreach (var warning in loadWarnings)
                {
                    sink.Warn(warning.Message, warning.Location);
                }
            }

            return RunOnEvaluatorThread(() =>
            {
                executedOn = Environment.CurrentManagedThreadId;
                return new ScadEvaluator(unit, settings, sink).RunPass(stackShortageIsRecursionError: true);
            });
        }
        finally
        {
            _lastRunThreadId = executedOn;
        }
    }

    /// <summary>
    ///     Runs work on a dedicated thread whose stack is sized for the recursion ceiling, with the number of such
    ///     threads bounded.
    /// </summary>
    /// <remarks>
    ///     The caller first waits for a slot of <see cref="FallbackGate"/>, then blocks until the thread finishes, so
    ///     the observable behavior is synchronous. An exception thrown by the work is captured and rethrown on the
    ///     calling thread with <see cref="ExceptionDispatchInfo"/>, which preserves its type and original stack trace.
    ///     The calling thread's current culture and UI culture are applied to the dedicated thread.
    /// </remarks>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="work">The work to run.</param>
    /// <returns>The result of the work.</returns>
    private static T RunOnEvaluatorThread<T>(Func<T> work)
    {
        var result = default(T);
        ExceptionDispatchInfo? failure = null;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;

        FallbackGate.Wait();
        TrackFallbackStart();
        try
        {
            Interlocked.Increment(ref _fallbackCount);
            var thread = new Thread(
                () =>
                {
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = uiCulture;
                    try
                    {
                        result = work();
                    }
                    catch (Exception ex)
                    {
                        // Every exception is handed back to the caller unchanged, so none may be lost here
                        failure = ExceptionDispatchInfo.Capture(ex);
                    }
                },
                EvaluatorStackSize)
            {
                IsBackground = true,
                Name = FallbackThreadName,
            };
            thread.Start();
            thread.Join();
        }
        finally
        {
            Interlocked.Decrement(ref _activeFallbacks);
            FallbackGate.Release();
        }

        failure?.Throw();
        return result!;
    }

    /// <summary>Records that a fallback thread is starting, keeping the observed peak up to date (a test hook).</summary>
    private static void TrackFallbackStart()
    {
        var active = Interlocked.Increment(ref _activeFallbacks);
        int peak;
        while (active > (peak = Volatile.Read(ref _peakFallbacks)) && Interlocked.CompareExchange(ref _peakFallbacks, active, peak) != peak)
        {
            // Another thread raised the peak concurrently; re-read and compare again
        }
    }

    /// <summary>
    ///     Runs one evaluation pass.
    /// </summary>
    /// <param name="stackShortageIsRecursionError">
    ///     <see langword="true"/> to report a tripped runtime stack guard as the recursion-limit error (used on the
    ///     dedicated thread, where it is only defense in depth); <see langword="false"/> to let
    ///     <see cref="InsufficientExecutionStackException"/> escape so the caller can retry on a larger stack.
    /// </param>
    /// <returns>The output.</returns>
    private ScadEvaluationOutput RunPass(bool stackShortageIsRecursionError)
    {
        try
        {
            var fileScope = BuildMainScope();
            var roots = new List<ScadInstance>();
            ExecuteStatements(_unit.Statements, fileScope, roots);
            return new ScadEvaluationOutput(
                roots,
                _sink.EchoLines.ToArray(),
                _sink.Diagnostics.ToArray(),
                _sink.UnsupportedFeatures.ToArray(),
                _instances,
                _steps,
                _loopIterations);
        }
        catch (InsufficientExecutionStackException) when (stackShortageIsRecursionError)
        {
            // Not expected with an evaluator-sized stack; report it like the frame counter would
            throw Fail($"Recursion too deep (limit {_limits.MaxRecursionDepth})", _lastFrameLocation);
        }
    }

    /// <summary>
    ///     Enters an evaluation frame: counts it against the recursion limit and checks the runtime stack guard.
    /// </summary>
    /// <param name="location">The location of the construct being evaluated.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the nominal recursion limit is exceeded.</exception>
    /// <exception cref="InsufficientExecutionStackException">Thrown when the caller's stack is too small (the restart trigger).</exception>
    private void EnterFrame(ScadSourceLocation location)
    {
        _depth++;
        _lastFrameLocation = location;
        if (_depth > _limits.MaxRecursionDepth)
        {
            throw Fail($"Recursion too deep (limit {_limits.MaxRecursionDepth})", location);
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
    }

    /// <summary>
    ///     Leaves an evaluation frame.
    /// </summary>
    private void LeaveFrame() => _depth--;

    /// <summary>
    ///     Charges evaluation steps, enforcing the step limit and polling the cancellation token periodically.
    /// </summary>
    /// <param name="steps">The number of steps; at least one.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the step limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested.</exception>
    private void Charge(long steps)
    {
        _steps += steps;
        if (_steps > _limits.MaxEvaluationSteps)
        {
            throw Fail($"Evaluation step limit exceeded (limit {_limits.MaxEvaluationSteps})", _lastFrameLocation);
        }

        if ((_steps / PollInterval) != (_lastPoll / PollInterval))
        {
            _lastPoll = _steps;
            _settings.Cancellation.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Charges one loop turn: one step, one loop iteration and a cancellation poll.
    /// </summary>
    /// <param name="location">The location of the loop.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the loop-iteration or step limit is exceeded.</exception>
    private void ChargeLoopTurn(ScadSourceLocation location)
    {
        if (++_loopIterations > _limits.MaxLoopIterations)
        {
            throw Fail($"Loop iteration limit exceeded (limit {_limits.MaxLoopIterations})", location);
        }

        _settings.Cancellation.ThrowIfCancellationRequested();
        Charge(1);
    }

    /// <summary>
    ///     Verifies that a loop with the given number of turns fits in the remaining loop budget, before any turn runs.
    /// </summary>
    /// <param name="count">The number of turns.</param>
    /// <param name="location">The location of the loop.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the turns cannot fit.</exception>
    private void CheckLoopBudget(long count, ScadSourceLocation location)
    {
        if (count > _limits.MaxLoopIterations - _loopIterations)
        {
            throw Fail($"Loop iteration limit exceeded (limit {_limits.MaxLoopIterations})", location);
        }
    }

    /// <summary>
    ///     Charges the extra steps for a long scope or dynamic-chain walk, so lookup cost is bounded by the step limit.
    /// </summary>
    /// <param name="hops">The number of scopes or frames walked.</param>
    private void ChargeHops(int hops)
    {
        if (hops >= 32)
        {
            Charge(hops >> 5);
        }
    }

    /// <summary>
    ///     Creates the exception for a failed evaluation.
    /// </summary>
    /// <param name="description">The description without location.</param>
    /// <param name="location">The location.</param>
    /// <returns>The exception to throw.</returns>
    private static ScadEvaluationException Fail(string description, ScadSourceLocation location) => new(description, location);

    /// <summary>
    ///     Records a warning.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="location">The location.</param>
    private void Warn(string message, ScadSourceLocation location) => _sink.Warn(message, location);
}
