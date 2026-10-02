using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx ooxml sppr txbody lststyle sldsz sldidlst sldid nvsppr nvpr ctrtitle srgb hlink calibri

/// <summary>
///     Unit-level tests for the <see cref="PptxDocument"/> OOXML package layer (Phase 1a: ZIP
///     opening, <c>[Content_Types].xml</c> resolution, package/part relationship resolution) and
///     the Phase 1b presentation/theme/master/layout/slide model and placeholder-inheritance
///     resolver. Complements <see cref="PptxSystemIntegrationTests"/>, which proves the same
///     behaviors end-to-end through the public <see cref="PptxDocument.Open(Stream)"/>/
///     <see cref="PptxDocument.Open(string)"/> API only.
/// </summary>
public class PptxDocumentTests
{
    private const string DefaultContentTypesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
        </Types>
        """;

    private const string DefaultPackageRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
        </Relationships>
        """;

    /// <summary>
    ///     A minimal, valid <c>ppt/presentation.xml</c> content (one slide, a valid
    ///     <c>&lt;p:sldSz&gt;</c>) reused by Phase 1a tests that need <see cref="PptxDocument.Open(Stream)"/>
    ///     to succeed but are not themselves testing presentation parsing.
    /// </summary>
    private const string DefaultPresentationXml =
        """
        <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <p:sldSz cx="9144000" cy="6858000"/>
          <p:sldIdLst><p:sldId id="256" r:id="rId2"/></p:sldIdLst>
        </p:presentation>
        """;

    /// <summary>
    ///     <c>ppt/_rels/presentation.xml.rels</c> content resolving <see cref="DefaultPresentationXml"/>'s
    ///     one declared slide ID (<c>rId2</c>) to <c>ppt/slides/slide1.xml</c>.
    /// </summary>
    private const string DefaultPresentationRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
        </Relationships>
        """;

    /// <summary>
    ///     Builds an in-memory, OPC-shaped <c>.pptx</c> ZIP package via <see cref="ZipArchive"/>,
    ///     with each supplied <paramref name="entries"/> name/content pair written verbatim as a
    ///     ZIP entry. Used by every test in this class so each test only needs to describe the
    ///     handful of entries relevant to the scenario under test, rather than a complete,
    ///     feature-complete presentation package.
    /// </summary>
    private static Stream BuildPackage(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>Builds a minimal, valid package: content types, package-level rels, and a minimal navigable presentation part.</summary>
    private static Stream BuildMinimalValidPackage() =>
        BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("ppt/presentation.xml", DefaultPresentationXml),
            ("ppt/_rels/presentation.xml.rels", DefaultPresentationRelsXml));

    // --- Phase 1b fixtures: presentation/theme/master/layout/slide model -------------------

    private const string PresentationNamespaceUri = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private const string RelationshipRefNamespaceUri = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly XNamespace PresentationNs = PresentationNamespaceUri;
    private static readonly XNamespace DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";

    private const string ValidSldSz = """<p:sldSz cx="9144000" cy="6858000"/>""";

    /// <summary>A package-level rels part whose single relationship's Type ends "/officeDocument".</summary>
    private const string OfficeDocumentPackageRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
        </Relationships>
        """;

    private const string ThemeXmlAllSrgb =
        """
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="TestTheme">
          <a:themeElements>
            <a:clrScheme name="Test">
              <a:dk1><a:srgbClr val="010101"/></a:dk1>
              <a:lt1><a:srgbClr val="020202"/></a:lt1>
              <a:dk2><a:srgbClr val="030303"/></a:dk2>
              <a:lt2><a:srgbClr val="040404"/></a:lt2>
              <a:accent1><a:srgbClr val="050505"/></a:accent1>
              <a:accent2><a:srgbClr val="060606"/></a:accent2>
              <a:accent3><a:srgbClr val="070707"/></a:accent3>
              <a:accent4><a:srgbClr val="080808"/></a:accent4>
              <a:accent5><a:srgbClr val="090909"/></a:accent5>
              <a:accent6><a:srgbClr val="0a0a0a"/></a:accent6>
              <a:hlink><a:srgbClr val="0b0b0b"/></a:hlink>
              <a:folHlink><a:srgbClr val="0c0c0c"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="TestFonts">
              <a:majorFont>
                <a:latin typeface="Calibri Light"/>
                <a:ea typeface="MajorEA"/>
                <a:cs typeface="MajorCS"/>
              </a:majorFont>
              <a:minorFont>
                <a:latin typeface="Calibri"/>
                <a:ea typeface="MinorEA"/>
                <a:cs typeface="MinorCS"/>
              </a:minorFont>
            </a:fontScheme>
          </a:themeElements>
        </a:theme>
        """;

    private const string ThemeXmlWithSysClr =
        """
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="TestTheme">
          <a:themeElements>
            <a:clrScheme name="Test">
              <a:dk1><a:sysClr val="windowText" lastClr="112233"/></a:dk1>
              <a:lt1><a:srgbClr val="020202"/></a:lt1>
              <a:dk2><a:srgbClr val="030303"/></a:dk2>
              <a:lt2><a:srgbClr val="040404"/></a:lt2>
              <a:accent1><a:srgbClr val="050505"/></a:accent1>
              <a:accent2><a:srgbClr val="060606"/></a:accent2>
              <a:accent3><a:srgbClr val="070707"/></a:accent3>
              <a:accent4><a:srgbClr val="080808"/></a:accent4>
              <a:accent5><a:srgbClr val="090909"/></a:accent5>
              <a:accent6><a:srgbClr val="0a0a0a"/></a:accent6>
              <a:hlink><a:srgbClr val="0b0b0b"/></a:hlink>
              <a:folHlink><a:srgbClr val="0c0c0c"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="TestFonts">
              <a:majorFont>
                <a:latin typeface="Calibri Light"/>
                <a:ea typeface="MajorEA"/>
                <a:cs typeface="MajorCS"/>
              </a:majorFont>
              <a:minorFont>
                <a:latin typeface="Calibri"/>
                <a:ea typeface="MinorEA"/>
                <a:cs typeface="MinorCS"/>
              </a:minorFont>
            </a:fontScheme>
          </a:themeElements>
        </a:theme>
        """;

    private const string MasterXml =
        """
        <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
          <p:cSld>
            <p:spTree>
              <p:sp>
                <p:nvSpPr>
                  <p:cNvPr id="2" name="Title Placeholder"/>
                  <p:cNvSpPr/>
                  <p:nvPr><p:ph type="title"/></p:nvPr>
                </p:nvSpPr>
                <p:spPr/>
              </p:sp>
              <p:sp>
                <p:nvSpPr>
                  <p:cNvPr id="3" name="Body Placeholder"/>
                  <p:cNvSpPr/>
                  <p:nvPr><p:ph type="body" idx="1"/></p:nvPr>
                </p:nvSpPr>
                <p:spPr/>
              </p:sp>
            </p:spTree>
          </p:cSld>
        </p:sldMaster>
        """;

    private const string MasterRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml" />
        </Relationships>
        """;

    private const string LayoutXmlDefaultPlaceholder =
        """
        <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
          <p:cSld>
            <p:spTree>
              <p:sp>
                <p:nvSpPr>
                  <p:cNvPr id="2" name="Placeholder"/>
                  <p:cNvSpPr/>
                  <p:nvPr><p:ph/></p:nvPr>
                </p:nvSpPr>
              </p:sp>
            </p:spTree>
          </p:cSld>
        </p:sldLayout>
        """;

    private const string LayoutRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
        </Relationships>
        """;

    private const string SlideXmlWithPlaceholderAndFreeform =
        """
        <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
          <p:cSld>
            <p:spTree>
              <p:sp>
                <p:nvSpPr>
                  <p:cNvPr id="2" name="Title"/>
                  <p:cNvSpPr/>
                  <p:nvPr><p:ph type="title"/></p:nvPr>
                </p:nvSpPr>
              </p:sp>
              <p:sp>
                <p:nvSpPr>
                  <p:cNvPr id="3" name="FreeformShape"/>
                  <p:cNvSpPr/>
                  <p:nvPr/>
                </p:nvSpPr>
              </p:sp>
            </p:spTree>
          </p:cSld>
        </p:sld>
        """;

    private const string SlideRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
        </Relationships>
        """;

    /// <summary>
    ///     Builds <c>ppt/presentation.xml</c>'s content declaring <paramref name="sldSzElement"/>
    ///     (or no <c>&lt;p:sldSz&gt;</c> element at all, when null) and an ordered
    ///     <c>&lt;p:sldIdLst&gt;</c> of <paramref name="slideCount"/> slides referencing
    ///     <c>rId2</c>, <c>rId3</c>, ... in order (<c>rId1</c> is reserved, matching real
    ///     PowerPoint convention, for the master/other non-slide relationship slot).
    /// </summary>
    private static string BuildPresentationXml(string? sldSzElement, int slideCount)
    {
        var sb = new StringBuilder();
        sb.Append($"""<p:presentation xmlns:p="{PresentationNamespaceUri}" xmlns:r="{RelationshipRefNamespaceUri}">""");
        if (sldSzElement is not null)
        {
            sb.Append(sldSzElement);
        }

        sb.Append("<p:sldIdLst>");
        for (var i = 0; i < slideCount; i++)
        {
            sb.Append($"""<p:sldId id="{256 + i}" r:id="rId{2 + i}"/>""");
        }

        sb.Append("</p:sldIdLst></p:presentation>");
        return sb.ToString();
    }

    /// <summary>
    ///     Builds <c>ppt/presentation.xml</c>'s content with <paramref name="sldSzElement"/> and an
    ///     empty <c>&lt;p:sldIdLst&gt;</c> (used by the "empty slide list" scenario).
    /// </summary>
    private static string BuildPresentationXmlEmptySlideList(string sldSzElement) =>
        $"""<p:presentation xmlns:p="{PresentationNamespaceUri}" xmlns:r="{RelationshipRefNamespaceUri}">{sldSzElement}<p:sldIdLst></p:sldIdLst></p:presentation>""";

    /// <summary>
    ///     Builds <c>ppt/_rels/presentation.xml.rels</c>'s content mapping <c>rId2</c>, <c>rId3</c>,
    ///     ... (one per slide) to <c>slides/slideN.xml</c> targets, matching
    ///     <see cref="BuildPresentationXml"/>'s slide ID numbering.
    /// </summary>
    private static string BuildPresentationRelsXml(int slideCount)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (var i = 0; i < slideCount; i++)
        {
            sb.Append($"""<Relationship Id="rId{2 + i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide{i + 1}.xml" />""");
        }

        sb.Append("</Relationships>");
        return sb.ToString();
    }

    /// <summary>
    ///     Builds the minimal set of entries needed for <see cref="PptxDocument.Open(Stream)"/> to
    ///     succeed as a navigable, single-slide presentation (content types, package-root
    ///     <c>/officeDocument</c> relationship, a valid <c>ppt/presentation.xml</c>, and its own
    ///     rels resolving its one declared slide ID) - the actual slide part itself is
    ///     deliberately NOT included, since most Phase 1b tests only need <c>Open</c> to succeed,
    ///     not a fully navigable slide/layout/master/theme chain (each test adds only the specific
    ///     additional parts its own scenario needs).
    /// </summary>
    private static (string Name, string Content)[] MinimalOpenableEntries() =>
    [
        ("[Content_Types].xml", DefaultContentTypesXml),
        ("_rels/.rels", OfficeDocumentPackageRelsXml),
        ("ppt/presentation.xml", BuildPresentationXml(ValidSldSz, 1)),
        ("ppt/_rels/presentation.xml.rels", BuildPresentationRelsXml(1)),
    ];

    /// <summary>Builds a test placeholder shape (<c>&lt;p:sp&gt;</c>) with optional marker-tagged <c>&lt;p:spPr&gt;</c>/<c>&lt;a:lstStyle&gt;</c> children.</summary>
    private static XElement BuildShapeElement(string? spPrMarker = null, string? lstStyleMarker = null)
    {
        var sp = new XElement(PresentationNs + "sp");
        if (spPrMarker is not null)
        {
            sp.Add(new XElement(PresentationNs + "spPr", new XAttribute("marker", spPrMarker)));
        }

        if (lstStyleMarker is not null)
        {
            sp.Add(new XElement(
                PresentationNs + "txBody",
                new XElement(DrawingNs + "lstStyle", new XAttribute("marker", lstStyleMarker))));
        }

        return sp;
    }

    /// <summary>Builds an arbitrary, fully-populated test <see cref="PptxTheme"/> for inheritance-resolver tests.</summary>
    private static PptxTheme BuildTestTheme() =>
        new(
            new PptxColorScheme(
                new Rgba32(1, 1, 1, 255), new Rgba32(2, 2, 2, 255), new Rgba32(3, 3, 3, 255), new Rgba32(4, 4, 4, 255),
                new Rgba32(5, 5, 5, 255), new Rgba32(6, 6, 6, 255), new Rgba32(7, 7, 7, 255), new Rgba32(8, 8, 8, 255),
                new Rgba32(9, 9, 9, 255), new Rgba32(10, 10, 10, 255), new Rgba32(11, 11, 11, 255), new Rgba32(12, 12, 12, 255)),
            new PptxFontScheme(
                new PptxFontCollection("MajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("MinorLatin", "MinorEA", "MinorCS")));

    /// <summary>Proves a minimal, well-formed OPC package opens successfully.</summary>
    [Fact]
    public void PptxDocument_Open_WellFormedMinimalPackage_Succeeds()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage();

        // Act
        using var document = PptxDocument.Open(stream);

        // Assert: construction completed without throwing, and the instance resolves the one
        // part declared in the package-level rels.
        Assert.Equal("ppt/presentation.xml", document.ResolveRelationship(string.Empty, "rId1"));
    }

    /// <summary>Proves a package missing <c>[Content_Types].xml</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_Open_MissingContentTypes_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildPackage(("_rels/.rels", DefaultPackageRelsXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    /// <summary>Proves a package missing the package-level <c>_rels/.rels</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_Open_MissingPackageRelationships_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildPackage(("[Content_Types].xml", DefaultContentTypesXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    /// <summary>Proves a corrupt/non-ZIP stream throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_Open_CorruptNonZipStream_ThrowsInvalidDataException()
    {
        // Arrange: a stream of arbitrary bytes, not a ZIP local-file-header signature.
        using var stream = new MemoryStream([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    /// <summary>
    ///     Proves <see cref="PptxDocument.ResolveRelationship"/> follows a relative target path
    ///     including a <c>"../"</c> traversal segment, resolved relative to the source part's own
    ///     directory (not the package root).
    /// </summary>
    [Fact]
    public void PptxDocument_ResolveRelationship_RelativeTargetWithParentTraversal_ResolvesCorrectTarget()
    {
        // Arrange: ppt/slides/slide1.xml has a relationship to "../slideLayouts/slideLayout1.xml",
        // which must resolve to "ppt/slideLayouts/slideLayout1.xml" (not "ppt/slides/slideLayouts/...").
        const string slideRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://example.com/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
            </Relationships>
            """;

        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("ppt/presentation.xml", DefaultPresentationXml),
            ("ppt/_rels/presentation.xml.rels", DefaultPresentationRelsXml),
            ("ppt/slides/_rels/slide1.xml.rels", slideRelsXml));
        using var document = PptxDocument.Open(stream);

        // Act
        var resolved = document.ResolveRelationship("ppt/slides/slide1.xml", "rId1");

        // Assert
        Assert.Equal("ppt/slideLayouts/slideLayout1.xml", resolved);
    }

    /// <summary>
    ///     Proves a relationship that would traverse past the package root (more <c>".."</c>
    ///     segments than preceding directories) throws <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void PptxDocument_ResolveRelationship_TargetEscapesPackageRoot_ThrowsInvalidDataException()
    {
        // Arrange
        const string slideRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://example.com/escape" Target="../../outside.xml" />
            </Relationships>
            """;

        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("ppt/presentation.xml", DefaultPresentationXml),
            ("ppt/_rels/presentation.xml.rels", DefaultPresentationRelsXml),
            ("ppt/_rels/slide1.xml.rels", slideRelsXml));
        using var document = PptxDocument.Open(stream);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => document.ResolveRelationship("ppt/slide1.xml", "rId1"));
    }

    /// <summary>
    ///     Proves <see cref="PptxDocument.ResolveRelationship"/> throws
    ///     <see cref="InvalidDataException"/> when the requested relationship ID is not present.
    /// </summary>
    [Fact]
    public void PptxDocument_ResolveRelationship_UnknownRelationshipId_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage();
        using var document = PptxDocument.Open(stream);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => document.ResolveRelationship(string.Empty, "rIdMissing"));
    }

    /// <summary>
    ///     Proves a part-specific <c>&lt;Override&gt;</c> content-type mapping in
    ///     <c>[Content_Types].xml</c> takes precedence over the default extension-based mapping
    ///     for the same part.
    /// </summary>
    [Fact]
    public void PptxDocument_ResolvePart_OverrideContentType_TakesPrecedenceOverDefaultExtension()
    {
        // Arrange: every ".xml" part defaults to "application/xml", but this package declares a
        // specific override for "ppt/presentation.xml" naming its real OOXML content type.
        const string contentTypesWithOverrideXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml" />
            </Types>
            """;

        using var stream = BuildPackage(
            ("[Content_Types].xml", contentTypesWithOverrideXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("ppt/presentation.xml", DefaultPresentationXml),
            ("ppt/_rels/presentation.xml.rels", DefaultPresentationRelsXml));
        using var document = PptxDocument.Open(stream);

        // Act
        var presentationContentType = document.ResolvePart("ppt/presentation.xml");

        // Assert
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
            presentationContentType);
    }

    /// <summary>Proves a plain, non-overridden part resolves via the default extension mapping.</summary>
    [Fact]
    public void PptxDocument_ResolvePart_NoOverride_ResolvesDefaultExtensionContentType()
    {
        // Arrange
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("ppt/presentation.xml", DefaultPresentationXml),
            ("ppt/_rels/presentation.xml.rels", DefaultPresentationRelsXml));
        using var document = PptxDocument.Open(stream);

        // Act
        var contentType = document.ResolvePart("ppt/presentation.xml");

        // Assert
        Assert.Equal("application/xml", contentType);
    }

    /// <summary>Proves resolving a part not present in the package throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_ResolvePart_UnknownPart_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage();
        using var document = PptxDocument.Open(stream);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => document.ResolvePart("ppt/missing.xml"));
    }

    /// <summary>Proves <see cref="PptxDocument.Open(Stream)"/> rejects a null stream.</summary>
    [Fact]
    public void PptxDocument_Open_NullStream_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PptxDocument.Open((Stream)null!));
    }

    /// <summary>Proves <see cref="PptxDocument.Open(string)"/> rejects a null path.</summary>
    [Fact]
    public void PptxDocument_Open_NullPath_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PptxDocument.Open((string)null!));
    }

    /// <summary>Proves <see cref="PptxDocument.Open(string)"/> rejects an empty/whitespace-only path.</summary>
    [Fact]
    public void PptxDocument_Open_WhitespacePath_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => PptxDocument.Open("   "));
    }

    /// <summary>Proves calling <see cref="PptxDocument.Dispose"/> more than once has no ill effect.</summary>
    [Fact]
    public void PptxDocument_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage();
        var document = PptxDocument.Open(stream);

        // Act
        document.Dispose();
        var exception = Record.Exception(document.Dispose);

        // Assert
        Assert.Null(exception);
    }

    // --- Phase 1b: presentation parsing / slide size / slide count -------------------------

    /// <summary>Proves a multi-slide presentation resolves <see cref="PptxDocument.SlideCount"/>/<see cref="PptxDocument.SlideSize"/> correctly.</summary>
    [Fact]
    public void PptxDocument_Open_MultiSlidePresentation_SlideCountAndSlideSizeResolveCorrectly()
    {
        // Arrange
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", OfficeDocumentPackageRelsXml),
            ("ppt/presentation.xml", BuildPresentationXml(ValidSldSz, 3)),
            ("ppt/_rels/presentation.xml.rels", BuildPresentationRelsXml(3)));

        // Act
        using var document = PptxDocument.Open(stream);

        // Assert
        Assert.Equal(3, document.SlideCount);
        Assert.Equal(9144000L, document.SlideSize.WidthEmu);
        Assert.Equal(6858000L, document.SlideSize.HeightEmu);
    }

    /// <summary>Proves a package with no <c>/officeDocument</c> relationship throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_Open_MissingOfficeDocumentRelationship_ThrowsInvalidDataException()
    {
        // Arrange: the package-level rels declares a relationship, but its Type does not end
        // with "/officeDocument".
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    /// <summary>Proves a presentation part missing <c>&lt;p:sldSz&gt;</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_Open_PresentationMissingSldSz_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", OfficeDocumentPackageRelsXml),
            ("ppt/presentation.xml", BuildPresentationXml(null, 1)),
            ("ppt/_rels/presentation.xml.rels", BuildPresentationRelsXml(1)));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    /// <summary>Proves a presentation declaring an empty <c>&lt;p:sldIdLst&gt;</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_Open_PresentationEmptySlideList_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", OfficeDocumentPackageRelsXml),
            ("ppt/presentation.xml", BuildPresentationXmlEmptySlideList(ValidSldSz)));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    /// <summary>Proves a presentation with a non-numeric <c>&lt;p:sldSz&gt;</c> attribute throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PptxDocument_Open_PresentationNonNumericSldSz_ThrowsInvalidDataException()
    {
        // Arrange
        const string invalidSldSz = """<p:sldSz cx="abc" cy="6858000"/>""";
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", OfficeDocumentPackageRelsXml),
            ("ppt/presentation.xml", BuildPresentationXml(invalidSldSz, 1)),
            ("ppt/_rels/presentation.xml.rels", BuildPresentationRelsXml(1)));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PptxDocument.Open(stream));
    }

    /// <summary>Proves <see cref="PptxDocument.GetSlideSizeInPixels"/> computes the expected pixel size at a known DPI.</summary>
    [Fact]
    public void PptxDocument_GetSlideSizeInPixels_ValidDpi_ComputesExpectedPixelSize()
    {
        // Arrange: 9144000 EMU = 10in, 6858000 EMU = 7.5in; at 96 DPI that is exactly 960x720px.
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", OfficeDocumentPackageRelsXml),
            ("ppt/presentation.xml", BuildPresentationXml(ValidSldSz, 1)),
            ("ppt/_rels/presentation.xml.rels", BuildPresentationRelsXml(1)));
        using var document = PptxDocument.Open(stream);

        // Act
        var (width, height) = document.GetSlideSizeInPixels(96f);

        // Assert
        Assert.Equal(960, width);
        Assert.Equal(720, height);
    }

    /// <summary>Proves <see cref="PptxDocument.GetSlideSizeInPixels"/> rejects a non-positive/non-finite DPI.</summary>
    [Fact]
    public void PptxDocument_GetSlideSizeInPixels_NonPositiveDpi_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var stream = BuildPackage(MinimalOpenableEntries());
        using var document = PptxDocument.Open(stream);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetSlideSizeInPixels(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetSlideSizeInPixels(float.NaN));
    }

    // --- Phase 1b: theme parsing -------------------------------------------------------------

    /// <summary>Proves <c>&lt;a:srgbClr&gt;</c> color scheme slots resolve to the expected <see cref="Rgba32"/> values.</summary>
    [Fact]
    public void PptxDocument_GetTheme_SrgbClrColorScheme_ResolvesRgba32Values()
    {
        // Arrange
        using var stream = BuildPackage(
            [.. MinimalOpenableEntries(), ("ppt/theme/theme1.xml", ThemeXmlAllSrgb)]);
        using var document = PptxDocument.Open(stream);

        // Act
        var theme = document.GetTheme("ppt/theme/theme1.xml");

        // Assert
        Assert.Equal(new Rgba32(0x01, 0x01, 0x01, 255), theme.ColorScheme.Dark1);
        Assert.Equal(new Rgba32(0x02, 0x02, 0x02, 255), theme.ColorScheme.Light1);
        Assert.Equal(new Rgba32(0x03, 0x03, 0x03, 255), theme.ColorScheme.Dark2);
        Assert.Equal(new Rgba32(0x04, 0x04, 0x04, 255), theme.ColorScheme.Light2);
        Assert.Equal(new Rgba32(0x05, 0x05, 0x05, 255), theme.ColorScheme.Accent1);
        Assert.Equal(new Rgba32(0x06, 0x06, 0x06, 255), theme.ColorScheme.Accent2);
        Assert.Equal(new Rgba32(0x07, 0x07, 0x07, 255), theme.ColorScheme.Accent3);
        Assert.Equal(new Rgba32(0x08, 0x08, 0x08, 255), theme.ColorScheme.Accent4);
        Assert.Equal(new Rgba32(0x09, 0x09, 0x09, 255), theme.ColorScheme.Accent5);
        Assert.Equal(new Rgba32(0x0a, 0x0a, 0x0a, 255), theme.ColorScheme.Accent6);
        Assert.Equal(new Rgba32(0x0b, 0x0b, 0x0b, 255), theme.ColorScheme.Hyperlink);
        Assert.Equal(new Rgba32(0x0c, 0x0c, 0x0c, 255), theme.ColorScheme.FollowedHyperlink);
    }

    /// <summary>Proves an <c>&lt;a:sysClr&gt;</c> color scheme slot resolves via its cached <c>lastClr</c> value.</summary>
    [Fact]
    public void PptxDocument_GetTheme_SysClrColorScheme_ResolvesLastClrValue()
    {
        // Arrange
        using var stream = BuildPackage(
            [.. MinimalOpenableEntries(), ("ppt/theme/theme1.xml", ThemeXmlWithSysClr)]);
        using var document = PptxDocument.Open(stream);

        // Act
        var theme = document.GetTheme("ppt/theme/theme1.xml");

        // Assert
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 255), theme.ColorScheme.Dark1);
    }

    /// <summary>Proves the font scheme's major/minor typefaces (Latin/East Asian/complex script) resolve correctly.</summary>
    [Fact]
    public void PptxDocument_GetTheme_FontScheme_ResolvesMajorMinorTypefaces()
    {
        // Arrange
        using var stream = BuildPackage(
            [.. MinimalOpenableEntries(), ("ppt/theme/theme1.xml", ThemeXmlAllSrgb)]);
        using var document = PptxDocument.Open(stream);

        // Act
        var theme = document.GetTheme("ppt/theme/theme1.xml");

        // Assert
        Assert.Equal("Calibri Light", theme.FontScheme.MajorFont.Latin);
        Assert.Equal("MajorEA", theme.FontScheme.MajorFont.EastAsian);
        Assert.Equal("MajorCS", theme.FontScheme.MajorFont.ComplexScript);
        Assert.Equal("Calibri", theme.FontScheme.MinorFont.Latin);
        Assert.Equal("MinorEA", theme.FontScheme.MinorFont.EastAsian);
        Assert.Equal("MinorCS", theme.FontScheme.MinorFont.ComplexScript);
    }

    // --- Phase 1b: master/layout/slide structural parsing -----------------------------------

    /// <summary>Proves a slide master's placeholder shapes parse with their explicit <c>type</c>/<c>idx</c> attributes.</summary>
    [Fact]
    public void PptxDocument_GetMaster_PlaceholderShapes_ParsedWithTypeAndIdx()
    {
        // Arrange
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slideMasters/slideMaster1.xml", MasterXml),
                ("ppt/slideMasters/_rels/slideMaster1.xml.rels", MasterRelsXml),
            ]);
        using var document = PptxDocument.Open(stream);

        // Act
        var master = document.GetMaster("ppt/slideMasters/slideMaster1.xml");

        // Assert
        Assert.Equal(2, master.Placeholders.Count);
        Assert.Equal("title", master.Placeholders[0].Type);
        Assert.Equal(0u, master.Placeholders[0].Idx);
        Assert.Equal("body", master.Placeholders[1].Type);
        Assert.Equal(1u, master.Placeholders[1].Idx);
        Assert.Equal("ppt/theme/theme1.xml", master.ThemePartPath);
    }

    /// <summary>Proves a slide layout's <c>&lt;p:ph/&gt;</c> with no attributes defaults to type <c>"obj"</c>/idx <c>0</c>.</summary>
    [Fact]
    public void PptxDocument_GetLayout_PlaceholderShapes_ParsedWithDefaultTypeAndIdx()
    {
        // Arrange
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slideLayouts/slideLayout1.xml", LayoutXmlDefaultPlaceholder),
                ("ppt/slideLayouts/_rels/slideLayout1.xml.rels", LayoutRelsXml),
            ]);
        using var document = PptxDocument.Open(stream);

        // Act
        var layout = document.GetLayout("ppt/slideLayouts/slideLayout1.xml");

        // Assert
        var placeholder = Assert.Single(layout.Placeholders);
        Assert.Equal("obj", placeholder.Type);
        Assert.Equal(0u, placeholder.Idx);
        Assert.Equal("ppt/slideMasters/slideMaster1.xml", layout.MasterPartPath);
    }

    /// <summary>Proves a slide's immediate placeholder shapes parse structurally, excluding a non-placeholder shape.</summary>
    [Fact]
    public void PptxDocument_GetSlide_ImmediatePlaceholderShapes_ParsedStructurally()
    {
        // Arrange
        using var stream = BuildPackage(
            [
                .. MinimalOpenableEntries(),
                ("ppt/slides/slide1.xml", SlideXmlWithPlaceholderAndFreeform),
                ("ppt/slides/_rels/slide1.xml.rels", SlideRelsXml),
            ]);
        using var document = PptxDocument.Open(stream);

        // Act
        var slide = document.GetSlide(0);

        // Assert: the "FreeformShape" <p:sp> (no <p:ph>) is excluded.
        var placeholder = Assert.Single(slide.Placeholders);
        Assert.Equal("title", placeholder.Type);
        Assert.Equal("ppt/slideLayouts/slideLayout1.xml", slide.LayoutPartPath);
    }

    /// <summary>Proves <see cref="PptxDocument.GetSlide"/> rejects an out-of-range slide index.</summary>
    [Fact]
    public void PptxDocument_GetSlide_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var stream = BuildPackage(MinimalOpenableEntries());
        using var document = PptxDocument.Open(stream);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetSlide(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetSlide(document.SlideCount));
    }

    // --- Phase 1b: placeholder property-inheritance resolver --------------------------------

    /// <summary>Proves an idx match at the layout level resolves the effective properties from the matched layout placeholder.</summary>
    [Fact]
    public void PptxDocumentInheritance_ResolvePlaceholderProperties_IdxMatchAtLayout_UsesLayoutSpPr()
    {
        // Arrange
        var theme = BuildTestTheme();
        var slidePlaceholder = new PptxPlaceholder("body", 5, BuildShapeElement());
        var layoutPlaceholder = new PptxPlaceholder("subTitle", 5, BuildShapeElement("layout", "layout"));
        var masterPlaceholder = new PptxPlaceholder("body", 0, BuildShapeElement("master", "master"));

        // Act
        var result = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, [layoutPlaceholder], [masterPlaceholder], theme);

        // Assert
        Assert.Equal("layout", (string?)result.EffectiveSpPr?.Attribute("marker"));
        Assert.Equal("layout", (string?)result.EffectiveTxBodyListStyle?.Attribute("marker"));
        Assert.Same(theme, result.Theme);
    }

    /// <summary>Proves a hop-1 (idx) miss at the layout level short-circuits the chain - hop 2 (master) is never consulted.</summary>
    [Fact]
    public void PptxDocumentInheritance_ResolvePlaceholderProperties_NoIdxMatchAtLayout_FallsThroughToMasterOnly()
    {
        // Arrange: layout's idx (5) does not match the slide placeholder's idx (7), so no layout
        // match is found - the master (type "body", which would match the slide's own type) must
        // NOT be consulted, per the verified algorithm's documented short-circuit.
        var theme = BuildTestTheme();
        var slidePlaceholder = new PptxPlaceholder("body", 7, BuildShapeElement("slide", null));
        var layoutPlaceholder = new PptxPlaceholder("body", 5, BuildShapeElement());
        var masterPlaceholder = new PptxPlaceholder("body", 0, BuildShapeElement("master", "master"));

        // Act
        var result = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, [layoutPlaceholder], [masterPlaceholder], theme);

        // Assert
        Assert.Equal("slide", (string?)result.EffectiveSpPr?.Attribute("marker"));
        Assert.Null(result.EffectiveTxBodyListStyle);
    }

    /// <summary>Proves the layout-&gt;master hop matches by type alone, ignoring a mismatched idx.</summary>
    [Fact]
    public void PptxDocumentInheritance_ResolvePlaceholderProperties_TypeMatchAtMaster_IgnoresMismatchedIdx()
    {
        // Arrange: master's idx (99) deliberately differs from the matched layout placeholder's
        // idx (10) - the type match ("dt") must still succeed.
        var theme = BuildTestTheme();
        var slidePlaceholder = new PptxPlaceholder("dt", 10, BuildShapeElement());
        var layoutPlaceholder = new PptxPlaceholder("dt", 10, BuildShapeElement());
        var masterPlaceholder = new PptxPlaceholder("dt", 99, BuildShapeElement("master", null));

        // Act
        var result = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, [layoutPlaceholder], [masterPlaceholder], theme);

        // Assert
        Assert.Equal("master", (string?)result.EffectiveSpPr?.Attribute("marker"));
    }

    /// <summary>Proves a remapped body-like layout type (<c>"subTitle"</c>) matches a master <c>"body"</c> placeholder.</summary>
    [Fact]
    public void PptxDocumentInheritance_ResolvePlaceholderProperties_RemappedBodyLikeType_MatchesMasterBodyPlaceholder()
    {
        // Arrange
        var theme = BuildTestTheme();
        var slidePlaceholder = new PptxPlaceholder("subTitle", 3, BuildShapeElement());
        var layoutPlaceholder = new PptxPlaceholder("subTitle", 3, BuildShapeElement());
        var masterPlaceholder = new PptxPlaceholder("body", 0, BuildShapeElement("master", null));

        // Act
        var result = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, [layoutPlaceholder], [masterPlaceholder], theme);

        // Assert
        Assert.Equal("master", (string?)result.EffectiveSpPr?.Attribute("marker"));
    }

    /// <summary>Proves each property category falls through independently: slide overrides fill but not text.</summary>
    [Fact]
    public void PptxDocumentInheritance_ResolvePlaceholderProperties_PerCategoryIndependentFallthrough_SlideOverridesFillButNotText()
    {
        // Arrange
        var theme = BuildTestTheme();
        var slidePlaceholder = new PptxPlaceholder("body", 1, BuildShapeElement("slide", null));
        var layoutPlaceholder = new PptxPlaceholder("body", 1, BuildShapeElement("layout", "layout"));
        var masterPlaceholder = new PptxPlaceholder("body", 0, BuildShapeElement("master", "master"));

        // Act
        var result = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, [layoutPlaceholder], [masterPlaceholder], theme);

        // Assert
        Assert.Equal("slide", (string?)result.EffectiveSpPr?.Attribute("marker"));
        Assert.Equal("layout", (string?)result.EffectiveTxBodyListStyle?.Attribute("marker"));
    }

    /// <summary>Proves no match at any level leaves both effective property categories null, while the theme is still returned.</summary>
    [Fact]
    public void PptxDocumentInheritance_ResolvePlaceholderProperties_NoMatchAtAnyLevel_ReturnsNullForBothCategories()
    {
        // Arrange: no layout placeholder at all, so hop 1 always misses.
        var theme = BuildTestTheme();
        var slidePlaceholder = new PptxPlaceholder("body", 42, BuildShapeElement());
        var masterPlaceholder = new PptxPlaceholder("body", 0, BuildShapeElement("master", "master"));

        // Act
        var result = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, [], [masterPlaceholder], theme);

        // Assert
        Assert.Null(result.EffectiveSpPr);
        Assert.Null(result.EffectiveTxBodyListStyle);
        Assert.Same(theme, result.Theme);
    }
}
