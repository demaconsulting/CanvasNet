namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Viewer"/> namespace is the one-stop package for reading everything
///     CanvasNet can read: <see cref="CanvasNetViewer"/> lists the image sources of every format package (SVG, PDF,
///     PPTX, VSDX, OpenSCAD, Vega-Lite and the raster formats), renders any such file in one call, and creates the
///     <see cref="DemaConsulting.CanvasNet.ImageSources.FolderImageResolver"/> that lets an SVG document embed all of
///     them with <c>&lt;image href="..."&gt;</c>. Applications add their own
///     <see cref="DemaConsulting.CanvasNet.ImageSources.IImageSource"/> implementations to it, and they take precedence
///     over the built-in ones. This namespace is distributed as the separate <c>DemaConsulting.CanvasNet.Viewer</c>
///     NuGet package, which references the core package and the format packages.
/// </summary>
internal static class NamespaceDoc
{
}
