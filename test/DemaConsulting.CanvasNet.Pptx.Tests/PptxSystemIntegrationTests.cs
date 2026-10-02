using System.IO.Compression;
using System.Text;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx ooxml

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

        const string presentationXml = "<p:presentation xmlns:p=\"http://example.com/presentationml\" />";

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", packageRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", presentationXml);
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
}
