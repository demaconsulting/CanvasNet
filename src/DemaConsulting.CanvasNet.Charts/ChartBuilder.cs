using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Provides an ergonomic, fluent, mutable-until-<see cref="Build"/> API for constructing an
///     immutable <see cref="Chart"/>.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="ChartBuilder"/> performs no data-shape validation of its own beyond reporting a
///     clear "nothing configured yet" error from <see cref="Build"/>: every other invariant
///     (non-empty series, finite values, matching category/value counts, and so on) is enforced
///     exactly once, by <see cref="Chart"/>'s and its constituent types' own validating
///     constructors, which <see cref="Build"/> ultimately calls. An invalid argument passed to
///     any of this type's methods therefore surfaces the same exception the corresponding model
///     constructor would throw.
///     </para>
///     <para>
///     Each fluent method returns the same <see cref="ChartBuilder"/> instance, so calls may be
///     chained. A single <see cref="ChartBuilder"/> instance is not thread-safe for concurrent
///     use; build one chart per instance.
///     </para>
/// </remarks>
public sealed class ChartBuilder
{
    private readonly List<ChartSeries> _series = [];
    private ChartType? _type;
    private ChartAxis? _categoryAxis;
    private ChartAxis? _valueAxis;
    private ChartLegend? _legend;
    private ChartTitle? _title;
    private IReadOnlyList<Rgba32>? _colorPalette;

    /// <summary>
    ///     Creates a new, empty <see cref="ChartBuilder"/> with no type, series, axes, legend,
    ///     title, or color palette configured yet.
    /// </summary>
    public ChartBuilder()
    {
    }

    /// <summary>
    ///     Sets the kind of chart being built.
    /// </summary>
    /// <param name="type">The kind of chart to build. Must be a defined <see cref="ChartType"/> value.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    public ChartBuilder OfType(ChartType type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    ///     Adds an already-constructed series to the chart being built.
    /// </summary>
    /// <param name="series">The series to add. Must not be <see langword="null"/>.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="series"/> is <see langword="null"/>.</exception>
    public ChartBuilder AddSeries(ChartSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        _series.Add(series);
        return this;
    }

    /// <summary>
    ///     Constructs a <see cref="ChartSeries"/> from the supplied values and adds it to the
    ///     chart being built.
    /// </summary>
    /// <param name="name">The series name. See <see cref="ChartSeries"/>'s constructor for validation.</param>
    /// <param name="values">The series' data points. See <see cref="ChartSeries"/>'s constructor for validation.</param>
    /// <param name="pointColors">An optional per-point color override. See <see cref="ChartSeries"/>'s constructor for validation.</param>
    /// <param name="pointLabels">An optional per-point label override. See <see cref="ChartSeries"/>'s constructor for validation.</param>
    /// <param name="color">An optional series-level color.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    public ChartBuilder AddSeries(
        string name,
        IReadOnlyList<double> values,
        IReadOnlyList<Rgba32>? pointColors = null,
        IReadOnlyList<string>? pointLabels = null,
        Rgba32? color = null) =>
        AddSeries(new ChartSeries(name, values, pointColors, pointLabels, color));

    /// <summary>
    ///     Sets an already-constructed category axis on the chart being built.
    /// </summary>
    /// <param name="axis">The category axis to set. Must not be <see langword="null"/>.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="axis"/> is <see langword="null"/>.</exception>
    public ChartBuilder WithCategoryAxis(ChartAxis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        _categoryAxis = axis;
        return this;
    }

    /// <summary>
    ///     Constructs a category axis from the supplied category labels and an optional title,
    ///     and sets it on the chart being built.
    /// </summary>
    /// <param name="labels">The ordered category labels. See <see cref="ChartAxis"/>'s constructor for validation.</param>
    /// <param name="title">An optional axis title. See <see cref="ChartAxis"/>'s constructor for validation.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    public ChartBuilder WithCategoryAxis(IReadOnlyList<string> labels, string? title = null) =>
        WithCategoryAxis(new ChartAxis(labels, title: title));

    /// <summary>
    ///     Sets an already-constructed value axis on the chart being built.
    /// </summary>
    /// <param name="axis">The value axis to set. Must not be <see langword="null"/>.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="axis"/> is <see langword="null"/>.</exception>
    public ChartBuilder WithValueAxis(ChartAxis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        _valueAxis = axis;
        return this;
    }

    /// <summary>
    ///     Constructs a value axis from the supplied range/tick/title and sets it on the chart
    ///     being built.
    /// </summary>
    /// <param name="minimum">The value-axis minimum. See <see cref="ChartAxis"/>'s constructor for validation.</param>
    /// <param name="maximum">The value-axis maximum. See <see cref="ChartAxis"/>'s constructor for validation.</param>
    /// <param name="tickInterval">The tick-mark spacing. See <see cref="ChartAxis"/>'s constructor for validation.</param>
    /// <param name="title">An optional axis title. See <see cref="ChartAxis"/>'s constructor for validation.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    public ChartBuilder WithValueAxis(
        float? minimum = null,
        float? maximum = null,
        float? tickInterval = null,
        string? title = null) =>
        WithValueAxis(new ChartAxis(minimum: minimum, maximum: maximum, tickInterval: tickInterval, title: title));

    /// <summary>
    ///     Sets an already-constructed legend on the chart being built.
    /// </summary>
    /// <param name="legend">The legend to set. Must not be <see langword="null"/>.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="legend"/> is <see langword="null"/>.</exception>
    public ChartBuilder WithLegend(ChartLegend legend)
    {
        ArgumentNullException.ThrowIfNull(legend);
        _legend = legend;
        return this;
    }

    /// <summary>
    ///     Constructs a legend from the supplied position/visibility and sets it on the chart
    ///     being built.
    /// </summary>
    /// <param name="position">The legend's placement. See <see cref="ChartLegend"/>'s constructor for validation.</param>
    /// <param name="isVisible">Whether the legend is shown.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    public ChartBuilder WithLegend(ChartLegendPosition position = ChartLegendPosition.Right, bool isVisible = true) =>
        WithLegend(new ChartLegend(position, isVisible));

    /// <summary>
    ///     Constructs a title from the supplied text/font size and sets it on the chart being
    ///     built.
    /// </summary>
    /// <param name="text">The title text. See <see cref="ChartTitle"/>'s constructor for validation.</param>
    /// <param name="fontSize">An optional font-size hint. See <see cref="ChartTitle"/>'s constructor for validation.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    public ChartBuilder WithTitle(string text, float? fontSize = null)
    {
        _title = new ChartTitle(text, fontSize);
        return this;
    }

    /// <summary>
    ///     Sets the default categorical color palette on the chart being built.
    /// </summary>
    /// <param name="colorPalette">The color palette. See <see cref="Chart"/>'s constructor for validation.</param>
    /// <returns>This <see cref="ChartBuilder"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colorPalette"/> is <see langword="null"/>.</exception>
    public ChartBuilder WithColorPalette(IReadOnlyList<Rgba32> colorPalette)
    {
        ArgumentNullException.ThrowIfNull(colorPalette);
        _colorPalette = colorPalette;
        return this;
    }

    /// <summary>
    ///     Constructs the immutable <see cref="Chart"/> described by this builder's accumulated
    ///     configuration.
    /// </summary>
    /// <returns>The constructed, validated <see cref="Chart"/>.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <see cref="OfType"/> was never called, or when no series was added via
    ///     <see cref="AddSeries(ChartSeries)"/>/<see cref="AddSeries(string, IReadOnlyList{double}, IReadOnlyList{Rgba32}?, IReadOnlyList{string}?, Rgba32?)"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the accumulated configuration is rejected by <see cref="Chart"/>'s own
    ///     constructor (for example a series/category-axis length mismatch).
    /// </exception>
    public Chart Build()
    {
        if (_type is null)
        {
            throw new InvalidOperationException(
                "A chart type must be set via OfType before calling Build().");
        }

        if (_series.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one series must be added via AddSeries before calling Build().");
        }

        return new Chart(_type.Value, _series, _categoryAxis, _valueAxis, _legend, _title, _colorPalette);
    }
}
