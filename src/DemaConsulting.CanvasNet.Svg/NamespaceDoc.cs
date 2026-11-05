namespace DemaConsulting.CanvasNet.Svg;

// cspell:ignore rasterizing rasterize

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Svg"/> namespace provides a restricted,
///     dependency-free, decode/rasterize-only codec for a common real-world subset of SVG
///     (Scalable Vector Graphics) documents: <see cref="SvgCodec"/>. Unlike the four raster
///     codecs in the core <c>DemaConsulting.CanvasNet</c> package's
///     <see cref="DemaConsulting.CanvasNet.Codecs"/> namespace (which each convert to and
///     from a <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> pixel buffer using only
///     its existing public API), <see cref="SvgCodec"/> only decodes/rasterizes SVG vector
///     artwork into a <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> - it has no
///     <c>Save</c> method, since rasterizing a textual vector document is a fundamentally
///     different (and non-invertible) operation from encoding a fixed-size raster format, so
///     it has no encode/save direction either. This namespace is distributed as the separate
///     <c>DemaConsulting.CanvasNet.Svg</c> NuGet package, which references the core
///     <c>DemaConsulting.CanvasNet</c> package.
/// </summary>
/// <remarks>
///     See <see cref="SvgCodec"/> for loading (<see cref="SvgLoadOptions"/> for the options, <see cref="SvgFontFace"/> to
///     register your own fonts) and <see cref="DemaConsulting.CanvasNet.Codecs"/> for saving the result. An
///     <c>&lt;image href="..."&gt;</c> that is not a <c>data:</c> URI is drawn only when you set
///     <see cref="SvgLoadOptions.ImageResolver"/>: see <see cref="SvgImageSource"/> and
///     <see cref="DemaConsulting.CanvasNet.ImageSources"/> to embed other SVG files, 3D models, charts, and documents
///     (<c>CanvasNetViewer.CreateImageResolver</c> in <c>DemaConsulting.CanvasNet.Viewer</c> builds a resolver with every
///     format package's source).
/// </remarks>
/// <example>
///     Rasterize an SVG file to a PNG at 800 x 600, with the pictures it links to taken from the same folder:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.ImageSources;
///     using DemaConsulting.CanvasNet.Svg;
///
///     var resolver = new FolderImageResolver("diagrams", [new RasterImageSource(), new SvgImageSource()]);
///     var options = new SvgLoadOptions { ImageResolver = resolver };
///     using var surface = SvgCodec.Load("diagrams/overview.svg", 800, 600, fonts: null, options);
///     PngCodec.Save(surface, "overview.png");
///     foreach (var note in resolver.Diagnostics)
///     {
///         Console.WriteLine($"{note.Code}: {note.Message}");
///     }
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
