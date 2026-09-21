using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Converts each <see cref="Path"/> subpath into a polyline while preserving whether that
///     subpath was originally open or closed.
/// </summary>
/// <remarks>
///     Unlike <see cref="EdgeFlattener"/>, which always appends an implicit closing edge because
///     fill semantics treat every subpath as closed, stroking must preserve the real open/closed
///     distinction from <see cref="Subpath.IsClosed"/> so that open subpaths receive end caps and
///     closed subpaths instead produce shell rings with no caps.
/// </remarks>
internal static class StrokePathFlattener
{
    /// <summary>
    ///     Flattens every subpath of <paramref name="path"/> into a polyline plus its open/closed
    ///     state.
    /// </summary>
    /// <param name="path">The path to flatten.</param>
    /// <param name="tolerance">
    ///     The maximum allowed curve-flattening deviation, forwarded to
    ///     <see cref="BezierFlattening"/>.
    /// </param>
    /// <returns>
    ///     One polyline per subpath of <paramref name="path"/>, in the same order as
    ///     <see cref="Path.Subpaths"/>.
    /// </returns>
    internal static List<(List<Vector2> Points, bool IsClosed)> Flatten(Path path, float tolerance)
    {
        var polylines = new List<(List<Vector2> Points, bool IsClosed)>(path.Subpaths.Count);

        foreach (var subpath in path.Subpaths)
        {
            var points = new List<Vector2> { subpath.Start };
            var current = subpath.Start;

            foreach (var command in subpath.Commands)
            {
                current = AppendCommand(current, command, tolerance, points);
            }

            polylines.Add((points, subpath.IsClosed));
        }

        return polylines;
    }

    /// <summary>
    ///     Converts one <see cref="PathCommand"/> into polyline vertices appended to
    ///     <paramref name="points"/>, returning the new current point.
    /// </summary>
    private static Vector2 AppendCommand(Vector2 current, PathCommand command, float tolerance, List<Vector2> points)
    {
        switch (command.Type)
        {
            case PathCommandType.LineTo:
                points.Add(command.EndPoint);
                return command.EndPoint;

            case PathCommandType.QuadraticBezierTo:
                BezierFlattening.FlattenQuadratic(current, command.Control1, command.EndPoint, tolerance, points);
                return command.EndPoint;

            case PathCommandType.CubicBezierTo:
                BezierFlattening.FlattenCubic(current, command.Control1, command.Control2, command.EndPoint, tolerance, points);
                return command.EndPoint;

            case PathCommandType.ArcTo:
                var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
                SvgArcConverter.ToBeziers(
                    current,
                    command.Radius,
                    command.RotationDegrees,
                    command.LargeArc,
                    command.Sweep,
                    command.EndPoint,
                    segments);

                var segmentStart = current;
                foreach (var segment in segments)
                {
                    BezierFlattening.FlattenCubic(
                        segmentStart,
                        segment.Control1,
                        segment.Control2,
                        segment.End,
                        tolerance,
                        points);
                    segmentStart = segment.End;
                }

                return command.EndPoint;

            case PathCommandType.Close:
            default:
                return current;
        }
    }
}
