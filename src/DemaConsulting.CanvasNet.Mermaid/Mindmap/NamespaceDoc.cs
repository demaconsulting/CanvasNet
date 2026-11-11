namespace DemaConsulting.CanvasNet.Mermaid.Mindmap;

/// <summary>
///     The <c>Mindmap</c> namespace reads and draws mindmaps (<c>mindmap</c>): a port of Mermaid's mindmap grammar
///     (<c>mindmap.jison</c>), of its mindmap database (<c>mindmapDb.ts</c>) and of its renderer
///     (<c>mindmapRenderer.ts</c> with the cose-bilkent renderer of the unified layouts).
/// </summary>
/// <remarks>
///     <para>
///         <c>MindmapParser</c> lexes and parses the preprocessed text and fills a <c>MindmapDiagram</c> as Mermaid's
///         database does: the nodes with their indentation levels, parents, shapes, descriptions, icons and classes, and
///         what Mermaid's <c>getData</c> derives from them (the sections, the node classes and the edges).
///     </para>
///     <para>
///         <c>MindmapRenderer</c> draws a diagram under the options of a call: each node's shape through the shared shape
///         library with the mindmap style sheet, the node boxes laid out by the cose-bilkent port of the <c>Cose</c>
///         namespace, the edges through the layout's points with the shared edge builder, and the view box grown by the
///         padding; it reports what it does not draw (other looks and layouts, icons).
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed diagram is never changed after parsing and is safe for
///         concurrent use; a renderer serves one call on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
