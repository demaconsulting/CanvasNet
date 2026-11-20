using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The geometry of the links of the shared engine: the curve through a route, the clipping of a route to the
///     outline of an entity or the border of a cluster, the shortening of a route by its decorations, and the shapes of
///     the decorations (<c>SvekGeometry.Extremities.cs</c>).
/// </summary>
/// <remarks>
///     <para>
///         Graphviz draws the edges of PlantUML's diagrams as cubic B-splines; dagre gives a route of points (the ends
///         on the node borders and one point per rank crossed). <see cref="Spline"/> draws the uniform cubic B-spline
///         clamped at both ends whose control points are the route, converted to cubic Bezier segments by inserting
///         every interior knot until it is triple (Boehm's knot insertion); the curve starts and ends at the ends of the
///         route and leaves them along the first and last legs of the route. The helper is written from the
///         mathematics of B-splines; no curve code of PlantUML, Graphviz, or d3 is ported.
///     </para>
///     <para>
///         The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static partial class SvekGeometry
{
    /// <summary>The degree of the spline.</summary>
    private const int Degree = 3;

    /// <summary>
    ///     Draws the curve of a route: a straight line for two points, a quadratic curve (as one cubic) for three, and
    ///     the clamped uniform cubic B-spline of the points for more.
    /// </summary>
    /// <param name="points">The route; at least two points.</param>
    /// <returns>The segments: a move to the first point, then lines or cubic curves ending at the last point.</returns>
    /// <exception cref="ArgumentException">Thrown when the route has fewer than two points.</exception>
    public static IReadOnlyList<SceneSegment> Spline(IReadOnlyList<SvekPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("A route needs at least two points.", nameof(points));
        }

        var segments = new List<SceneSegment> { SceneSegment.Move(points[0].X, points[0].Y) };
        if (points.Count == 2)
        {
            segments.Add(SceneSegment.Line(points[1].X, points[1].Y));
            return segments;
        }

        if (points.Count == 3)
        {
            // The clamped quadratic B-spline of three points is the quadratic Bezier curve, raised to a cubic
            var (p0, p1, p2) = (points[0], points[1], points[2]);
            segments.Add(SceneSegment.Cubic(
                p0.X + (2.0 / 3 * (p1.X - p0.X)),
                p0.Y + (2.0 / 3 * (p1.Y - p0.Y)),
                p2.X + (2.0 / 3 * (p1.X - p2.X)),
                p2.Y + (2.0 / 3 * (p1.Y - p2.Y)),
                p2.X,
                p2.Y));
            return segments;
        }

        var bezier = BezierPoints(points);
        for (var k = 1; k + 2 < bezier.Count; k += 3)
        {
            segments.Add(SceneSegment.Cubic(bezier[k].X, bezier[k].Y, bezier[k + 1].X, bezier[k + 1].Y, bezier[k + 2].X, bezier[k + 2].Y));
        }

        return segments;
    }

    /// <summary>
    ///     Finds where the segment from the center of an outline toward a point leaves the outline.
    /// </summary>
    /// <remarks>
    ///     The point found is on the outline in the direction of the point, whether the point is inside or outside
    ///     (dagre's <c>intersectRect</c> for a rectangle); a point at the center gives the center.
    /// </remarks>
    /// <param name="shape">The kind of outline.</param>
    /// <param name="box">The box of the outline.</param>
    /// <param name="toward">The point.</param>
    /// <returns>The point on the outline.</returns>
    public static SvekPoint Clip(ClipShape shape, SceneRect box, SvekPoint toward)
    {
        var (cx, cy) = (box.X + (box.Width / 2), box.Y + (box.Height / 2));
        var (dx, dy) = (toward.X - cx, toward.Y - cy);
        var (rx, ry) = (box.Width / 2, box.Height / 2);
        if ((dx == 0 && dy == 0) || rx <= 0 || ry <= 0)
        {
            return new SvekPoint(cx, cy);
        }

        double t;
        switch (shape)
        {
            case ClipShape.Ellipse:
                t = 1 / Math.Sqrt((dx * dx / (rx * rx)) + (dy * dy / (ry * ry)));
                break;
            case ClipShape.Diamond:
                t = 1 / ((Math.Abs(dx) / rx) + (Math.Abs(dy) / ry));
                break;
            default:
                // The side the direction leaves through: the one whose ratio is the larger
                t = Math.Abs(dy) * rx > Math.Abs(dx) * ry ? ry / Math.Abs(dy) : rx / Math.Abs(dx);
                break;
        }

        return new SvekPoint(cx + (t * dx), cy + (t * dy));
    }

    /// <summary>
    ///     Decides whether a point is inside an outline (the border included).
    /// </summary>
    /// <param name="shape">The kind of outline.</param>
    /// <param name="box">The box of the outline.</param>
    /// <param name="point">The point.</param>
    /// <returns>True when it is inside.</returns>
    public static bool Contains(ClipShape shape, SceneRect box, SvekPoint point)
    {
        var (rx, ry) = (box.Width / 2, box.Height / 2);
        var (dx, dy) = (point.X - box.X - rx, point.Y - box.Y - ry);
        if (rx <= 0 || ry <= 0)
        {
            return false;
        }

        return shape switch
        {
            ClipShape.Ellipse => (dx * dx / (rx * rx)) + (dy * dy / (ry * ry)) <= 1 + 1e-9,
            ClipShape.Diamond => (Math.Abs(dx) / rx) + (Math.Abs(dy) / ry) <= 1 + 1e-9,
            _ => Math.Abs(dx) <= rx + 1e-9 && Math.Abs(dy) <= ry + 1e-9,
        };
    }

    /// <summary>
    ///     Cuts a route where it first enters a box, walking from one end: the part inside the box is dropped and the
    ///     route ends on the border (a link to a cluster ends at the cluster's border).
    /// </summary>
    /// <param name="route">The route; at least two points.</param>
    /// <param name="box">The box.</param>
    /// <param name="atEnd">True to cut the end of the route (walking from its start), false to cut its start.</param>
    /// <returns>The cut route, or the route itself when it never enters the box from outside.</returns>
    public static IReadOnlyList<SvekPoint> CutAtBox(IReadOnlyList<SvekPoint> route, SceneRect box, bool atEnd)
    {
        ArgumentNullException.ThrowIfNull(route);
        var points = atEnd ? route.ToList() : route.Reverse().ToList();
        for (var k = 0; k + 1 < points.Count; k++)
        {
            var (a, b) = (points[k], points[k + 1]);
            if (Contains(ClipShape.Rectangle, box, a) || !Contains(ClipShape.Rectangle, box, b))
            {
                continue;
            }

            // The segment enters the box: keep the route up to the border
            var cut = points.Take(k + 1).ToList();
            cut.Add(EnterRect(a, b, box));
            if (!atEnd)
            {
                cut.Reverse();
            }

            return cut;
        }

        return route;
    }

    /// <summary>
    ///     Moves the ends of a route back along its first and last legs, so the line stops where a decoration begins.
    /// </summary>
    /// <param name="route">The route; at least two points.</param>
    /// <param name="atStart">How far to move the start; not negative.</param>
    /// <param name="atEnd">How far to move the end; not negative.</param>
    /// <returns>The shortened route; an end never moves past the next point of the route.</returns>
    public static IReadOnlyList<SvekPoint> Shorten(IReadOnlyList<SvekPoint> route, double atStart, double atEnd)
    {
        ArgumentNullException.ThrowIfNull(route);
        var points = route.ToList();
        if (points.Count < 2)
        {
            return points;
        }

        points[0] = MoveToward(points[0], points[1], atStart);
        points[^1] = MoveToward(points[^1], points[^2], atEnd);
        return points;
    }

    /// <summary>
    ///     Gets the direction in which a route arrives at its end (from its second last point to its last point).
    /// </summary>
    /// <param name="route">The route; at least two points.</param>
    /// <param name="atEnd">True for the end, false for the start (then the direction from the second point to the first).</param>
    /// <returns>The angle in radians (y down), or zero for a route whose end legs have no length.</returns>
    public static double ArrivalAngle(IReadOnlyList<SvekPoint> route, bool atEnd)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.Count < 2)
        {
            return 0;
        }

        // The last leg with a length decides the direction
        if (atEnd)
        {
            var tip = route[^1];
            for (var k = route.Count - 2; k >= 0; k--)
            {
                if (route[k] != tip)
                {
                    return Math.Atan2(tip.Y - route[k].Y, tip.X - route[k].X);
                }
            }
        }
        else
        {
            var tip = route[0];
            for (var k = 1; k < route.Count; k++)
            {
                if (route[k] != tip)
                {
                    return Math.Atan2(tip.Y - route[k].Y, tip.X - route[k].X);
                }
            }
        }

        return 0;
    }

    /// <summary>
    ///     Builds an arc of a circle as cubic segments (each at most a quarter circle).
    /// </summary>
    /// <param name="cx">The center x.</param>
    /// <param name="cy">The center y.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="start">The start angle in radians (y down).</param>
    /// <param name="sweep">The sweep in radians; positive turns clockwise on the screen.</param>
    /// <returns>The open path: a move to the start, then the cubic segments.</returns>
    public static IReadOnlyList<SceneSegment> Arc(double cx, double cy, double radius, double start, double sweep)
    {
        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) - 1e-9));
        var step = sweep / pieces;
        var k = 4.0 / 3 * Math.Tan(step / 4);
        var segments = new List<SceneSegment> { SceneSegment.Move(cx + (radius * Math.Cos(start)), cy + (radius * Math.Sin(start))) };
        for (var p = 0; p < pieces; p++)
        {
            var a0 = start + (p * step);
            var a1 = a0 + step;
            var (x0, y0) = (Math.Cos(a0), Math.Sin(a0));
            var (x1, y1) = (Math.Cos(a1), Math.Sin(a1));
            segments.Add(SceneSegment.Cubic(
                cx + (radius * (x0 - (k * y0))),
                cy + (radius * (y0 + (k * x0))),
                cx + (radius * (x1 + (k * y1))),
                cy + (radius * (y1 - (k * x1))),
                cx + (radius * x1),
                cy + (radius * y1)));
        }

        return segments;
    }

    /// <summary>
    ///     Builds a full circle.
    /// </summary>
    /// <param name="cx">The center x.</param>
    /// <param name="cy">The center y.</param>
    /// <param name="radius">The radius.</param>
    /// <returns>The closed outline.</returns>
    public static IReadOnlyList<SceneSegment> Circle(double cx, double cy, double radius) => SceneShapes.Ellipse(cx, cy, radius, radius);

    /// <summary>
    ///     Computes the Bezier control points of the clamped uniform cubic B-spline of at least four points.
    /// </summary>
    /// <param name="control">The control points.</param>
    /// <returns>The points of the Bezier segments: the first point, then three per segment.</returns>
    private static List<SvekPoint> BezierPoints(IReadOnlyList<SvekPoint> control)
    {
        // The clamped uniform knot vector: four zeros, the interior knots 1 .. m-1, four times m
        var segments = control.Count - Degree;
        var knots = new List<double>();
        knots.AddRange(Enumerable.Repeat(0.0, Degree + 1));
        for (var u = 1; u < segments; u++)
        {
            knots.Add(u);
        }

        knots.AddRange(Enumerable.Repeat((double)segments, Degree + 1));
        var points = control.ToList();

        // Boehm's insertion: raise every interior knot to the multiplicity of the degree
        for (var u = 1; u < segments; u++)
        {
            for (var times = 1; times < Degree; times++)
            {
                InsertKnot(knots, points, u);
            }
        }

        return points;
    }

    /// <summary>
    ///     Inserts one knot into a cubic B-spline (Boehm's algorithm), changing its control points but not its curve.
    /// </summary>
    /// <param name="knots">The knot vector; the knot is inserted.</param>
    /// <param name="points">The control points; one point is added.</param>
    /// <param name="u">The knot.</param>
    private static void InsertKnot(List<double> knots, List<SvekPoint> points, double u)
    {
        // The span the knot falls in: knots[span] <= u < knots[span + 1]
        var span = Degree;
        while (span + 1 < knots.Count && knots[span + 1] <= u)
        {
            span++;
        }

        var inserted = new List<SvekPoint>(points.Count + 1);
        for (var i = 0; i <= points.Count; i++)
        {
            if (i <= span - Degree)
            {
                inserted.Add(points[i]);
            }
            else if (i > span)
            {
                inserted.Add(points[i - 1]);
            }
            else
            {
                var a = (u - knots[i]) / (knots[i + Degree] - knots[i]);
                inserted.Add(new SvekPoint(((1 - a) * points[i - 1].X) + (a * points[i].X), ((1 - a) * points[i - 1].Y) + (a * points[i].Y)));
            }
        }

        knots.Insert(span + 1, u);
        points.Clear();
        points.AddRange(inserted);
    }

    /// <summary>
    ///     Finds where a segment from outside a rectangle to inside it crosses the border.
    /// </summary>
    /// <param name="outside">The point outside.</param>
    /// <param name="inside">The point inside.</param>
    /// <param name="box">The rectangle.</param>
    /// <returns>The crossing.</returns>
    private static SvekPoint EnterRect(SvekPoint outside, SvekPoint inside, SceneRect box)
    {
        // The largest parameter at which the segment is still outside, over the four sides
        var (dx, dy) = (inside.X - outside.X, inside.Y - outside.Y);
        var t = 0.0;
        if (outside.X < box.X && dx != 0)
        {
            t = Math.Max(t, (box.X - outside.X) / dx);
        }

        if (outside.X > box.Right && dx != 0)
        {
            t = Math.Max(t, (box.Right - outside.X) / dx);
        }

        if (outside.Y < box.Y && dy != 0)
        {
            t = Math.Max(t, (box.Y - outside.Y) / dy);
        }

        if (outside.Y > box.Bottom && dy != 0)
        {
            t = Math.Max(t, (box.Bottom - outside.Y) / dy);
        }

        t = Math.Clamp(t, 0, 1);
        return new SvekPoint(outside.X + (t * dx), outside.Y + (t * dy));
    }

    /// <summary>
    ///     Moves a point toward another by a distance, at most up to the other point.
    /// </summary>
    /// <param name="from">The point.</param>
    /// <param name="to">The point to move toward.</param>
    /// <param name="distance">The distance; not negative.</param>
    /// <returns>The moved point.</returns>
    private static SvekPoint MoveToward(SvekPoint from, SvekPoint to, double distance)
    {
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length == 0 || distance <= 0)
        {
            return from;
        }

        var t = Math.Min(1, distance / length);
        return new SvekPoint(from.X + (t * dx), from.Y + (t * dy));
    }
}
