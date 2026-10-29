namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The immutable payload of an array value: the elements, wrapped so that every array has its own identity.
/// </summary>
/// <remarks>
///     JavaScript compares arrays by identity, so two empty arrays are different values; wrapping the element array
///     (which .NET shares for every empty array) keeps that semantic. The wrapper is created by <see cref="DataValue"/>
///     only and is never changed.
/// </remarks>
/// <param name="Items">The elements; ownership passes to the wrapper.</param>
internal sealed class DataArray(DataValue[] Items)
{
    /// <summary>Gets the elements.</summary>
    public DataValue[] Items { get; } = Items;
}

/// <summary>
///     An immutable object value with string keys in insertion order, the composite result of an expression such as
///     <c>{a: 1, b: [2, 3]}</c> or of a calculated field.
/// </summary>
/// <remarks>
///     <para>
///         The keys keep the order in which they were first added, so enumeration is deterministic and independent of
///         hashing. A repeated key keeps its first position and takes the last value, as in a JavaScript object
///         literal. Member lookup is ordinal (code unit) and case sensitive. Equality of object values is identity
///         (JavaScript's <c>===</c> on objects), which is what <see cref="DataValue"/> implements for composites.
///     </para>
///     <para>
///         The type is immutable once built (the builder copies its input) and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class DataObject
{
    /// <summary>The keys in insertion order.</summary>
    private readonly string[] _keys;

    /// <summary>The values, parallel to the keys.</summary>
    private readonly DataValue[] _values;

    /// <summary>The key positions, built only for objects with many members.</summary>
    private readonly Dictionary<string, int>? _index;

    /// <summary>
    ///     Initializes an object from already de-duplicated keys and values.
    /// </summary>
    /// <param name="keys">The distinct keys in insertion order.</param>
    /// <param name="values">The values, parallel to the keys.</param>
    private DataObject(string[] keys, DataValue[] values)
    {
        _keys = keys;
        _values = values;
        if (keys.Length > 8)
        {
            _index = new Dictionary<string, int>(keys.Length, StringComparer.Ordinal);
            for (var i = 0; i < keys.Length; i++)
            {
                _index[keys[i]] = i;
            }
        }
    }

    /// <summary>Gets the number of members.</summary>
    public int Count => _keys.Length;

    /// <summary>Gets the keys in insertion order.</summary>
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>
    ///     Gets the value of a member by position.
    /// </summary>
    /// <param name="position">The zero-based position in insertion order.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the position is out of range.</exception>
    public DataValue ValueAt(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position, _values.Length);
        return _values[position];
    }

    /// <summary>
    ///     Creates an object from key/value pairs.
    /// </summary>
    /// <param name="members">The members in insertion order; a repeated key keeps its first position and the last value.</param>
    /// <returns>The object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is null.</exception>
    public static DataObject Create(IEnumerable<KeyValuePair<string, DataValue>> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var keys = new List<string>();
        var values = new List<DataValue>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            if (seen.TryGetValue(member.Key, out var position))
            {
                values[position] = member.Value;
            }
            else
            {
                seen[member.Key] = keys.Count;
                keys.Add(member.Key);
                values.Add(member.Value);
            }
        }

        return new DataObject(keys.ToArray(), values.ToArray());
    }

    /// <summary>
    ///     Looks up a member.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the object has the member.</returns>
    public bool TryGetValue(string key, out DataValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var position = -1;
        if (_index is not null)
        {
            if (!_index.TryGetValue(key, out position))
            {
                position = -1;
            }
        }
        else
        {
            for (var i = 0; i < _keys.Length; i++)
            {
                if (string.Equals(_keys[i], key, StringComparison.Ordinal))
                {
                    position = i;
                    break;
                }
            }
        }

        value = position < 0 ? default : _values[position];
        return position >= 0;
    }
}
