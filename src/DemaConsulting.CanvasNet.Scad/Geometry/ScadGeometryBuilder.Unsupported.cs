using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The module table: which built-in modules are built, and the placeholder that reports every other one.
/// </content>
internal sealed partial class ScadGeometryBuilder
{
    /// <summary>
    ///     Builds the geometry of one leaf module instance (its children, if any, are never visited).
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The geometry of the instance.</returns>
    private delegate ScadGeo LeafBuilder(ScadInstance node, ScadGeometryContext context);

    /// <summary>The leaf modules that are built, by name.</summary>
    private static readonly FrozenDictionary<string, LeafBuilder> LeafModules =
        new Dictionary<string, LeafBuilder>(StringComparer.Ordinal)
        {
            ["cube"] = ScadPrimitives.Cube,
            ["sphere"] = ScadPrimitives.Sphere,
            ["cylinder"] = ScadPrimitives.Cylinder,
            ["polyhedron"] = ScadPrimitives.Polyhedron,
            ["square"] = ScadPrimitives.Square,
            ["circle"] = ScadPrimitives.Circle,
            ["polygon"] = ScadPrimitives.Polygon,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The built-in modules whose children are combined as an implicit union.</summary>
    private static readonly FrozenSet<string> UnionModules =
        FrozenSet.ToFrozenSet(["union", "render"], StringComparer.Ordinal);

    /// <summary>
    ///     Reports a module that is recognized but not built, and yields nothing for it.
    /// </summary>
    /// <remarks>
    ///     The module's children are not visited, so what is inside an unsupported module is not reported
    ///     separately; the report names the module and carries its source location.
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns><see cref="ScadGeo.Empty"/>.</returns>
    private static ScadGeo ReportUnsupportedModule(ScadInstance node, ScadGeometryContext context)
    {
        context.Reporter.Unsupported(
            ScadFeatureIds.Module(node.Name),
            $"The module '{node.Name}' is not supported yet; it and its children are omitted",
            node.Location);
        return ScadGeo.Empty;
    }
}
