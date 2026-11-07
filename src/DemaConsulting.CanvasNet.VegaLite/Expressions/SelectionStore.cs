// Portions derived from Vega's vega-selections (BSD-3-Clause): the shape of the tuples of a selection store; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     One projected field of a selection tuple: the field the tuple constrains and how its value is compared.
/// </summary>
/// <remarks>
///     The type is Vega-Lite's <c>TupleStoreType</c>: <c>E</c> (enumerated values, or for an interval over a discrete axis the list of the
///     values at its ends), <c>R</c> (a range whose ends are inclusive) and <c>R-RE</c> (a range that includes its start and not its end, the range
///     of a binned field). The test functions also know the types <c>R-E</c>, <c>R-LE</c>, <c>E-LT</c>, <c>E-LTE</c>, <c>E-GT</c>, <c>E-GTE</c>,
///     <c>E-VALID</c> and <c>E-ONE</c>, which no static chart produces, so a test written in an expression behaves as it does in Vega.
/// </remarks>
/// <param name="Field">The field as written in the specification (a path such as <c>a.b</c>).</param>
/// <param name="Channel">The encoding channel the field was projected from, or null for a projection over a field name.</param>
/// <param name="Type">The tuple store type.</param>
internal sealed record SelectionTupleField(string Field, string? Channel, string Type)
{
    /// <summary>The path of the field in a datum, parsed once; null when the text is not a valid field path.</summary>
    private readonly IReadOnlyList<string>? _path = FieldPath.Parse(Field);

    /// <summary>Gets the segments of the field path in a datum; empty when the field is not a valid path (no datum has such a field).</summary>
    public IReadOnlyList<string> Path => _path ?? [];

    /// <summary>Gets a value indicating whether the field path was valid.</summary>
    public bool HasPath => _path is not null;
}

/// <summary>
///     One tuple of a selection store: the unit that holds it and the values of the projected fields (or, for a row-identity selection, the identity).
/// </summary>
/// <remarks>
///     <para>
///         Vega-Lite stores a selection as a list of tuples <c>{unit, fields, values}</c>; the values of a date are stored as numbers of milliseconds, and
///         the test converts the dates of the data in the same way, so the entry keeps numbers only (a date in a value is converted when the entry is
///         created). The entry is immutable.
///     </para>
/// </remarks>
internal sealed class SelectionEntry
{
    /// <summary>The value form of the entry, built on first use.</summary>
    private DataValue? _value;

    /// <summary>
    ///     Initializes an entry that constrains fields.
    /// </summary>
    /// <param name="unit">The name of the unit that holds the tuple.</param>
    /// <param name="fields">The projected fields.</param>
    /// <param name="values">One value for each field: a scalar, or an array (a range, or a list of values).</param>
    public SelectionEntry(string unit, IReadOnlyList<SelectionTupleField> fields, IReadOnlyList<DataValue> values)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(values);
        Unit = unit;
        Fields = fields;
        Values = [.. values.Select(NormalizeDates)];
    }

    /// <summary>
    ///     Initializes an entry that selects one row by its identity.
    /// </summary>
    /// <param name="unit">The name of the unit that holds the tuple.</param>
    /// <param name="id">The row identity.</param>
    public SelectionEntry(string unit, DataValue id)
    {
        ArgumentNullException.ThrowIfNull(unit);
        Unit = unit;
        Id = id;
        Values = [];
    }

    /// <summary>Gets the name of the unit that holds the tuple.</summary>
    public string Unit { get; }

    /// <summary>Gets the projected fields, or null for a row-identity entry.</summary>
    public IReadOnlyList<SelectionTupleField>? Fields { get; }

    /// <summary>Gets the values of the fields (numbers instead of dates); empty for a row-identity entry.</summary>
    public IReadOnlyList<DataValue> Values { get; }

    /// <summary>Gets the row identity of a row-identity entry.</summary>
    public DataValue Id { get; }

    /// <summary>
    ///     Gets the entry as the object that <c>data("name_store")</c> shows: <c>{unit, fields: [{field, channel, type}], values: [...]}</c>.
    /// </summary>
    /// <returns>The object value.</returns>
    public DataValue ToValue()
    {
        if (_value is { } built)
        {
            return built;
        }

        DataValue created;
        if (Fields is null)
        {
            created = DataValue.FromObject(DataObject.Create(
            [
                new KeyValuePair<string, DataValue>("unit", DataValue.FromString(Unit)),
                new KeyValuePair<string, DataValue>(SelectionTestSemantics.SelectionId, Id),
            ]));
        }
        else
        {
            var fields = Fields.Select(f => DataValue.FromObject(DataObject.Create(FieldMembers(f)))).ToArray();
            created = DataValue.FromObject(DataObject.Create(
            [
                new KeyValuePair<string, DataValue>("unit", DataValue.FromString(Unit)),
                new KeyValuePair<string, DataValue>("fields", DataValue.FromOwnedArray(fields)),
                new KeyValuePair<string, DataValue>("values", DataValue.FromOwnedArray([.. Values])),
            ]));
        }

        _value = created;
        return created;
    }

    /// <summary>
    ///     Lists the members of the object that shows a projected field.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The members <c>field</c>, <c>channel</c> (when the field came from a channel) and <c>type</c>.</returns>
    private static IEnumerable<KeyValuePair<string, DataValue>> FieldMembers(SelectionTupleField field)
    {
        yield return new KeyValuePair<string, DataValue>("field", DataValue.FromString(field.Field));
        if (field.Channel is not null)
        {
            yield return new KeyValuePair<string, DataValue>("channel", DataValue.FromString(field.Channel));
        }

        yield return new KeyValuePair<string, DataValue>("type", DataValue.FromString(field.Type));
    }

    /// <summary>
    ///     Replaces a date by its number of milliseconds, also inside an array (the conversion that the test of Vega does in place).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value with numbers instead of dates.</returns>
    private static DataValue NormalizeDates(DataValue value)
    {
        if (value.IsDate)
        {
            return DataValue.FromNumber(value.Milliseconds);
        }

        if (value.IsArray && value.Elements.ToArray().Any(e => e.IsDate))
        {
            return DataValue.FromOwnedArray([.. value.Elements.ToArray().Select(e => e.IsDate ? DataValue.FromNumber(e.Milliseconds) : e)]);
        }

        return value;
    }
}

/// <summary>
///     The tuples of one selection parameter, which expressions read as the data set <c>name_store</c>.
/// </summary>
/// <remarks>
///     A store is created once per call from the initial value of the parameter and never changes, so the evaluation is deterministic. It is
///     immutable and safe to share.
/// </remarks>
internal sealed class SelectionStore
{
    /// <summary>The array form of the store, built on first use.</summary>
    private DataValue? _value;

    /// <summary>
    ///     Initializes a store.
    /// </summary>
    /// <param name="name">The name of the data set: the name of the parameter followed by <c>_store</c>.</param>
    /// <param name="entries">The tuples.</param>
    public SelectionStore(string name, IReadOnlyList<SelectionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(entries);
        Name = name;
        Entries = entries;
    }

    /// <summary>Gets the name of the data set.</summary>
    public string Name { get; }

    /// <summary>Gets the tuples, in order.</summary>
    public IReadOnlyList<SelectionEntry> Entries { get; }

    /// <summary>
    ///     Gets the store as the array that <c>data("name_store")</c> returns.
    /// </summary>
    /// <returns>The array of the tuple objects.</returns>
    public DataValue ToValue() => _value ??= DataValue.FromOwnedArray([.. Entries.Select(e => e.ToValue())]);
}
