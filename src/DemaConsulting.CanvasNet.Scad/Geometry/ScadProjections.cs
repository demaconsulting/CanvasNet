using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The <c>projection</c> module: the 2D shadow of the 3D children on the XY plane (<c>cut = false</c>, the
///     default) or their cross-section at <c>z = 0</c> (<c>cut = true</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Result.</b> The result is a symbolic <see cref="ScadShape2D"/> tree of leaf regions and 2D booleans, exactly
///         like any other 2D result, so everything that consumes a 2D shape (transforms, extrusions, <c>hull</c>, 2D
///         booleans, the slabs of a top-level 2D shape) works on it; the package has no 2D or mesh boolean kernel and
///         projection does not add one. The children are the 3D results of the module's children (a union); 2D
///         children are ignored with a warning, <c>%</c> overlays are skipped, <c>#</c> copies are not counted twice
///         (the real subtree is projected, not its highlight), colors are not kept (a warning says so), and the
///         <c>convexity</c> argument is ignored. <c>cut</c> is honored when it is a boolean; any other value is
///         reported and counts as <c>false</c>. Nothing to project (no 3D child, or the plane misses every solid for
///         <c>cut = true</c>) is empty and silent.
///     </para>
///     <para>
///         <b>Silhouette (<c>cut = false</c>).</b> The projection of a union is the union of the projections, and the
///         projection of a closed solid is the union of the XY footprints of its upward-facing triangles (every
///         triangle that is not vertical for an open mesh, as the shadow of the back faces counts too). The triangles
///         of one mesh that share an edge and lie in one plane are merged into one region (the boundary edges are
///         chained into contours, holes are nesting), so a cube top is one rectangle and a cylinder cap one polygon;
///         a group that cannot be merged cleanly falls back to one region per triangle with the same area. The
///         projection of a <c>difference</c> or an <c>intersection</c> is not the projection of its operands, so each
///         maximal subtree rooted at one is classified exactly: every non-vertical face of its meshes (the upward
///         faces of the added operands and the downward faces of the subtracted ones) contributes the part of its
///         footprint where the solid is just below the face and not just above it. The "just below" and "just above"
///         sections are cross-sections of the boolean tree by the plane of the face, shifted by an infinitesimal
///         amount that is implemented as a tie rule of the slicer (see <c>ScadProjections.Slice.cs</c>). A tube is thus
///         an annulus, a through hole a hole and a blind pocket no hole.
///     </para>
///     <para>
///         <b>Cut (<c>cut = true</c>).</b> A cross-section commutes with the set operations, so the children are
///         sliced at <c>z = 0</c> and the boolean structure is kept symbolically (the section of a difference is the
///         difference of the sections). A face that lies exactly in the plane belongs to the section (closed-set
///         semantics): when a vertex is on the plane the section is the union of the sections just below and just above
///         it. The section of a closed mesh is a set of closed contours whose points are keyed by the mesh edge that was
///         cut (or by the vertex on the plane), not by coordinates, so the loops close exactly; a mesh whose section does
///         not close is reported (<see cref="ScadFeatureIds.ProjectionOpenMesh"/>) and the projection is omitted.
///     </para>
///     <para>
///         <b>Budget and cost.</b> Everything is bounded before it is allocated: the triangles of the children are
///         counted first (a model over <see cref="MaxProjectionWork"/> is reported as
///         <see cref="ScadFeatureIds.ProjectionTooComplex"/>) and the slabs the footprints could need (8 triangles per
///         candidate triangle, an upper bound) must fit the triangle budget (the located triangle-limit error otherwise);
///         every slice of the classification and the cut charges its triangles to a work meter with the same ceiling before it
///         runs, so a classification that would cost more is reported and the projection omitted, never approximated.
///         The transformed copies of the vertices are transient storage bounded by the size of meshes that are already
///         charged and are not charged separately, like the transient storage of <see cref="ScadHulls"/>. Everything
///         iterates over explicit stacks (no recursion), and cancellation is polled per slice.
///     </para>
///     <para>
///         The methods are stateless and thread-safe.
///     </para>
/// </remarks>
internal static partial class ScadProjections
{
    /// <summary>
    ///     The most work, in triangle visits, that one <c>projection</c> may do (2^23): the triangles of its children
    ///     and the triangles every slice of the classification or the cut visits.
    /// </summary>
    internal const long MaxProjectionWork = 1L << 23;

    /// <summary>The documented parameters of the module in positional order.</summary>
    private static readonly string[] Parameters = ["cut", "convexity"];

    /// <summary>
    ///     Builds <c>projection()</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The projection as a 2D result; nothing when there is nothing to project or after a report.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the footprints could not fit the triangle budget.</exception>
    public static ScadGeo Projection(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, Parameters, context);
        var cut = ReadCut(args, instance, context);
        var solids = results.Where(r => r.Kind == ScadGeoKind.Solid).ToList();
        if (results.Any(r => r.Kind == ScadGeoKind.Flat))
        {
            context.Reporter.Warn("projection: only 3D children can be projected; the 2D children are ignored", instance.Location);
        }

        if (solids.Count == 0)
        {
            return ScadGeo.Empty;
        }

        // Count first, then check the budget, and only then allocate anything
        var candidates = CountTriangles(solids, context);
        if (candidates > MaxProjectionWork)
        {
            ReportTooComplex(instance, context, $"its children have {candidates} triangles");
            return ScadGeo.Empty;
        }

        context.EnsureTriangles(8L * candidates, instance.Location);
        var tree = Collect(solids, context);
        if (tree.HasAppearance)
        {
            context.Reporter.Warn("projection: colors inside projection() are not kept; the result takes the enclosing color", instance.Location);
        }

        var state = new State(context, instance, tree.Extent, tree.MaxCoordinate);
        var shape = cut ? Cut(tree, state) : Silhouette(tree, state);
        if (state.Failure is { } failure)
        {
            context.Reporter.Unsupported(failure.Feature, failure.Message, instance.Location);
            return ScadGeo.Empty;
        }

        if (shape is null)
        {
            return ScadGeo.Empty;
        }

        var bounds = shape.ComputeVertexBounds();
        var approximate = !shape.IsUnionOnly() || state.Classified;
        return ScadGeo.FromShape(shape, bounds, !approximate, approximate, instance.Location);
    }

    /// <summary>
    ///     Reads the <c>cut</c> argument: a boolean is honored, anything else is reported and counts as false.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>Whether the module cuts.</returns>
    private static bool ReadCut(ScadArguments args, ScadInstance node, ScadGeometryContext context)
    {
        if (!args.TryGet("cut", out var value))
        {
            return false;
        }

        if (value.Kind == ScadValueKind.Boolean)
        {
            return value.AsBoolean;
        }

        context.Reporter.Warn("projection: 'cut' must be true or false; false is used", node.Location);
        return false;
    }

    /// <summary>
    ///     Reports a projection that exceeds the work ceiling.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="detail">What made it too complex.</param>
    private static void ReportTooComplex(ScadInstance node, ScadGeometryContext context, string detail) =>
        context.Reporter.Unsupported(
            ScadFeatureIds.ProjectionTooComplex,
            $"projection: {detail}, which is more than the {MaxProjectionWork} triangle visits one projection may do; the projection is omitted",
            node.Location);

    /// <summary>
    ///     Counts the triangles of the meshes below solid children (overlays excluded), walking iteratively.
    /// </summary>
    /// <param name="solids">The solid children.</param>
    /// <param name="context">The build context (cancellation is polled per node).</param>
    /// <returns>The number of triangles.</returns>
    private static long CountTriangles(List<ScadGeo> solids, ScadGeometryContext context)
    {
        long total = 0;
        var stack = new Stack<SceneNode>();
        foreach (var solid in solids)
        {
            stack.Push(solid.Node!);
        }

        while (stack.Count > 0)
        {
            context.Tick();
            switch (stack.Pop())
            {
                case MeshNode meshNode:
                    total += meshNode.Mesh.TriangleCount;
                    break;
                case GroupNode group:
                    PushAll(stack, group.Children);
                    break;
                case CsgNode csg:
                    PushAll(stack, csg.Children);
                    break;
                default:
                    break;
            }
        }

        return total;
    }

    /// <summary>
    ///     Pushes nodes onto a stack.
    /// </summary>
    /// <param name="stack">The stack.</param>
    /// <param name="children">The nodes.</param>
    private static void PushAll(Stack<SceneNode> stack, IReadOnlyList<SceneNode> children)
    {
        foreach (var child in children)
        {
            stack.Push(child);
        }
    }

    /// <summary>
    ///     Combines shapes into their union (one shape is returned as it is).
    /// </summary>
    /// <param name="shapes">The shapes.</param>
    /// <returns>The union, or <see langword="null"/> when there is none.</returns>
    private static ScadShape2D? UnionOf(List<ScadShape2D> shapes) => shapes.Count switch
    {
        0 => null,
        1 => shapes[0],
        _ => ScadShape2D.CreateCsg(CsgOperation.Union, shapes),
    };
}
