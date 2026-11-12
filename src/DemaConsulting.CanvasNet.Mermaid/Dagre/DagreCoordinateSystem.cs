// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's coordinate system changes (<c>coordinate-system.js</c>): the layout always runs top to bottom, so for
///     <c>LR</c> and <c>RL</c> the sizes are swapped before positioning, and afterwards <c>BT</c> and <c>RL</c> are
///     mirrored vertically and <c>LR</c> and <c>RL</c> transposed.
/// </summary>
internal static class DagreCoordinateSystem
{
    /// <summary>
    ///     Swaps widths and heights for a horizontal rank direction (dagre's <c>coordinateSystem.adjust</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    public static void Adjust(LayoutGraph g)
    {
        var rankDir = RankDir(g);
        if (rankDir is "lr" or "rl")
        {
            SwapWidthHeight(g);
        }
    }

    /// <summary>
    ///     Mirrors and transposes the positioned graph for the rank direction (dagre's <c>coordinateSystem.undo</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    public static void Undo(LayoutGraph g)
    {
        var rankDir = RankDir(g);
        if (rankDir is "bt" or "rl")
        {
            ReverseY(g);
        }

        if (rankDir is "lr" or "rl")
        {
            SwapXY(g);
            SwapWidthHeight(g);
        }
    }

    /// <summary>
    ///     Gets the rank direction in lower case.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <returns>The direction.</returns>
    private static string RankDir(LayoutGraph g) => (g.GraphLabel!.RankDir ?? string.Empty).ToLowerInvariant();

    /// <summary>
    ///     Swaps the width and height of every node and edge label.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void SwapWidthHeight(LayoutGraph g)
    {
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            (node.Width, node.Height) = (node.Height, node.Width);
        }

        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            (edge.Width, edge.Height) = (edge.Height, edge.Width);
        }
    }

    /// <summary>
    ///     Negates every y coordinate.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void ReverseY(LayoutGraph g)
    {
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            node.Y = -node.Y;
        }

        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            foreach (var point in edge.Points ?? [])
            {
                point.Y = -point.Y;
            }

            if (edge.Y is { } y)
            {
                edge.Y = -y;
            }
        }
    }

    /// <summary>
    ///     Swaps the x and y coordinates of every node, point and edge label.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void SwapXY(LayoutGraph g)
    {
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            (node.X, node.Y) = (node.Y, node.X);
        }

        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            foreach (var point in edge.Points ?? [])
            {
                (point.X, point.Y) = (point.Y, point.X);
            }

            if (edge.X is not null)
            {
                (edge.X, edge.Y) = (edge.Y, edge.X);
            }
        }
    }
}
