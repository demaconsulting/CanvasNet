using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx tbl tblgrid gridcol hmerge vmerge gridspan rowspan tcpr lnl lnr lnt lnb txbody unitsperem srgb
// cspell:ignore tblpr tblstyle tblstyleid tcstyle tcbdr wholetbl bandrow firstrow insideh insidev

/// <summary>
///     Unit-level tests for the Phase 1e table resolvers and painting primitive
///     (<c>PptxDocument.Tables.cs</c>'s <see cref="PptxDocument.ParseTable"/>,
///     <see cref="PptxDocument.ParseTableCell"/>, <see cref="PptxDocument.ResolveCellRects"/>,
///     <see cref="PptxDocument.PaintTable"/>). Every resolver under test is a plain static method
///     operating on directly-constructed <see cref="XElement"/> fragments and a
///     directly-constructed <see cref="PptxTheme"/>, mirroring <see cref="PptxGeometryTests"/>/
///     <see cref="PptxPaintTests"/>'s own "no full in-memory package needed" style (table
///     structure/cells/text are fully self-contained XML, same as the Phase 1c/1d resolvers these
///     tests mirror).
/// </summary>
public class PptxTablesTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private static PptxTheme BuildTestTheme() =>
        new(
            new PptxColorScheme(
                new Rgba32(10, 10, 10, 255), new Rgba32(20, 20, 20, 255), new Rgba32(30, 30, 30, 255), new Rgba32(40, 40, 40, 255),
                new Rgba32(50, 50, 50, 255), new Rgba32(60, 60, 60, 255), new Rgba32(70, 70, 70, 255), new Rgba32(80, 80, 80, 255),
                new Rgba32(90, 90, 90, 255), new Rgba32(100, 100, 100, 255), new Rgba32(110, 110, 110, 255), new Rgba32(120, 120, 120, 255)),
            new PptxFontScheme(
                new PptxFontCollection("ThemeMajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("ThemeMinorLatin", "MinorEA", "MinorCS")));

    /// <summary>Builds a <c>&lt;p:graphicFrame&gt;</c> wrapping a table's <c>&lt;a:tbl&gt;</c> with the given column widths/row heights, tagging each cell with its own index via a <c>t</c>-attribute marker text run (for identification in assertions).</summary>
    private static XElement BuildGraphicFrame(XElement tbl, string uriSuffix = "/table") =>
        new(P + "graphicFrame",
            new XElement(A + "graphic",
                new XElement(A + "graphicData", new XAttribute("uri", $"http://schemas.openxmlformats.org/drawingml/2006{uriSuffix}"),
                    tbl)));

    private static XElement BuildTbl(IReadOnlyList<float> columnWidths, params XElement[] rows) =>
        new(A + "tbl",
            new XElement(A + "tblGrid", columnWidths.Select(w => new XElement(A + "gridCol", new XAttribute("w", w)))),
            rows);

    private static XElement BuildTr(float heightEmu, params XElement[] cells) =>
        new(A + "tr", new XAttribute("h", heightEmu), cells);

    private static XElement BuildTc(
        int? gridSpan = null, int? rowSpan = null, bool? hMerge = null, bool? vMerge = null,
        XElement? tcPr = null, XElement? txBody = null)
    {
        var tc = new XElement(A + "tc");
        if (gridSpan is not null)
        {
            tc.Add(new XAttribute("gridSpan", gridSpan.Value));
        }

        if (rowSpan is not null)
        {
            tc.Add(new XAttribute("rowSpan", rowSpan.Value));
        }

        if (hMerge is not null)
        {
            tc.Add(new XAttribute("hMerge", hMerge.Value ? "1" : "0"));
        }

        if (vMerge is not null)
        {
            tc.Add(new XAttribute("vMerge", vMerge.Value ? "1" : "0"));
        }

        if (txBody is not null)
        {
            tc.Add(txBody);
        }

        if (tcPr is not null)
        {
            tc.Add(tcPr);
        }

        return tc;
    }

    private static XElement BuildTextBody(string text) =>
        new(A + "txBody",
            new XElement(A + "bodyPr", new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0)),
            new XElement(A + "p", new XElement(A + "r", new XElement(A + "rPr", new XAttribute("sz", 100)), new XElement(A + "t", text))));

    /// <summary>Builds an <c>&lt;a:tblPr&gt;</c> element declaring the given <c>tableStyleId</c>/<c>firstRow</c>/<c>bandRow</c>.</summary>
    private static XElement BuildTblPr(string? tableStyleId = null, bool? firstRow = null, bool? bandRow = null)
    {
        var tblPr = new XElement(A + "tblPr");
        if (firstRow is not null)
        {
            tblPr.Add(new XAttribute("firstRow", firstRow.Value ? "1" : "0"));
        }

        if (bandRow is not null)
        {
            tblPr.Add(new XAttribute("bandRow", bandRow.Value ? "1" : "0"));
        }

        if (tableStyleId is not null)
        {
            tblPr.Add(new XElement(A + "tableStyleId", tableStyleId));
        }

        return tblPr;
    }

    /// <summary>Builds an <c>&lt;a:tbl&gt;</c> carrying an explicit <c>&lt;a:tblPr&gt;</c> (unlike <see cref="BuildTbl"/>, which omits one).</summary>
    private static XElement BuildTblWithPr(XElement tblPr, IReadOnlyList<float> columnWidths, params XElement[] rows) =>
        new(A + "tbl",
            tblPr,
            new XElement(A + "tblGrid", columnWidths.Select(w => new XElement(A + "gridCol", new XAttribute("w", w)))),
            rows);

    /// <summary>Builds an <c>&lt;a:fill&gt;</c> (a direct <c>&lt;a:solidFill&gt;</c>/<c>&lt;a:srgbClr&gt;</c> wrapper), matching the shape <see cref="PptxDocument.ResolveTableCellStyle"/> passes to the existing <see cref="PptxDocument.ResolveFill"/>.</summary>
    private static XElement BuildFillElement(string colorHex) =>
        new(A + "fill", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", colorHex))));

    /// <summary>Builds a single named <c>&lt;a:tcBdr&gt;</c> edge (for example <c>"left"</c>/<c>"insideV"</c>), wrapping an <c>&lt;a:ln&gt;</c> of the given width/color.</summary>
    private static XElement BuildTcBdrEdge(string edgeName, float widthEmu, string colorHex) =>
        new(A + edgeName,
            new XElement(A + "ln", new XAttribute("w", widthEmu),
                new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", colorHex)))));

    /// <summary>Builds a single <c>&lt;a:tcPr&gt;</c>-level line edge (for example <c>"lnL"</c>/<c>"lnT"</c>) - unlike <see cref="BuildTcBdrEdge"/>'s style-tier edges, this is itself the line element (no nested <c>&lt;a:ln&gt;</c>), matching <c>CT_TableCellProperties</c>'s schema.</summary>
    private static XElement BuildTcPrLineEdge(string edgeName, float widthEmu, string colorHex) =>
        new(A + edgeName, new XAttribute("w", widthEmu),
            new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", colorHex))));

    /// <summary>Builds an <c>&lt;a:tcStyle&gt;</c> (a table style tier's cell-style payload) from an optional fill and zero or more <c>&lt;a:tcBdr&gt;</c> edges.</summary>
    private static XElement BuildTcStyle(XElement? fill = null, params XElement[] tcBdrEdges)
    {
        var tcStyle = new XElement(A + "tcStyle");
        if (tcBdrEdges.Length > 0)
        {
            tcStyle.Add(new XElement(A + "tcBdr", tcBdrEdges));
        }

        if (fill is not null)
        {
            tcStyle.Add(fill);
        }

        return tcStyle;
    }

    /// <summary>Builds an <c>&lt;a:tblStyle styleId="..."&gt;</c> from its optional <c>wholeTbl</c>/<c>band1H</c>/<c>band2H</c>/<c>firstRow</c> tiers (each itself an <see cref="BuildTcStyle"/>-built <c>&lt;a:tcStyle&gt;</c>, wrapped here in its own tier element).</summary>
    private static XElement BuildTblStyle(
        string styleId, XElement? wholeTbl = null, XElement? band1H = null, XElement? band2H = null, XElement? firstRow = null)
    {
        var tblStyle = new XElement(A + "tblStyle", new XAttribute("styleId", styleId));
        if (wholeTbl is not null)
        {
            tblStyle.Add(new XElement(A + "wholeTbl", wholeTbl));
        }

        if (band1H is not null)
        {
            tblStyle.Add(new XElement(A + "band1H", band1H));
        }

        if (band2H is not null)
        {
            tblStyle.Add(new XElement(A + "band2H", band2H));
        }

        if (firstRow is not null)
        {
            tblStyle.Add(new XElement(A + "firstRow", firstRow));
        }

        return tblStyle;
    }

    // --- ParseTable ------------------------------------------------------------------------------

    /// <summary>Proves a well-formed table parses its column widths and row/cell structure.</summary>
    [Fact]
    public void ParseTable_WellFormedTable_ParsesColumnsAndRows()
    {
        var tbl = BuildTbl([1000f, 2000f],
            BuildTr(500f, BuildTc(), BuildTc()),
            BuildTr(600f, BuildTc(), BuildTc()));
        var graphicFrame = BuildGraphicFrame(tbl);

        var table = PptxDocument.ParseTable(graphicFrame, BuildTestTheme());

        Assert.Equal([1000f, 2000f], table.ColumnWidthsEmu);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(500f, table.Rows[0].HeightEmu);
        Assert.Equal(600f, table.Rows[1].HeightEmu);
        Assert.Equal(2, table.Rows[0].Cells.Count);
    }

    /// <summary>Proves a missing <c>&lt;a:graphic&gt;/&lt;a:graphicData&gt;</c> child throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ParseTable_MissingGraphicData_ThrowsInvalidDataException()
    {
        var graphicFrame = new XElement(P + "graphicFrame");

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTable(graphicFrame, BuildTestTheme()));
    }

    /// <summary>Proves a non-table graphic-frame kind (for example a chart) throws <see cref="PptxUnsupportedFeatureException"/> with feature token <c>"pptx-graphic-frame-kind"</c>.</summary>
    [Fact]
    public void ParseTable_NonTableGraphicFrameKind_ThrowsPptxUnsupportedFeatureExceptionWithGraphicFrameKindToken()
    {
        var graphicFrame = new XElement(P + "graphicFrame",
            new XElement(A + "graphic",
                new XElement(A + "graphicData", new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/chart"))));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ParseTable(graphicFrame, BuildTestTheme()));
        Assert.Equal("pptx-graphic-frame-kind", ex.Feature);
    }

    /// <summary>Proves a missing <c>&lt;a:tblGrid&gt;</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ParseTable_MissingTblGrid_ThrowsInvalidDataException()
    {
        var graphicFrame = BuildGraphicFrame(new XElement(A + "tbl"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTable(graphicFrame, BuildTestTheme()));
    }

    /// <summary>Proves a <c>&lt;a:gridCol&gt;</c> with a non-numeric <c>w</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ParseTable_GridColNonNumericWidth_ThrowsInvalidDataException()
    {
        var tbl = new XElement(A + "tbl",
            new XElement(A + "tblGrid", new XElement(A + "gridCol", new XAttribute("w", "not-a-number"))));
        var graphicFrame = BuildGraphicFrame(tbl);

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTable(graphicFrame, BuildTestTheme()));
    }

    /// <summary>Proves an <c>&lt;a:tr&gt;</c> with a missing <c>h</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ParseTable_TrMissingHeight_ThrowsInvalidDataException()
    {
        var tbl = new XElement(A + "tbl",
            new XElement(A + "tblGrid", new XElement(A + "gridCol", new XAttribute("w", 1000))),
            new XElement(A + "tr", BuildTc()));
        var graphicFrame = BuildGraphicFrame(tbl);

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTable(graphicFrame, BuildTestTheme()));
    }

    /// <summary>Proves a <c>&lt;a:gridCol&gt;</c> with a non-finite <c>w</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ParseTable_NonFiniteColumnWidth_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var tbl = new XElement(A + "tbl",
            new XElement(A + "tblGrid", new XElement(A + "gridCol", new XAttribute("w", nonFiniteValue))));
        var graphicFrame = BuildGraphicFrame(tbl);

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTable(graphicFrame, BuildTestTheme()));
    }

    /// <summary>Proves an <c>&lt;a:tr&gt;</c> with a non-finite <c>h</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ParseTable_NonFiniteRowHeight_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var tbl = new XElement(A + "tbl",
            new XElement(A + "tblGrid", new XElement(A + "gridCol", new XAttribute("w", 1000))),
            new XElement(A + "tr", new XAttribute("h", nonFiniteValue), BuildTc()));
        var graphicFrame = BuildGraphicFrame(tbl);

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTable(graphicFrame, BuildTestTheme()));
    }

    /// <summary>Builds an <c>&lt;a:tcPr&gt;</c> wrapping a linear <c>&lt;a:gradFill&gt;</c> at the given 60000ths-of-a-degree angle, matching <see cref="PptxPaintTests"/>'s own <c>ResolveGradientFill</c> fixtures.</summary>
    private static XElement BuildGradFillTcPr(int angle60000ths) =>
        new(A + "tcPr",
            new XElement(
                A + "gradFill",
                new XElement(
                    A + "gsLst",
                    new XElement(A + "gs", new XAttribute("pos", 0), new XElement(A + "srgbClr", new XAttribute("val", "FF0000"))),
                    new XElement(A + "gs", new XAttribute("pos", 100000), new XElement(A + "srgbClr", new XAttribute("val", "0000FF")))),
                new XElement(A + "lin", new XAttribute("ang", angle60000ths))));

    /// <summary>
    ///     Proves <see cref="PptxDocument.ParseTable"/> sizes a <c>gridSpan="2"</c> cell's
    ///     gradient fill against its true summed two-column width, not only its first column's
    ///     own width - <c>ParseTable</c>'s internal <c>ParseTableCell</c> call previously received
    ///     always-span-1 dimensions, so a merged cell's gradient was built too narrow.
    /// </summary>
    [Fact]
    public void ParseTable_GridSpanCellWithGradientFill_UsesMergedWidth()
    {
        // A horizontal (ang=0) gradient's End.X - Start.X span equals the cell's own widthEmu
        // (see ResolveGradientFill's own center +/- direction * extent construction).
        var mergedTbl = BuildTbl([1000f, 1500f], BuildTr(500f, BuildTc(gridSpan: 2, tcPr: BuildGradFillTcPr(0))));
        var mergedTable = PptxDocument.ParseTable(BuildGraphicFrame(mergedTbl), BuildTestTheme());
        var mergedFill = Assert.IsType<PptxGradientFill>(mergedTable.Rows[0].Cells[0].Fill);
        var mergedGradient = Assert.IsType<LinearGradient>(mergedFill.Gradient);

        var unmergedTbl = BuildTbl([1000f], BuildTr(500f, BuildTc(tcPr: BuildGradFillTcPr(0))));
        var unmergedTable = PptxDocument.ParseTable(BuildGraphicFrame(unmergedTbl), BuildTestTheme());
        var unmergedFill = Assert.IsType<PptxGradientFill>(unmergedTable.Rows[0].Cells[0].Fill);
        var unmergedGradient = Assert.IsType<LinearGradient>(unmergedFill.Gradient);

        // The merged (gridSpan=2) cell's own gradient spans the summed two-column width (2500),
        // not the single-column width (1000) an un-merge-aware sizing would have produced.
        Assert.InRange(mergedGradient.End.X - mergedGradient.Start.X, 2499f, 2501f);
        Assert.InRange(unmergedGradient.End.X - unmergedGradient.Start.X, 999f, 1001f);
    }

    /// <summary>
    ///     Proves <see cref="PptxDocument.ParseTable"/> sizes a <c>rowSpan="2"</c> cell's gradient
    ///     fill against its true summed two-row height (looked ahead via the precomputed
    ///     <c>rowHeightsEmu</c> list), not only its own declared row's height.
    /// </summary>
    [Fact]
    public void ParseTable_RowSpanCellWithGradientFill_UsesMergedHeight()
    {
        // A vertical (ang=5400000, i.e. 90 degrees) gradient's End.Y - Start.Y span equals the
        // cell's own heightEmu.
        var mergedTbl = BuildTbl(
            [1000f],
            BuildTr(400f, BuildTc(rowSpan: 2, tcPr: BuildGradFillTcPr(5400000))),
            BuildTr(700f, BuildTc(vMerge: true)));
        var mergedTable = PptxDocument.ParseTable(BuildGraphicFrame(mergedTbl), BuildTestTheme());
        var mergedFill = Assert.IsType<PptxGradientFill>(mergedTable.Rows[0].Cells[0].Fill);
        var mergedGradient = Assert.IsType<LinearGradient>(mergedFill.Gradient);

        var unmergedTbl = BuildTbl([1000f], BuildTr(400f, BuildTc(tcPr: BuildGradFillTcPr(5400000))));
        var unmergedTable = PptxDocument.ParseTable(BuildGraphicFrame(unmergedTbl), BuildTestTheme());
        var unmergedFill = Assert.IsType<PptxGradientFill>(unmergedTable.Rows[0].Cells[0].Fill);
        var unmergedGradient = Assert.IsType<LinearGradient>(unmergedFill.Gradient);

        // The merged (rowSpan=2) cell's own gradient spans the summed two-row height (1100), not
        // the single-row height (400) an un-merge-aware sizing would have produced.
        Assert.InRange(mergedGradient.End.Y - mergedGradient.Start.Y, 1099f, 1101f);
        Assert.InRange(unmergedGradient.End.Y - unmergedGradient.Start.Y, 399f, 401f);
    }

    // --- ParseTableCell --------------------------------------------------------------------------

    /// <summary>Proves an absent <c>gridSpan</c>/<c>rowSpan</c>/<c>hMerge</c>/<c>vMerge</c> defaults to span 1, no merge.</summary>
    [Fact]
    public void ParseTableCell_NoAttributes_DefaultsToSpanOneNoMerge()
    {
        var cell = PptxDocument.ParseTableCell(BuildTc(), BuildTestTheme(), 1000f, 500f);

        Assert.Equal(1, cell.GridSpan);
        Assert.Equal(1, cell.RowSpan);
        Assert.False(cell.HMerge);
        Assert.False(cell.VMerge);
        Assert.Null(cell.TextBody);
        Assert.IsType<PptxNoFill>(cell.Fill);
    }

    /// <summary>Proves present <c>gridSpan</c>/<c>rowSpan</c>/<c>hMerge</c>/<c>vMerge</c> attributes are parsed.</summary>
    [Fact]
    public void ParseTableCell_AttributesPresent_ParsesSpanAndMergeFlags()
    {
        var cell = PptxDocument.ParseTableCell(
            BuildTc(gridSpan: 2, rowSpan: 3, hMerge: true, vMerge: false), BuildTestTheme(), 1000f, 500f);

        Assert.Equal(2, cell.GridSpan);
        Assert.Equal(3, cell.RowSpan);
        Assert.True(cell.HMerge);
        Assert.False(cell.VMerge);
    }

    /// <summary>Proves a non-numeric <c>gridSpan</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ParseTableCell_NonNumericGridSpan_ThrowsInvalidDataException()
    {
        var tc = BuildTc();
        tc.Add(new XAttribute("gridSpan", "not-a-number"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTableCell(tc, BuildTestTheme(), 1000f, 500f));
    }

    /// <summary>Proves a zero or negative <c>gridSpan</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ParseTableCell_NonPositiveGridSpan_ThrowsInvalidDataException(int gridSpan)
    {
        var tc = BuildTc(gridSpan: gridSpan);

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTableCell(tc, BuildTestTheme(), 1000f, 500f));
    }

    /// <summary>Proves a zero or negative <c>rowSpan</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ParseTableCell_NonPositiveRowSpan_ThrowsInvalidDataException(int rowSpan)
    {
        var tc = BuildTc(rowSpan: rowSpan);

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseTableCell(tc, BuildTestTheme(), 1000f, 500f));
    }

    /// <summary>Proves a cell's <c>&lt;a:tcPr&gt;</c> fill and all four border edges resolve via the reused <see cref="PptxDocument.ResolveFill"/>/<see cref="PptxDocument.ResolveLineStyle"/>.</summary>
    [Fact]
    public void ParseTableCell_TcPrWithFillAndBorders_ResolvesFillAndBorders()
    {
        var tcPr = new XElement(A + "tcPr",
            new XElement(A + "lnL", new XAttribute("w", 100), new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "FF0000")))),
            new XElement(A + "lnR", new XAttribute("w", 200), new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "00FF00")))),
            new XElement(A + "lnT", new XAttribute("w", 300), new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "0000FF")))),
            new XElement(A + "lnB", new XAttribute("w", 400), new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "FFFF00")))),
            new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "808080"))));

        var cell = PptxDocument.ParseTableCell(BuildTc(tcPr: tcPr), BuildTestTheme(), 1000f, 500f);

        var fill = Assert.IsType<PptxSolidFill>(cell.Fill);
        Assert.Equal(new Rgba32(0x80, 0x80, 0x80, 255), fill.Color);
        Assert.NotNull(cell.LeftBorder);
        Assert.Equal(100f, cell.LeftBorder.WidthEmu);
        Assert.NotNull(cell.RightBorder);
        Assert.Equal(200f, cell.RightBorder.WidthEmu);
        Assert.NotNull(cell.TopBorder);
        Assert.Equal(300f, cell.TopBorder.WidthEmu);
        Assert.NotNull(cell.BottomBorder);
        Assert.Equal(400f, cell.BottomBorder.WidthEmu);
    }

    /// <summary>Proves a cell with no <c>&lt;a:txBody&gt;</c> resolves a <see langword="null"/> <see cref="PptxTableCell.TextBody"/> (an empty cell).</summary>
    [Fact]
    public void ParseTableCell_NoTxBody_TextBodyIsNull()
    {
        var cell = PptxDocument.ParseTableCell(BuildTc(), BuildTestTheme(), 1000f, 500f);

        Assert.Null(cell.TextBody);
    }

    /// <summary>Proves a cell's <c>&lt;a:txBody&gt;</c> is parsed via the reused <see cref="PptxDocument.ParseTextBody"/>.</summary>
    [Fact]
    public void ParseTableCell_WithTxBody_ParsesTextBody()
    {
        var cell = PptxDocument.ParseTableCell(BuildTc(txBody: BuildTextBody("Hello")), BuildTestTheme(), 1000f, 500f);

        Assert.NotNull(cell.TextBody);
        Assert.Single(cell.TextBody.Paragraphs);
        Assert.Equal("Hello", cell.TextBody.Paragraphs[0].Runs[0].Text);
    }

    // --- ResolveCellRects --------------------------------------------------------------------------

    /// <summary>Proves a simple (no merges) 2x2 grid computes cumulative column/row offsets.</summary>
    [Fact]
    public void ResolveCellRects_SimpleGrid_ComputesCumulativeOffsets()
    {
        var table = new PptxTable(
            [1000f, 2000f],
            [
                new PptxTableRow(500f, [new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null), new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null)]),
                new PptxTableRow(600f, [new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null), new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null)]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table);

        Assert.Equal(4, rects.Count);
        Assert.Equal((0f, 0f, 1000f, 500f), (rects[0].XEmu, rects[0].YEmu, rects[0].WidthEmu, rects[0].HeightEmu));
        Assert.Equal((1000f, 0f, 2000f, 500f), (rects[1].XEmu, rects[1].YEmu, rects[1].WidthEmu, rects[1].HeightEmu));
        Assert.Equal((0f, 500f, 1000f, 600f), (rects[2].XEmu, rects[2].YEmu, rects[2].WidthEmu, rects[2].HeightEmu));
        Assert.Equal((1000f, 500f, 2000f, 600f), (rects[3].XEmu, rects[3].YEmu, rects[3].WidthEmu, rects[3].HeightEmu));
    }

    /// <summary>Proves a merge-continuation cell (<c>HMerge</c>/<c>VMerge</c>) contributes no rectangle of its own.</summary>
    [Fact]
    public void ResolveCellRects_MergeContinuationCells_AreSkipped()
    {
        var table = new PptxTable(
            [1000f, 1000f],
            [
                new PptxTableRow(500f,
                [
                    new PptxTableCell(2, 1, false, false, PptxNoFill.Instance, null, null, null, null, null),
                    new PptxTableCell(1, 1, true, false, PptxNoFill.Instance, null, null, null, null, null),
                ]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table);

        var rect = Assert.Single(rects);
        Assert.Equal(2000f, rect.WidthEmu);
    }

    /// <summary>Proves a horizontal merge (<c>gridSpan</c> &gt; 1) computes the spanned width as the sum of the consecutive column widths.</summary>
    [Fact]
    public void ResolveCellRects_HorizontalMerge_ComputesSpannedWidth()
    {
        var table = new PptxTable(
            [1000f, 2000f, 3000f],
            [
                new PptxTableRow(500f,
                [
                    new PptxTableCell(2, 1, false, false, PptxNoFill.Instance, null, null, null, null, null),
                    new PptxTableCell(1, 1, true, false, PptxNoFill.Instance, null, null, null, null, null),
                    new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null),
                ]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table);

        Assert.Equal(2, rects.Count);
        Assert.Equal(3000f, rects[0].WidthEmu); // 1000 + 2000
        Assert.Equal(3000f, rects[1].XEmu);
        Assert.Equal(3000f, rects[1].WidthEmu);
    }

    /// <summary>Proves a vertical merge (<c>rowSpan</c> &gt; 1) computes the spanned height as the sum of the consecutive row heights.</summary>
    [Fact]
    public void ResolveCellRects_VerticalMerge_ComputesSpannedHeight()
    {
        var table = new PptxTable(
            [1000f],
            [
                new PptxTableRow(500f, [new PptxTableCell(1, 2, false, false, PptxNoFill.Instance, null, null, null, null, null)]),
                new PptxTableRow(600f, [new PptxTableCell(1, 1, false, true, PptxNoFill.Instance, null, null, null, null, null)]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table);

        var rect = Assert.Single(rects);
        Assert.Equal(1100f, rect.HeightEmu); // 500 + 600
    }

    /// <summary>
    ///     Proves <see cref="PptxDocument.ResolveCellRects"/> defensively compensates when a
    ///     <c>gridSpan="2"</c> governing cell is followed immediately by a different, independent
    ///     cell rather than its own <c>hMerge</c> continuation placeholder (the schema - see this
    ///     method's own <c>&lt;remarks/&gt;</c> - does not guarantee the placeholder's presence).
    ///     The governing cell's own rect is unaffected, and the following independent cell's
    ///     <c>XEmu</c> lands at the correct compensated column offset (column 2's own width,
    ///     3000), not overlapping the governing cell's merged region (columns 0-1, width 3000).
    /// </summary>
    [Fact]
    public void ResolveCellRects_GridSpanMissingHMergePlaceholder_CompensatesColumnAdvance()
    {
        var table = new PptxTable(
            [1000f, 2000f, 3000f],
            [
                new PptxTableRow(500f,
                [
                    new PptxTableCell(2, 1, false, false, PptxNoFill.Instance, null, null, null, null, null),
                    // No hMerge continuation placeholder for the gridSpan=2 cell's second column -
                    // a different, independent governing cell follows immediately instead.
                    new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null),
                ]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table);

        Assert.Equal(2, rects.Count);
        Assert.Equal((0f, 3000f), (rects[0].XEmu, rects[0].WidthEmu)); // columns 0-1 (1000 + 2000), unchanged
        Assert.Equal((3000f, 3000f), (rects[1].XEmu, rects[1].WidthEmu)); // column 2, compensated past the missing placeholder
    }

    // --- PaintTable --------------------------------------------------------------------------------

    private static readonly Func<string, bool, bool, TrueTypeFont> ConstantFontResolver = (_, _, _) => NewFont();

    // Synthetic font: UnitsPerEm 1000, a single 'A' glyph (index 1) whose outline is a filled
    // square spanning font-unit [100,900] x [100,900] (mirrors PptxTextRenderTests.NewFilledSquareFont).
    private static TrueTypeFont NewFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph(
            [(100, 100, true), (900, 100, true), (900, 900, true), (100, 900, true)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 1000]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>
    ///     Proves a table cell's own <c>&lt;a:fld type="slidenum"&gt;</c> field is substituted
    ///     with the actual slide number during row-height measurement (not just left at its
    ///     cached, usually-longer placeholder text) when <see cref="PptxDocument.ResolveCellRects"/>
    ///     is given a <c>slideNumber</c> - a regression test for the review finding that
    ///     <see cref="PptxDocument.SubstituteSlideNumberField"/> was wired into
    ///     <c>PptxDocument.Render.cs</c>'s <c>RenderShape</c> only, leaving a table cell's slide
    ///     number field both measuring and painting its stale <c>&#8249;#&#8250;</c> cached text.
    ///     A narrow column forces the longer cached placeholder text (three "AA" tokens, each
    ///     costing one full line at this synthetic font's own metrics - see
    ///     <see cref="ResolveCellRects_CellTextRequiresMoreHeightThanStored_GrowsRowAndShiftsNextRowYOffset"/>'s
    ///     matching remarks for the sz/EMU derivation) to wrap across three lines - growing the
    ///     row - while the single-digit substituted slide number ("7", a single token with no
    ///     cmap-mapped glyph of its own, but still exactly one line) fits the stored row height
    ///     and requires no growth at all.
    /// </summary>
    [Fact]
    public void ResolveCellRects_SlideNumberFieldCell_MeasuresSubstitutedTextNotCachedPlaceholder()
    {
        var txBody = new XElement(
            A + "txBody",
            new XElement(A + "bodyPr", new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0)),
            new XElement(
                A + "p",
                new XElement(
                    A + "fld",
                    new XAttribute("type", "slidenum"),
                    new XElement(A + "rPr", new XAttribute("sz", 100)),
                    new XElement(A + "t", "AA AA AA"))));

        var cell = PptxDocument.ParseTableCell(BuildTc(txBody: txBody), BuildTestTheme(), 30000f, 15000f);
        var table = new PptxTable([30000f], [new PptxTableRow(15000f, [cell])]);

        var withoutSlideNumber = PptxDocument.ResolveCellRects(table, BuildTestTheme(), ConstantFontResolver);
        var withSlideNumber = PptxDocument.ResolveCellRects(table, BuildTestTheme(), ConstantFontResolver, slideNumber: 7);

        Assert.True(withoutSlideNumber[0].HeightEmu > withSlideNumber[0].HeightEmu);
        Assert.Equal(15000f, withSlideNumber[0].HeightEmu); // single-token "7" fits the stored row height unchanged
    }

    /// <summary>
    ///     Proves <see cref="PptxDocument.PaintTable"/> accepts and threads an optional
    ///     <c>slideNumber</c> through to both measurement and painting without throwing - the
    ///     render-time wiring counterpart to the measurement-only proof above.
    /// </summary>
    [Fact]
    public void PaintTable_SlideNumberFieldCell_PaintsWithoutThrowing()
    {
        var txBody = new XElement(
            A + "txBody",
            new XElement(A + "bodyPr", new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0)),
            new XElement(
                A + "p",
                new XElement(
                    A + "fld",
                    new XAttribute("type", "slidenum"),
                    new XElement(A + "rPr", new XAttribute("sz", 100)),
                    new XElement(A + "t", "PLACEHOLDER"))));

        var cell = PptxDocument.ParseTableCell(BuildTc(txBody: txBody), BuildTestTheme(), 2000f, 500f);
        var table = new PptxTable([2000f], [new PptxTableRow(500f, [cell])]);

        using var surface = new Surface(100, 100);

        PptxDocument.PaintTable(surface, table, BuildTestTheme(), Matrix3x2.Identity, ConstantFontResolver, slideNumber: 7);

        // Primarily a no-throw regression test (the measurement-vs-painting substitution itself
        // is proven by ResolveCellRects_SlideNumberFieldCell_MeasuresSubstitutedTextNotCachedPlaceholder
        // above); assert the surface survived painting untouched in size/identity.
        Assert.Equal(100, surface.Width);
    }

    /// <summary>Proves a solid-filled cell paints its fill color across the cell's own rectangle.</summary>
    [Fact]
    public void PaintTable_SolidFilledCell_PaintsFillColorAcrossCellRectangle()
    {
        var table = new PptxTable(
            [100f],
            [new PptxTableRow(100f, [new PptxTableCell(1, 1, false, false, new PptxSolidFill(new Rgba32(10, 20, 30, 255)), null, null, null, null, null)])]);

        using var surface = new Surface(100, 100);

        PptxDocument.PaintTable(surface, table, BuildTestTheme(), Matrix3x2.Identity, ConstantFontResolver);

        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[50, 50]);
    }

    /// <summary>
    ///     Proves <see cref="PptxDocument.PaintTable"/> composes a non-identity
    ///     <c>shapeToSurfaceTransform</c> into a gradient-filled cell's own
    ///     <see cref="Gradient"/> before filling, so the gradient ramp is positioned/scaled in
    ///     surface space rather than left in untransformed local-EMU space (see
    ///     <c>PptxDocument.Tables.cs</c>'s own <c>FillPaint</c> overload's XmlDoc).
    /// </summary>
    [Fact]
    public void PaintTable_GradientFilledCellUnderNonIdentityTransform_PositionsGradientInSurfaceSpace()
    {
        // A local-space 100x100 cell with a horizontal (ang=0) red -> blue gradient. A 2x scale
        // transform maps the cell onto a 200x200 region of the surface.
        var tcElement = BuildTc(tcPr: BuildGradFillTcPr(0));
        var cell = PptxDocument.ParseTableCell(tcElement, BuildTestTheme(), 100f, 100f);
        var table = new PptxTable([100f], [new PptxTableRow(100f, [cell])]);

        using var surface = new Surface(200, 200);

        PptxDocument.PaintTable(surface, table, BuildTestTheme(), Matrix3x2.CreateScale(2f), ConstantFontResolver);

        // Sampled comfortably inside each end of the surface-space-mapped gradient ramp (local
        // x=0 -> surface x=0, local x=100 -> surface x=200): the near-start pixel should be
        // dominated by the first stop's color (red), the near-end pixel by the last stop's color
        // (blue). If the gradient's own Transform were left at the default identity (the pre-fix
        // behavior), the gradient would instead be evaluated directly against these surface-pixel
        // coordinates in the gradient's own un-scaled local-space terms, producing the wrong
        // color at both samples.
        var nearStart = surface[5, 100];
        var nearEnd = surface[195, 100];

        Assert.True(nearStart.R > nearStart.B, $"Expected a red-dominant pixel near the gradient's start, got {nearStart}.");
        Assert.True(nearEnd.B > nearEnd.R, $"Expected a blue-dominant pixel near the gradient's end, got {nearEnd}.");
    }

    /// <summary>Proves a cell's resolved border paints a stroked line at the cell's own edge.</summary>
    [Fact]
    public void PaintTable_CellWithTopBorder_PaintsStrokedLineAtTopEdge()
    {
        var border = new PptxLineStyle(10f, new PptxSolidFill(new Rgba32(255, 0, 0, 255)), null);
        var table = new PptxTable(
            [100f],
            [new PptxTableRow(100f, [new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, border, null, null)])]);

        using var surface = new Surface(100, 100);

        PptxDocument.PaintTable(surface, table, BuildTestTheme(), Matrix3x2.Identity, ConstantFontResolver);

        // The top border is a 10-EMU-wide stroked line centered on y=0 - comfortably hit at (50, 2).
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 2]);
        // Comfortably below the border's own stroke width, inside the (unfilled - PptxNoFill) cell body.
        Assert.Equal(default, surface[50, 50]);
    }

    /// <summary>Proves a cell's text content paints glyph ink inside the cell's own rectangle.</summary>
    [Fact]
    public void PaintTable_CellWithText_PaintsGlyphInkInsideCellRectangle()
    {
        // sz="100" (1pt) -> 12700 EMU font size; zero insets; a 100000x100000 EMU cell scaled by
        // 0.001 maps the whole cell onto the 100x100 surface, comfortably containing the glyph.
        var tcElement = BuildTc(txBody: BuildTextBody("A"));
        var cell = PptxDocument.ParseTableCell(tcElement, BuildTestTheme(), 100000f, 100000f);
        var table = new PptxTable([100000f], [new PptxTableRow(100000f, [cell])]);

        using var surface = new Surface(100, 100);

        PptxDocument.PaintTable(surface, table, BuildTestTheme(), Matrix3x2.CreateScale(0.001f), ConstantFontResolver);

        // Some ink was painted somewhere inside the cell's own 100x100 surface-space footprint -
        // a coarse but robust assertion (exact glyph position/alignment math is already proven by
        // PptxTextLayoutTests/PptxTextRenderTests; this test only proves the table-paint plumbing
        // reaches PaintTextLayout at all).
        var anyInk = false;
        for (var y = 0; y < 100 && !anyInk; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                if (surface[x, y] != default)
                {
                    anyInk = true;
                    break;
                }
            }
        }

        Assert.True(anyInk);
    }

    // --- Row-height growth (Phase 2 Follow-Up: Table Row-Height Growth) ----------------------------

    /// <summary>
    ///     Proves a row whose stored <c>&lt;a:tr h="..."&gt;</c> height is too small to fit its own
    ///     cell's wrapped-text content is grown to exactly that natural required height, and that
    ///     the next row's own Y-offset is computed from the grown (not stale stored) height.
    /// </summary>
    [Fact]
    public void ResolveCellRects_CellTextRequiresMoreHeightThanStored_GrowsRowAndShiftsNextRowYOffset()
    {
        // sz="100" (1pt) -> 12700 EMU font size; the synthetic font's single 'A' glyph has a
        // 1000-font-unit advance (UnitsPerEm 1000) -> each "AA" token costs 2 * 12700 = 25400 EMU.
        // A 30000 EMU-wide cell fits one "AA" token per line but not "AA AA" (50800 EMU) on one
        // line, so "AA AA" wraps to 2 lines, each 12700 EMU tall (zero line gap/leading) -
        // a natural required height of 25400 EMU.
        var cell = PptxDocument.ParseTableCell(BuildTc(txBody: BuildTextBody("AA AA")), BuildTestTheme(), 30000f, 10000f);
        var table = new PptxTable(
            [30000f],
            [
                new PptxTableRow(10000f, [cell]), // stored height (10000) smaller than the required 25400
                new PptxTableRow(7000f, [new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null)]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table, BuildTestTheme(), ConstantFontResolver);

        Assert.Equal(2, rects.Count);
        Assert.Equal(25400f, rects[0].HeightEmu); // grown to the natural required height, not the smaller stored value
        Assert.Equal(25400f, rects[1].YEmu); // row 1 starts at the grown (not stale stored) Y-offset
        Assert.Equal(7000f, rects[1].HeightEmu); // row 1 itself is unaffected (no text, no growth)
    }

    /// <summary>
    ///     Proves a row whose stored height already comfortably exceeds its own cell's natural
    ///     wrapped-text height is left entirely unaffected (the regression-safety guarantee: grow,
    ///     never shrink, and never grow unnecessarily).
    /// </summary>
    [Fact]
    public void ResolveCellRects_CellTextFitsWithinStoredHeight_RowHeightUnaffected()
    {
        // Same wrapped-text shape as the growth test above (natural required height 25400 EMU),
        // but a stored height (1000000) that already comfortably exceeds it.
        var cell = PptxDocument.ParseTableCell(BuildTc(txBody: BuildTextBody("AA AA")), BuildTestTheme(), 30000f, 1000000f);
        var table = new PptxTable(
            [30000f],
            [
                new PptxTableRow(1000000f, [cell]),
                new PptxTableRow(7000f, [new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null)]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table, BuildTestTheme(), ConstantFontResolver);

        Assert.Equal(2, rects.Count);
        Assert.Equal(1000000f, rects[0].HeightEmu); // unchanged - the stored value already fits the text
        Assert.Equal(1000000f, rects[1].YEmu); // row 1's Y-offset is unaffected
    }

    /// <summary>
    ///     Proves a row-spanning cell (<c>RowSpan</c> &gt; 1) whose own wrapped-text natural
    ///     height exceeds the sum of its spanned rows' stored heights has its shortfall added
    ///     entirely to its <em>last</em> spanned row - the first spanned row (which may anchor its
    ///     own unrelated single-row cell) is left at its own stored height, documenting the
    ///     "grow the last spanned row" distribution policy (see <c>GrowRowHeightsToFitText</c>'s
    ///     own XmlDoc remarks for the full rationale).
    /// </summary>
    [Fact]
    public void ResolveCellRects_RowSpanCellTextRequiresMoreHeightThanSpanTotal_GrowsLastSpannedRow()
    {
        // "AA AA AA AA" (4 "AA" tokens) wraps to 4 lines at 30000 EMU width (one "AA" token per
        // line, each 12700 EMU tall) -> a natural required height of 4 * 12700 = 50800 EMU,
        // comfortably exceeding the spanned rows' own stored sum of 5000 + 5000 = 10000 EMU.
        var spanningCell = PptxDocument.ParseTableCell(
            BuildTc(rowSpan: 2, txBody: BuildTextBody("AA AA AA AA")), BuildTestTheme(), 30000f, 10000f);
        var vMergeContinuation = PptxDocument.ParseTableCell(BuildTc(vMerge: true), BuildTestTheme(), 30000f, 5000f);
        var row0Plain = PptxDocument.ParseTableCell(BuildTc(), BuildTestTheme(), 1000f, 5000f);
        var row1Plain = PptxDocument.ParseTableCell(BuildTc(), BuildTestTheme(), 1000f, 5000f);

        var table = new PptxTable(
            [30000f, 1000f],
            [
                new PptxTableRow(5000f, [spanningCell, row0Plain]),
                new PptxTableRow(5000f, [vMergeContinuation, row1Plain]),
            ]);

        var rects = PptxDocument.ResolveCellRects(table, BuildTestTheme(), ConstantFontResolver);

        Assert.Equal(3, rects.Count); // the vMerge continuation contributes no rectangle of its own
        // Identified by document-order position (WalkResolvedCells walks row-major, left-to-right
        // within each row) rather than Cell reference - PptxTableCell is a record, so
        // row0Plain/row1Plain (identical field values) would otherwise compare equal to each other.
        var spanningRect = rects[0]; // row 0, column 0: the RowSpan=2 governing cell
        var row0PlainRect = rects[1]; // row 0, column 1
        var row1PlainRect = rects[2]; // row 1, column 1 (row 1's own vMerge continuation at column 0 contributes no rectangle)

        Assert.Equal(50800f, spanningRect.HeightEmu); // the merged span's own total equals the natural required height exactly
        Assert.Equal(5000f, row0PlainRect.HeightEmu); // the first spanned row is unchanged - still its own stored value
        Assert.Equal(45800f, row1PlainRect.HeightEmu); // the last spanned row absorbs the entire 40800 EMU shortfall (50800 - 10000)
    }

    /// <summary>
    ///     Proves a row-spanning cell whose declared <c>rowSpan</c> extends past the table's own
    ///     actual row count (a malformed-but-not-rejected input, since <see cref="PptxDocument.ParseTableCell"/>
    ///     only rejects a non-positive <c>rowSpan</c>, not one exceeding the remaining row count)
    ///     has its shortfall clamped onto the table's own last actual row instead of indexing past
    ///     the end of the effective-heights array - a regression test for a growth-pass crash
    ///     (<see cref="ArgumentOutOfRangeException"/>) found during formal review.
    /// </summary>
    [Fact]
    public void ResolveCellRects_RowSpanExceedsTableRowCount_ClampsShortfallOntoLastActualRow()
    {
        // "AA AA AA AA" (4 "AA" tokens) wraps to a natural required height of 50800 EMU - see the
        // sibling GrowsLastSpannedRow test above for the identical wrapping math - comfortably
        // exceeding the table's own total stored height of 5000 + 5000 = 10000 EMU, even though
        // the cell declares a rowSpan of 5 while the table itself only has 2 rows.
        var spanningCell = PptxDocument.ParseTableCell(
            BuildTc(rowSpan: 5, txBody: BuildTextBody("AA AA AA AA")), BuildTestTheme(), 30000f, 10000f);
        var vMergeContinuation = PptxDocument.ParseTableCell(BuildTc(vMerge: true), BuildTestTheme(), 30000f, 5000f);

        var table = new PptxTable(
            [30000f],
            [
                new PptxTableRow(5000f, [spanningCell]),
                new PptxTableRow(5000f, [vMergeContinuation]),
            ]);

        // Must not throw ArgumentOutOfRangeException - the fix clamps the "last spanned row" index
        // to the table's own last actual row (index 1) instead of the declared-but-nonexistent
        // index 4 (rowIndex 0 + rowSpan 5 - 1).
        var rects = PptxDocument.ResolveCellRects(table, BuildTestTheme(), ConstantFontResolver);

        var spanningRect = Assert.Single(rects); // the vMerge continuation contributes no rectangle of its own
        Assert.Equal(50800f, spanningRect.HeightEmu); // the table's own last actual row absorbed the entire shortfall
    }

    /// <summary>
    ///     Proves a row-spanning cell whose declared <c>rowSpan</c> is large enough that
    ///     <c>rowIndex + rowSpan - 1</c> would overflow <see cref="int"/> (a malformed-but-not-
    ///     rejected input, since <see cref="PptxDocument.ParseTableCell"/> only rejects a
    ///     non-positive <c>rowSpan</c>, not an excessively large one) still clamps the shortfall
    ///     onto the table's own last actual row rather than overflowing into a negative index - a
    ///     regression test for an overflow found during a follow-up code review of the sibling
    ///     <see cref="ResolveCellRects_RowSpanExceedsTableRowCount_ClampsShortfallOntoLastActualRow"/>
    ///     fix above.
    /// </summary>
    [Fact]
    public void ResolveCellRects_RowSpanNearIntMaxValue_ClampsShortfallWithoutOverflow()
    {
        var spanningCell = PptxDocument.ParseTableCell(
            BuildTc(rowSpan: int.MaxValue - 1, txBody: BuildTextBody("AA AA AA AA")), BuildTestTheme(), 30000f, 10000f);

        var table = new PptxTable(
            [30000f],
            [new PptxTableRow(5000f, [spanningCell])]);

        // Must not throw ArgumentOutOfRangeException (or index with a wrapped-negative index) -
        // `rowIndex + rowSpan - 1` would overflow to a negative value for this rowSpan if computed
        // before clamping against the table's own (small) remaining row count.
        var rects = PptxDocument.ResolveCellRects(table, BuildTestTheme(), ConstantFontResolver);

        var spanningRect = Assert.Single(rects);
        Assert.Equal(50800f, spanningRect.HeightEmu); // the table's own only row absorbed the entire shortfall
    }

    /// <summary>
    ///     Proves growth is strictly opt-in via <see cref="PptxDocument.ResolveCellRects"/>'s new
    ///     optional <c>theme</c>/<c>fontResolver</c> parameters: a cell carrying a
    ///     <c>TextBody</c> that would otherwise require growth is left at its stored height when
    ///     neither parameter is supplied, matching the pre-existing default-path behavior for
    ///     every caller not yet passing them.
    /// </summary>
    [Fact]
    public void ResolveCellRects_NoThemeOrFontResolverSupplied_PreservesStoredHeightsUnconditionally()
    {
        var cell = PptxDocument.ParseTableCell(BuildTc(txBody: BuildTextBody("AA AA")), BuildTestTheme(), 30000f, 10000f);
        var table = new PptxTable([30000f], [new PptxTableRow(10000f, [cell])]);

        var rects = PptxDocument.ResolveCellRects(table);

        var rect = Assert.Single(rects);
        Assert.Equal(10000f, rect.HeightEmu); // the stored value, unconditionally - no growth without a theme/font resolver
    }

    /// <summary>
    ///     End-to-end proof that <see cref="PptxDocument.PaintTable"/> grows a row to fit wrapped
    ///     text and paints the next row below the grown (not stale stored) Y-offset - directly
    ///     proving the reported overlap symptom is fixed, not just at the
    ///     <see cref="PptxDocument.ResolveCellRects"/> unit level.
    /// </summary>
    [Fact]
    public void PaintTable_TwoRowTableWithWrappedTextOverflowingStoredHeight_PaintsSecondRowBelowGrownFirstRow()
    {
        // Row 0's own stored height (10000 EMU) is far smaller than its "AA AA" cell's own
        // natural required height (25400 EMU, same math as the ResolveCellRects growth test
        // above). Row 1 is a solid-filled cell with no text of its own, used purely as a probe:
        // if row 0 is not grown, row 1's stale (stored-height) Y-offset (10000) would sit inside
        // row 0's own still-overlapping wrapped second line of text; once grown, row 1 starts
        // comfortably below all of row 0's own glyph ink.
        var textCell = PptxDocument.ParseTableCell(BuildTc(txBody: BuildTextBody("AA AA")), BuildTestTheme(), 30000f, 10000f);
        var probeCell = new PptxTableCell(1, 1, false, false, new PptxSolidFill(new Rgba32(10, 20, 30, 255)), null, null, null, null, null);
        var table = new PptxTable(
            [30000f],
            [
                new PptxTableRow(10000f, [textCell]),
                new PptxTableRow(5000f, [probeCell]),
            ]);

        // 1 EMU == 1 surface pixel pre-scale; a 0.25x shapeToSurfaceTransform keeps every
        // dimension comfortably under Surface's own maximum (8192), while leaving the resolved
        // EMU math (and this test's own rationale) identical to the 1:1 case.
        const float scale = 0.25f;
        using var surface = new Surface(7500, 7600);

        PptxDocument.PaintTable(surface, table, BuildTestTheme(), Matrix3x2.CreateScale(scale), ConstantFontResolver);

        // Row 1's probe fill starts at Y=25400*0.25=6350 (row 0's grown height), not
        // Y=10000*0.25=2500 (row 0's stale stored height) - sampled comfortably inside row 1's
        // own fill, just below the grown boundary, proving row 1 was shifted down to avoid
        // overlapping row 0's own wrapped text.
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[3750, 6375]);

        // Comfortably above the grown boundary (inside row 0's own area, between its two
        // wrapped-text lines) - the probe's fill color must not have bled upward.
        Assert.NotEqual(new Rgba32(10, 20, 30, 255), surface[3750, 5000]);
    }

    // --- Table style resolution (Phase 2 Follow-Up: Table Style/Banding Resolution) --------------

    /// <summary>
    ///     Proves (a): a table with a matching <c>&lt;a:tableStyleId&gt;</c> and
    ///     <c>&lt;a:tblPr firstRow="1"&gt;</c> renders its header row (row 0) cell using the
    ///     matched style's <c>&lt;a:firstRow&gt;</c> tier fill, while a non-header row falls back
    ///     to the style's <c>&lt;a:wholeTbl&gt;</c> base-tier fill.
    /// </summary>
    [Fact]
    public void ParseTable_FirstRowTblPrWithMatchingTableStyle_HeaderRowCellUsesFirstRowStyleFill()
    {
        var tblStyle = BuildTblStyle(
            "styleA",
            wholeTbl: BuildTcStyle(BuildFillElement("111111")),
            firstRow: BuildTcStyle(BuildFillElement("222222")));

        var tbl = BuildTblWithPr(
            BuildTblPr(tableStyleId: "styleA", firstRow: true),
            [1000f],
            BuildTr(500f, BuildTc()),
            BuildTr(500f, BuildTc()));
        var graphicFrame = BuildGraphicFrame(tbl);

        var table = PptxDocument.ParseTable(
            graphicFrame, BuildTestTheme(), tableStyleResolver: id => id == "styleA" ? tblStyle : null);

        var headerFill = Assert.IsType<PptxSolidFill>(table.Rows[0].Cells[0].Fill);
        Assert.Equal(new Rgba32(0x22, 0x22, 0x22, 255), headerFill.Color);

        var dataFill = Assert.IsType<PptxSolidFill>(table.Rows[1].Cells[0].Fill);
        Assert.Equal(new Rgba32(0x11, 0x11, 0x11, 255), dataFill.Color);
    }

    /// <summary>
    ///     Proves (b): with a matching style and <c>&lt;a:tblPr firstRow="1" bandRow="1"&gt;</c>,
    ///     the header row uses <c>&lt;a:firstRow&gt;</c>'s fill and the remaining (non-header)
    ///     rows alternate <c>&lt;a:band1H&gt;</c>/<c>&lt;a:band2H&gt;</c> starting immediately
    ///     after the header row - with a band tier declaring no <c>&lt;a:fill&gt;</c> at all (here,
    ///     <c>band2H</c>) falling through to the <c>&lt;a:wholeTbl&gt;</c> base fill rather than
    ///     "no fill".
    /// </summary>
    [Fact]
    public void ParseTable_BandRowTblPrWithMatchingTableStyle_AlternatesBand1HAndBand2HFillStartingAfterHeaderRow()
    {
        var tblStyle = BuildTblStyle(
            "styleB",
            wholeTbl: BuildTcStyle(BuildFillElement("AAAAAA")),
            band1H: BuildTcStyle(BuildFillElement("BBBBBB")),
            band2H: BuildTcStyle(), // deliberately no <a:fill> - must fall through to wholeTbl
            firstRow: BuildTcStyle(BuildFillElement("CCCCCC")));

        var tbl = BuildTblWithPr(
            BuildTblPr(tableStyleId: "styleB", firstRow: true, bandRow: true),
            [1000f],
            BuildTr(500f, BuildTc()),
            BuildTr(500f, BuildTc()),
            BuildTr(500f, BuildTc()),
            BuildTr(500f, BuildTc()),
            BuildTr(500f, BuildTc()));
        var graphicFrame = BuildGraphicFrame(tbl);

        var table = PptxDocument.ParseTable(
            graphicFrame, BuildTestTheme(), tableStyleResolver: id => id == "styleB" ? tblStyle : null);

        Assert.Equal(new Rgba32(0xCC, 0xCC, 0xCC, 255), Assert.IsType<PptxSolidFill>(table.Rows[0].Cells[0].Fill).Color); // header (firstRow)
        Assert.Equal(new Rgba32(0xBB, 0xBB, 0xBB, 255), Assert.IsType<PptxSolidFill>(table.Rows[1].Cells[0].Fill).Color); // band1H
        Assert.Equal(new Rgba32(0xAA, 0xAA, 0xAA, 255), Assert.IsType<PptxSolidFill>(table.Rows[2].Cells[0].Fill).Color); // band2H -> wholeTbl
        Assert.Equal(new Rgba32(0xBB, 0xBB, 0xBB, 255), Assert.IsType<PptxSolidFill>(table.Rows[3].Cells[0].Fill).Color); // band1H
        Assert.Equal(new Rgba32(0xAA, 0xAA, 0xAA, 255), Assert.IsType<PptxSolidFill>(table.Rows[4].Cells[0].Fill).Color); // band2H -> wholeTbl
    }

    /// <summary>
    ///     Proves (c), case 1: a table whose <c>&lt;a:tblPr&gt;</c> declares no
    ///     <c>&lt;a:tableStyleId&gt;</c> at all never even invokes the table-style resolver
    ///     delegate, and falls back to today's plain, style-less cell-only fill/border resolution.
    /// </summary>
    [Fact]
    public void ParseTable_TblPrWithNoTableStyleId_FallsBackToPlainCellOnlyBorderAndNoFill()
    {
        var tcPr = new XElement(
            A + "tcPr",
            BuildTcPrLineEdge("lnL", 100f, "FF0000"),
            BuildFillElement("00FF00").Elements().First()); // <a:solidFill> direct child of <a:tcPr>, matching existing tcPr convention

        var tbl = BuildTblWithPr(
            BuildTblPr(firstRow: true, bandRow: true), // no tableStyleId
            [1000f],
            BuildTr(500f, BuildTc(tcPr: tcPr)));
        var graphicFrame = BuildGraphicFrame(tbl);

        var table = PptxDocument.ParseTable(
            graphicFrame, BuildTestTheme(),
            tableStyleResolver: _ => throw new InvalidOperationException("Resolver must not be invoked with no tableStyleId."));

        var cell = table.Rows[0].Cells[0];
        var fill = Assert.IsType<PptxSolidFill>(cell.Fill);
        Assert.Equal(new Rgba32(0x00, 0xFF, 0x00, 255), fill.Color);
        Assert.NotNull(cell.LeftBorder);
        Assert.Null(cell.RightBorder);
    }

    /// <summary>
    ///     Proves (c), case 2: a table whose <c>&lt;a:tableStyleId&gt;</c> does not resolve to any
    ///     known <c>&lt;a:tblStyle&gt;</c> (the resolver returns <see langword="null"/>) falls back
    ///     to the same plain, style-less cell-only fill/border resolution, without throwing.
    /// </summary>
    [Fact]
    public void ParseTable_TblPrWithUnresolvableTableStyleId_FallsBackToPlainCellOnlyBorderAndNoFillWithoutThrowing()
    {
        var tcPr = new XElement(A + "tcPr", BuildTcPrLineEdge("lnT", 150f, "0000FF"));

        var tbl = BuildTblWithPr(
            BuildTblPr(tableStyleId: "{NO-SUCH-STYLE}", firstRow: true, bandRow: true),
            [1000f],
            BuildTr(500f, BuildTc(tcPr: tcPr)));
        var graphicFrame = BuildGraphicFrame(tbl);

        var table = PptxDocument.ParseTable(graphicFrame, BuildTestTheme(), tableStyleResolver: _ => null);

        var cell = table.Rows[0].Cells[0];
        Assert.IsType<PptxNoFill>(cell.Fill);
        Assert.NotNull(cell.TopBorder);
        Assert.Null(cell.LeftBorder);
    }

    /// <summary>
    ///     Proves (d): a cell's own explicit <c>&lt;a:tcPr&gt;</c> fill and a single explicit
    ///     border edge (<c>&lt;a:lnT&gt;</c>) take final precedence over the matched table style's
    ///     fill/borders, while the cell's remaining (non-overridden) border edges still fall back
    ///     to the matched style's own <c>&lt;a:wholeTbl&gt;</c> tier.
    /// </summary>
    [Fact]
    public void ParseTableCell_TcPrExplicitFillAndBorder_OverridesTableStyleFillAndBorder()
    {
        var tblStyle = BuildTblStyle(
            "styleD",
            wholeTbl: BuildTcStyle(
                BuildFillElement("111111"),
                BuildTcBdrEdge("left", 50f, "AAAAAA"),
                BuildTcBdrEdge("right", 50f, "AAAAAA"),
                BuildTcBdrEdge("top", 50f, "AAAAAA"),
                BuildTcBdrEdge("bottom", 50f, "AAAAAA")));

        var tcPr = new XElement(
            A + "tcPr",
            BuildTcPrLineEdge("lnT", 999f, "FF00FF"),
            BuildFillElement("FFFFFF").Elements().First());

        var tbl = BuildTblWithPr(
            BuildTblPr(tableStyleId: "styleD"),
            [1000f],
            BuildTr(500f, BuildTc(tcPr: tcPr)));
        var graphicFrame = BuildGraphicFrame(tbl);

        var table = PptxDocument.ParseTable(
            graphicFrame, BuildTestTheme(), tableStyleResolver: id => id == "styleD" ? tblStyle : null);

        var cell = table.Rows[0].Cells[0];
        var fill = Assert.IsType<PptxSolidFill>(cell.Fill);
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xFF, 255), fill.Color);

        Assert.NotNull(cell.TopBorder);
        Assert.Equal(999f, cell.TopBorder.WidthEmu);

        Assert.NotNull(cell.LeftBorder);
        Assert.Equal(50f, cell.LeftBorder.WidthEmu); // falls back to the style's own wholeTbl tier
        Assert.NotNull(cell.RightBorder);
        Assert.Equal(50f, cell.RightBorder.WidthEmu);
        Assert.NotNull(cell.BottomBorder);
        Assert.Equal(50f, cell.BottomBorder.WidthEmu);
    }

    /// <summary>
    ///     Proves (recommended 5th case): an interior column boundary resolves against the style's
    ///     <c>&lt;a:tcBdr&gt;/&lt;a:insideV&gt;</c> edge rather than its <c>left</c>/<c>right</c>
    ///     edges, while the table's two true outer-boundary edges (the first cell's own left edge,
    ///     the last cell's own right edge) still resolve against <c>left</c>/<c>right</c>.
    /// </summary>
    [Fact]
    public void ParseTableCell_InteriorColumnBorder_UsesInsideVNotLeftRightTcBdrEdge()
    {
        var tblStyle = BuildTblStyle(
            "styleE",
            wholeTbl: BuildTcStyle(
                null,
                BuildTcBdrEdge("left", 10f, "111111"),
                BuildTcBdrEdge("right", 20f, "222222"),
                BuildTcBdrEdge("insideV", 30f, "333333")));

        var tbl = BuildTblWithPr(
            BuildTblPr(tableStyleId: "styleE"),
            [1000f, 1000f, 1000f],
            BuildTr(500f, BuildTc(), BuildTc(), BuildTc()));
        var graphicFrame = BuildGraphicFrame(tbl);

        var table = PptxDocument.ParseTable(
            graphicFrame, BuildTestTheme(), tableStyleResolver: id => id == "styleE" ? tblStyle : null);

        var firstCell = table.Rows[0].Cells[0];
        Assert.Equal(10f, firstCell.LeftBorder!.WidthEmu); // true outer edge
        Assert.Equal(30f, firstCell.RightBorder!.WidthEmu); // interior edge

        var middleCell = table.Rows[0].Cells[1];
        Assert.Equal(30f, middleCell.LeftBorder!.WidthEmu); // interior edge
        Assert.Equal(30f, middleCell.RightBorder!.WidthEmu); // interior edge

        var lastCell = table.Rows[0].Cells[2];
        Assert.Equal(30f, lastCell.LeftBorder!.WidthEmu); // interior edge
        Assert.Equal(20f, lastCell.RightBorder!.WidthEmu); // true outer edge
    }

    /// <summary>Proves a no-fill, no-border, no-text cell paints nothing at all.</summary>
    [Fact]
    public void PaintTable_EmptyCell_PaintsNothing()
    {
        var table = new PptxTable(
            [100f],
            [new PptxTableRow(100f, [new PptxTableCell(1, 1, false, false, PptxNoFill.Instance, null, null, null, null, null)])]);

        using var surface = new Surface(100, 100);

        PptxDocument.PaintTable(surface, table, BuildTestTheme(), Matrix3x2.Identity, ConstantFontResolver);

        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }
}
