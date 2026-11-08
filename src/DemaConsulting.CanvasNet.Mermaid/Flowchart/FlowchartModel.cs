// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <summary>
///     A parsed flowchart: a port of Mermaid's flowchart database (<c>flowDb.ts</c>), which the parser fills statement
///     by statement and which maps to the renderer's <see cref="LayoutData"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> Every operation the grammar calls is ported with Mermaid's semantics: how a vertex declared
///         again merges into the first declaration (text, type, styles, properties and shape data), the DOM ids with the
///         vertex counter, the shape data of <c>@{ ... }</c> read as YAML (for a vertex, an edge or a subgraph), the edge
///         ids (<c>L_start_end_n</c>, or <c>e1@</c>), the link types of <c>destructLink</c>, subgraph members and ids
///         (members of an earlier subgraph are removed from a later one), <c>classDef</c>, <c>class</c> and
///         <c>:::</c>, <c>style</c>, <c>linkStyle</c> (with <c>default</c> and <c>interpolate</c>), the labels cleaned
///         as Mermaid's <c>sanitizeText</c> does (<see cref="HtmlSanitizer"/>), and the accessible title and description.
///         <c>click</c>, <c>href</c> and <c>call</c> only add the <c>clickable</c> class, as they do in Mermaid; their links,
///         callbacks and tooltips are not kept, since nothing is interactive.
///     </para>
///     <para>
///         <b>Limits.</b> <see cref="MermaidLimits.MaxEdges"/> (Mermaid's <c>maxEdges</c>), <see cref="MermaidLimits.MaxNodes"/>
///         (vertices and subgraphs) and <see cref="MermaidLimits.MaxLabelLength"/> (labels, titles and the accessibility
///         texts) are enforced as the elements are
///         added; a breach is a located <see cref="MermaidSyntaxException"/> with code
///         <see cref="MermaidDiagnosticCodes.LimitExceeded"/>. Errors that Mermaid raises while it fills its database (an
///         unknown shape, a <c>linkStyle</c> index out of range, a subgraph without a name) are located
///         <see cref="MermaidSyntaxException"/>s too.
///     </para>
///     <para>
///         The model is filled by one parser on one thread; once parsing is over it is never changed, so it is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal sealed partial class FlowchartModel : IMermaidDiagram
{
    /// <summary>The prefix of the DOM id of a vertex.</summary>
    private const string DomIdPrefix = "flowchart-";

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The vertices in insertion order.</summary>
    private readonly List<FlowVertex> _vertices = [];

    /// <summary>The vertices by id.</summary>
    private readonly Dictionary<string, FlowVertex> _vertexById = new(StringComparer.Ordinal);

    /// <summary>The edges in insertion order.</summary>
    private readonly List<FlowEdge> _edges = [];

    /// <summary>The first edge of each id.</summary>
    private readonly Dictionary<string, FlowEdge> _edgeById = new(StringComparer.Ordinal);

    /// <summary>The number of edges of each start and end pair.</summary>
    private readonly Dictionary<(string Start, string End), int> _edgePairs = [];

    /// <summary>The classes in insertion order.</summary>
    private readonly List<FlowClass> _classes = [];

    /// <summary>The classes by name.</summary>
    private readonly Dictionary<string, FlowClass> _classById = new(StringComparer.Ordinal);

    /// <summary>The subgraphs in the order they end (inner subgraphs first).</summary>
    private readonly List<FlowSubgraph> _subgraphs = [];

    /// <summary>The last subgraph of each id.</summary>
    private readonly Dictionary<string, FlowSubgraph> _subgraphById = new(StringComparer.Ordinal);

    /// <summary>The ids that are members of a subgraph already.</summary>
    private readonly HashSet<string> _members = new(StringComparer.Ordinal);

    /// <summary>The vertex counter of the DOM ids (Mermaid's <c>vertexCounter</c>).</summary>
    private int _vertexCounter;

    /// <summary>The counter of the generated subgraph ids (Mermaid's <c>subCount</c>).</summary>
    private int _subgraphCounter;

    /// <summary>Whether the next header keyword is the first (Mermaid's <c>firstGraphFlag</c>).</summary>
    private bool _firstGraph = true;

    /// <summary>The direction as set by the header, before trimming.</summary>
    private string? _direction;

    /// <summary>
    ///     Initializes an empty model.
    /// </summary>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    public FlowchartModel(MermaidLimits limits, MermaidLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        KeywordLocation = keywordLocation;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets the vertices in insertion order.</summary>
    public IReadOnlyList<FlowVertex> Vertices => _vertices;

    /// <summary>Gets the edges in declaration order.</summary>
    public IReadOnlyList<FlowEdge> Edges => _edges;

    /// <summary>Gets the classes of <c>classDef</c> in declaration order.</summary>
    public IReadOnlyList<FlowClass> Classes => _classes;

    /// <summary>Gets the subgraphs in the order they end (inner subgraphs before the subgraph around them).</summary>
    public IReadOnlyList<FlowSubgraph> Subgraphs => _subgraphs;

    /// <summary>Gets the curve of <c>linkStyle default interpolate</c>, or null.</summary>
    public string? DefaultInterpolate { get; private set; }

    /// <summary>Gets the styles of <c>linkStyle default</c>, or null.</summary>
    public IReadOnlyList<string>? DefaultStyle { get; private set; }

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; private set; } = string.Empty;

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; private set; } = string.Empty;

    /// <inheritdoc />
    public string? Title => null;

    /// <inheritdoc />
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <inheritdoc />
    public string? Direction => _direction is null ? null : Preprocessor.TrimJs(_direction);

    /// <inheritdoc />
    public int NodeCount => _vertices.Count(vertex => !_subgraphById.ContainsKey(vertex.Id));

    /// <inheritdoc />
    public int EdgeCount => _edges.Count;

    /// <inheritdoc />
    public int SubgraphCount => _subgraphs.Count;

    /// <summary>
    ///     Gets a value indicating whether a header keyword is the first, and clears the flag (Mermaid's
    ///     <c>firstGraph</c>, which the lexer calls).
    /// </summary>
    /// <returns>Whether this is the first call.</returns>
    public bool FirstGraph()
    {
        var first = _firstGraph;
        _firstGraph = false;
        return first;
    }

    /// <summary>
    ///     Sets the direction of the header (Mermaid's <c>setDirection</c>): <c>&lt;</c> is <c>RL</c>, <c>^</c> is
    ///     <c>BT</c>, <c>&gt;</c> is <c>LR</c>, <c>v</c> and <c>TD</c> are <c>TB</c>, anything else is kept.
    /// </summary>
    /// <param name="direction">The direction as written.</param>
    public void SetDirection(string direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        var value = Preprocessor.TrimJs(direction);
        if (value.Contains('<', StringComparison.Ordinal))
        {
            value = "RL";
        }

        if (value.Contains('^', StringComparison.Ordinal))
        {
            value = "BT";
        }

        if (value.Contains('>', StringComparison.Ordinal))
        {
            value = "LR";
        }

        if (value.Contains('v', StringComparison.Ordinal))
        {
            value = "TB";
        }

        if (value == "TD")
        {
            value = "TB";
        }

        _direction = value;
    }

    /// <summary>
    ///     Sets the accessible title (Mermaid's <c>setAccTitle</c>: sanitized, leading white space removed).
    /// </summary>
    /// <param name="text">The title.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the title is longer than <see cref="MermaidLimits.MaxLabelLength"/>.</exception>
    public void SetAccessibleTitle(string text, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(text);
        CheckLabel(text, location);
        AccessibleTitle = HtmlSanitizer.Sanitize(text).TrimStart(JsWhitespace());
    }

    /// <summary>
    ///     Sets the accessible description (Mermaid's <c>setAccDescription</c>: sanitized, the white space after each line
    ///     break removed).
    /// </summary>
    /// <param name="text">The description.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the description is longer than <see cref="MermaidLimits.MaxLabelLength"/>.</exception>
    public void SetAccessibleDescription(string text, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(text);
        CheckLabel(text, location);
        var sanitized = HtmlSanitizer.Sanitize(text);
        var builder = new StringBuilder(sanitized.Length);
        var i = 0;
        while (i < sanitized.Length)
        {
            builder.Append(sanitized[i]);
            i++;

            // \n\s+ becomes \n: skip the white space after a line feed (more line feeds included)
            if (sanitized[i - 1] == '\n')
            {
                while (i < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[i]))
                {
                    i++;
                }
            }
        }

        AccessibleDescription = builder.ToString();
    }

    /// <summary>
    ///     Adds or updates a vertex (Mermaid's <c>addVertex</c>), or applies shape data to an edge or a subgraph of the
    ///     same id.
    /// </summary>
    /// <param name="id">The id; an empty or blank id is ignored.</param>
    /// <param name="location">The location of the mention.</param>
    /// <param name="text">The label, or null.</param>
    /// <param name="type">The bracket shape, or null.</param>
    /// <param name="style">The styles of a <c>style</c> statement, or null.</param>
    /// <param name="props">The property of a <c>[|field:value|...]</c> vertex, or null.</param>
    /// <param name="shapeData">The text of an <c>@{ ... }</c> block, or null.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, for invalid shape data, an unknown shape or a limit.</exception>
    public void AddVertex(
        string id,
        MermaidLocation location,
        FlowText? text = null,
        string? type = null,
        IReadOnlyList<string>? style = null,
        KeyValuePair<string, string>? props = null,
        ShapeDataText? shapeData = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (Preprocessor.TrimJs(id).Length == 0)
        {
            return;
        }

        // The shape data is YAML: one line is wrapped as a flow mapping, several lines are read as written
        var hasDoc = false;
        YamlNode? doc = null;
        if (shapeData is not null)
        {
            var multiLine = shapeData.Text.Contains('\n', StringComparison.Ordinal);
            var yaml = multiLine ? shapeData.Text + "\n" : "{\n" + shapeData.Text + "\n}";
            var offset = multiLine ? 0 : 2;
            doc = YamlSubsetParser.Parse(
                yaml,
                position => shapeData.Locate(Math.Clamp(position - offset, 0, Math.Max(shapeData.Text.Length - 1, 0))),
                _limits.MaxYamlDepth);
            hasDoc = true;
        }

        // Shape data of a subgraph declared before (sub1@{ view: collapsed }) is its metadata
        if (_subgraphById.TryGetValue(id, out var subgraph) && doc is { IsTruthy: true })
        {
            MergeMetadata(subgraph, doc);
            return;
        }

        // Shape data of an edge id sets its animation and curve; a mention of an edge id does nothing
        if (_edgeById.TryGetValue(id, out var edge))
        {
            ApplyEdgeData(edge, doc as YamlMapping);
            return;
        }

        var vertex = GetOrCreateVertex(id, location);
        _vertexCounter++;

        // The label replaces the earlier one; a vertex without a label shows its id
        if (text is not null)
        {
            CheckLabel(text.Text, location);
            vertex.Text = StripQuotes(HtmlSanitizer.Sanitize(Preprocessor.TrimJs(text.Text)));
            vertex.LabelType = text.Type;
        }
        else
        {
            vertex.Text ??= id;
        }

        if (type is not null)
        {
            vertex.Type = type;
        }

        if (style is not null)
        {
            vertex.Styles.AddRange(style);
        }

        if (props is { } property)
        {
            vertex.Props[property.Key] = property.Value;
        }

        if (hasDoc)
        {
            ApplyVertexData(vertex, doc);
        }
    }

    /// <summary>
    ///     Adds the edges of a link between two node lists (Mermaid's <c>addLink</c>): every start to every end, the id
    ///     going to the edge from the last start to the first end.
    /// </summary>
    /// <param name="starts">The start ids.</param>
    /// <param name="ends">The end ids.</param>
    /// <param name="link">The link.</param>
    /// <param name="location">The location of the link.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a limit is exceeded.</exception>
    public void AddLink(IReadOnlyList<string> starts, IReadOnlyList<string> ends, FlowLink link, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(starts);
        ArgumentNullException.ThrowIfNull(ends);
        ArgumentNullException.ThrowIfNull(link);

        // Mermaid removes the first '@' of the id
        var id = link.Id is null ? null : RemoveFirst(link.Id, '@');
        foreach (var start in starts)
        {
            foreach (var end in ends)
            {
                var useId = start == starts[^1] && end == ends[0];
                AddSingleLink(start, end, link, useId ? id : null, location);
            }
        }
    }

    /// <summary>
    ///     Sets the curve of edges (Mermaid's <c>updateLinkInterpolate</c>).
    /// </summary>
    /// <param name="positions">The edge indexes as written, or <c>default</c>.</param>
    /// <param name="interpolate">The curve name.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when an index names no edge.</exception>
    public void UpdateLinkInterpolate(IReadOnlyList<string> positions, string interpolate, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(positions);
        foreach (var position in positions)
        {
            if (position == "default")
            {
                DefaultInterpolate = interpolate;
            }
            else
            {
                EdgeAt(position, location).Interpolate = interpolate;
            }
        }
    }

    /// <summary>
    ///     Sets the styles of edges (Mermaid's <c>updateLink</c>): the edges of one statement share one style list, which
    ///     gets <c>fill:none</c> unless a style sets the fill.
    /// </summary>
    /// <param name="positions">The edge indexes as written, or <c>default</c>.</param>
    /// <param name="style">The styles; the list is shared, as in Mermaid.</param>
    /// <param name="location">The location of the statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when an index names no edge.</exception>
    public void UpdateLink(IReadOnlyList<string> positions, List<string> style, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(style);
        foreach (var position in positions)
        {
            if (position == "default")
            {
                DefaultStyle = style;
                continue;
            }

            var edge = EdgeAt(position, location);
            edge.Style = style;
            if (style.Count > 0 && !style.Exists(s => s.StartsWith("fill", StringComparison.Ordinal)))
            {
                style.Add("fill:none");
            }
        }
    }

    /// <summary>
    ///     Defines or extends classes (Mermaid's <c>addClass</c>): the styles are split at commas (a <c>\,</c> stays a
    ///     comma), and a style that mentions a color also becomes a text style.
    /// </summary>
    /// <param name="ids">The class names, separated by commas.</param>
    /// <param name="styles">The styles as the grammar delivers them.</param>
    public void AddClass(string ids, IReadOnlyList<string> styles)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(styles);
        const string Placeholder = "§§§";
        var split = string.Join(",", styles)
            .Replace("\\,", Placeholder, StringComparison.Ordinal)
            .Replace(",", ";", StringComparison.Ordinal)
            .Replace(Placeholder, ",", StringComparison.Ordinal)
            .Split(';');
        foreach (var id in ids.Split(','))
        {
            if (!_classById.TryGetValue(id, out var flowClass))
            {
                flowClass = new FlowClass(id);
                _classById.Add(id, flowClass);
                _classes.Add(flowClass);
            }

            foreach (var style in split)
            {
                if (style.Contains("color", StringComparison.Ordinal))
                {
                    flowClass.TextStyles.Add(ReplaceFirst(style, "fill", "bgFill"));
                }

                flowClass.Styles.Add(style);
            }
        }
    }

    /// <summary>
    ///     Adds a class to vertices, edges and subgraphs (Mermaid's <c>setClass</c>); ids that name nothing are ignored.
    /// </summary>
    /// <param name="ids">The ids, separated by commas.</param>
    /// <param name="className">The class name.</param>
    public void SetClass(string ids, string className)
    {
        ArgumentNullException.ThrowIfNull(ids);
        foreach (var id in ids.Split(','))
        {
            if (_vertexById.TryGetValue(id, out var vertex))
            {
                vertex.Classes.Add(className);
            }

            if (_edgeById.TryGetValue(id, out var edge))
            {
                edge.Classes.Add(className);
            }

            if (_subgraphById.TryGetValue(id, out var subgraph))
            {
                subgraph.Classes.Add(className);
            }
        }
    }

    /// <summary>
    ///     Records a <c>click</c> statement (Mermaid's <c>setClickEvent</c> and <c>setLink</c>): the ids get the
    ///     <c>clickable</c> class; the link, callback and tooltip are not kept.
    /// </summary>
    /// <param name="ids">The ids, separated by commas.</param>
    public void SetClickable(string ids) => SetClass(ids, "clickable");

    /// <summary>
    ///     Adds a subgraph (Mermaid's <c>addSubGraph</c>).
    /// </summary>
    /// <param name="id">The id text, or null when the subgraph has neither id nor title.</param>
    /// <param name="items">The values of the statements inside it.</param>
    /// <param name="title">The title text, or null.</param>
    /// <param name="titleIsId">Whether the id text is also the title (a subgraph written without brackets).</param>
    /// <param name="location">The location of the <c>subgraph</c> keyword.</param>
    /// <returns>The id of the subgraph.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, for a subgraph without id or title, or a limit.</exception>
    public string AddSubgraph(FlowText? id, IReadOnlyList<DocumentItem> items, FlowText? title, bool titleIsId, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Mermaid reads the id of a nameless subgraph from nothing and fails
        if (id is null || title is null)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.SyntaxError,
                "a subgraph needs an id or a title",
                location,
                "write 'subgraph id', 'subgraph id[title]' or 'subgraph title' on the line of the keyword");
        }

        CheckLabel(title.Text, location);

        // A title with white space that is also the id gets a generated id
        string? subgraphId = Preprocessor.TrimJs(id.Text);
        if (titleIsId && title.Text.Any(ScanPatterns.IsJsWhitespace))
        {
            subgraphId = null;
        }

        // The members: unique, non-blank texts; the last direction statement is the subgraph's direction
        string? direction = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var nodes = new List<string>();
        foreach (var item in items)
        {
            if (item.Direction is not null)
            {
                direction = item.Direction;
                continue;
            }

            var text = item.Text!;
            if (Preprocessor.TrimJs(text).Length > 0 && seen.Add(text) && !_members.Contains(text))
            {
                nodes.Add(text);
            }
        }

        subgraphId ??= "subGraph" + _subgraphCounter.ToString(CultureInfo.InvariantCulture);
        _subgraphCounter++;
        CheckNodeLimit(location);
        var titleText = Preprocessor.TrimJs(HtmlSanitizer.Sanitize(title.Text));
        var subgraph = new FlowSubgraph(subgraphId, nodes, titleText, SanitizeLabelType(title.Type), direction, location);

        // Members of earlier subgraphs were removed above; this subgraph's members are not available to later ones
        _members.UnionWith(nodes);
        _subgraphs.Add(subgraph);
        _subgraphById[subgraphId] = subgraph;
        return subgraphId;
    }

    /// <summary>
    ///     Gets the direction a subgraph is laid out in: its <c>direction</c> statement, else the diagram's when
    ///     <c>flowchart.inheritDir</c> is set, else none.
    /// </summary>
    /// <param name="subgraph">The subgraph.</param>
    /// <param name="inheritDirection">The effective <c>flowchart.inheritDir</c>.</param>
    /// <returns>The direction, or null.</returns>
    public string? SubgraphDirection(FlowSubgraph subgraph, bool inheritDirection)
    {
        ArgumentNullException.ThrowIfNull(subgraph);
        return subgraph.Direction ?? (inheritDirection ? Direction : null);
    }

    /// <summary>
    ///     Reads a link token (Mermaid's <c>destructLink</c>): the arrow type, the stroke and the length, checked against
    ///     the start token of a link with text.
    /// </summary>
    /// <param name="link">The link token (<c>--&gt;</c>, <c>-.-&gt;</c>, <c>==x</c>, <c>~~~</c>, ...).</param>
    /// <param name="start">The start token of a link with text (<c>--</c>, <c>==</c>, <c>-.</c>), or null.</param>
    /// <param name="text">The link text, or null.</param>
    /// <param name="id">The link id with its <c>@</c>, or null.</param>
    /// <returns>The link; type and stroke are <c>INVALID</c> when the start and the end do not match.</returns>
    public static FlowLink DestructLink(string link, string? start, FlowText? text, string? id)
    {
        ArgumentNullException.ThrowIfNull(link);
        var (endType, endStroke, endLength) = DestructEndLink(link);
        if (string.IsNullOrEmpty(start))
        {
            return new FlowLink(endType, endStroke, endLength, text, id);
        }

        var (startType, startStroke) = DestructStartLink(start);
        if (startStroke != endStroke)
        {
            return new FlowLink("INVALID", "INVALID", null, text, id);
        }

        if (startType == "arrow_open")
        {
            // -- text --> takes the arrow type of the end
            startType = endType;
        }
        else
        {
            // x-- text --> is not supported
            if (startType != endType)
            {
                return new FlowLink("INVALID", "INVALID", null, text, id);
            }

            startType = "double_" + startType;
        }

        if (startType == "double_arrow")
        {
            startType = "double_arrow_point";
        }

        return new FlowLink(startType, startStroke, endLength, text, id);
    }

    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The reports of the diagram come first, so they are delivered even though drawing is not available yet
        ReportUnsupported(context.Config, context.Sink);
        throw new MermaidRenderException(
            MermaidDiagnosticCodes.RenderFailed,
            "drawing flowchart diagrams is not available in this build yet",
            KeywordLocation);
    }

    /// <summary>
    ///     Reads the start of a link with text (Mermaid's <c>destructStartLink</c>).
    /// </summary>
    /// <param name="text">The start token.</param>
    /// <returns>The arrow type and the stroke.</returns>
    private static (string Type, string Stroke) DestructStartLink(string text)
    {
        var value = Preprocessor.TrimJs(text);
        var type = "arrow_open";
        if (value.Length > 0)
        {
            var head = value[0] switch { '<' => "arrow_point", 'x' => "arrow_cross", 'o' => "arrow_circle", _ => null };
            if (head is not null)
            {
                type = head;
                value = value[1..];
            }
        }

        var stroke = "normal";
        if (value.Contains('=', StringComparison.Ordinal))
        {
            stroke = "thick";
        }

        if (value.Contains('.', StringComparison.Ordinal))
        {
            stroke = "dotted";
        }

        return (type, stroke);
    }

    /// <summary>
    ///     Reads a link token (Mermaid's <c>destructEndLink</c>).
    /// </summary>
    /// <param name="text">The link token.</param>
    /// <returns>The arrow type, the stroke and the length.</returns>
    private static (string Type, string Stroke, int Length) DestructEndLink(string text)
    {
        var value = Preprocessor.TrimJs(text);
        var line = value.Length > 0 ? value[..^1] : value;
        var type = "arrow_open";
        var last = value.Length > 0 ? value[^1] : '\0';
        var (head, doubled) = last switch
        {
            'x' => ("arrow_cross", 'x'),
            '>' => ("arrow_point", '<'),
            'o' => ("arrow_circle", 'o'),
            _ => (null, '\0'),
        };
        if (head is not null)
        {
            type = head;
            if (value.StartsWith(doubled))
            {
                type = "double_" + type;
                line = line[1..];
            }
        }

        var stroke = "normal";
        var length = line.Length - 1;
        if (line.StartsWith('='))
        {
            stroke = "thick";
        }

        if (line.StartsWith('~'))
        {
            stroke = "invisible";
        }

        var dots = line.Count(c => c == '.');
        if (dots > 0)
        {
            stroke = "dotted";
            length = dots;
        }

        return (type, stroke, length);
    }

    /// <summary>
    ///     Adds one edge (Mermaid's <c>addSingleLink</c>).
    /// </summary>
    /// <param name="start">The start id.</param>
    /// <param name="end">The end id.</param>
    /// <param name="link">The link.</param>
    /// <param name="id">The id given in the text, or null.</param>
    /// <param name="location">The location of the link.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when a limit is exceeded.</exception>
    private void AddSingleLink(string start, string end, FlowLink link, string? id, MermaidLocation location)
    {
        var edge = new FlowEdge(start, end, location) { Interpolate = DefaultInterpolate };
        if (link.Text is not null)
        {
            CheckLabel(link.Text.Text, location);
            edge.Text = StripQuotes(HtmlSanitizer.Sanitize(Preprocessor.TrimJs(link.Text.Text)));
            edge.LabelType = SanitizeLabelType(link.Text.Type);
        }

        edge.Type = link.Type;
        edge.Stroke = link.Stroke;
        edge.Length = link.Length > 10 ? 10 : link.Length;

        // A given id is used once; otherwise the id counts the earlier edges between the same nodes (skipping 1)
        if (!string.IsNullOrEmpty(id) && !_edgeById.ContainsKey(id))
        {
            edge.Id = id;
            edge.IsUserDefinedId = true;
        }
        else
        {
            var existing = _edgePairs.GetValueOrDefault((start, end));
            var counter = existing == 0 ? 0 : existing + 1;
            edge.Id = string.Create(CultureInfo.InvariantCulture, $"L_{start}_{end}_{counter}");
        }

        if (_edges.Count >= _limits.MaxEdges)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} edges (MermaidLimits.MaxEdges)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxEdges (Mermaid's maxEdges) when the source is trusted");
        }

        _edges.Add(edge);
        _edgeById.TryAdd(edge.Id, edge);
        _edgePairs[(start, end)] = _edgePairs.GetValueOrDefault((start, end)) + 1;
    }

    /// <summary>
    ///     Gets the vertex of an id, creating it (with its DOM id) when it does not exist.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="location">The location of the mention.</param>
    /// <returns>The vertex.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when <see cref="MermaidLimits.MaxNodes"/> is exceeded.</exception>
    private FlowVertex GetOrCreateVertex(string id, MermaidLocation location)
    {
        if (_vertexById.TryGetValue(id, out var vertex))
        {
            return vertex;
        }

        CheckNodeLimit(location);
        vertex = new FlowVertex(id, DomIdPrefix + id + "-" + _vertexCounter.ToString(CultureInfo.InvariantCulture), location);
        _vertexById.Add(id, vertex);
        _vertices.Add(vertex);
        return vertex;
    }

    /// <summary>
    ///     Applies the shape data of a vertex (the part of Mermaid's <c>addVertex</c> after the label).
    /// </summary>
    /// <param name="vertex">The vertex.</param>
    /// <param name="doc">The YAML value, or null for an empty block.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, for an unknown shape or a value Mermaid cannot read.</exception>
    private void ApplyVertexData(FlowVertex vertex, YamlNode? doc)
    {
        if (doc is YamlScalar { Value: null })
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.SyntaxError,
                "the shape data is null; it must be a mapping such as @{ shape: rect }",
                doc.Location,
                "write the shape data as key: value pairs, for example A@{ shape: circle, label: \"Start\" }");
        }

        var data = doc as YamlMapping;
        YamlNode? Get(string key) => data?.Get(key);

        // The shape: lower case without underscores, and known
        if (Get("shape") is { IsTruthy: true } shapeNode)
        {
            vertex.Type = CheckShape(shapeNode);
        }

        var label = Get("label");
        if (label is { IsTruthy: true })
        {
            var labelText = label.ToJsString();
            CheckLabel(labelText, label.Location);
            vertex.Text = labelText;
            vertex.LabelType = SanitizeLabelType((Get("labelType") as YamlScalar)?.Value as string);
        }

        // An icon or an image without a label hides the id
        var labelIsBlank = label is null || label is YamlScalar { Value: null } || Preprocessor.TrimJs(label.ToJsString()).Length == 0;
        if (Get("icon") is { IsTruthy: true } icon)
        {
            vertex.Icon = icon.ToJsString();
            if (labelIsBlank && vertex.Text == vertex.Id)
            {
                vertex.Text = string.Empty;
            }
        }

        if (Get("form") is { IsTruthy: true } form)
        {
            vertex.Form = form.ToJsString();
        }

        if (Get("pos") is { IsTruthy: true } pos)
        {
            vertex.Pos = pos.ToJsString();
        }

        if (Get("img") is { IsTruthy: true } img)
        {
            vertex.Img = img.ToJsString();
            if (labelIsBlank && vertex.Text == vertex.Id)
            {
                vertex.Text = string.Empty;
            }
        }

        if (Get("constraint") is { IsTruthy: true } constraint)
        {
            vertex.Constraint = constraint.ToJsString();
        }

        if (Get("w") is { IsTruthy: true } width)
        {
            vertex.AssetWidth = JsNumber(width);
        }

        if (Get("h") is { IsTruthy: true } height)
        {
            vertex.AssetHeight = JsNumber(height);
        }
    }

    /// <summary>
    ///     Checks the shape name of <c>@{ shape }</c> as Mermaid's <c>addVertex</c> does.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The shape name.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, for a name that is not a known lower-case shape.</exception>
    private static string CheckShape(YamlNode node)
    {
        var name = node is YamlScalar { Value: string text } ? text : null;
        if (name is null)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.ShapeUnknown,
                $"the shape must be a name, not '{node.ToJsString()}'",
                node.Location,
                "write a shape name such as rect, rounded, circle or diamond");
        }

        var lowerCase = name == name.ToLowerInvariant() && !name.Contains('_', StringComparison.Ordinal);
        if (!lowerCase || !ShapeNames.IsKnown(name))
        {
            var nearest = ShapeNames.Nearest(name);
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.ShapeUnknown,
                lowerCase ? $"no such shape: '{name}'" : $"no such shape: '{name}'; shape names are lower case without underscores",
                node.Location,
                $"did you mean '{nearest}'? Shapes include rect, rounded, stadium, circle, diamond, hex, cyl, doc and the other names of Mermaid's shape list");
        }

        return name;
    }

    /// <summary>
    ///     Applies the shape data of an edge id (<c>e1@{ animate: true, curve: linear }</c>).
    /// </summary>
    /// <param name="edge">The edge.</param>
    /// <param name="data">The mapping, or null.</param>
    private static void ApplyEdgeData(FlowEdge edge, YamlMapping? data)
    {
        if (data is null)
        {
            return;
        }

        if (data.Get("animate") is { } animate)
        {
            edge.Animate = animate;
        }

        if (data.Get("animation") is { } animation)
        {
            edge.Animation = animation;
        }

        if (data.Get("curve") is { } curve && curve is not YamlScalar { Value: null })
        {
            edge.Interpolate = curve.ToJsString();
        }
    }

    /// <summary>
    ///     Merges the shape data of a subgraph id into its metadata, as JavaScript's object spread does.
    /// </summary>
    /// <param name="subgraph">The subgraph.</param>
    /// <param name="doc">The YAML value.</param>
    private static void MergeMetadata(FlowSubgraph subgraph, YamlNode doc)
    {
        subgraph.Metadata ??= [];
        if (doc is not YamlMapping mapping)
        {
            return;
        }

        foreach (var entry in mapping.Entries)
        {
            var index = subgraph.Metadata.FindIndex(e => e.Key == entry.Key);
            if (index >= 0)
            {
                subgraph.Metadata[index] = entry;
            }
            else
            {
                subgraph.Metadata.Add(entry);
            }
        }
    }

    /// <summary>
    ///     Gets the edge at an index of <c>linkStyle</c>, read as a JavaScript property name.
    /// </summary>
    /// <param name="position">The index as written.</param>
    /// <param name="location">The location of the statement.</param>
    /// <returns>The edge.</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the index names no edge.</exception>
    private FlowEdge EdgeAt(string position, MermaidLocation location)
    {
        if (Graphlib.ObjectKeyMap<string>.IsArrayIndex(position, out var index) && index < (uint)_edges.Count)
        {
            return _edges[(int)index];
        }

        throw new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            string.Create(CultureInfo.InvariantCulture, $"linkStyle names edge {position}, but the diagram has {_edges.Count} edges before it"),
            location,
            _edges.Count == 0
                ? "declare the links before the linkStyle statement"
                : string.Create(CultureInfo.InvariantCulture, $"edges are numbered from 0 in the order they are declared; use 0 to {_edges.Count - 1}"));
    }

    /// <summary>
    ///     Checks a label against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <param name="location">The location of the label.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the label is too long.</exception>
    private void CheckLabel(string text, MermaidLocation location)
    {
        if (text.Length > _limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a label is {text.Length} characters long, more than the limit of {_limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                location,
                "shorten the label, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }
    }

    /// <summary>
    ///     Checks that one more vertex or subgraph fits <see cref="MermaidLimits.MaxNodes"/>.
    /// </summary>
    /// <param name="location">The location of the new element.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the limit would be exceeded.</exception>
    private void CheckNodeLimit(MermaidLocation location)
    {
        if (_vertices.Count + _subgraphs.Count >= _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} nodes and subgraphs (MermaidLimits.MaxNodes)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxNodes when the source is trusted");
        }
    }

    /// <summary>
    ///     Removes the double quotes around a text, as Mermaid does after sanitizing a label.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without the quotes.</returns>
    private static string StripQuotes(string text) =>
        text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;

    /// <summary>
    ///     Mermaid's <c>sanitizeNodeLabelType</c>: <c>markdown</c>, <c>string</c> and <c>text</c> are kept, anything else
    ///     is <c>markdown</c>.
    /// </summary>
    /// <param name="labelType">The label type, or null.</param>
    /// <returns>The sanitized type.</returns>
    private static string SanitizeLabelType(string? labelType) =>
        labelType is "markdown" or "string" or "text" ? labelType : "markdown";

    /// <summary>
    ///     Converts a YAML value to a number as JavaScript's <c>Number(value)</c> does.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The number; NaN when it is not one.</returns>
    private static double JsNumber(YamlNode node)
    {
        if (node is not YamlScalar scalar)
        {
            return double.NaN;
        }

        return scalar.Value switch
        {
            double number => number,
            bool flag => flag ? 1 : 0,
            string text => JsStringToNumber(text),
            _ => 0,
        };
    }

    /// <summary>
    ///     Converts a text to a number as JavaScript's <c>Number(text)</c> does: trimmed, empty is zero, decimal (with
    ///     an exponent or <c>Infinity</c>) or <c>0x</c>, <c>0o</c> and <c>0b</c> integers; anything else is NaN.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    private static double JsStringToNumber(string text)
    {
        var value = Preprocessor.TrimJs(text);
        if (value.Length == 0)
        {
            return 0;
        }

        if (value.Length > 2 && value[0] == '0' && value[1] is 'x' or 'X' or 'o' or 'O' or 'b' or 'B')
        {
            var radix = char.ToLowerInvariant(value[1]) switch { 'x' => 16, 'o' => 8, _ => 2 };
            double result = 0;
            foreach (var c in value[2..])
            {
                var digit = char.IsAsciiHexDigit(c) ? Convert.ToInt32(c.ToString(), 16) : 99;
                if (digit >= radix)
                {
                    return double.NaN;
                }

                result = (result * radix) + digit;
            }

            return result;
        }

        var unsigned = value.TrimStart('+', '-');
        if (unsigned == "Infinity")
        {
            return value[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
        }

        var valid = unsigned.Length > 0 && unsigned.All(c => char.IsAsciiDigit(c) || c is '.' or 'e' or 'E' or '+' or '-')
            && value.Length - unsigned.Length <= 1;
        return valid && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
    }

    /// <summary>
    ///     Removes the first occurrence of a character, as JavaScript's <c>replace</c> with a string does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="c">The character.</param>
    /// <returns>The text without its first occurrence of the character.</returns>
    private static string RemoveFirst(string text, char c)
    {
        var index = text.IndexOf(c, StringComparison.Ordinal);
        return index < 0 ? text : text.Remove(index, 1);
    }

    /// <summary>
    ///     Replaces the first occurrence of a text, as JavaScript's <c>replace</c> with a string does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="search">The text to find.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns>The text with the first occurrence replaced.</returns>
    private static string ReplaceFirst(string text, string search, string replacement)
    {
        var index = text.IndexOf(search, StringComparison.Ordinal);
        return index < 0 ? text : string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + search.Length));
    }

    /// <summary>
    ///     Gets the characters of JavaScript's <c>\s</c> class that can start a text (for trimming).
    /// </summary>
    /// <returns>The characters.</returns>
    private static char[] JsWhitespace() =>
    [
        '\t', '\n', '\v', '\f', '\r', ' ', '\u00a0', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005',
        '\u2006', '\u2007', '\u2008', '\u2009', '\u200a', '\u2028', '\u2029', '\u202f', '\u205f', '\u3000', '\ufeff',
    ];
}
