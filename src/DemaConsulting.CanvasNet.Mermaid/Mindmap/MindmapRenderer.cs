// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Cose;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using LayoutWorkMeter = DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutWorkMeter;

namespace DemaConsulting.CanvasNet.Mermaid.Mindmap;

/// <summary>
///     The mindmap renderer: a port of Mermaid's <c>mindmapRenderer.ts</c> <c>draw</c> with the node data of the mindmap
///     database's <c>getData</c> and the cose-bilkent renderer (<c>layout-algorithms/cose-bilkent/render.ts</c>), which
///     turns a <see cref="MindmapDiagram"/> into a scene with the paint of Mermaid's mindmap style sheet
///     (<see cref="MindmapStyles"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Nodes.</b> Each node gets the data of <c>getData</c>: its shape, its classes, a markdown label, the
///         configured <c>mindmap.padding</c> (doubled for the rectangle, the rounded rectangle and the hexagon) and
///         <c>mindmap.maxNodeWidth</c> as the width its label wraps at. Then <c>draw</c> changes them per shape as Mermaid
///         does: a rounded rectangle gets a padding of 15 and a rectangle 10, and both, like the hexagon, a width of zero
///         (their labels wrap at <c>flowchart.wrappingWidth</c>). Each node is drawn by <see cref="ShapeLibrary"/>, and the
///         layout is given the box of the drawn node as the browser's <c>getBBox</c> gives it, in single precision.
///     </para>
///     <para>
///         <b>Layout.</b> The nodes, in order, and the edges of <c>generateEdges</c> are laid out by
///         <see cref="CoseLayout"/> (Mermaid's default mindmap layout), which gives each node's center and each edge's
///         three points (cytoscape's start, middle and end). As in Mermaid, the edges are drawn through those points with
///         the <c>basis</c> curve and are not cut at the node outlines (Mermaid hands the edge drawing node copies without
///         an intersect function). The view box is the box of everything drawn grown by <c>mindmap.padding</c>; an empty
///         mindmap (its keyword alone) draws nothing and gets the empty box grown by the padding.
///     </para>
///     <para>
///         <b>Reports.</b> In order: a look other than <c>classic</c>, a layout other than <c>cose-bilkent</c> (the
///         mindmap is still laid out with cose-bilkent), then for each node its icon (<c>::icon(...)</c>, which Mermaid
///         does not draw either) and the Font Awesome icons in its label (<c>fa:fa-...</c>).
///     </para>
///     <para>
///         <b>Limits and cost.</b> The label of each node, as <c>labelHelper</c> prepares it (entities decoded and
///         sanitized), is checked against the render's <see cref="MermaidLimits.MaxLabelLength"/>. The work meter
///         (<see cref="MermaidLimits.MaxLayoutWork"/>) is charged for the data (one per node and per edge), for the length
///         of every measured text, by the cose layout for its every step, and for every drawn edge (one per point), and
///         checks cancellation.
///     </para>
///     <para>
///         An instance draws one render on one thread.
///     </para>
/// </remarks>
internal sealed class MindmapRenderer
{
    /// <summary>The padding the renderer gives a rounded rectangle.</summary>
    private const double RoundedPadding = 15;

    /// <summary>The padding the renderer gives a rectangle.</summary>
    private const double RectPadding = 10;

    /// <summary>The diagram.</summary>
    private readonly MindmapDiagram _diagram;

    /// <summary>The context of the render.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The work meter of the render.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>
    ///     Initializes a renderer for one render.
    /// </summary>
    /// <param name="diagram">The diagram.</param>
    /// <param name="context">The context of the render.</param>
    public MindmapRenderer(MindmapDiagram diagram, DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(context);
        _diagram = diagram;
        _context = context;
        _meter = new LayoutWorkMeter(context.Options.Limits.MaxLayoutWork, diagram.KeywordLocation, context.CancellationToken);
    }

    /// <summary>
    ///     Gets the node sizes the last <see cref="Draw"/> gave the layout, in node order (empty before a draw and for an
    ///     empty mindmap); the reference tests compare them with the sizes Chrome measured.
    /// </summary>
    internal IReadOnlyList<CoseSize> Sizes { get; private set; } = [];

    /// <summary>
    ///     Gets the data <c>getData</c> gives a node (<c>flattenNodes</c>, with the padding and width of <c>addNode</c>),
    ///     before <c>draw</c> changes it per shape.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="config">The effective configuration.</param>
    /// <returns>The node data.</returns>
    internal static LayoutNode DataOf(MindmapNode node, MermaidConfig config)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(config);
        var padding = config.Mindmap.Padding;
        if (node.Type is MindmapNodeType.RoundedRect or MindmapNodeType.Rect or MindmapNodeType.Hexagon)
        {
            padding *= 2;
        }

        var id = node.Id.ToString(CultureInfo.InvariantCulture);
        return new LayoutNode
        {
            Id = id,
            DomId = "node_" + id,
            Label = node.Description,
            LabelType = "markdown",
            Shape = node.Shape,
            Width = config.Mindmap.MaxNodeWidth,
            Padding = padding,
            CssClasses = node.CssClasses,
            CssStyles = [],
            Look = config.Look,
            Icon = node.Icon,
            Location = node.Location,
        };
    }

    /// <summary>
    ///     Applies the changes <c>draw</c> makes per shape before the nodes are drawn: a rounded rectangle gets a padding
    ///     of 15 and a rectangle 10, and both, like the hexagon, a width of zero. (Its <c>circle</c> branch is never
    ///     taken, because a circle node's shape is <c>mindmapCircle</c>, and the radius, taper, stroke and height it sets
    ///     change nothing the classic shapes draw.)
    /// </summary>
    /// <param name="data">The node data.</param>
    /// <returns>The node data to draw.</returns>
    internal static LayoutNode Prepared(LayoutNode data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Shape switch
        {
            "rounded" => data with { Width = 0, Padding = RoundedPadding },
            "rect" => data with { Width = 0, Padding = RectPadding },
            "hexagon" => data with { Width = 0 },
            _ => data,
        };
    }

    /// <summary>
    ///     Gets the box of a drawn node as the browser's <c>getBBox</c> gives it to the layout: each side in single
    ///     precision, and the width and height subtracted in single precision.
    /// </summary>
    /// <param name="bounds">The box in double precision.</param>
    /// <returns>The box.</returns>
    internal static SceneRect Box32(SceneRect bounds)
    {
        var left = (float)bounds.X;
        var top = (float)bounds.Y;
        var right = (float)bounds.Right;
        var bottom = (float)bounds.Bottom;
        return new SceneRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    ///     Draws the mindmap.
    /// </summary>
    /// <returns>The scene, whose view box is the natural size.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when a limit is exceeded or the layout fails.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported feature.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public Scene Draw()
    {
        var config = _context.Config;
        var sink = _context.Sink;
        ReportUnsupported(config, sink);
        var padding = config.Mindmap.Padding;
        var nodes = _diagram.Nodes;
        var edges = _diagram.Edges;
        if (nodes.Count == 0)
        {
            // Mermaid returns before the view box is set; the empty box is grown by the padding
            return new Scene(new SceneRect(-padding, -padding, 2 * padding, 2 * padding), []);
        }

        // getData and the per-shape changes
        _meter.Charge(nodes.Count + edges.Count + 1L);
        var labels = new LabelBuilder(_context.Fonts, sink, null) { Meter = _meter };
        var shapes = new ShapeContext
        {
            Config = config,
            Styles = MindmapStyles.Create(config, sink),
            MeasureLabel = labels.Build,
            Sink = sink,
        };

        // Each node is drawn and measured, in order
        var drawn = new List<(LayoutNode Data, ShapeResult Shape)>(nodes.Count);
        var sizes = new List<CoseSize>(nodes.Count);
        foreach (var node in nodes)
        {
            var data = Prepared(DataOf(node, config));
            CheckLabel(data);
            var shape = ShapeLibrary.Draw(data, shapes);
            var size = Box32(shape.Bounds);
            drawn.Add((data, shape));
            sizes.Add(new CoseSize(size.Width, size.Height));
        }

        // The cose-bilkent layout of the tree
        Sizes = sizes;
        var links = edges.Select(edge => new CoseLink(edge.Start.Id, edge.End.Id)).ToList();
        var layout = CoseLayout.Run(sizes, links, _meter);

        // The edges through cytoscape's three points, not cut at the nodes
        var edgePaths = new List<SceneItem>();
        SceneRect? bounds = null;
        for (var i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            var points = layout.Edges[i];
            _meter.Charge(4);
            var data = new LayoutEdge
            {
                Id = edge.Id,
                Start = drawn[edge.Start.Id].Data.Id,
                End = drawn[edge.End.Id].Data.Id,
                Type = "normal",
                Curve = "basis",
                Thickness = "normal",
                Classes = edge.Classes,
                Look = config.Look,
                Location = edge.End.Location,
            };
            var route = new EdgeRoute(data, edge.Id, [Point(points.Start), Point(points.Mid), Point(points.End)]);
            var drawing = EdgeBuilder.Draw(route, End(layout.Centers[edge.Start.Id], sizes[edge.Start.Id]), End(layout.Centers[edge.End.Id], sizes[edge.End.Id]), shapes);
            edgePaths.AddRange(drawing.Items);
            bounds = Union(bounds, drawing.Bounds);
        }

        // The nodes at their centers (a transform that is not a number is ignored by the browser)
        var nodeGroups = new List<SceneItem>(nodes.Count);
        for (var i = 0; i < drawn.Count; i++)
        {
            var (data, shape) = drawn[i];
            var center = layout.Centers[i];
            var valid = double.IsFinite(center.X) && double.IsFinite(center.Y);
            var (x, y) = valid ? (center.X, center.Y) : (0.0, 0.0);
            nodeGroups.Add(new SceneGroup(x, y, shape.Items) { Role = "node-group", Id = data.DomId, Location = data.Location });
            bounds = Union(bounds, shape.Bounds.Offset(x, y));
        }

        // The view box: the box of the drawing (single precision, as getBBox gives it) grown by the padding
        var root = new SceneGroup(0, 0,
        [
            new SceneGroup(0, 0, edgePaths) { Role = "edgePaths" },
            new SceneGroup(0, 0, []) { Role = "edgeLabels" },
            new SceneGroup(0, 0, nodeGroups) { Role = "nodes" },
        ])
        {
            Role = "root",
            Location = _diagram.KeywordLocation,
        };
        var box = bounds is { } b ? Box32(b) : new SceneRect(0, 0, 0, 0);
        var viewBox = new SceneRect(box.X - padding, box.Y - padding, box.Width + (2 * padding), box.Height + (2 * padding));
        return new Scene(viewBox, [root]);
    }

    /// <summary>
    ///     Reports what the diagram uses that this version does not draw, in order: a look other than <c>classic</c>, a
    ///     layout other than <c>cose-bilkent</c>, then the icon of each node and the Font Awesome icons in its label.
    /// </summary>
    /// <param name="config">The effective configuration of the render.</param>
    /// <param name="sink">The report sink of the render.</param>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown by the sink in strict mode at the first report.</exception>
    private void ReportUnsupported(MermaidConfig config, MermaidReportSink sink)
    {
        if (config.Look != "classic")
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.LookUnsupported,
                $"the '{config.Look}' look is not supported; the diagram is drawn in the classic look",
                config.LocationOf("look"),
                "remove the look setting, or set look: classic");
        }

        if (config.Layout != "cose-bilkent")
        {
            sink.Unsupported(
                MermaidDiagnosticCodes.LayoutUnsupported,
                $"the '{config.Layout}' layout is not supported; the mindmap is laid out with cose-bilkent",
                config.LocationOf("layout"),
                "remove the layout setting, or set layout: cose-bilkent");
        }

        foreach (var node in _diagram.Nodes)
        {
            if (node.Icon is not null)
            {
                sink.Unsupported(
                    MermaidDiagnosticCodes.IconUnsupported,
                    $"the icon '{node.Icon}' of node '{node.NodeId}' is not drawn; the node keeps its shape and label",
                    node.IconLocation,
                    "icons need an icon font that is not available offline (Mermaid does not draw mindmap icons either); remove the ::icon(...) line");
            }

            if (LabelBuilder.ContainsFontAwesome(node.Description))
            {
                sink.Unsupported(
                    MermaidDiagnosticCodes.IconUnsupported,
                    "Font Awesome icons (fa:fa-...) are not drawn; the label is drawn without them",
                    node.Location,
                    "Font Awesome is not available offline; remove the icon from the label");
            }
        }
    }

    /// <summary>
    ///     Checks a node's label as <c>labelHelper</c> prepares it (entities decoded, then sanitized) against the render's
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, before it is measured.
    /// </summary>
    /// <param name="data">The node data.</param>
    /// <exception cref="MermaidRenderException">Thrown, located at the node, when the label is too long.</exception>
    private void CheckLabel(LayoutNode data)
    {
        var limit = _context.Options.Limits.MaxLabelLength;
        var label = data.Label ?? string.Empty;
        _meter.Charge(label.Length + 1L);
        var length = label.Length > limit ? label.Length : HtmlSanitizer.Sanitize(LabelBuilder.DecodeEntities(label)).Length;
        if (length > limit)
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a label is {length} characters long, more than the limit of {limit} (MermaidLimits.MaxLabelLength)"),
                data.Location,
                "shorten the label, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }
    }

    /// <summary>
    ///     Gets an edge end without an intersect function (Mermaid's node copy), so the route is not cut.
    /// </summary>
    /// <param name="center">The node's center.</param>
    /// <param name="size">The node's size.</param>
    /// <returns>The end.</returns>
    private static EdgeEnd End(CosePoint center, CoseSize size) => new(new NodeBox(center.X, center.Y, size.Width, size.Height), null);

    /// <summary>
    ///     Converts a point of the layout.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The point.</returns>
    private static ShapePoint Point(CosePoint point) => new(point.X, point.Y);

    /// <summary>
    ///     Grows a box by another, skipping boxes that are not finite.
    /// </summary>
    /// <param name="bounds">The box so far, or null.</param>
    /// <param name="box">The box to add, or null.</param>
    /// <returns>The union.</returns>
    private static SceneRect? Union(SceneRect? bounds, SceneRect? box)
    {
        if (box is not { } b || !double.IsFinite(b.X) || !double.IsFinite(b.Y) || !double.IsFinite(b.Width) || !double.IsFinite(b.Height))
        {
            return bounds;
        }

        return bounds is { } current ? current.Union(b) : b;
    }
}
