using System.Globalization;
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using Path = DemaConsulting.CanvasNet.Geometry.Path;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

public static partial class ChartRenderer
{
    /// <summary>The length, in pixels, a value/category axis tick mark extends beyond the plot frame.</summary>
    private const float TickLength = 4f;

    /// <summary>The gap, in pixels, between a tick mark and its text label.</summary>
    private const float AxisLabelGap = 4f;

    /// <summary>The radius, in pixels, of a <see cref="ChartType.Line"/> point marker.</summary>
    private const float LineMarkerRadius = 3f;

    /// <summary>The stroke width, in pixels, of a <see cref="ChartType.Line"/> polyline segment and an <see cref="ChartType.Area"/> top edge.</summary>
    private const float LineStrokeWidth = 2f;

    /// <summary>The gap, in pixels, between a data point and its data label text.</summary>
    private const float DataLabelGap = 4f;

    /// <summary>The alpha channel value used for an <see cref="ChartType.Area"/> series' fill, so overlapping series/the plot grid remain visible underneath.</summary>
    private const byte AreaFillAlpha = 170;

    /// <summary>The fraction of the available plot width a vertical category-axis label column (Bar's left-side labels) may claim at most.</summary>
    private const float MaxCategoryAxisWidthFraction = 0.4f;

    /// <summary>The gridline color used for value-axis gridlines - a light gray that recedes behind the plotted data.</summary>
    private static readonly Rgba32 GridlineColor = new(224, 224, 224, 255);

    /// <summary>The color used for axis lines, tick marks, and axis tick labels - a dark, legible gray.</summary>
    private static readonly Rgba32 AxisTextColor = new(64, 64, 64, 255);

    /// <summary>The color used for data-label text.</summary>
    private static readonly Rgba32 DataLabelColor = new(0, 0, 0, 255);

    /// <summary>
    ///     Paints a Bar (<paramref name="isHorizontal"/> = <see langword="true"/>) or Column
    ///     (<paramref name="isHorizontal"/> = <see langword="false"/>) chart: grouped bars per
    ///     category, one value/category axis pass, and per-point data labels.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="plotRect">The plot-area rectangle reserved by <see cref="ComputeLayout"/>.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    /// <param name="isHorizontal"><see langword="true"/> for Bar (horizontal bars); <see langword="false"/> for Column (vertical bars).</param>
    private static void PaintBarOrColumn(RenderCanvas canvas, Chart chart, Rect plotRect, ChartRenderOptions options, TrueTypeFont font, bool isHorizontal)
    {
        if (plotRect.Width <= 0f || plotRect.Height <= 0f)
        {
            return;
        }

        var (min, max) = ComputeValueRange(chart);
        var ticks = ComputeValueTicks(chart, min, max);
        var inner = isHorizontal
            ? ComputeHorizontalFrame(chart, plotRect, options, font, ticks, out var hasCategoryAxis, out var hasValueAxis)
            : ComputeVerticalFrame(chart, plotRect, options, font, ticks, out hasCategoryAxis, out hasValueAxis);

        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            return;
        }

        if (hasValueAxis)
        {
            PaintValueAxis(canvas, inner, min, max, ticks, options, font, isHorizontal);
        }

        if (hasCategoryAxis)
        {
            PaintCategoryAxis(canvas, chart, plotRect, inner, options, font, isHorizontal);
        }

        var categoryCount = GetCategoryCount(chart);
        if (categoryCount <= 0)
        {
            return;
        }

        var seriesCount = chart.Series.Count;
        var zero = Math.Clamp(0d, min, max);

        for (var c = 0; c < categoryCount; c++)
        {
            for (var s = 0; s < seriesCount; s++)
            {
                var series = chart.Series[s];
                if (c >= series.Values.Count)
                {
                    continue;
                }

                var value = series.Values[c];
                var color = ResolveSeriesColor(chart, options, series, s);

                if (isHorizontal)
                {
                    var bandHeight = inner.Height / categoryCount;
                    var groupTop = inner.Top + c * bandHeight + bandHeight * 0.1f;
                    var barHeight = bandHeight * 0.8f / seriesCount;
                    var barTop = groupTop + s * barHeight;
                    var zeroX = ValueToPixel(zero, min, max, inner.Left, inner.Width);
                    var valueX = ValueToPixel(value, min, max, inner.Left, inner.Width);
                    var left = Math.Min(zeroX, valueX);
                    var width = Math.Abs(valueX - zeroX);
                    canvas.FillRect(left, barTop, width, barHeight, color);

                    var labelY = barTop + barHeight * 0.65f;
                    var labelAlign = value >= zero ? TextAlign.Left : TextAlign.Right;
                    var labelX = value >= zero ? valueX + DataLabelGap : valueX - DataLabelGap;
                    PaintPointDataLabel(canvas, series, c, labelX, labelY, labelAlign, options, font);
                }
                else
                {
                    var bandWidth = inner.Width / categoryCount;
                    var groupLeft = inner.Left + c * bandWidth + bandWidth * 0.1f;
                    var barWidth = bandWidth * 0.8f / seriesCount;
                    var barLeft = groupLeft + s * barWidth;
                    var zeroY = inner.Bottom - ValueToPixel(zero, min, max, 0f, inner.Height);
                    var valueY = inner.Bottom - ValueToPixel(value, min, max, 0f, inner.Height);
                    var top = Math.Min(zeroY, valueY);
                    var height = Math.Abs(valueY - zeroY);
                    canvas.FillRect(barLeft, top, barWidth, height, color);

                    var labelX = barLeft + barWidth / 2f;
                    var labelY = value >= zero ? valueY - DataLabelGap : valueY + DataLabelGap + options.DataLabelFontSize * 0.8f;
                    PaintPointDataLabel(canvas, series, c, labelX, labelY, TextAlign.Center, options, font);
                }
            }
        }
    }

    /// <summary>
    ///     Paints a Line chart: one polyline + point markers per series, a vertical value axis,
    ///     a horizontal category axis, and per-point data labels.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="plotRect">The plot-area rectangle reserved by <see cref="ComputeLayout"/>.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    private static void PaintLine(RenderCanvas canvas, Chart chart, Rect plotRect, ChartRenderOptions options, TrueTypeFont font)
    {
        if (plotRect.Width <= 0f || plotRect.Height <= 0f)
        {
            return;
        }

        var (min, max) = ComputeValueRange(chart);
        var ticks = ComputeValueTicks(chart, min, max);
        var inner = ComputeVerticalFrame(chart, plotRect, options, font, ticks, out var hasCategoryAxis, out var hasValueAxis);
        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            return;
        }

        if (hasValueAxis)
        {
            PaintValueAxis(canvas, inner, min, max, ticks, options, font, isHorizontal: false);
        }

        if (hasCategoryAxis)
        {
            PaintCategoryAxis(canvas, chart, plotRect, inner, options, font, isHorizontal: false);
        }

        var categoryCount = GetCategoryCount(chart);
        if (categoryCount <= 0)
        {
            return;
        }

        var bandWidth = inner.Width / categoryCount;
        for (var s = 0; s < chart.Series.Count; s++)
        {
            var series = chart.Series[s];
            var color = ResolveSeriesColor(chart, options, series, s);
            Vector2? previous = null;
            for (var c = 0; c < series.Values.Count; c++)
            {
                var x = inner.Left + (c + 0.5f) * bandWidth;
                var y = inner.Bottom - ValueToPixel(series.Values[c], min, max, 0f, inner.Height);
                if (previous is { } prevPoint)
                {
                    StrokeLineSegment(canvas, prevPoint, new Vector2(x, y), color);
                }

                canvas.FillCircle(x, y, LineMarkerRadius, color);
                PaintPointDataLabel(canvas, series, c, x, y - LineMarkerRadius - DataLabelGap, TextAlign.Center, options, font);
                previous = new Vector2(x, y);
            }
        }
    }

    /// <summary>
    ///     Paints an Area chart: one filled region (between each series' line and the zero
    ///     baseline) + top-edge stroke per series, a vertical value axis, a horizontal category
    ///     axis, and per-point data labels.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="plotRect">The plot-area rectangle reserved by <see cref="ComputeLayout"/>.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    private static void PaintArea(RenderCanvas canvas, Chart chart, Rect plotRect, ChartRenderOptions options, TrueTypeFont font)
    {
        if (plotRect.Width <= 0f || plotRect.Height <= 0f)
        {
            return;
        }

        var (min, max) = ComputeValueRange(chart);
        var ticks = ComputeValueTicks(chart, min, max);
        var inner = ComputeVerticalFrame(chart, plotRect, options, font, ticks, out var hasCategoryAxis, out var hasValueAxis);
        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            return;
        }

        if (hasValueAxis)
        {
            PaintValueAxis(canvas, inner, min, max, ticks, options, font, isHorizontal: false);
        }

        if (hasCategoryAxis)
        {
            PaintCategoryAxis(canvas, chart, plotRect, inner, options, font, isHorizontal: false);
        }

        var categoryCount = GetCategoryCount(chart);
        if (categoryCount <= 0)
        {
            return;
        }

        var bandWidth = inner.Width / categoryCount;
        var zeroY = inner.Bottom - ValueToPixel(Math.Clamp(0d, min, max), min, max, 0f, inner.Height);

        for (var s = 0; s < chart.Series.Count; s++)
        {
            var series = chart.Series[s];
            if (series.Values.Count == 0)
            {
                continue;
            }

            var color = ResolveSeriesColor(chart, options, series, s);
            var fillColor = new Rgba32(color.R, color.G, color.B, AreaFillAlpha);

            var points = new Vector2[series.Values.Count];
            for (var c = 0; c < series.Values.Count; c++)
            {
                var x = inner.Left + (c + 0.5f) * bandWidth;
                var y = inner.Bottom - ValueToPixel(series.Values[c], min, max, 0f, inner.Height);
                points[c] = new Vector2(x, y);
            }

            var builder = new PathBuilder().MoveTo(new Vector2(points[0].X, zeroY));
            foreach (var point in points)
            {
                builder.LineTo(point);
            }

            builder.LineTo(new Vector2(points[^1].X, zeroY)).Close();
            canvas.FillPath(builder.Build(), fillColor);

            for (var c = 1; c < points.Length; c++)
            {
                StrokeLineSegment(canvas, points[c - 1], points[c], color);
            }

            for (var c = 0; c < points.Length; c++)
            {
                PaintPointDataLabel(canvas, series, c, points[c].X, points[c].Y - DataLabelGap, TextAlign.Center, options, font);
            }
        }
    }

    /// <summary>
    ///     Draws <paramref name="series"/>' <paramref name="pointIndex"/> data label at
    ///     (<paramref name="x"/>, <paramref name="y"/>) when that series supplies
    ///     <see cref="ChartSeries.PointLabels"/> for that point - the one, documented opt-in
    ///     mechanism for data labels (there is no separate "show data labels" flag on the model).
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="series">The series the point belongs to.</param>
    /// <param name="pointIndex">The point's zero-based index.</param>
    /// <param name="x">The label's anchor x-coordinate, interpreted per <paramref name="align"/>.</param>
    /// <param name="y">The label's baseline y-coordinate.</param>
    /// <param name="align">Horizontal text alignment relative to <paramref name="x"/>.</param>
    /// <param name="options">The effective render options (supplies <see cref="ChartRenderOptions.DataLabelFontSize"/>).</param>
    /// <param name="font">The resolved text font.</param>
    private static void PaintPointDataLabel(RenderCanvas canvas, ChartSeries series, int pointIndex, float x, float y, TextAlign align, ChartRenderOptions options, TrueTypeFont font)
    {
        var label = series.PointLabels?[pointIndex];
        if (label is null)
        {
            return;
        }

        canvas.DrawText(label, x, y, align, font, options.DataLabelFontSize, DataLabelColor);
    }

    /// <summary>
    ///     Strokes a single straight segment from <paramref name="start"/> to <paramref name="end"/>.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="start">The segment's start point.</param>
    /// <param name="end">The segment's end point.</param>
    /// <param name="color">The stroke color.</param>
    private static void StrokeLineSegment(RenderCanvas canvas, Vector2 start, Vector2 end, Rgba32 color)
    {
        var path = new PathBuilder().MoveTo(start).LineTo(end).Build();
        canvas.StrokePath(path, new StrokeStyle(LineStrokeWidth), color);
    }

    /// <summary>
    ///     Linearly maps <paramref name="value"/> (clamped to [<paramref name="min"/>,
    ///     <paramref name="max"/>]) onto [<paramref name="origin"/>, <paramref name="origin"/> +
    ///     <paramref name="extent"/>].
    /// </summary>
    /// <param name="value">The data value to map.</param>
    /// <param name="min">The value-axis minimum.</param>
    /// <param name="max">The value-axis maximum. Must be strictly greater than <paramref name="min"/>.</param>
    /// <param name="origin">The pixel coordinate corresponding to <paramref name="min"/>.</param>
    /// <param name="extent">The pixel distance spanning the full [<paramref name="min"/>, <paramref name="max"/>] range.</param>
    /// <returns>The mapped pixel coordinate.</returns>
    private static float ValueToPixel(double value, double min, double max, float origin, float extent)
    {
        var clamped = Math.Clamp(value, min, max);
        return origin + (float)((clamped - min) / (max - min)) * extent;
    }

    /// <summary>
    ///     Computes the inclusive value-axis [min, max] range for <paramref name="chart"/>: the
    ///     axis' own explicit <see cref="ChartAxis.Minimum"/>/<see cref="ChartAxis.Maximum"/> when
    ///     supplied, otherwise derived from the data with a forced zero baseline (so negative
    ///     values are always visible relative to zero) and a small symmetric padding on whichever
    ///     end(s) were auto-derived.
    /// </summary>
    /// <param name="chart">The chart to compute a value range for.</param>
    /// <returns>The resolved (min, max) pair, always satisfying <c>max &gt; min</c>.</returns>
    private static (double Min, double Max) ComputeValueRange(Chart chart)
    {
        var hasExplicitMin = chart.ValueAxis?.Minimum is not null;
        var hasExplicitMax = chart.ValueAxis?.Maximum is not null;

        double dataMin = 0d;
        double dataMax = 0d;
        foreach (var series in chart.Series)
        {
            foreach (var value in series.Values)
            {
                dataMin = Math.Min(dataMin, value);
                dataMax = Math.Max(dataMax, value);
            }
        }

        var min = hasExplicitMin ? chart.ValueAxis!.Minimum!.Value : dataMin;
        var max = hasExplicitMax ? chart.ValueAxis!.Maximum!.Value : dataMax;

        // Force a zero baseline into any auto-derived bound, so a mix of positive and negative
        // values is always visually anchored to zero rather than an arbitrary data-only range.
        if (!hasExplicitMin)
        {
            min = Math.Min(0d, min);
        }

        if (!hasExplicitMax)
        {
            max = Math.Max(0d, max);
        }

        // Degenerate guard: every value (and the forced zero baseline) resolved to the same
        // number - give an auto-derived side a unit span rather than dividing by zero later.
        // ChartAxis's own constructor already rejects an explicit min >= max, so both-explicit
        // is unreachable here in practice; the final branch is defensive only.
        if (max <= min)
        {
            if (!hasExplicitMax)
            {
                max = min + 1d;
            }
            else if (!hasExplicitMin)
            {
                min = max - 1d;
            }
            else
            {
                max = min + 1d;
            }
        }

        // Guard against overflow for extreme-but-finite bounds (e.g. [-double.MaxValue,
        // double.MaxValue]): both the range itself and min-padding/max+padding can overflow to
        // +/-Infinity, which would otherwise propagate into ValueToPixel as NaN. Fall back to a
        // zero padding, and/or clamp to the nearest finite double, whenever that would occur.
        var range = max - min;
        var padding = double.IsFinite(range) ? range * 0.05d : 0d;
        if (!hasExplicitMin)
        {
            var paddedMin = min - padding;
            min = double.IsFinite(paddedMin) ? paddedMin : double.MinValue;
        }

        if (!hasExplicitMax)
        {
            var paddedMax = max + padding;
            max = double.IsFinite(paddedMax) ? paddedMax : double.MaxValue;
        }

        return (min, max);
    }

    /// <summary>
    ///     Computes a bounded, auto-"nice" tick interval candidate (1/2/5 x a power of ten) for a
    ///     [<paramref name="min"/>, <paramref name="max"/>] range, targeting approximately five
    ///     gridlines.
    /// </summary>
    /// <param name="min">The value-axis minimum.</param>
    /// <param name="max">The value-axis maximum.</param>
    /// <returns>A positive, finite interval, never producing more than 100 gridlines for the given range.</returns>
    /// <remarks>
    ///     Capped defensively (<c>range / interval</c> never exceeds 100) regardless of the 1/2/5
    ///     candidate chosen, so a pathologically large or tiny range cannot drive the gridline
    ///     loop in <see cref="ComputeValueTicks"/> into an unbounded iteration count.
    /// </remarks>
    private static double ComputeNiceInterval(double min, double max)
    {
        const int targetTicks = 5;
        const int maxTicks = 100;

        var range = max - min;
        if (!double.IsFinite(range) || range <= 0d)
        {
            return 1d;
        }

        var rawStep = range / targetTicks;
        var magnitude = Math.Pow(10d, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;
        var niceFraction = normalized switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d,
        };

        var interval = niceFraction * magnitude;
        if (!double.IsFinite(interval) || interval <= 0d || range / interval > maxTicks)
        {
            interval = range / maxTicks;
        }

        return !double.IsFinite(interval) || interval <= 0d ? range : interval;
    }

    /// <summary>
    ///     Computes the list of value-axis tick positions between <paramref name="min"/> and
    ///     <paramref name="max"/>, using <see cref="ChartAxis.TickInterval"/> when the chart's
    ///     value axis supplies one, otherwise <see cref="ComputeNiceInterval"/>.
    /// </summary>
    /// <param name="chart">The chart whose value axis may supply an explicit tick interval.</param>
    /// <param name="min">The value-axis minimum.</param>
    /// <param name="max">The value-axis maximum.</param>
    /// <returns>An ascending list of tick values, starting at <paramref name="min"/>, capped at 100 entries.</returns>
    private static List<double> ComputeValueTicks(Chart chart, double min, double max)
    {
        const int maxTicks = 100;

        var interval = chart.ValueAxis?.TickInterval ?? ComputeNiceInterval(min, max);
        if (!double.IsFinite(interval) || interval <= 0d)
        {
            interval = ComputeNiceInterval(min, max);
        }

        // Defensive hard cap applied regardless of where the interval came from (an explicit,
        // caller-supplied ChartAxis.TickInterval included) - see the companion planning report's
        // "pathological gridline loop" risk mitigation.
        if ((max - min) / interval > maxTicks)
        {
            interval = (max - min) / maxTicks;
        }

        var ticks = new List<double>();
        var value = min;
        var count = 0;
        while (value <= max + interval * 0.001d && count < maxTicks)
        {
            ticks.Add(value);
            value += interval;
            count++;
        }

        if (ticks.Count == 0)
        {
            ticks.Add(min);
        }

        return ticks;
    }

    /// <summary>
    ///     Formats a value-axis tick value for display, rounding to at most two decimal places.
    /// </summary>
    /// <param name="value">The tick value to format.</param>
    /// <returns>The formatted tick label.</returns>
    private static string FormatTickValue(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Determines the number of category slots a category-based chart has: the category
    ///     axis' own label count when <see cref="ChartAxis.Labels"/> is supplied, otherwise the
    ///     longest series' <see cref="ChartSeries.Values"/> count.
    /// </summary>
    /// <param name="chart">The chart to determine a category count for.</param>
    /// <returns>The category count (at least 1, since every series has at least one value).</returns>
    private static int GetCategoryCount(Chart chart)
    {
        if (chart.CategoryAxis?.Labels is { } labels)
        {
            return labels.Count;
        }

        var max = 0;
        foreach (var series in chart.Series)
        {
            max = Math.Max(max, series.Values.Count);
        }

        return max;
    }

    /// <summary>
    ///     Resolves the display label for category <paramref name="index"/>: the category axis'
    ///     own <see cref="ChartAxis.Labels"/> entry when available, otherwise a 1-based ordinal
    ///     fallback (<c>"1"</c>, <c>"2"</c>, ...).
    /// </summary>
    /// <param name="chart">The chart to resolve a category label from.</param>
    /// <param name="index">The category's zero-based index.</param>
    /// <returns>The resolved category label.</returns>
    private static string GetCategoryLabel(Chart chart, int index)
    {
        var labels = chart.CategoryAxis?.Labels;
        return labels is not null && index < labels.Count
            ? labels[index]
            : (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Truncates <paramref name="text"/> with a trailing <c>"..."</c> ellipsis so it measures
    ///     no wider than <paramref name="maxWidth"/>, the documented behavior for the "very long
    ///     label text" edge case (rather than painting outside its allotted band or throwing).
    /// </summary>
    /// <param name="text">The candidate text.</param>
    /// <param name="font">The font the text will be measured/drawn with.</param>
    /// <param name="fontSize">The font size, in pixels.</param>
    /// <param name="maxWidth">The maximum allowed measured width, in pixels.</param>
    /// <returns>
    ///     <paramref name="text"/> unchanged when it already fits; otherwise a truncated,
    ///     ellipsis-suffixed prefix that fits; or <see cref="string.Empty"/> when
    ///     <paramref name="maxWidth"/> is non-positive or too small for even the ellipsis alone.
    /// </returns>
    private static string TruncateToFit(string text, TrueTypeFont font, float fontSize, float maxWidth)
    {
        if (maxWidth <= 0f)
        {
            return string.Empty;
        }

        if (TextRenderer.MeasureText(text, font, fontSize).Width <= maxWidth)
        {
            return text;
        }

        for (var length = text.Length - 1; length >= 0; length--)
        {
            var candidate = text[..length] + "...";
            if (TextRenderer.MeasureText(candidate, font, fontSize).Width <= maxWidth)
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    /// <summary>
    ///     Computes the Column/Line/Area plot frame: a left margin reserved for the vertical
    ///     value axis (sized from the widest tick label) and a bottom margin reserved for the
    ///     horizontal category axis (a fixed text-line height) - each skipped (and its own
    ///     <see langword="out"/> flag left <see langword="false"/>) when reserving it would leave
    ///     a non-positive remaining dimension, per the small-render-target graceful-degradation
    ///     behavior documented on <see cref="ComputeLayout"/>'s own <c>ChartLayout</c>.
    /// </summary>
    /// <param name="chart">The chart being laid out.</param>
    /// <param name="plotRect">The full plot-area rectangle.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font, used to measure tick label widths.</param>
    /// <param name="ticks">The value-axis tick values (see <see cref="ComputeValueTicks"/>).</param>
    /// <param name="hasCategoryAxis">Receives whether the bottom category-axis band was actually reserved.</param>
    /// <param name="hasValueAxis">Receives whether the left value-axis band was actually reserved.</param>
    /// <returns>The inner data rectangle, after reserving whichever bands fit.</returns>
    private static Rect ComputeVerticalFrame(
        Chart chart, Rect plotRect, ChartRenderOptions options, TrueTypeFont font, List<double> ticks,
        out bool hasCategoryAxis, out bool hasValueAxis)
    {
        var maxTickLabelWidth = 0f;
        foreach (var tick in ticks)
        {
            var width = TextRenderer.MeasureText(FormatTickValue(tick), font, options.AxisFontSize).Width;
            maxTickLabelWidth = MathF.Max(maxTickLabelWidth, width);
        }

        var leftMargin = maxTickLabelWidth + TickLength + AxisLabelGap * 2f;
        hasValueAxis = leftMargin > 0f && plotRect.Width - leftMargin > 0f;
        if (!hasValueAxis)
        {
            leftMargin = 0f;
        }

        var bottomMargin = options.AxisFontSize * 1.4f + TickLength + AxisLabelGap;
        var categoryCount = GetCategoryCount(chart);
        hasCategoryAxis = categoryCount > 0 && bottomMargin > 0f && plotRect.Height - bottomMargin > 0f;
        if (!hasCategoryAxis)
        {
            bottomMargin = 0f;
        }

        return new Rect(plotRect.X + leftMargin, plotRect.Y, plotRect.Width - leftMargin, plotRect.Height - bottomMargin);
    }

    /// <summary>
    ///     Computes the Bar plot frame: a bottom margin reserved for the horizontal value axis
    ///     (a fixed text-line height) and a left margin reserved for the vertical category axis
    ///     (sized from the widest category label, capped to
    ///     <see cref="MaxCategoryAxisWidthFraction"/> of the plot width) - each skipped (and its
    ///     own <see langword="out"/> flag left <see langword="false"/>) when reserving it would
    ///     leave a non-positive remaining dimension. See <see cref="ComputeVerticalFrame"/>'s own
    ///     remarks for the shared small-render-target rationale.
    /// </summary>
    /// <param name="chart">The chart being laid out.</param>
    /// <param name="plotRect">The full plot-area rectangle.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font, used to measure tick/category label widths.</param>
    /// <param name="ticks">The value-axis tick values (see <see cref="ComputeValueTicks"/>).</param>
    /// <param name="hasCategoryAxis">Receives whether the left category-axis band was actually reserved.</param>
    /// <param name="hasValueAxis">Receives whether the bottom value-axis band was actually reserved.</param>
    /// <returns>The inner data rectangle, after reserving whichever bands fit.</returns>
    private static Rect ComputeHorizontalFrame(
        Chart chart, Rect plotRect, ChartRenderOptions options, TrueTypeFont font, List<double> ticks,
        out bool hasCategoryAxis, out bool hasValueAxis)
    {
        _ = ticks;
        var bottomMargin = options.AxisFontSize * 1.4f + TickLength + AxisLabelGap;
        hasValueAxis = bottomMargin > 0f && plotRect.Height - bottomMargin > 0f;
        if (!hasValueAxis)
        {
            bottomMargin = 0f;
        }

        var categoryCount = GetCategoryCount(chart);
        var maxCategoryLabelWidth = 0f;
        for (var c = 0; c < categoryCount; c++)
        {
            var width = TextRenderer.MeasureText(GetCategoryLabel(chart, c), font, options.AxisFontSize).Width;
            maxCategoryLabelWidth = MathF.Max(maxCategoryLabelWidth, width);
        }

        var leftMargin = MathF.Min(
            maxCategoryLabelWidth + TickLength + AxisLabelGap * 2f,
            plotRect.Width * MaxCategoryAxisWidthFraction);
        hasCategoryAxis = categoryCount > 0 && leftMargin > 0f && plotRect.Width - leftMargin > 0f;
        if (!hasCategoryAxis)
        {
            leftMargin = 0f;
        }

        return new Rect(plotRect.X + leftMargin, plotRect.Y, plotRect.Width - leftMargin, plotRect.Height - bottomMargin);
    }

    /// <summary>
    ///     Paints value-axis gridlines, tick marks, and tick labels across <paramref name="inner"/>.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="inner">The inner data rectangle (see <see cref="ComputeVerticalFrame"/>/<see cref="ComputeHorizontalFrame"/>).</param>
    /// <param name="min">The value-axis minimum.</param>
    /// <param name="max">The value-axis maximum.</param>
    /// <param name="ticks">The tick values to paint, from <see cref="ComputeValueTicks"/>.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    /// <param name="isHorizontal"><see langword="true"/> for Bar's horizontal (bottom) value axis; <see langword="false"/> for a vertical (left) value axis.</param>
    private static void PaintValueAxis(RenderCanvas canvas, Rect inner, double min, double max, List<double> ticks, ChartRenderOptions options, TrueTypeFont font, bool isHorizontal)
    {
        foreach (var tick in ticks)
        {
            var label = FormatTickValue(tick);
            if (isHorizontal)
            {
                var x = ValueToPixel(tick, min, max, inner.Left, inner.Width);
                StrokeLineSegment(canvas, new Vector2(x, inner.Top), new Vector2(x, inner.Bottom), GridlineColor);
                StrokeLineSegment(canvas, new Vector2(x, inner.Bottom), new Vector2(x, inner.Bottom + TickLength), AxisTextColor);
                canvas.DrawText(label, x, inner.Bottom + TickLength + AxisLabelGap + options.AxisFontSize * 0.8f, TextAlign.Center, font, options.AxisFontSize, AxisTextColor);
            }
            else
            {
                var y = inner.Bottom - ValueToPixel(tick, min, max, 0f, inner.Height);
                StrokeLineSegment(canvas, new Vector2(inner.Left, y), new Vector2(inner.Right, y), GridlineColor);
                StrokeLineSegment(canvas, new Vector2(inner.Left - TickLength, y), new Vector2(inner.Left, y), AxisTextColor);
                canvas.DrawText(label, inner.Left - TickLength - AxisLabelGap, y + options.AxisFontSize * 0.3f, TextAlign.Right, font, options.AxisFontSize, AxisTextColor);
            }
        }
    }

    /// <summary>
    ///     Paints category-axis tick marks and (truncated-to-fit) tick labels across
    ///     <paramref name="inner"/>.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="chart">The chart supplying category labels (see <see cref="GetCategoryLabel"/>).</param>
    /// <param name="plotRect">The full plot-area rectangle, used to recover the reserved category-axis band width/height for truncation.</param>
    /// <param name="inner">The inner data rectangle.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    /// <param name="isHorizontal"><see langword="true"/> for Bar's vertical (left) category axis; <see langword="false"/> for a horizontal (bottom) category axis.</param>
    private static void PaintCategoryAxis(RenderCanvas canvas, Chart chart, Rect plotRect, Rect inner, ChartRenderOptions options, TrueTypeFont font, bool isHorizontal)
    {
        var categoryCount = GetCategoryCount(chart);
        if (categoryCount <= 0)
        {
            return;
        }

        if (isHorizontal)
        {
            var bandHeight = inner.Height / categoryCount;
            var labelBudget = inner.Left - plotRect.X - TickLength - AxisLabelGap * 2f;
            for (var c = 0; c < categoryCount; c++)
            {
                var y = inner.Top + (c + 0.5f) * bandHeight;
                var label = TruncateToFit(GetCategoryLabel(chart, c), font, options.AxisFontSize, labelBudget);
                StrokeLineSegment(canvas, new Vector2(inner.Left - TickLength, y), new Vector2(inner.Left, y), AxisTextColor);
                canvas.DrawText(label, inner.Left - TickLength - AxisLabelGap, y + options.AxisFontSize * 0.3f, TextAlign.Right, font, options.AxisFontSize, AxisTextColor);
            }
        }
        else
        {
            var bandWidth = inner.Width / categoryCount;
            for (var c = 0; c < categoryCount; c++)
            {
                var x = inner.Left + (c + 0.5f) * bandWidth;
                var label = TruncateToFit(GetCategoryLabel(chart, c), font, options.AxisFontSize, bandWidth - AxisLabelGap);
                StrokeLineSegment(canvas, new Vector2(x, inner.Bottom), new Vector2(x, inner.Bottom + TickLength), AxisTextColor);
                canvas.DrawText(label, x, inner.Bottom + TickLength + AxisLabelGap + options.AxisFontSize * 0.8f, TextAlign.Center, font, options.AxisFontSize, AxisTextColor);
            }
        }
    }
}
