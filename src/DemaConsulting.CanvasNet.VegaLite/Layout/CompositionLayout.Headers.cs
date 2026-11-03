// Portions derived from Vega (BSD-3-Clause): the grid and trellis layout of vega-view-transforms (layout/grid.js); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Layout;

/// <content>
///     The trellis part of the layout: the headers, footers and titles around the grid (Vega's <c>trellisLayout</c>, <c>layoutHeaders</c> and <c>layoutTitle</c>).
/// </content>
internal static partial class CompositionLayout
{
    /// <summary>
    ///     A cell after the grid has placed it.
    /// </summary>
    /// <param name="X">The x position of the origin of the cell.</param>
    /// <param name="Y">The y position of the origin of the cell.</param>
    /// <param name="Width">The width of the group of the cell.</param>
    /// <param name="Height">The height of the group of the cell.</param>
    /// <param name="Full">The bounds of the content of the cell in the coordinates of the composition; empty when the cell has no content.</param>
    private readonly record struct PlacedCell(double X, double Y, double Width, double Height, SceneBounds Full);

    /// <summary>
    ///     The side of a cell that a header or a footer reaches to.
    /// </summary>
    private enum CellEdge
    {
        /// <summary>The left edge.</summary>
        Left,

        /// <summary>The top edge.</summary>
        Top,

        /// <summary>The right edge.</summary>
        Right,

        /// <summary>The bottom edge.</summary>
        Bottom,
    }

    /// <summary>
    ///     How one kind of header is laid out (the parameters that Vega's <c>layoutHeaders</c> takes).
    /// </summary>
    /// <param name="Limit">The most headers there can be: the number of rows or columns.</param>
    /// <param name="Offset">The offset from the edge of the cells; negative for headers, positive for footers.</param>
    /// <param name="Footer">Whether the margin is the largest (rounded up) edge, for footers, instead of the smallest (rounded down), for headers.</param>
    /// <param name="IsColumn">Whether the headers are those of columns, which move along x, instead of those of rows.</param>
    /// <param name="Edge">The edge of the cells the margin is taken from.</param>
    /// <param name="Start">The index of the first cell that the headers align to.</param>
    /// <param name="Stride">The distance between the indexes of the cells of consecutive headers.</param>
    /// <param name="Back">The distance to step back from a missing cell to find the nearest one.</param>
    private readonly record struct HeaderPass(int Limit, double Offset, bool Footer, bool IsColumn, CellEdge Edge, int Start, int Stride, int Back);

    /// <summary>
    ///     Places the headers, footers and titles around a grid that is already laid out.
    /// </summary>
    /// <param name="headers">The headers.</param>
    /// <param name="groups">The placed cells (null for an empty cell) in grid order.</param>
    /// <param name="columns">The number of columns (0 or less: one row).</param>
    /// <param name="flush">Whether the edges of the cells are those of their groups instead of their bounds.</param>
    /// <param name="grid">The bounds of the grid; a point at the origin when the grid is empty.</param>
    /// <param name="items">The list that receives the placed groups in drawing order (titles, then row headers and footers, then column headers and footers).</param>
    /// <returns>The union of the bounds of everything that was placed.</returns>
    private static SceneBounds LayoutTrellis(CompositionHeaders headers, PlacedCell?[] groups, int columns, bool flush, SceneBounds grid, List<SceneItem> items)
    {
        var n = groups.Length;
        var columnCount = columns <= 0 ? n : columns;
        var rowCount = columnCount <= 0 ? 1 : (int)Math.Ceiling(n / (double)columnCount);
        var cells = rowCount * columnCount;
        var union = SceneBounds.Empty;
        var rowHeaders = new List<SceneItem>();
        var rowFooters = new List<SceneItem>();
        var columnHeaders = new List<SceneItem>();
        var columnFooters = new List<SceneItem>();

        var x = LayoutHeaders(headers.RowHeaders, groups, new HeaderPass(rowCount, -headers.RowHeaderOffset, false, false, CellEdge.Left, 0, columnCount, 1), flush, rowHeaders, ref union);
        var y = LayoutHeaders(headers.ColumnHeaders, groups, new HeaderPass(columnCount, -headers.ColumnHeaderOffset, false, true, CellEdge.Top, 0, 1, columnCount), flush, columnHeaders, ref union);
        var x2 = LayoutHeaders(headers.RowFooters, groups, new HeaderPass(rowCount, headers.RowFooterOffset, true, false, CellEdge.Right, columnCount - 1, columnCount, 1), flush, rowFooters, ref union);
        var y2 = LayoutHeaders(headers.ColumnFooters, groups, new HeaderPass(columnCount, headers.ColumnFooterOffset, true, true, CellEdge.Bottom, cells - columnCount, 1, columnCount), flush, columnFooters, ref union);

        // Row title: outside the row headers (or footers), centered along the grid by its band
        if (headers.RowTitle is { } rowTitle)
        {
            var offset = headers.RowTitleAtEnd ? x2 + headers.RowTitleOffset : x - headers.RowTitleOffset;
            var top = TextMeasurer.RoundHalfUp(grid.Y1 + (headers.RowTitleBand * grid.Height));
            items.Add(PlaceTitle(rowTitle, offset, top, ref union));
        }

        // Column title: above the column headers (or below the footers), centered along the grid by its band
        if (headers.ColumnTitle is { } columnTitle)
        {
            var offset = headers.ColumnTitleAtEnd ? y2 + headers.ColumnTitleOffset : y - headers.ColumnTitleOffset;
            var left = TextMeasurer.RoundHalfUp(grid.X1 + (headers.ColumnTitleBand * grid.Width));
            items.Add(PlaceTitle(columnTitle, left, offset, ref union));
        }

        items.AddRange(rowHeaders);
        items.AddRange(rowFooters);
        items.AddRange(columnHeaders);
        items.AddRange(columnFooters);
        return union;
    }

    /// <summary>
    ///     Places one kind of header (Vega's <c>layoutHeaders</c>).
    /// </summary>
    /// <remarks>
    ///     The margin starts at 0 and takes the edge of every cell of the first (last) column or row, rounded down (up) for headers (footers);
    ///     the offset is added only when there are headers. Each header then goes to the position of its cell: column headers at its x with
    ///     the margin as y, row headers the other way round. A header whose cell is missing uses the nearest earlier cell of its row or column.
    ///     More headers than <see cref="HeaderPass.Limit"/> are ignored, as in Vega.
    /// </remarks>
    /// <param name="headers">The headers.</param>
    /// <param name="groups">The placed cells.</param>
    /// <param name="pass">How the headers are laid out.</param>
    /// <param name="flush">Whether the edges of the cells are those of their groups instead of their bounds.</param>
    /// <param name="placed">The list that receives the placed header groups.</param>
    /// <param name="union">The union of the bounds of everything placed so far; grown by these headers.</param>
    /// <returns>The edge of the headers (rounded like the margin), or the margin without the offset when there are no headers; 0 when there are no cells.</returns>
    private static double LayoutHeaders(IReadOnlyList<HeaderGroup> headers, PlacedCell?[] groups, HeaderPass pass, bool flush, List<SceneItem> placed, ref SceneBounds union)
    {
        var n = groups.Length;
        double init = 0;
        double edge = 0;

        // If there are no cells, return 0
        if (n == 0)
        {
            return init;
        }

        // Compute the margin
        for (var i = pass.Start; i < n; i += pass.Stride)
        {
            if (groups[i] is { } cell && EdgeOf(cell, pass.Edge, flush) is { } value)
            {
                init = Aggregate(pass.Footer, init, value);
            }
        }

        // If there are no headers, return the margin
        if (headers.Count == 0)
        {
            return init;
        }

        var count = Math.Min(headers.Count, pass.Limit);
        init += pass.Offset;
        var mark = SceneBounds.Empty;
        for (int i = pass.Start, j = 0; j < count; j++, i += pass.Stride)
        {
            var header = headers[j];

            // Search for the nearest cell to align to, necessary if the table has empty cells
            PlacedCell? aligned = null;
            for (var k = i; k >= 0; k -= pass.Back)
            {
                if (k < n && groups[k] is { } found)
                {
                    aligned = found;
                    break;
                }
            }

            double x;
            double y;
            if (pass.IsColumn)
            {
                x = aligned?.X ?? 0;
                y = init;
            }
            else
            {
                x = init;
                y = aligned?.Y ?? 0;
            }

            var moved = header.Bounds.Translate(x, y);
            mark = mark.Union(moved);
            placed.Add(header.Group with { X = x, Y = y });

            // Update the current edge of the layout bounds
            if (!mark.IsEmpty)
            {
                edge = Aggregate(pass.Footer, edge, EdgeOf(mark, pass.Edge));
            }
        }

        union = union.Union(mark);
        return edge;
    }

    /// <summary>
    ///     Places a row or column title (Vega's <c>layoutTitle</c>).
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="x">The x position of its origin.</param>
    /// <param name="y">The y position of its origin.</param>
    /// <param name="union">The union of the bounds of everything placed so far; grown by the title.</param>
    /// <returns>The moved group.</returns>
    private static SceneGroup PlaceTitle(HeaderGroup title, double x, double y, ref SceneBounds union)
    {
        union = union.Union(title.Bounds.Translate(x, y));
        return title.Group with { X = x, Y = y };
    }

    /// <summary>
    ///     Combines a margin with an edge: Vega's <c>min</c> rounded down for headers and <c>max</c> rounded up for footers.
    /// </summary>
    /// <param name="footer">Whether the larger value counts, rounded up.</param>
    /// <param name="a">The margin so far.</param>
    /// <param name="b">The edge.</param>
    /// <returns>The new margin.</returns>
    private static double Aggregate(bool footer, double a, double b) => footer ? Math.Ceiling(Math.Max(a, b)) : Math.Floor(Math.Min(a, b));

    /// <summary>
    ///     Gets an edge of a placed cell.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="flush">Whether the edge is that of the group (the origin and the size) instead of the bounds of its content.</param>
    /// <returns>The edge, or null when the cell has no content and <paramref name="flush"/> is false (it takes no part in the margin).</returns>
    private static double? EdgeOf(PlacedCell cell, CellEdge edge, bool flush)
    {
        if (flush)
        {
            return edge switch
            {
                CellEdge.Left => cell.X,
                CellEdge.Top => cell.Y,
                CellEdge.Right => cell.X + cell.Width,
                _ => cell.Y + cell.Height,
            };
        }

        return cell.Full.IsEmpty ? null : EdgeOf(cell.Full, edge);
    }

    /// <summary>
    ///     Gets an edge of a box.
    /// </summary>
    /// <param name="box">The box.</param>
    /// <param name="edge">The edge.</param>
    /// <returns>The coordinate of the edge.</returns>
    private static double EdgeOf(SceneBounds box, CellEdge edge) => edge switch
    {
        CellEdge.Left => box.X1,
        CellEdge.Top => box.Y1,
        CellEdge.Right => box.X2,
        _ => box.Y2,
    };
}
