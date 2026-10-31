// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The row order of a mark: the <c>order</c> channel as the drawing order of the marks (Vega-Lite's <c>getSort</c>), and the step accounting of the sorts.
/// </content>
internal sealed partial class MarkBuilder
{
    /// <content>
    ///     The row order of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>The meter of the evaluation steps of the call, created on first use; null when the call has no evaluator.</summary>
        private WorkMeter? _meter;

        /// <summary>Whether the meter was looked for.</summary>
        private bool _meterCreated;

        /// <summary>
        ///     Charges work that is proportional to a number of elements to the evaluation-step budget of the call (one step for every 64 elements).
        /// </summary>
        /// <remarks>
        ///     The charge is made before the work it pays for. A call without an expression evaluator has no step budget and nothing is charged. The token is polled
        ///     as the step counter advances.
        /// </remarks>
        /// <param name="elements">The number of elements the work touches.</param>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the step budget is exhausted.</exception>
        private void Charge(long elements)
        {
            if (!_meterCreated)
            {
                _meterCreated = true;
                _meter = _owner._expressions is { } expressions ? new WorkMeter(expressions.Evaluator.Context) : null;
            }

            try
            {
                _meter?.Add(elements);
            }
            catch (ExpressionRuntimeException ex)
            {
                throw new VegaLiteRenderException(ex.Message, _location);
            }
        }

        /// <summary>
        ///     Gets the number of elements that sorting <paramref name="count"/> rows costs: <c>count * ceil(log2(max(count, 2)))</c>.
        /// </summary>
        /// <param name="count">The number of rows.</param>
        /// <returns>The elements to charge; saturates at <see cref="long.MaxValue"/>.</returns>
        private static long SortCost(long count)
        {
            var levels = 1;
            for (long width = 2; width < count; width <<= 1)
            {
                levels++;
            }

            return count > long.MaxValue / levels ? long.MaxValue : count * levels;
        }

        /// <summary>
        ///     Gets the fields of the <c>order</c> channel that decide the order of the rows of this mark.
        /// </summary>
        /// <returns>The fields with their direction; empty when the encoding has no <c>order</c> field, when the marks are stacked (the <c>order</c> then orders the layers of the stack) or when the order is switched off.</returns>
        private IReadOnlyList<SortField> RowOrderFields()
        {
            if (_plan is not { Stack: null, OrderFields.Count: > 0 } plan)
            {
                return [];
            }

            return plan.OrderFields;
        }

        /// <summary>
        ///     Tells whether the sort of the rows of a path is switched off: an <c>order</c> channel that is a value of null or false, or, without an <c>order</c> channel, the mark property
        ///     <c>order</c> set to null or false (Vega-Lite's <c>getSort</c>).
        /// </summary>
        /// <returns><see langword="true"/> when the rows keep the order of the data.</returns>
        private bool IsOrderOff()
        {
            if (_view["encoding"]?["order"] is { } order)
            {
                return order is { IsObject: true } && order["value"] is { } value && (value.IsNull || value is { IsBoolean: true, BoolValue: false });
            }

            return Prop("order") is { } property && (property.Kind == ConfigNodeKind.Null || property is { Kind: ConfigNodeKind.Boolean, BoolValue: false });
        }

        /// <summary>
        ///     Gets the order in which the rows of a mark other than a path are drawn (Vega-Lite sorts the marks by the <c>order</c> channel when the marks are not stacked).
        /// </summary>
        /// <returns>The row numbers in drawing order; null when the rows are drawn in the order of the data.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the step budget is exhausted.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
        private int[]? MarkRows()
        {
            var fields = RowOrderFields();
            var count = _table.RowCount;
            if (fields.Count == 0 || count < 2)
            {
                return null;
            }

            var rows = new int[count];
            for (var i = 0; i < count; i++)
            {
                rows[i] = i;
            }

            Charge(SortCost(count));
            StableSorter.Sort(rows, 0, count, new int[count], OrderComparison(fields), _cancellationToken);
            return rows;
        }

        /// <summary>
        ///     Creates the comparison of two rows by the fields of the <c>order</c> channel.
        /// </summary>
        /// <param name="fields">The fields with their direction, in order of significance.</param>
        /// <returns>The comparison: negative when the first row sorts first; ties compare equal so that the sort is stable.</returns>
        private Func<int, int, int> OrderComparison(IReadOnlyList<SortField> fields)
        {
            var indexes = new int[fields.Count];
            for (var i = 0; i < indexes.Length; i++)
            {
                indexes[i] = FieldIndexOf(fields[i].Field);
            }

            return (a, b) =>
            {
                for (var i = 0; i < indexes.Length; i++)
                {
                    if (indexes[i] < 0)
                    {
                        continue;
                    }

                    var result = DataValue.Compare(_table.GetValue(a, indexes[i]), _table.GetValue(b, indexes[i]));
                    if (result != 0)
                    {
                        return fields[i].Descending ? -result : result;
                    }
                }

                return 0;
            };
        }

        /// <summary>
        ///     Finds a field of the table that the plan names.
        /// </summary>
        /// <param name="field">The canonical name or the path of the field.</param>
        /// <returns>The position of the field in the table; -1 when the table has no such field.</returns>
        private int FieldIndexOf(string field)
        {
            var index = _table.IndexOfField(field);
            return index >= 0 ? index : _table.FindField(field);
        }
    }
}
