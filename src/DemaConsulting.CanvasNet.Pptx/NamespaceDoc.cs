namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore rasterizing rasterize pptx ooxml

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Pptx"/> namespace provides a decode/rasterize-only
///     codec for PowerPoint (<c>.pptx</c>) presentation documents: <see cref="PptxDocument"/>.
///     This namespace is distributed as the separate <c>DemaConsulting.CanvasNet.Pptx</c> NuGet
///     package, which references the core <c>DemaConsulting.CanvasNet</c> package.
/// </summary>
/// <remarks>
///     <para>
///         This feature is being delivered incrementally across several phases. Phase 1a
///         implemented only the underlying OOXML (Office Open XML) package layer: opening a
///         <c>.pptx</c> file as a ZIP archive, resolving <c>[Content_Types].xml</c>, and resolving
///         package- and part-level relationships (<c>_rels/.rels</c> and
///         <c>{part}/_rels/{part}.rels</c>).
///     </para>
///     <para>
///         Phase 1b (the current release) adds: parsing <c>ppt/presentation.xml</c> (slide size,
///         slide list); theme color/font scheme parsing; slide master/layout/slide structural
///         models (placeholder shapes only, not freeform shapes); and an isolated placeholder
///         property-inheritance resolver implementing ECMA-376's placeholder matching algorithm.
///         No shape geometry/paint rendering, freeform shape parsing, font loading, or rendering
///         surface exists yet - all deferred to Phase 1c+ - see <see cref="PptxDocument"/>'s own
///         remarks for the exact, current scope boundary.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
