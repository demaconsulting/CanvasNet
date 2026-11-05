using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The anchor pass: world points of the declared anchors.
/// </content>
internal sealed partial class ScadGeometryBuilder
{
    /// <summary>
    ///     The containers an anchor may sit inside without its point changing meaning: they combine or color their
    ///     children but never move them. (The matrix transforms are handled separately; groups are by kind.)
    /// </summary>
    private static readonly FrozenSet<string> AnchorPassThroughModules =
        new[] { "union", "render", "difference", "intersection", "color", "hull", "minkowski" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     One entry of the explicit stack of the anchor pass.
    /// </summary>
    /// <param name="Node">The instance to visit.</param>
    /// <param name="Matrix">The accumulated model-to-world matrix of the instance's parent (row-vector convention).</param>
    /// <param name="Blocker">The name of the enclosing module whose coordinate change is not supported, or <see langword="null"/>.</param>
    /// <param name="Dropped">Whether an enclosing transform is invalid, so the anchors below are dropped silently with their geometry.</param>
    private readonly record struct AnchorWalk(ScadInstance Node, Matrix4x4D Matrix, string? Blocker, bool Dropped);

    /// <summary>
    ///     Collects the declared anchors of the model with the enclosing transforms applied.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Evaluation has no transform context, so the anchors are resolved here: a walk over the same forest the
    ///         build used keeps the accumulated matrix of the enclosing <c>translate</c>, <c>rotate</c>,
    ///         <c>scale</c>, <c>mirror</c> and <c>multmatrix</c> instances, taking each matrix from
    ///         <see cref="ScadTransforms.TryGetMatrix"/> (the definition the geometry builders use), and applies it to
    ///         the anchor's point. Groups and the containers in <see cref="AnchorPassThroughModules"/> pass the matrix
    ///         through unchanged.
    ///     </para>
    ///     <para>
    ///         An anchor inside any other module (<c>resize</c>, the extrusions, <c>projection</c>, an unsupported
    ///         module) cannot be located exactly and is reported with a <c>scad-anchor-invalid</c> warning and
    ///         skipped; an anchor inside a transform that is not finite or collapses both solids and 2D shapes is
    ///         dropped silently, because its geometry is dropped and already reported (an anchor cannot tell 2D
    ///         siblings from solid ones, so a transform that only collapses Z, or only the XY plane, keeps it, projected
    ///         by the transform). Matrix problems are reported to a throw-away reporter so the builders'
    ///         own warnings are not duplicated. The walk is iterative like the build itself and returns the anchors in
    ///         document (echo) order.
    ///     </para>
    /// </remarks>
    /// <param name="forest">The sibling instances the build used (the marked subtrees when the model has <c>!</c>).</param>
    /// <param name="settings">The settings of the build.</param>
    /// <returns>The anchors with their world points, in document order.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token in <paramref name="settings"/> is canceled.</exception>
    private List<ScadWorldAnchor> CollectAnchors(IReadOnlyList<ScadInstance> forest, ScadGeometrySettings settings)
    {
        var anchors = new List<ScadWorldAnchor>();

        // The matrix helpers report into a context nobody reads, so a bad transform is not warned about twice
        var silent = new ScadGeometryContext(settings, new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
        var stack = new Stack<AnchorWalk>();
        for (var i = forest.Count - 1; i >= 0; i--)
        {
            stack.Push(new AnchorWalk(forest[i], Matrix4x4D.Identity, null, false));
        }

        while (stack.Count > 0)
        {
            var (node, parent, blocker, dropped) = stack.Pop();
            _context.Tick();

            // An anchor is a leaf: resolve it against the matrix of its ancestors
            if (node.Kind == ScadInstanceKind.Anchor)
            {
                ResolveAnchor(node, parent, blocker, dropped, anchors);
                continue;
            }

            // Work out what the children of this instance see
            var matrix = parent;
            if (!dropped && blocker is null)
            {
                if (ScadTransforms.TryGetMatrix(node, silent, out var own))
                {
                    // Row-vector convention: the instance's own matrix applies first, then its ancestors'
                    matrix = own * parent;
                    // An anchor cannot know whether its siblings are 2D or solid, so it is dropped only when the
                    // transform would drop geometry of either dimension (solid: singular in 3D; 2D: singular in XY).
                    // One that only loses Z (scale([1,1,0])) or only the XY plane (rotate([90,0,0])) is kept, projected
                    dropped = ScadTransforms.Collapses(own) && ScadTransforms.CollapsesPlanar(own);
                }
                else if (node.Kind == ScadInstanceKind.BuiltinModule && IsMatrixTransform(node.Name))
                {
                    dropped = true;
                }
                else if (node.Kind != ScadInstanceKind.Group && !AnchorPassThroughModules.Contains(node.Name))
                {
                    blocker = node.Name;
                }
            }

            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(new AnchorWalk(node.Children[i], matrix, blocker, dropped));
            }
        }

        return anchors;
    }

    /// <summary>
    ///     Resolves one anchor instance to a world anchor, or reports why it cannot be located.
    /// </summary>
    /// <param name="node">The anchor instance.</param>
    /// <param name="matrix">The accumulated matrix of its ancestors.</param>
    /// <param name="blocker">The enclosing module with an unsupported coordinate change, or <see langword="null"/>.</param>
    /// <param name="dropped">Whether an enclosing transform is invalid (the anchor is dropped silently).</param>
    /// <param name="anchors">The list that receives the world anchor.</param>
    private void ResolveAnchor(ScadInstance node, Matrix4x4D matrix, string? blocker, bool dropped, List<ScadWorldAnchor> anchors)
    {
        // The geometry below an invalid transform is dropped and already reported; the anchor goes with it
        if (dropped)
        {
            return;
        }

        if (blocker is not null)
        {
            _context.Reporter.Warn(
                $"{ScadEvaluator.AnchorInvalidCode}: the anchor '{node.Name}' is inside {blocker}, whose coordinate change is not supported; the anchor is skipped",
                node.Location);
            return;
        }

        // The evaluator guarantees a vector of three finite numbers
        var vector = node.Arguments[0].Value.AsVector;
        var point = matrix.TransformPoint(new Vector3D(vector[0].AsNumber, vector[1].AsNumber, vector[2].AsNumber));
        if (!(Math.Abs(point.X) <= ScadGeo.MaxCoordinate && Math.Abs(point.Y) <= ScadGeo.MaxCoordinate && Math.Abs(point.Z) <= ScadGeo.MaxCoordinate))
        {
            _context.Reporter.Warn(
                $"{ScadEvaluator.AnchorInvalidCode}: the anchor '{node.Name}' is not finite or exceeds 1e100 in magnitude after the transforms; the anchor is skipped",
                node.Location);
            return;
        }

        anchors.Add(new ScadWorldAnchor(node.Name, point, node.Location));
    }

    /// <summary>
    ///     Tells whether a built-in module name is one of the matrix transforms.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <returns><see langword="true"/> for <c>translate</c>, <c>rotate</c>, <c>scale</c>, <c>mirror</c> and <c>multmatrix</c>.</returns>
    private static bool IsMatrixTransform(string name) =>
        name is "translate" or "rotate" or "scale" or "mirror" or "multmatrix";
}
