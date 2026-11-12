namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <summary>
///     The <c>Flowchart</c> namespace reads flowcharts (<c>flowchart</c> and <c>graph</c>): a port of Mermaid's
///     <c>flow.jison</c> grammar and of its flowchart database.
/// </summary>
/// <remarks>
///     <para>
///         <c>FlowchartParser</c> lexes and parses the preprocessed text (with the token kinds of <c>FlowchartTokens</c>)
///         and fills a <c>FlowchartModel</c>, Mermaid's <c>flowDb</c>: vertices, edges, subgraphs, classes, the direction
///         and the accessibility texts. The model is the diagram of the document; it maps itself to the renderer's
///         <c>LayoutData</c> for each render, reports what this version does not draw (icons, looks, layouts) and draws
///         itself as Mermaid's flowchart renderer does: the layout data through the cluster layout, the title above it and
///         the view box with the diagram padding.
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed model is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
