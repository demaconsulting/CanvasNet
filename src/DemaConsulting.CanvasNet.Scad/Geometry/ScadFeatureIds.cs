// cspell:ignore multmatrix
namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The stable identifiers of the unsupported features the geometry builder reports
///     (<see cref="ScadUnsupportedFeature.Feature"/>).
/// </summary>
/// <remarks>
///     The identifiers are lower-case and stable so callers can match on them; they are part of the documented
///     behavior of the package. The prefix of a module that is recognized but not built is
///     <see cref="ModulePrefix"/> followed by the module name; <c>hull</c> and <c>minkowski</c> are built and
///     report the specific <c>scad-hull-*</c> and <c>scad-minkowski-*</c> identifiers instead.
/// </remarks>
internal static class ScadFeatureIds
{
    /// <summary>The prefix of the identifier of a recognized module that is not built (followed by the module name).</summary>
    public const string ModulePrefix = "scad-module-";

    /// <summary>The <c>#</c> modifier on a 2D shape: the shape renders normally and is not highlighted (a 3D result is highlighted and not reported).</summary>
    public const string ModifierHighlight = "scad-modifier-highlight";

    /// <summary>The <c>%</c> modifier on a 2D shape: the shape is omitted (a 3D result is drawn as a ghost and not reported).</summary>
    public const string ModifierBackground = "scad-modifier-background";

    /// <summary>A <c>polygon</c> was rejected as invalid (self-touching, crossing, zero area, non-finite).</summary>
    public const string PolygonInvalid = "scad-polygon-invalid";

    /// <summary>A <c>polyhedron</c> face cannot be triangulated; the polyhedron is omitted.</summary>
    public const string PolyhedronFaceInvalid = "scad-polyhedron-face-invalid";

    /// <summary>Catch-all for a Render3D rejection of an input the argument checks accepted.</summary>
    public const string GeometryRejected = "scad-geometry-rejected";

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

    /// <summary>The renderer found an open mesh (boundary edges) as an operand of a boolean (mapped from <c>render3d-csg-open-mesh</c>).</summary>
    public const string CsgOpenMesh = "scad-csg-open-mesh";

    /// <summary>The renderer found an edge shared by three or more triangles in a boolean operand (mapped from <c>render3d-csg-non-manifold-edge</c>).</summary>
    public const string CsgNonManifoldEdge = "scad-csg-non-manifold-edge";

    /// <summary>The renderer found inconsistently wound triangles in a boolean operand (mapped from <c>render3d-csg-inconsistent-winding</c>).</summary>
    public const string CsgInconsistentWinding = "scad-csg-inconsistent-winding";

    /// <summary>The renderer found degenerate triangles in a boolean operand (mapped from <c>render3d-csg-degenerate-triangle</c>).</summary>
    public const string CsgDegenerateTriangle = "scad-csg-degenerate-triangle";

    /// <summary>The renderer found a shell wound inside out in a boolean operand (mapped from <c>render3d-csg-inverted-mesh</c>).</summary>
    public const string CsgInvertedMesh = "scad-csg-inverted-mesh";

    /// <summary>The composed transform of a boolean operand is singular (mapped from <c>render3d-csg-singular-transform</c>).</summary>
    public const string CsgSingularTransform = "scad-csg-singular-transform";

    /// <summary>A <c>hull</c> has a difference or an intersection among its children, which is not the hull of its operands' points.</summary>
    public const string HullCsgOperand = "scad-hull-csg-operand";

    /// <summary>A <c>hull</c> of points that span no volume (3D) or no area (2D) cannot be built.</summary>
    public const string HullDegenerate = "scad-hull-degenerate";

    /// <summary>A <c>hull</c> has both 2D and 3D children.</summary>
    public const string HullMixedDimensions = "scad-hull-mixed-dimensions";

    /// <summary>A <c>minkowski</c> has a child that is not provably a convex solid (a boolean, or a mesh that is not convex).</summary>
    public const string MinkowskiNonConvex = "scad-minkowski-nonconvex";

    /// <summary>A 2D <c>minkowski</c> is deferred.</summary>
    public const string Minkowski2D = "scad-minkowski-2d";

    /// <summary>A <c>minkowski</c> has both 2D and 3D children.</summary>
    public const string MinkowskiMixedDimensions = "scad-minkowski-mixed-dimensions";

    /// <summary>A <c>minkowski</c> needs more pairwise vertex sums or convex combinations than the documented bounds.</summary>
    public const string MinkowskiTooComplex = "scad-minkowski-too-complex";

    /// <summary>
    ///     Gets the identifier of a module that is recognized but not built.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <returns>The identifier, <c>scad-module-{name}</c> with underscores kept.</returns>
    public static string Module(string moduleName) => ModulePrefix + moduleName;
}
