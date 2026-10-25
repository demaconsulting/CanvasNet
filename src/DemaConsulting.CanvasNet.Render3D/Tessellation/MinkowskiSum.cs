// cspell:ignore Minkowski convexity welded coplanar

using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     The Minkowski sum of two convex solids, computed as the exact convex hull of all pairwise vertex
///     sums, with a sound test that rejects non-convex operands instead of approximating them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Method.</b> For convex solids <c>A</c> and <c>B</c> the sum <c>A + B</c> is the convex hull of
///         the points <c>a + b</c> over all vertices <c>a</c> of <c>A</c> and <c>b</c> of <c>B</c>, which
///         <see cref="ConvexHull.Hull3D"/> computes exactly. Each pairwise sum is a single double addition,
///         so the result is the exact hull of the <em>rounded</em> sums (an error of at most one unit in the
///         last place per coordinate). Addition is commutative in IEEE arithmetic, and the hull depends only on
///         the point set, so <c>ConvexSum(a, b)</c> and <c>ConvexSum(b, a)</c> are byte-identical.
///     </para>
///     <para>
///         <b>Convexity test, its soundness and its tolerance.</b> The hull construction is only correct for
///         convex operands, so each operand is verified. The exact structural preconditions are that the
///         mesh is a closed, edge-manifold, consistently wound surface without degenerate triangles, is not
///         inverted, forms one connected shell and has a positive volume. The geometric test compares the
///         solid with its own convex hull: the hull of the operand's vertices contains the operand, so the
///         two are equal exactly when the operand is convex, and the operand is accepted only if
///         <c>|Vol(M) - Vol(H)| &lt;= tau * Vol(H) + e(M) + e(H)</c> and
///         <c>|Area(M) - Area(H)| &lt;= tau * Area(H)</c>, where <c>tau</c> is
///         <see cref="ConvexityTolerance"/> (1e-9, relative) and <c>e</c> is the rigorous rounding-error bound
///         of the volume computation. Any dent, notch, second shell, chord-triangulation of vertices in convex
///         position or multiply wrapped surface changes the volume or the area by far more than that, while
///         the rounding-level non-planarity of rotated cubes, cylinder caps and sphere quads (about 1e-16
///         relative) passes. <b>Tolerance statement:</b> an operand is accepted only if its solid differs from
///         its convex hull by at most 1e-9 of the hull volume and area; no macroscopic non-convexity can
///         pass, and the sum is computed from the hull of the operand, so it is exact for the hull. A local
///         (per-edge) tolerance was rejected because it is unsound: a gently concave bowl of many facets passes
///         any per-edge height tolerance although it is not convex. Models with a huge offset relative to their
///         size (more than about 1e6 times the feature size) can fail the test through coordinate rounding and
///         are then reported, never approximated.
///     </para>
///     <para>
///         <b>Work and memory.</b> The sum generates <c>Va * Vb</c> points and hulls them: O(Va * Vb log(Va * Vb))
///         time and O(Va * Vb) memory, bounded by <see cref="MaxPairwiseSums"/>; each operand's convexity test is
///         one hull of its own vertices. The array of sums is the only copy of the point set (the hull sorts and
///         de-duplicates it in place), so at the bound it is 2^21 points of 24 bytes (about 50 MB) and the peak is
///         about 70 MB: that array plus about 9 bytes per point of construction tables (about 20 MB), plus face
///         tables proportional to the size of the hull (about 70 bytes per hull vertex), which is far smaller
///         than the point set for typical operands. The work polls the cancellation token.
///     </para>
///     <para>
///         <b>Failure contract.</b> A null operand throws <see cref="ArgumentNullException"/>. An operand
///         without triangles, without volume (no four affinely independent vertices) or a vertex product above
///         <see cref="MaxPairwiseSums"/> throws <see cref="ArgumentException"/>. An operand that is not provably a
///         closed convex solid (open, non-manifold, inconsistently wound, inverted, several shells, not convex
///         within the tolerance) throws <see cref="NotSupportedException"/>; the message gives the reason.
///         Cancellation throws <see cref="OperationCanceledException"/>. Members are stateless and thread-safe.
///     </para>
/// </remarks>
public static class MinkowskiSum
{
    /// <summary>
    ///     The largest allowed product of the vertex counts of the two operands (2^21), the documented bound
    ///     of the work and memory of <see cref="ConvexSum"/>.
    /// </summary>
    public const int MaxPairwiseSums = 2_097_152;

    /// <summary>
    ///     The relative tolerance of the convexity test: an operand may differ from its convex hull by at most
    ///     this fraction of the hull volume and surface area.
    /// </summary>
    internal const double ConvexityTolerance = 1e-9;

    /// <summary>Number of pairwise sums between two cancellation polls.</summary>
    private const int PollInterval = 4096;

    /// <summary>
    ///     Computes the Minkowski sum of two convex solid meshes.
    /// </summary>
    /// <remarks>
    ///     The result is the closed, outward-wound, positive-volume convex hull of every pairwise vertex sum, a
    ///     valid constructive-solid-geometry operand that depends only on the vertex sets of the operands (see the
    ///     type remarks for exactness, the convexity tolerance and the work bound).
    /// </remarks>
    /// <param name="a">The first convex solid.</param>
    /// <param name="b">The second convex solid.</param>
    /// <param name="cancellationToken">A token that cancels the computation.</param>
    /// <returns>The mesh of the Minkowski sum.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an operand is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when an operand has no triangles or no volume, or when the vertex counts multiply to more than
    ///     <see cref="MaxPairwiseSums"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">Thrown when an operand is not provably a closed convex solid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Mesh ConvexSum(Mesh a, Mesh b, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        cancellationToken.ThrowIfCancellationRequested();

        // Distinct vertex positions and the work bound, both before any large allocation
        var weldedA = DistinctVertices(a, nameof(a), out var verticesA);
        cancellationToken.ThrowIfCancellationRequested();
        var weldedB = DistinctVertices(b, nameof(b), out var verticesB);
        cancellationToken.ThrowIfCancellationRequested();
        if ((long)verticesA.Length * verticesB.Length > MaxPairwiseSums)
        {
            throw new ArgumentException(
                $"The operands have {verticesA.Length} and {verticesB.Length} vertices; their product exceeds the limit of {MaxPairwiseSums} pairwise sums.");
        }

        // Only convex operands have a Minkowski sum equal to the hull of the vertex sums
        RequireConvex(a, weldedA, verticesA, nameof(a), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        RequireConvex(b, weldedB, verticesB, nameof(b), cancellationToken);

        return PairwiseHull(verticesA, verticesB, cancellationToken);
    }

    /// <summary>
    ///     Verifies once that an operand is a closed convex solid, so that <see cref="ConvexSumOfVerified"/> can
    ///     skip the check for every sum the operand takes part in.
    /// </summary>
    /// <remarks>
    ///     The checks and the exceptions are those of <see cref="ConvexSum"/> for one operand, including the
    ///     tolerance; the cost is one convex hull of the operand's own vertices.
    /// </remarks>
    /// <param name="mesh">The operand.</param>
    /// <param name="name">The operand name used in the exception messages.</param>
    /// <param name="cancellationToken">A token that cancels the test.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the operand has no triangles or no volume.</exception>
    /// <exception cref="NotSupportedException">Thrown when the operand is not provably a closed convex solid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    internal static void RequireConvexSolid(Mesh mesh, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        cancellationToken.ThrowIfCancellationRequested();
        var welded = DistinctVertices(mesh, name, out var vertices);
        cancellationToken.ThrowIfCancellationRequested();
        RequireConvex(mesh, welded, vertices, name, cancellationToken);
    }

    /// <summary>
    ///     Computes the Minkowski sum of two operands that the caller has already verified, without verifying
    ///     them again.
    /// </summary>
    /// <remarks>
    ///     Verified operands are those that passed <see cref="RequireConvexSolid"/> or that are the output of
    ///     <see cref="ConvexHull.Hull3D"/> or of a previous sum (convex by construction). The result is the same as
    ///     <see cref="ConvexSum"/> gives; only the repeated convexity test is skipped. The work bound still applies.
    /// </remarks>
    /// <param name="a">The first operand, known to be a closed convex solid.</param>
    /// <param name="b">The second operand, known to be a closed convex solid.</param>
    /// <param name="cancellationToken">A token that cancels the computation.</param>
    /// <returns>The mesh of the Minkowski sum.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an operand is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an operand has no triangles or the vertex counts multiply to more than <see cref="MaxPairwiseSums"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    internal static Mesh ConvexSumOfVerified(Mesh a, Mesh b, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        cancellationToken.ThrowIfCancellationRequested();
        _ = DistinctVertices(a, nameof(a), out var verticesA);
        cancellationToken.ThrowIfCancellationRequested();
        _ = DistinctVertices(b, nameof(b), out var verticesB);
        cancellationToken.ThrowIfCancellationRequested();
        if ((long)verticesA.Length * verticesB.Length > MaxPairwiseSums)
        {
            throw new ArgumentException(
                $"The operands have {verticesA.Length} and {verticesB.Length} vertices; their product exceeds the limit of {MaxPairwiseSums} pairwise sums.");
        }

        return PairwiseHull(verticesA, verticesB, cancellationToken);
    }

    /// <summary>
    ///     Computes the hull of all pairwise sums of two vertex sets.
    /// </summary>
    /// <remarks>
    ///     The sums array is the only copy of the point set: it is handed to the hull, which sorts and
    ///     de-duplicates it in place.
    /// </remarks>
    /// <param name="verticesA">The distinct vertices of the first operand.</param>
    /// <param name="verticesB">The distinct vertices of the second operand.</param>
    /// <param name="cancellationToken">A token that cancels the computation.</param>
    /// <returns>The hull mesh.</returns>
    private static Mesh PairwiseHull(Vector3D[] verticesA, Vector3D[] verticesB, CancellationToken cancellationToken)
    {
        var sums = new Vector3D[verticesA.Length * verticesB.Length];
        var count = 0;
        var sincePoll = 0;
        foreach (var left in verticesA)
        {
            foreach (var right in verticesB)
            {
                sums[count++] = left + right;
            }

            sincePoll += verticesB.Length;
            if (sincePoll >= PollInterval)
            {
                sincePoll = 0;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return ConvexHull.Hull3DInPlace(sums, count, cancellationToken);
    }

    /// <summary>
    ///     Collects the distinct positions of the vertices that a mesh's triangles reference.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="name">The parameter name for exceptions.</param>
    /// <param name="vertices">Receives the distinct referenced vertex positions in index order.</param>
    /// <returns>The triangle indices with coincident vertices welded.</returns>
    /// <exception cref="ArgumentException">Thrown when the mesh has no triangles.</exception>
    private static int[] DistinctVertices(Mesh mesh, string name, out Vector3D[] vertices)
    {
        if (mesh.TriangleCount == 0)
        {
            throw new ArgumentException("The operand has no triangles.", name);
        }

        var welded = MeshTopology.Weld(mesh.VertexSpan, mesh.IndexSpan.ToArray());
        var used = new bool[mesh.VertexCount];
        var distinct = 0;
        foreach (var index in welded)
        {
            if (!used[index])
            {
                used[index] = true;
                distinct++;
            }
        }

        vertices = new Vector3D[distinct];
        var next = 0;
        for (var i = 0; i < used.Length; i++)
        {
            if (used[i])
            {
                vertices[next++] = mesh.VertexSpan[i];
            }
        }

        return welded;
    }

    /// <summary>
    ///     Verifies that an operand is a closed convex solid (see the type remarks for the criterion).
    /// </summary>
    /// <param name="mesh">The operand.</param>
    /// <param name="welded">The operand's welded triangle indices.</param>
    /// <param name="vertices">The distinct referenced vertex positions of the operand.</param>
    /// <param name="name">The parameter name for exceptions.</param>
    /// <param name="cancellationToken">A token that cancels the test.</param>
    /// <exception cref="ArgumentException">Thrown when the operand spans no volume.</exception>
    /// <exception cref="NotSupportedException">Thrown when the operand is not provably a closed convex solid.</exception>
    private static void RequireConvex(Mesh mesh, int[] welded, Vector3D[] vertices, string name, CancellationToken cancellationToken)
    {
        // Exact structural preconditions
        var defects = mesh.OperandDefects;
        if (defects != MeshTopologyDefects.None)
        {
            throw new NotSupportedException(
                $"The operand '{name}' is not a closed solid ({DescribeDefects(defects)}); a Minkowski sum is only computed for convex solids.");
        }

        if (mesh.IsInverted)
        {
            throw new NotSupportedException($"The operand '{name}' is inside out (its triangles face inward).");
        }

        if (CountShells(welded, mesh.VertexCount) != 1)
        {
            throw new NotSupportedException($"The operand '{name}' consists of several shells and is therefore not convex.");
        }

        // The hull of the operand's own vertices: the operand is convex exactly when it fills that hull
        Mesh hull;
        try
        {
            hull = ConvexHull.Hull3D(vertices, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException($"The operand '{name}' has no volume (its vertices are collinear, coplanar or too few).", name, exception);
        }

        var volume = MeshTopology.SignedVolume(mesh.VertexSpan, mesh.IndexSpan, mesh.Bounds.Center, out var volumeError);
        var hullVolume = MeshTopology.SignedVolume(hull.VertexSpan, hull.IndexSpan, hull.Bounds.Center, out var hullError);
        var area = MeshTopology.SurfaceArea(mesh.VertexSpan, mesh.IndexSpan);
        var hullArea = MeshTopology.SurfaceArea(hull.VertexSpan, hull.IndexSpan);

        // A measurement that overflows (coordinates beyond about 1e102) is NaN or infinite, and every comparison with
        // NaN is false, so it must be rejected explicitly instead of passing the tolerance test
        if (!double.IsFinite(volume) || !double.IsFinite(volumeError) || !double.IsFinite(hullVolume)
            || !double.IsFinite(hullError) || !double.IsFinite(area) || !double.IsFinite(hullArea))
        {
            throw new NotSupportedException(
                $"The operand '{name}' is too large to measure (its volume or area is not a finite number), so its convexity cannot be verified.");
        }

        if (!(volume > volumeError))
        {
            throw new NotSupportedException($"The operand '{name}' has no determinable positive volume.");
        }

        var volumeLimit = (ConvexityTolerance * hullVolume) + volumeError + hullError;
        var volumeFits = Math.Abs(volume - hullVolume) <= volumeLimit;
        var areaFits = Math.Abs(area - hullArea) <= ConvexityTolerance * hullArea;
        if (!(volumeFits && areaFits))
        {
            throw new NotSupportedException(
                $"The operand '{name}' is not convex: it differs from its convex hull by more than {ConvexityTolerance:G} of the hull volume or area, so it is not approximated.");
        }
    }

    /// <summary>
    ///     Counts the connected shells of a welded index list (triangles sharing a vertex index are connected).
    /// </summary>
    /// <param name="welded">The welded triangle indices.</param>
    /// <param name="vertexCount">The number of vertex slots.</param>
    /// <returns>The number of connected components among the referenced vertices.</returns>
    private static int CountShells(int[] welded, int vertexCount)
    {
        var parent = new int[vertexCount];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        for (var t = 0; t + 2 < welded.Length; t += 3)
        {
            Join(parent, welded[t], welded[t + 1]);
            Join(parent, welded[t + 1], welded[t + 2]);
        }

        var roots = new HashSet<int>();
        foreach (var index in welded)
        {
            roots.Add(Find(parent, index));
        }

        return roots.Count;
    }

    /// <summary>Finds the union-find root of an index, halving the path as it goes.</summary>
    /// <param name="parent">The union-find parent table.</param>
    /// <param name="index">The index.</param>
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

    /// <summary>Joins the sets of two indices.</summary>
    /// <param name="parent">The union-find parent table.</param>
    /// <param name="first">The first index.</param>
    /// <param name="second">The second index.</param>
    private static void Join(int[] parent, int first, int second)
    {
        var a = Find(parent, first);
        var b = Find(parent, second);
        if (a != b)
        {
            parent[Math.Max(a, b)] = Math.Min(a, b);
        }
    }

    /// <summary>Describes a set of topology defects in words for an exception message.</summary>
    /// <param name="defects">The defects (not <see cref="MeshTopologyDefects.None"/>).</param>
    /// <returns>A comma-separated description.</returns>
    private static string DescribeDefects(MeshTopologyDefects defects)
    {
        var reasons = new List<string>();
        if ((defects & MeshTopologyDefects.BoundaryEdge) != 0)
        {
            reasons.Add("open: it has boundary edges");
        }

        if ((defects & MeshTopologyDefects.NonManifoldEdge) != 0)
        {
            reasons.Add("non-manifold: an edge is shared by more than two triangles");
        }

        if ((defects & MeshTopologyDefects.InconsistentWinding) != 0)
        {
            reasons.Add("its triangles are inconsistently wound");
        }

        if ((defects & MeshTopologyDefects.DegenerateTriangle) != 0)
        {
            reasons.Add("it has degenerate triangles");
        }

        return string.Join("; ", reasons);
    }
}
