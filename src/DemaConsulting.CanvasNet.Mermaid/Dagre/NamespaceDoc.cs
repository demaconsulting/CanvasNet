namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The <c>Dagre</c> namespace is the layered graph layout that Mermaid draws its flowcharts with: a port of the dagre
///     layout of dagre-d3-es 7.0.14, the version that Mermaid 11.17.2 bundles.
/// </summary>
/// <remarks>
///     <para>
///         <c>DagreLayout</c> is the entry point: it copies the input graph (<c>DagreGraph</c>) into the internal layout
///         graph (<c>LayoutModel</c>) and runs dagre's stages in dagre's order - the cycle breaking of
///         <c>DagreAcyclic</c>, the ranking of <c>DagreRanker</c>, the cluster handling of <c>DagreNesting</c>, the
///         long-edge chains of <c>DagreNormalizer</c>, the crossing reduction of <c>DagreOrdering</c>, the coordinates of
///         <c>DagrePositioning</c> and the rank-direction changes of <c>DagreCoordinateSystem</c> - then writes the node
///         centers, cluster sizes, edge routes and label centers back. <c>Lodash</c> keeps the JavaScript and lodash
///         collection semantics the stages depend on, so the port gives dagre's results exactly.
///     </para>
///     <para>
///         Every iterative stage charges the <c>LayoutWorkMeter</c> of the diagram, which bounds the work and observes
///         cancellation. The namespace is internal to the package; a layout is run by one call on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
