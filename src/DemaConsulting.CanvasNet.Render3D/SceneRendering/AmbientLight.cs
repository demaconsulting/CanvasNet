namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     A light that illuminates every surface equally, regardless of orientation.
/// </summary>
/// <remarks>
///     Ambient light prevents faces turned away from every directional light from rendering pure
///     black. A single <c>AmbientLight { Intensity = 1 }</c> with no other lights yields the flat,
///     unshaded base color.
/// </remarks>
public sealed class AmbientLight : Light
{
}
