using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;
using static DemaConsulting.CanvasNet.MathMl.Tests.MathMlTestHelpers;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Layout;

/// <summary>
///     Unit tests for <see cref="TableLayout"/>: the grid with the HTML spans, the padding and centering of the cells, the
///     baseline alignment of the rows, the spread of the excess of spanning cells, the centering on the math axis, the
///     MathML 3 alignment, spacing, line and frame attributes, the reports of invalid values, and the
///     <see cref="MathMlLimits.MaxTableCells"/> bound. The contents are synthetic boxes at a font size of 100 px; the font is
///     the bundled Noto Sans Math (padding 0.4em = 40 px and 0.5ex, AxisHeight 27.8 px).
/// </summary>
public class TableLayoutTests
{
    /// <summary>Opaque black.</summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>The compact style of the tests at 100 px.</summary>
    private static readonly MathStyle Style = new(100, false, 0, Black, null);

    /// <summary>The inline padding of a cell at 100 px (0.4em).</summary>
    private const double Pad = 40;

    /// <summary>The math axis at 100 px (AxisHeight 278).</summary>
    private const double Axis = 27.8;

    /// <summary>Gets the block padding of a cell at 100 px (0.5ex of the bundled font).</summary>
    private static double BlockPad => MathFont.Default.XHeight(100) / 2;

    /// <summary>
    ///     Validates the grid of MathML Core: the cells are padded by 0.4em and 0.5ex, a column is as wide as its widest
    ///     padded cell, and the content is centered in its cell.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_Grid_CellsArePaddedAndCentered()
    {
        // Act
        var box = Lay(Table(Row(Cell(10, 10, 5), Cell(30, 10, 5)), Row(Cell(20, 10, 5), Cell(20, 10, 5))));

        // Assert
        Assert.Equal(100 + 110, box.Width, 9);
        var rows = Rows(box);
        Assert.Equal([0.0, 100.0], rows[0].Children.Select(cell => System.Math.Round(cell.X, 9)));
        Assert.Equal([100.0, 110.0], rows[0].Children.Select(cell => System.Math.Round(cell.Box.Width, 9)));
        Assert.Equal(BlockPad + 15 + BlockPad, rows[0].Ascent + rows[0].Descent, 9);
        Assert.Equal(45, Content(rows[0], 0).X, 9);
        Assert.Equal(Pad, Content(rows[1], 0).X, 9);
        Assert.Equal(Pad, Content(rows[0], 1).X, 9);
        Assert.Equal(45, Content(rows[1], 1).X, 9);
    }

    /// <summary>
    ///     Validates that the table is centered on the math axis: its ascent is half its height plus AxisHeight.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_Table_IsCenteredOnTheAxis()
    {
        // Act
        var box = Lay(Table(Row(Cell(10, 10, 5)), Row(Cell(10, 30, 0))));

        // Assert
        var height = (15 + 30) + (4 * BlockPad);
        Assert.Equal((height / 2) + Axis, box.Ascent, 9);
        Assert.Equal((height / 2) - Axis, box.Descent, 9);
        Assert.Equal(-box.Ascent + BlockPad + 10, box.Children[0].Y, 9);
    }

    /// <summary>
    ///     Validates that the cells of a row share its baseline: the row ascent is the largest padded ascent and the row
    ///     descent the largest padded descent.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_Row_AlignsCellBaselines()
    {
        // Act
        var box = Lay(Table(Row(Cell(10, 30, 5), Cell(10, 10, 20))));

        // Assert
        var row = Rows(box)[0];
        Assert.Equal(BlockPad + 30, row.Ascent, 9);
        Assert.Equal(20 + BlockPad, row.Descent, 9);
        Assert.Equal(0, Content(row, 0).Y, 9);
        Assert.Equal(0, Content(row, 1).Y, 9);
        Assert.All(row.Children, cell => Assert.Equal(row.Ascent + row.Descent, cell.Box.Ascent + cell.Box.Descent, 9));
    }

    /// <summary>
    ///     Validates that a cell spanning columns or rows spreads its excess in proportion to their sizes, and that its
    ///     box covers the columns and rows it spans.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_Spans_SpreadTheirExcess()
    {
        // Act: a column span of width 300 over columns of 100 and 140; a row span of depth 400 over two rows
        var box = Lay(Table(
            Row(Cell(300, 10, 0, ("columnspan", "2")), Cell(10, 10, 400, ("rowspan", "2"))),
            Row(Cell(20, 10, 0), Cell(60, 30, 0))));

        // Assert
        var rows = Rows(box);
        var excess = (300 + 80) - (100 + 140);
        Assert.Equal(100 + (excess * 100.0 / 240), rows[1].Children[0].Box.Width, 9);
        Assert.Equal(140 + (excess * 140.0 / 240), rows[1].Children[1].Box.Width, 9);
        Assert.Equal(380, rows[0].Children[0].Box.Width, 9);
        var spanned = rows[0].Children[1].Box;
        var first = 10 + (2 * BlockPad);
        var second = 30 + (2 * BlockPad);
        var rowExcess = (410 + (2 * BlockPad)) - (first + second);
        Assert.Equal(first + (rowExcess * first / (first + second)), rows[0].Ascent + rows[0].Descent, 9);
        Assert.Equal(second + (rowExcess * second / (first + second)), rows[1].Ascent + rows[1].Descent, 9);
        Assert.Equal(410 + (2 * BlockPad), spanned.Ascent + spanned.Descent, 9);
    }

    /// <summary>
    ///     Validates that a row span of 0 spans to the last row, that a span past the last row ends there, and that the
    ///     cells of the later rows skip the spanned column.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_RowSpanZero_SpansToTheEnd()
    {
        // Act
        var box = Lay(Table(
            Row(Cell(10, 10, 0, ("rowspan", "0")), Cell(10, 10, 0, ("rowspan", "9"))),
            Row(Cell(30, 10, 0)),
            Row(Cell(50, 10, 0))));

        // Assert: the spanning cells cover the whole height; three columns, the later cells in the third
        var rows = Rows(box);
        var height = box.Ascent + box.Descent;
        Assert.Equal(rows.Sum(row => row.Ascent + row.Descent), height, 9);
        Assert.Equal(height, rows[0].Children[0].Box.Ascent + rows[0].Children[0].Box.Descent, 9);
        Assert.Equal(height, rows[0].Children[1].Box.Ascent + rows[0].Children[1].Box.Descent, 9);
        Assert.Equal(2 * 90, rows[1].Children[0].X, 9);
        Assert.Equal(2 * 90, rows[2].Children[0].X, 9);
        Assert.Equal(90 + 90 + 130, box.Width, 9);
    }

    /// <summary>
    ///     Validates <c>columnalign</c>: a list over the columns on <c>mtable</c> (the last value repeated), overridden by
    ///     <c>mtr</c> and then by <c>mtd</c>.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_ColumnAlign_LeftCenterRight()
    {
        // Act
        var box = Lay(Table(
            [("columnalign", "left right")],
            Row(Cell(200, 10, 0), Cell(200, 10, 0), Cell(200, 10, 0)),
            Row(Cell(50, 10, 0), Cell(50, 10, 0), Cell(50, 10, 0)),
            Row([("columnalign", "center")], Cell(50, 10, 0), Cell(50, 10, 0), Cell(50, 10, 0, ("columnalign", "left")))));

        // Assert
        var rows = Rows(box);
        Assert.Equal([Pad, Pad + 150, Pad + 150], Enumerable.Range(0, 3).Select(i => System.Math.Round(Content(rows[1], i).X, 9)));
        Assert.Equal([Pad + 75, Pad + 75, Pad], Enumerable.Range(0, 3).Select(i => System.Math.Round(Content(rows[2], i).X, 9)));
    }

    /// <summary>
    ///     Validates <c>rowalign</c>: top, bottom, center, baseline and axis, a list over the rows on <c>mtable</c>,
    ///     overridden by <c>mtr</c> and then by <c>mtd</c>.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_RowAlign_TopBottomCenterBaselineAxis()
    {
        // Act: a tall cell (ascent 250, descent 100) and a small one (ascent 120, descent 50) in every row
        var small = Style with { FontSize = 50 };
        var box = Lay(Table(
            [("rowalign", "top bottom center baseline axis")],
            Row(Cell(10, 250, 100), Cell(10, 120, 50)),
            Row(Cell(10, 250, 100), Cell(10, 120, 50)),
            Row(Cell(10, 250, 100), Cell(10, 120, 50)),
            Row(Cell(10, 250, 100), Cell(10, 120, 50)),
            Row(Cell(10, 250, 100), new TableCellInput(Node("mtd", []), small, new MathBox(10, 120, 50))),
            Row([("rowalign", "top")], Cell(10, 250, 100), Cell(10, 120, 50, ("rowalign", "bottom")))));

        // Assert: the space above and below the small content in its cell
        var rows = Rows(box);
        Assert.Equal(BlockPad, TopGap(rows[0], 1), 9);
        Assert.Equal(BlockPad, BottomGap(rows[1], 1), 9);
        Assert.Equal(TopGap(rows[2], 1), BottomGap(rows[2], 1), 9);
        Assert.Equal(0, Content(rows[3], 1).Y, 9);
        Assert.Equal(BlockPad + 250 - 120, TopGap(rows[3], 1), 9);
        Assert.Equal((Axis / 2) - Axis, Content(rows[4], 1).Y, 9);
        Assert.Equal(0, Content(rows[4], 0).Y, 9);
        Assert.Equal(BlockPad, TopGap(rows[5], 0), 9);
        Assert.Equal(BlockPad, BottomGap(rows[5], 1), 9);
        Assert.All(rows, row => Assert.Equal(350 + (2 * BlockPad), row.Ascent + row.Descent, 9));
    }

    /// <summary>
    ///     Validates the spacing of MathML 3: <c>columnspacing</c> and <c>rowspacing</c> are gaps between the cells, which
    ///     lose their inner padding and keep the outer padding of MathML Core; <c>framespacing</c> applies with a frame
    ///     only.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_Spacing_ReplacesPadding()
    {
        // Act
        var spaced = Lay(Table(
            [("columnspacing", "10px 30px"), ("rowspacing", "5px"), ("framespacing", "7px 9px")],
            Row(Cell(100, 50, 20), Cell(100, 50, 20), Cell(100, 50, 20)),
            Row(Cell(100, 50, 20), Cell(100, 50, 20), Cell(100, 50, 20))));
        var framed = Lay(Table(
            [("rowspacing", "0"), ("framespacing", "7px 9px"), ("frame", "solid")],
            Row(Cell(100, 50, 20))));

        // Assert
        var rows = Rows(spaced);
        Assert.Equal([0.0, 150.0, 280.0], rows[0].Children.Select(cell => System.Math.Round(cell.X, 9)));
        Assert.Equal([140.0, 100.0, 140.0], rows[0].Children.Select(cell => System.Math.Round(cell.Box.Width, 9)));
        Assert.Equal(140 + 10 + 100 + 30 + 140, spaced.Width, 9);
        Assert.Equal(BlockPad + 70, rows[0].Ascent + rows[0].Descent, 9);
        Assert.Equal(spaced.Children[0].Y + rows[0].Descent + 5 + rows[1].Ascent, spaced.Children[1].Y, 9);
        Assert.Equal(1 + 7 + Pad + 100 + Pad + 7 + 1, framed.Width, 9);
        Assert.Equal(1 + 7, framed.Children[0].X, 9);
        Assert.Equal(-framed.Ascent + 1 + 9 + BlockPad + 50, framed.Children[0].Y, 9);
    }

    /// <summary>
    ///     Validates the lines: <c>columnlines</c> and <c>rowlines</c> are rules of one pixel in the middle of their gaps,
    ///     which grow by the line, interrupted by a spanning cell; <c>frame</c> is a rule around the table; dashed lines
    ///     are dashes of three pixels.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_LinesAndFrame_AreRules()
    {
        // Act
        var box = Lay(Table(
            [("columnlines", "solid dashed"), ("rowlines", "solid"), ("frame", "solid")],
            Row(Cell(100, 50, 20, ("columnspan", "2")), Cell(100, 50, 20)),
            Row(Cell(100, 50, 20), Cell(100, 50, 20), Cell(100, 50, 20))));

        // Assert
        var rules = box.Items.OfType<RuleItem>().Select(rule => rule.Rect).ToList();
        Assert.Equal(1 + 180 + 1 + 180 + 1 + 180 + 1, box.Width, 9);
        var rowHeight = 70 + (2 * BlockPad);
        var top = -box.Ascent;

        // The solid column line only in the second row (the first spans it); the dashed one in both rows
        var solid = rules.Where(rect => Near(rect.Width, 1) && Near(rect.X, 181)).ToList();
        var single = Assert.Single(solid);
        Assert.Equal(top + 1 + rowHeight + 0.5, single.Y, 9);
        var dashes = rules.Where(rect => Near(rect.Width, 1) && Near(rect.X, 362)).ToList();
        Assert.True(dashes.Count > 10);
        Assert.All(dashes, rect => Assert.True(rect.Height <= TableLayout.DashLength + 1e-9));

        // The row line across the three columns, and the frame
        Assert.Contains(rules, rect => Near(rect.Height, 1) && Near(rect.Y, top + 1 + rowHeight) && rect.Width > 500);
        Assert.Contains(rules, rect => Near(rect.X, 0) && Near(rect.Y, top) && Near(rect.Width, box.Width) && Near(rect.Height, 1));
        Assert.Contains(rules, rect => Near(rect.X, 0) && Near(rect.Y, top) && Near(rect.Width, 1) && Near(rect.Height, box.Ascent + box.Descent));
    }

    /// <summary>
    ///     Validates the reports: invalid values of the table attributes are reported as
    ///     <see cref="MathMlDiagnosticCodes.AttributeInvalid"/> and ignored, spans are clamped or reset, and the legacy
    ///     attributes of <c>mtable</c> are reported as <see cref="MathMlDiagnosticCodes.AttributeUnsupported"/>.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_InvalidAndLegacyAttributes_AreReported()
    {
        // Arrange
        var sink = new MathMlReportSink(100, false);
        var table = Table(
            [("columnalign", "left middle"), ("rowalign", "up"), ("columnspacing", "-1em"), ("frame", "thick"), ("rowlines", "solid wavy"), ("framespacing", "1em 2em 3em")],
            Row([("rowalign", "top bottom")], Cell(10, 10, 0, ("rowspan", "x")), Cell(10, 10, 0, ("columnspan", "0")), Cell(10, 10, 0, ("columnspan", "2000"))));

        // Act
        var box = new TableLayout(MathFont.Default, sink, MathMlLimits.Default).Layout(table, (_, _) => { });
        var warnings = new List<MathMlDiagnostic>();
        sink.Deliver(warnings.Add, null);
        var legacy = new MathMlReportSink(100, false);
        LayoutText(Math("<mtable align=\"axis\" equalrows=\"true\" columnwidth=\"auto\" groupalign=\"left\"><mtr><mtd groupalign=\"left\"><mi>a</mi></mtd></mtr></mtable>"), 100, legacy);
        var unsupported = new List<MathMlUnsupportedFeature>();
        legacy.Deliver(null, unsupported.Add);

        // Assert
        Assert.Equal(10, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MathMlDiagnosticCodes.AttributeInvalid, warning.Code));
        Assert.Contains(warnings, warning => warning.Message.Contains("larger than 1000", StringComparison.Ordinal));
        Assert.Equal(3, Rows(box)[0].Children.Count);
        Assert.True(Rows(box)[0].Children[2].X > 0);
        Assert.Empty(box.Items);
        Assert.Equal(5, unsupported.Count);
        Assert.All(unsupported, report => Assert.Equal(MathMlDiagnosticCodes.AttributeUnsupported, report.Feature));
    }

    /// <summary>
    ///     Validates that a grid with more slots than <see cref="MathMlLimits.MaxTableCells"/> fails with a located limit
    ///     error before the grid is allocated: the slots are never charged.
    /// </summary>
    [Fact]
    public void TableLayout_Layout_TooManyCells_FailsBeforeAllocation()
    {
        // Arrange
        var layout = new TableLayout(MathFont.Default, new MathMlReportSink(10, false), new MathMlLimits { MaxTableCells = 100 });
        var table = Table(Row(Cell(10, 10, 0, ("columnspan", "1000"), ("rowspan", "0"))), Row(), Row());
        var charged = 0L;

        // Act
        var error = Assert.Throws<MathMlRenderException>(() => layout.Layout(table, (amount, _) => charged += amount));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, error.Code);
        Assert.Contains("MathMlLimits.MaxTableCells", error.Message, StringComparison.Ordinal);
        Assert.True(error.Location.IsKnown);
        Assert.True(charged < 100);
    }

    /// <summary>
    ///     Validates that a table without rows, or with rows without cells, has no size (no padding is added).
    /// </summary>
    [Fact]
    public void TableLayout_Layout_EmptyTable_HasNoSize()
    {
        // Act
        var empty = Lay(Table());
        var emptyRows = Lay(Table(Row(), Row()));

        // Assert
        Assert.Equal((0.0, 0.0), (empty.Width, empty.Ascent + empty.Descent));
        Assert.Equal((0.0, 0.0), (emptyRows.Width, emptyRows.Ascent + emptyRows.Descent));
    }

    /// <summary>
    ///     Lays out a table with the default limits, without charging.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <returns>The box.</returns>
    private static MathBox Lay(TableInput table) =>
        new TableLayout(MathFont.Default, new MathMlReportSink(100, false), MathMlLimits.Default).Layout(table, (_, _) => { });

    /// <summary>
    ///     Creates a table without attributes.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The table.</returns>
    private static TableInput Table(params TableRowInput[] rows) => Table([], rows);

    /// <summary>
    ///     Creates a table.
    /// </summary>
    /// <param name="attributes">The attributes of the <c>mtable</c>.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The table.</returns>
    private static TableInput Table((string Name, string Value)[] attributes, params TableRowInput[] rows) =>
        new(Node("mtable", [], attributes), Style, rows);

    /// <summary>
    ///     Creates a row without attributes.
    /// </summary>
    /// <param name="cells">The cells.</param>
    /// <returns>The row.</returns>
    private static TableRowInput Row(params TableCellInput[] cells) => Row([], cells);

    /// <summary>
    ///     Creates a row.
    /// </summary>
    /// <param name="attributes">The attributes of the <c>mtr</c>.</param>
    /// <param name="cells">The cells.</param>
    /// <returns>The row.</returns>
    private static TableRowInput Row((string Name, string Value)[] attributes, params TableCellInput[] cells) =>
        new(Node("mtr", [], attributes), Style, cells);

    /// <summary>
    ///     Creates a cell with a synthetic content box.
    /// </summary>
    /// <param name="width">The width of the content.</param>
    /// <param name="ascent">The ascent of the content.</param>
    /// <param name="descent">The descent of the content.</param>
    /// <param name="attributes">The attributes of the <c>mtd</c>.</param>
    /// <returns>The cell.</returns>
    private static TableCellInput Cell(double width, double ascent, double descent, params (string Name, string Value)[] attributes) =>
        new(Node("mtd", [], attributes), Style, new MathBox(width, ascent, descent));

    /// <summary>
    ///     Gets the boxes of the rows of a table.
    /// </summary>
    /// <param name="table">The box of the table.</param>
    /// <returns>The row boxes.</returns>
    private static List<MathBox> Rows(MathBox table) => table.Children.Select(child => child.Box).ToList();

    /// <summary>
    ///     Decides whether two lengths are equal within rounding.
    /// </summary>
    /// <param name="a">The first length.</param>
    /// <param name="b">The second length.</param>
    /// <returns>True when they differ by less than 1e-9.</returns>
    private static bool Near(double a, double b) => System.Math.Abs(a - b) < 1e-9;

    /// <summary>
    ///     Gets the space between the top of a cell and the top of its content.
    /// </summary>
    /// <param name="row">The box of the row.</param>
    /// <param name="index">The index of the cell in the row.</param>
    /// <returns>The space.</returns>
    private static double TopGap(MathBox row, int index)
    {
        var cell = row.Children[index].Box;
        var content = Content(row, index);
        return content.Y - content.Box.Ascent + cell.Ascent;
    }

    /// <summary>
    ///     Gets the space between the bottom of the content of a cell and the bottom of the cell.
    /// </summary>
    /// <param name="row">The box of the row.</param>
    /// <param name="index">The index of the cell in the row.</param>
    /// <returns>The space.</returns>
    private static double BottomGap(MathBox row, int index)
    {
        var cell = row.Children[index].Box;
        var content = Content(row, index);
        return cell.Descent - (content.Y + content.Box.Descent);
    }

    /// <summary>
    ///     Gets the placed content of a cell of a row.
    /// </summary>
    /// <param name="row">The box of the row.</param>
    /// <param name="index">The index of the cell in the row.</param>
    /// <returns>The placed content, relative to the cell.</returns>
    private static PlacedBox Content(MathBox row, int index) => Assert.Single(row.Children[index].Box.Children);
}
