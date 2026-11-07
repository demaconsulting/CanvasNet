// Portions derived from Vega (BSD-3-Clause): the cells of the pivot transform of vega-transforms; see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The transforms that change the shape of the table: fold, flatten and pivot (a partial file of the row transform unit).
/// </content>
internal sealed partial class RowTransforms
{
    /// <summary>
    ///     Folds fields into key and value rows.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The array of fields to fold.</param>
    /// <returns>The folded table, with one row per input row and folded field.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the output has more rows, fields or cells than the limits allow.</exception>
    private DataTable Fold(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var names = entry["as"] is { IsArray: true } given ? given.Items.Select(n => n.StringValue!).ToArray() : [];
        var keyName = TableLayout.Canonical(names.Length > 0 ? names[0] : "key");
        var valueName = TableLayout.Canonical(names.Length > 1 ? names[1] : "value");
        var fields = operation.Items.Select(f => f.StringValue!).ToArray();
        var readers = fields.Select(f => new FieldReader(input, f)).ToArray();

        // The key of every folded row is the name of its field, a text of the specification
        foreach (var field in fields)
        {
            CheckText(DataValue.FromString(field), "A field name of the fold transform", location);
        }

        // The output rows are counted before anything is allocated; the input counts until the output is complete
        var outputRows = (long)input.RowCount * fields.Length;
        _context.Loader.ReserveDerivedRows(outputRows, "fold transform output", location);
        var layout = new TableLayout(input, [keyName, valueName], keepInput: true, _context.Limits, location);
        var cells = layout.Allocate(outputRows);
        var width = layout.Fields.Length;
        var keyPosition = layout.Added[0];
        var valuePosition = layout.Added[1];
        long target = 0;
        for (var row = 0; row < input.RowCount; row++)
        {
            Tick(row);
            for (var j = 0; j < fields.Length; j++)
            {
                layout.CopyRow(input, row, cells, target);
                cells[(target * width) + keyPosition] = DataValue.FromString(fields[j]);
                cells[(target * width) + valuePosition] = readers[j].Read(row);
                target++;
            }
        }

        _context.Loader.ReleaseRows(input.RowCount);
        return layout.Build(cells, (int)outputRows);
    }

    /// <summary>
    ///     Flattens array-valued fields into rows.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The array of fields to flatten.</param>
    /// <returns>The flattened table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the output has more rows, fields or cells than the limits allow.</exception>
    private DataTable Flatten(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var fields = operation.Items.Select(f => f.StringValue!).ToArray();
        var given = entry["as"] is { IsArray: true } names ? names.Items.Select(n => n.StringValue!).ToArray() : [];
        var outputNames = fields
            .Select((field, i) => i < given.Length ? TableLayout.Canonical(given[i]) : FieldPath.Canonicalize(field)!)
            .ToArray();
        var readers = fields.Select(f => new FieldReader(input, f)).ToArray();

        // First pass: how many rows every input row becomes, so that the output is counted before it is allocated
        var lengths = new int[input.RowCount];
        long total = 0;
        for (var row = 0; row < input.RowCount; row++)
        {
            Tick(row);
            var longest = 0;
            foreach (var reader in readers)
            {
                longest = Math.Max(longest, ElementCount(reader.Read(row)));
            }

            lengths[row] = longest;
            total += longest;
            if (total > _context.Limits.MaxDataRows)
            {
                _context.Loader.ReserveDerivedRows(total, "flatten transform output", location);
            }
        }

        _context.Loader.ReserveDerivedRows(total, "flatten transform output", location);
        var layout = new TableLayout(input, outputNames, keepInput: true, _context.Limits, location);
        var cells = layout.Allocate(total);
        var width = layout.Fields.Length;
        var values = new DataValue[readers.Length];
        long target = 0;
        for (var row = 0; row < input.RowCount; row++)
        {
            Tick(row);
            if (lengths[row] == 0)
            {
                continue;
            }

            for (var j = 0; j < readers.Length; j++)
            {
                values[j] = readers[j].Read(row);
            }

            for (var i = 0; i < lengths[row]; i++)
            {
                Tick(target);
                layout.CopyRow(input, row, cells, target);
                for (var j = 0; j < values.Length; j++)
                {
                    cells[(target * width) + layout.Added[j]] = ElementAt(values[j], i);
                }

                target++;
            }
        }

        _context.Loader.ReleaseRows(input.RowCount);
        return layout.Build(cells, (int)total);
    }

    /// <summary>
    ///     Counts the values a field value flattens to: the elements of an array, one for a scalar, none for null.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The count.</returns>
    private static int ElementCount(DataValue value)
    {
        if (value.IsArray)
        {
            return value.Elements.Length;
        }

        return value.IsNull ? 0 : 1;
    }

    /// <summary>
    ///     Gets one of the values a field value flattens to.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="index">The position.</param>
    /// <returns>The element; null when the value has fewer.</returns>
    private static DataValue ElementAt(DataValue value, int index)
    {
        if (value.IsArray)
        {
            var elements = value.Elements;
            return index < elements.Length ? elements[index] : default;
        }

        return index == 0 ? value : default;
    }

    /// <summary>
    ///     Pivots the values of a field into columns.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field to pivot.</param>
    /// <returns>The table with one row per group and a column per pivoted value.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the output has more fields or cells than the limits allow.</exception>
    private DataTable Pivot(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var pivotPath = operation.StringValue!;
        var limit = entry["limit"] is { IsNumber: true } limitNode ? (long)Math.Floor(limitNode.NumberValue) : 0;
        var pivotReader = new FieldReader(input, pivotPath);
        var valueReader = new FieldReader(input, entry["value"]!.StringValue);
        var groupPaths = entry["groupby"] is { IsArray: true } groupBy ? groupBy.Items.Select(g => g.StringValue!).ToArray() : [];
        var groupReaders = groupPaths.Select(g => new FieldReader(input, g)).ToArray();
        var groupNames = groupPaths.Select(g => FieldPath.Canonicalize(g)!).ToArray();

        // First pass: the group and the pivot text of every row
        var groupOf = new int[input.RowCount];
        var textOf = new string?[input.RowCount];
        var groups = new Dictionary<GroupKey, int>();
        var groupValues = new List<DataValue[]>();
        var texts = new SortedSet<string>(StringComparer.Ordinal);
        var cut = false;
        var skipped = new RowFinding();
        var meter = new WorkMeter(_context.Evaluator.Context);
        for (var row = 0; row < input.RowCount; row++)
        {
            Tick(row);
            var pivotValue = pivotReader.Read(row);
            if (pivotValue.IsNull)
            {
                groupOf[row] = -1;
                skipped.Add(row);
                continue;
            }

            var groupValuesOfRow = groupReaders.Select(r => r.Read(row)).ToArray();

            // Hashing and comparing the text of a group value walks it, so it is charged to the step budget
            foreach (var groupValue in groupValuesOfRow)
            {
                if (groupValue.IsString)
                {
                    Charge(meter, groupValue.Text!.Length, location);
                }
            }

            var key = new GroupKey(groupValuesOfRow);
            if (!groups.TryGetValue(key, out var group))
            {
                group = groupValues.Count;
                groups[key] = group;
                groupValues.Add(key.Values);
            }

            groupOf[row] = group;

            // The text of an array value is bounded like every text of an expression, and the failure is located at the pivot
            try
            {
                textOf[row] = ExpressionEvaluator.ToJsString(pivotValue, _context.Limits.MaxStringLength);
            }
            catch (ExpressionRuntimeException ex)
            {
                throw new VegaLiteRenderException($"The pivot field '{pivotPath}' has a value whose text is too long: {ex.Message}", location);
            }

            // The working set keeps only the columns that can still be in the output: with a limit, the smallest `limit` texts
            // (a text that sorts after them is counted as cut and dropped); without one, every text. The field bound applies to
            // this set, so it rejects a pivot only when the columns that would be output are too many, and the set never
            // grows beyond that bound plus one.
            var text = textOf[row]!;

            // Sorting the texts into the set compares them, which walks them
            Charge(meter, text.Length, location);
            if (limit > 0 && texts.Count >= limit && !texts.Contains(text))
            {
                if (string.CompareOrdinal(text, texts.Max) > 0)
                {
                    cut = true;
                    continue;
                }

                texts.Remove(texts.Max!);
                cut = true;
            }

            if (texts.Add(text) && texts.Count + groupNames.Length > _context.Limits.MaxFieldCount)
            {
                throw new VegaLiteRenderException(
                    $"The pivot produces more than {_context.Limits.MaxFieldCount} fields, more than the limit of {_context.Limits.MaxFieldCount} (VegaLiteLimits.MaxFieldCount).", location);
            }
        }

        if (skipped.Count > 0)
        {
            Warn("vl-transform-runtime", $"The pivot skipped the rows whose '{pivotPath}' is null ({skipped.Describe()}).", location);
        }

        // The columns: the pivot values in ordinal order, at most the limit
        var columns = texts.ToList();
        if (cut)
        {
            Warn(
                "vl-transform-runtime",
                string.Create(CultureInfo.InvariantCulture, $"The pivot field '{pivotPath}' has more than {limit} distinct values; only the first {limit} in ordinal order are pivoted."),
                location);
        }

        var columnNames = columns.Select(TableLayout.Canonical).ToArray();
        var collision = groupNames.Intersect(columnNames, StringComparer.Ordinal).FirstOrDefault();
        if (collision is not null)
        {
            Warn("vl-transform-runtime", $"The pivot value '{collision}' has the name of a group field and replaces it.", location);
        }

        // Every operation of Vega's aggregate engine is available; the states of the cells are bounded before anything is allocated
        var op = entry["op"]?.AsString() is { } opName && AggregateMeasures.TryParse(opName, out var parsed) ? parsed : AggregateOp.Sum;
        var cellRows = (long)groupValues.Count * columns.Count;
        if (cellRows * MeasureCell.StateCells > TableLayout.MaxCells
            || ((op is AggregateOp.Distinct or AggregateOp.Values) && (long)input.RowCount * columns.Count > TableLayout.MaxCells))
        {
            throw new VegaLiteRenderException(
                $"The pivot would hold {cellRows} cells of {groupValues.Count} groups and {columns.Count} columns, more than the limit of {TableLayout.MaxCells} cells (TableLayout.MaxCells).",
                location);
        }

        var layout = new TableLayout(input, [.. groupNames, .. columnNames], keepInput: false, _context.Limits, location);
        var cells = layout.Allocate(groupValues.Count);
        var width = layout.Fields.Length;
        var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var c = 0; c < columns.Count; c++)
        {
            columnIndex[columns[c]] = c;
        }

        // Second pass: the cells. A cell holds the rows of its group whose pivot value is the column (the other rows of the group are the NaN that Vega's
        // aggregator ignores), and a combination without such rows has the empty result of the operation (null for a sum, 0 for a count)
        var states = new MeasureCell[cellRows];
        var environment = new MeasureEnvironment(_context.Evaluator.Context, meter);
        var whole = op is AggregateOp.ArgMin or AggregateOp.ArgMax or AggregateOp.Values ? new FieldReader(input, string.Empty) : null;
        var evaluation = _context.Evaluator.Context;
        Func<int, DataValue>? rowOf = whole is null ? null : r => whole.Read(r, evaluation);
        try
        {
            FeedPivotCells(input, op, valueReader, groupOf, textOf, columnIndex, columns.Count, states, rowOf, environment);
            var seed = RowSampler.Seed(entry.Pointer);
            for (var g = 0; g < groupValues.Count; g++)
            {
                Tick(g);
                for (var f = 0; f < groupNames.Length; f++)
                {
                    cells[((long)g * width) + layout.Added[f]] = groupValues[g][f];
                }

                for (var c = 0; c < columns.Count; c++)
                {
                    var at = ((long)g * columns.Count) + c;

                    // Vega's pivot gives the exponential operations no weighting parameter, so their result is NaN
                    cells[((long)g * width) + layout.Added[groupNames.Length + c]] = states[at].Result(op, double.NaN, MeasureCell.GroupSeed(seed, at), environment, rowOf);
                }
            }
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, location);
        }

        _context.Loader.ReleaseRows(input.RowCount - groupValues.Count);
        return layout.Build(cells, groupValues.Count);
    }

    /// <summary>
    ///     Feeds the rows of a pivot to the aggregate cells of their group and column.
    /// </summary>
    /// <remarks>
    ///     Vega turns a pivot into an aggregate with one measure per column over <c>key(d) === k ? value(d) : NaN</c>. A measure ignores NaN, so a
    ///     row reaches only the cell of its own column; the exceptions are the operations that read every row of the group through the
    ///     cell's tuple store: <c>distinct</c> sees the NaN of the other columns (so a group with a row of another column counts one more distinct
    ///     text, "NaN"), and <c>values</c> is the array of all the rows of the group. The count is Vega's <c>__count__</c>, the rows with a value that
    ///     is not NaN (null and the empty text included).
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="op">The operation.</param>
    /// <param name="valueReader">The reader of the value field.</param>
    /// <param name="groupOf">The group of every row, or -1 for a row that is skipped.</param>
    /// <param name="textOf">The pivot text of every row.</param>
    /// <param name="columnIndex">The column of every pivot text that is kept.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="states">The cells (<c>group * columns + column</c>).</param>
    /// <param name="rowOf">Reads a whole row as an object, for <c>argmin</c>, <c>argmax</c> and <c>values</c>; null otherwise.</param>
    /// <param name="environment">The evaluation and the meter to charge.</param>
    /// <exception cref="ExpressionRuntimeException">Thrown when the step budget or the string limit is exceeded.</exception>
    private void FeedPivotCells(
        DataTable input,
        AggregateOp op,
        FieldReader valueReader,
        int[] groupOf,
        string?[] textOf,
        Dictionary<string, int> columnIndex,
        int columns,
        MeasureCell[] states,
        Func<int, DataValue>? rowOf,
        MeasureEnvironment environment)
    {
        var allRows = op is AggregateOp.Distinct or AggregateOp.Values;
        for (var row = 0; row < input.RowCount; row++)
        {
            Tick(row);
            if (groupOf[row] < 0)
            {
                continue;
            }

            var own = columnIndex.TryGetValue(textOf[row]!, out var column) ? column : -1;
            var value = valueReader.Read(row, environment.Context);
            var first = (long)groupOf[row] * columns;
            if (allRows)
            {
                // The operations that keep the tuples of the group see the row in every column of the group
                environment.Meter.Add(columns);
                var tuple = op == AggregateOp.Values ? rowOf!(row) : default;
                for (var c = 0; c < columns; c++)
                {
                    var seen = c == own ? value : DataValue.FromNumber(double.NaN);
                    states[first + c].Add(op, double.NaN, op == AggregateOp.Values ? tuple : seen, row, environment);
                }

                continue;
            }

            if (own < 0)
            {
                continue;
            }

            // The count is the number of values that are not NaN
            if (op == AggregateOp.Count && value.IsNumber && double.IsNaN(value.Number))
            {
                continue;
            }

            environment.Meter.Add(1);
            states[first + own].Add(op, double.NaN, value, row, environment);
        }
    }

    /// <summary>
    ///     Charges work proportional to a size to the step budget of the call.
    /// </summary>
    /// <param name="meter">The meter of the transform.</param>
    /// <param name="size">The number of characters touched.</param>
    /// <param name="location">The location reported when the budget is exhausted.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the step budget is exhausted.</exception>
    private static void Charge(WorkMeter meter, long size, VegaLiteLocation location)
    {
        try
        {
            meter.Add(size);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, location);
        }
    }

    /// <summary>
    ///     The values of the group fields of a row, as a key that compares by value.
    /// </summary>
    private sealed class GroupKey : IEquatable<GroupKey>
    {
        /// <summary>The hash code, computed once.</summary>
        private readonly int _hash;

        /// <summary>
        ///     Initializes a key.
        /// </summary>
        /// <param name="values">The values; ownership passes to the key.</param>
        public GroupKey(DataValue[] values)
        {
            Values = values;
            var hash = new HashCode();
            foreach (var value in values)
            {
                hash.Add(value);
            }

            _hash = hash.ToHashCode();
        }

        /// <summary>Gets the values.</summary>
        public DataValue[] Values { get; }

        /// <inheritdoc/>
        public bool Equals(GroupKey? other) => other is not null && Values.AsSpan().SequenceEqual(other.Values);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as GroupKey);

        /// <inheritdoc/>
        public override int GetHashCode() => _hash;
    }
}
