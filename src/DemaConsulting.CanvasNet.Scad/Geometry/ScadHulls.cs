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
///         <see cref="MinkowskiSum.MaxPairwiseSums"/> bound). Each member of each child is verified once, up front
///         (<see cref="MinkowskiSum.RequireConvexSolid"/> proves it convex within its documented tolerance or
///         refuses), and the sums then skip the repeated test: the sums of a fold are convex hulls and need none.
///         The work of the whole module is capped as well: a member verification costs its vertex count and a sum
///         the product of its operands' vertex counts, and a total above <see cref="MaxMinkowskiWork"/> pairwise
///         vertex sums is reported as <see cref="ScadFeatureIds.MinkowskiTooComplex"/>, so many steps of moderate
///         size cannot add up to unbounded work. A child that contains a boolean or a mesh that is not convex is
///         reported (<see cref="ScadFeatureIds.MinkowskiNonConvex"/>) naming the child by its position and source
///         location, and the module is omitted as a whole. A 2D <c>minkowski</c> is deferred
///         (<see cref="ScadFeatureIds.Minkowski2D"/>). A module with one child is that child's geometry and
///         transforms, after its members have been verified in the same way, with any child appearances stripped (a
///         warning says so) so that an enclosing <c>color</c> applies.
///     </para>
///     <para>
///         <b>Budget and cancellation.</b> The exact triangle count of a hull or a sum is known only once it is
///         built, but the hull of <c>d</c> distinct points has at most <c>2d - 4</c> triangles. That upper bound
///         (<c>2n - 4</c> for the points of a hull, <c>2 * Va * Vb - 4</c> for one pairwise sum) is therefore charged
///         <em>before</em> any hull or sums memory is allocated, so a computation whose bound does not fit the
///         <em>remaining</em> budget is rejected with the triangle-limit error without being begun (the bound can
///         reject a hull that would fit exactly, which is the price of failing before allocating). After the mesh is
///         built the charge is settled to its actual triangle count by refunding the difference, and a rejection, a
///         failure or a cancellation refunds the whole pre-charge, so the total charged always equals the triangles of
///         the meshes created. Intermediate sums of a fold are charged (at their actual count) while they live and
///         refunded when the next step has consumed them, so the memory of a fold stays within the budget; operand
///         meshes stay charged like any consumed operand. Cancellation is polled between meshes and sums and inside
///         the Render3D loops.
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
    ///     The most work, counted in pairwise vertex sums, that one <c>minkowski</c> may do in total (2^21, the bound
    ///     of one pair of meshes). Every convex sum costs the product of the vertex counts of its two operands and
    ///     every member verification costs its vertex count, so neither many small steps nor a few large ones can
    ///     run unbounded: a module does at most the work of one maximal pair. Above it the module is reported as too
    ///     complex.
    /// </summary>
    internal const long MaxMinkowskiWork = MinkowskiSum.MaxPairwiseSums;

    /// <summary>
    ///     The running total of the work of one <c>minkowski</c>, in pairwise vertex sums.
    /// </summary>
    private sealed class WorkMeter
    {
        /// <summary>Gets the work charged so far.</summary>
        public long Used { get; private set; }

        /// <summary>
        ///     Charges work.
        /// </summary>
        /// <param name="cost">The work in pairwise vertex sums.</param>
        /// <returns><see langword="true"/> when the total is still within <see cref="MaxMinkowskiWork"/>.</returns>
        public bool TryCharge(long cost)
        {
            Used += cost;
            return Used <= MaxMinkowskiWork;
        }
    }

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
        var children = new List<ScadGeo>();
        var labels = new List<string>();
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i].Kind != ScadGeoKind.Empty)
            {
                children.Add(results[i]);
                labels.Add(DescribeChild(instance, results.Count, i));
            }
        }

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

        return MinkowskiSolids(instance, children, labels, context);
    }

    /// <summary>
    ///     Describes a child of a module for a message a SCAD author can follow: its position among the children
    ///     and, when known, the source location of the instance.
    /// </summary>
    /// <param name="node">The module instance.</param>
    /// <param name="resultCount">The number of results of the children (one per child instance).</param>
    /// <param name="index">The zero-based position of the child.</param>
    /// <returns>For example <c>child 2 (at 5:7)</c>.</returns>
    private static string DescribeChild(ScadInstance node, int resultCount, int index) =>
        node.Children.Count == resultCount
            ? $"child {index + 1} (at {node.Children[index].Location})"
            : $"child {index + 1}";

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

        // The hull of d distinct points has at most 2d - 4 triangles: that bound is charged, and so checked against
        // the remaining budget, before any hull memory exists; the charge is settled to the actual count afterwards
        var charged = ChargeHullBound(points, node, context);
        var settled = false;
        Mesh mesh;
        try
        {
            context.NoteHullStarted();
            mesh = ConvexHull.Hull3D(points, context.Cancellation);
            context.SettleTriangles(charged, mesh.TriangleCount, node.Location);
            settled = true;
        }
        catch (ArgumentException)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.HullDegenerate,
                "hull: the points do not span a volume (fewer than four distinct points, or all on one line or in one plane); the hull is omitted",
                node.Location);
            return ScadGeo.Empty;
        }
        finally
        {
            // A rejection, a cancellation or any other failure returns the whole pre-charge
            if (!settled)
            {
                context.RefundTriangles(charged);
            }
        }

        return ScadGeo.FromMesh(context.CreateMeshNode(mesh, node.Location));
    }

    /// <summary>
    ///     Charges the upper bound of the triangles of the hull of a point list, failing with the triangle-limit
    ///     error when it exceeds the remaining budget.
    /// </summary>
    /// <remarks>
    ///     The bound is <c>2d - 4</c> for <c>d</c> distinct points. The raw count of the points is used when it fits;
    ///     only when it does not, the distinct points are counted (children can share positions), so a hull whose
    ///     points repeat is not rejected by the count of its duplicates.
    /// </remarks>
    /// <param name="points">The candidate points.</param>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The number of triangles charged.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the bound does not fit the remaining budget.</exception>
    private static long ChargeHullBound(List<Vector3D> points, ScadInstance node, ScadGeometryContext context)
    {
        var bound = Math.Max(0L, (2L * points.Count) - 4L);
        if (bound > context.MaxTriangles - context.TrianglesCharged)
        {
            bound = Math.Max(0L, (2L * new HashSet<Vector3D>(points).Count) - 4L);
        }

        context.ChargeTriangles(bound, node.Location);
        return bound;
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
        catch (ArgumentException)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.GeometryRejected,
                "hull: the hull outline is not a valid planar shape (for example a coordinate is outside the supported range); the hull is omitted",
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
    /// <param name="labels">One description of each child for messages (position and location).</param>
    /// <param name="context">The build context.</param>
    /// <returns>The sum, or nothing after a report.</returns>
    private static ScadGeo MinkowskiSolids(ScadInstance node, List<ScadGeo> children, List<string> labels, ScadGeometryContext context)
    {
        try
        {
            var members = new List<List<Mesh>>();
            var hasAppearance = false;
            for (var c = 0; c < children.Count; c++)
            {
                var parts = Collect([children[c]], context);
                hasAppearance |= parts.HasAppearance;
                if (parts.HasBoolean)
                {
                    ReportNonConvex(node, context, $"{labels[c]} contains a difference or an intersection, which is not provably convex");
                    return ScadGeo.Empty;
                }

                if (parts.Meshes.Count > MaxMinkowskiCombinations)
                {
                    ReportTooComplex(node, context, $"{labels[c]} has {parts.Meshes.Count} convex parts");
                    return ScadGeo.Empty;
                }

                members.Add(InFrame(parts, context));
            }

            // Every step multiplies the number of members (a sum is made of every pair), so the last step is the largest
            long pairs = members[0].Count;
            for (var step = 1; step < members.Count; step++)
            {
                pairs *= members[step].Count;
                if (pairs > MaxMinkowskiCombinations)
                {
                    ReportTooComplex(node, context, $"a step needs {pairs} convex sums");
                    return ScadGeo.Empty;
                }
            }

            WarnColors(node, hasAppearance, context);

            // Each member is verified once here; the sums of the fold then skip the repeated test
            var meter = new WorkMeter();
            for (var c = 0; c < members.Count; c++)
            {
                var label = labels[c];
                if (members[c].Any(member => !VerifyMember(node, label, member, meter, context)))
                {
                    return ScadGeo.Empty;
                }
            }

            if (members.Count > 1)
            {
                return Fold(node, members, meter, context);
            }

            return hasAppearance ? StripAppearances(children[0], context) : children[0];
        }
        catch (ArgumentException)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.GeometryRejected,
                "minkowski: the sum is not valid geometry (for example a coordinate of the sum is outside the supported range); the minkowski is omitted",
                node.Location);
            return ScadGeo.Empty;
        }
    }

    /// <summary>
    ///     Verifies that one convex member of a child is a closed convex solid, charging the work of the test.
    /// </summary>
    /// <remarks>
    ///     The test is one hull of the vertices of the member. A lone child needs nothing else: a sum with a point
    ///     would be the child itself, so a verified lone child is returned as it is (without appearances), nothing
    ///     is created and nothing is charged against the triangle budget.
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="label">The description of the child the member belongs to.</param>
    /// <param name="member">The member.</param>
    /// <param name="meter">The work meter of the module.</param>
    /// <param name="context">The build context.</param>
    /// <returns><see langword="true"/> when the member is verified; <see langword="false"/> after a report.</returns>
    private static bool VerifyMember(ScadInstance node, string label, Mesh member, WorkMeter meter, ScadGeometryContext context)
    {
        context.Poll();
        if (!meter.TryCharge(member.VertexCount))
        {
            ReportTotalWork(node, context);
            return false;
        }

        try
        {
            MinkowskiSum.RequireConvexSolid(member, "operand", context.Cancellation);
            return true;
        }
        catch (NotSupportedException)
        {
            ReportNonConvex(node, context, $"{label} is not a closed convex solid");
            return false;
        }
        catch (ArgumentException)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.GeometryRejected,
                $"minkowski: {label} has no volume; the minkowski is omitted",
                node.Location);
            return false;
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
    ///     Removes appearances from a lone Minkowski child without changing its geometry or local transforms.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The geometry builder must remain safe for deeply nested models, so this scene copy is an iterative
    ///         post-order depth-first walk with an explicit stack rather than recursive calls. The copy of a node is
    ///         made only after every child has been copied: a node is expanded by pushing its completion frame and
    ///         then every child that has no copy yet, so a child still waiting deeper in the stack is simply
    ///         processed first and its stale frame is skipped when it surfaces. A node therefore has exactly one
    ///         copy however many parents reach it, which preserves shared (DAG-shaped) group or CSG subtrees, and
    ///         the walk takes time linear in the number of nodes and edges. The scene is immutable, so it has no
    ///         cycles.
    ///     </para>
    /// </remarks>
    /// <param name="child">The solid child whose appearances are to be removed.</param>
    /// <param name="context">The build context (cancellation and the work counter are polled per frame).</param>
    /// <returns>A solid with the same geometry and transforms and no appearances.</returns>
    internal static ScadGeo StripAppearances(ScadGeo child, ScadGeometryContext context)
    {
        var root = child.Node!;
        var copies = new Dictionary<SceneNode, SceneNode>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<(SceneNode Node, bool Expanded)>();
        stack.Push((root, false));

        while (stack.Count > 0)
        {
            context.Tick();
            var (node, expanded) = stack.Pop();

            // A shared node reached again after it was copied (or a duplicate frame of one that was) is done
            if (copies.ContainsKey(node))
            {
                continue;
            }

            if (!expanded)
            {
                stack.Push((node, true));
                switch (node)
                {
                    case GroupNode group:
                        PushPendingChildren(stack, group.Children, copies);
                        break;
                    case CsgNode csg:
                        PushPendingChildren(stack, csg.Children, copies);
                        break;
                }

                continue;
            }

            // Every child below the completion frame has been processed, so each one has a copy
            SceneNode copy = node switch
            {
                MeshNode mesh => CopyMeshNode(mesh, context),
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
    ///     Copies a mesh node without its appearance and records the copy in the location map under the location of
    ///     the original, so that a render-time report about the copy points at the instance that produced the mesh.
    /// </summary>
    /// <param name="original">The mesh node to copy.</param>
    /// <param name="context">The build context that owns the location map.</param>
    /// <returns>The copy, which shares the mesh and keeps the transform.</returns>
    private static MeshNode CopyMeshNode(MeshNode original, ScadGeometryContext context)
    {
        var copy = new MeshNode(original.Mesh) { Transform = original.Transform };
        context.Map.Add(copy, context.Map.Find(original));
        return copy;
    }

    /// <summary>
    ///     Pushes, in reverse order, every child that is still pending (has no copy yet) for the iterative appearance-stripping walk.
    /// </summary>
    /// <param name="stack">The walk stack.</param>
    /// <param name="children">The children.</param>
    /// <param name="copies">The copies made so far.</param>
    private static void PushPendingChildren(
        Stack<(SceneNode Node, bool Expanded)> stack,
        IReadOnlyList<SceneNode> children,
        Dictionary<SceneNode, SceneNode> copies)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (!copies.ContainsKey(children[i]))
            {
                stack.Push((children[i], false));
            }
        }
    }

    /// <summary>
    ///     Folds the Minkowski sum over the members of two or more children.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="members">The convex members of each child (at least two children), each already verified.</param>
    /// <param name="meter">The work meter of the module, shared with the verification of the members.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The sum, or nothing after a report.</returns>
    private static ScadGeo Fold(ScadInstance node, List<List<Mesh>> members, WorkMeter meter, ScadGeometryContext context)
    {
        var accumulated = members[0];
        var charges = new FoldCharges();
        var completed = false;
        try
        {
            // The number of sums of each step was bounded before the members were verified
            for (var step = 1; step < members.Count; step++)
            {
                var produced = SumStep(node, accumulated, members[step], meter, context, charges);
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
    /// <param name="meter">The work meter of the module.</param>
    /// <param name="context">The build context.</param>
    /// <param name="charges">The charges of the temporary sums; updated as sums are created and consumed.</param>
    /// <returns>The sums, or <see langword="null"/> after a pair above the work bound was reported.</returns>
    private static List<Mesh>? SumStep(
        ScadInstance node,
        List<Mesh> accumulated,
        List<Mesh> next,
        WorkMeter meter,
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
                var cost = PairCost(left, right);
                if (cost > MinkowskiSum.MaxPairwiseSums)
                {
                    ReportTooComplex(node, context, $"two parts have too many vertices to sum (limit {MinkowskiSum.MaxPairwiseSums} pairwise sums)");
                    return null;
                }

                if (!meter.TryCharge(cost))
                {
                    ReportTotalWork(node, context);
                    return null;
                }

                // The sum has at most 2 * (Va * Vb) - 4 triangles (the hull of that many points): the bound is charged,
                // and so checked against the remaining budget, before the sums array exists, then settled to the
                // actual count. Both operands are verified members or sums (hulls), so the repeated convexity test is
                // skipped
                var bound = Math.Max(0L, (2L * cost) - 4L);
                context.ChargeTriangles(bound, node.Location);
                var settled = false;
                Mesh sum;
                try
                {
                    context.NoteSumStarted();
                    sum = MinkowskiSum.ConvexSumOfVerified(left, right, context.Cancellation);
                    context.SettleTriangles(bound, sum.TriangleCount, node.Location);
                    settled = true;
                }
                finally
                {
                    // A failure or a cancellation returns the whole pre-charge of this sum
                    if (!settled)
                    {
                        context.RefundTriangles(bound);
                    }
                }

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
    ///     Gets the work of summing two meshes in pairwise vertex sums: the product of their vertex counts, with
    ///     distinct referenced vertices counted only when the raw product is over the bound of one pair.
    /// </summary>
    /// <param name="a">The first mesh.</param>
    /// <param name="b">The second mesh.</param>
    /// <returns>The number of pairwise sums; above <see cref="MinkowskiSum.MaxPairwiseSums"/> the pair is too large.</returns>
    private static long PairCost(Mesh a, Mesh b)
    {
        var raw = (long)a.VertexCount * b.VertexCount;
        return raw <= MinkowskiSum.MaxPairwiseSums ? raw : (long)DistinctVertexCount(a) * DistinctVertexCount(b);
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
    /// <param name="reason">The reason, as a plain description that names the child (it ends without punctuation).</param>
    private static void ReportNonConvex(ScadInstance node, ScadGeometryContext context, string reason) =>
        context.Reporter.Unsupported(
            ScadFeatureIds.MinkowskiNonConvex,
            $"minkowski: {reason}; only convex operands are supported and the minkowski is omitted",
            node.Location);

    /// <summary>
    ///     Reports that the cumulative work of the module is above the bound.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    private static void ReportTotalWork(ScadInstance node, ScadGeometryContext context) =>
        ReportTooComplex(node, context, $"the children need more than {MaxMinkowskiWork} pairwise vertex sums in total");

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
