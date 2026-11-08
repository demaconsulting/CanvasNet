// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's ranking (<c>rank/index.js</c> and <c>rank/util.js</c>): assigns each node a rank so that every edge
///     points to a greater rank by at least its minimum length.
/// </summary>
/// <remarks>
///     The ranker is chosen by the graph's <c>ranker</c> attribute: <c>network-simplex</c> (the default, also for an
///     unknown value) minimizes the weighted edge lengths with the network simplex method of Gansner et al.;
///     <c>tight-tree</c> builds a feasible tight tree on the longest-path ranking; <c>longest-path</c> pushes every
///     node as low as its successors allow. Ranks may be negative; a later stage normalizes them.
/// </remarks>
internal static partial class DagreRanker
{
    /// <summary>
    ///     Ranks the nodes with the graph's ranker (dagre's <c>rank</c>).
    /// </summary>
    /// <param name="g">A flat, acyclic, connected layout graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void Rank(LayoutGraph g, LayoutWorkMeter meter)
    {
        switch (g.GraphLabel!.Ranker)
        {
            case "tight-tree":
                LongestPath(g, meter);
                FeasibleTree(g, meter);
                break;
            case "longest-path":
                LongestPath(g, meter);
                break;
            default:
                NetworkSimplex(g, meter);
                break;
        }
    }

    /// <summary>
    ///     Ranks each node as far down as its successors allow: the least of the successor ranks minus the edge minimum
    ///     lengths, or zero for a sink (dagre's <c>longestPath</c>).
    /// </summary>
    /// <param name="g">An acyclic layout graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void LongestPath(LayoutGraph g, LayoutWorkMeter meter)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        meter.Charge(g.NodeCount + g.EdgeCount);
        foreach (var v in g.Sources())
        {
            Visit(v);
        }

        // The rank of a node from the ranks of its successors
        double? Visit(string v)
        {
            var label = g.Node(v)!;
            if (!visited.Add(v))
            {
                return label.Rank;
            }

            RuntimeHelpers.EnsureSufficientExecutionStack();
            var rank = Lodash.Min(g.OutEdges(v)!.Select(e => (double?)(Lodash.Num(Visit(e.W)) - g.Edge(e)!.MinLen)).ToList());
            if (rank is null || double.IsPositiveInfinity(rank.Value))
            {
                rank = 0;
            }

            label.Rank = rank;
            return rank;
        }
    }

    /// <summary>
    ///     Gets the slack of an edge: how much longer it is than its minimum length (dagre's <c>slack</c>).
    /// </summary>
    /// <param name="g">The ranked graph.</param>
    /// <param name="e">The edge.</param>
    /// <returns>The slack.</returns>
    public static double Slack(LayoutGraph g, GraphEdge e) =>
        Lodash.Num(g.Node(e.W)!.Rank) - Lodash.Num(g.Node(e.V)!.Rank) - g.Edge(e)!.MinLen;

    /// <summary>
    ///     The label of a node of the spanning tree: the postorder range of its subtree and its parent.
    /// </summary>
    internal sealed class TreeNode
    {
        /// <summary>Gets or sets the least postorder number of the subtree.</summary>
        public int Low { get; set; }

        /// <summary>Gets or sets the postorder number of the node.</summary>
        public int Lim { get; set; }

        /// <summary>Gets or sets the parent in the tree, or null for the root.</summary>
        public string? Parent { get; set; }
    }

    /// <summary>
    ///     The label of an edge of the spanning tree: its cut value.
    /// </summary>
    internal sealed class TreeEdge
    {
        /// <summary>Gets or sets the cut value.</summary>
        public double CutValue { get; set; }
    }
}
