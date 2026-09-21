using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Represents an immutable vector path: an ordered collection of independent
///     <see cref="Subpath"/> contours, each an ordered sequence of drawing commands.
/// </summary>
/// <remarks>
///     <see cref="Path"/> has no public constructor - every instance is produced by
///     <see cref="PathBuilder.Build"/>, which is the only component responsible for guaranteeing
///     every subpath is well-formed (starts with a real move-derived <see cref="Subpath.Start"/>,
///     never contains a dangling/empty subpath). This makes an invalid <see cref="Path"/>
///     unrepresentable rather than merely undocumented.
/// </remarks>
public sealed class Path
{
    /// <summary>
    ///     The singleton empty path, containing zero subpaths.
    /// </summary>
    /// <remarks>
    ///     <see cref="GetBounds"/> on this instance returns <see cref="Rect.Empty"/>, matching
    ///     <see cref="Rect"/>'s union-identity convention: an empty path contributes nothing to
    ///     any bounds it is combined with.
    /// </remarks>
    public static readonly Path Empty = new([]);

    /// <summary>
    ///     The ordered collection of independent subpaths making up this path.
    /// </summary>
    public IReadOnlyList<Subpath> Subpaths { get; }

    /// <summary>
    ///     Initializes a new immutable path snapshot. Used only by <see cref="PathBuilder.Build"/>
    ///     (and this type's own <see cref="Empty"/> singleton), which is the sole component
    ///     responsible for guaranteeing every subpath is well-formed.
    /// </summary>
    /// <param name="subpaths">The ordered collection of independent subpaths.</param>
    internal Path(IReadOnlyList<Subpath> subpaths)
    {
        Subpaths = subpaths;
    }

    /// <summary>
    ///     Computes an axis-aligned bounding box enclosing this entire path.
    /// </summary>
    /// <param name="flattenTolerance">
    ///     When less than or equal to zero (the default), the bounds are computed conservatively
    ///     and cheaply from each command's raw control points and endpoints - including
    ///     <see cref="PathCommandType.ArcTo"/> commands, which are converted to Bezier curves via
    ///     <see cref="SvgArcConverter"/> first, since arcs have no control points of their own -
    ///     relying on the convex-hull property of Bezier curves (every point on a quadratic or
    ///     cubic Bezier curve lies within the convex hull of its control points and endpoints, so
    ///     enclosing every control point and endpoint always encloses the whole curve, though
    ///     possibly more loosely than the curve's true bounds). When greater than zero, every
    ///     curve is first flattened to a polyline within the given tolerance via
    ///     <see cref="BezierFlattening"/>, and the bounds are computed from the flattened points
    ///     instead - this is more expensive but produces a tighter bound, since a curve's flattened
    ///     polyline hugs the true curve far more closely than its control-point convex hull does.
    /// </param>
    /// <returns>
    ///     The smallest axis-aligned rectangle (conservative, or tighter under a positive
    ///     <paramref name="flattenTolerance"/>) enclosing this path, or <see cref="Rect.Empty"/> if
    ///     this path has zero subpaths.
    /// </returns>
    public Rect GetBounds(float flattenTolerance = 0)
    {
        var bounds = Rect.Empty;

        foreach (var subpath in Subpaths)
        {
            var current = subpath.Start;
            bounds = bounds.Union(PointRect(current));

            foreach (var command in subpath.Commands)
            {
                bounds = AccumulateCommandBounds(current, command, flattenTolerance, ref bounds);

                // Close never carries an EndPoint of its own (it always returns to Start); every
                // other command type's EndPoint becomes the new "current point" for the next
                // command in sequence
                if (command.Type != PathCommandType.Close)
                {
                    current = command.EndPoint;
                }
            }
        }

        return bounds;
    }

    /// <summary>
    ///     Folds one command's contribution into <paramref name="bounds"/>, choosing between the
    ///     conservative control-point convex-hull bound and the flattening-based tighter bound
    ///     according to <paramref name="flattenTolerance"/>.
    /// </summary>
    private static Rect AccumulateCommandBounds(Vector2 current, PathCommand command, float flattenTolerance, ref Rect bounds)
    {
        switch (command.Type)
        {
            case PathCommandType.LineTo:
                return bounds.Union(PointRect(command.EndPoint));

            case PathCommandType.QuadraticBezierTo:
                if (flattenTolerance > 0)
                {
                    var points = new List<Vector2>();
                    BezierFlattening.FlattenQuadratic(current, command.Control1, command.EndPoint, flattenTolerance, points);
                    foreach (var point in points)
                    {
                        bounds = bounds.Union(PointRect(point));
                    }

                    return bounds;
                }

                return bounds.Union(PointRect(command.Control1)).Union(PointRect(command.EndPoint));

            case PathCommandType.CubicBezierTo:
                if (flattenTolerance > 0)
                {
                    var points = new List<Vector2>();
                    BezierFlattening.FlattenCubic(current, command.Control1, command.Control2, command.EndPoint, flattenTolerance, points);
                    foreach (var point in points)
                    {
                        bounds = bounds.Union(PointRect(point));
                    }

                    return bounds;
                }

                return bounds.Union(PointRect(command.Control1)).Union(PointRect(command.Control2)).Union(PointRect(command.EndPoint));

            case PathCommandType.ArcTo:
                // Arcs carry no control points of their own - convert to the equivalent cubic
                // Bezier segments first (via SvgArcConverter), then apply the same convex-hull or
                // flattening treatment as any other curve
                var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
                SvgArcConverter.ToBeziers(current, command.Radius, command.RotationDegrees, command.LargeArc, command.Sweep, command.EndPoint, segments);

                var segmentStart = current;
                foreach (var segment in segments)
                {
                    if (flattenTolerance > 0)
                    {
                        var points = new List<Vector2>();
                        BezierFlattening.FlattenCubic(segmentStart, segment.Control1, segment.Control2, segment.End, flattenTolerance, points);
                        foreach (var point in points)
                        {
                            bounds = bounds.Union(PointRect(point));
                        }
                    }
                    else
                    {
                        bounds = bounds.Union(PointRect(segment.Control1)).Union(PointRect(segment.Control2)).Union(PointRect(segment.End));
                    }

                    segmentStart = segment.End;
                }

                return bounds;

            case PathCommandType.Close:
            default:
                return bounds;
        }
    }

    /// <summary>
    ///     Returns a zero-size <see cref="Rect"/> located at <paramref name="point"/>, suitable
    ///     for folding into a running <see cref="Rect.Union(Rect)"/> accumulation.
    /// </summary>
    private static Rect PointRect(Vector2 point) => new(point.X, point.Y, 0, 0);
}
