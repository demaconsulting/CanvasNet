using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The datum access of the expression evaluator (a partial file of the evaluator unit).
/// </summary>
/// <remarks>
///     A table stores a nested row flattened: every leaf is a field named by the canonical text of its path. Reading
///     <c>datum.a</c> therefore first looks for a field <c>a</c> that holds a value and, when there is none, rebuilds the
///     nested array or object from the fields below it (<c>a.b</c>, <c>a.c</c>; the indexes <c>a.0</c>, <c>a.1</c> make an array);
///     reading the whole <c>datum</c> rebuilds the whole row. A rebuilt composite is cached for the evaluation, so reading it
///     again gives the same value (JavaScript identity). The work is charged to the step budget in proportion to the number
///     of fields.
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
    ///     Reads a field of the current table row, rebuilding a flattened nested array or object when needed.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>The value.</returns>
    private DataValue GetTableField(string name)
    {
        var escaped = FieldPath.Escape(name);
        var index = _table!.IndexOfField(escaped);
        if (index >= 0)
        {
            var cell = _table.GetValue(_row, index);
            if (!cell.IsNull)
            {
                return cell;
            }
        }

        // A null cell with fields below it is the composite of those fields (a column that is null in one row and nested in
        // another); the composite is the same array or object on every read of the row
        var built = BuildNested(escaped + ".");
        return built.IsNull && index >= 0 ? _table.GetValue(_row, index) : built;
    }

    /// <summary>
    ///     Rebuilds a nested array or object from the flattened fields that start with a prefix.
    /// </summary>
    /// <remarks>
    ///     The composites of the row are cached by canonical path for the duration of one evaluation, so that reading the same
    ///     path twice (<c>datum.a === datum.a</c>) or a path inside an earlier read (<c>datum.a</c> after <c>datum</c>) returns
    ///     the same value. Names that are the indexes 0 to n-1 make an array, as the data loader flattens one.
    /// </remarks>
    /// <param name="prefix">The canonical prefix including the trailing dot, or the empty text for the whole row.</param>
    /// <returns>The array or object; null when no field starts with the prefix.</returns>
    private DataValue BuildNested(string prefix)
    {
        var table = _table!;
        var path = prefix.Length == 0 ? string.Empty : prefix[..^1];
        _nested ??= new Dictionary<string, DataValue>(StringComparer.Ordinal);
        if (_nested.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // One extra step for every 16 fields scanned (the size unit is 64 per step)
        _context.ChargeSize(table.FieldCount * 4L);
        var root = new FlatRowNode();
        var found = false;
        if (prefix.Length > 0 && table.IndexOfField(prefix[..^1]) is var direct and >= 0)
        {
            // The cell of the path itself, which is null here: all-null elements below it leave it null
            root.Add([], 0, table.GetValue(_row, direct));
        }
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

        if (!found && prefix.Length == 0)
        {
            // A row without any field (an inline empty object) is the empty object, not null; it is cached like any composite
            // so that every read of the whole row is the same value
            var empty = DataValue.FromObject(DataObject.Create([]));
            _nested[path] = empty;
            return empty;
        }

        return found ? root.ToValue(path, _nested, prefix.Length == 0) : default;
    }
}
