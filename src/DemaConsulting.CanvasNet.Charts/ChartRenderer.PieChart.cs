using System.Numerics;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using Path = DemaConsulting.CanvasNet.Geometry.Path;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

public static partial class ChartRenderer
{
    /// <summary>The padding, in pixels, left between the plot area's edge and a Pie/Doughnut wedge's outer radius.</summary>
    private const float PieOuterPadding = 8f;

    /// <summary>The fraction of <see cref="ChartType.Doughnut"/>'s outer radius used as its inner (hole) radius.</summary>
    private const float DoughnutInnerRadiusFraction = 0.5f;

    /// <summary>
    ///     Paints a Pie (<paramref name="isDoughnut"/> = <see langword="false"/>) or Doughnut
    ///     (<paramref name="isDoughnut"/> = <see langword="true"/>) chart: one wedge per data
    ///     point in <c>chart.Series[0]</c>, proportional to that point's share of the series'
    ///     total, plus per-point data labels.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="chart">
    ///     The chart being rendered. Only <c>chart.Series[0]</c> is painted - matching the
    ///     companion planning report's "single series" Pie/Doughnut model, any further series are
    ///     silently ignored (a <see cref="Chart"/> does not itself restrict Pie/Doughnut to
    ///     exactly one series, so this is a deliberate, documented renderer-level convention, not
    ///     a model constraint).
    /// </param>
    /// <param name="plotRect">The plot-area rectangle reserved by <see cref="ComputeLayout"/>.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    /// <param name="isDoughnut"><see langword="true"/> to cut a central hole (Doughnut); <see langword="false"/> for a solid Pie.</param>
    private static void PaintPieOrDoughnut(RenderCanvas canvas, Chart chart, Rect plotRect, ChartRenderOptions options, TrueTypeFont font, bool isDoughnut)
    {
        if (plotRect.Width <= 0f || plotRect.Height <= 0f)
        {
            return;
        }

        var series = chart.Series[0];
        var values = series.Values;

        // Only positive values contribute angular share; a non-positive value draws no wedge
        // (rather than throwing) - the documented "negative/zero pie value" edge case. Summing
        // raw values directly can overflow to +Infinity when two or more large-but-finite values
        // are present (double.MaxValue + double.MaxValue), which would then make every share
        // 0/Infinity = 0 and paint no wedges at all. Avoid this by first normalizing every value
        // against the largest positive value before accumulating: each normalized term is in
        // [0, 1], so neither the running total nor any later share computation can overflow,
        // regardless of how extreme the input magnitudes are.
        var maxPositive = values.Count > 0 ? Math.Max(0d, values.Max()) : 0d;

        var normalizedTotal = 0d;
        if (maxPositive > 0d)
        {
            foreach (var value in values)
            {
                normalizedTotal += Math.Max(0d, value) / maxPositive;
            }
        }

        var centerX = plotRect.X + plotRect.Width / 2f;
        var centerY = plotRect.Y + plotRect.Height / 2f;
        var outerRadius = MathF.Min(plotRect.Width, plotRect.Height) / 2f - PieOuterPadding;
        if (outerRadius <= 0f)
        {
            return;
        }

        var innerRadius = isDoughnut ? outerRadius * DoughnutInnerRadiusFraction : 0f;

        // Wedges start at 12 o'clock (-90 degrees in standard math-angle convention, where 0 is
        // 3 o'clock) and sweep clockwise - the near-universal pie-chart convention - since this
        // canvas' y-axis already points down, an increasing angle via cos/sin moves clockwise on
        // screen with no extra sign flip needed.
        var angle = -MathF.PI / 2f;
        for (var i = 0; i < values.Count; i++)
        {
            var share = normalizedTotal > 0d ? Math.Max(0d, values[i]) / maxPositive / normalizedTotal : 0d;
            var sweepAngle = (float)(share * (2d * Math.PI));
            var color = ResolvePointColor(chart, options, series, i);

            if (sweepAngle > 0f)
            {
                var endAngle = angle + sweepAngle;
                var wedge = BuildWedgePath(centerX, centerY, innerRadius, outerRadius, angle, endAngle);
                canvas.FillPath(wedge, color);

                var midAngle = angle + sweepAngle / 2f;
                var labelRadius = (innerRadius + outerRadius) / 2f;
                var labelX = centerX + MathF.Cos(midAngle) * labelRadius;
                var labelY = centerY + MathF.Sin(midAngle) * labelRadius;
                PaintPointDataLabel(canvas, series, i, labelX, labelY + options.DataLabelFontSize * 0.35f, TextAlign.Center, options, font);

                angle = endAngle;
            }
        }
    }

    /// <summary>
    ///     Builds a single wedge <see cref="Path"/>: a Pie wedge (<paramref name="innerRadius"/>
    ///     = 0) is <c>center -&gt; outer arc start -&gt; outer arc end -&gt; close</c>; a Doughnut
    ///     wedge is the ring segment between <paramref name="innerRadius"/> and
    ///     <paramref name="outerRadius"/>.
    /// </summary>
    /// <param name="centerX">The pie/doughnut's center x-coordinate.</param>
    /// <param name="centerY">The pie/doughnut's center y-coordinate.</param>
    /// <param name="innerRadius">The ring's inner radius, or <c>0</c> for a solid Pie wedge.</param>
    /// <param name="outerRadius">The ring's outer radius.</param>
    /// <param name="startAngle">The wedge's start angle, in radians (0 = 3 o'clock, increasing clockwise on screen).</param>
    /// <param name="endAngle">The wedge's end angle, in radians. Must be greater than <paramref name="startAngle"/>.</param>
    /// <returns>The built, immutable wedge <see cref="Path"/>.</returns>
    private static Path BuildWedgePath(float centerX, float centerY, float innerRadius, float outerRadius, float startAngle, float endAngle)
    {
        var outerStart = new Vector2(centerX + MathF.Cos(startAngle) * outerRadius, centerY + MathF.Sin(startAngle) * outerRadius);
        var builder = new PathBuilder();

        if (innerRadius <= 0f)
        {
            builder.MoveTo(new Vector2(centerX, centerY)).LineTo(outerStart);
            AppendForwardArc(builder, centerX, centerY, outerRadius, startAngle, endAngle);
            builder.Close();
        }
        else
        {
            var innerEnd = new Vector2(centerX + MathF.Cos(endAngle) * innerRadius, centerY + MathF.Sin(endAngle) * innerRadius);
            builder.MoveTo(outerStart);
            AppendForwardArc(builder, centerX, centerY, outerRadius, startAngle, endAngle);
            builder.LineTo(innerEnd);
            AppendBackwardArc(builder, centerX, centerY, innerRadius, endAngle, startAngle);
            builder.Close();
        }

        return builder.Build();
    }

    /// <summary>
    ///     Appends one or more <see cref="PathBuilder.ArcTo"/> commands tracing a circular arc of
    ///     <paramref name="radius"/> from angle <paramref name="startAngle"/> to
    ///     <paramref name="endAngle"/> (increasing-angle, clockwise-on-screen direction), onto the
    ///     builder's current point (which must already be positioned at the arc's start).
    /// </summary>
    /// <param name="builder">The path builder, positioned at the arc's start point.</param>
    /// <param name="centerX">The arc's circle center x-coordinate.</param>
    /// <param name="centerY">The arc's circle center y-coordinate.</param>
    /// <param name="radius">The arc's radius.</param>
    /// <param name="startAngle">The arc's start angle, in radians.</param>
    /// <param name="endAngle">The arc's end angle, in radians. Must be greater than or equal to <paramref name="startAngle"/>.</param>
    /// <remarks>
    ///     A sweep greater than half a circle (pi radians) is split at its midpoint into two
    ///     (recursively, if still greater than pi) smaller arcs, each emitted as a non-"large-arc"
    ///     SVG arc command. This is not a cosmetic choice: a single <c>ArcTo</c> spanning exactly
    ///     a full circle would have numerically identical start/end points, and
    ///     <see cref="SvgArcConverter"/> treats a coincident start/end arc as a zero-length no-op
    ///     (per the SVG spec) - silently discarding the entire wedge outline for a 100%-share
    ///     single-point Pie/Doughnut. Splitting first guarantees every emitted arc's start/end
    ///     points are genuinely distinct.
    /// </remarks>
    private static void AppendForwardArc(PathBuilder builder, float centerX, float centerY, float radius, float startAngle, float endAngle)
    {
        var sweep = endAngle - startAngle;
        if (sweep <= MathF.PI)
        {
            var end = new Vector2(centerX + MathF.Cos(endAngle) * radius, centerY + MathF.Sin(endAngle) * radius);
            builder.ArcTo(new Vector2(radius, radius), 0f, largeArc: false, sweep: true, end);
        }
        else
        {
            var mid = (startAngle + endAngle) / 2f;
            AppendForwardArc(builder, centerX, centerY, radius, startAngle, mid);
            AppendForwardArc(builder, centerX, centerY, radius, mid, endAngle);
        }
    }

    /// <summary>
    ///     Appends one or more <see cref="PathBuilder.ArcTo"/> commands tracing a circular arc of
    ///     <paramref name="radius"/> from angle <paramref name="fromAngle"/> down to
    ///     <paramref name="toAngle"/> (decreasing-angle, counter-clockwise-on-screen direction) -
    ///     the Doughnut ring's inner-radius return edge. See <see cref="AppendForwardArc"/>'s own
    ///     remarks for why large sweeps are split.
    /// </summary>
    /// <param name="builder">The path builder, positioned at the arc's start point (angle <paramref name="fromAngle"/>).</param>
    /// <param name="centerX">The arc's circle center x-coordinate.</param>
    /// <param name="centerY">The arc's circle center y-coordinate.</param>
    /// <param name="radius">The arc's radius.</param>
    /// <param name="fromAngle">The arc's start angle, in radians. Must be greater than or equal to <paramref name="toAngle"/>.</param>
    /// <param name="toAngle">The arc's end angle, in radians.</param>
    private static void AppendBackwardArc(PathBuilder builder, float centerX, float centerY, float radius, float fromAngle, float toAngle)
    {
        var sweep = fromAngle - toAngle;
        if (sweep <= MathF.PI)
        {
            var end = new Vector2(centerX + MathF.Cos(toAngle) * radius, centerY + MathF.Sin(toAngle) * radius);
            builder.ArcTo(new Vector2(radius, radius), 0f, largeArc: false, sweep: false, end);
        }
        else
        {
            var mid = (fromAngle + toAngle) / 2f;
            AppendBackwardArc(builder, centerX, centerY, radius, fromAngle, mid);
            AppendBackwardArc(builder, centerX, centerY, radius, mid, toAngle);
        }
    }
}
