// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The graph label of dagre's internal layout graph: the attributes with their defaults applied, and the state the
///     stages leave on the graph.
/// </summary>
/// <remarks>
///     A nullable member is one that dagre may leave undefined; the stages treat null as JavaScript treats
///     <c>undefined</c>.
/// </remarks>
internal sealed class LayoutGraphLabel
{
    /// <summary>Gets or sets the space between ranks (halved for edge labels).</summary>
    public double RankSep { get; set; } = 50;

    /// <summary>Gets or sets the space between edges.</summary>
    public double EdgeSep { get; set; } = 20;

    /// <summary>Gets or sets the space between nodes.</summary>
    public double NodeSep { get; set; } = 50;

    /// <summary>Gets or sets the horizontal margin, or null.</summary>
    public double? MarginX { get; set; }

    /// <summary>Gets or sets the vertical margin, or null.</summary>
    public double? MarginY { get; set; }

    /// <summary>Gets or sets the cycle breaker, or null.</summary>
    public string? Acyclicer { get; set; }

    /// <summary>Gets or sets the ranker, or null.</summary>
    public string? Ranker { get; set; }

    /// <summary>Gets or sets the rank direction; <c>tb</c> by default.</summary>
    public string? RankDir { get; set; } = "tb";

    /// <summary>Gets or sets the alignment, or null.</summary>
    public string? Align { get; set; }

    /// <summary>Gets or sets the dummy root of the nesting graph while it exists.</summary>
    public string? NestingRoot { get; set; }

    /// <summary>Gets or sets the rank multiplier of the nesting graph.</summary>
    public double? NodeRankFactor { get; set; }

    /// <summary>Gets or sets the first dummy node of each long edge.</summary>
    public List<string> DummyChains { get; set; } = [];

    /// <summary>Gets or sets the greatest rank of a cluster (left undefined by dagre as soon as a cluster exists).</summary>
    public double? MaxRank { get; set; }

    /// <summary>Gets or sets the width of the drawing.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height of the drawing.</summary>
    public double Height { get; set; }
}

/// <summary>
///     The label of a node of dagre's internal layout graph: a real node, a cluster or one of the dummy nodes the stages
///     add.
/// </summary>
/// <remarks>
///     Every property a stage may read or write is here. Nullable members mirror properties that dagre adds only to some
///     nodes; <see cref="X"/> and <see cref="Y"/> start as NaN, which behaves like <c>undefined</c> in arithmetic.
/// </remarks>
internal sealed class LayoutNode
{
    /// <summary>Gets or sets the width.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the x coordinate of the center.</summary>
    public double X { get; set; } = double.NaN;

    /// <summary>Gets or sets the y coordinate of the center.</summary>
    public double Y { get; set; } = double.NaN;

    /// <summary>Gets or sets the rank, or null for a cluster (which is not ranked).</summary>
    public double? Rank { get; set; }

    /// <summary>Gets or sets the position in the rank.</summary>
    public int Order { get; set; }

    /// <summary>
    ///     Gets or sets the kind of a dummy node (<c>edge</c>, <c>edge-label</c>, <c>edge-proxy</c>, <c>selfedge</c>,
    ///     <c>border</c>, <c>root</c>), or null for a real node.
    /// </summary>
    public string? Dummy { get; set; }

    /// <summary>Gets or sets the edge an edge-proxy or self-edge dummy stands for.</summary>
    public GraphEdge? E { get; set; }

    /// <summary>Gets or sets the label of the self edge a self-edge dummy stands for.</summary>
    public LayoutEdge? Label { get; set; }

    /// <summary>Gets or sets the label of the long edge an edge dummy belongs to.</summary>
    public LayoutEdge? EdgeLabel { get; set; }

    /// <summary>Gets or sets the long edge an edge dummy belongs to.</summary>
    public GraphEdge? EdgeObj { get; set; }

    /// <summary>Gets or sets the label position of an edge-label dummy (present only on those).</summary>
    public string? LabelPos { get; set; }

    /// <summary>Gets or sets the top border node of a cluster.</summary>
    public string? BorderTop { get; set; }

    /// <summary>Gets or sets the bottom border node of a cluster.</summary>
    public string? BorderBottom { get; set; }

    /// <summary>Gets or sets the left border node of a cluster by rank (a JavaScript array indexed by rank).</summary>
    public List<string?>? BorderLeft { get; set; }

    /// <summary>Gets or sets the right border node of a cluster by rank.</summary>
    public List<string?>? BorderRight { get; set; }

    /// <summary>Gets or sets the side of a left or right border node (<c>borderLeft</c> or <c>borderRight</c>).</summary>
    public string? BorderType { get; set; }

    /// <summary>Gets or sets the first rank of a cluster (present only on clusters).</summary>
    public double? MinRank { get; set; }

    /// <summary>Gets or sets the last rank of a cluster.</summary>
    public double? MaxRank { get; set; }

    /// <summary>Gets or sets the self edges removed from the node until they are drawn again.</summary>
    public List<SelfEdge>? SelfEdges { get; set; }
}

/// <summary>
///     The label of an edge of dagre's internal layout graph.
/// </summary>
/// <remarks>
///     Edges added by the stages carry only the members they need (for example only <see cref="Weight"/>); the others
///     keep their defaults, which no later stage reads.
/// </remarks>
internal sealed class LayoutEdge
{
    /// <summary>Gets or sets the minimum rank span.</summary>
    public double MinLen { get; set; }

    /// <summary>Gets or sets the weight.</summary>
    public double Weight { get; set; }

    /// <summary>Gets or sets the label width.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the label height.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the label offset.</summary>
    public double LabelOffset { get; set; }

    /// <summary>Gets or sets the label position, or null on an edge added by a stage.</summary>
    public string? LabelPos { get; set; }

    /// <summary>Gets or sets the rank of the label, or null.</summary>
    public double? LabelRank { get; set; }

    /// <summary>Gets or sets the route, or null.</summary>
    public List<DagrePoint>? Points { get; set; }

    /// <summary>Gets or sets the x coordinate of the label, or null when the edge has no label node.</summary>
    public double? X { get; set; }

    /// <summary>Gets or sets the y coordinate of the label, or null.</summary>
    public double? Y { get; set; }

    /// <summary>Gets or sets whether the edge was reversed to break a cycle.</summary>
    public bool Reversed { get; set; }

    /// <summary>Gets or sets the name of a reversed edge before it was reversed.</summary>
    public string? ForwardName { get; set; }

    /// <summary>Gets or sets whether the edge belongs to the nesting graph.</summary>
    public bool NestingEdge { get; set; }
}

/// <summary>
///     A self edge removed from its node during the layout.
/// </summary>
/// <param name="E">The edge.</param>
/// <param name="Label">Its label.</param>
internal sealed record SelfEdge(GraphEdge E, LayoutEdge Label);
