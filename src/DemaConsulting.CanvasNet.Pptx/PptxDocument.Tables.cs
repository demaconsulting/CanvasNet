using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore tbl tblgrid gridcol hmerge vmerge gridspan rowspan tcpr lnl lnr lnt lnb pptx graphicframe
// cspell:ignore tblpr tblstyleid bandrow firstrow

/// <summary>
///     Implements the <see cref="PptxDocument"/> table resolvers and painting primitive
///     (Phase 1e): parsing a <c>&lt;p:graphicFrame&gt;</c>'s <c>&lt;a:tbl&gt;</c> into a
///     <see cref="PptxTable"/> (<see cref="ParseTable"/>, <see cref="ParseTableCell"/>),
///     resolving each cell's final, merge-aware rectangle (<see cref="ResolveCellRects"/>), and
///     painting the whole table - fill, borders, and cell text - onto a <see cref="Surface"/>
///     (<see cref="PaintTable"/>) - see <c>pptx-document.md</c>'s "Tables (Phase 1e)" design
///     section for the full merge/rect-resolution algorithm, documented deferrals (auto-sizing to
///     fit overflowing content, nested tables), and the "Phase 2 Follow-Up: Table Style/Banding
///     Resolution" section for the <c>&lt;a:tableStyleId&gt;</c>/<c>wholeTbl</c>/<c>band1H</c>/
///     <c>band2H</c>/<c>firstRow</c> cascade implemented by <c>PptxDocument.TableStyles.cs</c>'s
///     <see cref="ResolveTableCellStyle"/>.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Resolves a <c>&lt;p:graphicFrame&gt;</c>'s declared <c>&lt;a:tbl&gt;</c> table into a
    ///     <see cref="PptxTable"/>.
    /// </summary>
    /// <param name="graphicFrameElement">The <c>&lt;p:graphicFrame&gt;</c> element.</param>
    /// <param name="theme">The resolved theme, used to resolve each cell's own fill/border colors.</param>
    /// <param name="colorMap">
    ///     The effective color map consulted when a cell's own fill/border declares an
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the
    ///     default, resolving to <see cref="PptxColorMap.Default"/>). <see cref="GetSlide"/>
    ///     supplies this slide's own true effective color map (see
    ///     <see cref="ResolveEffectiveColorMap"/>) for slide-owned tables - its cache is keyed
    ///     1:1 by slide index, so there is no other consumer that could ever observe a different
    ///     value for the same cached table. <strong>Known limitation (master/layout-owned tables
    ///     only):</strong> a table placed directly on a master or layout's own shape tree (rather
    ///     than a slide's) is parsed once at master/layout <em>load</em> time and cached, shared
    ///     by every slide that uses that master/layout - because each such slide may have its own
    ///     distinct <c>&lt;p:clrMapOvr&gt;</c>, no single color map could be baked in without risk
    ///     of being wrong for some consuming slide, so <see cref="PptxDocument.GetLayout"/>/
    ///     <see cref="PptxDocument.GetMaster"/> intentionally omit this parameter and a
    ///     master/layout-owned table's own <c>bg1</c>/<c>tx1</c> scheme color always resolves
    ///     against <see cref="PptxColorMap.Default"/> regardless of any slide's
    ///     <c>&lt;p:clrMapOvr&gt;</c>. Fixing this residual case would require restructuring
    ///     table shape-tree parsing from parse-time to render-time, a materially larger,
    ///     separately-scoped change.
    /// </param>
    /// <param name="tableStyleResolver">
    ///     Lazily invoked, when <c>&lt;a:tbl&gt;/&lt;a:tblPr&gt;</c> declares a non-empty
    ///     <c>&lt;a:tableStyleId&gt;</c>, to resolve that id against <c>ppt/tableStyles.xml</c>
    ///     (see <see cref="TryResolveTableStyle"/>) - threaded the same way
    ///     <see cref="ParseShapeTree"/>'s own <c>themeResolver</c> parameter is, defaulting to
    ///     <see langword="null"/> ("no table style available") so every pre-existing call site
    ///     keeps compiling and behaving unchanged. A <see langword="null"/> result (from the
    ///     resolver itself, or from this parameter being <see langword="null"/>) means every cell
    ///     falls back to today's pre-existing cell-only fill/border resolution - see
    ///     <see cref="ResolveTableCellStyle"/>'s own <c>matchedTblStyle is null</c> fallback.
    /// </param>
    /// <returns>The resolved <see cref="PptxTable"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="graphicFrameElement"/> has no <c>&lt;a:graphic&gt;/
    ///     &lt;a:graphicData&gt;</c> child, <c>&lt;a:graphicData&gt;</c> has no <c>&lt;a:tbl&gt;</c>
    ///     child, <c>&lt;a:tbl&gt;</c> has no <c>&lt;a:tblGrid&gt;</c>, a <c>&lt;a:gridCol&gt;</c>
    ///     has a missing or non-numeric <c>w</c> attribute, or an <c>&lt;a:tr&gt;</c> has a
    ///     missing or non-numeric <c>h</c> attribute.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown (feature token <c>"pptx-graphic-frame-kind"</c>) when <c>&lt;a:graphicData&gt;</c>'s
    ///     <c>uri</c> attribute does not end in <c>"/table"</c> - a chart, SmartArt, OLE object,
    ///     or other non-table graphic-frame kind, all out of scope this phase.
    /// </exception>
    internal static PptxTable ParseTable(
        XElement graphicFrameElement, PptxTheme theme, PptxColorMap? colorMap = null,
        Func<string, XElement?>? tableStyleResolver = null)
    {
        var graphicData = graphicFrameElement.Element(DrawingNamespace + "graphic")?.Element(DrawingNamespace + "graphicData") ??
            throw new InvalidDataException("A <p:graphicFrame> element has no <a:graphic>/<a:graphicData> child.");

        var uri = (string?)graphicData.Attribute("uri") ?? string.Empty;
        if (!uri.EndsWith("/table", StringComparison.Ordinal))
        {
            throw new PptxUnsupportedFeatureException(
                "pptx-graphic-frame-kind",
                $"Graphic frame kind '{uri}' is not a table and is not supported.");
        }

        var tbl = graphicData.Element(DrawingNamespace + "tbl") ??
            throw new InvalidDataException("An <a:graphicData> table element has no <a:tbl> child.");

        var tblGrid = tbl.Element(DrawingNamespace + "tblGrid") ??
            throw new InvalidDataException("An <a:tbl> element has no <a:tblGrid> child.");

        var columnWidthsEmu = tblGrid.Elements(DrawingNamespace + "gridCol")
            .Select(gridCol => ParseRequiredFloatAttribute(gridCol, "w", "<a:gridCol>"))
            .ToList();

        // <a:tblPr>'s tableStyleId/firstRow/bandRow - parsed once per table, then threaded through
        // every cell so ParseTableCell can consult ResolveTableCellStyle's precedence cascade.
        // Absent/empty is tolerated throughout: a table with no <a:tblPr> at all, or one with no
        // <a:tableStyleId>, degrades to exactly today's pre-existing cell-only behavior.
        var tblPr = tbl.Element(DrawingNamespace + "tblPr");
        var tableStyleId = (string?)tblPr?.Element(DrawingNamespace + "tableStyleId");
        var firstRowEnabled = (bool?)tblPr?.Attribute("firstRow") ?? false;
        var bandRowEnabled = (bool?)tblPr?.Attribute("bandRow") ?? false;
        var matchedTblStyle = string.IsNullOrEmpty(tableStyleId) ? null : tableStyleResolver?.Invoke(tableStyleId);

        // Materialized eagerly (rather than the previous lazy .Select(...) chain) so each cell's
        // own rowIndex/totalRows context is known before the per-row loop runs - needed by
        // ResolveTableCellStyle's own band-row-parity and outer-edge-detection logic.
        var trElements = tbl.Elements(DrawingNamespace + "tr").ToList();
        var totalRows = trElements.Count;
        var totalColumns = columnWidthsEmu.Count;

        // Precomputed up front (rather than parsed once per row inside the loop below) so a
        // rowSpan-N cell's own dimension peek (below) can look ahead into rows not yet built.
        var rowHeightsEmu = trElements
            .Select(tr => ParseRequiredFloatAttribute(tr, "h", "<a:tr>"))
            .ToList();

        var rows = new List<PptxTableRow>(totalRows);
        for (var rowIndex = 0; rowIndex < totalRows; rowIndex++)
        {
            var tr = trElements[rowIndex];
            var heightEmu = rowHeightsEmu[rowIndex];
            var cells = new List<PptxTableCell>();
            var columnIndex = 0;
            foreach (var tc in tr.Elements(DrawingNamespace + "tc"))
            {
                // Peeked here - ahead of ParseTableCell's own authoritative parse/validation of
                // the same attributes - purely so a merged cell's gradient fill (if any) can be
                // sized against its true summed GridSpan/RowSpan dimensions instead of only its
                // first column/row's own span-1 dimensions. A non-positive peeked value safely
                // sums to zero width/height here (SumConsecutive's own clamp) - ParseTableCell
                // still performs the single authoritative validation and throws for that case.
                var peekedGridSpan = ParseOptionalIntAttribute(tc, "gridSpan") ?? 1;
                var peekedRowSpan = ParseOptionalIntAttribute(tc, "rowSpan") ?? 1;
                var cellWidthEmu = SumConsecutive(columnWidthsEmu, columnIndex, peekedGridSpan);
                var cellHeightEmu = SumConsecutive(rowHeightsEmu, rowIndex, peekedRowSpan);
                var cell = ParseTableCell(
                    tc, theme, cellWidthEmu, cellHeightEmu, colorMap,
                    matchedTblStyle, bandRowEnabled, firstRowEnabled, rowIndex, totalRows, columnIndex, totalColumns);
                cells.Add(cell);
                columnIndex++;
            }

            rows.Add(new PptxTableRow(heightEmu, cells));
        }

        return new PptxTable(columnWidthsEmu, rows);
    }

    /// <summary>
    ///     Resolves a single <c>&lt;a:tc&gt;</c> table cell into a <see cref="PptxTableCell"/>.
    /// </summary>
    /// <param name="tcElement">The <c>&lt;a:tc&gt;</c> element.</param>
    /// <param name="theme">The resolved theme, used to resolve the cell's own fill/border colors.</param>
    /// <param name="cellWidthEmu">
    ///     The cell's own merged width, in EMU - needed to position a gradient fill. Already
    ///     summed across the cell's own declared <c>gridSpan</c> consecutive columns by
    ///     <see cref="ParseTable"/>'s own dimension peek, so a merged cell's gradient is sized
    ///     against its true spanned width, not only its first column's own width.
    /// </param>
    /// <param name="cellHeightEmu">
    ///     The cell's own merged height, in EMU - needed to position a gradient fill. Already
    ///     summed across the cell's own declared <c>rowSpan</c> consecutive rows by
    ///     <see cref="ParseTable"/>'s own dimension peek, so a merged cell's gradient is sized
    ///     against its true spanned height, not only its first row's own height.
    /// </param>
    /// <param name="colorMap">The effective color map - see <see cref="ParseTable"/>'s matching parameter, including its documented limitation.</param>
    /// <param name="matchedTblStyle">
    ///     The table's own resolved <c>&lt;a:tblStyle&gt;</c> (see <see cref="ParseTable"/>'s
    ///     <c>tableStyleResolver</c> parameter and <see cref="TryResolveTableStyle"/>), or
    ///     <see langword="null"/> (the default) for "no table style available" - every call site
    ///     predating this parameter keeps compiling and behaving identically, since a
    ///     <see langword="null"/> value degrades <see cref="ResolveTableCellStyle"/>'s own cascade
    ///     to exactly this method's pre-existing cell-only fill/border resolution.
    /// </param>
    /// <param name="bandRowEnabled">The table's own <c>&lt;a:tblPr bandRow="1"&gt;</c> attribute, defaulting to <see langword="false"/>.</param>
    /// <param name="firstRowEnabled">The table's own <c>&lt;a:tblPr firstRow="1"&gt;</c> attribute, defaulting to <see langword="false"/>.</param>
    /// <param name="rowIndex">The cell's zero-based row index within the table, defaulting to <c>0</c>.</param>
    /// <param name="totalRows">The table's total declared <c>&lt;a:tr&gt;</c> row count, defaulting to <c>1</c>.</param>
    /// <param name="columnIndex">The cell's zero-based starting grid-column index, defaulting to <c>0</c>.</param>
    /// <param name="totalColumns">The table's total declared <c>&lt;a:tblGrid&gt;/&lt;a:gridCol&gt;</c> column count, defaulting to <c>1</c>.</param>
    /// <returns>The resolved <see cref="PptxTableCell"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a present <c>gridSpan</c>/<c>rowSpan</c> attribute is not a valid integer,
    ///     or is zero or negative.
    /// </exception>
    internal static PptxTableCell ParseTableCell(
        XElement tcElement, PptxTheme theme, float cellWidthEmu, float cellHeightEmu, PptxColorMap? colorMap = null,
        XElement? matchedTblStyle = null, bool bandRowEnabled = false, bool firstRowEnabled = false,
        int rowIndex = 0, int totalRows = 1, int columnIndex = 0, int totalColumns = 1)
    {
        var gridSpan = ParseOptionalIntAttribute(tcElement, "gridSpan") ?? 1;
        if (gridSpan <= 0)
        {
            throw new InvalidDataException(
                $"An <a:tc> element has a non-positive 'gridSpan' attribute value '{gridSpan}'.");
        }

        var rowSpan = ParseOptionalIntAttribute(tcElement, "rowSpan") ?? 1;
        if (rowSpan <= 0)
        {
            throw new InvalidDataException(
                $"An <a:tc> element has a non-positive 'rowSpan' attribute value '{rowSpan}'.");
        }

        var hMerge = (bool?)tcElement.Attribute("hMerge") ?? false;
        var vMerge = (bool?)tcElement.Attribute("vMerge") ?? false;

        var tcPr = tcElement.Element(DrawingNamespace + "tcPr");
        var (fill, leftBorder, rightBorder, topBorder, bottomBorder) = ResolveTableCellStyle(
            tcPr, matchedTblStyle, bandRowEnabled, firstRowEnabled,
            rowIndex, totalRows, columnIndex, gridSpan, rowSpan, totalColumns,
            cellWidthEmu, cellHeightEmu, theme, colorMap);

        var txBody = tcElement.Element(DrawingNamespace + "txBody");
        var textBody = txBody is null ? null : ParseTextBody(txBody);

        return new PptxTableCell(gridSpan, rowSpan, hMerge, vMerge, fill, leftBorder, rightBorder, topBorder, bottomBorder, textBody);
    }

    /// <summary>
    ///     Resolves every non-merge-continuation cell in <paramref name="table"/> to its final,
    ///     merge-aware, shape-local rectangle.
    /// </summary>
    /// <remarks>
    ///     A conformant producer (PowerPoint itself) always follows a <c>gridSpan="N"</c>
    ///     governing cell with exactly <c>(N-1)</c> <c>hMerge</c> continuation <c>&lt;a:tc&gt;</c>
    ///     placeholders - but the DrawingML schema does not actually guarantee this: a
    ///     <c>&lt;a:tr&gt;</c>'s <c>&lt;a:tc&gt;</c> children are declared
    ///     <c>minOccurs="0" maxOccurs="unbounded"</c> with no cross-element constraint tying their
    ///     count to a governing cell's own <c>gridSpan</c>. This method therefore defensively
    ///     compensates (rather than assumes) for a malformed row that omits some or all of a
    ///     governing cell's own placeholder continuations: it counts how many of the immediately
    ///     following cells are themselves real <c>hMerge</c> continuations, and advances past any
    ///     shortfall itself, so a later, unrelated cell in that same row still lands at its
    ///     correct absolute column instead of silently overlapping the governing cell's own
    ///     merged region. For a well-formed row (the common, real-world case), the shortfall is
    ///     always zero and this method's behavior is unchanged.
    /// </remarks>
    /// <param name="table">The parsed table.</param>
    /// <returns>
    ///     The resolved rectangles, in row-major document order. A cell with
    ///     <see cref="PptxTableCell.HMerge"/> or <see cref="PptxTableCell.VMerge"/> set is
    ///     skipped entirely - it is a merge continuation, not independently painted (the
    ///     governing cell's own <see cref="PptxTableCell.GridSpan"/>/<see cref="PptxTableCell.RowSpan"/>
    ///     already spans its full merged rectangle).
    /// </returns>
    internal static IReadOnlyList<PptxResolvedTableCell> ResolveCellRects(PptxTable table)
    {
        var results = new List<PptxResolvedTableCell>();

        var yEmu = 0f;
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var xEmu = 0f;
            var columnIndex = 0;

            // Index-based (rather than a foreach) so a governing cell can look ahead at its own
            // immediately-following siblings to count their real hMerge continuations - see this
            // method's own <remarks/> for why that lookahead is necessary.
            for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var cell = row.Cells[cellIndex];

                // Each <a:tc> entry (real or hMerge/vMerge continuation) represents exactly one
                // physical grid column, regardless of the cell's own GridSpan. xEmu therefore
                // always advances by this single column's own width per loop iteration, never by
                // the governing cell's full (GridSpan-summed) merged width, or a merged cell's
                // trailing continuation entries would double-count the columns the governing
                // cell's own merged rectangle already spans.
                var columnWidthEmu = columnIndex < table.ColumnWidthsEmu.Count ? table.ColumnWidthsEmu[columnIndex] : 0f;
                if (!cell.HMerge && !cell.VMerge)
                {
                    var cellWidthEmu = SumColumnWidths(table.ColumnWidthsEmu, columnIndex, cell.GridSpan);
                    var cellHeightEmu = SumRowHeights(table.Rows, rowIndex, cell.RowSpan);
                    results.Add(new PptxResolvedTableCell(xEmu, yEmu, cellWidthEmu, cellHeightEmu, cell));

                    // Count how many of the immediately-following cells are themselves real
                    // HMerge continuations of this governing cell (bounded by the expected count
                    // and the row's remaining length), then compensate for any shortfall - see
                    // this method's own <remarks/>.
                    var expectedContinuations = cell.GridSpan - 1;
                    var actualContinuations = 0;
                    while (actualContinuations < expectedContinuations &&
                           cellIndex + 1 + actualContinuations < row.Cells.Count &&
                           row.Cells[cellIndex + 1 + actualContinuations].HMerge)
                    {
                        actualContinuations++;
                    }

                    var missingContinuations = expectedContinuations - actualContinuations;
                    if (missingContinuations > 0)
                    {
                        xEmu += SumColumnWidths(table.ColumnWidthsEmu, columnIndex + 1, missingContinuations);
                        columnIndex += missingContinuations;
                    }
                }

                xEmu += columnWidthEmu;
                columnIndex++;
            }

            yEmu += row.HeightEmu;
        }

        return results;
    }

    /// <summary>
    ///     Paints a resolved <paramref name="table"/> onto <paramref name="surface"/>: each
    ///     surviving (non-merge-continuation) cell's fill, its four border edges, and its text
    ///     content, in that order.
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="table">The resolved table to paint.</param>
    /// <param name="theme">The resolved theme, used to lay out each cell's own text content.</param>
    /// <param name="shapeToSurfaceTransform">
    ///     The transform mapping the table's own local <c>(0,0)</c>-origin coordinate space into
    ///     surface pixel space (the owning <c>&lt;p:graphicFrame&gt;</c>'s own
    ///     <see cref="PptxShapeFrame.Transform"/>, or a further group-composed transform).
    /// </param>
    /// <param name="fontResolver">The font resolver delegate passed through to <see cref="ResolveTextLayout"/> for each cell's own text content.</param>
    /// <param name="colorMap">
    ///     The effective color map consulted when a cell's own text run/bullet color resolves an
    ///     <c>&lt;a:schemeClr val="bg1"/&gt;</c>-shaped token, or <see langword="null"/> (the
    ///     default, resolving to <see cref="PptxColorMap.Default"/>). Unlike a cell's own
    ///     fill/border colors (see <see cref="ParseTableCell"/>'s matching parameter and its
    ///     documented limitation), a cell's text is re-resolved here at paint time - not cached at
    ///     parse time - so this parameter <em>does</em> see the slide's true effective color map
    ///     when supplied by a render-time caller.
    /// </param>
    internal static void PaintTable(
        Surface surface,
        PptxTable table,
        PptxTheme theme,
        Matrix3x2 shapeToSurfaceTransform,
        Func<string, bool, bool, TrueTypeFont> fontResolver,
        PptxColorMap? colorMap = null)
    {
        foreach (var resolvedCell in ResolveCellRects(table))
        {
            var cellRectPath = Path.Rectangle(resolvedCell.XEmu, resolvedCell.YEmu, resolvedCell.WidthEmu, resolvedCell.HeightEmu)
                .Transform(shapeToSurfaceTransform);
            FillPaint(surface, cellRectPath, resolvedCell.Cell.Fill, shapeToSurfaceTransform);

            PaintCellBorder(surface, resolvedCell.Cell.LeftBorder, shapeToSurfaceTransform,
                resolvedCell.XEmu, resolvedCell.YEmu, resolvedCell.XEmu, resolvedCell.YEmu + resolvedCell.HeightEmu);
            PaintCellBorder(surface, resolvedCell.Cell.RightBorder, shapeToSurfaceTransform,
                resolvedCell.XEmu + resolvedCell.WidthEmu, resolvedCell.YEmu, resolvedCell.XEmu + resolvedCell.WidthEmu, resolvedCell.YEmu + resolvedCell.HeightEmu);
            PaintCellBorder(surface, resolvedCell.Cell.TopBorder, shapeToSurfaceTransform,
                resolvedCell.XEmu, resolvedCell.YEmu, resolvedCell.XEmu + resolvedCell.WidthEmu, resolvedCell.YEmu);
            PaintCellBorder(surface, resolvedCell.Cell.BottomBorder, shapeToSurfaceTransform,
                resolvedCell.XEmu, resolvedCell.YEmu + resolvedCell.HeightEmu, resolvedCell.XEmu + resolvedCell.WidthEmu, resolvedCell.YEmu + resolvedCell.HeightEmu);

            if (resolvedCell.Cell.TextBody is { } textBody)
            {
                var placeholderProperties = new PptxPlaceholderProperties(null, null, theme);
                var layout = ResolveTextLayout(
                    textBody, placeholderProperties, theme, string.Empty,
                    resolvedCell.WidthEmu, resolvedCell.HeightEmu, fontResolver, colorMap);

                var cellLocalToSurface = Matrix3x2.CreateTranslation(resolvedCell.XEmu, resolvedCell.YEmu) * shapeToSurfaceTransform;
                PaintTextLayout(surface, layout, cellLocalToSurface);
            }
        }
    }

    /// <summary>
    ///     Paints a single cell border edge, when present: builds a local-space line segment from
    ///     <c>(x1Emu, y1Emu)</c> to <c>(x2Emu, y2Emu)</c>, strokes it via
    ///     <see cref="ResolveStrokeOutline"/>, transforms the stroked outline into surface space,
    ///     and fills it with the border's own resolved paint.
    /// </summary>
    private static void PaintCellBorder(
        Surface surface,
        PptxLineStyle? border,
        Matrix3x2 shapeToSurfaceTransform,
        float x1Emu, float y1Emu, float x2Emu, float y2Emu)
    {
        if (border is null)
        {
            return;
        }

        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(x1Emu, y1Emu));
        builder.LineTo(new Vector2(x2Emu, y2Emu));
        var linePath = builder.Build();

        var strokedOutline = ResolveStrokeOutline(linePath, border, shapeToSurfaceTransform).Transform(shapeToSurfaceTransform);
        FillPaint(surface, strokedOutline, border.Paint, shapeToSurfaceTransform);
    }

    /// <summary>
    ///     Fills <paramref name="path"/> onto <paramref name="surface"/> with <paramref name="paint"/>,
    ///     dispatching to the matching <see cref="PathFiller.Fill(Surface, Path, Rgba32, FillRule, float)"/>/
    ///     <see cref="PathFiller.Fill(Surface, Path, Gradient, FillRule, float)"/> overload, or
    ///     no-op for <see cref="PptxNoFill"/>.
    /// </summary>
    /// <remarks>
    ///     This overload is retained, unchanged, for every pre-existing non-table call site in
    ///     this shared partial class (<c>PptxDocument.Render.cs</c>'s own shape/background/border
    ///     fills) - it composes no transform into a <see cref="PptxGradientFill"/>'s own
    ///     <see cref="Gradient"/> before filling, which is a pre-existing, codebase-wide
    ///     correctness gap shared identically by every one of those call sites (see the 4-
    ///     parameter <see cref="FillPaint(Surface, Path, PptxPaint, Matrix3x2)"/> overload's own
    ///     <c>&lt;remarks/&gt;</c> for the full rationale and why only this file's own two table
    ///     call sites - <see cref="PaintTable"/>/<see cref="PaintCellBorder"/> - compose it
    ///     today).
    /// </remarks>
    private static void FillPaint(Surface surface, Path path, PptxPaint paint) =>
        FillPaint(surface, path, paint, Matrix3x2.Identity);

    /// <summary>
    ///     Fills <paramref name="path"/> onto <paramref name="surface"/> with <paramref name="paint"/>,
    ///     composing <paramref name="shapeToSurfaceTransform"/> into a <see cref="PptxGradientFill"/>'s
    ///     own <see cref="Gradient"/> first so its coordinate space matches <paramref name="path"/>'s own.
    /// </summary>
    /// <remarks>
    ///     <paramref name="path"/> is already transformed into surface pixel space (see
    ///     <see cref="PathFiller.Fill(Surface, Path, Gradient, FillRule, float)"/>'s own XmlDoc:
    ///     it has no <c>transform</c> parameter of its own and interprets <paramref name="path"/>
    ///     directly as surface-pixel-space coordinates). A <see cref="PptxGradientFill"/>'s own
    ///     <see cref="Gradient"/> is built in the shape's local EMU space (see
    ///     <see cref="ResolveGradientFill"/>) with its own <see cref="Gradient.Transform"/> left
    ///     at the default identity, so it must be composed with the same
    ///     <paramref name="shapeToSurfaceTransform"/> applied to <paramref name="path"/> - via
    ///     <see cref="Gradient.WithTransform"/> - before filling, or the gradient would be
    ///     evaluated against untransformed local-space coordinates while the path it fills is in
    ///     surface space, mismatching their coordinate spaces (matching the only existing
    ///     transform-composition precedent in the codebase,
    ///     <c>Rendering.Canvas.cs</c>'s own <c>paint.WithTransform(_current)</c>). Used today only
    ///     by this file's own two table call sites (<see cref="PaintTable"/>'s cell-fill call and
    ///     <see cref="PaintCellBorder"/>'s stroked-border call), both of which already have
    ///     <paramref name="shapeToSurfaceTransform"/> in scope - the identical gap in every other
    ///     shape-fill call site (<c>PptxDocument.Render.cs</c>'s <c>RenderShape</c>/
    ///     <c>RenderPicture</c>/<c>RenderConnector</c>, <c>PptxDocument.Background.cs</c>) is a
    ///     separate, pre-existing, cross-cutting issue affecting every shape kind, not unique to
    ///     tables, and is intentionally left as its own separately-scoped follow-up rather than
    ///     bundled into this table-specific fix - see the 3-parameter
    ///     <see cref="FillPaint(Surface, Path, PptxPaint)"/> overload those call sites keep using
    ///     unchanged.
    /// </remarks>
    /// <param name="surface">The surface to fill onto.</param>
    /// <param name="path">The already-surface-space-transformed path to fill.</param>
    /// <param name="paint">The resolved paint to fill with.</param>
    /// <param name="shapeToSurfaceTransform">
    ///     The same transform already applied to <paramref name="path"/> - composed into a
    ///     <see cref="PptxGradientFill"/>'s own <see cref="Gradient"/> (via
    ///     <see cref="Gradient.WithTransform"/>) before filling, so the gradient's own
    ///     coordinate space matches the path it fills. Unused for <see cref="PptxSolidFill"/>/
    ///     <see cref="PptxNoFill"/>, which have no coordinate-space-dependent state.
    /// </param>
    private static void FillPaint(Surface surface, Path path, PptxPaint paint, Matrix3x2 shapeToSurfaceTransform)
    {
        switch (paint)
        {
            case PptxSolidFill solidFill:
                PathFiller.Fill(surface, path, solidFill.Color);
                break;

            case PptxGradientFill gradientFill:
                PathFiller.Fill(surface, path, gradientFill.Gradient.WithTransform(shapeToSurfaceTransform));
                break;
        }
    }

    /// <summary>Sums <paramref name="count"/> consecutive column widths starting at <paramref name="startIndex"/>, clamped to the available column count.</summary>
    private static float SumColumnWidths(IReadOnlyList<float> columnWidthsEmu, int startIndex, int count) =>
        SumConsecutive(columnWidthsEmu, startIndex, count);

    /// <summary>
    ///     Sums <paramref name="count"/> consecutive values starting at <paramref name="startIndex"/>,
    ///     clamped to <paramref name="valuesEmu"/>'s own available element count - the shared
    ///     summation core behind <see cref="SumColumnWidths"/> and <see cref="ParseTable"/>'s own
    ///     row-height dimension peek (both need the identical sum-and-clamp behavior, once for
    ///     column widths, once for row heights).
    /// </summary>
    private static float SumConsecutive(IReadOnlyList<float> valuesEmu, int startIndex, int count)
    {
        var sum = 0f;
        var endIndex = Math.Min(startIndex + count, valuesEmu.Count);
        for (var i = startIndex; i < endIndex; i++)
        {
            sum += valuesEmu[i];
        }

        return sum;
    }

    /// <summary>Sums <paramref name="count"/> consecutive row heights starting at <paramref name="startIndex"/>, clamped to the available row count.</summary>
    private static float SumRowHeights(IReadOnlyList<PptxTableRow> rows, int startIndex, int count)
    {
        var sum = 0f;
        var endIndex = Math.Min(startIndex + count, rows.Count);
        for (var i = startIndex; i < endIndex; i++)
        {
            sum += rows[i].HeightEmu;
        }

        return sum;
    }

    /// <summary>Parses a required, numeric float attribute, throwing <see cref="InvalidDataException"/> when missing, non-numeric, or non-finite.</summary>
    private static float ParseRequiredFloatAttribute(XElement element, string attributeName, string elementDescription)
    {
        var value = (string?)element.Attribute(attributeName) ??
            throw new InvalidDataException($"A {elementDescription} element has no '{attributeName}' attribute.");

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidDataException($"A {elementDescription} element has a non-numeric '{attributeName}' attribute value '{value}'.");
        }

        if (!float.IsFinite(parsed))
        {
            throw new InvalidDataException($"A {elementDescription} element has a non-finite '{attributeName}' attribute value '{value}'.");
        }

        return parsed;
    }

    /// <summary>Parses an optional integer attribute, throwing <see cref="InvalidDataException"/> when present but non-numeric.</summary>
    private static int? ParseOptionalIntAttribute(XElement element, string attributeName)
    {
        var value = (string?)element.Attribute(attributeName);
        if (value is null)
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidDataException($"An element has a non-numeric '{attributeName}' attribute value '{value}'.");
        }

        return parsed;
    }
}
