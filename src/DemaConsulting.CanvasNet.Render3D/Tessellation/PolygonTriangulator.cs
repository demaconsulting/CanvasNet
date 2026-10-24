using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Triangulates a <see cref="PolygonRegion"/> (outer boundaries, holes and islands) into
///     counter-clockwise triangles that use only the region's own vertices.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why.</b> Extruded caps and swept end caps must cover exactly the region, reuse the vertex indices
///         of the side walls (so the solid is watertight without T-junctions) and keep every collinear vertex.
///         The triangulator therefore adds no Steiner points and never drops or moves a vertex.
///     </para>
///     <para>
///         <b>Algorithm.</b> Each solid contour is merged with the holes it directly contains by zero-width
///         bridges to the nearest mutually visible vertex (rightmost hole vertex first), producing a weakly
///         simple ring, which is then ear-clipped. A vertex is an ear only when it is a strictly convex corner
///         and the closed ear triangle contains no other reflex or collinear vertex, so no zero-area triangle
///         is ever produced and a collinear vertex on a would-be diagonal blocks that diagonal. Every
///         decision uses the exact predicates of <see cref="PolygonPredicates"/>; there is no epsilon.
///         The result is deterministic. Worst-case time is quadratic in the number of vertices (a convex
///         ring is linear), which is adequate for CAD-sized profiles.
///     </para>
///     <para>
///         <b>Guarantees</b> for every valid region: the triangle count is exactly
///         <c>V + 2H - 2P</c> (<c>V</c> vertices, <c>H</c> holes, <c>P</c> solid contours); every triangle is
///         strictly counter-clockwise; every contour edge appears in exactly one triangle in the direction of
///         its contour and every other edge in one triangle per direction. The type is stateless and thread-safe.
///     </para>
/// </remarks>
public static class PolygonTriangulator
{
    /// <summary>
    ///     Triangulates a region.
    /// </summary>
    /// <param name="region">The region to triangulate; an empty region yields no triangles.</param>
    /// <returns>
    ///     Triangle corner indices into <see cref="PolygonRegion.Vertices"/>, three per triangle,
    ///     counter-clockwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown only if an internal invariant is violated (no ear or no visible bridge vertex exists), which
    ///     cannot happen for a region constructed through <see cref="PolygonRegion"/>.
    /// </exception>
    public static IReadOnlyList<int> Triangulate(PolygonRegion region) => Array.AsReadOnly(TriangulateToArray(region));

    /// <summary>
    ///     Triangulates a region into a new private array (no read-only wrapper), for callers inside the
    ///     assembly that map the indices immediately.
    /// </summary>
    /// <param name="region">The region to triangulate.</param>
    /// <returns>The triangle corner indices into <see cref="PolygonRegion.Vertices"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    internal static int[] TriangulateToArray(PolygonRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        var table = region.ContourTable;
        var points = region.VertexSpan.ToArray();

        // Group every hole under the solid contour that directly contains it
        var holesOf = new Dictionary<int, List<ContourInfo>>();
        for (var c = 0; c < table.Length; c++)
        {
            if (table[c].IsSolid)
            {
                continue;
            }

            if (!holesOf.TryGetValue(table[c].Parent, out var holes))
            {
                holes = [];
                holesOf.Add(table[c].Parent, holes);
            }

            holes.Add(table[c]);
        }

        // Each solid contour (outer boundary or island) is bridged with its holes and ear-clipped on its own
        var triangles = new List<int>();
        for (var c = 0; c < table.Length; c++)
        {
            if (!table[c].IsSolid)
            {
                continue;
            }

            var holes = holesOf.TryGetValue(c, out var own) ? own : [];
            var ring = PolygonBridger.Merge(points, table[c], holes);
            EarClipper.Clip(points, ring, triangles);
        }

        return [.. triangles];
    }
}

/// <summary>
///     Merges the holes of one solid contour into its boundary ring with zero-width bridges.
/// </summary>
/// <remarks>
///     The merged ring lists original vertex indices, so a bridge endpoint appears more than once and always
///     maps to the same index; the resulting ring is weakly simple (it touches itself only along the bridges).
///     Holes are processed by descending rightmost X so the +X ray argument guarantees a visible vertex.
///     Stateless and thread-safe.
/// </remarks>
internal static class PolygonBridger
{
    /// <summary>
    ///     Builds the merged ring of a solid contour and its direct holes.
    /// </summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="outer">The solid contour (counter-clockwise).</param>
    /// <param name="holes">The direct holes (clockwise); may be empty.</param>
    /// <returns>The weakly simple ring as vertex indices in counter-clockwise order.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no visible bridge vertex exists (internal invariant).</exception>
    public static List<int> Merge(Vector2D[] points, ContourInfo outer, IReadOnlyList<ContourInfo> holes)
    {
        var ring = new List<int>(outer.Count);
        for (var i = 0; i < outer.Count; i++)
        {
            ring.Add(outer.Start + i);
        }

        // Rightmost hole first: nothing unmerged lies to the right of its pivot vertex
        var order = holes
            .Select(hole => (Hole: hole, Pivot: RightmostOffset(points, hole)))
            .OrderByDescending(entry => points[entry.Hole.Start + entry.Pivot].X)
            .ToList();
        var pending = new List<ContourInfo>(holes);
        foreach (var (hole, pivot) in order)
        {
            var position = FindBridge(points, ring, hole, pivot, pending);

            // Insert P, M, hole..., M, P: walk the hole once from the pivot and come back to it
            var splice = new List<int>(hole.Count + 2);
            for (var k = 0; k <= hole.Count; k++)
            {
                splice.Add(hole.Start + ((pivot + k) % hole.Count));
            }

            splice.Add(ring[position]);
            ring.InsertRange(position + 1, splice);
            pending.Remove(hole);
        }

        return ring;
    }

    /// <summary>Finds the offset of the vertex with the largest X inside a contour (the first one on ties).</summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="contour">The contour.</param>
    /// <returns>The offset within the contour.</returns>
    private static int RightmostOffset(Vector2D[] points, ContourInfo contour)
    {
        var best = 0;
        for (var i = 1; i < contour.Count; i++)
        {
            if (points[contour.Start + i].X > points[contour.Start + best].X)
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    ///     Finds the ring position nearest to the hole's pivot that sees the pivot along a segment that
    ///     lies inside the polygon.
    /// </summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="ring">The current merged ring.</param>
    /// <param name="hole">The hole to merge.</param>
    /// <param name="pivot">The offset of the hole's pivot vertex.</param>
    /// <param name="pending">The holes not merged yet (including <paramref name="hole"/>); their edges obstruct bridges.</param>
    /// <returns>The ring position after which the hole is spliced in.</returns>
    private static int FindBridge(Vector2D[] points, List<int> ring, ContourInfo hole, int pivot, List<ContourInfo> pending)
    {
        var target = points[hole.Start + pivot];
        var found = Enumerable.Range(0, ring.Count)
            .OrderBy(position => (points[ring[position]] - target).LengthSquared)
            .FirstOrDefault(position => CanBridge(points, ring, position, hole, pivot, pending), -1);
        if (found < 0)
        {
            throw new InvalidOperationException("No visible bridge vertex exists for a polygon hole.");
        }

        return found;
    }

    /// <summary>
    ///     Tests whether the segment between a ring position and a hole's pivot lies inside the polygon:
    ///     it leaves both corners through their interior wedges and crosses no boundary edge.
    /// </summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="ring">The current merged ring.</param>
    /// <param name="position">The candidate ring position.</param>
    /// <param name="hole">The hole being merged.</param>
    /// <param name="pivot">The offset of the hole's pivot vertex.</param>
    /// <param name="pending">The unmerged holes.</param>
    /// <returns><see langword="true"/> when the bridge is valid.</returns>
    private static bool CanBridge(Vector2D[] points, List<int> ring, int position, ContourInfo hole, int pivot, List<ContourInfo> pending)
    {
        var ringVertex = ring[position];
        var holeVertex = hole.Start + pivot;
        var count = ring.Count;

        // The bridge must leave the ring corner through the solid side
        var ringPrevious = ring[(position + count - 1) % count];
        var ringNext = ring[(position + 1) % count];
        if (!PolygonPredicates.InsideCorner(points[ringPrevious], points[ringVertex], points[ringNext], points[holeVertex]))
        {
            return false;
        }

        // ... and the hole corner through the solid side (the left of the clockwise hole)
        var holePrevious = hole.Start + ((pivot + hole.Count - 1) % hole.Count);
        var holeNext = hole.Start + ((pivot + 1) % hole.Count);
        if (!PolygonPredicates.InsideCorner(points[holePrevious], points[holeVertex], points[holeNext], points[ringVertex]))
        {
            return false;
        }

        // No other boundary edge of the merged ring or of an unmerged hole may touch the segment
        for (var i = 0; i < count; i++)
        {
            if (Blocks(points, ring[i], ring[(i + 1) % count], ringVertex, holeVertex))
            {
                return false;
            }
        }

        foreach (var other in pending)
        {
            for (var i = 0; i < other.Count; i++)
            {
                if (Blocks(points, other.Start + i, other.Start + ((i + 1) % other.Count), ringVertex, holeVertex))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Tests whether an edge that does not end at either bridge endpoint touches the bridge segment.
    /// </summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="edgeStart">The edge start index.</param>
    /// <param name="edgeEnd">The edge end index.</param>
    /// <param name="first">One bridge endpoint index.</param>
    /// <param name="second">The other bridge endpoint index.</param>
    /// <returns><see langword="true"/> when the edge obstructs the bridge.</returns>
    private static bool Blocks(Vector2D[] points, int edgeStart, int edgeEnd, int first, int second)
    {
        // Edges at a bridge endpoint touch it by definition; their direction is covered by the corner tests
        if (edgeStart == first || edgeStart == second || edgeEnd == first || edgeEnd == second)
        {
            return false;
        }

        return PolygonPredicates.SegmentsIntersect(points[first], points[second], points[edgeStart], points[edgeEnd]);
    }
}

/// <summary>
///     Ear clipping of a weakly simple ring of vertex indices.
/// </summary>
/// <remarks>
///     Works on a doubly linked ring and tracks the reflex and collinear vertices so a convex ring costs one
///     check per vertex. Only strictly convex corners are clipped, and a clip is rejected when any other
///     reflex or collinear vertex (ignoring duplicates of the three corners created by bridges) lies in the
///     closed ear triangle. Stateless and thread-safe.
/// </remarks>
internal static class EarClipper
{
    /// <summary>
    ///     Clips a ring into counter-clockwise triangles.
    /// </summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="ring">The ring as vertex indices in counter-clockwise order (at least three entries).</param>
    /// <param name="output">Receives the triangles as vertex index triples.</param>
    /// <exception cref="InvalidOperationException">Thrown when no ear exists (internal invariant).</exception>
    public static void Clip(Vector2D[] points, IReadOnlyList<int> ring, List<int> output)
    {
        var size = ring.Count;
        var vertex = new int[size];
        var previous = new int[size];
        var next = new int[size];
        var reflex = new bool[size];
        var reflexList = new List<int>();
        for (var i = 0; i < size; i++)
        {
            vertex[i] = ring[i];
            previous[i] = (i + size - 1) % size;
            next[i] = (i + 1) % size;
        }

        for (var i = 0; i < size; i++)
        {
            reflex[i] = Turn(points, vertex, previous[i], i, next[i]) <= 0;
            if (reflex[i])
            {
                reflexList.Add(i);
            }
        }

        var remaining = size;
        var current = 0;
        var misses = 0;
        while (remaining > 3)
        {
            // A full unsuccessful lap means no ear exists, which a valid ring never allows
            if (misses > remaining)
            {
                throw new InvalidOperationException("Ear clipping found no ear; the polygon is not simple.");
            }

            if (!IsEar(points, vertex, previous, next, reflex, reflexList, current))
            {
                current = next[current];
                misses++;
                continue;
            }

            // Emit the ear, unlink its tip and refresh the convexity of its two neighbors
            var before = previous[current];
            var after = next[current];
            output.Add(vertex[before]);
            output.Add(vertex[current]);
            output.Add(vertex[after]);
            next[before] = after;
            previous[after] = before;
            // Clipping shrinks the interior angle at both neighbors (the ear triangle lies inside the polygon and the
            // new edge runs inside their old wedge), so a convex neighbor stays convex and no vertex can newly become
            // reflex or collinear; the list of reflex entries therefore only ever shrinks. This also holds at the
            // duplicated vertices of bridges, whose wedges lie on either side of the bridge. Entries that turned
            // convex (or were clipped) are dropped lazily by IsEar.
            reflex[current] = false;
            reflex[before] = Turn(points, vertex, previous[before], before, after) <= 0;
            reflex[after] = Turn(points, vertex, before, after, next[after]) <= 0; remaining--;
            misses = 0;
            current = after;
        }

        // The last three vertices form the final triangle, which must have positive area
        var last = previous[current];
        if (Turn(points, vertex, last, current, next[current]) <= 0)
        {
            throw new InvalidOperationException("Ear clipping ended with a degenerate triangle.");
        }

        output.Add(vertex[last]);
        output.Add(vertex[current]);
        output.Add(vertex[next[current]]);
    }

    /// <summary>Returns the exact turn direction at ring entry <paramref name="middle"/>.</summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="vertex">The vertex index of every ring entry.</param>
    /// <param name="before">The previous entry.</param>
    /// <param name="middle">The corner entry.</param>
    /// <param name="after">The next entry.</param>
    /// <returns>Positive for a convex corner, negative for reflex, zero for collinear.</returns>
    private static int Turn(Vector2D[] points, int[] vertex, int before, int middle, int after) =>
        PolygonPredicates.Orient2D(points[vertex[before]], points[vertex[middle]], points[vertex[after]]);

    /// <summary>
    ///     Tests whether a ring entry is an ear tip that can be removed.
    /// </summary>
    /// <param name="points">All region vertices.</param>
    /// <param name="vertex">The vertex index of every ring entry.</param>
    /// <param name="previous">The previous-entry links.</param>
    /// <param name="next">The next-entry links.</param>
    /// <param name="reflex">Whether each entry is reflex or collinear.</param>
    /// <param name="reflexList">The entries that are reflex or collinear; stale entries are removed here as they are met.</param>
    /// <param name="tip">The candidate tip entry.</param>
    /// <returns><see langword="true"/> when the entry is a strictly convex corner with an empty closed triangle.</returns>
    private static bool IsEar(Vector2D[] points, int[] vertex, int[] previous, int[] next, bool[] reflex, List<int> reflexList, int tip)
    {
        var before = previous[tip];
        var after = next[tip];
        if (reflex[tip])
        {
            return false;
        }

        var a = points[vertex[before]];
        var b = points[vertex[tip]];
        var c = points[vertex[after]];
        var minX = Math.Min(a.X, Math.Min(b.X, c.X));
        var maxX = Math.Max(a.X, Math.Max(b.X, c.X));
        var minY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
        var maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));

        // Walk backwards so stale entries can be swap-removed (order is irrelevant: any blocker rejects the ear)
        for (var i = reflexList.Count - 1; i >= 0; i--)
        {
            var entry = reflexList[i];
            if (!reflex[entry])
            {
                reflexList[i] = reflexList[^1];
                reflexList.RemoveAt(reflexList.Count - 1);
                continue;
            }

            // Duplicates of the corners (bridge endpoints) sit exactly on the triangle and never block it
            var id = vertex[entry];
            if (id == vertex[before] || id == vertex[tip] || id == vertex[after])
            {
                continue;
            }

            // The closed triangle lies inside its bounding box, so points outside it cannot block (exact, and cheap)
            var p = points[id];
            if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY)
            {
                continue;
            }

            if (PolygonPredicates.PointInClosedTriangle(a, b, c, p, counterClockwise: true))
            {
                return false;
            }
        }

        return true;
    }
}
