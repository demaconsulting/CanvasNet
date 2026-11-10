// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The shapes a state diagram adds: the state with descriptions (<c>rectWithTitle.ts</c>) and the clusters of
///     <c>clusters.js</c> for a composite state (<c>roundedWithTitle</c>), a concurrency region (<c>divider</c>) and a
///     note with its state (<c>noteGroup</c>); and the paint of the parts that rough.js draws, which only a sheet's
///     <c>.node path</c> rule overrides.
/// </content>
/// <remarks>
///     The parts are painted through the selectors of the diagram's style sheet, in the order of their specificity, so a
///     sheet without a selector (a flowchart's, for one) leaves the part as Mermaid's attributes paint it. A selector that
///     names an ancestor class (<c>.statediagram-state</c>, <c>.statediagram-cluster</c>) applies only when the node
///     carries that class.
/// </remarks>
internal static partial class ShapeLibrary
{
    /// <summary>The class of every state node and cluster (<c>CSS_DIAGRAM_STATE</c>).</summary>
    private const string StateClass = "statediagram-state";

    /// <summary>The class of a composite state's cluster (<c>CSS_DIAGRAM_CLUSTER</c>).</summary>
    private const string StateClusterClass = "statediagram-cluster";

    /// <summary>The class of a composite state's cluster at an alternate depth (<c>CSS_DIAGRAM_CLUSTER_ALT</c>).</summary>
    private const string StateClusterAltClass = "statediagram-cluster-alt";

    /// <summary>
    ///     Gets the paint of a part that rough.js draws as a path with paint attributes: the sheet's <c>.node path</c> rule
    ///     wins over each attribute it declares (a flowchart's and a class diagram's sheet have the rule; a state and an
    ///     entity relationship diagram's do not), and the node's own styles win over both.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="fill">The fill attribute.</param>
    /// <param name="stroke">The stroke attribute.</param>
    /// <param name="strokeWidth">The stroke width attribute.</param>
    /// <returns>The part paint.</returns>
    private static PartPaint RoughPaint(ShapeInput input, Rgba32? fill, Rgba32? stroke, double strokeWidth)
    {
        var rule = input.Context.Styles.Resolve(".node path");
        return Body(
            input.Paint,
            rule["fill"] is not null ? rule.Paint("fill") : fill,
            rule["stroke"] is not null ? rule.Paint("stroke") : stroke,
            rule.Length("stroke-width", strokeWidth));
    }

    /// <summary>
    ///     Gets a theme color by its variable name, or null when the variable is not set or not a color.
    /// </summary>
    /// <param name="context">The settings of the rendering.</param>
    /// <param name="name">The variable name.</param>
    /// <returns>The color, or null.</returns>
    private static Rgba32? ThemeColor(ShapeContext context, string name) =>
        context.Config.ThemeVariable(name) is { } value && StyleDeclarations.TryParsePaint(value, out var color) ? color : null;

    /// <summary>
    ///     Gets the fill of an end state's inner circle (<c>stateBorder ?? nodeBorder</c>).
    /// </summary>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The color, or null.</returns>
    private static Rgba32? StateBorder(ShapeContext context) =>
        context.Config.ThemeVariable("stateBorder") is not null ? ThemeColor(context, "stateBorder") : context.Theme.NodeBorder;

    /// <summary>
    ///     Gets whether a list of CSS classes holds a class.
    /// </summary>
    /// <param name="classes">The classes, separated by spaces.</param>
    /// <param name="name">The class.</param>
    /// <returns>Whether it does.</returns>
    private static bool HasClass(string classes, string name) =>
        classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal);

    /// <summary>
    ///     Cascades the rules of selectors given in the order of their specificity, each only when its condition holds.
    /// </summary>
    /// <param name="styles">The style sheet.</param>
    /// <param name="selectors">The selectors and whether each applies.</param>
    /// <returns>The cascaded declarations.</returns>
    private static StyleDeclarationSet Rules(UnifiedStyles styles, params (string Selector, bool Applies)[] selectors) =>
        selectors.Where(s => s.Applies).Aggregate(StyleDeclarationSet.Empty, (set, s) => set.Cascade(styles.Resolve(s.Selector)));

    /// <summary>
    ///     Gets the corner radius the CSS <c>rx</c> and <c>ry</c> of a rule give a <c>&lt;rect&gt;</c>, which win over its
    ///     attributes: <c>rx</c> (an <c>auto</c> radius takes the other), zero when either is zero, or the attribute radius
    ///     when the rule declares neither.
    /// </summary>
    /// <param name="rule">The cascaded rule of the rectangle.</param>
    /// <param name="radius">The attribute radius.</param>
    /// <returns>The radius.</returns>
    private static double CssRadius(StyleDeclarationSet rule, double radius)
    {
        var rx = rule.Length("rx", double.NaN);
        var ry = rule.Length("ry", double.NaN);
        if (double.IsNaN(rx) && double.IsNaN(ry))
        {
            return radius;
        }

        rx = double.IsNaN(rx) ? ry : rx;
        ry = double.IsNaN(ry) ? rx : ry;
        return rx == 0 || ry == 0 ? 0 : rx;
    }

    /// <summary>
    ///     Gets the paint of a part from a cascaded rule: its fill, stroke, width (1 when none) and dashes.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The part paint.</returns>
    private static PartPaint RulePaint(StyleDeclarationSet rule)
    {
        var dash = rule["stroke-dasharray"] is { } dashes && StyleDeclarations.TryParseDashArray(dashes, out var parsed) ? parsed : null;
        return new PartPaint(rule.Paint("fill"), rule.Paint("stroke"), rule.Length("stroke-width", 1), dash);
    }

    /// <summary>
    ///     Gets the corner radius of a <c>drawRect</c> rectangle (class <c>basic label-container</c>): the attribute radius,
    ///     unless a state's <c>.statediagram-state rect.basic</c> rule sets one.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="radius">The attribute radius.</param>
    /// <returns>The radius.</returns>
    private static double BasicRectRadius(ShapeInput input, double radius) =>
        CssRadius(Rules(input.Context.Styles, (".statediagram-state rect.basic", HasClass(input.Node.CssClasses, StateClass))), radius);

    /// <summary>
    ///     <c>rectWithTitle</c>: a state with descriptions. The title (the first description) and the other descriptions,
    ///     joined by line breaks, are measured as plain labels, centered over each other with 5 pixels and half the padding
    ///     between them, inside a rectangle with the padding around, with a divider line under the title.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult RectWithTitle(ShapeInput input)
    {
        var node = input.Node;
        var context = input.Context;
        var draft = new Draft(input);
        var halfPadding = node.Padding / 2;
        var title = MeasurePlainLabel(context, input.LabelStyles, node.Label ?? string.Empty, context.Styles.NodeLabel, node.Location);
        var description = MeasurePlainLabel(context, input.LabelStyles, string.Join("<br/>", node.Descriptions ?? []), context.Styles.NodeLabel, node.Location);

        // The wider text starts at 0 and the narrower is centered on it; the descriptions go under the title
        var titleDx = description.Width < title.Width ? 0 : (description.Width - title.Width) / 2;
        var descriptionDx = description.Width > title.Width ? 0 : (title.Width - description.Width) / 2;
        var descriptionDy = title.Height + halfPadding + 5;
        var box = title.Box.Offset(titleDx, 0).Union(description.Box.Offset(descriptionDx, descriptionDy));

        // The label group is centered by its size alone, then the rectangle and the divider are drawn around it
        var (w, h) = (box.Width, box.Height);
        var labelX = -w / 2;
        var labelY = (-h / 2) - halfPadding + 3;
        var x = (-w / 2) - halfPadding;
        var y = (-h / 2) - halfPadding;
        var isState = HasClass(node.CssClasses, StateClass);
        var radius = CssRadius(Rules(context.Styles, (".statediagram-state .title-state", isState)), 0);
        draft.Part(new PathData().Rect(x, y, w + node.Padding, h + node.Padding, radius), Body(input.Paint));
        var dividerY = y + title.Height + halfPadding;
        var divider = Rules(context.Styles, (".divider", true), (".statediagram-state .divider", isState));
        draft.Part(new PathData().M(x, dividerY).L((w / 2) + halfPadding, dividerY), RulePaint(divider) with { Fill = null }, role: "state-divider");

        // The title, then the descriptions
        draft.PlaceLabel(title, new ShapePoint(labelX + titleDx, labelY));
        var descriptionOffset = new ShapePoint(labelX + descriptionDx, labelY + descriptionDy);
        var descriptionBox = description.Box.Offset(descriptionOffset.X, descriptionOffset.Y);
        foreach (var item in description.ToScene(descriptionOffset.X, descriptionOffset.Y, "label", node.DomId ?? node.Id, node.Location))
        {
            draft.Item(item, descriptionBox);
        }

        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     Draws a state diagram's cluster: a composite state (<c>roundedWithTitle</c>), a concurrency region
    ///     (<c>divider</c>) or the invisible group of a note and its state (<c>noteGroup</c>).
    /// </summary>
    /// <param name="node">The cluster's node.</param>
    /// <param name="x">The x of the center the layout gave it.</param>
    /// <param name="y">The y of the center.</param>
    /// <param name="width">The width the layout gave it.</param>
    /// <param name="height">The height the layout gave it.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn cluster, or null when the node's shape is not one of the three.</returns>
    private static ClusterShape? DrawStateCluster(LayoutNode node, double x, double y, double width, double height, ShapeContext context)
    {
        // These clusters draw none of the node's own styles
        var plain = node with { CssStyles = null, CssCompiledStyles = [], LabelStyle = string.Empty };
        return node.Shape switch
        {
            "roundedWithTitle" => RoundedWithTitle(plain, x, y, width, height, context),
            "divider" => DividerCluster(plain, x, y, width, height, context),
            "noteGroup" => NoteGroup(plain, x, y, width, height, context),
            _ => null,
        };
    }

    /// <summary>
    ///     <c>roundedWithTitle</c>: a composite state. The outer rectangle (widened to the title and the padding when the
    ///     title is wider) holds the title at its top, and the inner rectangle fills the rest below the title, in the
    ///     composite background or, at an alternate depth, in the alternate background.
    /// </summary>
    /// <param name="node">The cluster's node.</param>
    /// <param name="x">The x of the center.</param>
    /// <param name="y">The y of the center.</param>
    /// <param name="width">The width the layout gave it.</param>
    /// <param name="height">The height the layout gave it.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn cluster.</returns>
    private static ClusterShape RoundedWithTitle(LayoutNode node, double x, double y, double width, double height, ShapeContext context)
    {
        var input = new ShapeInput(node, context, "roundedWithTitle");
        var draft = new Draft(input);
        var html = context.Config.HtmlLabels;
        var css = context.Styles.ClusterTitle(html);
        var label = MeasurePlainLabel(context, StyleDeclarationSet.Empty, node.Label ?? string.Empty, css, node.Location);

        // The rectangle is widened to the title and its padding, as clusters.js's rect does
        var padding = node.Padding;
        var wide = width <= label.Width + padding;
        var rectWidth = wide ? label.Width + padding : width;
        var diff = wide ? ((rectWidth - width) / 2) - padding : -padding;
        var left = x - (rectWidth / 2);
        var top = y - (height / 2);
        var isCluster = HasClass(node.CssClasses, StateClusterClass);
        var outer = Rules(context.Styles, (".statediagram-cluster rect", isCluster), (".statediagram-cluster rect.outer", isCluster));
        draft.Part(new PathData().Rect(left, top, rectWidth, height, CssRadius(outer, 0)), RulePaint(outer), role: "cluster");

        // The title sits a pixel under the top (3 pixels higher as SVG text), then the inner rectangle below it
        draft.PlaceLabel(label, new ShapePoint(x - (label.Width / 2), top + 1 - (html ? 0 : 3)));
        var inner = Rules(
            context.Styles,
            (".statediagram-cluster rect", isCluster),
            (".statediagram-cluster .inner", isCluster),
            (".statediagram-cluster.statediagram-cluster .inner", isCluster),
            (".statediagram-cluster.statediagram-cluster-alt .inner", isCluster && HasClass(node.CssClasses, StateClusterAltClass)));
        draft.Part(
            new PathData().Rect(left, top + label.Height + 2, rectWidth, height - label.Height - 6, CssRadius(inner, 0)),
            RulePaint(inner),
            role: "cluster-inner");
        return new ClusterShape(draft.Finish(IntersectRect), rectWidth, height, diff);
    }

    /// <summary>
    ///     <c>divider</c>: a concurrency region of a composite state, a dashed rectangle of the layout's size in the
    ///     alternate background, without a title.
    /// </summary>
    /// <param name="node">The cluster's node.</param>
    /// <param name="x">The x of the center.</param>
    /// <param name="y">The y of the center.</param>
    /// <param name="width">The width the layout gave it.</param>
    /// <param name="height">The height the layout gave it.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn cluster.</returns>
    private static ClusterShape DividerCluster(LayoutNode node, double x, double y, double width, double height, ShapeContext context)
    {
        var draft = new Draft(new ShapeInput(node, context, "divider"));
        var rule = Rules(
            context.Styles,
            (".statediagram-cluster rect", HasClass(node.CssClasses, StateClusterClass)),
            (".statediagram-state rect.divider", HasClass(node.CssClasses, StateClass)));
        draft.Part(new PathData().Rect(x - (width / 2), y - (height / 2), width, height, CssRadius(rule, 0)), RulePaint(rule), role: "cluster");
        return new ClusterShape(draft.Finish(IntersectRect), width, height, -node.Padding);
    }

    /// <summary>
    ///     <c>noteGroup</c>: the group of a note and its state, an unfilled rectangle of the layout's size that no rule
    ///     paints. Mermaid leaves the group's <c>diff</c> unset, which reads as zero.
    /// </summary>
    /// <param name="node">The cluster's node.</param>
    /// <param name="x">The x of the center.</param>
    /// <param name="y">The y of the center.</param>
    /// <param name="width">The width the layout gave it.</param>
    /// <param name="height">The height the layout gave it.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn cluster.</returns>
    private static ClusterShape NoteGroup(LayoutNode node, double x, double y, double width, double height, ShapeContext context)
    {
        var draft = new Draft(new ShapeInput(node, context, "noteGroup"));
        draft.Invisible(new PathData().Rect(x - (width / 2), y - (height / 2), width, height));
        return new ClusterShape(draft.Finish(IntersectRect), width, height, 0);
    }

    /// <summary>
    ///     Measures a label as Mermaid's <c>createLabel</c> creates it: a plain (not markdown) text that never wraps, as an
    ///     HTML label or as SVG text.
    /// </summary>
    /// <param name="context">The settings of the rendering.</param>
    /// <param name="labelStyles">The element's own label styles.</param>
    /// <param name="text">The text.</param>
    /// <param name="css">The text rule of the label.</param>
    /// <param name="location">The location of the element, for reports.</param>
    /// <returns>The measured label.</returns>
    private static LabelLayout MeasurePlainLabel(ShapeContext context, StyleDeclarationSet labelStyles, string text, TextPaint css, MermaidLocation location) =>
        context.MeasureLabel(new LabelRequest(text, LabelFontOf(labelStyles, css))
        {
            Markdown = false,
            HtmlLabels = context.Config.HtmlLabels,
            Width = double.PositiveInfinity,
            IsNode = true,
            Prepare = false,
            MarkdownAutoWrap = context.Config.MarkdownAutoWrap,
            RootFontSize = css.FontSize,
            ConfigFontSize = context.Config.FontSize,
            CenterSvgText = false,
            Location = location,
        });
}
