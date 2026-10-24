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
    ///     Welds vertices that have exactly the same position: every index is replaced by the lowest
    ///     index of a vertex at that position, so geometrically coincident (for example STL-style,
    ///     one-vertex-per-corner) vertices become one topological vertex.
    /// </summary>
    /// <remarks>
    ///     The rule is exact component equality (negative and positive zero are equal), never a tolerance,
    ///     the same rule <see cref="MeshBuilder"/> uses. Topology, shell and orientation analysis of a mesh
    ///     work on the welded indices, because a closed surface that merely stores its corners several
    ///     times is geometrically closed and renders identically to a welded one.
    /// </remarks>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="indices">The triangle index list (in range of <paramref name="vertices"/>).</param>
    /// <returns>
    ///     <paramref name="indices"/> itself when no two vertices share a position (so no allocation is
    ///     retained for an already welded mesh); otherwise a new welded index list. The result must not be modified.
    /// </returns>
    public static int[] Weld(ReadOnlySpan<Vector3D> vertices, int[] indices)
    {
        ArgumentNullException.ThrowIfNull(indices);

        // Map each vertex to the first vertex at its exact position
        var first = new Dictionary<Vector3D, int>(vertices.Length);
        var canonical = new int[vertices.Length];
        var merged = false;
        for (var i = 0; i < vertices.Length; i++)
        {
            if (first.TryGetValue(vertices[i], out var existing))
            {
                canonical[i] = existing;
                merged = true;
            }
            else
            {
                first.Add(vertices[i], i);
                canonical[i] = i;
            }
        }

        if (!merged)
        {
            return indices;
        }

        var welded = new int[indices.Length];
        for (var i = 0; i < indices.Length; i++)
        {
            welded[i] = canonical[indices[i]];
        }

        return welded;
    }

    /// <summary>
    ///     Determines whether any connected shell of a welded closed mesh is wound against its nesting:
    ///     a shell enclosed by an even number of other shells (depth 0, 2, ...) must have a positive signed
    ///     volume, one enclosed by an odd number (a cavity wall, depth 1, 3, ...) a negative one.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The index list is split into connected shells (triangles that share a vertex index, found with
    ///         a union-find over the indices, so the indices must already be welded; see
    ///         <see cref="Weld"/>). Each shell's signed volume is measured relative to its own bounds center
    ///         with the rigorous rounding bound of <see cref="SignedVolume(ReadOnlySpan{Vector3D}, ReadOnlySpan{int}, Vector3D, out double)"/>;
    ///         a shell whose volume is within that bound has no determinable orientation and is ignored
    ///         (neither judged nor counted as enclosing anything).
    ///     </para>
    ///     <para>
    ///         The depth of a shell is the number of <em>other</em> shells that enclose one of its vertices,
    ///         decided by the parity of the crossings of a fixed, generic ray against each candidate shell
    ///         (orientation independent, so a wrongly wound enclosing shell still counts as enclosing).
    ///         Crossings use a consistent edge tie-break, so a ray through an edge shared by two triangles
    ///         crosses exactly one of them. A shell that is the only one needs no depth test. The cost is
    ///         O(shells x triangles), reduced by a bounds pre-test.
    ///     </para>
    ///     <para>
    ///         Valid hollow solids (outer shell outward, cavity walls inward, solids inside cavities outward
    ///         again) are therefore not reported, whereas a disjoint inside-out shell, which the whole-mesh
    ///         volume would hide behind a larger positive shell, is.
    ///     </para>
    /// </remarks>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="indices">The welded triangle index list; must describe a closed surface (no defects).</param>
    /// <returns><see langword="true"/> when some shell's orientation disagrees with its nesting depth parity.</returns>
    public static bool HasInvertedShell(ReadOnlySpan<Vector3D> vertices, ReadOnlySpan<int> indices)
    {
        var triangleCount = indices.Length / 3;
        if (triangleCount == 0)
        {
            return false;
        }

        // Split into connected shells by index connectivity
        var parent = new int[vertices.Length];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        for (var t = 0; t < indices.Length; t += 3)
        {
            Union(parent, indices[t], indices[t + 1]);
            Union(parent, indices[t + 1], indices[t + 2]);
        }

        var shellOfRoot = new int[vertices.Length];
        Array.Fill(shellOfRoot, -1);
        var triangleShell = new int[triangleCount];
        var shellCount = 0;
        for (var t = 0; t < triangleCount; t++)
        {
            var root = Find(parent, indices[3 * t]);
            if (shellOfRoot[root] < 0)
            {
                shellOfRoot[root] = shellCount++;
            }

            triangleShell[t] = shellOfRoot[root];
        }

        // Group the triangles by shell (counting sort) so each shell is a contiguous slice
        var offsets = new int[shellCount + 1];
        for (var t = 0; t < triangleCount; t++)
        {
            offsets[triangleShell[t] + 1]++;
        }

        for (var s = 0; s < shellCount; s++)
        {
            offsets[s + 1] += offsets[s];
        }

        var grouped = new int[indices.Length];
        var cursor = new int[shellCount];
        for (var t = 0; t < triangleCount; t++)
        {
            var shell = triangleShell[t];
            var target = 3 * (offsets[shell] + cursor[shell]++);
            grouped[target] = indices[3 * t];
            grouped[target + 1] = indices[(3 * t) + 1];
            grouped[target + 2] = indices[(3 * t) + 2];
        }

        // Orientation (+1 outward, -1 inward, 0 undeterminable) and bounds of every shell
        var orientation = new int[shellCount];
        var shellBounds = new BoundingBox3D[shellCount];
        for (var s = 0; s < shellCount; s++)
        {
            var slice = grouped.AsSpan(3 * offsets[s], 3 * (offsets[s + 1] - offsets[s]));
            var bounds = BoundingBox3D.Empty;
            foreach (var index in slice)
            {
                bounds = bounds.Union(vertices[index]);
            }

            shellBounds[s] = bounds;
            var volume = SignedVolume(vertices, slice, bounds.Center, out var errorBound);
            if (volume < -errorBound)
            {
                orientation[s] = -1;
            }
            else if (volume > errorBound)
            {
                orientation[s] = 1;
            }
        }

        // A lone shell has depth zero and must simply be outward
        if (shellCount == 1)
        {
            return orientation[0] < 0;
        }

        for (var s = 0; s < shellCount; s++)
        {
            if (orientation[s] == 0)
            {
                continue;
            }

            var probe = vertices[grouped[3 * offsets[s]]];
            var depth = 0;
            for (var r = 0; r < shellCount; r++)
            {
                if (r != s && orientation[r] != 0 && Contains(shellBounds[r], probe) &&
                    IsInsideShell(vertices, grouped.AsSpan(3 * offsets[r], 3 * (offsets[r + 1] - offsets[r])), probe))
                {
                    depth++;
                }
            }

            // Even depth must be outward (positive), odd depth must be a cavity wall (negative)
            if ((depth % 2 == 0) != (orientation[s] > 0))
            {
                return true;
            }
        }

        return false;
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

    /// <summary>The fixed, deliberately generic ray direction of the shell-enclosure test (no axis alignment, no symmetry).</summary>
    private static readonly Vector3D EnclosureRay = new(0.6180339887498949, 0.4142135623730951, 0.7320508075688772);

    /// <summary>Finds the union-find root of a vertex index, halving the path as it goes.</summary>
    /// <param name="parent">The union-find parent table.</param>
    /// <param name="index">The vertex index.</param>
    /// <returns>The root index.</returns>
    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    /// <summary>Joins the sets of two vertex indices.</summary>
    /// <param name="parent">The union-find parent table.</param>
    /// <param name="first">The first vertex index.</param>
    /// <param name="second">The second vertex index.</param>
    private static void Union(int[] parent, int first, int second)
    {
        var a = Find(parent, first);
        var b = Find(parent, second);
        if (a != b)
        {
            parent[Math.Max(a, b)] = Math.Min(a, b);
        }
    }

    /// <summary>Determines whether a non-empty box contains a point (boundary included).</summary>
    /// <param name="box">The box.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point lies within the box.</returns>
    private static bool Contains(BoundingBox3D box, Vector3D point) =>
        point.X >= box.Min.X && point.X <= box.Max.X &&
        point.Y >= box.Min.Y && point.Y <= box.Max.Y &&
        point.Z >= box.Min.Z && point.Z <= box.Max.Z;

    /// <summary>
    ///     Determines whether a point lies inside one closed shell: an odd number of its triangles is
    ///     crossed by the fixed ray from the point (orientation independent).
    /// </summary>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="shell">The welded triangle indices of the shell.</param>
    /// <param name="point">The point to classify.</param>
    /// <returns><see langword="true"/> when the point is inside the shell.</returns>
    private static bool IsInsideShell(ReadOnlySpan<Vector3D> vertices, ReadOnlySpan<int> shell, Vector3D point)
    {
        var inside = false;
        for (var t = 0; t < shell.Length; t += 3)
        {
            if (RayCrossesTriangle(point, vertices, shell[t], shell[t + 1], shell[t + 2]))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    ///     Determines whether the forward fixed ray from a point crosses a triangle. A ray through an edge
    ///     is resolved by the canonical direction of the edge's indices, so the two triangles sharing the
    ///     edge (which traverse it oppositely) yield exactly one crossing between them.
    /// </summary>
    /// <param name="point">The ray origin.</param>
    /// <param name="vertices">The vertex positions.</param>
    /// <param name="ia">The first vertex index.</param>
    /// <param name="ib">The second vertex index.</param>
    /// <param name="ic">The third vertex index.</param>
    /// <returns><see langword="true"/> when the ray crosses the triangle in front of the origin.</returns>
    private static bool RayCrossesTriangle(Vector3D point, ReadOnlySpan<Vector3D> vertices, int ia, int ib, int ic)
    {
        var a = vertices[ia];
        var b = vertices[ib];
        var c = vertices[ic];
        var pa = a - point;
        var pb = b - point;
        var pc = c - point;

        // The ray passes through the triangle exactly when the three edge sides agree
        var side = EdgeSide(pb, pc, ib, ic);
        if (EdgeSide(pc, pa, ic, ia) != side || EdgeSide(pa, pb, ia, ib) != side)
        {
            return false;
        }

        // Only crossings in front of the origin count
        var normal = Vector3D.Cross(b - a, c - a);
        var denominator = Vector3D.Dot(normal, EnclosureRay);
        return denominator != 0.0 && (Vector3D.Dot(normal, pa) / denominator) > 0.0;
    }

    /// <summary>Gets the side of the fixed ray relative to the edge <c>from -&gt; to</c> (offsets from the ray origin), breaking exact ties by index order.</summary>
    /// <param name="from">The edge start relative to the ray origin.</param>
    /// <param name="to">The edge end relative to the ray origin.</param>
    /// <param name="fromIndex">The start vertex index.</param>
    /// <param name="toIndex">The end vertex index.</param>
    /// <returns><see langword="true"/> for the positive side.</returns>
    private static bool EdgeSide(Vector3D from, Vector3D to, int fromIndex, int toIndex)
    {
        var value = Vector3D.Dot(EnclosureRay, Vector3D.Cross(from, to));
        return value > 0.0 || (value == 0.0 && fromIndex < toIndex);
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
