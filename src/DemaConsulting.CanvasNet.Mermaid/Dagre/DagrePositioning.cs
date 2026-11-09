// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's coordinate assignment (<c>position/index.js</c>): y from the ranks, x from the Brandes-Koepf method.
/// </summary>
internal static partial class DagrePositioning
{
    /// <summary>
    ///     Sets the x and y coordinates of every node (dagre's <c>position</c>).
    /// </summary>
    /// <param name="g">The ordered layout graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void Position(LayoutGraph g, LayoutWorkMeter meter)
    {
        g = DagreLayout.AsNonCompoundGraph(g);
        PositionY(g, meter);
        foreach (var (v, x) in PositionX(g, meter))
        {
            g.Node(v)!.X = x;
        }
    }

    /// <summary>
    ///     Stacks the ranks: each rank is as tall as its tallest node, separated by the rank separation, and every node is
    ///     centered vertically in its rank.
    /// </summary>
    /// <param name="g">The flat layout graph.</param>
    /// <param name="meter">The work meter.</param>
    private static void PositionY(LayoutGraph g, LayoutWorkMeter meter)
    {
        var layering = DagreLayout.BuildLayerMatrix(g);
        var rankSep = g.GraphLabel!.RankSep;
        var prevY = 0.0;
        meter.Charge(g.NodeCount + layering.Count);
        foreach (var layer in layering)
        {
            var maxHeight = Lodash.Num(Lodash.Max(layer.Select(v => (double?)g.Node(v)!.Height)));
            foreach (var v in layer)
            {
                g.Node(v)!.Y = prevY + (maxHeight / 2);
            }

            prevY += maxHeight + rankSep;
        }
    }
}
