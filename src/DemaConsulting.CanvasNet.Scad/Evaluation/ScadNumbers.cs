namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Exact IEEE-754 comparisons of doubles, written as named helpers.
/// </summary>
/// <remarks>
///     The language defines number equality as exact IEEE equality (so <c>NaN</c> equals nothing and <c>-0</c>
///     equals <c>0</c>). Static analysis flags a raw <c>==</c> between two floating-point operands because it is
///     usually a bug; here it is the specification, so the intent is stated once in these helpers.
/// </remarks>
internal static class ScadNumbers
{
    /// <summary>
    ///     Tests two doubles for IEEE equality.
    /// </summary>
    /// <param name="left">The left number.</param>
    /// <param name="right">The right number.</param>
    /// <returns><see langword="true"/> when both are non-<c>NaN</c> and numerically equal (<c>0</c> equals <c>-0</c>).</returns>
    public static bool AreEqual(double left, double right) =>
        !double.IsNaN(left) && !double.IsNaN(right) && left.CompareTo(right) == 0;
}
