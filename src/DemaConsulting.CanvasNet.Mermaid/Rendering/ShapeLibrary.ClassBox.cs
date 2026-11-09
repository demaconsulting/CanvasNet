// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The class box of a class diagram (<c>classBox.ts</c>, with the text placement of the class diagram's
///     <c>shapeUtil.ts</c>): the first annotation, the bold name, the attributes and the methods, stacked in groups, and
///     the two dividers, inside a rectangle.
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>The style of a static member (<c>parseClassifier</c> of <c>$</c>).</summary>
    private const string UnderlineStyle = "text-decoration:underline;";

    /// <summary>The style of an abstract member (<c>parseClassifier</c> of <c>*</c>).</summary>
    private const string ItalicStyle = "font-style:italic;";

    /// <summary>The room Mermaid adds to the measured width of a class box text before it wraps.</summary>
    private const double ClassTextRoom = 50;

    /// <summary>
    ///     Gets whether a class box draws its texts as HTML labels (<c>evaluate(config.htmlLabels) ?? true</c>): the
    ///     top-level <c>htmlLabels</c> alone decides, and only <c>false</c>, <c>null</c> and the texts <c>false</c>,
    ///     <c>null</c> and <c>0</c> turn them off.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>Whether HTML labels are used.</returns>
    internal static bool ClassHtmlLabels(MermaidConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Raw.TryGetPropertyValue("htmlLabels", out var node))
        {
            return true;
        }

        if (node is not JsonValue value)
        {
            return node is not null;
        }

        var text = value.GetValueKind() switch
        {
            JsonValueKind.False => "false",
            JsonValueKind.Number => ColorMath.FormatNumber(value.GetValue<double>()),
            JsonValueKind.String => value.GetValue<string>(),
            _ => "true",
        };
        return text.Trim().ToLowerInvariant() is not ("false" or "null" or "0");
    }

    /// <summary>
    ///     <c>classBox</c>: the class's texts in groups (the first annotation as <c>«a»</c>, the name in bold, the
    ///     attributes, then the methods), the rectangle around them and the dividers under the name and the attributes,
    ///     placed with Mermaid's arithmetic (including its offsets for an empty class and for
    ///     <c>class.hideEmptyMembersBox</c>).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult ClassBox(ShapeInput input)
    {
        var node = input.Node;
        var box = node.ClassBox ?? new LayoutClassBox(node.Label ?? string.Empty, [], [], [], []);
        var config = input.Context.Config;
        var padding = config.Class.Padding ?? 12;
        var gap = padding;
        var html = ClassHtmlLabels(config);
        var textPadding = html ? 0 : 3;

        // The class's styles: the rectangle, the dividers and the texts take them; the user's text is reported
        var (labelItems, nodeItems) = SplitStyles(box.Styles);
        var labelStyles = StyleDeclarations.ParseItems(labelItems, node.Location, input.Context.Sink);
        var paint = new NodePaint(input.Context.Styles.Node, StyleDeclarations.ParseItems(nodeItems, node.Location, input.Context.Sink), input.Context.Theme);
        var texts = new ClassTexts(input, labelStyles, html);

        // textHelper: the groups, each stacked from its own origin
        var annotations = new ClassGroup("class-annotation");
        if (box.Annotations.Count > 0)
        {
            annotations.Add(texts.Row("\u00ab" + box.Annotations[0] + "\u00bb", 0, string.Empty, false));
        }

        var name = new ClassGroup("class-label");
        name.Add(texts.Row(box.Text, 0, string.Empty, true));
        var members = new ClassGroup("class-member");
        var offset = 0.0;
        foreach (var member in box.Members)
        {
            var row = texts.Row(member.Text, offset, member.CssStyle, false);
            members.Add(row);
            offset += row.Label.Height + textPadding;
        }

        var methods = new ClassGroup("class-method");
        offset = 0;
        foreach (var method in box.Methods)
        {
            var row = texts.Row(method.Text, offset, method.CssStyle, false);
            methods.Add(row);
            offset += row.Label.Height + textPadding;
        }

        var annotationHeight = annotations.Height;
        var labelHeight = name.Height;
        var membersHeight = members.Height <= 0 ? gap / 2 : members.Height;
        annotations.Move(-annotations.Width / 2, 0);
        name.Move(-name.Width / 2, annotationHeight);
        members.Move(0, annotationHeight + labelHeight + (gap * 2));
        methods.Move(0, annotationHeight + labelHeight + (membersHeight != 0 ? membersHeight + (gap * 4) : gap * 2));
        var groups = new[] { annotations, name, members, methods };
        var bounds = groups.Select(group => group.Placed).Aggregate((SceneRect?)null, (all, next) => next is { } b ? all?.Union(b) ?? b : all)
            ?? new SceneRect(0, 0, 0, 0);

        // classBox: the rectangle, centered on the node
        var empty = box.Members.Count == 0 && box.Methods.Count == 0;
        var hideEmpty = config.Class.HideEmptyMembersBox;
        var renderExtraBox = empty && !hideEmpty;
        var w = bounds.Width;
        var h = bounds.Height;
        if (empty)
        {
            h += gap;
        }
        else if (box.Methods.Count == 0)
        {
            h += gap * 2;
        }

        var x = -w / 2;
        var y = -h / 2;
        // The extra height and the upward shift of the rectangle: with the extra box, for an empty class, or none
        var (extraHeight, shift) = (0.0, 0.0);
        if (renderExtraBox)
        {
            (extraHeight, shift) = (padding * 2, padding);
        }
        else if (empty)
        {
            (extraHeight, shift) = (-padding, -padding / 2);
        }

        var rect = new SceneRect(x - padding, y - padding - shift, w + (2 * padding), h + (2 * padding) + extraHeight);

        // The group heights as classBox reads them back (less half the padding with the extra box; JavaScript's || 0)
        var reduce = renderExtraBox ? padding / 2 : 0;
        static double OrZero(double value) => double.IsNaN(value) ? 0 : value;
        var annotationHeight2 = OrZero(annotations.Height - reduce);
        var labelHeight2 = OrZero(name.Height - reduce);
        var membersHeight2 = OrZero(members.Height - reduce);

        // Each group moved under the rectangle's top
        foreach (var group in groups)
        {
            var translateY = group == annotations ? 0 : group.Y;
            var newY = translateY + y + padding - shift;
            if (group == methods)
            {
                newY = annotationHeight2 + labelHeight2 + Math.Max(membersHeight2, gap / 2) + y + (gap * 4) + padding;
            }

            if (empty && hideEmpty)
            {
                newY = box.Annotations.Count > 0 ? translateY - gap : translateY;
            }

            if (!html)
            {
                newY -= 4;
            }

            var newX = group == annotations || group == name ? OrZero(-group.Width / 2) : x;
            group.Move(newX, newY);
        }

        // The drawing: the rectangle (rough.js's fill path and stroke path), the texts, then the dividers
        var draft = new Draft(input);
        var body = Body(paint);
        var outline = new PathData().Rect(rect.X, rect.Y, rect.Width, rect.Height);
        draft.Part(outline, body);
        draft.Part(outline, body);
        foreach (var group in groups)
        {
            group.DrawRows(draft, node);
        }

        if (!empty || renderExtraBox)
        {
            var divider = body with { Fill = null };
            var first = annotationHeight2 + labelHeight2 + y + padding;
            var second = annotationHeight2 + labelHeight2 + membersHeight2 + y + (gap * 2) + padding;
            foreach (var lineY in new[] { first, second })
            {
                draft.Part(new PathData().M(rect.X, lineY).L(rect.Right, lineY + 0.001), divider, role: "class-divider");
            }
        }

        var label = name.Rows.Count > 0 ? name.Rows[0].Label : LabelLayout.Of(html, new SceneRect(0, 0, 0, 0));
        draft.Label = label;
        draft.LabelOffset = name.Rows.Count > 0 ? new ShapePoint(name.X, name.Y + name.Rows[0].Y) : default;
        return draft.Finish(IntersectRect) with { Bounds = rect };
    }

    /// <summary>
    ///     One text of a class box (Mermaid's <c>addText</c>): its measured label and the vertical translation of its
    ///     <c>g.label</c> in its group.
    /// </summary>
    /// <param name="Label">The measured label.</param>
    /// <param name="Y">The translation: minus half the height of a line, plus the row's offset in the group.</param>
    /// <param name="Underline">Whether the text is underlined (a static member).</param>
    private sealed record ClassRow(LabelLayout Label, double Y, bool Underline)
    {
        /// <summary>Gets the box of the row in its group, or null when nothing is drawn.</summary>
        public SceneRect? Box => Label.Width > 0 || Label.Height > 0 ? Label.Box.Offset(0, Y) : null;
    }

    /// <summary>
    ///     A group of a class box (<c>annotation-group</c>, <c>label-group</c>, <c>members-group</c> or
    ///     <c>methods-group</c>): its rows and its translation.
    /// </summary>
    /// <param name="role">The role of the group in the scene.</param>
    private sealed class ClassGroup(string role)
    {
        /// <summary>Gets the rows in order.</summary>
        public List<ClassRow> Rows { get; } = [];

        /// <summary>Gets the translation x of the group.</summary>
        public double X { get; private set; }

        /// <summary>Gets the translation y of the group.</summary>
        public double Y { get; private set; }

        /// <summary>Gets the box of the rows in the group's own coordinates (its <c>getBBox</c>), or null for an empty group.</summary>
        public SceneRect? Own => Rows.Select(row => row.Box).Aggregate((SceneRect?)null, (all, next) => next is { } b ? all?.Union(b) ?? b : all);

        /// <summary>Gets the box of the rows where the group is placed, or null for an empty group.</summary>
        public SceneRect? Placed => Own?.Offset(X, Y);

        /// <summary>Gets the width of the group's box (zero when empty).</summary>
        public double Width => Own?.Width ?? 0;

        /// <summary>Gets the height of the group's box (zero when empty).</summary>
        public double Height => Own?.Height ?? 0;

        /// <summary>
        ///     Adds a row.
        /// </summary>
        /// <param name="row">The row.</param>
        public void Add(ClassRow row) => Rows.Add(row);

        /// <summary>
        ///     Sets the group's translation.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public void Move(double x, double y) => (X, Y) = (x, y);

        /// <summary>
        ///     Draws the rows, each as a group of its texts at its place.
        /// </summary>
        /// <param name="draft">The draft of the node.</param>
        /// <param name="node">The node.</param>
        public void DrawRows(Draft draft, LayoutNode node)
        {
            var id = node.DomId ?? node.Id;
            foreach (var row in Rows)
            {
                if (row.Box is not { } rowBox)
                {
                    continue;
                }

                var items = row.Label.ToScene(0, 0, "label", id, node.Location);
                if (row.Underline)
                {
                    items = [.. items.Select(item => item is SceneText text ? text with { Runs = [.. text.Runs.Select(run => run with { Underline = true })] } : item)];
                }

                draft.Item(new SceneGroup(X, Y + row.Y, items) { Role = role }, rowBox.Offset(X, Y));
            }
        }
    }

    /// <summary>
    ///     Measures the texts of a class box: markdown, wrapped at Mermaid's measured width plus its room, in the node
    ///     label's font with the class's own text styles and the row's style (bold for the name, italic for an abstract
    ///     member).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="labelStyles">The text styles of the class.</param>
    /// <param name="html">Whether HTML labels are used.</param>
    private sealed class ClassTexts(ShapeInput input, StyleDeclarationSet labelStyles, bool html)
    {
        /// <summary>
        ///     Measures one text and places it in its group (<c>addText</c>).
        /// </summary>
        /// <param name="text">The text as the class database holds it.</param>
        /// <param name="offset">The offset of the row in its group.</param>
        /// <param name="style">The classifier style of the row.</param>
        /// <param name="bold">Whether the row is the bold name.</param>
        /// <returns>The row.</returns>
        public ClassRow Row(string text, double offset, string style, bool bold)
        {
            var context = input.Context;
            var css = context.Styles.NodeLabel;
            var font = LabelFontOf(labelStyles, css);
            font = font with
            {
                Bold = labelStyles["font-weight"] is null ? bold : font.Bold,
                Italic = labelStyles["font-style"] is null ? style == ItalicStyle : font.Italic,
            };

            // SVG text drops a leading backslash; the text wraps at its measured width plus room
            if (!html && text.StartsWith('\\'))
            {
                text = text[1..];
            }

            var width = context.TextWidth is { } measure ? measure(text) + ClassTextRoom : double.PositiveInfinity;
            var label = context.MeasureLabel(new LabelRequest(text, font)
            {
                Markdown = true,
                HtmlLabels = html,
                Width = width,
                MarkdownAutoWrap = context.Config.MarkdownAutoWrap,
                RootFontSize = css.FontSize,
                ConfigFontSize = context.Config.FontSize,
                CenterSvgText = false,
                SvgInheritsWeight = true,
                Location = input.Node.Location,
            });

            // The lines: the <br> breaks of an HTML label, the tspans of an SVG one
            var lines = html ? CountBreaks(text) + 1 : Math.Max(label.Lines.Count, 1);
            return new ClassRow(label, (-label.Height / (2 * lines)) + offset, style == UnderlineStyle);
        }

        /// <summary>
        ///     Counts the <c>&lt;br&gt;</c> tags of a text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The count.</returns>
        private static int CountBreaks(string text) => LabelBuilder.SplitBreaks(text).Count - 1;
    }
}
