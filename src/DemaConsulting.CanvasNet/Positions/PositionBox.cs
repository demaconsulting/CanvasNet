namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     The geometry box of a feature in the pixels of a rendered picture.
/// </summary>
/// <remarks>
///     The box is the bounds of the feature's geometry, the way SVG <c>getBBox</c> measures it: the stroke is not
///     included, so a stroked outline extends about half its stroke width beyond the box. The origin is the top-left
///     corner of the picture, x grows to the right and y grows downward. A vertex of a line has a box of zero size.
///     The value is immutable.
/// </remarks>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width; a valid box has a width that is not negative.</param>
/// <param name="Height">The height; a valid box has a height that is not negative.</param>
public readonly record struct PositionBox(double X, double Y, double Width, double Height)
{
    /// <summary>
    ///     Gets the center of the box.
    /// </summary>
    public PositionPoint Center => new(X + (Width / 2), Y + (Height / 2));

    /// <summary>
    ///     Tells whether every coordinate is finite and the size is not negative.
    /// </summary>
    /// <returns><see langword="true"/> when the box can be reported.</returns>
    public bool IsValid()
    {
        return double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height) &&
               Width >= 0 && Height >= 0;
    }

    /// <summary>
    ///     Gets the four corners of the box, clockwise from the top-left corner.
    /// </summary>
    /// <remarks>
    ///     A transform that is not a pure translation and scale (a rotation) is applied to the corners, and the
    ///     transformed corners are enclosed with <see cref="Enclose(IReadOnlyList{PositionPoint})"/>.
    /// </remarks>
    /// <returns>The corners: top-left, top-right, bottom-right, bottom-left.</returns>
    public PositionPoint[] Corners()
    {
        return
        [
            new PositionPoint(X, Y),
            new PositionPoint(X + Width, Y),
            new PositionPoint(X + Width, Y + Height),
            new PositionPoint(X, Y + Height)
        ];
    }

    /// <summary>
    ///     Gets the smallest box that encloses a set of points.
    /// </summary>
    /// <remarks>
    ///     Used to enclose the corners of a box after a transform (a rotation turns the box, and the enclosing box is
    ///     what can be reported).
    /// </remarks>
    /// <param name="points">The points; at least one.</param>
    /// <returns>The enclosing box.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="points"/> is empty.</exception>
    public static PositionBox Enclose(IReadOnlyList<PositionPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            throw new ArgumentException("At least one point is needed.", nameof(points));
        }

        // Track the extremes over every point
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var point in points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return new PositionBox(minX, minY, maxX - minX, maxY - minY);
    }
}
