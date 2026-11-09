// Portions derived from mermaid (MIT) and dagre-d3 (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <content>
///     The intersect functions of <c>rendering-elements/intersect/</c> (borrowed by Mermaid from dagre-d3): rectangle,
///     ellipse, circle, line and polygon, with their exact arithmetic (the polygon's line intersection keeps the
///     rounding offset of the Graphics Gems algorithm it comes from).
/// </content>
internal static partial class ShapeLibrary
{
    /// <summary>
    ///     Intersects the line from a node's center to a point with the node's rectangle (<c>intersect-rect.js</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="point">The point.</param>
    /// <returns>The intersection.</returns>
    internal static ShapePoint IntersectRect(NodeBox node, ShapePoint point)
    {
        var x = node.X;
        var y = node.Y;
        var dx = point.X - x;
        var dy = point.Y - y;
        var w = node.Width / 2;
        var h = node.Height / 2;
        double sx;
        double sy;
        if (Math.Abs(dy) * w > Math.Abs(dx) * h)
        {
            // The top or the bottom side
            if (dy < 0)
            {
                h = -h;
            }

            sx = dy == 0 ? 0 : h * dx / dy;
            sy = h;
        }
        else
        {
            // The left or the right side
            if (dx < 0)
            {
                w = -w;
            }

            sx = w;
            sy = dx == 0 ? 0 : w * dy / dx;
        }

        return new ShapePoint(x + sx, y + sy);
    }

    /// <summary>
    ///     Intersects the line from a node's center to a point with an ellipse around the center
    ///     (<c>intersect-ellipse.js</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="rx">The x radius.</param>
    /// <param name="ry">The y radius.</param>
    /// <param name="point">The point.</param>
    /// <returns>The intersection.</returns>
    internal static ShapePoint IntersectEllipse(NodeBox node, double rx, double ry, ShapePoint point)
    {
        var cx = node.X;
        var cy = node.Y;
        var px = cx - point.X;
        var py = cy - point.Y;
        var det = Math.Sqrt((rx * rx * py * py) + (ry * ry * px * px));
        var dx = Math.Abs(rx * ry * px / det);
        if (point.X < cx)
        {
            dx = -dx;
        }

        var dy = Math.Abs(rx * ry * py / det);
        if (point.Y < cy)
        {
            dy = -dy;
        }

        return new ShapePoint(cx + dx, cy + dy);
    }

    /// <summary>
    ///     Intersects the line from a node's center to a point with a circle around the center
    ///     (<c>intersect-circle.js</c>).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="r">The radius.</param>
    /// <param name="point">The point.</param>
    /// <returns>The intersection.</returns>
    internal static ShapePoint IntersectCircle(NodeBox node, double r, ShapePoint point) => IntersectEllipse(node, r, r, point);

    /// <summary>
    ///     Intersects two line segments (<c>intersect-line.js</c>, after Graphics Gems II): the intersection is moved by half
    ///     the denominator for rounding, as the original integer algorithm does.
    /// </summary>
    /// <param name="p1">The start of the first segment.</param>
    /// <param name="p2">The end of the first segment.</param>
    /// <param name="q1">The start of the second segment.</param>
    /// <param name="q2">The end of the second segment.</param>
    /// <returns>The intersection, or null when the segments do not intersect or are collinear.</returns>
    internal static ShapePoint? IntersectLine(ShapePoint p1, ShapePoint p2, ShapePoint q1, ShapePoint q2)
    {
        // The line through p1 and p2: a1 x + b1 y + c1 = 0
        var a1 = p2.Y - p1.Y;
        var b1 = p1.X - p2.X;
        var c1 = (p2.X * p1.Y) - (p1.X * p2.Y);
        var r3 = (a1 * q1.X) + (b1 * q1.Y) + c1;
        var r4 = (a1 * q2.X) + (b1 * q2.Y) + c1;
        const double epsilon = 1e-6;
        if (r3 != 0 && r4 != 0 && r3 * r4 > 0)
        {
            return null;
        }

        // The line through q1 and q2
        var a2 = q2.Y - q1.Y;
        var b2 = q1.X - q2.X;
        var c2 = (q2.X * q1.Y) - (q1.X * q2.Y);
        var r1 = (a2 * p1.X) + (b2 * p1.Y) + c2;
        var r2 = (a2 * p2.X) + (b2 * p2.Y) + c2;
        if (Math.Abs(r1) < epsilon && Math.Abs(r2) < epsilon && r1 * r2 > 0)
        {
            return null;
        }

        var denom = (a1 * b2) - (a2 * b1);
        if (denom == 0)
        {
            return null;
        }

        var offset = Math.Abs(denom / 2);
        var num = (b1 * c2) - (b2 * c1);
        var x = num < 0 ? (num - offset) / denom : (num + offset) / denom;
        num = (a2 * c1) - (a1 * c2);
        var y = num < 0 ? (num - offset) / denom : (num + offset) / denom;
        return new ShapePoint(x, y);
    }

    /// <summary>
    ///     Intersects the line from a node's center to a point with a polygon placed in the node's box
    ///     (<c>intersect-polygon.js</c>): the polygon's smallest x and y are put at the box's top left, and of several
    ///     intersections the one nearest the point wins.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="polygon">The polygon's points.</param>
    /// <param name="point">The point.</param>
    /// <returns>The intersection, or the node's center when there is none.</returns>
    internal static ShapePoint IntersectPolygon(NodeBox node, IReadOnlyList<ShapePoint> polygon, ShapePoint point)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        foreach (var entry in polygon)
        {
            minX = Math.Min(minX, entry.X);
            minY = Math.Min(minY, entry.Y);
        }

        var left = node.X - (node.Width / 2) - minX;
        var top = node.Y - (node.Height / 2) - minY;
        var center = new ShapePoint(node.X, node.Y);
        var intersections = new List<ShapePoint>();
        for (var i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[i < polygon.Count - 1 ? i + 1 : 0];
            if (IntersectLine(center, point, new ShapePoint(left + p1.X, top + p1.Y), new ShapePoint(left + p2.X, top + p2.Y)) is { } hit)
            {
                intersections.Add(hit);
            }
        }

        if (intersections.Count == 0)
        {
            return center;
        }

        // The nearest to the point; a stable sort keeps the first of equals, as V8 does
        double Distance(ShapePoint p) => Math.Sqrt(((p.X - point.X) * (p.X - point.X)) + ((p.Y - point.Y) * (p.Y - point.Y)));
        return intersections.OrderBy(Distance).First();
    }
}
