// Portions derived from Vega (BSD-3-Clause): the grid and trellis layout of vega-view-transforms (layout/grid.js); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Layout;

/// <summary>
///     One cell of a concatenation or a facet that the layout places: its built group and the bounds of everything in it.
/// </summary>
/// <param name="Group">The group of the cell. Its position is replaced by the layout (the grid decides where a cell goes); its width and height are the box that <c>bounds: "flush"</c> lays out.</param>
/// <param name="Bounds">
///     The bounding box of everything in the cell (the data rectangle, the axes and legends it owns, its title, the stroke of its border)
///     in the coordinates of the cell, that is relative to the origin of <paramref name="Group"/>; empty for a cell that contains nothing.
/// </param>
internal sealed record CompositionCell(SceneGroup Group, SceneBounds Bounds);

/// <summary>
///     A header, a footer or a title group of a facet (a row or column header with its axis and label, the title of a channel).
/// </summary>
/// <param name="Group">The group, built at the origin (its position is replaced by the layout).</param>
/// <param name="Bounds">The bounding box of everything in the group in the coordinates of the group, that is relative to its origin.</param>
internal sealed record HeaderGroup(SceneGroup Group, SceneBounds Bounds);

/// <summary>
///     The headers, footers and titles around the cells of a facet (the <c>row-header</c>, <c>row-footer</c>, <c>column-header</c>,
///     <c>column-footer</c>, <c>row-title</c> and <c>column-title</c> groups of Vega's layout).
/// </summary>
/// <remarks>
///     A header (footer) list has one entry per row (column) of the grid, in order; entries beyond the number of rows (columns) are ignored,
///     as Vega ignores them. The offsets are the distances from the edge of the cells, always given as positive numbers: a header is
///     placed that far to the left of (above) the cells, a footer that far to the right of (below) them.
/// </remarks>
internal sealed record CompositionHeaders
{
    /// <summary>Gets the row headers: one per row, placed left of the first column.</summary>
    public IReadOnlyList<HeaderGroup> RowHeaders { get; init; } = [];

    /// <summary>Gets the row footers: one per row, placed right of the last column.</summary>
    public IReadOnlyList<HeaderGroup> RowFooters { get; init; } = [];

    /// <summary>Gets the column headers: one per column, placed above the first row.</summary>
    public IReadOnlyList<HeaderGroup> ColumnHeaders { get; init; } = [];

    /// <summary>Gets the column footers: one per column, placed below the last row.</summary>
    public IReadOnlyList<HeaderGroup> ColumnFooters { get; init; } = [];

    /// <summary>Gets the title of the rows, or null.</summary>
    public HeaderGroup? RowTitle { get; init; }

    /// <summary>Gets the title of the columns, or null.</summary>
    public HeaderGroup? ColumnTitle { get; init; }

    /// <summary>Gets the distance between the row headers and the cells; 0 by default.</summary>
    public double RowHeaderOffset { get; init; }

    /// <summary>Gets the distance between the column headers and the cells; 0 by default.</summary>
    public double ColumnHeaderOffset { get; init; }

    /// <summary>Gets the distance between the cells and the row footers; 0 by default.</summary>
    public double RowFooterOffset { get; init; }

    /// <summary>Gets the distance between the cells and the column footers; 0 by default.</summary>
    public double ColumnFooterOffset { get; init; }

    /// <summary>Gets the distance between the row title and the row headers (or footers); Vega-Lite uses 10.</summary>
    public double RowTitleOffset { get; init; }

    /// <summary>Gets the distance between the column title and the column headers (or footers); Vega-Lite uses 10.</summary>
    public double ColumnTitleOffset { get; init; }

    /// <summary>Gets a value indicating whether the row title goes right of the row footers instead of left of the row headers.</summary>
    public bool RowTitleAtEnd { get; init; }

    /// <summary>Gets a value indicating whether the column title goes below the column footers instead of above the column headers.</summary>
    public bool ColumnTitleAtEnd { get; init; }

    /// <summary>Gets where the row title sits along the grid: 0 is the top, 0.5 (the default) the middle and 1 the bottom of the cells.</summary>
    public double RowTitleBand { get; init; } = 0.5;

    /// <summary>Gets where the column title sits along the grid: 0 is the left, 0.5 (the default) the middle and 1 the right of the cells.</summary>
    public double ColumnTitleBand { get; init; } = 0.5;
}

/// <summary>
///     The result of placing a composition: everything to draw and the room it takes.
/// </summary>
/// <param name="Items">
///     The placed groups in drawing order (Vega-Lite's order: the row title and the column title, the row headers and footers, the column headers
///     and footers, then the cells in the order given; a cell that is null has no group).
/// </param>
/// <param name="Bounds">The union of the full bounds of the cells, headers, footers and titles in the coordinates of the composition; empty when nothing was placed.</param>
/// <param name="CellOrigins">The position of the origin of every cell (a null cell too, which keeps its track), in the order the cells were given.</param>
internal sealed record CompositionResult(IReadOnlyList<SceneItem> Items, SceneBounds Bounds, IReadOnlyList<(double X, double Y)> CellOrigins);

/// <summary>
///     Places the cells of a concatenation or a facet in a grid with their headers, footers and titles: a port of Vega's grid layout and
///     trellis layout.
/// </summary>
/// <remarks>
///     <para>
///         <b>Pure boxes in, groups out.</b> The input is the built groups of the cells with the bounds of their content, the headers of a
///         facet built at the origin, and the layout settings that Vega-Lite assigns (<c>columns</c>, <c>spacing</c>, <c>align</c>, <c>center</c>,
///         <c>bounds</c>); nothing is measured or drawn here, so every number follows from the boxes by hand. The result is the same groups
///         moved to their places (a <c>with</c> copy: the inputs stay unchanged) together with the bounds of the whole.
///     </para>
///     <para>
///         <b>Grid.</b> The cells are placed with <see cref="LegendBuilder.GridLayout"/>, the port of Vega's <c>gridLayout</c> that the legend
///         layout shares: <c>bounds: "full"</c> lays out the bounds of the content of each cell (an empty box is the point at the origin),
///         <c>"flush"</c> the width and height of the group only; the spacing is the padding between two columns (rows), a column (row)
///         starts after the widest (tallest) cell of the previous one rounded up, <c>align: "all"</c> gives every column and row the size of
///         the largest cell, <c>"none"</c> lets cells follow each other, and <c>center</c> centers the smaller cells of a track. A cell that is
///         null is an empty cell: it keeps its track and its spacing but draws nothing, like the empty cell of a facet that has no data.
///     </para>
///     <para>
///         <b>Trellis.</b> A header margin is, like in Vega, the smallest (floor) left or top edge of the first column or row, or the largest
///         (ceiling) right or bottom edge of the last one, counting from 0, plus the offset; headers are then placed at the position of the
///         cell of their row or column (the nearest earlier cell when that cell is missing, for the empty cells of a wrapped last row) and the
///         row and column titles outside the headers by their offset, centered along the grid by their band (rounded half up).
///         With no header at all the margin without its offset is returned, as Vega does, so a title sits at the cells' edge minus its offset.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use; the loops run once over the cells and headers that the caller has bounded and
///         charged, so there is no cancellation point.
///     </para>
/// </remarks>
internal static partial class CompositionLayout
{
    /// <summary>
    ///     Places the cells, headers, footers and titles of a composition.
    /// </summary>
    /// <param name="cells">The cells in grid order (row-major); a null entry is an empty cell.</param>
    /// <param name="headers">The headers, footers and titles of a facet, or null for none.</param>
    /// <param name="settings">The layout settings.</param>
    /// <returns>The placed groups, the bounds of the whole, and the origin of each cell.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> or <paramref name="settings"/> is null.</exception>
    public static CompositionResult Place(IReadOnlyList<CompositionCell?> cells, CompositionHeaders? headers, CompositionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(settings);
        var n = cells.Count;

        // The initial grid layout
        var boxes = new SceneBounds[n];
        for (var i = 0; i < n; i++)
        {
            boxes[i] = BoxOf(cells[i], settings.Flush);
        }

        var columns = settings.Columns is > 0 ? settings.Columns.Value : n;
        var grid = LegendBuilder.GridLayout(
            boxes,
            new GridOptions(
                columns,
                ToGrid(settings.AlignColumn),
                ToGrid(settings.AlignRow),
                settings.SpacingColumn,
                settings.SpacingRow,
                settings.CenterColumn,
                settings.CenterRow));

        // Where the cells are, and the full bounds of the grid
        var placed = new PlacedCell?[n];
        var origins = new (double X, double Y)[n];
        var gridBounds = SceneBounds.Empty;
        for (var i = 0; i < n; i++)
        {
            origins[i] = (grid.Dx[i], grid.Dy[i]);
            if (cells[i] is { } cell)
            {
                var full = cell.Bounds.Translate(grid.Dx[i], grid.Dy[i]);
                placed[i] = new PlacedCell(grid.Dx[i], grid.Dy[i], cell.Group.Width, cell.Group.Height, full);
                gridBounds = gridBounds.Union(full);
            }
        }

        var bounds = gridBounds;
        var items = new List<SceneItem>();
        if (headers is not null)
        {
            bounds = bounds.Union(LayoutTrellis(headers, placed, columns, settings.Flush, gridBounds.IsEmpty ? new SceneBounds(0, 0, 0, 0) : gridBounds, items));
        }

        for (var i = 0; i < n; i++)
        {
            if (cells[i] is { } cell)
            {
                items.Add(cell.Group with { X = grid.Dx[i], Y = grid.Dy[i] });
            }
        }

        return new CompositionResult(items, bounds, origins);
    }

    /// <summary>
    ///     Gets the box of a cell that the grid lays out.
    /// </summary>
    /// <param name="cell">The cell, or null for an empty cell.</param>
    /// <param name="flush">Whether the width and height of the group count instead of its full bounds.</param>
    /// <returns>The box relative to the origin of the cell; the point at the origin for an empty cell or a cell with empty bounds.</returns>
    private static SceneBounds BoxOf(CompositionCell? cell, bool flush)
    {
        if (cell is null)
        {
            return new SceneBounds(0, 0, 0, 0);
        }

        if (flush)
        {
            return new SceneBounds(0, 0, Math.Max(0, cell.Group.Width), Math.Max(0, cell.Group.Height));
        }

        return cell.Bounds.IsEmpty ? new SceneBounds(0, 0, 0, 0) : cell.Bounds;
    }

    /// <summary>
    ///     Maps the alignment of the specification to that of the grid layout.
    /// </summary>
    /// <param name="align">The alignment.</param>
    /// <returns>The grid alignment.</returns>
    private static GridAlign ToGrid(CompositionAlign align) => align switch
    {
        CompositionAlign.All => GridAlign.All,
        CompositionAlign.None => GridAlign.None,
        _ => GridAlign.Each,
    };
}
