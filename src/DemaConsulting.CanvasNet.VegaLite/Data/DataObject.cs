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

/// <summary>
///     A node of the tree that is rebuilt from the flattened field names of a table row.
/// </summary>
/// <remarks>
///     <para>
///         A table stores a nested row flattened (<c>tags: ["a", "b"]</c> is the fields <c>tags.0</c> and <c>tags.1</c>, as the
///         data loader flattens it). A node converts back: a leaf that holds a value is that value; a node whose children are
///         exactly the canonical indexes 0 to n-1 is an array (trailing nulls are dropped, because a missing element and a
///         null one are the same cell); any other node with children is an object. A leaf that holds null and has fields below
///         it is the composite of those fields, so a column that is null in one row and nested in another reads sensibly.
///     </para>
///     <para>
///         The conversion can share a cache of the composites it built, keyed by the canonical path, so that reading the same
///         path again returns the same array or object (JavaScript identity, <c>datum.a === datum.a</c>). The cache belongs to
///         one row and one thread. The node is not safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class FlatRowNode
{
    /// <summary>The children by name.</summary>
    private readonly Dictionary<string, FlatRowNode> _children = new(StringComparer.Ordinal);

    /// <summary>The names of the children in insertion order.</summary>
    private readonly List<string> _order = [];

    /// <summary>The value of a leaf.</summary>
    private DataValue _leaf;

    /// <summary>Whether this node is a leaf.</summary>
    private bool _isLeaf;

    /// <summary>Gets a value indicating whether this node is a leaf that holds null (a missing or null cell).</summary>
    public bool IsNullLeaf => _isLeaf && _leaf.IsNull && _order.Count == 0;

    /// <summary>Gets a value indicating whether this node converts to an array or an object (it has children and no value of its own).</summary>
    private bool IsComposite => _order.Count > 0 && !(_isLeaf && !_leaf.IsNull);

    /// <summary>
    ///     Adds a leaf below this node.
    /// </summary>
    /// <param name="segments">The names that lead to the leaf.</param>
    /// <param name="position">The name to add next.</param>
    /// <param name="value">The value of the leaf.</param>
    public void Add(IReadOnlyList<string> segments, int position, DataValue value)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (position == segments.Count)
        {
            _isLeaf = true;
            _leaf = value;
            return;
        }

        if (!_children.TryGetValue(segments[position], out var child))
        {
            child = new FlatRowNode();
            _children[segments[position]] = child;
            _order.Add(segments[position]);
        }

        child.Add(segments, position + 1, value);
    }

    /// <summary>
    ///     Converts the node to a value: its leaf, an array when the names are the indexes 0 to n-1, or an object.
    /// </summary>
    /// <param name="path">The canonical path of this node (empty for the row); only used as the key in the cache.</param>
    /// <param name="cache">The composites already built for the row by canonical path, which also receives the new ones; null for no sharing.</param>
    /// <param name="forceObject">Whether this node is an object even when its names are indexes (the row itself).</param>
    /// <returns>The value; null for a leaf that holds null and has no children.</returns>
    public DataValue ToValue(string path = "", Dictionary<string, DataValue>? cache = null, bool forceObject = false)
    {
        if (_isLeaf && (!_leaf.IsNull || _order.Count == 0))
        {
            return _leaf;
        }

        if (_order.Count == 0)
        {
            return default;
        }

        if (cache is not null && cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        DataValue result;
        if (!forceObject && IsIndexed())
        {
            // A missing element and a null one are the same cell, so trailing nulls are not part of a rebuilt array: a row
            // whose array is shorter than that of another row (or empty) must not gain elements
            var length = _order.Count;
            while (length > 0 && _children[(length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)].IsNullLeaf)
            {
                length--;
            }

            var elements = new DataValue[length];
            if (length == 0 && _isLeaf)
            {
                // The cell of the path itself is null and every element below it is null too: the value is that null (a column
                // that is null in this row), not an empty array
                return default;
            }
            for (var i = 0; i < length; i++)
            {
                var name = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                elements[i] = _children[name].ToValue(ChildPath(path, name, cache is not null), cache);
            }

            result = DataValue.FromOwnedArray(elements);
        }
        else
        {
            var members = new List<KeyValuePair<string, DataValue>>(_order.Count);
            foreach (var name in _order)
            {
                members.Add(new KeyValuePair<string, DataValue>(name, _children[name].ToValue(ChildPath(path, name, cache is not null), cache)));
            }

            result = DataValue.FromObject(DataObject.Create(members));
        }

        if (cache is not null)
        {
            cache[path] = result;
        }

        return result;
    }

    /// <summary>
    ///     Gets the canonical path of a child, which is only needed to key the cache.
    /// </summary>
    /// <param name="path">The canonical path of this node.</param>
    /// <param name="name">The name of the child.</param>
    /// <param name="needed">Whether a cache is in use.</param>
    /// <returns>The path; the empty text when it is not needed.</returns>
    private string ChildPath(string path, string name, bool needed)
    {
        if (!needed || !_children[name].IsComposite)
        {
            return string.Empty;
        }

        var escaped = FieldPath.Escape(name);
        return path.Length == 0 ? escaped : path + "." + escaped;
    }

    /// <summary>
    ///     Determines whether the child names are exactly the canonical indexes 0 to n-1.
    /// </summary>
    /// <returns><see langword="true"/> when the node is an array.</returns>
    private bool IsIndexed()
    {
        if (_order.Count == 0)
        {
            return false;
        }

        var seen = new bool[_order.Count];
        foreach (var name in _order)
        {
            if (name.Length == 0 || name.Length > 9 || !name.All(char.IsAsciiDigit) || (name.Length > 1 && name[0] == '0'))
            {
                return false;
            }

            var index = int.Parse(name, System.Globalization.CultureInfo.InvariantCulture);
            if (index >= seen.Length || seen[index])
            {
                return false;
            }

            seen[index] = true;
        }

        return true;
    }
}
