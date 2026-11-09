// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     The paint of an edge marker: the fill and the stroke of its shape, after the style sheet's <c>.marker</c> rule and
///     the edge's own stroke color (Mermaid's colored marker copies).
/// </summary>
/// <param name="Fill">The fill, or null for none.</param>
/// <param name="Stroke">The stroke color, or null for none.</param>
internal sealed record MarkerPaint(Rgba32? Fill, Rgba32? Stroke);

/// <content>
///     The flowchart's edge markers (<c>markers.js</c>: <c>point</c>, <c>circle</c> and <c>cross</c>, each with its start
///     variant), placed as SVG places a marker: its <c>refX</c>/<c>refY</c> on the path's end vertex, scaled from its
///     view box to its <c>markerWidth</c> in user units, and turned along the path's direction there
///     (<c>orient="auto"</c>).
/// </content>
internal static partial class EdgeBuilder
{
    /// <summary>
    ///     The geometry of a marker of <c>markers.js</c>: its view box size, its size in user units, its reference point
    ///     and its stroke width, in view box units.
    /// </summary>
    /// <param name="Shape">The shape: <c>point</c>, <c>circle</c> or <c>cross</c>.</param>
    /// <param name="ViewBox">The side of the square view box.</param>
    /// <param name="Size">The side of the marker in user units (<c>markerWidth</c> and <c>markerHeight</c>).</param>
    /// <param name="RefX">The x of the reference point.</param>
    /// <param name="RefY">The y of the reference point.</param>
    /// <param name="StrokeWidth">The stroke width of the shape.</param>
    /// <param name="Start">Whether the marker is the start variant (its shape points back along the path).</param>
    internal sealed record MarkerGeometry(string Shape, double ViewBox, double Size, double RefX, double RefY, double StrokeWidth, bool Start)
    {
        /// <summary>Gets the scale from view box units to user units.</summary>
        public double Scale => Size / ViewBox;
    }

    /// <summary>
    ///     Gets the marker of an arrow type at one end of an edge (<c>edgeMarker.ts</c>'s <c>arrowTypesMap</c> with the
    ///     markers <c>markers.js</c> defines for a flowchart), or null when there is none.
    /// </summary>
    /// <param name="arrowType">The arrow type (<c>arrow_point</c>, <c>arrow_circle</c>, <c>arrow_cross</c>, <c>none</c>).</param>
    /// <param name="start">Whether the marker is at the start of the path.</param>
    /// <returns>The geometry, or null.</returns>
    internal static MarkerGeometry? MarkerOf(string? arrowType, bool start) => (arrowType, start) switch
    {
        ("arrow_point", false) => new MarkerGeometry("point", 10, 8, 5, 5, 1, false),
        ("arrow_point", true) => new MarkerGeometry("point", 10, 8, 4.5, 5, 1, true),
        ("arrow_circle", false) => new MarkerGeometry("circle", 10, 11, 11, 5, 1, false),
        ("arrow_circle", true) => new MarkerGeometry("circle", 10, 11, -1, 5, 1, true),
        ("arrow_cross", false) => new MarkerGeometry("cross", 11, 11, 12, 5.2, 2, false),
        ("arrow_cross", true) => new MarkerGeometry("cross", 11, 11, -1, 5.2, 2, true),
        _ => null,
    };

    /// <summary>
    ///     Draws the markers of an edge path.
    /// </summary>
    /// <param name="segments">The path.</param>
    /// <param name="arrowTypeStart">The arrow type at the start.</param>
    /// <param name="arrowTypeEnd">The arrow type at the end.</param>
    /// <param name="paintOf">The paint of a marker shape.</param>
    /// <param name="id">The edge id.</param>
    /// <returns>The marker items (start first).</returns>
    internal static List<SceneItem> DrawMarkers(
        IReadOnlyList<SceneSegment> segments,
        string? arrowTypeStart,
        string? arrowTypeEnd,
        Func<string, MarkerPaint> paintOf,
        string id)
    {
        var items = new List<SceneItem>();
        if (segments.Count == 0)
        {
            return items;
        }

        if (MarkerOf(arrowTypeStart, true) is { } startMarker && StartOf(segments) is { } start)
        {
            items.AddRange(DrawMarker(startMarker, start.X, start.Y, start.Angle, paintOf(startMarker.Shape), id));
        }

        if (MarkerOf(arrowTypeEnd, false) is { } endMarker && EndOf(segments) is { } end)
        {
            items.AddRange(DrawMarker(endMarker, end.X, end.Y, end.Angle, paintOf(endMarker.Shape), id));
        }

        return items;
    }

    /// <summary>
    ///     Draws one marker at a vertex.
    /// </summary>
    /// <param name="marker">The marker.</param>
    /// <param name="x">The vertex x.</param>
    /// <param name="y">The vertex y.</param>
    /// <param name="angle">The direction of the path at the vertex, in radians.</param>
    /// <param name="paint">The paint.</param>
    /// <param name="id">The edge id.</param>
    /// <returns>The items.</returns>
    internal static List<SceneItem> DrawMarker(MarkerGeometry marker, double x, double y, double angle, MarkerPaint paint, string id)
    {
        var (cos, sin) = (Math.Cos(angle), Math.Sin(angle));
        var scale = marker.Scale;
        ShapePoint Map(double mx, double my)
        {
            var (ux, uy) = ((mx - marker.RefX) * scale, (my - marker.RefY) * scale);
            return new ShapePoint(x + (ux * cos) - (uy * sin), y + (ux * sin) + (uy * cos));
        }

        var stroke = paint.Stroke is { } color ? new SceneStroke(color, marker.StrokeWidth * scale) : null;
        var items = new List<SceneItem>();
        switch (marker.Shape)
        {
            case "point":
                {
                    ShapePoint[] triangle = !marker.Start
                        ? [new(0, 0), new(10, 5), new(0, 10)]
                        : [new(0, 5), new(10, 10), new(10, 0)];
                    if (paint.Fill is { } fill && fill == paint.Stroke && fill.A == 255)
                    {
                        // The stroked triangle clipped to the marker's view box, as one exact outline
                        var outline = ClipToBox(OffsetPolygon(triangle, marker.StrokeWidth / 2), marker.ViewBox);
                        items.Add(new ScenePath(Polygon(outline.Select(p => Map(p.X, p.Y)))) { Role = "marker", Id = id, Fill = fill });
                    }
                    else
                    {
                        items.Add(new ScenePath(Polygon(triangle.Select(p => Map(p.X, p.Y))))
                        {
                            Role = "marker",
                            Id = id,
                            Fill = paint.Fill,
                            Stroke = stroke,
                        });
                    }

                    break;
                }

            case "circle":
                {
                    var center = Map(5, 5);
                    var r = 5 * scale;
                    List<SceneSegment> circle =
                    [
                        SceneSegment.Move(center.X + r, center.Y),
                    SceneSegment.Arc(r, r, 0, false, true, center.X - r, center.Y),
                    SceneSegment.Arc(r, r, 0, false, true, center.X + r, center.Y),
                    SceneSegment.Close(),
                ];
                    items.Add(new ScenePath(circle) { Role = "marker", Id = id, Fill = paint.Fill, Stroke = stroke });
                    break;
                }

            default:
                {
                    var a = Map(1, 1);
                    var b = Map(10, 10);
                    var c = Map(10, 1);
                    var d = Map(1, 10);
                    List<SceneSegment> cross =
                    [
                        SceneSegment.Move(a.X, a.Y), SceneSegment.Line(b.X, b.Y),
                    SceneSegment.Move(c.X, c.Y), SceneSegment.Line(d.X, d.Y),
                ];
                    items.Add(new ScenePath(cross) { Role = "marker", Id = id, Stroke = stroke });
                    break;
                }
        }

        return items;
    }

    /// <summary>
    ///     Gets the start vertex of a path and the direction there (SVG's <c>orient="auto"</c> for <c>marker-start</c>).
    /// </summary>
    /// <param name="segments">The path.</param>
    /// <returns>The vertex and the angle, or null for an empty path.</returns>
    internal static (double X, double Y, double Angle)? StartOf(IReadOnlyList<SceneSegment> segments)
    {
        if (segments.Count == 0 || segments[0].Kind != SceneSegmentKind.MoveTo)
        {
            return null;
        }

        var (x, y) = (segments[0].X, segments[0].Y);
        for (var i = 1; i < segments.Count; i++)
        {
            var s = segments[i];
            if (s.Kind is SceneSegmentKind.MoveTo or SceneSegmentKind.Close)
            {
                break;
            }

            // The first control point that differs from the start gives the direction
            foreach (var (px, py) in Controls(s))
            {
                if (!DagreLayout.Same(px, x) || !DagreLayout.Same(py, y))
                {
                    return (x, y, Math.Atan2(py - y, px - x));
                }
            }
        }

        return (x, y, 0);
    }

    /// <summary>
    ///     Gets the end vertex of a path and the direction there (SVG's <c>orient="auto"</c> for <c>marker-end</c>).
    /// </summary>
    /// <param name="segments">The path.</param>
    /// <returns>The vertex and the angle, or null for an empty path.</returns>
    internal static (double X, double Y, double Angle)? EndOf(IReadOnlyList<SceneSegment> segments)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        // The current point after each segment
        var points = new List<(double X, double Y)>();
        double cx = 0, cy = 0, sx = 0, sy = 0;
        foreach (var s in segments)
        {
            if (s.Kind == SceneSegmentKind.MoveTo)
            {
                (cx, cy, sx, sy) = (s.X, s.Y, s.X, s.Y);
            }
            else if (s.Kind == SceneSegmentKind.Close)
            {
                (cx, cy) = (sx, sy);
            }
            else
            {
                (cx, cy) = (s.X, s.Y);
            }

            points.Add((cx, cy));
        }

        var (ex, ey) = points[^1];
        for (var i = segments.Count - 1; i > 0; i--)
        {
            var s = segments[i];
            if (s.Kind is SceneSegmentKind.MoveTo or SceneSegmentKind.Close)
            {
                break;
            }

            // The last control point (or the segment start) that differs from the end gives the direction
            var (px0, py0) = points[i - 1];
            foreach (var (px, py) in Controls(s).Reverse().Append((px0, py0)))
            {
                if (!DagreLayout.Same(px, ex) || !DagreLayout.Same(py, ey))
                {
                    return (ex, ey, Math.Atan2(ey - py, ex - px));
                }
            }
        }

        return (ex, ey, 0);
    }

    /// <summary>
    ///     Gets the control points and the end point of a drawing segment, in path order.
    /// </summary>
    /// <param name="s">The segment.</param>
    /// <returns>The points.</returns>
    private static IEnumerable<(double X, double Y)> Controls(SceneSegment s) => s.Kind switch
    {
        SceneSegmentKind.CubicTo => [(s.X1, s.Y1), (s.X2, s.Y2), (s.X, s.Y)],
        SceneSegmentKind.QuadraticTo => [(s.X1, s.Y1), (s.X, s.Y)],
        _ => [(s.X, s.Y)],
    };

    /// <summary>
    ///     Makes a closed polygon path.
    /// </summary>
    /// <param name="points">The corners.</param>
    /// <returns>The segments.</returns>
    private static List<SceneSegment> Polygon(IEnumerable<ShapePoint> points)
    {
        var segments = new List<SceneSegment>();
        foreach (var p in points)
        {
            segments.Add(segments.Count == 0 ? SceneSegment.Move(p.X, p.Y) : SceneSegment.Line(p.X, p.Y));
        }

        if (segments.Count > 0)
        {
            segments.Add(SceneSegment.Close());
        }

        return segments;
    }

    /// <summary>
    ///     Grows a convex polygon by a distance with mitered corners: the outline of the polygon stroked with twice the
    ///     distance (every corner of the markers is within SVG's miter limit of 4).
    /// </summary>
    /// <param name="polygon">The corners, in either winding.</param>
    /// <param name="distance">The distance.</param>
    /// <returns>The grown polygon.</returns>
    internal static List<ShapePoint> OffsetPolygon(IReadOnlyList<ShapePoint> polygon, double distance)
    {
        var n = polygon.Count;
        var area = 0.0;
        for (var i = 0; i < n; i++)
        {
            var (a, b) = (polygon[i], polygon[(i + 1) % n]);
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        // The outward normal of an edge (dx, dy) is (dy, -dx) for a polygon whose shoelace sum is positive in y-down space
        var sign = area > 0 ? 1 : -1;
        var lines = new List<(ShapePoint P, ShapePoint D)>();
        for (var i = 0; i < n; i++)
        {
            var (a, b) = (polygon[i], polygon[(i + 1) % n]);
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            var (nx, ny) = (sign * dy / length * distance, -sign * dx / length * distance);
            lines.Add((new ShapePoint(a.X + nx, a.Y + ny), new ShapePoint(dx, dy)));
        }

        var result = new List<ShapePoint>();
        for (var i = 0; i < n; i++)
        {
            var (p1, d1) = lines[(i + n - 1) % n];
            var (p2, d2) = lines[i];
            var cross = (d1.X * d2.Y) - (d1.Y * d2.X);
            var t = (((p2.X - p1.X) * d2.Y) - ((p2.Y - p1.Y) * d2.X)) / cross;
            result.Add(new ShapePoint(p1.X + (t * d1.X), p1.Y + (t * d1.Y)));
        }

        return result;
    }

    /// <summary>
    ///     Clips a polygon to the square from 0 to a size (Sutherland-Hodgman): the marker's clip to its view box.
    /// </summary>
    /// <param name="polygon">The polygon.</param>
    /// <param name="size">The side of the square.</param>
    /// <returns>The clipped polygon.</returns>
    internal static List<ShapePoint> ClipToBox(IReadOnlyList<ShapePoint> polygon, double size)
    {
        var result = polygon.ToList();
        foreach (var (inside, cut) in new (Func<ShapePoint, bool>, Func<ShapePoint, ShapePoint, ShapePoint>)[]
        {
            (p => p.X >= 0, (a, b) => Lerp(a, b, (0 - a.X) / (b.X - a.X))),
            (p => p.X <= size, (a, b) => Lerp(a, b, (size - a.X) / (b.X - a.X))),
            (p => p.Y >= 0, (a, b) => Lerp(a, b, (0 - a.Y) / (b.Y - a.Y))),
            (p => p.Y <= size, (a, b) => Lerp(a, b, (size - a.Y) / (b.Y - a.Y))),
        })
        {
            var input = result;
            result = [];
            for (var i = 0; i < input.Count; i++)
            {
                var current = input[i];
                var previous = input[(i + input.Count - 1) % input.Count];
                if (inside(current))
                {
                    if (!inside(previous))
                    {
                        result.Add(cut(previous, current));
                    }

                    result.Add(current);
                }
                else if (inside(previous))
                {
                    result.Add(cut(previous, current));
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Interpolates between two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <param name="t">The fraction.</param>
    /// <returns>The point.</returns>
    private static ShapePoint Lerp(ShapePoint a, ShapePoint b, double t) => new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
}
