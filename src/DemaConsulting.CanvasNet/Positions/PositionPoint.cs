namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     A point in the pixels of a rendered picture.
/// </summary>
/// <remarks>
///     Continuous coordinates with the origin at the top-left corner of the picture: pixel <c>i</c> spans
///     <c>[i, i + 1)</c>, x grows to the right and y grows downward. The value is immutable.
/// </remarks>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct PositionPoint(double X, double Y)
{
    /// <summary>
    ///     Tells whether both coordinates are finite.
    /// </summary>
    /// <returns><see langword="true"/> when the point can be reported.</returns>
    public bool IsValid()
    {
        return double.IsFinite(X) && double.IsFinite(Y);
    }
}
