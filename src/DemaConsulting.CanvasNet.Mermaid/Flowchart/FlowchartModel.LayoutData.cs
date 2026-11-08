// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <content>
///     The mapping of the model to the renderer's <see cref="LayoutData"/> (Mermaid's <c>getData</c>) and the reports of
///     what the flowchart uses that this version does not draw.
/// </content>
internal sealed partial class FlowchartModel
{
    /// <summary>The classes of the path of a visible edge.</summary>
    private const string EdgeClasses = "edge-thickness-normal edge-pattern-solid flowchart-link";

    /// <summary>
    ///     Maps the model to the renderer's input under the configuration of a render (Mermaid's <c>getData</c>, with
    ///     the settings the flowchart renderer adds).
    /// </summary>
    /// <remarks>
    ///     The clusters come first, last declared first; then the vertices in declaration order, except those hidden in
    ///     a collapsed subgraph; a vertex with the id of a subgraph updates the subgraph's node instead. Edges keep their
    ///     order; an edge into or out of a collapsed subgraph is redirected to it, and one inside it is dropped.
    /// </remarks>
    /// <param name="config">The effective configuration of the render.</param>
    /// <param name="diagramType">The diagram type as Mermaid names it (<c>flowchart-v2</c> or <c>flowchart-elk</c>).</param>
    /// <returns>The layout data.</returns>
    public LayoutData GetData(MermaidConfig config, string diagramType)
    {
        ArgumentNullException.ThrowIfNull(config);
        var look = config.Look;
        var (hidden, collapsedAncestor) = CollapsedSubgraphs();

        // Which cluster each id is in, and which subgraphs have members (later subgraphs first, so earlier ones win)
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var groups = new HashSet<string>(StringComparer.Ordinal);
        for (var i = _subgraphs.Count - 1; i >= 0; i--)
        {
            var subgraph = _subgraphs[i];
            if (hidden.Contains(subgraph.Id))
            {
                continue;
            }

            if (subgraph.Nodes.Count > 0)
            {
                groups.Add(subgraph.Id);
            }

            foreach (var id in subgraph.Nodes)
            {
                parents[id] = subgraph.Id;
            }
        }

        // The clusters (or the compact nodes of collapsed subgraphs)
        var nodes = new List<LayoutNode>();
        var firstIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = _subgraphs.Count - 1; i >= 0; i--)
        {
            var subgraph = _subgraphs[i];
            if (hidden.Contains(subgraph.Id))
            {
                continue;
            }

            firstIndex.TryAdd(subgraph.Id, nodes.Count);
            nodes.Add(new LayoutNode
            {
                Id = subgraph.Id,
                Label = subgraph.Title,
                LabelType = subgraph.LabelType,
                ParentId = parents.GetValueOrDefault(subgraph.Id),
                Padding = 8,
                CssCompiledStyles = CompiledStyles(subgraph.Classes),
                CssClasses = string.Join(" ", subgraph.Classes),
                Shape = subgraph.IsCollapsed ? "collapsedGroup" : "rect",
                Dir = SubgraphDirection(subgraph, config.Flowchart.InheritDir),
                IsGroup = !subgraph.IsCollapsed,
                Look = look,
                Location = subgraph.Location,
            });
        }

        // The vertices
        var padding = config.Flowchart.Padding != 0 && !double.IsNaN(config.Flowchart.Padding) ? config.Flowchart.Padding : 8;
        foreach (var vertex in _vertices)
        {
            if (hidden.Contains(vertex.Id))
            {
                continue;
            }

            if (firstIndex.TryGetValue(vertex.Id, out var index))
            {
                // A vertex that names a subgraph styles the subgraph's node
                nodes[index] = nodes[index] with
                {
                    CssStyles = [.. vertex.Styles],
                    CssCompiledStyles = CompiledStyles(vertex.Classes),
                    CssClasses = string.Join(" ", vertex.Classes),
                };
                continue;
            }

            var isGroup = groups.Contains(vertex.Id);
            firstIndex.Add(vertex.Id, nodes.Count);
            nodes.Add(new LayoutNode
            {
                Id = vertex.Id,
                Label = vertex.Text,
                LabelType = vertex.LabelType,
                ParentId = parents.GetValueOrDefault(vertex.Id),
                Padding = padding,
                CssStyles = [.. vertex.Styles],
                CssCompiledStyles = CompiledStyles(["default", "node", .. vertex.Classes]),
                CssClasses = "default " + string.Join(" ", vertex.Classes),
                DomId = vertex.DomId,
                Look = look,
                IsGroup = isGroup,
                Shape = isGroup ? "rect" : ShapeOf(vertex),
                Icon = vertex.Icon,
                Pos = vertex.Pos,
                Img = vertex.Img,
                AssetWidth = vertex.AssetWidth,
                AssetHeight = vertex.AssetHeight,
                Constraint = vertex.Constraint,
                Location = vertex.Location,
            });
        }

        return new LayoutData(nodes, LayoutEdges(config, collapsedAncestor), config)
        {
            DiagramType = diagramType,
            Direction = Direction,
            NodeSpacing = config.Flowchart.NodeSpacing is not 0 and not double.NaN ? config.Flowchart.NodeSpacing : 50,
            RankSpacing = config.Flowchart.RankSpacing is not 0 and not double.NaN ? config.Flowchart.RankSpacing : 50,
            Markers = ["point", "circle", "cross"],
        };
    }

    /// <summary>
    ///     Reports what the diagram uses that this version does not draw, in order: a look other than <c>classic</c>, a
    ///     layout other than <c>dagre</c>, then the icons of the nodes (<c>@{ icon }</c>) and the Font Awesome icons in
    ///     labels (<c>fa:fa-...</c>).
    /// </summary>
    /// <param name="config">The effective configuration of the render.</param>
    /// <param name="sink">The report sink of the render.</param>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown by the sink in strict mode at the first report.</exception>
    public void ReportUnsupported(MermaidConfig config, MermaidReportSink sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(sink);
        if (config.Look != "classic")
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.LookUnsupported,
                $"the '{config.Look}' look is not supported; the diagram is drawn in the classic look",
                config.LocationOf("look"),
                "remove the look setting, or set look: classic");
        }

        if (config.Layout != "dagre")
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.LayoutUnsupported,
                $"the '{config.Layout}' layout is not supported; the diagram is laid out with dagre",
                config.LocationOf("layout"),
                "remove the layout setting, or set layout: dagre");
        }

        foreach (var vertex in _vertices)
        {
            if (vertex.Icon is not null)
            {
                sink.Unsupported(
                    MermaidDiagnosticCodes.IconUnsupported,
                    $"the icon '{vertex.Icon}' of node '{vertex.Id}' is not drawn; the node keeps its shape and label",
                    vertex.Location,
                    "icons need an icon pack that is not available offline; use a label or an image (@{ img: ... }) instead");
            }

            ReportFontAwesome(sink, vertex.Text, vertex.Location);
        }

        foreach (var edge in _edges)
        {
            ReportFontAwesome(sink, edge.Text, edge.Location);
        }

        foreach (var subgraph in _subgraphs)
        {
            ReportFontAwesome(sink, subgraph.Title, subgraph.Location);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether a label contains a Font Awesome icon as Mermaid finds it: a prefix (<c>fa</c>,
    ///     <c>fab</c>, <c>fak</c>, <c>fal</c>, <c>far</c> or <c>fas</c>), <c>:fa-</c> and an icon name of word characters and dashes.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <returns>Whether it does.</returns>
    internal static bool ContainsFontAwesome(string? text)
    {
        if (text is null)
        {
            return false;
        }

        for (var i = text.IndexOf("fa", StringComparison.Ordinal); i >= 0; i = text.IndexOf("fa", i + 1, StringComparison.Ordinal))
        {
            var colon = i + 2;
            if (colon < text.Length && text[colon] is 'b' or 'k' or 'l' or 'r' or 's' && !StartsWithIcon(text, colon))
            {
                colon++;
            }

            if (StartsWithIcon(text, colon))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets a value indicating whether <c>:fa-</c> and an icon-name character follow a position.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <param name="position">The position.</param>
    /// <returns>Whether they do.</returns>
    private static bool StartsWithIcon(string text, int position) =>
        position + 4 < text.Length
        && string.CompareOrdinal(text, position, ":fa-", 0, 4) == 0
        && (ScanPatterns.IsJsWordChar(text[position + 4]) || text[position + 4] == '-');

    /// <summary>
    ///     Reports a label that contains a Font Awesome icon.
    /// </summary>
    /// <param name="sink">The sink.</param>
    /// <param name="text">The label.</param>
    /// <param name="location">The location of its element.</param>
    private static void ReportFontAwesome(MermaidReportSink sink, string? text, MermaidLocation location)
    {
        if (ContainsFontAwesome(text))
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.IconUnsupported,
                "Font Awesome icons (fa:fa-...) are not drawn; the label is drawn without them",
                location,
                "Font Awesome is not available offline; remove the icon or use an image node (@{ img: ... })");
        }
    }

    /// <summary>
    ///     Gets the shape of a vertex (Mermaid's <c>getTypeFromVertex</c>): an image or icon shape, else the shape the
    ///     bracket or the shape data gave, with the legacy names mapped.
    /// </summary>
    /// <param name="vertex">The vertex.</param>
    /// <returns>The shape name.</returns>
    private static string ShapeOf(FlowVertex vertex)
    {
        if (vertex.Img is not null)
        {
            return "imageSquare";
        }

        if (vertex.Icon is not null)
        {
            return vertex.Form switch
            {
                "circle" => "iconCircle",
                "square" => "iconSquare",
                "rounded" => "iconRounded",
                _ => "icon",
            };
        }

        return vertex.Type switch
        {
            null or "square" => "squareRect",
            "round" => "roundedRect",
            _ => vertex.Type,
        };
    }

    /// <summary>
    ///     Finds the subgraphs hidden in a collapsed subgraph and what replaces each hidden id (the outermost collapsed
    ///     subgraph around it).
    /// </summary>
    /// <returns>The hidden ids, and the visible collapsed subgraph of each.</returns>
    private (HashSet<string> Hidden, Dictionary<string, string> Ancestor) CollapsedSubgraphs()
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        var ancestors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!_subgraphs.Exists(s => s.IsCollapsed))
        {
            return (hidden, ancestors);
        }

        // The subgraph that directly contains each subgraph
        var containers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subgraph in _subgraphs)
        {
            foreach (var child in subgraph.Nodes.Where(_subgraphById.ContainsKey))
            {
                containers[child] = subgraph.Id;
            }
        }

        foreach (var subgraph in _subgraphs)
        {
            // The outermost collapsed subgraph on the way up
            string? outermost = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var current = subgraph.Id; current is not null && seen.Add(current); current = containers.GetValueOrDefault(current))
            {
                if (_subgraphById.TryGetValue(current, out var candidate) && candidate.IsCollapsed)
                {
                    outermost = current;
                }
            }

            if (outermost is null)
            {
                continue;
            }

            if (subgraph.Id != outermost)
            {
                hidden.Add(subgraph.Id);
                ancestors[subgraph.Id] = outermost;
            }

            foreach (var child in subgraph.Nodes.Where(child => child != outermost))
            {
                hidden.Add(child);
                ancestors[child] = outermost;
            }
        }

        return (hidden, ancestors);
    }

    /// <summary>
    ///     Maps the edges (the edge part of Mermaid's <c>getData</c>).
    /// </summary>
    /// <param name="config">The effective configuration.</param>
    /// <param name="collapsedAncestor">The visible collapsed subgraph of each hidden id.</param>
    /// <returns>The edges.</returns>
    private List<LayoutEdge> LayoutEdges(MermaidConfig config, Dictionary<string, string> collapsedAncestor)
    {
        var edges = new List<LayoutEdge>();
        foreach (var raw in _edges)
        {
            var (arrowStart, arrowEnd) = ArrowTypes(raw.Type);

            // Edges crossing into a collapsed subgraph end at it; edges inside one disappear
            var start = collapsedAncestor.GetValueOrDefault(raw.Start) ?? raw.Start;
            var end = collapsedAncestor.GetValueOrDefault(raw.End) ?? raw.End;
            if (start == end && (collapsedAncestor.ContainsKey(raw.Start) || collapsedAncestor.ContainsKey(raw.End)))
            {
                continue;
            }

            List<string> styles = [.. DefaultStyle ?? []];
            if (raw.Style is not null)
            {
                styles.AddRange(raw.Style);
            }

            var invisible = raw.Stroke == "invisible";
            var open = invisible || raw.Type == "arrow_open";
            edges.Add(new LayoutEdge
            {
                Id = raw.Id,
                IsUserDefinedId = raw.IsUserDefinedId,
                Start = start,
                End = end,
                Type = raw.Type ?? "normal",
                Label = raw.Text,
                LabelType = raw.LabelType,
                Thickness = raw.Stroke,
                MinLen = raw.Length,
                Classes = invisible ? string.Empty : EdgeClasses,
                ArrowTypeStart = open ? "none" : arrowStart,
                ArrowTypeEnd = open ? "none" : arrowEnd,
                ArrowheadStyle = "fill: #333",
                CssCompiledStyles = CompiledStyles(raw.Classes),
                LabelStyle = styles,
                Style = styles,
                Pattern = raw.Stroke,
                Look = config.Look,
                Animate = raw.Animate,
                Animation = raw.Animation,
                Curve = FirstNonEmpty(raw.Interpolate, DefaultInterpolate) ?? config.Flowchart.Curve,
                Location = raw.Location,
            });
        }

        return edges;
    }

    /// <summary>
    ///     Gets the markers of an arrow type (Mermaid's <c>destructEdgeType</c>).
    /// </summary>
    /// <param name="type">The arrow type.</param>
    /// <returns>The start and end markers.</returns>
    private static (string Start, string End) ArrowTypes(string? type) => type switch
    {
        "arrow_point" or "arrow_circle" or "arrow_cross" => ("none", type),
        "double_arrow_point" or "double_arrow_circle" or "double_arrow_cross" => (type[7..], type[7..]),
        _ => ("none", "arrow_point"),
    };

    /// <summary>
    ///     Compiles the styles of classes (Mermaid's <c>getCompiledStyles</c>): each known class adds its styles, then its
    ///     text styles, all trimmed.
    /// </summary>
    /// <param name="classNames">The class names in order.</param>
    /// <returns>The compiled styles.</returns>
    private List<string> CompiledStyles(IEnumerable<string> classNames)
    {
        var compiled = new List<string>();
        foreach (var name in classNames)
        {
            if (!_classById.TryGetValue(name, out var flowClass))
            {
                continue;
            }

            compiled.AddRange(flowClass.Styles.Select(Preprocessor.TrimJs));
            compiled.AddRange(flowClass.TextStyles.Select(Preprocessor.TrimJs));
        }

        return compiled;
    }

    /// <summary>
    ///     Gets the first text that is not null or empty (JavaScript's <c>a || b</c> on strings).
    /// </summary>
    /// <param name="first">The first text.</param>
    /// <param name="second">The second text.</param>
    /// <returns>The first non-empty text, or null.</returns>
    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrEmpty(first))
        {
            return first;
        }

        return string.IsNullOrEmpty(second) ? null : second;
    }
}
