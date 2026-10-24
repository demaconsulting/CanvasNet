namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

// cspell:ignore rasterizer rasterizes rasterization supersampling Lambertian Sutherland Hodgman

/// <summary>
///     The <c>SceneRendering</c> namespace renders a
///     <c>Scene</c> into a CanvasNet
///     <c>Surface</c> using a pure-managed software
///     rasterizer: the <c>Camera</c> model, the <c>Light</c> types, and the
///     <c>SceneRenderer</c> entry points configured through <c>SceneRenderOptions</c>.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline flattens the scene graph, transforms vertices into camera-relative view
///         space in double precision, clips against the near plane, rasterizes with fixed-point
///         (24.8) top-left-rule edge functions into 32x32-pixel tiles with a z-buffer, shades each
///         face flat with a Lambertian light rig, and resolves k-by-k ordered-grid sub-pixel samples
///         with a premultiplied box filter. Tiles are independent, so rendering can run in
///         parallel with byte-identical output.
///     </para>
///     <para>
///         This namespace is part of the separate <c>DemaConsulting.CanvasNet.Render3D</c> NuGet
///         package, which references only the core <c>DemaConsulting.CanvasNet</c> package.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
