using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The signature of a function of the closed expression function table.
/// </summary>
/// <remarks>
///     A function receives its arguments as a read-only view (an absent argument is simply missing, which is how
///     JavaScript's <c>undefined</c> arises) and must be pure, deterministic and culture invariant. Failures are
///     reported with <see cref="ExpressionRuntimeException"/>.
/// </remarks>
/// <param name="arguments">The arguments.</param>
/// <param name="context">The per-evaluation context.</param>
/// <returns>The result.</returns>
internal delegate DataValue ExpressionFunction(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context);

/// <summary>
///     The per-evaluation state that functions and the evaluator share: the step budget, the cancellation token, the
///     string limit and a small cache of compiled regular expressions.
/// </summary>
/// <remarks>
///     <para>
///         One context serves one evaluator on one thread; it is the only mutable state of an evaluation, so separate
///         contexts never influence each other. The step counter is the cost model of the engine: every executed
///         instruction costs one step, and a function that does work proportional to the size of its input (searching,
///         splitting, replacing, matching) charges one more step for every 64 characters it touches, so a hostile
///         expression cannot do unbounded work within the budget.
///     </para>
///     <para>
///         Cancellation is checked whenever the step counter crosses a multiple of 1,024, which bounds the distance
///         between two checks to 1,024 instructions plus one charge.
///     </para>
/// </remarks>
internal sealed class ExpressionFunctionContext
{
    /// <summary>The number of regular expressions the cache holds before it is cleared.</summary>
    private const int RegexCacheSize = 64;

    /// <summary>The characters that one step pays for in size-proportional work.</summary>
    private const int CharactersPerStep = 64;

    /// <summary>The step interval at which the cancellation token is checked.</summary>
    private const long CancellationInterval = 1_024;

    /// <summary>The cancellation token of the call.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The compiled regular expressions of this evaluation by flags and source.</summary>
    private readonly Dictionary<string, ExpressionRegex> _regexCache = new(StringComparer.Ordinal);

    /// <summary>The compiled time formatters of this evaluation by specifier.</summary>
    private readonly Dictionary<string, TimeFormatter> _timeFormatCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionFunctionContext"/> class.
    /// </summary>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <param name="calendar">The calendar of the date and time functions (the zone of local dates); UTC when null.</param>
    public ExpressionFunctionContext(VegaLiteLimits? limits, CancellationToken cancellationToken, TimeCalendar? calendar = null)
    {
        limits ??= VegaLiteLimits.Default;
        MaxStringLength = limits.MaxStringLength;
        MaxSteps = limits.MaxEvaluationSteps;
        MaxArrayLength = limits.MaxScaleDomainSize;
        Calendar = calendar ?? TimeCalendar.Utc;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Gets the longest string an expression may produce.</summary>
    public int MaxStringLength { get; }

    /// <summary>Gets the most elements a function may build into one array (the date sequences); the limit of a scale domain.</summary>
    public int MaxArrayLength { get; }

    /// <summary>Gets the calendar of the date and time functions: the zone of local dates, UTC unless the render was given a time zone.</summary>
    public TimeCalendar Calendar { get; }

    /// <summary>Gets the cancellation token of the call, for functions that run their own loops.</summary>
    public CancellationToken CancellationToken => _cancellationToken;

    /// <summary>Gets the step budget of the evaluation.</summary>
    public long MaxSteps { get; }

    /// <summary>Gets the number of steps used so far.</summary>
    public long Steps { get; private set; }

    /// <summary>
    ///     Throws when the call has been canceled.
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested.</exception>
    public void ThrowIfCanceled() => _cancellationToken.ThrowIfCancellationRequested();

    /// <summary>
    ///     Counts one step.
    /// </summary>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested at a check point.</exception>
    public void Step()
    {
        // The counter is checked against the budget first so that the step which exhausts it is the reported one
        Steps++;
        if (Steps > MaxSteps)
        {
            throw BudgetExhausted();
        }

        if (Steps % CancellationInterval == 0)
        {
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Counts the steps of work that is proportional to a number of characters or elements.
    /// </summary>
    /// <param name="size">The number of characters or elements touched.</param>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested at a check point.</exception>
    public void ChargeSize(long size) => ChargeSteps(size / CharactersPerStep);

    /// <summary>
    ///     Counts a number of whole steps, for work that costs one step for every element it produces.
    /// </summary>
    /// <param name="units">The number of steps; zero and negative counts cost nothing.</param>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested at a check point.</exception>
    public void ChargeSteps(long units)
    {
        // Small inputs cost nothing extra: the instruction that called the function was already counted
        if (units <= 0)
        {
            return;
        }

        var before = Steps;
        if (units > MaxSteps - before)
        {
            Steps = MaxSteps + 1;
            throw BudgetExhausted();
        }

        Steps += units;
        if (before / CancellationInterval != Steps / CancellationInterval)
        {
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Checks that a string of a given length may be produced.
    /// </summary>
    /// <param name="length">The length in UTF-16 code units.</param>
    /// <exception cref="ExpressionRuntimeException">Thrown when the length exceeds <see cref="MaxStringLength"/>.</exception>
    public void CheckStringLength(long length)
    {
        if (length > MaxStringLength)
        {
            throw StringLimitExceeded();
        }
    }

    /// <summary>
    ///     Creates the exception for a string that is longer than <see cref="MaxStringLength"/>.
    /// </summary>
    /// <returns>The exception.</returns>
    public ExpressionRuntimeException StringLimitExceeded() =>
        new($"The expression produced a string longer than the limit of {MaxStringLength} characters (VegaLiteLimits.MaxStringLength).");

    /// <summary>
    ///     Gets the compiled formatter of a time format specifier, from the cache when it was used before.
    /// </summary>
    /// <param name="specifier">The d3 time format specifier.</param>
    /// <returns>The formatter.</returns>
    public TimeFormatter GetTimeFormatter(string specifier)
    {
        if (_timeFormatCache.TryGetValue(specifier, out var cached))
        {
            return cached;
        }

        var created = TimeFormat.Compile(specifier);
        if (_timeFormatCache.Count >= RegexCacheSize)
        {
            _timeFormatCache.Clear();
        }

        _timeFormatCache[specifier] = created;
        return created;
    }

    /// <summary>
    ///     Gets the compiled form of a regular expression, from the cache when it was used before.
    /// </summary>
    /// <param name="source">The pattern text.</param>
    /// <param name="flags">The JavaScript flags.</param>
    /// <returns>The compiled regular expression.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the pattern or the flags are invalid or too long.</exception>
    public ExpressionRegex GetRegex(string source, string flags)
    {
        var key = flags + "/" + source;
        if (_regexCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var created = ExpressionRegex.Create(source, flags);
        if (_regexCache.Count >= RegexCacheSize)
        {
            _regexCache.Clear();
        }

        _regexCache[key] = created;
        return created;
    }

    /// <summary>
    ///     Creates the exception for an exhausted step budget.
    /// </summary>
    /// <returns>The exception.</returns>
    private ExpressionRuntimeException BudgetExhausted() =>
        new($"The evaluation exceeded the limit of {MaxSteps} steps (VegaLiteLimits.MaxEvaluationSteps).");
}
