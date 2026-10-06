using System.Collections.ObjectModel;

namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Represents an immutable axis description for a <see cref="Chart"/>: an optional ordered
///     list of category labels (used for a category axis), an optional value-axis range (used
///     for a value axis), and an optional title.
/// </summary>
/// <remarks>
///     <para>
///     A single <see cref="ChartAxis"/> type is used for both a chart's category axis and its
///     value axis; a caller constructs one with only the category <see cref="Labels"/>
///     (category axis use) or only the value-range members (value axis use), or both, since
///     neither use is required to be exclusive of the other at the type level.
///     </para>
///     <para>
///     <see cref="ChartAxis"/> is immutable and thread-safe after construction: a caller-supplied
///     labels argument is defensively copied into an immutable snapshot, so mutating the
///     caller's original list after construction has no effect on this instance.
///     </para>
/// </remarks>
public sealed class ChartAxis
{
    /// <summary>
    ///     Initializes a new, validated <see cref="ChartAxis"/>.
    /// </summary>
    /// <param name="labels">
    ///     The ordered category labels for this axis, or <see langword="null"/> for an axis with
    ///     no category labels (for example, a pure value axis). An empty string entry is a
    ///     legitimate "blank category label" and is accepted; a <see langword="null"/> entry is
    ///     not.
    /// </param>
    /// <param name="minimum">
    ///     The value-axis minimum, or <see langword="null"/> to leave the minimum unspecified.
    ///     Must be finite when supplied.
    /// </param>
    /// <param name="maximum">
    ///     The value-axis maximum, or <see langword="null"/> to leave the maximum unspecified.
    ///     Must be finite when supplied, and greater than <paramref name="minimum"/> when both
    ///     are supplied.
    /// </param>
    /// <param name="tickInterval">
    ///     The spacing between value-axis tick marks, or <see langword="null"/> to leave the tick
    ///     interval unspecified. Must be finite and strictly greater than zero when supplied.
    /// </param>
    /// <param name="title">
    ///     An optional axis title, or <see langword="null"/> for no title. Must not be empty or
    ///     consist only of whitespace when supplied.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="labels"/> contains a <see langword="null"/> entry, or when
    ///     <paramref name="title"/> is empty or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="minimum"/> or <paramref name="maximum"/> is not finite;
    ///     when <paramref name="tickInterval"/> is not finite or is less than or equal to zero;
    ///     or when both <paramref name="minimum"/> and <paramref name="maximum"/> are supplied and
    ///     <paramref name="minimum"/> is not strictly less than <paramref name="maximum"/>.
    /// </exception>
    public ChartAxis(
        IReadOnlyList<string>? labels = null,
        float? minimum = null,
        float? maximum = null,
        float? tickInterval = null,
        string? title = null)
    {
        Labels = CopyLabels(labels);

        if (minimum.HasValue && !float.IsFinite(minimum.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "Minimum must be a finite value.");
        }

        if (maximum.HasValue && !float.IsFinite(maximum.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum must be a finite value.");
        }

        if (minimum.HasValue && maximum.HasValue && minimum.Value >= maximum.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimum), minimum, "Minimum must be strictly less than maximum when both are supplied.");
        }

        if (tickInterval.HasValue && (!float.IsFinite(tickInterval.Value) || tickInterval.Value <= 0f))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tickInterval), tickInterval, "Tick interval must be a finite value greater than zero.");
        }

        if (title is not null && string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title must not be empty or consist only of whitespace.", nameof(title));
        }

        Minimum = minimum;
        Maximum = maximum;
        TickInterval = tickInterval;
        Title = title;
    }

    /// <summary>
    ///     Gets the ordered category labels for this axis, or <see langword="null"/> when this
    ///     axis has no category labels.
    /// </summary>
    public IReadOnlyList<string>? Labels { get; }

    /// <summary>
    ///     Gets the value-axis minimum, or <see langword="null"/> when unspecified.
    /// </summary>
    public float? Minimum { get; }

    /// <summary>
    ///     Gets the value-axis maximum, or <see langword="null"/> when unspecified.
    /// </summary>
    public float? Maximum { get; }

    /// <summary>
    ///     Gets the spacing between value-axis tick marks, or <see langword="null"/> when
    ///     unspecified.
    /// </summary>
    public float? TickInterval { get; }

    /// <summary>
    ///     Gets the axis title, or <see langword="null"/> when this axis has no title.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    ///     Copies and validates <paramref name="labels"/>, producing an immutable snapshot or
    ///     <see langword="null"/> when no labels were supplied.
    /// </summary>
    /// <param name="labels">The caller-supplied labels list.</param>
    /// <returns>The copied, read-only labels snapshot, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="labels"/> contains a <see langword="null"/> entry.
    /// </exception>
    private static IReadOnlyList<string>? CopyLabels(IReadOnlyList<string>? labels)
    {
        if (labels is null)
        {
            return null;
        }

        var values = new string[labels.Count];
        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            if (label is null)
            {
                throw new ArgumentException("Labels must not contain a null entry.", nameof(labels));
            }

            values[i] = label;
        }

        return new ReadOnlyCollection<string>(values);
    }
}
