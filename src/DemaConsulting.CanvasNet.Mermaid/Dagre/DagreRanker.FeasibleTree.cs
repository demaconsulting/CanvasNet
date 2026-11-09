// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The feasible tight tree (<c>rank/feasible-tree.js</c>).
/// </summary>
internal static partial class DagreRanker
{
    /// <summary>
    ///     Builds a spanning tree of tight edges (slack zero), shifting the ranks of the tree found so far toward the
    ///     nearest edge that leaves it until the tree spans the graph (dagre's <c>feasibleTree</c>).
    /// </summary>
    /// <param name="g">A connected, ranked graph; its ranks are shifted.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The undirected tree.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the graph is not connected.</exception>
    public static Graph<object, TreeNode, TreeEdge> FeasibleTree(LayoutGraph g, LayoutWorkMeter meter)
    {
        var t = new Graph<object, TreeNode, TreeEdge>(directed: false);
        var nodes = g.Nodes();
        var size = g.NodeCount;
        t.SetNode(nodes.Length > 0 ? nodes[0] : "undefined", new TreeNode());
        while (TightTree(t, g, meter) < size)
        {
            var edge = FindMinSlackEdge(t, g)
                ?? throw new InvalidOperationException("the graph to rank is not connected");
            var delta = t.HasNode(edge.V) ? Slack(g, edge) : -Slack(g, edge);
            ShiftRanks(t, g, delta);
        }

        return t;
    }

    /// <summary>
    ///     Grows the tree along tight edges from every node in it.
    /// </summary>
    /// <param name="t">The tree.</param>
    /// <param name="g">The graph.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The number of nodes in the tree.</returns>
    private static int TightTree(Graph<object, TreeNode, TreeEdge> t, LayoutGraph g, LayoutWorkMeter meter)
    {
        meter.Charge(g.NodeCount + g.EdgeCount);
        foreach (var v in t.Nodes())
        {
            Visit(v);
        }

        return t.NodeCount;

        // Add each node joined to this one by a tight edge, and continue from it
        void Visit(string v)
        {
            var edges = g.NodeEdges(v);
            if (edges is null)
            {
                return;
            }

            RuntimeHelpers.EnsureSufficientExecutionStack();
            foreach (var e in edges)
            {
                var w = v == e.V ? e.W : e.V;
                if (!t.HasNode(w) && !DagreLayout.Truthy(Slack(g, e)))
                {
                    t.SetNode(w, new TreeNode());
                    t.SetEdge(v, w, new TreeEdge());
                    Visit(w);
                }
            }
        }
    }

    /// <summary>
    ///     Finds the edge with the least slack that has exactly one end in the tree.
    /// </summary>
    /// <param name="t">The tree.</param>
    /// <param name="g">The graph.</param>
    /// <returns>The edge, or null when none leaves the tree.</returns>
    private static GraphEdge? FindMinSlackEdge(Graph<object, TreeNode, TreeEdge> t, LayoutGraph g) =>
        Lodash.MinBy(g.Edges(), e => t.HasNode(e.V) != t.HasNode(e.W) ? Slack(g, e) : null, out _);

    /// <summary>
    ///     Shifts the rank of every node of the tree.
    /// </summary>
    /// <param name="t">The tree.</param>
    /// <param name="g">The graph.</param>
    /// <param name="delta">The shift.</param>
    private static void ShiftRanks(Graph<object, TreeNode, TreeEdge> t, LayoutGraph g, double delta)
    {
        foreach (var v in t.Nodes())
        {
            var node = g.Node(v)!;
            node.Rank = Lodash.Num(node.Rank) + delta;
        }
    }
}
