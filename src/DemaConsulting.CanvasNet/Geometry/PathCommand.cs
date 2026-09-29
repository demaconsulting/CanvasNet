using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Represents a single non-move drawing operation within a <see cref="Subpath"/>, as a tagged
///     union of every field any <see cref="PathCommandType"/> might need.
/// </summary>
/// <remarks>
///     A tagged union (rather than a small class hierarchy) is used because
///     <see cref="Subpath.Commands"/> is expected to be walked in tight loops by a future
///     rasterizer/stroker, and a single flat, non-nullable value type keeps that walk allocation-
///     free with no virtual dispatch. Not every field is meaningful for every <see cref="Type"/>;
///     see each field's own documentation for which command types populate it. A
///     <see cref="Subpath.Commands"/> list is guaranteed to only ever contain well-formed
///     commands, since every factory method other than <see cref="LineTo"/> is internal to this
///     package and used only by <see cref="PathBuilder"/>; <see cref="LineTo"/> alone is public,
///     for an external consumer to synthesize a standalone straight-line command to pass to
///     <see cref="ComputeTangents"/> (see its own documentation for why).
/// </remarks>
public readonly struct PathCommand
{
    /// <summary>
    ///     The kind of drawing operation this command represents.
    /// </summary>
    public PathCommandType Type { get; }

    /// <summary>
    ///     The point this command draws to. Meaningful for <see cref="PathCommandType.LineTo"/>,
    ///     <see cref="PathCommandType.QuadraticBezierTo"/>, <see cref="PathCommandType.CubicBezierTo"/>,
    ///     and <see cref="PathCommandType.ArcTo"/>. Unused (default) for
    ///     <see cref="PathCommandType.Close"/>, whose endpoint is always <see cref="Subpath.Start"/>.
    /// </summary>
    public Vector2 EndPoint { get; }

    /// <summary>
    ///     The first (or only) Bezier control point. Meaningful for
    ///     <see cref="PathCommandType.QuadraticBezierTo"/> (the curve's single control point) and
    ///     <see cref="PathCommandType.CubicBezierTo"/> (the curve's first control point). Unused
    ///     (default) for every other command type.
    /// </summary>
    public Vector2 Control1 { get; }

    /// <summary>
    ///     The second Bezier control point. Meaningful only for
    ///     <see cref="PathCommandType.CubicBezierTo"/> (the curve's second control point). Unused
    ///     (default) for every other command type.
    /// </summary>
    public Vector2 Control2 { get; }

    /// <summary>
    ///     The elliptical arc's x- and y-radii (rx, ry). Meaningful only for
    ///     <see cref="PathCommandType.ArcTo"/>. Unused (default) for every other command type.
    /// </summary>
    public Vector2 Radius { get; }

    /// <summary>
    ///     The elliptical arc's x-axis rotation, in degrees, matching the units used directly by
    ///     the SVG path data "A"/"a" command. Meaningful only for
    ///     <see cref="PathCommandType.ArcTo"/>. Unused (default, zero) for every other command
    ///     type.
    /// </summary>
    public float RotationDegrees { get; }

    /// <summary>
    ///     The SVG arc "large-arc-flag": <see langword="true"/> selects the arc sweep of 180
    ///     degrees or greater between the two candidate ellipses satisfying the endpoint
    ///     constraints. Meaningful only for <see cref="PathCommandType.ArcTo"/>. Unused (default,
    ///     <see langword="false"/>) for every other command type.
    /// </summary>
    public bool LargeArc { get; }

    /// <summary>
    ///     The SVG arc "sweep-flag": <see langword="true"/> selects the arc drawn in the
    ///     "positive-angle" direction. Meaningful only for <see cref="PathCommandType.ArcTo"/>.
    ///     Unused (default, <see langword="false"/>) for every other command type.
    /// </summary>
    public bool Sweep { get; }

    /// <summary>
    ///     Initializes every field of the tagged union directly. Private because only this
    ///     struct's own internal factory methods below construct instances, keeping every field
    ///     combination that is ever produced consistent with its <see cref="Type"/>.
    /// </summary>
    private PathCommand(
        PathCommandType type,
        Vector2 endPoint,
        Vector2 control1,
        Vector2 control2,
        Vector2 radius,
        float rotationDegrees,
        bool largeArc,
        bool sweep)
    {
        Type = type;
        EndPoint = endPoint;
        Control1 = control1;
        Control2 = control2;
        Radius = radius;
        RotationDegrees = rotationDegrees;
        LargeArc = largeArc;
        Sweep = sweep;
    }

    /// <summary>
    ///     Creates a <see cref="PathCommandType.LineTo"/> command.
    /// </summary>
    /// <param name="end">The point the line segment draws to.</param>
    /// <returns>The constructed command.</returns>
    /// <remarks>
    ///     Unlike this struct's other factory methods, <see cref="LineTo"/> is public rather than
    ///     internal: a straight line segment is the simplest possible <see cref="PathCommand"/>,
    ///     and consumers outside this package legitimately need to synthesize one - for example, a
    ///     format codec computing a marker/arrowhead orientation for a closed subpath's implicit
    ///     closing edge (which is not itself recorded as its own <see cref="PathCommand"/> - see
    ///     <see cref="Subpath.IsClosed"/>) can build a <see cref="LineTo"/> command representing
    ///     that edge and pass it to <see cref="ComputeTangents"/>, rather than reimplementing
    ///     tangent math of its own. <see cref="QuadraticBezierTo"/>, <see cref="CubicBezierTo"/>,
    ///     <see cref="ArcTo"/>, and <see cref="Close"/> remain internal because only
    ///     <see cref="PathBuilder"/> - which alone knows how to keep a <see cref="Subpath"/>'s
    ///     accumulated state consistent for those richer command shapes - constructs them.
    /// </remarks>
    public static PathCommand LineTo(Vector2 end) =>
        new(PathCommandType.LineTo, end, default, default, default, 0f, false, false);

    /// <summary>
    ///     Creates a <see cref="PathCommandType.QuadraticBezierTo"/> command. Used only by
    ///     <see cref="PathBuilder"/>.
    /// </summary>
    /// <param name="control">The curve's single control point.</param>
    /// <param name="end">The point the curve draws to.</param>
    /// <returns>The constructed command.</returns>
    internal static PathCommand QuadraticBezierTo(Vector2 control, Vector2 end) =>
        new(PathCommandType.QuadraticBezierTo, end, control, default, default, 0f, false, false);

    /// <summary>
    ///     Creates a <see cref="PathCommandType.CubicBezierTo"/> command. Used only by
    ///     <see cref="PathBuilder"/>.
    /// </summary>
    /// <param name="control1">The curve's first control point.</param>
    /// <param name="control2">The curve's second control point.</param>
    /// <param name="end">The point the curve draws to.</param>
    /// <returns>The constructed command.</returns>
    internal static PathCommand CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 end) =>
        new(PathCommandType.CubicBezierTo, end, control1, control2, default, 0f, false, false);

    /// <summary>
    ///     Creates an <see cref="PathCommandType.ArcTo"/> command, storing the raw SVG-style
    ///     endpoint-parameterized arc arguments exactly as supplied. Used only by
    ///     <see cref="PathBuilder"/>.
    /// </summary>
    /// <param name="radius">The arc's x- and y-radii (rx, ry).</param>
    /// <param name="rotationDegrees">The arc's x-axis rotation, in degrees.</param>
    /// <param name="largeArc">The SVG arc "large-arc-flag".</param>
    /// <param name="sweep">The SVG arc "sweep-flag".</param>
    /// <param name="end">The point the arc draws to.</param>
    /// <returns>The constructed command.</returns>
    /// <remarks>
    ///     No conversion to Bezier curves happens here: <see cref="PathBuilder.ArcTo"/> and this
    ///     factory always store the raw SVG parameters, deferring conversion to
    ///     <see cref="SvgArcConverter"/>, which a consumer invokes only when it needs the
    ///     flattening tolerance/segment shape that conversion requires.
    /// </remarks>
    internal static PathCommand ArcTo(Vector2 radius, float rotationDegrees, bool largeArc, bool sweep, Vector2 end) =>
        new(PathCommandType.ArcTo, end, default, default, radius, rotationDegrees, largeArc, sweep);

    /// <summary>
    ///     Creates a <see cref="PathCommandType.Close"/> command. Used only by
    ///     <see cref="PathBuilder"/>.
    /// </summary>
    /// <returns>The constructed command.</returns>
    internal static PathCommand Close() =>
        new(PathCommandType.Close, default, default, default, default, 0f, false, false);

    /// <summary>
    ///     Computes this command's outgoing (leaving <paramref name="start"/>) and incoming
    ///     (arriving at <see cref="EndPoint"/>) unit tangent directions.
    /// </summary>
    /// <param name="start">The command's start point (the previous vertex's position).</param>
    /// <returns>
    ///     The outgoing/incoming unit tangents, or <see langword="null"/> for either when the
    ///     relevant control points/endpoints are coincident (a zero-length direction has no
    ///     meaningful tangent), for <see cref="PathCommandType.LineTo"/>,
    ///     <see cref="PathCommandType.QuadraticBezierTo"/>, and
    ///     <see cref="PathCommandType.CubicBezierTo"/>. Returns <c>(null, null)</c> for
    ///     <see cref="PathCommandType.ArcTo"/> and <see cref="PathCommandType.Close"/> - an arc's
    ///     tangent depends on its converted Bezier representation (see
    ///     <see cref="SvgArcConverter"/>), which this method does not perform, and a
    ///     <see cref="PathCommandType.Close"/> command carries no <see cref="EndPoint"/> of its
    ///     own to compute a direction from at all.
    /// </returns>
    /// <remarks>
    ///     This is the shared building block a marker/arrowhead-orientation feature (or any other
    ///     consumer needing a path segment's direction of travel) needs: a straight line's
    ///     outgoing and incoming tangents are both simply its normalized direction of travel; a
    ///     Bezier curve's outgoing tangent points toward its first non-coincident control point
    ///     (falling back to its end point when every control point coincides with
    ///     <paramref name="start"/>), and its incoming tangent points away from its last
    ///     non-coincident control point (falling back to <paramref name="start"/> when every
    ///     control point coincides with <see cref="EndPoint"/>).
    /// </remarks>
    public (Vector2? Outgoing, Vector2? Incoming) ComputeTangents(Vector2 start)
    {
        switch (Type)
        {
            case PathCommandType.LineTo:
                var lineDirection = NormalizeOrNull(start, EndPoint);
                return (lineDirection, lineDirection);

            case PathCommandType.QuadraticBezierTo:
                var outgoingQuad = NormalizeOrNull(start, Control1)
                    ?? NormalizeOrNull(start, EndPoint);
                var incomingQuad = NormalizeOrNull(Control1, EndPoint)
                    ?? NormalizeOrNull(start, EndPoint);
                return (outgoingQuad, incomingQuad);

            case PathCommandType.CubicBezierTo:
                var outgoingCubic = NormalizeOrNull(start, Control1)
                    ?? NormalizeOrNull(start, Control2)
                    ?? NormalizeOrNull(start, EndPoint);
                var incomingCubic = NormalizeOrNull(Control2, EndPoint)
                    ?? NormalizeOrNull(Control1, EndPoint)
                    ?? NormalizeOrNull(start, EndPoint);
                return (outgoingCubic, incomingCubic);

            default:
                return (null, null);
        }
    }

    /// <summary>
    ///     Computes the normalized direction from <paramref name="from"/> to <paramref name="to"/>,
    ///     tolerating a zero-length or non-finite result.
    /// </summary>
    /// <param name="from">The direction's start point.</param>
    /// <param name="to">The direction's end point.</param>
    /// <returns>
    ///     The unit-length direction, or <see langword="null"/> if the two points coincide, or
    ///     the direction is subnormal-to-zero or non-finite (a degenerate direction has no
    ///     meaningful orientation to contribute).
    /// </returns>
    /// <remarks>
    ///     Every step of this computation - the point-to-point delta and its squared length -
    ///     is performed in <see langword="double"/> rather than <see langword="float"/>
    ///     precision. Computing the delta as a <see cref="Vector2"/> subtraction first would
    ///     itself overflow to <see cref="float.PositiveInfinity"/> for finite points as close
    ///     together (in magnitude terms) as <c>(-float.MaxValue, 0)</c> and
    ///     <c>(float.MaxValue, 0)</c>, before the squared-length widening in a prior revision of
    ///     this method ever got a chance to help; widening at the point-to-point delta itself,
    ///     rather than only at the squared-length step, avoids that false-positive degeneracy
    ///     while still correctly reporting a genuinely zero-length or non-finite direction.
    /// </remarks>
    private static Vector2? NormalizeOrNull(Vector2 from, Vector2 to)
    {
        // Compute the delta itself in double precision so that neither the subtraction nor the
        // squared-length step can spuriously overflow for finite, far-apart points
        double dx = (double)to.X - from.X;
        double dy = (double)to.Y - from.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (!double.IsFinite(lengthSquared) || lengthSquared <= float.Epsilon)
        {
            return null;
        }

        // Divide by the double-precision length so the resulting direction remains a valid unit
        // vector even for very large or far-apart input magnitudes
        var length = Math.Sqrt(lengthSquared);
        return new Vector2((float)(dx / length), (float)(dy / length));
    }
}
