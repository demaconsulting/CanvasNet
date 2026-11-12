// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayerGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerEdge>;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's crossing reduction (<c>order/index.js</c>, <c>init-order.js</c> and <c>build-layer-graph.js</c>): orders
///     the nodes of every rank to reduce edge crossings.
/// </summary>
/// <remarks>
///     <para>
///         The initial order comes from a depth-first search in rank order. Then sweeps alternate down (each rank by the
///         barycenters of its predecessors) and up (by its successors), with ties biased left or right in turns; each
///         sweep keeps the clusters contiguous through constraint edges between sibling clusters. The order with the
///         fewest crossings is kept, and the sweeps stop after four without improvement.
///     </para>
///     <para>
///         A layer graph holds the nodes of one rank under their clusters (top-level nodes under a dummy root), the
///         clusters with their border nodes at that rank, and the weighted edges from the fixed adjacent rank. Each sweep
///         is charged to the meter by the size of its layer graphs.
///     </para>
/// </remarks>
internal static partial class DagreOrdering
{
    /// <summary>
    ///     Orders the nodes of every rank (dagre's <c>order</c>).
    /// </summary>
    /// <param name="g">The normalized layout graph with border segments.</param>
    /// <param name="meter">The work meter.</param>
    public static void Order(LayoutGraph g, LayoutWorkMeter meter)
    {
        // The layer graphs of the down sweeps (ranks 1 to max) and the up sweeps (max - 1 to 0)
        var maxRank = DagreLayout.MaxRank(g);
        var downRanks = new List<int>();
        var upRanks = new List<int>();
        if (maxRank is { } max && !double.IsNaN(max))
        {
            for (var rank = 1; rank < max + 1; rank++)
            {
                downRanks.Add(rank);
            }

            for (var rank = (int)max - 1; rank > -1; rank--)
            {
                upRanks.Add(rank);
            }
        }
        else
        {
            // lodash's _.range(1, NaN) and _.range(NaN, -1, -1) give one rank each
            downRanks.Add(1);
            upRanks.Add(0);
        }

        var downLayerGraphs = downRanks.ConvertAll(rank => BuildLayerGraph(g, meter, rank, inEdges: true));
        var upLayerGraphs = upRanks.ConvertAll(rank => BuildLayerGraph(g, meter, rank, inEdges: false));

        var layering = InitOrder(g, meter);
        AssignOrder(g, layering);

        // Sweep until four sweeps in a row bring no improvement
        var bestCC = double.PositiveInfinity;
        List<List<string>>? best = null;
        var i = 0;
        var lastBest = 0;
        while (lastBest < 4)
        {
            SweepLayerGraphs(i % 2 != 0 ? downLayerGraphs : upLayerGraphs, i % 4 >= 2, meter);
            layering = DagreLayout.BuildLayerMatrix(g);
            var cc = CrossCount(g, layering, meter);
            if (cc < bestCC)
            {
                lastBest = 0;
                best = layering.ConvertAll(layer => new List<string>(layer));
                bestCC = cc;
            }

            ++i;
            ++lastBest;
        }

        // dagre keeps the initial order when no count is a number
        if (best is not null)
        {
            AssignOrder(g, best);
        }
    }

    /// <summary>
    ///     Builds the initial order: a depth-first search over successors from the nodes in rank order, each node
    ///     appended to its rank as it is reached (dagre's <c>initOrder</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The nodes of each rank in order.</returns>
    public static List<List<string>> InitOrder(LayoutGraph g, LayoutWorkMeter meter)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var simpleNodes = Array.FindAll(g.Nodes(), v => g.Children(v)!.Length == 0);
        meter.Charge(simpleNodes.Length + g.EdgeCount);
        var maxRank = Lodash.Max(simpleNodes.Select(v => g.Node(v)!.Rank));
        var count = maxRank is { } max && !double.IsNaN(max) ? (int)Math.Max(Math.Ceiling(max + 1), 0) : 0;
        var layers = new List<List<string>>(count);
        for (var i = 0; i < count; i++)
        {
            layers.Add([]);
        }

        // Search from the nodes in rank order
        foreach (var start in Lodash.SortBy(simpleNodes, v => g.Node(v)!.Rank))
        {
            Visit(start);
        }

        return layers;

        // Append the node to its rank, then its successors
        void Visit(string v)
        {
            if (!visited.Add(v))
            {
                return;
            }

            System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();
            layers[(int)Lodash.Num(g.Node(v)!.Rank)].Add(v);
            foreach (var w in g.Successors(v)!)
            {
                Visit(w);
            }
        }
    }

    /// <summary>
    ///     Builds the layer graph of one rank (dagre's <c>buildLayerGraph</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The work meter, which also names the dummy root.</param>
    /// <param name="rank">The rank.</param>
    /// <param name="inEdges">True for the in-edges (down sweeps), false for the out-edges (up sweeps).</param>
    /// <returns>The layer graph.</returns>
    public static LayerGraph BuildLayerGraph(LayoutGraph g, LayoutWorkMeter meter, int rank, bool inEdges)
    {
        var root = CreateRootNode(g, meter);
        var result = new LayerGraph(compound: true)
            .SetGraph(new LayerGraphLabel(root))
            .SetDefaultNodeLabel(v => new LayerNode(g.Node(v)));
        var nodes = g.Nodes();
        meter.Charge(nodes.Length);
        foreach (var v in nodes)
        {
            var node = g.Node(v)!;
            var parent = g.Parent(v);
            if ((node.Rank is { } nodeRank && DagreLayout.Same(nodeRank, rank)) || (node.MinRank <= rank && rank <= node.MaxRank))
            {
                result.SetNode(v);
                result.SetParent(v, string.IsNullOrEmpty(parent) ? root : parent);

                // The edges from the fixed rank, with the weights of parallel edges summed
                foreach (var e in (inEdges ? g.InEdges(v) : g.OutEdges(v))!)
                {
                    var u = e.V == v ? e.W : e.V;
                    var edge = result.Edge(u, v);
                    var weight = edge is not null ? edge.Weight : 0;
                    result.SetEdge(u, v, new LayerEdge(g.Edge(e)!.Weight + weight));
                }

                if (node.MinRank is not null)
                {
                    result.SetNode(v, new LayerNode(null, BorderAt(node.BorderLeft, rank), BorderAt(node.BorderRight, rank)));
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Sets the order of every node from a layering.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="layering">The nodes of each rank in order.</param>
    private static void AssignOrder(LayoutGraph g, List<List<string>> layering)
    {
        foreach (var layer in layering)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                g.Node(layer[i])!.Order = i;
            }
        }
    }

    /// <summary>
    ///     Sorts every layer graph of one sweep, recording the order and the cluster constraints as it goes.
    /// </summary>
    /// <param name="layerGraphs">The layer graphs in sweep order.</param>
    /// <param name="biasRight">Whether ties go to the right.</param>
    /// <param name="meter">The work meter.</param>
    private static void SweepLayerGraphs(List<LayerGraph> layerGraphs, bool biasRight, LayoutWorkMeter meter)
    {
        var cg = new Graph<object, object, object>();
        foreach (var lg in layerGraphs)
        {
            meter.Charge(lg.NodeCount + lg.EdgeCount + 1);
            var root = lg.GraphLabel!.Root;
            var sorted = SortSubgraph(lg, root, cg, biasRight);
            for (var i = 0; i < sorted.Vs.Count; i++)
            {
                lg.Node(sorted.Vs[i])!.Node!.Order = i;
            }

            AddSubgraphConstraints(lg, cg, sorted.Vs);
        }
    }

    /// <summary>
    ///     Names the dummy root of a layer graph with a name the layout graph does not use.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The meter, which gives the names.</param>
    /// <returns>The name.</returns>
    private static string CreateRootNode(LayoutGraph g, LayoutWorkMeter meter)
    {
        var v = meter.UniqueId("_root");
        while (g.HasNode(v))
        {
            v = meter.UniqueId("_root");
        }

        return v;
    }

    /// <summary>
    ///     Reads a border node of a cluster at a rank, or null where the JavaScript array has no item.
    /// </summary>
    /// <param name="borders">The border nodes by rank.</param>
    /// <param name="rank">The rank.</param>
    /// <returns>The node, or null.</returns>
    private static string? BorderAt(List<string?>? borders, int rank) =>
        borders is not null && rank >= 0 && rank < borders.Count ? borders[rank] : null;

    /// <summary>
    ///     The graph label of a layer graph.
    /// </summary>
    /// <param name="Root">The dummy root that holds the top-level nodes.</param>
    internal sealed record LayerGraphLabel(string Root);

    /// <summary>
    ///     The label of a node of a layer graph: the layout label of a node (shared, so that the order written here is the
    ///     node's order), or the border nodes of a cluster at the rank.
    /// </summary>
    /// <param name="Node">The layout label, or null for a cluster and for the dummy root.</param>
    /// <param name="BorderLeft">The left border node of a cluster at the rank, or null.</param>
    /// <param name="BorderRight">The right border node of a cluster at the rank, or null.</param>
    internal sealed record LayerNode(LayoutNode? Node, string? BorderLeft = null, string? BorderRight = null);

    /// <summary>
    ///     The label of an edge of a layer graph.
    /// </summary>
    /// <param name="Weight">The summed weight.</param>
    internal sealed record LayerEdge(double Weight);
}
