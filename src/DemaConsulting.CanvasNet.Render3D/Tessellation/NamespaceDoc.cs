namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

// cspell:ignore tessellator tessellators polyhedra polyhedron triangulator triangulation extrusion extrusions

/// <summary>
///     The <c>Tessellation</c> namespace converts
///     analytic primitives and two-dimensional polygon regions into <c>Mesh</c>
///     instances: <c>PrimitiveTessellator</c> builds boxes, UV spheres, cylinders and
///     cones (frusta), validated polyhedra and 2D shapes; <c>PolygonRegion</c> is a validated polygon
///     with holes, <c>PolygonTriangulator</c> triangulates it exactly, and <c>Extruder</c> turns it into
///     a solid by linear (optionally twisted and scaled) or rotational extrusion. <c>ConvexHull</c> computes
///     exact three-dimensional and planar convex hulls of point sets, and <c>MinkowskiSum</c> the Minkowski sum
///     of two convex solids (rejecting non-convex operands instead of approximating them).
/// </summary>
/// <remarks>
///     Every generated mesh is welded (each vertex is created once and shared by index), closed
///     (every edge is shared by exactly two triangles with opposite directions) and wound
///     counter-clockwise when seen from outside, so it can be used directly by the renderer and
///     as an operand of constructive-solid-geometry operations (<c>CsgNode</c>). Invalid polygons are
///     rejected with an exception when the region is created, never turned into a defective mesh.
/// </remarks>
internal static class NamespaceDoc
{
}
