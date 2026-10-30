using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>aggregate</c> transform and the grouping engine that the other grouped transforms and the encoding stage share (a
///     partial file of the aggregate transform unit).
/// </content>
internal sealed partial class AggregateTransforms
{
    /// <summary>
    ///     Groups the rows and aggregates every group, for an <c>aggregate</c> transform of the specification.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The array of measures.</param>
    /// <returns>The table of the groups.</returns>
    private DataTable Aggregate(DataTable input, SpecNode entry, SpecNode operation)
    {
        var groupBy = entry["groupby"] is { IsArray: true } list ? list.Items.Select(i => i.StringValue!).ToArray() : [];
        return Aggregate(input, groupBy, ReadMeasures(operation), Locate(entry), RowSampler.Seed(entry.Pointer), releaseInput: true);
    }

    /// <summary>
    ///     Reads the measures of an <c>aggregate</c> transform.
    /// </summary>
    /// <param name="list">The array of <c>{op, field, param, as}</c> objects (validated by the parser).</param>
    /// <returns>The measures in the order of the specification.</returns>
    internal static List<AggregateSpec> ReadMeasures(SpecNode list)
    {
        var measures = new List<AggregateSpec>(list.Items.Count);
        foreach (var item in list.Items)
        {
            if (!AggregateMeasures.TryParse(item["op"]?.AsString(), out var op))
            {
                continue;
            }

            var field = item["field"]?.AsString();
            var param = item["param"] is { IsNumber: true } parameter ? parameter.NumberValue : AggregateSpec.DefaultExponentialParameter;
            measures.Add(new AggregateSpec(op, field, param, item["as"]?.AsString() ?? AggregateSpec.DefaultName(op, field)));
        }

        return measures;
    }

    /// <summary>
    ///     Groups the rows of a table by fields and aggregates every group.
    /// </summary>
    /// <remarks>
    ///     This is the engine of the <c>aggregate</c> transform; the encoding stage of a chart calls it too. The output has the group
    ///     fields (named as the fields are, so a nested path stays nested) followed by one field per measure, one row per group in
    ///     order of first appearance.
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="groupBy">The group-by field paths (may be empty: one group).</param>
    /// <param name="measures">The measures.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <param name="seed">The seed of the bootstrap of <c>ci0</c> and <c>ci1</c> (<see cref="RowSampler.Seed"/> of the JSON pointer of the transform).</param>
    /// <param name="releaseInput">Whether the rows of the input are given back to the row budget when the output is complete (false when the input stays in use).</param>
    /// <returns>The table of the groups.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    internal DataTable Aggregate(
        DataTable input,
        IReadOnlyList<string> groupBy,
        IReadOnlyList<AggregateSpec> measures,
        VegaLiteLocation location,
        ulong seed,
        bool releaseInput)
    {
        try
        {
            return AggregateRows(input, groupBy, measures, location, seed, releaseInput);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw Located(ex, location);
        }
    }

    /// <summary>
    ///     Implements <see cref="Aggregate(DataTable, IReadOnlyList{string}, IReadOnlyList{AggregateSpec}, VegaLiteLocation, ulong, bool)"/>.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="groupBy">The group-by field paths.</param>
    /// <param name="measures">The measures.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <param name="seed">The seed of the bootstrap.</param>
    /// <param name="releaseInput">Whether the input rows are released.</param>
    /// <returns>The table of the groups.</returns>
    private DataTable AggregateRows(
        DataTable input,
        IReadOnlyList<string> groupBy,
        IReadOnlyList<AggregateSpec> measures,
        VegaLiteLocation location,
        ulong seed,
        bool releaseInput)
    {
        // The texts of the specification are bounded by the limit of this call
        foreach (var path in groupBy)
        {
            _context.CheckText(DataValue.FromString(path), "A group-by field of the aggregation", location);
        }

        foreach (var measure in measures)
        {
            _context.CheckText(DataValue.FromString(measure.Name), "An output name of the aggregation", location);
            _context.CheckText(DataValue.FromString(measure.Field), "A field of the aggregation", location);
        }

        var groupNames = groupBy.Select(g => FieldPath.Canonicalize(g)!).ToArray();
        var layout = new TableLayout(
            input,
            [.. groupNames, .. measures.Select(m => TableLayout.Canonical(m.Name))],
            keepInput: false,
            _context.Limits,
            location);
        var width = layout.Fields.Length;

        // The values that some operations keep are bounded before the first row is read
        var storing = measures.Count(m => MeasureCell.StoresValues(m.Op));
        if ((long)input.RowCount * storing > TableLayout.MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The aggregation would keep up to {(long)input.RowCount * storing} values in memory, more than the limit of {TableLayout.MaxCells} values (TableLayout.MaxCells).",
                location);
        }

        var perGroup = width + ((long)measures.Count * MeasureCell.StateCells);
        var (groupOf, groups) = GroupRows(input, groupBy, perGroup, location);

        // Second pass: every row goes to the cells of its group
        var count = measures.Count;
        var states = new MeasureCell[(long)groups.Count * count];
        var readers = new List<FieldReader>();
        var readerOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var measureReader = new int[count];
        for (var m = 0; m < count; m++)
        {
            var reader = -1;
            if (measures[m].Op != AggregateOp.Count && measures[m].Field is { } field && !readerOf.TryGetValue(field, out reader))
            {
                reader = readers.Count;
                readerOf[field] = reader;
                readers.Add(new FieldReader(input, field));
            }

            measureReader[m] = reader;
        }

        var evaluation = _context.Evaluator.Context;
        var values = new DataValue[readers.Count];
        for (var row = 0; row < input.RowCount; row++)
        {
            _context.Tick(row);
            _meter.Add(count);
            for (var r = 0; r < readers.Count; r++)
            {
                values[r] = readers[r].Read(row, evaluation);
            }

            var cell = (long)groupOf[row] * count;
            for (var m = 0; m < count; m++)
            {
                states[cell + m].Add(measures[m].Op, measures[m].Param, measureReader[m] < 0 ? default : values[measureReader[m]], row, _environment);
            }
        }

        // Third pass: the output, a row per group
        var cells = layout.Allocate(groups.Count);
        var whole = measures.Any(m => AggregateMeasures.IsArgOp(m.Op)) ? new FieldReader(input, string.Empty) : null;
        Func<int, DataValue>? rowOf = whole is null ? null : r => whole.Read(r, evaluation);
        for (var g = 0; g < groups.Count; g++)
        {
            _context.Tick(g);
            var offset = (long)g * width;
            for (var f = 0; f < groupNames.Length; f++)
            {
                cells[offset + layout.Added[f]] = groups[g][f];
            }

            var groupSeed = MeasureCell.GroupSeed(seed, g);
            for (var m = 0; m < count; m++)
            {
                cells[offset + layout.Added[groupNames.Length + m]] = states[((long)g * count) + m].Result(measures[m].Op, measures[m].Param, groupSeed, _environment, rowOf);
            }
        }

        if (releaseInput)
        {
            _context.Loader.ReleaseRows(input.RowCount);
        }

        return layout.Build(cells, groups.Count);
    }

    /// <summary>
    ///     Assigns every row to a group: the rows with the same values of the group-by fields, in order of first appearance.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="groupBy">The group-by field paths; none puts every row in one group.</param>
    /// <param name="perGroup">The number of cells that a group costs in the state and the output.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <returns>The group of every row, and the values of the group-by fields of every group.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the groups exceed the row budget or the cell limit.</exception>
    private (int[] GroupOf, List<DataValue[]> Groups) GroupRows(DataTable input, IReadOnlyList<string> groupBy, long perGroup, VegaLiteLocation location)
    {
        var groupOf = new int[input.RowCount];
        var groups = new List<DataValue[]>();
        if (groupBy.Count == 0)
        {
            if (input.RowCount > 0)
            {
                NewGroup(0, perGroup, location);
                groups.Add([]);
            }

            return (groupOf, groups);
        }

        var readers = groupBy.Select(g => new FieldReader(input, g)).ToArray();
        var index = new Dictionary<GroupKey, int>();
        for (var row = 0; row < input.RowCount; row++)
        {
            _context.Tick(row);
            _meter.Add(1 + readers.Length);
            var key = BuildKey(readers, row);
            if (!index.TryGetValue(key, out var group))
            {
                group = groups.Count;
                NewGroup(group, perGroup, location);
                index[key] = group;
                groups.Add(key.Values);
            }

            groupOf[row] = group;
        }

        return (groupOf, groups);
    }

    /// <summary>
    ///     Accounts for a new group: its output row in the row budget and its cells in the cell limit.
    /// </summary>
    /// <param name="group">The number of the group (zero based).</param>
    /// <param name="perGroup">The number of cells a group costs.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the group exceeds a limit.</exception>
    private void NewGroup(int group, long perGroup, VegaLiteLocation location)
    {
        if (((long)group + 1) * perGroup > TableLayout.MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The aggregation has more than {group} groups of {perGroup} cells each, more than the limit of {TableLayout.MaxCells} cells (TableLayout.MaxCells).",
                location);
        }

        _context.Loader.ReserveDerivedRows(1, "aggregate transform output", location);
    }
}
