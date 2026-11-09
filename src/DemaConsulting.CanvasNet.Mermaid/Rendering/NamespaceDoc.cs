namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     The <c>Rendering</c> namespace is the diagram-independent geometry of Mermaid's unified renderer
///     (<c>rendering-util</c>): what Mermaid draws for any <c>LayoutData</c>.
/// </summary>
/// <remarks>
///     <para>
///         <c>LayoutData</c> (with <c>LayoutNode</c> and <c>LayoutEdge</c>) is what a diagram model hands to the renderer,
///         and <c>ShapeNames</c> is the registry of the node shapes. <c>ClusterLayout</c> lays the data out with dagre
///         (subgraphs, recursively when they have no links to the outside) and draws it: the node shapes of
///         <c>ShapeLibrary</c> with the labels of <c>LabelBuilder</c> and the pictures of <c>NodeImages</c>, the subgraph
///         rectangles, and the edges, markers and edge labels of <c>EdgeBuilder</c>.
///     </para>
///     <para>
///         The namespace is internal to the package.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
