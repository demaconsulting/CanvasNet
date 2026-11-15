using System.Globalization;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     What <see cref="MarkBuilder"/> records for the positions of its mark items, when <see cref="RecordPositions"/> is set:
///     the row each item draws, the vertices of the path marks, and the normalized values and data fields of a row.
/// </content>
internal sealed partial class MarkBuilder
{
    /// <summary>
    ///     Gets a value indicating whether the mark items record the rows they draw (<see cref="SceneItem.Datum"/>, the
    ///     vertices of the path marks) and the marks group gets a <see cref="SceneMarkContext"/>; <see langword="false"/> by
    ///     default, so a render without a position handler records nothing.
    /// </summary>
    public bool RecordPositions { get; init; }

    /// <summary>
    ///     Converts a data value to a position value: numbers (a value that is not finite is null), text, booleans and
    ///     null as they are, a date as canonical UTC ISO-8601 text.
    /// </summary>
    /// <param name="value">The data value.</param>
    /// <param name="result">The position value.</param>
    /// <returns><see langword="false"/> for an array, an object or a regular expression, which are left out.</returns>
    internal static bool TryConvert(DataValue value, out PositionValue result)
    {
        switch (value.Kind)
        {
            case DataValueKind.Null:
                result = PositionValue.Null;
                return true;
            case DataValueKind.Boolean:
                result = PositionValue.FromBoolean(value.Boolean);
                return true;
            case DataValueKind.Number:
                result = double.IsFinite(value.Number) ? PositionValue.FromNumber(value.Number) : PositionValue.Null;
                return true;
            case DataValueKind.String:
                result = PositionValue.FromString(value.Text ?? string.Empty);
                return true;
            case DataValueKind.Date:
                var instant = DateTime.UnixEpoch.AddMilliseconds(value.Milliseconds);
                result = PositionValue.FromString(instant.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                return true;
            default:
                result = PositionValue.Null;
                return false;
        }
    }

    /// <summary>
    ///     Converts a data value to the position value of a data field: as <see cref="TryConvert"/>, except that a date
    ///     read from text is that text, so that a data field shows (and a selector matches) the value as the data gave it.
    /// </summary>
    /// <param name="value">The data value.</param>
    /// <param name="result">The position value.</param>
    /// <returns><see langword="false"/> for an array, an object or a regular expression, which are left out.</returns>
    internal static bool TryConvertData(DataValue value, out PositionValue result)
    {
        if (value.SourceText is { } text)
        {
            result = PositionValue.FromString(text);
            return true;
        }

        return TryConvert(value, out result);
    }

    /// <content>
    ///     The values of the rows of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>
        ///     Gets the row an item records: the row when the render records positions, else -1.
        /// </summary>
        /// <param name="row">The row the item draws.</param>
        /// <returns>The row to record.</returns>
        private int Datum(int row) => _owner.RecordPositions ? row : -1;

        /// <summary>
        ///     Creates the position context of the marks group of this run.
        /// </summary>
        /// <remarks>
        ///     Every channel of the encoding that reads a field of the table or has a scalar <c>datum</c> gives a value,
        ///     named by the channel: the normalized value after the transforms (the start of a bin or time unit, an
        ///     aggregate), for a stacked channel the value of the row before the stack, and the end of a bin or time unit
        ///     as the secondary channel (<c>x2</c>, <c>y2</c>). The first definition of a channel counts; the facet
        ///     channels are added by the cell. The data of a row are its fields as <see cref="DataFields"/> selects them.
        /// </remarks>
        /// <returns>The context.</returns>
        public SceneMarkContext CreateContext()
        {
            // Work out once which table field (or constant) gives each channel
            var sources = new List<(string Name, int Field, DataValue? Constant)>();
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (var channel in _plan?.Channels ?? [])
            {
                if (channel.Channel is "row" or "column" or "facet" || !named.Add(channel.Channel))
                {
                    continue;
                }

                var path = channel.IsStacked ? channel.ValueField : channel.FieldPath;
                if (channel.FieldDefinition is null && path is not null && _table.FindField(path) is var field and >= 0)
                {
                    sources.Add((channel.Channel, field, null));
                }
                else if (DatumOf(channel.Definition) is { } constant)
                {
                    sources.Add((channel.Channel, -1, constant));
                }

                // The end of a bin or a time unit is the secondary channel, unless the encoding names one itself
                if (!channel.IsStacked && (channel.Bin == BinKind.Binning || channel.HasBandEnd)
                    && channel.Field2Path is { } end && _table.FindField(end) is var endField and >= 0
                    && channel.Channel is "x" or "y" or "theta" or "radius")
                {
                    sources.Add((channel.Channel + "2", endField, null));
                }
            }

            // Keep the first source of each name (a secondary channel the encoding names wins over a derived end)
            var distinct = sources.GroupBy(source => source.Name, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(source => source.Name, StringComparer.Ordinal)
                .ToArray();
            var table = _table;
            var data = DataFields();
            return new SceneMarkContext(row =>
            {
                var values = new List<KeyValuePair<string, PositionValue>>(distinct.Length);
                foreach (var (name, field, constant) in distinct)
                {
                    var value = field >= 0 ? table.GetValue(row, field) : constant ?? DataValue.Null;
                    if (TryConvert(value, out var converted))
                    {
                        values.Add(new KeyValuePair<string, PositionValue>(name, converted));
                    }
                }

                return values;
            })
            {
                Data = row =>
                {
                    // Scalar fields only, in name order, up to the cap of a position
                    var fields = new List<KeyValuePair<string, PositionValue>>(Math.Min(data.Length, Positions.ImagePosition.MaxDataFields));
                    foreach (var (name, field) in data)
                    {
                        if (TryConvertData(table.GetValue(row, field), out var converted))
                        {
                            fields.Add(new KeyValuePair<string, PositionValue>(name, converted));
                            if (fields.Count == Positions.ImagePosition.MaxDataFields)
                            {
                                break;
                            }
                        }
                    }

                    return fields;
                }
            };
        }

        /// <summary>
        ///     Selects the fields of the table that a mark reports as its data, sorted by name.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The data of a mark is the row it draws as the specification's data and transforms made it, before the
        ///         encoding: the top-level fields (a field of a nested object or array is left out, and so is the object
        ///         itself), without the helper fields the encoding adds for its channels (the start, end and range of a
        ///         bin, the start and end of a time unit, and the start and end of a stack), which <see cref="TryConvert"/>
        ///         already reports as the channel values.
        ///     </para>
        ///     <para>
        ///         An aggregated mark draws a row of the aggregate, which has only its group-by fields and its measures
        ///         (<c>sum_revenue</c>, <c>__count</c>); a group-by field is kept even when it is a helper field (a time
        ///         unit or bin dimension such as <c>month_month</c>), because the source field no longer exists there.
        ///     </para>
        /// </remarks>
        /// <returns>The name and table position of every candidate field, sorted by name with ordinal comparison.</returns>
        private (string Name, int Field)[] DataFields()
        {
            // The helper fields the encoding derives, by canonical name, except the group-by fields of an aggregate
            var helpers = new HashSet<string>(StringComparer.Ordinal);
            if (_plan is { } plan)
            {
                foreach (var bin in plan.Bins)
                {
                    helpers.Add(FieldPath.Escape(bin.StartName));
                    helpers.Add(FieldPath.Escape(bin.EndName));
                    if (bin.RangeName is { } range)
                    {
                        helpers.Add(FieldPath.Escape(range));
                    }
                }

                foreach (var unit in plan.TimeUnits)
                {
                    // A binned unit's start is the source field itself
                    if (!unit.Spec.IsBinned)
                    {
                        helpers.Add(FieldPath.Escape(unit.StartName));
                    }

                    helpers.Add(unit.EndPath);
                }

                if (plan.Stack is { } stack)
                {
                    helpers.Add(FieldPath.Escape(stack.StartName));
                    helpers.Add(FieldPath.Escape(stack.EndName));
                }

                foreach (var group in plan.Aggregate?.GroupBy ?? [])
                {
                    if (FieldPath.Canonicalize(group) is { } canonical)
                    {
                        helpers.Remove(canonical);
                    }
                }
            }

            // The top-level fields: one path segment, and not the parent of a nested field
            var fields = _table.Fields;
            var parsed = fields.Select(FieldPath.Parse).ToArray();
            var parents = new HashSet<string>(
                parsed.Where(segments => segments is { Count: > 1 }).Select(segments => segments![0]),
                StringComparer.Ordinal);
            return Enumerable.Range(0, fields.Count)
                .Where(i => parsed[i] is { Count: 1 } && !helpers.Contains(fields[i]) && !parents.Contains(parsed[i]![0]))
                .Select(i => (Name: parsed[i]![0], Field: i))
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        ///     Reads the scalar <c>datum</c> of a channel definition.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <returns>The datum, or null when the definition has none or it is not a number, a text or a boolean.</returns>
        private static DataValue? DatumOf(SpecNode definition) => definition["datum"] switch
        {
            { IsNumber: true } n => DataValue.FromNumber(n.NumberValue),
            { IsString: true } s => DataValue.FromString(s.StringValue),
            { IsBoolean: true } b => DataValue.FromBoolean(b.BoolValue),
            _ => null,
        };

        /// <summary>
        ///     Gets the vertices of the defined rows of a path group, when the render records positions.
        /// </summary>
        /// <param name="rows">The rows of all groups.</param>
        /// <param name="start">The position of the first row of the group.</param>
        /// <param name="length">The number of rows of the group.</param>
        /// <param name="state">The state of every row.</param>
        /// <param name="x">Gives the x of a row.</param>
        /// <param name="y">Gives the y of a row.</param>
        /// <returns>The vertices in drawing order, or null when the render records no positions.</returns>
        private SceneVertex[]? VerticesOf(int[] rows, int start, int length, RowState[] state, Func<int, double> x, Func<int, double> y)
        {
            if (!_owner.RecordPositions)
            {
                return null;
            }

            var vertices = new List<SceneVertex>(length);
            for (var k = start; k < start + length; k++)
            {
                var row = rows[k];
                if (state[row] == RowState.Defined)
                {
                    vertices.Add(new SceneVertex(row, x(row), y(row)));
                }
            }

            return [.. vertices];
        }
    }
}
