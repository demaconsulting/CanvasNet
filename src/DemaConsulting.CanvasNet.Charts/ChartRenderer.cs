using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Renders an immutable <see cref="Chart"/> description onto a pixel <see cref="Surface"/>.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="ChartRenderer"/> is this library's second public unit (alongside the Phase 1
///     data model/<see cref="ChartBuilder"/>): a <see langword="static"/> entry point, mirroring
///     <c>DemaConsulting.CanvasNet.Pptx.PptxDocument</c>'s own <c>Render</c> shape, that paints a
///     <see cref="Chart"/> - built entirely in memory via <see cref="ChartBuilder"/> or the model
///     constructors directly, with no document-format parsing involved - onto a new
///     <see cref="Surface"/> the caller owns and must <see cref="Surface.Dispose"/>.
///     </para>
///     <para>
///     Rendering is dispatched by <see cref="Chart.Type"/>: <see cref="ChartType.Bar"/>/
///     <see cref="ChartType.Column"/>/<see cref="ChartType.Line"/>/<see cref="ChartType.Area"/>
///     share one value-range/axis/data-label implementation (see
///     <c>ChartRenderer.CategoryChart.cs</c>); <see cref="ChartType.Pie"/>/
///     <see cref="ChartType.Doughnut"/> share one wedge-layout implementation (see
///     <c>ChartRenderer.PieChart.cs</c>). Every chart type shares one title pass
///     (<c>ChartRenderer.Title.cs</c>) and one legend pass (<c>ChartRenderer.Legend.cs</c>), and
///     all share the band layout computed once up front (<c>ChartRenderer.Layout.cs</c>).
///     </para>
/// </remarks>
public static partial class ChartRenderer
{
    /// <summary>
    ///     Renders <paramref name="chart"/> onto a new <paramref name="width"/> x
    ///     <paramref name="height"/> pixel <see cref="Surface"/>.
    /// </summary>
    /// <param name="chart">The chart to render. Must not be <see langword="null"/>.</param>
    /// <param name="width">The render target width, in pixels. Must be a valid <see cref="Surface"/> size (1-8192).</param>
    /// <param name="height">The render target height, in pixels. Must be a valid <see cref="Surface"/> size (1-8192).</param>
    /// <param name="options">
    ///     Rendering configuration, or <see langword="null"/> (the default) to use
    ///     <see cref="ChartRenderOptions.Default"/>.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/>, owned by the caller, cleared to
    ///     <paramref name="options"/>' <see cref="ChartRenderOptions.BackgroundColor"/> and then
    ///     painted with <paramref name="chart"/>'s content.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="width"/>/<paramref name="height"/> is not a valid
    ///     <see cref="Surface"/> size (propagated directly from <see cref="Surface"/>'s own
    ///     constructor - this method performs no separate re-validation of its own).
    /// </exception>
    public static Surface Render(Chart chart, int width, int height, ChartRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var effectiveOptions = options ?? ChartRenderOptions.Default;

        // Surface's own constructor validates the requested width/height range and throws when
        // invalid; propagate that exception directly rather than duplicating the same check here.
        var surface = new Surface(width, height);
        try
        {
            surface.Clear(effectiveOptions.BackgroundColor);
            var canvas = new RenderCanvas(surface);
            var font = effectiveOptions.Font ?? SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);

            var layout = ComputeLayout(chart, width, height, effectiveOptions, font);

            // Dispatch to the per-ChartType painter first, so axes/wedges sit beneath the title
            // and legend passes that follow - matching normal chart-drawing (and PptxDocument
            // slide-background-then-shapes) document-order-of-paint conventions.
            switch (chart.Type)
            {
                case ChartType.Bar:
                    PaintBarOrColumn(canvas, chart, layout.PlotRect, effectiveOptions, font, isHorizontal: true);
                    break;
                case ChartType.Column:
                    PaintBarOrColumn(canvas, chart, layout.PlotRect, effectiveOptions, font, isHorizontal: false);
                    break;
                case ChartType.Line:
                    PaintLine(canvas, chart, layout.PlotRect, effectiveOptions, font);
                    break;
                case ChartType.Area:
                    PaintArea(canvas, chart, layout.PlotRect, effectiveOptions, font);
                    break;
                case ChartType.Pie:
                    PaintPieOrDoughnut(canvas, chart, layout.PlotRect, effectiveOptions, font, isDoughnut: false);
                    break;
                case ChartType.Doughnut:
                    PaintPieOrDoughnut(canvas, chart, layout.PlotRect, effectiveOptions, font, isDoughnut: true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(chart), chart.Type, "Chart.Type must be a defined ChartType value.");
            }

            if (layout.HasTitle)
            {
                PaintTitle(canvas, chart, layout.TitleRect, effectiveOptions, font);
            }

            if (layout.HasLegend)
            {
                PaintLegend(canvas, chart, layout.LegendRect, effectiveOptions, font);
            }
        }
        catch
        {
            surface.Dispose();
            throw;
        }

        return surface;
    }

    /// <summary>
    ///     Convenience overload of <see cref="Render(Chart, int, int, ChartRenderOptions?)"/> for
    ///     the common "render at a given physical size and resolution" case.
    /// </summary>
    /// <param name="chart">The chart to render. Must not be <see langword="null"/>.</param>
    /// <param name="widthInches">The desired render width, in inches. Must be finite and positive.</param>
    /// <param name="heightInches">The desired render height, in inches. Must be finite and positive.</param>
    /// <param name="dpi">The resolution, in pixels per inch. Must be finite and positive.</param>
    /// <param name="options">
    ///     Rendering configuration, or <see langword="null"/> (the default) to use
    ///     <see cref="ChartRenderOptions.Default"/>.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> of size <c>round(widthInches * dpi)</c> x
    ///     <c>round(heightInches * dpi)</c> pixels, painted exactly as
    ///     <see cref="Render(Chart, int, int, ChartRenderOptions?)"/> documents.
    /// </returns>
    /// <remarks>
    ///     Unlike <c>PptxDocument.Render(int, float, PptxRenderOptions?)</c> or
    ///     <c>PdfDocument.Render(int, float, PdfRenderOptions?)</c>, a <see cref="Chart"/> carries
    ///     no intrinsic physical page/slide size of its own to scale a pixel size from - it is a
    ///     pure in-memory data description, not a document page. This overload therefore takes
    ///     explicit <paramref name="widthInches"/>/<paramref name="heightInches"/> parameters in
    ///     addition to <paramref name="dpi"/>, a deliberate, documented reinterpretation of the
    ///     "DPI overload mirrors existing document renderers" convention for a data model that has
    ///     no physical size to mirror it from.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="widthInches"/>, <paramref name="heightInches"/>, or
    ///     <paramref name="dpi"/> is not finite or is not positive, or when the computed pixel
    ///     width/height is not a valid <see cref="Surface"/> size.
    /// </exception>
    public static Surface Render(Chart chart, float widthInches, float heightInches, float dpi, ChartRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (!float.IsFinite(widthInches) || widthInches <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(widthInches), widthInches, "Width in inches must be a finite, positive value.");
        }

        if (!float.IsFinite(heightInches) || heightInches <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(heightInches), heightInches, "Height in inches must be a finite, positive value.");
        }

        if (!float.IsFinite(dpi) || dpi <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be a finite, positive value.");
        }

        var widthPixels = widthInches * dpi;
        var heightPixels = heightInches * dpi;
        if (!float.IsFinite(widthPixels) || !float.IsFinite(heightPixels) ||
            widthPixels > Surface.MaxDimension || heightPixels > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dpi), dpi,
                $"The computed pixel dimensions must not exceed {Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        var width = (int)Math.Round(widthPixels, MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(heightPixels, MidpointRounding.AwayFromZero);
        return Render(chart, width, height, options);
    }

    /// <summary>
    ///     Determines whether <paramref name="type"/> associates each series' values one-for-one
    ///     with a category axis position (Bar/Column/Line/Area), as opposed to a proportional
    ///     wedge share (Pie/Doughnut). Mirrors <c>Chart</c>'s own private identically-named
    ///     check, since <see cref="ChartRenderer"/> needs the same classification for dispatch
    ///     and legend-entry construction but cannot reuse <c>Chart</c>'s private helper directly.
    /// </summary>
    /// <param name="type">The chart type to classify.</param>
    /// <returns><see langword="true"/> for a category-based type; otherwise, <see langword="false"/>.</returns>
    private static bool IsCategoryBased(ChartType type) =>
        type is ChartType.Bar or ChartType.Column or ChartType.Line or ChartType.Area;

    /// <summary>
    ///     Resolves a series' effective paint color: its own explicit
    ///     <see cref="ChartSeries.Color"/> when set, otherwise a palette color for
    ///     <paramref name="seriesIndex"/> (see <see cref="ResolvePaletteColor"/>).
    /// </summary>
    /// <param name="chart">The chart <paramref name="series"/> belongs to (supplies <see cref="Chart.ColorPalette"/>).</param>
    /// <param name="options">The effective render options (supplies <see cref="ChartRenderOptions.ColorPalette"/>).</param>
    /// <param name="series">The series to resolve a color for.</param>
    /// <param name="seriesIndex">The series' zero-based index within <see cref="Chart.Series"/>.</param>
    /// <returns>The resolved, fully opaque-or-as-specified color.</returns>
    private static Rgba32 ResolveSeriesColor(Chart chart, ChartRenderOptions options, ChartSeries series, int seriesIndex) =>
        series.Color ?? ResolvePaletteColor(chart, options, seriesIndex);

    /// <summary>
    ///     Resolves a single data point's effective paint color: its series' own explicit
    ///     <see cref="ChartSeries.PointColors"/> entry when set, otherwise a palette color for
    ///     <paramref name="pointIndex"/> (see <see cref="ResolvePaletteColor"/>).
    /// </summary>
    /// <param name="chart">The chart <paramref name="series"/> belongs to (supplies <see cref="Chart.ColorPalette"/>).</param>
    /// <param name="options">The effective render options (supplies <see cref="ChartRenderOptions.ColorPalette"/>).</param>
    /// <param name="series">The series the point belongs to.</param>
    /// <param name="pointIndex">The point's zero-based index within <see cref="ChartSeries.Values"/>.</param>
    /// <returns>The resolved, fully opaque-or-as-specified color.</returns>
    private static Rgba32 ResolvePointColor(Chart chart, ChartRenderOptions options, ChartSeries series, int pointIndex) =>
        series.PointColors?[pointIndex] ?? ResolvePaletteColor(chart, options, pointIndex);

    /// <summary>
    ///     Resolves the palette color for <paramref name="index"/>, the single shared helper every
    ///     per-series/per-point color resolution and legend-swatch painting goes through - so the
    ///     "which palette, and how does the index wrap" decision can never drift between painters
    ///     (see the companion planning report's color-resolution risk mitigation).
    /// </summary>
    /// <param name="chart">The chart being rendered (checked first for its own <see cref="Chart.ColorPalette"/>).</param>
    /// <param name="options">
    ///     The effective render options (checked second for its own
    ///     <see cref="ChartRenderOptions.ColorPalette"/>).
    /// </param>
    /// <param name="index">The series/point index to resolve a color for.</param>
    /// <returns>
    ///     <c>chart.ColorPalette[index % chart.ColorPalette.Count]</c> when
    ///     <see cref="Chart.ColorPalette"/> is non-null and non-empty; otherwise
    ///     <c>options.ColorPalette[index % options.ColorPalette.Count]</c> when that is non-null
    ///     and non-empty; otherwise <c>ChartColorPalette.Default[index % ChartColorPalette.Default.Count]</c>.
    /// </returns>
    private static Rgba32 ResolvePaletteColor(Chart chart, ChartRenderOptions options, int index)
    {
        IReadOnlyList<Rgba32> palette;
        if (chart.ColorPalette is { Count: > 0 } chartPalette)
        {
            palette = chartPalette;
        }
        else if (options.ColorPalette is { Count: > 0 } optionsPalette)
        {
            palette = optionsPalette;
        }
        else
        {
            palette = ChartColorPalette.Default;
        }

        // index is wrapped via modulo (never clamped or out-of-range) so a chart with more
        // series/points than palette entries simply repeats colors from the start, matching
        // ChartColorPalette's own documented wrapping convention.
        var wrapped = index % palette.Count;
        return palette[wrapped];
    }
}
