namespace DemaConsulting.CanvasNet.Mermaid.Cose;

/// <summary>
///     The <c>Cose</c> namespace is the force-directed layout that Mermaid draws its mindmaps with: a port of the
///     cose-bilkent layout (cytoscape-cose-bilkent over cose-base and layout-base) as Mermaid runs it, with the edge
///     points of cytoscape.
/// </summary>
/// <remarks>
///     <para>
///         <c>CoseLayout</c> is the one unit: it places a tree radially around its center, runs the spring embedder
///         (springs along the edges, repulsion between nearby nodes, a cooling schedule) and returns every node's center
///         and every edge's start, middle and end point, as Mermaid reads them from cytoscape. A tree never reaches the
///         random parts of the original, so the layout has no random source and is deterministic.
///     </para>
///     <para>
///         The layout is chaotic, so the port keeps the JavaScript operation order and uses its own correctly rounded
///         sine, cosine, logarithm and power, which give the results of the reference browser and are the same on
///         every platform. Every step charges the <c>LayoutWorkMeter</c> of the diagram, which bounds the work and
///         observes cancellation. The namespace is internal to the package; a layout is run by one call on one thread,
///         and the math functions are safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
