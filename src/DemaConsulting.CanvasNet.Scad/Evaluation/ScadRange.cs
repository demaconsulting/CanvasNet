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
///         The element count is <c>floor((End - Start) / Step) + 1</c>, corrected by at most one element in either
///         direction so that the last element is judged with the very value <see cref="ElementAt"/> computes: it is
///         kept when it does not lie beyond <see cref="End"/> by more than floating-point rounding error, and a
///         further element is added when it lies beyond <see cref="End"/> by no more than that error. The error
///         allowance is a few units in the last place (ULP) of the largest magnitude involved
///         (<c>max(|Start|, |End|, |Step| * n)</c>), and never more than a quarter of a step, so <c>[0 : 0.1 : 0.3]</c>
///         keeps its last element (about <c>0.3</c>) while <c>[0 : 0.1 : 0.2999999995]</c> does not reach
///         <c>0.3</c>: a fixed absolute tolerance would admit a value that is genuinely beyond the end. A range that
///         is invalid (a zero or non-finite step, or a non-finite bound) or that runs away from its end (a positive
///         step with <c>Start &gt; End</c>, a negative step with <c>Start &lt; End</c>) has no elements.
///     </para>
/// </remarks>
internal sealed class ScadRange : IEquatable<ScadRange>
{
    /// <summary>The number of units in the last place (ULP) of rounding error the end of a range may be exceeded by.</summary>
    private const int EndToleranceInLastPlaces = 4;

    /// <summary>The step counts below this value (2^53) are exact in a double, so the last element can be re-checked.</summary>
    private const double ExactStepLimit = 9007199254740992.0;

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

            // Saturate rather than overflow on absurd spans (the span itself may overflow to infinity)
            var steps = Math.Floor((End - Start) / Step);
            if (steps >= MaxReportedCount)
            {
                return MaxReportedCount;
            }

            // Beyond 2^53 steps the elements are not distinguishable anyway, so the estimate is used as is
            if (steps >= ExactStepLimit)
            {
                return (long)steps + 1;
            }

            // Judge the last element by the value that is actually generated, so that only rounding error is absorbed
            var last = (long)steps;
            while (last > 0 && IsBeyondEnd(ElementAt(last), last))
            {
                last--;
            }

            if (!IsBeyondEnd(ElementAt(last + 1), last + 1))
            {
                last++;
            }

            return last + 1;
        }
    }

    /// <summary>
    ///     Determines whether an element lies beyond the end by more than floating-point rounding error.
    /// </summary>
    /// <param name="value">The element value.</param>
    /// <param name="index">The element index, which scales the error of <c>index * Step</c>.</param>
    /// <returns><see langword="true"/> when the element is too far beyond <see cref="End"/> to be the end itself.</returns>
    private bool IsBeyondEnd(double value, long index)
    {
        // The rounding error is a few ULP of the largest magnitude that took part, but never a quarter of a step or more
        var scale = Math.Max(Math.Max(Math.Abs(Start), Math.Abs(End)), Math.Abs(Step) * index);
        var tolerance = Math.Min(EndToleranceInLastPlaces * (Math.BitIncrement(scale) - scale), Math.Abs(Step) / 4);
        return Step > 0 ? value > End + tolerance : value < End - tolerance;
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
