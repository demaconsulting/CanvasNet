using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The datum access of the expression evaluator (a partial file of the evaluator unit).
/// </summary>
/// <remarks>
///     A table stores a nested row flattened: every leaf is a field named by the canonical text of its path. Reading
///     <c>datum.a</c> therefore first looks for the field <c>a</c> and, when there is none, rebuilds the nested object
///     from the fields below it (<c>a.b</c>, <c>a.c</c>); reading the whole <c>datum</c> rebuilds the whole row. The work
///     is charged to the step budget in proportion to the number of fields.
/// </remarks>
internal sealed partial class ExpressionEvaluator
{
    /// <summary>
    ///     Reads one field of the current datum.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The value; null when the datum has no such field.</returns>
    private DataValue GetDatumField(string name)
    {
        switch (_mode)
        {
            case DatumMode.Table:
                return GetTableField(name);
            case DatumMode.Value:
                return _datumValue.IsObject && _datumValue.ObjectValue!.TryGetValue(name, out var member) ? member : default;
            case DatumMode.Custom:
                return _custom!.GetField(name);
            default:
                return default;
        }
    }

    /// <summary>
    ///     Gets the current datum as a value.
    /// </summary>
    /// <returns>The value; null when there is no datum.</returns>
    private DataValue DatumAsValue() => _mode switch
    {
        DatumMode.Table => BuildNested(string.Empty),
        DatumMode.Value => _datumValue,
        DatumMode.Custom => _custom!.ToValue(),
        _ => default,
    };

    /// <summary>
    ///     Reads a field of the current table row, rebuilding a flattened nested object when needed.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The value.</returns>
    private DataValue GetTableField(string name)
    {
        var escaped = FieldPath.Escape(name);
        var index = _table!.IndexOfField(escaped);
        return index >= 0 ? _table.GetValue(_row, index) : BuildNested(escaped + ".");
    }

    /// <summary>
    ///     Rebuilds a nested object from the flattened fields that start with a prefix.
    /// </summary>
    /// <param name="prefix">The canonical prefix including the trailing dot, or the empty text for the whole row.</param>
    /// <returns>The object; null when no field starts with the prefix.</returns>
    private DataValue BuildNested(string prefix)
    {
        var table = _table!;
        // One extra step for every 16 fields scanned (the size unit is 64 per step)
        _context.ChargeSize(table.FieldCount * 4L);
        var root = new NestedNode();
        var found = false;
        for (var i = 0; i < table.FieldCount; i++)
        {
            var field = table.Fields[i];
            if (!field.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var segments = FieldPath.Parse(field[prefix.Length..]);
            if (segments is null || segments.Count == 0)
            {
                continue;
            }

            found = true;
            root.Add(segments, 0, table.GetValue(_row, i));
        }

        return found ? root.ToValue() : default;
    }

    /// <summary>
    ///     A node of the tree that is rebuilt from flattened field names.
    /// </summary>
    private sealed class NestedNode
    {
        /// <summary>The child nodes by name.</summary>
        private readonly Dictionary<string, NestedNode> _children = new(StringComparer.Ordinal);

        /// <summary>The names of the children in insertion order.</summary>
        private readonly List<string> _order = [];

        /// <summary>The value of a leaf.</summary>
        private DataValue _leaf;

        /// <summary>Whether this node is a leaf.</summary>
        private bool _isLeaf;

        /// <summary>
        ///     Adds a leaf below this node.
        /// </summary>
        /// <param name="segments">The path of the leaf.</param>
        /// <param name="position">The segment to add next.</param>
        /// <param name="value">The value of the leaf.</param>
        public void Add(IReadOnlyList<string> segments, int position, DataValue value)
        {
            if (position == segments.Count)
            {
                _isLeaf = true;
                _leaf = value;
                return;
            }

            if (!_children.TryGetValue(segments[position], out var child))
            {
                child = new NestedNode();
                _children[segments[position]] = child;
                _order.Add(segments[position]);
            }

            child.Add(segments, position + 1, value);
        }

        /// <summary>
        ///     Converts the node to a value.
        /// </summary>
        /// <returns>The leaf value, or an object of the children in insertion order.</returns>
        public DataValue ToValue()
        {
            if (_isLeaf)
            {
                return _leaf;
            }

            var members = new List<KeyValuePair<string, DataValue>>(_order.Count);
            foreach (var name in _order)
            {
                members.Add(new KeyValuePair<string, DataValue>(name, _children[name].ToValue()));
            }

            return DataValue.FromObject(DataObject.Create(members));
        }
    }
}
