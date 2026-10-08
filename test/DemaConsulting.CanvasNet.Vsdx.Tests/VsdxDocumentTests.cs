// cspell:ignore vsdx davehoward jgreywolfvsdxjs basicshapes Visio visio diagramwithstyles flowchartshapes Jgreywolf

using System.IO.Compression;
using System.Text;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for the <see cref="VsdxDocument"/> OPC package layer (opening the
///     <c>.vsdx</c> ZIP, resolving <c>[Content_Types].xml</c>, resolving package/part
///     relationships) and page index parsing (<see cref="VsdxDocument.PageCount"/>/
///     <see cref="VsdxDocument.GetPageSize(int)"/>) delivered by Milestone 2 of the
///     <c>CanvasNetVsdx</c> Implementation Phase Plan. No shape/geometry/master-content/style/
///     theme/text/connector parsing exists yet - all deferred to later milestones.
/// </summary>
public class VsdxDocumentTests
{
    // --- Minimal, synthetic package fixtures ------------------------------------------------

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
          <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/document" Target="visio/document.xml" />
        </Relationships>
        """;

    private const string DefaultDocumentXml =
        """
        <VisioDocument xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" />
        """;

    private const string DefaultDocumentRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/pages" Target="pages/pages.xml" />
        </Relationships>
        """;

    private const string EmptyPagesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" />
        """;

    /// <summary>Builds a minimal, valid <c>visio/pages/pages.xml</c> with the declared <paramref name="pageCount"/> pages, each 8.5in x 11in.</summary>
    private static string BuildPagesXml(int pageCount)
    {
        var pages = new StringBuilder();
        for (var i = 0; i < pageCount; i++)
        {
            pages.Append(
                $"""<Page ID="{i}" Name="Page-{i + 1}" NameU="Page-{i + 1}"><PageSheet><Cell N="PageWidth" V="8.5"/><Cell N="PageHeight" V="11"/></PageSheet></Page>""");
        }

        return
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
            {pages}
            </Pages>
            """;
    }

    /// <summary>
    ///     Builds an in-memory, OPC-shaped <c>.vsdx</c> ZIP package via <see cref="ZipArchive"/>,
    ///     with each supplied <paramref name="entries"/> name/content pair written verbatim as a
    ///     ZIP entry. Used by every test in this class so each test only needs to describe the
    ///     handful of entries relevant to the scenario under test, rather than a complete,
    ///     feature-complete package.
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

    /// <summary>
    ///     Builds a minimal, valid package: content types, package-level rels, a minimal
    ///     <c>VisioDocument</c> part, its rels resolving a <c>pages</c> relationship, and an
    ///     <paramref name="pageCount"/>-page <c>pages.xml</c> (no masters/theme relationships at
    ///     all).
    /// </summary>
    private static Stream BuildMinimalValidPackage(int pageCount = 1) =>
        BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", BuildPagesXml(pageCount)));

    // --- Real, staged fixture discovery ------------------------------------------------------

    /// <summary>The root folder a built test project copies the staged <c>.vsdx</c> fixtures into (see the <c>.csproj</c>'s fixture <c>&lt;None&gt;</c> wiring).</summary>
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "VsdxFixtures");

    /// <summary>Resolves a staged fixture's full path by file name, failing clearly if the fixture-copy wiring did not place it in the output directory.</summary>
    private static string FixturePath(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected staged fixture '{path}' to exist in the test output directory.");
        return path;
    }

    /// <summary>Every <c>.vsdx</c> fixture file name staged for this test project (see the fixtures folder's own README).</summary>
    public static TheoryData<string> AllFixtureFileNames =>
    [
        "davehoward-test1.vsdx",
        "davehoward-test10-nested-shapes.vsdx",
        "davehoward-test11-rotate.vsdx",
        "davehoward-test12-colors.vsdx",
        "davehoward-test3-house.vsdx",
        "davehoward-test4-connectors.vsdx",
        "davehoward-test5-master.vsdx",
        "davehoward-test6-shape-properties.vsdx",
        "davehoward-test9-rect-and-line.vsdx",
        "jgreywolfvsdxjs-basicshapes.vsdx",
        "jgreywolfvsdxjs-connectors.vsdx",
        "jgreywolfvsdxjs-diagramwithstyles.vsdx",
        "jgreywolfvsdxjs-drawing.vsdx",
        "jgreywolfvsdxjs-flowchartshapes.vsdx"
    ];

    // --- CanvasNetVsdx-VsdxDocument-OpenStream ----------------------------------------------

    /// <summary>Proves a minimal, well-formed OPC package opens successfully.</summary>
    [Fact]
    public void VsdxDocument_Open_WellFormedMinimalPackage_Succeeds()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage(pageCount: 1);

        // Act
        using var document = VsdxDocument.Open(stream);

        // Assert
        Assert.Equal(1, document.PageCount);
    }

    /// <summary>Proves <see cref="VsdxDocument.Open(Stream)"/> rejects a null stream.</summary>
    [Fact]
    public void VsdxDocument_Open_NullStream_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => VsdxDocument.Open((Stream)null!));
    }

    /// <summary>
    ///     A stream that never ends, always reporting its requested buffer as fully read with
    ///     zero bytes - simulates a deliberately non-terminating (or simply enormous)
    ///     caller-supplied input to <see cref="VsdxDocument.Open(Stream)"/>, proving the buffering
    ///     step bounds both the memory and time it can spend reading such a stream rather than
    ///     looping/allocating without limit.
    /// </summary>
    private sealed class InfiniteZeroStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    ///     Proves <see cref="VsdxDocument.Open(Stream)"/> rejects a stream exceeding its
    ///     documented maximum buffered-package size with <see cref="InvalidDataException"/>,
    ///     rather than buffering a non-terminating (or simply oversized) stream without limit
    ///     until memory is exhausted.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_NonTerminatingOversizedStream_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = new InfiniteZeroStream();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    // --- CanvasNetVsdx-VsdxDocument-OpenPath -------------------------------------------------

    /// <summary>Proves <see cref="VsdxDocument.Open(string)"/> rejects a null path.</summary>
    [Fact]
    public void VsdxDocument_Open_NullPath_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => VsdxDocument.Open((string)null!));
    }

    /// <summary>Proves <see cref="VsdxDocument.Open(string)"/> rejects an empty/whitespace-only path.</summary>
    [Fact]
    public void VsdxDocument_Open_WhitespacePath_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => VsdxDocument.Open("   "));
    }

    // --- CanvasNetVsdx-VsdxDocument-ZipValidation --------------------------------------------

    /// <summary>Proves a corrupt/non-ZIP stream throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_CorruptNonZipStream_ThrowsInvalidDataException()
    {
        // Arrange: a stream of arbitrary bytes, not a ZIP local-file-header signature.
        using var stream = new MemoryStream([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    // --- CanvasNetVsdx-VsdxDocument-ContentTypesValidation -----------------------------------

    /// <summary>Proves a package missing <c>[Content_Types].xml</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_MissingContentTypes_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildPackage(("_rels/.rels", DefaultPackageRelsXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>
    ///     Proves an oversized <c>[Content_Types].xml</c> part (exceeding the bounded XML
    ///     parser's character budget - an "XML bomb"/zip-bomb-style attack) throws
    ///     <see cref="InvalidDataException"/> rather than exhausting memory while parsing.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_OversizedContentTypesPart_ThrowsInvalidDataException()
    {
        // Arrange: pad with an oversized comment so the part exceeds the parser's character
        // budget well before any genuine content-types declaration is reached.
        var padding = new string('x', 2_100_000);
        var oversizedContentTypesXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <!--{padding}-->
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
            </Types>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", oversizedContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    // --- CanvasNetVsdx-VsdxDocument-RelationshipResolution -----------------------------------

    /// <summary>
    ///     Proves <see cref="VsdxDocument.Open(Stream)"/> resolves <c>visio/document.xml</c> and
    ///     <c>visio/pages/pages.xml</c> through the relationship graph, and also resolves an
    ///     optional <c>visio/masters/masters.xml</c> relationship when present (asserted via the
    ///     internal <see cref="VsdxDocument.MastersPartPath"/> accessor), proving masters
    ///     resolution is driven by relationship <c>Type</c>, never by filename convention.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_ResolvesDocumentMastersPagesViaRelationships()
    {
        // Arrange: document.xml.rels additionally declares a "masters" relationship pointing at
        // an arbitrarily-named part (proving resolution is by Type, not filename convention).
        const string documentRelsWithMastersXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/pages" Target="pages/pages.xml" />
              <Relationship Id="rId2" Type="http://schemas.microsoft.com/visio/2010/relationships/masters" Target="not-the-conventional-name.xml" />
            </Relationships>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", documentRelsWithMastersXml),
            ("visio/pages/pages.xml", BuildPagesXml(1)),
            ("visio/not-the-conventional-name.xml", "<Masters xmlns=\"http://schemas.microsoft.com/office/visio/2012/main\"/>"));

        // Act
        using var document = VsdxDocument.Open(stream);

        // Assert
        Assert.Equal(1, document.PageCount);
        Assert.Equal("visio/not-the-conventional-name.xml", document.MastersPartPath);
    }

    /// <summary>
    ///     Proves <see cref="VsdxDocument.Open(Stream)"/> succeeds when the package declares no
    ///     <c>masters</c>/<c>theme</c> relationship at all - grounded directly in the real
    ///     <c>davehoward-test1.vsdx</c> fixture's shape (3 pages, no masters, no theme).
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_MastersRelationshipAbsent_StillSucceeds()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage(pageCount: 1);

        // Act
        using var document = VsdxDocument.Open(stream);

        // Assert
        Assert.Null(document.MastersPartPath);
        Assert.Null(document.ThemePartPath);
    }

    /// <summary>
    ///     Proves a package whose <c>document.xml.rels</c> has no <c>pages</c> relationship at
    ///     all throws <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_MissingRequiredRelationshipTarget_ThrowsInvalidDataException()
    {
        // Arrange: document.xml.rels exists but declares no "pages" relationship.
        const string documentRelsNoPagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/windows" Target="windows.xml" />
            </Relationships>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", documentRelsNoPagesXml),
            ("visio/windows.xml", "<Windows xmlns=\"http://schemas.microsoft.com/office/visio/2012/main\"/>"));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>
    ///     Proves a package with no <c>document</c> relationship from the package root throws
    ///     <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_MissingDocumentRelationship_ThrowsInvalidDataException()
    {
        // Arrange: package-level rels exists but declares no "document" relationship.
        const string packageRelsNoDocumentXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/windows" Target="visio/windows.xml" />
            </Relationships>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", packageRelsNoDocumentXml),
            ("visio/windows.xml", "<Windows xmlns=\"http://schemas.microsoft.com/office/visio/2012/main\"/>"));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>
    ///     Proves <c>visio/pages/pages.xml</c> declaring zero <c>&lt;Page&gt;</c> children
    ///     throws <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_EmptyPagesIndex_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", EmptyPagesXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    // --- CanvasNetVsdx-VsdxDocument-Dispose --------------------------------------------------

    /// <summary>
    ///     Proves <see cref="VsdxDocument.Dispose"/> releases the underlying
    ///     <see cref="ZipArchive"/>, observed indirectly through the owning document rejecting
    ///     further property access once disposed.
    /// </summary>
    [Fact]
    public void VsdxDocument_Dispose_ReleasesUnderlyingZipArchive()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage();
        var document = VsdxDocument.Open(stream);

        // Act
        document.Dispose();

        // Assert: the now-disposed document's properties reject further access.
        Assert.Throws<ObjectDisposedException>(() => document.PageCount);
    }

    /// <summary>Proves calling <see cref="VsdxDocument.Dispose"/> more than once has no ill effect.</summary>
    [Fact]
    public void VsdxDocument_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage();
        var document = VsdxDocument.Open(stream);

        // Act
        document.Dispose();
        var exception = Record.Exception(document.Dispose);

        // Assert
        Assert.Null(exception);
    }

    // --- CanvasNetVsdx-VsdxDocument-PageCount ------------------------------------------------

    /// <summary>Proves a multi-page package resolves <see cref="VsdxDocument.PageCount"/> correctly.</summary>
    [Fact]
    public void VsdxDocument_PageCount_ReturnsDeclaredPageCount()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage(pageCount: 3);

        // Act
        using var document = VsdxDocument.Open(stream);

        // Assert
        Assert.Equal(3, document.PageCount);
    }

    // --- CanvasNetVsdx-VsdxDocument-GetPageSize -----------------------------------------------

    /// <summary>
    ///     Proves <see cref="VsdxDocument.GetPageSize(int)"/> converts a page's declared
    ///     <c>PageWidth</c>/<c>PageHeight</c> (always expressed in inches) into the exact EMU
    ///     values reproduced from the real <c>davehoward-test1.vsdx</c> fixture's page 1
    ///     (<c>8.26771653543307</c> in. x <c>11.69291338582677</c> in. -&gt;
    ///     <c>7,560,000</c> x <c>10,692,000</c> EMU).
    /// </summary>
    [Fact]
    public void VsdxDocument_GetPageSize_ReturnsDeclaredSizeInEmu()
    {
        // Arrange
        const string pagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <Page ID="0" Name="Page-1" NameU="Page-1">
                <PageSheet>
                  <Cell N="PageWidth" V="8.26771653543307"/>
                  <Cell N="PageHeight" V="11.69291338582677"/>
                </PageSheet>
              </Page>
            </Pages>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", pagesXml));
        using var document = VsdxDocument.Open(stream);

        // Act
        var pageInfo = document.GetPageSize(0);

        // Assert
        Assert.Equal("Page-1", pageInfo.Name);
        Assert.Equal(7_560_000L, pageInfo.WidthEmu);
        Assert.Equal(10_692_000L, pageInfo.HeightEmu);
    }

    /// <summary>
    ///     Proves a <c>U=</c> attribute on an unrelated, sibling <c>&lt;PageSheet&gt;</c> cell
    ///     does not influence <c>PageWidth</c>/<c>PageHeight</c>'s own unit interpretation -
    ///     reproducing the real <c>jgreywolfvsdxjs-basicshapes.vsdx</c> fixture's evidence that
    ///     <c>U="IN_F"</c> appears on <c>PageScale</c> as a UI-format hint only.
    /// </summary>
    [Fact]
    public void VsdxDocument_GetPageSize_UnrelatedCellUnitAttributeIsIgnored_StillConvertsAsInches()
    {
        // Arrange
        const string pagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <Page ID="0" Name="Page-1" NameU="Page-1">
                <PageSheet>
                  <Cell N="PageWidth" V="11"/>
                  <Cell N="PageHeight" V="16.5"/>
                  <Cell N="PageScale" V="1" U="IN_F"/>
                </PageSheet>
              </Page>
            </Pages>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", pagesXml));
        using var document = VsdxDocument.Open(stream);

        // Act
        var pageInfo = document.GetPageSize(0);

        // Assert
        Assert.Equal(10_058_400L, pageInfo.WidthEmu);
        Assert.Equal(15_087_600L, pageInfo.HeightEmu);
    }

    /// <summary>
    ///     Proves <see cref="VsdxDocument.GetPageSize(int)"/> falls back to a page's <c>NameU</c>
    ///     attribute when <c>Name</c> is absent.
    /// </summary>
    [Fact]
    public void VsdxDocument_GetPageSize_NameAbsent_FallsBackToNameU()
    {
        // Arrange
        const string pagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <Page ID="0" NameU="Page-1">
                <PageSheet>
                  <Cell N="PageWidth" V="8.5"/>
                  <Cell N="PageHeight" V="11"/>
                </PageSheet>
              </Page>
            </Pages>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", pagesXml));
        using var document = VsdxDocument.Open(stream);

        // Act
        var pageInfo = document.GetPageSize(0);

        // Assert
        Assert.Equal("Page-1", pageInfo.Name);
    }

    /// <summary>
    ///     Proves <see cref="VsdxDocument.GetPageSize(int)"/> throws
    ///     <see cref="ArgumentOutOfRangeException"/> for a negative index and for an index equal
    ///     to <see cref="VsdxDocument.PageCount"/>.
    /// </summary>
    [Fact]
    public void VsdxDocument_GetPageSize_OutOfRangeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var stream = BuildMinimalValidPackage(pageCount: 1);
        using var document = VsdxDocument.Open(stream);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPageSize(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPageSize(document.PageCount));
    }

    // --- Additional negative-path coverage (not tied to a single forward-declared requirement) -

    /// <summary>Proves a package missing the package-level <c>_rels/.rels</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_MissingPackageRelationships_ThrowsInvalidDataException()
    {
        // Arrange
        using var stream = BuildPackage(("[Content_Types].xml", DefaultContentTypesXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>Proves a package whose <c>visio/document.xml</c> root is not <c>VisioDocument</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_DocumentPartWrongRoot_ThrowsInvalidDataException()
    {
        // Arrange
        const string wrongRootDocumentXml =
            """<wrongRoot xmlns="http://schemas.microsoft.com/office/visio/2012/main"/>""";
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", wrongRootDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", BuildPagesXml(1)));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>Proves a package whose <c>visio/pages/pages.xml</c> root is not <c>Pages</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_PagesPartWrongRoot_ThrowsInvalidDataException()
    {
        // Arrange
        const string wrongRootPagesXml =
            """<wrongRoot xmlns="http://schemas.microsoft.com/office/visio/2012/main"/>""";
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", wrongRootPagesXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>Proves a page missing its nested <c>&lt;PageSheet&gt;</c> element throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_PageMissingPageSheet_ThrowsInvalidDataException()
    {
        // Arrange
        const string pagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <Page ID="0" Name="Page-1" NameU="Page-1"/>
            </Pages>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", pagesXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>Proves a page's <c>&lt;PageSheet&gt;</c> missing its <c>PageWidth</c> cell throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_PageMissingPageWidthCell_ThrowsInvalidDataException()
    {
        // Arrange
        const string pagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <Page ID="0" Name="Page-1" NameU="Page-1">
                <PageSheet>
                  <Cell N="PageHeight" V="11"/>
                </PageSheet>
              </Page>
            </Pages>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", pagesXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>Proves a page's <c>PageWidth</c> cell with a non-positive <c>V</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void VsdxDocument_Open_PageWidthCellNonPositive_ThrowsInvalidDataException()
    {
        // Arrange
        const string pagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <Page ID="0" Name="Page-1" NameU="Page-1">
                <PageSheet>
                  <Cell N="PageWidth" V="0"/>
                  <Cell N="PageHeight" V="11"/>
                </PageSheet>
              </Page>
            </Pages>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", pagesXml));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => VsdxDocument.Open(stream));
    }

    /// <summary>
    ///     Proves a pathologically large (but still finite and positive) <c>PageWidth</c> value
    ///     - one that overflows <see cref="long"/> once scaled to EMU - throws the documented
    ///     <see cref="InvalidDataException"/> error contract for malformed page dimensions,
    ///     rather than an internal-implementation-detail <see cref="OverflowException"/> leaking
    ///     through from the EMU conversion's own <c>checked</c> cast - PR #42 review round 2
    ///     (Finding #3, Medium).
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_PageWidthCellPathologicallyLarge_ThrowsInvalidDataExceptionNotOverflowException()
    {
        // Arrange: 1e300 inches, scaled by EmuPerInch (914,400), vastly exceeds long.MaxValue.
        const string pagesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <Page ID="0" Name="Page-1" NameU="Page-1">
                <PageSheet>
                  <Cell N="PageWidth" V="1e300"/>
                  <Cell N="PageHeight" V="11"/>
                </PageSheet>
              </Page>
            </Pages>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", DefaultPackageRelsXml),
            ("visio/document.xml", DefaultDocumentXml),
            ("visio/_rels/document.xml.rels", DefaultDocumentRelsXml),
            ("visio/pages/pages.xml", pagesXml));

        // Act
        var exception = Record.Exception(() => VsdxDocument.Open(stream));

        // Assert: specifically InvalidDataException, not OverflowException (nor any other type).
        Assert.IsType<InvalidDataException>(exception);
    }

    /// <summary>
    ///     Proves a relationship target with a <c>"../"</c> traversal segment resolves relative
    ///     to the source part's own directory - exercised here through
    ///     <c>visio/pages/pages.xml</c> being resolved from a <c>visio/sub/document.xml</c> part
    ///     whose own relative target climbs back up to <c>visio/pages/pages.xml</c>.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_RelationshipTargetWithParentTraversal_ResolvesCorrectTarget()
    {
        // Arrange: document.xml lives at "visio/sub/document.xml" so its "../pages/pages.xml"
        // relative target must resolve to "visio/pages/pages.xml" (not "visio/sub/pages/pages.xml").
        const string packageRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/document" Target="visio/sub/document.xml" />
            </Relationships>
            """;
        const string documentRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/pages" Target="../pages/pages.xml" />
            </Relationships>
            """;
        using var stream = BuildPackage(
            ("[Content_Types].xml", DefaultContentTypesXml),
            ("_rels/.rels", packageRelsXml),
            ("visio/sub/document.xml", DefaultDocumentXml),
            ("visio/sub/_rels/document.xml.rels", documentRelsXml),
            ("visio/pages/pages.xml", BuildPagesXml(1)));

        // Act
        using var document = VsdxDocument.Open(stream);

        // Assert
        Assert.Equal(1, document.PageCount);
    }

    // --- Real-fixture sanity tests ------------------------------------------------------------

    /// <summary>
    ///     Proves the real <c>davehoward-test1.vsdx</c> fixture (3 pages, no masters/theme
    ///     relationship) opens successfully and its page count/first-page size match
    ///     <c>pages.xml</c>'s own declared content.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_RealFixture_DaveHowardTest1_PageCountAndSizesMatchPagesXml()
    {
        // Arrange
        var path = FixturePath("davehoward-test1.vsdx");

        // Act
        using var document = VsdxDocument.Open(path);

        // Assert
        Assert.Equal(3, document.PageCount);
        Assert.Null(document.MastersPartPath);
        Assert.Null(document.ThemePartPath);

        var page1 = document.GetPageSize(0);
        Assert.Equal("Page-1", page1.Name);
        Assert.Equal(7_560_000L, page1.WidthEmu);
        Assert.Equal(10_692_000L, page1.HeightEmu);
    }

    /// <summary>
    ///     Proves the real <c>jgreywolfvsdxjs-basicshapes.vsdx</c> fixture (masters and theme
    ///     relationships both present) opens successfully, resolves the optional masters/theme
    ///     parts, and its first page's declared size matches <c>pages.xml</c>'s own declared
    ///     content.
    /// </summary>
    [Fact]
    public void VsdxDocument_Open_RealFixture_JgreywolfBasicShapes_PageCountAndSizesMatchPagesXml()
    {
        // Arrange
        var path = FixturePath("jgreywolfvsdxjs-basicshapes.vsdx");

        // Act
        using var document = VsdxDocument.Open(path);

        // Assert
        Assert.True(document.PageCount > 0);
        Assert.NotNull(document.MastersPartPath);
        Assert.NotNull(document.ThemePartPath);

        var page1 = document.GetPageSize(0);
        Assert.Equal("Page-1", page1.Name);
        Assert.Equal(10_058_400L, page1.WidthEmu);
        Assert.Equal(15_087_600L, page1.HeightEmu);
    }

    /// <summary>
    ///     Broad regression net: proves every staged real-world <c>.vsdx</c> fixture opens
    ///     successfully and declares at least one page, cheaply exercising the full package/page
    ///     parsing pipeline against real-world input diversity beyond the two fixtures sampled in
    ///     detail above.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtureFileNames))]
    public void VsdxDocument_Open_AllStagedFixtures_SucceedsWithAtLeastOnePage(string fileName)
    {
        // Arrange
        var path = FixturePath(fileName);

        // Act
        using var document = VsdxDocument.Open(path);

        // Assert
        Assert.True(document.PageCount > 0, $"Expected '{fileName}' to declare at least one page.");
    }
}
