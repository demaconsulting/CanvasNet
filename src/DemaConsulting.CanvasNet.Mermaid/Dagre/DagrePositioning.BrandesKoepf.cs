// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The Brandes-Koepf horizontal coordinate assignment (<c>position/bk.js</c>), from Brandes and Koepf's "Fast and
///     Simple Horizontal Coordinate Assignment".
/// </summary>
/// <remarks>
///     Four alignments are computed (up or down, left or right): nodes are aligned with a median neighbor into vertical
///     blocks, avoiding marked conflicts with inner segments of long edges, and the blocks are compacted as far as the
///     node and edge separations allow. The narrowest alignment is the reference the others are shifted to, and each
///     node's x is the average of the two middle values of the four (or the value of the alignment the graph's
///     <c>align</c> names).
/// </remarks>
internal static partial class DagrePositioning
{
    /// <summary>
    ///     Computes the x coordinate of every node (dagre's <c>positionX</c>).
    /// </summary>
    /// <param name="g">The flat layout graph with y coordinates.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The x coordinate of every node.</returns>
    public static Dictionary<string, double> PositionX(LayoutGraph g, LayoutWorkMeter meter)
    {
        var layering = DagreLayout.BuildLayerMatrix(g);
        var conflicts = FindType1Conflicts(g, layering, meter);
        conflicts.UnionWith(FindType2Conflicts(g, layering, meter));

        // The four alignments: up or down, then left or right
        var xss = new List<(string Name, Dictionary<string, double> Xs)>();
        foreach (var vert in new[] { 'u', 'd' })
        {
            var adjustedLayering = vert == 'u' ? layering : Enumerable.Reverse(layering).ToList();
            foreach (var horiz in new[] { 'l', 'r' })
            {
                if (horiz == 'r')
                {
                    adjustedLayering = adjustedLayering.ConvertAll(inner => Enumerable.Reverse(inner).ToList());
                }

                meter.Charge(2L * (g.NodeCount + g.EdgeCount));
                Func<string, string[]> neighborFn = vert == 'u' ? v => g.Predecessors(v)! : v => g.Successors(v)!;
                var (root, align) = VerticalAlignment(adjustedLayering, conflicts, neighborFn);
                var xs = HorizontalCompaction(g, adjustedLayering, root, align, horiz == 'r');
                if (horiz == 'r')
                {
                    xs = xs.ToDictionary(pair => pair.Key, pair => -pair.Value, StringComparer.Ordinal);
                }

                xss.Add((string.Concat(vert, horiz), xs));
            }
        }

        var smallestWidth = FindSmallestWidthAlignment(g, xss.ConvertAll(pair => pair.Xs));
        AlignCoordinates(xss, smallestWidth);
        return Balance(xss, g.GraphLabel!.Align);
    }

    /// <summary>
    ///     Marks the type-1 conflicts: edges that cross an inner segment (an edge between two dummy nodes), which keep
    ///     their alignment priority (dagre's <c>findType1Conflicts</c>).
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="layering">The ranks in order.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The conflicting node pairs.</returns>
    public static HashSet<(string, string)> FindType1Conflicts(LayoutGraph g, List<List<string>> layering, LayoutWorkMeter meter)
    {
        var conflicts = new HashSet<(string, string)>();
        for (var l = 1; l < layering.Count; l++)
        {
            var prevLayer = layering[l - 1];
            var layer = layering[l];
            meter.Charge(prevLayer.Count + layer.Count);

            // The last node in the previous layer that is incident on an inner segment
            var k0 = 0;
            var scanPos = 0;
            var prevLayerLength = prevLayer.Count;
            var lastNode = layer.Count > 0 ? layer[^1] : null;
            for (var i = 0; i < layer.Count; i++)
            {
                var v = layer[i];
                var w = FindOtherInnerSegmentNode(g, v);
                var k1 = w is not null ? g.Node(w)!.Order : prevLayerLength;
                if (w is not null || v == lastNode)
                {
                    foreach (var scanNode in layer.GetRange(scanPos, i + 1 - scanPos))
                    {
                        foreach (var u in g.Predecessors(scanNode)!)
                        {
                            meter.Charge(1);
                            var uLabel = g.Node(u)!;
                            var uPos = uLabel.Order;
                            if ((uPos < k0 || k1 < uPos) && !(uLabel.Dummy is not null && g.Node(scanNode)!.Dummy is not null))
                            {
                                AddConflict(conflicts, u, scanNode);
                            }
                        }
                    }

                    scanPos = i + 1;
                    k0 = k1;
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    ///     Marks the type-2 conflicts: inner segments that cross the border segments of a cluster (dagre's
    ///     <c>findType2Conflicts</c>).
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="layering">The ranks in order.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The conflicting node pairs.</returns>
    public static HashSet<(string, string)> FindType2Conflicts(LayoutGraph g, List<List<string>> layering, LayoutWorkMeter meter)
    {
        var conflicts = new HashSet<(string, string)>();
        for (var l = 1; l < layering.Count; l++)
        {
            var north = layering[l - 1];
            var south = layering[l];
            var prevNorthPos = -1.0;
            var nextNorthPos = double.NaN;
            var southPos = 0;
            for (var southLookahead = 0; southLookahead < south.Count; southLookahead++)
            {
                var v = south[southLookahead];
                if (g.Node(v)!.Dummy == "border")
                {
                    var predecessors = g.Predecessors(v)!;
                    if (predecessors.Length > 0)
                    {
                        nextNorthPos = g.Node(predecessors[0])!.Order;
                        Scan(southPos, southLookahead, prevNorthPos, nextNorthPos);
                        southPos = southLookahead;
                        prevNorthPos = nextNorthPos;
                    }
                }

                Scan(southPos, south.Count, nextNorthPos, north.Count);
            }

            // Mark the inner segments from the south range whose north end lies outside the borders
            void Scan(int start, int southEnd, double prevNorthBorder, double nextNorthBorder)
            {
                foreach (var i in Range(start, southEnd))
                {
                    meter.Charge(1);
                    var sv = south[i];
                    if (g.Node(sv)!.Dummy is null)
                    {
                        continue;
                    }

                    foreach (var u in g.Predecessors(sv)!)
                    {
                        var uNode = g.Node(u)!;
                        if (uNode.Dummy is not null && (uNode.Order < prevNorthBorder || uNode.Order > nextNorthBorder))
                        {
                            AddConflict(conflicts, u, sv);
                        }
                    }
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    ///     Records a conflict between two nodes, in either order.
    /// </summary>
    /// <param name="conflicts">The conflicts.</param>
    /// <param name="v">One node.</param>
    /// <param name="w">The other node.</param>
    public static void AddConflict(HashSet<(string, string)> conflicts, string v, string w) =>
        conflicts.Add(string.CompareOrdinal(v, w) > 0 ? (w, v) : (v, w));

    /// <summary>
    ///     Gets whether two nodes conflict, in either order.
    /// </summary>
    /// <param name="conflicts">The conflicts.</param>
    /// <param name="v">One node.</param>
    /// <param name="w">The other node.</param>
    /// <returns>True when they do.</returns>
    public static bool HasConflict(HashSet<(string, string)> conflicts, string v, string w) =>
        conflicts.Contains(string.CompareOrdinal(v, w) > 0 ? (w, v) : (v, w));

    /// <summary>
    ///     Aligns each node with a median neighbor into blocks, left to right, skipping conflicts and crossings (dagre's
    ///     <c>verticalAlignment</c>).
    /// </summary>
    /// <param name="layering">The ranks in sweep order.</param>
    /// <param name="conflicts">The conflicts.</param>
    /// <param name="neighborFn">The neighbors in the previous rank of the sweep.</param>
    /// <returns>The block root of every node and the next node of its block (cyclic).</returns>
    public static (Dictionary<string, string> Root, Dictionary<string, string> Align) VerticalAlignment(
        List<List<string>> layering,
        HashSet<(string, string)> conflicts,
        Func<string, string[]> neighborFn)
    {
        var root = new Dictionary<string, string>(StringComparer.Ordinal);
        var align = new Dictionary<string, string>(StringComparer.Ordinal);
        var pos = new Dictionary<string, double>(StringComparer.Ordinal);

        // Every node starts as its own block
        foreach (var layer in layering)
        {
            for (var order = 0; order < layer.Count; order++)
            {
                var v = layer[order];
                root[v] = v;
                align[v] = v;
                pos[v] = order;
            }
        }

        foreach (var layer in layering)
        {
            var prevIdx = -1.0;
            foreach (var v in layer)
            {
                var ws = neighborFn(v);
                if (ws.Length == 0)
                {
                    continue;
                }

                var sorted = Lodash.SortBy(ws, w => Position(pos, w));
                var mp = (sorted.Count - 1) / 2.0;
                for (int i = (int)Math.Floor(mp), il = (int)Math.Ceiling(mp); i <= il; ++i)
                {
                    var w = sorted[i];
                    if (align[v] == v && prevIdx < Position(pos, w) && !HasConflict(conflicts, v, w))
                    {
                        align[w] = v;
                        align[v] = root[v] = root[w];
                        prevIdx = Position(pos, w);
                    }
                }
            }
        }

        return (root, align);
    }

    /// <summary>
    ///     Places the blocks as far left as the separations allow, then moves each right toward its successors where that
    ///     is free (dagre's <c>horizontalCompaction</c>).
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="layering">The ranks in sweep order.</param>
    /// <param name="root">The block roots.</param>
    /// <param name="align">The block links.</param>
    /// <param name="reverseSep">Whether the sweep runs right to left.</param>
    /// <returns>The x coordinate of every node.</returns>
    public static Dictionary<string, double> HorizontalCompaction(
        LayoutGraph g,
        List<List<string>> layering,
        Dictionary<string, string> root,
        Dictionary<string, string> align,
        bool reverseSep)
    {
        var xs = new Dictionary<string, double>(StringComparer.Ordinal);
        var blockG = BuildBlockGraph(g, layering, root, reverseSep);
        var borderType = reverseSep ? "borderLeft" : "borderRight";

        // First pass: the least x each block may take after its predecessors
        Iterate(
            elem => xs[elem] = blockG.InEdges(elem)!.Aggregate(0.0, (acc, e) => Math.Max(acc, Value(xs, e.V) + blockG.Edge(e))),
            elem => blockG.Predecessors(elem)!);

        // Second pass: move toward the successors where the border allows
        Iterate(
            elem =>
            {
                var min = blockG.OutEdges(elem)!.Aggregate(double.PositiveInfinity, (acc, e) => Math.Min(acc, Value(xs, e.W) - blockG.Edge(e)));
                var node = g.Node(elem)!;
                if (!double.IsPositiveInfinity(min) && node.BorderType != borderType)
                {
                    xs[elem] = Math.Max(Value(xs, elem), min);
                }
            },
            elem => blockG.Successors(elem)!);

        // Every node takes the x of its block
        foreach (var v in align.Values)
        {
            xs[v] = Value(xs, root[v]);
        }

        return xs;

        // A depth-first walk with an explicit stack that sets each block after its neighbors
        void Iterate(Action<string> setXs, Func<string, string[]> nextNodes)
        {
            var stack = new List<string>(blockG.Nodes());
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (stack.Count > 0)
            {
                var elem = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                if (string.IsNullOrEmpty(elem))
                {
                    break;
                }

                if (visited.Contains(elem))
                {
                    setXs(elem);
                }
                else
                {
                    visited.Add(elem);
                    stack.Add(elem);
                    stack.AddRange(nextNodes(elem));
                }
            }
        }
    }

    /// <summary>
    ///     Picks the alignment with the least total width (dagre's <c>findSmallestWidthAlignment</c>).
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="xss">The four alignments in order.</param>
    /// <returns>The first narrowest alignment.</returns>
    public static Dictionary<string, double> FindSmallestWidthAlignment(LayoutGraph g, List<Dictionary<string, double>> xss) =>
        Lodash.MinBy(xss, xs =>
        {
            var max = double.NegativeInfinity;
            var min = double.PositiveInfinity;
            foreach (var (v, x) in xs)
            {
                var halfWidth = g.Node(v)!.Width / 2;
                max = Math.Max(x + halfWidth, max);
                min = Math.Min(x - halfWidth, min);
            }

            return max - min;
        }, out _)!;

    /// <summary>
    ///     Shifts the left alignments to the left edge of the reference and the right ones to its right edge (dagre's
    ///     <c>alignCoordinates</c>).
    /// </summary>
    /// <param name="xss">The four alignments; replaced when shifted.</param>
    /// <param name="alignTo">The reference alignment.</param>
    public static void AlignCoordinates(List<(string Name, Dictionary<string, double> Xs)> xss, Dictionary<string, double>? alignTo)
    {
        var alignToValues = alignTo?.Values.Select(x => (double?)x).ToList() ?? [];
        var alignToMin = Lodash.Num(Lodash.Min(alignToValues));
        var alignToMax = Lodash.Num(Lodash.Max(alignToValues));
        for (var i = 0; i < xss.Count; i++)
        {
            var (name, xs) = xss[i];
            if (ReferenceEquals(xs, alignTo))
            {
                continue;
            }

            var xsValues = xs.Values.Select(x => (double?)x).ToList();
            var delta = name[1] == 'l'
                ? alignToMin - Lodash.Num(Lodash.Min(xsValues))
                : alignToMax - Lodash.Num(Lodash.Max(xsValues));
            if (DagreLayout.Truthy(delta))
            {
                xss[i] = (name, xs.ToDictionary(pair => pair.Key, pair => pair.Value + delta, StringComparer.Ordinal));
            }
        }
    }

    /// <summary>
    ///     Combines the four alignments: the average of the two middle values, or the alignment the graph names (dagre's
    ///     <c>balance</c>).
    /// </summary>
    /// <param name="xss">The four alignments in the order ul, ur, dl, dr.</param>
    /// <param name="align">The alignment name (<c>UL</c>, <c>UR</c>, <c>DL</c>, <c>DR</c>), or null.</param>
    /// <returns>The x coordinate of every node.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the alignment name is not one of the four.</exception>
    public static Dictionary<string, double> Balance(List<(string Name, Dictionary<string, double> Xs)> xss, string? align)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        Dictionary<string, double>? chosen = null;
        if (!string.IsNullOrEmpty(align))
        {
            var name = align.ToLowerInvariant();
            chosen = xss.Find(pair => pair.Name == name).Xs
                ?? throw new InvalidOperationException($"unknown alignment '{align}'");
        }

        foreach (var v in xss[0].Xs.Keys)
        {
            if (chosen is not null)
            {
                result[v] = Value(chosen, v);
            }
            else
            {
                var xs = Lodash.SortBy(xss.Select(pair => pair.Xs.TryGetValue(v, out var x) ? (double?)x : null), x => x);
                result[v] = (Lodash.Num(xs[1]) + Lodash.Num(xs[2])) / 2;
            }
        }

        return result;
    }

    /// <summary>
    ///     Builds the block graph: an edge from each block to the next block in some rank, weighted by the least
    ///     separation of their nodes (dagre's <c>buildBlockGraph</c>).
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="layering">The ranks in sweep order.</param>
    /// <param name="root">The block roots.</param>
    /// <param name="reverseSep">Whether the sweep runs right to left.</param>
    /// <returns>The block graph.</returns>
    private static Graph<object, object, double> BuildBlockGraph(LayoutGraph g, List<List<string>> layering, Dictionary<string, string> root, bool reverseSep)
    {
        var blockGraph = new Graph<object, object, double>();
        var graphLabel = g.GraphLabel!;
        foreach (var layer in layering)
        {
            string? u = null;
            foreach (var v in layer)
            {
                var vRoot = root[v];
                blockGraph.SetNode(vRoot);
                if (!string.IsNullOrEmpty(u))
                {
                    var uRoot = root[u];
                    var prevMax = blockGraph.Edge(uRoot, vRoot);
                    blockGraph.SetEdge(uRoot, vRoot, Math.Max(Sep(g, graphLabel.NodeSep, graphLabel.EdgeSep, reverseSep, v, u), DagreLayout.Truthy(prevMax) ? prevMax : 0));
                }

                u = v;
            }
        }

        return blockGraph;
    }

    /// <summary>
    ///     Gets the least distance between the centers of two neighbors in a rank (dagre's <c>sep</c>).
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="nodeSep">The node separation.</param>
    /// <param name="edgeSep">The edge separation.</param>
    /// <param name="reverseSep">Whether the sweep runs right to left.</param>
    /// <param name="v">The node.</param>
    /// <param name="w">Its neighbor before it.</param>
    /// <returns>The separation.</returns>
    private static double Sep(LayoutGraph g, double nodeSep, double edgeSep, bool reverseSep, string v, string w)
    {
        var vLabel = g.Node(v)!;
        var wLabel = g.Node(w)!;
        var sum = 0.0;
        var delta = 0.0;

        // Half of the node, shifted for a label at its side
        sum += vLabel.Width / 2;
        if (vLabel.LabelPos is not null)
        {
            switch (vLabel.LabelPos.ToLowerInvariant())
            {
                case "l":
                    delta = -vLabel.Width / 2;
                    break;
                case "r":
                    delta = vLabel.Width / 2;
                    break;
            }
        }

        if (DagreLayout.Truthy(delta))
        {
            sum += reverseSep ? delta : -delta;
        }

        delta = 0;

        // Half of the separation of each
        sum += (vLabel.Dummy is not null ? edgeSep : nodeSep) / 2;
        sum += (wLabel.Dummy is not null ? edgeSep : nodeSep) / 2;

        // Half of the neighbor, shifted for a label at its side
        sum += wLabel.Width / 2;
        if (wLabel.LabelPos is not null)
        {
            switch (wLabel.LabelPos.ToLowerInvariant())
            {
                case "l":
                    delta = wLabel.Width / 2;
                    break;
                case "r":
                    delta = -wLabel.Width / 2;
                    break;
            }
        }

        if (DagreLayout.Truthy(delta))
        {
            sum += reverseSep ? delta : -delta;
        }

        return sum;
    }

    /// <summary>
    ///     Finds the other end of an inner segment: a dummy predecessor of a dummy node.
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="v">The node.</param>
    /// <returns>The predecessor, or null.</returns>
    private static string? FindOtherInnerSegmentNode(LayoutGraph g, string v) =>
        g.Node(v)!.Dummy is not null ? Array.Find(g.Predecessors(v)!, u => g.Node(u)!.Dummy is not null) : null;

    /// <summary>
    ///     Gets the position of a node in its rank, NaN when unknown.
    /// </summary>
    /// <param name="pos">The positions.</param>
    /// <param name="w">The node.</param>
    /// <returns>The position.</returns>
    private static double Position(Dictionary<string, double> pos, string w) => pos.TryGetValue(w, out var p) ? p : double.NaN;

    /// <summary>
    ///     Gets a coordinate, NaN when unset (JavaScript's <c>undefined</c> in arithmetic).
    /// </summary>
    /// <param name="xs">The coordinates.</param>
    /// <param name="v">The node.</param>
    /// <returns>The coordinate.</returns>
    private static double Value(Dictionary<string, double> xs, string v) => xs.TryGetValue(v, out var x) ? x : double.NaN;

    /// <summary>
    ///     lodash's <c>_.range(start, end)</c>: ascending, or descending when the end is below the start.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end (exclusive).</param>
    /// <returns>The numbers.</returns>
    private static IEnumerable<int> Range(int start, int end)
    {
        var step = start < end ? 1 : -1;
        for (var i = start; step > 0 ? i < end : i > end; i += step)
        {
            yield return i;
        }
    }
}
