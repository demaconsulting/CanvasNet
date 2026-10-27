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
///         <b>Transparency:</b> the alpha channel of <see cref="Color"/> is honored. A color with alpha
///         below 255 makes the <em>visible surfaces of the resolved solid</em> translucent: the renderer
///         composites the layers front to back, deterministically and independent of insertion order,
///         over the background. Members of one union merge, so only the boundary of a union of translucent
///         bodies is visible; use <see cref="OverlayNode"/> for independent see-through bodies. Alpha 0 is
///         invisible but still solid (it takes part in boolean operations). A scene that uses
///         translucency is resolved with signed crossing counters, so its meshes must be closed,
///         outward-wound solids. The alpha of the renderer's default and back-face colors is ignored.
///     </para>
/// </remarks>
/// <param name="Color">The base surface color before lighting; alpha below 255 makes the surface translucent.</param>
public sealed record NodeAppearance(Rgba32 Color);
