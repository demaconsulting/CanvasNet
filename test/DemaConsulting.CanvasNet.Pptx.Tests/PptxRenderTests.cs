using System.IO.Compression;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx sppr nvsppr nvpr cnvpr cnvsppr grpsp nvgrpsppr grpsppr cxnsp nvcxnsppr cnvcxnsppr
// cspell:ignore nvpicpr nvpr cnvpicpr blipfill srcrect embed graphicframe tbl tblgrid gridcol tcpr srgb
// cspell:ignore calibri txbox xfrm prst Xfrm hlink Hlink cust

/// <summary>
///     Unit-level tests for the Phase 1f public, slide-level rendering API
///     (<c>PptxDocument.Render.cs</c>'s <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>/
///     <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/>). Every test opens a
///     full, navigable, in-memory <c>.pptx</c>-shaped package built by <see cref="BuildRenderPackage"/>
///     (mirroring <see cref="PptxSystemIntegrationTests"/>'s own <c>BuildTextPackage</c>/
///     <c>BuildGeometryPaintPackage</c> pattern) and drives rendering purely through the public
///     <see cref="PptxDocument"/> surface, since this phase is pure integration of already-unit-
///     tested Phase 1c/1d/1e resolvers/painters.
/// </summary>
public class PptxRenderTests
{
    /// <summary>
    ///     Builds a full, navigable presentation package (presentation -&gt; slide -&gt; layout -&gt;
    ///     master -&gt; theme, each with its own relationships part) whose one slide's
    ///     <c>&lt;p:spTree&gt;</c> content is supplied verbatim by <paramref name="spTreeInnerXml"/>,
    ///     mirroring <see cref="PptxSystemIntegrationTests"/>'s own <c>BuildGeometryPaintPackage</c>/
    ///     <c>BuildTextPackage</c> helpers, extended with an optional embedded media part (for
    ///     picture-rendering tests).
    /// </summary>
    /// <param name="spTreeInnerXml">The slide's own <c>&lt;p:spTree&gt;</c> inner content.</param>
    /// <param name="masterTxStylesXml">The slide master's own <c>&lt;p:txStyles&gt;</c> inner content.</param>
    /// <param name="layoutPlaceholderXml">The slide layout's own <c>&lt;p:spTree&gt;</c> inner content.</param>
    /// <param name="media">
    ///     An optional embedded media part (extension, content type, bytes) referenced by the
    ///     slide's own relationship <c>rId2</c> - present only when a test's shape tree declares a
    ///     <c>&lt;p:pic&gt;</c> referencing <c>r:embed="rId2"</c>.
    /// </param>
    private static Stream BuildRenderPackage(
        string spTreeInnerXml,
        string masterTxStylesXml = "",
        string layoutPlaceholderXml = "",
        (string Extension, string ContentType, byte[] Bytes)? media = null)
    {
        var contentTypesXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              {(media is { } m ? $"""<Default Extension="{m.Extension}" ContentType="{m.ContentType}" />""" : string.Empty)}
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
                  {spTreeInnerXml}
                </p:spTree>
              </p:cSld>
            </p:sld>
            """;

        var slideRelsXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
              {(media is { } m2 ? $"""<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/image1.{m2.Extension}" />""" : string.Empty)}
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

            if (media is { } writtenMedia)
            {
                WriteBinaryEntry(archive, $"ppt/media/image1.{writtenMedia.Extension}", writtenMedia.Bytes);
            }
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

    private static void WriteBinaryEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }

    /// <summary>Builds a minimal, valid single-pixel PNG's encoded bytes, via the core <see cref="PngCodec"/>.</summary>
    private static byte[] BuildPngBytes(Rgba32 color)
    {
        using var surface = new Surface(1, 1);
        surface[0, 0] = color;
        using var buffer = new MemoryStream();
        PngCodec.Save(surface, buffer);
        return buffer.ToArray();
    }

    /// <summary>A single, full-slide-sized freeform shape with a solid red fill.</summary>
    private const string RedFullSlideShapeXml =
        """
        <p:sp>
          <p:nvSpPr>
            <p:cNvPr id="2" name="Rect"/>
            <p:cNvSpPr/>
            <p:nvPr/>
          </p:nvSpPr>
          <p:spPr>
            <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
            <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
            <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
          </p:spPr>
        </p:sp>
        """;

    // --- Basic shape rendering -----------------------------------------------------------------

    /// <summary>Proves a single, full-slide solid-filled shape paints its fill color across the rendered surface.</summary>
    [Fact]
    public void Render_SingleFullSlideShape_PaintsFillColorAcrossSurface()
    {
        using var stream = BuildRenderPackage(RedFullSlideShapeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 50]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[1, 1]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[98, 98]);
    }

    /// <summary>Proves the surface is cleared to the default opaque-white background before any shape paints, for pixels outside every shape's own footprint.</summary>
    [Fact]
    public void Render_DefaultOptions_ClearsUnpaintedAreaToOpaqueWhite()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr><p:cNvPr id="2" name="Small"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="1000000" cy="1000000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="0000FF"/></a:solidFill>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // The small blue shape occupies only the top-left ~11x15 pixel corner (1,000,000 / 9,144,000
        // * 100 ~= 10.9; 1,000,000 / 6,858,000 * 100 ~= 14.6) - the bottom-right corner is untouched.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[90, 90]);
    }

    /// <summary>Proves a caller-supplied <see cref="PptxRenderOptions.BackgroundColor"/> clears the surface to that color instead of the default opaque white.</summary>
    [Fact]
    public void Render_CustomBackgroundColor_ClearsToThatColor()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);
        var options = new PptxRenderOptions { BackgroundColor = new Rgba32(10, 20, 30, 255) };

        using var surface = document.Render(0, 20, 20, options);

        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[5, 5]);
    }

    /// <summary>Proves a fully transparent custom background color is honored (reproducing a transparent render).</summary>
    [Fact]
    public void Render_FullyTransparentBackgroundColor_ClearsToTransparent()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);
        var options = new PptxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };

        using var surface = document.Render(0, 20, 20, options);

        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[5, 5]);
    }

    /// <summary>Proves an empty shape tree renders a surface filled with only the background color (no painted shapes at all).</summary>
    [Fact]
    public void Render_EmptyShapeTree_RendersOnlyBackground()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        for (var y = 0; y < 50; y++)
        {
            for (var x = 0; x < 50; x++)
            {
                Assert.Equal(new Rgba32(255, 255, 255, 255), surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves the EMU-to-pixel base transform does not suffer integer-division truncation to
    ///     zero (a shape occupying the slide's full EMU extent must still paint at a requested
    ///     pixel size far smaller than the EMU magnitude).
    /// </summary>
    [Fact]
    public void Render_SmallPixelDimensionsAgainstLargeEmuSlideSize_DoesNotTruncateTransformToZero()
    {
        using var stream = BuildRenderPackage(RedFullSlideShapeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 4, 3);

        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[2, 1]);
    }

    /// <summary>Proves two overlapping shapes composite in document order - the later, overlapping shape paints on top of the earlier one.</summary>
    [Fact]
    public void Render_TwoOverlappingShapes_LaterShapePaintsOnTopInDocumentOrder()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr><p:cNvPr id="2" name="Back"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
              </p:spPr>
            </p:sp>
            <p:sp>
              <p:nvSpPr><p:cNvPr id="3" name="Front"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="00FF00"/></a:solidFill>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
    }

    /// <summary>Proves a shape with no resolvable <c>&lt;a:xfrm&gt;</c> anywhere is skipped silently (no exception, no painted pixels).</summary>
    [Fact]
    public void Render_ShapeWithNoXfrm_SkippedSilently()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr><p:cNvPr id="2" name="NoXfrm"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[10, 10]);
    }

    /// <summary>Proves a <c>&lt;p:cxnSp&gt;</c> connector shape (never represented in the parsed shape tree at all) is tolerated - rendering completes without error and paints nothing where the connector would have been.</summary>
    [Fact]
    public void Render_ConnectorShape_SkippedSilentlyWithoutError()
    {
        const string spTreeInnerXml =
            """
            <p:cxnSp>
              <p:nvCxnSpPr><p:cNvPr id="2" name="Connector"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="line"><a:avLst/></a:prstGeom>
                <a:ln><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:ln>
              </p:spPr>
            </p:cxnSp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                Assert.Equal(new Rgba32(255, 255, 255, 255), surface[x, y]);
            }
        }
    }

    // --- Group transform composition ------------------------------------------------------------

    /// <summary>Proves a nested group's own transform composes with its parent's, placing a child shape at the expected final surface location.</summary>
    [Fact]
    public void Render_NestedGroup_ComposesChildTransformIntoExpectedSurfaceLocation()
    {
        const string spTreeInnerXml =
            """
            <p:grpSp>
              <p:nvGrpSpPr><p:cNvPr id="2" name="Group"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
              <p:grpSpPr>
                <a:xfrm>
                  <a:off x="4572000" y="0"/><a:ext cx="4572000" cy="6858000"/>
                  <a:chOff x="0" y="0"/><a:chExt cx="4572000" cy="6858000"/>
                </a:xfrm>
              </p:grpSpPr>
              <p:sp>
                <p:nvSpPr><p:cNvPr id="3" name="Inner"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
                <p:spPr>
                  <a:xfrm><a:off x="0" y="0"/><a:ext cx="4572000" cy="6858000"/></a:xfrm>
                  <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                  <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
                </p:spPr>
              </p:sp>
            </p:grpSp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // The group occupies the slide's right half (offset by half the slide width); its inner
        // shape, spanning the full group child extent, therefore paints only the right half.
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[75, 50]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[25, 50]);
    }

    /// <summary>Proves two independently-nested groups each resolve and compose their own child transform correctly.</summary>
    [Fact]
    public void Render_DoublyNestedGroups_EachComposeOwnChildTransform()
    {
        const string spTreeInnerXml =
            """
            <p:grpSp>
              <p:nvGrpSpPr><p:cNvPr id="2" name="Outer"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
              <p:grpSpPr>
                <a:xfrm>
                  <a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/>
                  <a:chOff x="0" y="0"/><a:chExt cx="2000000" cy="2000000"/>
                </a:xfrm>
              </p:grpSpPr>
              <p:grpSp>
                <p:nvGrpSpPr><p:cNvPr id="3" name="Inner"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
                <p:grpSpPr>
                  <a:xfrm>
                    <a:off x="1000000" y="1000000"/><a:ext cx="1000000" cy="1000000"/>
                    <a:chOff x="0" y="0"/><a:chExt cx="1000000" cy="1000000"/>
                  </a:xfrm>
                </p:grpSpPr>
                <p:sp>
                  <p:nvSpPr><p:cNvPr id="4" name="Leaf"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
                  <p:spPr>
                    <a:xfrm><a:off x="0" y="0"/><a:ext cx="1000000" cy="1000000"/></a:xfrm>
                    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                    <a:solidFill><a:srgbClr val="00FF00"/></a:solidFill>
                  </p:spPr>
                </p:sp>
              </p:grpSp>
            </p:grpSp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // Outer group: child space (0,0)-(2,000,000, 2,000,000) maps to the full slide
        // (9,144,000 x 6,858,000). Inner group occupies child-space quadrant
        // (1,000,000..2,000,000, 1,000,000..2,000,000) of that - i.e. its bottom-right quadrant -
        // which the leaf shape fills entirely. Sample a point inside that composed footprint.
        var x = (int)(9144000.0 / 2000000.0 * 1500000.0 / 9144000.0 * 100);
        var y = (int)(6858000.0 / 2000000.0 * 1500000.0 / 6858000.0 * 100);
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[Math.Clamp(x, 0, 99), Math.Clamp(y, 0, 99)]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[2, 2]);
    }

    // --- Picture rendering -----------------------------------------------------------------------

    /// <summary>Proves a <c>&lt;p:pic&gt;</c> decodes and composites its embedded image onto the surface at the expected location.</summary>
    [Fact]
    public void Render_Picture_PaintsEmbeddedImageAtExpectedLocation()
    {
        var pngBytes = BuildPngBytes(new Rgba32(10, 20, 30, 255));
        const string spTreeInnerXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="2" name="Pic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:pic>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, media: ("png", "image/png", pngBytes));
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[10, 10]);
    }

    /// <summary>Proves a <c>&lt;p:pic&gt;</c> missing its own <c>&lt;p:blipFill&gt;</c> is skipped silently rather than throwing.</summary>
    [Fact]
    public void Render_PictureMissingBlipFill_SkippedSilently()
    {
        const string spTreeInnerXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="2" name="Pic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:pic>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[10, 10]);
    }

    // --- Table rendering --------------------------------------------------------------------------

    /// <summary>Proves a table's resolved cell fill paints across its own cell rectangle via the graphic frame's own direct <c>&lt;p:xfrm&gt;</c>.</summary>
    [Fact]
    public void Render_Table_PaintsCellFillAcrossCellRectangle()
    {
        const string spTreeInnerXml =
            """
            <p:graphicFrame>
              <p:nvGraphicFramePr><p:cNvPr id="2" name="Table"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>
              <p:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></p:xfrm>
              <a:graphic>
                <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/table">
                  <a:tbl>
                    <a:tblGrid><a:gridCol w="9144000"/></a:tblGrid>
                    <a:tr h="6858000">
                      <a:tc>
                        <a:tcPr><a:solidFill><a:srgbClr val="00FFFF"/></a:solidFill></a:tcPr>
                      </a:tc>
                    </a:tr>
                  </a:tbl>
                </a:graphicData>
              </a:graphic>
            </p:graphicFrame>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(0, 255, 255, 255), surface[25, 25]);
    }

    /// <summary>Proves a <c>&lt;p:graphicFrame&gt;</c> missing its own direct <c>&lt;p:xfrm&gt;</c> is skipped silently rather than throwing.</summary>
    [Fact]
    public void Render_GraphicFrameMissingXfrm_SkippedSilently()
    {
        const string spTreeInnerXml =
            """
            <p:graphicFrame>
              <p:nvGraphicFramePr><p:cNvPr id="2" name="Table"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>
              <a:graphic>
                <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/table">
                  <a:tbl>
                    <a:tblGrid><a:gridCol w="9144000"/></a:tblGrid>
                    <a:tr h="6858000">
                      <a:tc>
                        <a:tcPr><a:solidFill><a:srgbClr val="00FFFF"/></a:solidFill></a:tcPr>
                      </a:tc>
                    </a:tr>
                  </a:tbl>
                </a:graphicData>
              </a:graphic>
            </p:graphicFrame>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[10, 10]);
    }

    // --- Text rendering -------------------------------------------------------------------------

    /// <summary>Proves a non-placeholder shape's own text body paints glyph ink somewhere inside the shape's own rectangle.</summary>
    [Fact]
    public void Render_NonPlaceholderShapeWithText_PaintsGlyphInkInsideShapeRectangle()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr><p:cNvPr id="2" name="TextBox 1"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:rPr sz="4400"/><a:t>Hello</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 200, 150);

        var paintedAnyInk = false;
        for (var y = 0; y < 150 && !paintedAnyInk; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                if (surface[x, y] != new Rgba32(255, 255, 255, 255))
                {
                    paintedAnyInk = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyInk, "Expected at least one non-background pixel to be painted for the shape's text.");
    }

    /// <summary>
    ///     Proves a placeholder shape's own text content is read from the slide-level shape itself,
    ///     not from the matched layout placeholder (which declares different text, never used as
    ///     content, only as a styling-match target) - a layout placeholder supplies styling only.
    /// </summary>
    [Fact]
    public void Render_PlaceholderShape_ReadsTextFromSlideLevelShapeNotLayout()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="title" idx="1"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:rPr sz="4400"/><a:t>Slide Title</a:t></a:r></a:p>
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
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:t>Layout placeholder text (never rendered as content)</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, layoutPlaceholderXml: layoutPlaceholderXml);
        using var document = PptxDocument.Open(stream);

        // Rendering must complete without error and paint glyph ink for the slide-level shape's
        // own text content, proving placeholder text is read from the slide level, not pulled
        // from the matched layout placeholder shape used only as a styling-match target.
        using var surface = document.Render(0, 200, 150);

        var paintedAnyInk = false;
        for (var y = 0; y < 150 && !paintedAnyInk; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                if (surface[x, y] != new Rgba32(255, 255, 255, 255))
                {
                    paintedAnyInk = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyInk, "Expected the slide-level placeholder shape's own text to paint glyph ink.");
    }

    /// <summary>
    ///     Proves a placeholder shape whose own <c>&lt;p:spPr/&gt;</c> is present but empty (no
    ///     <c>&lt;a:xfrm&gt;</c>, no <c>&lt;a:prstGeom&gt;</c>/<c>&lt;a:custGeom&gt;</c> - relying
    ///     entirely on its matched layout placeholder for position/size/geometry, exactly as real-
    ///     world (non-PowerPoint-authored) <c>.pptx</c> generators commonly emit) still renders and
    ///     paints visible content, rather than being silently skipped (an empty-but-present
    ///     <c>&lt;p:spPr/&gt;</c> must not "win" the slide-vs-layout-vs-master fallback chain the
    ///     way it does for fill/line, which this project deliberately does not resolve past the
    ///     first present element - geometry/position, unlike fill/line, has no graceful "absent
    ///     means no-op" behavior, so failing to walk past an empty element here would either skip
    ///     the shape outright or throw).
    /// </summary>
    [Fact]
    public void Render_PlaceholderShapeWithEmptySpPr_InheritsXfrmAndGeometryFromLayout()
    {
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
                <a:p><a:r><a:rPr sz="4400"/><a:t>Slide Title</a:t></a:r></a:p>
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
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:t>Layout placeholder text (never rendered as content)</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, layoutPlaceholderXml: layoutPlaceholderXml);
        using var document = PptxDocument.Open(stream);

        // Rendering must complete without error (no skip, no InvalidDataException from unresolved
        // geometry) and paint glyph ink for the slide-level shape's own text content, proving both
        // <a:xfrm> and <a:prstGeom> were inherited from the matched layout placeholder past the
        // slide's own empty <p:spPr/>.
        using var surface = document.Render(0, 200, 150);

        var paintedAnyInk = false;
        for (var y = 0; y < 150 && !paintedAnyInk; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                if (surface[x, y] != new Rgba32(255, 255, 255, 255))
                {
                    paintedAnyInk = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyInk, "Expected the placeholder's inherited geometry/position to allow its own text to paint glyph ink.");
    }

    // --- Argument validation ---------------------------------------------------------------------

    /// <summary>Proves <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/> rejects a negative slide index.</summary>
    [Fact]
    public void Render_NegativeSlideIndex_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(-1, 10, 10));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/> rejects a slide index at/beyond <see cref="PptxDocument.SlideCount"/>.</summary>
    [Fact]
    public void Render_SlideIndexAtSlideCount_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(1, 10, 10));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/> propagates <see cref="Surface"/>'s own non-positive-dimension rejection.</summary>
    [Fact]
    public void Render_NonPositiveWidth_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 0, 10));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/> propagates <see cref="Surface"/>'s own non-positive-dimension rejection for height.</summary>
    [Fact]
    public void Render_NonPositiveHeight_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 10, 0));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/> throws <see cref="ObjectDisposedException"/> once the document has been disposed.</summary>
    [Fact]
    public void Render_DisposedDocument_ThrowsObjectDisposedException()
    {
        var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        var document = PptxDocument.Open(stream);
        document.Dispose();

        Assert.Throws<ObjectDisposedException>(() => document.Render(0, 10, 10));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/> rejects a non-positive DPI.</summary>
    [Fact]
    public void Render_Dpi_NonPositiveDpi_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, -96f));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/> rejects a non-finite DPI.</summary>
    [Fact]
    public void Render_Dpi_NonFiniteDpi_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, float.PositiveInfinity));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/> rejects a negative slide index.</summary>
    [Fact]
    public void Render_Dpi_NegativeSlideIndex_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(-1, 96f));
    }

    /// <summary>Proves <see cref="PptxDocument.Render(int, float, PptxRenderOptions?)"/> rejects a slide index at/beyond <see cref="PptxDocument.SlideCount"/>.</summary>
    [Fact]
    public void Render_Dpi_SlideIndexAtSlideCount_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(1, 96f));
    }

    // --- DPI convenience overload -----------------------------------------------------------------

    /// <summary>
    ///     Proves the DPI overload computes the expected pixel width/height from the slide's own
    ///     EMU size (9,144,000 x 6,858,000 EMU = 10in x 7.5in at 914,400 EMU/inch), preserving the
    ///     slide's own aspect ratio.
    /// </summary>
    [Fact]
    public void Render_DpiOverload_ComputesExpectedPixelDimensionsFromSlideSize()
    {
        using var stream = BuildRenderPackage(RedFullSlideShapeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 96f);

        // 10in * 96dpi = 960px; 7.5in * 96dpi = 720px.
        Assert.Equal(960, surface.Width);
        Assert.Equal(720, surface.Height);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[480, 360]);
    }
}
