using System.Collections.ObjectModel;
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
    public static readonly Path Empty = new(new List<Subpath>());

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
    /// <remarks>
    ///     Wraps <paramref name="subpaths"/> in a <see cref="ReadOnlyCollection{T}"/> rather than
    ///     exposing it directly: <see cref="IReadOnlyList{T}"/> only hides mutating members from
    ///     the compile-time API, but a caller could still downcast <see cref="Subpaths"/> back to
    ///     <see cref="IList{T}"/> (its underlying <see cref="List{T}"/> implements it) and mutate
    ///     a supposedly-immutable <see cref="Path"/> in place. <see cref="ReadOnlyCollection{T}"/>
    ///     closes that hole - its own mutating members throw <see cref="NotSupportedException"/>
    ///     regardless of how it is cast.
    /// </remarks>
    internal Path(IList<Subpath> subpaths)
    {
        Subpaths = new ReadOnlyCollection<Subpath>(subpaths);
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
    ///     Returns a new <see cref="Path"/> obtained by applying <paramref name="transform"/> to
    ///     every subpath's start point and every drawing command's control points and endpoints.
    /// </summary>
    /// <param name="transform">The transform to apply, row-vector convention (<see cref="Vector2.Transform(Vector2, Matrix3x2)"/>).</param>
    /// <returns>
    ///     A new immutable <see cref="Path"/> containing the transformed geometry; the original
    ///     is unchanged. <see cref="PathCommandType.ArcTo"/> commands are first converted to
    ///     cubic Bezier segments via <see cref="SvgArcConverter"/>, then transformed, so an
    ///     arbitrary (including non-uniformly scaling, sheared, or rotated) transform can be
    ///     applied without needing to re-derive equivalent SVG arc parameters in the new frame.
    /// </returns>
    /// <remarks>
    ///     Row-vector convention matches <c>Vector2.Transform(p, A * B)</c>, which applies
    ///     <c>A</c> first, then <c>B</c> — the same order used by the <c>Rendering.Canvas</c>
    ///     transform stack.
    /// </remarks>
    public Path Transform(Matrix3x2 transform)
    {
        var builder = new PathBuilder();
        foreach (var subpath in Subpaths)
        {
            var current = subpath.Start;
            builder.MoveTo(Vector2.Transform(current, transform));

            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        builder.LineTo(Vector2.Transform(command.EndPoint, transform));
                        current = command.EndPoint;
                        break;
                    case PathCommandType.QuadraticBezierTo:
                        builder.QuadraticBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        current = command.EndPoint;
                        break;
                    case PathCommandType.CubicBezierTo:
                        builder.CubicBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.Control2, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        current = command.EndPoint;
                        break;
                    case PathCommandType.ArcTo:
                        var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
                        SvgArcConverter.ToBeziers(current, command.Radius, command.RotationDegrees, command.LargeArc, command.Sweep, command.EndPoint, segments);
                        foreach (var segment in segments)
                        {
                            builder.CubicBezierTo(
                                Vector2.Transform(segment.Control1, transform),
                                Vector2.Transform(segment.Control2, transform),
                                Vector2.Transform(segment.End, transform));
                        }

                        current = command.EndPoint;
                        break;
                    case PathCommandType.Close:
                        builder.Close();
                        break;
                }
            }
        }

        return builder.Build();
    }

    /// <summary>
    ///     Builds a new <see cref="Path"/> describing an axis-aligned rectangle with its top-left
    ///     corner at <c>(x, y)</c> and the given <paramref name="width"/> and
    ///     <paramref name="height"/>.
    /// </summary>
    /// <param name="x">The rectangle's left edge x-coordinate.</param>
    /// <param name="y">The rectangle's top edge y-coordinate.</param>
    /// <param name="width">The rectangle's width. When non-positive, <see cref="Empty"/> is returned.</param>
    /// <param name="height">The rectangle's height. When non-positive, <see cref="Empty"/> is returned.</param>
    /// <returns>A closed rectangular <see cref="Path"/> traversed clockwise in y-down space.</returns>
    public static Path Rectangle(float x, float y, float width, float height)
    {
        if (width <= 0f || height <= 0f)
        {
            return Empty;
        }

        return new PathBuilder()
            .MoveTo(new Vector2(x, y))
            .LineTo(new Vector2(x + width, y))
            .LineTo(new Vector2(x + width, y + height))
            .LineTo(new Vector2(x, y + height))
            .Close()
            .Build();
    }

    /// <summary>
    ///     Builds a new <see cref="Path"/> describing a rounded rectangle whose four corners are
    ///     replaced with tangent circular arcs of radius <paramref name="radius"/> (clamped to
    ///     <c>min(width, height) / 2</c>).
    /// </summary>
    /// <param name="x">The rectangle's left edge x-coordinate.</param>
    /// <param name="y">The rectangle's top edge y-coordinate.</param>
    /// <param name="width">The rectangle's width. When non-positive, <see cref="Empty"/> is returned.</param>
    /// <param name="height">The rectangle's height. When non-positive, <see cref="Empty"/> is returned.</param>
    /// <param name="radius">The corner radius. Negative values are treated as zero; large values are clamped to <c>min(width, height) / 2</c>.</param>
    /// <returns>
    ///     A closed rounded-rectangle <see cref="Path"/>. When <paramref name="radius"/> is zero
    ///     (or clamped to zero), the result is equivalent to <see cref="Rectangle"/>.
    /// </returns>
    public static Path RoundRectangle(float x, float y, float width, float height, float radius)
    {
        if (width <= 0f || height <= 0f)
        {
            return Empty;
        }

        if (radius < 0f)
        {
            radius = 0f;
        }

        var maxRadius = MathF.Min(width, height) / 2f;
        if (radius > maxRadius)
        {
            radius = maxRadius;
        }

        if (radius == 0f)
        {
            return Rectangle(x, y, width, height);
        }

        // Quarter-circle cubic Bezier approximation: kappa = 4/3 * (sqrt(2) - 1). At 90 degrees
        // this is exact enough for round-corner rendering (max radial error < 3e-4 * r).
        // (See Stanislaw K. Dzik / IBM 1975.)
        const float kappa = 0.5522847498f;
        var offset = radius * kappa;
        var right = x + width;
        var bottom = y + height;

        return new PathBuilder()
            .MoveTo(new Vector2(x + radius, y))
            .LineTo(new Vector2(right - radius, y))
            .CubicBezierTo(
                new Vector2(right - radius + offset, y),
                new Vector2(right, y + radius - offset),
                new Vector2(right, y + radius))
            .LineTo(new Vector2(right, bottom - radius))
            .CubicBezierTo(
                new Vector2(right, bottom - radius + offset),
                new Vector2(right - radius + offset, bottom),
                new Vector2(right - radius, bottom))
            .LineTo(new Vector2(x + radius, bottom))
            .CubicBezierTo(
                new Vector2(x + radius - offset, bottom),
                new Vector2(x, bottom - radius + offset),
                new Vector2(x, bottom - radius))
            .LineTo(new Vector2(x, y + radius))
            .CubicBezierTo(
                new Vector2(x, y + radius - offset),
                new Vector2(x + radius - offset, y),
                new Vector2(x + radius, y))
            .Close()
            .Build();
    }

    /// <summary>
    ///     Builds a new <see cref="Path"/> describing a circle of the given
    ///     <paramref name="radius"/> centered at <c>(centerX, centerY)</c>, approximated by four
    ///     cubic Bezier quadrants.
    /// </summary>
    /// <param name="centerX">The circle's center x-coordinate.</param>
    /// <param name="centerY">The circle's center y-coordinate.</param>
    /// <param name="radius">The circle's radius. When non-positive, <see cref="Empty"/> is returned.</param>
    /// <returns>
    ///     A closed circular <see cref="Path"/> composed of four cubic Bezier quadrants joined at
    ///     the four cardinal points.
    /// </returns>
    public static Path Circle(float centerX, float centerY, float radius)
    {
        if (radius <= 0f)
        {
            return Empty;
        }

        // Circle approximation using the same kappa=0.5522847498 quarter-turn Bezier constant.
        const float kappa = 0.5522847498f;
        var offset = radius * kappa;

        var right = new Vector2(centerX + radius, centerY);
        var bottom = new Vector2(centerX, centerY + radius);
        var left = new Vector2(centerX - radius, centerY);
        var top = new Vector2(centerX, centerY - radius);

        return new PathBuilder()
            .MoveTo(right)
            .CubicBezierTo(new Vector2(centerX + radius, centerY + offset), new Vector2(centerX + offset, centerY + radius), bottom)
            .CubicBezierTo(new Vector2(centerX - offset, centerY + radius), new Vector2(centerX - radius, centerY + offset), left)
            .CubicBezierTo(new Vector2(centerX - radius, centerY - offset), new Vector2(centerX - offset, centerY - radius), top)
            .CubicBezierTo(new Vector2(centerX + offset, centerY - radius), new Vector2(centerX + radius, centerY - offset), right)
            .Close()
            .Build();
    }

    /// <summary>
    ///     Returns a zero-size <see cref="Rect"/> located at <paramref name="point"/>, suitable
    ///     for folding into a running <see cref="Rect.Union(Rect)"/> accumulation.
    /// </summary>
    private static Rect PointRect(Vector2 point) => new(point.X, point.Y, 0, 0);
}
