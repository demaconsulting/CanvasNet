using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     The surface appearance a <see cref="SceneNode"/> imposes on the meshes beneath it.
/// </summary>
/// <remarks>
///     <para>
///         The <b>innermost</b> appearance wins: a mesh takes the color of the closest ancestor (or
///         itself) that has an appearance; a mesh with none uses the renderer's configurable default
///         color. The type is an immutable record so one appearance instance can be shared by many
///         nodes.
///     </para>
///     <para>
///         <b>Limitation:</b> the alpha channel of <see cref="Color"/> is ignored (surfaces
///         are always opaque); transparency is planned for a later release.
///     </para>
/// </remarks>
/// <param name="Color">The base surface color before lighting; alpha is ignored.</param>
public sealed record NodeAppearance(Rgba32 Color);
