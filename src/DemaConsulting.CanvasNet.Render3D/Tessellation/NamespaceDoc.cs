namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

// cspell:ignore tessellator tessellators polyhedra polyhedron triangulator triangulation extrusion extrusions

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Render3D.Tessellation"/> namespace converts
///     analytic primitives and two-dimensional polygon regions into <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel.Mesh"/>
///     instances: <see cref="PrimitiveTessellator"/> builds boxes, UV spheres, cylinders and
///     cones (frusta), validated polyhedra and 2D shapes; <see cref="PolygonRegion"/> is a validated polygon
///     with holes, <see cref="PolygonTriangulator"/> triangulates it exactly, and <see cref="Extruder"/> turns it into
///     a solid by linear (optionally twisted and scaled) or rotational extrusion. <see cref="ConvexHull"/> computes
///     exact three-dimensional and planar convex hulls of point sets, and <see cref="MinkowskiSum"/> the Minkowski sum
///     of two convex solids (rejecting non-convex operands instead of approximating them).
/// </summary>
/// <remarks>
///     <para>
///         Every generated mesh is welded (each vertex is created once and shared by index), closed
///         (every edge is shared by exactly two triangles with opposite directions) and wound
///         counter-clockwise when seen from outside, so it can be used directly by the renderer and
///         as an operand of constructive-solid-geometry operations
///         (<see cref="DemaConsulting.CanvasNet.Render3D.SceneModel.CsgNode"/>). Invalid polygons are
///         rejected with an exception when the region is created, never turned into a defective mesh.
///     </para>
///     <para>
///         See <see cref="PrimitiveTessellator"/> for the common solids and <see cref="Extruder"/> for extruded profiles.
///         The meshes become nodes of a <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel.Scene"/> (see
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel"/>), which
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneRendering"/> draws. <c>DemaConsulting.CanvasNet.Scad</c> uses
///         this namespace to build the geometry of OpenSCAD models.
///     </para>
/// </remarks>
/// <example>
///     Extrude a rectangle with a rectangular hole into a plate, and check that it is a closed solid:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Render3D.SceneModel;
///     using DemaConsulting.CanvasNet.Render3D.Tessellation;
///
///     // Points 0-3 are the outer contour and 4-7 the hole (a nested contour is a hole)
///     Vector2D[] points =
///     [
///         new(0, 0), new(20, 0), new(20, 10), new(0, 10),
///         new(5, 3), new(15, 3), new(15, 7), new(5, 7),
///     ];
///     var region = PrimitiveTessellator.Polygon(points, [[0, 1, 2, 3], [4, 5, 6, 7]]);
///     var plate = Extruder.Linear(region, 3);
///     Console.WriteLine($"closed: {plate.IsClosed}, volume: {plate.SignedVolume}");   // 480 = (200 - 40) * 3
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
