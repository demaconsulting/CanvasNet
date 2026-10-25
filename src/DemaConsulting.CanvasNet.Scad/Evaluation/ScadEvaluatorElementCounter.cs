using System.Runtime.CompilerServices;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Counts the logical elements of a nested vector (nested elements included) with a cap, saturating arithmetic and
///     a memo for shared sub-vectors, so that the count is cheap even for vectors that share their children.
/// </summary>
/// <remarks>
///     <para>
///         Vectors are shared by reference: seven levels of vectors, each holding a thousand references to the level
///         below, are a few thousand objects but stand for about 10^21 logical elements. Walking every reference
///         would never finish, so the count of each large sub-vector is remembered by reference and found again
///         instead of being recomputed; and the count stops as soon as it exceeds the cap, because anything above
///         the cap is refused anyway.
///     </para>
///     <para>
///         Only the exact count of a sub-vector that was counted completely is remembered, and only when it holds at
///         least <see cref="MemoThreshold"/> elements, so lists of small vectors (such as point lists) never pay
///         for the memo. A counter is for one count; it is not thread-safe.
///     </para>
/// </remarks>
internal sealed class ScadEvaluatorElementCounter
{
    /// <summary>The smallest exact count of a sub-vector that is remembered.</summary>
    internal const long MemoThreshold = 1024;

    /// <summary>The number of counted elements between two cancellation checks.</summary>
    internal const long PollInterval = 1L << 16;

    /// <summary>The token checked every <see cref="PollInterval"/> counted elements.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The exact counts of the large sub-vectors counted completely so far, by reference; created on first use.</summary>
    private Dictionary<ScadVector, long>? _memo;

    /// <summary>The number of elements counted since the last poll.</summary>
    private long _unpolled;

    /// <summary>
    ///     Initializes a counter.
    /// </summary>
    /// <param name="cancellation">The token checked every <see cref="PollInterval"/> counted elements; default for none.</param>
    /// <exception cref="OperationCanceledException">Thrown by <see cref="Count"/> when the token is canceled while counting.</exception>
    public ScadEvaluatorElementCounter(CancellationToken cancellation = default) => _cancellation = cancellation;

    /// <summary>
    ///     Gets the number of vector visits that were answered from the memo (for tests).
    /// </summary>
    public int MemoHits { get; private set; }

    /// <summary>
    ///     Counts the elements of a vector: its own length (at least one) plus the counts of its nested vectors.
    /// </summary>
    /// <remarks>
    ///     The result is exact when it is not above <paramref name="cap"/>. Once the running count exceeds the cap
    ///     the walk stops and the result is some value above the cap, so the caller learns only that the cap was
    ///     exceeded. The sum cannot overflow: it saturates at <see cref="long.MaxValue"/>.
    /// </remarks>
    /// <param name="vector">The vector.</param>
    /// <param name="cap">The count beyond which counting stops; zero or more.</param>
    /// <returns>The count, or a value above <paramref name="cap"/> when the count exceeds it.</returns>
    public long Count(ScadVector vector, long cap)
    {
        // A vector counted completely before (a shared child) is answered without walking it again
        if (_memo is not null && _memo.TryGetValue(vector, out var known))
        {
            MemoHits++;
            return known;
        }

        var items = vector.AsSpan();
        long count = Math.Max(1, items.Length);
        _unpolled += count;
        if (_unpolled >= PollInterval)
        {
            _unpolled = 0;
            _cancellation.ThrowIfCancellationRequested();
        }

        foreach (var item in items)
        {
            if (count > cap)
            {
                // Over the cap: the result is incomplete, so it must not be remembered
                return count;
            }

            if (item.Kind == ScadValueKind.Vector)
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
                var nested = Count(item.AsVector, cap - count);
                count = nested > long.MaxValue - count ? long.MaxValue : count + nested;
            }
        }

        // The loop ends without the check of the last element, so a final overshoot is also incomplete
        if (count <= cap && count >= MemoThreshold)
        {
            (_memo ??= new Dictionary<ScadVector, long>())[vector] = count;
        }

        return count;
    }
}
