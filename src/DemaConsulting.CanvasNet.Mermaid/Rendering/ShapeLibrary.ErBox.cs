// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Class;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The entity box of an entity relationship diagram (<c>erBox.ts</c>): an entity without attributes is a plain
///     rectangle around its name; an entity with attributes is a table of the name over one row per attribute, in
///     columns of types, names, keys and comments, with alternating row fills and divider lines, drawn as rough.js
///     draws it in the classic look.
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>The room Mermaid adds to the measured width of an entity box text before it wraps.</summary>
    private const double ErTextRoom = 100;

    /// <summary>The thickness of the polygons Mermaid draws as the divider lines of an entity box.</summary>
    private const double ErDividerThickness = 0.0001;

    /// <summary>The stroke width of the parts rough.js draws when the entity's styles give none.</summary>
    private const double ErRoughStrokeWidth = 1.3;

    /// <summary>The roles of the four cells of an attribute row, in their columns' order.</summary>
    private static readonly string[] ErCellRoles = ["er-attribute-type", "er-attribute-name", "er-attribute-keys", "er-attribute-comment"];

    /// <summary>
    ///     Gets how an entity box treats the top-level <c>htmlLabels</c>, which <c>erBox</c> reads as it is (not through
    ///     <c>getEffectiveHtmlLabels</c>): the texts of a table are HTML labels unless the value is falsy (unset counts as
    ///     HTML, <c>createText</c>'s default), the paddings grow by a quarter unless it is truthy (unset grows them), and the
    ///     name of an entity without attributes is an HTML label when the value is truthy or Mermaid's <c>evaluate</c>
    ///     keeps it (unset is HTML).
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>Whether the table's texts are HTML, whether the paddings grow, and whether a plain name is HTML.</returns>
    internal static (bool Cells, bool Scaled, bool Plain) ErHtmlLabels(MermaidConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Raw.TryGetPropertyValue("htmlLabels", out var node))
        {
            return (true, true, true);
        }

        // JavaScript's truthiness of the value
        var truthy = node switch
        {
            null => false,
            System.Text.Json.Nodes.JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
            System.Text.Json.Nodes.JsonValue value when value.TryGetValue<double>(out var number) => number != 0 && !double.IsNaN(number),
            System.Text.Json.Nodes.JsonValue value when value.TryGetValue<string>(out var text) => text.Length > 0,
            _ => true,
        };
        return (truthy, !truthy, truthy || ClassHtmlLabels(config));
    }

    /// <summary>
    ///     <c>erBox</c>: an entity. Without attributes, its name (or alias) inside a rectangle with the diagram padding
    ///     left and right and one and a half times it above and below, at least <c>er.minEntityWidth</c> wide when the
    ///     name is narrow. With attributes, a table: the name centered in the top row, then one row per attribute with
    ///     its type, name, keys and comment in columns (an empty keys or comment column is dropped, and any width the
    ///     name needs more is shared among the columns), the rows filled <c>rowOdd</c> and <c>rowEven</c> in turn, and the
    ///     dividers under the name and between the columns. With SVG text (and when <c>htmlLabels</c> is unset) the
    ///     paddings grow by a quarter.
    /// </summary>
    /// <remarks>
    ///     The parts are painted as rough.js paints them: the outline in the entity's <c>fill</c> (else <c>mainBkg</c>),
    ///     the rows in the theme's row colors and every line in the entity's <c>stroke</c> (else <c>nodeBorder</c>) at its
    ///     <c>stroke-width</c> (else 1.3). When the entity has styles of its own, Mermaid writes their stroke properties on
    ///     every path and all of them on the even rows, so an even row takes the entity's fill and the odd rows keep
    ///     theirs. The box of the node is everything drawn: with SVG text an empty cell counts as a point where it is
    ///     placed (with HTML labels it does not), which can make the node wider than its table.
    /// </remarks>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when the node is not an entity of an ER diagram.</exception>
    private static ShapeResult ErBox(ShapeInput input)
    {
        var node = input.Node;
        var box = node.ErBox ?? throw new MermaidRenderException(
            MermaidDiagnosticCodes.RenderFailed,
            "the shape 'erBox' draws only the entities of an entity relationship diagram",
            node.Location,
            "draw entities in an erDiagram; for a flowchart node, use another shape, for example 'rect'");
        var config = input.Context.Config;
        var (cellsHtml, scaled, plainHtml) = ErHtmlLabels(config);
        var name = box.Alias.Length > 0 ? box.Alias : node.Label ?? string.Empty;
        var padding = config.Er.DiagramPadding;
        var textPadding = config.Er.EntityPadding;
        if (box.Attributes.Count == 0 && name.Length > 0)
        {
            return ErPlainBox(input, name, padding, plainHtml);
        }

        if (scaled)
        {
            padding *= 1.25;
            textPadding *= 1.25;
        }

        // The texts, each measured where addText first puts it: the name at the origin, each row at its offset
        var nameText = ErText(input, name, cellsHtml);
        var nameHeight = nameText.Height + textPadding;
        var rows = new List<(LabelLayout[] Cells, double Y, double Height)>();
        var widths = new double[4];
        var offset = 0.0;
        foreach (var attribute in box.Attributes)
        {
            LabelLayout[] cells =
            [
                ErText(input, attribute.Type, cellsHtml),
                ErText(input, attribute.Name, cellsHtml),
                ErText(input, string.Join(",", attribute.Keys), cellsHtml),
                ErText(input, attribute.Comment, cellsHtml),
            ];
            for (var i = 0; i < 4; i++)
            {
                widths[i] = Math.Max(widths[i], cells[i].Width + padding);
            }

            var height = cells.Max(cell => cell.Height) + textPadding;
            rows.Add((cells, offset, height));
            offset += height;
        }

        // An empty keys or comment column is dropped
        var sections = 4;
        var keysPresent = widths[2] > padding;
        var commentPresent = widths[3] > padding;
        if (!keysPresent)
        {
            widths[2] = 0;
            sections--;
        }

        if (!commentPresent)
        {
            widths[3] = 0;
            sections--;
        }

        // The box of the texts before they move, and the width the name needs more, shared among the live columns
        var texts = ErCellBox(nameText, cellsHtml, 0, 0);
        foreach (var row in rows)
        {
            foreach (var cell in row.Cells)
            {
                texts = Union(texts, ErCellBox(cell, cellsHtml, 0, row.Y));
            }
        }

        var difference = nameText.Width + (padding * 2) - widths.Sum();
        if (difference > 0)
        {
            for (var i = 0; i < 4; i++)
            {
                widths[i] += i < 2 || widths[i] > 0 ? difference / sections : 0;
            }
        }

        var w = Math.Max((texts?.Width ?? 0) + (padding * 2), widths.Sum());
        var h = rows.Sum(row => row.Height) + nameHeight;
        var x = -w / 2;
        var y = -h / 2;

        // The outline, then the rows (each inserted before the name), then the texts, then the dividers
        var draft = new Draft(input);
        var paint = new ErPaint(input);
        var outline = new PathData().Rect(x, y, w, h);
        draft.Part(outline, paint.FillPath(paint.Fill));
        draft.Part(outline, paint.StrokePath);
        foreach (var (row, index) in rows.Select((row, index) => (row, index)))
        {
            var even = (index + 1) % 2 == 0 && row.Y != 0;
            var rect = new PathData().Rect(x, nameHeight + y + row.Y, w, row.Height);
            draft.Part(rect, paint.Row(even, fill: true));
            draft.Part(rect, paint.Row(even, fill: false));
        }

        var nameOffset = new ShapePoint(-nameText.Width / 2, y + (textPadding / 2));
        ErDrawCell(draft, input, nameText, cellsHtml, "er-name", nameOffset);
        foreach (var row in rows)
        {
            var columnX = 0.0;
            for (var i = 0; i < 4; i++)
            {
                var cellOffset = new ShapePoint(x + (padding / 2) + columnX, row.Y + y + nameHeight + (textPadding / 2));
                ErDrawCell(draft, input, row.Cells[i], cellsHtml, ErCellRoles[i], cellOffset);
                columnX += widths[i];
            }
        }

        // The dividers: under the name, between the columns, and under the name again (Mermaid's first row line)
        var top = nameHeight + y;
        ErDivider(draft, paint, false, x, top, w + x, top);
        ErDivider(draft, paint, true, widths[0] + x, top, widths[0] + x, h + y);
        if (keysPresent)
        {
            ErDivider(draft, paint, true, widths[0] + widths[1] + x, top, widths[0] + widths[1] + x, h + y);
        }

        if (commentPresent)
        {
            var columnX = widths[0] + widths[1] + widths[2] + x;
            ErDivider(draft, paint, true, columnX, top, columnX, h + y);
        }

        ErDivider(draft, paint, false, x, top, w + x, top);
        draft.Label = nameText;
        draft.LabelOffset = nameOffset;
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     An entity without attributes: Mermaid's <c>drawRect</c> with the diagram padding left and right and one and a
    ///     half times it above and below, as wide as <c>er.minEntityWidth</c> when the name and its padding are narrower,
    ///     the name centered (an SVG name is moved left by half its width, as <c>erBox</c> does).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="name">The name or alias.</param>
    /// <param name="padding">The diagram padding.</param>
    /// <param name="html">Whether the name is an HTML label.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult ErPlainBox(ShapeInput input, string name, double padding, bool html)
    {
        var context = input.Context;
        var config = context.Config;
        var minWidth = config.Er.MinEntityWidth;
        var nodeWidth = context.TextWidth is { } measure && measure(name) + (padding * 2) < minWidth ? minWidth : 0;
        var wrap = nodeWidth > 0 ? nodeWidth : config.Flowchart.WrappingWidth;
        var css = context.Styles.NodeLabel;
        var label = context.MeasureLabel(new LabelRequest(name, LabelFontOf(input.LabelStyles, css))
        {
            Markdown = input.Node.LabelType == "markdown",
            HtmlLabels = html,
            Width = wrap > 0 ? wrap : 200,
            MarkdownAutoWrap = config.MarkdownAutoWrap,
            RootFontSize = css.FontSize,
            ConfigFontSize = config.FontSize,
            CenterSvgText = false,
            Location = input.Node.Location,
        });

        var w = Math.Max(label.Width + (padding * 2), nodeWidth);
        var h = label.Height + (padding * 3);
        var draft = new Draft(input);
        draft.Part(new PathData().Rect(-w / 2, -h / 2, w, h), Body(input.Paint));
        draft.PlaceLabel(label, new ShapePoint(-label.Width / 2, -label.Height / 2));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     Measures a text of an entity table (<c>addText</c>): a markdown string in the node label's font with the
    ///     entity's text styles, wrapped at its measured width plus Mermaid's room (so never in practice); a type with
    ///     generics is written in TypeScript syntax (<c>~T~</c> as <c>&lt;T&gt;</c>), its angle brackets kept as text, and
    ///     the lines of an HTML label start at its left, as Mermaid aligns them once the label is measured.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="text">The text.</param>
    /// <param name="html">Whether the text is an HTML label.</param>
    /// <returns>The measured text.</returns>
    private static LabelLayout ErText(ShapeInput input, string text, bool html)
    {
        var context = input.Context;
        var generic = ClassModel.ParseGenericTypes(text);
        if (generic != text)
        {
            text = generic.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
        }

        var css = context.Styles.NodeLabel;
        var width = context.TextWidth is { } measure ? measure(text) + ErTextRoom : double.PositiveInfinity;
        var label = context.MeasureLabel(new LabelRequest(text, LabelFontOf(input.LabelStyles, css))
        {
            Markdown = true,
            HtmlLabels = html,
            Width = width,
            MarkdownAutoWrap = context.Config.MarkdownAutoWrap,
            RootFontSize = css.FontSize,
            ConfigFontSize = context.Config.FontSize,
            CenterSvgText = false,
            Location = input.Node.Location,
        });
        if (!html || label.Lines.Count < 2)
        {
            return label;
        }

        return label with { Lines = [.. label.Lines.Select(line => line with { X = 0, Box = line.Box.Offset(-line.X, 0) })] };
    }

    /// <summary>
    ///     Gets the box a text of an entity table adds to the node's box where it is placed: its measured box, or for an
    ///     empty text nothing with HTML labels (a foreignObject of no size) and a point with SVG text (an empty text
    ///     element counts).
    /// </summary>
    /// <param name="text">The measured text.</param>
    /// <param name="html">Whether the text is an HTML label.</param>
    /// <param name="dx">The translation x of the text's group.</param>
    /// <param name="dy">The translation y of the text's group.</param>
    /// <returns>The box, or null.</returns>
    private static SceneRect? ErCellBox(LabelLayout text, bool html, double dx, double dy)
    {
        if (text.Width > 0 || text.Height > 0)
        {
            return text.Box.Offset(dx, dy);
        }

        return html ? null : new SceneRect(dx, dy, 0, 0);
    }

    /// <summary>
    ///     Draws a text of an entity table as a group of its role at its place; an empty SVG text keeps an unpainted point
    ///     so that it counts in the node's box.
    /// </summary>
    /// <param name="draft">The draft of the node.</param>
    /// <param name="input">The node.</param>
    /// <param name="text">The measured text.</param>
    /// <param name="html">Whether the text is an HTML label.</param>
    /// <param name="role">The role of the group.</param>
    /// <param name="offset">The translation of the group.</param>
    private static void ErDrawCell(Draft draft, ShapeInput input, LabelLayout text, bool html, string role, ShapePoint offset)
    {
        var node = input.Node;
        var box = ErCellBox(text, html, offset.X, offset.Y);
        IReadOnlyList<SceneItem> items = box is { Width: 0, Height: 0 } && !html
            ? [new ScenePath([SceneSegment.Move(0, 0)]) { Role = "er-empty-text" }]
            : text.ToScene(0, 0, "label", node.DomId ?? node.Id, node.Location);
        draft.Item(new SceneGroup(offset.X, offset.Y, items) { Role = role }, box);
    }

    /// <summary>
    ///     Draws a divider of an entity table as Mermaid does: a polygon a ten-thousandth of a pixel thick around the line,
    ///     filled and then stroked.
    /// </summary>
    /// <param name="draft">The draft of the node.</param>
    /// <param name="paint">The paint of the entity.</param>
    /// <param name="vertical">Whether the line is vertical (else horizontal).</param>
    /// <param name="x1">The x of the start.</param>
    /// <param name="y1">The y of the start.</param>
    /// <param name="x2">The x of the end.</param>
    /// <param name="y2">The y of the end.</param>
    private static void ErDivider(Draft draft, ErPaint paint, bool vertical, double x1, double y1, double x2, double y2)
    {
        const double half = ErDividerThickness / 2;
        ShapePoint[] points = vertical
            ? [new(x1 - half, y1), new(x1 + half, y1), new(x2 + half, y2), new(x2 - half, y2)]
            : [new(x1, y1 - half), new(x1, y1 + half), new(x2, y2 + half), new(x2, y2 - half)];
        var polygon = new PathData().Polygon(points);
        draft.Part(polygon, paint.FillPath(paint.Fill), role: "er-divider");
        draft.Part(polygon, paint.StrokePath, role: "er-divider");
    }

    /// <summary>
    ///     Unites two optional boxes.
    /// </summary>
    /// <param name="a">The first box, or null.</param>
    /// <param name="b">The second box, or null.</param>
    /// <returns>The union, or null when both are null.</returns>
    private static SceneRect? Union(SceneRect? a, SceneRect? b)
    {
        if (a is not { } first)
        {
            return b;
        }

        return b is { } second ? first.Union(second) : first;
    }

    /// <summary>
    ///     The paint of an entity table's parts, as rough.js writes it and Mermaid's styles override it.
    /// </summary>
    private sealed class ErPaint
    {
        /// <summary>The node.</summary>
        private readonly ShapeInput _input;

        /// <summary>
        ///     The stroke of rough.js's fill paths: none (their stroke width is zero), unless the entity's own styles set
        ///     both a stroke and a stroke width, which Mermaid writes on every path.
        /// </summary>
        private readonly Rgba32? _fillPathStroke;

        /// <summary>The stroke width.</summary>
        private readonly double _width;

        /// <summary>The dash lengths, or null.</summary>
        private readonly IReadOnlyList<double>? _dash;

        /// <summary>
        ///     Initializes the paint of an entity from its styles and the theme (<c>userNodeOverrides</c>).
        /// </summary>
        /// <param name="input">The node.</param>
        public ErPaint(ShapeInput input)
        {
            _input = input;
            var user = input.User;
            var theme = input.Context.Theme;
            Fill = user["fill"] is not null ? user.Paint("fill") : theme.MainBkg;
            var userStroke = user["stroke"] is not null ? user.Paint("stroke") : null;
            Stroke = userStroke ?? theme.NodeBorder;
            _fillPathStroke = user["stroke-width"] is not null ? userStroke : null;
            _width = user.Length("stroke-width", ErRoughStrokeWidth);
            _dash = user["stroke-dasharray"] is { } dashes && StyleDeclarations.TryParseDashArray(dashes, out var parsed) ? parsed : null;
        }

        /// <summary>Gets the fill of the outline and the dividers: the entity's <c>fill</c>, else <c>mainBkg</c>.</summary>
        public Rgba32? Fill { get; }

        /// <summary>Gets the stroke of every line: the entity's <c>stroke</c>, else <c>nodeBorder</c>.</summary>
        public Rgba32? Stroke { get; }

        /// <summary>Gets the paint of rough.js's stroke path.</summary>
        public PartPaint StrokePath => new(null, Stroke, _width, _dash);

        /// <summary>
        ///     Gets the paint of rough.js's fill path, which only the stroke and stroke width of the entity's own styles stroke.
        /// </summary>
        /// <param name="fill">The fill.</param>
        /// <returns>The paint.</returns>
        public PartPaint FillPath(Rgba32? fill) => new(fill, _fillPathStroke, _width, _dash);

        /// <summary>
        ///     Gets the paint of a row's fill or stroke path: <c>rowOdd</c> or <c>rowEven</c>, and on an even row every
        ///     style of the entity's own.
        /// </summary>
        /// <param name="even">Whether the row is even.</param>
        /// <param name="fill">Whether the paint is the fill path's.</param>
        /// <returns>The paint.</returns>
        public PartPaint Row(bool even, bool fill)
        {
            var rowFill = ThemeColor(_input.Context, even ? "rowEven" : "rowOdd");
            var paint = fill ? FillPath(rowFill) : StrokePath;
            if (!even)
            {
                return paint;
            }

            // An even row takes every style of the entity's own: its fill on both paths, and its opacity
            var user = _input.User;
            var opacity = 1.0;
            if (user["opacity"] is { } value && StyleDeclarations.TryParseOpacity(value, out var factor))
            {
                opacity = factor;
            }

            return paint with { Fill = user["fill"] is not null ? user.Paint("fill") : paint.Fill, Opacity = opacity };
        }
    }
}
