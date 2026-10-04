namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore hmerge vmerge gridspan rowspan pptx

/// <summary>
///     A parsed <c>&lt;a:tbl&gt;</c> table (Phase 1e): its column widths and ordered rows,
///     produced by <see cref="PptxDocument.ParseTable"/>.
/// </summary>
/// <param name="ColumnWidthsEmu">
///     Each <c>&lt;a:tblGrid&gt;/&lt;a:gridCol w="..."/&gt;</c> column's declared width, in EMU,
///     in document (left-to-right) order.
/// </param>
/// <param name="Rows">The table's ordered <c>&lt;a:tr&gt;</c> rows.</param>
internal sealed record PptxTable(IReadOnlyList<float> ColumnWidthsEmu, IReadOnlyList<PptxTableRow> Rows);

/// <summary>
///     A parsed <c>&lt;a:tr&gt;</c> table row: its declared height plus its ordered cells,
///     produced by <see cref="PptxDocument.ParseTable"/>.
/// </summary>
/// <param name="HeightEmu">
///     The row's declared <c>&lt;a:tr h="..."/&gt;</c> height, in EMU, used as-is (this phase does
///     not grow a row to fit overflowing cell content - see the design document's documented
///     deferral).
/// </param>
/// <param name="Cells">The row's ordered <c>&lt;a:tc&gt;</c> cells.</param>
internal sealed record PptxTableRow(float HeightEmu, IReadOnlyList<PptxTableCell> Cells);

/// <summary>
///     A parsed <c>&lt;a:tc&gt;</c> table cell: its merge span/continuation flags, resolved
///     fill/border paint, and optional text content, produced by
///     <see cref="PptxDocument.ParseTableCell"/>.
/// </summary>
/// <param name="GridSpan">
///     The cell's <c>gridSpan</c> attribute (the number of grid columns this cell spans,
///     including itself), defaulting to <c>1</c> when absent.
/// </param>
/// <param name="RowSpan">
///     The cell's <c>rowSpan</c> attribute (the number of grid rows this cell spans, including
///     itself), defaulting to <c>1</c> when absent.
/// </param>
/// <param name="HMerge">
///     Whether this cell is a horizontal-merge continuation (<c>hMerge="1"</c>) - a placeholder
///     cell beneath a preceding cell's <see cref="GridSpan"/>, not independently painted.
/// </param>
/// <param name="VMerge">
///     Whether this cell is a vertical-merge continuation (<c>vMerge="1"</c>) - a placeholder
///     cell beneath a preceding row's cell's <see cref="RowSpan"/>, not independently painted.
/// </param>
/// <param name="Fill">The cell's resolved <c>&lt;a:tcPr&gt;</c> fill (<see cref="PptxDocument.ResolveFill"/>), or <see cref="PptxNoFill.Instance"/> when absent.</param>
/// <param name="LeftBorder">The cell's resolved <c>&lt;a:tcPr&gt;/&lt;a:lnL&gt;</c> border, or <see langword="null"/> for "no border".</param>
/// <param name="RightBorder">The cell's resolved <c>&lt;a:tcPr&gt;/&lt;a:lnR&gt;</c> border, or <see langword="null"/> for "no border".</param>
/// <param name="TopBorder">The cell's resolved <c>&lt;a:tcPr&gt;/&lt;a:lnT&gt;</c> border, or <see langword="null"/> for "no border".</param>
/// <param name="BottomBorder">The cell's resolved <c>&lt;a:tcPr&gt;/&lt;a:lnB&gt;</c> border, or <see langword="null"/> for "no border".</param>
/// <param name="TextBody">The cell's parsed <c>&lt;a:txBody&gt;</c> content, or <see langword="null"/> for an empty cell (no <c>&lt;a:txBody&gt;</c> declared).</param>
internal sealed record PptxTableCell(
    int GridSpan,
    int RowSpan,
    bool HMerge,
    bool VMerge,
    PptxPaint Fill,
    PptxLineStyle? LeftBorder,
    PptxLineStyle? RightBorder,
    PptxLineStyle? TopBorder,
    PptxLineStyle? BottomBorder,
    PptxTextBody? TextBody);

/// <summary>
///     A single <see cref="PptxTableCell"/> resolved to its final, merge-aware, shape-local
///     rectangle, produced by <see cref="PptxDocument.ResolveCellRects"/>. Only cells that are
///     not themselves a merge continuation (<see cref="PptxTableCell.HMerge"/>/
///     <see cref="PptxTableCell.VMerge"/> both <see langword="false"/>) are resolved - a merge
///     continuation cell contributes no rectangle of its own (its space is already covered by the
///     governing cell's own <see cref="WidthEmu"/>/<see cref="HeightEmu"/>).
/// </summary>
/// <param name="XEmu">The cell's left edge, in the table's own shape-local coordinate space, in EMU.</param>
/// <param name="YEmu">The cell's top edge, in the table's own shape-local coordinate space, in EMU.</param>
/// <param name="WidthEmu">The cell's full merged width (the sum of <see cref="PptxTableCell.GridSpan"/> consecutive column widths), in EMU.</param>
/// <param name="HeightEmu">The cell's full merged height (the sum of <see cref="PptxTableCell.RowSpan"/> consecutive row heights), in EMU.</param>
/// <param name="Cell">The resolved cell this rectangle belongs to.</param>
internal sealed record PptxResolvedTableCell(float XEmu, float YEmu, float WidthEmu, float HeightEmu, PptxTableCell Cell);
