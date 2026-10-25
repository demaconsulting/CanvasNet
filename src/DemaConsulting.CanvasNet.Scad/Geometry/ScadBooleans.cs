using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Combines the results of sibling instances: the implicit union and the <c>difference</c> and
///     <c>intersection</c> modules, for 3D solids and for 2D shape trees.
/// </summary>
/// <remarks>
///     <para>
///         <b>Operands.</b> Every child instance is one operand: a group, a loop or a user module under a boolean
///         is that child's own implicit union, counted once. A solid operand is a scene node and the boolean becomes
///         a <see cref="CsgNode"/>; a 2D operand is a <see cref="ScadShape2D"/> and the boolean becomes a
///         <see cref="ScadShape2DKind.Csg"/> node that an extrusion later distributes over (see
///         <see cref="ScadShape2D"/>).
///     </para>
///     <para>
///         <b>Position-preserving rules.</b> <c>difference</c> removes the later operands from the <i>first</i>
///         operand, so an empty first operand makes the result empty while empty later operands simply vanish;
///         <c>intersection</c> of anything with nothing is nothing, so any empty operand makes the result empty; a
///         boolean with a single remaining operand is that operand, unchanged (no wrapper node).
///     </para>
///     <para>
///         <b>Dimensions.</b> The union of 2D and 3D operands keeps the 3D ones; <c>difference</c> and
///         <c>intersection</c> keep the operands that have the dimension of the first non-empty operand; in every
///         case the dropped operands are reported with a warning at the location of the first dropped 2D shape or of
///         the module, so a mixed model is never silently mis-rendered.
///     </para>
///     <para>
///         The methods are stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ScadBooleans
{
    /// <summary>
    ///     Combines results into their implicit union (the <c>union</c> and <c>render</c> modules, groups and the
    ///     top level).
    /// </summary>
    /// <remarks>
    ///     Empty results vanish. Solids become one <see cref="GroupNode"/> (or the single solid itself); 2D shapes
    ///     become one union node. When both dimensions are present the 2D shapes are dropped with a warning at the
    ///     location of the first dropped shape.
    /// </remarks>
    /// <param name="node">The instance (unused by a union), or <see langword="null"/>.</param>
    /// <param name="results">The results of the operands, in order.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The union.</returns>
    public static ScadGeo Union(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        List<ScadGeo>? solids = null;
        List<ScadGeo>? flats = null;
        foreach (var result in results)
        {
            if (result.Kind == ScadGeoKind.Solid)
            {
                (solids ??= []).Add(result);
            }
            else if (result.Kind == ScadGeoKind.Flat)
            {
                (flats ??= []).Add(result);
            }
        }

        if (solids is not null)
        {
            // Mixed dimensions: keep the 3D members and say what was dropped
            if (flats is not null)
            {
                context.Reporter.Warn("A union mixes 2D and 3D objects; the 2D objects are ignored", flats[0].FlatLocation);
            }

            return Combine3D(CsgOperation.Union, solids);
        }

        return flats is null ? ScadGeo.Empty : Combine2D(CsgOperation.Union, flats);
    }

    /// <summary>
    ///     Combines results into the difference of the first and all the others (the <c>difference</c> module).
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the operands, in order; the first is the minuend.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The difference; empty when the first operand is empty.</returns>
    public static ScadGeo Difference(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        if (results.Count == 0 || results[0].Kind == ScadGeoKind.Empty)
        {
            return ScadGeo.Empty;
        }

        // Later operands that are empty vanish
        var operands = results.Where(r => r.Kind != ScadGeoKind.Empty).ToList();
        return Combine(CsgOperation.Difference, operands, node!, context);
    }

    /// <summary>
    ///     Combines results into their intersection (the <c>intersection</c> module).
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the operands, in order.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The intersection; empty when there is no operand or any operand is empty.</returns>
    public static ScadGeo Intersection(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        if (results.Count == 0 || results.Any(r => r.Kind == ScadGeoKind.Empty))
        {
            return ScadGeo.Empty;
        }

        return Combine(CsgOperation.Intersection, results, node!, context);
    }

    /// <summary>
    ///     Combines non-empty operands with a difference or an intersection after resolving mixed dimensions.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="operands">The non-empty operands; at least one.</param>
    /// <param name="node">The instance of the boolean module.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The combination.</returns>
    private static ScadGeo Combine(CsgOperation operation, List<ScadGeo> operands, ScadInstance node, ScadGeometryContext context)
    {
        // The first operand decides the dimension; the others must match it
        var kind = operands[0].Kind;
        if (operands.Any(o => o.Kind != kind))
        {
            context.Reporter.Warn(
                $"{node.Name} mixes 2D and 3D objects; the objects that differ in dimension from the first one are ignored",
                node.Location);
            operands = operands.Where(o => o.Kind == kind).ToList();
        }

        if (operands.Count == 1)
        {
            return operands[0];
        }

        return kind == ScadGeoKind.Solid ? Combine3D(operation, operands) : Combine2D(operation, operands);
    }

    /// <summary>
    ///     Combines solid operands into a union group or a boolean node.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="operands">The solid operands; at least one.</param>
    /// <returns>The combined result.</returns>
    private static ScadGeo Combine3D(CsgOperation operation, List<ScadGeo> operands)
    {
        if (operands.Count == 1)
        {
            return operands[0];
        }

        var nodes = operands.Select(o => o.Node!).ToList();
        SceneNode node = operation == CsgOperation.Union ? new GroupNode(nodes) : new CsgNode(operation, nodes);
        var (bounds, tight, approximate) = CombineBounds(operation, operands);
        return ScadGeo.FromSolid(node, bounds, tight, approximate);
    }

    /// <summary>
    ///     Combines flat operands into one boolean tree.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="operands">The flat operands; at least one.</param>
    /// <returns>The combined result.</returns>
    private static ScadGeo Combine2D(CsgOperation operation, List<ScadGeo> operands)
    {
        if (operands.Count == 1)
        {
            return operands[0];
        }

        var shape = ScadShape2D.CreateCsg(operation, operands.Select(o => o.Shape!).ToList());
        var (bounds, tight, approximate) = CombineBounds(operation, operands);
        return ScadGeo.FromShape(shape, bounds, tight, approximate, operands[0].FlatLocation);
    }

    /// <summary>
    ///     Computes the bounds of a combination from the bounds of its operands.
    /// </summary>
    /// <remarks>
    ///     A union has the union of the boxes; a difference at most the extent of its first operand; an intersection
    ///     at most the overlap of the boxes. The last two are upper bounds only, so the result is approximate.
    /// </remarks>
    /// <param name="operation">The operation.</param>
    /// <param name="operands">The operands (two or more).</param>
    /// <returns>The bounds and whether they are tight and approximate.</returns>
    private static (BoundingBox3D Bounds, bool IsTight, bool IsApproximate) CombineBounds(CsgOperation operation, List<ScadGeo> operands)
    {
        var tight = operands.All(o => o.IsTight);
        var approximate = operands.Any(o => o.IsApproximate) || operation != CsgOperation.Union;
        var bounds = operands[0].Bounds;
        switch (operation)
        {
            case CsgOperation.Union:
                for (var i = 1; i < operands.Count; i++)
                {
                    bounds = bounds.Union(operands[i].Bounds);
                }

                break;
            case CsgOperation.Intersection:
                for (var i = 1; i < operands.Count; i++)
                {
                    bounds = Overlap(bounds, operands[i].Bounds);
                }

                break;
        }

        return (bounds, tight, approximate);
    }

    /// <summary>
    ///     Computes the overlap of two boxes.
    /// </summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <returns>The overlap, or an empty box when they do not overlap.</returns>
    private static BoundingBox3D Overlap(BoundingBox3D a, BoundingBox3D b)
    {
        var min = Vector3D.Max(a.Min, b.Min);
        var max = Vector3D.Min(a.Max, b.Max);
        var box = new BoundingBox3D(min, max);
        return box.IsEmpty ? BoundingBox3D.Empty : box;
    }
}
