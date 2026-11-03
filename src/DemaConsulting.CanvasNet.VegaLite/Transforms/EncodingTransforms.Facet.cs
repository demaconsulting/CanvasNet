// Portions derived from Vega-Lite (BSD-3-Clause): compile/facet.ts (the order of the cells: facetSortFields, facetSortOrder) and compile/data/calculate.ts (sortArrayIndexField); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The ordered distinct values of the fields of a facet.
/// </summary>
/// <param name="Rows">The values of the <c>row</c> field in the order of the rows of the grid; empty when the facet has no row field.</param>
/// <param name="Columns">The values of the <c>column</c> field in the order of the columns of the grid; empty when the facet has no column field.</param>
/// <param name="Wrap">The values of the field of a wrapped facet in the order of the cells; empty for a <c>row</c> and <c>column</c> facet.</param>
internal sealed record FacetDomains(IReadOnlyList<DataValue> Rows, IReadOnlyList<DataValue> Columns, IReadOnlyList<DataValue> Wrap);

/// <summary>
///     One cell of a facet with the rows that belong to it.
/// </summary>
/// <param name="Cell">The cell: its position and the values of the facet fields.</param>
/// <param name="Table">The rows of the cell in the order they have in the table of the facet, with the same fields; a table without rows for a cell the data has no row for.</param>
internal sealed record FacetCellData(FacetCellInfo Cell, DataTable Table);

/// <summary>
///     The cells of a facet: the ordered values of its fields and, for every cell of the grid, its rows.
/// </summary>
/// <param name="Domains">The ordered distinct values of the facet fields.</param>
/// <param name="RowCount">The number of rows of the grid (one for a facet without a row field and for a wrapped facet).</param>
/// <param name="ColumnCount">The number of columns of the grid (one for a facet without a column field; the number of cells for a wrapped facet is the number of its values).</param>
/// <param name="Cells">The cells in the order they are laid out (row-major, or the order of the values of a wrapped facet).</param>
internal sealed record FacetPartition(FacetDomains Domains, int RowCount, int ColumnCount, IReadOnlyList<FacetCellData> Cells)
{
    /// <summary>Gets the cells without their rows, for <see cref="LayerTree.InstantiateFacet"/>.</summary>
    public IReadOnlyList<FacetCellInfo> Infos => [.. Cells.Select(c => c.Cell)];
}

/// <content>
///     The cells of a facet: the values of its fields, their order, the grid they make and the rows of each cell.
/// </content>
/// <remarks>
///     <para>
///         <b>Domain.</b> The distinct values of a facet field are the cells of that field (Vega's <c>aggregate</c> by the field: a value is the same as another by SameValueZero, a
///         null or missing value is a value of its own). They are ordered like Vega-Lite's facet sort: ascending by <see cref="DataValue.Compare"/> (a null first), also for
///         <c>sort: null</c>, which is not a data order as it is for a scale; <c>"descending"</c> reverses it; an array puts the listed values first in the order of the array and then the
///         values that it does not list in the order they first appear in the data (Vega-Lite's <c>sort_index</c> is the position in the array, or its length for an unlisted value, and
///         the order of equal indexes is the order of the groups); a sort by an aggregate (<c>{field, op}</c>) is not supported, the ascending order of the field is used. A <c>bin</c>,
///         <c>timeUnit</c> or <c>aggregate</c> of the field definition is not applied either: the raw value of the field is the value of the cell. The values of an array or an object
///         are compared by their text.
///     </para>
///     <para>
///         <b>Grid.</b> A <c>row</c> and <c>column</c> facet has a cell for every combination of a row value and a column value, row-major, including the combinations that have no row
///         (they are drawn as empty cells); a facet with one of the two has one column or one row. A wrapped facet has a cell for every value. The number of cells is computed in <see cref="long"/>
///         and checked against <see cref="VegaLiteLimits.MaxViews"/> (with the views that one cell holds and the other views of the specification) before the partition is allocated.
///     </para>
///     <para>
///         <b>Partition.</b> One pass assigns every row to its cell, one more builds a table for each cell with the same fields and the rows in their original order (the tables of cells
///         without rows share one empty table). The rows of the cells are counted in the row budget once (<see cref="DataLoader.ReserveDerivedRows"/>); the table of the facet stays counted,
///         as the other views that read it may still need it. The work of every row is charged to the evaluation-step budget and the cancellation token is checked every 1,024 rows.
///     </para>
/// </remarks>
internal sealed partial class EncodingTransforms
{
    /// <summary>
    ///     The values of one facet field for every row of a table.
    /// </summary>
    /// <param name="Domain">The distinct values in the order of the cells.</param>
    /// <param name="RowToIndex">For every row of the table, the position in <paramref name="Domain"/> of its value.</param>
    private sealed record FacetKeys(List<DataValue> Domain, int[] RowToIndex);

    /// <summary>
    ///     Computes the ordered distinct values of the fields of a facet.
    /// </summary>
    /// <param name="facet">The facet: its field definitions (the <c>field</c>, and the <c>sort</c>).</param>
    /// <param name="table">The rows of the facet (after its own transforms).</param>
    /// <param name="context">The context of the call (budget, evaluator, limits, token).</param>
    /// <returns>The values of the row, column and wrapped field.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, located at the field definition, when a sort array is longer than <see cref="VegaLiteLimits.MaxScaleDomainSize"/> or the work exceeds the step budget.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    public static FacetDomains FacetDomain(FacetDefinition facet, DataTable table, TransformContext context)
    {
        ArgumentNullException.ThrowIfNull(facet);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(context);
        var run = new EncodingTransforms(context);
        return DomainsOf(run.KeysOf(facet.Row, table), run.KeysOf(facet.Column, table), run.KeysOf(facet.Wrap, table));
    }

    /// <summary>
    ///     Checks that a facet with a number of cells keeps the specification within <see cref="VegaLiteLimits.MaxViews"/>.
    /// </summary>
    /// <remarks>
    ///     The count is computed in <see cref="long"/> from the number of cells and never overflows: more cells than the limit fail before the multiplication.
    /// </remarks>
    /// <param name="cells">The number of cells.</param>
    /// <param name="viewsPerCell">The number of views one cell holds (its chart and the layers of the chart).</param>
    /// <param name="viewsOutside">The number of views that are not part of the cells (the facet itself and the rest of the specification).</param>
    /// <param name="location">The node the failure is located at.</param>
    /// <param name="context">The context of the call (limits and locations).</param>
    /// <exception cref="VegaLiteRenderException">Thrown, located at <paramref name="location"/>, when the views would be more than <see cref="VegaLiteLimits.MaxViews"/>.</exception>
    public static void CheckFacetViews(long cells, long viewsPerCell, long viewsOutside, SpecNode location, TransformContext context)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(context);
        var max = context.Limits.MaxViews;
        if (cells > max || (cells * Math.Max(1, viewsPerCell)) + viewsOutside > max)
        {
            throw new VegaLiteRenderException(
                $"The facet has {cells} cells of {Math.Max(1, viewsPerCell)} views each, which with the other {viewsOutside} views are more than {max} views, the limit (MaxViews).",
                context.Locate(location));
        }
    }

    /// <summary>
    ///     Splits the rows of a facet into its cells.
    /// </summary>
    /// <param name="facet">The facet.</param>
    /// <param name="table">The rows of the facet (after its own transforms).</param>
    /// <param name="viewsPerCell">The number of views that one cell holds (<see cref="LayerTree.FacetViews"/>).</param>
    /// <param name="viewsOutside">The number of views that are not in the cells (<see cref="LayerTree.FacetViews"/>).</param>
    /// <param name="context">The context of the call (row budget, evaluator, limits, token).</param>
    /// <returns>The values of the fields, the size of the grid and the cells with their rows.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, located at the facet field, when the cells are more than <see cref="VegaLiteLimits.MaxViews"/> allows, the rows exceed the row budget or the work exceeds the step budget.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    public static FacetPartition Facet(FacetDefinition facet, DataTable table, long viewsPerCell, long viewsOutside, TransformContext context)
    {
        ArgumentNullException.ThrowIfNull(facet);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(context);
        return new EncodingTransforms(context).Partition(facet, table, viewsPerCell, viewsOutside);
    }

    /// <summary>
    ///     Builds the cells of a facet and their tables.
    /// </summary>
    /// <param name="facet">The facet.</param>
    /// <param name="table">The rows.</param>
    /// <param name="viewsPerCell">The views one cell holds.</param>
    /// <param name="viewsOutside">The views outside the cells.</param>
    /// <returns>The partition.</returns>
    private FacetPartition Partition(FacetDefinition facet, DataTable table, long viewsPerCell, long viewsOutside)
    {
        var anchor = facet.Wrap ?? facet.Row ?? facet.Column ?? throw new ArgumentException("The facet has no field.", nameof(facet));
        var rowKeys = KeysOf(facet.Row, table);
        var columnKeys = KeysOf(facet.Column, table);
        var wrapKeys = KeysOf(facet.Wrap, table);
        var domains = DomainsOf(rowKeys, columnKeys, wrapKeys);

        // The grid: a wrapped facet has a cell for every value, the others the product of the rows and the columns (one when the field is missing)
        var (rows, columns) = GridOf(facet, domains);
        long cellCount = (long)rows * columns;
        CheckFacetViews(cellCount, viewsPerCell, viewsOutside, anchor, _context);

        // The rows of the cells are new tables: counted once, before anything is allocated
        _context.Loader.ReserveDerivedRows(table.RowCount, "The rows of the facet cells", _context.Locate(anchor));
        var count = (int)cellCount;
        var sizes = new int[count];
        var cellOf = new int[table.RowCount];
        try
        {
            for (var row = 0; row < table.RowCount; row++)
            {
                _context.Tick(row);
                _meter.Add(1);
                var cell = facet.IsWrap
                    ? wrapKeys!.RowToIndex[row]
                    : ((rowKeys?.RowToIndex[row] ?? 0) * columns) + (columnKeys?.RowToIndex[row] ?? 0);
                cellOf[row] = cell;
                sizes[cell]++;
            }
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, _context.Locate(anchor));
        }

        var width = table.FieldCount;
        var fields = new string[width];
        for (var i = 0; i < width; i++)
        {
            fields[i] = table.Fields[i];
        }

        var empty = DataTable.FromSortedCells(fields, [], 0);
        var cellCells = new DataValue[count][];
        var filled = new int[count];
        for (var cell = 0; cell < count; cell++)
        {
            cellCells[cell] = sizes[cell] == 0 ? [] : new DataValue[(long)sizes[cell] * width];
        }

        for (var row = 0; row < table.RowCount; row++)
        {
            _context.Tick(row);
            var cell = cellOf[row];
            table.GetRow(row).CopyTo(cellCells[cell].AsSpan(filled[cell]++ * width, width));
        }

        var result = new List<FacetCellData>(count);
        for (var cell = 0; cell < count; cell++)
        {
            var info = CellOf(facet, domains, cell, columns);
            result.Add(new FacetCellData(info, sizes[cell] == 0 ? empty : DataTable.FromSortedCells(fields, cellCells[cell], sizes[cell])));
        }

        return new FacetPartition(domains, rows, columns, result);
    }

    /// <summary>
    ///     Gets the size of the grid of a facet.
    /// </summary>
    /// <param name="facet">The facet.</param>
    /// <param name="domains">The values of its fields.</param>
    /// <returns>The rows and the columns: a wrapped facet has one row and a column for every value; a facet without a row (column) field has one row (column).</returns>
    private static (int Rows, int Columns) GridOf(FacetDefinition facet, FacetDomains domains)
    {
        if (facet.IsWrap)
        {
            return (1, domains.Wrap.Count);
        }

        return (facet.Row is null ? 1 : domains.Rows.Count, facet.Column is null ? 1 : domains.Columns.Count);
    }

    /// <summary>
    ///     Describes one cell of a facet.
    /// </summary>
    /// <param name="facet">The facet.</param>
    /// <param name="domains">The values of its fields.</param>
    /// <param name="cell">The position of the cell in layout order.</param>
    /// <param name="columns">The columns of the grid.</param>
    /// <returns>The cell.</returns>
    private static FacetCellInfo CellOf(FacetDefinition facet, FacetDomains domains, int cell, int columns)
    {
        if (facet.IsWrap)
        {
            return new FacetCellInfo(cell, -1, -1, default, default, domains.Wrap[cell]);
        }

        var (row, column) = (cell / columns, cell % columns);
        var rowValue = facet.Row is null ? default : domains.Rows[row];
        var columnValue = facet.Column is null ? default : domains.Columns[column];
        return new FacetCellInfo(cell, row, column, rowValue, columnValue, default);
    }

    /// <summary>
    ///     Gathers the domains of the three fields of a facet.
    /// </summary>
    /// <param name="row">The keys of the row field, or null.</param>
    /// <param name="column">The keys of the column field, or null.</param>
    /// <param name="wrap">The keys of the wrapped field, or null.</param>
    /// <returns>The domains.</returns>
    private static FacetDomains DomainsOf(FacetKeys? row, FacetKeys? column, FacetKeys? wrap) =>
        new(row?.Domain ?? [], column?.Domain ?? [], wrap?.Domain ?? []);

    /// <summary>
    ///     Reads a facet field for every row, and orders its distinct values.
    /// </summary>
    /// <param name="definition">The field definition (its <c>field</c> and <c>sort</c>); null when the facet has no such field.</param>
    /// <param name="table">The rows.</param>
    /// <returns>The ordered values and the position of each row's value; null when there is no field definition.</returns>
    private FacetKeys? KeysOf(SpecNode? definition, DataTable table)
    {
        if (definition is not { IsObject: true })
        {
            return null;
        }

        try
        {
            var path = definition["field"] is { IsString: true } field ? field.StringValue : null;
            var reader = new FieldReader(table, path);
            var evaluation = _context.Evaluator.Context;
            var firstSeen = new List<DataValue>();
            var positions = new Dictionary<DataValue, int>();
            var rowToSeen = new int[table.RowCount];
            for (var row = 0; row < table.RowCount; row++)
            {
                _context.Tick(row);
                _meter.Add(1);
                var value = Comparable(reader.Read(row, evaluation));
                if (!positions.TryGetValue(value, out var position))
                {
                    position = firstSeen.Count;
                    positions[value] = position;
                    firstSeen.Add(value);
                }

                rowToSeen[row] = position;
            }

            // The order of the cells: a stable sort of the values in order of first appearance
            var order = OrderValues(firstSeen, definition);
            var rank = new int[firstSeen.Count];
            var domain = new List<DataValue>(firstSeen.Count);
            for (var i = 0; i < order.Length; i++)
            {
                rank[order[i]] = i;
                domain.Add(firstSeen[order[i]]);
            }

            for (var row = 0; row < rowToSeen.Length; row++)
            {
                rowToSeen[row] = rank[rowToSeen[row]];
            }

            return new FacetKeys(domain, rowToSeen);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, _context.Locate(definition));
        }
    }

    /// <summary>
    ///     Makes the value of a row a value that the cells can be told apart by: an array or an object is its text.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value itself; the text of an array, an object or a regular expression; null when its text is too long.</returns>
    private static DataValue Comparable(DataValue value)
    {
        if (value.Kind is not (DataValueKind.Array or DataValueKind.Object or DataValueKind.RegExp))
        {
            return value;
        }

        try
        {
            return DataValue.FromString(value.ToText());
        }
        catch (InvalidOperationException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Orders the distinct values of a facet field as the <c>sort</c> of its definition asks.
    /// </summary>
    /// <param name="values">The values in order of first appearance.</param>
    /// <param name="definition">The field definition.</param>
    /// <returns>The positions in <paramref name="values"/> in the order of the cells.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the sort array is longer than <see cref="VegaLiteLimits.MaxScaleDomainSize"/>.</exception>
    private int[] OrderValues(List<DataValue> values, SpecNode definition)
    {
        var positions = Enumerable.Range(0, values.Count);
        var sort = definition["sort"];
        if (sort is { IsArray: true } listed)
        {
            // Vega-Lite's sort_index: the position of the value in the array, the length of the array for a value that is not listed; the first of equal indexes is the first seen
            if (listed.Items.Count > _context.Limits.MaxScaleDomainSize)
            {
                throw new VegaLiteRenderException(
                    $"The sort of the facet field lists {listed.Items.Count} values, more than the limit of {_context.Limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).",
                    _context.Locate(listed));
            }

            var index = new Dictionary<DataValue, int>();
            for (var i = 0; i < listed.Items.Count; i++)
            {
                _context.Tick(i);
                if (ListedValue(listed.Items[i]) is { } value)
                {
                    index.TryAdd(value, i);
                }
            }

            return [.. positions.OrderBy(p => index.TryGetValue(values[p], out var found) ? found : listed.Items.Count)];
        }

        var comparer = sort is { IsString: true } && sort.StringValue == "descending"
            ? Comparer<DataValue>.Create((x, y) => DataValue.Compare(y, x))
            : Comparer<DataValue>.Create(DataValue.Compare);
        return [.. positions.OrderBy(p => values[p], comparer)];
    }

    /// <summary>
    ///     Reads a value of a sort array.
    /// </summary>
    /// <param name="item">The item of the array.</param>
    /// <returns>The value; null for an item that is not a string, a number, a boolean or <c>null</c> (it matches no value).</returns>
    private static DataValue? ListedValue(SpecNode item)
    {
        if (item.IsString)
        {
            return DataValue.FromString(item.StringValue);
        }

        if (item.IsNumber)
        {
            return DataValue.FromNumber(item.NumberValue);
        }

        if (item.IsBoolean)
        {
            return DataValue.FromBoolean(item.BoolValue);
        }

        return item.IsNull ? default(DataValue) : null;
    }
}
