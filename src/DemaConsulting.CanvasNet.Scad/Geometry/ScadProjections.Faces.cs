using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The face groups of a mesh and their footprints on the XY plane.
/// </content>
internal static partial class ScadProjections
{
    /// <summary>The cosine above which two neighboring triangles count as lying in one plane (about 1.4e-6 radians).</summary>
    private const double CoplanarCosine = 1.0 - 1e-12;

    /// <summary>
    ///     Which faces of a mesh are wanted.
    /// </summary>
    private enum Facing
    {
        /// <summary>The faces that look upward (the normal has a positive Z component).</summary>
        Up,

        /// <summary>The faces that look downward.</summary>
        Down,

        /// <summary>Every face that is not vertical.</summary>
        Any,
    }

    /// <summary>
    ///     Triangles of one mesh that share edges and lie in one plane.
    /// </summary>
    /// <param name="Triangles">The indices of the triangles (triangle number, not index offset), in mesh order.</param>
    /// <param name="Normal">The unit normal of the plane, oriented so that its Z component is positive.</param>
    /// <param name="Offset">The offset of the plane: <c>Normal . p = Offset</c> for every point of it.</param>
    /// <param name="FacesUp">Whether the triangles look upward (their winding normal has a positive Z component).</param>
    private sealed record FaceGroup(List<int> Triangles, Vector3D Normal, double Offset, bool FacesUp);

    /// <summary>
    ///     Gets the upward, downward or non-vertical faces of a mesh, merged into groups of coplanar triangles that
    ///     share edges.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="facing">Which faces are wanted.</param>
    /// <param name="state">The shared state (work meter and cancellation).</param>
    /// <returns>The groups in the order of their first triangle; empty when the work meter failed (the state records it).</returns>
    private static List<FaceGroup> GroupFaces(FrameMesh mesh, Facing facing, State state)
    {
        // Each pass is charged before it runs, one triangle visit per triangle it looks at
        if (!state.Charge(mesh.TriangleCount, poll: false))
        {
            return [];
        }

        var kept = new List<int>();
        var normals = new List<Vector3D>();
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            state.Context.Tick();
            var (a, b, c) = Corners(mesh, t);
            var cross = Vector3D.Cross(b - a, c - a);

            // A vertical triangle has no footprint; a face that is wanted looks the right way
            if (Math.Abs(cross.Z) <= mesh.AreaEpsilon || !IsWanted(cross.Z, facing))
            {
                continue;
            }

            kept.Add(t);
            normals.Add(cross / cross.Length);
        }

        // The merge and the collection visit each wanted triangle once (the edge table adds a constant factor)
        if (!state.Charge(2L * kept.Count, poll: false))
        {
            return [];
        }

        var parent = new int[kept.Count];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        MergeNeighbors(mesh, kept, normals, parent, state);
        return CollectGroups(mesh, kept, normals, parent, state);
    }

    /// <summary>
    ///     Tells whether a face with the given doubled XY area (the Z component of its cross product) is wanted.
    /// </summary>
    /// <param name="crossZ">The Z component of the cross product of two edges; positive for an upward face.</param>
    /// <param name="facing">Which faces are wanted.</param>
    /// <returns><see langword="true"/> when the face is wanted.</returns>
    private static bool IsWanted(double crossZ, Facing facing) => facing switch
    {
        Facing.Up => crossZ > 0.0,
        Facing.Down => crossZ < 0.0,
        _ => true,
    };

    /// <summary>
    ///     Joins the triangles that share an edge and lie in one plane (union-find).
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="kept">The numbers of the wanted triangles.</param>
    /// <param name="normals">The unit normals of the wanted triangles.</param>
    /// <param name="parent">The union-find parents, one per wanted triangle.</param>
    /// <param name="state">The shared state.</param>
    private static void MergeNeighbors(FrameMesh mesh, List<int> kept, List<Vector3D> normals, int[] parent, State state)
    {
        var owner = new Dictionary<long, int>();
        for (var k = 0; k < kept.Count; k++)
        {
            state.Context.Tick();
            var (a, b, c) = Indices(mesh, kept[k]);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                var key = UndirectedKey(u, v);
                if (!owner.TryGetValue(key, out var other))
                {
                    owner[key] = k;
                }
                else if (AreCoplanar(mesh, kept, normals, k, other))
                {
                    Join(parent, k, other);
                }
            }
        }
    }

    /// <summary>
    ///     Tells whether two wanted triangles lie in one plane within the tolerance of the mesh.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="kept">The numbers of the wanted triangles.</param>
    /// <param name="normals">The unit normals of the wanted triangles.</param>
    /// <param name="first">The index of the first triangle among the wanted ones.</param>
    /// <param name="second">The index of the second triangle among the wanted ones.</param>
    /// <returns><see langword="true"/> when they are coplanar.</returns>
    private static bool AreCoplanar(FrameMesh mesh, List<int> kept, List<Vector3D> normals, int first, int second)
    {
        if (Vector3D.Dot(normals[first], normals[second]) < CoplanarCosine)
        {
            return false;
        }

        var (a, _, _) = Corners(mesh, kept[first]);
        var (o, _, _) = Corners(mesh, kept[second]);
        return Math.Abs(Vector3D.Dot(normals[first], o - a)) <= mesh.Tolerance;
    }

    /// <summary>
    ///     Gathers the merged triangles into groups with their planes.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="kept">The numbers of the wanted triangles.</param>
    /// <param name="normals">The unit normals of the wanted triangles.</param>
    /// <param name="parent">The union-find parents.</param>
    /// <param name="state">The shared state (cancellation is polled per triangle).</param>
    /// <returns>The groups in the order of their first triangle.</returns>
    private static List<FaceGroup> CollectGroups(FrameMesh mesh, List<int> kept, List<Vector3D> normals, int[] parent, State state)
    {
        var groups = new List<FaceGroup>();
        var byRoot = new Dictionary<int, FaceGroup>();
        for (var k = 0; k < kept.Count; k++)
        {
            state.Context.Tick();
            var root = Find(parent, k);
            if (!byRoot.TryGetValue(root, out var group))
            {
                var normal = normals[k].Z > 0.0 ? normals[k] : -normals[k];
                var (a, _, _) = Corners(mesh, kept[k]);
                group = new FaceGroup([], normal, Vector3D.Dot(normal, a), normals[k].Z > 0.0);
                byRoot[root] = group;
                groups.Add(group);
            }

            group.Triangles.Add(kept[k]);
        }

        return groups;
    }

    /// <summary>
    ///     Finds the root of a union-find element, compressing the path.
    /// </summary>
    /// <param name="parent">The parents.</param>
    /// <param name="element">The element.</param>
    /// <returns>The root.</returns>
    private static int Find(int[] parent, int element)
    {
        var root = element;
        while (parent[root] != root)
        {
            root = parent[root];
        }

        while (parent[element] != root)
        {
            var following = parent[element];
            parent[element] = root;
            element = following;
        }

        return root;
    }

    /// <summary>
    ///     Joins two union-find elements.
    /// </summary>
    /// <param name="parent">The parents.</param>
    /// <param name="a">The first element.</param>
    /// <param name="b">The second element.</param>
    private static void Join(int[] parent, int a, int b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra != rb)
        {
            parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
        }
    }

    /// <summary>
    ///     Builds the XY footprint of a group of triangles as regions.
    /// </summary>
    /// <remarks>
    ///     The boundary edges of the group (those whose reverse edge is not in the group) are chained into contours and
    ///     become one region, with holes by nesting. When the boundary is ambiguous (a vertex where two contours touch),
    ///     does not close, or the region is rejected, the group falls back to one region per triangle, which covers the
    ///     same area.
    /// </remarks>
    /// <param name="mesh">The mesh.</param>
    /// <param name="group">The group.</param>
    /// <param name="state">The shared state (work meter and cancellation).</param>
    /// <returns>The regions; their union is the footprint; empty when the work meter failed (the state records it).</returns>
    private static List<PolygonRegion> Footprint(FrameMesh mesh, FaceGroup group, State state)
    {
        var merged = TryMerged(mesh, group, state);
        if (merged is not null)
        {
            return [merged];
        }

        // The per-triangle fallback is one more pass over the group
        if (state.Failed || !state.Charge(group.Triangles.Count, poll: false))
        {
            return [];
        }

        var regions = new List<PolygonRegion>(group.Triangles.Count);
        foreach (var t in group.Triangles)
        {
            state.Context.Tick();
            var (a, b, c) = Corners(mesh, t);
            try
            {
                regions.Add(new PolygonRegion([[Flat(a), Flat(b), Flat(c)]]));
            }
            catch (ArgumentException)
            {
                // A triangle with no usable area has no footprint
            }
        }

        return regions;
    }

    /// <summary>
    ///     Tries to build one region from the boundary of a group.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="group">The group.</param>
    /// <param name="state">The shared state (work meter and cancellation).</param>
    /// <returns>The region, or <see langword="null"/> when the boundary cannot be chained cleanly or the work meter failed.</returns>
    private static PolygonRegion? TryMerged(FrameMesh mesh, FaceGroup group, State state)
    {
        if (group.Triangles.Count == 1)
        {
            return null;
        }

        // The edge pass and the successor pass each visit every triangle of the group
        if (!state.Charge(2L * group.Triangles.Count, poll: false))
        {
            return null;
        }

        var edges = new HashSet<long>();
        foreach (var t in group.Triangles)
        {
            state.Context.Tick();
            var (a, b, c) = Indices(mesh, t);
            edges.Add(DirectedKey(a, b));
            edges.Add(DirectedKey(b, c));
            edges.Add(DirectedKey(c, a));
        }

        var next = new Dictionary<int, int>();
        foreach (var t in group.Triangles)
        {
            state.Context.Tick();
            var (a, b, c) = Indices(mesh, t);
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                if (!edges.Contains(DirectedKey(v, u)) && !next.TryAdd(u, v))
                {
                    return null;
                }
            }
        }

        var contours = ChainContours(mesh, next, state);
        if (contours is null)
        {
            return null;
        }

        try
        {
            return new PolygonRegion(contours);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Chains boundary edges into closed contours.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="next">The successor of each boundary vertex.</param>
    /// <param name="state">The shared state (cancellation is polled per boundary vertex).</param>
    /// <returns>The contours projected to the XY plane, or <see langword="null"/> when a chain does not close.</returns>
    private static List<IReadOnlyList<Vector2D>>? ChainContours(FrameMesh mesh, Dictionary<int, int> next, State state)
    {
        var contours = new List<IReadOnlyList<Vector2D>>();
        var visited = new HashSet<int>();
        foreach (var start in next.Keys)
        {
            state.Context.Tick();
            if (visited.Contains(start))
            {
                continue;
            }

            var points = new List<Vector2D>();
            var current = start;
            do
            {
                state.Context.Tick();
                if (!visited.Add(current) || !next.TryGetValue(current, out var following))
                {
                    return null;
                }

                points.Add(Flat(mesh.Vertices[current]));
                current = following;
            }
            while (current != start);

            contours.Add(points);
        }

        return contours;
    }

    /// <summary>
    ///     Gets the three corners of a triangle.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="triangle">The triangle number.</param>
    /// <returns>The corners in winding order.</returns>
    private static (Vector3D A, Vector3D B, Vector3D C) Corners(FrameMesh mesh, int triangle)
    {
        var (a, b, c) = Indices(mesh, triangle);
        return (mesh.Vertices[a], mesh.Vertices[b], mesh.Vertices[c]);
    }

    /// <summary>
    ///     Gets the vertex indices of a triangle.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="triangle">The triangle number.</param>
    /// <returns>The indices in winding order.</returns>
    private static (int A, int B, int C) Indices(FrameMesh mesh, int triangle) =>
        (mesh.TriangleIndices[3 * triangle], mesh.TriangleIndices[(3 * triangle) + 1], mesh.TriangleIndices[(3 * triangle) + 2]);

    /// <summary>
    ///     Projects a point onto the XY plane.
    /// </summary>
    /// <param name="p">The point.</param>
    /// <returns>Its X and Y.</returns>
    private static Vector2D Flat(Vector3D p) => new(p.X, p.Y);

    /// <summary>
    ///     Makes the key of a directed edge.
    /// </summary>
    /// <param name="from">The start vertex.</param>
    /// <param name="to">The end vertex.</param>
    /// <returns>The key.</returns>
    private static long DirectedKey(int from, int to) => ((long)from << 32) | (uint)to;

    /// <summary>
    ///     Makes the key of an undirected edge.
    /// </summary>
    /// <param name="a">One vertex.</param>
    /// <param name="b">The other vertex.</param>
    /// <returns>The key.</returns>
    private static long UndirectedKey(int a, int b) => a < b ? DirectedKey(a, b) : DirectedKey(b, a);
}
