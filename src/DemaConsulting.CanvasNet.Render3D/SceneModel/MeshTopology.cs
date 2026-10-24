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
    /// <summary>The machine epsilon (2^-52) used by the volume error bound.</summary>
    private const double MachineEpsilon = 2.220446049250313e-16;

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
    ///     edge, so it is O(E log E) with two array allocations and no per-edge objects. It is
    ///     expressed through <see cref="Analyze(ReadOnlySpan{int})"/>: closed means "no defect".
    /// </remarks>
    /// <param name="indices">The triangle index list (three indices per triangle, in range).</param>
    /// <returns>
    ///     <see langword="true"/> when the surface is closed and consistently oriented; an empty
    ///     index list is not closed (it has no surface).
    /// </returns>
    public static bool IsClosedOrientable(ReadOnlySpan<int> indices)
    {
        // A mesh without triangles has no surface to be closed; otherwise closed means defect-free
        return indices.Length > 0 && Analyze(indices) == MeshTopologyDefects.None;
    }

    /// <summary>
    ///     Classifies every topology defect of a triangle index list: boundary edges, edges shared
    ///     by three or more triangles, inconsistently wound neighbors and degenerate triangles.
    /// </summary>
    /// <remarks>
    ///     Each undirected edge is counted by direction (<c>n_ab</c> triangles traverse it low to
    ///     high, <c>n_ba</c> high to low). One traversal in total is a boundary edge; exactly one in
    ///     each direction is correct; two in the same direction is inconsistent winding; three or
    ///     more is non-manifold. A triangle that repeats an index is reported as degenerate and its
    ///     edges are ignored, so one collapsed triangle does not also masquerade as edge defects.
    ///     The analysis sorts one <see cref="long"/> key per edge, so it is O(E log E) with two
    ///     allocations. Multi-shell meshes (for example two disjoint cubes) are defect-free.
    /// </remarks>
    /// <param name="indices">The triangle index list (three indices per triangle, in range).</param>
    /// <returns>The set of defects found; <see cref="MeshTopologyDefects.None"/> for a closed, consistently oriented surface or an empty list.</returns>
    public static MeshTopologyDefects Analyze(ReadOnlySpan<int> indices)
    {
        var defects = MeshTopologyDefects.None;

        // One key per undirected edge (low, high) with a parallel direction flag (1 = low->high)
        var keys = new long[indices.Length];
        var directions = new byte[indices.Length];
        var count = 0;
        for (var t = 0; t + 2 < indices.Length; t += 3)
        {
            var a0 = indices[t];
            var a1 = indices[t + 1];
            var a2 = indices[t + 2];
            if (a0 == a1 || a1 == a2 || a2 == a0)
            {
                defects |= MeshTopologyDefects.DegenerateTriangle;
                continue;
            }

            count = AddEdge(keys, directions, count, a0, a1);
            count = AddEdge(keys, directions, count, a1, a2);
            count = AddEdge(keys, directions, count, a2, a0);
        }

        Array.Sort(keys, directions, 0, count);

        // Walk each run of equal keys (one undirected edge) and count the traversals per direction
        var i = 0;
        while (i < count)
        {
            var forward = 0;
            var backward = 0;
            var key = keys[i];
            while (i < count && keys[i] == key)
            {
                if (directions[i] == 1)
                {
                    forward++;
                }
                else
                {
                    backward++;
                }

                i++;
            }

            var total = forward + backward;
            if (total == 1)
            {
                defects |= MeshTopologyDefects.BoundaryEdge;
            }
            else if (total >= 3)
            {
                defects |= MeshTopologyDefects.NonManifoldEdge;
            }
            else if (forward != 1)
            {
                // Two traversals in the same direction
                defects |= MeshTopologyDefects.InconsistentWinding;
            }
        }

        return defects;
    }

    /// <summary>
    ///     Classifies every defect of a mesh, including geometrically degenerate triangles: the topology
    ///     defects of <see cref="Analyze(ReadOnlySpan{int})"/> plus <see cref="MeshTopologyDefects.DegenerateTriangle"/>
    ///     for every triangle with distinct indices whose vertices are coincident or collinear.
    /// </summary>
    /// <remarks>
    ///     The renderer silently discards a triangle that has no face normal (see <see cref="TryGetFaceNormal"/>),
    ///     which removes a crossing from a constructive-solid-geometry operand; this overload reports exactly the
    ///     triangles that criterion rejects. The edges of such a triangle stay in the edge table, so they never
    ///     masquerade as boundary defects of their neighbors.
    /// </remarks>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="indices">The triangle index list (three indices per triangle, in range of <paramref name="vertices"/>).</param>
    /// <returns>The set of defects found; <see cref="MeshTopologyDefects.None"/> for a closed, consistently oriented surface without degenerate triangles.</returns>
    public static MeshTopologyDefects Analyze(ReadOnlySpan<Vector3D> vertices, ReadOnlySpan<int> indices)
    {
        var defects = Analyze(indices);
        if ((defects & MeshTopologyDefects.DegenerateTriangle) != 0)
        {
            return defects;
        }

        for (var t = 0; t + 2 < indices.Length; t += 3)
        {
            if (!TryGetFaceNormal(vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]], out _))
            {
                return defects | MeshTopologyDefects.DegenerateTriangle;
            }
        }

        return defects;
    }

    /// <summary>
    ///     Computes the unit face normal of a triangle, or reports that it has none. This is the single
    ///     criterion by which the renderer discards a triangle and by which operand validation reports one.
    /// </summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    /// <param name="normal">Receives the unit normal (the right-hand-rule direction of <c>(b - a) x (c - a)</c>), or zero when there is none.</param>
    /// <returns><see langword="true"/> when the triangle has a finite, non-zero normal; <see langword="false"/> for a zero-area (coincident or collinear) or overflowed triangle.</returns>
    public static bool TryGetFaceNormal(Vector3D a, Vector3D b, Vector3D c, out Vector3D normal)
    {
        // Normalize scales by the largest component first, so only an exactly zero cross product has no direction
        normal = Vector3D.Cross(b - a, c - a).Normalize();
        return normal.LengthSquared > 0.0 && normal.IsFinite;
    }

    /// <summary>Appends one traversal of the directed edge <c>from -&gt; to</c> to the edge table.</summary>
    /// <param name="keys">The undirected edge keys.</param>
    /// <param name="directions">The parallel direction flags (1 when the edge runs from the lower to the higher index).</param>
    /// <param name="count">The number of entries already used.</param>
    /// <param name="first">The start vertex index.</param>
    /// <param name="second">The end vertex index.</param>
    /// <returns>The new number of used entries.</returns>
    private static int AddEdge(long[] keys, byte[] directions, int count, int first, int second)
    {
        var forward = first < second;
        keys[count] = forward ? EdgeKey(first, second) : EdgeKey(second, first);
        directions[count] = forward ? (byte)1 : (byte)0;
        return count + 1;
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
    public static double SignedVolume(ReadOnlySpan<Vector3D> vertices, ReadOnlySpan<int> indices, Vector3D reference) =>
        SignedVolume(vertices, indices, reference, out _);

    /// <summary>
    ///     Computes the signed enclosed volume together with a rigorous bound on its floating-point error.
    /// </summary>
    /// <remarks>
    ///     Each triangle contributes the triple product <c>a . (b x c)</c> of its reference-relative vertices.
    ///     Evaluating one triple product in double precision errs by at most a few units of machine epsilon
    ///     times the same product with every component replaced by its absolute value, and summing
    ///     <c>n</c> terms adds at most <c>n</c> epsilons of the sum of the absolute terms. The bound is therefore
    ///     <c>(n + 12) * epsilon * sum(|a| . (|b| x |c|))</c> (divided by six like the volume): it scales with
    ///     the mesh's own magnitude instead of a fixed fraction of its bounds, so a legitimately thin solid keeps
    ///     a volume far above its bound while a (near) zero-volume surface cannot be distinguished from noise.
    /// </remarks>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="indices">The triangle index list.</param>
    /// <param name="reference">A reference point near the mesh (typically its bounds center).</param>
    /// <param name="errorBound">Receives the largest absolute error the volume can have through rounding.</param>
    /// <returns>The signed volume; positive for an outward counter-clockwise closed mesh.</returns>
    public static double SignedVolume(ReadOnlySpan<Vector3D> vertices, ReadOnlySpan<int> indices, Vector3D reference, out double errorBound)
    {
        var sum = 0.0;
        var absolute = 0.0;
        for (var t = 0; t < indices.Length; t += 3)
        {
            // Signed volume of the tetrahedron (reference, a, b, c) = a . (b x c) / 6
            var a = vertices[indices[t]] - reference;
            var b = vertices[indices[t + 1]] - reference;
            var c = vertices[indices[t + 2]] - reference;
            sum += Vector3D.Dot(a, Vector3D.Cross(b, c));

            // The same product with absolute components bounds the rounding error of the term
            var ax = Math.Abs(a.X);
            var ay = Math.Abs(a.Y);
            var az = Math.Abs(a.Z);
            var bx = Math.Abs(b.X);
            var by = Math.Abs(b.Y);
            var bz = Math.Abs(b.Z);
            var cx = Math.Abs(c.X);
            var cy = Math.Abs(c.Y);
            var cz = Math.Abs(c.Z);
            absolute += (ax * ((by * cz) + (bz * cy))) + (ay * ((bz * cx) + (bx * cz))) + (az * ((bx * cy) + (by * cx)));
        }

        errorBound = ((indices.Length / 3) + 12) * MachineEpsilon * absolute / 6.0;
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

    /// <summary>Packs an ordered vertex pair into a single sortable key.</summary>
    /// <param name="from">The first vertex index.</param>
    /// <param name="to">The second vertex index.</param>
    /// <returns>A 64-bit key unique to the ordered pair.</returns>
    private static long EdgeKey(int from, int to) => ((long)from << 32) | (uint)to;
}
