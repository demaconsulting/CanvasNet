// cspell:ignore minkowski Minkowski
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The <c>hull</c> and <c>minkowski</c> modules: the convex hull of the points of the children, and the
///     Minkowski sum of convex children.
/// </summary>
/// <remarks>
///     <para>
///         <b>Operands.</b> Both modules take the geometry of their children, empty ones dropped; with no child left
///         the result is empty and nothing is reported. A module with both 2D and 3D children is reported
///         (<see cref="ScadFeatureIds.HullMixedDimensions"/>, <see cref="ScadFeatureIds.MinkowskiMixedDimensions"/>)
///         and yields nothing. The <c>convexity</c> argument of <c>minkowski</c> is ignored, and arguments are not
///         validated. A color inside either module is not kept (the result takes the enclosing <c>color</c>) and a
///         warning says so.
///     </para>
///     <para>
///         <b><c>hull</c>.</b> The hull of the union of the children is the hull of every vertex of every mesh below
///         them, taken in the frame of the module (the transform wrappers of the scene tree are accumulated
///         iteratively); only vertices that a triangle references count. A difference or an intersection below a
///         3D <c>hull</c>, or in a 2D <c>hull</c>, is reported (<see cref="ScadFeatureIds.HullCsgOperand"/>)
///         because the hull of a boolean is not the hull of its operands' points; it is never approximated. Points
///         that span no volume (3D) or no area (2D) are reported (<see cref="ScadFeatureIds.HullDegenerate"/>). The 3D
///         result is the closed, outward-wound mesh of <see cref="ConvexHull.Hull3D"/> (a valid operand for later
///         booleans); the 2D result is the convex polygon of <see cref="ConvexHull.Hull2D"/> as a flat result that
///         an extrusion consumes like any other shape.
///     </para>
///     <para>
///         <b><c>minkowski</c>.</b> The sum is folded over the children from the left. Each child reduces to its
///         convex members (the meshes of a union-only scene tree, each in the frame of the module); because
///         <c>(A u B) + C = (A + C) u (B + C)</c> the members of the accumulated result are summed pairwise with
///         the members of the next child (at most <see cref="MaxMinkowskiCombinations"/> sums per step, else
///         <see cref="ScadFeatureIds.MinkowskiTooComplex"/>, as is a pair of meshes above the
///         <see cref="MinkowskiSum.MaxPairwiseSums"/> bound). <see cref="MinkowskiSum.ConvexSum"/> proves each
///         operand convex (within its documented tolerance) or refuses; a child that contains a boolean or a mesh
///         that is not convex is reported (<see cref="ScadFeatureIds.MinkowskiNonConvex"/>) and the module is
///         omitted as a whole. A 2D <c>minkowski</c> is deferred (<see cref="ScadFeatureIds.Minkowski2D"/>). A
///         module with one child is that child's geometry, after its convexity has been verified in the same way and
///         any child appearances have been stripped.
///     </para>
///     <para>
///         <b>Budget and cancellation.</b> The triangle count of a hull or a sum is known only once it is built
///         (the hull of <c>n</c> points has at most <c>2n - 4</c> triangles), so it is charged, exactly, right after
///         the computation and before the node is created: the work and the memory before the charge are bounded
///         by the number of points (itself capped by the triangle budget for a hull and by
///         <see cref="MinkowskiSum.MaxPairwiseSums"/> for a sum), and the total charged always equals the
///         triangles of the meshes created. Intermediate sums of a fold are charged while they live and refunded
///         when the next step has consumed them, so the memory of a fold stays within the budget; operand meshes
///         stay charged like any consumed operand. Cancellation is polled between meshes and sums and inside the
///         Render3D loops.
///     </para>
///     <para>
///         The methods are stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ScadHulls
{
    /// <summary>
    ///     The largest number of convex members of one child, and of pairwise sums in one step of a fold, that a
    ///     <c>minkowski</c> processes.
    /// </summary>
    internal const int MaxMinkowskiCombinations = 4096;

    /// <summary>
    ///     A small convex solid (a tetrahedron) summed with a lone child to verify its convexity; the sum is discarded.
    /// </summary>
    private static readonly Mesh ConvexityProbe = new(
        [new Vector3D(0.0, 0.0, 0.0), new Vector3D(1.0, 0.0, 0.0), new Vector3D(0.0, 1.0, 0.0), new Vector3D(0.0, 0.0, 1.0)],
        [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3]);

    /// <summary>
    ///     The meshes below a set of children, each with the matrix that takes it into the frame of the module.
    /// </summary>
    /// <param name="Meshes">The meshes in document order.</param>
    /// <param name="HasBoolean">Whether a difference or an intersection was found (the walk stops there).</param>
    /// <param name="HasAppearance">Whether any node carries an appearance.</param>
    private sealed record SolidParts(List<(Mesh Mesh, Matrix4x4D Matrix)> Meshes, bool HasBoolean, bool HasAppearance);

    /// <summary>
    ///     Builds <c>hull()</c>: the convex hull of the points of the children.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The hull; nothing when there is no child or the hull is reported.</returns>
    public static ScadGeo Hull(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var children = results.Where(r => r.Kind != ScadGeoKind.Empty).ToList();
        if (children.Count == 0)
        {
            return ScadGeo.Empty;
        }

        var solids = children.Where(c => c.Kind == ScadGeoKind.Solid).ToList();
        if (solids.Count == 0)
        {
            return Hull2D(instance, children, context);
        }

        if (solids.Count != children.Count)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.HullMixedDimensions,
                "hull: the children mix 2D and 3D objects, which has no single hull; the hull is omitted",
                instance.Location);
            return ScadGeo.Empty;
        }

        return Hull3D(instance, solids, context);
    }

    /// <summary>
    ///     Builds <c>minkowski()</c>: the Minkowski sum of the convex children.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The sum; nothing when there is no child or the sum is reported.</returns>
    public static ScadGeo Minkowski(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var children = results.Where(r => r.Kind != ScadGeoKind.Empty).ToList();
        if (children.Count == 0)
        {
            return ScadGeo.Empty;
        }

        var solidCount = children.Count(c => c.Kind == ScadGeoKind.Solid);
        if (solidCount == 0)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.Minkowski2D,
                "minkowski: the Minkowski sum of 2D objects is not supported yet; the minkowski is omitted",
                instance.Location);
            return ScadGeo.Empty;
        }

        if (solidCount != children.Count)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.MinkowskiMixedDimensions,
                "minkowski: the children mix 2D and 3D objects, which has no Minkowski sum; the minkowski is omitted",
                instance.Location);
            return ScadGeo.Empty;
        }

        return MinkowskiSolids(instance, children, context);
    }

    /// <summary>
    ///     Builds the hull of solid children.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="solids">The solid children (at least one).</param>
    /// <param name="context">The build context.</param>
    /// <returns>The hull, or nothing after a report.</returns>
    private static ScadGeo Hull3D(ScadInstance node, List<ScadGeo> solids, ScadGeometryContext context)
    {
        var parts = Collect(solids, context);
        if (parts.HasBoolean)
        {
            ReportHullBoolean(node, context);
            return ScadGeo.Empty;
        }

        WarnColors(node, parts.HasAppearance, context);
        var points = GatherPoints(parts, node, context);
        context.Poll();
        Mesh mesh;
        try
        {
            mesh = ConvexHull.Hull3D(points, context.Cancellation);
        }
        catch (ArgumentException ex)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.HullDegenerate,
                $"hull: the points do not span a volume ({ex.Message}); the hull is omitted",
                node.Location);
            return ScadGeo.Empty;
        }

        // The exact count is known only now; it is charged before the node is created
        context.ChargeTriangles(mesh.TriangleCount, node.Location);
        return ScadGeo.FromMesh(context.CreateMeshNode(mesh, node.Location));
    }

    /// <summary>
    ///     Builds the hull of flat children.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="flats">The flat children (at least one).</param>
    /// <param name="context">The build context.</param>
    /// <returns>The hull as a flat result, or nothing after a report.</returns>
    private static ScadGeo Hull2D(ScadInstance node, List<ScadGeo> flats, ScadGeometryContext context)
    {
        if (flats.Any(f => !f.Shape!.IsUnionOnly()))
        {
            ReportHullBoolean(node, context);
            return ScadGeo.Empty;
        }

        WarnColors(node, flats.Any(f => f.Shape!.HasColor()), context);
        var points = new List<Vector2D>();
        foreach (var flat in flats)
        {
            context.Poll();
            foreach (var (leaf, matrix) in flat.Shape!.EnumerateLeaves())
            {
                foreach (var vertex in leaf.Region!.Vertices)
                {
                    var moved = matrix.TransformPoint(new Vector3D(vertex.X, vertex.Y, 0.0));
                    points.Add(new Vector2D(moved.X, moved.Y));
                }
            }
        }

        var hull = ConvexHull.Hull2D(points, context.Cancellation);
        if (hull.Count < 3)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.HullDegenerate,
                "hull: the points do not span an area; the hull is omitted",
                node.Location);
            return ScadGeo.Empty;
        }

        try
        {
            return ScadGeo.FromRegion(new PolygonRegion([hull]), node.Location);
        }
        catch (ArgumentException ex)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.GeometryRejected,
                $"hull: the shape was rejected: {ex.Message}",
                node.Location);
            return ScadGeo.Empty;
        }
    }

    /// <summary>
    ///     Reports a boolean inside a hull.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    private static void ReportHullBoolean(ScadInstance node, ScadGeometryContext context) =>
        context.Reporter.Unsupported(
            ScadFeatureIds.HullCsgOperand,
            "hull: a child contains a difference or an intersection, which is not the hull of its operands' points; the hull is omitted",
            node.Location);

    /// <summary>
    ///     Warns that colors inside the module are dropped.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="hasColor">Whether any child carries a color.</param>
    /// <param name="context">The build context.</param>
    private static void WarnColors(ScadInstance node, bool hasColor, ScadGeometryContext context)
    {
        if (hasColor)
        {
            context.Reporter.Warn($"{node.Name}: colors inside {node.Name}() are not kept; the result takes the enclosing color", node.Location);
        }
    }

    /// <summary>
    ///     Collects the meshes below solid children with the matrices that take them into the frame of the module,
    ///     walking the scene trees iteratively.
    /// </summary>
    /// <param name="solids">The solid children.</param>
    /// <param name="context">The build context (cancellation is polled per node).</param>
    /// <returns>The meshes in document order; the walk stops at the first difference or intersection.</returns>
    private static SolidParts Collect(IEnumerable<ScadGeo> solids, ScadGeometryContext context)
    {
        var meshes = new List<(Mesh, Matrix4x4D)>();
        var hasAppearance = false;
        var stack = new Stack<(SceneNode Node, Matrix4x4D Parent)>();
        foreach (var solid in solids.Reverse())
        {
            stack.Push((solid.Node!, Matrix4x4D.Identity));
        }

        while (stack.Count > 0)
        {
            context.Tick();
            var (node, parent) = stack.Pop();
            hasAppearance |= node.Appearance is not null;
            var matrix = node.Transform * parent;
            switch (node)
            {
                case MeshNode meshNode:
                    meshes.Add((meshNode.Mesh, matrix));
                    break;
                case CsgNode { Operation: not CsgOperation.Union }:
                    return new SolidParts(meshes, true, hasAppearance);
                case GroupNode group:
                    PushChildren(stack, group.Children, matrix);
                    break;
                case CsgNode csg:
                    PushChildren(stack, csg.Children, matrix);
                    break;
                default:
                    break;
            }
        }

        return new SolidParts(meshes, false, hasAppearance);
    }

    /// <summary>
    ///     Pushes children so that they are popped in document order.
    /// </summary>
    /// <param name="stack">The stack.</param>
    /// <param name="children">The children.</param>
    /// <param name="matrix">The matrix of their parent.</param>
    private static void PushChildren(Stack<(SceneNode Node, Matrix4x4D Parent)> stack, IReadOnlyList<SceneNode> children, Matrix4x4D matrix)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            stack.Push((children[i], matrix));
        }
    }

    /// <summary>
    ///     Gathers the referenced vertices of the meshes in the frame of the module, bounded by the triangle budget.
    /// </summary>
    /// <param name="parts">The meshes.</param>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The points.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when there are more points than the triangle budget.</exception>
    private static List<Vector3D> GatherPoints(SolidParts parts, ScadInstance node, ScadGeometryContext context)
    {
        var points = new List<Vector3D>();
        foreach (var (mesh, matrix) in parts.Meshes)
        {
            context.Poll();
            var used = ReferencedVertices(mesh);
            for (var i = 0; i < used.Length; i++)
            {
                if (!used[i])
                {
                    continue;
                }

                // Welded meshes have fewer vertices than triangles, so only a pathological mesh gets here
                if (points.Count >= context.MaxTriangles)
                {
                    context.FailTriangleLimit(node.Location);
                }

                points.Add(matrix.TransformPoint(mesh.Vertices[i]));
            }
        }

        return points;
    }

    /// <summary>
    ///     Marks the vertices that a triangle of a mesh references.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>One flag per vertex.</returns>
    private static bool[] ReferencedVertices(Mesh mesh)
    {
        var used = new bool[mesh.VertexCount];
        foreach (var index in mesh.TriangleIndices)
        {
            used[index] = true;
        }

        return used;
    }

    /// <summary>
    ///     Builds the Minkowski sum of solid children.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="children">The solid children (at least one).</param>
    /// <param name="context">The build context.</param>
    /// <returns>The sum, or nothing after a report.</returns>
    private static ScadGeo MinkowskiSolids(ScadInstance node, List<ScadGeo> children, ScadGeometryContext context)
    {
        try
        {
            var members = new List<List<Mesh>>();
            var hasAppearance = false;
            foreach (var child in children)
            {
                var parts = Collect([child], context);
                hasAppearance |= parts.HasAppearance;
                if (parts.HasBoolean)
                {
                    ReportNonConvex(node, context, "a child contains a difference or an intersection, which is not provably convex");
                    return ScadGeo.Empty;
                }

                if (parts.Meshes.Count > MaxMinkowskiCombinations)
                {
                    ReportTooComplex(node, context, $"a child has {parts.Meshes.Count} convex parts");
                    return ScadGeo.Empty;
                }

                members.Add(InFrame(parts, context));
            }

            WarnColors(node, hasAppearance, context);
            return members.Count == 1
                ? VerifyLoneChild(node, children[0], members[0], hasAppearance, context)
                : Fold(node, members, context);
        }
        catch (NotSupportedException ex)
        {
            ReportNonConvex(node, context, ex.Message);
            return ScadGeo.Empty;
        }
        catch (ArgumentException ex)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.GeometryRejected,
                $"minkowski: the geometry was rejected: {ex.Message}; the minkowski is omitted",
                node.Location);
            return ScadGeo.Empty;
        }
    }

    /// <summary>
    ///     Brings the meshes of a child into the frame of the module.
    /// </summary>
    /// <param name="parts">The meshes of the child.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The meshes (the original mesh when its matrix is the identity).</returns>
    private static List<Mesh> InFrame(SolidParts parts, ScadGeometryContext context)
    {
        var members = new List<Mesh>(parts.Meshes.Count);
        foreach (var (mesh, matrix) in parts.Meshes)
        {
            context.Poll();
            members.Add(matrix == Matrix4x4D.Identity ? mesh : mesh.Transform(matrix));
        }

        return members;
    }

    /// <summary>
    ///     Verifies that a lone child is convex (a sum with a point would be the child itself) and returns its geometry
    ///     without child appearances.
    /// </summary>
    /// <remarks>
    ///     The sum with a small tetrahedron makes <see cref="MinkowskiSum.ConvexSum"/> verify the member; the sum is
    ///     discarded, so nothing is created and nothing is charged. The child scene is cloned iteratively only when
    ///     it carries an appearance, preserving every mesh, transform, group, and CSG node while removing the
    ///     appearance wrappers so an enclosing color can apply.
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="child">The child.</param>
    /// <param name="members">The convex members of the child.</param>
    /// <param name="hasAppearance">Whether the child scene carries an appearance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The verified child geometry, or nothing after a report.</returns>
    private static ScadGeo VerifyLoneChild(
        ScadInstance node,
        ScadGeo child,
        List<Mesh> members,
        bool hasAppearance,
        ScadGeometryContext context)
    {
        foreach (var member in members)
        {
            context.Poll();
            if (!PairWithinLimit(member, ConvexityProbe))
            {
                ReportTooComplex(node, context, "a child has too many vertices to verify");
                return ScadGeo.Empty;
            }

            _ = MinkowskiSum.ConvexSum(member, ConvexityProbe, context.Cancellation);
        }

        return hasAppearance ? StripAppearances(child, context) : child;
    }

    /// <summary>
    ///     Removes appearances from a lone Minkowski child without changing its geometry or local transforms.
    /// </summary>
    /// <remarks>
    ///     The geometry builder must remain safe for deeply nested models, so this scene copy uses an explicit
    ///     post-order stack rather than recursive calls. A reference map also preserves shared group or CSG
    ///     subtrees if one is present.
    /// </remarks>
    /// <param name="child">The solid child whose appearances are to be removed.</param>
    /// <param name="context">The build context.</param>
    /// <returns>A solid with the same geometry and transforms and no appearances.</returns>
    private static ScadGeo StripAppearances(ScadGeo child, ScadGeometryContext context)
    {
        var root = child.Node!;
        var copies = new Dictionary<SceneNode, SceneNode>(ReferenceEqualityComparer.Instance);
        var scheduled = new HashSet<SceneNode>(ReferenceEqualityComparer.Instance) { root };
        var stack = new Stack<(SceneNode Node, bool Expanded)>();
        stack.Push((root, false));

        while (stack.Count > 0)
        {
            context.Tick();
            var (node, expanded) = stack.Pop();
            if (!expanded)
            {
                stack.Push((node, true));
                switch (node)
                {
                    case GroupNode group:
                        PushCopyChildren(stack, group.Children, scheduled);
                        break;
                    case CsgNode csg:
                        PushCopyChildren(stack, csg.Children, scheduled);
                        break;
                }

                continue;
            }

            SceneNode copy = node switch
            {
                MeshNode mesh => new MeshNode(mesh.Mesh) { Transform = mesh.Transform },
                GroupNode group => new GroupNode(group.Children.Select(childNode => copies[childNode]))
                {
                    Transform = group.Transform,
                },
                CsgNode csg => new CsgNode(csg.Operation, csg.Children.Select(childNode => copies[childNode]))
                {
                    Transform = csg.Transform,
                },
                _ => throw new InvalidOperationException($"Unknown scene node type {node.GetType().FullName}."),
            };
            copies.Add(node, copy);
        }

        return ScadGeo.FromSolid(copies[root], child.Bounds, child.IsTight, child.IsApproximate, child.Reach);
    }

    /// <summary>
    ///     Pushes scene children in reverse order for the iterative appearance-stripping walk.
    /// </summary>
    /// <param name="stack">The walk stack.</param>
    /// <param name="children">The children.</param>
    /// <param name="scheduled">The nodes already scheduled for copying.</param>
    private static void PushCopyChildren(
        Stack<(SceneNode Node, bool Expanded)> stack,
        IReadOnlyList<SceneNode> children,
        HashSet<SceneNode> scheduled)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (scheduled.Add(children[i]))
            {
                stack.Push((children[i], false));
            }
        }
    }

    /// <summary>
    ///     Folds the Minkowski sum over the members of two or more children.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="members">The convex members of each child (at least two children).</param>
    /// <param name="context">The build context.</param>
    /// <returns>The sum, or nothing after a report.</returns>
    private static ScadGeo Fold(ScadInstance node, List<List<Mesh>> members, ScadGeometryContext context)
    {
        var accumulated = members[0];
        var charges = new FoldCharges();
        var completed = false;
        try
        {
            for (var step = 1; step < members.Count; step++)
            {
                var next = members[step];
                var pairs = (long)accumulated.Count * next.Count;
                if (pairs > MaxMinkowskiCombinations)
                {
                    ReportTooComplex(node, context, $"a step needs {pairs} convex sums");
                    return ScadGeo.Empty;
                }

                var produced = SumStep(node, accumulated, next, context, charges);
                if (produced is null)
                {
                    return ScadGeo.Empty;
                }

                accumulated = produced;
            }

            completed = true;
            return ToSolid(node, accumulated, context);
        }
        finally
        {
            // Whatever failed, the sums are scene-less temporaries: only the operands stay charged
            if (!completed)
            {
                context.RefundTriangles(charges.Live + charges.Step);
            }
        }
    }

    /// <summary>
    ///     The triangles charged for the temporary sums of a fold.
    /// </summary>
    private sealed class FoldCharges
    {
        /// <summary>Gets or sets the charge of the accumulated sums of the previous step (the operands of the next).</summary>
        public long Live { get; set; }

        /// <summary>Gets or sets the charge of the sums of the step in progress.</summary>
        public long Step { get; set; }
    }

    /// <summary>
    ///     Sums every member of the accumulated result with every member of the next child.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="accumulated">The accumulated members.</param>
    /// <param name="next">The members of the next child.</param>
    /// <param name="context">The build context.</param>
    /// <param name="charges">The charges of the temporary sums; updated as sums are created and consumed.</param>
    /// <returns>The sums, or <see langword="null"/> after a pair above the work bound was reported.</returns>
    private static List<Mesh>? SumStep(
        ScadInstance node,
        List<Mesh> accumulated,
        List<Mesh> next,
        ScadGeometryContext context,
        FoldCharges charges)
    {
        var sums = new List<Mesh>(accumulated.Count * next.Count);
        charges.Step = 0;
        foreach (var left in accumulated)
        {
            foreach (var right in next)
            {
                context.Poll();
                if (!PairWithinLimit(left, right))
                {
                    ReportTooComplex(node, context, $"two parts have too many vertices to sum (limit {MinkowskiSum.MaxPairwiseSums} pairwise sums)");
                    return null;
                }

                var sum = MinkowskiSum.ConvexSum(left, right, context.Cancellation);
                context.ChargeTriangles(sum.TriangleCount, node.Location);
                charges.Step += sum.TriangleCount;
                sums.Add(sum);
            }
        }

        // The sums of the previous step have been consumed and are gone
        context.RefundTriangles(charges.Live);
        charges.Live = charges.Step;
        charges.Step = 0;
        return sums;
    }

    /// <summary>
    ///     Turns the final sums (already charged) into scene nodes.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="sums">The final sums.</param>
    /// <param name="context">The build context.</param>
    /// <returns>One mesh node or a group of them.</returns>
    private static ScadGeo ToSolid(ScadInstance node, List<Mesh> sums, ScadGeometryContext context)
    {
        var nodes = sums.Select(m => context.CreateMeshNode(m, node.Location)).ToList();
        if (nodes.Count == 1)
        {
            return ScadGeo.FromMesh(nodes[0]);
        }

        var bounds = BoundingBox3D.Empty;
        foreach (var mesh in sums)
        {
            bounds = bounds.Union(mesh.Bounds);
        }

        return ScadGeo.FromSolid(new GroupNode(nodes), bounds, true, false, bounds);
    }

    /// <summary>
    ///     Checks whether the pairwise sums of two meshes are within the documented bound, counting distinct
    ///     referenced vertices only when the raw vertex counts are over it.
    /// </summary>
    /// <param name="a">The first mesh.</param>
    /// <param name="b">The second mesh.</param>
    /// <returns><see langword="true"/> when the number of pairwise sums is at most <see cref="MinkowskiSum.MaxPairwiseSums"/>.</returns>
    private static bool PairWithinLimit(Mesh a, Mesh b)
    {
        if ((long)a.VertexCount * b.VertexCount <= MinkowskiSum.MaxPairwiseSums)
        {
            return true;
        }

        return (long)DistinctVertexCount(a) * DistinctVertexCount(b) <= MinkowskiSum.MaxPairwiseSums;
    }

    /// <summary>
    ///     Counts the distinct positions of the vertices that a mesh's triangles reference.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The number of distinct referenced positions.</returns>
    private static int DistinctVertexCount(Mesh mesh)
    {
        var used = ReferencedVertices(mesh);
        var distinct = new HashSet<Vector3D>();
        for (var i = 0; i < used.Length; i++)
        {
            if (used[i])
            {
                distinct.Add(mesh.Vertices[i]);
            }
        }

        return distinct.Count;
    }

    /// <summary>
    ///     Reports a child that is not provably convex.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="reason">The reason.</param>
    private static void ReportNonConvex(ScadInstance node, ScadGeometryContext context, string reason) =>
        context.Reporter.Unsupported(
            ScadFeatureIds.MinkowskiNonConvex,
            $"minkowski: {reason}; only convex operands are supported and the minkowski is omitted",
            node.Location);

    /// <summary>
    ///     Reports work above the documented bounds.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="reason">The reason.</param>
    private static void ReportTooComplex(ScadInstance node, ScadGeometryContext context, string reason) =>
        context.Reporter.Unsupported(
            ScadFeatureIds.MinkowskiTooComplex,
            $"minkowski: {reason}, which is above the supported work bound; the minkowski is omitted",
            node.Location);
}
