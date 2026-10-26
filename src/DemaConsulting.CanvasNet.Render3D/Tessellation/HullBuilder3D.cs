// cspell:ignore coplanar collinear lexicographic lexicographically halfspaces

using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     The incremental three-dimensional convex hull engine behind <see cref="ConvexHull.Hull3D"/>: a sorted
///     insertion algorithm with exact predicates, explicit face adjacency and a deterministic free list.
/// </summary>
/// <remarks>
///     <para>
///         <b>Algorithm.</b> The input is the sorted array of distinct points. The initial polyhedron is the
///         hull of the sorted prefix that ends at the first point off the plane of the first non-collinear
///         triple: a pyramid over the exact planar hull of that coplanar prefix. The remaining points are
///         inserted in sorted order. Faces are triangles stored in arrays with neighbor indices; a face is
///         <em>visible</em> from a point when <see cref="SolidPredicates.Orient3D"/> is strictly positive (the
///         point is above its plane). An insertion replaces the connected set of strictly visible faces with the
///         cone from the new point to the horizon (the edges between visible and non-visible faces).
///     </para>
///     <para>
///         <b>Invariants and why they hold (the basis of the correctness argument).</b>
///         (1) The mesh is always the boundary of the convex hull of the inserted points, with every edge shared
///         by exactly two triangles in opposite directions. (2) Each inserted point is the lexicographic
///         maximum of all points so far, hence a strict extreme point (a vertex) of the new hull. (3) Some face
///         incident to the previously inserted vertex <c>q</c> is strictly visible from the new point <c>p</c>:
///         the tangent cone of the hull at <c>q</c> consists only of lexicographically non-positive directions
///         (every hull point is at most <c>q</c>), whereas <c>p - q</c> is lexicographically positive, so
///         <c>p</c> cannot lie in the closed halfspaces of all faces at <c>q</c>. The fan of <c>q</c> is
///         therefore searched for a seed, and its length is amortized by the number of faces created by the
///         previous insertion. (4) The strictly visible faces are connected (the visible part of a convex
///         polytope surface is a topological disc, and coplanar triangles have identical visibility because
///         the predicate is exact), so a flood fill through the neighbor links finds them all, and the horizon
///         is one simple cycle. (5) <em>Strict</em> visibility never creates a degenerate triangle: a horizon
///         edge has one strictly visible face, so <c>p</c> is off that face's plane and cannot be collinear with
///         the edge; a coplanar (non-visible) face across a horizon edge receives a new coplanar triangle on its
///         outer side, which neither overlaps nor creates a T-junction.
///     </para>
///     <para>
///         <b>Determinism.</b> No randomness, hashing order or tolerance is used. Faces occupy slots allocated
///         from an explicit LIFO free list and the flood fill and the horizon walk run in index order, so equal
///         sorted input always gives the identical face structure. The finished surface is then reduced to its
///         canonical form: coplanar triangles are merged into facets, points on a face or an edge are dropped
///         (the mesh keeps exactly the strictly extreme points) and every facet is fan-triangulated from its
///         lowest-index corner, with the facets in the order of their corner indices, so the output depends only
///         on the hull polytope. A final check of the edge pairing, the facet boundaries and the Euler relation
///         <c>F = 2V - 4</c> turns any (theoretically impossible) violation into an
///         <see cref="InvalidOperationException"/> instead of a defective mesh.
///     </para>
///     <para>
///         <b>Cost.</b> An insertion costs the length of the previous vertex fan, the visible faces and the
///         horizon: the insertions take O(n log n) in total while the hull stays small and O(n * h) in the worst
///         case (h = hull vertices, up to O(n^2)); the final merge of coplanar triangles is O(F log F) for F
///         triangles. The sort that precedes the build is O(n log n). Memory is
///         O(n). An instance is single-use and not thread-safe.
///     </para>
/// </remarks>
internal sealed class HullBuilder3D
{
    /// <summary>Number of elementary work units between two cancellation polls.</summary>
    private const int PollInterval = 4096;

    /// <summary>The initial number of face slots.</summary>
    private const int InitialCapacity = 64;

    /// <summary>The sorted distinct input points; only the first <see cref="_count"/> entries are meaningful.</summary>
    private readonly Vector3D[] _points;

    /// <summary>The number of sorted distinct input points (a prefix of <see cref="_points"/>).</summary>
    private readonly int _count;

    /// <summary>The cancellation token polled during construction.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The faces visible from the point being inserted, in discovery order (also the flood-fill queue).</summary>
    private readonly List<int> _visibleFaces = [];

    /// <summary>The start vertices of the horizon edges of the current insertion.</summary>
    private readonly List<int> _horizonFrom = [];

    /// <summary>The end vertices of the horizon edges of the current insertion.</summary>
    private readonly List<int> _horizonTo = [];

    /// <summary>The non-visible faces across the horizon edges of the current insertion.</summary>
    private readonly List<int> _horizonOuter = [];

    /// <summary>The new faces created by the current insertion, parallel to the horizon edges.</summary>
    private readonly List<int> _newFaces = [];

    /// <summary>
    ///     Scratch map from a vertex to another vertex or face (-1 when unused): the new face starting at each
    ///     horizon vertex during an insertion, and the loop successor of each facet boundary vertex while a facet
    ///     polygon is extracted. Always reset to -1 after use.
    /// </summary>
    private readonly int[] _startAt;

    /// <summary>The three vertex indices of every face slot.</summary>
    private int[] _faceVertices = new int[3 * InitialCapacity];

    /// <summary>The neighbor across each edge <c>(v[k], v[k + 1])</c> of every face slot.</summary>
    private int[] _faceNeighbors = new int[3 * InitialCapacity];

    /// <summary>Whether each face slot holds a live face.</summary>
    private bool[] _faceAlive = new bool[InitialCapacity];

    /// <summary>The insertion stamp at which each face was last classified.</summary>
    private int[] _faceMark = new int[InitialCapacity];

    /// <summary>The visibility classification of each face, valid when its mark equals the current stamp.</summary>
    private bool[] _faceVisible = new bool[InitialCapacity];

    /// <summary>The LIFO stack of free face slots.</summary>
    private int[] _freeList = new int[InitialCapacity];

    /// <summary>The number of entries in <see cref="_freeList"/>.</summary>
    private int _freeCount;

    /// <summary>The number of face slots ever used.</summary>
    private int _slots;

    /// <summary>The number of live faces.</summary>
    private int _liveFaces;

    /// <summary>The most recently inserted vertex (a strict extreme point of the current hull).</summary>
    private int _lastVertex;

    /// <summary>A live face incident to <see cref="_lastVertex"/>.</summary>
    private int _lastFace;

    /// <summary>The current insertion stamp.</summary>
    private int _stamp;

    /// <summary>The work counter for cancellation polling.</summary>
    private int _work;

    /// <summary>
    ///     Initializes a builder for sorted distinct points.
    /// </summary>
    /// <param name="points">The array that holds the sorted, canonicalized, distinct, finite points as a prefix.</param>
    /// <param name="count">The number of points in the prefix.</param>
    /// <param name="cancellationToken">A token that cancels the construction.</param>
    private HullBuilder3D(Vector3D[] points, int count, CancellationToken cancellationToken)
    {
        _points = points;
        _count = count;
        _cancellationToken = cancellationToken;
        _startAt = new int[count];
        Array.Fill(_startAt, -1);
    }

    /// <summary>
    ///     Builds the hull mesh of sorted distinct points.
    /// </summary>
    /// <param name="sortedPoints">An array whose first <paramref name="count"/> entries are the points in ascending lexicographic order without duplicates (finite, no negative zero); entries beyond the count are ignored.</param>
    /// <param name="count">The number of points at the start of <paramref name="sortedPoints"/>.</param>
    /// <param name="cancellationToken">A token that cancels the construction.</param>
    /// <returns>The closed, outward-wound hull mesh.</returns>
    /// <exception cref="ArgumentException">Thrown when the points do not span a volume.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the internal closure check fails.</exception>
    public static Mesh Build(Vector3D[] sortedPoints, int count, CancellationToken cancellationToken)
    {
        var builder = new HullBuilder3D(sortedPoints, count, cancellationToken);
        var apex = builder.CreateInitialPolyhedron();
        for (var i = apex + 1; i < count; i++)
        {
            builder.Insert(i);
        }

        return builder.ToMesh();
    }

    /// <summary>
    ///     Finds the initial simplex, builds the pyramid over the exact hull of the coplanar sorted prefix and
    ///     returns the index of its apex.
    /// </summary>
    /// <returns>The sorted index of the apex, the last point of the initial polyhedron.</returns>
    /// <exception cref="ArgumentException">Thrown when the points are fewer than four, collinear or coplanar.</exception>
    private int CreateInitialPolyhedron()
    {
        var points = _points;
        var count = _count;
        if (count < 4)
        {
            throw new ArgumentException(
                $"The hull needs at least four distinct points, but only {count} were given (degenerate input).");
        }

        // The third point is the first one off the line through the first two points
        var first = points[0];
        var second = points[1];
        var third = -1;
        for (var i = 2; i < count && third < 0; i++)
        {
            Charge(1);
            if (SolidPredicates.NonDegenerateProjection(first, second, points[i]) >= 0)
            {
                third = i;
            }
        }

        if (third < 0)
        {
            throw new ArgumentException("The hull needs points that are not all collinear (degenerate input).");
        }

        // The apex is the first point off the plane of the three; every earlier point lies in that plane
        var apex = -1;
        for (var i = third + 1; i < count && apex < 0; i++)
        {
            Charge(1);
            if (SolidPredicates.Orient3D(first, second, points[third], points[i]) != 0)
            {
                apex = i;
            }
        }

        if (apex < 0)
        {
            throw new ArgumentException("The hull needs points that are not all coplanar (degenerate input).");
        }

        // Exact planar hull of the coplanar prefix, in the coordinate plane where the first triple has area
        var dropped = SolidPredicates.NonDegenerateProjection(first, second, points[third]);
        var projected = new Vector2D[apex];
        var order = new int[apex];
        for (var i = 0; i < apex; i++)
        {
            projected[i] = SolidPredicates.Project(points[i], dropped);
            order[i] = i;
        }

        Array.Sort(order, (left, right) =>
        {
            var result = projected[left].X.CompareTo(projected[right].X);
            return result != 0 ? result : projected[left].Y.CompareTo(projected[right].Y);
        });
        var polygon = ConvexHull.ChainIndices(projected, order, _cancellationToken);

        // Orient the polygon so that the apex lies below its (outward) base faces
        if (SolidPredicates.Orient3D(points[polygon[0]], points[polygon[1]], points[polygon[2]], points[apex]) > 0)
        {
            Array.Reverse(polygon);
        }

        // Base fan (apex below) and one side face per polygon edge, which runs against the base boundary
        for (var i = 1; i + 1 < polygon.Length; i++)
        {
            AllocateFace(polygon[0], polygon[i], polygon[i + 1]);
        }

        for (var i = 0; i < polygon.Length; i++)
        {
            var from = polygon[i];
            var to = polygon[(i + 1) % polygon.Length];
            _lastFace = AllocateFace(to, from, apex);
        }

        LinkInitialFaces();
        _lastVertex = apex;
        return apex;
    }

    /// <summary>
    ///     Links the neighbors of the initial faces by matching every directed edge with its reverse.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an edge has no partner (cannot happen for a valid pyramid).</exception>
    private void LinkInitialFaces()
    {
        // The lookup is only probed, never enumerated, so its hashing order cannot reach the output
        var edges = new Dictionary<long, int>(_slots * 3);
        for (var f = 0; f < _slots; f++)
        {
            for (var k = 0; k < 3; k++)
            {
                edges[EdgeKey(_faceVertices[(3 * f) + k], _faceVertices[(3 * f) + ((k + 1) % 3)])] = (3 * f) + k;
            }
        }

        for (var f = 0; f < _slots; f++)
        {
            for (var k = 0; k < 3; k++)
            {
                var from = _faceVertices[(3 * f) + k];
                var to = _faceVertices[(3 * f) + ((k + 1) % 3)];
                if (!edges.TryGetValue(EdgeKey(to, from), out var partner))
                {
                    throw new InvalidOperationException("The initial hull polyhedron is not closed.");
                }

                _faceNeighbors[(3 * f) + k] = partner / 3;
            }
        }
    }

    /// <summary>
    ///     Inserts the point with the given sorted index, which is the lexicographic maximum so far.
    /// </summary>
    /// <param name="index">The sorted index of the point to insert.</param>
    /// <exception cref="InvalidOperationException">Thrown when an invariant fails (cannot happen for valid input).</exception>
    private void Insert(int index)
    {
        var point = _points[index];
        _stamp++;
        Charge(1);

        // Seed: a strictly visible face around the previous vertex, then flood the visible region
        var seed = FindVisibleFaceAroundLastVertex(point);
        _visibleFaces.Clear();
        _visibleFaces.Add(seed);
        for (var i = 0; i < _visibleFaces.Count; i++)
        {
            var face = _visibleFaces[i];
            Charge(1);
            for (var k = 0; k < 3; k++)
            {
                var neighbor = _faceNeighbors[(3 * face) + k];
                if (_faceMark[neighbor] == _stamp)
                {
                    continue;
                }

                if (ClassifyFace(neighbor, point))
                {
                    _visibleFaces.Add(neighbor);
                }
            }
        }

        // The horizon is every edge of a visible face whose neighbor is not visible
        _horizonFrom.Clear();
        _horizonTo.Clear();
        _horizonOuter.Clear();
        foreach (var face in _visibleFaces)
        {
            for (var k = 0; k < 3; k++)
            {
                var neighbor = _faceNeighbors[(3 * face) + k];
                if (!_faceVisible[neighbor])
                {
                    _horizonFrom.Add(_faceVertices[(3 * face) + k]);
                    _horizonTo.Add(_faceVertices[(3 * face) + ((k + 1) % 3)]);
                    _horizonOuter.Add(neighbor);
                }
            }
        }

        if (_horizonFrom.Count < 3)
        {
            throw new InvalidOperationException("The hull horizon is not a closed curve.");
        }

        // Retire the visible faces, then cover the hole with the cone from the new point to the horizon
        foreach (var face in _visibleFaces)
        {
            _faceAlive[face] = false;
            PushFree(face);
            _liveFaces--;
        }

        _newFaces.Clear();
        for (var h = 0; h < _horizonFrom.Count; h++)
        {
            var from = _horizonFrom[h];
            var to = _horizonTo[h];
            var outer = _horizonOuter[h];
            var created = AllocateFace(from, to, index);
            _newFaces.Add(created);

            // The outer face traverses the edge the other way round; point it at the new face
            var outerEdge = FindEdge(outer, to, from);
            _faceNeighbors[(3 * outer) + outerEdge] = created;
            _faceNeighbors[3 * created] = outer;

            if (_startAt[from] >= 0)
            {
                throw new InvalidOperationException("The hull horizon touches itself.");
            }

            _startAt[from] = created;
        }

        // Neighboring cone faces pair up around the new point: (to, p) of one is (p, from) of the next
        for (var h = 0; h < _newFaces.Count; h++)
        {
            var created = _newFaces[h];
            var next = _startAt[_horizonTo[h]];
            if (next < 0)
            {
                throw new InvalidOperationException("The hull horizon is not a closed curve.");
            }

            _faceNeighbors[(3 * created) + 1] = next;
            _faceNeighbors[(3 * next) + 2] = created;
        }

        for (var h = 0; h < _horizonFrom.Count; h++)
        {
            _startAt[_horizonFrom[h]] = -1;
        }

        _lastVertex = index;
        _lastFace = _newFaces[0];
        Charge(_visibleFaces.Count + _horizonFrom.Count);
    }

    /// <summary>
    ///     Finds a face strictly visible from a point by walking the fan of faces around the last inserted vertex.
    /// </summary>
    /// <param name="point">The point being inserted.</param>
    /// <returns>A strictly visible face.</returns>
    /// <exception cref="InvalidOperationException">Thrown when none exists (impossible by the seed argument).</exception>
    private int FindVisibleFaceAroundLastVertex(Vector3D point)
    {
        var start = _lastFace;
        var face = start;
        var steps = 0;
        do
        {
            // The first strictly visible face of the fan is the seed (non-visible ones are classified for reuse)
            if (ClassifyFace(face, point))
            {
                return face;
            }

            // Rotate around the vertex: the edge leaving it leads to the next face of the fan
            var local = LocalIndex(face, _lastVertex);
            face = _faceNeighbors[(3 * face) + local];
            Charge(1);
            if (++steps > _slots)
            {
                throw new InvalidOperationException("The fan around the last hull vertex does not close.");
            }
        }
        while (face != start);

        throw new InvalidOperationException("No face around the last hull vertex is visible from the inserted point.");
    }

    /// <summary>
    ///     Classifies a face as strictly visible from a point and records the result for the current stamp.
    /// </summary>
    /// <param name="face">The face slot.</param>
    /// <param name="point">The point being inserted.</param>
    /// <returns><see langword="true"/> when the point is strictly above the plane of the face.</returns>
    private bool ClassifyFace(int face, Vector3D point)
    {
        var visible = SolidPredicates.Orient3D(
            _points[_faceVertices[3 * face]],
            _points[_faceVertices[(3 * face) + 1]],
            _points[_faceVertices[(3 * face) + 2]],
            point) > 0;
        _faceMark[face] = _stamp;
        _faceVisible[face] = visible;
        return visible;
    }

    /// <summary>
    ///     Finds the local index of the edge <c>tail -&gt; head</c> in a face.
    /// </summary>
    /// <param name="face">The face slot.</param>
    /// <param name="tail">The start vertex of the edge.</param>
    /// <param name="head">The end vertex of the edge.</param>
    /// <returns>The local edge index 0 to 2.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the face has no such directed edge.</exception>
    private int FindEdge(int face, int tail, int head)
    {
        for (var k = 0; k < 3; k++)
        {
            if (_faceVertices[(3 * face) + k] == tail && _faceVertices[(3 * face) + ((k + 1) % 3)] == head)
            {
                return k;
            }
        }

        throw new InvalidOperationException("A hull face lacks the expected edge.");
    }

    /// <summary>
    ///     Finds the local index of a vertex in a face.
    /// </summary>
    /// <param name="face">The face slot.</param>
    /// <param name="vertex">The vertex index.</param>
    /// <returns>The local vertex index 0 to 2.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the face does not contain the vertex.</exception>
    private int LocalIndex(int face, int vertex)
    {
        for (var k = 0; k < 3; k++)
        {
            if (_faceVertices[(3 * face) + k] == vertex)
            {
                return k;
            }
        }

        throw new InvalidOperationException("A hull face does not contain the expected vertex.");
    }

    /// <summary>
    ///     Takes a face slot from the free list (or a new one) and stores a live face in it.
    /// </summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    /// <returns>The slot of the new face; its neighbors are not set yet.</returns>
    private int AllocateFace(int a, int b, int c)
    {
        int slot;
        if (_freeCount > 0)
        {
            slot = _freeList[--_freeCount];
        }
        else
        {
            slot = _slots++;
            if (slot >= _faceAlive.Length)
            {
                Grow();
            }
        }

        _faceVertices[3 * slot] = a;
        _faceVertices[(3 * slot) + 1] = b;
        _faceVertices[(3 * slot) + 2] = c;
        _faceAlive[slot] = true;
        _liveFaces++;
        return slot;
    }

    /// <summary>Returns a face slot to the free list.</summary>
    /// <param name="slot">The slot to free.</param>
    private void PushFree(int slot) => _freeList[_freeCount++] = slot;

    /// <summary>Doubles the capacity of all per-face arrays.</summary>
    private void Grow()
    {
        var capacity = _faceAlive.Length * 2;
        Array.Resize(ref _faceVertices, 3 * capacity);
        Array.Resize(ref _faceNeighbors, 3 * capacity);
        Array.Resize(ref _faceAlive, capacity);
        Array.Resize(ref _faceMark, capacity);
        Array.Resize(ref _faceVisible, capacity);
        Array.Resize(ref _freeList, capacity);
    }

    /// <summary>
    ///     Accounts for elementary work and polls the cancellation token every <see cref="PollInterval"/> units.
    /// </summary>
    /// <param name="units">The number of work units.</param>
    private void Charge(int units)
    {
        _work += units;
        if (_work >= PollInterval)
        {
            _work = 0;
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Verifies the closure of the finished hull and converts it into a canonical mesh: coplanar triangles
    ///     are merged into facets, vertices that are not corners of their facets (points on a face or an edge)
    ///     are dropped, and each facet is fan-triangulated from its lowest-index corner.
    /// </summary>
    /// <remarks>
    ///     The result contains exactly the strictly extreme points as vertices, and its triangles are a function of
    ///     the hull polytope only (facets are ordered by their corner indices), so the incremental history leaves
    ///     no trace. Every corner of a facet is a corner of the polytope, so neighboring facets agree on their
    ///     shared edges and the mesh stays welded and closed.
    /// </remarks>
    /// <returns>The hull mesh.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the edge pairing, a facet boundary or the Euler relation is violated.</exception>
    private Mesh ToMesh()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // Every live edge must be matched by the reverse edge of its live neighbor
        for (var f = 0; f < _slots; f++)
        {
            if (!_faceAlive[f])
            {
                continue;
            }

            for (var k = 0; k < 3; k++)
            {
                var neighbor = _faceNeighbors[(3 * f) + k];
                var from = _faceVertices[(3 * f) + k];
                var to = _faceVertices[(3 * f) + ((k + 1) % 3)];
                if (!_faceAlive[neighbor] || _faceNeighbors[(3 * neighbor) + FindEdge(neighbor, to, from)] != f)
                {
                    throw new InvalidOperationException("The hull is not a closed manifold surface.");
                }
            }
        }

        // Merge coplanar triangles into facets and reduce every facet to its strictly convex polygon
        var polygons = BuildFacetPolygons();
        polygons.Sort(ComparePolygons);

        // Fan-triangulate each polygon from its first (lowest-index) corner and collect the used vertices
        var used = new bool[_count];
        var triangles = new List<int>(3 * _liveFaces);
        foreach (var polygon in polygons)
        {
            for (var i = 1; i + 1 < polygon.Length; i++)
            {
                triangles.Add(polygon[0]);
                triangles.Add(polygon[i]);
                triangles.Add(polygon[i + 1]);
            }

            foreach (var corner in polygon)
            {
                used[corner] = true;
            }
        }

        // Map the corners to dense indices in sorted order
        var map = new int[_count];
        var vertices = new List<Vector3D>();
        for (var i = 0; i < used.Length; i++)
        {
            if (used[i])
            {
                map[i] = vertices.Count;
                vertices.Add(_points[i]);
            }
        }

        if (triangles.Count / 3 != (2 * vertices.Count) - 4)
        {
            throw new InvalidOperationException("The hull violates the Euler relation F = 2V - 4.");
        }

        for (var i = 0; i < triangles.Count; i++)
        {
            triangles[i] = map[triangles[i]];
        }

        return new Mesh(vertices, triangles);
    }

    /// <summary>
    ///     Groups the live triangles into maximal coplanar facets and returns each facet's boundary polygon
    ///     without collinear vertices, wound like the triangles and starting at its lowest-index corner.
    /// </summary>
    /// <returns>The facet polygons (vertex indices), in first-appearance order of their lowest slot.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a facet boundary is not one simple closed loop.</exception>
    private List<int[]> BuildFacetPolygons()
    {
        // Union-find over face slots: two neighbors are in one facet when the apex of one is in the plane of the other
        var parent = new int[_slots];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        for (var f = 0; f < _slots; f++)
        {
            if (!_faceAlive[f])
            {
                continue;
            }

            Charge(1);
            for (var k = 0; k < 3; k++)
            {
                var neighbor = _faceNeighbors[(3 * f) + k];
                if (neighbor < f)
                {
                    continue;
                }

                var apex = _faceVertices[(3 * neighbor) + ((FindEdge(neighbor, _faceVertices[(3 * f) + ((k + 1) % 3)], _faceVertices[(3 * f) + k]) + 2) % 3)];
                if (SolidPredicates.Orient3D(
                        _points[_faceVertices[3 * f]],
                        _points[_faceVertices[(3 * f) + 1]],
                        _points[_faceVertices[(3 * f) + 2]],
                        _points[apex]) == 0)
                {
                    Join(parent, f, neighbor);
                }
            }
        }

        // Collect the boundary edges (neighbor in another facet) of every facet, facets numbered by first slot
        var facetOfRoot = new int[_slots];
        Array.Fill(facetOfRoot, -1);
        var boundaries = new List<List<(int From, int To)>>();
        for (var f = 0; f < _slots; f++)
        {
            if (!_faceAlive[f])
            {
                continue;
            }

            var root = Find(parent, f);
            if (facetOfRoot[root] < 0)
            {
                facetOfRoot[root] = boundaries.Count;
                boundaries.Add([]);
            }

            for (var k = 0; k < 3; k++)
            {
                if (Find(parent, _faceNeighbors[(3 * f) + k]) != root)
                {
                    boundaries[facetOfRoot[root]].Add((_faceVertices[(3 * f) + k], _faceVertices[(3 * f) + ((k + 1) % 3)]));
                }
            }
        }

        var polygons = new List<int[]>(boundaries.Count);
        foreach (var edges in boundaries)
        {
            polygons.Add(ReduceBoundary(edges));
        }

        return polygons;
    }

    /// <summary>
    ///     Turns the directed boundary edges of one facet into its polygon: one closed loop, rotated to start at the
    ///     lowest vertex index, with the vertices that continue a straight run removed.
    /// </summary>
    /// <param name="edges">The directed boundary edges of the facet.</param>
    /// <returns>The strictly convex polygon of the facet (at least three corners).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the edges are not one simple closed loop of non-collinear corners.</exception>
    private int[] ReduceBoundary(List<(int From, int To)> edges)
    {
        // Link every start vertex to its end vertex; a vertex that starts two edges would pinch the boundary
        var first = int.MaxValue;
        foreach (var (from, to) in edges)
        {
            if (_startAt[from] >= 0)
            {
                throw new InvalidOperationException("A hull facet boundary touches itself.");
            }

            _startAt[from] = to;
            first = Math.Min(first, from);
        }

        // Walk the loop from its lowest vertex and clear the links again
        var loop = new List<int>(edges.Count);
        var current = first;
        do
        {
            loop.Add(current);
            var next = _startAt[current];
            _startAt[current] = -1;
            if (next < 0 || loop.Count > edges.Count)
            {
                throw new InvalidOperationException("A hull facet boundary is not a closed loop.");
            }

            current = next;
        }
        while (current != first);

        if (loop.Count != edges.Count)
        {
            throw new InvalidOperationException("A hull facet boundary consists of several loops.");
        }

        // Keep only the corners: a vertex collinear with its neighbors lies on an edge of the facet
        var corners = new List<int>(loop.Count);
        for (var i = 0; i < loop.Count; i++)
        {
            var previous = _points[loop[(i + loop.Count - 1) % loop.Count]];
            var next = _points[loop[(i + 1) % loop.Count]];
            if (SolidPredicates.NonDegenerateProjection(previous, _points[loop[i]], next) >= 0)
            {
                corners.Add(loop[i]);
            }
        }

        if (corners.Count < 3)
        {
            throw new InvalidOperationException("A hull facet has fewer than three corners.");
        }

        return [.. corners];
    }

    /// <summary>
    ///     Orders two facet polygons by their corner index sequences (lexicographically, shorter first on a tie),
    ///     which is a total order because distinct facets never have the same corners.
    /// </summary>
    /// <param name="left">The first polygon.</param>
    /// <param name="right">The second polygon.</param>
    /// <returns>A negative, zero or positive value.</returns>
    private static int ComparePolygons(int[] left, int[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            var result = left[i].CompareTo(right[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    /// <summary>Finds the union-find root of a face slot, halving the path as it goes.</summary>
    /// <param name="parent">The union-find parent table.</param>
    /// <param name="slot">The face slot.</param>
    /// <returns>The root slot.</returns>
    private static int Find(int[] parent, int slot)
    {
        while (parent[slot] != slot)
        {
            parent[slot] = parent[parent[slot]];
            slot = parent[slot];
        }

        return slot;
    }

    /// <summary>Joins the sets of two face slots.</summary>
    /// <param name="parent">The union-find parent table.</param>
    /// <param name="first">The first slot.</param>
    /// <param name="second">The second slot.</param>
    private static void Join(int[] parent, int first, int second)
    {
        var a = Find(parent, first);
        var b = Find(parent, second);
        if (a != b)
        {
            parent[Math.Max(a, b)] = Math.Min(a, b);
        }
    }

    /// <summary>Packs an ordered vertex pair into a single key.</summary>
    /// <param name="tail">The first vertex index.</param>
    /// <param name="head">The second vertex index.</param>
    /// <returns>A 64-bit key unique to the ordered pair.</returns>
    private static long EdgeKey(int tail, int head) => ((long)tail << 32) | (uint)head;
}
