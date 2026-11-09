// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     What a diagram model hands to the unified renderer: Mermaid's <c>LayoutData</c> (<c>rendering-util/types.ts</c>),
///     the nodes (clusters included), the edges and the drawing settings of one render.
/// </summary>
/// <remarks>
///     <para>
///         A diagram's <c>getData()</c> (the flowchart's <c>FlowchartModel.GetData</c>) builds it from the parsed model
///         and the effective configuration of the call, and the renderer sizes, lays out and draws it. As in Mermaid, a
///         cluster (a subgraph) is a node with <see cref="LayoutNode.IsGroup"/> set, and a node's
///         <see cref="LayoutNode.ParentId"/> names the cluster it is in.
///     </para>
///     <para>
///         The records are immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Nodes">The nodes and clusters, in Mermaid's order (clusters first, last declared first, then the vertices).</param>
/// <param name="Edges">The edges in declaration order.</param>
/// <param name="Config">The effective configuration of the render.</param>
internal sealed record LayoutData(IReadOnlyList<LayoutNode> Nodes, IReadOnlyList<LayoutEdge> Edges, MermaidConfig Config)
{
    /// <summary>
    ///     Gets the diagram type as Mermaid names it (<c>flowchart-v2</c>, <c>classDiagram</c>, the legacy
    ///     <c>class</c>, ...); it decides whether self-loops are merged into one compact loop.
    /// </summary>
    public required string DiagramType { get; init; }

    /// <summary>Gets the layout algorithm (always <c>dagre</c>; other layouts fall back to it).</summary>
    public string LayoutAlgorithm { get; init; } = "dagre";

    /// <summary>Gets the direction of the diagram (<c>TB</c>, <c>BT</c>, <c>LR</c>, <c>RL</c>, or what the header gave).</summary>
    public string? Direction { get; init; }

    /// <summary>Gets the space between nodes of a rank (<c>nodeSpacing || 50</c>).</summary>
    public double NodeSpacing { get; init; } = 50;

    /// <summary>Gets the space between ranks (<c>rankSpacing || 50</c>).</summary>
    public double RankSpacing { get; init; } = 50;

    /// <summary>Gets the edge markers the diagram defines (<c>point</c>, <c>circle</c> and <c>cross</c> for a flowchart).</summary>
    public IReadOnlyList<string> Markers { get; init; } = [];

    /// <summary>Gets the clusters: the nodes that are groups, in node order.</summary>
    public IEnumerable<LayoutNode> Clusters => Nodes.Where(node => node.IsGroup);
}

/// <summary>
///     A node or a cluster of <see cref="LayoutData"/> (Mermaid's <c>Node</c>).
/// </summary>
/// <remarks>
///     A property that Mermaid leaves <c>undefined</c> is null. Labels are HTML when the source text contained markup
///     (Mermaid sanitizes them with DOMPurify), and markdown when <see cref="LabelType"/> is <c>markdown</c>.
/// </remarks>
internal sealed record LayoutNode
{
    /// <summary>Gets the node id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the label text.</summary>
    public string? Label { get; init; }

    /// <summary>Gets the label type: <c>text</c>, <c>string</c> or <c>markdown</c>.</summary>
    public string? LabelType { get; init; }

    /// <summary>Gets the style of the label (always empty for a flowchart).</summary>
    public string LabelStyle { get; init; } = string.Empty;

    /// <summary>Gets the id of the cluster the node is in, or null.</summary>
    public string? ParentId { get; init; }

    /// <summary>Gets the padding around the label.</summary>
    public double Padding { get; init; }

    /// <summary>Gets the inline styles of the node (<c>style</c> statements), or null for a cluster.</summary>
    public IReadOnlyList<string>? CssStyles { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the node's styles are written by the diagram itself (a class note's colors, a
    ///     lollipop interface's opacity) rather than by the user: they are applied without reports.
    /// </summary>
    public bool TrustedStyles { get; init; }

    /// <summary>Gets the styles of the node's classes (<c>classDef</c>), compiled in class order.</summary>
    public IReadOnlyList<string> CssCompiledStyles { get; init; } = [];

    /// <summary>Gets the CSS classes, separated by spaces.</summary>
    public string CssClasses { get; init; } = string.Empty;

    /// <summary>Gets the direction of a cluster, or null.</summary>
    public string? Dir { get; init; }

    /// <summary>Gets the DOM id Mermaid gives a vertex (<c>flowchart-A-0</c>), or null for a cluster.</summary>
    public string? DomId { get; init; }

    /// <summary>Gets the look (<c>classic</c>, or a look drawn classic).</summary>
    public string? Look { get; init; }

    /// <summary>Gets a value indicating whether the node is a cluster.</summary>
    public bool IsGroup { get; init; }

    /// <summary>Gets the shape name (a key of <see cref="ShapeNames"/>, or a legacy name such as <c>ellipse</c>).</summary>
    public required string Shape { get; init; }

    /// <summary>Gets the icon name of <c>@{ icon }</c> (not drawn), or null.</summary>
    public string? Icon { get; init; }

    /// <summary>Gets the label position of an icon or image node (<c>t</c> or <c>b</c>), or null.</summary>
    public string? Pos { get; init; }

    /// <summary>Gets the image reference of <c>@{ img }</c>, or null.</summary>
    public string? Img { get; init; }

    /// <summary>Gets the image width of <c>@{ w }</c> (NaN when it is not a number), or null.</summary>
    public double? AssetWidth { get; init; }

    /// <summary>Gets the image height of <c>@{ h }</c> (NaN when it is not a number), or null.</summary>
    public double? AssetHeight { get; init; }

    /// <summary>Gets the aspect constraint of an image (<c>on</c> or <c>off</c>), or null.</summary>
    public string? Constraint { get; init; }

    /// <summary>Gets the texts of a class box (a node of shape <c>classBox</c>), or null for any other node.</summary>
    public LayoutClassBox? ClassBox { get; init; }

    /// <summary>Gets the location of the node's first mention in the text, for reports.</summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     The texts of a class box as Mermaid's <c>classBox</c> shape reads them from a class node: the drawn name, the
///     annotations, the attributes and the methods.
/// </summary>
/// <remarks>
///     The texts are as the class database holds them (Mermaid's <c>ClassNode.text</c> and <c>ClassMember.text</c>):
///     generics already written as <c>&amp;lt;T&amp;gt;</c> and a visibility escaped with a backslash; the shape decodes
///     and draws them. The record is immutable.
/// </remarks>
/// <param name="Text">The class name as drawn (<c>ClassNode.text</c>: the name with its generic type).</param>
/// <param name="Annotations">The annotations in order, without the angle quotes Mermaid draws around them (only the first is drawn).</param>
/// <param name="Members">The attributes in order.</param>
/// <param name="Methods">The methods in order.</param>
/// <param name="Styles">The class's own styles (<c>ClassNode.styles</c>: its <c>style</c> statements and class styles).</param>
internal sealed record LayoutClassBox(
    string Text,
    IReadOnlyList<string> Annotations,
    IReadOnlyList<LayoutClassMember> Members,
    IReadOnlyList<LayoutClassMember> Methods,
    IReadOnlyList<string> Styles);

/// <summary>
///     An attribute or a method of a class box: its text and the style of its classifier.
/// </summary>
/// <param name="Text">The member's text as the class database holds it (<c>ClassMember.text</c>).</param>
/// <param name="CssStyle">
///     The style of its classifier (<c>parseClassifier()</c>): <c>font-style:italic;</c> for an abstract member,
///     <c>text-decoration:underline;</c> for a static one, else empty.
/// </param>
internal sealed record LayoutClassMember(string Text, string CssStyle);

/// <summary>
///     An edge of <see cref="LayoutData"/> (Mermaid's <c>Edge</c>).
/// </summary>
/// <remarks>
///     A property that Mermaid leaves <c>undefined</c> is null.
/// </remarks>
internal sealed record LayoutEdge
{
    /// <summary>Gets the edge id (<c>L_A_B_0</c>, or the id given with <c>e1@</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Gets a value indicating whether the id was given in the text.</summary>
    public bool IsUserDefinedId { get; init; }

    /// <summary>Gets the id of the start node.</summary>
    public required string Start { get; init; }

    /// <summary>Gets the id of the end node.</summary>
    public required string End { get; init; }

    /// <summary>Gets the arrow type (<c>arrow_point</c>, <c>double_arrow_cross</c>, <c>arrow_open</c>, ...).</summary>
    public required string Type { get; init; }

    /// <summary>Gets the label text (empty when there is none).</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the label type: <c>text</c>, <c>string</c> or <c>markdown</c>.</summary>
    public string LabelType { get; init; } = "text";

    /// <summary>Gets the label position (<c>c</c>).</summary>
    public string LabelPos { get; init; } = "c";

    /// <summary>Gets the stroke: <c>normal</c>, <c>thick</c>, <c>dotted</c>, <c>invisible</c> (or <c>INVALID</c>).</summary>
    public string? Thickness { get; init; }

    /// <summary>Gets the minimum rank span (the link length, at most 10), or null for an invalid link.</summary>
    public int? MinLen { get; init; }

    /// <summary>Gets the CSS classes of the path.</summary>
    public string Classes { get; init; } = string.Empty;

    /// <summary>Gets the marker at the start (<c>none</c>, <c>arrow_point</c>, <c>arrow_circle</c> or <c>arrow_cross</c>).</summary>
    public string ArrowTypeStart { get; init; } = "none";

    /// <summary>Gets the marker at the end.</summary>
    public string ArrowTypeEnd { get; init; } = "none";

    /// <summary>Gets the style of the arrow heads.</summary>
    public string ArrowheadStyle { get; init; } = string.Empty;

    /// <summary>Gets the styles of the edge's classes, compiled in class order.</summary>
    public IReadOnlyList<string> CssCompiledStyles { get; init; } = [];

    /// <summary>Gets the label styles (the same list as <see cref="Style"/>).</summary>
    public IReadOnlyList<string> LabelStyle { get; init; } = [];

    /// <summary>Gets the styles: the <c>linkStyle default</c> styles, then the edge's own.</summary>
    public IReadOnlyList<string> Style { get; init; } = [];

    /// <summary>Gets the stroke pattern (the same value as <see cref="Thickness"/>).</summary>
    public string? Pattern { get; init; }

    /// <summary>Gets the look.</summary>
    public string? Look { get; init; }

    /// <summary>Gets the <c>animate</c> value of <c>e1@{ animate: ... }</c> as written, or null.</summary>
    public YamlNode? Animate { get; init; }

    /// <summary>Gets the <c>animation</c> value of <c>e1@{ animation: ... }</c> as written, or null.</summary>
    public YamlNode? Animation { get; init; }

    /// <summary>Gets the curve: the edge's own, else the <c>linkStyle default</c> one, else the configured one.</summary>
    public string? Curve { get; init; }

    /// <summary>
    ///     Gets the terminal label beside the start of the edge (Mermaid's <c>startLabelRight</c>: a class relation's
    ///     first cardinality), empty for none.
    /// </summary>
    public string StartLabelRight { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the terminal label beside the end of the edge (Mermaid's <c>endLabelLeft</c>: a class relation's second
    ///     cardinality), empty for none.
    /// </summary>
    public string EndLabelLeft { get; init; } = string.Empty;

    /// <summary>Gets the location of the link in the text, for reports.</summary>
    public MermaidLocation Location { get; init; }
}
