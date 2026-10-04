using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx tbl tblgrid gridcol hmerge vmerge gridspan rowspan tcpr lnl lnr lnt lnb txbody unitsperem srgb

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
