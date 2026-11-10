// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Class;

/// <content>
///     The mapping of the model to the renderer's <see cref="LayoutData"/> (Mermaid's <c>getData</c>) and the class
///     renderer (<c>classRenderer-v3-unified.ts</c>'s <c>draw</c>): the layout data through the cluster layout, the
///     diagram title and the view box with its padding of 8.
/// </content>
internal sealed partial class ClassModel
{
    /// <summary>The markers a class diagram defines (the class renderer's <c>markers</c>).</summary>
    private static readonly string[] ClassMarkers = ["aggregation", "extension", "composition", "dependency", "lollipop"];

    /// <summary>The padding of the view box, which the class renderer hard-codes.</summary>
    private const double ViewBoxPadding = 8;

    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The reports of what is not drawn come first
        var config = context.Config;
        var sink = context.Sink;
        ReportUnsupported(config, sink);
        var data = GetData(config);

        // The settings of the rendering: pictures, labels, style sheet and the shared work budget
        var limits = context.Options.Limits;
        var meter = new Dagre.LayoutWorkMeter(limits.MaxLayoutWork, KeywordLocation, context.CancellationToken);
        var images = new NodeImages(context.Options.ImageResolver, limits.MaxImages, sink);
        var labels = new LabelBuilder(context.Fonts, sink, images) { Meter = meter };
        var font = new MermaidFontConfig(
            config.FontFamily ?? "Arial",
            ConfigScalar.FromJson(config.Raw["fontSize"]) ?? ConfigScalar.FromNumber(12),
            ConfigScalar.FromJson(config.Raw["fontWeight"]) ?? ConfigScalar.FromNumber(400));
        var shapes = new ShapeContext
        {
            Config = config,
            Styles = ClassStyles.Create(config, sink),
            MeasureLabel = labels.Build,
            Images = images,
            Sink = sink,
            TextWidth = text => labels.CalculateTextWidth(text, font),
        };

        // The drawing, the title above it and the view box grown by the padding
        return new ClusterLayout(shapes, meter).RenderDiagram(
            data,
            context.Source.Frontmatter?.Title,
            config.Class.TitleTopMargin,
            ViewBoxPadding,
            labels);
    }

    /// <summary>
    ///     Reports what the diagram uses that this version does not draw: a look other than <c>classic</c> and a layout
    ///     other than <c>dagre</c>.
    /// </summary>
    /// <param name="config">The effective configuration of the render.</param>
    /// <param name="sink">The report sink of the render.</param>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown by the sink in strict mode at the first report.</exception>
    public static void ReportUnsupported(MermaidConfig config, MermaidReportSink sink)
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
    }

    /// <summary>
    ///     Maps the model to the renderer's input under the configuration of a render (Mermaid's <c>getData</c>, with the
    ///     settings the class renderer adds).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The nodes come in Mermaid's order: the namespaces as <c>rect</c> clusters (with
    ///         <c>class.hierarchicalNamespaces</c> false, only the declared ones, labeled with their qualified id and without
    ///         a parent), the classes as <c>classBox</c> nodes, the notes as <c>note</c> nodes (with an edge to their class
    ///         when it exists) and the lollipop interfaces as invisible <c>rect</c> nodes. The relations follow the note
    ///         edges, numbered from 1 in their ids.
    ///     </para>
    ///     <para>
    ///         Mermaid's class edges have no <c>minlen</c>, so dagre's default of 1 applies: every edge has a
    ///         <see cref="LayoutEdge.MinLen"/> of 1. A cardinality of <c>none</c> is no terminal label. The spacing is the
    ///         state section's, as Mermaid's class renderer reads it (<see cref="ClassConfig"/>).
    ///     </para>
    ///     <para>
    ///         A class named like a namespace shares the namespace's node, so the namespace is nested in the class's own
    ///         namespace; chained, such names nest namespaces without any nesting in the text. The nesting the parents
    ///         make is checked against <see cref="MermaidLimits.MaxNestingDepth"/> (<see cref="LayoutData.CheckNestingDepth"/>).
    ///     </para>
    /// </remarks>
    /// <param name="config">The effective configuration of the render.</param>
    /// <returns>The layout data.</returns>
    /// <exception cref="MermaidRenderException">
    ///     Thrown, located, when the namespaces nest deeper than <see cref="MermaidLimits.MaxNestingDepth"/>.
    /// </exception>
    public LayoutData GetData(MermaidConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var look = config.Look;
        var hierarchical = config.Class.HierarchicalNamespaces;
        var nodes = new List<LayoutNode>();
        var edges = new List<LayoutEdge>();

        // The namespaces
        foreach (var space in _namespaces)
        {
            if (!hierarchical && !space.Explicit)
            {
                continue;
            }

            nodes.Add(new LayoutNode
            {
                Id = space.Id,
                Label = hierarchical ? space.Label : space.Id,
                IsGroup = true,
                Padding = config.Class.Padding ?? 16,
                Shape = "rect",
                CssStyles = [],
                Look = look,
                ParentId = hierarchical ? space.Parent : null,
                Location = space.Location,
            });
        }

        // The classes
        foreach (var node in _classes)
        {
            nodes.Add(new LayoutNode
            {
                Id = node.Id,
                Label = node.Label,
                Shape = "classBox",
                IsGroup = false,
                ParentId = hierarchical ? node.Parent : ResolveExplicitAncestor(node.Parent),
                CssClasses = node.CssClasses,
                DomId = node.DomId,
                Look = look,
                ClassBox = new LayoutClassBox(
                    node.Text,
                    [.. node.Annotations],
                    [.. node.Members.Select(member => new LayoutClassMember(member.Text, member.CssStyle))],
                    [.. node.Methods.Select(method => new LayoutClassMember(method.Text, method.CssStyle))],
                    [.. node.Styles]),
                Location = node.Location,
            });
        }

        // The notes, each with an edge to its class when the class exists
        // A number or a boolean theme variable is stringified as JavaScript would, a missing one is "undefined"
        var noteFill = config.ThemeVariable("noteBkgColor") ?? "undefined";
        var noteStroke = config.ThemeVariable("noteBorderColor") ?? "undefined";
        foreach (var note in _notes)
        {
            nodes.Add(new LayoutNode
            {
                Id = note.Id,
                Label = note.Text,
                IsGroup = false,
                Shape = "note",
                Padding = config.Class.Padding ?? 6,
                CssStyles = ["text-align: left", "white-space: nowrap", $"fill: {noteFill}", $"stroke: {noteStroke}"],
                TrustedStyles = true,
                Look = look,
                ParentId = hierarchical ? note.Parent : ResolveExplicitAncestor(note.Parent),
                LabelType = "markdown",
                Location = note.Location,
            });

            // The class is looked up by the name as written, as Mermaid's getData does with this.classes.get(note.class)
            // (packages/mermaid/src/diagrams/class/classDb.ts:806): a note for Box~T~ (the class is Box) stays unlinked
            if (note.Class is not null && _classIndex.TryGetValue(note.Class, out var target))
            {
                edges.Add(new LayoutEdge
                {
                    Id = string.Create(CultureInfo.InvariantCulture, $"edgeNote{note.Index}"),
                    Start = note.Id,
                    End = target.Id,
                    Type = "normal",
                    Thickness = "normal",
                    MinLen = 1,
                    Classes = "relation",
                    ArrowTypeStart = "none",
                    ArrowTypeEnd = "none",
                    ArrowheadStyle = string.Empty,
                    LabelStyle = [string.Empty],
                    Style = ["fill: none"],
                    Pattern = "dotted",
                    Look = look,
                    Location = note.Location,
                });
            }
        }

        // The lollipop interfaces, drawn invisible
        foreach (var face in _interfaces)
        {
            nodes.Add(new LayoutNode
            {
                Id = face.Id,
                Label = face.Label,
                IsGroup = false,
                Shape = "rect",
                CssStyles = ["opacity: 0;"],
                TrustedStyles = true,
                Look = look,
                Location = face.Location,
            });
        }

        // The relations, numbered from 1
        var count = 0;
        foreach (var relation in _relations)
        {
            count++;
            edges.Add(new LayoutEdge
            {
                Id = string.Create(CultureInfo.InvariantCulture, $"id_{relation.Id1}_{relation.Id2}_{count}"),
                Start = relation.Id1,
                End = relation.Id2,
                Type = "normal",
                Label = relation.Title ?? string.Empty,
                LabelType = "markdown",
                LabelPos = "c",
                Thickness = "normal",
                MinLen = 1,
                Classes = "relation",
                ArrowTypeStart = ArrowMarker(relation.Relation.Type1),
                ArrowTypeEnd = ArrowMarker(relation.Relation.Type2),
                StartLabelRight = relation.RelationTitle1 == "none" ? string.Empty : relation.RelationTitle1,
                EndLabelLeft = relation.RelationTitle2 == "none" ? string.Empty : relation.RelationTitle2,
                ArrowheadStyle = string.Empty,
                LabelStyle = ["display: inline-block"],
                Pattern = relation.Relation.LineType == DottedLine ? "dashed" : "solid",
                Look = look,
                Location = relation.Location,
            });
        }

        var data = new LayoutData(nodes, edges, config)
        {
            DiagramType = MermaidId,
            Direction = Direction,
            NodeSpacing = config.Class.NodeSpacing,
            RankSpacing = config.Class.RankSpacing,
            Markers = ClassMarkers,
        };

        // A class named like a namespace nests that namespace in the class's own, without any nesting in the text
        data.CheckNestingDepth(_limits.MaxNestingDepth, "namespaces");
        return data;
    }

    /// <summary>
    ///     Gets the marker of a relation end (Mermaid's <c>getArrowMarker</c>).
    /// </summary>
    /// <param name="type">The relation code, or null for none.</param>
    /// <returns>The marker name, or <c>none</c>.</returns>
    private static string ArrowMarker(int? type) => type switch
    {
        Aggregation => "aggregation",
        Extension => "extension",
        Composition => "composition",
        Dependency => "dependency",
        Lollipop => "lollipop",
        _ => "none",
    };

    /// <summary>
    ///     Gets the nearest declared namespace at or above a namespace (Mermaid's <c>resolveExplicitAncestor</c>), which
    ///     holds a class or note when <c>class.hierarchicalNamespaces</c> is false.
    /// </summary>
    /// <param name="id">The qualified id of the namespace, or null.</param>
    /// <returns>The qualified id of the declared namespace, or null.</returns>
    private string? ResolveExplicitAncestor(string? id)
    {
        var current = id;
        while (current is not null)
        {
            if (!_namespaceIndex.TryGetValue(current, out var space))
            {
                return null;
            }

            if (space.Explicit)
            {
                return current;
            }

            current = space.Parent;
        }

        return null;
    }
}
