namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Homogeneous Sutherland-Hodgman polygon clipping against view-space planes, with
///     intersection points that are bit-identical for the two triangles sharing an edge.
/// </summary>
/// <remarks>
///     <para>
///         An intersection on edge <c>(a, b)</c> is computed with the endpoints first put into the
///         canonical lexicographic (X, Y, Z) order, so <c>Clip(a, b)</c> and <c>Clip(b, a)</c> yield
///         the same double-precision point. Two neighboring triangles that share an edge therefore
///         share the exact clipped end point and no crack opens along clipped edges.
///     </para>
///     <para>
///         The clipped polygon of a convex input stays convex and gains at most one vertex per
///         plane, so the caller's buffers need <c>input + 1</c> slots per plane. Stateless and
///         thread-safe.
///     </para>
/// </remarks>
internal static class TriangleClipper
{
    /// <summary>The largest polygon produced when a triangle is clipped by all five planes (near + four guard-band sides) with slack.</summary>
    public const int MaxVertices = 16;

    /// <summary>Creates the near plane that keeps vertices whose view depth is at least <paramref name="near"/>.</summary>
    /// <param name="near">The near distance (greater than zero).</param>
    /// <returns>The plane <c>-Z - near &gt;= 0</c>.</returns>
    public static ClipPlane NearPlane(double near) => new(0.0, 0.0, -1.0, -near);

    /// <summary>
    ///     Clips a convex polygon against one plane, keeping the non-negative side.
    /// </summary>
    /// <param name="polygon">The input polygon's vertices in order.</param>
    /// <param name="output">Receives the clipped polygon; must have room for <c>polygon.Length + 1</c> vertices.</param>
    /// <param name="plane">The clip plane.</param>
    /// <returns>The number of vertices written (0 when the polygon is entirely outside).</returns>
    public static int Clip(ReadOnlySpan<ClipVertex> polygon, Span<ClipVertex> output, in ClipPlane plane)
    {
        var count = 0;
        var n = polygon.Length;
        for (var i = 0; i < n; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % n];
            var currentInside = plane.Distance(current) >= 0.0;
            var nextInside = plane.Distance(next) >= 0.0;

            // Keep inside vertices; add the crossing point whenever the edge changes sides
            if (currentInside)
            {
                output[count++] = current;
            }

            if (currentInside != nextInside)
            {
                output[count++] = Intersect(current, next, plane);
            }
        }

        // Fewer than three vertices is not a polygon
        return count < 3 ? 0 : count;
    }

    /// <summary>
    ///     Intersects the edge <paramref name="a"/>-<paramref name="b"/> with the plane using
    ///     canonically ordered endpoints, making the result independent of the edge direction.
    /// </summary>
    /// <param name="a">The first endpoint.</param>
    /// <param name="b">The second endpoint.</param>
    /// <param name="plane">The plane that separates the endpoints.</param>
    /// <returns>The intersection point.</returns>
    private static ClipVertex Intersect(in ClipVertex a, in ClipVertex b, in ClipPlane plane)
    {
        // Canonical endpoint order: identical inputs in either direction give identical output
        var p = b.IsBefore(a) ? b : a;
        var q = b.IsBefore(a) ? a : b;
        var dp = plane.Distance(p);
        var dq = plane.Distance(q);
        var t = dp / (dp - dq);
        return new ClipVertex(
            p.X + ((q.X - p.X) * t),
            p.Y + ((q.Y - p.Y) * t),
            p.Z + ((q.Z - p.Z) * t));
    }
}
