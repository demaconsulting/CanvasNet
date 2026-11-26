using System.Globalization;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     A cell of a table to lay out: its element (null for an anonymous cell around stray content), its computed style,
///     and the box of its content (the anonymous row of its children).
/// </summary>
/// <param name="Cell">The <c>mtd</c> element, or null for an anonymous cell.</param>
/// <param name="Style">The computed style of the cell (of its parent for an anonymous cell).</param>
/// <param name="Content">The box of the content.</param>
internal sealed record TableCellInput(MathElement? Cell, MathStyle Style, MathBox Content);

/// <summary>
///     A row of a table to lay out: its element (null for an anonymous row around stray children), its computed style,
///     and its cells in order.
/// </summary>
/// <param name="Row">The <c>mtr</c> element, or null for an anonymous row.</param>
/// <param name="Style">The computed style of the row (of the table for an anonymous row).</param>
/// <param name="Cells">The cells in order.</param>
internal sealed record TableRowInput(MathElement? Row, MathStyle Style, IReadOnlyList<TableCellInput> Cells);

/// <summary>
///     A table to lay out: its element, its computed style and its rows in order.
/// </summary>
/// <param name="Table">The <c>mtable</c> element.</param>
/// <param name="Style">The computed style of the table.</param>
/// <param name="Rows">The rows in order.</param>
internal sealed record TableInput(MathElement Table, MathStyle Style, IReadOnlyList<TableRowInput> Rows);

/// <summary>
///     The horizontal alignment of the content of a cell (<c>columnalign</c>).
/// </summary>
internal enum TableColumnAlign
{
    /// <summary>Centered (the default of MathML Core: <c>text-align: center</c>).</summary>
    Center,

    /// <summary>At the left edge of the cell content.</summary>
    Left,

    /// <summary>At the right edge of the cell content.</summary>
    Right,
}

/// <summary>
///     The vertical alignment of the content of a cell (<c>rowalign</c>).
/// </summary>
internal enum TableRowAlign
{
    /// <summary>On the baseline of the row (the default of MathML Core).</summary>
    Baseline,

    /// <summary>At the top of the cell content.</summary>
    Top,

    /// <summary>At the bottom of the cell content.</summary>
    Bottom,

    /// <summary>Centered in the cell content.</summary>
    Center,

    /// <summary>The math axis of the content on the math axis of the row.</summary>
    Axis,
}

/// <summary>
///     A line of a table (<c>columnlines</c>, <c>rowlines</c>, <c>frame</c>).
/// </summary>
internal enum TableLine
{
    /// <summary>No line.</summary>
    None,

    /// <summary>A solid line.</summary>
    Solid,

    /// <summary>A dashed line.</summary>
    Dashed,
}

/// <summary>
///     Lays out <c>mtable</c>, <c>mtr</c> and <c>mtd</c> (MathML Core 3.5): a CSS table of baseline-aligned rows,
///     centered on the math axis, with the cell spans of HTML and the MathML 3 alignment, spacing and line attributes.
/// </summary>
/// <remarks>
///     <para>
///         <b>Grid.</b> The cells are placed by the HTML algorithm for processing rows: each cell takes the first
///         column of its row that no cell of an earlier row spans down into, and covers <c>columnspan</c> columns
///         (1 to 1,000) and <c>rowspan</c> rows (0 to 65,534; 0 spans to the last row, and a span past the last row ends
///         there, as browsers end it), read with the HTML rules for parsing non-negative integers (whitespace and a plus
///         sign before the digits are allowed; characters after them are ignored, as browsers ignore them, and reported);
///         an invalid span is 1 and reported as <see cref="MathMlDiagnosticCodes.AttributeInvalid"/>. The number of grid slots (rows times columns) is checked
///         against <see cref="MathMlLimits.MaxTableCells"/> as the columns grow, before the grid is allocated, and every
///         slot is charged to the work budget.
///     </para>
///     <para>
///         <b>Sizes (MathML Core).</b> A cell is padded by 0.4em on the left and right and 0.5ex on the top and bottom
///         at its font size (the user agent stylesheet: <c>mtd { padding: 0.5ex 0.4em }</c>; an anonymous cell is not
///         padded). A column is as wide as its widest padded cell of one column; a row is as tall as the largest padded
///         ascent of its baseline-aligned cells (those spanning down from it included) plus the largest padded descent
///         of its baseline-aligned cells of one row, and at least as tall as its other padded cells. A cell that spans
///         several columns or rows and needs more room spreads the excess over them in proportion to their sizes
///         (equally when they are empty), narrower spans first, as Chrome does. The content is centered in its cell and
///         on the baseline of its first row. The table is centered on the math axis: its ascent is half its height plus
///         AxisHeight, its descent half its height minus AxisHeight.
///     </para>
///     <para>
///         <b>Boxes.</b> The box of the <c>mtable</c> holds the boxes of its rows (as wide as the cells, with the
///         baseline of the row), which hold the boxes of their cells (the whole cell rectangle, padding and spans
///         included, on the baseline of their first row), which hold the content: the boxes that
///         <c>getBoundingClientRect</c> gives the elements in a browser.
///     </para>
///     <para>
///         <b>Extensions (MathML 3, drawn as Firefox draws them).</b> <c>columnalign</c> (<c>left</c>, <c>center</c>,
///         <c>right</c>; a list over the columns on <c>mtable</c> and <c>mtr</c>, the last value repeated, one value on
///         <c>mtd</c>; the innermost wins) aligns the content in its cell; <c>rowalign</c> (<c>top</c>, <c>bottom</c>,
///         <c>center</c>, <c>baseline</c>, <c>axis</c>; a list over the rows on <c>mtable</c>, one value on <c>mtr</c>
///         and <c>mtd</c>) aligns it vertically. <c>columnspacing</c> and <c>rowspacing</c> (lists of lengths over the
///         gaps; 0.8em and 1.0ex by default) and <c>framespacing</c> (a horizontal and a vertical length; 0.4em 0.5ex
///         by default) switch the table to the spacing of MathML 3: the cells lose their inner padding, the gaps lie
///         between the cells, and with a frame the frame spacing lies between the frame and the rows; the outer padding
///         of MathML Core stays. <c>columnlines</c> and <c>rowlines</c> (<c>none</c>, <c>solid</c>, <c>dashed</c>; lists
///         over the gaps) draw a line of <see cref="LineThickness"/> in the middle of each gap, which grows by the
///         line, except across a cell that spans the gap; <c>frame</c> draws one around the table, outside the outer
///         padding. Dashes are <see cref="DashLength"/> long with gaps of the same length. An invalid value is reported
///         as <see cref="MathMlDiagnosticCodes.AttributeInvalid"/> and ignored.
///     </para>
///     <para>
///         An instance serves one call on one thread (it reports into the sink of the call).
///     </para>
/// </remarks>
internal sealed class TableLayout
{
    /// <summary>The thickness of a line of <c>columnlines</c>, <c>rowlines</c> and <c>frame</c> in CSS pixels.</summary>
    public const double LineThickness = 1;

    /// <summary>The length of a dash and of the gap after it in CSS pixels.</summary>
    public const double DashLength = 3;

    /// <summary>The largest column span (HTML).</summary>
    public const int MaxColumnSpan = 1_000;

    /// <summary>The largest row span (HTML).</summary>
    public const int MaxRowSpan = 65_534;

    /// <summary>The inline padding of a cell in em (MathML Core user agent stylesheet).</summary>
    private const double InlinePaddingEm = 0.4;

    /// <summary>The block padding of a cell in ex (MathML Core user agent stylesheet).</summary>
    private const double BlockPaddingEx = 0.5;

    /// <summary>The attributes that switch a table to the spacing of MathML 3.</summary>
    private static readonly string[] SpacingAttributes = ["columnspacing", "rowspacing", "framespacing"];

    /// <summary>The values of <c>columnalign</c>.</summary>
    private static readonly Dictionary<string, TableColumnAlign> ColumnAlignValues = new(StringComparer.Ordinal)
    {
        ["left"] = TableColumnAlign.Left,
        ["center"] = TableColumnAlign.Center,
        ["right"] = TableColumnAlign.Right,
    };

    /// <summary>The values of <c>rowalign</c>.</summary>
    private static readonly Dictionary<string, TableRowAlign> RowAlignValues = new(StringComparer.Ordinal)
    {
        ["top"] = TableRowAlign.Top,
        ["bottom"] = TableRowAlign.Bottom,
        ["center"] = TableRowAlign.Center,
        ["baseline"] = TableRowAlign.Baseline,
        ["axis"] = TableRowAlign.Axis,
    };

    /// <summary>The values of <c>columnlines</c>, <c>rowlines</c> and <c>frame</c>.</summary>
    private static readonly Dictionary<string, TableLine> LineValues = new(StringComparer.Ordinal)
    {
        ["none"] = TableLine.None,
        ["solid"] = TableLine.Solid,
        ["dashed"] = TableLine.Dashed,
    };

    /// <summary>The math font, whose constants and x-height size the table.</summary>
    private readonly MathFont _font;

    /// <summary>The sink of the call.</summary>
    private readonly MathMlReportSink _sink;

    /// <summary>The limits of the call.</summary>
    private readonly MathMlLimits _limits;

    /// <summary>
    ///     Initializes the table layout of one call.
    /// </summary>
    /// <param name="font">The math font.</param>
    /// <param name="sink">The sink of the call (invalid attribute values are reported into it).</param>
    /// <param name="limits">The limits of the call (<see cref="MathMlLimits.MaxTableCells"/>).</param>
    public TableLayout(MathFont font, MathMlReportSink sink, MathMlLimits limits)
    {
        _font = font;
        _sink = sink;
        _limits = limits;
    }

    /// <summary>
    ///     Lays out a table from the boxes of the contents of its cells.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="charge">Charges work to the budget of the call and checks its cancellation.</param>
    /// <returns>The box of the table, without its element (the layout engine completes it).</returns>
    /// <exception cref="MathMlRenderException">Thrown, located, when the grid has more slots than <see cref="MathMlLimits.MaxTableCells"/> or the work budget is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public MathBox Layout(TableInput table, Action<long, MathMlLocation> charge)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(charge);
        var rowCount = table.Rows.Count;
        if (rowCount == 0)
        {
            return new MathBox(0, 0, 0);
        }

        // The grid: the position and the spans of every cell, then the owner of every slot (every slot charged)
        var cells = PlaceCells(table, charge, out var columnCount);
        charge((long)rowCount * columnCount, table.Table.Location);
        var owners = new int[rowCount * columnCount];
        Array.Fill(owners, -1);
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            charge((long)cell.RowSpan * cell.ColumnSpan, cell.Input.Cell?.Location ?? table.Table.Location);
            for (var r = cell.Row; r < cell.Row + cell.RowSpan; r++)
            {
                for (var c = cell.Column; c < cell.Column + cell.ColumnSpan; c++)
                {
                    owners[(r * columnCount) + c] = i;
                }
            }
        }

        // The attributes of the table, then the padding and the alignment of every cell
        var attributes = ReadTableAttributes(table, rowCount, columnCount);
        var rowAttributes = table.Rows.Select(row => ReadRowAttributes(row.Row)).ToArray();
        foreach (var cell in cells)
        {
            Prepare(cell, table, attributes, rowAttributes[cell.Row], rowCount, columnCount);
        }

        // The sizes of the columns and rows, the spanning cells spreading their excess
        var widths = ColumnWidths(cells, columnCount, attributes.ColumnGaps);
        var (ascents, heights) = RowHeights(cells, rowCount, attributes.RowGaps);

        // The positions of the columns and rows inside the frame and the frame spacing
        var outerX = attributes.FrameWidth + attributes.FrameSpacingX;
        var outerY = attributes.FrameWidth + attributes.FrameSpacingY;
        var columnX = Positions(widths, attributes.ColumnGaps, outerX);
        var rowY = Positions(heights, attributes.RowGaps, outerY);
        var width = columnX[columnCount] + outerX;
        var height = rowY[rowCount] + outerY;
        var axis = _font.Constant(MathConstant.AxisHeight, table.Style.FontSize);
        var tableAscent = (height / 2) + axis;

        // The rows, their cells, and the content of each cell
        var rowBoxes = new List<PlacedBox>(rowCount);
        for (var r = 0; r < rowCount; r++)
        {
            charge(1, table.Rows[r].Row?.Location ?? table.Table.Location);
            var rowLeft = outerX;
            var rowWidth = width - (2 * outerX);
            var rowCells = new List<PlacedBox>();
            foreach (var cell in cells.Where(cell => cell.Row == r))
            {
                rowCells.Add(PlaceCell(cell, columnX, widths, rowY, heights, ascents, outerX));
            }

            var row = table.Rows[r];
            var rowBox = Complete(new MathBox(rowWidth, ascents[r], heights[r] - ascents[r], rowCells), row.Row, row.Style);
            rowBoxes.Add(new PlacedBox(rowBox, rowLeft, -tableAscent + rowY[r] + ascents[r]));
        }

        // The lines and the frame
        var lines = new List<IPaintItem>();
        var geometry = new TableGeometry(columnX, widths, rowY, heights, owners, columnCount, -tableAscent);
        DrawColumnLines(lines, geometry, attributes, table.Style);
        DrawRowLines(lines, geometry, attributes, table.Style);
        if (attributes.Frame != TableLine.None)
        {
            var top = -tableAscent;
            AddLine(lines, attributes.Frame, true, 0, top, width, table.Style);
            AddLine(lines, attributes.Frame, true, 0, top + height - LineThickness, width, table.Style);
            AddLine(lines, attributes.Frame, false, 0, top, height, table.Style);
            AddLine(lines, attributes.Frame, false, width - LineThickness, top, height, table.Style);
        }

        return new MathBox(width, tableAscent, height - tableAscent, rowBoxes, lines);
    }

    /// <summary>
    ///     Places the cells on the grid (the HTML algorithm for processing rows) and checks the number of grid slots
    ///     before the grid is allocated.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="charge">Charges work.</param>
    /// <param name="columnCount">The number of columns of the grid.</param>
    /// <returns>The placed cells in order.</returns>
    private List<PlacedCell> PlaceCells(TableInput table, Action<long, MathMlLocation> charge, out int columnCount)
    {
        var rowCount = table.Rows.Count;
        CheckSlots(rowCount, 1, table.Table.Location);
        var busyUntil = new List<int>();
        var cells = new List<PlacedCell>();
        for (var r = 0; r < rowCount; r++)
        {
            charge(1, table.Rows[r].Row?.Location ?? table.Table.Location);
            var column = 0;
            foreach (var input in table.Rows[r].Cells)
            {
                // The first column of the row that no earlier cell spans down into
                while (column < busyUntil.Count && busyUntil[column] > r)
                {
                    column++;
                }

                var (rowSpan, columnSpan) = ReadSpans(input.Cell, rowCount - r);
                var location = input.Cell?.Location ?? table.Table.Location;
                CheckSlots(rowCount, (long)column + columnSpan, location);
                charge(columnSpan, location);
                while (busyUntil.Count < column + columnSpan)
                {
                    busyUntil.Add(0);
                }

                for (var c = column; c < column + columnSpan; c++)
                {
                    busyUntil[c] = Math.Max(busyUntil[c], r + rowSpan);
                }

                cells.Add(new PlacedCell(input, r, column, rowSpan, columnSpan));
                column += columnSpan;
            }
        }

        columnCount = busyUntil.Count;
        return cells;
    }

    /// <summary>
    ///     Checks that a grid of a size has at most <see cref="MathMlLimits.MaxTableCells"/> slots.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="location">The location of the construct that grows the grid.</param>
    /// <exception cref="MathMlRenderException">Thrown when the grid is too large.</exception>
    private void CheckSlots(long rows, long columns, MathMlLocation location)
    {
        if (rows * columns > _limits.MaxTableCells)
        {
            throw new MathMlRenderException(
                MathMlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the table needs more than {_limits.MaxTableCells} grid slots, rows times columns after the spans (MathMlLimits.MaxTableCells)"),
                location,
                "simplify the table or its spans, or raise the limit in MathMlRenderOptions.Limits");
        }
    }

    /// <summary>
    ///     Reads the <c>rowspan</c> and <c>columnspan</c> of a cell (HTML: a non-negative integer; a row span of 0 spans
    ///     to the last row, a column span of 0 is invalid), clamped to the HTML maximums and to the rows left.
    /// </summary>
    /// <param name="cell">The cell element, or null for an anonymous cell.</param>
    /// <param name="rowsLeft">The number of rows from the row of the cell to the last.</param>
    /// <returns>The spans.</returns>
    private (int RowSpan, int ColumnSpan) ReadSpans(MathElement? cell, int rowsLeft)
    {
        if (cell is null)
        {
            return (1, 1);
        }

        var rowSpan = ReadSpan(cell, "rowspan", MaxRowSpan, allowZero: true);
        var columnSpan = ReadSpan(cell, "columnspan", MaxColumnSpan, allowZero: false);
        return (rowSpan == 0 ? rowsLeft : Math.Min(rowSpan, rowsLeft), columnSpan);
    }

    /// <summary>
    ///     Reads one span attribute with the HTML rules for parsing non-negative integers (leading ASCII whitespace, an
    ///     optional <c>+</c>, then ASCII digits; the characters after the digits are ignored, as browsers ignore them, and
    ///     reported), clamped to the maximum; 1 when absent or invalid (reported).
    /// </summary>
    /// <param name="cell">The cell element.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="maximum">The largest span.</param>
    /// <param name="allowZero">Whether 0 is valid.</param>
    /// <returns>The span.</returns>
    private int ReadSpan(MathElement cell, string name, int maximum, bool allowZero)
    {
        if (!cell.Attributes.TryGetValue(name, out var attribute))
        {
            return 1;
        }

        // The HTML rules for parsing non-negative integers: whitespace, an optional plus sign, then the digits
        var text = attribute.Value;
        var start = 0;
        while (start < text.Length && text[start] is ' ' or '\t' or '\n' or '\f' or '\r')
        {
            start++;
        }

        if (start < text.Length && text[start] == '+')
        {
            start++;
        }

        var end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        var digits = text[start..end].TrimStart('0');
        if (end == start || (!allowZero && digits.Length == 0))
        {
            Invalid(name, attribute, allowZero ? "a non-negative integer" : "a positive integer");
            return 1;
        }

        if (end < text.Length)
        {
            _sink.Warn(
                MathMlDiagnosticCodes.AttributeInvalid,
                string.Create(CultureInfo.InvariantCulture, $"the {name} '{attribute.Value}' has characters after its number; {text[start..end]} is used, as HTML reads it"),
                attribute.Location,
                string.Create(CultureInfo.InvariantCulture, $"write the {name} as an integer such as 2"));
        }

        // A value beyond the maximum (however many digits) is the maximum, as HTML clamps it
        var value = 0;
        if (digits.Length > 9)
        {
            value = int.MaxValue;
        }
        else if (digits.Length > 0)
        {
            value = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        }
        if (value > maximum)
        {
            _sink.Warn(
                MathMlDiagnosticCodes.AttributeInvalid,
                string.Create(CultureInfo.InvariantCulture, $"the {name} '{attribute.Value}' is larger than {maximum}; {maximum} is used, as HTML clamps it"),
                attribute.Location,
                string.Create(CultureInfo.InvariantCulture, $"use a {name} of at most {maximum}"));
            return maximum;
        }

        return value;
    }

    /// <summary>
    ///     Reads the attributes of the table: alignments, spacing, lines and frame.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="rowCount">The number of rows.</param>
    /// <param name="columnCount">The number of columns.</param>
    /// <returns>The resolved attributes.</returns>
    private TableAttributes ReadTableAttributes(TableInput table, int rowCount, int columnCount)
    {
        var element = table.Table;
        var style = table.Style;
        var em = style.FontSize;
        var ex = _font.XHeight(style.FontSize);
        var spacingMode = SpacingAttributes.Any(element.Attributes.ContainsKey);
        var frame = ReadLines(element, "frame", single: true).FirstOrDefault();
        var columnLines = ReadLines(element, "columnlines", single: false);
        var rowLines = ReadLines(element, "rowlines", single: false);
        var columnSpacing = spacingMode ? ReadLengths(element, "columnspacing", style, 0.8 * em, single: false) : [];
        var rowSpacing = spacingMode ? ReadLengths(element, "rowspacing", style, ex, single: false) : [];
        var frameSpacing = spacingMode ? ReadLengths(element, "framespacing", style, 0, single: false) : [];
        if (frameSpacing.Count > 2)
        {
            Invalid("framespacing", element.Attributes["framespacing"], "a horizontal and a vertical length such as 0.4em 0.5ex");
            frameSpacing = [];
        }

        var frameSpacingX = frameSpacing.Count > 0 ? frameSpacing[0] : InlinePaddingEm * em;
        var frameSpacingY = frameSpacing.Count > 0 ? frameSpacing[^1] : BlockPaddingEx * ex;
        var hasFrame = frame != TableLine.None;
        var columnLineList = Expand(columnLines, Math.Max(0, columnCount - 1), TableLine.None);
        var rowLineList = Expand(rowLines, Math.Max(0, rowCount - 1), TableLine.None);
        var columnGaps = Expand(columnSpacing, Math.Max(0, columnCount - 1), 0.0).Select((gap, i) => gap + (columnLineList[i] == TableLine.None ? 0 : LineThickness)).ToArray();
        var rowGaps = Expand(rowSpacing, Math.Max(0, rowCount - 1), 0.0).Select((gap, i) => gap + (rowLineList[i] == TableLine.None ? 0 : LineThickness)).ToArray();
        return new TableAttributes(
            spacingMode,
            ReadAligns(element, "columnalign", ColumnAlignValues, single: false),
            ReadAligns(element, "rowalign", RowAlignValues, single: false),
            columnGaps,
            rowGaps,
            columnLineList,
            rowLineList,
            frame,
            hasFrame ? LineThickness : 0,
            hasFrame && spacingMode ? frameSpacingX : 0,
            hasFrame && spacingMode ? frameSpacingY : 0);
    }

    /// <summary>
    ///     Reads the alignment attributes of a row.
    /// </summary>
    /// <param name="row">The row element, or null for an anonymous row.</param>
    /// <returns>The column alignments over its cells and its row alignment.</returns>
    private RowAttributes ReadRowAttributes(MathElement? row) =>
        row is null
            ? new RowAttributes([], null)
            : new RowAttributes(ReadAligns(row, "columnalign", ColumnAlignValues, single: false), ReadAligns(row, "rowalign", RowAlignValues, single: true).Select(value => (TableRowAlign?)value).FirstOrDefault());

    /// <summary>
    ///     Resolves the padding and the alignment of a cell.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="table">The table.</param>
    /// <param name="attributes">The attributes of the table.</param>
    /// <param name="row">The attributes of the first row of the cell.</param>
    /// <param name="rowCount">The number of rows.</param>
    /// <param name="columnCount">The number of columns.</param>
    private void Prepare(PlacedCell cell, TableInput table, TableAttributes attributes, RowAttributes row, int rowCount, int columnCount)
    {
        var element = cell.Input.Cell;
        if (element is not null)
        {
            // The padding of MathML Core; the spacing of MathML 3 keeps only the outer padding
            var inline = InlinePaddingEm * cell.Input.Style.FontSize;
            var block = BlockPaddingEx * _font.XHeight(cell.Input.Style.FontSize);
            var spacing = attributes.SpacingMode;
            cell.PadLeft = !spacing || cell.Column == 0 ? inline : 0;
            cell.PadRight = !spacing || cell.Column + cell.ColumnSpan == columnCount ? inline : 0;
            cell.PadTop = !spacing || cell.Row == 0 ? block : 0;
            cell.PadBottom = !spacing || cell.Row + cell.RowSpan == rowCount ? block : 0;
        }

        // The innermost alignment wins: the cell, the row, the table
        cell.ColumnAlign = (element is null ? null : ReadAligns(element, "columnalign", ColumnAlignValues, single: true).Select(value => (TableColumnAlign?)value).FirstOrDefault())
            ?? Pick(row.ColumnAligns, cell.Column)
            ?? Pick(attributes.ColumnAligns, cell.Column)
            ?? TableColumnAlign.Center;
        cell.RowAlign = (element is null ? null : ReadAligns(element, "rowalign", RowAlignValues, single: true).Select(value => (TableRowAlign?)value).FirstOrDefault())
            ?? row.RowAlign
            ?? Pick(attributes.RowAligns, cell.Row)
            ?? TableRowAlign.Baseline;

        // An axis-aligned cell puts its math axis on the math axis of the table: its baseline moves by the difference
        cell.Shift = cell.RowAlign == TableRowAlign.Axis
            ? _font.Constant(MathConstant.AxisHeight, cell.Input.Style.FontSize) - _font.Constant(MathConstant.AxisHeight, table.Style.FontSize)
            : 0;
    }

    /// <summary>
    ///     Computes the widths of the columns: the widest padded cell of one column, then the excess of each spanning
    ///     cell (narrower spans first) spread in proportion to the widths.
    /// </summary>
    /// <param name="cells">The placed cells.</param>
    /// <param name="columnCount">The number of columns.</param>
    /// <param name="gaps">The gaps between the columns.</param>
    /// <returns>The widths.</returns>
    private static double[] ColumnWidths(List<PlacedCell> cells, int columnCount, double[] gaps)
    {
        var widths = new double[columnCount];
        foreach (var cell in cells.Where(cell => cell.ColumnSpan == 1))
        {
            widths[cell.Column] = Math.Max(widths[cell.Column], cell.PaddedWidth);
        }

        foreach (var cell in cells.Where(cell => cell.ColumnSpan > 1).OrderBy(cell => cell.ColumnSpan))
        {
            Spread(widths, gaps, cell.Column, cell.ColumnSpan, cell.PaddedWidth);
        }

        return widths;
    }

    /// <summary>
    ///     Computes the heights of the rows and the ascents of their baselines.
    /// </summary>
    /// <param name="cells">The placed cells.</param>
    /// <param name="rowCount">The number of rows.</param>
    /// <param name="gaps">The gaps between the rows.</param>
    /// <returns>The ascent (from the top of the row to its baseline) and the height of every row.</returns>
    private static (double[] Ascents, double[] Heights) RowHeights(List<PlacedCell> cells, int rowCount, double[] gaps)
    {
        // The baseline-aligned cells set the ascent (spanning cells in their first row) and the descent (one-row cells)
        var ascents = new double[rowCount];
        var descents = new double[rowCount];
        foreach (var cell in cells.Where(cell => cell.OnBaseline))
        {
            ascents[cell.Row] = Math.Max(ascents[cell.Row], cell.PadTop + cell.Input.Content.Ascent - cell.Shift);
            if (cell.RowSpan == 1)
            {
                descents[cell.Row] = Math.Max(descents[cell.Row], cell.Input.Content.Descent + cell.Shift + cell.PadBottom);
            }
        }

        var heights = new double[rowCount];
        for (var r = 0; r < rowCount; r++)
        {
            heights[r] = ascents[r] + descents[r];
        }

        foreach (var cell in cells.Where(cell => !cell.OnBaseline && cell.RowSpan == 1))
        {
            heights[cell.Row] = Math.Max(heights[cell.Row], cell.PaddedHeight);
        }

        // The cells that span rows spread their excess, shorter spans first
        foreach (var cell in cells.Where(cell => cell.RowSpan > 1).OrderBy(cell => cell.RowSpan))
        {
            var needed = cell.OnBaseline
                ? Math.Max(cell.PaddedHeight, ascents[cell.Row] + cell.Input.Content.Descent + cell.Shift + cell.PadBottom)
                : cell.PaddedHeight;
            Spread(heights, gaps, cell.Row, cell.RowSpan, needed);
        }

        return (ascents, heights);
    }

    /// <summary>
    ///     Spreads the excess of a spanning cell over the sizes it spans, in proportion to them (equally when they are all
    ///     zero).
    /// </summary>
    /// <param name="sizes">The sizes of the columns or rows.</param>
    /// <param name="gaps">The gaps between them.</param>
    /// <param name="start">The first spanned index.</param>
    /// <param name="span">The number of spanned sizes.</param>
    /// <param name="needed">The size the cell needs.</param>
    private static void Spread(double[] sizes, double[] gaps, int start, int span, double needed)
    {
        var total = 0.0;
        for (var i = start; i < start + span; i++)
        {
            total += sizes[i] + (i > start ? gaps[i - 1] : 0);
        }

        var excess = needed - total;
        if (excess <= 0)
        {
            return;
        }

        var sum = 0.0;
        for (var i = start; i < start + span; i++)
        {
            sum += sizes[i];
        }

        for (var i = start; i < start + span; i++)
        {
            sizes[i] += sum > 0 ? excess * sizes[i] / sum : excess / span;
        }
    }

    /// <summary>
    ///     Computes the start positions of sizes separated by gaps, and the end of the last.
    /// </summary>
    /// <param name="sizes">The sizes.</param>
    /// <param name="gaps">The gaps between them.</param>
    /// <param name="start">The position of the first.</param>
    /// <returns>The positions; the last element is the end of the last size.</returns>
    private static double[] Positions(double[] sizes, double[] gaps, double start)
    {
        var positions = new double[sizes.Length + 1];
        positions[0] = start;
        for (var i = 0; i < sizes.Length; i++)
        {
            positions[i + 1] = positions[i] + sizes[i] + (i < gaps.Length ? gaps[i] : 0);
        }

        // The end of the last size has no gap after it
        return positions;
    }

    /// <summary>
    ///     Builds the box of a cell with its content placed, relative to its row.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="columnX">The positions of the columns.</param>
    /// <param name="widths">The widths of the columns.</param>
    /// <param name="rowY">The positions of the rows.</param>
    /// <param name="heights">The heights of the rows.</param>
    /// <param name="ascents">The ascents of the rows.</param>
    /// <param name="rowLeft">The left edge of the rows.</param>
    /// <returns>The placed box of the cell.</returns>
    private static PlacedBox PlaceCell(PlacedCell cell, double[] columnX, double[] widths, double[] rowY, double[] heights, double[] ascents, double rowLeft)
    {
        var lastColumn = cell.Column + cell.ColumnSpan - 1;
        var lastRow = cell.Row + cell.RowSpan - 1;
        var width = columnX[lastColumn] + widths[lastColumn] - columnX[cell.Column];
        var top = rowY[cell.Row];
        var bottom = rowY[lastRow] + heights[lastRow];
        var ascent = ascents[cell.Row];
        var descent = bottom - top - ascent;
        var content = cell.Input.Content;

        // Horizontally in the cell content; vertically by the alignment, relative to the baseline of the first row
        var inner = width - cell.PadLeft - cell.PadRight;
        var x = cell.ColumnAlign switch
        {
            TableColumnAlign.Left => cell.PadLeft,
            TableColumnAlign.Right => cell.PadLeft + inner - content.Width,
            _ => cell.PadLeft + ((inner - content.Width) / 2),
        };
        var innerTop = -ascent + cell.PadTop;
        var innerBottom = descent - cell.PadBottom;
        var y = cell.RowAlign switch
        {
            TableRowAlign.Top => innerTop + content.Ascent,
            TableRowAlign.Bottom => innerBottom - content.Descent,
            TableRowAlign.Center => ((innerTop + innerBottom) / 2) - ((content.Descent - content.Ascent) / 2),
            _ => cell.Shift,
        };
        var box = Complete(new MathBox(width, ascent, descent, [new PlacedBox(content, x, y)]), cell.Input.Cell, cell.Input.Style);
        return new PlacedBox(box, columnX[cell.Column] - rowLeft, 0);
    }

    /// <summary>
    ///     Draws the column lines: in the middle of each gap with a line, across every row but where a cell spans the gap.
    /// </summary>
    /// <param name="lines">The line items.</param>
    /// <param name="geometry">The geometry of the table.</param>
    /// <param name="attributes">The attributes of the table.</param>
    /// <param name="style">The style of the table (its color).</param>
    private static void DrawColumnLines(List<IPaintItem> lines, TableGeometry geometry, TableAttributes attributes, MathStyle style)
    {
        var rows = geometry.Heights.Length;
        for (var c = 0; c < attributes.ColumnLines.Length; c++)
        {
            if (attributes.ColumnLines[c] == TableLine.None)
            {
                continue;
            }

            var x = geometry.ColumnX[c] + geometry.Widths[c] + ((attributes.ColumnGaps[c] - LineThickness) / 2);
            var r = 0;
            while (r < rows)
            {
                if (geometry.SameCell(r, c, r, c + 1))
                {
                    r++;
                    continue;
                }

                // A run of rows where the line is drawn, through the gaps between them
                var start = r;
                while (r < rows && !geometry.SameCell(r, c, r, c + 1))
                {
                    r++;
                }

                var top = start == 0 ? geometry.RowY[0] : geometry.RowY[start] - ((geometry.RowY[start] - geometry.RowY[start - 1] - geometry.Heights[start - 1]) / 2);
                var bottom = r == rows ? geometry.RowY[rows - 1] + geometry.Heights[rows - 1] : geometry.RowY[r - 1] + geometry.Heights[r - 1] + ((geometry.RowY[r] - geometry.RowY[r - 1] - geometry.Heights[r - 1]) / 2);
                AddLine(lines, attributes.ColumnLines[c], false, x, geometry.Top + top, bottom - top, style);
            }
        }
    }

    /// <summary>
    ///     Draws the row lines: in the middle of each gap with a line, across every column but where a cell spans the gap.
    /// </summary>
    /// <param name="lines">The line items.</param>
    /// <param name="geometry">The geometry of the table.</param>
    /// <param name="attributes">The attributes of the table.</param>
    /// <param name="style">The style of the table (its color).</param>
    private static void DrawRowLines(List<IPaintItem> lines, TableGeometry geometry, TableAttributes attributes, MathStyle style)
    {
        var columns = geometry.Widths.Length;
        for (var r = 0; r < attributes.RowLines.Length; r++)
        {
            if (attributes.RowLines[r] == TableLine.None)
            {
                continue;
            }

            var y = geometry.RowY[r] + geometry.Heights[r] + ((attributes.RowGaps[r] - LineThickness) / 2);
            var c = 0;
            while (c < columns)
            {
                if (geometry.SameCell(r, c, r + 1, c))
                {
                    c++;
                    continue;
                }

                var start = c;
                while (c < columns && !geometry.SameCell(r, c, r + 1, c))
                {
                    c++;
                }

                var left = start == 0 ? geometry.ColumnX[0] : geometry.ColumnX[start] - ((geometry.ColumnX[start] - geometry.ColumnX[start - 1] - geometry.Widths[start - 1]) / 2);
                var right = c == columns ? geometry.ColumnX[columns - 1] + geometry.Widths[columns - 1] : geometry.ColumnX[c - 1] + geometry.Widths[c - 1] + ((geometry.ColumnX[c] - geometry.ColumnX[c - 1] - geometry.Widths[c - 1]) / 2);
                AddLine(lines, attributes.RowLines[r], true, left, geometry.Top + y, right - left, style);
            }
        }
    }

    /// <summary>
    ///     Adds a horizontal or vertical line of <see cref="LineThickness"/>, solid or in dashes.
    /// </summary>
    /// <param name="lines">The line items.</param>
    /// <param name="line">The kind of line.</param>
    /// <param name="horizontal">Whether the line is horizontal.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="length">The length.</param>
    /// <param name="style">The style of the table (its color).</param>
    private static void AddLine(List<IPaintItem> lines, TableLine line, bool horizontal, double x, double y, double length, MathStyle style)
    {
        if (length <= 0)
        {
            return;
        }

        // A solid line is one rule; a dashed line a dash every two dash lengths (a dash at least at the start)
        var step = line == TableLine.Dashed ? DashLength : length;
        for (var offset = 0.0; offset < length; offset += 2 * step)
        {
            var piece = Math.Min(step, length - offset);
            lines.Add(new RuleItem(
                horizontal ? new MathRect(x + offset, y, piece, LineThickness) : new MathRect(x, y + offset, LineThickness, piece),
                style.Color));
        }
    }

    /// <summary>
    ///     Completes the box of a row or a cell: its element and its background.
    /// </summary>
    /// <param name="box">The laid-out box.</param>
    /// <param name="element">The element, or null for an anonymous row or cell.</param>
    /// <param name="style">Its computed style.</param>
    /// <returns>The completed box.</returns>
    private static MathBox Complete(MathBox box, MathElement? element, MathStyle style)
    {
        if (element is null)
        {
            return box;
        }

        IReadOnlyList<IPaintItem> items = style.Background is { } background ? [new BackgroundItem(box.ContentRect, background)] : [];
        return new MathBox(box.Width, box.Ascent, box.Descent, box.Children, items) { Element = element };
    }

    /// <summary>
    ///     Gets the value of a list for an index, the last value repeated.
    /// </summary>
    /// <typeparam name="T">The kind of value.</typeparam>
    /// <param name="values">The list.</param>
    /// <param name="index">The index.</param>
    /// <returns>The value, or null when the list is empty.</returns>
    private static T? Pick<T>(IReadOnlyList<T> values, int index)
        where T : struct =>
        values.Count == 0 ? null : values[Math.Min(index, values.Count - 1)];

    /// <summary>
    ///     Expands a list to a count, the last value repeated (the default when the list is empty).
    /// </summary>
    /// <typeparam name="T">The kind of value.</typeparam>
    /// <param name="values">The list.</param>
    /// <param name="count">The count.</param>
    /// <param name="fallback">The value of an empty list.</param>
    /// <returns>The expanded list.</returns>
    private static T[] Expand<T>(IReadOnlyList<T> values, int count, T fallback)
    {
        var result = new T[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = values.Count == 0 ? fallback : values[Math.Min(i, values.Count - 1)];
        }

        return result;
    }

    /// <summary>
    ///     Reads an alignment attribute: one value or a whitespace-separated list of values.
    /// </summary>
    /// <typeparam name="T">The kind of alignment.</typeparam>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="values">The valid values.</param>
    /// <param name="single">Whether the attribute takes one value.</param>
    /// <returns>The values; empty when absent or invalid (reported).</returns>
    private List<T> ReadAligns<T>(MathElement element, string name, Dictionary<string, T> values, bool single)
        where T : struct =>
        ReadList(element, name, single, string.Join(", ", values.Keys), token => (values.TryGetValue(token, out var value), value));

    /// <summary>
    ///     Reads a line attribute.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="single">Whether the attribute takes one value.</param>
    /// <returns>The values; empty when absent or invalid (reported).</returns>
    private List<TableLine> ReadLines(MathElement element, string name, bool single) =>
        ReadList(element, name, single, "none, solid, dashed", token => (LineValues.TryGetValue(token, out var value), value));

    /// <summary>
    ///     Reads a spacing attribute: lengths (a percentage or a negative length is invalid).
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="style">The style of the table (the base of <c>em</c> and <c>ex</c>).</param>
    /// <param name="fallback">The default, used when the attribute is absent or invalid.</param>
    /// <param name="single">Whether the attribute takes one value.</param>
    /// <returns>The lengths in CSS pixels; the default alone when absent or invalid (reported), or empty when the default is zero.</returns>
    private List<double> ReadLengths(MathElement element, string name, MathStyle style, double fallback, bool single)
    {
        var xHeight = _font.XHeight(style.FontSize);
        var lengths = ReadList(element, name, single, "lengths such as 0.8em or 4px", token =>
            AttributeValues.TryParseLength(token, out var length) && length.Unit != MathLengthUnit.Percent && length.Value >= 0
                ? (true, length.Resolve(style.FontSize, xHeight, 0))
                : (false, 0.0));
        if (lengths.Count > 0 || fallback <= 0)
        {
            return lengths;
        }

        return [fallback];
    }

    /// <summary>
    ///     Reads a whitespace-separated list attribute; an invalid token makes the whole attribute invalid (reported).
    /// </summary>
    /// <typeparam name="T">The kind of value.</typeparam>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <param name="single">Whether the attribute takes one value.</param>
    /// <param name="expected">The valid values in words, for the report.</param>
    /// <param name="parse">Parses one token.</param>
    /// <returns>The values; empty when absent or invalid.</returns>
    private List<T> ReadList<T>(MathElement element, string name, bool single, string expected, Func<string, (bool Valid, T Value)> parse)
    {
        if (!element.Attributes.TryGetValue(name, out var attribute))
        {
            return [];
        }

        var tokens = attribute.Value.Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<T>(tokens.Length);
        foreach (var token in tokens)
        {
            var (valid, value) = parse(token);
            if (!valid)
            {
                break;
            }

            result.Add(value);
        }

        if (tokens.Length == 0 || result.Count != tokens.Length || (single && tokens.Length != 1))
        {
            Invalid(name, attribute, single ? $"one of {expected}" : $"a list of {expected}");
            return [];
        }

        return result;
    }

    /// <summary>
    ///     Reports an invalid attribute value, which is ignored.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="attribute">The attribute.</param>
    /// <param name="expected">The valid values in words.</param>
    private void Invalid(string name, MathAttribute attribute, string expected) =>
        _sink.Warn(
            MathMlDiagnosticCodes.AttributeInvalid,
            $"'{attribute.Value}' is not a valid value of the attribute '{name}'; it is ignored",
            attribute.Location,
            $"use {expected}");

    /// <summary>
    ///     A cell placed on the grid with its resolved padding and alignment.
    /// </summary>
    /// <param name="Input">The cell.</param>
    /// <param name="Row">Its first row.</param>
    /// <param name="Column">Its first column.</param>
    /// <param name="RowSpan">The number of rows it spans.</param>
    /// <param name="ColumnSpan">The number of columns it spans.</param>
    private sealed record PlacedCell(TableCellInput Input, int Row, int Column, int RowSpan, int ColumnSpan)
    {
        /// <summary>Gets or sets the left padding.</summary>
        public double PadLeft { get; set; }

        /// <summary>Gets or sets the right padding.</summary>
        public double PadRight { get; set; }

        /// <summary>Gets or sets the top padding.</summary>
        public double PadTop { get; set; }

        /// <summary>Gets or sets the bottom padding.</summary>
        public double PadBottom { get; set; }

        /// <summary>Gets or sets the horizontal alignment.</summary>
        public TableColumnAlign ColumnAlign { get; set; }

        /// <summary>Gets or sets the vertical alignment.</summary>
        public TableRowAlign RowAlign { get; set; }

        /// <summary>Gets or sets how far an axis-aligned content is moved down from the baseline of the row.</summary>
        public double Shift { get; set; }

        /// <summary>Gets a value indicating whether the cell is aligned on the baseline (or the axis) of its row.</summary>
        public bool OnBaseline => RowAlign is TableRowAlign.Baseline or TableRowAlign.Axis;

        /// <summary>Gets the width of the content with the padding.</summary>
        public double PaddedWidth => PadLeft + Input.Content.Width + PadRight;

        /// <summary>Gets the height of the content with the padding.</summary>
        public double PaddedHeight => PadTop + Input.Content.Ascent + Input.Content.Descent + PadBottom;
    }

    /// <summary>
    ///     The attributes of a table, resolved.
    /// </summary>
    /// <param name="SpacingMode">Whether the table uses the spacing of MathML 3.</param>
    /// <param name="ColumnAligns">The column alignments of <c>columnalign</c>.</param>
    /// <param name="RowAligns">The row alignments of <c>rowalign</c>.</param>
    /// <param name="ColumnGaps">The gaps between the columns, lines included.</param>
    /// <param name="RowGaps">The gaps between the rows, lines included.</param>
    /// <param name="ColumnLines">The lines between the columns.</param>
    /// <param name="RowLines">The lines between the rows.</param>
    /// <param name="Frame">The frame.</param>
    /// <param name="FrameWidth">The width of the frame line (0 without a frame).</param>
    /// <param name="FrameSpacingX">The horizontal space between the frame and the rows.</param>
    /// <param name="FrameSpacingY">The vertical space between the frame and the rows.</param>
    private sealed record TableAttributes(
        bool SpacingMode,
        List<TableColumnAlign> ColumnAligns,
        List<TableRowAlign> RowAligns,
        double[] ColumnGaps,
        double[] RowGaps,
        TableLine[] ColumnLines,
        TableLine[] RowLines,
        TableLine Frame,
        double FrameWidth,
        double FrameSpacingX,
        double FrameSpacingY);

    /// <summary>
    ///     The alignment attributes of a row.
    /// </summary>
    /// <param name="ColumnAligns">The column alignments over its cells.</param>
    /// <param name="RowAlign">Its row alignment, or null.</param>
    private sealed record RowAttributes(List<TableColumnAlign> ColumnAligns, TableRowAlign? RowAlign);

    /// <summary>
    ///     The geometry of a laid-out table, for its lines.
    /// </summary>
    /// <param name="ColumnX">The positions of the columns.</param>
    /// <param name="Widths">The widths of the columns.</param>
    /// <param name="RowY">The positions of the rows.</param>
    /// <param name="Heights">The heights of the rows.</param>
    /// <param name="Owners">The cell of every slot, row by row (-1 for an empty slot).</param>
    /// <param name="ColumnCount">The number of columns.</param>
    /// <param name="Top">The top of the table relative to its baseline.</param>
    private sealed record TableGeometry(double[] ColumnX, double[] Widths, double[] RowY, double[] Heights, int[] Owners, int ColumnCount, double Top)
    {
        /// <summary>
        ///     Decides whether two slots belong to the same cell (two empty slots do not).
        /// </summary>
        /// <param name="row1">The row of the first slot.</param>
        /// <param name="column1">The column of the first slot.</param>
        /// <param name="row2">The row of the second slot.</param>
        /// <param name="column2">The column of the second slot.</param>
        /// <returns>True when one cell covers both.</returns>
        public bool SameCell(int row1, int column1, int row2, int column2)
        {
            var first = Owners[(row1 * ColumnCount) + column1];
            return first >= 0 && first == Owners[(row2 * ColumnCount) + column2];
        }
    }
}
