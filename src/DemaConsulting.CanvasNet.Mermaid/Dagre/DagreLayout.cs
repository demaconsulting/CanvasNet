// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     dagre's layered layout (<c>layout.js</c> of dagre-d3-es 7.0.14, the version Mermaid 11.17.2 uses): positions the
///     nodes of a directed, possibly compound and cyclic graph in ranks and routes its edges.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Run{TNode,TEdge}"/> copies the input graph into an internal layout graph with dagre's defaults, runs
///         the stages of <c>runLayout</c> in dagre's order (edge-label space, self-edge removal, cycle breaking, the
///         nesting graph, ranking, edge-label proxies, rank clean-up, normalization, cluster chains and borders, ordering,
///         self-edge insertion, the coordinate system, positioning, and the undo steps), and writes the results back:
///         every node's center, every cluster's size, every edge's route (ending on the node borders) and label center,
///         and the drawing size.
///     </para>
///     <para>
///         The port reproduces dagre's results exactly, including its JavaScript key order, stable sorts and lodash
///         semantics, as the stage goldens recorded from dagre-d3-es check. Every iterative stage charges the
///         <see cref="LayoutWorkMeter"/>, which also observes cancellation. A layout that dagre would fail (for example
///         an edge whose route ends at its node's center) fails with a located <see cref="MermaidRenderException"/>
///         (<see cref="MermaidDiagnosticCodes.RenderFailed"/>); a stack that runs short in a deeply nested graph fails as
///         a <see cref="MermaidDiagnosticCodes.LimitExceeded"/>.
///     </para>
/// </remarks>
internal static partial class DagreLayout
{
    /// <summary>
    ///     Lays out a graph (dagre's <c>layout</c>).
    /// </summary>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="g">
    ///     The graph to lay out: a multigraph or simple graph, compound or flat, whose graph label, node labels and edge
    ///     labels are set. The results are written to the labels.
    /// </param>
    /// <param name="meter">The work meter of the diagram.</param>
    /// <returns>The internal layout graph after the layout, with the ranks and orders of the real nodes, for checks.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the work limit is reached or the layout fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public static LayoutGraph Run<TNode, TEdge>(Graph<DagreGraph, TNode, TEdge> g, LayoutWorkMeter meter)
        where TNode : DagreNode
        where TEdge : DagreEdge
    {
        try
        {
            meter.Charge(g.NodeCount + g.EdgeCount + 1);
            var layoutGraph = BuildLayoutGraph(g);
            RunLayout(layoutGraph, meter);
            UpdateInputGraph(g, layoutGraph);
            return layoutGraph;
        }
        catch (InsufficientExecutionStackException)
        {
            throw meter.LimitExceeded();
        }
        catch (MermaidRenderException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw meter.Failure(ex.Message);
        }
    }

    /// <summary>
    ///     Runs the stages on the layout graph (dagre's <c>runLayout</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The work meter.</param>
    private static void RunLayout(LayoutGraph g, LayoutWorkMeter meter)
    {
        MakeSpaceForEdgeLabels(g);
        RemoveSelfEdges(g);
        DagreAcyclic.Run(g, meter);
        DagreNesting.Run(g, meter);
        DagreRanker.Rank(AsNonCompoundGraph(g), meter);
        InjectEdgeLabelProxies(g, meter);
        RemoveEmptyRanks(g);
        DagreNesting.Cleanup(g);
        NormalizeRanks(g);
        AssignRankMinMax(g);
        RemoveEdgeLabelProxies(g);
        DagreNormalizer.Run(g, meter);
        DagreNesting.ParentDummyChains(g, meter);
        DagreNesting.AddBorderSegments(g, meter);
        DagreOrdering.Order(g, meter);
        InsertSelfEdges(g, meter);
        DagreCoordinateSystem.Adjust(g);
        DagrePositioning.Position(g, meter);
        PositionSelfEdges(g);
        RemoveBorderNodes(g);
        DagreNormalizer.Undo(g, meter);
        FixupEdgeLabelCoords(g);
        DagreCoordinateSystem.Undo(g);
        TranslateGraph(g);
        AssignNodeIntersects(g);
        ReversePointsForReversedEdges(g);
        DagreAcyclic.Undo(g);
    }

    /// <summary>
    ///     Copies the input graph with dagre's defaults applied (dagre's <c>buildLayoutGraph</c>).
    /// </summary>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="inputGraph">The input graph.</param>
    /// <returns>The layout graph: a compound multigraph with new labels.</returns>
    private static LayoutGraph BuildLayoutGraph<TNode, TEdge>(Graph<DagreGraph, TNode, TEdge> inputGraph)
        where TNode : DagreNode
        where TEdge : DagreEdge
    {
        var g = new LayoutGraph(multigraph: true, compound: true);
        var graph = inputGraph.GraphLabel ?? new DagreGraph();
        var label = new LayoutGraphLabel
        {
            MarginX = graph.MarginX,
            MarginY = graph.MarginY,
            Acyclicer = graph.Acyclicer,
            Ranker = graph.Ranker,
            Align = graph.Align,
        };
        label.RankSep = graph.RankSep ?? label.RankSep;
        label.EdgeSep = graph.EdgeSep ?? label.EdgeSep;
        label.NodeSep = graph.NodeSep ?? label.NodeSep;
        label.RankDir = graph.RankDir ?? label.RankDir;
        g.SetGraph(label);

        foreach (var v in inputGraph.Nodes())
        {
            var node = inputGraph.Node(v);
            g.SetNode(v, new LayoutNode { Width = node?.Width ?? 0, Height = node?.Height ?? 0 });
            g.SetParent(v, inputGraph.IsCompound ? inputGraph.Parent(v) : null);
        }

        foreach (var e in inputGraph.Edges())
        {
            var edge = inputGraph.Edge(e);
            g.SetEdge(e, new LayoutEdge
            {
                MinLen = edge?.MinLen ?? 1,
                Weight = edge?.Weight ?? 1,
                Width = edge?.Width ?? 0,
                Height = edge?.Height ?? 0,
                LabelOffset = edge?.LabelOffset ?? 10,
                LabelPos = edge?.LabelPos ?? "r",
            });
        }

        return g;
    }

    /// <summary>
    ///     Writes the results to the input graph (dagre's <c>updateInputGraph</c>).
    /// </summary>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="inputGraph">The input graph.</param>
    /// <param name="layoutGraph">The layout graph.</param>
    private static void UpdateInputGraph<TNode, TEdge>(Graph<DagreGraph, TNode, TEdge> inputGraph, LayoutGraph layoutGraph)
        where TNode : DagreNode
        where TEdge : DagreEdge
    {
        foreach (var v in inputGraph.Nodes())
        {
            var inputLabel = inputGraph.Node(v);
            var layoutLabel = layoutGraph.Node(v)!;
            if (inputLabel is not null)
            {
                inputLabel.X = layoutLabel.X;
                inputLabel.Y = layoutLabel.Y;
                if (layoutGraph.Children(v)!.Length > 0)
                {
                    inputLabel.Width = layoutLabel.Width;
                    inputLabel.Height = layoutLabel.Height;
                }
            }
        }

        foreach (var e in inputGraph.Edges())
        {
            var inputLabel = inputGraph.Edge(e)!;
            var layoutLabel = layoutGraph.Edge(e)!;
            inputLabel.Points = layoutLabel.Points;
            if (layoutLabel.X is not null)
            {
                inputLabel.X = layoutLabel.X;
                inputLabel.Y = layoutLabel.Y;
            }
        }

        var graph = inputGraph.GraphLabel;
        if (graph is not null)
        {
            graph.Width = layoutGraph.GraphLabel!.Width;
            graph.Height = layoutGraph.GraphLabel.Height;
        }
    }

    /// <summary>
    ///     Halves the rank separation and doubles every minimum length, so that a label rank fits between two node ranks,
    ///     and widens each edge by its label offset when the label sits at its side (dagre's
    ///     <c>makeSpaceForEdgeLabels</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void MakeSpaceForEdgeLabels(LayoutGraph g)
    {
        var graph = g.GraphLabel!;
        graph.RankSep /= 2;
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            edge.MinLen *= 2;
            if (!string.Equals(edge.LabelPos!.ToLowerInvariant(), "c", StringComparison.Ordinal))
            {
                // dagre compares the direction in upper case here
                if (graph.RankDir is "TB" or "BT")
                {
                    edge.Width += edge.LabelOffset;
                }
                else
                {
                    edge.Height += edge.LabelOffset;
                }
            }
        }
    }

    /// <summary>
    ///     Removes the self edges, keeping them on their node (dagre's <c>removeSelfEdges</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void RemoveSelfEdges(LayoutGraph g)
    {
        foreach (var e in g.Edges())
        {
            if (e.V == e.W)
            {
                var node = g.Node(e.V)!;
                node.SelfEdges ??= [];
                node.SelfEdges.Add(new SelfEdge(e, g.Edge(e)!));
                g.RemoveEdge(e);
            }
        }
    }

    /// <summary>
    ///     Adds a proxy node halfway along each labeled edge, so that the label rank is known after empty ranks are removed
    ///     (dagre's <c>injectEdgeLabelProxies</c>).
    /// </summary>
    /// <param name="g">The ranked layout graph.</param>
    /// <param name="meter">The work meter.</param>
    private static void InjectEdgeLabelProxies(LayoutGraph g, LayoutWorkMeter meter)
    {
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            if (Truthy(edge.Width) && Truthy(edge.Height))
            {
                var vRank = Lodash.Num(g.Node(e.V)!.Rank);
                var wRank = Lodash.Num(g.Node(e.W)!.Rank);
                AddDummyNode(g, meter, "edge-proxy", new LayoutNode { Rank = ((wRank - vRank) / 2) + vRank, E = e }, "_ep");
            }
        }
    }

    /// <summary>
    ///     Records the rank range of every cluster from its top and bottom borders (dagre's <c>assignRankMinMax</c>).
    /// </summary>
    /// <remarks>
    ///     dagre calls lodash's <c>_.max</c> with two numbers instead of an array, which gives <c>undefined</c>, so the
    ///     graph's <c>maxRank</c> is 0 without clusters and undefined with them; the port keeps that value.
    /// </remarks>
    /// <param name="g">The layout graph.</param>
    private static void AssignRankMinMax(LayoutGraph g)
    {
        double? maxRank = 0;
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            if (!string.IsNullOrEmpty(node.BorderTop))
            {
                node.MinRank = g.Node(node.BorderTop)!.Rank;
                node.MaxRank = g.Node(node.BorderBottom!)!.Rank;
                maxRank = null;
            }
        }

        g.GraphLabel!.MaxRank = maxRank;
    }

    /// <summary>
    ///     Removes the edge-label proxies, recording their rank on the edge (dagre's <c>removeEdgeLabelProxies</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void RemoveEdgeLabelProxies(LayoutGraph g)
    {
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            if (node.Dummy == "edge-proxy")
            {
                g.Edge(node.E!)!.LabelRank = node.Rank;
                g.RemoveNode(v);
            }
        }
    }

    /// <summary>
    ///     Gives each self edge a dummy node right of its node in the order (dagre's <c>insertSelfEdges</c>).
    /// </summary>
    /// <param name="g">The ordered layout graph.</param>
    /// <param name="meter">The work meter.</param>
    private static void InsertSelfEdges(LayoutGraph g, LayoutWorkMeter meter)
    {
        var layers = BuildLayerMatrix(g);
        foreach (var layer in layers)
        {
            var orderShift = 0;
            for (var i = 0; i < layer.Count; i++)
            {
                var node = g.Node(layer[i])!;
                node.Order = i + orderShift;
                foreach (var selfEdge in node.SelfEdges ?? [])
                {
                    AddDummyNode(
                        g,
                        meter,
                        "selfedge",
                        new LayoutNode
                        {
                            Width = selfEdge.Label.Width,
                            Height = selfEdge.Label.Height,
                            Rank = node.Rank,
                            Order = i + ++orderShift,
                            E = selfEdge.E,
                            Label = selfEdge.Label,
                        },
                        "_se");
                }

                node.SelfEdges = null;
            }
        }
    }

    /// <summary>
    ///     Turns each self-edge dummy back into its edge, routed as a loop on the right of its node (dagre's
    ///     <c>positionSelfEdges</c>).
    /// </summary>
    /// <param name="g">The positioned layout graph.</param>
    private static void PositionSelfEdges(LayoutGraph g)
    {
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            if (node.Dummy == "selfedge")
            {
                var selfNode = g.Node(node.E!.V)!;
                var x = selfNode.X + (selfNode.Width / 2);
                var y = selfNode.Y;
                var dx = node.X - x;
                var dy = selfNode.Height / 2;
                g.SetEdge(node.E, node.Label);
                g.RemoveNode(v);
                node.Label!.Points =
                [
                    new DagrePoint(x + (2 * dx / 3), y - dy),
                    new DagrePoint(x + (5 * dx / 6), y - dy),
                    new DagrePoint(x + dx, y),
                    new DagrePoint(x + (5 * dx / 6), y + dy),
                    new DagrePoint(x + (2 * dx / 3), y + dy),
                ];
                node.Label.X = node.X;
                node.Label.Y = node.Y;
            }
        }
    }

    /// <summary>
    ///     Sizes each cluster from its border nodes and removes the border nodes (dagre's <c>removeBorderNodes</c>).
    /// </summary>
    /// <param name="g">The positioned layout graph.</param>
    private static void RemoveBorderNodes(LayoutGraph g)
    {
        foreach (var v in g.Nodes())
        {
            if (g.Children(v)!.Length > 0)
            {
                var node = g.Node(v)!;
                var t = g.Node(node.BorderTop!)!;
                var b = g.Node(node.BorderBottom!)!;
                var l = g.Node(node.BorderLeft![^1]!)!;
                var r = g.Node(node.BorderRight![^1]!)!;
                node.Width = Math.Abs(r.X - l.X);
                node.Height = Math.Abs(b.Y - t.Y);
                node.X = l.X + (node.Width / 2);
                node.Y = t.Y + (node.Height / 2);
            }
        }

        foreach (var v in g.Nodes())
        {
            if (g.Node(v)!.Dummy == "border")
            {
                g.RemoveNode(v);
            }
        }
    }

    /// <summary>
    ///     Moves each label that sits at the side of its edge off the edge, and removes the label offset from its width
    ///     (dagre's <c>fixupEdgeLabelCoords</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void FixupEdgeLabelCoords(LayoutGraph g)
    {
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            if (edge.X is not { } x)
            {
                continue;
            }

            if (edge.LabelPos is "l" or "r")
            {
                edge.Width -= edge.LabelOffset;
            }

            switch (edge.LabelPos)
            {
                case "l":
                    edge.X = x - ((edge.Width / 2) + edge.LabelOffset);
                    break;
                case "r":
                    edge.X = x + ((edge.Width / 2) + edge.LabelOffset);
                    break;
            }
        }
    }

    /// <summary>
    ///     Moves the drawing so that its top-left corner is at the margins, and records its size (dagre's
    ///     <c>translateGraph</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void TranslateGraph(LayoutGraph g)
    {
        var minX = double.PositiveInfinity;
        var maxX = 0.0;
        var minY = double.PositiveInfinity;
        var maxY = 0.0;
        var graphLabel = g.GraphLabel!;
        var marginX = Truthy(graphLabel.MarginX) ? graphLabel.MarginX!.Value : 0;
        var marginY = Truthy(graphLabel.MarginY) ? graphLabel.MarginY!.Value : 0;

        // The extremes of the nodes and of the edge labels
        void GetExtremes(double x, double y, double w, double h)
        {
            minX = Math.Min(minX, x - (w / 2));
            maxX = Math.Max(maxX, x + (w / 2));
            minY = Math.Min(minY, y - (h / 2));
            maxY = Math.Max(maxY, y + (h / 2));
        }

        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            GetExtremes(node.X, node.Y, node.Width, node.Height);
        }

        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            if (edge.X is { } x)
            {
                GetExtremes(x, Lodash.Num(edge.Y), edge.Width, edge.Height);
            }
        }

        minX -= marginX;
        minY -= marginY;

        // Move everything
        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            node.X -= minX;
            node.Y -= minY;
        }

        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            foreach (var p in edge.Points ?? [])
            {
                p.X -= minX;
                p.Y -= minY;
            }

            if (edge.X is { } x)
            {
                edge.X = x - minX;
            }

            if (edge.Y is { } y)
            {
                edge.Y = y - minY;
            }
        }

        graphLabel.Width = maxX - minX + marginX;
        graphLabel.Height = maxY - minY + marginY;
    }

    /// <summary>
    ///     Ends every route on the borders of its nodes (dagre's <c>assignNodeIntersects</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <exception cref="InvalidOperationException">Thrown when a route ends at the center of its node.</exception>
    private static void AssignNodeIntersects(LayoutGraph g)
    {
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            var nodeV = g.Node(e.V)!;
            var nodeW = g.Node(e.W)!;
            DagrePoint p1;
            DagrePoint p2;
            if (edge.Points is null)
            {
                edge.Points = [];
                p1 = new DagrePoint(nodeW.X, nodeW.Y);
                p2 = new DagrePoint(nodeV.X, nodeV.Y);
            }
            else
            {
                if (edge.Points.Count == 0)
                {
                    throw new InvalidOperationException("an edge route has no points");
                }

                p1 = edge.Points[0];
                p2 = edge.Points[^1];
            }

            edge.Points.Insert(0, IntersectRect(nodeV.X, nodeV.Y, nodeV.Width, nodeV.Height, p1));
            edge.Points.Add(IntersectRect(nodeW.X, nodeW.Y, nodeW.Width, nodeW.Height, p2));
        }
    }

    /// <summary>
    ///     Reverses the routes of the edges that were reversed to break cycles (dagre's
    ///     <c>reversePointsForReversedEdges</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    private static void ReversePointsForReversedEdges(LayoutGraph g)
    {
        foreach (var e in g.Edges())
        {
            var edge = g.Edge(e)!;
            if (edge.Reversed)
            {
                edge.Points?.Reverse();
            }
        }
    }
}
