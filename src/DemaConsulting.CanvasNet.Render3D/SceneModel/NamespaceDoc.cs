namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

// cspell:ignore polyhedra

/// <summary>
///     The <c>SceneModel</c> namespace provides the
///     format-agnostic, double-precision data model that a 3D scene is described with: the
///     <c>Vector2D</c>, <c>Vector3D</c>, <c>Matrix4x4D</c> and
///     <c>BoundingBox3D</c> math types; the immutable indexed triangle <c>Mesh</c>
///     (with <c>MeshBuilder</c>); and the scene graph made of <c>SceneNode</c>
///     derivatives (<c>MeshNode</c>, <c>GroupNode</c>, <c>CsgNode</c> with its
///     <c>CsgOperation</c>), <c>NodeAppearance</c>
///     and the <c>Scene</c> root.
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
/// </remarks>
internal static class NamespaceDoc
{
}
