// cspell:ignore vsdx Visio xmlns FutureRowKind
using System.Globalization;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     The original VSDX test drawing.
/// </summary>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     The width in pixels at 96 dpi of the first page of <see cref="CreateVsdx"/>.
    /// </summary>
    public const int VsdxPageWidth = 384;

    /// <summary>
    ///     The height in pixels at 96 dpi of the first page of <see cref="CreateVsdx"/>.
    /// </summary>
    public const int VsdxPageHeight = 288;

    /// <summary>
    ///     Creates a small two-page Visio drawing as an Open Packaging Convention package, assembled by hand for these
    ///     tests.
    /// </summary>
    /// <param name="withUnsupportedShape">
    ///     <see langword="true"/> to add a shape with a geometry row of a kind that does not exist to page 1, which the
    ///     renderer approximates and reports (or refuses when strict).
    /// </param>
    /// <returns>The bytes of the package.</returns>
    /// <remarks>
    ///     Page 1 is named <c>First</c> and is 4 by 3 inches (384x288 pixels at 96 dpi) with a red rectangle over its
    ///     top-left quarter. Page 2 is named <c>Second</c> and is 3 by 4 inches with a blue rectangle over its
    ///     bottom half. There is no text, so that the pixels do not depend on the fonts of the machine.
    /// </remarks>
    public static byte[] CreateVsdx(bool withUnsupportedShape = false)
    {
        const string visioNamespaces =
            "xmlns=\"http://schemas.microsoft.com/office/visio/2012/main\" "
            + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
        const string relationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string visioType = "http://schemas.microsoft.com/visio/2010/relationships";

        var page1Shapes = VsdxRectangle(1, 0, 1.5, 2, 1.5, "#FF0000");
        if (withUnsupportedShape)
        {
            page1Shapes +=
                """
                <Shape ID="2" Type="Shape">
                  <Cell N="PinX" V="3"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
                  <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
                  <Section N="Geometry" IX="0">
                    <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                    <Row T="FutureRowKind" IX="2"><Cell N="X" V="1"/><Cell N="Y" V="1"/></Row>
                  </Section>
                </Shape>
                """;
        }

        return CreateZip(
        [
            Utf8(
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
                  <Default Extension="xml" ContentType="application/xml" />
                </Types>
                """),
            Utf8(
                "_rels/.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId1" Type="{visioType}/document" Target="visio/document.xml" />
                </Relationships>
                """),
            Utf8("visio/document.xml", $"<VisioDocument {visioNamespaces}></VisioDocument>"),
            Utf8(
                "visio/_rels/document.xml.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId1" Type="{visioType}/pages" Target="pages/pages.xml" />
                </Relationships>
                """),
            Utf8(
                "visio/pages/pages.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Pages {visioNamespaces}>
                  <Page ID="0" Name="First" NameU="First">
                    <PageSheet><Cell N="PageWidth" V="4"/><Cell N="PageHeight" V="3"/></PageSheet>
                    <Rel r:id="rId1"/>
                  </Page>
                  <Page ID="1" Name="Second" NameU="Second">
                    <PageSheet><Cell N="PageWidth" V="3"/><Cell N="PageHeight" V="4"/></PageSheet>
                    <Rel r:id="rId2"/>
                  </Page>
                </Pages>
                """),
            Utf8(
                "visio/pages/_rels/pages.xml.rels",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{relationshipsNamespace}">
                  <Relationship Id="rId1" Type="{visioType}/page" Target="page1.xml" />
                  <Relationship Id="rId2" Type="{visioType}/page" Target="page2.xml" />
                </Relationships>
                """),
            Utf8("visio/pages/page1.xml", VsdxPageContents(visioNamespaces, page1Shapes)),
            Utf8("visio/pages/page2.xml", VsdxPageContents(visioNamespaces, VsdxRectangle(1, 0, 0, 3, 2, "#0000FF")))
        ]);
    }

    /// <summary>
    ///     Creates the XML of the contents of a page.
    /// </summary>
    /// <param name="namespaces">The namespace declarations.</param>
    /// <param name="shapes">The shape elements.</param>
    /// <returns>The XML.</returns>
    private static string VsdxPageContents(string namespaces, string shapes)
    {
        return $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <PageContents {namespaces}>
                  <Shapes>
                {shapes}
                  </Shapes>
                </PageContents>
                """;
    }

    /// <summary>
    ///     Creates the XML of a filled rectangle shape.
    /// </summary>
    /// <param name="id">The shape id.</param>
    /// <param name="x">The left edge in inches.</param>
    /// <param name="y">The bottom edge in inches (the page's origin is its bottom-left corner).</param>
    /// <param name="width">The width in inches.</param>
    /// <param name="height">The height in inches.</param>
    /// <param name="color">The fill color as <c>#RRGGBB</c>.</param>
    /// <returns>The XML.</returns>
    private static string VsdxRectangle(int id, double x, double y, double width, double height, string color)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
             <Shape ID="{id}" Type="Shape">
               <Cell N="PinX" V="{x}"/><Cell N="PinY" V="{y}"/><Cell N="Width" V="{width}"/><Cell N="Height" V="{height}"/>
               <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
               <Cell N="FillForegnd" V="{color}"/><Cell N="FillPattern" V="1"/><Cell N="LinePattern" V="0"/>
               <Section N="Geometry" IX="0">
                 <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                 <Row T="LineTo" IX="2"><Cell N="X" V="{width}"/><Cell N="Y" V="0"/></Row>
                 <Row T="LineTo" IX="3"><Cell N="X" V="{width}"/><Cell N="Y" V="{height}"/></Row>
                 <Row T="LineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="{height}"/></Row>
               </Section>
             </Shape>
             """);
    }
}
