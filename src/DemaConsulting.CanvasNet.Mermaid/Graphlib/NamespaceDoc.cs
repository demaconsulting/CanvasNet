namespace DemaConsulting.CanvasNet.Mermaid.Graphlib;

/// <summary>
///     The <c>Graphlib</c> namespace is the graph data structure of the layout: a port of graphlib as bundled by
///     dagre-d3-es 7.0.14, the version that Mermaid 11.17.2 lays its diagrams out with.
/// </summary>
/// <remarks>
///     <para>
///         <c>Graph</c> is a directed or undirected, simple or multi, flat or compound graph whose nodes, edges and
///         children are kept in the key order of a JavaScript object (<c>ObjectKeyMap</c>): keys that are array indexes
///         (such as <c>"2"</c> and <c>"10"</c>) come first in ascending numeric order, then the other keys in insertion
///         order. Every dagre stage iterates these collections, so this order decides ties and therefore the layout;
///         keeping it makes the port give the same results as Mermaid for any node names. <c>GraphAlgorithms</c> holds the
///         traversals that dagre and Mermaid's dagre wrapper use (<c>preorder</c>, <c>postorder</c>, <c>components</c>,
///         <c>tarjan</c> and <c>findCycles</c>).
///     </para>
///     <para>
///         The namespace is internal to the package. A graph is mutable and is used by one call on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
