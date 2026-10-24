namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Describes one normalized contour of a <see cref="PolygonRegion"/> inside the region's flattened
///     vertex list.
/// </summary>
/// <remarks>
///     Internal bookkeeping shared by <see cref="PolygonTriangulator"/> and the extrusion code so they
///     never have to recompute the nesting of contours. Immutable and thread-safe.
/// </remarks>
/// <param name="Start">Index of the contour's first vertex in <see cref="PolygonRegion.Vertices"/>.</param>
/// <param name="Count">Number of vertices of the contour.</param>
/// <param name="Depth">
///     Number of other contours that contain this contour. An even depth is a solid boundary (counter-clockwise),
///     an odd depth a hole (clockwise).
/// </param>
/// <param name="Parent">
///     Index of the innermost contour that contains this contour, or <c>-1</c> for a contour at depth zero.
/// </param>
internal readonly record struct ContourInfo(int Start, int Count, int Depth, int Parent)
{
    /// <summary>Gets a value indicating whether the contour bounds solid material (even nesting depth).</summary>
    public bool IsSolid => (Depth & 1) == 0;
}
