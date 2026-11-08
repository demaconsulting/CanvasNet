// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <summary>
///     A text of the grammar with its type (Mermaid's <c>FlowText</c>).
/// </summary>
/// <param name="Text">The text as written (a quoted or markdown string without its delimiters).</param>
/// <param name="Type">The type: <c>text</c>, <c>string</c> or <c>markdown</c>.</param>
internal sealed record FlowText(string Text, string Type);

/// <summary>
///     The value of the <c>link</c> rule of the grammar: what <c>destructLink</c> made of the link token, its text and its
///     id.
/// </summary>
/// <param name="Type">The arrow type, or <c>INVALID</c>.</param>
/// <param name="Stroke">The stroke, or <c>INVALID</c>.</param>
/// <param name="Length">The length (strokes or dots), or null for an invalid link.</param>
/// <param name="Text">The link text, or null.</param>
/// <param name="Id">The id with its <c>@</c> (<c>e1@</c>), or null.</param>
internal sealed record FlowLink(string Type, string Stroke, int? Length, FlowText? Text, string? Id);

/// <summary>
///     The text of an <c>@{ ... }</c> block as the lexer delivers it, with the map of its characters to the original
///     text.
/// </summary>
/// <param name="Text">The concatenated shape-data text.</param>
/// <param name="Locate">Locates a position of <see cref="Text"/> in the original text.</param>
internal sealed record ShapeDataText(string Text, Func<int, MermaidLocation> Locate);

/// <summary>
///     One value of a statement list of the grammar (a <c>document</c>): a node or subgraph id, the text of a line token,
///     or a <c>direction</c> statement.
/// </summary>
/// <param name="Text">The text, or null for a direction.</param>
/// <param name="Direction">The direction of a <c>direction</c> statement, or null.</param>
internal readonly record struct DocumentItem(string? Text, string? Direction);

/// <summary>
///     A vertex of the flowchart database (Mermaid's <c>FlowVertex</c>).
/// </summary>
/// <remarks>
///     Vertices are changed only while the text is parsed; afterwards the model is read only.
/// </remarks>
internal sealed class FlowVertex
{
    /// <summary>
    ///     Initializes a vertex.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="domId">The DOM id.</param>
    /// <param name="location">The location of its first mention.</param>
    public FlowVertex(string id, string domId, MermaidLocation location)
    {
        Id = id;
        DomId = domId;
        Location = location;
    }

    /// <summary>Gets the id.</summary>
    public string Id { get; }

    /// <summary>Gets the DOM id (<c>flowchart-</c>, the id, a dash and the vertex counter).</summary>
    public string DomId { get; }

    /// <summary>Gets the location of the first mention.</summary>
    public MermaidLocation Location { get; }

    /// <summary>Gets or sets the label text.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the label type.</summary>
    public string LabelType { get; set; } = "text";

    /// <summary>Gets or sets the vertex type (a bracket shape such as <c>square</c>, or a shape name), or null.</summary>
    public string? Type { get; set; }

    /// <summary>Gets the inline styles.</summary>
    public List<string> Styles { get; } = [];

    /// <summary>Gets the classes.</summary>
    public List<string> Classes { get; } = [];

    /// <summary>Gets the properties of a <c>[|field:value|...]</c> vertex, in JavaScript key order.</summary>
    public ObjectKeyMap<string> Props { get; } = new();

    /// <summary>Gets or sets the icon of <c>@{ icon }</c>, or null.</summary>
    public string? Icon { get; set; }

    /// <summary>Gets or sets the icon form of <c>@{ form }</c>, or null.</summary>
    public string? Form { get; set; }

    /// <summary>Gets or sets the label position of <c>@{ pos }</c>, or null.</summary>
    public string? Pos { get; set; }

    /// <summary>Gets or sets the image of <c>@{ img }</c>, or null.</summary>
    public string? Img { get; set; }

    /// <summary>Gets or sets the aspect constraint of <c>@{ constraint }</c>, or null.</summary>
    public string? Constraint { get; set; }

    /// <summary>Gets or sets the asset width of <c>@{ w }</c>, or null.</summary>
    public double? AssetWidth { get; set; }

    /// <summary>Gets or sets the asset height of <c>@{ h }</c>, or null.</summary>
    public double? AssetHeight { get; set; }
}

/// <summary>
///     An edge of the flowchart database (Mermaid's <c>FlowEdge</c>).
/// </summary>
/// <remarks>
///     Edges are changed only while the text is parsed; afterwards the model is read only.
/// </remarks>
internal sealed class FlowEdge
{
    /// <summary>
    ///     Initializes an edge.
    /// </summary>
    /// <param name="start">The start node id.</param>
    /// <param name="end">The end node id.</param>
    /// <param name="location">The location of the link.</param>
    public FlowEdge(string start, string end, MermaidLocation location)
    {
        Start = start;
        End = end;
        Location = location;
    }

    /// <summary>Gets the start node id.</summary>
    public string Start { get; }

    /// <summary>Gets the end node id.</summary>
    public string End { get; }

    /// <summary>Gets the location of the link.</summary>
    public MermaidLocation Location { get; }

    /// <summary>Gets or sets the arrow type.</summary>
    public string? Type { get; set; }

    /// <summary>Gets or sets the stroke.</summary>
    public string? Stroke { get; set; }

    /// <summary>Gets or sets the length (at most 10), or null.</summary>
    public int? Length { get; set; }

    /// <summary>Gets or sets the label text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the label type.</summary>
    public string LabelType { get; set; } = "text";

    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the id was given in the text.</summary>
    public bool IsUserDefinedId { get; set; }

    /// <summary>Gets or sets the curve, or null.</summary>
    public string? Interpolate { get; set; }

    /// <summary>Gets or sets the styles of <c>linkStyle</c> (a list that several edges of one statement share), or null.</summary>
    public List<string>? Style { get; set; }

    /// <summary>Gets the classes.</summary>
    public List<string> Classes { get; } = [];

    /// <summary>Gets or sets the <c>animate</c> value, or null when it is not set.</summary>
    public YamlNode? Animate { get; set; }

    /// <summary>Gets or sets the <c>animation</c> value, or null when it is not set.</summary>
    public YamlNode? Animation { get; set; }
}

/// <summary>
///     A subgraph of the flowchart database (Mermaid's <c>FlowSubGraph</c>).
/// </summary>
/// <remarks>
///     Subgraphs are changed only while the text is parsed; afterwards the model is read only.
/// </remarks>
internal sealed class FlowSubgraph
{
    /// <summary>
    ///     Initializes a subgraph.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="nodes">The member ids.</param>
    /// <param name="title">The title.</param>
    /// <param name="labelType">The label type of the title.</param>
    /// <param name="direction">The direction of a <c>direction</c> statement inside it, or null.</param>
    /// <param name="location">The location of the <c>subgraph</c> keyword.</param>
    public FlowSubgraph(string id, IReadOnlyList<string> nodes, string title, string labelType, string? direction, MermaidLocation location)
    {
        Id = id;
        Nodes = nodes;
        Title = title;
        LabelType = labelType;
        Direction = direction;
        Location = location;
    }

    /// <summary>Gets the id.</summary>
    public string Id { get; }

    /// <summary>Gets the member ids (vertices and subgraphs), without members of earlier subgraphs.</summary>
    public IReadOnlyList<string> Nodes { get; }

    /// <summary>Gets the title.</summary>
    public string Title { get; }

    /// <summary>Gets the label type of the title.</summary>
    public string LabelType { get; }

    /// <summary>Gets the direction of a <c>direction</c> statement inside it, or null (see <see cref="FlowchartModel.SubgraphDirection"/>).</summary>
    public string? Direction { get; }

    /// <summary>Gets the location of the <c>subgraph</c> keyword.</summary>
    public MermaidLocation Location { get; }

    /// <summary>Gets the classes.</summary>
    public List<string> Classes { get; } = [];

    /// <summary>Gets the metadata of <c>id@{ ... }</c> (for example <c>view: collapsed</c>), in key order, or null.</summary>
    public List<YamlEntry>? Metadata { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the subgraph is drawn collapsed (<c>@{ view: collapsed }</c>).
    /// </summary>
    public bool IsCollapsed =>
        Metadata?.LastOrDefault(entry => entry.Key == "view")?.Value is YamlScalar { Value: "collapsed" };
}

/// <summary>
///     A class of <c>classDef</c> (Mermaid's <c>FlowClass</c>).
/// </summary>
/// <param name="Id">The class name.</param>
internal sealed record FlowClass(string Id)
{
    /// <summary>Gets the styles.</summary>
    public List<string> Styles { get; } = [];

    /// <summary>Gets the text styles (the styles that set a color, with <c>fill</c> renamed <c>bgFill</c>).</summary>
    public List<string> TextStyles { get; } = [];
}
