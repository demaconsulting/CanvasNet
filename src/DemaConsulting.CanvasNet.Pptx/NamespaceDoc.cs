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
///         pattern/picture background fills, radial/path gradients, full text justification,
///         <c>spAutoFit</c> shape-resize behavior, kerning, text clipping on overflow, nested
///         tables, and table auto-sizing/banding remain explicitly deferred - see
///         <see cref="PptxDocument"/>'s own remarks for the exact, current scope boundary.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
