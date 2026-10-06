using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Charts;
using DemaConsulting.CanvasNet.Charts.OpenXml;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx graphicframe chartspace numcache strcache sppr nvpr nvgraphicframepr cnvgraphicframepr
// cspell:ignore barchart linechart piechart areachart radarchart

/// <summary>
///     Unit-level tests for Phase 4's chart-graphic-frame integration:
///     <see cref="PptxDocument.ParseShapeTree"/>'s table-vs-chart dispatch (bar/line/pie/area),
///     <see cref="PptxDocument.ParseChart"/>'s deferred-feature wrap-to-
///     <see cref="PptxUnsupportedFeatureException"/> and malformed-chart rejection, and a
///     synthetic, fully in-memory chart-only-slide end-to-end render proving visible pixels -
///     mirroring <see cref="PptxTablesTests"/>'s own "directly-constructed <see cref="XElement"/>
///     fragments, no full on-disk package needed" style for the dispatch/wrap/malformed tests, and
///     <see cref="PptxRenderTests"/>'s own in-memory-package style for the end-to-end test.
///     Complements (does not duplicate) the real-world-fixture-driven proofs in
///     <see cref="PptxFixturesCorpusTests"/>.
/// </summary>
public class PptxChartsTests
{
    /// <summary>The PresentationML namespace.</summary>
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>The DrawingML-Main namespace.</summary>
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>The DrawingML-Charts namespace.</summary>
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    /// <summary>The OfficeDocument-Relationships namespace, used for a <c>&lt;c:chart&gt;</c> element's own <c>r:id</c> attribute.</summary>
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    #region Chart XML fragment builders (mirroring DemaConsulting.CanvasNet.Charts.Tests.OpenXmlChartParserTests)

    /// <summary>Builds a <c>c:numCache</c> element for the given dense values.</summary>
    private static XElement NumCache(params double[] values) =>
        new(C + "numCache",
            new XElement(C + "formatCode", "General"),
            new XElement(C + "ptCount", new XAttribute("val", values.Length)),
            values.Select((v, i) => new XElement(C + "pt", new XAttribute("idx", i), new XElement(C + "v", v))));

    /// <summary>Builds a <c>c:strCache</c> element for the given dense labels.</summary>
    private static XElement StrCache(params string[] values) =>
        new(C + "strCache",
            new XElement(C + "ptCount", new XAttribute("val", values.Length)),
            values.Select((v, i) => new XElement(C + "pt", new XAttribute("idx", i), new XElement(C + "v", v))));

    /// <summary>Builds a <c>c:tx</c> element with a cached string name.</summary>
    private static XElement Tx(string name) =>
        new(C + "tx", new XElement(C + "strRef", new XElement(C + "f", "Sheet1!$B$1"), StrCache(name)));

    /// <summary>Builds a <c>c:val</c> element with cached numeric values.</summary>
    private static XElement Val(params double[] values) =>
        new(C + "val", new XElement(C + "numRef", new XElement(C + "f", "Sheet1!$B$2:$B$5"), NumCache(values)));

    /// <summary>Builds a <c>c:cat</c> element with cached string labels.</summary>
    private static XElement Cat(params string[] labels) =>
        new(C + "cat", new XElement(C + "strRef", new XElement(C + "f", "Sheet1!$A$2:$A$5"), StrCache(labels)));

    /// <summary>Builds a <c>c:ser</c> element from the given name/values/categories.</summary>
    private static XElement Ser(string name, double[] values, string[] categories) =>
        new(C + "ser",
            new XElement(C + "idx", new XAttribute("val", 0)),
            new XElement(C + "order", new XAttribute("val", 0)),
            Tx(name),
            Cat(categories),
            Val(values));

    /// <summary>Builds a minimal <c>c:chart</c> element wrapping a single chart-type element.</summary>
    private static XElement ChartElement(XElement chartTypeElement) =>
        new(C + "chart",
            new XElement(C + "autoTitleDeleted", new XAttribute("val", 0)),
            new XElement(C + "plotArea", new XElement(C + "layout"), chartTypeElement));

    #endregion

    /// <summary>Builds a <c>&lt;p:graphicFrame&gt;</c> wrapping a chart's <c>&lt;a:graphicData&gt;</c>, referencing the given relationship id.</summary>
    private static XElement BuildChartGraphicFrame(string relationshipId) =>
        new(P + "graphicFrame",
            new XElement(P + "nvGraphicFramePr",
                new XElement(P + "cNvPr", new XAttribute("id", 2), new XAttribute("name", "Chart 1")),
                new XElement(P + "cNvGraphicFramePr"),
                new XElement(P + "nvPr")),
            new XElement(P + "xfrm",
                new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(A + "ext", new XAttribute("cx", 4572000), new XAttribute("cy", 3429000))),
            new XElement(A + "graphic",
                new XElement(A + "graphicData",
                    new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/chart"),
                    new XElement(C + "chart", new XAttribute(R + "id", relationshipId)))));

    // --- Dispatch: bar/line/pie/area ------------------------------------------------------------

    /// <summary>Proves a <c>&lt;p:graphicFrame&gt;</c> declaring each of bar/line/pie/area chart kinds is dispatched by <see cref="PptxDocument.ParseShapeTree"/> to a non-null <see cref="PptxGraphicFrameShapeNode.Chart"/> (and a null <see cref="PptxGraphicFrameShapeNode.Table"/>) of the expected <see cref="ChartType"/>, rather than to <see cref="PptxDocument.ParseTable"/>.</summary>
    [Theory]
    [InlineData("barChart", "bar", ChartType.Bar)]
    [InlineData("lineChart", null, ChartType.Line)]
    [InlineData("pieChart", null, ChartType.Pie)]
    [InlineData("areaChart", null, ChartType.Area)]
    public void ParseShapeTree_GraphicFrameWithSupportedChart_DispatchesToChartNode(string chartTypeLocalName, string? barDir, ChartType expectedType)
    {
        // Arrange
        var ser = Ser("Series 1", [1.0, 2.0, 3.0], ["A", "B", "C"]);
        var chartTypeElement = barDir is null
            ? new XElement(C + chartTypeLocalName, ser)
            : new XElement(C + chartTypeLocalName, new XElement(C + "barDir", new XAttribute("val", barDir)), ser);
        var chartSpace = ChartElement(chartTypeElement);
        var graphicFrame = BuildChartGraphicFrame("rId1");
        var spTree = new XElement(P + "spTree", graphicFrame);

        // Act
        var nodes = PptxDocument.ParseShapeTree(
            spTree,
            themeResolver: () => throw new InvalidOperationException("A chart graphic frame must never resolve a theme."),
            resolveChartPart: _ => chartSpace);

        // Assert
        var node = Assert.IsType<PptxGraphicFrameShapeNode>(Assert.Single(nodes));
        Assert.Null(node.Table);
        Assert.NotNull(node.Chart);
        Assert.Equal(expectedType, node.Chart.Type);
    }

    /// <summary>Proves a <c>&lt;p:graphicFrame&gt;</c> declaring a table (not a chart) is still dispatched to <see cref="PptxDocument.ParseTable"/> even when <c>resolveChartPart</c> is supplied, since its own <c>&lt;a:graphicData&gt;</c> <c>uri</c> does not end in <c>"/chart"</c>.</summary>
    [Fact]
    public void ParseShapeTree_GraphicFrameWithTable_StillDispatchesToTableNode()
    {
        // Arrange
        var tbl = new XElement(A + "tbl",
            new XElement(A + "tblGrid", new XElement(A + "gridCol", new XAttribute("w", 100))),
            new XElement(A + "tr", new XAttribute("h", 100), new XElement(A + "tc", new XElement(A + "tcPr"))));
        var graphicFrame = new XElement(P + "graphicFrame",
            new XElement(A + "graphic",
                new XElement(A + "graphicData", new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/table"), tbl)));
        var spTree = new XElement(P + "spTree", graphicFrame);
        var theme = new PptxTheme(
            new PptxColorScheme(
                new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255),
                new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255),
                new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255), new Rgba32(0, 0, 0, 255)),
            new PptxFontScheme(
                new PptxFontCollection("Latin", "EA", "CS"),
                new PptxFontCollection("Latin", "EA", "CS")));

        // Act
        var nodes = PptxDocument.ParseShapeTree(
            spTree,
            themeResolver: () => theme,
            resolveChartPart: _ => throw new InvalidOperationException("A table graphic frame must never resolve a chart part."));

        // Assert
        var node = Assert.IsType<PptxGraphicFrameShapeNode>(Assert.Single(nodes));
        Assert.NotNull(node.Table);
        Assert.Null(node.Chart);
    }

    // --- Deferred feature: radar -----------------------------------------------------------------

    /// <summary>Proves a <c>&lt;p:graphicFrame&gt;</c> declaring a recognized-but-unsupported chart kind (a radar chart) is wrapped by <see cref="PptxDocument.ParseChart"/> into a <see cref="PptxUnsupportedFeatureException"/> with feature token <c>"pptx-chart-" + ex.Feature</c> (here <c>"pptx-chart-charts-openxml-radar-chart"</c>), rather than the underlying <see cref="ChartUnsupportedFeatureException"/> escaping unwrapped.</summary>
    [Fact]
    public void ParseShapeTree_GraphicFrameWithRadarChart_ThrowsPptxUnsupportedFeatureExceptionWithWrappedFeatureToken()
    {
        // Arrange
        var radarChart = new XElement(C + "radarChart", Ser("Series 1", [1.0, 2.0, 3.0], ["A", "B", "C"]));
        var chartSpace = ChartElement(radarChart);
        var graphicFrame = BuildChartGraphicFrame("rId1");
        var spTree = new XElement(P + "spTree", graphicFrame);

        // Act
        var exception = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ParseShapeTree(
            spTree,
            themeResolver: () => throw new InvalidOperationException("A chart graphic frame must never resolve a theme."),
            resolveChartPart: _ => chartSpace));

        // Assert
        Assert.Equal("pptx-chart-charts-openxml-radar-chart", exception.Feature);
        Assert.IsType<ChartUnsupportedFeatureException>(exception.InnerException);
    }

    // --- Malformed chart ---------------------------------------------------------------------------

    /// <summary>Proves an <c>&lt;a:graphicData&gt;</c> whose <c>&lt;c:chart&gt;</c> child has no <c>r:id</c> attribute throws <see cref="InvalidDataException"/> (a malformed chart graphic frame, distinct from a recognized-but-unsupported chart kind).</summary>
    [Fact]
    public void ParseChart_ChartElementWithNoRelationshipId_ThrowsInvalidDataException()
    {
        // Arrange
        var graphicData = new XElement(A + "graphicData",
            new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/chart"),
            new XElement(C + "chart"));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseChart(
            graphicData, _ => throw new InvalidOperationException("No relationship id to resolve - the exception must be thrown before this is called.")));
    }

    /// <summary>Proves an <c>&lt;a:graphicData&gt;</c> with no <c>&lt;c:chart&gt;</c> child at all throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ParseChart_GraphicDataWithNoChartChild_ThrowsInvalidDataException()
    {
        // Arrange
        var graphicData = new XElement(A + "graphicData", new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/chart"));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseChart(
            graphicData, _ => throw new InvalidOperationException("No relationship id to resolve - the exception must be thrown before this is called.")));
    }

    // --- Synthetic end-to-end render -------------------------------------------------------------

    /// <summary>
    ///     Proves a synthetic, fully in-memory chart-only slide (no tables, pictures, or other
    ///     shapes) opens and renders successfully, painting visible pixels - an end-to-end proof
    ///     that a chart graphic frame's own <see cref="PptxDocument.ParseChart"/> result reaches
    ///     <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/>'s compositing via
    ///     <c>PptxDocument.Render.cs</c>'s chart branch (<c>ChartRenderer.Render</c> then
    ///     <c>PaintPicture</c>), not merely that the shape tree parses correctly.
    /// </summary>
    [Fact]
    public void Render_ChartOnlySlide_PaintsVisibleContent()
    {
        // Arrange
        var barChart = new XElement(C + "barChart",
            new XElement(C + "barDir", new XAttribute("val", "col")),
            Ser("Revenue", [10.0, 20.0, 30.0], ["Q1", "Q2", "Q3"]));
        var chartSpace = new XElement(C + "chartSpace", ChartElement(barChart));
        using var stream = BuildChartOnlySlidePackage(chartSpace);
        using var document = PptxDocument.Open(stream);

        // Act
        Assert.Equal(1, document.SlideCount);
        using var surface = document.Render(0, dpi: 96f, new PptxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) });

        // Assert
        var paintedAnyPixel = false;
        for (var y = 0; y < surface.Height && !paintedAnyPixel; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A != 0)
                {
                    paintedAnyPixel = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyPixel, "Expected the rendered chart-only slide to paint at least one non-transparent pixel.");
    }

    /// <summary>
    ///     Builds a minimal, fully navigable, in-memory <c>.pptx</c>-shaped package (presentation
    ///     -&gt; slide -&gt; layout -&gt; master -&gt; theme, each with its own relationships part,
    ///     mirroring <see cref="PptxRenderTests"/>'s own <c>BuildRenderPackage</c> helper) whose
    ///     one slide's <c>&lt;p:spTree&gt;</c> declares a single chart <c>&lt;p:graphicFrame&gt;</c>
    ///     referencing <paramref name="chartSpace"/> as <c>ppt/charts/chart1.xml</c>.
    /// </summary>
    private static Stream BuildChartOnlySlidePackage(XElement chartSpace)
    {
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/ppt/charts/chart1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml" />
            </Types>
            """;

        const string packageRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
            </Relationships>
            """;

        const string presentationXml =
            """
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:sldSz cx="9144000" cy="6858000"/>
              <p:sldIdLst><p:sldId id="256" r:id="rId2"/></p:sldIdLst>
            </p:presentation>
            """;

        const string presentationRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
            </Relationships>
            """;

        var slideXml =
            $"""
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cSld>
                <p:spTree>
                  <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
                  <p:grpSpPr/>
                  <p:graphicFrame>
                    <p:nvGraphicFramePr>
                      <p:cNvPr id="2" name="Chart 1"/>
                      <p:cNvGraphicFramePr/>
                      <p:nvPr/>
                    </p:nvGraphicFramePr>
                    <p:xfrm>
                      <a:off x="0" y="0"/>
                      <a:ext cx="9144000" cy="6858000"/>
                    </p:xfrm>
                    <a:graphic>
                      <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart">
                        <c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" r:id="rId2"/>
                      </a:graphicData>
                    </a:graphic>
                  </p:graphicFrame>
                </p:spTree>
              </p:cSld>
            </p:sld>
            """;

        const string slideRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" Target="../charts/chart1.xml" />
            </Relationships>
            """;

        const string layoutXml =
            """
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
            </p:sldLayout>
            """;

        const string layoutRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
            </Relationships>
            """;

        const string masterXml =
            """
            <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
              <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2"/>
              <p:txStyles/>
            </p:sldMaster>
            """;

        const string masterRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml" />
            </Relationships>
            """;

        const string themeXml =
            """
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="TestTheme">
              <a:themeElements>
                <a:clrScheme name="Test">
                  <a:dk1><a:srgbClr val="101010"/></a:dk1>
                  <a:lt1><a:srgbClr val="F0F0F0"/></a:lt1>
                  <a:dk2><a:srgbClr val="202020"/></a:dk2>
                  <a:lt2><a:srgbClr val="E0E0E0"/></a:lt2>
                  <a:accent1><a:srgbClr val="4472C4"/></a:accent1>
                  <a:accent2><a:srgbClr val="ED7D31"/></a:accent2>
                  <a:accent3><a:srgbClr val="A5A5A5"/></a:accent3>
                  <a:accent4><a:srgbClr val="FFC000"/></a:accent4>
                  <a:accent5><a:srgbClr val="5B9BD5"/></a:accent5>
                  <a:accent6><a:srgbClr val="70AD47"/></a:accent6>
                  <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
                  <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
                </a:clrScheme>
                <a:fontScheme name="TestFonts">
                  <a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
                  <a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
                </a:fontScheme>
              </a:themeElements>
            </a:theme>
            """;

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteTextEntry(archive, "_rels/.rels", packageRelsXml);
            WriteTextEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteTextEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
            WriteTextEntry(archive, "ppt/slides/slide1.xml", slideXml);
            WriteTextEntry(archive, "ppt/slides/_rels/slide1.xml.rels", slideRelsXml);
            WriteTextEntry(archive, "ppt/slideLayouts/slideLayout1.xml", layoutXml);
            WriteTextEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", layoutRelsXml);
            WriteTextEntry(archive, "ppt/slideMasters/slideMaster1.xml", masterXml);
            WriteTextEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", masterRelsXml);
            WriteTextEntry(archive, "ppt/theme/theme1.xml", themeXml);
            WriteTextEntry(archive, "ppt/charts/chart1.xml", chartSpace.ToString());
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteTextEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }
}
