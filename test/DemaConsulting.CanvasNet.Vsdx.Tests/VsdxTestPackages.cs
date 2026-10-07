// cspell:ignore vsdx Visio

using System.IO.Compression;
using System.Text;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Shared, minimal synthetic <c>.vsdx</c>-shaped OPC package builders used by this
///     milestone's own test classes (<c>VsdxGeometryTests</c>, <c>VsdxTransformTests</c>,
///     <c>VsdxMasterInheritanceTests</c>, <c>VsdxStyleResolutionTests</c>). Deliberately separate
///     from <see cref="VsdxDocumentTests"/>'s own, Milestone-2-era, package-literal helpers: this
///     milestone's scenarios need a page with a separate content part
///     (<c>visio/pages/page1.xml</c>, referenced via <c>&lt;Rel r:id="..."/&gt;</c>), and often a
///     <c>masters.xml</c>/<c>masterN.xml</c> pair and/or a <c>&lt;StyleSheets&gt;</c> block -
///     none of which Milestone 2's own fixtures needed.
/// </summary>
internal static class VsdxTestPackages
{
    private const string ContentTypesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
        </Types>
        """;

    private const string PackageRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/document" Target="visio/document.xml" />
        </Relationships>
        """;

    /// <summary>Builds <c>visio/document.xml</c>, optionally with a <c>&lt;StyleSheets&gt;</c> block and/or a <c>masters</c> relationship declared in its own rels.</summary>
    /// <param name="styleSheetsXml">The literal <c>&lt;StyleSheets&gt;...&lt;/StyleSheets&gt;</c> markup to embed, or <see langword="null"/> to omit it entirely.</param>
    public static string BuildDocumentXml(string? styleSheetsXml = null) =>
        $"""
        <VisioDocument xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
        {styleSheetsXml ?? string.Empty}
        </VisioDocument>
        """;

    /// <summary>Builds <c>visio/_rels/document.xml.rels</c>, declaring a required <c>pages</c> relationship and an optional <c>masters</c> relationship.</summary>
    /// <param name="includeMasters">Whether to declare a relationship to <c>visio/masters/masters.xml</c>.</param>
    public static string BuildDocumentRelsXml(bool includeMasters) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/pages" Target="pages/pages.xml" />
        {(includeMasters ? """<Relationship Id="rId2" Type="http://schemas.microsoft.com/visio/2010/relationships/masters" Target="masters/masters.xml" />""" : string.Empty)}
        </Relationships>
        """;

    /// <summary>Builds a one-page <c>visio/pages/pages.xml</c>, whose single <c>&lt;Page&gt;</c> declares a <c>&lt;Rel r:id="rId1"/&gt;</c> to its own content part.</summary>
    public static string BuildPagesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Pages xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <Page ID="0" Name="Page-1" NameU="Page-1">
            <PageSheet><Cell N="PageWidth" V="8.5"/><Cell N="PageHeight" V="11"/></PageSheet>
            <Rel r:id="rId1"/>
          </Page>
        </Pages>
        """;

    /// <summary>Builds <c>visio/pages/_rels/pages.xml.rels</c>, resolving <c>rId1</c> to <c>page1.xml</c>.</summary>
    public static string BuildPagesRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/page" Target="page1.xml" />
        </Relationships>
        """;

    /// <summary>Builds <c>visio/pages/page1.xml</c>, wrapping the supplied literal <c>&lt;Shape&gt;</c> markup in a <c>&lt;PageContents&gt;&lt;Shapes&gt;...&lt;/Shapes&gt;&lt;/PageContents&gt;</c> envelope.</summary>
    /// <param name="shapesXml">The literal <c>&lt;Shape&gt;...&lt;/Shape&gt;</c> markup for every top-level shape on the page.</param>
    public static string BuildPageContentXml(string shapesXml) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <PageContents xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <Shapes>
        {shapesXml}
          </Shapes>
        </PageContents>
        """;

    /// <summary>Builds <c>visio/masters/masters.xml</c>, declaring a single <c>&lt;Master ID="1"&gt;</c> resolving to <c>master1.xml</c>.</summary>
    public static string BuildMastersXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Masters xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <Master ID="1" Name="Master-1" NameU="Master-1">
            <Rel r:id="rId1"/>
          </Master>
        </Masters>
        """;

    /// <summary>Builds <c>visio/masters/_rels/masters.xml.rels</c>, resolving <c>rId1</c> to <c>master1.xml</c>.</summary>
    public static string BuildMastersRelsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.microsoft.com/visio/2010/relationships/master" Target="master1.xml" />
        </Relationships>
        """;

    /// <summary>Builds <c>visio/masters/master1.xml</c>, wrapping the supplied literal <c>&lt;Shape&gt;</c> markup as the Master's single top-level shape.</summary>
    /// <param name="shapeXml">The literal <c>&lt;Shape&gt;...&lt;/Shape&gt;</c> markup for the Master's own top-level shape.</param>
    public static string BuildMasterContentXml(string shapeXml) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <MasterContents xmlns="http://schemas.microsoft.com/office/visio/2012/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <Shapes>
        {shapeXml}
          </Shapes>
        </MasterContents>
        """;

    /// <summary>
    ///     Assembles a complete in-memory <c>.vsdx</c> ZIP package from its parts, omitting the
    ///     masters entries entirely when <paramref name="mastersXml"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="pageShapesXml">The page's own top-level <c>&lt;Shape&gt;</c> markup (see <see cref="BuildPageContentXml"/>).</param>
    /// <param name="styleSheetsXml">The optional literal <c>&lt;StyleSheets&gt;</c> markup to embed in <c>visio/document.xml</c>.</param>
    /// <param name="mastersXml">The optional literal <c>&lt;Shape&gt;</c> markup for a single Master (<c>ID="1"</c>); when supplied, the package also declares the <c>masters</c> relationship and parts.</param>
    public static Stream BuildPackage(string pageShapesXml, string? styleSheetsXml = null, string? mastersXml = null)
    {
        var entries = new List<(string Name, string Content)>
        {
            ("[Content_Types].xml", ContentTypesXml),
            ("_rels/.rels", PackageRelsXml),
            ("visio/document.xml", BuildDocumentXml(styleSheetsXml)),
            ("visio/_rels/document.xml.rels", BuildDocumentRelsXml(includeMasters: mastersXml is not null)),
            ("visio/pages/pages.xml", BuildPagesXml()),
            ("visio/pages/_rels/pages.xml.rels", BuildPagesRelsXml()),
            ("visio/pages/page1.xml", BuildPageContentXml(pageShapesXml))
        };

        if (mastersXml is not null)
        {
            entries.Add(("visio/masters/masters.xml", BuildMastersXml()));
            entries.Add(("visio/masters/_rels/masters.xml.rels", BuildMastersRelsXml()));
            entries.Add(("visio/masters/master1.xml", BuildMasterContentXml(mastersXml)));
        }

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
}
