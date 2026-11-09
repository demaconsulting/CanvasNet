// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The subgraph handling of <c>mermaid-graphlib.js</c>: the subgraph table (<c>clusterDb</c>), the anchors that
///     replace links to subgraphs (<c>adjustClustersAndEdges</c>, <c>findNonClusterChild</c>), the extraction of the
///     subgraphs without links to the outside into graphs of their own (<c>extractor</c>, <c>copy</c>) and the drawing
///     order (<c>sortNodesByHierarchy</c>).
/// </content>
internal sealed partial class ClusterLayout
{
    /// <summary>The deepest level <c>extractor</c> goes to.</summary>
    private const int MaxExtractorDepth = 10;

    /// <summary>The subgraph table (Mermaid's <c>clusterDb</c>).</summary>
    private readonly Dictionary<string, ClusterInfo> _clusterDb = new(StringComparer.Ordinal);

    /// <summary>The descendants of each subgraph (Mermaid's <c>descendants</c>).</summary>
    private readonly Dictionary<string, HashSet<string>> _descendants = new(StringComparer.Ordinal);

    /// <summary>
    ///     Moves the links to subgraphs onto their anchors and extracts the subgraphs without links to the outside
    ///     (Mermaid's <c>adjustClustersAndEdges</c>).
    /// </summary>
    /// <param name="graph">The root graph.</param>
    /// <exception cref="MermaidRenderException">Thrown, located, when a link to a subgraph has no anchor (Mermaid fails there).</exception>
    private void AdjustClustersAndEdges(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph)
    {
        // Every subgraph: its descendants and its first non-subgraph descendant
        var order = new List<string>();
        foreach (var id in graph.Nodes())
        {
            _meter.Charge(1);
            if (graph.Children(id) is { Length: > 0 })
            {
                _descendants[id] = [.. ExtractDescendants(id, graph)];
                _clusterDb[id] = new ClusterInfo { Id = FindNonClusterChild(id, graph, id), ClusterData = graph.Node(id) };
                order.Add(id);
            }
        }

        // A subgraph that an edge leaves or enters has external connections
        foreach (var id in graph.Nodes())
        {
            if (graph.Children(id) is not { Length: > 0 })
            {
                continue;
            }

            var edges = graph.Edges();
            _meter.Charge(edges.Length);
            if (Array.Exists(edges, edge => IsDescendant(edge.V, id) ^ IsDescendant(edge.W, id)))
            {
                _clusterDb[id].ExternalConnections = true;
            }
        }

        // The anchors: a node that survives the extraction
        foreach (var id in order)
        {
            var info = _clusterDb[id];
            var nonClusterChild = info.Id;
            var parent = nonClusterChild is null ? null : graph.Parent(nonClusterChild);
            if (parent is not null && parent != id && _clusterDb.TryGetValue(parent, out var parentInfo) && !parentInfo.ExternalConnections)
            {
                info.Id = parent;
            }

            _meter.Charge(graph.EdgeCount);
            var hasDirectOutgoingEdge = Array.Exists(graph.Edges(), edge => edge.V == id);
            if (nonClusterChild is not null
                && info.ExternalConnections
                && hasDirectOutgoingEdge
                && IsNodeInExtractableCluster(graph, nonClusterChild, id)
                && FindSafeAnchorNode(graph, id, graph.Parent(nonClusterChild)) is { } safeAnchor)
            {
                info.Id = safeAnchor;
            }
        }

        // The links to subgraphs with external connections move to the anchors
        foreach (var e in graph.Edges())
        {
            _meter.Charge(1);
            if (!_clusterDb.ContainsKey(e.V) && !_clusterDb.ContainsKey(e.W))
            {
                continue;
            }

            var edge = graph.Edge(e);
            var v = GetAnchorId(e.V);
            var w = GetAnchorId(e.W);
            graph.RemoveEdge(e.V, e.W, e.Name);
            if (edge is null)
            {
                continue;
            }

            if (v != e.V)
            {
                MarkExternal(graph, v, edge);
                edge.FromCluster = e.V;
            }

            if (w != e.W)
            {
                MarkExternal(graph, w, edge);
                edge.ToCluster = e.W;
            }

            graph.SetEdge(v, w, edge, e.Name);
        }

        Extractor(graph, 0);
    }

    /// <summary>
    ///     Marks the subgraph around an anchor as having external connections.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="anchor">The anchor.</param>
    /// <param name="edge">The moved edge.</param>
    /// <exception cref="MermaidRenderException">Thrown when the anchor is not inside a subgraph (Mermaid fails there).</exception>
    private void MarkExternal(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, string anchor, ClusterLayoutEdge edge)
    {
        if (graph.Parent(anchor) is not { } parent || !_clusterDb.TryGetValue(parent, out var info))
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.RenderFailed,
                $"the link '{edge.Id}' to a subgraph has no node to attach to",
                edge.Data.Location,
                "link to a node inside the subgraph instead");
        }

        info.ExternalConnections = true;
    }

    /// <summary>
    ///     Gets the node a link to an id attaches to (Mermaid's <c>getAnchorId</c>): the anchor of a subgraph with
    ///     external connections, else the id itself.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The anchor.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the subgraph has no anchor (Mermaid fails there).</exception>
    private string GetAnchorId(string id)
    {
        if (!_clusterDb.TryGetValue(id, out var info) || !info.ExternalConnections)
        {
            return id;
        }

        return info.Id ?? throw new MermaidRenderException(
            MermaidDiagnosticCodes.RenderFailed,
            $"the subgraph '{id}' has no node that a link can attach to",
            info.ClusterData?.Data?.Location ?? default,
            "put a node in the subgraph, or link to a node inside it");
    }

    /// <summary>
    ///     Lays out each subgraph without external connections as a graph of its own, recursively (Mermaid's
    ///     <c>extractor</c>): its contents move to a new graph, and a <c>clusterNode</c> takes its place.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="depth">The depth of the recursion.</param>
    private void Extractor(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, int depth)
    {
        if (depth > MaxExtractorDepth)
        {
            return;
        }

        var nodes = graph.Nodes();
        if (!Array.Exists(nodes, node => graph.Children(node) is { Length: > 0 }))
        {
            return;
        }

        foreach (var node in nodes)
        {
            _meter.Charge(1);
            if (!_clusterDb.TryGetValue(node, out var info) || info.ExternalConnections || graph.Children(node) is not { Length: > 0 })
            {
                continue;
            }

            // The other direction of the graph's, unless the subgraph declares its own
            var direction = graph.GraphLabel?.RankDir == "TB" ? "LR" : "TB";
            if (info.ClusterData?.Data?.Dir is { Length: > 0 } declared)
            {
                direction = declared;
            }

            var clusterGraph = new Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge>(multigraph: true, compound: true)
                .SetGraph(new DagreGraph { RankDir = direction, NodeSep = 50, RankSep = 50, MarginX = GraphMargin, MarginY = GraphMargin });
            Copy(node, graph, clusterGraph, node);
            graph.SetNode(node, new ClusterLayoutNode(node, info.ClusterData?.Data, clusterGraph));
        }

        foreach (var node in graph.Nodes())
        {
            if (graph.Node(node)?.SubGraph is { } subGraph)
            {
                Extractor(subGraph, depth + 1);
            }
        }
    }

    /// <summary>
    ///     Moves a subgraph's contents to a new graph (Mermaid's <c>copy</c>): every leaf with its parent, the edges
    ///     inside the subgraph, and the nested subgraphs.
    /// </summary>
    /// <param name="clusterId">The subgraph being copied.</param>
    /// <param name="graph">The graph it is in.</param>
    /// <param name="newGraph">The new graph.</param>
    /// <param name="rootId">The subgraph being extracted.</param>
    private void Copy(
        string clusterId,
        Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph,
        Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> newGraph,
        string rootId)
    {
        var nodes = (graph.Children(clusterId) ?? []).ToList();
        if (clusterId != rootId)
        {
            nodes.Add(clusterId);
        }

        foreach (var node in nodes)
        {
            _meter.Charge(1);
            if (graph.Children(node) is { Length: > 0 })
            {
                Copy(node, graph, newGraph, rootId);
            }
            else
            {
                newGraph.SetNode(node, graph.Node(node));
                var parent = graph.Parent(node);
                if (rootId != parent)
                {
                    newGraph.SetParent(node, parent);
                }

                if (clusterId != rootId && node != clusterId)
                {
                    newGraph.SetParent(node, clusterId);
                }

                // graph.edges(node) returns every edge of the graph; those inside the subgraph are copied
                var edges = graph.Edges();
                _meter.Charge(edges.Length);
                foreach (var edge in edges.Where(edge => EdgeInCluster(edge.V, edge.W, rootId)))
                {
                    newGraph.SetEdge(edge.V, edge.W, graph.Edge(edge), edge.Name);
                }
            }

            graph.RemoveNode(node);
        }
    }

    /// <summary>
    ///     Gets whether an edge is inside a subgraph and does not touch the subgraph itself (Mermaid's
    ///     <c>edgeInCluster</c>).
    /// </summary>
    /// <param name="v">The edge's start.</param>
    /// <param name="w">The edge's end.</param>
    /// <param name="clusterId">The subgraph.</param>
    /// <returns>Whether it is.</returns>
    private bool EdgeInCluster(string v, string w, string clusterId) =>
        v != clusterId && w != clusterId && (IsDescendant(v, clusterId) || IsDescendant(w, clusterId));

    /// <summary>
    ///     Gets whether an id is a descendant of a subgraph.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="ancestorId">The subgraph, or null.</param>
    /// <returns>Whether it is.</returns>
    private bool IsDescendant(string id, string? ancestorId) =>
        ancestorId is not null && _descendants.TryGetValue(ancestorId, out var descendants) && descendants.Contains(id);

    /// <summary>
    ///     Gets the descendants of a node, depth first (Mermaid's <c>extractDescendants</c>).
    /// </summary>
    /// <param name="id">The node.</param>
    /// <param name="graph">The graph.</param>
    /// <returns>The descendants.</returns>
    private List<string> ExtractDescendants(string id, Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph)
    {
        var children = graph.Children(id) ?? [];
        var result = new List<string>(children);
        foreach (var child in children)
        {
            _meter.Charge(1);
            result.AddRange(ExtractDescendants(child, graph));
        }

        return result;
    }

    /// <summary>
    ///     Finds a descendant of a node that is not a subgraph, preferring one that does not share an edge with a
    ///     subgraph (Mermaid's <c>findNonClusterChild</c>).
    /// </summary>
    /// <param name="id">The node.</param>
    /// <param name="graph">The graph.</param>
    /// <param name="clusterId">The subgraph whose edges are compared, or null.</param>
    /// <returns>The descendant (the node itself when it has no children), or null.</returns>
    private string? FindNonClusterChild(string id, Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, string? clusterId)
    {
        var children = graph.Children(id);
        if (children is not { Length: > 0 })
        {
            return id;
        }

        string? reserve = null;
        foreach (var child in children)
        {
            var found = FindNonClusterChild(child, graph, clusterId);
            var common = CountCommonEdges(graph, clusterId, found);
            if (found is not null)
            {
                if (common > 0)
                {
                    reserve = found;
                }
                else
                {
                    return found;
                }
            }
        }

        return reserve;
    }

    /// <summary>
    ///     Counts the edges of one id that, redirected to another, an edge of the other already has (Mermaid's
    ///     <c>findCommonEdges</c>, quirks included).
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="id1">The first id, or null.</param>
    /// <param name="id2">The second id, or null.</param>
    /// <returns>The number of common edges.</returns>
    private int CountCommonEdges(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, string? id1, string? id2)
    {
        var edges = graph.Edges();
        _meter.Charge(edges.Length);
        var edges2 = edges.Where(e => e.V == id2 || e.W == id2).Select(e => (e.V, e.W)).ToHashSet();
        return edges
            .Where(e => e.V == id1 || e.W == id1)
            .Select(e => (V: e.V == id1 ? id2 : e.V, W: e.W == id1 ? id1 : e.W))
            .Count(e => e.V is not null && edges2.Contains((e.V, e.W)));
    }

    /// <summary>
    ///     Gets whether a node is inside a subgraph (below a root) that will be extracted (Mermaid's
    ///     <c>isNodeInExtractableCluster</c>).
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="node">The node.</param>
    /// <param name="rootId">The subgraph to stop at.</param>
    /// <returns>Whether it is.</returns>
    private bool IsNodeInExtractableCluster(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, string node, string rootId)
    {
        for (var parent = graph.Parent(node); parent is not null && parent != rootId; parent = graph.Parent(parent))
        {
            if (_clusterDb.TryGetValue(parent, out var cluster) && !cluster.ExternalConnections)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Finds an anchor of a subgraph that is not inside a subgraph that will be extracted (Mermaid's
    ///     <c>findSafeAnchorNode</c>).
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="clusterId">The subgraph.</param>
    /// <param name="excludedCluster">The nested subgraph to skip, or null.</param>
    /// <returns>The anchor, or null.</returns>
    private string? FindSafeAnchorNode(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, string clusterId, string? excludedCluster)
    {
        foreach (var child in graph.Children(clusterId) ?? [])
        {
            if (child == excludedCluster || IsDescendant(child, excludedCluster))
            {
                continue;
            }

            if (FindNonClusterChild(child, graph, clusterId) is { } candidate && !IsNodeInExtractableCluster(graph, candidate, clusterId))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     Orders the nodes of a graph by level: the top level, then each node's children subtree by subtree (Mermaid's
    ///     <c>sortNodesByHierarchy</c>).
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The nodes.</returns>
    internal static List<string> SortNodesByHierarchy(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Sorter(graph, graph.Children() ?? []);
    }

    /// <summary>
    ///     Mermaid's <c>sorter</c>: the nodes, then the sorted children of each.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="nodes">The nodes.</param>
    /// <returns>The sorted nodes.</returns>
    private static List<string> Sorter(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, string[] nodes)
    {
        var result = new List<string>(nodes);
        foreach (var node in nodes)
        {
            result.AddRange(Sorter(graph, graph.Children(node) ?? []));
        }

        return result;
    }

    /// <summary>
    ///     An entry of the subgraph table.
    /// </summary>
    private sealed class ClusterInfo
    {
        /// <summary>Gets or sets the anchor (Mermaid's <c>id</c>), or null.</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the subgraph's node when the table was built (Mermaid's <c>clusterData</c>).</summary>
        public ClusterLayoutNode? ClusterData { get; set; }

        /// <summary>Gets or sets a value indicating whether an edge leaves or enters the subgraph.</summary>
        public bool ExternalConnections { get; set; }

        /// <summary>Gets or sets the drawn subgraph node whose box cuts the edges (Mermaid's <c>node</c>).</summary>
        public ClusterLayoutNode? Node { get; set; }
    }
}
