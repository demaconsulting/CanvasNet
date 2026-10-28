// cspell:ignore scenegraph
namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     An axis-aligned bounding box, or the empty box that contains nothing (Vega's <c>Bounds</c>).
/// </summary>
/// <remarks>
///     The empty box is the identity of <see cref="Union"/>: its minimum is positive infinity and its maximum negative
///     infinity, so that the union of a box with it is the box. Layout grows the bounds of a view by uniting the boxes of
///     its guides and title.
/// </remarks>
/// <param name="X1">The left edge.</param>
/// <param name="Y1">The top edge.</param>
/// <param name="X2">The right edge.</param>
/// <param name="Y2">The bottom edge.</param>
internal readonly record struct SceneBounds(double X1, double Y1, double X2, double Y2)
{
    /// <summary>Gets the empty box.</summary>
    public static SceneBounds Empty { get; } = new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

    /// <summary>Gets a value indicating whether the box contains nothing.</summary>
    public bool IsEmpty => X1 > X2 || Y1 > Y2;

    /// <summary>Gets the width, or 0 for the empty box.</summary>
    public double Width => IsEmpty ? 0 : X2 - X1;

    /// <summary>Gets the height, or 0 for the empty box.</summary>
    public double Height => IsEmpty ? 0 : Y2 - Y1;

    /// <summary>
    ///     Gets the smallest box that contains this box and another.
    /// </summary>
    /// <param name="other">The other box.</param>
    /// <returns>The union.</returns>
    public SceneBounds Union(SceneBounds other)
    {
        if (other.IsEmpty)
        {
            return this;
        }

        return IsEmpty
            ? other
            : new SceneBounds(Math.Min(X1, other.X1), Math.Min(Y1, other.Y1), Math.Max(X2, other.X2), Math.Max(Y2, other.Y2));
    }

    /// <summary>
    ///     Moves the box.
    /// </summary>
    /// <param name="dx">The horizontal distance.</param>
    /// <param name="dy">The vertical distance.</param>
    /// <returns>The moved box; the empty box stays empty.</returns>
    public SceneBounds Translate(double dx, double dy) => IsEmpty ? this : new SceneBounds(X1 + dx, Y1 + dy, X2 + dx, Y2 + dy);

    /// <summary>
    ///     Gets the box that encloses this box after it is rotated about the origin.
    /// </summary>
    /// <remarks>Quarter turns are exact (no rounding noise from the sine and cosine).</remarks>
    /// <param name="degrees">The angle in degrees, clockwise on a canvas whose y axis points down.</param>
    /// <returns>The enclosing box of the rotated corners; the empty box stays empty.</returns>
    public SceneBounds Rotate(double degrees)
    {
        if (IsEmpty || degrees == 0)
        {
            return this;
        }

        var radians = degrees * Math.PI / 180;
        var cos = Snap(Math.Cos(radians));
        var sin = Snap(Math.Sin(radians));
        var result = Empty;
        foreach (var (x, y) in new[] { (X1, Y1), (X2, Y1), (X2, Y2), (X1, Y2) })
        {
            var rx = (x * cos) - (y * sin);
            var ry = (x * sin) + (y * cos);
            result = result.Union(new SceneBounds(rx, ry, rx, ry));
        }

        return result;
    }

    /// <summary>
    ///     Removes the floating-point noise around 0 and 1 of a sine or cosine.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The snapped value.</returns>
    private static double Snap(double value)
    {
        if (Math.Abs(value) < 1e-12)
        {
            return 0;
        }

        return Math.Abs(Math.Abs(value) - 1) < 1e-12 ? Math.Sign(value) : value;
    }
}
