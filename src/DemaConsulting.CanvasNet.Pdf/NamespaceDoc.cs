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
///     <see cref="PdfDocument.Render(int, int, int, PdfRenderOptions?)"/> fully interprets a page's content stream:
///     vector path construction/painting with real device color (<c>DeviceGray</c>/
///     <c>DeviceRGB</c>/<c>DeviceCMYK</c>, <c>CalRGB</c>, ICC-based, and <c>/Indexed</c> color
///     spaces), text shown with a resolved font - simple <c>/Subtype /TrueType</c>/<c>/Type1</c>
///     fonts, composite <c>/Subtype /Type0</c> fonts, and procedure-painted <c>/Subtype /Type3</c>
///     fonts are all supported, with embedded font programs used directly and non-embedded simple
///     fonts automatically substituted with a matching system or bundled font (only
///     <c>/MMType1</c> is unsupported) - placed raster image XObjects (<c>DCTDecode</c>/
///     <c>CCITTFaxDecode</c> (Group 4)/raw samples through the full supported <c>/Filter</c>
///     pipeline: <c>FlateDecode</c>, <c>LZWDecode</c>, <c>ASCII85Decode</c>,
///     <c>ASCIIHexDecode</c>, and <c>RunLengthDecode</c>, each with PNG/TIFF predictor reversal
///     where applicable), placed Form XObjects (nested content streams with their own
///     <c>/Matrix</c>/<c>/Resources</c>), and <c>/Pattern</c>-color-space shading (axial/radial)
///     and tiling pattern fills/strokes. Opening a document encrypted with the PDF
///     <c>/Filter /Standard</c> security handler (RC4, AES-128, or AES-256/R5) is also supported.
///     See <see cref="PdfDocument"/>'s own remarks for the complete, current feature list and its
///     documented scope boundaries (for example mesh shadings, <c>/FunctionType 4</c>
///     PostScript-calculator functions, the <c>sh</c> operator, generic path clipping,
///     transparency groups, and clip text-rendering modes).
/// </remarks>
/// <example>
///     Rendering every page of a PDF document to a 300 DPI PNG file, using
///     <see cref="PdfDocument.Render(int, float, PdfRenderOptions?)"/> (which preserves each page's own aspect
///     ratio) together with the core <c>DemaConsulting.CanvasNet</c> package's
///     <see cref="DemaConsulting.CanvasNet.Codecs.PngCodec"/> - the separate package that holds
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> and every raster codec, since this
///     namespace only rasterizes into a <c>Surface</c> and has no <c>Save</c> method of its own:
///     <code>
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Pdf;
///
///     using var document = PdfDocument.Open("input.pdf");
///     for (var pageIndex = 0; pageIndex &lt; document.PageCount; pageIndex++)
///     {
///         using var surface = document.Render(pageIndex, dpi: 300f);
///         PngCodec.Save(surface, $"page-{pageIndex}.png");
///     }
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
