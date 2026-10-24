using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Describes one scene feature or input that <see cref="SceneRenderer"/> cannot render
///     faithfully (for example a constructive-solid-geometry operand that is not a closed,
///     outward-oriented mesh) - a non-throwing diagnostic delivered to
///     <see cref="SceneRenderOptions.UnsupportedFeatureHandler"/> (or thrown as a
///     <see cref="SceneRenderUnsupportedFeatureException"/> when
///     <see cref="SceneRenderOptions.ThrowOnUnsupportedFeature"/> is set).
/// </summary>
/// <param name="Feature">
///     A short, stable, machine-matchable token identifying the feature (for example
///     <c>"render3d-csg-open-mesh"</c>) - the same value
///     <see cref="SceneRenderUnsupportedFeatureException.Feature"/> carries in strict mode.
/// </param>
/// <param name="Message">A free-text, human-readable description of what is wrong.</param>
/// <param name="Node">The mesh node the report is about, so a front end can map it back to its source, or <see langword="null"/> when it concerns the whole render.</param>
public sealed record SceneRenderUnsupportedFeature(string Feature, string Message, MeshNode? Node = null);
