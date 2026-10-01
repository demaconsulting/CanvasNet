namespace DemaConsulting.CanvasNet.Pdf;

// cspell:ignore rasterizing rasterize

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Pdf"/> namespace provides a restricted,
///     dependency-free, decode/rasterize-only codec for PDF (Portable Document Format)
///     documents: <see cref="PdfDocument"/>. Unlike the four raster codecs in the core
///     <c>DemaConsulting.CanvasNet</c> package's <see cref="DemaConsulting.CanvasNet.Codecs"/>
///     namespace (which each convert to and from a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> pixel buffer using only its
///     existing public API), <see cref="PdfDocument"/> only decodes/rasterizes PDF page content
///     into a <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> - it has no <c>Save</c>
///     method, since rasterizing a paginated vector document is a fundamentally different (and
///     non-invertible) operation from encoding a fixed-size raster format, so it has no
///     encode/save direction either. This namespace is distributed as the separate
///     <c>DemaConsulting.CanvasNet.Pdf</c> NuGet package, which references the core
///     <c>DemaConsulting.CanvasNet</c> package.
/// </summary>
/// <remarks>
///     Phase 1 of this package's implementation establishes document parsing (tokenizer, object
///     model, cross-reference resolution, page-tree traversal) and the public
///     <see cref="PdfDocument"/> API surface (<see cref="PdfDocument.Open(System.IO.Stream, string?)"/>,
///     <see cref="PdfDocument.PageCount"/>, <see cref="PdfDocument.GetPageInfo(int)"/>,
///     <see cref="PdfDocument.Render(int, int, int)"/>, <see cref="PdfDocument.Dispose"/>), but
///     does not yet interpret page content streams: <see cref="PdfDocument.Render(int, int, int)"/>
///     currently returns a correctly sized but fully transparent (blank) <c>Surface</c>. Actual
///     page content (paths, text, images) is planned for a later phase.
/// </remarks>
internal static class NamespaceDoc
{
}
