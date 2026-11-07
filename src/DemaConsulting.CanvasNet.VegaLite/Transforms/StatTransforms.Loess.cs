// Portions derived from Vega (BSD-3-Clause): the loess transform of vega-regression; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>loess</c> transform (a partial file of the statistical transform unit).
/// </content>
internal sealed partial class StatTransforms
{
    /// <summary>The bandwidth of a loess that gives none, or zero (Vega's <c>bandwidth || 0.3</c>).</summary>
    private const double DefaultLoessBandwidth = 0.3;

    /// <summary>
    ///     Smooths every group with locally weighted regression (<see cref="LoessFit"/>) and generates the smoothed points.
    /// </summary>
    /// <remarks>
    ///     The output has a row for each distinct x of the valid points of a group, in increasing x, with the group fields and the
    ///     fields <c>as</c> (default: the <c>on</c> field and the smoothed field). A group of one point has a NaN value, as in Vega.
    ///     The work of a group is charged to the step budget before the fit starts, so a large group with a wide bandwidth is
    ///     refused rather than run.
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field that is smoothed (<c>loess</c>).</param>
    /// <returns>The generated table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the output exceeds a limit.</exception>
    private DataTable Loess(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var yPath = operation.StringValue!;
        var xPath = entry["on"]!.StringValue!;
        var groupBy = ReadGroupBy(entry);
        var names = ReadAs(entry, xPath, yPath);
        CheckTexts("A field or output name of the loess", location, [xPath, yPath, names.First, names.Second, .. groupBy ?? []]);
        var given = entry["bandwidth"]?.AsNumber() ?? 0;
        var bandwidth = StatisticsPrimitives.IsTruthy(given) ? given : DefaultLoessBandwidth;
        var xs = ReadColumn(input, xPath);
        var ys = ReadColumn(input, yPath);
        var groups = Partition(input, groupBy);
        var output = new OutputTable(this, GroupedLayout(input, groupBy, names, location), "loess transform output", location);
        foreach (var group in groups)
        {
            // Charged before the fit starts: the window of every point is visited three times
            _meter.Add(LoessFit.Cost(group.Rows.Count, bandwidth));
            var points = LoessFit.Fit(
                Pick(xs, group.Rows),
                Pick(ys, group.Rows),
                bandwidth,
                _ => _context.CancellationToken.ThrowIfCancellationRequested());
            var xIndex = group.Dimensions?.Length ?? 0;
            foreach (var (x, y) in points)
            {
                var row = output.AddRow();
                WriteDimensions(output, row, group);
                output.Set(row, xIndex, Number(x));
                output.Set(row, xIndex + 1, Number(y));
            }
        }

        _context.Loader.ReleaseRows(input.RowCount);
        return output.Build();
    }
}
