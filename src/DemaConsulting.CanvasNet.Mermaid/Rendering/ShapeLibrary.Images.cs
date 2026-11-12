// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The image and icon shapes (<c>imageSquare</c>, <c>icon</c>, <c>iconSquare</c>, <c>iconCircle</c>,
///     <c>iconRounded</c>): a picture or an icon with the label above or below it. Icons are not drawn (an icon is taken as
///     a square of its size); pictures come from <see cref="NodeImages"/>.
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>
    ///     <c>imageSquare</c> (<c>@{ img: ... }</c>): the picture in a frame, sized by its natural size, the
    ///     <c>w</c>/<c>h</c> of the node and the aspect constraint, with the label below or above (<c>pos: t</c>).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult ImageSquare(ShapeInput input)
    {
        var node = input.Node;
        var draft = new Draft(input);
        var natural = node.Img is { Length: > 0 } href && input.Context.Images is { } images ? images.NaturalSize(href, node.Location) : null;
        if (natural is null && input.Context.Images is null && node.Img is { Length: > 0 })
        {
            input.Context.Sink?.Warn(
                MermaidDiagnosticCodes.ImageUnresolved,
                $"the picture '{node.Img}' is not drawn: no image resolver was given (MermaidRenderOptions.ImageResolver).",
                node.Location,
                "give an image resolver that can load the link (for example a FolderImageResolver over the diagram's folder)");
        }

        // A picture that cannot be loaded has no natural size; its aspect ratio is taken as 1
        var (naturalWidth, naturalHeight) = natural ?? (0, 0);
        var aspect = natural is { Width: > 0, Height: > 0 } ? naturalWidth / naturalHeight : 1;
        var hasLabel = !string.IsNullOrEmpty(node.Label);
        var defaultWidth = input.Context.Config.Flowchart.WrappingWidth;
        var rawWidth = Math.Max(hasLabel ? defaultWidth : 0, node.AssetWidth ?? naturalWidth);
        var constrained = node.Constraint == "on";
        var imageWidth = constrained && node.AssetHeight is { } assetHeight && assetHeight != 0 && !double.IsNaN(assetHeight)
            ? assetHeight * aspect
            : rawWidth;
        var imageHeight = constrained ? imageWidth / aspect : node.AssetHeight ?? naturalHeight;
        var label = input.MeasureLabel(Math.Max(imageWidth, defaultWidth), assetShape: true);

        var top = node.Pos == "t";
        var labelPadding = hasLabel ? 8 : 0;
        var outerWidth = Math.Max(imageWidth, label.Width);
        var outerHeight = imageHeight + label.Height + labelPadding;
        var frameY = top ? (label.Height / 2) + (labelPadding / 2) : (-label.Height / 2) - (labelPadding / 2);
        var theme = input.Paint.Theme;
        draft.Part(
            new PathData().Rect(-imageWidth / 2, -imageHeight / 2, imageWidth, imageHeight),
            Body(input.Paint, theme.MainBkg, theme.NodeBorder, RoughStrokeWidth),
            0,
            frameY);
        LabelBackground(draft, input, label, new ShapePoint((-label.Width / 2) - label.OffsetX, top
            ? (-imageHeight / 2) - (label.Height / 2) - (labelPadding / 2)
            : (imageHeight / 2) - (label.Height / 2) + (labelPadding / 2)));
        draft.Invisible(new PathData().Rect(-outerWidth / 2, -outerHeight / 2, outerWidth, outerHeight));
        if (node.Img is { Length: > 0 } link)
        {
            var box = new SceneRect(-imageWidth / 2, top ? (outerHeight / 2) - imageHeight : -outerHeight / 2, imageWidth, imageHeight);
            if (natural is not null && box.Width > 0 && box.Height > 0)
            {
                draft.Item(new SceneImage(box, link) { Role = "node-image" }, box);
            }
            else
            {
                draft.Include(box);
            }
        }

        return draft.Finish(AssetIntersect(input, label, imageWidth, imageHeight, labelPadding, top));
    }

    /// <summary>
    ///     <c>icon</c>: an icon without a frame (not drawn), with the label below or above.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Icon(ShapeInput input) => IconShape(input, IconFrame.None);

    /// <summary>
    ///     <c>iconSquare</c>: an icon in a square frame.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult IconSquare(ShapeInput input) => IconShape(input, IconFrame.Square);

    /// <summary>
    ///     <c>iconRounded</c>: an icon in a rounded frame.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult IconRounded(ShapeInput input) => IconShape(input, IconFrame.Rounded);

    /// <summary>
    ///     <c>iconCircle</c>: an icon in a circle.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult IconCircle(ShapeInput input) => IconShape(input, IconFrame.Circle);

    /// <summary>
    ///     Draws an icon shape: the frame (the icon itself is not drawn; it counts as a square of its size), the label
    ///     below or above.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="frame">The frame.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult IconShape(ShapeInput input, IconFrame frame)
    {
        var node = input.Node;
        var draft = new Draft(input);
        var iconSize = Math.Max(node.AssetHeight ?? 48, node.AssetWidth ?? 48);
        var label = input.MeasureLabel(Math.Max(iconSize, input.Context.Config.Flowchart.WrappingWidth), assetShape: true);
        var top = node.Pos == "t";
        var labelPadding = string.IsNullOrEmpty(node.Label) ? 0 : 8;
        var halfPadding = input.Padding / 2;
        var size = frame switch
        {
            IconFrame.None => iconSize,
            IconFrame.Circle => (iconSize * Math.Sqrt(2)) + 40,
            _ => iconSize + (halfPadding * 2),
        };
        var outerWidth = Math.Max(size, label.Width);
        var outerHeight = size + label.Height + labelPadding;
        var frameY = top ? (label.Height / 2) + (labelPadding / 2) : (-label.Height / 2) - (labelPadding / 2);

        // The frame: the node's fill, its outline in the same color
        var theme = input.Paint.Theme;
        var fill = input.User["fill"] is not null ? input.User.Paint("fill") : theme.MainBkg;
        var paint = frame == IconFrame.None
            ? PartPaint.None
            : new PartPaint(fill, fill, input.User.Length("stroke-width", RoughStrokeWidth), null);
        var path = frame switch
        {
            IconFrame.Circle => new PathData().Oval(0, 0, size / 2, size / 2),
            IconFrame.Rounded => new PathData().Rect(-size / 2, -size / 2, size, size, 5),
            IconFrame.Square => new PathData().Rect(-size / 2, -size / 2, size, size, 0.1),
            _ => new PathData().Rect(-size / 2, -size / 2, size, size),
        };
        draft.Part(path, paint, 0, frameY);
        LabelBackground(draft, input, label, new ShapePoint((-label.Width / 2) - label.OffsetX, top ? -outerHeight / 2 : (outerHeight / 2) - label.Height));
        draft.Invisible(new PathData().Rect(-outerWidth / 2, -outerHeight / 2, outerWidth, outerHeight));

        // The icon's place, a square of its size centered in the frame
        draft.Include(new SceneRect(-iconSize / 2, frameY - (iconSize / 2), iconSize, iconSize));
        if (frame == IconFrame.Circle)
        {
            return draft.Finish(IntersectRect);
        }

        return draft.Finish(AssetIntersect(input, label, size, size, labelPadding, top));
    }

    /// <summary>
    ///     Places the label of an image or icon shape over its background (the label's paragraph has the edge label
    ///     background: opaque for an HTML label, at half opacity for an SVG label).
    /// </summary>
    /// <param name="draft">The draft.</param>
    /// <param name="input">The node.</param>
    /// <param name="label">The label.</param>
    /// <param name="offset">The translation of the label.</param>
    private static void LabelBackground(Draft draft, ShapeInput input, LabelLayout label, ShapePoint offset)
    {
        if (label.Width > 0 && label.Height > 0 && input.Paint.Theme.EdgeLabelBackground is { } background)
        {
            var box = label.Box.Offset(offset.X, offset.Y);
            var paint = new PartPaint(background, null, 0, null, label.IsHtml ? 1 : 0.5);
            draft.Part(new PathData().Rect(box.X, box.Y, box.Width, box.Height), paint, role: "label-background");
        }

        draft.PlaceLabel(label, offset);
    }

    /// <summary>
    ///     The intersect function of the image and icon shapes: the rectangle without a label; with one, the outline of
    ///     the asset and the label, written with absolute coordinates as Mermaid writes it.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="label">The label.</param>
    /// <param name="width">The width of the asset.</param>
    /// <param name="height">The height of the asset.</param>
    /// <param name="labelPadding">The space between the asset and the label.</param>
    /// <param name="top">Whether the label is above the asset.</param>
    /// <returns>The intersect function.</returns>
    private static ShapeIntersect AssetIntersect(ShapeInput input, LabelLayout label, double width, double height, double labelPadding, bool top)
    {
        if (string.IsNullOrEmpty(input.Node.Label))
        {
            return IntersectRect;
        }

        var bw = label.Width;
        var bh = label.Height;
        return (node, point) =>
        {
            var dx = node.X;
            var dy = node.Y;
            var nh = node.Height;
            List<ShapePoint> points = top
                ?
                [
                    new(dx - (bw / 2), dy - (nh / 2)), new(dx + (bw / 2), dy - (nh / 2)), new(dx + (bw / 2), dy - (nh / 2) + bh + labelPadding),
                    new(dx + (width / 2), dy - (nh / 2) + bh + labelPadding), new(dx + (width / 2), dy + (nh / 2)), new(dx - (width / 2), dy + (nh / 2)),
                    new(dx - (width / 2), dy - (nh / 2) + bh + labelPadding), new(dx - (bw / 2), dy - (nh / 2) + bh + labelPadding),
                ]
                :
                // dx + bw / 2 / 2 (a quarter of the label width) is Mermaid's own vertex in the intersection outline of
                // imageSquare, icon, iconSquare, and iconRounded (x: dx + bbox.width / 2 / 2); it is kept on purpose so that
                // edges end exactly where Mermaid ends them
                [
                    new(dx - (width / 2), dy - (nh / 2)), new(dx + (width / 2), dy - (nh / 2)), new(dx + (width / 2), dy - (nh / 2) + height),
                    new(dx + (bw / 2), dy - (nh / 2) + height), new(dx + (bw / 2 / 2), dy + (nh / 2)), new(dx - (bw / 2), dy + (nh / 2)),
                    new(dx - (bw / 2), dy - (nh / 2) + height), new(dx - (width / 2), dy - (nh / 2) + height),
                ];
            return IntersectPolygon(node, points, point);
        };
    }

    /// <summary>
    ///     The frame of an icon shape.
    /// </summary>
    private enum IconFrame
    {
        /// <summary>No frame (<c>icon</c>).</summary>
        None,

        /// <summary>A square (<c>iconSquare</c>).</summary>
        Square,

        /// <summary>A rounded square (<c>iconRounded</c>).</summary>
        Rounded,

        /// <summary>A circle (<c>iconCircle</c>).</summary>
        Circle,
    }
}
