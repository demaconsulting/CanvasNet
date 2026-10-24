namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     A view-space vertex used during clipping (double precision, promoted exactly from the narrowed single-precision positions).
/// </summary>
/// <remarks>View space is right-handed with the camera looking down -Z; the view depth of a vertex is <c>-Z</c>.</remarks>
/// <param name="X">The view-space X (right).</param>
/// <param name="Y">The view-space Y (up).</param>
/// <param name="Z">The view-space Z (negative in front of the camera).</param>
internal readonly record struct ClipVertex(double X, double Y, double Z)
{
    /// <summary>Gets the view depth (distance along the view direction), <c>-Z</c>.</summary>
    public double Depth => -Z;

    /// <summary>
    ///     Determines whether this vertex sorts before <paramref name="other"/> in the canonical
    ///     (lexicographic X, Y, Z) order that makes clipped-edge intersections independent of the
    ///     direction in which an edge is traversed.
    /// </summary>
    /// <param name="other">The vertex to compare against.</param>
    /// <returns><see langword="true"/> when this vertex is canonically first.</returns>
    public bool IsBefore(in ClipVertex other)
    {
        // Compare with ordered inequalities only (no floating-point equality tests)
        if (X < other.X)
        {
            return true;
        }

        if (X > other.X)
        {
            return false;
        }

        if (Y < other.Y)
        {
            return true;
        }

        if (Y > other.Y)
        {
            return false;
        }

        return Z < other.Z;
    }
}
