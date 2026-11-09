// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     A node of the graphs the cluster layout hands to dagre (Mermaid's node objects, <c>{ ...node }</c>): a node or a
///     subgraph of the layout data, a hidden self-loop node, or a laid-out subgraph that stands in for its contents (a
///     <c>clusterNode</c>).
/// </summary>
internal sealed class ClusterLayoutNode : DagreNode
{
    /// <summary>
    ///     Initializes a node of the layout data.
    /// </summary>
    /// <param name="data">The node.</param>
    public ClusterLayoutNode(LayoutNode data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Id = data.Id;
        Data = data;
    }

    /// <summary>
    ///     Initializes the node that stands in for a subgraph laid out on its own (Mermaid's <c>clusterNode</c>).
    /// </summary>
    /// <param name="id">The subgraph id.</param>
    /// <param name="clusterData">The subgraph's node.</param>
    /// <param name="subGraph">The graph of the subgraph's contents.</param>
    public ClusterLayoutNode(string id, LayoutNode? clusterData, Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> subGraph)
    {
        Id = id;
        ClusterData = clusterData;
        SubGraph = subGraph;
    }

    /// <summary>Gets the node id.</summary>
    public string Id { get; }

    /// <summary>Gets the node of the layout data, or null for a <c>clusterNode</c>.</summary>
    public LayoutNode? Data { get; }

    /// <summary>Gets a value indicating whether the node stands in for a subgraph laid out on its own.</summary>
    public bool IsClusterNode => SubGraph is not null;

    /// <summary>Gets the subgraph's node of a <c>clusterNode</c>.</summary>
    public LayoutNode? ClusterData { get; }

    /// <summary>Gets the graph of a <c>clusterNode</c>'s contents.</summary>
    public Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge>? SubGraph { get; }

    /// <summary>Gets or sets the horizontal correction of a subgraph whose title is wider than its box (<c>node.diff</c>).</summary>
    public double Diff { get; set; }

    /// <summary>Gets or sets the drawn shape of a node.</summary>
    public ShapeResult? Shape { get; set; }

    /// <summary>Gets or sets the drawing of a <c>clusterNode</c>'s contents.</summary>
    public RenderedGraph? Rendered { get; set; }

    /// <summary>Gets or sets the intersect function (set when the node or the subgraph is drawn), or null.</summary>
    public ShapeIntersect? Intersect { get; set; }

    /// <summary>Gets the node's current box.</summary>
    public NodeBox Box => new(X, Y, Width ?? 0, Height ?? 0);
}

/// <summary>
///     An edge of the graphs the cluster layout hands to dagre (Mermaid's edge objects): an edge of the layout data, one
///     of the three segments of a self-loop, or the merged self-loop that is drawn.
/// </summary>
internal sealed class ClusterLayoutEdge : DagreEdge
{
    /// <summary>
    ///     Initializes an edge of the layout data.
    /// </summary>
    /// <param name="data">The edge.</param>
    public ClusterLayoutEdge(LayoutEdge data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        Id = data.Id;
        Label = data.Label;
        ArrowTypeStart = data.ArrowTypeStart;
        ArrowTypeEnd = data.ArrowTypeEnd;

        // A link of unknown length keeps Mermaid's undefined minlen, which dagre reads as NaN
        MinLen = data.MinLen is { } length ? length : double.NaN;
        LabelPos = data.LabelPos;
    }

    /// <summary>Gets the edge of the layout data.</summary>
    public LayoutEdge Data { get; }

    /// <summary>Gets or sets the id of the drawn edge.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the label text.</summary>
    public string Label { get; set; }

    /// <summary>Gets or sets the arrow type at the start.</summary>
    public string ArrowTypeStart { get; set; }

    /// <summary>Gets or sets the arrow type at the end.</summary>
    public string ArrowTypeEnd { get; set; }

    /// <summary>Gets or sets the subgraph the edge leaves (the route is cut at its border), or null.</summary>
    public string? FromCluster { get; set; }

    /// <summary>Gets or sets the subgraph the edge enters, or null.</summary>
    public string? ToCluster { get; set; }

    /// <summary>Gets or sets the id of the self-loop this edge is a segment of, or null.</summary>
    public string? SelfLoopId { get; set; }

    /// <summary>Gets or sets the order of the self-loop segment (0, 1 or 2).</summary>
    public int SelfLoopOrder { get; set; }

    /// <summary>Gets or sets the measured label, or null when the label was not measured on this edge.</summary>
    public LabelLayout? MeasuredLabel { get; set; }

    /// <summary>Gets or sets the edge whose label this drawn edge places (the middle segment of a merged self-loop), or null for itself.</summary>
    public ClusterLayoutEdge? LabelOwner { get; set; }
}

/// <summary>
///     A drawn graph: the items of its four layers (subgraphs, edge paths, edge labels and nodes), their box and the
///     correction of its subgraph's title (<c>diff</c>).
/// </summary>
/// <param name="Items">The layers as groups, in drawing order.</param>
/// <param name="Bounds">The box of everything drawn (SVG's <c>getBBox</c> of the root group), or null when empty.</param>
/// <param name="Diff">The <c>diff</c> of the last subgraph of the graph.</param>
internal sealed record RenderedGraph(IReadOnlyList<SceneItem> Items, SceneRect? Bounds, double Diff);

/// <summary>
///     Mermaid's dagre renderer (<c>layout-algorithms/dagre/index.js</c> with <c>mermaid-graphlib.js</c>): the layout
///     data becomes a compound graph, subgraphs without links to the outside are laid out on their own (recursively,
///     with their own direction) and stand in for their contents in the graph around them, every node is sized by its
///     shape, every edge label is measured, dagre lays each graph out, and the subgraphs, edges, edge labels and nodes
///     are drawn.
/// </summary>
/// <remarks>
///     <para>
///         <b>Graphs.</b> The root graph has dagre's <c>nodesep</c> and <c>ranksep</c> from the configuration and margins
///         of 8. A subgraph laid out on its own gets the direction it declares (else the other one of <c>TB</c> and
///         <c>LR</c>), the parent's <c>nodesep</c> and the parent's <c>ranksep</c> plus 25. A self-loop is laid out as
///         three edges through two hidden nodes and drawn as one compact loop beside its node.
///     </para>
///     <para>
///         <b>Subgraph links.</b> A link to a subgraph that links to the outside is moved to a node inside it (its
///         anchor) and the route is cut where it enters the subgraph's rectangle.
///     </para>
///     <para>
///         An instance belongs to one render on one thread; every stage charges the <see cref="LayoutWorkMeter"/> of the
///         render.
///     </para>
/// </remarks>
internal sealed partial class ClusterLayout
{
    /// <summary>The margin dagre leaves around each graph.</summary>
    private const double GraphMargin = 8;

    /// <summary>The vertical padding <c>positionNode</c> takes off a laid-out subgraph.</summary>
    private const double ClusterNodePadding = 8;

    /// <summary>The settings of the rendering.</summary>
    private readonly ShapeContext _context;

    /// <summary>The work meter of the render.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>The total subgraph title margin (<c>subGraphTitleTotalMargin</c>).</summary>
    private readonly double _titleMargin;

    /// <summary>
    ///     Initializes the layout of one render.
    /// </summary>
    /// <param name="context">The settings of the rendering (configuration, style sheet, label measurement).</param>
    /// <param name="meter">The work meter of the render.</param>
    public ClusterLayout(ShapeContext context, LayoutWorkMeter meter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(meter);
        _context = context;
        _meter = meter;
        _titleMargin = context.Config.Flowchart.SubGraphTitleMarginTop + context.Config.Flowchart.SubGraphTitleMarginBottom;
    }

    /// <summary>
    ///     Lays out and draws layout data.
    /// </summary>
    /// <param name="data">The layout data.</param>
    /// <returns>The drawn root graph.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when the work limit is reached or the layout fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public RenderedGraph Render(LayoutData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var graph = Prepare(data);
        AdjustClustersAndEdges(graph);
        return RenderGraph(graph, null);
    }

    /// <summary>
    ///     Builds the root graph (Mermaid's <c>prepareLayoutForDagre</c>): every node with its parent, every edge, and the
    ///     three segments and two hidden nodes of each self-loop.
    /// </summary>
    /// <param name="data">The layout data.</param>
    /// <returns>The graph.</returns>
    private Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> Prepare(LayoutData data)
    {
        var graph = new Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge>(multigraph: true, compound: true);
        graph.SetGraph(new DagreGraph
        {
            RankDir = data.Direction,
            NodeSep = TopLevelNumber(data, "nodeSpacing") ?? data.NodeSpacing,
            RankSep = TopLevelNumber(data, "rankSpacing") ?? data.RankSpacing,
            MarginX = GraphMargin,
            MarginY = GraphMargin,
        });

        foreach (var node in data.Nodes)
        {
            _meter.Charge(1);
            graph.SetNode(node.Id, new ClusterLayoutNode(node));
            if (!string.IsNullOrEmpty(node.ParentId))
            {
                SetParent(graph, node.Id, node.ParentId, node.Location);
            }
        }

        foreach (var edge in data.Edges)
        {
            _meter.Charge(1);
            if (edge.Start == edge.End)
            {
                AddSelfLoop(graph, edge);
            }
            else
            {
                graph.SetEdge(edge.Start, edge.End, new ClusterLayoutEdge(edge), edge.Id);
            }
        }

        return graph;
    }

    /// <summary>
    ///     Sets the parent of a node; a parent that would make a cycle (subgraphs inside each other) fails as graphlib's
    ///     <c>setParent</c> fails Mermaid's render.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="id">The node.</param>
    /// <param name="parentId">The parent.</param>
    /// <param name="location">The location of the node, for the failure.</param>
    /// <exception cref="MermaidRenderException">Thrown, located, when the parent would make a cycle.</exception>
    private static void SetParent(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, string id, string parentId, MermaidLocation location)
    {
        try
        {
            graph.SetParent(id, parentId);
        }
        catch (InvalidOperationException ex)
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.RenderFailed,
                $"the subgraphs cannot be laid out: {ex.Message}",
                location,
                "make sure no subgraph contains a subgraph that contains it");
        }
    }

    /// <summary>
    ///     Adds a self-loop as dagre lays it out: two hidden 10 by 10 nodes beside the node and three edges through them.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="edge">The self-loop.</param>
    private static void AddSelfLoop(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, LayoutEdge edge)
    {
        var nodeId = edge.Start;
        var node = graph.Node(nodeId)?.Data;
        var special1 = nodeId + "---" + nodeId + "---1";
        var special2 = nodeId + "---" + nodeId + "---2";
        foreach (var special in new[] { special1, special2 })
        {
            var hidden = new LayoutNode
            {
                Id = special,
                DomId = special,
                ParentId = node?.ParentId,
                Label = string.Empty,
                Padding = 0,
                Shape = "labelRect",
                Location = edge.Location,
            };
            graph.SetNode(special, new ClusterLayoutNode(hidden) { Width = 10, Height = 10 });
            graph.SetParent(special, string.IsNullOrEmpty(node?.ParentId) ? null : node.ParentId);
        }

        var first = new ClusterLayoutEdge(edge)
        {
            Id = nodeId + "-cyclic-special-1",
            Label = string.Empty,
            ArrowTypeEnd = "none",
            SelfLoopId = edge.Id,
            SelfLoopOrder = 0,
        };
        var middle = new ClusterLayoutEdge(edge)
        {
            Id = nodeId + "-cyclic-special-mid",
            ArrowTypeStart = "none",
            ArrowTypeEnd = "none",
            SelfLoopId = edge.Id,
            SelfLoopOrder = 1,
        };
        var last = new ClusterLayoutEdge(edge)
        {
            Id = nodeId + "-cyclic-special-2",
            Label = string.Empty,
            ArrowTypeStart = "none",
            SelfLoopId = edge.Id,
            SelfLoopOrder = 2,
        };
        if (node?.IsGroup == true)
        {
            first.FromCluster = nodeId;
            last.ToCluster = nodeId;
        }

        graph.SetEdge(nodeId, special1, first, nodeId + "-cyclic-special-0");
        graph.SetEdge(special1, special2, middle, nodeId + "-cyclic-special-1");
        graph.SetEdge(special2, nodeId, last, nodeId + "-cyclic-special-2");
    }

    /// <summary>
    ///     Gets a top-level spacing key of the configuration (<c>config.nodeSpacing</c>, which has no default and wins
    ///     when set to a number other than zero).
    /// </summary>
    /// <param name="data">The layout data.</param>
    /// <param name="key">The key.</param>
    /// <returns>The number, or null.</returns>
    private static double? TopLevelNumber(LayoutData data, string key) =>
        data.Config.Raw[key] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<double>(out var number) && number != 0 && double.IsFinite(number)
            ? number
            : null;

    /// <summary>
    ///     Measures, lays out and draws one graph (Mermaid's <c>measureDagreGraph</c>, <c>runDagreGraphLayout</c> and
    ///     <c>paintDagreLayoutCore</c>).
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="parentCluster">The <c>clusterNode</c> whose contents the graph holds, or null for the root graph.</param>
    /// <returns>The drawn graph.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when the work limit is reached or the layout fails.</exception>
    private RenderedGraph RenderGraph(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph, ClusterLayoutNode? parentCluster)
    {
        var (nodeOrder, labelOrder) = Measure(graph, parentCluster);
        CheckNoEdgeAtSubgraph(graph);
        DagreLayout.Run(graph, _meter);
        return Paint(graph, nodeOrder, labelOrder);
    }

    /// <summary>
    ///     Fails, located at the link, when an edge handed to dagre still ends at a subgraph that holds other nodes of the
    ///     graph: the anchors did not move it (for example a subgraph used as a node of another subgraph, or a self-loop
    ///     on a subgraph). Dagre ranks a flat copy of the graph without such subgraphs, so the edge's end has no label
    ///     there and Mermaid's render fails (<c>Cannot set properties of undefined (setting 'rank')</c> in
    ///     <c>longestPath</c>).
    /// </summary>
    /// <param name="graph">The graph about to be laid out.</param>
    /// <exception cref="MermaidRenderException">Thrown, located at the link, when an edge ends at a subgraph.</exception>
    private void CheckNoEdgeAtSubgraph(Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph)
    {
        var edges = graph.Edges();
        _meter.Charge(edges.Length);
        foreach (var e in edges)
        {
            // Dagre removes self-edges before ranking; a subgraph at either end of any other edge fails
            if (e.V == e.W)
            {
                continue;
            }

            string? subgraph = null;
            if (graph.Children(e.V) is { Length: > 0 })
            {
                subgraph = e.V;
            }
            else if (graph.Children(e.W) is { Length: > 0 })
            {
                subgraph = e.W;
            }

            if (subgraph is not null && graph.Edge(e) is { } edge)
            {
                throw new MermaidRenderException(
                    MermaidDiagnosticCodes.RenderFailed,
                    $"the link '{edge.Data.Id}' cannot be laid out: it still ends at the subgraph '{subgraph}', which holds other nodes (Mermaid fails on this diagram too)",
                    edge.Data.Location,
                    "link to a node inside the subgraph instead");
            }
        }
    }

    /// <summary>
    ///     Sizes the nodes and edge labels of a graph (Mermaid's <c>measureDagreGraph</c>): each node by its shape, each
    ///     <c>clusterNode</c> by laying its contents out first, each edge label by measuring it.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="parentCluster">The <c>clusterNode</c> whose contents the graph holds, or null.</param>
    /// <returns>The nodes in the order they are drawn, and the edges whose labels are drawn, in order.</returns>
    private (string[] NodeOrder, List<ClusterLayoutEdge> LabelOrder) Measure(
        Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph,
        ClusterLayoutNode? parentCluster)
    {
        var nodeOrder = graph.Nodes();
        foreach (var v in nodeOrder)
        {
            _meter.Charge(1);
            var node = graph.Node(v);

            // The subgraph itself becomes the parent of the top level of its contents
            if (parentCluster?.ClusterData is { } clusterData)
            {
                graph.SetNode(parentCluster.Id, new ClusterLayoutNode(clusterData));
                if (graph.Parent(v) is null)
                {
                    graph.SetParent(v, parentCluster.Id);
                }
            }

            if (node is null)
            {
                continue;
            }

            if (node.SubGraph is { } subGraph)
            {
                // The contents are laid out first, with the parent's spacing (25 more between ranks)
                var parentLabel = graph.GraphLabel ?? new DagreGraph();
                var subLabel = subGraph.GraphLabel ?? new DagreGraph();
                subGraph.SetGraph(new DagreGraph
                {
                    RankDir = subLabel.RankDir,
                    NodeSep = parentLabel.NodeSep,
                    RankSep = parentLabel.RankSep + 25,
                    MarginX = subLabel.MarginX,
                    MarginY = subLabel.MarginY,
                });
                var rendered = RenderGraph(subGraph, node);
                node.Width = rendered.Bounds?.Width ?? 0;
                node.Height = rendered.Bounds?.Height ?? 0;
                node.Diff = rendered.Diff;
                node.Rendered = rendered;
            }
            else if (graph.Children(v) is { Length: > 0 })
            {
                // A subgraph laid out with its graph
                _clusterDb[node.Id] = new ClusterInfo { Id = FindNonClusterChild(node.Id, graph, null), Node = node };
            }
            else if (node.Data is { } data)
            {
                // The shapes see the direction of the graph they are laid out in (insertMeasuredNode's dir)
                var shape = ShapeLibrary.Draw(data with { Dir = graph.GraphLabel?.RankDir }, _context);
                node.Shape = shape;
                node.Width = shape.Width;
                node.Height = shape.Height;
                node.Intersect = shape.Intersect;
            }
        }

        // Every edge label is measured; of a self-loop only the middle segment's
        var labelOrder = new List<ClusterLayoutEdge>();
        foreach (var e in graph.Edges())
        {
            _meter.Charge(1);
            if (graph.Edge(e) is not { } edge || (edge.SelfLoopId is not null && edge.SelfLoopOrder != 1))
            {
                continue;
            }

            edge.MeasuredLabel = EdgeBuilder.MeasureLabel(edge.Data, edge.Label, _context);
            edge.Width = edge.MeasuredLabel.Width;
            edge.Height = edge.MeasuredLabel.Height;
            labelOrder.Add(edge);
        }

        return (nodeOrder, labelOrder);
    }

    /// <summary>
    ///     Draws a laid-out graph (Mermaid's <c>paintDagreLayoutCore</c>, and for the root graph
    ///     <c>applyDagreLayoutResult</c> with <c>paintLayoutData</c>, which do the same).
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="nodeOrder">The nodes in the order they were measured.</param>
    /// <param name="labelOrder">The edges whose labels were measured, in order.</param>
    /// <returns>The drawn graph.</returns>
    private RenderedGraph Paint(
        Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge> graph,
        string[] nodeOrder,
        List<ClusterLayoutEdge> labelOrder)
    {
        // The subgraphs are drawn and the nodes moved by the title margins
        var clusters = new SceneLayer();
        foreach (var v in SortNodesByHierarchy(graph))
        {
            _meter.Charge(1);
            if (graph.Node(v) is not { } node)
            {
                continue;
            }

            if (node.IsClusterNode)
            {
                node.Y += _titleMargin;
                if (_clusterDb.TryGetValue(node.Id, out var info))
                {
                    info.Node = node;
                }
            }
            else if (graph.Children(v) is { Length: > 0 })
            {
                node.Height = (node.Height ?? 0) + _titleMargin;
                if (node.Data is { } data)
                {
                    var cluster = ShapeLibrary.DrawCluster(data, node.X, node.Y, node.Width ?? 0, node.Height ?? 0, _context);
                    node.Width = cluster.Width;
                    node.Height = cluster.Height;
                    node.Diff = cluster.Diff;
                    node.Intersect = cluster.Shape.Intersect;
                    clusters.Add(cluster.Shape.Items, cluster.Shape.Bounds);
                }

                if (_clusterDb.TryGetValue(node.Id, out var info))
                {
                    info.Node = node;
                }
            }
            else
            {
                node.Y += _titleMargin / 2;
            }
        }

        // The edges, and where their labels go
        var edgePaths = new SceneLayer();
        var labelPositions = new Dictionary<ClusterLayoutEdge, (ShapePoint Position, string Id)>();
        foreach (var (edge, start, end) in GetEdgesToRender(graph, _titleMargin / 2))
        {
            _meter.Charge((edge.Points?.Count ?? 0) + 1);
            var points = (edge.Points ?? []).Select(p => new ShapePoint(p.X, p.Y + (_titleMargin / 2))).ToList();
            var route = new EdgeRoute(edge.Data, edge.Id, points)
            {
                ArrowTypeStart = edge.ArrowTypeStart,
                ArrowTypeEnd = edge.ArrowTypeEnd,
                FromCluster = ClusterBox(edge.FromCluster),
                ToCluster = ClusterBox(edge.ToCluster),
            };
            var drawing = EdgeBuilder.Draw(route, EndOf(graph.Node(start)), EndOf(graph.Node(end)), _context);
            edgePaths.Add(drawing.Items, drawing.Bounds);
            if (EdgeBuilder.LabelPosition(edge.Label, edge.X, edge.Y, drawing, _titleMargin) is { } position)
            {
                labelPositions[edge.LabelOwner ?? edge] = (position, edge.Id);
            }
        }

        // The edge labels, in the order they were measured
        var edgeLabels = new SceneLayer();
        foreach (var owner in labelOrder)
        {
            if (owner.MeasuredLabel is { } label && labelPositions.TryGetValue(owner, out var placed))
            {
                var drawing = EdgeBuilder.DrawLabel(label, placed.Position, owner.Data, placed.Id, _context.Styles);
                edgeLabels.Add(drawing.Items, drawing.Bounds);
            }
        }

        // The nodes and the laid-out subgraphs, in the order they were measured
        var nodes = new SceneLayer();
        foreach (var v in nodeOrder)
        {
            if (graph.Node(v) is not { } node)
            {
                continue;
            }

            if (node.Rendered is { } rendered)
            {
                // positionNode of a clusterNode
                var x = node.X + node.Diff - ((node.Width ?? 0) / 2);
                var y = node.Y - ((node.Height ?? 0) / 2) - ClusterNodePadding;
                nodes.Add([new SceneGroup(x, y, rendered.Items) { Role = "subgraph", Id = node.Id, Location = node.ClusterData?.Location ?? default }], rendered.Bounds?.Offset(x, y));
            }
            else if (node.Shape is { } shape)
            {
                var role = node.Data?.Shape == "labelRect" ? "dummy-group" : "node-group";
                nodes.Add(
                    [new SceneGroup(node.X, node.Y, shape.Items) { Role = role, Id = node.Data?.DomId ?? node.Id, Location = node.Data?.Location ?? default }],
                    shape.Bounds.Offset(node.X, node.Y));
            }
        }

        // The diff of the graph is that of its last subgraph
        var diff = 0.0;
        foreach (var v in graph.Nodes())
        {
            if (graph.Node(v) is { Data.IsGroup: true } group)
            {
                diff = group.Diff;
            }
        }

        var items = new List<SceneItem>
        {
            new SceneGroup(0, 0, clusters.Items) { Role = "clusters" },
            new SceneGroup(0, 0, edgePaths.Items) { Role = "edgePaths" },
            new SceneGroup(0, 0, edgeLabels.Items) { Role = "edgeLabels" },
            new SceneGroup(0, 0, nodes.Items) { Role = "nodes" },
        };
        return new RenderedGraph(items, SceneLayer.Union(clusters.Bounds, edgePaths.Bounds, edgeLabels.Bounds, nodes.Bounds), diff);
    }

    /// <summary>
    ///     Gets an edge end of a node.
    /// </summary>
    /// <param name="node">The node, or null.</param>
    /// <returns>The end, or null.</returns>
    private static EdgeEnd? EndOf(ClusterLayoutNode? node) => node is null ? null : new EdgeEnd(node.Box, node.Intersect);

    /// <summary>
    ///     Gets the drawn box of a subgraph that an edge leaves or enters.
    /// </summary>
    /// <param name="id">The subgraph id, or null.</param>
    /// <returns>The box, or null when there is none.</returns>
    private NodeBox? ClusterBox(string? id) =>
        id is not null && _clusterDb.TryGetValue(id, out var info) && info.Node is { } node ? node.Box : null;

    /// <summary>
    ///     The items of one layer of a drawn graph and their box.
    /// </summary>
    private sealed class SceneLayer
    {
        /// <summary>Gets the items.</summary>
        public List<SceneItem> Items { get; } = [];

        /// <summary>Gets the box of the items, or null.</summary>
        public SceneRect? Bounds { get; private set; }

        /// <summary>
        ///     Gets the union of boxes.
        /// </summary>
        /// <param name="boxes">The boxes, some null.</param>
        /// <returns>The union, or null.</returns>
        public static SceneRect? Union(params SceneRect?[] boxes)
        {
            SceneRect? result = null;
            foreach (var box in boxes)
            {
                if (box is { } b && double.IsFinite(b.X) && double.IsFinite(b.Y) && double.IsFinite(b.Width) && double.IsFinite(b.Height))
                {
                    result = result is { } r ? r.Union(b) : b;
                }
            }

            return result;
        }

        /// <summary>
        ///     Adds items and their box.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="box">Their box, or null.</param>
        public void Add(IEnumerable<SceneItem> items, SceneRect? box)
        {
            Items.AddRange(items);
            Bounds = Union(Bounds, box);
        }
    }
}
