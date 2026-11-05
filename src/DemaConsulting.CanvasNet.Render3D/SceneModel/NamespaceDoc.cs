namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

// cspell:ignore polyhedra

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel"/> namespace provides the
///     format-agnostic, double-precision data model that a 3D scene is described with: the
///     <see cref="Vector2D"/>, <see cref="Vector3D"/>, <see cref="Matrix4x4D"/> and
///     <see cref="BoundingBox3D"/> math types; the immutable indexed triangle <see cref="Mesh"/>
///     (with <see cref="MeshBuilder"/>); and the scene graph made of <see cref="SceneNode"/>
///     derivatives (<see cref="MeshNode"/>, <see cref="GroupNode"/>, <see cref="CsgNode"/> with its
///     <see cref="CsgOperation"/>, and the independent translucent body <see cref="OverlayNode"/>),
///     <see cref="NodeAppearance"/> (whose alpha is honored) and the <see cref="Scene"/> root.
/// </summary>
/// <remarks>
///     <para>
///         Model space is right-handed with +Z up. Mesh triangles are wound counter-clockwise when
///         seen from outside (right-hand outward normal). All model-space math is performed in
///         <c>double</c> precision so that large-offset models (for example features of
///         size 0.1 located 100000 units from the origin) keep their accuracy; narrowing to
///         single precision happens only after the camera-relative view transform in the renderer.
///     </para>
///     <para>
///         This namespace is part of the separate <c>DemaConsulting.CanvasNet.Render3D</c> NuGet
///         package, which references only the core <c>DemaConsulting.CanvasNet</c> package.
///     </para>
///     <para>
///         See <see cref="Scene"/> for the root of a model. Meshes come from
///         <see cref="DemaConsulting.CanvasNet.Render3D.Tessellation"/> (boxes, spheres, extrusions, hulls) or from your
///         own <see cref="MeshBuilder"/>; a scene is drawn with
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneRendering"/>. The <c>DemaConsulting.CanvasNet.Scad</c>
///         package builds a <see cref="Scene"/> from OpenSCAD source (<c>ScadDocument.BuildScene</c>).
///     </para>
/// </remarks>
/// <example>
///     Build a scene of a red cube with a sphere on top, and measure it:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Canvas;
///     using DemaConsulting.CanvasNet.Render3D.SceneModel;
///     using DemaConsulting.CanvasNet.Render3D.Tessellation;
///
///     var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true))
///     {
///         Appearance = new NodeAppearance(new Rgba32(200, 40, 40, 255)),
///     };
///     var ball = new MeshNode(PrimitiveTessellator.Sphere(1, 32))
///     {
///         Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 2)),
///     };
///     var scene = new Scene(new GroupNode([cube, ball]));
///     var bounds = scene.ComputeBounds();
///     Console.WriteLine($"{bounds.Max.Z - bounds.Min.Z} units tall");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
