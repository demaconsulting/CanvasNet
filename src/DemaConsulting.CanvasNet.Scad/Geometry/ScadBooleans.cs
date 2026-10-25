using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Combines the results of sibling instances: the implicit union, for 3D solids and for 2D shape trees.
/// </summary>
/// <remarks>
///     <para>
///         <b>Operands.</b> Every child instance is one operand: a group, a loop or a user module is that child's own
///         implicit union, counted once. A solid operand is a scene node and the union becomes a
///         <see cref="GroupNode"/>; a 2D operand is a <see cref="ScadShape2D"/> and the union becomes a
///         <see cref="ScadShape2DKind.Csg"/> node that an extrusion later distributes over (see
///         <see cref="ScadShape2D"/>).
///     </para>
///     <para>
///         <b>Dimensions.</b> The union of 2D and 3D operands keeps the 3D ones and warns about the dropped 2D ones
///         at the location of the first dropped 2D shape, so a mixed model is never silently mis-rendered. A union
///         with one operand is that operand, unchanged (no wrapper node).
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

            return Combine3D(solids);
        }

        return flats is null ? ScadGeo.Empty : Combine2D(flats);
    }

    /// <summary>
    ///     Combines solid operands into a union group.
    /// </summary>
    /// <param name="operands">The solid operands; at least one.</param>
    /// <returns>The combined result.</returns>
    private static ScadGeo Combine3D(List<ScadGeo> operands)
    {
        if (operands.Count == 1)
        {
            return operands[0];
        }

        var node = new GroupNode(operands.Select(o => o.Node!).ToList());
        var (bounds, tight, approximate) = UnionBounds(operands);
        return ScadGeo.FromSolid(node, bounds, tight, approximate);
    }

    /// <summary>
    ///     Combines flat operands into one union tree.
    /// </summary>
    /// <param name="operands">The flat operands; at least one.</param>
    /// <returns>The combined result.</returns>
    private static ScadGeo Combine2D(List<ScadGeo> operands)
    {
        if (operands.Count == 1)
        {
            return operands[0];
        }

        var shape = ScadShape2D.CreateCsg(CsgOperation.Union, operands.Select(o => o.Shape!).ToList());
        var (bounds, tight, approximate) = UnionBounds(operands);
        return ScadGeo.FromShape(shape, bounds, tight, approximate, operands[0].FlatLocation);
    }

    /// <summary>
    ///     Computes the bounds of a union from the bounds of its operands.
    /// </summary>
    /// <param name="operands">The operands (two or more).</param>
    /// <returns>The bounds and whether they are tight and approximate.</returns>
    private static (BoundingBox3D Bounds, bool IsTight, bool IsApproximate) UnionBounds(List<ScadGeo> operands)
    {
        var bounds = operands[0].Bounds;
        for (var i = 1; i < operands.Count; i++)
        {
            bounds = bounds.Union(operands[i].Bounds);
        }

        return (bounds, operands.All(o => o.IsTight), operands.Any(o => o.IsApproximate));
    }
}
