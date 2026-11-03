using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Layout;

/// <summary>
///     Unit tests of <see cref="CompositionLayout"/>: the grid of a concatenation or a facet and the headers, footers and titles around it. The
///     cells and headers are pure boxes (a group with a width and height and the bounds of its content), and every expected position is derived by
///     hand from the rules of Vega's <c>gridLayout</c> and <c>trellisLayout</c> (the margins are rounded up or down to whole pixels, a track starts
///     after the largest cell of the previous one), never copied from a rendering.
/// </summary>
public class CompositionLayoutTests
{
    /// <summary>
    ///     Creates the layout settings of a concatenation: spacing 20, one row, aligned each, not centered, full bounds.
    /// </summary>
    /// <param name="columns">The number of columns, or null for one row.</param>
    /// <param name="align">The alignment of both directions.</param>
    /// <param name="spacingColumn">The spacing between the columns.</param>
    /// <param name="spacingRow">The spacing between the rows.</param>
    /// <param name="center">Whether the cells are centered in both directions.</param>
    /// <param name="flush">Whether the layout uses the width and height of the cells only.</param>
    /// <returns>The settings.</returns>
    private static CompositionSettings Settings(
        int? columns = null,
        CompositionAlign align = CompositionAlign.Each,
        double spacingColumn = 20,
        double spacingRow = 20,
        bool center = false,
        bool flush = false) =>
        new(columns, spacingColumn, spacingRow, align, align, center, center, flush);

    /// <summary>
    ///     Creates a cell whose group is <paramref name="width"/> by <paramref name="height"/> and whose content has the given bounds.
    /// </summary>
    /// <param name="bounds">The bounds of the content in the coordinates of the cell.</param>
    /// <param name="width">The width of the group.</param>
    /// <param name="height">The height of the group.</param>
    /// <returns>The cell.</returns>
    private static CompositionCell Cell(SceneBounds bounds, double width = 100, double height = 50) =>
        new(new SceneGroup("cell", 0, 0) { Width = width, Height = height }, bounds);

    /// <summary>
    ///     Creates a header group with the given role and bounds, at the origin.
    /// </summary>
    /// <param name="role">The role of the group.</param>
    /// <param name="bounds">The bounds of its content in its own coordinates.</param>
    /// <returns>The header group.</returns>
    private static HeaderGroup Header(string role, SceneBounds bounds) => new(new SceneGroup(role, 0, 0), bounds);

    /// <summary>
    ///     Creates the four cells of a grid of two columns and two rows, each 100 by 50 pixels with a half pixel border.
    /// </summary>
    /// <returns>The cells in row-major order.</returns>
    private static CompositionCell?[] Grid2X2()
    {
        var bounds = new SceneBounds(-0.5, -0.5, 100.5, 50.5);
        return [Cell(bounds), Cell(bounds), Cell(bounds), Cell(bounds)];
    }

    /// <summary>
    ///     Gets the position of the group of a placed item.
    /// </summary>
    /// <param name="item">The item, a group.</param>
    /// <returns>The x and y position.</returns>
    private static (double X, double Y) At(SceneItem item)
    {
        var group = Assert.IsType<SceneGroup>(item);
        return (group.X, group.Y);
    }

    /// <summary>
    ///     Two cells side by side are one track plus the spacing apart, counting the room a cell needs left of its origin and rounding the edge up.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_HConcatBoxesAreSpacedByTwentyPlusRoundedMargins()
    {
        // Arrange: two cells whose content reaches from x = -30.5 (an axis) to 100.5 (the border) and from -0.5 to 110.5
        var bounds = new SceneBounds(-30.5, -0.5, 100.5, 110.5);

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds)], null, Settings());

        // Assert: the second cell starts at ceil(30.5) + 20 left margin and after ceil(100.5) of the first: 31 + 20 + 101 = 152
        Assert.Equal([(0.0, 0.0), (152.0, 0.0)], result.CellOrigins);
        Assert.Equal([(0.0, 0.0), (152.0, 0.0)], result.Items.Select(At));

        // Assert: the bounds are the union of the two placed boxes
        Assert.Equal(new SceneBounds(-30.5, -0.5, 252.5, 110.5), result.Bounds);
    }

    /// <summary>
    ///     A single column stacks the cells, a row apart: the top margin plus the spacing and the largest cell above.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_VConcatColumnsOneStacksCells()
    {
        // Arrange: cells that reach 5 above and 10 left of their origin
        var bounds = new SceneBounds(-10, -5, 110, 85);

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds)], null, Settings(columns: 1));

        // Assert: the second cell is at 5 + 20 above its box and below ceil(85): 25 + 85 = 110; all cells share the first column at 0
        Assert.Equal([(0.0, 0.0), (0.0, 110.0)], result.CellOrigins);
    }

    /// <summary>
    ///     With <c>columns</c> the cells wrap: the third of two columns starts the second row, below the tallest cell of the first, at the first column.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_WrapColumnsTwoPlacesThirdCellInSecondRow()
    {
        // Arrange: three cells of 100 by 50
        var bounds = new SceneBounds(0, 0, 100, 50);
        var cells = new[] { Cell(bounds), Cell(bounds), Cell(bounds) };

        // Act
        var result = CompositionLayout.Place(cells, null, Settings(columns: 2, spacingColumn: 20, spacingRow: 10));

        // Assert: x = 20 + 100 for the second column; y = 10 + 50 for the second row
        Assert.Equal([(0.0, 0.0), (120.0, 0.0), (0.0, 60.0)], result.CellOrigins);
    }

    /// <summary>
    ///     The row spacing and the column spacing are independent.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_SpacingRowAndColumnDiffer()
    {
        // Arrange: a grid of 2 by 2 cells of 100 by 50
        var bounds = new SceneBounds(0, 0, 100, 50);
        var cells = new[] { Cell(bounds), Cell(bounds), Cell(bounds), Cell(bounds) };

        // Act
        var result = CompositionLayout.Place(cells, null, Settings(columns: 2, spacingColumn: 20, spacingRow: 8));

        // Assert: columns 120 apart, rows 58 apart
        Assert.Equal([(0.0, 0.0), (120.0, 0.0), (0.0, 58.0), (120.0, 58.0)], result.CellOrigins);
    }

    /// <summary>
    ///     Builds the cells of a grid of 2 by 2 cells of different sizes; the second cell reaches 15 pixels left of its origin.
    /// </summary>
    /// <returns>The cells.</returns>
    private static CompositionCell?[] UnevenCells() =>
    [
        Cell(new SceneBounds(0, 0, 100, 40)),
        Cell(new SceneBounds(-15, 0, 50, 70)),
        Cell(new SceneBounds(0, 0, 60, 30)),
        Cell(new SceneBounds(0, 0, 120, 90)),
    ];

    /// <summary>
    ///     With <c>align: each</c> a column starts after its widest cell and a row after its tallest, whatever the other columns and rows hold.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_AlignEachSizesRowsAndColumnsIndividually()
    {
        // Act
        var result = CompositionLayout.Place(UnevenCells(), null, Settings(columns: 2, CompositionAlign.Each, 10, 10));

        // Assert: column 0 is 100 wide (the widest of 100 and 60); the second column starts at the widest margin (10 + 15 = 25) plus that: 125
        // Assert: row 0 is 70 tall (the tallest of 40 and 70); the second row starts at 10 + 70 = 80
        Assert.Equal([(0.0, 0.0), (125.0, 0.0), (0.0, 80.0), (125.0, 80.0)], result.CellOrigins);
    }

    /// <summary>
    ///     With <c>align: all</c> every column and row is as large as the largest cell of the whole grid.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_AlignAllUsesTheLargestCell()
    {
        // Act
        var result = CompositionLayout.Place(UnevenCells(), null, Settings(columns: 2, CompositionAlign.All, 10, 10));

        // Assert: the widest cell is 120, so the second column starts at 25 + 120 = 145; the tallest is 90, so the second row at 10 + 90 = 100
        Assert.Equal([(0.0, 0.0), (145.0, 0.0), (0.0, 100.0), (145.0, 100.0)], result.CellOrigins);
    }

    /// <summary>
    ///     With <c>align: none</c> each cell follows the one before it in its row: only its own left margin counts, not the widest margin of the column.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_AlignNoneFlowsCells()
    {
        // Act
        var result = CompositionLayout.Place(UnevenCells(), null, Settings(columns: 2, CompositionAlign.None, 10, 10));

        // Assert: the first row's second cell needs its 15 pixel margin (10 + 15 + 100 = 125); the second row's has none (10 + 100 = 110)
        Assert.Equal([(0.0, 0.0), (125.0, 0.0), (0.0, 80.0), (110.0, 80.0)], result.CellOrigins);
    }

    /// <summary>
    ///     With <c>center</c> a smaller cell is centered in the room of its track (Vega does this for the cells at the start of the track).
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_CenterCentersSmallCellsInTheirTracks()
    {
        // Arrange: cells of 100 by 40, 50 by 70, 60 by 30, 120 by 90 that have no margin
        CompositionCell?[] cells =
        [
            Cell(new SceneBounds(0, 0, 100, 40)),
            Cell(new SceneBounds(0, 0, 50, 70)),
            Cell(new SceneBounds(0, 0, 60, 30)),
            Cell(new SceneBounds(0, 0, 120, 90)),
        ];

        // Act: centered horizontally in the column, and (second call) vertically in the row
        var horizontal = CompositionLayout.Place(cells, null, new CompositionSettings(2, 10, 10, CompositionAlign.Each, CompositionAlign.Each, true, false, false));
        var vertical = CompositionLayout.Place(cells, null, new CompositionSettings(2, 10, 10, CompositionAlign.Each, CompositionAlign.Each, false, true, false));

        // Assert: the 60 wide cell in the 100 wide first column moves right by (100 - 60) / 2 = 20; the others fill their column
        Assert.Equal([(0.0, 0.0), (110.0, 0.0), (20.0, 80.0), (110.0, 80.0)], horizontal.CellOrigins);

        // Assert: the 40 tall cell in the 70 tall first row moves down by (70 - 40) / 2 = 15
        Assert.Equal([(0.0, 15.0), (110.0, 0.0), (0.0, 80.0), (110.0, 80.0)], vertical.CellOrigins);
    }

    /// <summary>
    ///     With <c>bounds: flush</c> only the width and height of the groups are laid out: axes and borders that reach outside do not move the next cell.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_FlushBoundsUseWidthAndHeightOnly()
    {
        // Arrange: groups of 100 by 50 whose content reaches far outside
        var bounds = new SceneBounds(-30.5, -0.5, 100.5, 110.5);

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds)], null, Settings(flush: true));

        // Assert: 20 + 100, with no margin and no rounding of the content
        Assert.Equal([(0.0, 0.0), (120.0, 0.0)], result.CellOrigins);

        // Assert: the bounds of the whole are still those of the content
        Assert.Equal(new SceneBounds(-30.5, -0.5, 220.5, 110.5), result.Bounds);
    }

    /// <summary>
    ///     A cell that is null is an empty cell: it keeps its track and spacing but has no group.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_EmptyCellsKeepTheirTracks()
    {
        // Arrange: a cell, a hole, a cell
        var bounds = new SceneBounds(0, 0, 100, 50);

        // Act
        var result = CompositionLayout.Place([Cell(bounds), null, Cell(bounds)], null, Settings());

        // Assert: the hole is a zero wide track that is 20 after the first cell (100 + 20), and the third is another 20 further
        Assert.Equal([(0.0, 0.0), (120.0, 0.0), (140.0, 0.0)], result.CellOrigins);

        // Assert: only the two real cells are items
        Assert.Equal([(0.0, 0.0), (140.0, 0.0)], result.Items.Select(At));
    }

    /// <summary>
    ///     A cell whose content has empty bounds is laid out as a point at its origin, and takes no part in the margin of a footer.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_CellWithEmptyBoundsIsAPointAndDoesNotCountForMargins()
    {
        // Arrange: a cell above a cell with nothing in it, in one column, and one column footer
        CompositionCell?[] cells = [Cell(new SceneBounds(0, 0, 100, 50)), Cell(SceneBounds.Empty)];
        var headers = new CompositionHeaders { ColumnFooters = [Header("footer", new SceneBounds(0, 0, 100, 30))] };

        // Act
        var result = CompositionLayout.Place(cells, headers, Settings(columns: 1));

        // Assert: the empty cell is 20 + 50 below the first
        Assert.Equal([(0.0, 0.0), (0.0, 70.0)], result.CellOrigins);

        // Assert: the footer margin comes from the last row, which has no edge: it stays at 0 (Vega's rule)
        Assert.Equal((0.0, 0.0), At(result.Items[0]));
    }

    /// <summary>
    ///     The column footers of a wrapped grid go below the last row for every column, also the one the last row does not reach.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_ColumnFootersGoBelowTheLastRowIncludingEmptyColumns()
    {
        // Arrange: three cells in two columns (the last row has one empty slot), two column footers
        var bounds = new SceneBounds(-0.5, -0.5, 100.5, 80.5);
        var headers = new CompositionHeaders
        {
            ColumnFooters = [Header("footer-a", new SceneBounds(0, 0, 100, 30)), Header("footer-b", new SceneBounds(0, 0, 100, 30))],
        };

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds), Cell(bounds)], headers, Settings(columns: 2, CompositionAlign.All));

        // Assert: the cells: second column at 21 + 101 = 122; second row at 21 + 81 = 102
        Assert.Equal([(0.0, 0.0), (122.0, 0.0), (0.0, 102.0)], result.CellOrigins);

        // Assert: the margin is the last row's bottom edge 102 + 80.5 rounded up: 183. The first footer is under the first column, the second under
        // the second column (the nearest earlier cell of its column, since the last row has none there): both at y = 183
        var footers = result.Items.Where(i => i.Role == "footer-a" || i.Role == "footer-b").Select(At).ToList();
        Assert.Equal([(0.0, 183.0), (122.0, 183.0)], footers);

        // Assert: the bounds grow to the bottom of the footers: 183 + 30
        Assert.Equal(213.0, result.Bounds.Y2);
    }

    /// <summary>
    ///     The row headers go left of the first column, at the vertical position of their row.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_RowHeadersGoLeftOfTheFirstColumn()
    {
        // Arrange: a grid of 2 by 2 and a row header (an axis 40 pixels wide left of its origin) for each row
        var headers = new CompositionHeaders
        {
            RowHeaders = [Header("row-a", new SceneBounds(-40, 0, 0, 50)), Header("row-b", new SceneBounds(-40, 0, 0, 50))],
        };

        // Act
        var result = CompositionLayout.Place(Grid2X2(), headers, Settings(columns: 2));

        // Assert: the cells: second column at 21 + 101 = 122, second row at 21 + 51 = 72
        Assert.Equal([(0.0, 0.0), (122.0, 0.0), (0.0, 72.0), (122.0, 72.0)], result.CellOrigins);

        // Assert: the headers sit at the margin floor(-0.5) = -1 and at the y of their row
        Assert.Equal([(-1.0, 0.0), (-1.0, 72.0)], result.Items.Take(2).Select(At));

        // Assert: the bounds reach to the left of the headers: -1 - 40
        Assert.Equal(-41.0, result.Bounds.X1);
    }

    /// <summary>
    ///     The offsets of the headers and footers move them away from the cells.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_HeadersAreOffsetByTheirOffset()
    {
        // Arrange: one of each header and footer, and offsets 5 (row header), 3 (column header), 4 (row footer) and 6 (column footer)
        var headers = new CompositionHeaders
        {
            RowHeaders = [Header("row-header", new SceneBounds(-40, 0, 0, 50))],
            RowFooters = [Header("row-footer", new SceneBounds(0, 0, 40, 50))],
            ColumnHeaders = [Header("column-header", new SceneBounds(0, -20, 100, 0))],
            ColumnFooters = [Header("column-footer", new SceneBounds(0, 0, 100, 30))],
            RowHeaderOffset = 5,
            ColumnHeaderOffset = 3,
            RowFooterOffset = 4,
            ColumnFooterOffset = 6,
        };

        // Act
        var result = CompositionLayout.Place(Grid2X2(), headers, Settings(columns: 2));
        var at = result.Items.Where(i => i.Role != "cell").ToDictionary(i => i.Role, At);

        // Assert: row header: floor(-0.5) - 5 = -6; column header: floor(-0.5) - 3 = -4
        Assert.Equal((-6.0, 0.0), at["row-header"]);
        Assert.Equal((0.0, -4.0), at["column-header"]);

        // Assert: row footer: ceil(222.5) + 4 = 227 at the row of the last column's first cell; column footer: ceil(72 + 50.5) + 6 = 129
        Assert.Equal((227.0, 0.0), at["row-footer"]);
        Assert.Equal((0.0, 129.0), at["column-footer"]);
    }

    /// <summary>
    ///     The row title sits its offset left of the row headers and is centered along the grid.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_RowTitleSitsHeaderEdgeMinusOffsetAndIsCenteredByBand()
    {
        // Arrange: row headers 40 wide and a row title 14 wide and 60 tall, 10 pixels from the headers
        var headers = new CompositionHeaders
        {
            RowHeaders = [Header("row-header", new SceneBounds(-40, 0, 0, 50)), Header("row-header", new SceneBounds(-40, 0, 0, 50))],
            RowTitle = Header("row-title", new SceneBounds(-12, -30, 2, 30)),
            RowTitleOffset = 10,
        };

        // Act
        var result = CompositionLayout.Place(Grid2X2(), headers, Settings(columns: 2));

        // Assert: the headers' edge is floor(-1 - 40) = -41, so the title is at -41 - 10 = -51
        // Assert: the grid is 123 tall from -0.5 (to 72 + 50.5 = 122.5), so its middle is -0.5 + 61.5 = 61
        var title = result.Items.Single(i => i.Role == "row-title");
        Assert.Equal((-51.0, 61.0), At(title));

        // Assert: the bounds reach to the left of the title's box: -51 - 12
        Assert.Equal(-63.0, result.Bounds.X1);
    }

    /// <summary>
    ///     A column title at the end goes below the column footers, its offset from them, centered over the grid.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_ColumnTitleAtEndUsesFooterEdgePlusOffset()
    {
        // Arrange: a column footer under each of the two columns and a column title at the end
        var headers = new CompositionHeaders
        {
            ColumnFooters = [Header("column-footer", new SceneBounds(0, 0, 100, 30)), Header("column-footer", new SceneBounds(0, 0, 100, 30))],
            ColumnTitle = Header("column-title", new SceneBounds(-20, 0, 20, 12)),
            ColumnTitleOffset = 10,
            ColumnTitleAtEnd = true,
        };

        // Act
        var result = CompositionLayout.Place(Grid2X2(), headers, Settings(columns: 2));

        // Assert: the footers are at ceil(72 + 50.5) = 123 and end at 153; the title is at 153 + 10 = 163
        // Assert: the grid is 223 wide from -0.5, so its middle is round(-0.5 + 111.5) = 111
        var title = result.Items.Single(i => i.Role == "column-title");
        Assert.Equal((111.0, 163.0), At(title));
    }

    /// <summary>
    ///     A title band of 0 or 1 puts the title at the start or the end of the grid; the middle is rounded half up.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_TitleBandZeroAndOneAnchorStartAndEnd()
    {
        // Arrange: titles without headers (the margin is floor(-0.5) = -1 for rows and the same for columns)
        CompositionHeaders Titles(double band) => new()
        {
            RowTitle = Header("row-title", new SceneBounds(0, 0, 10, 10)),
            ColumnTitle = Header("column-title", new SceneBounds(0, 0, 10, 10)),
            RowTitleOffset = 10,
            ColumnTitleOffset = 10,
            RowTitleBand = band,
            ColumnTitleBand = band,
        };

        // Act
        var start = CompositionLayout.Place(Grid2X2(), Titles(0), Settings(columns: 2));
        var end = CompositionLayout.Place(Grid2X2(), Titles(1), Settings(columns: 2));

        // Assert: band 0: the row title y is round(-0.5) = 0 (half up) and the column title x round(-0.5) = 0; offsets -1 - 10 = -11
        Assert.Equal((-11.0, 0.0), At(start.Items.Single(i => i.Role == "row-title")));
        Assert.Equal((0.0, -11.0), At(start.Items.Single(i => i.Role == "column-title")));

        // Assert: band 1: y = round(-0.5 + 123) = 123 (122.5 half up), x = round(-0.5 + 223) = 223 (222.5 half up)
        Assert.Equal((-11.0, 123.0), At(end.Items.Single(i => i.Role == "row-title")));
        Assert.Equal((223.0, -11.0), At(end.Items.Single(i => i.Role == "column-title")));
    }

    /// <summary>
    ///     A title at the end of the rows goes right of the row footers.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_RowTitleAtEndUsesTheFooterEdge()
    {
        // Arrange: no footers: the margin is ceil(222.5) = 223
        var headers = new CompositionHeaders
        {
            RowTitle = Header("row-title", new SceneBounds(0, 0, 10, 10)),
            RowTitleOffset = 10,
            RowTitleAtEnd = true,
        };

        // Act
        var result = CompositionLayout.Place(Grid2X2(), headers, Settings(columns: 2));

        // Assert: 223 + 10, centered along the grid at -0.5 + 61.5 = 61
        Assert.Equal((233.0, 61.0), At(result.Items.Single(i => i.Role == "row-title")));
    }

    /// <summary>
    ///     The headers that there are more of than rows or columns are ignored, as in Vega.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_MoreHeadersThanRowsAreIgnored()
    {
        // Arrange: a single row of 2 cells and 3 row headers
        var bounds = new SceneBounds(0, 0, 100, 50);
        var headers = new CompositionHeaders
        {
            RowHeaders = [Header("row-header", new SceneBounds(-10, 0, 0, 50)), Header("row-header", new SceneBounds(-10, 0, 0, 50)), Header("row-header", new SceneBounds(-10, 0, 0, 50))],
        };

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds)], headers, Settings());

        // Assert: one row, so one header
        Assert.Single(result.Items, i => i.Role == "row-header");
    }

    /// <summary>
    ///     A header whose column has no cell at all is placed at 0.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_HeaderWithoutAnyCellToAlignToIsAtZero()
    {
        // Arrange: three columns but two cells, and three column footers
        var bounds = new SceneBounds(0, 0, 100, 50);
        var headers = new CompositionHeaders
        {
            ColumnFooters = [Header("footer", new SceneBounds(0, 0, 10, 10)), Header("footer", new SceneBounds(0, 0, 10, 10)), Header("footer", new SceneBounds(0, 0, 10, 10))],
        };

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds)], headers, Settings(columns: 3));

        // Assert: the third footer has no cell in its column or before it
        var footers = result.Items.Where(i => i.Role == "footer").Select(At).ToList();
        Assert.Equal([(0.0, 50.0), (120.0, 50.0), (0.0, 50.0)], footers);
    }

    /// <summary>
    ///     With <c>bounds: flush</c> the headers align to the group boxes, not to the content that reaches beyond them.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_FlushHeadersAlignToTheGroupBoxes()
    {
        // Arrange: two groups of 100 by 50 with content to y = 110.5, and a footer under each
        var bounds = new SceneBounds(-30.5, -0.5, 100.5, 110.5);
        var headers = new CompositionHeaders
        {
            ColumnFooters = [Header("footer", new SceneBounds(0, 0, 100, 30)), Header("footer", new SceneBounds(0, 0, 100, 30))],
        };

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds)], headers, Settings(flush: true));

        // Assert: the footers are at the bottom of the group boxes, 50, under each cell (0 and 120)
        Assert.Equal([(0.0, 50.0), (120.0, 50.0)], result.Items.Where(i => i.Role == "footer").Select(At));
    }

    /// <summary>
    ///     The bounds of the result are the union of the cells, the headers, the footers and the titles.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_ResultBoundsUnionCellsHeadersFootersTitles()
    {
        // Arrange: one cell of 100 by 50 (bounds from 0), a header left, a footer below, and both titles
        var headers = new CompositionHeaders
        {
            RowHeaders = [Header("row-header", new SceneBounds(-40, 0, 0, 50))],
            ColumnFooters = [Header("column-footer", new SceneBounds(0, 0, 100, 30))],
            RowTitle = Header("row-title", new SceneBounds(-12, -30, 2, 30)),
            ColumnTitle = Header("column-title", new SceneBounds(-20, 0, 20, 12)),
            RowTitleOffset = 10,
            ColumnTitleOffset = 10,
            ColumnTitleAtEnd = true,
        };

        // Act
        var result = CompositionLayout.Place([Cell(new SceneBounds(0, 0, 100, 50))], headers, Settings());

        // Assert: the cell is at 0; the row header is at margin 0 and spans -40..0; its edge -40 puts the row title at -50 (box -62..-48, y 25 - 30 .. 25 + 30)
        // Assert: the footer is at 50 and spans to 80; the column title at the end is at 80 + 10 = 90 and spans to 102, centered at x = 50
        Assert.Equal(new SceneBounds(-62, -5, 100, 102), result.Bounds);
    }

    /// <summary>
    ///     The items are in Vega-Lite's drawing order: titles, then headers and footers of the rows and of the columns, then the cells.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_ItemsAreInVegaZOrder()
    {
        // Arrange: every kind of header, and two cells
        var box = new SceneBounds(0, 0, 100, 50);
        var headers = new CompositionHeaders
        {
            RowHeaders = [Header("row-header", box)],
            RowFooters = [Header("row-footer", box)],
            ColumnHeaders = [Header("column-header", box)],
            ColumnFooters = [Header("column-footer", box)],
            RowTitle = Header("row-title", box),
            ColumnTitle = Header("column-title", box),
        };

        // Act
        var result = CompositionLayout.Place([Cell(box), Cell(box)], headers, Settings());

        // Assert
        Assert.Equal(
            ["row-title", "column-title", "row-header", "row-footer", "column-header", "column-footer", "cell", "cell"],
            result.Items.Select(i => i.Role));
    }

    /// <summary>
    ///     The inputs are not changed: the placed groups are copies at the new position, with everything else kept.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_DoesNotChangeTheInputGroups()
    {
        // Arrange: a group at (7, 9) with a size and a fill
        var original = new SceneGroup("cell", 7, 9) { Width = 100, Height = 50, Fill = "red" };
        var cell = new CompositionCell(original, new SceneBounds(0, 0, 100, 50));

        // Act
        var result = CompositionLayout.Place([cell, cell], null, Settings());

        // Assert: the original is untouched; the copies are at the grid positions with the rest kept
        Assert.Equal((7.0, 9.0), (original.X, original.Y));
        var second = Assert.IsType<SceneGroup>(result.Items[1]);
        Assert.Equal((120.0, 0.0, 100.0, 50.0, "red"), (second.X, second.Y, second.Width, second.Height, second.Fill));
    }

    /// <summary>
    ///     Without cells there is nothing to place: no items, empty bounds, no origins.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_NoCellsGiveEmptyBounds()
    {
        // Act
        var result = CompositionLayout.Place([], new CompositionHeaders(), Settings());

        // Assert
        Assert.Empty(result.Items);
        Assert.Empty(result.CellOrigins);
        Assert.True(result.Bounds.IsEmpty);
    }

    /// <summary>
    ///     Without cells the titles still go to the origin minus their offset (an empty grid is a point at the origin).
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_NoCellsStillPlacesTitlesAtTheOrigin()
    {
        // Arrange
        var headers = new CompositionHeaders
        {
            RowTitle = Header("row-title", new SceneBounds(0, 0, 10, 10)),
            RowTitleOffset = 10,
        };

        // Act
        var result = CompositionLayout.Place([], headers, Settings());

        // Assert: x = 0 - 10, y = round(0)
        Assert.Equal((-10.0, 0.0), At(Assert.Single(result.Items)));
        Assert.Equal(new SceneBounds(-10, 0, 0, 10), result.Bounds);
    }

    /// <summary>
    ///     A column count of 0 or less, like none, puts every cell in one row.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_ZeroColumnsIsOneRow()
    {
        // Arrange
        var bounds = new SceneBounds(0, 0, 100, 50);

        // Act
        var result = CompositionLayout.Place([Cell(bounds), Cell(bounds), Cell(bounds)], null, Settings(columns: 0));

        // Assert
        Assert.Equal([(0.0, 0.0), (120.0, 0.0), (240.0, 0.0)], result.CellOrigins);
    }

    /// <summary>
    ///     The arguments are validated.
    /// </summary>
    [Fact]
    public void CompositionLayout_Place_NullArgumentsThrow()
    {
        // Act and assert
        Assert.Throws<ArgumentNullException>(() => CompositionLayout.Place(null!, null, Settings()));
        Assert.Throws<ArgumentNullException>(() => CompositionLayout.Place([], null, null!));
    }
}
