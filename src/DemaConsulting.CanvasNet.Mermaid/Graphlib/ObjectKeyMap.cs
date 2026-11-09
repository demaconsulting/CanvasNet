using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Graphlib;

/// <summary>
///     A string-keyed map that enumerates its keys in the order of the own keys of a JavaScript object.
/// </summary>
/// <remarks>
///     ECMAScript orders the own string keys of an ordinary object with the array indexes first, in ascending numeric
///     order, then every other key in the order it was added; a key that is removed and added again moves to the end.
///     graphlib keeps its nodes, children, predecessors and successors in such objects and dagre iterates them, so the
///     port keeps this order to make the same choices. An array index is the canonical decimal form of an integer from 0
///     to 2<sup>32</sup> - 2 (<c>"0"</c>, <c>"7"</c>, <c>"4294967294"</c>, but not <c>"07"</c>, <c>"-1"</c> or
///     <c>"1.5"</c>). <see cref="Keys"/> and <see cref="Values"/> return snapshots, so the map may change while they are
///     iterated, as lodash's <c>_.keys</c> allows.
/// </remarks>
/// <typeparam name="TValue">The value type.</typeparam>
internal sealed class ObjectKeyMap<TValue>
{
    /// <summary>The greatest array index (2<sup>32</sup> - 2).</summary>
    private const uint MaxArrayIndex = uint.MaxValue - 1;

    /// <summary>The entries by key.</summary>
    private readonly Dictionary<string, Slot> _slots = new(StringComparer.Ordinal);

    /// <summary>The keys that are not array indexes, in insertion order.</summary>
    private readonly LinkedList<string> _named = new();

    /// <summary>The keys that are array indexes, in ascending order.</summary>
    private readonly SortedSet<uint> _indexes = [];

    /// <summary>
    ///     Gets the number of keys.
    /// </summary>
    public int Count => _slots.Count;

    /// <summary>
    ///     Gets or sets the value of a key; setting a new key adds it at its place in the key order, setting an existing
    ///     key keeps its place.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value.</returns>
    /// <exception cref="KeyNotFoundException">Thrown on get when the key is absent.</exception>
    public TValue this[string key]
    {
        get => _slots[key].Value;
        set
        {
            if (_slots.TryGetValue(key, out var slot))
            {
                slot.Value = value;
                return;
            }

            // A new key goes to the end of the named keys, or among the indexes in numeric order
            var node = IsArrayIndex(key, out var index) ? null : _named.AddLast(key);
            if (node is null)
            {
                _indexes.Add(index);
            }

            _slots.Add(key, new Slot(value, node));
        }
    }

    /// <summary>
    ///     Gets whether a string is an array index, the keys that JavaScript enumerates first.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="index">The numeric value when it is.</param>
    /// <returns>True when the key is the canonical decimal form of an integer from 0 to 2<sup>32</sup> - 2.</returns>
    public static bool IsArrayIndex(string key, out uint index)
    {
        index = 0;

        // Canonical form: digits only, no leading zero except "0" itself, at most ten digits
        if (key.Length is 0 or > 10 || (key.Length > 1 && key[0] == '0'))
        {
            return false;
        }

        ulong value = 0;
        foreach (var c in key)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }

            value = (value * 10) + (ulong)(c - '0');
        }

        if (value > MaxArrayIndex)
        {
            return false;
        }

        index = (uint)value;
        return true;
    }

    /// <summary>
    ///     Gets whether a key is present.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>True when present.</returns>
    public bool ContainsKey(string key) => _slots.ContainsKey(key);

    /// <summary>
    ///     Gets the value of a key when present.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, or the default when absent.</param>
    /// <returns>True when present.</returns>
    public bool TryGetValue(string key, out TValue value)
    {
        if (_slots.TryGetValue(key, out var slot))
        {
            value = slot.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    ///     Removes a key, as the JavaScript <c>delete</c> operator does.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>True when the key was present.</returns>
    public bool Remove(string key)
    {
        if (!_slots.Remove(key, out var slot))
        {
            return false;
        }

        if (slot.Node is null)
        {
            _indexes.Remove(uint.Parse(key, NumberStyles.None, CultureInfo.InvariantCulture));
        }
        else
        {
            _named.Remove(slot.Node);
        }

        return true;
    }

    /// <summary>
    ///     Gets a snapshot of the keys in JavaScript key order.
    /// </summary>
    /// <returns>The keys.</returns>
    public string[] Keys()
    {
        var keys = new string[_slots.Count];
        var i = 0;
        foreach (var index in _indexes)
        {
            keys[i++] = index.ToString(CultureInfo.InvariantCulture);
        }

        foreach (var key in _named)
        {
            keys[i++] = key;
        }

        return keys;
    }

    /// <summary>
    ///     Gets a snapshot of the values in JavaScript key order.
    /// </summary>
    /// <returns>The values.</returns>
    public TValue[] Values()
    {
        var keys = Keys();
        var values = new TValue[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            values[i] = _slots[keys[i]].Value;
        }

        return values;
    }

    /// <summary>
    ///     One entry: the value and, for a key that is not an array index, its place in the insertion order.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="node">The place of a named key, or null for an array index.</param>
    private sealed class Slot(TValue value, LinkedListNode<string>? node)
    {
        /// <summary>Gets or sets the value.</summary>
        public TValue Value { get; set; } = value;

        /// <summary>Gets the place of a named key, or null for an array index.</summary>
        public LinkedListNode<string>? Node { get; } = node;
    }
}
