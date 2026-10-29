using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <content>
///     The sample and lookup transforms (a partial file of the row transform unit).
/// </content>
internal sealed partial class RowTransforms
{
    /// <summary>The name of the output field of a lookup that copies the whole row and names no field.</summary>
    private const string DefaultLookupField = "data";

    /// <summary>
    ///     Keeps a deterministic sample of the rows.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The sample size.</param>
    /// <returns>The table of the sampled rows in source order; the input itself when the size is not less than the number of rows.</returns>
    private DataTable Sample(DataTable input, SpecNode entry, SpecNode operation)
    {
        var size = (int)Math.Min(Math.Floor(operation.NumberValue), int.MaxValue);
        if (size >= input.RowCount)
        {
            return input;
        }

        var rows = RowSampler.Select(input.RowCount, size, entry.Pointer, Cancellation);
        return SelectRows(input, rows);
    }

    /// <summary>
    ///     Looks up fields of the first matching row of another data source.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The field whose value is looked up.</param>
    /// <returns>The table with the looked-up fields; the input itself for a lookup in the data of a parameter, which is not supported.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the source cannot be loaded or exceeds a limit, or the output exceeds the field or cell limits.</exception>
    private DataTable Lookup(DataTable input, SpecNode entry, SpecNode operation)
    {
        var from = entry["from"]!;
        if (from["data"] is not { IsObject: true } source)
        {
            return input;
        }

        var location = Locate(entry);

        // The secondary rows come from the loader of the call: same sandbox, file cache, file and byte limits, row budget
        var secondary = _context.Loader.Load(source);
        var keyReader = new FieldReader(secondary, from["key"]!.StringValue);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var row = 0; row < secondary.RowCount; row++)
        {
            Tick(row);
            if (KeyText(keyReader.Read(row)) is { } key)
            {
                index.TryAdd(key, row);
            }
        }

        // What is copied, and the names it goes to
        var paths = from["fields"] is { IsArray: true } fields ? fields.Items.Select(f => f.StringValue!).ToArray() : [];
        var given = entry["as"] switch
        {
            { IsString: true } single => new[] { single.StringValue! },
            { IsArray: true } list => list.Items.Select(n => n.StringValue!).ToArray(),
            _ => [],
        };
        string[] outputNames = [.. paths.Select((path, i) => i < given.Length ? TableLayout.Canonical(given[i]) : FieldPath.Canonicalize(path)!)];
        var wholeRow = paths.Length == 0;
        if (wholeRow)
        {
            outputNames = [TableLayout.Canonical(given.Length > 0 ? given[0] : DefaultLookupField)];
        }

        var readers = wholeRow ? new[] { new FieldReader(secondary, string.Empty) } : paths.Select(p => new FieldReader(secondary, p)).ToArray();
        var fallback = entry["default"] is { } defaultNode ? SpecValues.ToDataValue(defaultNode) : DataValue.Null;

        var layout = new TableLayout(input, outputNames, keepInput: true, _context.Limits, location);
        var cells = layout.Allocate(input.RowCount);
        var width = layout.Fields.Length;
        var primary = new FieldReader(input, operation.StringValue);
        var wholeRows = new Dictionary<int, DataValue>();
        for (var row = 0; row < input.RowCount; row++)
        {
            Tick(row);
            layout.CopyRow(input, row, cells, row);
            var key = KeyText(primary.Read(row));
            var matched = key is not null && index.TryGetValue(key, out var hit) ? hit : -1;
            for (var j = 0; j < outputNames.Length; j++)
            {
                DataValue value;
                if (matched < 0)
                {
                    value = fallback;
                }
                else
                {
                    value = wholeRow ? WholeRow(readers[0], matched, wholeRows) : readers[j].Read(matched);
                }

                cells[((long)row * width) + layout.Added[j]] = value;
            }
        }

        return layout.Build(cells, input.RowCount);
    }

    /// <summary>
    ///     Gets the text that a lookup compares: the text of a string, number, boolean or date, so that <c>1</c> and <c>"1"</c> match.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text; null for null and for arrays, objects and regular expressions, which never match.</returns>
    private static string? KeyText(DataValue value) =>
        value.IsString || value.IsNumber || value.IsBoolean || value.IsDate ? value.ToText() : null;

    /// <summary>
    ///     Gets the whole secondary row as an object, rebuilding every row once.
    /// </summary>
    /// <param name="reader">The reader of the whole row.</param>
    /// <param name="row">The secondary row.</param>
    /// <param name="cache">The rows rebuilt so far.</param>
    /// <returns>The object.</returns>
    private static DataValue WholeRow(FieldReader reader, int row, Dictionary<int, DataValue> cache)
    {
        if (!cache.TryGetValue(row, out var value))
        {
            value = reader.Read(row);
            cache[row] = value;
        }

        return value;
    }
}
