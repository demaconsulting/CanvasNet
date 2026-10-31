// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     The painting of paths with segments, arcs and trails, and of the miter limit (a part of <see cref="ScenePainter"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Segments.</b> A <see cref="ScenePath"/> with <see cref="ScenePath.Segments"/> is drawn from its commands: a
///         line or cubic command before the first move is skipped, a command after a close continues at the start of the
///         closed subpath (as d3-path does), and a command with a coordinate that is not finite, or an end point beyond
///         <see cref="MaxMagnitude"/> surface pixels, makes the whole item draw nothing (as a points path does). A cubic command with a finite control point
///         beyond <see cref="MaxMagnitude"/> is drawn as a line to its end point. The fill is nonzero; the stroke is the item's stroke.
///     </para>
///     <para>
///         <b>Arcs.</b> A <see cref="SceneArc"/> is the outline of <see cref="ArcGeometry"/> translated to the center of the
///         item; an arc with a member that is not finite, or a center, radius or corner radius beyond <see cref="MaxMagnitude"/> surface pixels, draws nothing.
///     </para>
///     <para>
///         <b>Trails.</b> A <see cref="SceneTrail"/> is drawn as Vega's trail: for every pair of consecutive points of a run a
///         capsule of two straight edges and two half circles, all capsules filled and stroked as one path. A point that has
///         a coordinate or a size that is not finite, a magnitude above <see cref="MaxMagnitude"/> surface pixels or a size that is not positive ends its run; a
///         run needs two points to draw anything. Two coincident points draw a circle (Vega starts it with a line from the
///         previous capsule; this painter starts it with a move, which has the same fill and no stray stroke).
///         Cancellation is polled every 1,024 points and before every fill. The filler cannot be interrupted, so the capsules are filled and stroked
///         in chunks of at most <see cref="TrailChunkWeight"/> (the estimate of <see cref="TrailCapsuleWeight"/>): a trail of ordinary size is
///         one chunk, and a trail of huge points costs a bounded amount between two polls.
///     </para>
///     <para>
///         <b>Miter limit.</b> The miter limit of an item is 4 when it has none, 1 when it is not finite or is below 1 and
///         1,000 when it is above 1,000.
///     </para>
/// </remarks>
internal sealed partial class ScenePainter
{
    /// <summary>The miter limit of an item that has none.</summary>
    private const double DefaultMiterLimit = 4;

    /// <summary>The largest miter limit that is passed to the stroker.</summary>
    private const double MaxMiterLimit = 1000;

    /// <summary>The estimated cost, in line pieces of flattened curves, of the capsules that a trail paints in one fill (see <see cref="TrailCapsuleWeight"/>); a larger trail is painted in several chunks with a poll of the cancellation token between them. A chunk of the largest capsules (a radius of 16,384 pixels, weight about 906) is about 110 capsules, which take well under a second.</summary>
    internal const double TrailChunkWeight = 100_000;

    /// <summary>
    ///     Gets an observer that is told the number of capsules of a trail chunk just before the chunk is polled for cancellation and painted; null in production. It makes the chunking of
    ///     a trail testable by counting (and lets a test cancel at a chunk boundary), without measuring time.
    /// </summary>
    internal Action<int>? TrailChunkFlushed { get; init; }

    /// <summary>The number of trail points between two checks of the cancellation token.</summary>
    private const int TrailPollInterval = 1024;

    /// <summary>
    ///     Resolves the miter limit that the stroker receives.
    /// </summary>
    /// <param name="value">The miter limit of the item, or <see langword="null"/>.</param>
    /// <returns>The limit: 4 for <see langword="null"/>, otherwise the value clamped to 1 to 1,000 (1 when it is not a number).</returns>
    internal static float ResolveMiterLimit(double? value)
    {
        if (value is not { } limit)
        {
            return (float)DefaultMiterLimit;
        }

        if (!double.IsFinite(limit) || limit < 1)
        {
            return 1f;
        }

        return (float)Math.Min(limit, MaxMiterLimit);
    }

    /// <summary>
    ///     Draws a path that is made of drawing commands.
    /// </summary>
    /// <param name="item">The path item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintSegments(ScenePath item, Context local)
    {
        // A coordinate that is not finite, or an end point beyond MaxMagnitude surface pixels, makes the whole item draw nothing
        if (item.Opacity <= 0 || item.Segments.Any(s => !s.IsFinite() || !IsEndPointBounded(s, local.Scale)))
        {
            return;
        }

        var builder = new PathBuilder();
        var hasStart = false;
        var isOpen = false;
        var startX = 0.0;
        var startY = 0.0;
        foreach (var segment in item.Segments)
        {
            if (segment.Kind == ScenePathSegmentKind.MoveTo)
            {
                builder.MoveTo(new Vector2((float)segment.X, (float)segment.Y));
                (hasStart, isOpen, startX, startY) = (true, true, segment.X, segment.Y);
                continue;
            }

            // Everything else needs a subpath; after a close it continues at the start of the closed subpath
            if (!hasStart)
            {
                continue;
            }

            if (!isOpen)
            {
                builder.MoveTo(new Vector2((float)startX, (float)startY));
                isOpen = true;
            }

            switch (segment.Kind)
            {
                case ScenePathSegmentKind.LineTo:
                    builder.LineTo(new Vector2((float)segment.X, (float)segment.Y));
                    break;
                case ScenePathSegmentKind.CubicTo:
                    // A control point beyond MaxMagnitude (a spline with a huge tension, for example) would make the flattening of the curve expensive: a line instead
                    if (IsBoundedAt(local.Scale, segment.X1, segment.Y1, segment.X2, segment.Y2))
                    {
                        builder.CubicBezierTo(
                            new Vector2((float)segment.X1, (float)segment.Y1),
                            new Vector2((float)segment.X2, (float)segment.Y2),
                            new Vector2((float)segment.X, (float)segment.Y));
                    }
                    else
                    {
                        builder.LineTo(new Vector2((float)segment.X, (float)segment.Y));
                    }

                    break;
                default:
                    builder.Close();
                    isOpen = false;
                    break;
            }
        }

        FillAndStroke(builder.Build(), item, local, local.Transform);
    }

    /// <summary>
    ///     Draws a circular or annular sector.
    /// </summary>
    /// <param name="arc">The arc item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void PaintArc(SceneArc arc, Context local)
    {
        // The center, the radii and the corner radius are lengths and must be drawable; the angles only have to be numbers
        // (ArcGeometry bounds them itself, and the sector repeats every full turn)
        if (!IsBoundedAt(local.Scale, arc.X, arc.Y, arc.InnerRadius, arc.OuterRadius, arc.CornerRadius)
            || !IsFinite(arc.StartAngle, arc.EndAngle, arc.PadAngle)
            || arc.Opacity <= 0)
        {
            return;
        }

        var path = ArcGeometry.Build(arc.InnerRadius, arc.OuterRadius, arc.StartAngle, arc.EndAngle, arc.PadAngle, arc.CornerRadius);
        var placed = Matrix3x2.CreateTranslation((float)arc.X, (float)arc.Y) * local.Transform;
        FillAndStroke(path, arc, local, placed);
    }

    /// <summary>
    ///     Draws a trail.
    /// </summary>
    /// <param name="trail">The trail item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    private void PaintTrail(SceneTrail trail, Context local, CancellationToken cancellationToken)
    {
        if (trail.Opacity <= 0)
        {
            return;
        }

        var writer = new ArcPathWriter();
        var weight = 0.0;
        var capsules = 0;
        var visited = 0;
        foreach (var run in trail.Runs)
        {
            var ready = false;
            double x1 = 0;
            double y1 = 0;
            double r1 = 0;
            foreach (var point in run)
            {
                if (++visited % TrailPollInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (!IsDrawable(point, local.Scale))
                {
                    ready = false;
                    continue;
                }

                var r2 = point.Size / 2;
                if (ready)
                {
                    AppendCapsule(writer, x1, y1, r1, point.X, point.Y, r2);

                    // The filler cannot be interrupted, so the capsules are painted in chunks of a bounded cost, with a poll before each
                    weight += TrailCapsuleWeight(Math.Max(r1, r2), local.Scale);
                    capsules++;
                    if (weight >= TrailChunkWeight)
                    {
                        TrailChunkFlushed?.Invoke(capsules);
                        cancellationToken.ThrowIfCancellationRequested();
                        FillAndStroke(writer.Build(), trail, local, local.Transform);
                        writer = new ArcPathWriter();
                        (weight, capsules) = (0, 0);
                    }
                }

                (ready, x1, y1, r1) = (true, point.X, point.Y, r2);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        FillAndStroke(writer.Build(), trail, local, local.Transform);
    }

    /// <summary>
    ///     Estimates the cost of painting the capsule of a trail in units of one line piece of a flattened curve.
    /// </summary>
    /// <remarks>
    ///     A capsule has two round caps; the filler flattens a circle of a radius <c>r</c> surface pixels into a number of pieces that is proportional to
    ///     <c>sqrt(r / tolerance)</c>, so that is the weight of the caps; the two straight edges and the bookkeeping are one more unit.
    /// </remarks>
    /// <param name="radius">The larger radius of the capsule in scene units.</param>
    /// <param name="scale">The uniform scale of the placement.</param>
    /// <returns>The weight; at least one.</returns>
    internal static double TrailCapsuleWeight(double radius, float scale) => 1 + Math.Sqrt(radius * scale / FlattenTolerance);

    /// <summary>
    ///     Determines whether a trail point can be drawn.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="scale">The uniform scale of the placement.</param>
    /// <returns><see langword="true"/> when the coordinates and the size are bounded (see <see cref="IsBoundedAt"/>) and the size is positive.</returns>
    private static bool IsDrawable(SceneTrailPoint point, float scale) => IsBoundedAt(scale, point.X, point.Y, point.Size) && point.Size > 0;

    /// <summary>
    ///     Determines whether the end point of a path command can be drawn.
    /// </summary>
    /// <param name="segment">The command.</param>
    /// <param name="scale">The uniform scale of the placement.</param>
    /// <returns><see langword="true"/> for a close, and for any other command when its end point is bounded (see <see cref="IsBoundedAt"/>).</returns>
    private static bool IsEndPointBounded(ScenePathSegment segment, float scale) => segment.Kind == ScenePathSegmentKind.Close || IsBoundedAt(scale, segment.X, segment.Y);

    /// <summary>
    ///     Writes the capsule between two trail points (the segment of Vega's trail shape).
    /// </summary>
    /// <param name="writer">The path writer.</param>
    /// <param name="x1">The x coordinate of the previous point.</param>
    /// <param name="y1">The y coordinate of the previous point.</param>
    /// <param name="r1">The radius (half the size) at the previous point.</param>
    /// <param name="x2">The x coordinate of the point.</param>
    /// <param name="y2">The y coordinate of the point.</param>
    /// <param name="r2">The radius (half the size) at the point.</param>
    private static void AppendCapsule(ArcPathWriter writer, double x1, double y1, double r1, double x2, double y2, double r2)
    {
        var ux = y1 - y2;
        var uy = x2 - x1;
        if (ux != 0 || uy != 0)
        {
            // The unit normal of the segment
            var length = Math.Sqrt((ux * ux) + (uy * uy));
            ux /= length;
            uy /= length;
            var rx = ux * r1;
            var ry = uy * r1;
            var t = Math.Atan2(uy, ux);
            writer.MoveTo(x1 - rx, y1 - ry);
            writer.LineTo(x2 - (ux * r2), y2 - (uy * r2));
            writer.Arc(x2, y2, r2, t - Math.PI, t, false);
            writer.LineTo(x1 + rx, y1 + ry);
            writer.Arc(x1, y1, r1, t, t + Math.PI, false);
        }
        else
        {
            // Coincident points: a circle
            writer.MoveTo(x2 + r2, y2);
            writer.Arc(x2, y2, r2, 0, 2 * Math.PI, false);
        }

        writer.Close();
    }
}
