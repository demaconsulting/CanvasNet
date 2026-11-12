// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's cycle breaking (<c>acyclic.js</c>, <c>greedy-fas.js</c> and <c>data/list.js</c>): reverses a feedback
///     arc set so that the graph can be ranked, and restores the edges after the layout.
/// </summary>
/// <remarks>
///     The default breaker reverses the edges that a depth-first search finds going back to a node on the stack; with
///     <c>acyclicer: greedy</c> the Eades-Lin-Smyth greedy heuristic picks the set. A reversed edge gets a
///     <c>rev</c>-prefixed name and remembers its original name.
/// </remarks>
internal static class DagreAcyclic
{
    /// <summary>
    ///     Reverses a feedback arc set (dagre's <c>acyclic.run</c>).
    /// </summary>
    /// <param name="g">The layout graph, without self edges.</param>
    /// <param name="meter">The work meter.</param>
    public static void Run(LayoutGraph g, LayoutWorkMeter meter)
    {
        var fas = g.GraphLabel!.Acyclicer == "greedy"
            ? GreedyFas(g, e => g.Edge(e)!.Weight, meter)
            : DfsFas(g, meter);
        foreach (var e in fas)
        {
            var label = g.Edge(e)!;
            g.RemoveEdge(e);
            label.ForwardName = e.Name;
            label.Reversed = true;
            g.SetEdge(e.W, e.V, label, meter.UniqueId("rev"));
        }
    }

    /// <summary>
    ///     Restores the reversed edges (dagre's <c>acyclic.undo</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    public static void Undo(LayoutGraph g)
    {
        foreach (var e in g.Edges())
        {
            var label = g.Edge(e)!;
            if (label.Reversed)
            {
                g.RemoveEdge(e);
                var forwardName = label.ForwardName;
                label.Reversed = false;
                label.ForwardName = null;
                g.SetEdge(e.W, e.V, label, forwardName);
            }
        }
    }

    /// <summary>
    ///     Finds the edges that a depth-first search sees going back to a node on its stack.
    /// </summary>
    /// <param name="g">The graph.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The back edges.</returns>
    public static List<GraphEdge> DfsFas(LayoutGraph g, LayoutWorkMeter meter)
    {
        var fas = new List<GraphEdge>();
        var stack = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var nodes = g.Nodes();
        meter.Charge(nodes.Length + g.EdgeCount);
        foreach (var v in nodes)
        {
            Visit(v);
        }

        return fas;

        // Mark the node on the stack while its out-edges are followed
        void Visit(string v)
        {
            if (!visited.Add(v))
            {
                return;
            }

            RuntimeHelpers.EnsureSufficientExecutionStack();
            stack.Add(v);
            foreach (var e in g.OutEdges(v)!)
            {
                if (stack.Contains(e.W))
                {
                    fas.Add(e);
                }
                else
                {
                    Visit(e.W);
                }
            }

            stack.Remove(v);
        }
    }

    /// <summary>
    ///     Finds a feedback arc set with the greedy heuristic of Eades, Lin and Smyth (dagre's <c>greedyFAS</c>).
    /// </summary>
    /// <param name="g">The graph.</param>
    /// <param name="weightOf">The weight of an edge.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The edges to reverse, every parallel edge included.</returns>
    public static List<GraphEdge> GreedyFas(LayoutGraph g, Func<GraphEdge, double> weightOf, LayoutWorkMeter meter)
    {
        if (g.NodeCount <= 1)
        {
            return [];
        }

        meter.Charge(2L * (g.NodeCount + g.EdgeCount));
        var (fasGraph, buckets, zeroIdx) = BuildState(g, weightOf);
        var results = DoGreedyFas(fasGraph, buckets, zeroIdx);

        // Expand multi-edges
        return [.. results.SelectMany(e => g.OutEdges(e.V, e.W)!)];
    }

    /// <summary>
    ///     Removes sinks and sources, and otherwise the node with the greatest out-minus-in weight, collecting the
    ///     in-edges of the latter.
    /// </summary>
    /// <param name="g">The weighted state graph; emptied.</param>
    /// <param name="buckets">The buckets by out-minus-in weight.</param>
    /// <param name="zeroIdx">The bucket of a zero difference.</param>
    /// <returns>The collected edges.</returns>
    private static List<GraphEdge> DoGreedyFas(Graph<object, FasEntry, double> g, FasList[] buckets, int zeroIdx)
    {
        var results = new List<GraphEdge>();
        var sources = buckets[^1];
        var sinks = buckets[0];
        while (g.NodeCount > 0)
        {
            FasEntry? entry;
            while ((entry = sinks.Dequeue()) is not null)
            {
                RemoveNode(g, buckets, zeroIdx, entry, null);
            }

            while ((entry = sources.Dequeue()) is not null)
            {
                RemoveNode(g, buckets, zeroIdx, entry, null);
            }

            if (g.NodeCount > 0)
            {
                for (var i = buckets.Length - 2; i > 0; --i)
                {
                    entry = buckets[i].Dequeue();
                    if (entry is not null)
                    {
                        RemoveNode(g, buckets, zeroIdx, entry, results);
                        break;
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    ///     Removes a node from the state graph, moving its neighbors to their new buckets.
    /// </summary>
    /// <param name="g">The state graph.</param>
    /// <param name="buckets">The buckets.</param>
    /// <param name="zeroIdx">The bucket of a zero difference.</param>
    /// <param name="entry">The node entry.</param>
    /// <param name="results">The list that collects the in-edges, or null.</param>
    private static void RemoveNode(Graph<object, FasEntry, double> g, FasList[] buckets, int zeroIdx, FasEntry entry, List<GraphEdge>? results)
    {
        foreach (var edge in g.InEdges(entry.V)!)
        {
            var weight = g.Edge(edge);
            var uEntry = g.Node(edge.V)!;
            results?.Add(new GraphEdge(edge.V, edge.W));
            uEntry.Out -= weight;
            AssignBucket(buckets, zeroIdx, uEntry);
        }

        foreach (var edge in g.OutEdges(entry.V)!)
        {
            var weight = g.Edge(edge);
            var wEntry = g.Node(edge.W)!;
            wEntry.In -= weight;
            AssignBucket(buckets, zeroIdx, wEntry);
        }

        g.RemoveNode(entry.V);
    }

    /// <summary>
    ///     Builds the weighted state graph (parallel edges summed) and the buckets.
    /// </summary>
    /// <param name="g">The graph.</param>
    /// <param name="weightOf">The weight of an edge.</param>
    /// <returns>The state graph, the buckets and the bucket of a zero difference.</returns>
    private static (Graph<object, FasEntry, double> Graph, FasList[] Buckets, int ZeroIdx) BuildState(LayoutGraph g, Func<GraphEdge, double> weightOf)
    {
        var fasGraph = new Graph<object, FasEntry, double>();
        var maxIn = 0.0;
        var maxOut = 0.0;
        foreach (var v in g.Nodes())
        {
            fasGraph.SetNode(v, new FasEntry(v));
        }

        // Aggregate weights on nodes, and sum the weights across multi-edges into a single edge
        foreach (var e in g.Edges())
        {
            var prevWeight = fasGraph.Edge(e.V, e.W);
            var weight = weightOf(e);
            fasGraph.SetEdge(e.V, e.W, prevWeight + weight);
            var vEntry = fasGraph.Node(e.V)!;
            vEntry.Out += weight;
            maxOut = Math.Max(maxOut, vEntry.Out);
            var wEntry = fasGraph.Node(e.W)!;
            wEntry.In += weight;
            maxIn = Math.Max(maxIn, wEntry.In);
        }

        var buckets = new FasList[(int)Math.Ceiling(maxOut + maxIn + 3)];
        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = new FasList();
        }

        var zeroIdx = (int)maxIn + 1;
        foreach (var v in fasGraph.Nodes())
        {
            AssignBucket(buckets, zeroIdx, fasGraph.Node(v)!);
        }

        return (fasGraph, buckets, zeroIdx);
    }

    /// <summary>
    ///     Puts an entry in the bucket of its out-minus-in weight: sinks first, sources last.
    /// </summary>
    /// <param name="buckets">The buckets.</param>
    /// <param name="zeroIdx">The bucket of a zero difference.</param>
    /// <param name="entry">The entry.</param>
    private static void AssignBucket(FasList[] buckets, int zeroIdx, FasEntry entry)
    {
        if (!DagreLayout.Truthy(entry.Out))
        {
            buckets[0].Enqueue(entry);
        }
        else if (!DagreLayout.Truthy(entry.In))
        {
            buckets[^1].Enqueue(entry);
        }
        else
        {
            buckets[(int)(entry.Out - entry.In) + zeroIdx].Enqueue(entry);
        }
    }

    /// <summary>
    ///     The state of one node of the greedy heuristic, linked into one bucket at a time.
    /// </summary>
    /// <param name="v">The node name.</param>
    private sealed class FasEntry(string v)
    {
        /// <summary>Gets the node name.</summary>
        public string V { get; } = v;

        /// <summary>Gets or sets the remaining in-weight.</summary>
        public double In { get; set; }

        /// <summary>Gets or sets the remaining out-weight.</summary>
        public double Out { get; set; }

        /// <summary>Gets or sets the previous entry of the bucket list, or null when unlinked.</summary>
        public FasEntry? Prev { get; set; }

        /// <summary>Gets or sets the next entry of the bucket list, or null when unlinked.</summary>
        public FasEntry? Next { get; set; }
    }

    /// <summary>
    ///     dagre's circular list with a sentinel: entries are added at the front and taken from the back.
    /// </summary>
    private sealed class FasList
    {
        /// <summary>The sentinel.</summary>
        private readonly FasEntry _sentinel = new(string.Empty);

        /// <summary>
        ///     Initializes an empty list.
        /// </summary>
        public FasList()
        {
            _sentinel.Next = _sentinel;
            _sentinel.Prev = _sentinel;
        }

        /// <summary>
        ///     Takes the entry at the back.
        /// </summary>
        /// <returns>The entry, or null when empty.</returns>
        public FasEntry? Dequeue()
        {
            var entry = _sentinel.Prev!;
            if (entry == _sentinel)
            {
                return null;
            }

            Unlink(entry);
            return entry;
        }

        /// <summary>
        ///     Adds an entry at the front, taking it out of its current list first.
        /// </summary>
        /// <param name="entry">The entry.</param>
        public void Enqueue(FasEntry entry)
        {
            if (entry.Prev is not null && entry.Next is not null)
            {
                Unlink(entry);
            }

            entry.Next = _sentinel.Next;
            _sentinel.Next!.Prev = entry;
            _sentinel.Next = entry;
            entry.Prev = _sentinel;
        }

        /// <summary>
        ///     Takes an entry out of its list.
        /// </summary>
        /// <param name="entry">The entry.</param>
        private static void Unlink(FasEntry entry)
        {
            entry.Prev!.Next = entry.Next;
            entry.Next!.Prev = entry.Prev;
            entry.Next = null;
            entry.Prev = null;
        }
    }
}
