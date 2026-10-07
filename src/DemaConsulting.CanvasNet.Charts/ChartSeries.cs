using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Represents an immutable named series of numeric values for a <see cref="Chart"/>, with
///     optional per-point colors, per-point labels, and a series-level color.
/// </summary>
/// <remarks>
///     <see cref="ChartSeries"/> is immutable and thread-safe after construction: every
///     caller-supplied list argument is defensively copied into an immutable snapshot, so
///     mutating the caller's original list after construction has no effect on this instance.
/// </remarks>
public sealed class ChartSeries
{
    /// <summary>
    ///     Initializes a new, validated <see cref="ChartSeries"/>.
    /// </summary>
    /// <param name="name">
    ///     The series name. Must not be <see langword="null"/>, empty, or consist only of
    ///     whitespace.
    /// </param>
    /// <param name="values">
    ///     The series' data points. Must contain at least one value, and every value must be
    ///     finite.
    /// </param>
    /// <param name="pointColors">
    ///     An optional per-point color override, or <see langword="null"/> for none. When
    ///     supplied, its <see cref="IReadOnlyCollection{T}.Count"/> must exactly equal
    ///     <paramref name="values"/>'s count.
    /// </param>
    /// <param name="pointLabels">
    ///     An optional per-point label override, or <see langword="null"/> for none. When
    ///     supplied, its <see cref="IReadOnlyCollection{T}.Count"/> must exactly equal
    ///     <paramref name="values"/>'s count. A <see langword="null"/> entry is not permitted.
    /// </param>
    /// <param name="color">
    ///     An optional series-level color, or <see langword="null"/> to let a renderer choose a
    ///     default (for example from a <see cref="Chart"/>'s color palette).
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="name"/> is empty or consists only of whitespace; when
    ///     <paramref name="values"/> is empty or contains a non-finite entry; when
    ///     <paramref name="pointColors"/>'s count does not exactly equal
    ///     <paramref name="values"/>'s count; when <paramref name="pointLabels"/>'s count does
    ///     not exactly equal <paramref name="values"/>'s count; or when
    ///     <paramref name="pointLabels"/> contains a <see langword="null"/> entry.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="name"/> or <paramref name="values"/> is
    ///     <see langword="null"/>.
    /// </exception>
    public ChartSeries(
        string name,
        IReadOnlyList<double> values,
        IReadOnlyList<Rgba32>? pointColors = null,
        IReadOnlyList<string>? pointLabels = null,
        Rgba32? color = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(values);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must not be empty or consist only of whitespace.", nameof(name));
        }

        Values = CopyValues(values);

        if (pointColors is not null && pointColors.Count != Values.Count)
        {
            throw new ArgumentException(
                "Point colors count must exactly equal the values count.", nameof(pointColors));
        }

        if (pointLabels is not null && pointLabels.Count != Values.Count)
        {
            throw new ArgumentException(
                "Point labels count must exactly equal the values count.", nameof(pointLabels));
        }

        Name = name;
        PointColors = pointColors is null ? null : new ReadOnlyCollection<Rgba32>([.. pointColors]);
        PointLabels = CopyPointLabels(pointLabels);
        Color = color;
    }

    /// <summary>
    ///     Gets the series name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the series' data points.
    /// </summary>
    public IReadOnlyList<double> Values { get; }

    /// <summary>
    ///     Gets the per-point color override, or <see langword="null"/> when none was supplied.
    /// </summary>
    public IReadOnlyList<Rgba32>? PointColors { get; }

    /// <summary>
    ///     Gets the per-point label override, or <see langword="null"/> when none was supplied.
    /// </summary>
    public IReadOnlyList<string>? PointLabels { get; }

    /// <summary>
    ///     Gets the series-level color, or <see langword="null"/> when unspecified.
    /// </summary>
    public Rgba32? Color { get; }

    /// <summary>
    ///     Copies and validates <paramref name="values"/>, producing an immutable snapshot.
    /// </summary>
    /// <param name="values">The caller-supplied values list.</param>
    /// <returns>The copied, read-only values snapshot.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="values"/> is empty or contains a non-finite entry.
    /// </exception>
    private static IReadOnlyList<double> CopyValues(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("Values must contain at least one entry.", nameof(values));
        }

        var copy = new double[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (!double.IsFinite(value))
            {
                throw new ArgumentException("Values entries must all be finite.", nameof(values));
            }

            copy[i] = value;
        }

        return new ReadOnlyCollection<double>(copy);
    }

    /// <summary>
    ///     Copies and validates <paramref name="pointLabels"/>, producing an immutable snapshot
    ///     or <see langword="null"/> when no labels were supplied.
    /// </summary>
    /// <param name="pointLabels">The caller-supplied point labels list.</param>
    /// <returns>The copied, read-only point labels snapshot, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="pointLabels"/> contains a <see langword="null"/> entry.
    /// </exception>
    private static IReadOnlyList<string>? CopyPointLabels(IReadOnlyList<string>? pointLabels)
    {
        if (pointLabels is null)
        {
            return null;
        }

        var copy = new string[pointLabels.Count];
        for (var i = 0; i < pointLabels.Count; i++)
        {
            var label = pointLabels[i];
            if (label is null)
            {
                throw new ArgumentException("Point labels must not contain a null entry.", nameof(pointLabels));
            }

            copy[i] = label;
        }

        return new ReadOnlyCollection<string>(copy);
    }
}
