using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The silhouette, the exact classification of boolean subtrees, and the cut.
/// </content>
internal static partial class ScadProjections
{
    /// <summary>
    ///     How much of a window a section covers.
    /// </summary>
    private enum Coverage
    {
        /// <summary>The section has no point in the window.</summary>
        Empty,

        /// <summary>The section covers the whole footprint of the face that is being classified.</summary>
        Full,

        /// <summary>The section is the symbolic shape.</summary>
        Shape,
    }

    /// <summary>
    ///     The cross-section of a subtree by a plane, restricted to the window of a face.
    /// </summary>
    /// <param name="Coverage">How much of the window the section covers.</param>
    /// <param name="Shape">The symbolic shape when <paramref name="Coverage"/> is <see cref="Coverage.Shape"/>.</param>
    private readonly record struct SectionResult(Coverage Coverage, ScadShape2D? Shape)
    {
        /// <summary>Gets the result for a section with no point in the window.</summary>
        public static SectionResult None { get; } = new(Coverage.Empty, null);

        /// <summary>Gets the result for a section that covers the whole footprint.</summary>
        public static SectionResult All { get; } = new(Coverage.Full, null);
    }

    /// <summary>
    ///     The XY box that contains the footprint of a face; a section outside it cannot matter for the face.
    /// </summary>
    /// <param name="MinX">The smallest X.</param>
    /// <param name="MaxX">The largest X.</param>
    /// <param name="MinY">The smallest Y.</param>
    /// <param name="MaxY">The largest Y.</param>
    private readonly record struct Window(double MinX, double MaxX, double MinY, double MaxY)
    {
        /// <summary>Gets a window that contains everything.</summary>
        public static Window Everything { get; } = new(double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity);

        /// <summary>
        ///     Tells whether the bounds of a section overlap the window.
        /// </summary>
        /// <param name="entry">The section.</param>
        /// <param name="tolerance">The tolerance by which touching boxes still count as overlapping.</param>
        /// <returns><see langword="false"/> when the section lies entirely outside the window.</returns>
        public bool Overlaps(SectionEntry entry, double tolerance) =>
            entry.MaxX >= MinX - tolerance && entry.MinX <= MaxX + tolerance && entry.MaxY >= MinY - tolerance && entry.MinY <= MaxY + tolerance;
    }

    /// <summary>
    ///     What is needed to build the section of a subtree for one face.
    /// </summary>
    /// <param name="Plane">The plane of the face.</param>
    /// <param name="Down">Whether the plane is shifted down or up.</param>
    /// <param name="Window">The XY box of the face.</param>
    /// <param name="OwnNode">
    ///     The index of the mesh node the face belongs to when its section on this side of the face is known to cover the
    ///     whole footprint (the side of the face its solid lies on); otherwise -1.
    /// </param>
    /// <param name="MakeFace">Makes a new copy of the footprint of the face as a shape, or <see langword="null"/> when there is no face.</param>
    private sealed record SectionRequest(CutPlane Plane, bool Down, Window Window, int OwnNode, Func<ScadShape2D>? MakeFace);

    /// <summary>
    ///     Builds the shadow of the solid tree (<c>cut = false</c>).
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="state">The shared state; receives a failure.</param>
    /// <returns>The shape; <see langword="null"/> when it is empty or a failure was recorded.</returns>
    private static ScadShape2D? Silhouette(SolidTree tree, State state)
    {
        var leaves = new List<ScadShape2D>();
        var stack = new Stack<int>();
        stack.Push(0);
        while (stack.Count > 0 && !state.Failed)
        {
            state.Context.Tick();
            var index = stack.Pop();
            var node = tree.Nodes[index];
            switch (node.Kind)
            {
                case NodeKind.Mesh:
                    AddFootprints(node.Mesh!, node.Mesh!.IsClosedOutward ? Facing.Up : Facing.Any, leaves, state);
                    break;
                case NodeKind.Union:
                    for (var i = node.Children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(node.Children[i]);
                    }

                    break;
                default:
                    Classify(tree, index, leaves, state);
                    break;
            }
        }

        return state.Failed ? null : UnionOf(leaves);
    }

    /// <summary>
    ///     Adds the footprints of the wanted faces of a mesh as leaves.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="facing">Which faces are wanted.</param>
    /// <param name="leaves">The leaves of the union that is being built.</param>
    /// <param name="state">The shared state.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the footprints could not fit the triangle budget.</exception>
    private static void AddFootprints(FrameMesh mesh, Facing facing, List<ScadShape2D> leaves, State state)
    {
        foreach (var group in GroupFaces(mesh, facing, state))
        {
            state.Context.Tick();
            foreach (var region in Footprint(mesh, group))
            {
                leaves.Add(state.NewLeaf(region));
            }
        }

        state.EnsureLeafBudget();
    }

    /// <summary>
    ///     Classifies the subtree rooted at a difference or an intersection and adds the exact silhouette of its solid.
    /// </summary>
    /// <remarks>
    ///     For every face group of a mesh below the node (the upward faces of the added operands, the downward faces of
    ///     the subtracted ones) the part of its footprint where the solid is just below the face and not just above it is
    ///     part of the silhouette; the two conditions are cross-sections of the subtree by the plane of the face, shifted
    ///     down and up (see <c>ScadProjections.Slice.cs</c>). The work of every slice is charged to the meter before it
    ///     runs.
    /// </remarks>
    /// <param name="tree">The tree.</param>
    /// <param name="root">The index of the root of the subtree.</param>
    /// <param name="leaves">The leaves of the union that is being built.</param>
    /// <param name="state">The shared state; receives a failure.</param>
    private static void Classify(SolidTree tree, int root, List<ScadShape2D> leaves, State state)
    {
        state.Classified = true;
        var subtree = Descendants(tree, root);
        foreach (var index in subtree.Where(i => tree.Nodes[i].Kind == NodeKind.Mesh))
        {
            var node = tree.Nodes[index];
            var mesh = node.Mesh!;
            foreach (var group in GroupFaces(mesh, FacingOf(node, mesh), state))
            {
                if (state.Failed)
                {
                    return;
                }

                ClassifyGroup(tree, subtree, index, group, leaves, state);
            }
        }
    }

    /// <summary>
    ///     Gets the faces of a mesh that can bound the solid: the upward faces of an added operand and the downward
    ///     faces of a subtracted one (every face of a mesh that is not a closed outward solid).
    /// </summary>
    /// <param name="node">The node of the mesh.</param>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The wanted faces.</returns>
    private static Facing FacingOf(TreeNode node, FrameMesh mesh)
    {
        if (!mesh.IsClosedOutward)
        {
            return Facing.Any;
        }

        return node.IsNegative ? Facing.Down : Facing.Up;
    }

    /// <summary>
    ///     Adds the part of one face group that bounds the solid from above.
    /// </summary>
    /// <remarks>
    ///     A section is only needed inside the window of the face, so a mesh whose section lies outside it counts as empty,
    ///     and the section of the mesh the face belongs to covers the footprint of the face on the side of its solid, which
    ///     is how a face that nothing else touches yields just its footprint and no boolean at all.
    /// </remarks>
    /// <param name="tree">The tree.</param>
    /// <param name="subtree">The nodes of the subtree in ascending order.</param>
    /// <param name="meshIndex">The index of the mesh node of the face group.</param>
    /// <param name="group">The face group.</param>
    /// <param name="leaves">The leaves of the union that is being built.</param>
    /// <param name="state">The shared state; receives a failure.</param>
    private static void ClassifyGroup(SolidTree tree, List<int> subtree, int meshIndex, FaceGroup group, List<ScadShape2D> leaves, State state)
    {
        var mesh = tree.Nodes[meshIndex].Mesh!;
        var footprint = Footprint(mesh, group);
        if (footprint.Count == 0)
        {
            return;
        }

        var plane = new CutPlane(group.Normal, group.Offset);
        var window = WindowOf(footprint);

        // The solid of a closed mesh lies below an upward face and above a downward one, so its section covers the face on that side
        var own = mesh.IsClosedOutward ? meshIndex : -1;
        ScadShape2D MakeFace() => FaceShape(footprint, state);

        // Just below the face the solid must be there; if it is not, the face does not bound the solid from above
        var below = SectionTree(tree, subtree, new SectionRequest(plane, true, window, group.FacesUp ? own : -1, MakeFace), state);
        if (below.Coverage == Coverage.Empty)
        {
            return;
        }

        var above = SectionTree(tree, subtree, new SectionRequest(plane, false, window, group.FacesUp ? -1 : own, MakeFace), state);
        if (state.Failed || above.Coverage == Coverage.Full)
        {
            return;
        }

        leaves.Add(Boundary(below, above, MakeFace));
        state.EnsureLeafBudget();
    }

    /// <summary>
    ///     Combines the sections just below and just above a face into the part of the face that bounds the solid:
    ///     <c>face AND below AND NOT above</c>.
    /// </summary>
    /// <param name="below">The section just below the face (not empty).</param>
    /// <param name="above">The section just above the face (not full).</param>
    /// <param name="makeFace">Makes a copy of the footprint of the face.</param>
    /// <returns>The shape.</returns>
    private static ScadShape2D Boundary(SectionResult below, SectionResult above, Func<ScadShape2D> makeFace)
    {
        if (below.Coverage == Coverage.Full)
        {
            return above.Coverage == Coverage.Empty ? makeFace() : ScadShape2D.CreateCsg(CsgOperation.Difference, [makeFace(), above.Shape!]);
        }

        var inside = above.Coverage == Coverage.Empty ? below.Shape! : ScadShape2D.CreateCsg(CsgOperation.Difference, [below.Shape!, above.Shape!]);
        return ScadShape2D.CreateCsg(CsgOperation.Intersection, [makeFace(), inside]);
    }

    /// <summary>
    ///     Makes a new shape from the footprint regions of a face.
    /// </summary>
    /// <param name="footprint">The regions.</param>
    /// <param name="state">The shared state.</param>
    /// <returns>The union of new leaves.</returns>
    private static ScadShape2D FaceShape(List<PolygonRegion> footprint, State state) =>
        UnionOf([.. footprint.Select(state.NewLeaf)])!;

    /// <summary>
    ///     Gets the XY box of the vertices of footprint regions.
    /// </summary>
    /// <param name="footprint">The regions.</param>
    /// <returns>The window.</returns>
    private static Window WindowOf(List<PolygonRegion> footprint)
    {
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var region in footprint)
        {
            foreach (var v in region.Vertices)
            {
                minX = Math.Min(minX, v.X);
                maxX = Math.Max(maxX, v.X);
                minY = Math.Min(minY, v.Y);
                maxY = Math.Max(maxY, v.Y);
            }
        }

        return new Window(minX, maxX, minY, maxY);
    }

    /// <summary>
    ///     Builds the cross-section of the solid tree at <c>z = 0</c> (<c>cut = true</c>).
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="state">The shared state; receives a failure.</param>
    /// <returns>The shape; <see langword="null"/> when the plane misses the solids or a failure was recorded.</returns>
    private static ScadShape2D? Cut(SolidTree tree, State state)
    {
        var all = Enumerable.Range(0, tree.Nodes.Count).ToList();
        var plane = new CutPlane(Vector3D.UnitZ, 0.0);
        var below = SectionTree(tree, all, new SectionRequest(plane, true, Window.Everything, -1, null), state);
        if (state.Failed)
        {
            return null;
        }

        // A vertex on the plane makes the two sides differ: the section includes everything on the plane (closed set)
        if (!state.NearPlane)
        {
            state.EnsureLeafBudget();
            return below.Shape;
        }

        var above = SectionTree(tree, all, new SectionRequest(plane, false, Window.Everything, -1, null), state);
        state.EnsureLeafBudget();
        if (state.Failed)
        {
            return null;
        }

        return UnionOf([.. new[] { below.Shape, above.Shape }.Where(s => s is not null).Select(s => s!)]);
    }

    /// <summary>
    ///     Lists the nodes below a node (itself included) in ascending order, so that every child follows its parent.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="root">The index of the root.</param>
    /// <returns>The indices in ascending order.</returns>
    private static List<int> Descendants(SolidTree tree, int root)
    {
        var result = new List<int>();
        var stack = new Stack<int>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var index = stack.Pop();
            result.Add(index);
            foreach (var child in tree.Nodes[index].Children)
            {
                stack.Push(child);
            }
        }

        result.Sort();
        return result;
    }

    /// <summary>
    ///     Builds the cross-section of a subtree by a plane as a symbolic shape: the sections of the meshes combined with
    ///     the same booleans as the solids (slicing commutes with set operations).
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="subtree">The nodes of the subtree in ascending order; the first is the root.</param>
    /// <param name="request">The plane, the side, the window and the mesh whose section covers the face.</param>
    /// <param name="state">The shared state; receives a failure.</param>
    /// <returns>The section; empty when it has nothing in the window or a failure was recorded.</returns>
    private static SectionResult SectionTree(SolidTree tree, List<int> subtree, SectionRequest request, State state)
    {
        if (!state.Charge(subtree.Count))
        {
            return SectionResult.None;
        }

        var results = new Dictionary<int, SectionResult>();
        for (var i = subtree.Count - 1; i >= 0 && !state.Failed; i--)
        {
            var index = subtree[i];
            var node = tree.Nodes[index];
            results[index] = node.Kind == NodeKind.Mesh
                ? Leaf(tree, index, request, state)
                : Combine(node, results, request);
        }

        return state.Failed ? SectionResult.None : results[subtree[0]];
    }

    /// <summary>
    ///     Makes the result for the section of a mesh node.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="index">The index of the mesh node.</param>
    /// <param name="request">The request.</param>
    /// <param name="state">The shared state.</param>
    /// <returns>Empty when the section is empty or outside the window, full for the mesh of the face, otherwise a leaf.</returns>
    private static SectionResult Leaf(SolidTree tree, int index, SectionRequest request, State state)
    {
        var entry = SectionRegion(tree, index, request.Plane, request.Down, state);
        if (entry is null || !request.Window.Overlaps(entry, state.Tolerance))
        {
            return SectionResult.None;
        }

        return index == request.OwnNode ? SectionResult.All : Windowed(entry, request.Window, state);
    }

    /// <summary>
    ///     Makes the result for a section that is not the mesh of the face, clipped to the window when that is exact.
    /// </summary>
    /// <remarks>
    ///     A convex section is clipped to the (slightly padded) box of the face, which keeps its leaf as small as the
    ///     face instead of as large as the mesh, so the renderer does not resolve a large slab for every face; a section
    ///     that covers the whole box covers the face, and is full. A section that is not one convex contour is used as it
    ///     is (so is a clipped one that the region validator rejects).
    /// </remarks>
    /// <param name="entry">The section of the mesh.</param>
    /// <param name="window">The box of the face.</param>
    /// <param name="state">The shared state.</param>
    /// <returns>The result.</returns>
    private static SectionResult Windowed(SectionEntry entry, Window window, State state)
    {
        if (!entry.IsConvex || !double.IsFinite(window.MinX) || !double.IsFinite(window.MaxX))
        {
            return new SectionResult(Coverage.Shape, state.NewLeaf(entry.Region));
        }

        var pad = state.Tolerance;
        var box = new Window(window.MinX - pad, window.MaxX + pad, window.MinY - pad, window.MaxY + pad);
        var points = ClipToWindow(entry.Region.Contours[0], box);
        if (points.Count < 3)
        {
            return SectionResult.None;
        }

        var boxArea = (box.MaxX - box.MinX) * (box.MaxY - box.MinY);
        if (boxArea > 0.0 && PolygonArea(points) >= boxArea * (1.0 - 1e-9))
        {
            return SectionResult.All;
        }

        try
        {
            return new SectionResult(Coverage.Shape, state.NewLeaf(new PolygonRegion([points])));
        }
        catch (ArgumentException)
        {
            return new SectionResult(Coverage.Shape, state.NewLeaf(entry.Region));
        }
    }

    /// <summary>
    ///     Clips a convex polygon to a box (Sutherland-Hodgman against the four sides).
    /// </summary>
    /// <param name="polygon">The convex polygon.</param>
    /// <param name="box">The box.</param>
    /// <returns>The vertices of the clipped polygon; fewer than three when nothing is left.</returns>
    private static List<Vector2D> ClipToWindow(IReadOnlyList<Vector2D> polygon, Window box)
    {
        var points = polygon.ToList();
        points = ClipSide(points, p => p.X - box.MinX);
        points = ClipSide(points, p => box.MaxX - p.X);
        points = ClipSide(points, p => p.Y - box.MinY);
        return ClipSide(points, p => box.MaxY - p.Y);
    }

    /// <summary>
    ///     Keeps the part of a polygon where a signed distance is not negative.
    /// </summary>
    /// <param name="points">The polygon.</param>
    /// <param name="distance">The signed distance from the side; not negative inside.</param>
    /// <returns>The clipped polygon.</returns>
    private static List<Vector2D> ClipSide(List<Vector2D> points, Func<Vector2D, double> distance)
    {
        var result = new List<Vector2D>(points.Count + 2);
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var previous = points[(i + points.Count - 1) % points.Count];
            var dc = distance(current);
            var dp = distance(previous);
            if ((dc >= 0.0) != (dp >= 0.0))
            {
                result.Add(previous + ((current - previous) * (dp / (dp - dc))));
            }

            if (dc >= 0.0)
            {
                result.Add(current);
            }
        }

        return result;
    }

    /// <summary>
    ///     Computes the area of a simple polygon.
    /// </summary>
    /// <param name="points">The vertices.</param>
    /// <returns>The area (not negative).</returns>
    private static double PolygonArea(List<Vector2D> points)
    {
        var twice = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            twice += Vector2D.Cross(points[i], points[(i + 1) % points.Count]);
        }

        return Math.Abs(twice) / 2.0;
    }

    /// <summary>
    ///     Combines the sections of the children of a node with the node's boolean.
    /// </summary>
    /// <remarks>
    ///     An empty section is an empty operand: it vanishes from a union, empties an intersection, and empties a
    ///     difference when it is the minuend and vanishes otherwise. A full section (it covers the whole footprint of the
    ///     face) makes a union full, vanishes from an intersection, and empties a difference when it is subtracted.
    /// </remarks>
    /// <param name="node">The node.</param>
    /// <param name="results">The sections of the nodes that were done.</param>
    /// <param name="request">The request (it supplies the footprint of the face for a full minuend).</param>
    /// <returns>The section of the node.</returns>
    private static SectionResult Combine(TreeNode node, Dictionary<int, SectionResult> results, SectionRequest request)
    {
        var operands = node.Children.Select(c => results[c]).ToList();
        return node.Kind switch
        {
            NodeKind.Difference => Subtract(operands, request),
            NodeKind.Intersection => Intersect(operands),
            _ => Join(operands),
        };
    }

    /// <summary>
    ///     Unites sections.
    /// </summary>
    /// <param name="operands">The sections.</param>
    /// <returns>The union.</returns>
    private static SectionResult Join(List<SectionResult> operands)
    {
        if (operands.Any(o => o.Coverage == Coverage.Full))
        {
            return SectionResult.All;
        }

        var shapes = operands.Where(o => o.Coverage == Coverage.Shape).Select(o => o.Shape!).ToList();
        return shapes.Count == 0 ? SectionResult.None : new SectionResult(Coverage.Shape, UnionOf(shapes));
    }

    /// <summary>
    ///     Intersects sections.
    /// </summary>
    /// <param name="operands">The sections.</param>
    /// <returns>The intersection.</returns>
    private static SectionResult Intersect(List<SectionResult> operands)
    {
        if (operands.Count == 0 || operands.Any(o => o.Coverage == Coverage.Empty))
        {
            return SectionResult.None;
        }

        var shapes = operands.Where(o => o.Coverage == Coverage.Shape).Select(o => o.Shape!).ToList();
        return shapes.Count switch
        {
            0 => SectionResult.All,
            1 => new SectionResult(Coverage.Shape, shapes[0]),
            _ => new SectionResult(Coverage.Shape, ScadShape2D.CreateCsg(CsgOperation.Intersection, shapes)),
        };
    }

    /// <summary>
    ///     Subtracts the later sections from the first.
    /// </summary>
    /// <param name="operands">The sections; the first is the minuend.</param>
    /// <param name="request">The request.</param>
    /// <returns>The difference.</returns>
    private static SectionResult Subtract(List<SectionResult> operands, SectionRequest request)
    {
        if (operands.Count == 0 || operands[0].Coverage == Coverage.Empty)
        {
            return SectionResult.None;
        }

        var subtrahends = operands.Skip(1).Where(o => o.Coverage != Coverage.Empty).ToList();
        if (subtrahends.Any(o => o.Coverage == Coverage.Full))
        {
            return SectionResult.None;
        }

        if (subtrahends.Count == 0)
        {
            return operands[0];
        }

        var minuend = operands[0].Coverage == Coverage.Full ? request.MakeFace!() : operands[0].Shape!;
        var shapes = new List<ScadShape2D> { minuend };
        shapes.AddRange(subtrahends.Select(o => o.Shape!));
        return new SectionResult(Coverage.Shape, ScadShape2D.CreateCsg(CsgOperation.Difference, shapes));
    }
}
