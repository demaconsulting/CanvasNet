namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore rasterizing rasterize pptx ooxml patt

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Pptx"/> namespace provides read access to
///     PowerPoint (<c>.pptx</c>) presentation documents via <see cref="PptxDocument"/>: the OOXML
///     (Office Open XML) package layer, the presentation/theme/master/layout/slide structural
///     model, DrawingML shape geometry and paint resolution, and DrawingML text layout and
///     rendering. This namespace is distributed as the separate
///     <c>DemaConsulting.CanvasNet.Pptx</c> NuGet package, which references the core
///     <c>DemaConsulting.CanvasNet</c> package.
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
///         <c>DemaConsulting.CanvasNet.Canvas</c> types); and Phase 1d (DrawingML text structural
///         parsing, an attribute-level run/paragraph property-inheritance resolver, word-wrap/
///         alignment/vertical-anchor/autofit text layout, and glyph-ink text rendering reusing the
///         core <see cref="Fonts.TrueTypeFont"/>/<see cref="Fonts.SystemFontCatalog"/>
///         infrastructure).
///     </para>
///     <para>
///         As of the current release (through Phase 1d), a freeform (non-placeholder) shape's
///         full <c>&lt;p:spTree&gt;</c> *enumeration*, bullets/numbering, full text justification,
///         <c>spAutoFit</c> shape-resize behavior, kerning, text clipping on overflow, and a full
///         per-slide public <c>Render</c> API are explicitly deferred to a later phase - see
///         <see cref="PptxDocument"/>'s own remarks for the exact, current scope boundary.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
