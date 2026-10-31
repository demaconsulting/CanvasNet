// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The generators of <see cref="CurveBuilder"/>: the d3-shape <c>line</c> and <c>area</c> (<c>line.js</c>, <c>area.js</c>).
/// </content>
internal static partial class CurveBuilder
{
    /// <summary>
    ///     Writes the outline of a line through points, as the d3-shape <c>line</c> generator does.
    /// </summary>
    /// <param name="kind">The curve.</param>
    /// <param name="tension">The <c>tension</c> property (bundle <c>beta</c>, cardinal tension, Catmull-Rom <c>alpha</c>); <see langword="null"/> for the default.</param>
    /// <param name="points">The points in drawing order; every undefined point ends the current subpath.</param>
    /// <param name="output">The list that receives the segments (they are appended).</param>
    /// <param name="cancellationToken">Polled every 1,024 points.</param>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public static void Line(
        CurveKind kind,
        double? tension,
        ReadOnlySpan<CurvePoint> points,
        List<ScenePathSegment> output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        var curve = Create(kind, tension, new SegmentSink(output));
        var defined0 = false;
        for (var i = 0; i <= points.Length; i++)
        {
            // Poll the token at the start and then every 1,024 points
            if ((i & (PollInterval - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            // A change of the defined flag starts or ends a subpath
            var defined = i < points.Length && IsDefined(points[i]);
            if (defined != defined0)
            {
                defined0 = defined;
                if (defined0)
                {
                    curve.LineStart();
                }
                else
                {
                    curve.LineEnd();
                }
            }

            if (defined0)
            {
                curve.Point(points[i].X, points[i].Y);
            }
        }
    }

    /// <summary>
    ///     Writes the outline of an area between a top line and a baseline, as the d3-shape <c>area</c> generator does.
    /// </summary>
    /// <remarks>
    ///     A vertical area has the points <c>(Along, Top)</c> on its top line and <c>(Along, Base)</c> on its baseline; a horizontal
    ///     area has <c>(Top, Along)</c> and <c>(Base, Along)</c>. For each run of defined points the top line is drawn forward
    ///     and the baseline backward, then the outline is closed.
    /// </remarks>
    /// <param name="kind">The curve; <see cref="CurveKind.Bundle"/> is not supported.</param>
    /// <param name="tension">The <c>tension</c> property; <see langword="null"/> for the default.</param>
    /// <param name="horizontal">Whether the area is horizontal (<c>Along</c> is y).</param>
    /// <param name="points">The points in drawing order; every undefined point ends the current run.</param>
    /// <param name="output">The list that receives the segments (they are appended).</param>
    /// <param name="cancellationToken">Polled every 1,024 points.</param>
    /// <exception cref="NotSupportedException">Thrown for <see cref="CurveKind.Bundle"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public static void Area(
        CurveKind kind,
        double? tension,
        bool horizontal,
        ReadOnlySpan<AreaPoint> points,
        List<ScenePathSegment> output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!SupportsArea(kind))
        {
            throw new NotSupportedException("The bundle curve cannot draw an area.");
        }

        var curve = Create(kind, tension, new SegmentSink(output));
        var defined0 = false;
        var start = 0;
        for (var i = 0; i <= points.Length; i++)
        {
            if ((i & (PollInterval - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var defined = i < points.Length && IsDefined(points[i]);
            if (defined != defined0)
            {
                defined0 = defined;
                if (defined0)
                {
                    // A run starts: the top line is drawn forward
                    start = i;
                    curve.AreaStart();
                    curve.LineStart();
                }
                else
                {
                    // The run ends: the baseline is drawn backward as a second line of the same area
                    curve.LineEnd();
                    curve.LineStart();
                    for (var k = i - 1; k >= start; k--)
                    {
                        var p = points[k];
                        if (horizontal)
                        {
                            curve.Point(p.Base, p.Along);
                        }
                        else
                        {
                            curve.Point(p.Along, p.Base);
                        }
                    }

                    curve.LineEnd();
                    curve.AreaEnd();
                }
            }

            if (defined0)
            {
                var p = points[i];
                if (horizontal)
                {
                    curve.Point(p.Top, p.Along);
                }
                else
                {
                    curve.Point(p.Along, p.Top);
                }
            }
        }
    }

    /// <summary>
    ///     Tells whether a point takes part in the path.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is defined and its coordinates are drawable (finite and within <see cref="ScenePainter.MaxMagnitude"/>).</returns>
    private static bool IsDefined(in CurvePoint point) => point.Defined && ScenePainter.IsBounded(point.X, point.Y);

    /// <summary>
    ///     Tells whether an area point takes part in the outline.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is defined and its coordinates are drawable (finite and within <see cref="ScenePainter.MaxMagnitude"/>).</returns>
    private static bool IsDefined(in AreaPoint point) =>
        point.Defined && ScenePainter.IsBounded(point.Along, point.Top, point.Base);
}
