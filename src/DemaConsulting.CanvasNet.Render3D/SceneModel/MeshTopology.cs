// cspell:ignore Orientable

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     Internal, allocation-light topology and measure helpers shared by <see cref="Mesh"/>.
/// </summary>
/// <remarks>
///     Kept separate from <see cref="Mesh"/> so the closed-surface test and the divergence-theorem
///     volume can be verified in isolation on raw index/vertex spans. All members are pure and
///     thread-safe.
/// </remarks>
internal static class MeshTopology
{
    /// <summary>
    ///     Determines whether the triangle index list describes a closed, consistently oriented
    ///     surface: every directed edge <c>a -&gt; b</c> occurs exactly once and its reverse
    ///     <c>b -&gt; a</c> also occurs exactly once.
    /// </summary>
    /// <remarks>
    ///     This single rule rejects open edges (missing reverse), inconsistently wound neighbors
    ///     (the same directed edge twice), edges shared by more than two triangles (some direction
    ///     repeats) and degenerate triangles with a repeated index. Multi-shell meshes (for example
    ///     two disjoint cubes) are closed. The check sorts one <see cref="long"/> key per directed
    ///     edge, so it is O(E log E) with a single allocation and no per-edge objects.
    /// </remarks>
    /// <param name="indices">The triangle index list (three indices per triangle, in range).</param>
    /// <returns>
    ///     <see langword="true"/> when the surface is closed and consistently oriented; an empty
    ///     index list is not closed (it has no surface).
    /// </returns>
    public static bool IsClosedOrientable(ReadOnlySpan<int> indices)
    {
        // A mesh without triangles has no surface to be closed
        if (indices.Length == 0)
        {
            return false;
        }

        // Build one sortable key per directed edge (a -> b), rejecting degenerate edges outright
        var keys = new long[indices.Length];
        for (var t = 0; t < indices.Length; t += 3)
        {
            for (var e = 0; e < 3; e++)
            {
                var a = indices[t + e];
                var b = indices[t + ((e + 1) % 3)];
                if (a == b)
                {
                    return false;
                }

                keys[t + e] = EdgeKey(a, b);
            }
        }

        Array.Sort(keys);

        // A repeated directed edge means inconsistent winding or an edge shared by 3+ triangles
        for (var i = 1; i < keys.Length; i++)
        {
            if (keys[i] == keys[i - 1])
            {
                return false;
            }
        }

        // Every directed edge must have its opposite directed edge present
        for (var t = 0; t < indices.Length; t += 3)
        {
            for (var e = 0; e < 3; e++)
            {
                var a = indices[t + e];
                var b = indices[t + ((e + 1) % 3)];
                if (Array.BinarySearch(keys, EdgeKey(b, a)) < 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Computes the signed volume enclosed by a triangle mesh with the divergence theorem,
    ///     measured relative to <paramref name="reference"/> so that meshes far from the origin
    ///     keep their precision.
    /// </summary>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="indices">The triangle index list.</param>
    /// <param name="reference">A reference point near the mesh (typically its bounds center).</param>
    /// <returns>The signed volume; positive for an outward counter-clockwise closed mesh.</returns>
    public static double SignedVolume(ReadOnlySpan<Vector3D> vertices, ReadOnlySpan<int> indices, Vector3D reference)
    {
        var sum = 0.0;
        for (var t = 0; t < indices.Length; t += 3)
        {
            // Signed volume of the tetrahedron (reference, a, b, c) = a . (b x c) / 6
            var a = vertices[indices[t]] - reference;
            var b = vertices[indices[t + 1]] - reference;
            var c = vertices[indices[t + 2]] - reference;
            sum += Vector3D.Dot(a, Vector3D.Cross(b, c));
        }

        return sum / 6.0;
    }

    /// <summary>Computes the total area of all triangles.</summary>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="indices">The triangle index list.</param>
    /// <returns>The surface area (sum of triangle areas).</returns>
    public static double SurfaceArea(ReadOnlySpan<Vector3D> vertices, ReadOnlySpan<int> indices)
    {
        var sum = 0.0;
        for (var t = 0; t < indices.Length; t += 3)
        {
            var a = vertices[indices[t]];
            var edge1 = vertices[indices[t + 1]] - a;
            var edge2 = vertices[indices[t + 2]] - a;
            sum += Vector3D.Cross(edge1, edge2).Length * 0.5;
        }

        return sum;
    }

    /// <summary>Packs a directed edge into a single sortable key.</summary>
    /// <param name="from">The start vertex index.</param>
    /// <param name="to">The end vertex index.</param>
    /// <returns>A 64-bit key unique to the ordered pair.</returns>
    private static long EdgeKey(int from, int to) => ((long)from << 32) | (uint)to;
}
