using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Represents an immutable, fully validated description of a chart: its kind, data series,
///     optional category/value axes, optional legend, optional title, and an optional default
///     color palette.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="Chart"/> is the root aggregate of this library's data model. It is immutable
///     and thread-safe after construction: every caller-supplied list argument is defensively
///     copied into an immutable snapshot, so mutating the caller's original list after
///     construction has no effect on this instance.
///     </para>
///     <para>
///     Rather than constructing a <see cref="Chart"/> directly, most callers will find
///     <see cref="ChartBuilder"/>'s fluent API more ergonomic; <see cref="ChartBuilder"/>
///     ultimately calls this constructor, so the two approaches enforce identical invariants.
///     </para>
/// </remarks>
public sealed class Chart
{
    /// <summary>
    ///     Initializes a new, validated <see cref="Chart"/>.
    /// </summary>
    /// <param name="type">
    ///     The kind of chart this instance describes. Must be a defined <see cref="ChartType"/>
    ///     value.
    /// </param>
    /// <param name="series">
    ///     The chart's data series. Must contain at least one entry, and no entry may be
    ///     <see langword="null"/>. For <see cref="ChartType.Bar"/>, <see cref="ChartType.Column"/>,
    ///     <see cref="ChartType.Line"/>, and <see cref="ChartType.Area"/>, when
    ///     <paramref name="categoryAxis"/> is supplied with non-null
    ///     <see cref="ChartAxis.Labels"/>, every series' <see cref="ChartSeries.Values"/> count
    ///     must exactly equal that label count.
    /// </param>
    /// <param name="categoryAxis">
    ///     An optional category axis, or <see langword="null"/> for none.
    /// </param>
    /// <param name="valueAxis">
    ///     An optional value axis, or <see langword="null"/> for none.
    /// </param>
    /// <param name="legend">
    ///     An optional legend, or <see langword="null"/> for none.
    /// </param>
    /// <param name="title">
    ///     An optional title, or <see langword="null"/> for none.
    /// </param>
    /// <param name="colorPalette">
    ///     An optional default categorical color palette used by a renderer for any series or
    ///     point that does not specify its own color, or <see langword="null"/> to let a renderer
    ///     choose a default. Must not be empty when supplied.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="series"/> is empty, contains a <see langword="null"/>
    ///     entry, or contains a series/category-axis length mismatch for a category-based chart
    ///     type; or when <paramref name="colorPalette"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="type"/> is not a defined <see cref="ChartType"/> value.
    /// </exception>
    public Chart(
        ChartType type,
        IReadOnlyList<ChartSeries> series,
        ChartAxis? categoryAxis = null,
        ChartAxis? valueAxis = null,
        ChartLegend? legend = null,
        ChartTitle? title = null,
        IReadOnlyList<Rgba32>? colorPalette = null)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Type must be a defined ChartType value.");
        }

        Series = CopySeries(series);

        if (IsCategoryBased(type) && categoryAxis?.Labels is { } labels)
        {
            foreach (var entry in Series)
            {
                if (entry.Values.Count != labels.Count)
                {
                    throw new ArgumentException(
                        "Every series' values count must exactly equal the category axis label count.",
                        nameof(series));
                }
            }
        }

        if (colorPalette is not null && colorPalette.Count == 0)
        {
            throw new ArgumentException("Color palette must not be empty when supplied.", nameof(colorPalette));
        }

        Type = type;
        CategoryAxis = categoryAxis;
        ValueAxis = valueAxis;
        Legend = legend;
        Title = title;
        ColorPalette = colorPalette is null ? null : new ReadOnlyCollection<Rgba32>([.. colorPalette]);
    }

    /// <summary>
    ///     Gets the kind of chart this instance describes.
    /// </summary>
    public ChartType Type { get; }

    /// <summary>
    ///     Gets the chart's data series.
    /// </summary>
    public IReadOnlyList<ChartSeries> Series { get; }

    /// <summary>
    ///     Gets the category axis, or <see langword="null"/> when none was supplied.
    /// </summary>
    public ChartAxis? CategoryAxis { get; }

    /// <summary>
    ///     Gets the value axis, or <see langword="null"/> when none was supplied.
    /// </summary>
    public ChartAxis? ValueAxis { get; }

    /// <summary>
    ///     Gets the legend, or <see langword="null"/> when none was supplied.
    /// </summary>
    public ChartLegend? Legend { get; }

    /// <summary>
    ///     Gets the title, or <see langword="null"/> when none was supplied.
    /// </summary>
    public ChartTitle? Title { get; }

    /// <summary>
    ///     Gets the default categorical color palette, or <see langword="null"/> when none was
    ///     supplied.
    /// </summary>
    public IReadOnlyList<Rgba32>? ColorPalette { get; }

    /// <summary>
    ///     Determines whether <paramref name="type"/> associates each series' values
    ///     one-for-one with a category axis label (as opposed to, for example,
    ///     <see cref="ChartType.Pie"/>/<see cref="ChartType.Doughnut"/>, whose single series'
    ///     values instead represent proportional wedge shares with no category-axis concept).
    /// </summary>
    private static bool IsCategoryBased(ChartType type) =>
        type is ChartType.Bar or ChartType.Column or ChartType.Line or ChartType.Area;

    /// <summary>
    ///     Copies and validates <paramref name="series"/>, producing an immutable snapshot.
    /// </summary>
    /// <param name="series">The caller-supplied series list.</param>
    /// <returns>The copied, read-only series snapshot.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="series"/> is empty or contains a <see langword="null"/>
    ///     entry.
    /// </exception>
    private static IReadOnlyList<ChartSeries> CopySeries(IReadOnlyList<ChartSeries> series)
    {
        if (series.Count == 0)
        {
            throw new ArgumentException("Series must contain at least one entry.", nameof(series));
        }

        var copy = new ChartSeries[series.Count];
        for (var i = 0; i < series.Count; i++)
        {
            var entry = series[i];
            if (entry is null)
            {
                throw new ArgumentException("Series must not contain a null entry.", nameof(series));
            }

            copy[i] = entry;
        }

        return new ReadOnlyCollection<ChartSeries>(copy);
    }
}
