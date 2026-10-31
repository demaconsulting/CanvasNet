// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The <c>impute</c> transform (a partial file of the aggregate transform unit).
/// </content>
internal sealed partial class AggregateTransforms
{
    /// <summary>
    ///     Appends the missing (group, key) combinations to the rows, for an <c>impute</c> transform of the specification.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The member that names the imputed field.</param>
    /// <returns>The input rows followed by one imputed row for every missing combination of group and key.</returns>
    private DataTable Impute(DataTable input, SpecNode entry, SpecNode operation)
    {
        var (method, value, frame) = ReadImputeParameters(entry);
        return ImputeField(
            input,
            operation.StringValue!,
            entry["key"]!.StringValue!,
            ReadGroupBy(entry),
            method,
            value,
            frame,
            entry["keyvals"],
            RowSampler.Seed(entry.Pointer),
            Locate(entry));
    }

    /// <summary>
    ///     Reads the parameters that an <c>impute</c> transform and an encoding <c>impute</c> object share.
    /// </summary>
    /// <param name="parameters">The transform object or the <c>impute</c> object of a channel definition (validated by the parser).</param>
    /// <returns>The method (<c>value</c> by default), the constant of the <c>value</c> method (0 by default) and the frame.</returns>
    internal static (string Method, DataValue Value, (int? Start, int? End) Frame) ReadImputeParameters(SpecNode parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var method = parameters["method"]?.AsString() ?? "value";
        var value = parameters["value"] is { } given ? SpecValues.ToDataValue(given) : DataValue.FromNumber(0);
        var frame = parameters["frame"] is { IsArray: true } frameNode ? ReadFrame(frameNode) : (null, null);
        return (method, value, frame);
    }

    /// <summary>
    ///     Appends the missing (group, key) combinations to the rows: the <c>impute</c> transform behind a typed entry, which the encoding
    ///     <c>impute</c> and the implicit impute of a stacked path mark call as well.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="field">The imputed field.</param>
    /// <param name="key">The key field.</param>
    /// <param name="groupBy">The group-by field paths.</param>
    /// <param name="method">The method: <c>value</c>, <c>mean</c>, <c>median</c>, <c>min</c> or <c>max</c>.</param>
    /// <param name="value">The constant of the <c>value</c> method.</param>
    /// <param name="frame">The frame of the other methods.</param>
    /// <param name="keyValues">The <c>keyvals</c> array or sequence <c>{start, stop, step}</c>, or null.</param>
    /// <param name="seed">The seed of the transform.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <param name="releaseInput">Whether the rows of <paramref name="input"/> are given back to the row budget (the table is replaced by the result); false when the caller keeps the input table in use.</param>
    /// <returns>The input rows followed by one imputed row for every missing combination of group and key; <paramref name="input"/> itself when it has no rows.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    internal DataTable ImputeField(
        DataTable input,
        string field,
        string key,
        string[] groupBy,
        string method,
        DataValue value,
        (int? Start, int? End) frame,
        SpecNode? keyValues,
        ulong seed,
        VegaLiteLocation location,
        bool releaseInput = true)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(groupBy);
        ArgumentNullException.ThrowIfNull(method);
        try
        {
            CheckTexts("A field of the impute", location, [field, key], groupBy);
            _context.CheckText(value, "The value of the impute", location);
            if (input.RowCount == 0)
            {
                return input;
            }

            var domain = new List<DataValue>();
            var domainIndex = new Dictionary<GroupKey, int>();
            ReadKeyValues(keyValues, domain, domainIndex, location);
            return ImputeRows(input, field, key, groupBy, method, value, frame, domain, domainIndex, seed, location, releaseInput);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw Located(ex, location);
        }
    }

    /// <summary>
    ///     Reads the <c>keyvals</c> of an impute into the key domain, bounding their number before they are materialized.
    /// </summary>
    /// <param name="keyValues">The array of values or the sequence <c>{start, stop, step}</c>, or null.</param>
    /// <param name="domain">Receives the distinct key values in order.</param>
    /// <param name="index">Receives the position of every key.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the number of key values exceeds the row limit.</exception>
    private void ReadKeyValues(SpecNode? keyValues, List<DataValue> domain, Dictionary<GroupKey, int> index, VegaLiteLocation location)
    {
        if (keyValues is null)
        {
            return;
        }

        var limit = _context.Limits.MaxDataRows;
        double count;
        double start = 0, step = 1;
        if (keyValues.IsArray)
        {
            count = keyValues.Items.Count;
        }
        else if (keyValues.IsObject)
        {
            start = keyValues["start"]?.AsNumber() ?? 0;
            step = keyValues["step"]?.AsNumber() ?? 1;
            var stop = keyValues["stop"]!.NumberValue;
            count = Math.Max(0, Math.Ceiling((stop - start) / step));
        }
        else
        {
            return;
        }

        // Every key is at least one imputed row for a group, so more keys than rows can never fit
        if (!(count <= limit))
        {
            throw new VegaLiteRenderException(
                $"The keyvals of the impute have {(double.IsFinite(count) ? count.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "an unrepresentable number of")} values, more than the limit of {limit} (MaxDataRows).",
                location);
        }

        for (var i = 0; i < (int)count; i++)
        {
            _context.Tick(i);
            _meter.Add(1);
            var value = keyValues.IsArray ? SpecValues.ToDataValue(keyValues.Items[i]) : DataValue.FromNumber(start + (i * step));
            _context.CheckText(value, "A key value of the impute", location);
            if (index.TryAdd(new GroupKey([value], [value], null), domain.Count))
            {
                domain.Add(value);
            }
        }
    }

    /// <summary>
    ///     Imputes the missing combinations of group and key of a non-empty table.
    /// </summary>
    /// <param name="input">The input table (at least one row).</param>
    /// <param name="field">The imputed field.</param>
    /// <param name="key">The key field.</param>
    /// <param name="groupBy">The group-by field paths.</param>
    /// <param name="method">The method: <c>value</c>, <c>mean</c>, <c>median</c>, <c>min</c> or <c>max</c>.</param>
    /// <param name="value">The constant of the <c>value</c> method.</param>
    /// <param name="frame">The frame of the other methods.</param>
    /// <param name="domain">The key values the specification requires; the keys of the data are appended.</param>
    /// <param name="domainIndex">The position of every key of the domain.</param>
    /// <param name="seed">The seed of the transform.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <param name="releaseInput">Whether the rows of the input are given back to the row budget once the result exists.</param>
    /// <returns>The table with the imputed rows appended.</returns>
    private DataTable ImputeRows(
        DataTable input,
        string field,
        string key,
        string[] groupBy,
        string method,
        DataValue value,
        (int? Start, int? End) frame,
        List<DataValue> domain,
        Dictionary<GroupKey, int> domainIndex,
        ulong seed,
        VegaLiteLocation location,
        bool releaseInput)
    {
        var rows = input.RowCount;
        var evaluation = _context.Evaluator.Context;

        // Pass 1: the group of every row and the position of its key in the key domain
        var (groupOf, groups) = GroupRows(input, groupBy, 2 + groupBy.Length, location, reserveRows: false);
        var keyReader = new FieldReader(input, key);
        var keyOf = new int[rows];
        var single = new FieldReader[] { keyReader };
        for (var row = 0; row < rows; row++)
        {
            _context.Tick(row);
            _meter.Add(2);
            var groupKey = BuildKey(single, row);
            if (!domainIndex.TryGetValue(groupKey, out var position))
            {
                position = domain.Count;
                domainIndex[groupKey] = position;
                domain.Add(groupKey.Values[0]);
            }

            keyOf[row] = position;
        }

        // The result has at least one row for every (group, key) combination, so that bound decides before anything is allocated
        var combinations = (long)groups.Count * domain.Count;
        _context.Loader.ReserveDerivedRows(combinations, "impute transform output", location);
        if (combinations > TableLayout.MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The impute has {groups.Count} groups and {domain.Count} keys, more than the limit of {TableLayout.MaxCells} combinations (TableLayout.MaxCells).",
                location);
        }

        var present = new ulong[(combinations + 63) / 64];
        long presentCount = 0;
        for (var row = 0; row < rows; row++)
        {
            _context.Tick(row);
            var bit = ((long)groupOf[row] * domain.Count) + keyOf[row];
            var mask = 1UL << (int)(bit & 63);
            if ((present[bit >> 6] & mask) == 0)
            {
                present[bit >> 6] |= mask;
                presentCount++;
            }
        }

        var total = rows + (combinations - presentCount);
        _context.Loader.ReserveDerivedRows(total - combinations, "impute transform output", location);

        // The layout keeps every input field and adds the group fields, the key and the imputed field where they do not exist
        var groupNames = groupBy.Select(g => FieldPath.Canonicalize(g)!).ToArray();
        var keyName = FieldPath.Canonicalize(key)!;
        var fieldName = FieldPath.Canonicalize(field)!;
        var names = new SortedSet<string>(input.Fields, StringComparer.Ordinal);
        names.UnionWith(groupNames);
        names.Add(keyName);
        names.Add(fieldName);
        if (names.Count > _context.Limits.MaxFieldCount)
        {
            throw new VegaLiteRenderException(
                $"The transform produces {names.Count} fields, more than the limit of {_context.Limits.MaxFieldCount} (MaxFieldCount).", location);
        }

        var fields = names.ToArray();
        var width = fields.Length;
        if (total > int.MaxValue || total * width > TableLayout.MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The transform produces a table of {total} rows with {width} fields, more than the {TableLayout.MaxCells} cells that a derived table may have.",
                location);
        }

        var slotOf = fields.Select((f, i) => (f, i)).ToDictionary(p => p.f, p => p.i, StringComparer.Ordinal);
        var cells = new DataValue[total * width];

        // The input rows keep their place; the combinations the groups lack follow in group order, then key order
        var inputPosition = input.Fields.Select(f => slotOf[f]).ToArray();
        for (var row = 0; row < rows; row++)
        {
            _context.Tick(row);
            _meter.Add(1);
            var source = input.GetRow(row);
            var offset = (long)row * width;
            for (var f = 0; f < inputPosition.Length; f++)
            {
                cells[offset + inputPosition[f]] = source[f];
            }
        }

        var constant = method == "value" ? value : default;
        var next = (long)rows;
        for (var g = 0; g < groups.Count; g++)
        {
            for (var k = 0; k < domain.Count; k++)
            {
                _context.Tick(next);
                var bit = ((long)g * domain.Count) + k;
                if ((present[bit >> 6] & (1UL << (int)(bit & 63))) != 0)
                {
                    continue;
                }

                var offset = next++ * width;
                _meter.Add(groupNames.Length + 1);
                for (var f = 0; f < groupNames.Length; f++)
                {
                    cells[offset + slotOf[groupNames[f]]] = groups[g][f];
                }

                cells[offset + slotOf[keyName]] = domain[k];
                cells[offset + slotOf[fieldName]] = constant;
            }
        }

        if (method != "value")
        {
            // Vega-Lite derives the imputed value with a window over the group in data order and replaces every null with it
            var table = DataTable.FromSortedCells(fields, cells, (int)total);
            AggregateMeasures.TryParse(method, out var op);
            var results = new DataValue[total];
            Window(
                table,
                groupBy,
                [],
                frame.Start,
                frame.End,
                ignorePeers: false,
                [new WindowSpec(null, op, field, AggregateSpec.DefaultExponentialParameter, "imputed")],
                seed,
                location,
                (row, _, result) => results[row] = result);
            var reader = new FieldReader(table, field);
            var fieldAt = slotOf[fieldName];
            for (var row = 0; row < total; row++)
            {
                _context.Tick(row);
                _meter.Add(1);
                if (reader.Read(row, evaluation).IsNull)
                {
                    cells[(row * width) + fieldAt] = results[row];
                }
            }
        }

        if (releaseInput)
        {
            _context.Loader.ReleaseRows(rows);
        }

        return DataTable.FromSortedCells(fields, cells, (int)total);
    }
}
