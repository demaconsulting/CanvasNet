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

    /// <summary>
    ///     The most logical elements (nested elements included, shared vectors counted once per reference) an operand
    ///     of a recursive operator may hold: 2^26 = 67,108,864, above the default step limit of 50,000,000 so that it
    ///     only matters when the step budget is raised far beyond it.
    /// </summary>
    /// <remarks>
    ///     Operators on nested vectors visit and rebuild every logical element, and a handful of vectors that share
    ///     their children can stand for more elements than any memory could hold. A fixed cap makes the refusal
    ///     deterministic and independent of the configured step limit, and keeps the result of a shape-preserving
    ///     operator to about 1.5 GiB at most (24 bytes per element). It is deliberately not a public limit.
    /// </remarks>
    internal const long MaxAggregateElements = 1L << 26;

    /// <summary>The process-wide slots that bound the fallback threads; callers beyond the limit wait for a slot.</summary>
    private static readonly ScadEvaluatorFallbackSlots SharedSlots = new(MaxConcurrentFallbackThreads);

    /// <summary>The number of passes restarted on a fallback thread (updated with <see cref="Interlocked"/>).</summary>
    private static long _fallbackCount;

    /// <summary>The slots the current thread uses instead of <see cref="SharedSlots"/>, or <see langword="null"/> (a test seam).</summary>
    [ThreadStatic]
    private static ScadEvaluatorFallbackSlots? _slotsForThread;

    /// <summary>The action the current thread uses to start a fallback thread instead of <see cref="Thread.Start()"/>, or <see langword="null"/> (a test seam).</summary>
    [ThreadStatic]
    private static Action<Thread>? _threadStarterForThread;

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

    /// <summary>Gets the largest number of process-wide fallback threads that ever ran at once (a test hook).</summary>
    internal static int PeakConcurrentFallbackThreads => SharedSlots.Peak;

    /// <summary>
    ///     Gets or sets the slots that evaluations started by the <em>calling thread</em> use instead of the
    ///     process-wide ones; <see langword="null"/> (the default) means the process-wide slots (a test seam).
    /// </summary>
    /// <remarks>
    ///     The setting is per thread, so a test can hold every slot of its own set and observe its own waiting callers
    ///     without being affected by (or affecting) evaluations that other tests run in parallel. It must be set on the
    ///     thread that calls <c>Evaluate</c>, which is the thread that waits for a slot.
    /// </remarks>
    internal static ScadEvaluatorFallbackSlots? SlotsForCurrentThread
    {
        get => _slotsForThread;
        set => _slotsForThread = value;
    }

    /// <summary>Whether the current thread's evaluations skip the inline attempt and go straight to the fallback thread (a test seam).</summary>
    [ThreadStatic]
    private static bool _forceFallbackForThread;

    /// <summary>
    ///     Gets or sets a value indicating whether evaluations started by the <em>calling thread</em> skip the inline
    ///     attempt and run on the dedicated fallback thread, exactly as if the runtime stack guard had reported an
    ///     insufficient stack (a test seam; <see langword="false"/> by default). It makes the fallback path
    ///     deterministic, because how small a caller stack the platform really grants is not.
    /// </summary>
    internal static bool ForceFallbackForCurrentThread
    {
        get => _forceFallbackForThread;
        set => _forceFallbackForThread = value;
    }

    /// <summary>
    ///     Gets or sets the action that evaluations started by the <em>calling thread</em> use to start a fallback
    ///     thread instead of <see cref="Thread.Start()"/>; <see langword="null"/> (the default) starts it normally
    ///     (a test seam that simulates a thread-start failure, such as an out-of-memory condition).
    /// </summary>
    internal static Action<Thread>? ThreadStarterForCurrentThread
    {
        get => _threadStarterForThread;
        set => _threadStarterForThread = value;
    }

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
    /// <exception cref="ScadEvaluationException">Thrown for a failed assertion, an exceeded limit, an include problem, a dedicated large-stack thread that cannot be started (for example in a 32-bit process) and similar errors.</exception>
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
    /// <exception cref="ScadEvaluationException">Thrown when the dedicated large-stack thread the restart needs cannot be created or started.</exception>
    internal static ScadEvaluationOutput Evaluate(ScadSourceUnit unit, ScadEvaluationSettings settings, ScadEvaluationSink sink)
    {
        var executedOn = Environment.CurrentManagedThreadId;

        // Warnings recorded while the files were loaded belong to no pass, so a restart must keep them
        var loadWarnings = sink.Diagnostics.ToArray();
        try
        {
            try
            {
                if (_forceFallbackForThread)
                {
                    throw new InsufficientExecutionStackException();
                }

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

            return RunOnEvaluatorThread(
                () =>
                {
                    executedOn = Environment.CurrentManagedThreadId;
                    return new ScadEvaluator(unit, settings, sink).RunPass(stackShortageIsRecursionError: true);
                },
                settings.Cancellation);
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
    ///     <para>
    ///         The caller first waits for a slot of the slot set (see <see cref="ScadEvaluatorFallbackSlots"/>), then blocks until the thread finishes, so
    ///         the observable behavior is synchronous. The wait for a slot honors <paramref name="cancellation"/>: a
    ///         canceled token ends the wait with <see cref="OperationCanceledException"/> without acquiring a slot, so a
    ///         caller queued behind long-running evaluations can always be abandoned (the wait stays outside the
    ///         <see langword="try"/> block that releases a slot, so a slot is never released that was not acquired). An
    ///         exception thrown by the work is captured and rethrown on the calling thread with
    ///         <see cref="ExceptionDispatchInfo"/>, which preserves its type and original stack trace. The calling
    ///         thread's current culture and UI culture are applied to the dedicated thread.
    ///     </para>
    ///     <para>
    ///         <b>Memory reservation.</b> At most <see cref="MaxConcurrentFallbackThreads"/> (<c>max(2, ProcessorCount)</c>)
    ///         fallback threads exist at once and each reserves <see cref="EvaluatorStackSize"/> (384 MiB) of address
    ///         space, so the worst-case reservation is that product (768 MiB on one or two cores, 24 GiB on 64 cores).
    ///         It is reserved, not committed: the cost of a thread is the memory its stack actually touches. Measured
    ///         on Windows x64 (Release, .NET 10): a fallback thread adds about 0.1 MiB of private bytes while idle,
    ///         about 19 to 21 MiB while a recursion of 5,000 or 16,384 frames runs, and nothing after it ends, while
    ///         the process's virtual size grows by about 767 MiB per thread (the operating system reports roughly
    ///         twice the requested reservation). This is
    ///         intended for 64-bit hosts. When the reservation cannot be made (a 32-bit process, strict overcommit
    ///         accounting such as <c>vm.overcommit_memory=2</c>, or a tight virtual-memory limit) creating or
    ///         starting the thread fails with <see cref="OutOfMemoryException"/> or <see cref="ThreadStartException"/>;
    ///         that failure, and only that, is converted to a <see cref="ScadEvaluationException"/> (the slot is
    ///         released) so that the caller sees the documented controlled failure instead of a runtime exception.
    ///         Failures of the evaluation itself are never converted.
    ///     </para>
    /// </remarks>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="work">The work to run.</param>
    /// <param name="cancellation">The token that ends the wait for a slot.</param>
    /// <returns>The result of the work.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled before a slot was acquired.</exception>
    /// <exception cref="ScadEvaluationException">Thrown when the dedicated thread cannot be created or started.</exception>
    private static T RunOnEvaluatorThread<T>(Func<T> work, CancellationToken cancellation)
    {
        var result = default(T);
        ExceptionDispatchInfo? failure = null;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;

        var slots = _slotsForThread ?? SharedSlots;
        slots.Acquire(cancellation);
        try
        {
            Interlocked.Increment(ref _fallbackCount);
            var thread = StartFallbackThread(
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
                });
            thread.Join();
        }
        finally
        {
            slots.Release();
        }

        failure?.Throw();
        return result!;
    }

    /// <summary>
    ///     Creates and starts the dedicated fallback thread, converting a failure to obtain it into the documented
    ///     controlled failure.
    /// </summary>
    /// <remarks>
    ///     Only the creation and the start of the thread are guarded: reserving <see cref="EvaluatorStackSize"/>
    ///     (384 MiB) of address space can fail with <see cref="OutOfMemoryException"/> (typically in a 32-bit process
    ///     or under a tight virtual-memory limit) or with <see cref="ThreadStartException"/>. The evaluation itself
    ///     runs on the new thread, so an <see cref="OutOfMemoryException"/> it raises is captured there and rethrown
    ///     unchanged by the caller; it is never converted.
    /// </remarks>
    /// <param name="body">The thread body.</param>
    /// <returns>The started thread.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the thread cannot be created or started.</exception>
    private static Thread StartFallbackThread(ThreadStart body)
    {
        try
        {
            var thread = new Thread(body, EvaluatorStackSize)
            {
                IsBackground = true,
                Name = FallbackThreadName,
            };
            (_threadStarterForThread ?? StartThread)(thread);
            return thread;
        }
        catch (Exception ex) when (ex is OutOfMemoryException or ThreadStartException)
        {
            throw new ScadEvaluationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Evaluation needs a dedicated thread with a {EvaluatorStackSize / (1024 * 1024)} MiB stack and it could not be started (not enough memory or address space; a 32-bit process cannot supply it)"),
                ex);
        }
    }

    /// <summary>
    ///     Starts a thread (the default of the thread-start seam).
    /// </summary>
    /// <param name="thread">The thread.</param>
    private static void StartThread(Thread thread) => thread.Start();

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
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"Recursion too deep (limit {_limits.MaxRecursionDepth})"), _lastFrameLocation);
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
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"Recursion too deep (limit {_limits.MaxRecursionDepth})"), location);
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
        _steps = steps > long.MaxValue - _steps ? long.MaxValue : _steps + steps;
        if (_steps > _limits.MaxEvaluationSteps)
        {
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"Evaluation step limit exceeded (limit {_limits.MaxEvaluationSteps})"), _lastFrameLocation);
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
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"Loop iteration limit exceeded (limit {_limits.MaxLoopIterations})"), location);
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
            throw Fail(string.Create(CultureInfo.InvariantCulture, $"Loop iteration limit exceeded (limit {_limits.MaxLoopIterations})"), location);
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
