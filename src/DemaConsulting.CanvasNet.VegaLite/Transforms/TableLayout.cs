using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The field layout of a table a transform builds from another: the input fields that survive, the fields it adds, and
///     the checks that bound the size of the result.
/// </summary>
/// <remarks>
///     <para>
///         A table keeps its fields in ordinal order, so the layout fixes where every input field and every added field
///         goes. An added field replaces an input field of the same name, and drops the fields below it (an input
///         <c>tags.0</c> disappears when a <c>tags</c> is added), as a property of a row object is replaced as a whole.
///         The number of fields is checked against <see cref="VegaLiteLimits.MaxFieldCount"/> and the number of cells
///         against <see cref="MaxCells"/> before anything is allocated.
///     </para>
/// </remarks>
internal sealed class TableLayout
{
    /// <summary>
    ///     The most cells (rows times fields) a table that a transform builds may have: 33,554,432, which bounds the memory of
    ///     one table to a few hundred megabytes whatever the other limits allow.
    /// </summary>
    public const long MaxCells = 1L << 25;

    /// <summary>The location reported when a limit is exceeded.</summary>
    private readonly VegaLiteLocation _location;

    /// <summary>
    ///     Initializes a layout.
    /// </summary>
    /// <param name="input">The table the new one is built from.</param>
    /// <param name="added">The canonical names of the fields to add (they may repeat; the last write wins).</param>
    /// <param name="keepInput">Whether the surviving input fields are kept (false builds a table of the added fields only).</param>
    /// <param name="limits">The limits.</param>
    /// <param name="location">The location reported when a limit is exceeded.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the table would have more fields than <see cref="VegaLiteLimits.MaxFieldCount"/>.</exception>
    public TableLayout(DataTable input, IReadOnlyList<string> added, bool keepInput, VegaLiteLimits limits, VegaLiteLocation location)
    {
        _location = location;
        var names = new SortedDictionary<string, int>(StringComparer.Ordinal);
        InputToOutput = new int[input.FieldCount];
        var keepers = new List<int>(input.FieldCount);
        for (var i = 0; i < input.FieldCount; i++)
        {
            InputToOutput[i] = -1;
            if (keepInput && !IsReplaced(input.Fields[i], added))
            {
                keepers.Add(i);
                names[input.Fields[i]] = i;
            }
        }

        var distinctAdded = added.Distinct(StringComparer.Ordinal).ToList();
        foreach (var name in distinctAdded)
        {
            names[name] = -1;
        }

        if (names.Count > limits.MaxFieldCount)
        {
            throw new VegaLiteRenderException(
                $"The transform produces {names.Count} fields, more than the limit of {limits.MaxFieldCount} (MaxFieldCount).", location);
        }

        Fields = [.. names.Keys];
        var position = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < Fields.Length; i++)
        {
            position[Fields[i]] = i;
        }

        foreach (var i in keepers)
        {
            InputToOutput[i] = position[input.Fields[i]];
        }

        Added = [.. added.Select(name => position[name])];
        Kept = [.. keepers.Where(i => InputToOutput[i] >= 0)];
    }

    /// <summary>Gets the fields of the new table in ordinal order.</summary>
    public string[] Fields { get; }

    /// <summary>Gets, for each input field, its position in the new table, or -1 when it is dropped.</summary>
    public int[] InputToOutput { get; }

    /// <summary>Gets, for each added name (in the order given), its position in the new table.</summary>
    public int[] Added { get; }

    /// <summary>Gets the positions of the input fields that survive.</summary>
    public int[] Kept { get; }

    /// <summary>
    ///     Allocates the cells of a table with a number of rows, after checking the size.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The cells, all null.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the table would have more than <see cref="MaxCells"/> cells.</exception>
    public DataValue[] Allocate(long rows)
    {
        if (rows * Fields.Length > MaxCells)
        {
            throw new VegaLiteRenderException(
                $"The transform produces a table of {rows} rows with {Fields.Length} fields, more than the {MaxCells} cells that a derived table may have.",
                _location);
        }

        return new DataValue[rows * Fields.Length];
    }

    /// <summary>
    ///     Copies the surviving input fields of a row into the cells of the new table.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="row">The input row.</param>
    /// <param name="cells">The cells of the new table.</param>
    /// <param name="outputRow">The row of the new table.</param>
    public void CopyRow(DataTable input, int row, DataValue[] cells, long outputRow)
    {
        var source = input.GetRow(row);
        var offset = outputRow * Fields.Length;
        foreach (var i in Kept)
        {
            cells[offset + InputToOutput[i]] = source[i];
        }
    }

    /// <summary>
    ///     Builds the table.
    /// </summary>
    /// <param name="cells">The finished cells.</param>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The table.</returns>
    public DataTable Build(DataValue[] cells, int rows) => DataTable.FromSortedCells(Fields, cells, rows);

    /// <summary>
    ///     Gets the canonical name of an output field.
    /// </summary>
    /// <param name="name">The name as written in the specification (a literal name, which may contain a dot).</param>
    /// <returns>The canonical field name.</returns>
    public static string Canonical(string name) => FieldPath.Escape(name);

    /// <summary>
    ///     Determines whether a field is replaced by an added field of the same name or an ancestor of it.
    /// </summary>
    /// <param name="field">The input field.</param>
    /// <param name="added">The added names.</param>
    /// <returns><see langword="true"/> when the field is dropped.</returns>
    private static bool IsReplaced(string field, IReadOnlyList<string> added)
    {
        foreach (var name in added)
        {
            if (string.Equals(field, name, StringComparison.Ordinal)
                || (field.Length > name.Length && field[name.Length] == '.' && field.StartsWith(name, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
///     Reads a field of a row of a table, rebuilding an array or an object from the flattened fields below it.
/// </summary>
/// <remarks>
///     A table stores a nested row flattened: <c>tags: ["a", "b"]</c> is the fields <c>tags.0</c> and <c>tags.1</c>. A reader
///     for <c>tags</c> returns the cell of a field of that name when it holds a value (a scalar, a calculated or flattened
///     value) and otherwise rebuilds the value from the fields below the path: an array when the names below it are the
///     indexes 0 to n-1 (trailing nulls are dropped, because a missing element and a null one are the same cell), an object
///     otherwise. A path without any field below it reads null. The reader is created for one table.
/// </remarks>
internal sealed class FieldReader
{
    /// <summary>The table.</summary>
    private readonly DataTable _table;

    /// <summary>The position of the field of that exact name, or -1.</summary>
    private readonly int _direct;

    /// <summary>The fields below the path with the names that follow it.</summary>
    private readonly List<(int Field, IReadOnlyList<string> Segments)> _below = [];

    /// <summary>
    ///     Initializes a reader.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="path">The field path as written in the specification; empty for the whole row.</param>
    public FieldReader(DataTable table, string? path)
    {
        _table = table;
        var canonical = path is { Length: 0 } ? string.Empty : FieldPath.Canonicalize(path);
        _direct = canonical is null || canonical.Length == 0 ? -1 : table.IndexOfField(canonical);
        if (canonical is null)
        {
            return;
        }

        // The fields below the path are collected even when a field of that exact name exists: a column can hold arrays in some
        // rows (flattened into tags.0, tags.1) and a scalar in others (tags), and the reader returns whichever the row has
        var prefix = canonical.Length == 0 ? string.Empty : canonical + ".";
        for (var i = 0; i < table.FieldCount; i++)
        {
            var field = table.Fields[i];
            if (field.StartsWith(prefix, StringComparison.Ordinal) && FieldPath.Parse(field[prefix.Length..]) is { Count: > 0 } segments)
            {
                _below.Add((i, segments));
            }
        }
    }

    /// <summary>Gets a value indicating whether the reader can return anything but null.</summary>
    public bool HasField => _direct >= 0 || _below.Count > 0;

    /// <summary>
    ///     Reads the value of a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The cell, the rebuilt array or object, or null.</returns>
    public DataValue Read(int row)
    {
        if (_direct >= 0)
        {
            var cell = _table.GetValue(row, _direct);
            if (!cell.IsNull || _below.Count == 0)
            {
                return cell;
            }
        }

        if (_below.Count == 0)
        {
            return default;
        }

        var root = new Node();
        foreach (var (field, segments) in _below)
        {
            root.Add(segments, 0, _table.GetValue(row, field));
        }

        return root.ToValue();
    }

    /// <summary>
    ///     A node of the tree rebuilt from flattened names.
    /// </summary>
    private sealed class Node
    {
        /// <summary>The children by name.</summary>
        private readonly Dictionary<string, Node> _children = new(StringComparer.Ordinal);

        /// <summary>The names of the children in insertion order.</summary>
        private readonly List<string> _order = [];

        /// <summary>The value of a leaf.</summary>
        private DataValue _leaf;

        /// <summary>Whether this node is a leaf.</summary>
        private bool _isLeaf;

        /// <summary>Gets a value indicating whether this node is a leaf that holds null (a missing or null cell).</summary>
        public bool IsNullLeaf => _isLeaf && _leaf.IsNull;

        /// <summary>
        ///     Adds a leaf below this node.
        /// </summary>
        /// <param name="segments">The names that lead to the leaf.</param>
        /// <param name="position">The name to add next.</param>
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
                child = new Node();
                _children[segments[position]] = child;
                _order.Add(segments[position]);
            }

            child.Add(segments, position + 1, value);
        }

        /// <summary>
        ///     Converts the node to a value: its leaf, an array when the names are the indexes 0 to n-1, or an object.
        /// </summary>
        /// <returns>The value.</returns>
        public DataValue ToValue()
        {
            if (_isLeaf)
            {
                return _leaf;
            }

            if (IsIndexed())
            {
                // A missing element and a null one are the same cell, so trailing nulls are not part of a rebuilt array: a row
                // whose array is shorter than that of another row (or empty) must not gain elements
                var length = _order.Count;
                while (length > 0 && _children[(length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)].IsNullLeaf)
                {
                    length--;
                }

                var elements = new DataValue[length];
                for (var i = 0; i < length; i++)
                {
                    elements[i] = _children[i.ToString(System.Globalization.CultureInfo.InvariantCulture)].ToValue();
                }

                return DataValue.FromOwnedArray(elements);
            }

            return DataValue.FromObject(DataObject.Create(
                _order.Select(name => new KeyValuePair<string, DataValue>(name, _children[name].ToValue()))));
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
}
