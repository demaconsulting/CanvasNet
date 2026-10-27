using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The <c>!</c> (show only) pre-pass and the <c>#</c> and <c>%</c> modifiers.
/// </content>
internal sealed partial class ScadGeometryBuilder
{
    /// <summary>
    ///     Applies the <c>#</c> (highlight) and <c>%</c> (background) modifiers of an instance to its finished result.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b><c>%</c> (ghost).</b> The solid becomes an <see cref="OverlayNode"/> in
    ///         <see cref="ScadGeometryContext.GhostColor"/>: it is drawn translucent, but it is <i>false</i> in every
    ///         enclosing boolean (it does not add to a union, does not subtract from a difference, and an
    ///         intersection with it is empty), and it contributes no bounds, so <c>resize</c> and the
    ///         propagated bounds ignore it. It does count as reach, so the coordinate guard still sees its vertices.
    ///         A ghost as the <i>first</i> operand of a <c>difference</c> therefore leaves the difference empty.
    ///         When both modifiers are present the background wins.
    ///     </para>
    ///     <para>
    ///         <b><c>#</c> (highlight).</b> The solid is kept exactly as it is and paired with an
    ///         <see cref="OverlayNode"/> in <see cref="ScadGeometryContext.HighlightColor"/> that holds the <i>same</i>
    ///         node, so the highlight adds no mesh, and the subtree takes part in the booleans
    ///         around it as if the modifier were absent: a highlighted subtrahend still cuts, and its translucent
    ///         body is drawn where it passed through the cut. The renderer draws both paths to every mesh, so the
    ///         triangles of the subtree are charged to the triangle budget a second time (a nested highlight
    ///         therefore cannot multiply the work beyond <see cref="ScadLimits.MaxTriangles"/>).
    ///     </para>
    ///     <para>
    ///         <b>2D shapes.</b> A result that is still flat has no overlay form (the overlay is a body of solids): a
    ///         <c>#</c> shape renders normally and a <c>%</c> shape is omitted, and both are reported as unsupported
    ///         (<see cref="ScadFeatureIds.ModifierHighlight"/>, <see cref="ScadFeatureIds.ModifierBackground"/>). The
    ///         modifier on an extrusion of the shape, a 3D result, works completely.
    ///     </para>
    /// </remarks>
    /// <param name="node">The instance that produced the result.</param>
    /// <param name="geo">The result of the instance.</param>
    /// <returns>The result, with the overlay applied; unchanged when the instance has neither modifier or built nothing.</returns>
    private ScadGeo ApplyModifiers(ScadInstance node, ScadGeo geo)
    {
        var background = (node.Modifiers & ScadModifiers.Background) != 0;
        var highlight = (node.Modifiers & ScadModifiers.Highlight) != 0;
        if ((!background && !highlight) || geo.Kind == ScadGeoKind.Empty)
        {
            return geo;
        }

        if (geo.Kind == ScadGeoKind.Flat)
        {
            return background ? OmitBackgroundShape(node) : ReportHighlightedShape(node, geo);
        }

        if (background)
        {
            var ghost = new OverlayNode(_context.GhostColor, [geo.Node!]);
            return ScadGeo.FromSolid(ghost, BoundingBox3D.Empty, true, false, geo.Reach);
        }

        var highlighted = new OverlayNode(_context.HighlightColor, [geo.Node!]);

        // The copy shares the meshes but the renderer draws every path to a mesh, so the copy is charged like geometry
        // (this also bounds nested highlights, whose paths would otherwise double with every level)
        _context.ChargeTriangles(EffectiveTriangles(geo.Node!), node.Location);
        return ScadGeo.FromSolid(new GroupNode([geo.Node!, highlighted]), geo.Bounds, geo.IsTight, geo.IsApproximate, geo.Reach);
    }

    /// <summary>
    ///     Counts the triangles the renderer draws for a node: the triangles of every mesh once per path from the node
    ///     to it (a shared mesh counts every time it is reached), saturating far above any budget.
    /// </summary>
    /// <remarks>
    ///     The result of every node is remembered, so repeated calls over nested highlights only visit the nodes added
    ///     since the last call. The walk is iterative and polls cancellation.
    /// </remarks>
    /// <param name="root">The node.</param>
    /// <returns>The effective triangle count, at most <see cref="EffectiveCap"/>.</returns>
    private long EffectiveTriangles(SceneNode root)
    {
        var stack = new Stack<SceneNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            if (_effectiveTriangles.ContainsKey(current))
            {
                stack.Pop();
                continue;
            }

            _context.Tick();
            var children = ChildrenOf(current);
            var pending = false;
            foreach (var child in children.Where(c => !_effectiveTriangles.ContainsKey(c)))
            {
                stack.Push(child);
                pending = true;
            }

            if (pending)
            {
                continue;
            }

            stack.Pop();
            var sum = current is MeshNode mesh ? (long)mesh.Mesh.TriangleCount : 0L;
            foreach (var child in children)
            {
                sum = Math.Min(EffectiveCap, sum + _effectiveTriangles[child]);
            }

            _effectiveTriangles[current] = sum;
        }

        return _effectiveTriangles[root];
    }

    /// <summary>
    ///     Gets the children of a scene node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The children; empty for a mesh.</returns>
    private static IReadOnlyList<SceneNode> ChildrenOf(SceneNode node) => node switch
    {
        GroupNode group => group.Children,
        CsgNode csg => csg.Children,
        OverlayNode overlay => overlay.Children,
        _ => [],
    };

    /// <summary>
    ///     Reports a <c>%</c> on a 2D shape and omits the shape.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <returns>The empty result.</returns>
    private ScadGeo OmitBackgroundShape(ScadInstance node)
    {
        _context.Reporter.Unsupported(
            ScadFeatureIds.ModifierBackground,
            "The '%' modifier on a 2D shape is not supported; the shape is omitted (extrude it to draw a ghost)",
            node.Location);
        return ScadGeo.Empty;
    }

    /// <summary>
    ///     Reports a <c>#</c> on a 2D shape; the shape is rendered normally.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="geo">The result, returned unchanged.</param>
    /// <returns>The result.</returns>
    private ScadGeo ReportHighlightedShape(ScadInstance node, ScadGeo geo)
    {
        _context.Reporter.Unsupported(
            ScadFeatureIds.ModifierHighlight,
            "The '#' modifier on a 2D shape is not highlighted; the shape is rendered normally (extrude it to draw a highlight)",
            node.Location);
        return geo;
    }

    /// <summary>
    ///     Finds the outermost instances marked with <c>!</c>, in document order.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The search is an explicit-stack depth-first walk that never descends into a marked subtree (a
    ///         <c>!</c> nested inside a marked subtree has no further effect) nor into a <c>%</c> subtree (that
    ///         subtree is a ghost, not part of the model proper, so a mark inside it does not take part in the model). It visits every other
    ///         instance regardless of its module, including modules that are not built, so a mark below an
    ///         unsupported module is still honored.
    ///     </para>
    ///     <para>
    ///         The marked subtrees become the whole model without the transforms and colors of their ancestors:
    ///         the engine simply builds them instead of the roots.
    ///     </para>
    /// </remarks>
    /// <param name="roots">The top-level instances.</param>
    /// <param name="context">The build context, which polls cancellation on its fixed schedule once per visited instance.</param>
    /// <returns>The marked instances in document order; empty when the model has no <c>!</c>.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token of the build is canceled during the walk.</exception>
    private static List<ScadInstance> FindShowOnlyRoots(IReadOnlyList<ScadInstance> roots, ScadGeometryContext context)
    {
        var found = new List<ScadInstance>();
        var stack = new Stack<ScadInstance>();
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push(roots[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            context.Tick();
            if ((node.Modifiers & ScadModifiers.ShowOnly) != 0)
            {
                found.Add(node);
                continue;
            }

            if ((node.Modifiers & ScadModifiers.Background) != 0)
            {
                continue;
            }

            // Push in reverse so the first child is visited first
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }

        return found;
    }
}
