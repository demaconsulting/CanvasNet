// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The terminal labels of an edge (<c>edges.js</c>: the <c>g.edgeTerminals</c> groups of <c>insertEdgeLabel</c>,
///     placed by <c>positionEdgeLabel</c> with <c>utils.ts</c>'s <c>calcTerminalLabelPosition</c>): a class relation's
///     cardinalities, beside the start and the end of its drawn route.
/// </content>
internal static partial class EdgeBuilder
{
    /// <summary>The distance along the route to the point a terminal label is placed beside (<c>calcTerminalLabelPosition</c>'s 25).</summary>
    private const double TerminalDistance = 25;

    /// <summary>The marker size <c>positionEdgeLabel</c> passes for an end with an arrow type (any non-empty type, <c>none</c> included).</summary>
    private const double TerminalMarkerSize = 10;

    /// <summary>The offset of a terminal label from the route, before the marker's share (<c>calcTerminalLabelPosition</c>'s 10).</summary>
    private const double TerminalOffset = 10;

    /// <summary>The shift of an end label up and to the left (<c>calcTerminalLabelPosition</c>'s 5 for <c>end_left</c>).</summary>
    private const double TerminalEndShift = 5;

    /// <summary>The width per character Mermaid gives the <c>foreignObject</c> of an HTML terminal label (<c>setTerminalWidth</c>).</summary>
    internal const double TerminalCharacterWidth = 9;

    /// <summary>The height Mermaid gives the <c>foreignObject</c> of an HTML terminal label (<c>setTerminalWidth</c>).</summary>
    internal const double TerminalHeight = 12;

    /// <summary>
    ///     Measures a terminal label as <c>insertEdgeLabel</c> creates it (<c>createLabel</c>: a plain string, never
    ///     wrapped, with the edge's label styles, in the <c>.edgeTerminals</c> font).
    /// </summary>
    /// <param name="edge">The edge.</param>
    /// <param name="text">The terminal label; not empty.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The measured label.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static LabelLayout MeasureTerminal(LayoutEdge edge, string text, ShapeContext context)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(context);

        // The label styles were reported when the edge's own label was measured
        var html = context.Config.HtmlLabels;
        var css = context.Styles.TerminalLabel(html);
        return context.MeasureLabel(new LabelRequest(text, ShapeLibrary.LabelFontOf(LabelStylesOf(edge, null), css))
        {
            Markdown = false,
            HtmlLabels = html,
            Width = double.PositiveInfinity,
            IsNode = false,
            AddBackground = false,
            Prepare = false,
            MarkdownAutoWrap = context.Config.MarkdownAutoWrap,
            RootFontSize = css.FontSize,
            ConfigFontSize = context.Config.FontSize,
            CenterSvgText = true,
            Location = edge.Location,
        });
    }

    /// <summary>
    ///     Gets where a terminal label goes (Mermaid's <c>calcTerminalLabelPosition</c> for <c>start_right</c> and
    ///     <c>end_left</c>): the point 25 pixels (plus the marker size) along the route from its start, or from its end,
    ///     and the position beside the middle of the route's first part, 10 pixels (plus half the marker size) to its
    ///     right; an end label is moved 5 pixels up and to the left.
    /// </summary>
    /// <param name="markerSize">The marker size (10 for an end with an arrow type, else 0).</param>
    /// <param name="atEnd">Whether the label is the end label (the route is walked from its end).</param>
    /// <param name="points">The drawn route (<c>paths.updatedPath</c>, else <c>paths.originalPath</c>).</param>
    /// <returns>The position, or null when the route is too short to walk (where Mermaid's <c>calculatePoint</c> throws).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    internal static ShapePoint? CalcTerminalLabelPosition(double markerSize, bool atEnd, IReadOnlyList<ShapePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var route = points.ToList();
        if (atEnd)
        {
            route.Reverse();
        }

        if (CalculatePoint(route, TerminalDistance + markerSize) is not { } center)
        {
            return null;
        }

        // Beside the middle of the walked part, on the right of the direction toward the route's first point
        var d = TerminalOffset + (markerSize * 0.5);
        var first = route[0];
        var angle = Math.Atan2(first.Y - center.Y, first.X - center.X);
        var x = (Math.Sin(angle) * d) + ((first.X + center.X) / 2);
        var y = (-Math.Cos(angle) * d) + ((first.Y + center.Y) / 2);
        return atEnd ? new ShapePoint(x - TerminalEndShift, y - TerminalEndShift) : new ShapePoint(x, y);
    }

    /// <summary>
    ///     Draws the terminal labels of a drawn edge: its start label, then its end label, each measured and placed
    ///     beside the drawn route.
    /// </summary>
    /// <remarks>
    ///     A start label is centered on its position (its inner group's centering transform); an end label is not,
    ///     because Mermaid appends it to the outer group, so its box starts at its position. An HTML label's box is that of
    ///     its <c>foreignObject</c>, which <c>setTerminalWidth</c> sizes to 9 pixels per character by 12 pixels.
    /// </remarks>
    /// <param name="edge">The edge.</param>
    /// <param name="id">The id of the drawn edge.</param>
    /// <param name="startLabel">The start label (<c>startLabelRight</c>), empty for none.</param>
    /// <param name="endLabel">The end label (<c>endLabelLeft</c>), empty for none.</param>
    /// <param name="arrowTypeStart">The arrow type at the start, which sets the start label's marker size.</param>
    /// <param name="arrowTypeEnd">The arrow type at the end.</param>
    /// <param name="drawing">The drawn edge.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The groups of the labels and their box (no items when the edge has no terminal labels).</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="MermaidRenderException">
    ///     Thrown, located at the edge, when the drawn route is too short to place a label, as Mermaid's render fails on
    ///     such a diagram (<c>Could not find a suitable point for the given distance</c>).
    /// </exception>
    public static EdgeLabelDrawing DrawTerminals(
        LayoutEdge edge,
        string id,
        string startLabel,
        string endLabel,
        string? arrowTypeStart,
        string? arrowTypeEnd,
        EdgeDrawing drawing,
        ShapeContext context)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(startLabel);
        ArgumentNullException.ThrowIfNull(endLabel);
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(context);
        var items = new List<SceneItem>();
        SceneRect? bounds = null;
        var route = drawing.UpdatedPath ?? drawing.OriginalPath;
        foreach (var (text, arrowType, atEnd) in new[] { (startLabel, arrowTypeStart, false), (endLabel, arrowTypeEnd, true) })
        {
            if (text.Length == 0)
            {
                continue;
            }

            // An arrow type that JavaScript reads as true (any non-empty type, none included) widens the offsets
            var markerSize = string.IsNullOrEmpty(arrowType) ? 0 : TerminalMarkerSize;
            var position = CalcTerminalLabelPosition(markerSize, atEnd, route) ?? throw new MermaidRenderException(
                MermaidDiagnosticCodes.RenderFailed,
                $"the terminal label '{text}' of the link '{edge.Id}' cannot be placed: the link is too short (Mermaid fails on this diagram too)",
                edge.Location,
                "lengthen the link, for example with more rank spacing");
            var label = MeasureTerminal(edge, text, context);
            var drawn = DrawTerminal(label, text, position, atEnd, edge, id);
            items.AddRange(drawn.Items);
            if (drawn.Bounds is { } box)
            {
                bounds = bounds is { } current ? current.Union(box) : box;
            }
        }

        return new EdgeLabelDrawing(items, bounds);
    }

    /// <summary>
    ///     Draws one placed terminal label.
    /// </summary>
    /// <param name="label">The measured label.</param>
    /// <param name="text">The label text (its length sizes an HTML label's box).</param>
    /// <param name="position">The position of the label's group.</param>
    /// <param name="atEnd">Whether the label is the end label (not centered on its position).</param>
    /// <param name="edge">The edge.</param>
    /// <param name="id">The id of the drawn edge.</param>
    /// <returns>The group and its box.</returns>
    internal static EdgeLabelDrawing DrawTerminal(LabelLayout label, string text, ShapePoint position, bool atEnd, LayoutEdge edge, string id)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edge);

        // computeLabelTransform of the inner group: HTML labels by their box, SVG labels by the box of their text; an end
        // label is in the outer group, which has no transform
        var (dx, dy) = (0.0, 0.0);
        if (!atEnd)
        {
            (dx, dy) = label.IsHtml
                ? (-label.Width / 2, -label.Height / 2)
                : (-(label.Box.X + (label.Box.Width / 2)), -(label.Box.Y + (label.Box.Height / 2)));
        }

        var box = label.IsHtml
            ? new SceneRect(dx, dy, text.Length * TerminalCharacterWidth, TerminalHeight)
            : label.Box.Offset(dx, dy);
        var children = label.ToScene(dx, dy, "label", id, edge.Location);
        var group = new SceneGroup(position.X, position.Y, children) { Role = "edge-terminal", Id = id, Location = edge.Location };
        return new EdgeLabelDrawing([group], box.Offset(position.X, position.Y));
    }
}
