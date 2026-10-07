namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Identifies where a <see cref="Chart"/>'s legend is placed relative to the plot area, or
///     that it is not shown at all.
/// </summary>
public enum ChartLegendPosition
{
    /// <summary>
    ///     The legend is placed above the plot area.
    /// </summary>
    Top,

    /// <summary>
    ///     The legend is placed below the plot area.
    /// </summary>
    Bottom,

    /// <summary>
    ///     The legend is placed to the left of the plot area.
    /// </summary>
    Left,

    /// <summary>
    ///     The legend is placed to the right of the plot area.
    /// </summary>
    Right,

    /// <summary>
    ///     The legend is not shown.
    /// </summary>
    None
}

/// <summary>
///     Represents an immutable legend description for a <see cref="Chart"/>: a placement position
///     and an overall visibility flag.
/// </summary>
/// <remarks>
///     <see cref="ChartLegend"/> is immutable and thread-safe after construction.
/// </remarks>
public sealed class ChartLegend
{
    /// <summary>
    ///     Initializes a new, validated <see cref="ChartLegend"/>.
    /// </summary>
    /// <param name="position">
    ///     The legend's placement relative to the plot area. Must be a defined
    ///     <see cref="ChartLegendPosition"/> value. Defaults to <see cref="ChartLegendPosition.Right"/>.
    /// </param>
    /// <param name="isVisible">
    ///     Whether the legend is shown at all. Defaults to <see langword="true"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="position"/> is not a defined <see cref="ChartLegendPosition"/> value.
    /// </exception>
    public ChartLegend(ChartLegendPosition position = ChartLegendPosition.Right, bool isVisible = true)
    {
        if (!Enum.IsDefined(position))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position), position, "Position must be a defined ChartLegendPosition value.");
        }

        Position = position;
        IsVisible = isVisible;
    }

    /// <summary>
    ///     Gets the legend's placement relative to the plot area.
    /// </summary>
    public ChartLegendPosition Position { get; }

    /// <summary>
    ///     Gets whether the legend is shown at all.
    /// </summary>
    public bool IsVisible { get; }
}
