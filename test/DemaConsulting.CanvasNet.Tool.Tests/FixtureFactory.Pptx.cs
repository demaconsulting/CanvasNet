// cspell:ignore pptx xmlns sldSz sldIdLst sldId prst srgbClr fmtScheme clrMap emf notarealshape ooxml
using System.IO.Compression;
using System.Text;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     The original PPTX test presentation.
/// </summary>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     The width in pixels at 96 dpi of a slide of <see cref="CreatePptx"/>.
    /// </summary>
    public const int PptxSlideWidth = 480;

    /// <summary>
    ///     The height in pixels at 96 dpi of a slide of <see cref="CreatePptx"/>.
    /// </summary>
    public const int PptxSlideHeight = 360;

    /// <summary>
    ///     Creates a small two-slide presentation as an Office Open XML package, assembled by hand for these tests.
    /// </summary>
    /// <param name="withMetafile">
    ///     <see langword="true"/> to add a picture of an EMF metafile to slide 1, which the renderer cannot draw and
    ///     skips (or refuses when strict).
    /// </param>
    /// <param name="withUnsupportedShape">
    ///     <see langword="true"/> to add a shape with a preset geometry that does not exist to slide 1, which the
    ///     renderer refuses.
    /// </param>
    /// <returns>The bytes of the package.</returns>
    /// <remarks>
    ///     The slides are 5 by 3.75 inches (480x360 pixels at 96 dpi). Slide 1 has a red rectangle over its left
    ///     half; slide 2 has a blue rectangle over its top half. There is no text, so that the pixels do not depend
    ///     on the fonts of the machine.
    /// </remarks>
    public static byte[] CreatePptx(bool withMetafile = false, bool withUnsupportedShape = false)
    {
        const string officeNamespaces =
            "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" "
            + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
        const string relationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string relationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        var slide1Shapes = PptxRectangle(2, "Left half", 0, 0, 2286000, 3429000, "FF0000");
        if (withMetafile)
        {
            slide1Shapes +=
                """
                <p:pic>
                  <p:nvPicPr><p:cNvPr id="3" name="Metafile"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
                  <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
                  <p:spPr>
                    <a:xfrm><a:off x="2286000" y="0"/><a:ext cx="2286000" cy="3429000"/></a:xfrm>
                    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                  </p:spPr>
                </p:pic>
                """;
        }

        if (withUnsupportedShape)
        {
            slide1Shapes += PptxRectangle(4, "Unsupported", 2286000, 0, 2286000, 1714500, "00FF00", "notARealShape");
        }

        var entries = new List<(string Name, byte[] Content)>
        {
            Utf8(
                "[Content_Types].xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                  <Default Extension="xml" ContentType="application/xml" />
                  {(withMetafile ? "<Default Extension=\"emf\" ContentType=\"image/x-emf\" />" : string.Empty)}
                </Types>
                """),
            Utf8(
                "_rels/.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId1" Type="{relationshipType}/officeDocument" Target="ppt/presentation.xml" />
                </Relationships>
                """),
            Utf8(
                "ppt/presentation.xml",
                $"""
                <p:presentation {officeNamespaces}>
                  <p:sldSz cx="4572000" cy="3429000"/>
                  <p:sldIdLst><p:sldId id="256" r:id="rId2"/><p:sldId id="257" r:id="rId3"/></p:sldIdLst>
                </p:presentation>
                """),
            Utf8(
                "ppt/_rels/presentation.xml.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId2" Type="{relationshipType}/slide" Target="slides/slide1.xml" />
                  <Relationship Id="rId3" Type="{relationshipType}/slide" Target="slides/slide2.xml" />
                </Relationships>
                """),
            Utf8("ppt/slides/slide1.xml", PptxSlide(officeNamespaces, slide1Shapes)),
            Utf8("ppt/slides/slide2.xml", PptxSlide(officeNamespaces, PptxRectangle(2, "Top half", 0, 0, 4572000, 1714500, "0000FF"))),
            Utf8("ppt/slides/_rels/slide1.xml.rels", PptxSlideRelationships(relationshipsNamespace, relationshipType, withMetafile)),
            Utf8("ppt/slides/_rels/slide2.xml.rels", PptxSlideRelationships(relationshipsNamespace, relationshipType, false)),
            Utf8(
                "ppt/slideLayouts/slideLayout1.xml",
                $"<p:sldLayout {officeNamespaces}><p:cSld><p:spTree></p:spTree></p:cSld></p:sldLayout>"),
            Utf8(
                "ppt/slideLayouts/_rels/slideLayout1.xml.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId1" Type="{relationshipType}/slideMaster" Target="../slideMasters/slideMaster1.xml" />
                </Relationships>
                """),
            Utf8(
                "ppt/slideMasters/slideMaster1.xml",
                $"""
                <p:sldMaster {officeNamespaces}>
                  <p:cSld><p:spTree></p:spTree></p:cSld>
                  <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2"/>
                  <p:txStyles></p:txStyles>
                </p:sldMaster>
                """),
            Utf8(
                "ppt/slideMasters/_rels/slideMaster1.xml.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId1" Type="{relationshipType}/theme" Target="../theme/theme1.xml" />
                </Relationships>
                """),
            Utf8(
                "ppt/theme/theme1.xml",
                """
                <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Test">
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
                    <a:fontScheme name="Test">
                      <a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
                      <a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
                    </a:fontScheme>
                  </a:themeElements>
                </a:theme>
                """)
        };
        if (withMetafile)
        {
            // Not a real metafile: the renderer decides by the extension that it cannot draw it
            entries.Add(("ppt/media/image1.emf", [1, 2, 3, 4]));
        }

        return CreateZip(entries);
    }

    /// <summary>
    ///     Creates the XML of a slide.
    /// </summary>
    /// <param name="namespaces">The namespace declarations.</param>
    /// <param name="shapes">The shape elements.</param>
    /// <returns>The XML.</returns>
    private static string PptxSlide(string namespaces, string shapes)
    {
        return $"<p:sld {namespaces}><p:cSld><p:spTree>{shapes}</p:spTree></p:cSld></p:sld>";
    }

    /// <summary>
    ///     Creates the XML of the relationships of a slide: its layout, and optionally a picture.
    /// </summary>
    /// <param name="relationshipsNamespace">The namespace of the relationships.</param>
    /// <param name="relationshipType">The prefix of the relationship types.</param>
    /// <param name="withMetafile"><see langword="true"/> to add the relationship to the metafile picture.</param>
    /// <returns>The XML.</returns>
    private static string PptxSlideRelationships(string relationshipsNamespace, string relationshipType, bool withMetafile)
    {
        var picture = withMetafile
            ? $"<Relationship Id=\"rId2\" Type=\"{relationshipType}/image\" Target=\"../media/image1.emf\" />"
            : string.Empty;
        return $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId1" Type="{relationshipType}/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
                  {picture}
                </Relationships>
                """;
    }

    /// <summary>
    ///     Creates the XML of a filled rectangle shape.
    /// </summary>
    /// <param name="id">The shape id.</param>
    /// <param name="name">The shape name.</param>
    /// <param name="x">The left edge in EMU.</param>
    /// <param name="y">The top edge in EMU.</param>
    /// <param name="width">The width in EMU.</param>
    /// <param name="height">The height in EMU.</param>
    /// <param name="color">The fill color as six hexadecimal digits.</param>
    /// <param name="geometry">The preset geometry name.</param>
    /// <returns>The XML.</returns>
    private static string PptxRectangle(int id, string name, int x, int y, int width, int height, string color, string geometry = "rect")
    {
        return $"""
                <p:sp>
                  <p:nvSpPr><p:cNvPr id="{id}" name="{name}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
                  <p:spPr>
                    <a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{width}" cy="{height}"/></a:xfrm>
                    <a:prstGeom prst="{geometry}"><a:avLst/></a:prstGeom>
                    <a:solidFill><a:srgbClr val="{color}"/></a:solidFill>
                  </p:spPr>
                </p:sp>
                """;
    }

    /// <summary>
    ///     Creates a ZIP archive from entries.
    /// </summary>
    /// <param name="entries">The entry names and contents.</param>
    /// <returns>The bytes of the archive.</returns>
    public static byte[] CreateZip(IEnumerable<(string Name, byte[] Content)> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Replaces the content of one entry of a ZIP archive.
    /// </summary>
    /// <param name="zip">The bytes of the archive.</param>
    /// <param name="name">The name of the entry to replace.</param>
    /// <param name="text">The new content, stored as UTF-8.</param>
    /// <returns>The bytes of the changed archive.</returns>
    public static byte[] ReplaceZipEntry(byte[] zip, string name, string text)
    {
        using var input = new MemoryStream(zip);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var entries = new List<(string Name, byte[] Content)>();
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName == name)
            {
                entries.Add(Utf8(name, text));
                continue;
            }

            using var source = entry.Open();
            using var copy = new MemoryStream();
            source.CopyTo(copy);
            entries.Add((entry.FullName, copy.ToArray()));
        }

        return CreateZip(entries);
    }

    /// <summary>
    ///     Creates a ZIP entry from text.
    /// </summary>
    /// <param name="name">The entry name.</param>
    /// <param name="text">The text, stored as UTF-8.</param>
    /// <returns>The entry.</returns>
    private static (string Name, byte[] Content) Utf8(string name, string text)
    {
        return (name, new UTF8Encoding(false).GetBytes(text));
    }
}
