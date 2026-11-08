// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;
using TreeGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    object,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreRanker.TreeNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreRanker.TreeEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The network simplex ranker (<c>rank/network-simplex.js</c>).
/// </summary>
internal static partial class DagreRanker
{
    /// <summary>
    ///     Ranks the nodes with the network simplex method (dagre's <c>networkSimplex</c>): a longest-path ranking, a
    ///     feasible tight tree, then pivots that swap a tree edge with a negative cut value for the non-tree edge of least
    ///     slack across the same cut, until no cut value is negative.
    /// </summary>
    /// <remarks>
    ///     Each pivot recomputes the postorder numbers, the cut values and the ranks, and is charged to the meter as such.
    /// </remarks>
    /// <param name="g">A connected, acyclic layout graph; its node ranks are set.</param>
    /// <param name="meter">The work meter.</param>
    public static void NetworkSimplex(LayoutGraph g, LayoutWorkMeter meter)
    {
        g = DagreLayout.Simplify(g);
        LongestPath(g, meter);
        var t = FeasibleTree(g, meter);
        InitLowLimValues(t);
        InitCutValues(t, g, meter);
        while (LeaveEdge(t, meter) is { } e)
        {
            var f = EnterEdge(t, g, e, meter);
            ExchangeEdges(t, g, e, f, meter);
        }
    }

    /// <summary>
    ///     Computes the cut value of every tree edge, children before parents.
    /// </summary>
    /// <param name="t">The tree, with postorder numbers and parents.</param>
    /// <param name="g">The graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void InitCutValues(TreeGraph t, LayoutGraph g, LayoutWorkMeter meter)
    {
        meter.Charge(t.NodeCount + g.EdgeCount);
        var vs = GraphAlgorithms.Postorder(t, t.Nodes());
        vs.RemoveAt(vs.Count - 1);
        foreach (var child in vs)
        {
            var parent = t.Node(child)!.Parent!;
            t.Edge(child, parent)!.CutValue = CalcCutValue(t, g, child);
        }
    }

    /// <summary>
    ///     Computes the cut value of the tree edge from a node to its parent, from the cut values of its child edges
    ///     (dagre's <c>calcCutValue</c>).
    /// </summary>
    /// <param name="t">The tree.</param>
    /// <param name="g">The graph.</param>
    /// <param name="child">The node.</param>
    /// <returns>The cut value.</returns>
    public static double CalcCutValue(TreeGraph t, LayoutGraph g, string child)
    {
        var parent = t.Node(child)!.Parent!;

        // The graph edge of the tree edge, and whether the child is its tail
        var childIsTail = true;
        var graphEdge = g.Edge(child, parent);
        if (graphEdge is null)
        {
            childIsTail = false;
            graphEdge = g.Edge(parent, child)!;
        }

        var cutValue = graphEdge.Weight;
        foreach (var e in g.NodeEdges(child)!)
        {
            var isOutEdge = e.V == child;
            var other = isOutEdge ? e.W : e.V;
            if (other != parent)
            {
                var pointsToHead = isOutEdge == childIsTail;
                var otherWeight = g.Edge(e)!.Weight;
                cutValue += pointsToHead ? otherWeight : -otherWeight;
                if (t.HasEdge(child, other))
                {
                    var otherCutValue = t.Edge(child, other)!.CutValue;
                    cutValue += pointsToHead ? -otherCutValue : otherCutValue;
                }
            }
        }

        return cutValue;
    }

    /// <summary>
    ///     Numbers the tree in postorder from its first node, recording for each node the range of its subtree and its
    ///     parent (dagre's <c>initLowLimValues</c>).
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="root">The root, or null for the first node.</param>
    public static void InitLowLimValues(TreeGraph tree, string? root = null)
    {
        root ??= tree.Nodes()[0];
        var visited = new HashSet<string>(StringComparer.Ordinal);
        DfsAssignLowLim(tree, visited, 1, root, null);
    }

    /// <summary>
    ///     Finds the first tree edge with a negative cut value (dagre's <c>leaveEdge</c>).
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The edge, or null when the ranking is optimal.</returns>
    public static GraphEdge? LeaveEdge(TreeGraph tree, LayoutWorkMeter meter)
    {
        meter.Charge(tree.EdgeCount + 1);
        return Array.Find(tree.Edges(), e => tree.Edge(e)!.CutValue < 0);
    }

    /// <summary>
    ///     Finds the graph edge of least slack that crosses the cut of a leaving tree edge in the other direction (dagre's
    ///     <c>enterEdge</c>).
    /// </summary>
    /// <param name="t">The tree.</param>
    /// <param name="g">The graph.</param>
    /// <param name="edge">The leaving tree edge.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The entering edge.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no edge crosses the cut.</exception>
    public static GraphEdge EnterEdge(TreeGraph t, LayoutGraph g, GraphEdge edge, LayoutWorkMeter meter)
    {
        meter.Charge(g.EdgeCount + 1);
        var v = edge.V;
        var w = edge.W;

        // For the rest of this function v is the tail and w the head, so the tail side is known
        if (!g.HasEdge(v, w))
        {
            v = edge.W;
            w = edge.V;
        }

        var vLabel = t.Node(v)!;
        var wLabel = t.Node(w)!;
        var tailLabel = vLabel;
        var flip = false;

        // When the root is in the tail of the edge the head side is used instead
        if (vLabel.Lim > wLabel.Lim)
        {
            tailLabel = wLabel;
            flip = true;
        }

        var candidates = Array.FindAll(
            g.Edges(),
            candidate => flip == IsDescendant(t.Node(candidate.V)!, tailLabel) &&
                         flip != IsDescendant(t.Node(candidate.W)!, tailLabel));
        return Lodash.MinBy(candidates, candidate => Slack(g, candidate), out var found) is { } entering && found
            ? entering
            : throw new InvalidOperationException("no edge enters the cut of the network simplex");
    }

    /// <summary>
    ///     Replaces a tree edge with a graph edge and recomputes the numbering, the cut values and the ranks (dagre's
    ///     <c>exchangeEdges</c>).
    /// </summary>
    /// <param name="t">The tree.</param>
    /// <param name="g">The graph.</param>
    /// <param name="e">The leaving tree edge.</param>
    /// <param name="f">The entering graph edge.</param>
    /// <param name="meter">The work meter.</param>
    public static void ExchangeEdges(TreeGraph t, LayoutGraph g, GraphEdge e, GraphEdge f, LayoutWorkMeter meter)
    {
        t.RemoveEdge(e.V, e.W);
        t.SetEdge(f.V, f.W, new TreeEdge());
        meter.Charge(2L * t.NodeCount);
        InitLowLimValues(t);
        InitCutValues(t, g, meter);
        UpdateRanks(t, g);
    }

    /// <summary>
    ///     Sets the ranks from the root of the tree down, each node at its tree edge's minimum length from its parent.
    /// </summary>
    /// <remarks>
    ///     dagre picks as root the first tree node whose graph label has no <c>parent</c> property; layout labels never
    ///     have one, so the root is the first tree node.
    /// </remarks>
    /// <param name="t">The tree.</param>
    /// <param name="g">The graph.</param>
    private static void UpdateRanks(TreeGraph t, LayoutGraph g)
    {
        var root = t.Nodes()[0];
        var vs = GraphAlgorithms.Preorder(t, [root]);
        foreach (var v in vs.Skip(1))
        {
            var parent = t.Node(v)!.Parent!;
            var edge = g.Edge(v, parent);
            var flipped = false;
            if (edge is null)
            {
                edge = g.Edge(parent, v)!;
                flipped = true;
            }

            g.Node(v)!.Rank = Lodash.Num(g.Node(parent)!.Rank) + (flipped ? edge.MinLen : -edge.MinLen);
        }
    }

    /// <summary>
    ///     Numbers a subtree in postorder (dagre's <c>dfsAssignLowLim</c>).
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="visited">The nodes numbered so far.</param>
    /// <param name="nextLim">The next number.</param>
    /// <param name="v">The subtree root.</param>
    /// <param name="parent">Its parent, or null.</param>
    /// <returns>The next number after the subtree.</returns>
    private static int DfsAssignLowLim(TreeGraph tree, HashSet<string> visited, int nextLim, string v, string? parent)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var low = nextLim;
        var label = tree.Node(v)!;
        visited.Add(v);
        foreach (var w in tree.Neighbors(v)!.Where(w => !visited.Contains(w)))
        {
            nextLim = DfsAssignLowLim(tree, visited, nextLim, w, v);
        }

        label.Low = low;
        label.Lim = nextLim++;
        label.Parent = string.IsNullOrEmpty(parent) ? null : parent;
        return nextLim;
    }

    /// <summary>
    ///     Gets whether a node is in the subtree of another, by their postorder numbers.
    /// </summary>
    /// <param name="vLabel">The node.</param>
    /// <param name="rootLabel">The subtree root.</param>
    /// <returns>True when it is.</returns>
    private static bool IsDescendant(TreeNode vLabel, TreeNode rootLabel) =>
        rootLabel.Low <= vLabel.Lim && vLabel.Lim <= rootLabel.Lim;
}
