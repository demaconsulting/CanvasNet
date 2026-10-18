using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.Charts;

public static partial class ChartRenderer
{
    /// <summary>
    ///     Vertical padding, in pixels, added above/below a title's text when sizing its band.
    /// </summary>
    private const float TitleBandPadding = 8f;

    /// <summary>
    ///     Padding, in pixels, added around a legend band's content (both the swatch/label rows
    ///     for Top/Bottom and the stacked column for Left/Right).
    /// </summary>
    private const float LegendBandPadding = 8f;

    /// <summary>
    ///     The side length, in pixels, of a legend entry's color swatch square.
    /// </summary>
    private const float LegendSwatchSize = 12f;

    /// <summary>
    ///     Horizontal gap, in pixels, between a legend entry's swatch and its label text, and
    ///     between consecutive entries flowing on the same row (Top/Bottom legends).
    /// </summary>
    private const float LegendEntryGap = 6f;

    /// <summary>
    ///     Vertical gap, in pixels, between consecutive legend entries stacked in one column
    ///     (Left/Right legends).
    /// </summary>
    private const float LegendRowGap = 4f;

    /// <summary>
    ///     The maximum fraction of the full render width a Left/Right legend band may claim,
    ///     regardless of how wide its longest label measures - a defensive cap so one
    ///     pathologically long legend label cannot collapse the plot area to nothing (see the
    ///     "very long label text" edge case).
    /// </summary>
    private const float MaxLegendWidthFraction = 0.4f;

    /// <summary>
    ///     Describes the three rectangular regions a <see cref="Render(Chart, int, int, ChartRenderOptions?)"/>
    ///     call paints into, computed once up front and shared by every per-<see cref="ChartType"/>
    ///     painter and the title/legend passes.
    /// </summary>
    /// <remarks>
    ///     Bands are reserved in a fixed order - title first, then legend - so that, on a very
    ///     small render target, the plot area is the band that shrinks (and is ultimately omitted)
    ///     before either decorative band is; this matches the documented small-render-target
    ///     graceful-degradation behavior (see the companion design doc). A band whose reservation
    ///     would leave a non-positive width or height for the remaining area is skipped entirely
    ///     (its own rectangle becomes <see cref="Rect.Empty"/>) rather than reserving a
    ///     degenerate sliver - the corresponding <c>Has*</c> flag reports whether the band was
    ///     actually reserved.
    /// </remarks>
    private readonly struct ChartLayout
    {
        /// <summary>
        ///     The title band's rectangle, or <see cref="Rect.Empty"/> when <see cref="HasTitle"/>
        ///     is <see langword="false"/>.
        /// </summary>
        public Rect TitleRect { get; }

        /// <summary>
        ///     The legend band's rectangle, or <see cref="Rect.Empty"/> when
        ///     <see cref="HasLegend"/> is <see langword="false"/>.
        /// </summary>
        public Rect LegendRect { get; }

        /// <summary>
        ///     The remaining plot-area rectangle, after any title/legend bands were reserved. May
        ///     itself have a non-positive width or height on a sufficiently small render target;
        ///     every painter must tolerate that (relying on
        ///     <see cref="DemaConsulting.CanvasNet.Geometry.Path.Rectangle"/>/
        ///     <see cref="DemaConsulting.CanvasNet.Geometry.Path.Circle"/>'s own documented
        ///     no-op-on-non-positive-size behavior) rather than assuming a usable plot area.
        /// </summary>
        public Rect PlotRect { get; }

        /// <summary><see langword="true"/> if a title band was actually reserved in <see cref="TitleRect"/>.</summary>
        public bool HasTitle { get; }

        /// <summary><see langword="true"/> if a legend band was actually reserved in <see cref="LegendRect"/>.</summary>
        public bool HasLegend { get; }

        /// <summary>
        ///     Initializes a new, fully computed <see cref="ChartLayout"/>.
        /// </summary>
        /// <param name="titleRect">See <see cref="TitleRect"/>.</param>
        /// <param name="legendRect">See <see cref="LegendRect"/>.</param>
        /// <param name="plotRect">See <see cref="PlotRect"/>.</param>
        /// <param name="hasTitle">See <see cref="HasTitle"/>.</param>
        /// <param name="hasLegend">See <see cref="HasLegend"/>.</param>
        public ChartLayout(Rect titleRect, Rect legendRect, Rect plotRect, bool hasTitle, bool hasLegend)
        {
            TitleRect = titleRect;
            LegendRect = legendRect;
            PlotRect = plotRect;
            HasTitle = hasTitle;
            HasLegend = hasLegend;
        }
    }

    /// <summary>
    ///     Computes the title/legend/plot-area band layout for a <paramref name="width"/> x
    ///     <paramref name="height"/> render target, defensively skipping any band that would
    ///     leave a non-positive remaining area.
    /// </summary>
    /// <param name="chart">The chart being rendered. Must not be <see langword="null"/>.</param>
    /// <param name="width">The full render target width, in pixels. Must be positive.</param>
    /// <param name="height">The full render target height, in pixels. Must be positive.</param>
    /// <param name="options">The effective render options (never <see langword="null"/> by the time this is called).</param>
    /// <param name="font">The resolved font used to measure title/legend text extents.</param>
    /// <returns>The computed <see cref="ChartLayout"/>.</returns>
    private static ChartLayout ComputeLayout(Chart chart, int width, int height, ChartRenderOptions options, TrueTypeFont font)
    {
        var remaining = new Rect(0f, 0f, width, height);

        // Reserve the title band first (top of the remaining area) - only when a title is
        // present and doing so leaves a positive-height remainder.
        var hasTitle = TryReserveTitleBand(chart, options, ref remaining, out var titleRect);

        // Reserve the legend band second, from whichever edge of the remaining area its
        // ChartLegendPosition indicates - only when the legend is visible, has a real position,
        // has at least one entry to show, and doing so leaves a positive plot area.
        var hasLegend = TryReserveLegendBand(chart, options, font, ref remaining, out var legendRect);

        return new ChartLayout(titleRect, legendRect, remaining, hasTitle, hasLegend);
    }

    /// <summary>
    ///     Reserves the title band at the top of <paramref name="remaining"/> when a title is present
    ///     and doing so leaves a positive-height remainder.
    /// </summary>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="remaining">The remaining area; shrunk when the band is reserved.</param>
    /// <param name="titleRect">Receives the reserved band, or <see cref="Rect.Empty"/>.</param>
    /// <returns><see langword="true"/> when the band was reserved.</returns>
    private static bool TryReserveTitleBand(Chart chart, ChartRenderOptions options, ref Rect remaining, out Rect titleRect)
    {
        titleRect = Rect.Empty;
        if (chart.Title is null)
        {
            return false;
        }

        var titleFontSize = chart.Title.FontSize ?? options.TitleFontSize;
        var titleHeight = titleFontSize * 1.6f + TitleBandPadding * 2f;
        if (titleHeight <= 0f || remaining.Height - titleHeight <= 0f)
        {
            return false;
        }

        titleRect = new Rect(remaining.X, remaining.Y, remaining.Width, titleHeight);
        remaining = new Rect(remaining.X, remaining.Y + titleHeight, remaining.Width, remaining.Height - titleHeight);
        return true;
    }

    /// <summary>
    ///     Reserves the legend band from the edge of <paramref name="remaining"/> indicated by the
    ///     chart's legend position, when the legend is visible, has entries, and leaves a positive plot area.
    /// </summary>
    /// <param name="chart">The chart being rendered.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved font used to measure legend text.</param>
    /// <param name="remaining">The remaining area; shrunk when the band is reserved.</param>
    /// <param name="legendRect">Receives the reserved band, or <see cref="Rect.Empty"/>.</param>
    /// <returns><see langword="true"/> when the band was reserved.</returns>
    private static bool TryReserveLegendBand(Chart chart, ChartRenderOptions options, TrueTypeFont font, ref Rect remaining, out Rect legendRect)
    {
        legendRect = Rect.Empty;
        if (chart.Legend is not { IsVisible: true, Position: not ChartLegendPosition.None } legend)
        {
            return false;
        }

        var entries = GetLegendEntries(chart, options);
        if (entries.Count == 0)
        {
            return false;
        }

        return legend.Position is ChartLegendPosition.Top or ChartLegendPosition.Bottom
            ? TryReserveHorizontalLegendBand(legend.Position, options, ref remaining, out legendRect)
            : TryReserveVerticalLegendBand(legend.Position, entries, options, font, ref remaining, out legendRect);
    }

    /// <summary>
    ///     Reserves a Top/Bottom legend band.
    /// </summary>
    /// <param name="position">The legend position (Top or Bottom).</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="remaining">The remaining area; shrunk when the band is reserved.</param>
    /// <param name="legendRect">Receives the reserved band, or <see cref="Rect.Empty"/>.</param>
    /// <returns><see langword="true"/> when the band was reserved.</returns>
    private static bool TryReserveHorizontalLegendBand(ChartLegendPosition position, ChartRenderOptions options, ref Rect remaining, out Rect legendRect)
    {
        legendRect = Rect.Empty;
        var bandHeight = options.LegendFontSize * 1.4f + LegendBandPadding * 2f;
        if (bandHeight <= 0f || remaining.Height - bandHeight <= 0f)
        {
            return false;
        }

        if (position == ChartLegendPosition.Top)
        {
            legendRect = new Rect(remaining.X, remaining.Y, remaining.Width, bandHeight);
            remaining = new Rect(remaining.X, remaining.Y + bandHeight, remaining.Width, remaining.Height - bandHeight);
        }
        else
        {
            legendRect = new Rect(remaining.X, remaining.Bottom - bandHeight, remaining.Width, bandHeight);
            remaining = new Rect(remaining.X, remaining.Y, remaining.Width, remaining.Height - bandHeight);
        }

        return true;
    }

    /// <summary>
    ///     Reserves a Left/Right legend band, sized from the widest entry label.
    /// </summary>
    /// <param name="position">The legend position (Left or Right).</param>
    /// <param name="entries">The legend entries to measure.</param>
    /// <param name="options">The effective render options.</param>
    /// <param name="font">The resolved font used to measure legend text.</param>
    /// <param name="remaining">The remaining area; shrunk when the band is reserved.</param>
    /// <param name="legendRect">Receives the reserved band, or <see cref="Rect.Empty"/>.</param>
    /// <returns><see langword="true"/> when the band was reserved.</returns>
    private static bool TryReserveVerticalLegendBand(
        ChartLegendPosition position, IReadOnlyList<LegendEntry> entries, ChartRenderOptions options, TrueTypeFont font,
        ref Rect remaining, out Rect legendRect)
    {
        legendRect = Rect.Empty;
        var maxLabelWidth = 0f;
        foreach (var entry in entries)
        {
            var measured = TextRenderer.MeasureText(entry.Label, font, options.LegendFontSize).Width;
            maxLabelWidth = MathF.Max(maxLabelWidth, measured);
        }

        var desiredWidth = LegendSwatchSize + LegendEntryGap + maxLabelWidth + LegendBandPadding * 2f;
        var bandWidth = MathF.Min(desiredWidth, remaining.Width * MaxLegendWidthFraction);
        if (bandWidth <= 0f || remaining.Width - bandWidth <= 0f)
        {
            return false;
        }

        if (position == ChartLegendPosition.Left)
        {
            legendRect = new Rect(remaining.X, remaining.Y, bandWidth, remaining.Height);
            remaining = new Rect(remaining.X + bandWidth, remaining.Y, remaining.Width - bandWidth, remaining.Height);
        }
        else
        {
            legendRect = new Rect(remaining.Right - bandWidth, remaining.Y, bandWidth, remaining.Height);
            remaining = new Rect(remaining.X, remaining.Y, remaining.Width - bandWidth, remaining.Height);
        }

        return true;
    }
}
