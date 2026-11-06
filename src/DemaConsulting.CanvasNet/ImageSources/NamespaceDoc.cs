namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.ImageSources"/> namespace lets a document that links to other pictures
///     (an SVG <c>&lt;image href="..."&gt;</c>, for example) draw 3D models, charts, other vector files, document pages
///     and rasters, without the library that draws the document knowing any of those formats.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="IImageResolver"/> is the one-method contract a document renderer calls to turn a link into a
///         <see cref="Canvas.Surface"/>. <see cref="FolderImageResolver"/> is the ready-made implementation: it confines
///         the link to a folder (through <see cref="Files.SandboxedFileResolver"/>), enforces depth, cycle and count
///         limits, and hands the file to the <see cref="IImageSource"/> that claims its file name extension (or the link
///         itself to the one that claims its URI scheme).
///     </para>
///     <para>
///         <see cref="IImageSource"/> is the contract of one format. This library ships <see cref="RasterImageSource"/>;
///         the format packages ship the others (SVG, OpenSCAD, Vega-Lite, PDF, PPTX, VSDX), the
///         <c>DemaConsulting.CanvasNet.Viewer</c> package lists them all, and any application can implement the
///         interface for its own format or its own live source. <see cref="ImageSourceSet"/> composes sources and
///         states which one wins when two claim the same extension or scheme: the later one.
///     </para>
///     <para>
///         The namespace has no global state and no discovery: a source is used only when the caller passes it in.
///     </para>
///     <para>
///         See <see cref="FolderImageResolver"/> for the resolver (and <see cref="FolderImageResolverOptions"/> for its
///         limits), <see cref="IImageSource"/> and <see cref="ImageSourceRequest"/> for writing a source of your own, and
///         <see cref="ImageSourceDiagnostic"/> for the notes a render collects. A link can carry parameters in its
///         fragment (<c>report.pdf#page=3</c>): see <see cref="ImageFragment"/> and <see cref="IFragmentImageSource"/>.
///         A document renderer takes the resolver
///         as an <see cref="IImageResolver"/>: for SVG, see <c>SvgLoadOptions.ImageResolver</c> in
///         <c>DemaConsulting.CanvasNet.Svg</c>. The sources of the format packages are
///         <c>SvgImageSource</c>, <c>ScadImageSource</c>, <c>VegaLiteImageSource</c>, <c>PdfImageSource</c>,
///         <c>PptxImageSource</c>, and <c>VsdxImageSource</c>, and <c>CanvasNetViewer.CreateImageResolver</c> in
///         <c>DemaConsulting.CanvasNet.Viewer</c> builds a resolver with all of them. Files are read through
///         <see cref="DemaConsulting.CanvasNet.Files"/>.
///     </para>
/// </remarks>
/// <example>
///     Write a source for a made-up <c>.dots</c> file (this one only draws a blank picture of the requested size) and let a
///     resolver draw one, together with the built-in raster
///     source. The source is listed last, so it wins when two sources claim the same extension:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Canvas;
///     using DemaConsulting.CanvasNet.ImageSources;
///
///     var resolver = new FolderImageResolver("assets", [new RasterImageSource(), new DotsSource()]);
///     using var picture = resolver.Render("art/many.dots", 200, 100).Surface;
///     foreach (var note in resolver.Diagnostics)
///     {
///         Console.WriteLine($"{note.Code}: {note.Message}");
///     }
///
///     // A source claims file name extensions (or URI schemes) and returns a new surface on every call
///     sealed class DotsSource : IImageSource
///     {
///         public string Name => "dots";
///         public IReadOnlyList&lt;string&gt; Extensions => [".dots"];
///         public IReadOnlyList&lt;string&gt; Schemes => [];
///
///         public ImageSourceResult Render(ImageSourceRequest request)
///         {
///             var (width, height) = request.SizeFor(200, 100);
///             request.EnsureWithinBudget(width, height);   // charge the pixel budget before allocating
///             var surface = new Surface(width, height);
///             surface.Clear(new Rgba32(255, 255, 255, 255));
///             return new ImageSourceResult(surface) { Format = "dots" };
///         }
///     }
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
