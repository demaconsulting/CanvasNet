namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     Identifies the kind of chart a <see cref="Chart"/> describes.
/// </summary>
/// <remarks>
///     This enum enumerates only the chart kinds supported as of this release. A future release
///     may add further members (for example <c>Scatter</c> or <c>Radar</c>); existing members'
///     numeric values are stable across releases.
/// </remarks>
public enum ChartType
{
    /// <summary>
    ///     A horizontal bar chart: each data point is drawn as a horizontal bar whose length is
    ///     proportional to its value, with categories arranged along the vertical axis.
    /// </summary>
    Bar,

    /// <summary>
    ///     A vertical bar chart (also known as a column chart): each data point is drawn as a
    ///     vertical bar whose height is proportional to its value, with categories arranged along
    ///     the horizontal axis. This is the orientation-flipped counterpart of <see cref="Bar"/>.
    /// </summary>
    Column,

    /// <summary>
    ///     A line chart: each series is drawn as a connected sequence of line segments joining
    ///     one point per category, with categories arranged along the horizontal axis.
    /// </summary>
    Line,

    /// <summary>
    ///     A pie chart: a single series' values are drawn as proportionally sized wedges of a
    ///     single circle, each wedge's angular extent proportional to its share of the series'
    ///     total.
    /// </summary>
    Pie,

    /// <summary>
    ///     A doughnut chart: the same proportional-wedge layout as <see cref="Pie"/>, but with a
    ///     circular hole removed from the center of the wedge ring.
    /// </summary>
    Doughnut,

    /// <summary>
    ///     An area chart: the same connected-line-segment layout as <see cref="Line"/>, but with
    ///     the region between each series' line and the category axis filled.
    /// </summary>
    Area
}
