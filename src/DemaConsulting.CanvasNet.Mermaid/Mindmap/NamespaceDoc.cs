namespace DemaConsulting.CanvasNet.Mermaid.Mindmap;

/// <summary>
///     The <c>Mindmap</c> namespace reads mindmaps (<c>mindmap</c>): a port of Mermaid's mindmap grammar
///     (<c>mindmap.jison</c>) and of its mindmap database (<c>mindmapDb.ts</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>MindmapParser</c> lexes and parses the preprocessed text and fills a <c>MindmapDiagram</c> as Mermaid's
///         database does: the nodes with their indentation levels, parents, shapes, descriptions, icons and classes, and
///         what Mermaid's <c>getData</c> derives from them (the sections, the node classes and the edges).
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed diagram is never changed after parsing and is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
