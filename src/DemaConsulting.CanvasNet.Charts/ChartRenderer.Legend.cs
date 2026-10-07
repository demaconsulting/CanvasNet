using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

public static partial class ChartRenderer
{
    /// <summary>
    ///     Describes one legend entry: the label text painted beside a color swatch, and the
    ///     swatch's own fill color.
    /// </summary>
    /// <param name="Label">The entry's label text.</param>
    /// <param name="Color">The entry's swatch color.</param>
    private readonly record struct LegendEntry(string Label, Rgba32 Color);

    /// <summary>
    ///     Builds the ordered list of legend entries for <paramref name="chart"/>: one entry per
    ///     series for category-based types (<see cref="IsCategoryBased"/>), or one entry per data
    ///     point in <c>chart.Series[0]</c> for Pie/Doughnut.
    /// </summary>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="options">The effective render options.</param>
    /// <returns>
    ///     The legend entries, in the same order <see cref="ComputeLayout"/>/<see cref="PaintLegend"/>
    ///     will lay them out. Empty when <paramref name="chart"/> has no series.
    /// </returns>
    /// <remarks>
    ///     For Pie/Doughnut, a point's label is its series' <c>PointLabels[i]</c> entry when
    ///     present, otherwise the literal fallback <c>"Series {i + 1}"</c> (1-based) - this exact,
    ///     slightly unusual fallback wording is specified by the companion planning report and is
    ///     intentional, not a naming oversight: a Pie/Doughnut's "series" in the legend sense is
    ///     really each wedge/point, not <see cref="Chart.Series"/> itself.
    /// </remarks>
    private static IReadOnlyList<LegendEntry> GetLegendEntries(Chart chart, ChartRenderOptions options)
    {
        if (chart.Series.Count == 0)
        {
            return [];
        }

        var entries = new List<LegendEntry>();
        if (IsCategoryBased(chart.Type))
        {
            for (var s = 0; s < chart.Series.Count; s++)
            {
                var series = chart.Series[s];
                entries.Add(new LegendEntry(series.Name, ResolveSeriesColor(chart, options, series, s)));
            }
        }
        else
        {
            var series = chart.Series[0];
            for (var i = 0; i < series.Values.Count; i++)
            {
                var label = series.PointLabels?[i] ?? $"Series {i + 1}";
                entries.Add(new LegendEntry(label, ResolvePointColor(chart, options, series, i)));
            }
        }

        return entries;
    }

    /// <summary>
    ///     Paints the legend band computed by <see cref="ComputeLayout"/>: a swatch followed by
    ///     its label, for each entry returned by <see cref="GetLegendEntries"/>.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="legendRect">The legend band's rectangle, as computed by <see cref="ComputeLayout"/>.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    /// <remarks>
    ///     Top/Bottom legends flow entries left-to-right on a single centered row; Left/Right
    ///     legends stack entries in a single left-aligned column. Neither layout wraps onto
    ///     additional rows/columns: once an entry would overflow the band's bounds, it (and every
    ///     entry after it) is simply omitted - the documented "many series" graceful-overflow
    ///     behavior (no entry is ever drawn clipped or outside the band).
    /// </remarks>
    private static void PaintLegend(RenderCanvas canvas, Chart chart, Rect legendRect, ChartRenderOptions options, TrueTypeFont font)
    {
        if (legendRect.Width <= 0f || legendRect.Height <= 0f)
        {
            return;
        }

        var entries = GetLegendEntries(chart, options);
        if (entries.Count == 0)
        {
            return;
        }

        var isHorizontal = chart.Legend!.Position is ChartLegendPosition.Top or ChartLegendPosition.Bottom;
        if (isHorizontal)
        {
            PaintHorizontalLegend(canvas, entries, legendRect, options, font);
        }
        else
        {
            PaintVerticalLegend(canvas, entries, legendRect, options, font);
        }
    }

    /// <summary>
    ///     Paints a single centered row of swatch+label entries flowing left-to-right, for a
    ///     Top/Bottom legend band. See <see cref="PaintLegend"/>'s remarks for the
    ///     overflow-by-omission behavior.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="entries">The legend entries to paint.</param>
    /// <param name="legendRect">The legend band's rectangle.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    private static void PaintHorizontalLegend(RenderCanvas canvas, IReadOnlyList<LegendEntry> entries, Rect legendRect, ChartRenderOptions options, TrueTypeFont font)
    {
        // Measure every entry's full width (swatch + gap + label) up front so the whole row of
        // entries that actually fit can be centered as a group, matching how a real chart legend
        // visually balances within its band.
        var widths = new float[entries.Count];
        var totalWidth = 0f;
        var fittingCount = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var labelWidth = TextRenderer.MeasureText(entries[i].Label, font, options.LegendFontSize).Width;
            widths[i] = LegendSwatchSize + LegendEntryGap + labelWidth;
            var candidateWidth = totalWidth + widths[i] + (fittingCount > 0 ? LegendEntryGap : 0f);
            if (candidateWidth > legendRect.Width - LegendBandPadding * 2f)
            {
                break;
            }

            totalWidth = candidateWidth;
            fittingCount++;
        }

        if (fittingCount == 0)
        {
            return;
        }

        var x = legendRect.X + (legendRect.Width - totalWidth) / 2f;
        var centerY = legendRect.Y + legendRect.Height / 2f;
        for (var i = 0; i < fittingCount; i++)
        {
            PaintLegendEntry(canvas, entries[i], x, centerY - LegendSwatchSize / 2f, options, font);
            x += widths[i] + LegendEntryGap;
        }
    }

    /// <summary>
    ///     Paints a single left-aligned column of swatch+label entries stacked top-to-bottom, for
    ///     a Left/Right legend band. See <see cref="PaintLegend"/>'s remarks for the
    ///     overflow-by-omission behavior.
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="entries">The legend entries to paint.</param>
    /// <param name="legendRect">The legend band's rectangle.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    private static void PaintVerticalLegend(RenderCanvas canvas, IReadOnlyList<LegendEntry> entries, Rect legendRect, ChartRenderOptions options, TrueTypeFont font)
    {
        var rowHeight = MathF.Max(LegendSwatchSize, options.LegendFontSize) + LegendRowGap;
        var totalHeight = entries.Count * rowHeight - LegendRowGap;
        var x = legendRect.X + LegendBandPadding;
        var y = legendRect.Y + MathF.Max(LegendBandPadding, (legendRect.Height - totalHeight) / 2f);

        foreach (var entry in entries)
        {
            if (y + LegendSwatchSize > legendRect.Bottom - LegendBandPadding)
            {
                break;
            }

            PaintLegendEntry(canvas, entry, x, y, options, font);
            y += rowHeight;
        }
    }

    /// <summary>
    ///     Paints one legend entry's swatch square followed by its label, with the swatch's
    ///     top-left corner at (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    /// <param name="canvas">The target canvas.</param>
    /// <param name="entry">The entry to paint.</param>
    /// <param name="x">The swatch's left x-coordinate.</param>
    /// <param name="y">The swatch's top y-coordinate.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved text font.</param>
    private static void PaintLegendEntry(RenderCanvas canvas, LegendEntry entry, float x, float y, ChartRenderOptions options, TrueTypeFont font)
    {
        canvas.FillRect(x, y, LegendSwatchSize, LegendSwatchSize, entry.Color);
        var textX = x + LegendSwatchSize + LegendEntryGap;
        var textY = y + LegendSwatchSize / 2f + options.LegendFontSize * 0.35f;
        canvas.DrawText(entry.Label, textX, textY, TextAlign.Left, font, options.LegendFontSize, AxisTextColor);
    }
}
