// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's cluster handling (<c>nesting-graph.js</c>, <c>parent-dummy-chains.js</c> and
///     <c>add-border-segments.js</c>): keeps the members of a cluster between its top and bottom borders while ranking,
///     moves the dummy nodes of long edges into the clusters they cross, and adds the left and right border nodes of
///     each cluster rank.
/// </summary>
/// <remarks>
///     The nesting graph is the method of Sander's "Layout of Compound Directed Graphs": every cluster gets a top and a
///     bottom border node joined to its members, a dummy root joins every top-level node, and every edge length is
///     multiplied so that border ranks fit between the ranks of the real nodes.
/// </remarks>
internal static class DagreNesting
{
    /// <summary>
    ///     Adds the nesting graph (dagre's <c>nestingGraph.run</c>).
    /// </summary>
    /// <param name="g">The compound layout graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void Run(LayoutGraph g, LayoutWorkMeter meter)
    {
        var root = DagreLayout.AddDummyNode(g, meter, "root", new LayoutNode(), "_root");
        var depths = TreeDepths(g);
        var height = Lodash.Num(Lodash.Max(depths.Values.Select(d => (double?)d))) - 1;
        var nodeSep = (2 * height) + 1;
        meter.Charge(g.NodeCount + g.EdgeCount);

        g.GraphLabel!.NestingRoot = root;
        foreach (var e in g.Edges())
        {
            g.Edge(e)!.MinLen *= nodeSep;
        }

        // Join every top-level node and cluster to the root
        var weight = 0.0;
        foreach (var e in g.Edges())
        {
            weight += g.Edge(e)!.Weight;
        }

        weight += 1;
        foreach (var child in g.Children()!)
        {
            Visit(g, meter, root, nodeSep, weight, height, depths, child);
        }

        g.GraphLabel.NodeRankFactor = nodeSep;
    }

    /// <summary>
    ///     Removes the nesting root and the nesting edges (dagre's <c>nestingGraph.cleanup</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    public static void Cleanup(LayoutGraph g)
    {
        var graphLabel = g.GraphLabel!;
        g.RemoveNode(graphLabel.NestingRoot!);
        graphLabel.NestingRoot = null;
        foreach (var e in g.Edges())
        {
            if (g.Edge(e)!.NestingEdge)
            {
                g.RemoveEdge(e);
            }
        }
    }

    /// <summary>
    ///     Moves each dummy node of a long edge into the cluster it crosses at its rank (dagre's
    ///     <c>parentDummyChains</c>).
    /// </summary>
    /// <param name="g">The layout graph, after normalization.</param>
    /// <param name="meter">The work meter.</param>
    public static void ParentDummyChains(LayoutGraph g, LayoutWorkMeter meter)
    {
        var postorderNums = Postorder(g);
        meter.Charge(g.NodeCount + g.GraphLabel!.DummyChains.Count);
        foreach (var start in g.GraphLabel.DummyChains)
        {
            var v = start;
            var node = g.Node(v)!;
            var edgeObj = node.EdgeObj!;
            var (path, lca) = FindPath(g, postorderNums, edgeObj.V, edgeObj.W);
            var pathIdx = 0;
            var ascending = true;
            while (v != edgeObj.W)
            {
                node = g.Node(v)!;

                // Climb while the clusters of the tail's side end above this rank
                if (ascending)
                {
                    var pathV = At(path, pathIdx);
                    while (pathV != lca && Less(g.Node(pathV!)!.MaxRank, node.Rank))
                    {
                        pathIdx++;
                        pathV = At(path, pathIdx);
                    }

                    if (pathV == lca)
                    {
                        ascending = false;
                    }
                }

                // Descend while the clusters of the head's side start at or above this rank
                if (!ascending)
                {
                    while (pathIdx < path.Count - 1 && LessOrEqual(g.Node(path[pathIdx + 1]!)!.MinRank, node.Rank))
                    {
                        pathIdx++;
                    }
                }

                g.SetParent(v, At(path, pathIdx));
                meter.Charge(path.Count + 1);
                v = g.Successors(v)![0];
            }
        }
    }

    /// <summary>
    ///     Adds the left and right border nodes of every rank of every cluster, joined from rank to rank (dagre's
    ///     <c>addBorderSegments</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void AddBorderSegments(LayoutGraph g, LayoutWorkMeter meter)
    {
        meter.Charge(g.NodeCount);
        foreach (var v in g.Children()!)
        {
            Visit(v);
        }

        // Children first, then the borders of this cluster
        void Visit(string v)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            var children = g.Children(v)!;
            var node = g.Node(v)!;
            foreach (var child in children)
            {
                Visit(child);
            }

            if (node.MinRank is { } minRank)
            {
                node.BorderLeft = [];
                node.BorderRight = [];
                var maxRank = Lodash.Num(node.MaxRank) + 1;
                for (var rank = minRank; rank < maxRank; ++rank)
                {
                    meter.Charge(2);
                    AddBorderNode(g, meter, true, v, node, (int)rank);
                    AddBorderNode(g, meter, false, v, node, (int)rank);
                }
            }
        }
    }

    /// <summary>
    ///     Adds one left or right border node of a cluster at a rank, joined to the one of the rank above.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The work meter.</param>
    /// <param name="left">True for the left border, false for the right.</param>
    /// <param name="sg">The cluster.</param>
    /// <param name="sgNode">The cluster label.</param>
    /// <param name="rank">The rank.</param>
    private static void AddBorderNode(LayoutGraph g, LayoutWorkMeter meter, bool left, string sg, LayoutNode sgNode, int rank)
    {
        var borders = left ? sgNode.BorderLeft! : sgNode.BorderRight!;
        var label = new LayoutNode { Rank = rank, BorderType = left ? "borderLeft" : "borderRight" };
        var prev = rank - 1 >= 0 && rank - 1 < borders.Count ? borders[rank - 1] : null;
        var curr = DagreLayout.AddDummyNode(g, meter, "border", label, left ? "_bl" : "_br");
        while (borders.Count <= rank)
        {
            borders.Add(null);
        }

        borders[rank] = curr;
        g.SetParent(curr, sg);
        if (!string.IsNullOrEmpty(prev))
        {
            g.SetEdge(prev, curr, new LayoutEdge { Weight = 1 });
        }
    }

    /// <summary>
    ///     Adds the nesting edges of a node or cluster.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The work meter.</param>
    /// <param name="root">The nesting root.</param>
    /// <param name="nodeSep">The rank multiplier.</param>
    /// <param name="weight">The weight of a nesting edge to a cluster (twice that to a node).</param>
    /// <param name="height">The nesting height.</param>
    /// <param name="depths">The depth of every node.</param>
    /// <param name="v">The node or cluster.</param>
    private static void Visit(LayoutGraph g, LayoutWorkMeter meter, string root, double nodeSep, double weight, double height, Dictionary<string, int> depths, string v)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var children = g.Children(v)!;
        if (children.Length == 0)
        {
            if (v != root)
            {
                g.SetEdge(root, v, new LayoutEdge { Weight = 0, MinLen = nodeSep });
            }

            return;
        }

        // The top and bottom borders of the cluster
        var top = DagreLayout.AddBorderNode(g, meter, "_bt");
        var bottom = DagreLayout.AddBorderNode(g, meter, "_bb");
        var label = g.Node(v)!;
        g.SetParent(top, v);
        label.BorderTop = top;
        g.SetParent(bottom, v);
        label.BorderBottom = bottom;

        // Each member between them
        foreach (var child in children)
        {
            Visit(g, meter, root, nodeSep, weight, height, depths, child);
            var childNode = g.Node(child)!;
            var childTop = !string.IsNullOrEmpty(childNode.BorderTop) ? childNode.BorderTop : child;
            var childBottom = !string.IsNullOrEmpty(childNode.BorderBottom) ? childNode.BorderBottom : child;
            var thisWeight = !string.IsNullOrEmpty(childNode.BorderTop) ? weight : 2 * weight;
            var minlen = childTop != childBottom ? 1 : height - depths[v] + 1;
            g.SetEdge(top, childTop, new LayoutEdge { Weight = thisWeight, MinLen = minlen, NestingEdge = true });
            g.SetEdge(childBottom, bottom, new LayoutEdge { Weight = thisWeight, MinLen = minlen, NestingEdge = true });
        }

        if (string.IsNullOrEmpty(g.Parent(v)))
        {
            g.SetEdge(root, top, new LayoutEdge { Weight = 0, MinLen = height + depths[v] });
        }
    }

    /// <summary>
    ///     Gets the depth of every node in the cluster tree, the top level at depth 1.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <returns>The depths.</returns>
    private static Dictionary<string, int> TreeDepths(LayoutGraph g)
    {
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var v in g.Children()!)
        {
            Visit(v, 1);
        }

        return depths;

        // The depth of the children, then of the node
        void Visit(string v, int depth)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            foreach (var child in g.Children(v) ?? [])
            {
                Visit(child, depth + 1);
            }

            depths[v] = depth;
        }
    }

    /// <summary>
    ///     Finds the cluster path of an edge: the clusters from the tail up to the lowest common ancestor, then down to the
    ///     head.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="postorderNums">The postorder range of every node.</param>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <returns>The path (null is the top level) and the lowest common ancestor.</returns>
    private static (List<string?> Path, string? Lca) FindPath(LayoutGraph g, Dictionary<string, (int Low, int Lim)> postorderNums, string v, string w)
    {
        var vPath = new List<string?>();
        var wPath = new List<string?>();
        var low = Math.Min(postorderNums[v].Low, postorderNums[w].Low);
        var lim = Math.Max(postorderNums[v].Lim, postorderNums[w].Lim);

        // Traverse up from v to find the LCA
        string? parent = v;
        do
        {
            parent = g.Parent(parent);
            vPath.Add(parent);
        }
        while (!string.IsNullOrEmpty(parent) && (postorderNums[parent].Low > low || lim > postorderNums[parent].Lim));

        var lca = parent;

        // Traverse from w to the LCA (stopping at the top level, where JavaScript would loop forever)
        parent = g.Parent(w);
        while (parent != lca)
        {
            if (parent is null)
            {
                throw new InvalidOperationException("the cluster path of an edge has no common ancestor");
            }

            wPath.Add(parent);
            parent = g.Parent(parent);
        }

        wPath.Reverse();
        vPath.AddRange(wPath);
        return (vPath, lca);
    }

    /// <summary>
    ///     Numbers the cluster tree in postorder.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <returns>The postorder range of every node.</returns>
    private static Dictionary<string, (int Low, int Lim)> Postorder(LayoutGraph g)
    {
        var result = new Dictionary<string, (int Low, int Lim)>(StringComparer.Ordinal);
        var lim = 0;
        foreach (var v in g.Children()!)
        {
            Visit(v);
        }

        return result;

        // The children first, then the node with the range of its subtree
        void Visit(string v)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            var low = lim;
            foreach (var child in g.Children(v)!)
            {
                Visit(child);
            }

            result[v] = (low, lim++);
        }
    }

    /// <summary>
    ///     Gets an item of a path, or null past its end (JavaScript's out-of-range read).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="index">The index.</param>
    /// <returns>The item, or null.</returns>
    private static string? At(List<string?> path, int index) => index < path.Count ? path[index] : null;

    /// <summary>
    ///     JavaScript's <c>a &lt; b</c> on numbers that may be <c>undefined</c>.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The comparison, false when either is undefined or NaN.</returns>
    private static bool Less(double? a, double? b) => a is { } x && b is { } y && x < y;

    /// <summary>
    ///     JavaScript's <c>a &lt;= b</c> on numbers that may be <c>undefined</c>.
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The comparison, false when either is undefined or NaN.</returns>
    private static bool LessOrEqual(double? a, double? b) => a is { } x && b is { } y && x <= y;
}
