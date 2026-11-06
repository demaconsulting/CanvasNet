namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

// cspell:ignore rasterizer rasterizes rasterization supersampling Lambertian Sutherland Hodgman

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Render3D.SceneRendering"/> namespace renders a
///     <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel.Scene"/> into a CanvasNet
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> using a pure-managed software
///     rasterizer: the <see cref="Camera"/> model, the <see cref="Light"/> types, and the
///     <see cref="SceneRenderer"/> entry points configured through <see cref="SceneRenderOptions"/>.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline flattens the scene graph, transforms vertices into camera-relative view
///         space in double precision, clips against the near plane, rasterizes with fixed-point
///         (24.8) top-left-rule edge functions into 32x32-pixel tiles with a z-buffer (or, for
///         constructive solid geometry with differences or intersections, translucent appearances
///         and overlays, per-pixel fragment lists resolved against the boolean tree and
///         composited front to back), shades each
///         face flat with a Lambertian light rig, and resolves k-by-k ordered-grid sub-pixel samples
///         with a premultiplied box filter. Tiles are independent, so rendering can run in
///         parallel with byte-identical output.
///     </para>
///     <para>
///         This namespace is part of the separate <c>DemaConsulting.CanvasNet.Render3D</c> NuGet
///         package, which references only the core <c>DemaConsulting.CanvasNet</c> package.
///     </para>
///     <para>
///         See <c>SceneRenderer.RenderFitted</c>
///         for a quick view that frames the whole scene, <see cref="SceneRenderer"/> with an explicit
///         <see cref="Camera"/> for full control, and <see cref="SceneRenderOptions"/> for lights, background, and
///         supersampling. The scene is described in <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel"/>;
///         <see cref="DemaConsulting.CanvasNet.Codecs"/> saves the surface. <c>DemaConsulting.CanvasNet.Scad</c> draws
///         OpenSCAD models through this renderer and takes a <see cref="Camera"/> in <c>ScadRenderOptions.Camera</c>.
///     </para>
/// </remarks>
/// <example>
///     Render a sphere from an explicit camera with a transparent background, and save it:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Canvas;
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Render3D.SceneModel;
///     using DemaConsulting.CanvasNet.Render3D.SceneRendering;
///     using DemaConsulting.CanvasNet.Render3D.Tessellation;
///
///     var scene = new Scene(new MeshNode(PrimitiveTessellator.Sphere(2, 48))
///     {
///         Appearance = new NodeAppearance(new Rgba32(200, 40, 40, 255)),
///     });
///     var camera = Camera.FitToBounds(scene.ComputeBounds(), azimuthDegrees: 30, elevationDegrees: 25, aspectRatio: 640.0 / 480.0);
///     var options = new SceneRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0), Supersampling = 4 };
///     using var image = SceneRenderer.Render(scene, camera, 640, 480, options);
///     PngCodec.Save(image, "sphere.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
