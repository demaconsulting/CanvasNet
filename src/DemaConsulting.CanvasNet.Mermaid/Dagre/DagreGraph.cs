// Portions derived from dagre-d3-es (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The graph label of a graph given to <see cref="DagreLayout.Run{TNode,TEdge}"/>: dagre's graph attributes, and the
///     size of the drawing after the layout.
/// </summary>
/// <remarks>
///     A null attribute is one the caller did not set, so dagre's default applies (<c>ranksep</c> 50, <c>edgesep</c> 20,
///     <c>nodesep</c> 50, <c>rankdir</c> <c>tb</c>, no margins). A number that is set is used as given; dagre turns a key
///     that is present but undefined into NaN, so a caller that mirrors such a key passes <see cref="double.NaN"/>.
///     <see cref="RankDir"/> is <c>TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c> in either case; note that dagre compares it
///     in upper case when it makes room for edge labels and in lower case everywhere else, as Mermaid's upper-case value
///     relies on.
/// </remarks>
internal sealed class DagreGraph
{
    /// <summary>Gets or sets the horizontal space between nodes (<c>nodesep</c>).</summary>
    public double? NodeSep { get; set; }

    /// <summary>Gets or sets the horizontal space between edges (<c>edgesep</c>).</summary>
    public double? EdgeSep { get; set; }

    /// <summary>Gets or sets the space between ranks (<c>ranksep</c>).</summary>
    public double? RankSep { get; set; }

    /// <summary>Gets or sets the horizontal margin of the drawing (<c>marginx</c>).</summary>
    public double? MarginX { get; set; }

    /// <summary>Gets or sets the vertical margin of the drawing (<c>marginy</c>).</summary>
    public double? MarginY { get; set; }

    /// <summary>Gets or sets the cycle breaker (<c>acyclicer</c>): <c>greedy</c>, or null for the depth-first one.</summary>
    public string? Acyclicer { get; set; }

    /// <summary>
    ///     Gets or sets the ranker (<c>ranker</c>): <c>network-simplex</c> (the default), <c>tight-tree</c> or
    ///     <c>longest-path</c>.
    /// </summary>
    public string? Ranker { get; set; }

    /// <summary>Gets or sets the rank direction (<c>rankdir</c>).</summary>
    public string? RankDir { get; set; }

    /// <summary>
    ///     Gets or sets the alignment (<c>align</c>): <c>UL</c>, <c>UR</c>, <c>DL</c> or <c>DR</c> picks one of the four
    ///     Brandes-Koepf alignments; null balances them.
    /// </summary>
    public string? Align { get; set; }

    /// <summary>Gets or sets the width of the drawing, set by the layout.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height of the drawing, set by the layout.</summary>
    public double Height { get; set; }
}

/// <summary>
///     The label of a node of a graph given to <see cref="DagreLayout.Run{TNode,TEdge}"/>: its size, and its center after
///     the layout.
/// </summary>
/// <remarks>
///     The type is not sealed so that a caller can carry its own data on the node. A node with children (a cluster) gets
///     its size from the layout; the size of a leaf is kept.
/// </remarks>
internal class DagreNode
{
    /// <summary>Gets or sets the width; null is zero.</summary>
    public double? Width { get; set; }

    /// <summary>Gets or sets the height; null is zero.</summary>
    public double? Height { get; set; }

    /// <summary>Gets or sets the x coordinate of the center, set by the layout.</summary>
    public double X { get; set; }

    /// <summary>Gets or sets the y coordinate of the center, set by the layout.</summary>
    public double Y { get; set; }
}

/// <summary>
///     The label of an edge of a graph given to <see cref="DagreLayout.Run{TNode,TEdge}"/>: its layout attributes, and its
///     route and label position after the layout.
/// </summary>
/// <remarks>
///     The type is not sealed so that a caller can carry its own data on the edge. A null attribute takes dagre's default
///     (<c>minlen</c> 1, <c>weight</c> 1, <c>width</c> and <c>height</c> 0, <c>labeloffset</c> 10, <c>labelpos</c>
///     <c>r</c>).
/// </remarks>
internal class DagreEdge
{
    /// <summary>Gets or sets the minimum number of ranks the edge spans (<c>minlen</c>).</summary>
    public double? MinLen { get; set; }

    /// <summary>Gets or sets the weight that pulls the ends together (<c>weight</c>).</summary>
    public double? Weight { get; set; }

    /// <summary>Gets or sets the width of the edge label (<c>width</c>).</summary>
    public double? Width { get; set; }

    /// <summary>Gets or sets the height of the edge label (<c>height</c>).</summary>
    public double? Height { get; set; }

    /// <summary>Gets or sets the space between the edge and a label at its left or right (<c>labeloffset</c>).</summary>
    public double? LabelOffset { get; set; }

    /// <summary>Gets or sets where the label sits on the edge (<c>labelpos</c>): <c>l</c>, <c>c</c> or <c>r</c>.</summary>
    public string? LabelPos { get; set; }

    /// <summary>Gets or sets the route of the edge, set by the layout.</summary>
    public List<DagrePoint>? Points { get; set; }

    /// <summary>Gets or sets the x coordinate of the label center, set by the layout for a labeled edge.</summary>
    public double? X { get; set; }

    /// <summary>Gets or sets the y coordinate of the label center, set by the layout for a labeled edge.</summary>
    public double? Y { get; set; }
}

/// <summary>
///     A point of a layout: mutable, as the stages move points in place.
/// </summary>
/// <param name="x">The x coordinate.</param>
/// <param name="y">The y coordinate.</param>
internal sealed class DagrePoint(double x, double y)
{
    /// <summary>Gets or sets the x coordinate.</summary>
    public double X { get; set; } = x;

    /// <summary>Gets or sets the y coordinate.</summary>
    public double Y { get; set; } = y;
}
