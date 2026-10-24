namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The immutable payload of a range <see cref="ScadValue"/>: a lazily generated arithmetic sequence.
/// </summary>
/// <remarks>
///     <para>
///         A range is never materialized by the value model; iteration constructs ask for <see cref="Count"/>
///         first (so a huge range can be rejected before any work is done) and then for single elements.
///         Element <c>i</c> is always computed as <c>Start + i * Step</c> and never accumulated, so results do not
///         drift and are identical on every platform.
///     </para>
///     <para>
///         The element count is <c>floor((End - Start) / Step + 1e-9) + 1</c>. The small epsilon keeps ranges such as
///         <c>[0 : 0.1 : 1]</c> from losing their last element to representation error. A range that is invalid
///         (a zero or non-finite step, or a non-finite bound) or that runs away from its end (a positive step with
///         <c>Start &gt; End</c>, a negative step with <c>Start &lt; End</c>) has no elements.
///     </para>
/// </remarks>
internal sealed class ScadRange : IEquatable<ScadRange>
{
    /// <summary>The tolerance added before truncating the step count so that representation error cannot drop the last element.</summary>
    private const double CountEpsilon = 1e-9;

    /// <summary>The largest element count a range reports (one less than the maximum so adding one cannot overflow).</summary>
    private const long MaxReportedCount = long.MaxValue - 1;

    /// <summary>
    ///     Initializes a range.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="step">The increment between values.</param>
    /// <param name="end">The inclusive limit.</param>
    internal ScadRange(double start, double step, double end)
    {
        Start = start;
        Step = step;
        End = end;
    }

    /// <summary>
    ///     Gets the first value.
    /// </summary>
    public double Start { get; }

    /// <summary>
    ///     Gets the increment between values (one when the source omitted it).
    /// </summary>
    public double Step { get; }

    /// <summary>
    ///     Gets the inclusive limit.
    /// </summary>
    public double End { get; }

    /// <summary>
    ///     Gets a value indicating whether the range cannot be iterated because its step is zero or a bound or the step is not finite.
    /// </summary>
    /// <remarks>
    ///     The evaluator reports this condition as a warning; <see cref="Count"/> is zero.
    /// </remarks>
    public bool IsInvalid =>
        Step == 0 || !double.IsFinite(Start) || !double.IsFinite(Step) || !double.IsFinite(End);

    /// <summary>
    ///     Gets the number of elements.
    /// </summary>
    /// <remarks>
    ///     The count saturates at <c>long.MaxValue - 1</c> so callers can compare it against a loop budget without overflow.
    /// </remarks>
    public long Count
    {
        get
        {
            // Invalid ranges and ranges that run away from their end are empty
            if (IsInvalid || (Step > 0 && Start > End) || (Step < 0 && Start < End))
            {
                return 0;
            }

            // The step count is truncated after adding the epsilon; saturate rather than overflow on absurd spans
            var steps = Math.Floor(((End - Start) / Step) + CountEpsilon);
            return steps >= MaxReportedCount ? MaxReportedCount : (long)steps + 1;
        }
    }

    /// <summary>
    ///     Computes an element.
    /// </summary>
    /// <param name="index">The zero-based index; must be less than <see cref="Count"/>.</param>
    /// <returns><c>Start + index * Step</c>.</returns>
    public double ElementAt(long index) => Start + (index * Step);

    /// <inheritdoc />
    public bool Equals(ScadRange? other) =>
        other is not null
        && ScadNumbers.AreEqual(Start, other.Start)
        && ScadNumbers.AreEqual(Step, other.Step)
        && ScadNumbers.AreEqual(End, other.End);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ScadRange);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Start, Step, End);
}
