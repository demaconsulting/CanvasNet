// Portions derived from Vega (BSD-3-Clause): the extent transform of vega-transforms; see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>extent</c> transform (a partial file of the statistical transform unit).
/// </content>
internal sealed partial class StatTransforms
{
    /// <summary>
    ///     Computes the minimum and the maximum of a field and stores them in a parameter; the table is not changed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Vega's <c>Extent</c> converts every value with <c>toNumber</c> (null and the empty text are skipped, everything else
    ///         is read as JavaScript's unary plus reads it), and NaN fails every comparison, so it is ignored. When no value is
    ///         finite (an empty table, no numbers, an infinite value) Vega warns and the signal is
    ///         <c>[undefined, undefined]</c>; here the parameter is <c>[null, null]</c> and the warning is
    ///         <c>vl-transform-runtime</c>.
    ///     </para>
    ///     <para>
    ///         The value is written to the shared parameter table of the call while the data stage runs, so the expressions of
    ///         the transforms that follow, <c>bin.extent</c> and <c>scale.domain</c> of the form <c>{"param": name}</c> read it.
    ///         The properties that are fixed before the data is read (a title or a configuration value that is an expression
    ///         reference) see no value; the renderer reports that (<c>vl-param-extent-static</c>).
    ///     </para>
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field whose extent is computed (<c>extent</c>).</param>
    /// <returns><paramref name="input"/> itself.</returns>
    private DataTable Extent(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var path = operation.StringValue!;
        var name = entry["param"]!.StringValue!;
        CheckTexts("A field or parameter name of the extent", location, [path, name]);
        var reader = new FieldReader(input, path);
        var evaluation = _context.Evaluator.Context;
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var row = 0; row < input.RowCount; row++)
        {
            _context.Tick(row);
            var value = reader.Read(row, evaluation);

            // Vega's toNumber: null and the empty text are no value; a NaN fails both comparisons
            if (value.IsNull || (value.IsString && value.Text!.Length == 0))
            {
                continue;
            }

            var number = ExpressionEvaluator.ToNumber(value, evaluation);
            if (number < min)
            {
                min = number;
            }

            if (number > max)
            {
                max = number;
            }
        }

        _meter.Add(input.RowCount);
        if (!double.IsFinite(min) || !double.IsFinite(max))
        {
            Warn(
                string.Create(CultureInfo.InvariantCulture, $"The extent of '{path}' is infinite ([{DataValue.FromNumber(min).ToText()}, {DataValue.FromNumber(max).ToText()}]); the parameter '{name}' is set to [null, null]."),
                location);
            _context.SetParameter(name, DataValue.FromArray([default, default]));
        }
        else
        {
            _context.SetParameter(name, DataValue.FromArray([Number(min), Number(max)]));
        }

        return input;
    }
}
