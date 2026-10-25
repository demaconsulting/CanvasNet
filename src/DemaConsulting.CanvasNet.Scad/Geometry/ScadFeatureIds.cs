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

    /// <summary>A <c>color</c> with alpha below 1 was drawn opaque.</summary>
    public const string ColorAlpha = "scad-color-alpha";

    /// <summary>A <c>resize</c> of children that contain a difference or an intersection used estimated bounds.</summary>
    public const string ResizeApproximate = "scad-resize-approximate";

    /// <summary>A <c>multmatrix</c> with a projective (non-affine) bottom row was omitted.</summary>
    public const string MultmatrixProjective = "scad-multmatrix-projective";

    /// <summary>A <c>linear_extrude</c> with <c>scale = 0</c> over a profile with holes or islands was omitted.</summary>
    public const string LinearExtrudeApexMultipleContours = "scad-linear-extrude-apex-multiple-contours";

    /// <summary>A <c>linear_extrude</c> with a negative scale component, or exactly one zero component, was omitted.</summary>
    public const string LinearExtrudeScale = "scad-linear-extrude-scale";

    /// <summary>A <c>rotate_extrude</c> profile that lies on both sides of the axis was omitted.</summary>
    public const string RotateExtrudeAxisCrossing = "scad-rotate-extrude-axis-crossing";

    /// <summary>A <c>rotate_extrude</c> profile that touches the axis at an isolated vertex was omitted.</summary>
    public const string RotateExtrudeAxisContact = "scad-rotate-extrude-axis-contact";

    /// <summary>
    ///     Gets the identifier of a module that is recognized but not built.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <returns>The identifier, <c>scad-module-{name}</c> with underscores kept.</returns>
    public static string Module(string moduleName) => ModulePrefix + moduleName;
}
