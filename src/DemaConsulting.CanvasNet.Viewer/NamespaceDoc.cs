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
/// <remarks>
///     <para>
///         <see cref="CanvasNetViewer"/> is the entry point: <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/>
///         draws a file and returns a <see cref="ViewerImage"/> with its notes, echo, anchors, page, and embedded images,
///         <see cref="ViewerRenderOptions"/> says how (size, page, resolution, background, model view, sandbox folder,
///         your own <see cref="ViewerRenderOptions.ExtraSources"/>), and
///         <see cref="CanvasNetViewer.CreateImageResolver(string, DemaConsulting.CanvasNet.ImageSources.FolderImageResolverOptions?)"/>
///         is for a render that you drive yourself. A failure the caller can fix is reported by a
///         <see cref="ViewerException"/> with a stable <see cref="ViewerException.Code"/> from <see cref="ErrorCodes"/>;
///         both types live in this namespace, while the operations that throw them are in
///         <see cref="DemaConsulting.CanvasNet.Viewer.Imaging"/>, so an example that catches one needs both
///         <c>using DemaConsulting.CanvasNet.Viewer;</c> and <c>using DemaConsulting.CanvasNet.Viewer.Imaging;</c>.
///     </para>
///     <para>
///         See <see cref="DemaConsulting.CanvasNet.Viewer.Imaging"/> for what the <c>canvasnet</c> tool does as library
///         calls (probe, render with image operations, inspect, compare, and montage), and
///         <see cref="DemaConsulting.CanvasNet.ImageSources"/> to write a source for another format. The pictures are
///         <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> objects; <see cref="DemaConsulting.CanvasNet.Codecs"/>
///         saves them. An SVG document embeds the other formats through
///         <see cref="DemaConsulting.CanvasNet.Svg.SvgLoadOptions.ImageResolver"/>.
///     </para>
///     <para>
///         <b>Ownership.</b> Dispose a <see cref="ViewerImage"/> (it owns its surface). A
///         <see cref="DemaConsulting.CanvasNet.ImageSources.FolderImageResolver"/> is not disposable and holds no open
///         files. The <see cref="DemaConsulting.CanvasNet.Viewer.Imaging"/> operations return plain values and write
///         files; what to dispose there is described in that namespace.
///     </para>
/// </remarks>
/// <example>
///     Render a file of any supported format to a PNG, then let an SVG document embed other files from its folder:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Svg;
///     using DemaConsulting.CanvasNet.Viewer;
///
///     using var image = CanvasNetViewer.Render("bracket.scad", new ViewerRenderOptions { Width = 800 });
///     Console.WriteLine($"{image.Format}: {image.Diagnostics.Count} notes, {image.Echo.Count} echo lines");
///     PngCodec.Save(image.Surface, "bracket.png");
///
///     // For a render you drive yourself, create a resolver for every top-level render
///     var resolver = CanvasNetViewer.CreateImageResolver("diagrams");
///     using var diagram = SvgCodec.Load("diagrams/overview.svg", 800, 600, fonts: null, new SvgLoadOptions { ImageResolver = resolver });
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
