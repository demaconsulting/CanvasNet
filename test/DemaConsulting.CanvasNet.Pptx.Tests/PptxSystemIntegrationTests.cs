using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx ooxml sppr nvsppr nvpr grpsp spsrc xfrm chOff chExt prstGeom srgbclr schemeclr hlink Calibri prst

/// <summary>
///     System-level integration tests for the OOXML package layer via the CanvasNet.Pptx
///     package. Each test proves a <c>CanvasNetPptx-*</c> top-level requirement end-to-end
///     through the public <see cref="PptxDocument"/> API, complementing (never replacing) the
///     unit-level <c>PptxDocument_*</c> coverage in <see cref="PptxDocumentTests"/>, which
///     exercises each feature's finer-grained behavioral variations.
/// </summary>
public class PptxSystemIntegrationTests
{
    /// <summary>
    ///     Builds a minimal, well-formed, in-memory <c>.pptx</c>-shaped ZIP package: a
    ///     <c>[Content_Types].xml</c> declaring the default <c>.rels</c>/<c>.xml</c> extensions
    ///     plus a specific override for <c>ppt/presentation.xml</c>, a package-level
    ///     <c>_rels/.rels</c> relationship to that part, and the (otherwise unparsed, this phase)
    ///     <c>ppt/presentation.xml</c> part itself - representative of the minimum a real
    ///     PowerPoint-produced <c>.pptx</c> file would always contain.
    /// </summary>
    private static Stream BuildWellFormedPresentationPackage()
    {
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml" />
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

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", packageRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }

    /// <summary>
    ///     Opens a well-formed, minimal presentation package end-to-end and proves both its
    ///     package-level relationship and its overridden part content type resolve correctly -
    ///     demonstrating the "open once, resolve package structure" property that
    ///     <see cref="PptxDocument"/>'s Phase 1a package layer exists to provide. This is also the
    ///     shared platform-proof test referenced by every <c>CanvasNetPptx-Platform-*</c>
    ///     requirement.
    /// </summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_PptxOpen_SucceedsOnWellFormedPackage()
    {
        // Arrange
        using var stream = BuildWellFormedPresentationPackage();

        // Act
        using var document = PptxDocument.Open(stream);
        var presentationPartPath = document.ResolveRelationship(string.Empty, "rId1");
        var presentationContentType = document.ResolvePart(presentationPartPath);

        // Assert
        Assert.Equal("ppt/presentation.xml", presentationPartPath);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
            presentationContentType);
    }

    /// <summary>Proves <see cref="PptxDocument.Open(Stream)"/> rejects a null stream argument end-to-end.</summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_PptxOpenValidationNull_NullStreamThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PptxDocument.Open((Stream)null!));
    }

    /// <summary>Proves <see cref="PptxDocument.Open(string)"/> rejects an empty path end-to-end.</summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_PptxOpenValidationEmptyPath_EmptyPathThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => PptxDocument.Open(string.Empty));
    }

    /// <summary>
    ///     Opens a multi-slide presentation package end-to-end and proves both
    ///     <see cref="PptxDocument.SlideCount"/> and <see cref="PptxDocument.SlideSize"/> resolve
    ///     correctly from <c>ppt/presentation.xml</c>'s <c>&lt;p:sldIdLst&gt;</c>/<c>&lt;p:sldSz&gt;</c>.
    /// </summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_PptxOpenPresentation_SlideCountAndSizeResolveEndToEnd()
    {
        // Arrange
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
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
              <p:sldIdLst>
                <p:sldId id="256" r:id="rId2"/>
                <p:sldId id="257" r:id="rId3"/>
              </p:sldIdLst>
            </p:presentation>
            """;

        const string presentationRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
              <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide2.xml" />
            </Relationships>
            """;

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", packageRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
        }

        stream.Position = 0;

        // Act
        using var document = PptxDocument.Open(stream);

        // Assert
        Assert.Equal(2, document.SlideCount);
        Assert.Equal(9144000L, document.SlideSize.WidthEmu);
        Assert.Equal(6858000L, document.SlideSize.HeightEmu);
    }

    /// <summary>
    ///     Proves opening a presentation package whose <c>&lt;p:sldIdLst&gt;</c> is empty throws
    ///     <see cref="InvalidDataException"/> end-to-end.
    /// </summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_PptxOpenValidationEmptySlideList_ThrowsInvalidDataException()
    {
        // Arrange
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
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
              <p:sldIdLst></p:sldIdLst>
            </p:presentation>
            """;

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", packageRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", presentationXml);
        }

        stream.Position = 0;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    // --- Phase 1c: geometry + paint, exercised end-to-end through the full package-load path ---

    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>
    ///     Builds a full, navigable presentation package (presentation -&gt; slide -&gt; layout -&gt;
    ///     master -&gt; theme, each with its own relationships part) whose one slide's <c>&lt;p:spTree&gt;</c>
    ///     content is supplied verbatim by <paramref name="spTreeInnerXml"/> - letting each Phase 1c
    ///     test describe only the shape(s) relevant to its scenario.
    /// </summary>
    private static Stream BuildGeometryPaintPackage(string spTreeInnerXml)
    {
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
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
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld>
                <p:spTree>
                  {spTreeInnerXml}
                </p:spTree>
              </p:cSld>
            </p:sld>
            """;

        const string slideRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
            </Relationships>
            """;

        const string layoutXml =
            """
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
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
            <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
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
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", packageRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
            WriteEntry(archive, "ppt/slides/slide1.xml", slideXml);
            WriteEntry(archive, "ppt/slides/_rels/slide1.xml.rels", slideRelsXml);
            WriteEntry(archive, "ppt/slideLayouts/slideLayout1.xml", layoutXml);
            WriteEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", layoutRelsXml);
            WriteEntry(archive, "ppt/slideMasters/slideMaster1.xml", masterXml);
            WriteEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", masterRelsXml);
            WriteEntry(archive, "ppt/theme/theme1.xml", themeXml);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    ///     Resolves the theme that governs <paramref name="document"/>'s slide at
    ///     <paramref name="slideIndex"/>, by walking its full slide -&gt; layout -&gt; master -&gt; theme
    ///     relationship chain exactly as a real renderer would - rather than constructing a
    ///     <see cref="PptxTheme"/> directly, as the unit-level <see cref="PptxPaintTests"/> do.
    /// </summary>
    private static PptxTheme ResolveSlideTheme(PptxDocument document, int slideIndex)
    {
        var slide = document.GetSlide(slideIndex);
        var layout = document.GetLayout(slide.LayoutPartPath);
        var master = document.GetMaster(layout.MasterPartPath);
        return document.GetTheme(master.ThemePartPath);
    }

    /// <summary>
    ///     Opens a full presentation package end-to-end whose one slide contains a single
    ///     freeform <c>&lt;p:sp&gt;</c> declaring a rotated <c>&lt;a:xfrm&gt;</c>, an
    ///     <c>&lt;a:prstGeom prst="roundRect"&gt;</c>, a theme-scheme-color <c>&lt;a:solidFill&gt;</c>,
    ///     and an <c>&lt;a:ln&gt;</c> stroke - proving the Phase 1c geometry and paint resolvers
    ///     compose correctly when driven from real, package-resolved <c>&lt;p:spTree&gt;</c> and
    ///     <see cref="PptxTheme"/> inputs (rather than hand-built fragments, as in
    ///     <see cref="PptxGeometryTests"/>/<see cref="PptxPaintTests"/>).
    /// </summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_GeometryAndPaint_FreeformShapeResolvesEndToEnd()
    {
        // Arrange
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="RoundedRect"/>
                <p:cNvSpPr/>
                <p:nvPr/>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm rot="5400000">
                  <a:off x="914400" y="457200"/>
                  <a:ext cx="1828800" cy="914400"/>
                </a:xfrm>
                <a:prstGeom prst="roundRect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:schemeClr val="accent1"/></a:solidFill>
                <a:ln w="25400"><a:solidFill><a:srgbClr val="000000"/></a:solidFill></a:ln>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildGeometryPaintPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        // Act
        var slidePartPath = document.GetSlide(0).PartPath;
        var spTree = document.LoadPartXmlRoot(slidePartPath).Element(P + "cSld")!.Element(P + "spTree")!;
        var spPr = spTree.Element(P + "sp")!.Element(P + "spPr")!;
        var theme = ResolveSlideTheme(document, 0);

        var frame = PptxDocument.ResolveShapeFrame(spPr.Element(A + "xfrm")!);
        var geometry = PptxDocument.ResolveShapeGeometry(spPr, frame.WidthEmu, frame.HeightEmu);
        var fill = PptxDocument.ResolveFill(spPr, theme, frame.WidthEmu, frame.HeightEmu);
        var lineStyle = PptxDocument.ResolveLineStyle(spPr.Element(A + "ln"), theme);

        // Assert
        Assert.NotEmpty(geometry.Subpaths);
        var solid = Assert.IsType<PptxSolidFill>(fill);
        Assert.Equal(theme.ColorScheme.Accent1, solid.Color);
        Assert.NotNull(lineStyle);
        Assert.Equal(25400f, lineStyle.WidthEmu);

        // The shape's own 90-degree rotation (5,400,000 sixtieths of a degree) about its own
        // center must carry through to its resolved frame transform.
        var transformedTopLeft = System.Numerics.Vector2.Transform(System.Numerics.Vector2.Zero, frame.Transform);
        var transformedCenter = System.Numerics.Vector2.Transform(
            new System.Numerics.Vector2(frame.WidthEmu / 2, frame.HeightEmu / 2), frame.Transform);
        Assert.NotEqual(transformedTopLeft, transformedCenter);
    }

    /// <summary>
    ///     Opens a full presentation package end-to-end whose one slide contains a
    ///     <c>&lt;p:grpSp&gt;</c> (with its own <c>&lt;a:xfrm&gt;</c> declaring both the group's
    ///     parent-space placement and its child coordinate space) wrapping a single child
    ///     <c>&lt;p:sp&gt;</c> - proving group child-transform composition resolves correctly
    ///     when driven through the full package-load path.
    /// </summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_GeometryAndPaint_GroupShapeChildTransformComposesEndToEnd()
    {
        // Arrange: group occupies a 100x100 EMU box at (1000,1000) in slide space, but its
        // children are authored in a 200x200 child coordinate space starting at (0,0) - every
        // child coordinate must be halved, then offset by (1000,1000), to land in slide space.
        const string spTreeInnerXml =
            """
            <p:grpSp>
              <p:nvGrpSpPr>
                <p:cNvPr id="2" name="Group"/>
                <p:cNvGrpSpPr/>
                <p:nvPr/>
              </p:nvGrpSpPr>
              <p:grpSpPr>
                <a:xfrm>
                  <a:off x="1000" y="1000"/>
                  <a:ext cx="100" cy="100"/>
                  <a:chOff x="0" y="0"/>
                  <a:chExt cx="200" cy="200"/>
                </a:xfrm>
              </p:grpSpPr>
              <p:sp>
                <p:nvSpPr>
                  <p:cNvPr id="3" name="Child"/>
                  <p:cNvSpPr/>
                  <p:nvPr/>
                </p:nvSpPr>
                <p:spPr>
                  <a:xfrm>
                    <a:off x="0" y="0"/>
                    <a:ext cx="200" cy="200"/>
                  </a:xfrm>
                  <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                  <a:noFill/>
                </p:spPr>
              </p:sp>
            </p:grpSp>
            """;
        using var stream = BuildGeometryPaintPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        // Act
        var slidePartPath = document.GetSlide(0).PartPath;
        var spTree = document.LoadPartXmlRoot(slidePartPath).Element(P + "cSld")!.Element(P + "spTree")!;
        var grpSp = spTree.Element(P + "grpSp")!;
        var groupXfrm = grpSp.Element(P + "grpSpPr")!.Element(A + "xfrm")!;
        var childSpPr = grpSp.Element(P + "sp")!.Element(P + "spPr")!;

        var groupChildTransform = PptxDocument.ResolveGroupChildTransform(groupXfrm);
        var childFrame = PptxDocument.ResolveShapeFrame(childSpPr.Element(A + "xfrm")!);
        var fullTransform = childFrame.Transform * groupChildTransform;

        // Assert: child-local (200,200) (its own bottom-right corner) -> child space (200,200) ->
        // group box (100,100) (halved) -> slide space (1100,1100) (offset by the group's own
        // placement at (1000,1000)).
        var resolved = System.Numerics.Vector2.Transform(new System.Numerics.Vector2(200, 200), fullTransform);
        Assert.Equal(1100f, resolved.X, 0.01f);
        Assert.Equal(1100f, resolved.Y, 0.01f);
    }

    // --- Phase 1d: text layout + rendering, exercised end-to-end through the full package-load path ---

    /// <summary>
    ///     Builds a full, navigable presentation package (presentation -&gt; slide -&gt; layout -&gt;
    ///     master -&gt; theme) whose master declares <c>&lt;p:txStyles&gt;</c>
    ///     (<paramref name="masterTxStylesXml"/>), whose layout declares
    ///     <paramref name="layoutPlaceholderXml"/> placeholder shape(s), and whose one slide's
    ///     <c>&lt;p:spTree&gt;</c> content is supplied verbatim by <paramref name="spTreeInnerXml"/>
    ///     - letting each Phase 1d test describe only the text-bearing shape(s) and master text
    ///     styles relevant to its scenario, exactly as <see cref="BuildGeometryPaintPackage"/> does
    ///     for Phase 1c.
    /// </summary>
    private static Stream BuildTextPackage(string spTreeInnerXml, string masterTxStylesXml, string layoutPlaceholderXml)
    {
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
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
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld>
                <p:spTree>
                  {spTreeInnerXml}
                </p:spTree>
              </p:cSld>
            </p:sld>
            """;

        const string slideRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
            </Relationships>
            """;

        var layoutXml =
            $"""
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld><p:spTree>{layoutPlaceholderXml}</p:spTree></p:cSld>
            </p:sldLayout>
            """;

        const string layoutRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
            </Relationships>
            """;

        var masterXml =
            $"""
            <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
              <p:txStyles>{masterTxStylesXml}</p:txStyles>
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
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", packageRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
            WriteEntry(archive, "ppt/slides/slide1.xml", slideXml);
            WriteEntry(archive, "ppt/slides/_rels/slide1.xml.rels", slideRelsXml);
            WriteEntry(archive, "ppt/slideLayouts/slideLayout1.xml", layoutXml);
            WriteEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", layoutRelsXml);
            WriteEntry(archive, "ppt/slideMasters/slideMaster1.xml", masterXml);
            WriteEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", masterRelsXml);
            WriteEntry(archive, "ppt/theme/theme1.xml", themeXml);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    ///     Opens a full presentation package end-to-end whose one slide contains a <c>title</c>
    ///     placeholder shape (<c>idx="1"</c>) with a <c>&lt;p:txBody&gt;</c> run declaring no font
    ///     size of its own, matched through the real slide -&gt; layout -&gt; master placeholder
    ///     chain to a layout placeholder of the same <c>idx</c> (itself declaring no <c>sz</c>
    ///     either) - proving the run's effective font size falls all the way through to the
    ///     master's own <c>&lt;p:titleStyle&gt;</c> (Phase 1d's master <c>&lt;p:txStyles&gt;</c>
    ///     bucket selected by the matched layout placeholder's <c>type="title"</c>), then that the
    ///     resulting text body lays out and paints without error.
    /// </summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_TextLayoutAndRender_TitlePlaceholderResolvesMasterTitleStyleEndToEnd()
    {
        // Arrange
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="title" idx="1"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr/>
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:t>Hello</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;
        const string layoutPlaceholderXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="title" idx="1"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr/>
            </p:sp>
            """;
        const string masterTxStylesXml =
            """
            <p:titleStyle><a:lvl1pPr><a:defRPr sz="4400"/></a:lvl1pPr></p:titleStyle>
            <p:bodyStyle><a:lvl1pPr><a:defRPr sz="1800"/></a:lvl1pPr></p:bodyStyle>
            """;
        using var stream = BuildTextPackage(spTreeInnerXml, masterTxStylesXml, layoutPlaceholderXml);
        using var document = PptxDocument.Open(stream);

        // Act
        var slide = document.GetSlide(0);
        var layout = document.GetLayout(slide.LayoutPartPath);
        var master = document.GetMaster(layout.MasterPartPath);
        var theme = document.GetTheme(master.ThemePartPath);

        var slidePlaceholder = slide.Placeholders[0];
        var placeholderProperties = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, layout.Placeholders, master.Placeholders, theme, master.TxStyles);

        var txBodyElement = slidePlaceholder.ShapeElement.Element(P + "txBody")!;
        var textBody = PptxDocument.ParseTextBody(txBodyElement);
        var paragraph = textBody.Paragraphs[0];
        var run = paragraph.Runs[0];

        var runProperties = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, slidePlaceholder.Type);

        // Assert: the master's own titleStyle sz="4400" (hundredths of a point) -> EMU.
        Assert.Equal(4400f * 127f, runProperties.SizeEmu);

        // Act: lay out and paint the resolved text body end-to-end, using a deterministic,
        // installed-font-discovery-bypassing resolver (see PptxTextLayoutTests/PptxTextRenderTests
        // for why FindBestMatch is not reliable for cross-machine test determinism).
        var textLayout = PptxDocument.ResolveTextLayout(
            textBody, placeholderProperties, theme, slidePlaceholder.Type, 2000000f, 500000f,
            (_, bold, italic) => DemaConsulting.CanvasNet.Fonts.SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold, italic));

        using var surface = new Surface(200, 200);
        var shapeTransform = System.Numerics.Matrix3x2.CreateScale(200f / 2000000f, 200f / 500000f);
        PptxDocument.PaintTextLayout(surface, textLayout, shapeTransform);

        Assert.NotEmpty(textLayout.Glyphs);
    }

    /// <summary>
    ///     Opens a full presentation package end-to-end whose one slide contains an ordinary,
    ///     non-placeholder text box (a <c>&lt;p:sp&gt;</c> with no <c>&lt;p:ph&gt;</c> descendant)
    ///     - proving the <c>placeholderType: ""</c> sentinel convention (see
    ///     <c>PptxDocument.TextInheritance.cs</c>'s <c>SelectMasterTextStyle</c>) correctly selects
    ///     the master's <c>&lt;p:otherStyle&gt;</c> bucket, not <c>&lt;p:bodyStyle&gt;</c>, when
    ///     driven through a real, non-placeholder shape resolved from the full package-load path
    ///     (not a hand-built <see cref="PptxTextTests"/> fixture).
    /// </summary>
    [Fact]
    public void CanvasNetPptx_SystemIntegration_TextLayoutAndRender_NonPlaceholderShapeResolvesMasterOtherStyleEndToEnd()
    {
        // Arrange
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="TextBox 1"/>
                <p:cNvSpPr txBox="1"/>
                <p:nvPr/>
              </p:nvSpPr>
              <p:spPr/>
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:t>Plain text</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;
        const string masterTxStylesXml =
            """
            <p:bodyStyle><a:lvl1pPr><a:defRPr sz="1800"/></a:lvl1pPr></p:bodyStyle>
            <p:otherStyle><a:lvl1pPr><a:defRPr sz="1200"/></a:lvl1pPr></p:otherStyle>
            """;
        using var stream = BuildTextPackage(spTreeInnerXml, masterTxStylesXml, layoutPlaceholderXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        // Act
        var slide = document.GetSlide(0);
        var layout = document.GetLayout(slide.LayoutPartPath);
        var master = document.GetMaster(layout.MasterPartPath);
        var theme = document.GetTheme(master.ThemePartPath);

        var spTree = document.LoadPartXmlRoot(slide.PartPath).Element(P + "cSld")!.Element(P + "spTree")!;
        var shapeElement = spTree.Element(P + "sp")!;
        var txBodyElement = shapeElement.Element(P + "txBody")!;
        var textBody = PptxDocument.ParseTextBody(txBodyElement);
        var paragraph = textBody.Paragraphs[0];
        var run = paragraph.Runs[0];

        // A non-placeholder shape has no PptxPlaceholder/slide-layout-master match to walk, so
        // its PptxPlaceholderProperties carries only the theme/master text styles through,
        // exactly as a real renderer would construct for such a shape.
        var placeholderProperties = new PptxPlaceholderProperties(null, null, theme, master.TxStyles);

        var runProperties = PptxDocument.ResolveEffectiveRunProperties(run, paragraph, placeholderProperties, theme, string.Empty);

        // Assert: the master's own otherStyle sz="1200" wins, not bodyStyle's sz="1800".
        Assert.Equal(1200f * 127f, runProperties.SizeEmu);
    }
}
