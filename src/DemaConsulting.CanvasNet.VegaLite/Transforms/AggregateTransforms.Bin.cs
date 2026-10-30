// Portions derived from Vega (BSD-3-Clause): the bin transform; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>bin</c> transform (a partial file of the aggregate transform unit).
/// </content>
internal sealed partial class AggregateTransforms
{
    /// <summary>The number of bins Vega-Lite asks for when a <c>bin</c> transform gives neither <c>maxbins</c> nor <c>step</c>.</summary>
    internal const double DefaultTransformMaxBins = 10;

    /// <summary>
    ///     Writes the bin of a field to two new fields of every row.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The <c>bin</c> value: true or the bin parameters.</param>
    /// <returns>The table with the lower and the upper boundary of the bin of every row.</returns>
    private DataTable Bin(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var settings = BinSettings.Read(operation, DefaultTransformMaxBins);
        var (startName, endName) = BinOutputNames(entry["as"]);
        _context.CheckText(DataValue.FromString(startName), "An output name of the bin transform", location);
        _context.CheckText(DataValue.FromString(endName), "An output name of the bin transform", location);
        var reader = new FieldReader(input, entry["field"]?.AsString());
        var evaluation = _context.Evaluator.Context;
        try
        {
            // The extent of the finite values (or the one the specification gives) decides the bins
            var min = double.PositiveInfinity;
            var max = double.NegativeInfinity;
            if (settings.Extent is { } given)
            {
                (min, max) = given;
            }
            else
            {
                for (var row = 0; row < input.RowCount; row++)
                {
                    _context.Tick(row);
                    _meter.Add(1);
                    if (reader.Read(row, evaluation).TryGetQuantitative(out var number))
                    {
                        min = Math.Min(min, number);
                        max = Math.Max(max, number);
                    }
                }
            }

            BinSpec? bins = min <= max ? BinAlgorithm.Resolve(min, max, settings, _context.Limits.MaxScaleDomainSize, location) : null;
            var layout = new TableLayout(input, [startName, endName], keepInput: true, _context.Limits, location);
            var cells = layout.Allocate(input.RowCount);
            var width = layout.Fields.Length;
            for (var row = 0; row < input.RowCount; row++)
            {
                _context.Tick(row);
                _meter.Add(1);
                layout.CopyRow(input, row, cells, row);
                if (bins is { } spec && reader.Read(row, evaluation).TryGetQuantitative(out var number))
                {
                    var start = BinAlgorithm.Assign(number, spec);
                    cells[((long)row * width) + layout.Added[0]] = DataValue.FromNumber(start);
                    cells[((long)row * width) + layout.Added[1]] = DataValue.FromNumber(BinAlgorithm.End(start, spec));
                }
            }

            return layout.Build(cells, input.RowCount);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw Located(ex, location);
        }
    }

    /// <summary>
    ///     Gets the canonical names of the two fields a bin writes.
    /// </summary>
    /// <param name="output">The <c>as</c> value of the transform: a name, two names or nothing.</param>
    /// <returns>The names of the lower and the upper boundary: the name and <c>name_end</c> for one name, the two names, or <c>bin0</c> and <c>bin1</c>.</returns>
    internal static (string Start, string End) BinOutputNames(SpecNode? output)
    {
        if (output is { IsString: true } single)
        {
            return (TableLayout.Canonical(single.StringValue!), TableLayout.Canonical(single.StringValue + "_end"));
        }

        if (output is { IsArray: true, Items.Count: 2 } pair && pair.Items.All(i => i.IsString))
        {
            return (TableLayout.Canonical(pair.Items[0].StringValue!), TableLayout.Canonical(pair.Items[1].StringValue!));
        }

        return ("bin0", "bin1");
    }
}
