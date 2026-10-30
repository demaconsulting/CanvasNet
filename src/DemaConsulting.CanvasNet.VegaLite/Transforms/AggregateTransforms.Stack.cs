// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The baseline of a stack.
/// </summary>
internal enum StackOffset
{
    /// <summary>The values are stacked from zero; the negative values go down from zero, separately from the positive ones.</summary>
    Zero,

    /// <summary>The absolute values are stacked centered on the widest group.</summary>
    Center,

    /// <summary>The absolute values are stacked and scaled so that every group ends at 1.</summary>
    Normalize,
}

/// <summary>
///     One field of a sort: the field path and the direction.
/// </summary>
/// <param name="Field">The field path.</param>
/// <param name="Descending">Whether the field sorts from the largest value (the default is ascending).</param>
internal readonly record struct SortField(string Field, bool Descending);

/// <content>
///     The <c>stack</c> transform and the stack engine that the encoding stage shares (a partial file of the aggregate transform unit).
/// </content>
internal sealed partial class AggregateTransforms
{
    /// <summary>
    ///     Gets the names of the two fields a stack writes.
    /// </summary>
    /// <param name="output">The <c>as</c> value of the transform: a name, two names or nothing.</param>
    /// <param name="field">The stacked field.</param>
    /// <returns>The names of the lower and the upper end: the name and <c>name_end</c> for one name, the two names, or <c>field_start</c> and <c>field_end</c>.</returns>
    internal static (string Start, string End) StackOutputNames(SpecNode? output, string field)
    {
        if (output is { IsString: true } single)
        {
            return (single.StringValue!, single.StringValue + "_end");
        }

        if (output is { IsArray: true, Items.Count: 2 } pair && pair.Items.All(i => i.IsString))
        {
            return (pair.Items[0].StringValue!, pair.Items[1].StringValue!);
        }

        return (field + "_start", field + "_end");
    }

    /// <summary>
    ///     Stacks a field, for a <c>stack</c> transform of the specification.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The member that names the stacked field.</param>
    /// <returns>The input rows, in their order, with the two ends of the stack added.</returns>
    private DataTable Stack(DataTable input, SpecNode entry, SpecNode operation)
    {
        var field = operation.StringValue!;
        var offset = entry["offset"]?.AsString() switch
        {
            "center" => StackOffset.Center,
            "normalize" => StackOffset.Normalize,
            _ => StackOffset.Zero,
        };
        var (start, end) = StackOutputNames(entry["as"], field);
        return Stack(input, field, ReadGroupBy(entry), ReadSortFields(entry["sort"]), offset, start, end, Locate(entry));
    }

    /// <summary>
    ///     Stacks the values of a field within groups and writes the lower and the upper end of every row to two new fields.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the engine of the <c>stack</c> transform; the encoding stage of a chart calls it too (with the group-by fields
    ///         of the dimension channel, the stack-by sort of Vega-Lite and the names <c>field_start</c> and <c>field_end</c>). The rows are
    ///         grouped by structural keys; inside a group they are stacked in the order of <paramref name="sort"/> (a stable sort: null
    ///         first, NaN next, then by value, ties in the input order; without a sort the input order). The table keeps its rows in
    ///         place and gains the two fields.
    ///     </para>
    ///     <para>
    ///         The values are read as numbers like JavaScript's unary plus (null is 0, text that is not a number is NaN). With
    ///         <see cref="StackOffset.Zero"/> a value below 0 stacks downwards from 0 and any other value (a NaN included) upwards;
    ///         <see cref="StackOffset.Center"/> and <see cref="StackOffset.Normalize"/> stack the absolute values, centered
    ///         ((widest sum − group sum) / 2 below the first row) or scaled by 1 / group sum. The arithmetic is IEEE: a NaN value makes the
    ///         ends of the rows after it NaN, a NaN sum makes every end of its group NaN under the other offsets, and a group that sums to 0
    ///         under <see cref="StackOffset.Normalize"/> gets NaN ends (0 times infinity).
    ///     </para>
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="field">The field path that is stacked.</param>
    /// <param name="groupBy">The group-by field paths (none: one group).</param>
    /// <param name="sort">The sort fields that order the rows inside a group (none: the input order).</param>
    /// <param name="offset">The baseline.</param>
    /// <param name="startName">The name of the field that receives the lower end (a literal name).</param>
    /// <param name="endName">The name of the field that receives the upper end (a literal name).</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <returns>The table with the two ends added.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    internal DataTable Stack(
        DataTable input,
        string field,
        IReadOnlyList<string> groupBy,
        IReadOnlyList<SortField> sort,
        StackOffset offset,
        string startName,
        string endName,
        VegaLiteLocation location)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            CheckTexts("A field of the stack", location, [field, startName, endName], groupBy, sort.Select(s => s.Field));
            var layout = new TableLayout(input, [TableLayout.Canonical(startName), TableLayout.Canonical(endName)], keepInput: true, _context.Limits, location);
            var width = layout.Fields.Length;
            var rows = input.RowCount;
            var cells = layout.Allocate(rows);
            for (var row = 0; row < rows; row++)
            {
                _context.Tick(row);
                _meter.Add(1);
                layout.CopyRow(input, row, cells, row);
            }

            if (rows > 0)
            {
                StackGroups(input, field, groupBy, sort, offset, location, (row, lower, upper) =>
                {
                    cells[((long)row * width) + layout.Added[0]] = DataValue.FromNumber(lower);
                    cells[((long)row * width) + layout.Added[1]] = DataValue.FromNumber(upper);
                });
            }

            return layout.Build(cells, rows);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw Located(ex, location);
        }
    }

    /// <summary>
    ///     Computes the two ends of every row of a non-empty table.
    /// </summary>
    /// <param name="input">The input table (at least one row).</param>
    /// <param name="field">The stacked field.</param>
    /// <param name="groupBy">The group-by field paths.</param>
    /// <param name="sort">The sort fields.</param>
    /// <param name="offset">The baseline.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <param name="sink">Receives the row, the lower end and the upper end.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded.</exception>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
    private void StackGroups(
        DataTable input,
        string field,
        IReadOnlyList<string> groupBy,
        IReadOnlyList<SortField> sort,
        StackOffset offset,
        VegaLiteLocation location,
        Action<int, double, double> sink)
    {
        var rows = input.RowCount;
        var (groupOf, groups) = GroupRows(input, groupBy, 2 + groupBy.Count, location, reserveRows: false);
        var (order, starts, largest) = Partition(groupOf, groups.Count);

        // The values as numbers, and the sum of the absolute values of every group
        var reader = new FieldReader(input, field);
        var evaluation = _context.Evaluator.Context;
        var numbers = new double[rows];
        var sums = new double[groups.Count];
        for (var row = 0; row < rows; row++)
        {
            _context.Tick(row);
            _meter.Add(1);
            numbers[row] = ExpressionEvaluator.ToNumber(reader.Read(row, evaluation), evaluation);
            sums[groupOf[row]] += Math.Abs(numbers[row]);
        }

        // A NaN sum is never larger than the maximum, as in Vega
        var max = 0.0;
        foreach (var sum in sums.Where(s => s > max))
        {
            max = sum;
        }

        Func<int, int, int>? compare = null;
        int[]? buffer = null;
        if (sort.Count > 0)
        {
            compare = RowComparison(ReadSortKeys(input, sort, location), sort);
            buffer = new int[largest];
        }

        for (var g = 0; g < groups.Count; g++)
        {
            var first = starts[g];
            var count = starts[g + 1] - first;
            if (compare is not null)
            {
                SortRows(order, first, count, buffer!, compare);
            }

            StackGroup(order, first, count, numbers, sums[g], max, offset, sink);
            _context.Tick(g);
            _meter.Add(count);
        }
    }

    /// <summary>
    ///     Stacks the rows of one group, which are already in stacking order.
    /// </summary>
    /// <param name="order">The row numbers.</param>
    /// <param name="first">The first position of the group in <paramref name="order"/>.</param>
    /// <param name="count">The number of rows of the group.</param>
    /// <param name="numbers">The value of every row.</param>
    /// <param name="sum">The sum of the absolute values of the group.</param>
    /// <param name="max">The largest sum of any group.</param>
    /// <param name="offset">The baseline.</param>
    /// <param name="sink">Receives the row, the lower end and the upper end.</param>
    private static void StackGroup(int[] order, int first, int count, double[] numbers, double sum, double max, StackOffset offset, Action<int, double, double> sink)
    {
        double last, running;
        switch (offset)
        {
            case StackOffset.Center:
                last = (max - sum) / 2;
                for (var j = 0; j < count; j++)
                {
                    var row = order[first + j];
                    var lower = last;
                    last += Math.Abs(numbers[row]);
                    sink(row, lower, last);
                }

                break;
            case StackOffset.Normalize:
                var scale = 1 / sum;
                last = 0;
                running = 0;
                for (var j = 0; j < count; j++)
                {
                    var row = order[first + j];
                    var lower = last;
                    running += Math.Abs(numbers[row]);
                    last = scale * running;
                    sink(row, lower, last);
                }

                break;
            default:
                double positive = 0, negative = 0;
                for (var j = 0; j < count; j++)
                {
                    var row = order[first + j];
                    var v = numbers[row];
                    if (v < 0)
                    {
                        var lower = negative;
                        negative += v;
                        sink(row, lower, negative);
                    }
                    else
                    {
                        var lower = positive;
                        positive += v;
                        sink(row, lower, positive);
                    }
                }

                break;
        }
    }

    /// <summary>
    ///     Lists the rows of every group, each group's rows in input order, in one array.
    /// </summary>
    /// <param name="groupOf">The group of every row.</param>
    /// <param name="groupCount">The number of groups.</param>
    /// <returns>The row numbers grouped by group, the start of every group (and the end of the last), and the size of the largest group.</returns>
    private static (int[] Order, int[] Starts, int Largest) Partition(int[] groupOf, int groupCount)
    {
        var starts = new int[groupCount + 1];
        foreach (var group in groupOf)
        {
            starts[group + 1]++;
        }

        var largest = 0;
        for (var g = 0; g < groupCount; g++)
        {
            largest = Math.Max(largest, starts[g + 1]);
            starts[g + 1] += starts[g];
        }

        var order = new int[groupOf.Length];
        var fill = new int[groupCount];
        for (var row = 0; row < groupOf.Length; row++)
        {
            var g = groupOf[row];
            order[starts[g] + fill[g]++] = row;
        }

        return (order, starts, largest);
    }
}
