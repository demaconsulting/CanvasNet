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
///         This feature is being delivered incrementally across several phases. Phase 1a (the
///         current release) implements only the underlying OOXML (Office Open XML) package
///         layer: opening a <c>.pptx</c> file as a ZIP archive, resolving
///         <c>[Content_Types].xml</c>, and resolving package- and part-level relationships
///         (<c>_rels/.rels</c> and <c>{part}/_rels/{part}.rels</c>). No presentation-specific
///         content (<c>ppt/presentation.xml</c>, slides, slide layouts/masters, or any rendering)
///         is implemented yet - see <see cref="PptxDocument"/>'s own remarks for the exact,
///         current scope boundary.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
