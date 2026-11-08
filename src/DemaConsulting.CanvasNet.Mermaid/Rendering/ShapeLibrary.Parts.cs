// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The parts of a drawn node: path data written as Mermaid writes it, the paint of a part and the draft that
///     collects the parts and their box.
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>
    ///     Gets the paint of the parts that the node rule and the node's own styles paint (the <c>label-container</c>
    ///     elements): the node's styles win over the style sheet.
    /// </summary>
    /// <param name="paint">The node paint.</param>
    /// <returns>The part paint.</returns>
    private static PartPaint Body(NodePaint paint) => Body(paint, paint.Css.Fill, paint.Css.Stroke, paint.Css.StrokeWidth);

    /// <summary>
    ///     Gets the paint of a part with its own defaults that the node's own styles override.
    /// </summary>
    /// <param name="paint">The node paint.</param>
    /// <param name="fill">The default fill.</param>
    /// <param name="stroke">The default stroke.</param>
    /// <param name="strokeWidth">The default stroke width.</param>
    /// <returns>The part paint.</returns>
    private static PartPaint Body(NodePaint paint, Rgba32? fill, Rgba32? stroke, double strokeWidth)
    {
        var user = paint.User;
        var dash = user["stroke-dasharray"] is { } dashes && StyleDeclarations.TryParseDashArray(dashes, out var parsed) ? parsed : null;
        var opacity = user["opacity"] is { } value && StyleDeclarations.TryParseOpacity(value, out var factor) ? factor : 1;
        return new PartPaint(
            user["fill"] is not null ? user.Paint("fill") : fill,
            user["stroke"] is not null ? user.Paint("stroke") : stroke,
            user.Length("stroke-width", strokeWidth),
            dash,
            opacity);
    }

    /// <summary>
    ///     Gets whether the node has its own styles (Mermaid's <c>nodeStyles</c> is not empty), which replace a part's
    ///     inline style.
    /// </summary>
    /// <param name="paint">The node paint.</param>
    /// <returns>Whether it has.</returns>
    private static bool HasUserStyles(NodePaint paint) => paint.User.Declarations.Count > 0;

    /// <summary>
    ///     The paint of a part: fill, stroke, width, dashes and opacity.
    /// </summary>
    /// <param name="Fill">The fill, or null for none.</param>
    /// <param name="Stroke">The stroke color, or null for none.</param>
    /// <param name="StrokeWidth">The stroke width.</param>
    /// <param name="Dash">The dash lengths, or null for solid.</param>
    /// <param name="Opacity">The opacity of the part.</param>
    private sealed record PartPaint(Rgba32? Fill, Rgba32? Stroke, double StrokeWidth, IReadOnlyList<double>? Dash, double Opacity = 1)
    {
        /// <summary>Gets the paint that draws nothing.</summary>
        public static PartPaint None { get; } = new(null, null, 0, null);

        /// <summary>Gets the same paint with a stroke opacity.</summary>
        /// <param name="factor">The stroke opacity.</param>
        /// <returns>The paint.</returns>
        public PartPaint WithStrokeOpacity(double factor) => this with { Stroke = Scale(Stroke, factor) };

        /// <summary>
        ///     Makes the scene path of segments with this paint.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <param name="role">The role.</param>
        /// <returns>The path.</returns>
        public ScenePath ToPath(IReadOnlyList<SceneSegment> segments, string role)
        {
            var dash = Dash is { Count: > 0 } d && d.Any(v => v > 0) ? d : null;
            return new ScenePath(segments)
            {
                Role = role,
                Fill = Scale(Fill, Opacity),
                Stroke = Stroke is { } color && StrokeWidth > 0 ? new SceneStroke(Scale(color, Opacity)!.Value, StrokeWidth) { Dash = dash } : null,
            };
        }

        /// <summary>Multiplies the alpha of a color.</summary>
        /// <param name="color">The color.</param>
        /// <param name="factor">The factor.</param>
        /// <returns>The color.</returns>
        private static Rgba32? Scale(Rgba32? color, double factor) =>
            color is { } c && factor < 1 ? c with { A = (byte)Math.Round(c.A * Math.Max(factor, 0), MidpointRounding.AwayFromZero) } : color;
    }

    /// <summary>
    ///     SVG path data, written with Mermaid's commands (absolute and relative) and kept as absolute scene segments.
    /// </summary>
    private sealed class PathData
    {
        /// <summary>The segments.</summary>
        private readonly List<SceneSegment> _segments = [];

        /// <summary>The current point.</summary>
        private double _x;

        /// <summary>The current point.</summary>
        private double _y;

        /// <summary>The start of the current subpath.</summary>
        private double _startX;

        /// <summary>The start of the current subpath.</summary>
        private double _startY;

        /// <summary>Gets the segments.</summary>
        public IReadOnlyList<SceneSegment> Segments => _segments;

        /// <summary>Moves to a point (<c>M</c>).</summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>This path.</returns>
        public PathData M(double x, double y)
        {
            _segments.Add(SceneSegment.Move(x, y));
            (_x, _y, _startX, _startY) = (x, y, x, y);
            return this;
        }

        /// <summary>Draws a line to a point (<c>L</c>).</summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>This path.</returns>
        public PathData L(double x, double y)
        {
            _segments.Add(SceneSegment.Line(x, y));
            (_x, _y) = (x, y);
            return this;
        }

        /// <summary>Draws a line by an offset (<c>l</c>).</summary>
        /// <param name="dx">The x offset.</param>
        /// <param name="dy">The y offset.</param>
        /// <returns>This path.</returns>
        public PathData RelativeL(double dx, double dy) => L(_x + dx, _y + dy);

        /// <summary>Draws a horizontal line (<c>H</c>).</summary>
        /// <param name="x">The x.</param>
        /// <returns>This path.</returns>
        public PathData H(double x) => L(x, _y);

        /// <summary>Draws a vertical line (<c>V</c>).</summary>
        /// <param name="y">The y.</param>
        /// <returns>This path.</returns>
        public PathData V(double y) => L(_x, y);

        /// <summary>Draws an elliptical arc to a point (<c>A</c>).</summary>
        /// <param name="rx">The x radius.</param>
        /// <param name="ry">The y radius.</param>
        /// <param name="rotation">The rotation in degrees.</param>
        /// <param name="large">The large-arc flag.</param>
        /// <param name="sweep">The sweep flag.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>This path.</returns>
        public PathData A(double rx, double ry, double rotation, bool large, bool sweep, double x, double y)
        {
            _segments.Add(SceneSegment.Arc(rx, ry, rotation, large, sweep, x, y));
            (_x, _y) = (x, y);
            return this;
        }

        /// <summary>Draws an elliptical arc by an offset (<c>a</c>).</summary>
        /// <param name="rx">The x radius.</param>
        /// <param name="ry">The y radius.</param>
        /// <param name="rotation">The rotation in degrees.</param>
        /// <param name="large">The large-arc flag.</param>
        /// <param name="sweep">The sweep flag.</param>
        /// <param name="dx">The x offset.</param>
        /// <param name="dy">The y offset.</param>
        /// <returns>This path.</returns>
        public PathData RelativeA(double rx, double ry, double rotation, bool large, bool sweep, double dx, double dy) =>
            A(rx, ry, rotation, large, sweep, _x + dx, _y + dy);

        /// <summary>Closes the subpath (<c>Z</c>).</summary>
        /// <returns>This path.</returns>
        public PathData Z()
        {
            _segments.Add(SceneSegment.Close());
            (_x, _y) = (_startX, _startY);
            return this;
        }

        /// <summary>
        ///     Writes a polygon of points (<c>createPathFromPoints</c>: <c>M</c>, <c>L</c>..., then <c>Z</c> unless
        ///     open).
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="close">Whether the polygon is closed.</param>
        /// <returns>This path.</returns>
        public PathData Polygon(IReadOnlyList<ShapePoint> points, bool close = true)
        {
            for (var i = 0; i < points.Count; i++)
            {
                if (i == 0)
                {
                    M(points[i].X, points[i].Y);
                }
                else
                {
                    L(points[i].X, points[i].Y);
                }
            }

            return close && points.Count > 0 ? Z() : this;
        }

        /// <summary>
        ///     Writes a full ellipse as two arcs.
        /// </summary>
        /// <param name="cx">The center x.</param>
        /// <param name="cy">The center y.</param>
        /// <param name="rx">The x radius.</param>
        /// <param name="ry">The y radius.</param>
        /// <returns>This path.</returns>
        public PathData Oval(double cx, double cy, double rx, double ry) =>
            M(cx + rx, cy).A(rx, ry, 0, false, true, cx - rx, cy).A(rx, ry, 0, false, true, cx + rx, cy).Z();

        /// <summary>
        ///     Writes a rectangle, with rounded corners when the radius is positive (as an SVG <c>rect</c> with
        ///     <c>rx</c> and <c>ry</c>).
        /// </summary>
        /// <param name="x">The left.</param>
        /// <param name="y">The top.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="radius">The corner radius (clamped to half the sides, as SVG does).</param>
        /// <returns>This path.</returns>
        public PathData Rect(double x, double y, double width, double height, double radius = 0)
        {
            var r = Math.Min(Math.Max(radius, 0), Math.Min(width / 2, height / 2));
            if (!(r > 0))
            {
                return M(x, y).H(x + width).V(y + height).H(x).Z();
            }

            return M(x + r, y).H(x + width - r).A(r, r, 0, false, true, x + width, y + r)
                .V(y + height - r).A(r, r, 0, false, true, x + width - r, y + height)
                .H(x + r).A(r, r, 0, false, true, x, y + height - r)
                .V(y + r).A(r, r, 0, false, true, x + r, y).Z();
        }
    }

    /// <summary>
    ///     The draft of a drawn node: the items in drawing order and the box of everything drawn.
    /// </summary>
    private sealed class Draft
    {
        /// <summary>The items.</summary>
        private readonly List<SceneItem> _items = [];

        /// <summary>The input of the node.</summary>
        private readonly ShapeInput _input;

        /// <summary>The box of everything drawn so far.</summary>
        private SceneRect? _bounds;

        /// <summary>
        ///     Initializes the draft of a node.
        /// </summary>
        /// <param name="input">The node.</param>
        public Draft(ShapeInput input) => _input = input;

        /// <summary>Gets or sets the label.</summary>
        public LabelLayout Label { get; set; } = LabelLayout.Of(true, new SceneRect(0, 0, 0, 0));

        /// <summary>Gets or sets the translation of the label.</summary>
        public ShapePoint LabelOffset { get; set; }

        /// <summary>
        ///     Adds a part: its geometry, translated, painted.
        /// </summary>
        /// <param name="path">The geometry.</param>
        /// <param name="paint">The paint.</param>
        /// <param name="dx">The translation x of the element.</param>
        /// <param name="dy">The translation y of the element.</param>
        /// <param name="role">The role.</param>
        public void Part(PathData path, PartPaint paint, double dx = 0, double dy = 0, string role = "node")
        {
            var segments = Translate(path.Segments, dx, dy);
            Include(SegmentBounds(segments));
            if (paint.Fill is not null || paint.Stroke is not null)
            {
                _items.Add(paint.ToPath(segments, role) with { Id = _input.Node.DomId ?? _input.Node.Id, Location = _input.Node.Location });
            }
        }

        /// <summary>
        ///     Adds geometry that only counts for the box (an invisible part).
        /// </summary>
        /// <param name="path">The geometry.</param>
        /// <param name="dx">The translation x.</param>
        /// <param name="dy">The translation y.</param>
        public void Invisible(PathData path, double dx = 0, double dy = 0) => Include(SegmentBounds(Translate(path.Segments, dx, dy)));

        /// <summary>
        ///     Adds an item that is drawn as is (text, picture), counting its box.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="box">Its box.</param>
        public void Item(SceneItem item, SceneRect box)
        {
            _items.Add(item with { Id = _input.Node.DomId ?? _input.Node.Id, Location = _input.Node.Location });
            Include(box);
        }

        /// <summary>
        ///     Places the label at a translation and adds its items and its box.
        /// </summary>
        /// <param name="label">The measured label.</param>
        /// <param name="offset">The translation of the label's group.</param>
        public void PlaceLabel(LabelLayout label, ShapePoint offset)
        {
            Label = label;
            LabelOffset = offset;
            _items.AddRange(label.ToScene(offset.X, offset.Y, "label", _input.Node.DomId ?? _input.Node.Id, _input.Node.Location));
            if (label.Width > 0 || label.Height > 0)
            {
                Include(label.Box.Offset(offset.X, offset.Y));
            }
        }

        /// <summary>
        ///     Counts a box.
        /// </summary>
        /// <param name="box">The box, or null.</param>
        public void Include(SceneRect? box)
        {
            if (box is { } b && double.IsFinite(b.X) && double.IsFinite(b.Y) && double.IsFinite(b.Width) && double.IsFinite(b.Height))
            {
                _bounds = _bounds is { } current ? current.Union(b) : b;
            }
        }

        /// <summary>
        ///     Finishes the node.
        /// </summary>
        /// <param name="intersect">The intersect function.</param>
        /// <returns>The drawn node.</returns>
        public ShapeResult Finish(ShapeIntersect intersect) =>
            new(_input.Handler, _bounds ?? new SceneRect(0, 0, 0, 0), _items, Label, LabelOffset, intersect);

        /// <summary>
        ///     Translates segments.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <param name="dx">The x offset.</param>
        /// <param name="dy">The y offset.</param>
        /// <returns>The translated segments.</returns>
        private static List<SceneSegment> Translate(IReadOnlyList<SceneSegment> segments, double dx, double dy) =>
            [.. segments.Select(s => s.Kind switch
            {
                SceneSegmentKind.Close => s,
                SceneSegmentKind.ArcTo => s with { X = s.X + dx, Y = s.Y + dy },
                _ => s with { X = s.X + dx, Y = s.Y + dy, X1 = s.X1 + dx, Y1 = s.Y1 + dy, X2 = s.X2 + dx, Y2 = s.Y2 + dy },
            })];
    }

    /// <summary>
    ///     Gets the exact box of path segments (SVG's <c>getBBox</c>): end points, arc extremes and curve extremes.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <returns>The box, or null for no geometry or geometry that is not finite.</returns>
    internal static SceneRect? SegmentBounds(IReadOnlyList<SceneSegment> segments)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        void Add(double x, double y)
        {
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        double cx = 0, cy = 0, sx = 0, sy = 0;
        foreach (var s in segments)
        {
            switch (s.Kind)
            {
                case SceneSegmentKind.MoveTo:
                    (cx, cy, sx, sy) = (s.X, s.Y, s.X, s.Y);
                    continue;
                case SceneSegmentKind.Close:
                    (cx, cy) = (sx, sy);
                    continue;
                case SceneSegmentKind.ArcTo:
                    foreach (var (x, y) in ArcExtremes(cx, cy, s))
                    {
                        Add(x, y);
                    }

                    break;
                case SceneSegmentKind.QuadraticTo:
                    foreach (var t in QuadraticRoots(cx, s.X1, s.X).Concat(QuadraticRoots(cy, s.Y1, s.Y)))
                    {
                        var u = 1 - t;
                        Add((u * u * cx) + (2 * u * t * s.X1) + (t * t * s.X), (u * u * cy) + (2 * u * t * s.Y1) + (t * t * s.Y));
                    }

                    break;
                case SceneSegmentKind.CubicTo:
                    foreach (var t in CubicRoots(cx, s.X1, s.X2, s.X).Concat(CubicRoots(cy, s.Y1, s.Y2, s.Y)))
                    {
                        var u = 1 - t;
                        Add(
                            (u * u * u * cx) + (3 * u * u * t * s.X1) + (3 * u * t * t * s.X2) + (t * t * t * s.X),
                            (u * u * u * cy) + (3 * u * u * t * s.Y1) + (3 * u * t * t * s.Y2) + (t * t * t * s.Y));
                    }

                    break;
            }

            // Every drawing segment touches its start and end points
            Add(cx, cy);
            Add(s.X, s.Y);
            (cx, cy) = (s.X, s.Y);
        }

        if (!double.IsFinite(minX) || !double.IsFinite(minY) || !double.IsFinite(maxX) || !double.IsFinite(maxY))
        {
            return null;
        }

        return new SceneRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    ///     Gets the points of an SVG arc where x or y is extreme, by the endpoint-to-center conversion of SVG
    ///     (F.6.5 and F.6.6, with the radii scaled up when too small).
    /// </summary>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="arc">The arc segment.</param>
    /// <returns>The extreme points within the arc.</returns>
    private static IEnumerable<(double X, double Y)> ArcExtremes(double x1, double y1, SceneSegment arc)
    {
        var (x2, y2) = (arc.X, arc.Y);
        var rx = Math.Abs(arc.X1);
        var ry = Math.Abs(arc.Y1);
        if (rx == 0 || ry == 0 || (Same(x1, x2) && Same(y1, y2)))
        {
            yield break;
        }

        var phi = arc.X2 * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var dx2 = (x1 - x2) / 2;
        var dy2 = (y1 - y2) / 2;
        var x1p = (cos * dx2) + (sin * dy2);
        var y1p = (-sin * dx2) + (cos * dy2);
        var lambda = (x1p * x1p / (rx * rx)) + (y1p * y1p / (ry * ry));
        if (lambda > 1)
        {
            var root = Math.Sqrt(lambda);
            rx *= root;
            ry *= root;
        }

        var numerator = (rx * rx * ry * ry) - (rx * rx * y1p * y1p) - (ry * ry * x1p * x1p);
        var denominator = (rx * rx * y1p * y1p) + (ry * ry * x1p * x1p);
        var coefficient = Math.Sqrt(Math.Max(0, numerator / denominator)) * (arc.LargeArc == arc.Sweep ? -1 : 1);
        var cxp = coefficient * rx * y1p / ry;
        var cyp = coefficient * -ry * x1p / rx;
        var cx = (cos * cxp) - (sin * cyp) + ((x1 + x2) / 2);
        var cy = (sin * cxp) + (cos * cyp) + ((y1 + y2) / 2);
        var theta1 = Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
        var theta2 = Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
        var delta = theta2 - theta1;
        if (arc.Sweep && delta < 0)
        {
            delta += 2 * Math.PI;
        }
        else if (!arc.Sweep && delta > 0)
        {
            delta -= 2 * Math.PI;
        }

        // The angles where dx/dθ or dy/dθ is zero, and each half turn after them
        var thetaX = Math.Atan2(-ry * sin, rx * cos);
        var thetaY = Math.Atan2(ry * cos, rx * sin);
        foreach (var start in new[] { thetaX, thetaY })
        {
            for (var k = -2; k <= 2; k++)
            {
                var theta = start + (k * Math.PI);
                var t = delta > 0 ? Normalize(theta - theta1) : Normalize(theta1 - theta);
                if (t <= Math.Abs(delta))
                {
                    yield return (
                        cx + (rx * cos * Math.Cos(theta)) - (ry * sin * Math.Sin(theta)),
                        cy + (rx * sin * Math.Cos(theta)) + (ry * cos * Math.Sin(theta)));
                }
            }
        }
    }

    /// <summary>Gets whether two numbers are the same number, as JavaScript's <c>==</c> on numbers (NaN is never the same).</summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>Whether they are equal.</returns>
    private static bool Same(double a, double b) => !double.IsNaN(a) && !double.IsNaN(b) && a.CompareTo(b) == 0;

    /// <summary>Normalizes an angle to [0, 2π).</summary>
    /// <param name="angle">The angle.</param>
    /// <returns>The angle.</returns>
    private static double Normalize(double angle)
    {
        var turn = 2 * Math.PI;
        var a = angle % turn;
        return a < 0 ? a + turn : a;
    }

    /// <summary>Gets the parameters in (0, 1) where a quadratic Bezier coordinate is extreme.</summary>
    /// <param name="p0">The start.</param>
    /// <param name="p1">The control.</param>
    /// <param name="p2">The end.</param>
    /// <returns>The parameters.</returns>
    private static IEnumerable<double> QuadraticRoots(double p0, double p1, double p2)
    {
        var denominator = p0 - (2 * p1) + p2;
        if (denominator != 0)
        {
            var t = (p0 - p1) / denominator;
            if (t is > 0 and < 1)
            {
                yield return t;
            }
        }
    }

    /// <summary>Gets the parameters in (0, 1) where a cubic Bezier coordinate is extreme.</summary>
    /// <param name="p0">The start.</param>
    /// <param name="p1">The first control.</param>
    /// <param name="p2">The second control.</param>
    /// <param name="p3">The end.</param>
    /// <returns>The parameters.</returns>
    private static IEnumerable<double> CubicRoots(double p0, double p1, double p2, double p3)
    {
        // The derivative: a t² + b t + c
        var a = 3 * (-p0 + (3 * p1) - (3 * p2) + p3);
        var b = 6 * (p0 - (2 * p1) + p2);
        var c = 3 * (p1 - p0);
        var roots = new List<double>();
        if (Math.Abs(a) < 1e-12)
        {
            if (Math.Abs(b) > 1e-12)
            {
                roots.Add(-c / b);
            }
        }
        else
        {
            var discriminant = (b * b) - (4 * a * c);
            if (discriminant >= 0)
            {
                var root = Math.Sqrt(discriminant);
                roots.Add((-b + root) / (2 * a));
                roots.Add((-b - root) / (2 * a));
            }
        }

        return roots.Where(t => t is > 0 and < 1);
    }

    /// <summary>
    ///     Gets Mermaid's <c>generateCirclePoints</c> (the points are mirrored through the origin).
    /// </summary>
    /// <param name="centerX">The center x.</param>
    /// <param name="centerY">The center y.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="numPoints">The number of points.</param>
    /// <param name="startAngle">The start angle in degrees.</param>
    /// <param name="endAngle">The end angle in degrees.</param>
    /// <param name="mirror">Whether the points are mirrored (every shape but the right curly brace).</param>
    /// <returns>The points.</returns>
    private static List<ShapePoint> CirclePoints(double centerX, double centerY, double radius, int numPoints, double startAngle, double endAngle, bool mirror = true)
    {
        var points = new List<ShapePoint>(numPoints);
        var startRad = startAngle * Math.PI / 180;
        var endRad = endAngle * Math.PI / 180;
        var step = (endRad - startRad) / (numPoints - 1);
        for (var i = 0; i < numPoints; i++)
        {
            var angle = startRad + (i * step);
            var x = centerX + (radius * Math.Cos(angle));
            var y = centerY + (radius * Math.Sin(angle));
            points.Add(mirror ? new ShapePoint(-x, -y) : new ShapePoint(x, y));
        }

        return points;
    }

    /// <summary>
    ///     Gets Mermaid's <c>generateFullSineWavePoints</c>: 51 points of a sine wave between two points.
    /// </summary>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="x2">The end x.</param>
    /// <param name="y2">The end y.</param>
    /// <param name="amplitude">The amplitude.</param>
    /// <param name="numCycles">The number of cycles.</param>
    /// <returns>The points.</returns>
    private static List<ShapePoint> SineWavePoints(double x1, double y1, double x2, double y2, double amplitude, double numCycles)
    {
        const int steps = 50;
        var points = new List<ShapePoint>(steps + 1);
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        var cycleLength = deltaX / numCycles;
        var frequency = 2 * Math.PI / cycleLength;
        var midY = y1 + (deltaY / 2);
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var x = x1 + (t * deltaX);
            var y = midY + (amplitude * Math.Sin(frequency * (x - x1)));
            points.Add(new ShapePoint(x, y));
        }

        return points;
    }
}
