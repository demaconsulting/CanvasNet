using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Converts a <see cref="Path"/> into one flattened polygon (a closed, ordered
///     <see cref="List{T}"/> of <see cref="Vector2"/> vertices) per <see cref="Subpath"/>, ready
///     for <see cref="ScanlineRasterizer"/> to accumulate coverage from.
/// </summary>
/// <remarks>
///     Walks each <see cref="Subpath"/>'s <see cref="PathCommand"/> sequence using the same
///     current-point bookkeeping pattern already established by <see cref="Path.GetBounds"/>:
///     <see cref="PathCommandType.LineTo"/> converts directly, <see cref="PathCommandType.QuadraticBezierTo"/>
///     and <see cref="PathCommandType.CubicBezierTo"/> flatten via <see cref="BezierFlattening"/>,
///     and <see cref="PathCommandType.ArcTo"/> first converts to cubic Bezier segments via
///     <see cref="SvgArcConverter.ToBeziers"/> and then flattens each segment the same way.
///     Critically, every subpath is treated as implicitly closed for fill purposes regardless of
///     <see cref="Subpath.IsClosed"/> (matching SVG's own fill semantics for open subpaths, which
///     differ from stroke semantics): if the last point written to a polygon does not already
///     coincide with the subpath's <see cref="Subpath.Start"/>, an implicit closing point back to
///     <see cref="Subpath.Start"/> is appended. A subpath with fewer than three effective points
///     after flattening (a degenerate zero-area "polygon") is passed through unmodified rather
///     than special-cased - <see cref="ScanlineRasterizer"/> naturally contributes zero coverage
///     for such a shape, so no additional handling is needed here.
/// </remarks>
internal static class EdgeFlattener
{
    /// <summary>
    ///     Flattens every subpath of <paramref name="path"/> into a closed polygon.
    /// </summary>
    /// <param name="path">The path to flatten. Must not be null.</param>
    /// <param name="tolerance">
    ///     The maximum allowed perpendicular deviation, in the same units as the path's points,
    ///     between a flattened curve segment's polyline and the true curve - forwarded directly to
    ///     <see cref="BezierFlattening"/>. Must be greater than zero (enforced by
    ///     <see cref="BezierFlattening"/> itself; this method performs no additional validation).
    /// </param>
    /// <returns>
    ///     One closed polygon per subpath of <paramref name="path"/>, in the same order as
    ///     <see cref="Path.Subpaths"/>. Never null; an empty <paramref name="path"/> yields an
    ///     empty list.
    /// </returns>
    internal static List<List<Vector2>> Flatten(Path path, float tolerance)
    {
        var polygons = new List<List<Vector2>>(path.Subpaths.Count);

        foreach (var subpath in path.Subpaths)
        {
            var polygon = new List<Vector2> { subpath.Start };
            var current = subpath.Start;

            foreach (var command in subpath.Commands)
            {
                current = AppendCommand(current, command, tolerance, polygon);
            }

            // Implicit close for fill purposes: append a closing point back to Start unless the
            // polygon already ends there (either because the last command was itself a LineTo to
            // Start, or because Subpath.IsClosed's underlying Close command is conceptually a line
            // back to Start that the loop above does not draw directly - see AppendCommand)
            if (polygon[^1] != subpath.Start)
            {
                polygon.Add(subpath.Start);
            }

            polygons.Add(polygon);
        }

        return polygons;
    }

    /// <summary>
    ///     Converts one <see cref="PathCommand"/> into polygon vertices appended to
    ///     <paramref name="polygon"/>, returning the new current point for the next command.
    /// </summary>
    private static Vector2 AppendCommand(Vector2 current, PathCommand command, float tolerance, List<Vector2> polygon)
    {
        switch (command.Type)
        {
            case PathCommandType.LineTo:
                polygon.Add(command.EndPoint);
                return command.EndPoint;

            case PathCommandType.QuadraticBezierTo:
                BezierFlattening.FlattenQuadratic(current, command.Control1, command.EndPoint, tolerance, polygon);
                return command.EndPoint;

            case PathCommandType.CubicBezierTo:
                BezierFlattening.FlattenCubic(current, command.Control1, command.Control2, command.EndPoint, tolerance, polygon);
                return command.EndPoint;

            case PathCommandType.ArcTo:
                // Arcs carry no control points of their own - convert to the equivalent cubic
                // Bezier segments first (via SvgArcConverter), then flatten each segment in turn,
                // exactly mirroring the pattern Path.GetBounds already uses for the same reason
                var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
                SvgArcConverter.ToBeziers(
                    current, command.Radius, command.RotationDegrees, command.LargeArc, command.Sweep,
                    command.EndPoint, segments);

                var segmentStart = current;
                foreach (var segment in segments)
                {
                    BezierFlattening.FlattenCubic(segmentStart, segment.Control1, segment.Control2, segment.End, tolerance, polygon);
                    segmentStart = segment.End;
                }

                return command.EndPoint;

            case PathCommandType.Close:
            default:
                // Close never carries an EndPoint of its own - it always conceptually returns to
                // Start. No point is drawn here directly; the implicit-close check in Flatten
                // appends the closing point back to Start once, after the loop, whether or not an
                // explicit Close command was present - so this case intentionally leaves the
                // current point (and the polygon) unchanged.
                return current;
        }
    }
}
