using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     Reads the data of a view from the <c>data</c> property of its specification.
/// </summary>
/// <remarks>
///     <para>
///         Only inline <c>values</c> given as an array are read: an array of objects gives one row per object, and an
///         array of primitives gives one row per value with the value in the field <c>data</c> (the name Vega-Lite uses
///         for it). The JSON types are kept (number, string, boolean, null); a nested object or array is flattened into
///         fields named by the canonical path of each leaf (see <see cref="FieldPath"/>), and a field a row does not
///         have is null.
///     </para>
///     <para>
///         Every other kind of data source yields an empty table and no error: <c>values</c> given as text (it needs
///         a format), a <c>url</c> (never opened or fetched, whatever it says), a <c>name</c>, a <c>sequence</c>, or no
///         source at all. Those cases are reported to the caller by the feature catalog
///         (<c>vl-data-empty</c>, <c>vl-data-remote-url</c>, <c>vl-data-file</c>, <c>vl-data-named</c>,
///         <c>vl-data-sequence</c>, <c>vl-data-values-text</c>) so that a lenient render still draws the rest of the
///         chart. The loader has no side effects and never touches the file system or the network.
///     </para>
///     <para>
///         Resource limits are enforced while reading: more rows than <see cref="VegaLiteLimits.MaxDataRows"/> or more
///         fields than <see cref="VegaLiteLimits.MaxFieldCount"/> throws a <see cref="VegaLiteRenderException"/> that
///         carries the location of the offending data.
///     </para>
/// </remarks>
internal static class DataLoader
{
    /// <summary>The field that holds the value of a row when the inline values are primitives.</summary>
    public const string PrimitiveField = "data";

    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>
    ///     Loads the rows of a <c>data</c> property.
    /// </summary>
    /// <param name="data">The <c>data</c> node of the view; null (or anything but an object) gives an empty table.</param>
    /// <param name="sourceName">The source name used in locations of errors, or null.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">A token that stops the loading.</param>
    /// <returns>The table: the inline rows, or an empty table for every other kind of source.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the row or field limit is exceeded; carries the location of the data.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static DataTable Load(
        SpecNode? data,
        string? sourceName,
        VegaLiteLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        // Only inline values given as an array of rows are read; every other source is an empty table
        if (data is not { IsObject: true } || data["values"] is not { IsArray: true } values)
        {
            return DataTable.Empty;
        }

        limits ??= VegaLiteLimits.Default;
        var location = values.GetLocation(sourceName);
        if (values.Items.Count > limits.MaxDataRows)
        {
            throw new VegaLiteRenderException(
                $"The inline data has {values.Items.Count} rows, more than the limit of {limits.MaxDataRows} (MaxDataRows).", location);
        }

        // Turn every row into a record of its leaf values
        var records = new List<IReadOnlyDictionary<string, DataValue>>(values.Items.Count);
        var counter = 0;
        foreach (var row in values.Items)
        {
            if (++counter % CancellationInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var record = new Dictionary<string, DataValue>(StringComparer.Ordinal);
            if (row.IsObject)
            {
                FlattenProperties(row, string.Empty, record, limits, sourceName);
            }
            else
            {
                // A primitive (or an array) row becomes a record with the one field "data"
                Flatten(row, PrimitiveField, record, limits, sourceName);
            }

            records.Add(record);
        }

        return DataTable.FromRecords(records, limits, location);
    }

    /// <summary>
    ///     Adds the leaves of one value to a record.
    /// </summary>
    /// <remarks>
    ///     Recursion is bounded by the nesting depth the specification parser accepted (at most 256 levels).
    /// </remarks>
    /// <param name="node">The value.</param>
    /// <param name="key">The canonical name of the value's path.</param>
    /// <param name="record">The record being built.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="sourceName">The source name for locations.</param>
    private static void Flatten(SpecNode node, string key, Dictionary<string, DataValue> record, VegaLiteLimits limits, string? sourceName)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.Object:
                FlattenProperties(node, key, record, limits, sourceName);
                break;
            case SpecNodeKind.Array:
                for (var i = 0; i < node.Items.Count; i++)
                {
                    Flatten(node.Items[i], Join(key, i.ToString(CultureInfo.InvariantCulture)), record, limits, sourceName);
                }

                break;
            case SpecNodeKind.String:
                Add(node, key, DataValue.FromString(node.StringValue), record, limits, sourceName);
                break;
            case SpecNodeKind.Number:
                Add(node, key, DataValue.FromNumber(node.NumberValue), record, limits, sourceName);
                break;
            case SpecNodeKind.Boolean:
                Add(node, key, DataValue.FromBoolean(node.BoolValue), record, limits, sourceName);
                break;
            default:
                Add(node, key, DataValue.Null, record, limits, sourceName);
                break;
        }
    }

    /// <summary>
    ///     Adds the leaves of the properties of an object to a record.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="prefix">The canonical name of the object's path; empty for the row itself.</param>
    /// <param name="record">The record being built.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="sourceName">The source name for locations.</param>
    private static void FlattenProperties(SpecNode node, string prefix, Dictionary<string, DataValue> record, VegaLiteLimits limits, string? sourceName)
    {
        foreach (var property in node.Properties)
        {
            Flatten(property, Join(prefix, property.Name!), record, limits, sourceName);
        }
    }

    /// <summary>
    ///     Stores one leaf, enforcing the field limit.
    /// </summary>
    /// <param name="node">The leaf node (for the location).</param>
    /// <param name="key">The canonical field name.</param>
    /// <param name="value">The value.</param>
    /// <param name="record">The record being built.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="sourceName">The source name for locations.</param>
    private static void Add(SpecNode node, string key, DataValue value, Dictionary<string, DataValue> record, VegaLiteLimits limits, string? sourceName)
    {
        record[key] = value;
        if (record.Count > limits.MaxFieldCount)
        {
            throw new VegaLiteRenderException(
                $"A data row has more than {limits.MaxFieldCount} fields, the limit (MaxFieldCount).", node.GetLocation(sourceName));
        }
    }

    /// <summary>
    ///     Appends a property name or array index to a canonical path name.
    /// </summary>
    /// <param name="prefix">The canonical name so far; empty at the top.</param>
    /// <param name="segment">The raw property name or index text.</param>
    /// <returns>The canonical name of the child.</returns>
    private static string Join(string prefix, string segment) =>
        prefix.Length == 0 ? FieldPath.Escape(segment) : prefix + "." + FieldPath.Escape(segment);
}
