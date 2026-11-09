// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's edge normalization (<c>normalize.js</c>): splits every edge that spans more than one rank into a chain of
///     unit edges through dummy nodes, one of which carries the edge label at the label rank, and turns the chain back
///     into the edge's route after positioning.
/// </summary>
internal static class DagreNormalizer
{
    /// <summary>
    ///     Splits the long edges (dagre's <c>normalize.run</c>); the first dummy of each chain is recorded in
    ///     <see cref="LayoutGraphLabel.DummyChains"/>.
    /// </summary>
    /// <param name="g">The ranked layout graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void Run(LayoutGraph g, LayoutWorkMeter meter)
    {
        g.GraphLabel!.DummyChains = [];
        foreach (var e in g.Edges())
        {
            meter.Charge(1);
            NormalizeEdge(g, meter, e);
        }
    }

    /// <summary>
    ///     Replaces each chain with its edge, recording the dummy positions as the route and the label dummy's position
    ///     and size as the label's (dagre's <c>normalize.undo</c>).
    /// </summary>
    /// <param name="g">The positioned layout graph.</param>
    /// <param name="meter">The work meter.</param>
    public static void Undo(LayoutGraph g, LayoutWorkMeter meter)
    {
        foreach (var start in g.GraphLabel!.DummyChains)
        {
            var v = start;
            var node = g.Node(v)!;
            var origLabel = node.EdgeLabel!;
            g.SetEdge(node.EdgeObj!, origLabel);
            while (node?.Dummy is not null)
            {
                meter.Charge(1);
                var w = g.Successors(v)![0];
                g.RemoveNode(v);
                origLabel.Points!.Add(new DagrePoint(node.X, node.Y));
                if (node.Dummy == "edge-label")
                {
                    origLabel.X = node.X;
                    origLabel.Y = node.Y;
                    origLabel.Width = node.Width;
                    origLabel.Height = node.Height;
                }

                v = w;
                node = g.Node(v);
            }
        }
    }

    /// <summary>
    ///     Splits one edge into unit edges through dummy nodes.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The work meter.</param>
    /// <param name="e">The edge.</param>
    private static void NormalizeEdge(LayoutGraph g, LayoutWorkMeter meter, Graphlib.GraphEdge e)
    {
        var v = e.V;
        var vRank = Lodash.Num(g.Node(v)!.Rank);
        var w = e.W;
        var wRank = Lodash.Num(g.Node(w)!.Rank);
        var name = e.Name;
        var edgeLabel = g.Edge(e)!;
        var labelRank = edgeLabel.LabelRank;
        if (DagreLayout.Same(wRank, vRank + 1))
        {
            return;
        }

        g.RemoveEdge(e);
        var i = 0;
        for (++vRank; vRank < wRank; ++i, ++vRank)
        {
            meter.Charge(1);
            edgeLabel.Points = [];
            var attrs = new LayoutNode { EdgeLabel = edgeLabel, EdgeObj = e, Rank = vRank };
            var dummy = DagreLayout.AddDummyNode(g, meter, "edge", attrs, "_d");
            if (labelRank is { } rank && DagreLayout.Same(vRank, rank))
            {
                attrs.Width = edgeLabel.Width;
                attrs.Height = edgeLabel.Height;
                attrs.Dummy = "edge-label";
                attrs.LabelPos = edgeLabel.LabelPos;
            }

            g.SetEdge(v, dummy, new LayoutEdge { Weight = edgeLabel.Weight }, name);
            if (i == 0)
            {
                g.GraphLabel!.DummyChains.Add(dummy);
            }

            v = dummy;
        }

        g.SetEdge(v, w, new LayoutEdge { Weight = edgeLabel.Weight }, name);
    }
}
