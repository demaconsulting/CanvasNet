namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The stable identifiers of the unsupported features the geometry builder reports
///     (<see cref="ScadUnsupportedFeature.Feature"/>).
/// </summary>
/// <remarks>
///     The identifiers are lower-case and stable so callers can match on them; they are part of the documented
///     behavior of the package. The prefix of a module that is recognized but not built is
///     <see cref="ModulePrefix"/> followed by the module name.
/// </remarks>
internal static class ScadFeatureIds
{
    /// <summary>The prefix of the identifier of a recognized module that is not built (followed by the module name).</summary>
    public const string ModulePrefix = "scad-module-";

    /// <summary>The <c>#</c> modifier was degraded: the subtree renders normally and is not highlighted.</summary>
    public const string ModifierHighlight = "scad-modifier-highlight";

    /// <summary>The <c>%</c> modifier omitted its subtree.</summary>
    public const string ModifierBackground = "scad-modifier-background";

    /// <summary>A <c>polygon</c> was rejected as invalid (self-touching, crossing, zero area, non-finite).</summary>
    public const string PolygonInvalid = "scad-polygon-invalid";

    /// <summary>A <c>polyhedron</c> face cannot be triangulated; the polyhedron is omitted.</summary>
    public const string PolyhedronFaceInvalid = "scad-polyhedron-face-invalid";

    /// <summary>Catch-all for a Render3D rejection of an input the argument checks accepted.</summary>
    public const string GeometryRejected = "scad-geometry-rejected";

    /// <summary>
    ///     Gets the identifier of a module that is recognized but not built.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <returns>The identifier, <c>scad-module-{name}</c> with underscores kept.</returns>
    public static string Module(string moduleName) => ModulePrefix + moduleName;
}
