using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The flattened solid tree of a projection and the shared state of its computations.
/// </content>
internal static partial class ScadProjections
{
    /// <summary>The part of the extent of a mesh below which two distances coincide.</summary>
    private const double DistanceFactor = 1e-9;

    /// <summary>The part of the squared extent of a mesh below which a doubled XY area counts as zero.</summary>
    private const double AreaFactor = 1e-12;

    /// <summary>The number of spacings of the doubles that the rounding errors of a dot or cross product can add up to.</summary>
    private const double RoundingMargin = 16.0;

    /// <summary>
    ///     The kind of a node of the flattened solid tree.
    /// </summary>
    private enum NodeKind
    {
        /// <summary>A mesh.</summary>
        Mesh,

        /// <summary>A union of the children.</summary>
        Union,

        /// <summary>A difference: the first child minus the others.</summary>
        Difference,

        /// <summary>An intersection of the children.</summary>
        Intersection,
    }

    /// <summary>
    ///     A mesh in the frame of the projection: its vertices transformed and welded (referenced ones only), with the
    ///     winding restored after a mirror.
    /// </summary>
    private sealed class FrameMesh
    {
        /// <summary>
        ///     Initializes a mesh.
        /// </summary>
        /// <param name="vertices">The welded vertices in the frame of the projection.</param>
        /// <param name="indices">The triangles as indices into <paramref name="vertices"/>, three per triangle, outward wound when the mesh is closed.</param>
        /// <param name="bounds">The bounds of the vertices.</param>
        /// <param name="isClosedOutward">Whether the mesh is a closed, outward-wound solid.</param>
        public FrameMesh(Vector3D[] vertices, int[] indices, BoundingBox3D bounds, bool isClosedOutward)
        {
            Vertices = vertices;
            TriangleIndices = indices;
            Bounds = bounds;
            IsClosedOutward = isClosedOutward;
            if (!bounds.IsEmpty)
            {
                Extent = Math.Max(bounds.Max.X - bounds.Min.X, Math.Max(bounds.Max.Y - bounds.Min.Y, bounds.Max.Z - bounds.Min.Z));
                MaxCoordinate = Math.Max(MaxAbs(bounds.Min), MaxAbs(bounds.Max));
            }

            AreaEpsilon = (AreaFactor * Extent * Extent) + (RoundingMargin * CoordinateUlp(MaxCoordinate) * Extent);

            // The tolerance follows the size of this mesh and the rounding error of its coordinates, never the distance from
            // the origin or the size of any other mesh
            Tolerance = (DistanceFactor * Extent) + (RoundingMargin * CoordinateUlp(MaxCoordinate));
            if (Tolerance <= 0.0)
            {
                // Only an empty mesh has no extent; any positive value serves
                Tolerance = DistanceFactor;
            }
        }

        /// <summary>Gets the welded vertices.</summary>
        public Vector3D[] Vertices { get; }

        /// <summary>Gets the triangle indices, three per triangle.</summary>
        public int[] TriangleIndices { get; }

        /// <summary>Gets the bounds of the vertices.</summary>
        public BoundingBox3D Bounds { get; }

        /// <summary>Gets the largest side of the bounds; it does not change when the mesh is translated.</summary>
        public double Extent { get; }

        /// <summary>Gets the largest absolute coordinate of any vertex (it sets the rounding error of the coordinates).</summary>
        public double MaxCoordinate { get; }

        /// <summary>
        ///     Gets the doubled XY area below which a triangle of this mesh counts as vertical: a part of the squared extent
        ///     plus the rounding error of the coordinates times the extent.
        /// </summary>
        public double AreaEpsilon { get; }

        /// <summary>
        ///     Gets the distance from a plane (or in the XY plane) below which a vertex of this mesh counts as on it: a part of
        ///     the extent of this mesh plus the rounding error of its coordinates, so it depends neither on the translation nor
        ///     on any other mesh.
        /// </summary>
        public double Tolerance { get; }

        /// <summary>Gets a value indicating whether the mesh is a closed, outward-wound solid (only its upward faces then matter for a shadow).</summary>
        public bool IsClosedOutward { get; }

        /// <summary>Gets the number of triangles.</summary>
        public int TriangleCount => TriangleIndices.Length / 3;

        /// <summary>
        ///     Transforms a mesh into the frame of the projection.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="matrix">The matrix that takes the mesh into the frame.</param>
        /// <param name="context">The build context (cancellation is polled per triangle and per vertex).</param>
        /// <returns>The mesh in the frame.</returns>
        public static FrameMesh Create(Mesh mesh, Matrix4x4D matrix, ScadGeometryContext context)
        {
            var remap = new Dictionary<int, int>(Math.Min(mesh.VertexCount, mesh.TriangleIndices.Count));
            var welded = new Dictionary<Vector3D, int>();
            var vertices = new List<Vector3D>();
            var source = mesh.TriangleIndices;
            var indices = new List<int>(source.Count);
            var mirrored = matrix.Determinant < 0.0;
            var bounds = BoundingBox3D.Empty;
            for (var i = 0; i + 2 < source.Count; i += 3)
            {
                context.Tick();
                var a = Weld(mesh, matrix, source[i], remap, welded, vertices);
                var b = Weld(mesh, matrix, source[i + 1], remap, welded, vertices);
                var c = Weld(mesh, matrix, source[i + 2], remap, welded, vertices);
                if (a == b || b == c || a == c)
                {
                    continue;
                }

                indices.Add(a);
                indices.Add(mirrored ? c : b);
                indices.Add(mirrored ? b : c);
            }

            foreach (var vertex in vertices)
            {
                context.Tick();
                bounds = bounds.Union(vertex);
            }

            return new FrameMesh([.. vertices], [.. indices], bounds, mesh.IsClosed && mesh.SignedVolume > 0.0);
        }

        /// <summary>
        ///     Transforms one vertex into the frame and welds it with every vertex at exactly the same position.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="matrix">The matrix.</param>
        /// <param name="source">The index of the vertex in the mesh.</param>
        /// <param name="remap">The index among the welded vertices of each mesh vertex met so far (only referenced vertices have an entry).</param>
        /// <param name="welded">The welded vertices by position.</param>
        /// <param name="vertices">The welded vertices in order.</param>
        /// <returns>The index of the welded vertex.</returns>
        private static int Weld(Mesh mesh, Matrix4x4D matrix, int source, Dictionary<int, int> remap, Dictionary<Vector3D, int> welded, List<Vector3D> vertices)
        {
            if (remap.TryGetValue(source, out var known))
            {
                return known;
            }

            var point = matrix.TransformPoint(mesh.Vertices[source]);
            if (!welded.TryGetValue(point, out var id))
            {
                id = vertices.Count;
                vertices.Add(point);
                welded[point] = id;
            }

            remap[source] = id;
            return id;
        }
    }

    /// <summary>
    ///     A node of the flattened solid tree.
    /// </summary>
    private sealed class TreeNode
    {
        /// <summary>
        ///     Initializes a node.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="isNegative">Whether the node is subtracted an odd number of times.</param>
        /// <param name="mesh">The mesh of a mesh node.</param>
        public TreeNode(NodeKind kind, bool isNegative, FrameMesh? mesh = null)
        {
            Kind = kind;
            IsNegative = isNegative;
            Mesh = mesh;
        }

        /// <summary>Gets the kind.</summary>
        public NodeKind Kind { get; }

        /// <summary>Gets a value indicating whether the node is subtracted an odd number of times (it is a cutter of the solid).</summary>
        public bool IsNegative { get; }

        /// <summary>Gets the mesh of a mesh node.</summary>
        public FrameMesh? Mesh { get; }

        /// <summary>Gets the children in document order (the first child of a difference is the minuend).</summary>
        public List<int> Children { get; } = [];
    }

    /// <summary>
    ///     The solid children of a projection as a flat list of nodes; a child always has a larger index than its parent
    ///     and node 0 is the union of the children of the module.
    /// </summary>
    private sealed class SolidTree
    {
        /// <summary>Gets the nodes.</summary>
        public List<TreeNode> Nodes { get; } = [];

        /// <summary>Gets or sets a value indicating whether any node carries an appearance.</summary>
        public bool HasAppearance { get; set; }

        /// <summary>
        ///     Adds a node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="parent">The index of its parent, or -1 for the root.</param>
        /// <returns>The index of the node.</returns>
        public int Add(TreeNode node, int parent)
        {
            Nodes.Add(node);
            var index = Nodes.Count - 1;
            if (parent >= 0)
            {
                Nodes[parent].Children.Add(index);
            }

            return index;
        }
    }

    /// <summary>
    ///     A plane <c>n . p = d</c> with a normal that points upward (<c>n.Z &gt; 0</c>), identified by rounded values so
    ///     that coplanar faces share their cross-sections.
    /// </summary>
    /// <param name="Nx">The rounded X component of the normal in units of 1e-9.</param>
    /// <param name="Ny">The rounded Y component of the normal in units of 1e-9.</param>
    /// <param name="Nz">The rounded Z component of the normal in units of 1e-9.</param>
    /// <param name="D">The rounded offset in units of ten times the tolerance of the mesh that is sliced.</param>
    private readonly record struct PlaneKey(long Nx, long Ny, long Nz, long D);

    /// <summary>
    ///     The cross-section of a mesh with the bounds of its vertices in the XY plane.
    /// </summary>
    private sealed class SectionEntry
    {
        /// <summary>
        ///     Initializes an entry.
        /// </summary>
        /// <param name="region">The section.</param>
        public SectionEntry(PolygonRegion region)
        {
            Region = region;
            MinX = double.PositiveInfinity;
            MinY = double.PositiveInfinity;
            MaxX = double.NegativeInfinity;
            MaxY = double.NegativeInfinity;
            foreach (var v in region.Vertices)
            {
                MinX = Math.Min(MinX, v.X);
                MaxX = Math.Max(MaxX, v.X);
                MinY = Math.Min(MinY, v.Y);
                MaxY = Math.Max(MaxY, v.Y);
            }

            IsConvex = region.Contours.Count == 1 && IsConvexPolygon(region.Contours[0]);
        }

        /// <summary>Gets the section.</summary>
        public PolygonRegion Region { get; }

        /// <summary>Gets a value indicating whether the section is one convex contour (it can be clipped to a window exactly).</summary>
        public bool IsConvex { get; }

        /// <summary>Gets the smallest X of the vertices.</summary>
        public double MinX { get; }

        /// <summary>Gets the largest X of the vertices.</summary>
        public double MaxX { get; }

        /// <summary>Gets the smallest Y of the vertices.</summary>
        public double MinY { get; }

        /// <summary>Gets the largest Y of the vertices.</summary>
        public double MaxY { get; }

        /// <summary>
        ///     Tells whether a contour is convex (every turn goes the same way; straight vertices are allowed).
        /// </summary>
        /// <param name="contour">The contour.</param>
        /// <returns><see langword="true"/> when the contour is convex.</returns>
        private static bool IsConvexPolygon(IReadOnlyList<Vector2D> contour)
        {
            var sign = 0;
            for (var i = 0; i < contour.Count; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Count];
                var c = contour[(i + 2) % contour.Count];
                var turn = Vector2D.Cross(b - a, c - b);
                var direction = Math.Sign(turn);
                if (direction == 0)
                {
                    continue;
                }

                if (sign == 0)
                {
                    sign = direction;
                }
                else if (sign != direction)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    ///     The shared state of one projection: the work meter, the cross-section cache and the first failure. The
    ///     tolerances are not shared; every mesh has its own.
    /// </summary>
    private sealed class State
    {
        /// <summary>The cached cross-sections of the meshes by node, plane and shift direction.</summary>
        private readonly Dictionary<(int Node, PlaneKey Plane, bool Down), SectionEntry?> _sections = [];

        /// <summary>The work charged so far, in triangle visits.</summary>
        private long _work;

        /// <summary>
        ///     Initializes the state.
        /// </summary>
        /// <param name="context">The build context.</param>
        /// <param name="node">The instance.</param>
        public State(ScadGeometryContext context, ScadInstance node)
        {
            Context = context;
            Node = node;
        }

        /// <summary>Gets the build context.</summary>
        public ScadGeometryContext Context { get; }

        /// <summary>Gets the instance of the module.</summary>
        public ScadInstance Node { get; }

        /// <summary>Gets the first failure (the feature identifier and the message), or <see langword="null"/>.</summary>
        public (string Feature, string Message)? Failure { get; private set; }

        /// <summary>Gets a value indicating whether a mesh vertex was on a cut plane (the cut then needs both shifts).</summary>
        public bool NearPlane { get; set; }

        /// <summary>Gets or sets a value indicating whether a boolean subtree was classified (the bounds are then an upper bound).</summary>
        public bool Classified { get; set; }

        /// <summary>Gets or sets the number of leaf regions created so far.</summary>
        public long Leaves { get; set; }

        /// <summary>Gets the cache of the cross-sections.</summary>
        public Dictionary<(int Node, PlaneKey Plane, bool Down), SectionEntry?> Sections => _sections;

        /// <summary>Gets a value indicating whether a failure was recorded.</summary>
        public bool Failed => Failure is not null;

        /// <summary>
        ///     Records the first failure; later ones are ignored.
        /// </summary>
        /// <param name="feature">The feature identifier.</param>
        /// <param name="message">The message, without location.</param>
        public void Fail(string feature, string message) => Failure ??= (feature, message);

        /// <summary>
        ///     Charges work and records the too-complex failure when the ceiling is passed; optionally polls cancellation.
        /// </summary>
        /// <param name="cost">The triangle visits.</param>
        /// <param name="poll">
        ///     Whether to poll cancellation: a slice does (it has no counted loop of its own before the charge); a pass over
        ///     triangles does not, because its own loop polls on the counted interval.
        /// </param>
        /// <returns><see langword="true"/> when the work is still within <see cref="ScadGeometryContext.MaxProjectionWork"/>.</returns>
        public bool Charge(long cost, bool poll = true)
        {
            if (poll)
            {
                Context.Poll();
            }

            _work += cost;
            if (_work <= Context.MaxProjectionWork)
            {
                return true;
            }

            Fail(
                ScadFeatureIds.ProjectionTooComplex,
                $"projection: the projection needs more than the {Context.MaxProjectionWork} triangle visits one projection may do; the projection is omitted");
            return false;
        }

        /// <summary>
        ///     Makes a leaf from a region and counts it against the leaf budget.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <returns>The leaf.</returns>
        public ScadShape2D NewLeaf(PolygonRegion region)
        {
            Leaves++;
            return ScadShape2D.CreateLeaf(region, Node.Location);
        }

        /// <summary>
        ///     Verifies that the slabs the leaves created so far could need fit the triangle budget (8 triangles per leaf
        ///     is the cost of a triangle leaf; larger leaves are charged exactly when the slabs are built).
        /// </summary>
        /// <exception cref="ScadEvaluationException">Thrown when they cannot fit.</exception>
        public void EnsureLeafBudget() => Context.EnsureTriangles(8L * Leaves, Node.Location);
    }

    /// <summary>
    ///     Flattens the scene trees of the solid children into a <see cref="SolidTree"/>, walking iteratively.
    /// </summary>
    /// <remarks>
    ///     Overlays are skipped; every node gets a flag that says whether it is subtracted an odd number of times, which
    ///     decides which faces of its mesh bound the solid (the upward faces of an added operand and the downward faces
    ///     of a subtracted one).
    /// </remarks>
    /// <param name="solids">The solid children.</param>
    /// <param name="context">The build context (cancellation is polled per node).</param>
    /// <returns>The tree.</returns>
    private static SolidTree Collect(List<ScadGeo> solids, ScadGeometryContext context)
    {
        var tree = new SolidTree();
        tree.Add(new TreeNode(NodeKind.Union, false), -1);
        var stack = new Stack<(SceneNode Node, Matrix4x4D Parent, int ParentIndex, bool Negative)>();
        for (var i = solids.Count - 1; i >= 0; i--)
        {
            stack.Push((solids[i].Node!, Matrix4x4D.Identity, 0, false));
        }

        while (stack.Count > 0)
        {
            context.Tick();
            var (node, parent, parentIndex, negative) = stack.Pop();
            if (node is OverlayNode)
            {
                continue;
            }

            tree.HasAppearance |= node.Appearance is not null;
            var matrix = node.Transform * parent;
            switch (node)
            {
                case MeshNode meshNode:
                    AddMesh(tree, meshNode.Mesh, matrix, parentIndex, negative, context);
                    break;
                case GroupNode group:
                    PushChildren(stack, group.Children, matrix, tree.Add(new TreeNode(NodeKind.Union, negative), parentIndex), negative, false);
                    break;
                case CsgNode csg:
                    var kind = csg.Operation switch
                    {
                        CsgOperation.Difference => NodeKind.Difference,
                        CsgOperation.Intersection => NodeKind.Intersection,
                        _ => NodeKind.Union,
                    };
                    PushChildren(stack, csg.Children, matrix, tree.Add(new TreeNode(kind, negative), parentIndex), negative, kind == NodeKind.Difference);
                    break;
                default:
                    break;
            }
        }

        return tree;
    }

    /// <summary>
    ///     Adds a mesh to the tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="mesh">The mesh.</param>
    /// <param name="matrix">The matrix into the frame of the projection.</param>
    /// <param name="parentIndex">The index of the parent node.</param>
    /// <param name="negative">Whether the mesh is subtracted an odd number of times.</param>
    /// <param name="context">The build context.</param>
    private static void AddMesh(SolidTree tree, Mesh mesh, Matrix4x4D matrix, int parentIndex, bool negative, ScadGeometryContext context)
    {
        var frame = FrameMesh.Create(mesh, matrix, context);
        tree.Add(new TreeNode(NodeKind.Mesh, negative, frame), parentIndex);
    }

    /// <summary>
    ///     Pushes the children of a node so that they are popped in document order.
    /// </summary>
    /// <param name="stack">The stack.</param>
    /// <param name="children">The children.</param>
    /// <param name="matrix">The matrix of the parent.</param>
    /// <param name="parentIndex">The index of the parent in the tree.</param>
    /// <param name="negative">The polarity of the parent.</param>
    /// <param name="isDifference">Whether the parent is a difference (the children after the first flip the polarity).</param>
    private static void PushChildren(
        Stack<(SceneNode Node, Matrix4x4D Parent, int ParentIndex, bool Negative)> stack,
        IReadOnlyList<SceneNode> children,
        Matrix4x4D matrix,
        int parentIndex,
        bool negative,
        bool isDifference)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            stack.Push((children[i], matrix, parentIndex, negative ^ (isDifference && i > 0)));
        }
    }

    /// <summary>
    ///     Gets the largest absolute component of a vector.
    /// </summary>
    /// <param name="v">The vector.</param>
    /// <returns>The largest absolute component.</returns>
    private static double MaxAbs(Vector3D v) => Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));

    /// <summary>
    ///     Gets the spacing of the doubles at a coordinate: the rounding error that a coordinate of that size carries.
    /// </summary>
    /// <param name="maxCoordinate">The largest absolute coordinate.</param>
    /// <returns>The spacing, or 0 when the coordinate is 0.</returns>
    private static double CoordinateUlp(double maxCoordinate) =>
        maxCoordinate > 0.0 ? Math.BitIncrement(maxCoordinate) - maxCoordinate : 0.0;
}
