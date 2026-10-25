// cspell:ignore multmatrix minkowski
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <content>
///     The module table: which built-in modules are built, how, and the report for every other one.
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

    /// <summary>
    ///     Combines the results of the children of a container instance into the result of the instance.
    /// </summary>
    /// <param name="node">The instance, or <see langword="null"/> for the top-level forest (an implicit union).</param>
    /// <param name="results">The results of the children in order, one per child, empty ones included.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The geometry of the instance.</returns>
    internal delegate ScadGeo ContainerBuilder(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context);

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

    /// <summary>The built-in modules that combine, transform or extrude the results of their children, by name.</summary>
    private static readonly FrozenDictionary<string, ContainerBuilder> ContainerModules =
        new Dictionary<string, ContainerBuilder>(StringComparer.Ordinal)
        {
            ["union"] = ScadBooleans.Union,
            ["render"] = ScadBooleans.Union,
            ["difference"] = ScadBooleans.Difference,
            ["intersection"] = ScadBooleans.Intersection,
            ["translate"] = ScadTransforms.Translate,
            ["rotate"] = ScadTransforms.Rotate,
            ["scale"] = ScadTransforms.Scale,
            ["mirror"] = ScadTransforms.Mirror,
            ["multmatrix"] = ScadTransforms.Multmatrix,
            ["resize"] = ScadTransforms.Resize,
            ["color"] = ScadColors.Color,
            ["linear_extrude"] = ScadExtrusions.LinearExtrude,
            ["rotate_extrude"] = ScadExtrusions.RotateExtrude,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Reports a module that is recognized but not supported, and yields nothing for it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The modules that reach this method are <c>text</c>, <c>offset</c>, <c>projection</c>, <c>surface</c>,
    ///         <c>import</c>, <c>roof</c>, <c>hull</c> and <c>minkowski</c>. The module's children are not visited, so
    ///         what is inside an unsupported module is not reported separately; the report names the module and
    ///         carries its source location.
    ///     </para>
    /// </remarks>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <returns><see cref="ScadGeo.Empty"/>.</returns>
    private static ScadGeo ReportUnsupportedModule(ScadInstance node, ScadGeometryContext context)
    {
        context.Reporter.Unsupported(
            ScadFeatureIds.Module(node.Name),
            $"The module '{node.Name}' is not supported; it and its children are omitted",
            node.Location);
        return ScadGeo.Empty;
    }
}
