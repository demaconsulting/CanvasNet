// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayerGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The sort of one layer (<c>order/sort-subgraph.js</c>, <c>barycenter.js</c>, <c>resolve-conflicts.js</c> and
///     <c>sort.js</c>).
/// </summary>
internal static partial class DagreOrdering
{
    /// <summary>
    ///     Sorts the members of a cluster (or of the layer root) by barycenter, with each sub-cluster sorted first and
    ///     kept contiguous, and the cluster's border nodes at the ends (dagre's <c>sortSubgraph</c>).
    /// </summary>
    /// <param name="g">The layer graph.</param>
    /// <param name="v">The cluster, or the layer root.</param>
    /// <param name="cg">The constraint graph between sibling clusters.</param>
    /// <param name="biasRight">Whether ties go to the right.</param>
    /// <returns>The sorted nodes and their barycenter.</returns>
    public static SortResult SortSubgraph(LayerGraph g, string v, Graph<object, object, object> cg, bool biasRight)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var movable = g.Children(v) ?? [];
        var node = g.Node(v);
        var bl = node?.BorderLeft;
        var br = node?.BorderRight;
        var subgraphs = new Dictionary<string, SortResult>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(bl))
        {
            movable = Array.FindAll(movable, w => w != bl && w != br);
        }

        // Sort the sub-clusters first and fold their barycenters in
        var barycenters = Barycenters(g, movable);
        foreach (var entry in barycenters)
        {
            if (g.Children(entry.V)!.Length > 0)
            {
                var subgraphResult = SortSubgraph(g, entry.V, cg, biasRight);
                subgraphs[entry.V] = subgraphResult;
                if (subgraphResult.Barycenter is not null)
                {
                    MergeBarycenters(entry, subgraphResult);
                }
            }
        }

        var entries = ResolveConflicts(barycenters, cg);
        ExpandSubgraphs(entries, subgraphs);
        var result = Sort(entries, biasRight);

        // The borders go at the ends, and pull the barycenter toward the borders of the fixed rank
        if (!string.IsNullOrEmpty(bl))
        {
            result.Vs = [bl, .. result.Vs, br!];
            var blPreds = g.Predecessors(bl)!;
            if (blPreds.Length > 0)
            {
                var blPred = g.Node(blPreds[0])!.Node!;
                var brPred = g.Node(g.Predecessors(br!)![0])!.Node!;
                if (result.Barycenter is null)
                {
                    result.Barycenter = 0;
                    result.Weight = 0;
                }

                result.Barycenter = ((result.Barycenter.Value * Lodash.Num(result.Weight)) + blPred.Order + brPred.Order) /
                                    (Lodash.Num(result.Weight) + 2);
                result.Weight = Lodash.Num(result.Weight) + 2;
            }
        }

        return result;
    }

    /// <summary>
    ///     Computes the weighted average order of the predecessors of each node (dagre's <c>barycenter</c>).
    /// </summary>
    /// <param name="g">The layer graph.</param>
    /// <param name="movable">The nodes.</param>
    /// <returns>An entry per node, without a barycenter for a node with no in-edges.</returns>
    public static List<BarycenterEntry> Barycenters(LayerGraph g, IEnumerable<string> movable)
    {
        var result = new List<BarycenterEntry>();
        foreach (var v in movable)
        {
            var inV = g.InEdges(v)!;
            if (inV.Length == 0)
            {
                result.Add(new BarycenterEntry(v));
                continue;
            }

            var sum = 0.0;
            var weight = 0.0;
            foreach (var e in inV)
            {
                var edge = g.Edge(e)!;
                var nodeU = g.Node(e.V)!.Node!;
                sum += edge.Weight * nodeU.Order;
                weight += edge.Weight;
            }

            result.Add(new BarycenterEntry(v) { Barycenter = sum / weight, Weight = weight });
        }

        return result;
    }

    /// <summary>
    ///     Merges entries whose barycenters violate a constraint, so that the constraint holds after sorting (dagre's
    ///     <c>resolveConflicts</c>, from Forster's "A Fast and Simple Heuristic for Constrained Two-Level Crossing
    ///     Reduction").
    /// </summary>
    /// <param name="entries">The barycenter entries in order.</param>
    /// <param name="cg">The constraint graph.</param>
    /// <returns>The resolved entries, each with its nodes, first index and barycenter.</returns>
    public static List<ConflictEntry> ResolveConflicts(List<BarycenterEntry> entries, Graph<object, object, object> cg)
    {
        // The entries by node, in JavaScript key order
        var mappedEntries = new ObjectKeyMap<ConflictEntry>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var tmp = new ConflictEntry([entry.V], i);
            if (entry.Barycenter is not null)
            {
                tmp.Barycenter = entry.Barycenter;
                tmp.Weight = entry.Weight;
            }

            mappedEntries[entry.V] = tmp;
        }

        foreach (var e in cg.Edges())
        {
            if (mappedEntries.TryGetValue(e.V, out var entryV) && mappedEntries.TryGetValue(e.W, out var entryW))
            {
                entryW.Indegree++;
                entryV.Out.Add(entryW);
            }
        }

        var sourceSet = Array.FindAll(mappedEntries.Values(), entry => entry.Indegree == 0).ToList();
        return DoResolveConflicts(sourceSet);
    }

    /// <summary>
    ///     Sorts entries by barycenter, placing the entries without one at their original index (dagre's <c>sort</c>).
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="biasRight">Whether ties go to the right.</param>
    /// <returns>The nodes in order, with the overall barycenter when any entry has weight.</returns>
    public static SortResult Sort(List<ConflictEntry> entries, bool biasRight)
    {
        var sortable = entries.FindAll(entry => entry.Barycenter is not null);
        var unsortable = Lodash.SortBy(entries.FindAll(entry => entry.Barycenter is null), entry => -entry.I);
        var vs = new List<string>();
        var sum = 0.0;
        var weight = 0.0;

        Lodash.Sort(sortable, (entryV, entryW) =>
        {
            if (entryV.Barycenter < entryW.Barycenter)
            {
                return -1;
            }

            if (entryV.Barycenter > entryW.Barycenter)
            {
                return 1;
            }

            return !biasRight ? entryV.I - entryW.I : entryW.I - entryV.I;
        });

        // Interleave the unsortable entries at their indexes
        var vsIndex = ConsumeUnsortable(vs, unsortable, 0);
        foreach (var entry in sortable)
        {
            vsIndex += entry.Vs.Count;
            vs.AddRange(entry.Vs);
            sum += entry.Barycenter!.Value * Lodash.Num(entry.Weight);
            weight += Lodash.Num(entry.Weight);
            vsIndex = ConsumeUnsortable(vs, unsortable, vsIndex);
        }

        var result = new SortResult(vs);
        if (DagreLayout.Truthy(weight))
        {
            result.Barycenter = sum / weight;
            result.Weight = weight;
        }

        return result;
    }

    /// <summary>
    ///     Takes the entries off a constraint-ordered source set, merging each into the next when their barycenters are
    ///     out of order.
    /// </summary>
    /// <param name="sourceSet">The entries without unresolved predecessors.</param>
    /// <returns>The entries that were not merged away.</returns>
    private static List<ConflictEntry> DoResolveConflicts(List<ConflictEntry> sourceSet)
    {
        var entries = new List<ConflictEntry>();
        while (sourceSet.Count > 0)
        {
            var entry = sourceSet[^1];
            sourceSet.RemoveAt(sourceSet.Count - 1);
            entries.Add(entry);

            // Merge a predecessor whose barycenter is not less
            entry.In.Reverse();
            foreach (var uEntry in entry.In)
            {
                if (uEntry.Merged)
                {
                    continue;
                }

                if (uEntry.Barycenter is null || entry.Barycenter is null || uEntry.Barycenter >= entry.Barycenter)
                {
                    MergeEntries(entry, uEntry);
                }
            }

            // Release the successors
            foreach (var wEntry in entry.Out)
            {
                wEntry.In.Add(entry);
                if (--wEntry.Indegree == 0)
                {
                    sourceSet.Add(wEntry);
                }
            }
        }

        return entries.FindAll(entry => !entry.Merged);
    }

    /// <summary>
    ///     Merges one entry into another: the nodes of the source go first, and the barycenter is the weighted average.
    /// </summary>
    /// <param name="target">The entry that remains.</param>
    /// <param name="source">The entry merged away.</param>
    private static void MergeEntries(ConflictEntry target, ConflictEntry source)
    {
        var sum = 0.0;
        var weight = 0.0;
        if (DagreLayout.Truthy(target.Weight))
        {
            sum += Lodash.Num(target.Barycenter) * target.Weight!.Value;
            weight += target.Weight.Value;
        }

        if (DagreLayout.Truthy(source.Weight))
        {
            sum += Lodash.Num(source.Barycenter) * source.Weight!.Value;
            weight += source.Weight.Value;
        }

        target.Vs = [.. source.Vs, .. target.Vs];
        target.Barycenter = sum / weight;
        target.Weight = weight;
        target.I = Math.Min(source.I, target.I);
        source.Merged = true;
    }

    /// <summary>
    ///     Replaces each sub-cluster in the entries with its sorted nodes.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="subgraphs">The sort results of the sub-clusters.</param>
    private static void ExpandSubgraphs(List<ConflictEntry> entries, Dictionary<string, SortResult> subgraphs)
    {
        foreach (var entry in entries)
        {
            var vs = new List<string>();
            foreach (var v in entry.Vs)
            {
                if (subgraphs.TryGetValue(v, out var subgraph))
                {
                    vs.AddRange(subgraph.Vs);
                }
                else
                {
                    vs.Add(v);
                }
            }

            entry.Vs = vs;
        }
    }

    /// <summary>
    ///     Folds the barycenter of a sorted sub-cluster into its entry.
    /// </summary>
    /// <param name="target">The entry of the sub-cluster.</param>
    /// <param name="other">The sort result of the sub-cluster.</param>
    private static void MergeBarycenters(BarycenterEntry target, SortResult other)
    {
        if (target.Barycenter is { } barycenter)
        {
            var targetWeight = Lodash.Num(target.Weight);
            var otherWeight = Lodash.Num(other.Weight);
            target.Barycenter = ((barycenter * targetWeight) + (other.Barycenter!.Value * otherWeight)) / (targetWeight + otherWeight);
            target.Weight = targetWeight + otherWeight;
        }
        else
        {
            target.Barycenter = other.Barycenter;
            target.Weight = other.Weight;
        }
    }

    /// <summary>
    ///     Appends the unsortable entries whose index has been reached.
    /// </summary>
    /// <param name="vs">The nodes so far.</param>
    /// <param name="unsortable">The unsortable entries, the least index last.</param>
    /// <param name="index">The index reached.</param>
    /// <returns>The new index.</returns>
    private static int ConsumeUnsortable(List<string> vs, List<ConflictEntry> unsortable, int index)
    {
        while (unsortable.Count > 0 && unsortable[^1].I <= index)
        {
            var last = unsortable[^1];
            unsortable.RemoveAt(unsortable.Count - 1);
            vs.AddRange(last.Vs);
            index++;
        }

        return index;
    }

    /// <summary>
    ///     The barycenter of one node or cluster of a layer.
    /// </summary>
    /// <param name="v">The node.</param>
    internal sealed class BarycenterEntry(string v)
    {
        /// <summary>Gets the node.</summary>
        public string V { get; } = v;

        /// <summary>Gets or sets the barycenter, or null when the node has no in-edges.</summary>
        public double? Barycenter { get; set; }

        /// <summary>Gets or sets the total in-edge weight, or null.</summary>
        public double? Weight { get; set; }
    }

    /// <summary>
    ///     An entry of the conflict resolution: one or more nodes that stay together.
    /// </summary>
    /// <param name="vs">The nodes.</param>
    /// <param name="i">The original index.</param>
    internal sealed class ConflictEntry(List<string> vs, int i)
    {
        /// <summary>Gets or sets the nodes.</summary>
        public List<string> Vs { get; set; } = vs;

        /// <summary>Gets or sets the least original index.</summary>
        public int I { get; set; } = i;

        /// <summary>Gets or sets the barycenter; null when absent, possibly NaN when present.</summary>
        public double? Barycenter { get; set; }

        /// <summary>Gets or sets the weight; null when absent.</summary>
        public double? Weight { get; set; }

        /// <summary>Gets or sets the number of unresolved constraint predecessors.</summary>
        public int Indegree { get; set; }

        /// <summary>Gets the resolved constraint predecessors.</summary>
        public List<ConflictEntry> In { get; } = [];

        /// <summary>Gets the constraint successors.</summary>
        public List<ConflictEntry> Out { get; } = [];

        /// <summary>Gets or sets whether the entry was merged into another.</summary>
        public bool Merged { get; set; }
    }

    /// <summary>
    ///     The result of sorting a layer or cluster.
    /// </summary>
    /// <param name="vs">The nodes in order.</param>
    internal sealed class SortResult(List<string> vs)
    {
        /// <summary>Gets or sets the nodes in order.</summary>
        public List<string> Vs { get; set; } = vs;

        /// <summary>Gets or sets the barycenter, or null.</summary>
        public double? Barycenter { get; set; }

        /// <summary>Gets or sets the weight, or null.</summary>
        public double? Weight { get; set; }
    }
}
