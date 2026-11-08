// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The edges that are drawn (Mermaid's <c>getEdgesToRender</c>): every edge of the graph, except that the three laid
///     out segments of a self-loop become one compact loop beside its node, on the side the layout put the loop.
/// </content>
internal sealed partial class ClusterLayout
{
    /// <summary>The space between a self-loop and its label.</summary>
    private const double SelfLoopLabelGap = 4;

    /// <summary>
    ///     Gets the edges to draw: the edges of the graph in order, then one merged edge per self-loop.
    /// </summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <param name="yOffset">Half the subgraph title margin, by which the nodes were already moved down.</param>
    /// <returns>The edges with their start and end node ids.</returns>
    private static List<(ClusterLayoutEdge Edge, string Start, string End)> GetEdgesToRender(
        Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph,
        double yOffset)
    {
        var loops = new Dictionary<string, List<(ClusterLayoutEdge Edge, string Start, string End)>>(StringComparer.Ordinal);
        var loopOrder = new List<string>();
        var result = new List<(ClusterLayoutEdge Edge, string Start, string End)>();
        foreach (var e in graph.Edges())
        {
            if (graph.Edge(e) is not { } edge)
            {
                continue;
            }

            if (edge.SelfLoopId is { } key)
            {
                if (!loops.TryGetValue(key, out var segments))
                {
                    segments = [];
                    loops[key] = segments;
                    loopOrder.Add(key);
                }

                segments.Add((edge, e.V, e.W));
            }
            else
            {
                result.Add((edge, e.V, e.W));
            }
        }

        var rankDir = graph.GraphLabel?.RankDir;
        foreach (var key in loopOrder)
        {
            var segments = loops[key];
            var original = segments[0].Edge.Data;
            if (segments.Count != 3 || graph.Node(original.Start) is not { } node)
            {
                // An unexpected state keeps the segments rather than dropping the edge
                result.AddRange(segments);
                continue;
            }

            segments = [.. segments.OrderBy(s => s.Edge.SelfLoopOrder)];
            var (first, middle, last) = (segments[0].Edge, segments[1].Edge, segments[2].Edge);
            var labelWidth = middle.Width ?? 0;
            var labelHeight = middle.Height ?? 0;
            var side = SelfLoopSide(graph, node, segments, original.Start, rankDir);
            var points = SelfLoopPoints(node, side, yOffset, labelWidth);
            var label = SelfLoopLabelPosition(node, points, side, yOffset, labelWidth, labelHeight);
            var merged = new ClusterLayoutEdge(original)
            {
                Points = [.. points.Select(p => new DagrePoint(p.X, p.Y))],
                X = label.X,
                Y = label.Y,
                Width = labelWidth,
                Height = labelHeight,
                FromCluster = first.FromCluster ?? middle.FromCluster ?? last.FromCluster,
                ToCluster = first.ToCluster ?? middle.ToCluster ?? last.ToCluster,
                LabelOwner = middle,
            };
            result.Add((merged, original.Start, original.End));
        }

        return result;
    }

    /// <summary>
    ///     Gets the side of a node a self-loop is drawn on (Mermaid's <c>getSelfLoopSide</c>): toward the hidden nodes of
    ///     its layout, else toward its laid-out points, else the side the direction gives.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="node">The node.</param>
    /// <param name="segments">The three segments.</param>
    /// <param name="nodeId">The node id.</param>
    /// <param name="rankDir">The direction of the graph.</param>
    /// <returns>The side: <c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</returns>
    private static string SelfLoopSide(
        Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph,
        ClusterLayoutNode node,
        List<(ClusterLayoutEdge Edge, string Start, string End)> segments,
        string nodeId,
        string? rankDir)
    {
        var dummies = new List<string>();
        foreach (var (_, start, end) in segments)
        {
            foreach (var id in new[] { start, end }.Where(id => id != nodeId && !dummies.Contains(id)))
            {
                dummies.Add(id);
            }
        }

        var hints = dummies.Select(graph.Node).OfType<ClusterLayoutNode>().Select(d => new ShapePoint(d.X, d.Y)).ToList();
        if (hints.Count == 0)
        {
            hints = [.. segments.SelectMany(s => s.Edge.Points ?? []).Select(p => new ShapePoint(p.X, p.Y))];
        }

        if (hints.Count == 0)
        {
            return DefaultSelfLoopSide(rankDir);
        }

        // The mean of the hints, summed as Mermaid's reduce does
        var (cx, cy) = (0.0, 0.0);
        foreach (var point in hints)
        {
            cx += point.X / hints.Count;
            cy += point.Y / hints.Count;
        }

        var dx = cx - node.X;
        var dy = cy - node.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? "right" : "left";
        }

        if (Math.Abs(dy) > 0)
        {
            return dy > 0 ? "bottom" : "top";
        }

        return DefaultSelfLoopSide(rankDir);
    }

    /// <summary>
    ///     Gets the side of a self-loop that a direction gives (Mermaid's <c>getDefaultSelfLoopSide</c>).
    /// </summary>
    /// <param name="rankDir">The direction.</param>
    /// <returns>The side.</returns>
    private static string DefaultSelfLoopSide(string? rankDir) => rankDir switch
    {
        "BT" => "bottom",
        "LR" => "right",
        "RL" => "left",
        _ => "top",
    };

    /// <summary>
    ///     Gets the four points of a compact self-loop on a side of a node (Mermaid's <c>getSelfLoopPoints</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="side">The side.</param>
    /// <param name="yOffset">The offset the node was moved down by.</param>
    /// <param name="labelWidth">The width of the loop's label.</param>
    /// <returns>The points.</returns>
    private static List<ShapePoint> SelfLoopPoints(ClusterLayoutNode node, string side, double yOffset, double labelWidth)
    {
        static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
        var width = node.Width ?? 0;
        var height = node.Height ?? 0;
        var x = node.X;
        var y = node.Y - yOffset;
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var maxSpan = Math.Max(36, Math.Min(100, width * 0.8));
        var span = Clamp(Math.Max(labelWidth, width * 0.35), 36, maxSpan);
        var depth = Clamp(Math.Min(width, height) * 0.45, 24, 48);
        switch (side)
        {
            case "bottom":
                {
                    var bottom = y + halfHeight;
                    return [new(x - (span / 2), bottom), new(x - (span / 2), bottom + depth), new(x + (span / 2), bottom + depth), new(x + (span / 2), bottom)];
                }

            case "right":
                {
                    var right = x + halfWidth;
                    return [new(right, y - (span / 2)), new(right + depth, y - (span / 2)), new(right + depth, y + (span / 2)), new(right, y + (span / 2))];
                }

            case "left":
                {
                    var left = x - halfWidth;
                    return [new(left, y - (span / 2)), new(left - depth, y - (span / 2)), new(left - depth, y + (span / 2)), new(left, y + (span / 2))];
                }

            default:
                {
                    var top = y - halfHeight;
                    return [new(x - (span / 2), top), new(x - (span / 2), top - depth), new(x + (span / 2), top - depth), new(x + (span / 2), top)];
                }
        }
    }

    /// <summary>
    ///     Gets the center of a self-loop's label, beyond the loop (Mermaid's <c>getSelfLoopLabelPosition</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="points">The loop.</param>
    /// <param name="side">The side.</param>
    /// <param name="yOffset">The offset the node was moved down by.</param>
    /// <param name="labelWidth">The label width.</param>
    /// <param name="labelHeight">The label height.</param>
    /// <returns>The center.</returns>
    private static ShapePoint SelfLoopLabelPosition(
        ClusterLayoutNode node,
        List<ShapePoint> points,
        string side,
        double yOffset,
        double labelWidth,
        double labelHeight)
    {
        var x = node.X;
        var y = node.Y - yOffset;
        return side switch
        {
            "bottom" => new ShapePoint(x, points.Max(p => p.Y) + (labelHeight / 2) + SelfLoopLabelGap),
            "right" => new ShapePoint(points.Max(p => p.X) + (labelWidth / 2) + SelfLoopLabelGap, y),
            "left" => new ShapePoint(points.Min(p => p.X) - (labelWidth / 2) - SelfLoopLabelGap, y),
            _ => new ShapePoint(x, points.Min(p => p.Y) - (labelHeight / 2) - SelfLoopLabelGap),
        };
    }
}
