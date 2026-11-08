// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The shapes of Mermaid v11's extended shape set: cards, documents, cylinders, braces, triangles, the state and
///     junction circles, and the newer folder, bucket, console, browser, person, bang, cloud and collapsed-group shapes.
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>
    ///     <c>card</c>: a rectangle with its top-left corner cut by 12.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Card(ShapeInput input)
    {
        const double notch = 12;
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding + notch;
        var h = label.Height + input.Padding;
        return PolygonShape(input, label, w, h, [new(notch, -h), new(w, -h), new(w, 0), new(0, 0), new(0, -h + notch), new(notch, -h)]);
    }

    /// <summary>
    ///     <c>shadedProcess</c>: a rectangle with a band of 8 on the left.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult ShadedProcess(ShapeInput input)
    {
        const double frame = 8;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var totalWidth = label.Width + (input.Padding * 2) + (frame * 2);
        var totalHeight = label.Height + (input.Padding * 2);
        var w = totalWidth - frame;
        var h = totalHeight;
        var x = frame - (totalWidth / 2);
        var y = -totalHeight / 2;
        List<ShapePoint> points = [new(x, y), new(x + w, y), new(x + w, y + h), new(x - frame, y + h), new(x - frame, y), new(x, y), new(x, y + h)];
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        draft.PlaceLabel(label, new ShapePoint((frame / 2) - (label.Width / 2) - label.OffsetX, (-label.Height / 2) - label.OffsetY));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>stateStart</c>: a circle of 14 (the style sheet's node rule paints it).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult StateStart(ShapeInput input)
    {
        const double width = 14;
        var draft = new Draft(input);
        var css = input.Paint.Css;
        draft.Part(new PathData().Oval(0, 0, width / 2, width / 2), new PartPaint(css.Fill, css.Stroke, css.StrokeWidth, null));
        return draft.Finish((node, point) => IntersectCircle(node, width / 2, point));
    }

    /// <summary>
    ///     <c>stateEnd</c>: a circle of 14 with a circle of 5 inside.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult StateEnd(ShapeInput input)
    {
        const double width = 14;
        var draft = new Draft(input);
        draft.Part(new PathData().Oval(0, 0, width / 2, width / 2), Body(input.Paint));
        var inner = width * 5 / 14 / 2;
        draft.Part(new PathData().Oval(0, 0, inner, inner), Body(input.Paint));
        return draft.Finish((node, point) => IntersectCircle(node, width / 2, point));
    }

    /// <summary>
    ///     <c>forkJoin</c>: a bar of 70 by 10 (10 by 70 for a node whose direction is <c>LR</c>).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult ForkJoin(ShapeInput input)
    {
        var draft = new Draft(input);
        var (width, height) = input.Node.Dir == "LR" ? (10.0, 70.0) : (70.0, 10.0);
        draft.Part(new PathData().Rect(-width / 2, -height / 2, width, height), Body(input.Paint));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>hourglass</c>: two triangles meeting at the center, 30 by 30, without a label.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Hourglass(ShapeInput input)
    {
        const double w = 30;
        const double h = 30;
        var draft = new Draft(input);
        var label = input.MeasureLabel(label: string.Empty);
        List<ShapePoint> points = [new(0, 0), new(w, 0), new(0, h), new(w, h)];
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -w / 2, -h / 2);
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>curlyBraceLeft</c>: a brace on the left of the label, with an invisible outline that catches the edges.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult CurlyBraceLeft(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var h = label.Height + input.Padding;
        var radius = Math.Max(5, h * 0.1);
        var points = LeftBrace(w, h, radius, radius * 2, 30);
        var rectPoints = new List<ShapePoint> { new(w / 2, (-h / 2) - radius), new(-w / 2, (-h / 2) - radius) };
        rectPoints.AddRange(CirclePoints(w / 2, -h / 2, radius, 20, -90, 0));
        rectPoints.Add(new ShapePoint((-w / 2) - radius, -radius));
        rectPoints.AddRange(CirclePoints((w / 2) + (w * 0.1), -radius, radius, 20, -180, -270));
        rectPoints.AddRange(CirclePoints((w / 2) + (w * 0.1), radius, radius, 20, -90, -180));
        rectPoints.Add(new ShapePoint((-w / 2) - radius, h / 2));
        rectPoints.AddRange(CirclePoints(w / 2, h / 2, radius, 20, 0, 90));
        rectPoints.Add(new ShapePoint(-w / 2, (h / 2) + radius));
        rectPoints.Add(new ShapePoint(w / 2, (h / 2) + radius));
        var paint = Body(input.Paint);
        draft.Part(new PathData().Polygon(points, close: false), paint, radius);
        draft.Part(new PathData().Polygon(rectPoints), paint.WithStrokeOpacity(0), radius);
        draft.PlaceLabel(label, new ShapePoint((-w / 2) + radius - label.OffsetX, (-h / 2) + (input.Padding / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, rectPoints, point));
    }

    /// <summary>
    ///     The points of a left brace (shared by <c>curlyBraceLeft</c> and <c>curlyBraces</c>).
    /// </summary>
    /// <param name="w">The width.</param>
    /// <param name="h">The height.</param>
    /// <param name="radius">The radius of the curls.</param>
    /// <param name="tip">The distance of the tip's curls from the right edge.</param>
    /// <param name="firstCount">The number of points of the first curl.</param>
    /// <returns>The points.</returns>
    private static List<ShapePoint> LeftBrace(double w, double h, double radius, double tip, int firstCount)
    {
        var points = CirclePoints(w / 2, -h / 2, radius, firstCount, -90, 0);
        points.Add(new ShapePoint((-w / 2) - radius, radius));
        points.AddRange(CirclePoints((w / 2) + tip, -radius, radius, 20, -180, -270));
        points.AddRange(CirclePoints((w / 2) + tip, radius, radius, 20, -90, -180));
        points.Add(new ShapePoint((-w / 2) - radius, -h / 2));
        points.AddRange(CirclePoints(w / 2, h / 2, radius, 20, 0, 90));
        return points;
    }

    /// <summary>
    ///     <c>curlyBraceRight</c>: a brace on the right of the label (its circle points are not mirrored).
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult CurlyBraceRight(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var h = label.Height + input.Padding;
        var radius = Math.Max(5, h * 0.1);
        var points = CirclePoints(w / 2, -h / 2, radius, 20, -90, 0, mirror: false);
        points.Add(new ShapePoint((w / 2) + radius, -radius));
        points.AddRange(CirclePoints((w / 2) + (radius * 2), -radius, radius, 20, -180, -270, mirror: false));
        points.AddRange(CirclePoints((w / 2) + (radius * 2), radius, radius, 20, -90, -180, mirror: false));
        points.Add(new ShapePoint((w / 2) + radius, h / 2));
        points.AddRange(CirclePoints(w / 2, h / 2, radius, 20, 0, 90, mirror: false));
        var rectPoints = new List<ShapePoint> { new(-w / 2, (-h / 2) - radius), new(w / 2, (-h / 2) - radius) };
        rectPoints.AddRange(CirclePoints(w / 2, -h / 2, radius, 20, -90, 0, mirror: false));
        rectPoints.Add(new ShapePoint((w / 2) + radius, -radius));
        rectPoints.AddRange(CirclePoints((w / 2) + (radius * 2), -radius, radius, 20, -180, -270, mirror: false));
        rectPoints.AddRange(CirclePoints((w / 2) + (radius * 2), radius, radius, 20, -90, -180, mirror: false));
        rectPoints.Add(new ShapePoint((w / 2) + radius, h / 2));
        rectPoints.AddRange(CirclePoints(w / 2, h / 2, radius, 20, 0, 90, mirror: false));
        rectPoints.Add(new ShapePoint(w / 2, (h / 2) + radius));
        rectPoints.Add(new ShapePoint(-w / 2, (h / 2) + radius));
        var paint = Body(input.Paint);
        draft.Part(new PathData().Polygon(points, close: false), paint, -radius);
        draft.Part(new PathData().Polygon(rectPoints), paint.WithStrokeOpacity(0), -radius);
        draft.PlaceLabel(label, new ShapePoint((-w / 2) + (input.Padding / 2) - label.OffsetX, (-h / 2) + (input.Padding / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, rectPoints, point));
    }

    /// <summary>
    ///     <c>curlyBraces</c>: braces on both sides of the label.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult CurlyBraces(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var h = label.Height + input.Padding;
        var radius = Math.Max(5, h * 0.1);
        var left = LeftBrace(w, h, radius, radius * 2, 30);
        var right = RightBraceOfPair(w, h, radius);
        var rectPoints = new List<ShapePoint> { new(w / 2, (-h / 2) - radius), new(-w / 2, (-h / 2) - radius) };
        rectPoints.AddRange(CirclePoints(w / 2, -h / 2, radius, 20, -90, 0));
        rectPoints.Add(new ShapePoint((-w / 2) - radius, -radius));
        rectPoints.AddRange(CirclePoints((w / 2) + (radius * 2), -radius, radius, 20, -180, -270));
        rectPoints.AddRange(CirclePoints((w / 2) + (radius * 2), radius, radius, 20, -90, -180));
        rectPoints.Add(new ShapePoint((-w / 2) - radius, h / 2));
        rectPoints.AddRange(CirclePoints(w / 2, h / 2, radius, 20, 0, 90));
        rectPoints.Add(new ShapePoint(-w / 2, (h / 2) + radius));
        rectPoints.Add(new ShapePoint((w / 2) - radius - (radius / 2), (h / 2) + radius));
        rectPoints.AddRange(CirclePoints((-w / 2) + radius + (radius / 2), -h / 2, radius, 20, -90, -180));
        rectPoints.Add(new ShapePoint((w / 2) - (radius / 2), radius));
        rectPoints.AddRange(CirclePoints((-w / 2) - (radius / 2), -radius, radius, 20, 0, 90));
        rectPoints.AddRange(CirclePoints((-w / 2) - (radius / 2), radius, radius, 20, -90, 0));
        rectPoints.Add(new ShapePoint((w / 2) - (radius / 2), -radius));
        rectPoints.AddRange(CirclePoints((-w / 2) + radius + (radius / 2), h / 2, radius, 30, -180, -270));
        var paint = Body(input.Paint);
        var dx = radius - (radius / 4);
        draft.Part(new PathData().Polygon(right, close: false), paint, dx);
        draft.Part(new PathData().Polygon(left, close: false), paint, dx);
        draft.Part(new PathData().Polygon(rectPoints), paint.WithStrokeOpacity(0), dx);
        draft.PlaceLabel(label, new ShapePoint((-w / 2) + (input.Padding / 2) - label.OffsetX, (-h / 2) + (input.Padding / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, rectPoints, point));
    }

    /// <summary>
    ///     The points of the right brace of <c>curlyBraces</c>.
    /// </summary>
    /// <param name="w">The width.</param>
    /// <param name="h">The height.</param>
    /// <param name="radius">The radius of the curls.</param>
    /// <returns>The points.</returns>
    private static List<ShapePoint> RightBraceOfPair(double w, double h, double radius)
    {
        var points = CirclePoints((-w / 2) + radius + (radius / 2), -h / 2, radius, 20, -90, -180);
        points.Add(new ShapePoint((w / 2) - (radius / 2), radius));
        points.AddRange(CirclePoints((-w / 2) - (radius / 2), -radius, radius, 20, 0, 90));
        points.AddRange(CirclePoints((-w / 2) - (radius / 2), radius, radius, 20, -90, 0));
        points.Add(new ShapePoint((w / 2) - (radius / 2), -radius));
        points.AddRange(CirclePoints((-w / 2) + radius + (radius / 2), h / 2, radius, 30, -180, -270));
        return points;
    }

    /// <summary>
    ///     <c>lightningBolt</c>: a bolt of 35 by 70 without a label.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult LightningBolt(ShapeInput input)
    {
        const double width = 35;
        const double height = 35;
        const double gap = 7;
        var draft = new Draft(input);
        List<ShapePoint> points =
        [
            new(width, 0), new(0, height + (gap / 2)), new(width - (2 * gap), height + (gap / 2)),
            new(0, 2 * height), new(width, height - (gap / 2)), new(2 * gap, height - (gap / 2)),
        ];
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -width / 2, -height);
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>waveEdgedRectangle</c>: a document with a wavy bottom edge.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult WaveEdgedRectangle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 2);
        var amplitude = h / 8;
        var finalH = h + amplitude;
        var extraW = 14 - w > 0 ? (14 - w) / 2 : 0;
        var points = new List<ShapePoint> { new((-w / 2) - extraW, finalH / 2) };
        points.AddRange(SineWavePoints((-w / 2) - extraW, finalH / 2, (w / 2) + extraW, finalH / 2, amplitude, 0.8));
        points.Add(new ShapePoint((w / 2) + extraW, -finalH / 2));
        points.Add(new ShapePoint((-w / 2) - extraW, -finalH / 2));
        draft.Part(new PathData().Polygon(points), Body(input.Paint), 0, -amplitude / 2);
        draft.PlaceLabel(label, new ShapePoint((-w / 2) + input.Padding - label.OffsetX, (-h / 2) + input.Padding - amplitude - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>halfRoundedRectangle</c>: a rectangle with a round right end.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult HalfRoundedRectangle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = Math.Max(15, label.Width) + (input.Padding * 2);
        var h = Math.Max(10, label.Height) + (input.Padding * 2);
        var radius = h / 2;
        var points = new List<ShapePoint> { new(-w / 2, -h / 2), new((w / 2) - radius, -h / 2) };
        points.AddRange(CirclePoints((-w / 2) + radius, 0, radius, 50, 90, 270));
        points.Add(new ShapePoint((w / 2) - radius, h / 2));
        points.Add(new ShapePoint(-w / 2, h / 2));
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>tiltedCylinder</c>: a cylinder lying on its side.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult TiltedCylinder(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var labelPadding = input.Padding / 2;
        var h = label.Height + labelPadding;
        var ry = h / 2;
        var rx = ry / (2.5 + (h / 50));
        var w = label.Width + rx + labelPadding;
        var path = new PathData().M(0, 0)
            .RelativeA(rx, ry, 0, false, true, 0, -h)
            .RelativeL(w, 0)
            .RelativeA(rx, ry, 0, false, true, 0, h)
            .M(w, -h)
            .RelativeA(rx, ry, 0, false, false, 0, h)
            .RelativeL(-w, 0);
        draft.Part(path, Body(input.Paint), -w / 2, h / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - rx - label.OffsetX, (-label.Height / 2) - label.OffsetY));
        return draft.Finish((node, point) =>
        {
            var pos = IntersectRect(node, point);
            var y = pos.Y - node.Y;
            if (ry != 0 && (Math.Abs(y) < node.Height / 2 || (Same(Math.Abs(y), node.Height / 2) && Math.Abs(pos.X - node.X) > (node.Width / 2) - rx)))
            {
                var x = rx * rx * (1 - (y * y / (ry * ry)));
                if (x != 0)
                {
                    x = Math.Sqrt(Math.Abs(x));
                }

                x = rx - x;
                if (point.X - node.X > 0)
                {
                    x = -x;
                }

                pos = pos with { X = pos.X + x };
            }

            return pos;
        });
    }

    /// <summary>
    ///     <c>linedCylinder</c>: a cylinder with a second line under its top.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult LinedCylinder(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var rx = w / 2;
        var ry = rx / (2.5 + (w / 50));
        var h = label.Height + ry + (input.Padding * 2);
        draft.Part(CylinderPath(w, h, rx, ry, h * 0.1), Body(input.Paint), -w / 2, -((h / 2) + ry));
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, (-label.Height / 2) + ry - label.OffsetY));
        return draft.Finish((node, point) => CylinderIntersect(node, point, rx, ry));
    }

    /// <summary>
    ///     <c>curvedTrapezoid</c>: a display shape, pointed left and round right.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult CurvedTrapezoid(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = Math.Max(20, (label.Width + (input.Padding * 2)) * 1.25);
        var h = Math.Max(5, label.Height + (input.Padding * 2));
        var radius = h / 2;
        var rw = w - radius;
        var tw = h / 4;
        var points = new List<ShapePoint> { new(rw, 0), new(tw, 0), new(0, h / 2), new(tw, h), new(rw, h) };
        points.AddRange(CirclePoints(-rw, -h / 2, radius, 50, 270, 90));
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -w / 2, -h / 2);
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>dividedRectangle</c>: a rectangle with a band at the top.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult DividedRectangle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var h = label.Height + input.Padding;
        var rectOffset = h * 0.2;
        var x = -w / 2;
        var y = (-h / 2) - (rectOffset / 2);
        List<ShapePoint> points = [new(x, y + rectOffset), new(-x, y + rectOffset), new(-x, -y), new(x, -y), new(x, y), new(-x, y), new(-x, y + rectOffset)];
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        draft.PlaceLabel(label, new ShapePoint(x + (input.Padding / 2) - label.OffsetX, y + rectOffset + (input.Padding / 2) - label.OffsetY));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>triangle</c>: a triangle pointing up, the label at its base.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Triangle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var h = w + label.Height;
        List<ShapePoint> points = [new(0, 0), new(h, 0), new(h / 2, -h)];
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -h / 2, h / 2);

        // The label's distance from the base reads the flowchart's own htmlLabels, which Mermaid's defaults set
        var divisor = input.Context.Config.Flowchart.HtmlLabels ?? true ? 2 : 1;
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, (h / 2) - (label.Height + (input.Padding / divisor) - label.OffsetY)));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>windowPane</c>: a rectangle with lines of a window pane at its top and left.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult WindowPane(ShapeInput input)
    {
        const double rectOffset = 10;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 2);
        var x = -w / 2;
        var y = -h / 2;
        List<ShapePoint> outer = [new(x - rectOffset, y - rectOffset), new(x - rectOffset, y + h), new(x + w, y + h), new(x + w, y - rectOffset)];
        var path = new PathData()
            .M(x - rectOffset, y - rectOffset).L(x + w, y - rectOffset).L(x + w, y + h).L(x - rectOffset, y + h).L(x - rectOffset, y - rectOffset)
            .M(x - rectOffset, y).L(x + w, y)
            .M(x, y - rectOffset).L(x, y + h);
        draft.Part(path, Body(input.Paint), rectOffset / 2, rectOffset / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) + (rectOffset / 2) - label.OffsetX, (-label.Height / 2) + (rectOffset / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, outer, point));
    }

    /// <summary>
    ///     <c>filledCircle</c>: a junction dot of radius 7, filled with the node border color.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult FilledCircle(ShapeInput input)
    {
        const double radius = 7;
        var draft = new Draft(input);
        var css = input.Paint.Css;

        // The inline fill holds unless the node's own styles replace the inline style
        var paint = HasUserStyles(input.Paint) ? Body(input.Paint) : new PartPaint(input.Paint.Theme.NodeBorder, css.Stroke, css.StrokeWidth, null);
        draft.Part(new PathData().Oval(0, 0, radius, radius), paint);
        return draft.Finish((node, point) => IntersectCircle(node, radius, point));
    }

    /// <summary>
    ///     <c>trapezoidalPentagon</c>: a loop-limit pentagon with cut top corners.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult TrapezoidalPentagon(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 2);
        List<ShapePoint> points =
        [
            new(-w / 2 * 0.8, -h / 2), new(w / 2 * 0.8, -h / 2), new(w / 2, -h / 2 * 0.6),
            new(w / 2, h / 2), new(-w / 2, h / 2), new(-w / 2, -h / 2 * 0.6),
        ];
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>flippedTriangle</c>: a triangle pointing down, the label near its top.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult FlippedTriangle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + input.Padding;
        var h = w + label.Height;
        List<ShapePoint> points = [new(0, -h), new(h, -h), new(h / 2, 0)];
        draft.Part(new PathData().Polygon(points), Body(input.Paint), -h / 2, h / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, (-h / 2) + (input.Padding / 2) + label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>slopedRect</c>: a manual-input shape with a sloped top.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult SlopedRect(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 2);
        var x = -w / 2;
        var y = -h / 2;
        List<ShapePoint> points = [new(x, y), new(x, y + h), new(x + w, y + h), new(x + w, y - (h / 2))];
        draft.Part(new PathData().Polygon(points), Body(input.Paint), 0, h / 4);
        draft.PlaceLabel(label, new ShapePoint((-w / 2) + input.Padding - label.OffsetX, (-h / 4) + input.Padding - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>multiWaveEdgedRectangle</c>: stacked documents with wavy bottom edges.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult MultiWaveEdgedRectangle(ShapeInput input)
    {
        const double rectOffset = 10;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 3);
        var amplitude = h / 8;
        var finalH = h + (amplitude / 2);
        var x = -w / 2;
        var y = -finalH / 2;
        var wave = SineWavePoints(x - rectOffset, y + finalH + rectOffset, x + w - rectOffset, y + finalH + rectOffset, amplitude, 0.8);
        var last = wave[^1];
        var outer = new List<ShapePoint> { new(x - rectOffset, y + rectOffset), new(x - rectOffset, y + finalH + rectOffset) };
        outer.AddRange(wave);
        outer.AddRange(
        [
            new(x + w - rectOffset, last.Y - rectOffset), new(x + w, last.Y - rectOffset), new(x + w, last.Y - (2 * rectOffset)),
            new(x + w + rectOffset, last.Y - (2 * rectOffset)), new(x + w + rectOffset, y - rectOffset), new(x + rectOffset, y - rectOffset),
            new(x + rectOffset, y), new(x, y), new(x, y + rectOffset),
        ]);
        List<ShapePoint> inner = [new(x, y + rectOffset), new(x + w - rectOffset, y + rectOffset), new(x + w - rectOffset, last.Y - rectOffset), new(x + w, last.Y - rectOffset), new(x + w, y), new(x, y)];
        draft.Part(new PathData().Polygon(outer), Body(input.Paint), 0, -amplitude / 2);
        draft.Part(new PathData().Polygon(inner), Body(input.Paint), 0, -amplitude / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - rectOffset - label.OffsetX, (-label.Height / 2) + rectOffset - (amplitude / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, outer, point));
    }

    /// <summary>
    ///     <c>multiRect</c>: stacked rectangles.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult MultiRect(ShapeInput input)
    {
        const double rectOffset = 5;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 2);
        var x = -w / 2;
        var y = -h / 2;
        List<ShapePoint> outer =
        [
            new(x - rectOffset, y + rectOffset), new(x - rectOffset, y + h + rectOffset), new(x + w - rectOffset, y + h + rectOffset),
            new(x + w - rectOffset, y + h), new(x + w, y + h), new(x + w, y + h - rectOffset), new(x + w + rectOffset, y + h - rectOffset),
            new(x + w + rectOffset, y - rectOffset), new(x + rectOffset, y - rectOffset), new(x + rectOffset, y), new(x, y), new(x, y + rectOffset),
        ];
        List<ShapePoint> inner = [new(x, y + rectOffset), new(x + w - rectOffset, y + rectOffset), new(x + w - rectOffset, y + h), new(x + w, y + h), new(x + w, y), new(x, y)];
        draft.Part(new PathData().Polygon(outer), Body(input.Paint));
        draft.Part(new PathData().Polygon(inner), Body(input.Paint));
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - rectOffset - label.OffsetX, (-label.Height / 2) + rectOffset - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, outer, point));
    }

    /// <summary>
    ///     <c>bowTieRect</c>: stored data, a rectangle with concave and convex arcs at its ends.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult BowTieRect(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var totalHeight = label.Height + input.Padding;
        var ry = totalHeight / 2;
        var rx = ry / (2.5 + (totalHeight / 50));

        // Mermaid widens the box by the arc's sagitta and takes it off again, so the width is the label's and padding's
        var w = label.Width + (input.Padding * 2);
        var h = totalHeight;
        var points = new List<ShapePoint> { new(w / 2, -h / 2), new(-w / 2, -h / 2) };
        points.AddRange(ArcPoints(-w / 2, -h / 2, -w / 2, h / 2, rx, ry, clockwise: false));
        points.Add(new ShapePoint(w / 2, h / 2));
        points.AddRange(ArcPoints(w / 2, h / 2, w / 2, -h / 2, rx, ry, clockwise: true));
        draft.Part(new PathData().Polygon(points), Body(input.Paint), rx / 2, 0);
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     The 20 points of an elliptical arc between two points (<c>bowTieRect</c>'s <c>generateArcPoints</c>).
    /// </summary>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="x2">The end x.</param>
    /// <param name="y2">The end y.</param>
    /// <param name="rx">The x radius.</param>
    /// <param name="ry">The y radius.</param>
    /// <param name="clockwise">Whether the arc turns clockwise.</param>
    /// <returns>The points (none when the radii are too small, where Mermaid throws).</returns>
    private static List<ShapePoint> ArcPoints(double x1, double y1, double x2, double y2, double rx, double ry, bool clockwise)
    {
        const int count = 20;
        var midX = (x1 + x2) / 2;
        var midY = (y1 + y2) / 2;
        var angle = Math.Atan2(y2 - y1, x2 - x1);
        var tx = (x2 - x1) / 2 / rx;
        var ty = (y2 - y1) / 2 / ry;
        var distance = Math.Sqrt((tx * tx) + (ty * ty));
        if (distance > 1)
        {
            return [];
        }

        var scaled = Math.Sqrt(1 - (distance * distance));
        var sign = clockwise ? -1 : 1;
        var centerX = midX + (scaled * ry * Math.Sin(angle) * sign);
        var centerY = midY - (scaled * rx * Math.Cos(angle) * sign);
        var start = Math.Atan2((y1 - centerY) / ry, (x1 - centerX) / rx);
        var end = Math.Atan2((y2 - centerY) / ry, (x2 - centerX) / rx);
        var range = end - start;
        if (clockwise && range < 0)
        {
            range += 2 * Math.PI;
        }

        if (!clockwise && range > 0)
        {
            range -= 2 * Math.PI;
        }

        var points = new List<ShapePoint>(count);
        for (var i = 0; i < count; i++)
        {
            var a = start + ((double)i / (count - 1) * range);
            points.Add(new ShapePoint(centerX + (rx * Math.Cos(a)), centerY + (ry * Math.Sin(a))));
        }

        return points;
    }

    /// <summary>
    ///     <c>crossedCircle</c>: a summary circle of radius 30 with a cross, without a label.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult CrossedCircle(ShapeInput input)
    {
        const double radius = 30;
        var draft = new Draft(input);
        var d = radius * Math.Cos(Math.PI / 4);
        var e = radius * Math.Sin(Math.PI / 4);
        draft.Part(new PathData().Oval(0, 0, radius, radius), Body(input.Paint));
        draft.Part(new PathData().M(-d, e).L(d, -e).M(d, e).L(-d, -e), Body(input.Paint));
        return draft.Finish((node, point) => IntersectCircle(node, radius, point));
    }

    /// <summary>
    ///     <c>taggedWaveEdgedRectangle</c>: a document with a folded tag at its bottom right.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult TaggedWaveEdgedRectangle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 2);
        var amplitude = h / 8;
        var tagWidth = 0.2 * w;
        var tagHeight = 0.2 * h;
        var finalH = h + amplitude;
        var edge = (w / 2) + (w / 2 * 0.1);
        var points = new List<ShapePoint> { new(-edge, finalH / 2) };
        points.AddRange(SineWavePoints(-edge, finalH / 2, edge, finalH / 2, amplitude, 0.8));
        points.Add(new ShapePoint(edge, -finalH / 2));
        points.Add(new ShapePoint(-edge, -finalH / 2));
        var x = (-w / 2) + (w / 2 * 0.1);
        var y = (-finalH / 2) - (tagHeight * 0.4);
        var tag = new List<ShapePoint> { new(x + w - tagWidth, (y + h) * 1.3), new(x + w, y + h - tagHeight), new(x + w, (y + h) * 0.9) };
        tag.AddRange(SineWavePoints(x + w, (y + h) * 1.25, x + w - tagWidth, (y + h) * 1.3, -h * 0.02, 0.5));
        draft.Part(new PathData().Polygon(points), Body(input.Paint), 0, -amplitude / 2);
        draft.Part(new PathData().Polygon(tag), Body(input.Paint), 0, -amplitude / 2);
        draft.PlaceLabel(label, new ShapePoint((-w / 2) + input.Padding - label.OffsetX, (-h / 2) + input.Padding - (amplitude / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>taggedRect</c>: a rectangle with a tag at its bottom right.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult TaggedRect(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var h = label.Height + (input.Padding * 2);
        var tag = 0.2 * h;
        var w = label.Width + (input.Padding * 2);
        var x = -w / 2;
        var y = -h / 2;
        List<ShapePoint> rect = [new(x - (tag / 2), y), new(x + w + (tag / 2), y), new(x + w + (tag / 2), y + h), new(x - (tag / 2), y + h)];
        List<ShapePoint> tagPoints = [new(x + w - (tag / 2), y + h), new(x + w + (tag / 2), y + h), new(x + w + (tag / 2), y + h - tag)];
        draft.Part(new PathData().Polygon(rect), Body(input.Paint));
        draft.Part(new PathData().Polygon(tagPoints), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, rect, point));
    }

    /// <summary>
    ///     <c>waveRectangle</c>: a paper tape with wavy top and bottom edges.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult WaveRectangle(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + input.Padding;
        var amplitude = h / 8;
        var finalH = h + (amplitude * 2);
        var points = new List<ShapePoint> { new(-w / 2, finalH / 2) };
        points.AddRange(SineWavePoints(-w / 2, finalH / 2, w / 2, finalH / 2, amplitude, 1));
        points.Add(new ShapePoint(w / 2, -finalH / 2));
        points.AddRange(SineWavePoints(w / 2, -finalH / 2, -w / 2, -finalH / 2, amplitude, -1));
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        draft.PlaceLabel(label, ShapeInput.DefaultLabelOffset(label));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>linedWaveEdgedRect</c>: a document with a wavy bottom edge and a line down its left side.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult LinedWaveEdgedRect(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var w = label.Width + (input.Padding * 2);
        var h = label.Height + (input.Padding * 2);
        var amplitude = h / 8;
        var finalH = h + amplitude;
        var edge = (w / 2) + (w / 2 * 0.1);
        var points = new List<ShapePoint> { new(-edge, -finalH / 2), new(-edge, finalH / 2) };
        points.AddRange(SineWavePoints(-edge, finalH / 2, edge, finalH / 2, amplitude, 0.8));
        points.AddRange([new(edge, -finalH / 2), new(-edge, -finalH / 2), new(-w / 2, -finalH / 2), new(-w / 2, finalH / 2 * 1.1), new(-w / 2, -finalH / 2)]);
        draft.Part(new PathData().Polygon(points), Body(input.Paint), 0, -amplitude / 2);
        draft.PlaceLabel(label, new ShapePoint((-w / 2) + input.Padding + (w / 2 * 0.1 / 2) - label.OffsetX, (-h / 2) + input.Padding - (amplitude / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>folder</c>: a rectangle with a raised tab on its top-left edge.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Folder(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var padding = input.Padding;
        var w = Math.Max(label.Width + (padding * 2), 90);
        var contentHeight = label.Height + (padding * 2);
        var tabHeight = Math.Max(Math.Min(contentHeight * 0.16, 14), 8);
        var totalHeight = contentHeight + tabHeight;
        var bodyHeight = totalHeight - tabHeight;
        var tabWidth = Math.Max(w * 0.38, 28);
        var top = -totalHeight / 2;
        List<ShapePoint> points =
        [
            new(-w / 2, top), new((-w / 2) + tabWidth, top), new((-w / 2) + tabWidth, top + tabHeight),
            new(w / 2, top + tabHeight), new(w / 2, totalHeight / 2), new(-w / 2, totalHeight / 2),
        ];
        draft.Part(new PathData().Polygon(points), Body(input.Paint));
        var bodyCenterY = top + tabHeight + (bodyHeight / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, bodyCenterY - (label.Height / 2) - label.OffsetY));
        return draft.Finish((node, point) => IntersectPolygon(node, points, point));
    }

    /// <summary>
    ///     <c>bucket</c>: an open-top container narrowing to a curved base, with an elliptical rim.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Bucket(ShapeInput input)
    {
        const int arcSegments = 12;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var accent = input.Paint.Theme.NodeBorder ?? input.Paint.Theme.LineColor;
        var padding = input.Padding;
        var w = Math.Max(label.Width + (padding * 2), 80);
        var rimRy = Math.Max(Math.Min(w * 0.08, 12), 5);
        var totalHeight = label.Height + (padding * 2) + rimRy;
        var topY = (-totalHeight / 2) + rimRy;
        var bottomY = totalHeight / 2;
        var bottomW = w * 0.72;
        var body = new PathData().M(-w / 2, topY).L(-bottomW / 2, bottomY).A(bottomW / 2, rimRy, 0, false, false, bottomW / 2, bottomY)
            .L(w / 2, topY).A(w / 2, rimRy, 0, false, false, -w / 2, topY).Z();
        draft.Part(body, Body(input.Paint));
        draft.Part(new PathData().Oval(0, topY, w / 2, rimRy), new PartPaint(null, accent, 1, null));
        var bodyCenterY = topY + ((bottomY - topY) / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, bodyCenterY - (label.Height / 2) - label.OffsetY));
        List<ShapePoint> Arc(double rx, double cy, double ySign) =>
            [.. Enumerable.Range(0, arcSegments + 1).Select(i =>
            {
                var theta = Math.PI - (i * Math.PI / arcSegments);
                return new ShapePoint(rx * Math.Cos(theta), cy + (ySign * rimRy * Math.Sin(theta)));
            })];
        var outline = Arc(w / 2, topY, -1);
        var bottom = Arc(bottomW / 2, bottomY, 1);
        bottom.Reverse();
        outline.AddRange(bottom);
        return draft.Finish((node, point) => IntersectPolygon(node, outline, point));
    }

    /// <summary>
    ///     <c>consoleWindow</c>: a rounded terminal window with a prompt glyph at its top left.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult ConsoleWindow(ShapeInput input)
    {
        const double glyphBand = 20;
        const double radius = 12;
        const double glyphSize = 14;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var accent = input.Paint.Theme.NodeBorder ?? input.Paint.Theme.LineColor;
        var w = Math.Max(label.Width + (input.Padding * 2), 90);
        var h = label.Height + (input.Padding * 2) + glyphBand;
        var top = -h / 2;
        draft.Part(new PathData().Rect(-w / 2, top, w, h, radius), Body(input.Paint));
        if (accent is { } color)
        {
            // The prompt glyph '>_' in a bold monospace face, 0.6 em per character
            var x = (-w / 2) + 12;
            var baseline = top + 16;
            var font = new SceneFont("monospace", glyphSize, Bold: true, Italic: false);
            var box = new SceneRect(x, baseline - (glyphSize * 0.9), 2 * 0.6 * glyphSize, glyphSize * 1.2);
            draft.Item(new SceneText(x, baseline, SceneTextAnchor.Start, [new SceneTextRun(">_", font, color)], box) { Role = "node-glyph" }, box);
        }

        var bodyCenterY = top + glyphBand + ((h - glyphBand) / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, bodyCenterY - (label.Height / 2) - label.OffsetY));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>browser</c>: a rounded window with a bar of dots and an address-bar hint.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Browser(ShapeInput input)
    {
        const double barHeight = 18;
        const double radius = 12;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var accent = input.Paint.Theme.NodeBorder ?? input.Paint.Theme.LineColor;
        var w = Math.Max(label.Width + (input.Padding * 2), 90);
        var h = label.Height + (input.Padding * 2) + barHeight;
        var top = -h / 2;
        draft.Part(new PathData().Rect(-w / 2, top, w, h, radius), Body(input.Paint));
        draft.Part(new PathData().M(-w / 2, top + barHeight).L(w / 2, top + barHeight), new PartPaint(null, accent, 1, null));
        for (var i = 0; i < 3; i++)
        {
            draft.Part(new PathData().Oval((-w / 2) + 12 + (i * 9), top + (barHeight / 2), 2.5, 2.5), new PartPaint(accent, null, 0, null));
        }

        draft.Part(new PathData().Rect((-w / 2) + 44, top + 4, Math.Max(w - 56, 10), barHeight - 8, 3), new PartPaint(null, accent, 1, null, 0.6));
        var bodyCenterY = top + barHeight + ((h - barHeight) / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, bodyCenterY - (label.Height / 2) - label.OffsetY));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>person</c>: a circular head above a rounded body.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Person(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var padding = input.Padding;
        var w = Math.Max(label.Width + (padding * 2), 100);
        var headRadius = Math.Min(Math.Max(w * 0.23, 16), 56);
        var overlap = headRadius * 0.27;
        var bodyHeight = label.Height + (padding * 2);
        var bodyRadius = Math.Min(w * 0.177, bodyHeight * 0.45);
        var totalHeight = bodyHeight + (2 * headRadius) - overlap;
        var top = -totalHeight / 2;
        var bodyTop = top + (2 * headRadius) - overlap;
        draft.Part(new PathData().Rect(-w / 2, bodyTop, w, bodyHeight, bodyRadius), Body(input.Paint));
        draft.Part(new PathData().Oval(0, top + headRadius, headRadius, headRadius), Body(input.Paint));
        var bodyCenterY = bodyTop + (bodyHeight / 2);
        draft.PlaceLabel(label, new ShapePoint((-label.Width / 2) - label.OffsetX, bodyCenterY - (label.Height / 2) - label.OffsetY));

        var headCenterY = top + headRadius;
        var phiRight = Math.Asin(Math.Min(1, (bodyTop - headCenterY) / headRadius)) * 180 / Math.PI;
        var outline = CirclePoints(0, -headCenterY, headRadius, 24, 180 + phiRight, -phiRight);
        outline.AddRange(CirclePoints(-((-w / 2) + bodyRadius), -(bodyTop + bodyRadius), bodyRadius, 12, 90, 0));
        outline.AddRange(CirclePoints(-((-w / 2) + bodyRadius), -((totalHeight / 2) - bodyRadius), bodyRadius, 12, 360, 270));
        outline.AddRange(CirclePoints(-((w / 2) - bodyRadius), -((totalHeight / 2) - bodyRadius), bodyRadius, 12, 270, 180));
        outline.AddRange(CirclePoints(-((w / 2) - bodyRadius), -(bodyTop + bodyRadius), bodyRadius, 12, 180, 90));
        return draft.Finish((node, point) => IntersectPolygon(node, outline, point));
    }

    /// <summary>
    ///     <c>bang</c>: a spiky bubble of arcs.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Bang(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var halfPadding = input.Padding / 2;
        var w = label.Width + (10 * halfPadding);
        var h = label.Height + (8 * halfPadding);
        var r = 0.15 * w;
        var ew = Math.Max(w, label.Width + 20);
        var eh = Math.Max(h, label.Height + 20);
        var path = new PathData().M(0, 0)
            .RelativeA(r, r, 1, false, false, ew * 0.25, -eh * 0.1)
            .RelativeA(r, r, 1, false, false, ew * 0.25, 0)
            .RelativeA(r, r, 1, false, false, ew * 0.25, 0)
            .RelativeA(r, r, 1, false, false, ew * 0.25, eh * 0.1)
            .RelativeA(r, r, 1, false, false, ew * 0.15, eh * 0.33)
            .RelativeA(r * 0.8, r * 0.8, 1, false, false, 0, eh * 0.34)
            .RelativeA(r, r, 1, false, false, -ew * 0.15, eh * 0.33)
            .RelativeA(r, r, 1, false, false, -ew * 0.25, eh * 0.15)
            .RelativeA(r, r, 1, false, false, -ew * 0.25, 0)
            .RelativeA(r, r, 1, false, false, -ew * 0.25, 0)
            .RelativeA(r, r, 1, false, false, -ew * 0.25, -eh * 0.15)
            .RelativeA(r, r, 1, false, false, -ew * 0.1, -eh * 0.33)
            .RelativeA(r * 0.8, r * 0.8, 1, false, false, 0, -eh * 0.34)
            .RelativeA(r, r, 1, false, false, ew * 0.1, -eh * 0.33)
            .H(0).V(0).Z();
        draft.Part(path, Body(input.Paint), -ew / 2, -eh / 2);
        draft.PlaceLabel(label, new ShapePoint(-label.Width / 2, -label.Height / 2));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>cloud</c>: a cloud of arcs.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult Cloud(ShapeInput input)
    {
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var halfPadding = input.Padding / 2;
        var w = label.Width + (2 * halfPadding);
        var h = label.Height + (2 * halfPadding);
        var r1 = 0.15 * w;
        var r2 = 0.25 * w;
        var r3 = 0.35 * w;
        var r4 = 0.2 * w;
        var path = new PathData().M(0, 0)
            .RelativeA(r1, r1, 0, false, true, w * 0.25, -w * 0.1)
            .RelativeA(r3, r3, 1, false, true, w * 0.4, -w * 0.1)
            .RelativeA(r2, r2, 1, false, true, w * 0.35, w * 0.2)
            .RelativeA(r1, r1, 1, false, true, w * 0.15, h * 0.35)
            .RelativeA(r4, r4, 1, false, true, -w * 0.15, h * 0.65)
            .RelativeA(r2, r1, 1, false, true, -w * 0.25, w * 0.15)
            .RelativeA(r3, r3, 1, false, true, -w * 0.5, 0)
            .RelativeA(r1, r1, 1, false, true, -w * 0.25, -w * 0.15)
            .RelativeA(r1, r1, 1, false, true, -w * 0.1, -h * 0.35)
            .RelativeA(r4, r4, 1, false, true, w * 0.1, -h * 0.65)
            .H(0).V(0).Z();
        draft.Part(path, Body(input.Paint), -w / 2, -h / 2);
        draft.PlaceLabel(label, new ShapePoint(-label.Width / 2, -label.Height / 2));
        return draft.Finish(IntersectRect);
    }

    /// <summary>
    ///     <c>collapsedGroup</c>: a collapsed subgraph, its title above a dashed separator and an ellipsis of three dots.
    /// </summary>
    /// <param name="input">The node.</param>
    /// <returns>The drawn node.</returns>
    private static ShapeResult CollapsedGroup(ShapeInput input)
    {
        const double indicatorRow = 20;
        const double separatorGap = 8;
        const double radius = 8;
        var draft = new Draft(input);
        var label = input.MeasureLabel();
        var padding = input.Padding;
        var clusterBorder = input.Paint.Theme.ClusterBorder;
        var totalWidth = Math.Max(label.Width + (padding * 2), 80);
        var totalHeight = label.Height + separatorGap + indicatorRow + (padding * 2);
        var x = -totalWidth / 2;
        var y = -totalHeight / 2;
        var shift = -(separatorGap + indicatorRow) / 2;
        draft.Part(new PathData().Rect(x, y, totalWidth, totalHeight, radius), Body(input.Paint));
        draft.PlaceLabel(label, label.IsHtml
            ? new ShapePoint(-label.Width / 2, (-label.Height / 2) + shift)
            : new ShapePoint(0, (-label.Height / 2) + shift));
        var separatorY = y + padding + label.Height + separatorGap;
        draft.Part(new PathData().M(x + 8, separatorY).L(x + totalWidth - 8, separatorY), new PartPaint(null, clusterBorder, 0.75, [3, 3]));
        for (var i = -1; i <= 1; i++)
        {
            draft.Part(new PathData().Oval(i * 10, separatorY + (indicatorRow / 2), 2.5, 2.5), new PartPaint(clusterBorder, null, 0, null, 0.6));
        }

        return draft.Finish(IntersectRect);
    }
}
