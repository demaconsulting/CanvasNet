// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     A drawn subgraph: its rectangle and title, and the size and offset the layout continues with.
/// </summary>
/// <param name="Shape">The items (rectangle, then title), their box, the title and the intersect function.</param>
/// <param name="Width">The width of the rectangle (Mermaid's <c>node.width</c> after <c>rect</c>).</param>
/// <param name="Height">The height of the rectangle.</param>
/// <param name="Diff">The horizontal correction for a title wider than the layout's box (Mermaid's <c>node.diff</c>).</param>
internal sealed record ClusterShape(ShapeResult Shape, double Width, double Height, double Diff);

/// <content>
///     The subgraph drawing of <c>rendering-elements/clusters.js</c> that a flowchart uses (<c>rect</c>), and the hidden
///     <c>labelRect</c> node of Mermaid's self-loop workaround.
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>The side of the rectangle of a <c>labelRect</c> node.</summary>
    private const double LabelRectSide = 0.1;

    /// <summary>
    ///     Draws a subgraph (<c>clusters.js</c>'s <c>rect</c>): a rectangle of the layout's size, widened to its title
    ///     plus the padding when the title is wider, with the title centered on its top edge.
    /// </summary>
    /// <param name="node">The subgraph's node.</param>
    /// <param name="x">The x of the center the layout gave it.</param>
    /// <param name="y">The y of the center.</param>
    /// <param name="width">The width the layout gave it.</param>
    /// <param name="height">The height the layout gave it (with the title margins).</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn subgraph, in the coordinates of its graph.</returns>
    public static ClusterShape DrawCluster(LayoutNode node, double x, double y, double width, double height, ShapeContext context)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);
        var input = new ShapeInput(node, context, "rect");
        var draft = new Draft(input);

        // The title: a label without wrapping (createLabel), or a markdown string wrapped at the layout's width
        var css = context.Styles.ClusterLabel;
        var html = context.Config.HtmlLabels;
        var markdown = node.LabelType == "markdown";
        var color = html ? input.LabelStyles.Paint("color") ?? css.Color : css.Color;
        var label = context.MeasureLabel(new LabelRequest(node.Label ?? string.Empty, new LabelFont(css.FontFamily, css.FontSize, false, false, color))
        {
            Markdown = markdown,
            HtmlLabels = html,
            Width = markdown && width > 0 ? width : double.PositiveInfinity,
            IsNode = true,
            Prepare = false,
            MarkdownAutoWrap = context.Config.MarkdownAutoWrap,
            RootFontSize = css.FontSize,
            ConfigFontSize = context.Config.FontSize,
            CenterSvgText = false,
            Location = node.Location,
        });

        // The rectangle is widened to the title and its padding
        var padding = node.Padding;
        var wide = width <= label.Width + padding;
        var rectWidth = wide ? label.Width + padding : width;
        var diff = wide ? ((rectWidth - width) / 2) - padding : -padding;
        var paint = new NodePaint(context.Styles.Cluster, input.User, context.Theme);
        draft.Part(new PathData().Rect(x - (rectWidth / 2), y - (height / 2), rectWidth, height), Body(paint), role: "cluster");

        // The title sits on the top edge
        var titleTop = context.Config.Flowchart.SubGraphTitleMarginTop;
        draft.PlaceLabel(label, new ShapePoint(x - (label.Width / 2), y - (height / 2) + titleTop));
        return new ClusterShape(draft.Finish(IntersectRect), rectWidth, height, diff);
    }

    /// <summary>
    ///     <c>labelRect</c>: the hidden node of Mermaid's self-loop workaround, a rectangle of a tenth of a pixel in the
    ///     style sheet's edge label background, with its (empty) label.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult LabelRect(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var rule = input.Context.Styles.Resolve(".edgeLabel rect");
        var opacity = rule["opacity"] is { } text && StyleDeclarations.TryParseOpacity(text, out var factor) ? factor : 1;
        draft.Part(new PathData().Rect(0, 0, LabelRectSide, LabelRectSide), new PartPaint(rule.Paint("fill"), null, 0, null, opacity), role: "dummy");
        draft.PlaceLabel(label, new ShapePoint(-(label.Width / 2) - label.OffsetX, -(label.Height / 2) - label.OffsetY));
        return draft.Finish(IntersectRect);
    }
}
