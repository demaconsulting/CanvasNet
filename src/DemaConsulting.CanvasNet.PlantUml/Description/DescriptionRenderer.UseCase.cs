// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <content>
///     The symbols of use case diagrams: the actor and the business actor (<c>decoration/symbol/USymbolActor</c>,
///     <c>USymbolActorBusiness</c>, and <c>USymbolSimpleAbstract</c> with <c>skin/ActorStickMan</c>) and the use case and
///     the business use case (<c>decoration/symbol/USymbolUsecase</c> with <c>klimt/shape/TextBlockInEllipse</c>,
///     <c>svek/image/Footprint</c>, <c>ContainingEllipse</c>, <c>SmallestEnclosingCircle</c>, and
///     <c>RotatedEllipse</c>).
/// </content>
internal static partial class DescriptionRenderer
{
    /// <summary>The space PlantUML adds to the width and the height of a use case's enclosing ellipse.</summary>
    public const double UseCaseGrowth = 6;

    /// <summary>The margin on each side of the texts of a business use case.</summary>
    public const double BusinessUseCaseMargin = 7;

    /// <summary>The width of a stick figure drawn with no line width: its arms.</summary>
    public const double StickFigureWidth = 26;

    /// <summary>The height of a stick figure drawn with no line width: head 16, body 27, legs 15, and 1.</summary>
    public const double StickFigureHeight = 59;

    /// <summary>The smallest ratio of height to width of a use case's ellipse (before it grows).</summary>
    private const double MinEllipseRatio = 0.2;

    /// <summary>The largest ratio of height to width of a use case's ellipse (before it grows).</summary>
    private const double MaxEllipseRatio = 0.8;

    /// <summary>
    ///     Builds the image of an actor or a business actor (<c>USymbolSimpleAbstract</c> with <c>ActorStickMan</c>):
    ///     the stereotypes (with a margin of 1 on each side), the stick figure below them, and the name below the
    ///     figure (its lines aligned left), each centered. The figure is 26 wide and 59 high plus twice the line width;
    ///     a business actor's head is crossed by a slash.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <param name="business">Whether it is a business actor.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage ActorImage(Painter p, bool business)
    {
        var thickness = p.Style.LineWidth;
        var (figureWidth, figureHeight) = (StickFigureWidth + (2 * thickness), StickFigureHeight + (2 * thickness));
        var stereoHeight = p.Stereotypes?.Height ?? 0;
        var name = p.Name;
        var w = Math.Max(Math.Max(p.StereotypeWidth, figureWidth), name.Width);
        var h = stereoHeight + figureHeight + name.Height;
        return Painter.Box(w, h, (x, y, items) =>
        {
            // The figure: the sequence diagram's stick figure, which is drawn for a line half a pixel wide
            var center = x + (w / 2);
            var top = y + stereoHeight + thickness - 0.5;
            var figure = SequenceRenderer.StickFigure(center, top, p.Fill, p.Stroke).ToList();
            items.Add(p.Own(figure[0], "description"));
            if (business)
            {
                // The slash of ActorStickMan.specialBusiness: a chord of the head between two angles
                var (headX, headY) = (center, top + 8.5);
                var alpha = 21 * Math.PI / 64;
                var (a1, a2) = ((Math.PI / 4) + alpha, (Math.PI / 4) - alpha);
                items.Add(p.Path(
                    SceneShapes.Line(headX + (8 * Math.Cos(a1)), headY + (8 * Math.Sin(a1)), headX + (8 * Math.Cos(a2)), headY + (8 * Math.Sin(a2))),
                    "description-detail",
                    filled: false));
            }

            items.AddRange(figure.Skip(1).Select(f => p.Own(f, "description-detail")));
            if (p.Stereotypes is { } stereo)
            {
                items.AddRange(p.DrawText(stereo, x + ((w - p.StereotypeWidth) / 2) + 1, y, TextAlign.Center, "description-stereotype"));
            }

            items.AddRange(p.DrawText(name, x + ((w - name.Width) / 2), y + stereoHeight + figureHeight, TextAlign.Left, "description-name"));
        });
    }

    /// <summary>
    ///     Builds the image of a use case or a business use case (<c>USymbolUsecase</c>): the stereotypes (with a margin
    ///     of 1 on each side) over the name, both centered, in the smallest ellipse of PlantUML's proportions that holds
    ///     every line of text, grown by 6 (<c>TextBlockInEllipse</c>). A business use case has a margin of 7 on each side
    ///     of its texts and a slash across its lower right side. Links end on the ellipse.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <param name="business">Whether it is a business use case.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage UseCaseImage(Painter p, bool business)
    {
        var name = p.Name;
        var stereo = p.Stereotypes;
        var stereoWidth = p.StereotypeWidth;
        var stereoHeight = stereo?.Height ?? 0;
        var margin = business ? BusinessUseCaseMargin : 0;
        var textWidth = Math.Max(stereoWidth, name.Width);
        var (width, height) = (textWidth + (2 * margin), stereoHeight + name.Height);

        // The footprint of the texts (Footprint): the corners of every line, of the stereotypes' margin box, and of
        // a business use case's margin box
        var points = new List<(double X, double Y)>();
        var stereoLeft = margin + ((textWidth - stereoWidth) / 2);
        var nameLeft = margin + ((textWidth - name.Width) / 2);
        if (stereo is not null)
        {
            points.Add((stereoLeft, 0));
            points.Add((stereoLeft + stereoWidth, stereoHeight));
            AddFootprint(points, stereo, stereoLeft + 1, 0);
        }

        AddFootprint(points, name, nameLeft, stereoHeight);
        if (business)
        {
            points.Add((0, 0));
            points.Add((width, height));
        }

        // The ellipse (ContainingEllipse): the smallest circle around the points with y divided by the ratio of the
        // texts, scaled back, grown by 6
        var ratio = EllipseRatio(width, height);
        var (cx, cy, radius) = EnclosingCircle(points.Select(pt => (pt.X, pt.Y / ratio)).ToList());
        var (w, h) = ((2 * radius) + UseCaseGrowth, (2 * radius * ratio) + UseCaseGrowth);
        var dx = (w / 2) - cx;
        var dy = (h / 2) - (cy * ratio) - 2;
        return new DescriptionImage(w, h, new SvekClip(ClipShape.Ellipse, new SceneRect(0, 0, w, h)), (x, y) =>
        {
            var items = new List<SceneItem> { p.Path(SceneShapes.Ellipse(x + (w / 2), y + (h / 2), w / 2, h / 2), "description") };
            if (business)
            {
                var ((x1, y1), (x2, y2)) = BusinessSlash(w, h);
                items.Add(p.Path(SceneShapes.Line(x + x1, y + y1, x + x2, y + y2), "description-detail", filled: false));
            }

            if (stereo is not null)
            {
                items.AddRange(p.DrawText(stereo, x + dx + stereoLeft + 1, y + dy, TextAlign.Center, "description-stereotype"));
            }

            items.AddRange(p.DrawText(name, x + dx + nameLeft, y + dy + stereoHeight, TextAlign.Center, "description-name"));
            return items;
        });
    }

    /// <summary>
    ///     Gets the ratio of height to width of a use case's ellipse: the ratio of its texts, between 0.2 and 0.8 (1 for
    ///     an empty text, as PlantUML's <c>YTransformer</c> takes a ratio that is not a number).
    /// </summary>
    /// <param name="width">The width of the texts.</param>
    /// <param name="height">The height of the texts.</param>
    /// <returns>The ratio.</returns>
    internal static double EllipseRatio(double width, double height)
    {
        if (!(width > 0))
        {
            return height > 0 ? MaxEllipseRatio : 1;
        }

        return Math.Clamp(height / width, MinEllipseRatio, MaxEllipseRatio);
    }

    /// <summary>
    ///     Gets the slash of a business use case (<c>USymbolUsecase.specialBusiness</c>): the chord of the ellipse
    ///     shrunk by 1 % between the angle of -20 degrees and the other angle where the ellipse turned by 45 degrees
    ///     crosses the vertical through the first point.
    /// </summary>
    /// <param name="width">The width of the ellipse.</param>
    /// <param name="height">The height of the ellipse.</param>
    /// <returns>The two ends, relative to the top-left corner of the ellipse.</returns>
    internal static ((double X, double Y) P1, (double X, double Y) P2) BusinessSlash(double width, double height)
    {
        // RotatedEllipse.getOtherTheta with beta = 45 degrees
        var (a, b, beta) = (width / 2, height / 2, Math.PI / 4);
        var theta1 = 20.0 * Math.PI / 180;
        var z = (a * Math.Cos(theta1) * Math.Cos(beta)) - (b * Math.Sin(theta1) * Math.Sin(beta));
        var (ra, rb) = (a * Math.Cos(beta), b * Math.Sin(beta));
        var other = (2 * ra * z / ((ra * ra) + (rb * rb))) - Math.Cos(theta1);
        var theta2 = -Math.Acos(Math.Clamp(other, -1, 1));

        // UEllipse.getPointAtAngle of the ellipse scaled by 0.99 (from the same corner)
        var (sw, sh) = (width * 0.99, height * 0.99);
        (double X, double Y) At(double angle) => ((sw / 2) + (sw / 2 * Math.Cos(angle)), (sh / 2) + (sh / 2 * Math.Sin(angle)));
        return (At(-theta1), At(-theta2));
    }

    /// <summary>
    ///     Finds the smallest circle that encloses points (PlantUML's <c>SmallestEnclosingCircle</c>; the circle is
    ///     unique, so the points are visited in a fixed shuffled order, which keeps the expected work linear).
    /// </summary>
    /// <param name="points">The points.</param>
    /// <returns>The center and the radius; a circle of radius 0 at the origin for no points.</returns>
    internal static (double X, double Y, double Radius) EnclosingCircle(IReadOnlyList<(double X, double Y)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var order = points.Distinct().ToList();
        if (order.Count == 0)
        {
            return (0, 0, 0);
        }

        // A fixed linear congruential shuffle: the same order on every platform and every call
        var seed = 0x2545F491u;
        for (var i = order.Count - 1; i > 0; i--)
        {
            seed = unchecked((seed * 1664525u) + 1013904223u);
            var j = (int)(seed % (uint)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }

        var circle = (order[0].X, order[0].Y, 0.0);
        for (var i = 1; i < order.Count; i++)
        {
            if (Inside(circle, order[i]))
            {
                continue;
            }

            circle = (order[i].X, order[i].Y, 0.0);
            for (var j = 0; j < i; j++)
            {
                if (Inside(circle, order[j]))
                {
                    continue;
                }

                circle = Diameter(order[i], order[j]);
                for (var k = 0; k < j; k++)
                {
                    if (!Inside(circle, order[k]))
                    {
                        circle = Circumscribed(order[i], order[j], order[k]);
                    }
                }
            }
        }

        return circle;
    }

    /// <summary>
    ///     Adds the footprint of a text block to the points: the corners of each line's box as PlantUML's
    ///     <c>Footprint</c> measures a text (from 1.5 below the top of the line's ascent to 1.5 below its baseline).
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="block">The block, its lines centered in its width.</param>
    /// <param name="left">The left of the block.</param>
    /// <param name="top">The top of the block.</param>
    private static void AddFootprint(List<(double X, double Y)> points, TextBlock block, double left, double top)
    {
        var width = block.Width;
        foreach (var line in block.Lines)
        {
            if (line.Runs.Count > 0)
            {
                var x = left + ((width - line.Width) / 2);
                var (y1, y2) = (top + 1.5 - line.Descent, top + line.Height + 1.5 - line.Descent);
                points.Add((x, y1));
                points.Add((x, y2));
                points.Add((x + line.Width, y1));
                points.Add((x + line.Width, y2));
            }

            top += line.Height;
        }
    }

    /// <summary>
    ///     Decides whether a point is in a circle (with a tolerance for rounding).
    /// </summary>
    /// <param name="circle">The circle.</param>
    /// <param name="point">The point.</param>
    /// <returns>True when it is.</returns>
    private static bool Inside((double X, double Y, double Radius) circle, (double X, double Y) point) =>
        Math.Sqrt(((point.X - circle.X) * (point.X - circle.X)) + ((point.Y - circle.Y) * (point.Y - circle.Y))) <= circle.Radius + (1e-9 * (1 + circle.Radius));

    /// <summary>
    ///     Gets the circle whose diameter joins two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The circle.</returns>
    private static (double X, double Y, double Radius) Diameter((double X, double Y) a, (double X, double Y) b)
    {
        var (x, y) = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        return (x, y, Math.Sqrt(((a.X - x) * (a.X - x)) + ((a.Y - y) * (a.Y - y))));
    }

    /// <summary>
    ///     Gets the circle through three points, or the largest circle on a diameter of two of them when they are on a
    ///     line.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <param name="c">The third point.</param>
    /// <returns>The circle.</returns>
    private static (double X, double Y, double Radius) Circumscribed((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        var d = 2 * ((a.X * (b.Y - c.Y)) + (b.X * (c.Y - a.Y)) + (c.X * (a.Y - b.Y)));
        if (Math.Abs(d) < 1e-12)
        {
            return new[] { Diameter(a, b), Diameter(a, c), Diameter(b, c) }.MaxBy(t => t.Radius);
        }

        var (a2, b2, c2) = ((a.X * a.X) + (a.Y * a.Y), (b.X * b.X) + (b.Y * b.Y), (c.X * c.X) + (c.Y * c.Y));
        var x = ((a2 * (b.Y - c.Y)) + (b2 * (c.Y - a.Y)) + (c2 * (a.Y - b.Y))) / d;
        var y = ((a2 * (c.X - b.X)) + (b2 * (a.X - c.X)) + (c2 * (b.X - a.X))) / d;
        return (x, y, Math.Sqrt(((a.X - x) * (a.X - x)) + ((a.Y - y) * (a.Y - y))));
    }
}
