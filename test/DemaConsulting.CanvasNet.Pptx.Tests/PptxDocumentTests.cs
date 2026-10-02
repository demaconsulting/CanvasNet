using System.IO.Compression;
using System.Text;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx ooxml

/// <summary>
///     Unit-level tests for the <see cref="PptxDocument"/> OOXML package layer (Phase 1a): ZIP
///     opening, <c>[Content_Types].xml</c> resolution (including part-specific overrides), and
///     package/part relationship resolution (including relative-target traversal). Complements
///     <see cref="PptxSystemIntegrationTests"/>, which proves the same behaviors end-to-end
///     through the public <see cref="PptxDocument.Open(Stream)"/>/<see cref="PptxDocument.Open(string)"/>
///     API only.
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
          <Relationship Id="rId1" Type="http://example.com/presentation" Target="ppt/presentation.xml" />
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

    /// <summary>Builds a minimal, valid package: content types plus the package-level rels part only.</summary>
    private static Stream BuildMinimalValidPackage() =>
        BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml));

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
            ("ppt/presentation.xml", "<p:presentation />"));
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
            ("ppt/presentation.xml", "<p:presentation />"));
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
}
