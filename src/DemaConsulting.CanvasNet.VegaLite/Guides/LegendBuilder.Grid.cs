// Portions derived from Vega (BSD-3-Clause): the layout transform of vega-view-transforms; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     How the cells of a grid are aligned: Vega's <c>align</c> of a layout.
/// </summary>
internal enum GridAlign
{
    /// <summary>No alignment: each cell follows its neighbor.</summary>
    None,

    /// <summary>Each column and row is as wide and tall as its largest cell.</summary>
    Each,

    /// <summary>Every column and row is as wide and tall as the largest cell of the grid.</summary>
    All,
}

/// <summary>
///     Where a grid is anchored on an axis: its start (the anchor is its first edge), its middle or its end.
/// </summary>
internal enum GridAnchor
{
    /// <summary>The anchor is the first edge of the grid.</summary>
    Start,

    /// <summary>The anchor is the middle of the grid.</summary>
    Middle,

    /// <summary>The anchor is the last edge of the grid.</summary>
    End,
}

/// <summary>
///     The parameters of <see cref="LegendBuilder.GridLayout"/>, Vega's layout options.
/// </summary>
/// <param name="Columns">The number of columns; 0 or less puts every cell in one row.</param>
/// <param name="AlignColumns">How the columns are aligned.</param>
/// <param name="AlignRows">How the rows are aligned.</param>
/// <param name="ColumnPadding">The gap between two columns.</param>
/// <param name="RowPadding">The gap between two rows.</param>
/// <param name="CenterColumns">Whether the cells are centered in their column when there is more than one row.</param>
/// <param name="CenterRows">Whether the cells are centered in their row when there is more than one column.</param>
/// <param name="X">The x coordinate of the anchor.</param>
/// <param name="Y">The y coordinate of the anchor.</param>
/// <param name="ColumnAnchor">How the grid is anchored horizontally at <paramref name="X"/>.</param>
/// <param name="RowAnchor">How the grid is anchored vertically at <paramref name="Y"/>.</param>
internal sealed record GridOptions(
    int Columns,
    GridAlign AlignColumns = GridAlign.Each,
    GridAlign AlignRows = GridAlign.Each,
    double ColumnPadding = 0,
    double RowPadding = 0,
    bool CenterColumns = false,
    bool CenterRows = false,
    double X = 0,
    double Y = 0,
    GridAnchor ColumnAnchor = GridAnchor.Start,
    GridAnchor RowAnchor = GridAnchor.Start);

/// <summary>
///     The result of a grid layout: where each cell goes and the box of the whole grid.
/// </summary>
/// <param name="Dx">The horizontal shift of each cell from its own origin.</param>
/// <param name="Dy">The vertical shift of each cell from its own origin.</param>
/// <param name="Bounds">The union of the shifted boxes of the cells.</param>
internal sealed record GridResult(IReadOnlyList<double> Dx, IReadOnlyList<double> Dy, SceneBounds Bounds);

/// <content>
///     The grid layout that places the entries of a symbol legend and, in the <c>LegendLayout</c> unit, the legends themselves.
/// </content>
internal sealed partial class LegendBuilder
{
    /// <summary>
    ///     Lays out cells in a grid the way Vega's <c>layout</c> transform does.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each cell is given as its box relative to its own origin (the origin of the cell is where the layout shifts from). A
    ///         column offset is the padding plus the room the box has on the left of its origin; with <see cref="GridAlign.Each"/> a column
    ///         starts after the widest cell (the box edge, rounded up) of the previous one, with <see cref="GridAlign.All"/> after the
    ///         widest cell of the whole grid; rows likewise. The result is anchored: the grid is moved so that its first edge, middle or last
    ///         edge is at the anchor, and the anchor is rounded half up. The loops run once over the cells (at most the entries that the
    ///         callers have bounded and charged), so no cancellation point is needed inside.
    ///     </para>
    ///     <para>
    ///         The vertical centering of a cell in its row, as in Vega, uses the shift of the cell from the top of the grid, so it only has an
    ///         effect on the first row.
    ///     </para>
    /// </remarks>
    /// <param name="boxes">The box of each cell relative to its origin; not empty.</param>
    /// <param name="options">The parameters.</param>
    /// <returns>The shifts of the cells and the box of the grid.</returns>
    internal static GridResult GridLayout(IReadOnlyList<SceneBounds> boxes, GridOptions options)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(options);
        var n = boxes.Count;
        if (n == 0)
        {
            return new GridResult([], [], SceneBounds.Empty);
        }

        var columns = options.Columns > 0 ? options.Columns : n;
        var rows = (int)Math.Ceiling(n / (double)columns);
        var xOffset = new double[n];
        var yOffset = new double[n];
        var xExtent = new double[columns];
        var yExtent = new double[rows];
        var dx = new double[n];
        var dy = new double[n];
        double xMax = 0;
        double yMax = 0;

        // Determine the offset of each cell
        for (var i = 0; i < n; i++)
        {
            var b = boxes[i];
            var c = i % columns;
            var r = i / columns;
            var px = Math.Ceiling(b.X2);
            var py = Math.Ceiling(b.Y2);
            xMax = Math.Max(xMax, px);
            yMax = Math.Max(yMax, py);
            xExtent[c] = Math.Max(xExtent[c], px);
            yExtent[r] = Math.Max(yExtent[r], py);
            xOffset[i] = options.ColumnPadding + OffsetOf(b.X1);
            yOffset[i] = options.RowPadding + OffsetOf(b.Y1);
        }

        // The first column and the first row start at the edge
        for (var i = 0; i < n; i++)
        {
            if (i % columns == 0)
            {
                xOffset[i] = 0;
            }

            if (i < columns)
            {
                yOffset[i] = 0;
            }
        }

        AlignColumns(options.AlignColumns, columns, n, xOffset, xExtent, xMax);
        AlignRows(options.AlignRows, columns, rows, n, yOffset, yExtent, yMax);

        // Horizontal layout
        double x = 0;
        for (var i = 0; i < n; i++)
        {
            x = xOffset[i] + (i % columns != 0 ? x : 0);
            dx[i] += x;
        }

        // Vertical layout
        for (var c = 0; c < columns; c++)
        {
            double y = 0;
            for (var i = c; i < n; i += columns)
            {
                y += yOffset[i];
                dy[i] += y;
            }
        }

        // Centering
        if (options.AlignColumns != GridAlign.None && options.CenterColumns && rows > 1)
        {
            for (var i = 0; i < n; i++)
            {
                var b = options.AlignColumns == GridAlign.All ? xMax : xExtent[i % columns];
                var gap = b - boxes[i].X2 - dx[i];
                if (gap > 0)
                {
                    dx[i] += gap / 2;
                }
            }
        }

        if (options.AlignRows != GridAlign.None && options.CenterRows && columns != 1)
        {
            for (var i = 0; i < n; i++)
            {
                var b = options.AlignRows == GridAlign.All ? yMax : yExtent[i / columns];
                var gap = b - boxes[i].Y2 - dy[i];
                if (gap > 0)
                {
                    dy[i] += gap / 2;
                }
            }
        }

        // Position the grid relative to its anchor
        var bounds = SceneBounds.Empty;
        for (var i = 0; i < n; i++)
        {
            bounds = bounds.Union(boxes[i].Translate(dx[i], dy[i]));
        }

        var ax = options.X;
        var ay = options.Y;
        ax -= options.ColumnAnchor switch
        {
            GridAnchor.End => bounds.Width,
            GridAnchor.Middle => bounds.Width / 2,
            _ => 0,
        };
        ay -= options.RowAnchor switch
        {
            GridAnchor.End => bounds.Height,
            GridAnchor.Middle => bounds.Height / 2,
            _ => 0,
        };
        ax = TextMeasurer.RoundHalfUp(ax);
        ay = TextMeasurer.RoundHalfUp(ay);
        var placed = SceneBounds.Empty;
        for (var i = 0; i < n; i++)
        {
            dx[i] += ax;
            dy[i] += ay;
            placed = placed.Union(boxes[i].Translate(dx[i], dy[i]));
        }

        return new GridResult(dx, dy, placed);
    }

    /// <summary>
    ///     Gets the room a box needs before its origin: the distance it reaches to the left or the top, rounded up.
    /// </summary>
    /// <param name="start">The first edge of the box.</param>
    /// <returns>The room, or 0 when the box starts at or after the origin.</returns>
    private static double OffsetOf(double start) => start < 0 ? Math.Ceiling(-start) : 0;

    /// <summary>
    ///     Applies the column alignment to the column offsets.
    /// </summary>
    /// <param name="align">The alignment.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="n">The number of cells.</param>
    /// <param name="offset">The offset of each cell; updated.</param>
    /// <param name="extent">The extent of each column.</param>
    /// <param name="max">The largest extent.</param>
    private static void AlignColumns(GridAlign align, int columns, int n, double[] offset, double[] extent, double max)
    {
        if (align == GridAlign.Each)
        {
            for (var c = 1; c < columns; c++)
            {
                double widest = 0;
                for (var i = c; i < n; i += columns)
                {
                    widest = Math.Max(widest, offset[i]);
                }

                for (var i = c; i < n; i += columns)
                {
                    offset[i] = widest + extent[c - 1];
                }
            }
        }
        else if (align == GridAlign.All)
        {
            double widest = 0;
            for (var i = 0; i < n; i++)
            {
                if (i % columns != 0)
                {
                    widest = Math.Max(widest, offset[i]);
                }
            }

            for (var i = 0; i < n; i++)
            {
                if (i % columns != 0)
                {
                    offset[i] = widest + max;
                }
            }
        }
        else
        {
            for (var c = 1; c < columns; c++)
            {
                for (var i = c; i < n; i += columns)
                {
                    offset[i] += extent[c - 1];
                }
            }
        }
    }

    /// <summary>
    ///     Applies the row alignment to the row offsets.
    /// </summary>
    /// <param name="align">The alignment.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="n">The number of cells.</param>
    /// <param name="offset">The offset of each cell; updated.</param>
    /// <param name="extent">The extent of each row.</param>
    /// <param name="max">The largest extent.</param>
    private static void AlignRows(GridAlign align, int columns, int rows, int n, double[] offset, double[] extent, double max)
    {
        if (align == GridAlign.Each)
        {
            for (var r = 1; r < rows; r++)
            {
                double tallest = 0;
                var end = Math.Min(n, (r * columns) + columns);
                for (var i = r * columns; i < end; i++)
                {
                    tallest = Math.Max(tallest, offset[i]);
                }

                for (var i = r * columns; i < end; i++)
                {
                    offset[i] = tallest + extent[r - 1];
                }
            }
        }
        else if (align == GridAlign.All)
        {
            double tallest = 0;
            for (var i = columns; i < n; i++)
            {
                tallest = Math.Max(tallest, offset[i]);
            }

            for (var i = columns; i < n; i++)
            {
                offset[i] = tallest + max;
            }
        }
        else
        {
            for (var r = 1; r < rows; r++)
            {
                var end = Math.Min(n, (r * columns) + columns);
                for (var i = r * columns; i < end; i++)
                {
                    offset[i] += extent[r - 1];
                }
            }
        }
    }
}
