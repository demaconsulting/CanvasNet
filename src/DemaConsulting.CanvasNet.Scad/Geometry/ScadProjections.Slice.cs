using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The cross-section of a mesh by a plane, with the symbolic shift of the plane that the classification needs.
/// </content>
/// <remarks>
///     <para>
///         <b>Section by a shifted plane.</b> The section of a closed mesh by the plane <c>n . p = d</c> shifted by an
///         infinitesimal amount along its normal is computed without a number for the shift: a vertex that lies on the
///         plane (within the tolerance of <see cref="State"/>) is treated as being <em>above</em> the plane when the plane
///         is shifted down and <em>below</em> it when the plane is shifted up. Every other vertex is classified by the
///         sign of its distance. Each triangle with vertices on both sides then contributes one directed segment, from
///         the edge that leaves the upper side to the edge that enters it, which orients every contour consistently.
///         The points of the contours are keyed by the cut edge, or by the vertex when the edge has an end on the plane
///         (the point is then that vertex), never by coordinates, so the contours close exactly whatever the rounding.
///     </para>
///     <para>
///         The contours of one mesh are the boundary of its section: they become one <see cref="PolygonRegion"/> (the
///         even-odd rule makes holes and islands). A set of segments that does not balance at a point means that the mesh
///         is not closed (<see cref="ScadFeatureIds.ProjectionOpenMesh"/>); a region the validator rejects is reported as
///         <see cref="ScadFeatureIds.GeometryRejected"/>. Both omit the whole projection, because dropping an operand of
///         a boolean would silently change its meaning.
///     </para>
/// </remarks>
internal static partial class ScadProjections
{
    /// <summary>
    ///     A plane <c>Normal . p = Offset</c> with a unit normal that points upward.
    /// </summary>
    /// <param name="Normal">The unit normal.</param>
    /// <param name="Offset">The offset.</param>
    private readonly record struct CutPlane(Vector3D Normal, double Offset)
    {
        /// <summary>
        ///     Gets the key that identifies the plane up to the tolerance.
        /// </summary>
        /// <param name="tolerance">The plane tolerance.</param>
        /// <returns>The key.</returns>
        public PlaneKey Key(double tolerance) => new(
            (long)Math.Round(Normal.X * 1e9),
            (long)Math.Round(Normal.Y * 1e9),
            (long)Math.Round(Normal.Z * 1e9),
            (long)Math.Round(Offset / (10.0 * tolerance)));

        /// <summary>
        ///     Gets the signed distance of a point from the plane.
        /// </summary>
        /// <param name="p">The point.</param>
        /// <returns>The distance; positive on the side the normal points to.</returns>
        public double Distance(Vector3D p) => Vector3D.Dot(Normal, p) - Offset;
    }

    /// <summary>
    ///     Gets the cross-section of a mesh node by a plane, shifted down or up by an infinitesimal amount.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="index">The index of the mesh node.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="down">Whether the plane is shifted down (a vertex on the plane counts as above it) or up.</param>
    /// <param name="state">The shared state.</param>
    /// <returns>The section with its bounds; <see langword="null"/> when it is empty or the projection failed.</returns>
    private static SectionEntry? SectionRegion(SolidTree tree, int index, CutPlane plane, bool down, State state)
    {
        var key = (index, plane.Key(state.Tolerance), down);
        if (state.Sections.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var mesh = tree.Nodes[index].Mesh!;
        SectionEntry? entry = null;

        // A mesh that does not reach the plane has no section; every other slice is charged before it runs
        if (Reaches(mesh.Bounds, plane, state.Tolerance) && state.Charge(mesh.TriangleCount + mesh.Vertices.Length)
            && SliceMesh(mesh, plane, down, state) is { } region)
        {
            entry = new SectionEntry(region);
        }

        state.Sections[key] = entry;
        return entry;
    }

    /// <summary>
    ///     Tells whether a box reaches a plane (touching counts).
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="tolerance">The plane tolerance.</param>
    /// <returns><see langword="false"/> when the whole box is strictly on one side of the plane.</returns>
    private static bool Reaches(BoundingBox3D box, CutPlane plane, double tolerance)
    {
        if (box.IsEmpty)
        {
            return false;
        }

        var n = plane.Normal;
        var (lowX, highX) = Range(n.X, box.Min.X, box.Max.X);
        var (lowY, highY) = Range(n.Y, box.Min.Y, box.Max.Y);
        var (lowZ, highZ) = Range(n.Z, box.Min.Z, box.Max.Z);
        var low = lowX + lowY + lowZ;
        var high = highX + highY + highZ;
        return high - plane.Offset >= -tolerance && low - plane.Offset <= tolerance;
    }

    /// <summary>
    ///     Gets the range of <c>coefficient * x</c> over an interval of <c>x</c>.
    /// </summary>
    /// <param name="coefficient">The coefficient.</param>
    /// <param name="min">The smallest <c>x</c>.</param>
    /// <param name="max">The largest <c>x</c>.</param>
    /// <returns>The smallest and the largest product.</returns>
    private static (double Low, double High) Range(double coefficient, double min, double max)
    {
        var a = coefficient * min;
        var b = coefficient * max;
        return (Math.Min(a, b), Math.Max(a, b));
    }

    /// <summary>
    ///     Classifies a vertex against the plane that is shifted by an infinitesimal amount.
    /// </summary>
    /// <param name="distance">The signed distance of the vertex from the (unshifted) plane.</param>
    /// <param name="onPlane">Whether the vertex is on the plane within the tolerance.</param>
    /// <param name="down">Whether the plane is shifted down.</param>
    /// <returns>1 when the vertex is above the shifted plane, -1 when it is below.</returns>
    private static sbyte Side(double distance, bool onPlane, bool down)
    {
        if (onPlane)
        {
            // Shifting the plane down puts a vertex that was on it above the plane, shifting it up puts it below
            return down ? (sbyte)1 : (sbyte)-1;
        }

        return distance > 0.0 ? (sbyte)1 : (sbyte)-1;
    }

    /// <summary>
    ///     Slices a mesh with the plane.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="down">Whether the plane is shifted down or up.</param>
    /// <param name="state">The shared state; receives a failure.</param>
    /// <returns>The section, or <see langword="null"/> when it is empty or a failure was recorded.</returns>
    private static PolygonRegion? SliceMesh(FrameMesh mesh, CutPlane plane, bool down, State state)
    {
        var count = mesh.Vertices.Length;
        var distance = new double[count];
        var sign = new sbyte[count];
        var positive = false;
        var negative = false;
        for (var i = 0; i < count; i++)
        {
            var d = plane.Distance(mesh.Vertices[i]);
            distance[i] = d;
            var onPlane = Math.Abs(d) <= state.Tolerance;
            state.NearPlane |= onPlane;
            sign[i] = Side(d, onPlane, down);
            positive |= sign[i] > 0;
            negative |= sign[i] < 0;
        }

        if (!positive || !negative)
        {
            return null;
        }

        var section = new Section(mesh, distance, state.Tolerance);
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = Indices(mesh, t);
            if (sign[a] == sign[b] && sign[b] == sign[c])
            {
                continue;
            }

            section.AddTriangle(a, b, c, sign);
        }

        return section.ToRegion(state);
    }

    /// <summary>
    ///     The segments of the cross-section of one mesh, keyed by cut edge, and their chaining into a region.
    /// </summary>
    private sealed class Section
    {
        /// <summary>The mesh.</summary>
        private readonly FrameMesh _mesh;

        /// <summary>The signed distance of every vertex from the plane.</summary>
        private readonly double[] _distance;

        /// <summary>The distance below which a vertex is on the plane.</summary>
        private readonly double _tolerance;

        /// <summary>The points of the section in the XY plane.</summary>
        private readonly List<Vector2D> _points = [];

        /// <summary>The point of a vertex that lies on the plane, by vertex.</summary>
        private readonly Dictionary<int, int> _vertexPoints = [];

        /// <summary>The point where an edge crosses the plane, by edge.</summary>
        private readonly Dictionary<long, int> _edgePoints = [];

        /// <summary>The directed segments between points.</summary>
        private readonly List<(int From, int To)> _segments = [];

        /// <summary>
        ///     Initializes a section.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="distance">The signed distance of every vertex from the plane.</param>
        /// <param name="tolerance">The distance below which a vertex is on the plane.</param>
        public Section(FrameMesh mesh, double[] distance, double tolerance)
        {
            _mesh = mesh;
            _distance = distance;
            _tolerance = tolerance;
        }

        /// <summary>
        ///     Adds the segment that a triangle with vertices on both sides of the plane contributes.
        /// </summary>
        /// <param name="a">The first vertex.</param>
        /// <param name="b">The second vertex.</param>
        /// <param name="c">The third vertex.</param>
        /// <param name="sign">The side of every vertex: 1 above the (shifted) plane, -1 below.</param>
        public void AddTriangle(int a, int b, int c, sbyte[] sign)
        {
            var from = -1;
            var to = -1;
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                if (sign[u] == sign[v])
                {
                    continue;
                }

                // Walking the winding order, the edge that leaves the upper side starts the segment and the one that enters it ends it
                var point = PointOn(u, v);
                if (sign[u] > 0)
                {
                    from = point;
                }
                else
                {
                    to = point;
                }
            }

            if (from >= 0 && to >= 0 && from != to)
            {
                _segments.Add((from, to));
            }
        }

        /// <summary>
        ///     Chains the segments into contours and validates them as one region.
        /// </summary>
        /// <param name="state">The shared state; receives a failure.</param>
        /// <returns>The region, or <see langword="null"/> when the section is empty or a failure was recorded.</returns>
        public PolygonRegion? ToRegion(State state)
        {
            var outgoing = new List<int>[_points.Count];
            var balance = new int[_points.Count];
            foreach (var (from, to) in _segments)
            {
                (outgoing[from] ??= []).Add(to);
                balance[from]++;
                balance[to]--;
            }

            if (balance.Any(b => b != 0))
            {
                state.Fail(
                    ScadFeatureIds.ProjectionOpenMesh,
                    "projection: the cross-section of a mesh does not close, so the mesh is not a closed solid; the projection is omitted");
                return null;
            }

            var contours = new List<IReadOnlyList<Vector2D>>();
            for (var start = 0; start < outgoing.Length; start++)
            {
                while (outgoing[start] is { Count: > 0 })
                {
                    var contour = Walk(outgoing, start);
                    if (contour.Count >= 3)
                    {
                        contours.Add(contour);
                    }
                }
            }

            return contours.Count == 0 ? null : Validate(contours, state);
        }

        /// <summary>
        ///     Follows unused segments from a point until the walk returns to it.
        /// </summary>
        /// <param name="outgoing">The unused segments by start point.</param>
        /// <param name="start">The start point.</param>
        /// <returns>The points of the contour.</returns>
        private List<Vector2D> Walk(List<int>[] outgoing, int start)
        {
            var contour = new List<Vector2D>();
            var current = start;
            while (true)
            {
                contour.Add(_points[current]);
                var list = outgoing[current];
                var next = list[^1];
                list.RemoveAt(list.Count - 1);
                if (next == start)
                {
                    return contour;
                }

                current = next;
            }
        }

        /// <summary>
        ///     Builds the region from the contours, recording a failure when the validator rejects it.
        /// </summary>
        /// <param name="contours">The contours.</param>
        /// <param name="state">The shared state.</param>
        /// <returns>The region, or <see langword="null"/> after a failure.</returns>
        private static PolygonRegion? Validate(List<IReadOnlyList<Vector2D>> contours, State state)
        {
            try
            {
                return new PolygonRegion(contours);
            }
            catch (ArgumentException)
            {
                state.Fail(
                    ScadFeatureIds.GeometryRejected,
                    "projection: a cross-section of the solid is not a valid planar shape (its contours touch or cross each other); the projection is omitted");
                return null;
            }
        }

        /// <summary>
        ///     Gets the index of the point where an edge with ends on both sides of the plane crosses it.
        /// </summary>
        /// <param name="u">The first vertex.</param>
        /// <param name="v">The second vertex.</param>
        /// <returns>The index of the point: the vertex that is on the plane, else the point on the edge.</returns>
        private int PointOn(int u, int v)
        {
            if (Math.Abs(_distance[u]) <= _tolerance)
            {
                return VertexPoint(u);
            }

            if (Math.Abs(_distance[v]) <= _tolerance)
            {
                return VertexPoint(v);
            }

            var key = UndirectedKey(u, v);
            if (_edgePoints.TryGetValue(key, out var existing))
            {
                return existing;
            }

            // Interpolate from the lower index so that both triangles of an edge compute the very same point
            var (low, high) = u < v ? (u, v) : (v, u);
            var t = _distance[low] / (_distance[low] - _distance[high]);
            var p = _mesh.Vertices[low] + ((_mesh.Vertices[high] - _mesh.Vertices[low]) * t);
            _points.Add(Flat(p));
            _edgePoints[key] = _points.Count - 1;
            return _points.Count - 1;
        }

        /// <summary>
        ///     Gets the index of the point of a vertex that lies on the plane.
        /// </summary>
        /// <param name="vertex">The vertex.</param>
        /// <returns>The index of the point.</returns>
        private int VertexPoint(int vertex)
        {
            if (_vertexPoints.TryGetValue(vertex, out var existing))
            {
                return existing;
            }

            _points.Add(Flat(_mesh.Vertices[vertex]));
            _vertexPoints[vertex] = _points.Count - 1;
            return _points.Count - 1;
        }
    }
}
