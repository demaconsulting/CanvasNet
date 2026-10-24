using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     An immutable, validated two-dimensional region bounded by simple polygon contours, with holes
///     expressed by nesting (the even-odd rule).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a validated type.</b> Extrusion and triangulation are only well defined for non-degenerate,
///         non-self-intersecting input. Instead of leaving malformed polygons as undefined behavior (which
///         would surface as cracked or overlapping meshes far from the cause), the constructor checks every
///         contour exactly (see <see cref="PolygonPredicates"/>) and throws <see cref="ArgumentException"/>,
///         so a successfully constructed region guarantees closed, manifold, valid constructive-solid-geometry
///         operands downstream.
///     </para>
///     <para>
///         <b>Even-odd rule.</b> The depth of a contour is the number of other contours containing it. An even
///         depth bounds solid material and is normalized to counter-clockwise winding; an odd depth is a hole
///         and is normalized clockwise, regardless of the winding supplied. An island inside a hole is solid
///         again. In every normalized contour the solid lies on the left of the direction of travel.
///     </para>
///     <para>
///         <b>Accepted and normalized.</b> A closing duplicate of the first point and consecutive duplicate
///         points are dropped; collinear points are kept (they stay vertices of every triangulation).
///         <b>Rejected</b> (<see cref="ArgumentException"/>): non-finite coordinates, fewer than three distinct
///         vertices, zero-area (all collinear) contours, contours that cross or touch themselves (including
///         a repeated non-consecutive point and edges that fold back onto each other), and contours that cross
///         or touch one another (a shared point or a vertex lying on another contour's edge), because touching
///         contours would create non-manifold vertices in an extrusion, and regions whose area is not a finite
///         number because the (finite) coordinates are extreme (beyond about 1e154).
///     </para>
///     <para>
///         A region may have no contours at all (<see cref="Empty"/>); it then has no area and extrudes to an
///         empty mesh. The region stores private copies of its input, so it never changes after construction
///         and is thread-safe.
///     </para>
/// </remarks>
public sealed class PolygonRegion
{
    /// <summary>The normalized contours (owned; read-only wrappers handed to callers).</summary>
    private readonly IReadOnlyList<Vector2D>[] _contours;

    /// <summary>All contour vertices concatenated in contour order (owned; never exposed mutably).</summary>
    private readonly Vector2D[] _vertices;

    /// <summary>Read-only wrapper over the contour list handed to callers.</summary>
    private readonly IReadOnlyList<IReadOnlyList<Vector2D>> _contourView;

    /// <summary>Read-only wrapper over the flattened vertex list handed to callers.</summary>
    private readonly IReadOnlyList<Vector2D> _vertexView;

    /// <summary>Start, count, depth and parent of every contour (owned; internal consumers only).</summary>
    private readonly ContourInfo[] _table;

    /// <summary>
    ///     Creates a region from contours, normalizing and validating them.
    /// </summary>
    /// <param name="contours">
    ///     The contours in any winding order; each is a list of at least three distinct points that is
    ///     implicitly closed. A contour is an outer boundary or a hole depending only on its nesting.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contours"/> or one of the contours is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a point is not finite, a contour has fewer than three distinct vertices or zero area,
    ///     a contour intersects or touches itself, two contours intersect or touch each other, or the
    ///     coordinates are so large that the area of the region is not a finite number (the region is
    ///     rejected rather than reporting infinity or NaN). The message names the offending contour (and
    ///     point or edge) where there is one.
    /// </exception>
    public PolygonRegion(IEnumerable<IReadOnlyList<Vector2D>> contours)
    {
        ArgumentNullException.ThrowIfNull(contours);

        // Clean and validate every contour on its own, then validate the contours against each other
        var cleaned = CleanContours(contours);
        CheckSimplicity(cleaned);
        var nesting = ClassifyNesting(cleaned);

        // Normalize the winding by nesting parity (solid on the left) and flatten into private storage
        var total = 0;
        foreach (var contour in cleaned)
        {
            total += contour.Length;
        }

        _vertices = new Vector2D[total];
        _table = new ContourInfo[cleaned.Count];
        _contours = new IReadOnlyList<Vector2D>[cleaned.Count];
        var area = new AreaSum();
        var start = 0;
        for (var c = 0; c < cleaned.Count; c++)
        {
            var points = cleaned[c];
            var solid = (nesting[c].Depth & 1) == 0;
            if (IsCounterClockwise(points) != solid)
            {
                Reverse(points);
            }

            Array.Copy(points, 0, _vertices, start, points.Length);
            _table[c] = new ContourInfo(start, points.Length, nesting[c].Depth, nesting[c].Parent);
            _contours[c] = Array.AsReadOnly(points);
            var magnitude = Math.Abs(SignedArea(points));
            area.Add(solid ? magnitude : -magnitude);
            start += points.Length;
        }

        // Finite but extreme coordinates (about 1e154 and beyond) overflow the shoelace terms; such a region
        // cannot report a usable area or volume, so it is rejected instead of carrying infinity or NaN
        if (!double.IsFinite(area.Total))
        {
            throw new ArgumentException("The coordinates are too large: the area of the region is not a finite number.", nameof(contours));
        }

        Area = Math.Max(0.0, area.Total);
        _contourView = Array.AsReadOnly(_contours);
        _vertexView = Array.AsReadOnly(_vertices);
    }

    /// <summary>Gets a shared region without any contours (zero area; extrudes to an empty mesh).</summary>
    public static PolygonRegion Empty { get; } = new(Array.Empty<IReadOnlyList<Vector2D>>());

    /// <summary>
    ///     Gets the normalized contours in input order: solid boundaries counter-clockwise, holes
    ///     clockwise, each starting at its original first point, without closing or consecutive duplicates.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Vector2D>> Contours => _contourView;

    /// <summary>
    ///     Gets all contour vertices concatenated in contour order. This is the index space of the
    ///     triangles returned by <see cref="PolygonTriangulator.Triangulate"/>.
    /// </summary>
    public IReadOnlyList<Vector2D> Vertices => _vertexView;

    /// <summary>Gets the total number of vertices over all contours.</summary>
    public int VertexCount => _vertices.Length;

    /// <summary>Gets a value indicating whether the region has no contours.</summary>
    public bool IsEmpty => _contours.Length == 0;

    /// <summary>
    ///     Gets the area of the region: solid contours minus holes plus islands (the sum of the signed
    ///     areas of the normalized contours); finite and never negative.
    /// </summary>
    public double Area { get; }

    /// <summary>Gets the flattened vertices as a span (no copy; internal so callers cannot mutate).</summary>
    internal ReadOnlySpan<Vector2D> VertexSpan => _vertices;

    /// <summary>Gets the contour table (no copy; internal consumers must not modify it).</summary>
    internal ReadOnlySpan<ContourInfo> ContourTable => _table;

    /// <summary>
    ///     Validates each contour, drops duplicate points and returns private arrays.
    /// </summary>
    /// <param name="contours">The caller's contours.</param>
    /// <returns>The cleaned contours (each at least three distinct, non-collinear-only points).</returns>
    private static List<Vector2D[]> CleanContours(IEnumerable<IReadOnlyList<Vector2D>> contours)
    {
        var result = new List<Vector2D[]>();
        var index = 0;
        foreach (var contour in contours)
        {
            if (contour is null)
            {
                throw new ArgumentNullException(nameof(contours), $"Contour {index} must not be null.");
            }

            result.Add(CleanContour(contour, index));
            index++;
        }

        return result;
    }

    /// <summary>
    ///     Cleans one contour: rejects non-finite points, removes consecutive and closing duplicates and
    ///     rejects contours without area.
    /// </summary>
    /// <param name="source">The caller's contour.</param>
    /// <param name="index">The contour's index, for messages.</param>
    /// <returns>A private array of the cleaned points.</returns>
    private static Vector2D[] CleanContour(IReadOnlyList<Vector2D> source, int index)
    {
        // Snapshot the points, rejecting non-finite values and consecutive duplicates
        var points = new List<Vector2D>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var point = source[i];
            if (!point.IsFinite)
            {
                throw new ArgumentException($"Contour {index} point {i} is not finite.");
            }

            if (points.Count == 0 || points[^1] != point)
            {
                points.Add(point);
            }
        }

        // An explicit closing point repeats the first one
        while (points.Count > 1 && points[^1] == points[0])
        {
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 3)
        {
            throw new ArgumentException($"Contour {index} needs at least three distinct points.");
        }

        // All points collinear with the first edge means zero area (this also catches a folded-back path)
        var hasArea = false;
        for (var i = 2; i < points.Count && !hasArea; i++)
        {
            hasArea = PolygonPredicates.Orient2D(points[0], points[1], points[i]) != 0;
        }

        if (!hasArea)
        {
            throw new ArgumentException($"Contour {index} has zero area (all points are collinear).");
        }

        return points.ToArray();
    }

    /// <summary>
    ///     Verifies that no edge of any contour intersects or touches another edge, except for the shared
    ///     end of two consecutive edges of the same contour, using a sweep along X.
    /// </summary>
    /// <param name="contours">The cleaned contours.</param>
    /// <exception cref="ArgumentException">Thrown when two edges conflict.</exception>
    private static void CheckSimplicity(List<Vector2D[]> contours)
    {
        // Collect every edge with its X extent; sorting by the minimum X bounds the pairs to compare
        var edges = new List<Edge>();
        for (var c = 0; c < contours.Count; c++)
        {
            var points = contours[c];
            for (var i = 0; i < points.Length; i++)
            {
                edges.Add(new Edge(points[i], points[(i + 1) % points.Length], c, i, points.Length));
            }
        }

        edges.Sort((left, right) => left.MinX.CompareTo(right.MinX));

        // Keep only edges whose X range still reaches the current edge (touching ranges are kept)
        var active = new List<Edge>();
        foreach (var edge in edges)
        {
            active.RemoveAll(other => other.MaxX < edge.MinX);
            var conflict = active.FindIndex(other => EdgesConflict(other, edge));
            if (conflict >= 0)
            {
                throw new ArgumentException(
                    $"Contour {active[conflict].Contour} edge {active[conflict].Index} intersects or touches contour {edge.Contour} edge {edge.Index}.");
            }

            active.Add(edge);
        }
    }

    /// <summary>
    ///     Tests whether two edges are not allowed to coexist in a valid region.
    /// </summary>
    /// <param name="first">The first edge.</param>
    /// <param name="second">The second edge.</param>
    /// <returns><see langword="true"/> when they share a point other than a legitimate common corner.</returns>
    private static bool EdgesConflict(Edge first, Edge second)
    {
        // Cheap rejection on the Y extent (the X extents already overlap by construction of the sweep)
        if (first.MaxY < second.MinY || second.MaxY < first.MinY)
        {
            return false;
        }

        if (first.Contour == second.Contour)
        {
            // Consecutive edges legitimately share one corner; only a fold back onto each other conflicts
            if (first.IsFollowedBy(second))
            {
                return FoldsBack(first.Start, first.End, second.End);
            }

            if (second.IsFollowedBy(first))
            {
                return FoldsBack(second.Start, second.End, first.End);
            }
        }

        return PolygonPredicates.SegmentsIntersect(first.Start, first.End, second.Start, second.End);
    }

    /// <summary>
    ///     Tests whether two consecutive edges <c>previous -&gt; corner</c> and <c>corner -&gt; next</c>
    ///     overlap each other beyond the corner (a spike), which happens only when the three points are
    ///     collinear and the corner is not between the other two.
    /// </summary>
    /// <param name="previous">The start of the first edge.</param>
    /// <param name="corner">The shared corner.</param>
    /// <param name="next">The end of the second edge.</param>
    /// <returns><see langword="true"/> when the edges overlap.</returns>
    private static bool FoldsBack(Vector2D previous, Vector2D corner, Vector2D next) =>
        PolygonPredicates.Orient2D(previous, corner, next) == 0 && !PolygonPredicates.InBoundingBox(previous, next, corner);

    /// <summary>
    ///     Computes the nesting depth and innermost parent of every contour.
    /// </summary>
    /// <param name="contours">The validated contours (no two contours touch).</param>
    /// <returns>The depth and parent index per contour.</returns>
    private static (int Depth, int Parent)[] ClassifyNesting(List<Vector2D[]> contours)
    {
        // Contours never touch, so one vertex decides whether a contour lies inside another
        var count = contours.Count;
        var bounds = new (double MinX, double MinY, double MaxX, double MaxY)[count];
        for (var c = 0; c < count; c++)
        {
            bounds[c] = BoundsOf(contours[c]);
        }

        var containers = new List<int>[count];
        for (var i = 0; i < count; i++)
        {
            containers[i] = [];
            for (var j = 0; j < count; j++)
            {
                if (i != j && Contains(contours[j], bounds[j], contours[i][0]))
                {
                    containers[i].Add(j);
                }
            }
        }

        // The parent is the containing contour that is itself nested deepest
        var result = new (int Depth, int Parent)[count];
        for (var i = 0; i < count; i++)
        {
            var parent = -1;
            foreach (var j in containers[i])
            {
                if (parent < 0 || containers[j].Count > containers[parent].Count)
                {
                    parent = j;
                }
            }

            result[i] = (containers[i].Count, parent);
        }

        return result;
    }

    /// <summary>Computes the axis-aligned bounds of a contour.</summary>
    /// <param name="points">The contour points.</param>
    /// <returns>The minimum and maximum coordinates.</returns>
    private static (double MinX, double MinY, double MaxX, double MaxY) BoundsOf(Vector2D[] points)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var point in points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>Tests whether a point (not on the boundary) lies inside a contour, using its bounds as a prefilter.</summary>
    /// <param name="contour">The containing candidate.</param>
    /// <param name="bounds">The candidate's bounds.</param>
    /// <param name="point">The point to classify.</param>
    /// <returns><see langword="true"/> when the point is inside the contour.</returns>
    private static bool Contains(Vector2D[] contour, (double MinX, double MinY, double MaxX, double MaxY) bounds, Vector2D point) =>
        point.X >= bounds.MinX && point.X <= bounds.MaxX && point.Y >= bounds.MinY && point.Y <= bounds.MaxY &&
        PolygonPredicates.PointInPolygon(point, contour);

    /// <summary>
    ///     Determines the winding of a simple contour exactly, from the turn at its lowest-leftmost vertex
    ///     (a strictly convex corner of the hull, which can never be collinear for a valid contour).
    /// </summary>
    /// <param name="points">The simple contour.</param>
    /// <returns><see langword="true"/> for counter-clockwise winding.</returns>
    private static bool IsCounterClockwise(Vector2D[] points)
    {
        var lowest = 0;
        for (var i = 1; i < points.Length; i++)
        {
            var byY = points[i].Y.CompareTo(points[lowest].Y);
            if (byY < 0 || (byY == 0 && points[i].X < points[lowest].X))
            {
                lowest = i;
            }
        }

        var previous = points[(lowest + points.Length - 1) % points.Length];
        var next = points[(lowest + 1) % points.Length];
        return PolygonPredicates.Orient2D(previous, points[lowest], next) > 0;
    }

    /// <summary>Reverses a contour in place while keeping its first vertex first.</summary>
    /// <param name="points">The contour to reverse.</param>
    private static void Reverse(Vector2D[] points) => Array.Reverse(points, 1, points.Length - 1);

    /// <summary>
    ///     Computes the signed shoelace area of a contour (positive when counter-clockwise), relative to
    ///     its first vertex for precision and with compensated summation.
    /// </summary>
    /// <param name="points">The contour.</param>
    /// <returns>The signed area.</returns>
    private static double SignedArea(Vector2D[] points)
    {
        var sum = new AreaSum();
        var origin = points[0];
        for (var i = 1; i < points.Length - 1; i++)
        {
            sum.Add(Vector2D.Cross(points[i] - origin, points[i + 1] - origin) * 0.5);
        }

        return sum.Total;
    }

    /// <summary>A compensated running sum, so areas of many thin pieces do not lose precision.</summary>
    private struct AreaSum
    {
        /// <summary>The running sum.</summary>
        private double _sum;

        /// <summary>The accumulated rounding error.</summary>
        private double _error;

        /// <summary>Gets the compensated total.</summary>
        public readonly double Total => _sum + _error;

        /// <summary>Adds a value.</summary>
        /// <param name="value">The value to add.</param>
        public void Add(double value)
        {
            var total = _sum + value;
            _error += Math.Abs(_sum) >= Math.Abs(value) ? (_sum - total) + value : (value - total) + _sum;
            _sum = total;
        }
    }

    /// <summary>One contour edge with its extent, used by the simplicity sweep.</summary>
    private readonly struct Edge
    {
        /// <summary>Initializes a new instance of the <see cref="Edge"/> struct.</summary>
        /// <param name="start">The edge start.</param>
        /// <param name="end">The edge end.</param>
        /// <param name="contour">The contour index.</param>
        /// <param name="index">The edge index within its contour.</param>
        /// <param name="count">The number of edges of the contour.</param>
        public Edge(Vector2D start, Vector2D end, int contour, int index, int count)
        {
            Start = start;
            End = end;
            Contour = contour;
            Index = index;
            Count = count;
        }

        /// <summary>Gets the edge start.</summary>
        public Vector2D Start { get; }

        /// <summary>Gets the edge end.</summary>
        public Vector2D End { get; }

        /// <summary>Gets the contour index.</summary>
        public int Contour { get; }

        /// <summary>Gets the edge index within its contour.</summary>
        public int Index { get; }

        /// <summary>Gets the number of edges of the contour.</summary>
        public int Count { get; }

        /// <summary>Gets the smallest X of the edge.</summary>
        public double MinX => Math.Min(Start.X, End.X);

        /// <summary>Gets the largest X of the edge.</summary>
        public double MaxX => Math.Max(Start.X, End.X);

        /// <summary>Gets the smallest Y of the edge.</summary>
        public double MinY => Math.Min(Start.Y, End.Y);

        /// <summary>Gets the largest Y of the edge.</summary>
        public double MaxY => Math.Max(Start.Y, End.Y);

        /// <summary>Tests whether <paramref name="other"/> is the next edge of the same contour.</summary>
        /// <param name="other">The candidate successor (same contour).</param>
        /// <returns><see langword="true"/> when <paramref name="other"/> starts where this edge ends.</returns>
        public bool IsFollowedBy(Edge other) => other.Index == (Index + 1) % Count;
    }
}
