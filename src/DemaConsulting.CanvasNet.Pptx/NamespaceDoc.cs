namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore rasterizing rasterize pptx ooxml patt

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Pptx"/> namespace provides read access to
///     PowerPoint (<c>.pptx</c>) presentation documents via <see cref="PptxDocument"/>: the OOXML
///     (Office Open XML) package layer, the presentation/theme/master/layout/slide structural
///     model, DrawingML shape geometry and paint resolution, DrawingML text layout and rendering,
///     picture/table/recursive-shape-tree resolution, and a public, per-slide rendering API. This
///     namespace is distributed as the separate <c>DemaConsulting.CanvasNet.Pptx</c> NuGet
///     package, which references the core <c>DemaConsulting.CanvasNet</c> package.
/// </summary>
/// <remarks>
///     <para>
///         This feature has been delivered incrementally across several phases, each additive to
///         the last: Phase 1a (the underlying OOXML package layer - opening a <c>.pptx</c> file
///         as a ZIP archive, resolving <c>[Content_Types].xml</c>, and resolving package- and
///         part-level relationships); Phase 1b (parsing <c>ppt/presentation.xml</c>'s slide size
///         and slide list; theme color/font scheme parsing; slide master/layout/slide structural
///         models; and a placeholder property-inheritance resolver); Phase 1c (DrawingML shape
///         geometry - position/rotation/flip transforms, group child-coordinate-space composition,
///         preset and custom geometry resolution - and paint resolution - solid/gradient fills and
///         line styles - into the core <c>DemaConsulting.CanvasNet.Drawing</c>/
///         <c>DemaConsulting.CanvasNet.Canvas</c> types); Phase 1d (DrawingML text structural
///         parsing, an attribute-level run/paragraph property-inheritance resolver, word-wrap/
///         alignment/vertical-anchor/autofit text layout, and glyph-ink text rendering reusing the
///         core <see cref="Fonts.TrueTypeFont"/>/<see cref="Fonts.SystemFontCatalog"/>
///         infrastructure); Phase 1e (<c>&lt;p:pic&gt;</c> picture-shape decoding/cropping/
///         compositing, <c>&lt;a:tbl&gt;</c> table structure/cell-rect/paint resolution, and
///         recursive, full <c>&lt;p:spTree&gt;</c> shape-tree parsing - including nested
///         <c>&lt;p:grpSp&gt;</c> enumeration - exposed via the internal shape-tree model); and
///         Phase 1f (the public, slide-level <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>/
///         <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/> rendering API, a
///         document-order walk of a slide's full shape tree dispatching each node to the already-
///         verified Phase 1c/1d/1e resolvers and painters). Subsequent Phase 2 Follow-Ups added
///         slide/layout/master <c>&lt;p:bg&gt;</c> background-fill resolution ahead of that
///         shape-tree walk, <c>&lt;a:buChar&gt;</c>/<c>&lt;a:buAutoNum&gt;</c> bullet/numbering
///         rendering, and <c>&lt;p:cxnSp&gt;</c> connector-shape rendering.
///     </para>
///     <para>
///         As of the current release (through Phase 1f and its subsequent Phase 2 Follow-Ups),
///         slide/layout/master pattern and picture background fills are now resolved and painted
///         (see <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>'s own
///         remarks for the exact fidelity achieved). Radial/path gradients, full text
///         justification, <c>spAutoFit</c> shape-resize behavior, kerning, text clipping on
///         overflow, nested tables, and table auto-sizing/banding remain explicitly deferred -
///         see <see cref="PptxDocument"/>'s own remarks for the exact, current scope boundary.
///     </para>
///     <para>
///         See <see cref="PptxDocument"/> for opening a presentation and rendering a slide (<see cref="PptxRenderOptions"/>
///         for the options) and <see cref="DemaConsulting.CanvasNet.Codecs"/> for saving the result. Slide numbers here are
///         <b>0-based</b> (<c>slideIndex</c>); the <c>DemaConsulting.CanvasNet.Viewer</c> package, which reads PPTX
///         through the same code, numbers pages from 1. Charts on a slide are parsed with
///         <see cref="DemaConsulting.CanvasNet.Charts.OpenXml"/> and drawn with
///         <see cref="DemaConsulting.CanvasNet.Charts"/>; to embed a slide in an SVG document, see
///         <c>PptxImageSource</c> and <see cref="DemaConsulting.CanvasNet.ImageSources"/>.
///     </para>
/// </remarks>
/// <example>
///     Render the first slide of a presentation at 150 dots per inch and save it:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Pptx;
///
///     using var document = PptxDocument.Open("deck.pptx");
///     using var slide = document.Render(0, 150f);   // the first slide is index 0
///     PngCodec.Save(slide, "slide-1.png");
///     Console.WriteLine($"{document.SlideCount} slides");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
