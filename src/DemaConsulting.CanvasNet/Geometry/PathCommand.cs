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
///     see each field's own documentation for which command types populate it. Instances are
///     produced only by <see cref="PathBuilder"/>'s internal factory methods, so a
///     <see cref="Subpath.Commands"/> list is guaranteed to only ever contain well-formed
///     commands.
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
    ///     Creates a <see cref="PathCommandType.LineTo"/> command. Used only by
    ///     <see cref="PathBuilder"/>.
    /// </summary>
    /// <param name="end">The point the line segment draws to.</param>
    /// <returns>The constructed command.</returns>
    internal static PathCommand LineTo(Vector2 end) =>
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
}
