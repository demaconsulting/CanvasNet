namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     A bounded set of slots for evaluator fallback threads, with the counters that observe it.
/// </summary>
/// <remarks>
///     <para>
///         Each fallback thread reserves a large stack, so the number that may run at once is bounded: a caller that
///         must hop to a fallback thread first <see cref="Acquire"/>s a slot and <see cref="Release"/>s it when the
///         thread has finished. The process-wide set is <see cref="ScadEvaluator"/>'s shared instance. The counters
///         (waiting callers, active threads, peak, starts) belong to the instance, so a test that injects its own
///         instance for one calling thread observes only its own callers and cannot be confused by evaluations that
///         other tests run in parallel.
///     </para>
///     <para>All members are thread-safe.</para>
/// </remarks>
internal sealed class ScadEvaluatorFallbackSlots
{
    /// <summary>Limits the number of concurrent holders; callers beyond the limit wait.</summary>
    private readonly SemaphoreSlim _gate;

    /// <summary>The number of callers currently waiting for a slot (updated with <see cref="Interlocked"/>).</summary>
    private int _waiting;

    /// <summary>The number of slots currently held (updated with <see cref="Interlocked"/>).</summary>
    private int _active;

    /// <summary>The largest number of slots that were held at once (updated with <see cref="Interlocked"/>).</summary>
    private int _peak;

    /// <summary>The number of slots acquired since creation (updated with <see cref="Interlocked"/>).</summary>
    private long _started;

    /// <summary>
    ///     Initializes a set of slots.
    /// </summary>
    /// <param name="capacity">The number of slots; at least one.</param>
    public ScadEvaluatorFallbackSlots(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _gate = new SemaphoreSlim(capacity, capacity);
    }

    /// <summary>Gets the number of slots.</summary>
    public int Capacity { get; }

    /// <summary>Gets the number of slots that are free right now.</summary>
    public int FreeSlots => _gate.CurrentCount;

    /// <summary>Gets the number of callers that are waiting for a slot right now.</summary>
    public int Waiting => Volatile.Read(ref _waiting);

    /// <summary>Gets the largest number of slots that were held at once.</summary>
    public int Peak => Volatile.Read(ref _peak);

    /// <summary>Gets the number of slots acquired since creation.</summary>
    public long Started => Interlocked.Read(ref _started);

    /// <summary>
    ///     Waits for a free slot and takes it, honoring cancellation while waiting.
    /// </summary>
    /// <param name="cancellation">The token that ends the wait.</param>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled; no slot was taken.</exception>
    public void Acquire(CancellationToken cancellation)
    {
        Interlocked.Increment(ref _waiting);
        try
        {
            _gate.Wait(cancellation);
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
        }

        // The slot is held from here: record the start and keep the observed peak up to date
        Interlocked.Increment(ref _started);
        var active = Interlocked.Increment(ref _active);
        int peak;
        while (active > (peak = Volatile.Read(ref _peak)) && Interlocked.CompareExchange(ref _peak, active, peak) != peak)
        {
            // Another thread raised the peak in between; look again
        }
    }

    /// <summary>
    ///     Gives back a slot taken by <see cref="Acquire"/>; it must be called exactly once per successful acquisition.
    /// </summary>
    public void Release()
    {
        Interlocked.Decrement(ref _active);
        _gate.Release();
    }
}
