namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

// cspell:ignore tessellator tessellators polyhedra polyhedron

/// <summary>
///     The <c>Tessellation</c> namespace converts
///     analytic primitives into <c>Mesh</c>
///     instances through <c>PrimitiveTessellator</c>: boxes, UV spheres, cylinders and
///     cones (frusta), and validated polyhedra.
/// </summary>
/// <remarks>
///     Every generated mesh is welded (each vertex is created once and shared by index), closed
///     (every edge is shared by exactly two triangles with opposite directions) and wound
///     counter-clockwise when seen from outside, so it can be used directly by the renderer and,
///     in later phases, by constructive-solid-geometry operations.
/// </remarks>
internal static class NamespaceDoc
{
}
