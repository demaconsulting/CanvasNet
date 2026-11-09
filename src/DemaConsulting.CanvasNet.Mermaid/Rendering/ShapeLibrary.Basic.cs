// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The classic shapes: the rectangles of <c>drawRect</c> and the shapes of the flowchart's bracket syntax
///     (<c>squareRect</c>, <c>roundedRect</c>, <c>stadium</c>, <c>subroutine</c>, <c>cylinder</c>, <c>circle</c>,
///     <c>doublecircle</c>, <c>question</c>, <c>hexagon</c>, <c>lean_right</c>, <c>lean_left</c>, <c>trapezoid</c>,
///     <c>inv_trapezoid</c>, <c>rect_left_inv_arrow</c> and the legacy <c>ellipse</c>), and the small undocumented shapes
///     a flowchart accepts (<c>state</c>, <c>composite</c>, <c>text</c>, <c>datastore</c>, <c>choice</c>, <c>note</c>,
///     <c>anchor</c>).
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>
    ///     <c>squareRect</c>: a rectangle with twice the padding left and right.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult SquareRect(ShapeInput input) => DrawRect(input, 0, input.Padding * 2, input.Padding);

    /// <summary>
    ///     <c>roundedRect</c>: a rectangle with rounded corners (the theme's <c>radius</c>, 5 by default).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult RoundedRect(ShapeInput input)
    {
        var radius = input.Context.Config.ThemeVariable("radius") is { } text
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : 5;
        return DrawRect(input, radius, input.Padding, input.Padding);
    }

    /// <summary>
    ///     <c>state</c>: <c>drawRect</c> without label paddings, whose size is not a number in Mermaid, so only the label is
    ///     drawn and sized.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult State(ShapeInput input) => DrawRect(input, 5, double.NaN, double.NaN);

    /// <summary>
    ///     Mermaid's <c>drawRect</c>: a rectangle around the label and its paddings, rounded when a radius is given.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="radius">The corner radius (zero for none).</param>
    /// <param name="labelPaddingX">The padding left and right of the label.</param>
    /// <param name="labelPaddingY">The padding above and below the label.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult DrawRect(ShapeInput input, double radius, double labelPaddingX, double labelPaddingY)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var totalWidth = label.Width + (labelPaddingX * 2);
        var totalHeight = label.Height + (labelPaddingY * 2);
        if (double.IsFinite(totalWidth) && double.IsFinite(totalHeight))
        {
            draft.Part(new PathData().Rect(-totalWidth / 2, -totalHeight / 2, totalWidth, totalHeight, radius), Body(input.Paint));
        }

        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>composite</c>: a rectangle of the label and the padding.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Composite(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var halfPadding = input.Padding / 2;
        var totalWidth = label.Width + input.Padding;
        var totalHeight = label.Height + input.Padding;
        draft.Part(new PathData().Rect((-label.Width / 2) - halfPadding, (-label.Height / 2) - halfPadding, totalWidth, totalHeight), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>text</c>: the label in an invisible rectangle (the style sheet's <c>rect.text</c> has no fill and no stroke
    ///     width).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Text(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var totalWidth = label.Width + input.Padding;
        var totalHeight = label.Height + input.Padding;
        draft.Part(new PathData().Rect(-totalWidth / 2, -totalHeight / 2, totalWidth, totalHeight), Body(input.Paint, null, input.Paint.Css.Stroke, 0));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>datastore</c>: <c>squareRect</c> dashed by its own width and height, so only the top and bottom lines show.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Datastore(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var totalWidth = label.Width + (input.Padding * 4);
        var totalHeight = label.Height + (input.Padding * 2);
        var paint = Body(input.Paint);
        if (paint.Dash is null && totalWidth > 0 && totalHeight > 0)
        {
            paint = paint with { Dash = [totalWidth, totalHeight] };
        }

        draft.Part(new PathData().Rect(-totalWidth / 2, -totalHeight / 2, totalWidth, totalHeight), paint);
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>stadium</c>: a rectangle with half-circle ends (as a polygon of 50 points per end).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Stadium(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var h = label.Height + input.Padding;
        var w = label.Width + (h / 4) + input.Padding;
        var radius = h / 2;
        var points = new List<ShapePoint> { new((-w / 2) + radius, -h / 2), new((w / 2) - radius, -h / 2) };
        points.AddRange(CirclePoints((-w / 2) + radius, 0, radius, 50, 90, 270));
        points.Add(new ShapePoint((w / 2) - radius, h / 2));
        points.AddRange(CirclePoints((w / 2) - radius, 0, radius, 50, 270, 450));
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>subroutine</c>: a rectangle with a frame of 8 on the left and right.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Subroutine(ShapeInput input)
    {
        const double frame = 8;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var totalWidth = label.Width + (2 * frame) + input.Padding;
        var h = label.Height + input.Padding;
        var w = totalWidth - (2 * frame);
        var points = new List<ShapePoint>
        {
            new(0, 0), new(w, 0), new(w, -h), new(0, -h), new(0, 0),
            new(-8, 0), new(w + 8, 0), new(w + 8, -h), new(-8, -h), new(-8, 0),
        };
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -w / 2, h / 2);
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>cylinder</c>: a cylinder whose ellipses have a height that falls with the width.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Cylinder(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var rx = w / 2;
        var ry = rx / (2.5 + (w / 50));
        var h = label.Height + input.Padding + ry;
        draft.Part(CylinderPath(w, h, rx, ry, null), Body(input.Paint), -w / 2, -((h / 2) + ry));
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, (-label.Height / 2) + (input.Padding / 1.5) - label.OffsetY));
        return draft.Finish((node, point) => CylinderIntersect(node, point, rx, ry));
    }

    /// <summary>
    ///     The path of a cylinder (<c>createCylinderPathD</c>), with the extra line of the lined cylinder.
    /// </summary>
    /// <param name="w">The width.</param>
    /// <param name="h">The height of the body.</param>
    /// <param name="rx">The x radius.</param>
    /// <param name="ry">The y radius.</param>
    /// <param name="outerOffset">The offset of the extra line, or null for none.</param>
    /// <returns>The path.</returns>
    private static PathData CylinderPath(double w, double h, double rx, double ry, double? outerOffset)
    {
        var path = new PathData().M(0, ry)
            .RelativeA(rx, ry, 0, false, false, w, 0)
            .RelativeA(rx, ry, 0, false, false, -w, 0)
            .RelativeL(0, h)
            .RelativeA(rx, ry, 0, false, false, w, 0)
            .RelativeL(0, -h);
        return outerOffset is { } offset ? path.M(0, ry + offset).RelativeA(rx, ry, 0, false, false, w, 0) : path;
    }

    /// <summary>
    ///     The intersect function of the cylinders: the rectangle, moved onto the ellipse of the top or bottom.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="point">The point.</param>
    /// <param name="rx">The x radius.</param>
    /// <param name="ry">The y radius.</param>
    /// <returns>The intersection.</returns>
    private static ShapePoint CylinderIntersect(NodeBox node, ShapePoint point, double rx, double ry)
    {
        var pos = IntersectRect(node, point);
        var x = pos.X - node.X;
        if (rx != 0 && (Math.Abs(x) < node.Width / 2 || (Same(Math.Abs(x), node.Width / 2) && Math.Abs(pos.Y - node.Y) > (node.Height / 2) - ry)))
        {
            var y = ry * ry * (1 - (x * x / (rx * rx)));
            if (y > 0)
            {
                y = Math.Sqrt(y);
            }

            y = ry - y;
            if (point.Y - node.Y > 0)
            {
                y = -y;
            }

            pos = pos with { Y = pos.Y + y };
        }

        return pos;
    }

    /// <summary>
    ///     <c>circle</c>: a circle around the label with half the padding.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Circle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var radius = (label.Width / 2) + (input.Padding / 2);
        draft.Part(new PathData().Oval(0, 0, radius, radius), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectCircle(node, radius, point));
    }

    /// <summary>
    ///     <c>doublecircle</c>: two circles 5 apart around the label with the padding.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult DoubleCircle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var outerRadius = (label.Width / 2) + input.Padding;
        var innerRadius = outerRadius - 5;
        draft.Part(new PathData().Oval(0, 0, outerRadius, outerRadius), Body(input.Paint));
        draft.Part(new PathData().Oval(0, 0, innerRadius, innerRadius), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectCircle(node, outerRadius, point));
    }

    /// <summary>
    ///     <c>question</c>: a diamond whose diagonal is the label's width plus height, moved half a pixel right.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Question(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var h = label.Height + input.Padding;
        var s = w + h;
        var points = DiamondPoints(s);
        draft.Part(new PathData().Polygon(points), Body(input.Paint), (-s / 2) + 0.5, s / 2);
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) =>
        {
            // calcIntersect: the diamond of the node's width, moved back by half a pixel
            var result = IntersectPolygon(node, DiamondPoints(node.Width), point);
            return new ShapePoint(result.X - 0.5, result.Y - 0.5);
        });
    }

    /// <summary>
    ///     The points of a diamond of a size (<c>question</c>).
    /// </summary>
    /// <param name="s">The size.</param>
    /// <returns>The points.</returns>
    private static List<ShapePoint> DiamondPoints(double s) => [new(s / 2, 0), new(s, -s / 2), new(s / 2, -s), new(0, -s / 2)];

    /// <summary>
    ///     <c>hexagon</c>: a hexagon whose points are a quarter of the height out.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Hexagon(ShapeInput input)
    {
        var label = input.MeasureLabel();
        var h = label.Height + input.Padding;
        var m = h / 4;
        var w = label.Width + (2 * m) + input.Padding;
        return PolygonShape(input, label, w, h, [new(m, 0), new(w - m, 0), new(w, -h / 2), new(w - m, -h), new(m, -h), new(0, -h / 2)]);
    }

    /// <summary>
    ///     <c>lean_right</c>: a parallelogram leaning right.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult LeanRight(ShapeInput input)
    {
        var label = input.MeasureLabel();
        var h = label.Height + input.Padding;
        var w = Math.Max(label.Width + input.Padding, -h);
        return PolygonShape(input, label, w, h, [new(-3 * h / 6, 0), new(w, 0), new(w + (3 * h / 6), -h), new(0, -h)]);
    }

    /// <summary>
    ///     <c>lean_left</c>: a parallelogram leaning left.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult LeanLeft(ShapeInput input)
    {
        var label = input.MeasureLabel();
        var h = label.Height + input.Padding;
        var w = Math.Max(label.Width + input.Padding, -h);
        return PolygonShape(input, label, w, h, [new(0, 0), new(w + (3 * h / 6), 0), new(w, -h), new(-3 * h / 6, -h)]);
    }

    /// <summary>
    ///     <c>trapezoid</c>: wider at the bottom.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Trapezoid(ShapeInput input)
    {
        var label = input.MeasureLabel();
        var h = label.Height + input.Padding;
        var w = Math.Max(label.Width + input.Padding, -h);
        return PolygonShape(input, label, w, h, [new(-3 * h / 6, 0), new(w + (3 * h / 6), 0), new(w, -h), new(0, -h)]);
    }

    /// <summary>
    ///     <c>inv_trapezoid</c>: wider at the top, with twice the padding.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult InvTrapezoid(ShapeInput input)
    {
        var label = input.MeasureLabel();
        var h = label.Height + (input.Padding * 2);
        var w = Math.Max(label.Width + (input.Padding * 2), -h);
        return PolygonShape(input, label, w, h, [new(0, 0), new(w, 0), new(w + (3 * h / 6), -h), new(-3 * h / 6, -h)]);
    }

    /// <summary>
    ///     A shape drawn with <c>insertPolygonShape</c> (the polygon moved by <c>(-w/2, h/2)</c>) with the default label
    ///     and the polygon intersect function.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <param name="label">The measured label.</param>
    /// <param name="w">The width of the translation.</param>
    /// <param name="h">The height of the translation.</param>
    /// <param name="points">The polygon.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult PolygonShape(ShapeInput input, LabelLayout label, double w, double h, List<ShapePoint> points)
    {
        var draft = new Draft(input);
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -w / 2, h / 2);
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>rect_left_inv_arrow</c>: a rectangle with a notch pointing left.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult RectLeftInvArrow(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var h = label.Height + input.Padding;
        var notchWidth = h / 4;
        var w = Math.Max(label.Width + input.Padding, -notchWidth);
        var x = -w / 2;
        var y = -h / 2;
        var notch = y / 2;
        var points = new List<ShapePoint> { new(x + notch, y), new(x, 0), new(x + notch, -y), new(-x, -y), new(-x, y) };
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -notch / 2, 0);
        draft.PlaceLabel(label, new ShapePoint((-notch / 2) - (label.Width / 2) - label.OffsetX, (-label.Height / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     The legacy <c>ellipse</c> (<c>A(-text-)</c>), which Mermaid's registry no longer has: an ellipse around the label
    ///     with the padding left and right and half the padding above and below.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Ellipse(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var rx = (label.Width / 2) + input.Padding;
        var ry = (label.Height / 2) + (input.Padding / 2);
        draft.Part(new PathData().Oval(0, 0, rx, ry), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectEllipse(node, rx, ry, point));
    }

    /// <summary>
    ///     <c>choice</c>: a small diamond of 28 without a label.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Choice(ShapeInput input)
    {
        const double s = 28;
        var draft = new Draft(input);
        var points = new List<ShapePoint> { new(0, s / 2), new(s / 2, 0), new(0, -s / 2), new(-s / 2, 0) };
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>note</c>: a rectangle with twice the padding (the style sheet's node rule wins over the note colors).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Note(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel(css: input.Context.Styles.NoteLabel);
        var totalWidth = label.Width + (input.Padding * 2);
        var totalHeight = label.Height + (input.Padding * 2);
        draft.Part(new PathData().Rect(-totalWidth / 2, -totalHeight / 2, totalWidth, totalHeight), Body(input.Paint));
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, (-label.Height / 2) - label.OffsetY));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>anchor</c>: a dot of radius 1 without a label.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Anchor(ShapeInput input)
    {
        const double radius = 1;
        var draft = new Draft(input);
        draft.Part(new PathData().Oval(0, 0, radius, radius), Body(input.Paint));
        return draft.Finish((node, point) => IntersectCircle(node, radius, point));
    }
}
