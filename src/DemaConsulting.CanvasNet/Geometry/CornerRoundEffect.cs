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
///     Scope policy (v1): only vertices whose incoming and outgoing commands are both straight
///     <see cref="PathCommandType.LineTo"/> segments (with a <see cref="PathBuilder.MoveTo"/>
///     opening the subpath treated as the "incoming" segment at the first vertex) are rounded.
///     Vertices adjacent to a curved command (<see cref="PathCommandType.QuadraticBezierTo"/>,
///     <see cref="PathCommandType.CubicBezierTo"/>, or <see cref="PathCommandType.ArcTo"/>) are
///     left unchanged, matching the policy used by Skia's <c>SKPathEffect.CreateCorner</c>.
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
    ///     A new <see cref="Path"/> whose polyline corners have been replaced with tangent
    ///     circular arcs, sharing every non-polyline-corner vertex position with the original.
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
            builder.MoveTo(subpath.Start);

            if (commands.Count == 0)
            {
                continue;
            }

            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];

                // A "polyline corner" is the endpoint of a LineTo whose *following* command is
                // also a LineTo. When both conditions hold, we replace the two straight edges
                // that meet at command.EndPoint with LineTo(tangent-in) + CubicBezier(tangent-arc)
                // to tangent-out (all handled internally by PathBuilder.TangentArcTo). The
                // subsequent LineTo, on the next loop iteration, starts from the tangent-out
                // point and goes to its own vertex (which may itself be another polyline corner
                // handled the same way).
                var next = i + 1 < commands.Count ? (PathCommandType?)commands[i + 1].Type : null;
                var isPolylineCorner = command.Type == PathCommandType.LineTo &&
                                        next == PathCommandType.LineTo;

                if (isPolylineCorner)
                {
                    var cornerPoint = command.EndPoint;
                    var nextEnd = commands[i + 1].EndPoint;

                    // Clamp radius per corner to min(incoming length, outgoing length) / 2 so
                    // an aggressive requested radius never overshoots either adjacent segment.
                    // "Incoming length" here uses the builder pen's current position (which is
                    // the tangent-out point of the previous corner if the previous vertex was
                    // also rounded, or the previous vertex itself otherwise).
                    var incomingStart = i >= 1 ? commands[i - 1].EndPoint : subpath.Start;
                    var incomingLen = Vector2.Distance(incomingStart, cornerPoint);
                    var outgoingLen = Vector2.Distance(cornerPoint, nextEnd);
                    var effectiveRadius = radius;
                    if (incomingLen > 0f && outgoingLen > 0f)
                    {
                        var maxRadius = MathF.Min(incomingLen, outgoingLen) / 2f;
                        if (effectiveRadius > maxRadius)
                        {
                            effectiveRadius = maxRadius;
                        }
                    }

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
}
