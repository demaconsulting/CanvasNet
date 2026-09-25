using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Path-level effect that replaces straight-segment (polyline) corners in a
///     <see cref="Path"/> with tangent circular arcs of a fixed radius.
/// </summary>
/// <remarks>
///     <para>
///     Composition: this effect is a <see cref="Path"/>-to-<see cref="Path"/> pre-processing
///     step. It composes naturally with dashing because dashing is applied post-flatten inside
///     <c>PathStroker.Stroke</c> (see <c>Drawing.PathStroker</c>); the supported composition is
///     "round-then-stroke-with-dashing", producing a dashed rounded outline. "Dash-then-round"
///     is not supported architecturally because dashed segments never surface as a
///     <see cref="Path"/>.
///     </para>
///     <para>
///     Scope policy (v1): only vertices whose incoming and outgoing edges are both straight
///     line segments are rounded. Vertices adjacent to a curved command
///     (<see cref="PathCommandType.QuadraticBezierTo"/>, <see cref="PathCommandType.CubicBezierTo"/>,
///     or <see cref="PathCommandType.ArcTo"/>) are left unchanged, matching the policy used
///     by Skia's <c>SKPathEffect.CreateCorner</c>. For an interior vertex, "incoming" and
///     "outgoing" are the two adjacent <see cref="PathCommandType.LineTo"/> commands in the
///     subpath's command list. For a closed subpath (one ending in a trailing
///     <see cref="PathCommandType.Close"/>), the vertex at the subpath's start point is also
///     eligible for rounding: its "incoming" edge is the implicit closing edge back from the
///     last real command's endpoint (drawn by <see cref="PathCommandType.Close"/>), and its
///     "outgoing" edge is the subpath's first <see cref="PathCommandType.LineTo"/>. Likewise,
///     the vertex at the last real command's endpoint (immediately before a trailing
///     <see cref="PathCommandType.Close"/>) is eligible using that same implicit closing edge
///     as its "outgoing" edge.
///     </para>
///     <para>
///     The requested radius is clamped per corner to <c>min(incoming, outgoing) / 2</c> to
///     prevent overshoot on short segments.
///     </para>
/// </remarks>
public static class CornerRoundEffect
{
    /// <summary>
    ///     Applies corner rounding to <paramref name="source"/> at the given
    ///     <paramref name="radius"/>, returning a new <see cref="Path"/>.
    /// </summary>
    /// <param name="source">The source path.</param>
    /// <param name="radius">The corner radius. When zero, returns <paramref name="source"/> unchanged.</param>
    /// <returns>
    ///     A new <see cref="Path"/> whose polyline corners (including, for a closed subpath, the
    ///     wrap-around corner at the subpath's start point and the corner at the last real
    ///     vertex before the closing edge) have been replaced with tangent circular arcs, sharing
    ///     every non-polyline-corner vertex position with the original.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="radius"/> is not finite or is negative.
    /// </exception>
    public static Path Apply(Path source, float radius)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!float.IsFinite(radius) || radius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be finite and non-negative.");
        }

        if (radius == 0f || source.Subpaths.Count == 0)
        {
            return source;
        }

        var builder = new PathBuilder();

        foreach (var subpath in source.Subpaths)
        {
            var commands = subpath.Commands;

            // A "wrap-around corner" is the vertex at subpath.Start where the *implicit* closing
            // edge (drawn by a trailing Close, from the last real command's endpoint back to
            // subpath.Start) meets the *first* LineTo (from subpath.Start to commands[0].EndPoint).
            // This is the same kind of "LineTo meets LineTo" polyline corner the main loop below
            // detects for every interior vertex, except neither edge is literally adjacent in the
            // commands list - one is the implicit closing edge (whose "command" is Close, at
            // lastIndex), the other is the first command. It requires at least two LineTo
            // commands before Close (a single LineTo before Close would make the "incoming" and
            // "outgoing" edges the same segment, which is not a real corner).
            var lastIndex = commands.Count - 1;
            var closingLineIndex = lastIndex - 1;
            var hasWrapAroundCorner = closingLineIndex > 0 &&
                                       commands[lastIndex].Type == PathCommandType.Close &&
                                       commands[closingLineIndex].Type == PathCommandType.LineTo &&
                                       commands[0].Type == PathCommandType.LineTo;

            var moveToPoint = subpath.Start;
            var wrapRadius = radius;
            if (hasWrapAroundCorner)
            {
                var incomingStart = commands[closingLineIndex].EndPoint;
                var outgoingEnd = commands[0].EndPoint;
                wrapRadius = ClampRadius(radius, incomingStart, subpath.Start, outgoingEnd);

                // Compute where the wrap-around arc's tangent-out point lands using a scratch
                // builder (the same TangentArcTo math used for every other corner), so the real
                // subpath below can start there directly instead of at the un-rounded Start.
                var scratch = new PathBuilder()
                    .MoveTo(incomingStart)
                    .TangentArcTo(subpath.Start, outgoingEnd, wrapRadius)
                    .Build();
                moveToPoint = scratch.Subpaths[0].Commands[^1].EndPoint;
            }

            builder.MoveTo(moveToPoint);

            if (commands.Count == 0)
            {
                continue;
            }

            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];

                // The implicit closing edge of a wrap-around corner is replaced by the tangent
                // arc back to the (already-computed) moveToPoint, in place of a plain Close.
                if (hasWrapAroundCorner && i == lastIndex)
                {
                    builder.TangentArcTo(subpath.Start, commands[0].EndPoint, wrapRadius);
                    builder.Close();
                    continue;
                }

                // A "polyline corner" is the endpoint of a LineTo whose *following* command is
                // also a LineTo, or the implicit closing edge (a trailing Close, whose effective
                // endpoint is always subpath.Start). When either condition holds, we replace the
                // two straight edges that meet at command.EndPoint with LineTo(tangent-in) +
                // CubicBezier(tangent-arc) to tangent-out (all handled internally by
                // PathBuilder.TangentArcTo). The subsequent command, on the next loop iteration,
                // starts from the tangent-out point and goes to its own vertex (which may itself
                // be another polyline corner handled the same way).
                //
                // Both the INCOMING and OUTGOING edges meeting at this vertex must be straight
                // for it to qualify: the incoming edge is `command` itself (the edge arriving at
                // command.EndPoint from the previous command's endpoint, or from subpath.Start
                // when i == 0), so requiring command.Type == LineTo is what enforces "incoming
                // edge is straight" - a curved command (QuadraticBezierTo/CubicBezierTo/ArcTo)
                // immediately before a LineTo never satisfies this, so the vertex where a curve
                // meets a following LineTo is correctly left unrounded. The outgoing edge is the
                // next command, `next`, checked separately below.
                var next = i + 1 < commands.Count ? (PathCommandType?)commands[i + 1].Type : null;
                var isIncomingStraight = command.Type == PathCommandType.LineTo;
                var isOutgoingStraight = next == PathCommandType.LineTo || next == PathCommandType.Close;
                var isPolylineCorner = isIncomingStraight && isOutgoingStraight;

                if (isPolylineCorner)
                {
                    var cornerPoint = command.EndPoint;
                    var nextEnd = next == PathCommandType.Close ? subpath.Start : commands[i + 1].EndPoint;

                    // Clamp radius per corner to min(incoming length, outgoing length) / 2 so
                    // an aggressive requested radius never overshoots either adjacent segment.
                    // "Incoming length" here uses the builder pen's current position (which is
                    // the tangent-out point of the previous corner if the previous vertex was
                    // also rounded, or the previous vertex itself otherwise). For the first
                    // corner (i == 0), the pen's actual current position is moveToPoint, but when
                    // a wrap-around corner was rounded, moveToPoint is already the tangent-out
                    // point of that rounding - i.e. it has been pulled in along this same edge.
                    // Using it here would shorten the apparent edge length and clamp this
                    // corner's radius too aggressively. The clamp must instead reflect the
                    // original (pre-rounding) edge length, so use the subpath's original,
                    // un-adjusted start point in that case.
                    var incomingStart = i >= 1 ? commands[i - 1].EndPoint : subpath.Start;
                    var effectiveRadius = ClampRadius(radius, incomingStart, cornerPoint, nextEnd);

                    builder.TangentArcTo(cornerPoint, nextEnd, effectiveRadius);
                }
                else
                {
                    switch (command.Type)
                    {
                        case PathCommandType.LineTo:
                            builder.LineTo(command.EndPoint);
                            break;
                        case PathCommandType.QuadraticBezierTo:
                            builder.QuadraticBezierTo(command.Control1, command.EndPoint);
                            break;
                        case PathCommandType.CubicBezierTo:
                            builder.CubicBezierTo(command.Control1, command.Control2, command.EndPoint);
                            break;
                        case PathCommandType.ArcTo:
                            builder.ArcTo(command.Radius, command.RotationDegrees, command.LargeArc, command.Sweep, command.EndPoint);
                            break;
                        case PathCommandType.Close:
                            builder.Close();
                            break;
                    }
                }
            }
        }

        return builder.Build();
    }

    /// <summary>
    ///     Clamps <paramref name="radius"/> to half of the shorter of the incoming and outgoing
    ///     segment lengths meeting at <paramref name="cornerPoint"/>, so an aggressive requested
    ///     radius never overshoots either adjacent segment.
    /// </summary>
    private static float ClampRadius(float radius, Vector2 incomingStart, Vector2 cornerPoint, Vector2 outgoingEnd)
    {
        var incomingLen = Vector2.Distance(incomingStart, cornerPoint);
        var outgoingLen = Vector2.Distance(cornerPoint, outgoingEnd);
        if (incomingLen <= 0f || outgoingLen <= 0f)
        {
            return radius;
        }

        var maxRadius = MathF.Min(incomingLen, outgoingLen) / 2f;
        return radius > maxRadius ? maxRadius : radius;
    }
}
