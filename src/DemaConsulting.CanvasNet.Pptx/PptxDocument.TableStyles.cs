using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore tbl tblstyle tblstylelst tcstyle tcbdr wholetbl bandrow firstrow insideh insidev pptx tblpr tcpr gridlines

/// <summary>
///     Implements the <see cref="PptxDocument"/> table-style resolver (Phase 2 Follow-Up: Table
///     Style/Banding Resolution): resolves a <c>&lt;a:tbl&gt;/&lt;a:tblPr&gt;</c>'s
///     <c>&lt;a:tableStyleId&gt;</c> against the optional, auxiliary <c>ppt/tableStyles.xml</c>
///     part (<see cref="TryResolveTableStyle"/>) and implements the core
///     <c>wholeTbl</c>/<c>band1H</c>/<c>band2H</c>/<c>firstRow</c>/explicit-cell-override
///     precedence cascade (<see cref="ResolveTableCellStyle"/>) consulted by
///     <see cref="ParseTableCell"/> - see <c>pptx-document.md</c>'s "Phase 2 Follow-Up: Table
///     Style/Banding Resolution" design section for the full algorithm, the confirmed
///     ground-truth row-parity evidence, and the explicitly deferred scope (<c>firstCol</c>/
///     <c>lastCol</c>/<c>lastRow</c>/corner cells, <c>bandCol</c>/<c>band1V</c>/<c>band2V</c>, and
///     table-style-driven font color).
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>Caches each <c>ppt/tableStyles.xml</c> part's parsed <c>styleId -&gt; &lt;a:tblStyle&gt;</c> lookup, keyed by its resolved part path, on first access.</summary>
    private readonly Dictionary<string, IReadOnlyDictionary<string, XElement>> _tableStylesCache;

    /// <summary>
    ///     Resolves the <c>&lt;a:tblStyle&gt;</c> element matching <paramref name="styleId"/>
    ///     within this package's <c>ppt/tableStyles.xml</c> part (located via the presentation
    ///     part's own <c>/tableStyles</c> relationship), tolerating every cause of "not found" by
    ///     returning <see langword="null"/> rather than throwing - a table-style-sheet is an
    ///     optional, auxiliary part; its absence, or an unresolvable <paramref name="styleId"/>,
    ///     must gracefully fall back to plain, style-less cell rendering (see
    ///     <see cref="ResolveTableCellStyle"/>'s own <c>matchedTblStyle is null</c> fallback), not
    ///     fail the whole table parse closed.
    /// </summary>
    /// <param name="styleId">The <c>&lt;a:tableStyleId&gt;</c> element's own text content (a GUID, by convention).</param>
    /// <returns>
    ///     The matched <c>&lt;a:tblStyle&gt;</c> element, or <see langword="null"/> when this
    ///     package has no <c>/tableStyles</c> relationship from its presentation part, when
    ///     <c>ppt/tableStyles.xml</c> is missing or not well-formed XML, or when no
    ///     <c>&lt;a:tblStyle&gt;</c> child declares a <c>styleId</c> attribute matching
    ///     <paramref name="styleId"/> (an ordinal, case-sensitive comparison, consistent with
    ///     every other ID-keyed cache in this unit).
    /// </returns>
    internal XElement? TryResolveTableStyle(string styleId)
    {
        var tableStylesPartPath = TryResolveRelationshipByType(_presentationPartPath, "/tableStyles");
        if (tableStylesPartPath is null)
        {
            return null;
        }

        var tableStyles = GetTableStyles(tableStylesPartPath);
        return tableStyles.TryGetValue(styleId, out var tblStyle) ? tblStyle : null;
    }

    /// <summary>
    ///     Returns the parsed <c>styleId -&gt; &lt;a:tblStyle&gt;</c> lookup for the
    ///     <c>ppt/tableStyles.xml</c>-shaped part at <paramref name="tableStylesPartPath"/>,
    ///     parsing and caching it on first access.
    /// </summary>
    /// <param name="tableStylesPartPath">The table-styles part's resolved path (for example <c>"ppt/tableStyles.xml"</c>).</param>
    /// <returns>
    ///     The parsed lookup, or an empty lookup when the part is missing, not well-formed XML, or
    ///     declares no <c>&lt;a:tblStyleLst&gt;/&lt;a:tblStyle&gt;</c> children at all - tolerated
    ///     rather than treated as malformed, mirroring <c>PptxDocument.Theme.cs</c>'s own
    ///     <c>ParseBgFillStyleList</c>/<c>ParseFillStyleList</c>/<c>ParseLnStyleList</c>
    ///     leniency precedent for optional, rarely-consulted style-sheet content.
    /// </returns>
    private IReadOnlyDictionary<string, XElement> GetTableStyles(string tableStylesPartPath)
    {
        if (_tableStylesCache.TryGetValue(tableStylesPartPath, out var cached))
        {
            return cached;
        }

        var tableStyles = new Dictionary<string, XElement>(StringComparer.Ordinal);
        try
        {
            var root = LoadPartXmlRoot(tableStylesPartPath);
            foreach (var tblStyle in root.Elements(DrawingNamespace + "tblStyle"))
            {
                var styleId = (string?)tblStyle.Attribute("styleId");
                if (styleId is not null)
                {
                    tableStyles[styleId] = tblStyle;
                }
            }
        }
        catch (InvalidDataException)
        {
            // Missing or malformed ppt/tableStyles.xml is tolerated - an empty lookup means every
            // styleId resolves to "not found", degrading to plain, style-less cell rendering.
        }

        _tableStylesCache[tableStylesPartPath] = tableStyles;
        return tableStyles;
    }

    /// <summary>
    ///     Implements the core table-cell style precedence cascade: a cell's own explicit
    ///     <c>&lt;a:tcPr&gt;</c> fill/border always wins outright; otherwise the matched
    ///     <c>&lt;a:tblStyle&gt;</c>'s <c>&lt;a:firstRow&gt;</c> tier (only for the header row, when
    ///     <paramref name="firstRowEnabled"/>) wins over its <c>&lt;a:band1H&gt;</c>/
    ///     <c>&lt;a:band2H&gt;</c> banding tier (only when <paramref name="bandRowEnabled"/>,
    ///     alternating by row parity with the header row excluded from the count), which in turn
    ///     wins over its <c>&lt;a:wholeTbl&gt;</c> base tier - the only non-null tier applied when
    ///     neither <paramref name="firstRowEnabled"/> nor <paramref name="bandRowEnabled"/> select
    ///     a higher tier for this cell.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <strong>Row-parity convention</strong> (confirmed against a real-world ground-truth
    ///         render): a styled header row (<paramref name="firstRowEnabled"/>, <c>rowIndex ==
    ///         0</c>) is excluded from band-row counting entirely, and <c>band1H</c> is applied to
    ///         the first row after the header, alternating with <c>band2H</c> thereafter
    ///         (<c>bandRowIndex = firstRowEnabled ? rowIndex - 1 : rowIndex</c>; even
    ///         <c>bandRowIndex</c> selects <c>band1H</c>, odd selects <c>band2H</c>) - the same
    ///         convention ECMA-376 itself documents for a styled, banded table.
    ///     </para>
    ///     <para>
    ///         <strong>Border edge-name mapping</strong>: each of a tier's own <c>&lt;a:tcBdr&gt;</c>
    ///         declares up to six named edges (<c>left</c>/<c>right</c>/<c>top</c>/<c>bottom</c>/
    ///         <c>insideH</c>/<c>insideV</c>). A cell edge on the table's own true outer boundary
    ///         (determined structurally from <paramref name="columnIndex"/>/<paramref name="gridSpan"/>/
    ///         <paramref name="totalColumns"/> for left/right, and <paramref name="rowIndex"/>/
    ///         <paramref name="rowSpan"/>/<paramref name="totalRows"/> for top/bottom) consults the
    ///         literal edge name; every interior edge instead consults <c>insideV</c> (left/right)
    ///         or <c>insideH</c> (top/bottom) - the ECMA-376-defined mapping, required so a style
    ///         that visually differentiates its outer frame from its interior gridlines (unlike
    ///         the one available real-file fixture, whose style uses an identical line for all six
    ///         edges) renders correctly.
    ///     </para>
    /// </remarks>
    /// <param name="tcPrElement">The cell's own (possibly <see langword="null"/>) <c>&lt;a:tcPr&gt;</c> element.</param>
    /// <param name="matchedTblStyle">
    ///     The resolved <c>&lt;a:tblStyle&gt;</c> element (see <see cref="TryResolveTableStyle"/>),
    ///     or <see langword="null"/> for "no table style available" - every tier below contributes
    ///     nothing, so the cascade degrades to exactly today's pre-existing cell-only behavior.
    /// </param>
    /// <param name="bandRowEnabled">The table's own <c>&lt;a:tblPr bandRow="1"&gt;</c> attribute.</param>
    /// <param name="firstRowEnabled">The table's own <c>&lt;a:tblPr firstRow="1"&gt;</c> attribute.</param>
    /// <param name="rowIndex">The cell's zero-based row index within the table.</param>
    /// <param name="totalRows">The table's total declared <c>&lt;a:tr&gt;</c> row count.</param>
    /// <param name="columnIndex">The cell's zero-based starting grid-column index.</param>
    /// <param name="gridSpan">The cell's own <c>gridSpan</c> (see <see cref="PptxTableCell.GridSpan"/>).</param>
    /// <param name="rowSpan">The cell's own <c>rowSpan</c> (see <see cref="PptxTableCell.RowSpan"/>).</param>
    /// <param name="totalColumns">The table's total declared <c>&lt;a:tblGrid&gt;/&lt;a:gridCol&gt;</c> column count.</param>
    /// <param name="cellWidthEmu">The cell's own (unmerged, single-column) width, in EMU - needed to position a gradient fill.</param>
    /// <param name="cellHeightEmu">The cell's own (unmerged, single-row) height, in EMU - needed to position a gradient fill.</param>
    /// <param name="theme">The resolved theme, used to resolve any <c>&lt;a:schemeClr&gt;</c> in a style tier's fill/border.</param>
    /// <param name="colorMap">The effective color map - see <see cref="ParseTable"/>'s matching parameter.</param>
    /// <returns>
    ///     The resolved fill and four border edges, in the same shape <see cref="ParseTableCell"/>
    ///     already stores on <see cref="PptxTableCell"/>.
    /// </returns>
    internal static (PptxPaint Fill, PptxLineStyle? LeftBorder, PptxLineStyle? RightBorder, PptxLineStyle? TopBorder, PptxLineStyle? BottomBorder)
        ResolveTableCellStyle(
            XElement? tcPrElement,
            XElement? matchedTblStyle,
            bool bandRowEnabled,
            bool firstRowEnabled,
            int rowIndex,
            int totalRows,
            int columnIndex,
            int gridSpan,
            int rowSpan,
            int totalColumns,
            float cellWidthEmu,
            float cellHeightEmu,
            PptxTheme theme,
            PptxColorMap? colorMap)
    {
        var isHeaderRow = firstRowEnabled && rowIndex == 0;

        var firstRowTcStyle = isHeaderRow
            ? matchedTblStyle?.Element(DrawingNamespace + "firstRow")?.Element(DrawingNamespace + "tcStyle")
            : null;

        XElement? bandTcStyle = null;
        if (bandRowEnabled && !isHeaderRow && matchedTblStyle is not null)
        {
            var bandRowIndex = firstRowEnabled ? rowIndex - 1 : rowIndex;
            var bandElementName = bandRowIndex % 2 == 0 ? "band1H" : "band2H";
            bandTcStyle = matchedTblStyle.Element(DrawingNamespace + bandElementName)?.Element(DrawingNamespace + "tcStyle");
        }

        var wholeTcStyle = matchedTblStyle?.Element(DrawingNamespace + "wholeTbl")?.Element(DrawingNamespace + "tcStyle");

        // Highest to lowest precedence - consulted in this order by both the fill and each border
        // edge's own tier walk below.
        var tiers = new[] { firstRowTcStyle, bandTcStyle, wholeTcStyle };

        var fill = ResolveTableCellFill(tcPrElement, tiers, cellWidthEmu, cellHeightEmu, theme, colorMap);

        var isFirstColumn = columnIndex == 0;
        var isLastColumn = columnIndex + gridSpan >= totalColumns;
        var isFirstPhysicalRow = rowIndex == 0;
        var isLastPhysicalRow = rowIndex + rowSpan >= totalRows;

        var leftBorder = ResolveTableCellBorderEdge(tcPrElement, "lnL", tiers, "left", "insideV", isFirstColumn, theme, colorMap);
        var rightBorder = ResolveTableCellBorderEdge(tcPrElement, "lnR", tiers, "right", "insideV", isLastColumn, theme, colorMap);
        var topBorder = ResolveTableCellBorderEdge(tcPrElement, "lnT", tiers, "top", "insideH", isFirstPhysicalRow, theme, colorMap);
        var bottomBorder = ResolveTableCellBorderEdge(tcPrElement, "lnB", tiers, "bottom", "insideH", isLastPhysicalRow, theme, colorMap);

        return (fill, leftBorder, rightBorder, topBorder, bottomBorder);
    }

    /// <summary>
    ///     Resolves a cell's fill: the cell's own explicit <c>&lt;a:tcPr&gt;</c> fill-definition
    ///     child (including an explicit <c>&lt;a:noFill/&gt;</c>) wins outright when present at
    ///     all; otherwise the first of <paramref name="tiers"/> (highest to lowest precedence)
    ///     declaring an <c>&lt;a:fill&gt;</c> child element at all is resolved via the existing
    ///     <see cref="ResolveFill"/>; when no tier declares one, the result is
    ///     <see cref="PptxNoFill.Instance"/> - today's existing fallback, unchanged.
    /// </summary>
    private static PptxPaint ResolveTableCellFill(
        XElement? tcPrElement, IReadOnlyList<XElement?> tiers, float cellWidthEmu, float cellHeightEmu,
        PptxTheme theme, PptxColorMap? colorMap)
    {
        if (tcPrElement is not null && HasExplicitFillChild(tcPrElement))
        {
            return ResolveFill(tcPrElement, theme, cellWidthEmu, cellHeightEmu, colorMap: colorMap);
        }

        foreach (var tier in tiers)
        {
            var fillElement = tier?.Element(DrawingNamespace + "fill");
            if (fillElement is not null)
            {
                return ResolveFill(fillElement, theme, cellWidthEmu, cellHeightEmu, colorMap: colorMap);
            }
        }

        return PptxNoFill.Instance;
    }

    /// <summary>
    ///     Resolves a single border edge: the cell's own explicit <c>&lt;a:tcPr&gt;</c> edge
    ///     element (<paramref name="explicitEdgeElementName"/>, for example <c>"lnL"</c>) wins
    ///     outright when present at all (including when it resolves to <see langword="null"/>, an
    ///     explicit "no border"); otherwise the first of <paramref name="tiers"/> declaring the
    ///     structurally-selected <c>&lt;a:tcBdr&gt;</c> edge name (<paramref name="outerEdgeName"/>
    ///     when <paramref name="isOuterEdge"/>, else <paramref name="interiorEdgeName"/>) is
    ///     resolved via the existing <see cref="ResolveLineStyle"/> against that edge's own
    ///     <c>&lt;a:ln&gt;</c> child; when no tier declares that edge, the result is
    ///     <see langword="null"/> - today's existing "no border" fallback, unchanged.
    /// </summary>
    private static PptxLineStyle? ResolveTableCellBorderEdge(
        XElement? tcPrElement, string explicitEdgeElementName, IReadOnlyList<XElement?> tiers,
        string outerEdgeName, string interiorEdgeName, bool isOuterEdge,
        PptxTheme theme, PptxColorMap? colorMap)
    {
        var explicitEdgeElement = tcPrElement?.Element(DrawingNamespace + explicitEdgeElementName);
        if (explicitEdgeElement is not null)
        {
            return ResolveLineStyle(explicitEdgeElement, theme, colorMap);
        }

        var edgeName = isOuterEdge ? outerEdgeName : interiorEdgeName;
        foreach (var tier in tiers)
        {
            var edgeElement = tier?.Element(DrawingNamespace + "tcBdr")?.Element(DrawingNamespace + edgeName);
            if (edgeElement is not null)
            {
                return ResolveLineStyle(edgeElement.Element(DrawingNamespace + "ln"), theme, colorMap);
            }
        }

        return null;
    }
}
