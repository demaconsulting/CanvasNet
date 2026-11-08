// Portions derived from Vega (BSD-3-Clause): the quantile transform of vega-transforms; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>quantile</c> transform (a partial file of the statistical transform unit).
/// </content>
internal sealed partial class StatTransforms
{
    /// <summary>The default probability step of a quantile transform (Vega's <c>step || 0.01</c>).</summary>
    private const double DefaultQuantileStep = 0.01;

    /// <summary>The margin that keeps the last probability of the default range below one (Vega's <c>EPSILON</c>).</summary>
    private const double QuantileEpsilon = 1e-14;

    /// <summary>
    ///     Generates the sample quantiles of a field for every group.
    /// </summary>
    /// <remarks>
    ///     The probabilities are <c>probs</c> or, without them, <c>d3.range(step / 2, 1 - 1e-14, step)</c> (100 values for the
    ///     default step of 0.01). The quantile of a probability is read by linear interpolation between the closest ranks of the
    ///     sorted numbers of the group (null, the empty text and NaN are not numbers); a group without numbers gives no value
    ///     (null). The output has the group fields, <c>prob</c> and <c>value</c> (<c>as</c> renames them), one row per group and
    ///     probability, with the probabilities of every group together. The rows are counted in the row budget before the
    ///     probabilities are generated, so a tiny step is refused rather than run.
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field whose quantiles are computed (<c>quantile</c>).</param>
    /// <returns>The generated table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the output exceeds a limit.</exception>
    private DataTable Quantile(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var fieldPath = operation.StringValue!;
        var groupBy = ReadGroupBy(entry);
        var names = ReadAs(entry, "prob", "value");
        CheckTexts("A field or output name of the quantile", location, [fieldPath, names.First, names.Second, .. groupBy ?? []]);
        var values = ReadColumn(input, fieldPath);
        var groups = Partition(input, groupBy);

        // The number of probabilities is known before they are generated, so the rows can be counted first
        double[] probabilities;
        if (entry["probs"] is { IsArray: true } list)
        {
            probabilities = [.. list.Items.Select(p => p.NumberValue)];
        }
        else
        {
            var given = entry["step"]?.AsNumber() ?? 0;
            var step = StatisticsPrimitives.IsTruthy(given) ? given : DefaultQuantileStep;
            var count = StatisticsPrimitives.RangeCount(step / 2, 1 - QuantileEpsilon, step);
            CheckRows(Math.Max(1, groups.Count) * (double)count, "quantile transform output", location);

            // The row budget can be raised by the caller, but the probabilities are held in one array: never allocate more than the cell limit
            if (count > TableLayout.MaxCells)
            {
                throw new VegaLiteRenderException(
                    string.Create(System.Globalization.CultureInfo.InvariantCulture, $"The quantile transform would generate {count} probabilities, more than the limit of {TableLayout.MaxCells} values (TableLayout.MaxCells)."),
                    location);
            }

            probabilities = new double[count];
            for (var i = 0; i < probabilities.Length; i++)
            {
                probabilities[i] = (step / 2) + (i * step);
            }
        }

        var output = new OutputTable(this, GroupedLayout(input, groupBy, names, location), "quantile transform output", location);
        var xIndex = groupBy?.Length ?? 0;
        foreach (var group in groups)
        {
            var groupValues = Pick(values, group.Rows);
            _meter.Add((long)groupValues.Length * (System.Numerics.BitOperations.Log2((uint)groupValues.Length | 1) + 2));
            var quantiles = StatisticsPrimitives.Quantiles(groupValues, probabilities);
            for (var i = 0; i < probabilities.Length; i++)
            {
                var row = output.AddRow();
                WriteDimensions(output, row, group);
                output.Set(row, xIndex, Number(probabilities[i]));

                // A quantile of nothing is undefined in Vega: no value
                output.Set(row, xIndex + 1, NumberOrNull(quantiles[i]));
            }
        }

        _context.Loader.ReleaseRows(input.RowCount);
        return output.Build();
    }
}
