using System.IO.Compression;
using System.Linq;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx sppr nvsppr nvpr cnvpr cnvsppr grpsp nvgrpsppr grpsppr cxnsp nvcxnsppr cnvcxnsppr
// cspell:ignore nvpicpr nvpr cnvpicpr blipfill srcrect embed graphicframe tbl tblgrid gridcol tcpr srgb
// cspell:ignore calibri txbox xfrm prst Xfrm hlink Hlink cust pythonpptx bbox

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
    /// <param name="slideBackgroundXml">
    ///     The slide's own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> inner content (a <c>&lt;p:bgPr&gt;</c>
    ///     or <c>&lt;p:bgRef&gt;</c> element), or empty (the default) to omit <c>&lt;p:bg&gt;</c>
    ///     from the slide entirely.
    /// </param>
    /// <param name="layoutBackgroundXml">The slide's layout's own <c>&lt;p:bg&gt;</c> inner content - see <paramref name="slideBackgroundXml"/>.</param>
    /// <param name="masterBackgroundXml">That layout's master's own <c>&lt;p:bg&gt;</c> inner content - see <paramref name="slideBackgroundXml"/>.</param>
    /// <param name="themeBgFillStyleListXml">
    ///     The theme's own <c>&lt;a:fmtScheme&gt;/&lt;a:bgFillStyleLst&gt;</c> inner content (one or
    ///     more raw fill-definition elements, e.g. <c>&lt;a:solidFill&gt;...&lt;/a:solidFill&gt;</c>),
    ///     or empty (the default) to omit <c>&lt;a:fmtScheme&gt;</c> from the theme entirely - only
    ///     needed by a <c>&lt;p:bgRef&gt;</c>-based test.
    /// </param>
    /// <param name="masterShapeTreeXml">
    ///     The slide master's own <c>&lt;p:spTree&gt;</c> inner content, or empty (the default) for
    ///     an empty master shape tree - used (Phase 2 Follow-Up) by master decorative-shape tests.
    /// </param>
    /// <param name="masterMedia">
    ///     An optional embedded media part (extension, content type, bytes) owned by the slide
    ///     <em>master</em> (its own <c>ppt/slideMasters/_rels/slideMaster1.xml.rels</c>, referenced
    ///     via <c>r:embed="rId2"</c>) - present only when <paramref name="masterShapeTreeXml"/>
    ///     declares a <c>&lt;p:pic&gt;</c> referencing <c>r:embed="rId2"</c>, proving a
    ///     master-owned picture's relationship resolves against the master's own <c>.rels</c> file
    ///     rather than the slide's (see the companion planning report's <c>ownerPartPath</c>
    ///     bug-fix rationale).
    /// </param>
    /// <param name="layoutMedia">
    ///     An optional embedded media part (extension, content type, bytes) owned by the slide
    ///     <em>layout</em> (its own <c>ppt/slideLayouts/_rels/slideLayout1.xml.rels</c>, referenced
    ///     via <c>r:embed="rId2"</c>) - present only when <paramref name="layoutPlaceholderXml"/>
    ///     declares a <c>&lt;p:pic&gt;</c> referencing <c>r:embed="rId2"</c>. See
    ///     <paramref name="masterMedia"/>'s own remarks.
    /// </param>
    /// <param name="slideClrMapOvrXml">
    ///     The slide's own raw <c>&lt;p:clrMapOvr&gt;</c> inner content (an <c>&lt;a:overrideClrMapping
    ///     .../&gt;</c> or <c>&lt;a:masterClrMapping/&gt;</c> element), or empty (the default) to
    ///     omit <c>&lt;p:clrMapOvr&gt;</c> from the slide entirely - used by the
    ///     <c>&lt;p:clrMap&gt;</c>/<c>&lt;p:clrMapOvr&gt;</c> color-map fix's own render-level
    ///     regression test.
    /// </param>
    /// <param name="themeFillStyleListXml">
    ///     The theme's own <c>&lt;a:fmtScheme&gt;/&lt;a:fillStyleLst&gt;</c> inner content, or
    ///     empty (the default) to omit it - used by a <c>&lt;p:style&gt;/&lt;a:fillRef&gt;</c>-based
    ///     test (Phase 2 Follow-Up shape style references).
    /// </param>
    /// <param name="themeLnStyleListXml">
    ///     The theme's own <c>&lt;a:fmtScheme&gt;/&lt;a:lnStyleLst&gt;</c> inner content, or empty
    ///     (the default) to omit it - used by a <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c>-based test.
    /// </param>
    /// <param name="slideWidthEmu">
    ///     The presentation's own <c>&lt;p:sldSz cx="..."/&gt;</c> width, in EMU, or
    ///     <c>9144000</c> (the default - a standard 10" 4:3 slide width) - used by the default-
    ///     line-width render regression test to shrink the slide so a sub-pixel-at-the-default-
    ///     scale 9525 EMU stroke still renders as several solid pixels wide.
    /// </param>
    /// <param name="slideHeightEmu">
    ///     The presentation's own <c>&lt;p:sldSz cy="..."/&gt;</c> height, in EMU, or
    ///     <c>6858000</c> (the default - a standard 7.5" 4:3 slide height) - see
    ///     <paramref name="slideWidthEmu"/>'s own remarks.
    /// </param>
    private static Stream BuildRenderPackage(
        string spTreeInnerXml,
        string masterTxStylesXml = "",
        string layoutPlaceholderXml = "",
        (string Extension, string ContentType, byte[] Bytes)? media = null,
        string slideBackgroundXml = "",
        string layoutBackgroundXml = "",
        string masterBackgroundXml = "",
        string themeBgFillStyleListXml = "",
        string masterShapeTreeXml = "",
        (string Extension, string ContentType, byte[] Bytes)? masterMedia = null,
        (string Extension, string ContentType, byte[] Bytes)? layoutMedia = null,
        string slideClrMapOvrXml = "",
        string themeFillStyleListXml = "",
        string themeLnStyleListXml = "",
        float slideWidthEmu = 9144000f,
        float slideHeightEmu = 6858000f)
    {
        // Distinct <Default Extension=".../> content-type entries, one per distinct extension
        // across all three possible media owners (slide/layout/master), avoiding a duplicate
        // Default entry when two owners happen to share the same extension/content-type.
        var allMedia = new[] { media, layoutMedia, masterMedia };
        var distinctMediaDefaults = allMedia
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .GroupBy(m => m.Extension)
            .Select(g => g.First());

        var contentTypesXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              {string.Join("\n  ", distinctMediaDefaults.Select(m => $"""<Default Extension="{m.Extension}" ContentType="{m.ContentType}" />"""))}
            </Types>
            """;

        const string packageRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
            </Relationships>
            """;

        var presentationXml =
            $"""
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:sldSz cx="{slideWidthEmu:F0}" cy="{slideHeightEmu:F0}"/>
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
                {(slideBackgroundXml.Length > 0 ? $"""<p:bg>{slideBackgroundXml}</p:bg>""" : string.Empty)}
                <p:spTree>
                  {spTreeInnerXml}
                </p:spTree>
              </p:cSld>
              {(slideClrMapOvrXml.Length > 0 ? $"""<p:clrMapOvr>{slideClrMapOvrXml}</p:clrMapOvr>""" : string.Empty)}
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
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cSld>
                {(layoutBackgroundXml.Length > 0 ? $"""<p:bg>{layoutBackgroundXml}</p:bg>""" : string.Empty)}
                <p:spTree>{layoutPlaceholderXml}</p:spTree>
              </p:cSld>
            </p:sldLayout>
            """;

        var layoutRelsXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
              {(layoutMedia is { } lm ? $"""<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/layoutImage1.{lm.Extension}" />""" : string.Empty)}
            </Relationships>
            """;

        var masterXml =
            $"""
            <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cSld>
                {(masterBackgroundXml.Length > 0 ? $"""<p:bg>{masterBackgroundXml}</p:bg>""" : string.Empty)}
                <p:spTree>{masterShapeTreeXml}</p:spTree>
              </p:cSld>
              <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2"/>
              <p:txStyles>{masterTxStylesXml}</p:txStyles>
            </p:sldMaster>
            """;

        var masterRelsXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml" />
              {(masterMedia is { } mm ? $"""<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/masterImage1.{mm.Extension}" />""" : string.Empty)}
            </Relationships>
            """;

        var fillStyleLstXml = themeFillStyleListXml.Length > 0 ? $"<a:fillStyleLst>{themeFillStyleListXml}</a:fillStyleLst>" : string.Empty;
        var lnStyleLstXml = themeLnStyleListXml.Length > 0 ? $"<a:lnStyleLst>{themeLnStyleListXml}</a:lnStyleLst>" : string.Empty;
        var bgFillStyleLstXml = themeBgFillStyleListXml.Length > 0 ? $"<a:bgFillStyleLst>{themeBgFillStyleListXml}</a:bgFillStyleLst>" : string.Empty;
        var fmtSchemeXml = themeBgFillStyleListXml.Length > 0 || themeFillStyleListXml.Length > 0 || themeLnStyleListXml.Length > 0
            ? $"""
              <a:fmtScheme name="TestFormat">
                {fillStyleLstXml}
                {lnStyleLstXml}
                {bgFillStyleLstXml}
              </a:fmtScheme>
              """
            : string.Empty;

        var themeXml =
            $$"""
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
                {{fmtSchemeXml}}
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

            if (layoutMedia is { } writtenLayoutMedia)
            {
                WriteBinaryEntry(archive, $"ppt/media/layoutImage1.{writtenLayoutMedia.Extension}", writtenLayoutMedia.Bytes);
            }

            if (masterMedia is { } writtenMasterMedia)
            {
                WriteBinaryEntry(archive, $"ppt/media/masterImage1.{writtenMasterMedia.Extension}", writtenMasterMedia.Bytes);
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

    // --- Slide/layout/master background fill (<p:bg>) -----------------------------------------

    /// <summary>A <c>&lt;p:bgPr&gt;</c> inner content declaring an opaque solid green fill.</summary>
    private const string GreenSolidBgPrXml =
        """<p:bgPr><a:solidFill><a:srgbClr val="00FF00"/></a:solidFill><a:effectLst/></p:bgPr>""";

    /// <summary>Proves a slide's own solid-fill <c>&lt;p:bg&gt;</c> paints across the full slide, beneath (and visible outside) its own shape tree.</summary>
    [Fact]
    public void Render_SlideLevelSolidBackground_PaintsAcrossFullSlide()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty, slideBackgroundXml: GreenSolidBgPrXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[49, 49]);
    }

    /// <summary>Proves a slide's own background fill paints first, with its own shape tree content still painting on top of it.</summary>
    [Fact]
    public void Render_SlideLevelBackgroundWithShape_ShapePaintsOnTopOfBackground()
    {
        const string smallBlueShapeXml =
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
        using var stream = BuildRenderPackage(smallBlueShapeXml, slideBackgroundXml: GreenSolidBgPrXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // The small blue shape occupies only the top-left corner (see Render_DefaultOptions_
        // ClearsUnpaintedAreaToOpaqueWhite's own remarks for the EMU -> pixel math) - it must
        // paint on top of the green background there, while the green background remains visible
        // everywhere else.
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[1, 1]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[90, 90]);
    }

    /// <summary>Proves a layout's own <c>&lt;p:bg&gt;</c> paints when the slide itself declares none.</summary>
    [Fact]
    public void Render_LayoutLevelSolidBackground_PaintsWhenSlideDeclaresNone()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty, layoutBackgroundXml: GreenSolidBgPrXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
    }

    /// <summary>Proves a master's own <c>&lt;p:bg&gt;</c> paints when neither the slide nor its layout declares one.</summary>
    [Fact]
    public void Render_MasterLevelSolidBackground_PaintsWhenNeitherSlideNorLayoutDeclaresOne()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty, masterBackgroundXml: GreenSolidBgPrXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
    }

    /// <summary>Proves the slide's own <c>&lt;p:bg&gt;</c> takes priority over both its layout's and its master's own <c>&lt;p:bg&gt;</c>.</summary>
    [Fact]
    public void Render_SlideLevelBackground_TakesPriorityOverLayoutAndMasterBackgrounds()
    {
        const string blueSolidBgPrXml = """<p:bgPr><a:solidFill><a:srgbClr val="0000FF"/></a:solidFill><a:effectLst/></p:bgPr>""";
        const string redSolidBgPrXml = """<p:bgPr><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill><a:effectLst/></p:bgPr>""";
        using var stream = BuildRenderPackage(
            spTreeInnerXml: string.Empty,
            slideBackgroundXml: GreenSolidBgPrXml,
            layoutBackgroundXml: blueSolidBgPrXml,
            masterBackgroundXml: redSolidBgPrXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
    }

    /// <summary>Proves a layout's own <c>&lt;p:bg&gt;</c> takes priority over its master's own <c>&lt;p:bg&gt;</c> when the slide itself declares none.</summary>
    [Fact]
    public void Render_LayoutLevelBackground_TakesPriorityOverMasterBackgroundWhenSlideDeclaresNone()
    {
        const string redSolidBgPrXml = """<p:bgPr><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill><a:effectLst/></p:bgPr>""";
        using var stream = BuildRenderPackage(
            spTreeInnerXml: string.Empty,
            layoutBackgroundXml: GreenSolidBgPrXml,
            masterBackgroundXml: redSolidBgPrXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
    }

    /// <summary>Proves that when none of the slide/layout/master declare a <c>&lt;p:bg&gt;</c>, rendering falls back unchanged to <see cref="PptxRenderOptions.BackgroundColor"/>.</summary>
    [Fact]
    public void Render_NoBackgroundAnywhere_FallsBackToOptionsBackgroundColor()
    {
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty);
        using var document = PptxDocument.Open(stream);
        var options = new PptxRenderOptions { BackgroundColor = new Rgba32(10, 20, 30, 255) };

        using var surface = document.Render(0, 50, 50, options);

        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[25, 25]);
    }

    /// <summary>Proves a slide's own theme-indexed <c>&lt;p:bgRef&gt;</c> background resolves the matched <c>&lt;a:bgFillStyleLst&gt;</c> entry, substituting its own color child for the entry's <c>phClr</c> token.</summary>
    [Fact]
    public void Render_SlideLevelThemeIndexedBackgroundReference_ResolvesBgFillStyleListEntryWithPhClrSubstitution()
    {
        // idx 1001 is the bgFillStyleLst's first (0-based) entry; its own phClr token is
        // substituted with the <p:bgRef>'s own srgbClr child (FF00FF, magenta).
        const string bgRefXml = """<p:bgRef idx="1001"><a:srgbClr val="FF00FF"/></p:bgRef>""";
        const string bgFillStyleListXml = """<a:solidFill><a:schemeClr val="phClr"/></a:solidFill>""";
        using var stream = BuildRenderPackage(
            spTreeInnerXml: string.Empty,
            slideBackgroundXml: bgRefXml,
            themeBgFillStyleListXml: bgFillStyleListXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(255, 0, 255, 255), surface[25, 25]);
    }

    /// <summary>Proves a <c>&lt;p:bgRef idx="0"&gt;</c> (the ECMA-376 "no background" sentinel) paints nothing, falling through to the default opaque-white clear.</summary>
    [Fact]
    public void Render_SlideLevelThemeIndexedBackgroundReferenceIdxZero_PaintsNoBackground()
    {
        const string bgRefXml = """<p:bgRef idx="0"><a:srgbClr val="FF00FF"/></p:bgRef>""";
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty, slideBackgroundXml: bgRefXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 50, 50);

        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[25, 25]);
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

    /// <summary>
    ///     Proves a <c>&lt;p:cxnSp&gt;</c> straight connector (Phase 2 Follow-Up: Connector Shape
    ///     Rendering) actually paints a visible diagonal line - the real, positive replacement for
    ///     the old "connectors are silently skipped" behavior this phase removes.
    /// </summary>
    [Fact]
    public void Render_StraightConnector_PaintsDiagonalLineAtExpectedPixels()
    {
        const string spTreeInnerXml =
            """
            <p:cxnSp>
              <p:nvCxnSpPr><p:cNvPr id="2" name="Connector"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="straightConnector1"><a:avLst/></a:prstGeom>
                <a:ln w="914400"><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:ln>
              </p:spPr>
            </p:cxnSp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // The connector's own local geometry runs from (0,0) to (w,h) - its full diagonal, here
        // coincident with the slide's own full extent - so its midpoint lands at pixel (50,50).
        // A point well off that diagonal (the top-right corner) must stay unpainted.
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 50]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[95, 5]);
    }

    /// <summary>
    ///     Proves the zero-height (perfectly horizontal) connector bug fix: a connector whose own
    ///     <c>&lt;a:ext cy="0"/&gt;</c> (confirmed in the real-world
    ///     <c>pythonpptx-shp-connector-props.pptx</c> fixture's own second slide) still paints its
    ///     horizontal line rather than degrading to <see cref="Geometry.Path.Empty"/> under the
    ///     ordinary preset-geometry "non-positive bounding box" guard.
    /// </summary>
    [Fact]
    public void Render_ZeroHeightHorizontalConnector_PaintsLineWithoutDegradingToEmpty()
    {
        const string spTreeInnerXml =
            """
            <p:cxnSp>
              <p:nvCxnSpPr><p:cNvPr id="2" name="Connector"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>
              <p:spPr>
                <a:xfrm><a:off x="1000000" y="3000000"/><a:ext cx="5000000" cy="0"/></a:xfrm>
                <a:prstGeom prst="straightConnector1"><a:avLst/></a:prstGeom>
                <a:ln w="457200"><a:solidFill><a:srgbClr val="0000FF"/></a:solidFill></a:ln>
              </p:spPr>
            </p:cxnSp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // 1000000 EMU is roughly 10.9px (x), and 3000000 EMU is roughly 43.8px (y, out of the
        // slide's 6858000 EMU height), within a 9144000x6858000 slide rendered at 100x100 pixels.
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[33, 43]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[33, 10]);
    }

    /// <summary>
    ///     Proves a connector's own <c>&lt;a:tailEnd type="triangle"/&gt;</c> arrowhead actually
    ///     paints extra ink beyond the plain stroked line width - the triangle's tip sits at the
    ///     connector's own end point and its base flares out well past the line's own thickness.
    /// </summary>
    [Fact]
    public void Render_ConnectorWithTailArrowhead_PaintsArrowheadNearEndpoint()
    {
        const string spTreeInnerXml =
            """
            <p:cxnSp>
              <p:nvCxnSpPr><p:cNvPr id="2" name="Connector"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>
              <p:spPr>
                <a:xfrm><a:off x="1000000" y="1000000"/><a:ext cx="0" cy="4000000"/></a:xfrm>
                <a:prstGeom prst="straightConnector1"><a:avLst/></a:prstGeom>
                <a:ln w="182880"><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill><a:tailEnd type="triangle" w="lg" len="lg"/></a:ln>
              </p:spPr>
            </p:cxnSp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 200, 200);

        // The connector is a vertical line (cx=0) from (1000000,1000000) to (1000000,5000000) EMU
        // - rendered at 200x200px against a 9144000x6858000 slide, that is roughly (21.9, 29.2)
        // to (21.9, 145.8) in pixel space, centered on x=22. A thin (182880 EMU, about 4px) line
        // alone would never paint more than ~2px either side of that centerline; the "lg" triangle
        // tailEnd arrowhead flares out much further than that just above its tip at the endpoint
        // - assert a pixel well off the centerline (x=15, 7px off-center), just above the endpoint
        // (within the arrowhead's own flared base, not the plain line), is red.
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[15, 122]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[15, 60]);
    }

    /// <summary>
    ///     Proves the real-world-corpus crash is fixed: a shape using
    ///     <c>&lt;a:prstGeom prst="curvedUpArrow"/&gt;</c> (previously unsupported, throwing
    ///     <see cref="PptxUnsupportedFeatureException"/> for the entire slide) renders without
    ///     throwing, and paints ink (not merely the unpainted background) within its own shape
    ///     rectangle.
    /// </summary>
    [Fact]
    public void Render_CurvedUpArrowShape_RendersWithoutThrowingAndPaintsInk()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr><p:cNvPr id="2" name="CurvedUpArrow"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="curvedUpArrow"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        var paintedSomeInk = false;
        for (var y = 0; y < 100 && !paintedSomeInk; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                if (surface[x, y] == new Rgba32(255, 0, 0, 255))
                {
                    paintedSomeInk = true;
                    break;
                }
            }
        }

        Assert.True(paintedSomeInk, "curvedUpArrow shape should have painted at least one red pixel.");
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

    /// <summary>
    ///     Proves a <c>&lt;p:pic&gt;</c> declaring <c>&lt;a:prstGeom prst="ellipse"&gt;</c> is
    ///     clipped to the ellipse: a bounding-box corner (well outside the ellipse) remains the
    ///     slide's background color, while the shape's own center paints the embedded image's
    ///     color - this is the confirmed real-world bug (an unclipped full rectangle) this fix
    ///     resolves.
    /// </summary>
    [Fact]
    public void Render_PictureEllipseGeometry_ClipsImageToEllipticalRegion()
    {
        var pngBytes = BuildPngBytes(new Rgba32(10, 20, 30, 255));
        const string spTreeInnerXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="2" name="Pic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="ellipse"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:pic>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, media: ("png", "image/png", pngBytes));
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        // Well inside the ellipse: the embedded image's own color.
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[10, 10]);
        // Bounding-box corners, well outside the ellipse: the slide's background color.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[19, 19]);
    }

    /// <summary>
    ///     Regression guard using the real-world corpus fixture's own exact shape geometry
    ///     (<c>&lt;a:xfrm&gt;</c>/<c>&lt;a:prstGeom prst="ellipse"&gt;</c>/<c>&lt;a:srcRect&gt;</c>/
    ///     red <c>&lt;a:ln&gt;</c> transcribed verbatim from the bug report), rendered at the
    ///     report's own 16:9 slide size (<c>12192000 x 6858000</c> EMU, 1280x720px) - a near-square
    ///     (<c>4678591 x 5053859</c> EMU, ratio ~0.9256) ellipse that the report claimed rendered
    ///     with an unclipped rectangular <em>top</em> while the bottom clipped correctly. A direct
    ///     mask-boundary-trace overlay against the real rendered output (see
    ///     <c>.agent-logs/planning-picture-ellipse-clip-bug-reverify-7c3e1a.md</c>) proved the clip
    ///     mask is in fact pixel-correct and fully symmetric on all four sides - the reported
    ///     asymmetry was a visual illusion caused by the embedded photo's own light background
    ///     color blending into the slide's background near the ellipse's top edge, not a code
    ///     defect. This test asserts all four bounding-box corners (not just one diagonal pair, as
    ///     the pre-existing <see cref="Render_PictureEllipseGeometry_ClipsImageToEllipticalRegion"/>
    ///     does) remain the slide's background color, including a point in the upper quarter of the
    ///     bounding box that sits outside the ellipse's own curve (the specific region the bug
    ///     report claimed was wrongly left unclipped), while the shape's own center still paints
    ///     the embedded image's color.
    /// </summary>
    [Fact]
    public void Render_PictureEllipseGeometry_NearSquareRealWorldAspect_ClipsAllFourBoundingBoxCorners()
    {
        var pngBytes = BuildPngBytes(new Rgba32(10, 20, 30, 255));
        const string spTreeInnerXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="2" name="Pic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill>
                <a:blip r:embed="rId2"/>
                <a:srcRect l="-4697" t="-3319" r="-3698" b="583"/>
              </p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="406259" y="1259164"/><a:ext cx="4678591" cy="5053859"/></a:xfrm>
                <a:prstGeom prst="ellipse"><a:avLst/></a:prstGeom>
                <a:ln><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:ln>
              </p:spPr>
            </p:pic>
            """;

        // The report's own 16:9 presentation <p:sldSz>.
        const float slideWidthEmu = 12192000f;
        const float slideHeightEmu = 6858000f;
        const int renderWidthPx = 1280;
        const int renderHeightPx = 720;

        using var stream = BuildRenderPackage(
            spTreeInnerXml, media: ("png", "image/png", pngBytes),
            slideWidthEmu: slideWidthEmu, slideHeightEmu: slideHeightEmu);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, renderWidthPx, renderHeightPx);

        // Compute the shape's own bounding box in device-pixel space algebraically from its EMU
        // <a:xfrm>, rather than hard-coding magic pixel literals.
        var scaleX = renderWidthPx / slideWidthEmu;
        var scaleY = renderHeightPx / slideHeightEmu;
        const float offXEmu = 406259f;
        const float offYEmu = 1259164f;
        const float extCxEmu = 4678591f;
        const float extCyEmu = 5053859f;
        var bboxLeft = offXEmu * scaleX;
        var bboxTop = offYEmu * scaleY;
        var bboxRight = (offXEmu + extCxEmu) * scaleX;
        var bboxBottom = (offYEmu + extCyEmu) * scaleY;
        var centerX = (bboxLeft + bboxRight) / 2f;
        var centerY = (bboxTop + bboxBottom) / 2f;

        // Sampled a small margin inward from each bounding-box edge, well clear of the clip
        // mask's own anti-aliased boundary.
        const float marginPx = 8f;
        var background = new Rgba32(255, 255, 255, 255);
        var imageColor = new Rgba32(10, 20, 30, 255);

        // All four bounding-box corners - including both top corners, the specific region the
        // bug report claimed was wrongly rendered as an unclipped rectangle.
        Assert.Equal(background, surface[(int)(bboxLeft + marginPx), (int)(bboxTop + marginPx)]);
        Assert.Equal(background, surface[(int)(bboxRight - marginPx), (int)(bboxTop + marginPx)]);
        Assert.Equal(background, surface[(int)(bboxLeft + marginPx), (int)(bboxBottom - marginPx)]);
        Assert.Equal(background, surface[(int)(bboxRight - marginPx), (int)(bboxBottom - marginPx)]);

        // A point in the upper quarter of the bounding box, near its left/right edge (normalized
        // ellipse-local u ~= 0.9, v ~= -0.7; u^2 + v^2 ~= 1.3 > 1, i.e. geometrically outside the
        // ellipse's own curve while still inside the bounding box) - the single assertion most
        // directly aimed at the bug report's literal claim that the top of the shape rendered as
        // an unclipped rectangle.
        var halfWidth = (bboxRight - bboxLeft) / 2f;
        var halfHeight = (bboxBottom - bboxTop) / 2f;
        var upperOutsidePointX = centerX + 0.9f * halfWidth;
        var upperOutsidePointY = centerY - 0.7f * halfHeight;
        Assert.Equal(background, surface[(int)upperOutsidePointX, (int)upperOutsidePointY]);

        // The shape's own center: well inside the ellipse, the embedded image's own color.
        Assert.Equal(imageColor, surface[(int)centerX, (int)centerY]);
    }

    /// <summary>
    ///     Proves a <c>&lt;p:pic&gt;</c> declaring an explicit <c>&lt;a:prstGeom prst="rect"&gt;</c>
    ///     still paints the full, unclipped bounding-box rectangle - strengthens
    ///     <see cref="Render_Picture_PaintsEmbeddedImageAtExpectedLocation"/> (which only samples
    ///     the center) into an explicit no-regression proof by also sampling a bounding-box corner.
    /// </summary>
    [Fact]
    public void Render_PictureRectGeometry_PaintsFullBoundingBoxUnclipped()
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
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[19, 19]);
    }

    /// <summary>
    ///     Proves a <c>&lt;p:pic&gt;</c> whose <c>&lt;p:spPr&gt;</c> declares no
    ///     <c>&lt;a:prstGeom&gt;</c>/<c>&lt;a:custGeom&gt;</c> at all still paints the full,
    ///     unclipped bounding-box rectangle - a defensive, schema-edge-case regression guard (see
    ///     <see cref="PptxDocument.ResolvePictureClipPath"/>'s own remarks).
    /// </summary>
    [Fact]
    public void Render_PictureNoPrstGeom_PaintsFullBoundingBoxUnclipped()
    {
        var pngBytes = BuildPngBytes(new Rgba32(10, 20, 30, 255));
        const string spTreeInnerXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="2" name="Pic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
              </p:spPr>
            </p:pic>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, media: ("png", "image/png", pngBytes));
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[10, 10]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[19, 19]);
    }

    /// <summary>
    ///     Proves a <c>&lt;p:pic&gt;</c> declaring <c>&lt;a:prstGeom prst="roundRect"&gt;</c> is
    ///     clipped to the rounded-rectangle outline: a bounding-box corner (outside the rounded
    ///     corner's radius) remains the slide's background color, while the shape's own center
    ///     still paints the embedded image's color - the task's required "at least one other
    ///     common non-rect preset" case.
    /// </summary>
    [Fact]
    public void Render_PictureRoundRectGeometry_ClipsImageCorners()
    {
        var pngBytes = BuildPngBytes(new Rgba32(10, 20, 30, 255));
        const string spTreeInnerXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="2" name="Pic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="6096000" cy="6096000"/></a:xfrm>
                <a:prstGeom prst="roundRect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:pic>
            """;

        // A square slide/shape (rather than the usual 4:3 slide) keeps the roundRect preset's own
        // circular corner arc (radius = min(w,h)/6 in local, pre-transform space) circular in
        // device space too - avoiding the anisotropic x/y scale a non-square slide would otherwise
        // apply to that arc, which would make an exact-corner-pixel sample unreliable.
        using var stream = BuildRenderPackage(
            spTreeInnerXml, media: ("png", "image/png", pngBytes), slideWidthEmu: 6096000f, slideHeightEmu: 6096000f);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // Well inside the rounded rectangle (its own center): the embedded image's own color.
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[50, 50]);
        // Well inside the excluded corner nook (radius is 100px/6 ~= 16.7px; sampled well short of
        // the arc itself), outside the rounded corner's own radius: background.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[2, 2]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[97, 97]);
    }

    /// <summary>
    ///     Not a permanent regression guard (the <c>[Fact]</c> tests above already provide those) -
    ///     renders the same ellipse-clipped picture end-to-end through the public
    ///     <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/> API and saves it
    ///     to an inspectable PNG file (<c>.agent-logs/pptx-picture-ellipse-clip-repro.png</c>) for
    ///     a human reviewer to open and visually confirm the circular photo-crop effect, mirroring
    ///     <c>PptxTextLayoutTests.cs</c>'s own <c>GeneratePlusMinusDegreeTofuReproPng</c> precedent.
    /// </summary>
    [Fact]
    public void GeneratePictureEllipseClipReproPng()
    {
        var pngBytes = BuildPngBytes(new Rgba32(10, 20, 30, 255));
        const string spTreeInnerXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="2" name="Pic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="ellipse"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:pic>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, media: ("png", "image/png", pngBytes));
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 300, 300);

        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".agent-logs");
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "pptx-picture-ellipse-clip-repro.png");

        // Three target-framework test processes may run this test concurrently against the same
        // shared output path - render to a process-unique temp file first (identical content
        // regardless of which TFM wins), then best-effort copy it into place, tolerating (rather
        // than failing on) a transient sharing violation from a sibling process doing the same
        // thing at the same moment.
        var tempPath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}.png.tmp");
        PngCodec.Save(surface, tempPath);
        try
        {
            File.Copy(tempPath, outputPath, overwrite: true);
        }
        catch (IOException)
        {
            // A sibling TFM process is writing/has already written the identical content -
            // nothing further to do here.
        }
        finally
        {
            File.Delete(tempPath);
        }

        Assert.True(File.Exists(outputPath));
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

    /// <summary>
    ///     Proves a ctrTitle placeholder whose slide-level <c>&lt;a:lstStyle/&gt;</c> is present but
    ///     empty (no level-override child - the exact shape reported from "ERF IWF Breadboard Peer
    ///     Review.pptx") resolves its run's effective font size from the idx-matched layout
    ///     placeholder's own <c>&lt;a:lstStyle&gt;</c>/<c>&lt;a:lvl1pPr&gt;</c>/<c>&lt;a:defRPr
    ///     sz="6000"&gt;</c> (60pt), not the master's <c>&lt;p:titleStyle&gt;</c> fallback (28pt) -
    ///     the exact pre-fix symptom magnitude described in the bug report.
    /// </summary>
    [Fact]
    public void ResolveEffectiveRunProperties_CtrTitlePlaceholderWithEmptySlideLstStyle_ResolvesLayoutDefRPrSizeNotMasterTitleStyleFallback()
    {
        const string masterTxStylesXml =
            """
            <p:titleStyle><a:lvl1pPr><a:defRPr sz="2800"/></a:lvl1pPr></p:titleStyle>
            """;
        const string layoutPlaceholderXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="ctrTitle" hasCustomPrompt="1"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="685800" y="2130425"/><a:ext cx="7772400" cy="2259013"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
              <p:txBody>
                <a:bodyPr anchor="b"/>
                <a:lstStyle><a:lvl1pPr algn="l"><a:defRPr sz="6000"/></a:lvl1pPr></a:lstStyle>
                <a:p><a:r><a:t>Click to edit Master title style</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;

        static string BuildSlideXml(string lstStyleXml) =>
            $"""
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title 1"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="ctrTitle"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr/>
              <p:txBody>
                <a:bodyPr/>
                {lstStyleXml}
                <a:p><a:r><a:rPr lang="en-US"/><a:t>Fluidic Schematics</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;

        using var stream = BuildRenderPackage(BuildSlideXml("<a:lstStyle/>"), masterTxStylesXml, layoutPlaceholderXml);
        using var document = PptxDocument.Open(stream);

        var slide = document.GetSlide(0);
        var layout = document.GetLayout(slide.LayoutPartPath);
        var master = document.GetMaster(layout.MasterPartPath);
        var theme = document.GetTheme(master.ThemePartPath);
        var slidePlaceholder = slide.Placeholders[0];

        var placeholderProperties = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, layout.Placeholders, master.Placeholders, theme, master.TxStyles);

        var txBody = slidePlaceholder.ShapeElement.Element("{http://schemas.openxmlformats.org/presentationml/2006/main}txBody")!;
        var textBody = PptxDocument.ParseTextBody(txBody);
        var paragraph = textBody.Paragraphs[0];
        var run = paragraph.Runs[0];

        var effective = PptxDocument.ResolveEffectiveRunProperties(
            run, paragraph, placeholderProperties, theme,
            placeholderProperties.EffectivePlaceholderType ?? slidePlaceholder.Type);

        Assert.Equal(60f * 12700f, effective.SizeEmu);
    }

    /// <summary>
    ///     End-to-end proof (through the public <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>
    ///     API) that a ctrTitle placeholder's own empty-but-present <c>&lt;a:lstStyle/&gt;</c> paints
    ///     pixel-for-pixel identical glyph ink to the same placeholder whose <c>&lt;a:lstStyle&gt;</c>
    ///     is omitted entirely - proving the empty element no longer "wins" differently from an
    ///     absent one, and that the layout's real 60pt override is actually painted (not merely
    ///     resolved) in both cases.
    /// </summary>
    [Fact]
    public void Render_CtrTitlePlaceholderWithEmptySlideLstStyle_PaintsIdenticallyToAbsentLstStyle()
    {
        const string masterTxStylesXml =
            """
            <p:titleStyle><a:lvl1pPr><a:defRPr sz="2800"/></a:lvl1pPr></p:titleStyle>
            """;
        const string layoutPlaceholderXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="ctrTitle" hasCustomPrompt="1"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="685800" y="2130425"/><a:ext cx="7772400" cy="2259013"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
              <p:txBody>
                <a:bodyPr anchor="b"/>
                <a:lstStyle><a:lvl1pPr algn="l"><a:defRPr sz="6000"/></a:lvl1pPr></a:lstStyle>
                <a:p><a:r><a:t>Click to edit Master title style</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;

        static string BuildSlideXml(string lstStyleXml) =>
            $"""
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title 1"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="ctrTitle"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr/>
              <p:txBody>
                <a:bodyPr/>
                {lstStyleXml}
                <a:p><a:r><a:rPr lang="en-US"/><a:t>Fluidic Schematics</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;

        using var emptyLstStyleStream = BuildRenderPackage(BuildSlideXml("<a:lstStyle/>"), masterTxStylesXml, layoutPlaceholderXml);
        using var emptyLstStyleDocument = PptxDocument.Open(emptyLstStyleStream);
        using var emptyLstStyleSurface = emptyLstStyleDocument.Render(0, 300, 200);

        using var absentLstStyleStream = BuildRenderPackage(BuildSlideXml(string.Empty), masterTxStylesXml, layoutPlaceholderXml);
        using var absentLstStyleDocument = PptxDocument.Open(absentLstStyleStream);
        using var absentLstStyleSurface = absentLstStyleDocument.Render(0, 300, 200);

        var paintedAnyInk = false;
        for (var y = 0; y < 200; y++)
        {
            for (var x = 0; x < 300; x++)
            {
                var emptyPixel = emptyLstStyleSurface[x, y];
                if (emptyPixel != new Rgba32(255, 255, 255, 255))
                {
                    paintedAnyInk = true;
                }

                Assert.Equal(emptyPixel, absentLstStyleSurface[x, y]);
            }
        }

        Assert.True(paintedAnyInk, "Expected the ctrTitle placeholder's inherited 60pt text to paint glyph ink.");
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

    /// <summary>
    ///     Proves a "Section Header" layout-style title placeholder whose slide-level
    ///     <c>&lt;p:ph idx="0"/&gt;</c> omits <c>type</c> (relying entirely on the idx-matched
    ///     layout placeholder's own <c>type="title"</c>) resolves its run font size from the
    ///     master's <c>&lt;p:titleStyle&gt;</c> (90pt), not from <c>&lt;p:bodyStyle&gt;</c> (28pt) -
    ///     the exact reported real-world regression (CanvasNet previously rendered this
    ///     placeholder's title at roughly 1/3 PowerPoint's own ground-truth size because the
    ///     render pipeline previously passed the slide placeholder's own raw, schema-defaulted
    ///     <c>type</c> ("obj") directly into text-style-bucket selection instead of the
    ///     idx-matched layout placeholder's effective type).
    /// </summary>
    [Fact]
    public void ResolveEffectiveRunProperties_SectionHeaderTitlePlaceholderWithOmittedType_ResolvesMasterTitleStyleNotBodyStyle()
    {
        const string masterTxStylesXml =
            """
            <p:titleStyle><a:lvl1pPr><a:defRPr sz="9000"/></a:lvl1pPr></p:titleStyle>
            <p:bodyStyle><a:lvl1pPr><a:defRPr sz="2800"/></a:lvl1pPr></p:bodyStyle>
            """;
        const string layoutPlaceholderXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="title" idx="0"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="1600200"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:sp>
            """;
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph idx="0"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr/>
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:t>Section Header Title</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, masterTxStylesXml, layoutPlaceholderXml);
        using var document = PptxDocument.Open(stream);

        var slide = document.GetSlide(0);
        var layout = document.GetLayout(slide.LayoutPartPath);
        var master = document.GetMaster(layout.MasterPartPath);
        var theme = document.GetTheme(master.ThemePartPath);

        var slidePlaceholder = slide.Placeholders[0];
        Assert.Equal("obj", slidePlaceholder.Type); // Schema-defaulted, omitted on the slide itself.
        Assert.Null(slidePlaceholder.DeclaredType);

        var placeholderProperties = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, layout.Placeholders, master.Placeholders, theme, master.TxStyles);

        // The effective type must be inherited from the idx-matched layout placeholder ("title"),
        // not the slide's own schema-defaulted "obj".
        Assert.Equal("title", placeholderProperties.EffectivePlaceholderType);

        var txBody = slidePlaceholder.ShapeElement.Element("{http://schemas.openxmlformats.org/presentationml/2006/main}txBody")!;
        var textBody = PptxDocument.ParseTextBody(txBody);
        var paragraph = textBody.Paragraphs[0];
        var run = paragraph.Runs[0];

        // This mirrors exactly what RenderShape passes into ResolveEffectiveRunProperties.
        var effective = PptxDocument.ResolveEffectiveRunProperties(
            run, paragraph, placeholderProperties, theme,
            placeholderProperties.EffectivePlaceholderType ?? slidePlaceholder.Type);

        Assert.Equal(90f * 12700f, effective.SizeEmu);
    }

    /// <summary>
    ///     Proves an attribute-less <c>&lt;a:normAutofit/&gt;</c> on a subtitle placeholder whose
    ///     content genuinely fits its declared box does not over-shrink: the resolved
    ///     <see cref="PptxTextLayout.AppliedFontScale"/> stays at <c>1.0</c> rather than being
    ///     driven toward the shrink loop's floor. This directly tests (and refutes, for this
    ///     scenario) the "subtitle sliver" bug's second hypothesis - that the bounded shrink loop
    ///     itself over-shrinks given correct inputs - documented as confirmed-correct behavior to
    ///     guard against a future regression of this mechanism (see this test class's remarks and
    ///     the companion planning report for the full investigation).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_SubtitlePlaceholderWithAttributeLessNormAutofit_DoesNotOverShrinkContentThatFits()
    {
        const string masterTxStylesXml =
            """
            <p:bodyStyle><a:lvl1pPr><a:defRPr sz="3200"/></a:lvl1pPr></p:bodyStyle>
            """;
        const string layoutPlaceholderXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="3" name="Subtitle Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="subTitle" idx="1"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="2000000"/><a:ext cx="9144000" cy="1200000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:sp>
            """;
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="3" name="Subtitle"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="subTitle" idx="1"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr/>
              <p:txBody>
                <a:bodyPr><a:normAutofit/></a:bodyPr>
                <a:p><a:r><a:t>Subtitle</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, masterTxStylesXml, layoutPlaceholderXml);
        using var document = PptxDocument.Open(stream);

        var slide = document.GetSlide(0);
        var layout = document.GetLayout(slide.LayoutPartPath);
        var master = document.GetMaster(layout.MasterPartPath);
        var theme = document.GetTheme(master.ThemePartPath);

        var slidePlaceholder = slide.Placeholders[0];

        var placeholderProperties = PptxDocument.ResolvePlaceholderProperties(
            slidePlaceholder, layout.Placeholders, master.Placeholders, theme, master.TxStyles);

        var txBody = slidePlaceholder.ShapeElement.Element("{http://schemas.openxmlformats.org/presentationml/2006/main}txBody")!;
        var textBody = PptxDocument.ParseTextBody(txBody);

        Assert.NotNull(placeholderProperties.EffectiveXfrmElement);
        var frame = PptxDocument.ResolveShapeFrame(placeholderProperties.EffectiveXfrmElement);

        var layoutResult = PptxDocument.ResolveTextLayout(
            textBody, placeholderProperties, theme,
            placeholderProperties.EffectivePlaceholderType ?? slidePlaceholder.Type,
            frame.WidthEmu, frame.HeightEmu,
            (family, bold, italic) => DemaConsulting.CanvasNet.Fonts.SystemFontCatalog.LoadBundledFallback(bold, italic, bold, italic));

        Assert.Equal(1f, layoutResult.AppliedFontScale);
    }

    /// <summary>
    ///     End-to-end proof (through the public <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>
    ///     API, not just the internal resolver) that a "Section Header" title placeholder whose
    ///     slide-level <c>&lt;p:ph idx="0"/&gt;</c> omits <c>type</c> paints pixel-for-pixel
    ///     identical glyph ink to the same placeholder declaring its type explicitly
    ///     (<c>type="title"</c>) - both must resolve to the master's <c>&lt;p:titleStyle&gt;</c>
    ///     font size. Before the fix, the omitted-type variant painted visibly smaller (28pt
    ///     <c>&lt;p:bodyStyle&gt;</c>) glyph ink than the explicit-type variant (90pt
    ///     <c>&lt;p:titleStyle&gt;</c>), so the two renders differed.
    /// </summary>
    [Fact]
    public void Render_SectionHeaderTitleWithOmittedType_PaintsIdenticallyToExplicitTitleType()
    {
        const string masterTxStylesXml =
            """
            <p:titleStyle><a:lvl1pPr><a:defRPr sz="9000"/></a:lvl1pPr></p:titleStyle>
            <p:bodyStyle><a:lvl1pPr><a:defRPr sz="2800"/></a:lvl1pPr></p:bodyStyle>
            """;
        const string layoutPlaceholderXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="title" idx="0"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="1600200"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:sp>
            """;

        string BuildSlideXml(string phXml) =>
            $"""
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title"/>
                <p:cNvSpPr/>
                <p:nvPr>{phXml}</p:nvPr>
              </p:nvSpPr>
              <p:spPr/>
              <p:txBody>
                <a:bodyPr/>
                <a:p><a:r><a:t>Section Header Title</a:t></a:r></a:p>
              </p:txBody>
            </p:sp>
            """;

        using var omittedTypeStream = BuildRenderPackage(BuildSlideXml("""<p:ph idx="0"/>"""), masterTxStylesXml, layoutPlaceholderXml);
        using var omittedTypeDocument = PptxDocument.Open(omittedTypeStream);
        using var omittedTypeSurface = omittedTypeDocument.Render(0, 300, 160);

        using var explicitTypeStream = BuildRenderPackage(BuildSlideXml("""<p:ph type="title" idx="0"/>"""), masterTxStylesXml, layoutPlaceholderXml);
        using var explicitTypeDocument = PptxDocument.Open(explicitTypeStream);
        using var explicitTypeSurface = explicitTypeDocument.Render(0, 300, 160);

        var paintedAnyInk = false;
        for (var y = 0; y < 160; y++)
        {
            for (var x = 0; x < 300; x++)
            {
                var omittedPixel = omittedTypeSurface[x, y];
                if (omittedPixel != new Rgba32(255, 255, 255, 255))
                {
                    paintedAnyInk = true;
                }

                Assert.Equal(omittedPixel, explicitTypeSurface[x, y]);
            }
        }

        Assert.True(paintedAnyInk, "Expected the title placeholder's text to paint glyph ink.");
    }

    // --- Master/layout non-placeholder decorative shape rendering (Phase 2 Follow-Up) ---------

    /// <summary>
    ///     Builds a two-slide, two-layout, one-master package (presentation -&gt; 2x slide -&gt;
    ///     2x layout -&gt; 1x master -&gt; theme): slide index 0 uses layout 1 (whose own
    ///     <c>&lt;p:spTree&gt;</c> inner content is <paramref name="layout1ShapeTreeXml"/>), slide
    ///     index 1 uses layout 2 (<paramref name="layout2ShapeTreeXml"/>) - both layouts share the
    ///     same, otherwise-empty master. Used by
    ///     <see cref="Render_LayoutNonPlaceholderAutoshape_PaintsOnlyOnSlidesUsingThatLayout"/> to
    ///     prove a layout's own decorative shapes paint only on slides using that specific layout,
    ///     not on sibling slides using a different layout that shares the same master.
    /// </summary>
    private static Stream BuildTwoLayoutTwoSlideRenderPackage(string layout1ShapeTreeXml, string layout2ShapeTreeXml)
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
              <p:sldIdLst><p:sldId id="256" r:id="rId2"/><p:sldId id="257" r:id="rId3"/></p:sldIdLst>
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

        const string emptySpTreeSlideXml =
            """
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld>
                <p:spTree/>
              </p:cSld>
            </p:sld>
            """;

        const string slide1RelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
            </Relationships>
            """;

        const string slide2RelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout2.xml" />
            </Relationships>
            """;

        var layout1Xml =
            $"""
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld>
                <p:spTree>{layout1ShapeTreeXml}</p:spTree>
              </p:cSld>
            </p:sldLayout>
            """;

        var layout2Xml =
            $"""
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld>
                <p:spTree>{layout2ShapeTreeXml}</p:spTree>
              </p:cSld>
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
              <p:cSld>
                <p:spTree/>
              </p:cSld>
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
            WriteTextEntry(archive, "ppt/slides/slide1.xml", emptySpTreeSlideXml);
            WriteTextEntry(archive, "ppt/slides/_rels/slide1.xml.rels", slide1RelsXml);
            WriteTextEntry(archive, "ppt/slides/slide2.xml", emptySpTreeSlideXml);
            WriteTextEntry(archive, "ppt/slides/_rels/slide2.xml.rels", slide2RelsXml);
            WriteTextEntry(archive, "ppt/slideLayouts/slideLayout1.xml", layout1Xml);
            WriteTextEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", layoutRelsXml);
            WriteTextEntry(archive, "ppt/slideLayouts/slideLayout2.xml", layout2Xml);
            WriteTextEntry(archive, "ppt/slideLayouts/_rels/slideLayout2.xml.rels", layoutRelsXml);
            WriteTextEntry(archive, "ppt/slideMasters/slideMaster1.xml", masterXml);
            WriteTextEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", masterRelsXml);
            WriteTextEntry(archive, "ppt/theme/theme1.xml", themeXml);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>A full-slide-sized freeform shape with a solid fill of the given color, as a layout/master decorative shape.</summary>
    private static string FullSlideShapeXml(string name, string hexColor) =>
        $"""
        <p:sp>
          <p:nvSpPr>
            <p:cNvPr id="2" name="{name}"/>
            <p:cNvSpPr/>
            <p:nvPr/>
          </p:nvSpPr>
          <p:spPr>
            <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
            <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
            <a:solidFill><a:srgbClr val="{hexColor}"/></a:solidFill>
          </p:spPr>
        </p:sp>
        """;

    /// <summary>A full-slide-sized freeform shape with a solid fill resolving an <c>&lt;a:schemeClr val="..."/&gt;</c> scheme-color slot name.</summary>
    private static string FullSlideShapeSchemeClrXml(string name, string schemeVal) =>
        $"""
        <p:sp>
          <p:nvSpPr>
            <p:cNvPr id="2" name="{name}"/>
            <p:cNvSpPr/>
            <p:nvPr/>
          </p:nvSpPr>
          <p:spPr>
            <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
            <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
            <a:solidFill><a:schemeClr val="{schemeVal}"/></a:solidFill>
          </p:spPr>
        </p:sp>
        """;

    /// <summary>
    ///     Proves a slide's own <c>&lt;p:clrMapOvr&gt;/&lt;a:overrideClrMapping&gt;</c> is
    ///     consulted when resolving a shape's <c>&lt;a:schemeClr val="bg1"/&gt;</c> fill: without
    ///     the fix, <c>bg1</c> is hardcoded to the theme's <c>Light1</c> slot regardless of any
    ///     slide-level override; with the fix, an inverted color map (<c>bg1 -&gt; dk1</c>) makes
    ///     the same shape resolve to the theme's <c>Dark1</c> slot instead.
    /// </summary>
    [Fact]
    public void Render_SlideClrMapOvrInvertsBg1_SchemeClrFillResolvesToOverriddenSlotNotHardcodedAlias()
    {
        var shapeXml = FullSlideShapeSchemeClrXml("Bg1Shape", "bg1");
        const string invertedClrMapOvr = """<a:overrideClrMapping bg1="dk1" tx1="lt1" bg2="dk2" tx2="lt2"/>""";
        using var stream = BuildRenderPackage(shapeXml, slideClrMapOvrXml: invertedClrMapOvr);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        // The test theme's Dark1 slot is 101010 (see BuildRenderPackage's own themeXml) - without
        // the clrMap/clrMapOvr fix, this would instead sample Light1 (F0F0F0).
        Assert.Equal(new Rgba32(0x10, 0x10, 0x10, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Proves that, absent any <c>&lt;p:clrMapOvr&gt;</c>, a shape's <c>&lt;a:schemeClr
    ///     val="bg1"/&gt;</c> fill still resolves through the master's own (identity) <c>&lt;p:clrMap&gt;</c>
    ///     to <c>Light1</c>, preserving this unit's pre-existing baseline behavior.
    /// </summary>
    [Fact]
    public void Render_NoClrMapOvr_SchemeClrFillResolvesToMasterColorMapDefault()
    {
        var shapeXml = FullSlideShapeSchemeClrXml("Bg1Shape", "bg1");
        using var stream = BuildRenderPackage(shapeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(0xF0, 0xF0, 0xF0, 255), surface[10, 10]);
    }

    /// <summary>A full-slide-sized freeform shape with a <c>&lt;p:style&gt;</c> declaring the given <c>&lt;a:fillRef&gt;</c>/<c>&lt;a:lnRef&gt;</c> inner XML, and optionally an explicit <c>&lt;a:solidFill&gt;</c>/<c>&lt;a:ln&gt;</c> of its own.</summary>
    private static string FullSlideShapeStyleRefXml(
        string name, string fillRefXml = "", string lnRefXml = "", string? explicitFillHexColor = null, string explicitLnXml = "",
        string xfrmXml = """<a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>""") =>
        $"""
        <p:sp>
          <p:nvSpPr>
            <p:cNvPr id="2" name="{name}"/>
            <p:cNvSpPr/>
            <p:nvPr/>
          </p:nvSpPr>
          <p:spPr>
            {xfrmXml}
            <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
            {(explicitFillHexColor is not null ? $"""<a:solidFill><a:srgbClr val="{explicitFillHexColor}"/></a:solidFill>""" : string.Empty)}
            {explicitLnXml}
          </p:spPr>
          <p:style>
            {fillRefXml}
            {lnRefXml}
          </p:style>
        </p:sp>
        """;

    /// <summary>
    ///     Proves a shape declaring only a <c>&lt;p:style&gt;/&lt;a:fillRef idx="1"/&gt;</c> shape-style
    ///     reference (no explicit <c>&lt;p:spPr&gt;</c> fill at all) resolves its fill entirely from
    ///     the theme's <c>&lt;a:fmtScheme&gt;/&lt;a:fillStyleLst&gt;</c>, substituting the
    ///     <c>&lt;a:fillRef&gt;</c>'s own color child for the matched entry's <c>phClr</c> token -
    ///     the direct regression test for the customer-reported "Shape Styles gallery" symptom.
    /// </summary>
    [Fact]
    public void Render_ShapeWithOnlyStyleFillRefNoExplicitSpPrFill_RendersResolvedTintedAccentColor()
    {
        const string fillRefXml = """<a:fillRef idx="1"><a:srgbClr val="FF00FF"/></a:fillRef>""";
        const string fillStyleListXml = """<a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:srgbClr val="111111"/></a:solidFill><a:solidFill><a:srgbClr val="222222"/></a:solidFill>""";
        var shapeXml = FullSlideShapeStyleRefXml("StyleFillShape", fillRefXml: fillRefXml);
        using var stream = BuildRenderPackage(shapeXml, themeFillStyleListXml: fillStyleListXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(0xFF, 0x00, 0xFF, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Proves an explicit <c>&lt;a:solidFill&gt;</c> on a shape's own <c>&lt;p:spPr&gt;</c>
    ///     always wins over its own <c>&lt;p:style&gt;/&lt;a:fillRef&gt;</c>, even when both are
    ///     present on the same shape.
    /// </summary>
    [Fact]
    public void Render_ShapeWithBothExplicitSpPrFillAndStyleFillRef_ExplicitFillWins()
    {
        const string fillRefXml = """<a:fillRef idx="1"><a:srgbClr val="FF00FF"/></a:fillRef>""";
        const string fillStyleListXml = """<a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:srgbClr val="111111"/></a:solidFill><a:solidFill><a:srgbClr val="222222"/></a:solidFill>""";
        var shapeXml = FullSlideShapeStyleRefXml("StyleFillShape", fillRefXml: fillRefXml, explicitFillHexColor: "00FF00");
        using var stream = BuildRenderPackage(shapeXml, themeFillStyleListXml: fillStyleListXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(0x00, 0xFF, 0x00, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Proves a shape declaring only a <c>&lt;p:style&gt;/&lt;a:lnRef idx="1"/&gt;</c> shape-style
    ///     reference (no explicit <c>&lt;a:ln&gt;</c> at all) resolves its stroke entirely from the
    ///     theme's <c>&lt;a:fmtScheme&gt;/&lt;a:lnStyleLst&gt;</c>, substituting the
    ///     <c>&lt;a:lnRef&gt;</c>'s own color child for the matched entry's <c>phClr</c> token.
    /// </summary>
    [Fact]
    public void Render_ShapeWithOnlyStyleLnRefNoExplicitLn_RendersResolvedStrokeColor()
    {
        const string lnRefXml = """<a:lnRef idx="1"><a:srgbClr val="FF00FF"/></a:lnRef>""";
        const string lnStyleListXml =
            """
            <a:ln w="900000"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln>
            <a:ln w="100000"><a:solidFill><a:srgbClr val="111111"/></a:solidFill></a:ln>
            <a:ln w="100000"><a:solidFill><a:srgbClr val="222222"/></a:solidFill></a:ln>
            """;
        const string fillRefXml = """<a:fillRef idx="0"/>""";
        const string xfrmXml = """<a:xfrm><a:off x="914400" y="685800"/><a:ext cx="7315200" cy="5486400"/></a:xfrm>""";
        var shapeXml = FullSlideShapeStyleRefXml("StyleLnShape", fillRefXml: fillRefXml, lnRefXml: lnRefXml, xfrmXml: xfrmXml);
        using var stream = BuildRenderPackage(shapeXml, themeLnStyleListXml: lnStyleListXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 200, 200);

        // The lnRef-resolved stroke (900000 EMU wide, centered on the shape's inset top edge at
        // y=685800 EMU, i.e. pixel row 20 of 200) is thick enough to fully cover several pixel
        // rows either side of its centerline - sampled well away from any corner so only this one
        // edge's stroke band (not an adjacent edge's) can contribute to the sampled pixel.
        Assert.Equal(new Rgba32(0xFF, 0x00, 0xFF, 255), surface[100, 20]);
    }

    /// <summary>
    ///     Proves a present <c>&lt;a:ln&gt;</c> (even with no recognized fill child of its own)
    ///     always wins over a shape's own <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c> - only a fully
    ///     absent <c>&lt;a:ln&gt;</c> falls back to it.
    /// </summary>
    [Fact]
    public void Render_ShapeWithExplicitLnNoFillAndStyleLnRef_ExplicitLnWins()
    {
        const string lnRefXml = """<a:lnRef idx="1"><a:srgbClr val="FF00FF"/></a:lnRef>""";
        const string lnStyleListXml =
            """
            <a:ln w="900000"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln>
            <a:ln w="100000"><a:solidFill><a:srgbClr val="111111"/></a:solidFill></a:ln>
            <a:ln w="100000"><a:solidFill><a:srgbClr val="222222"/></a:solidFill></a:ln>
            """;
        const string fillRefXml = """<a:fillRef idx="0"/>""";
        const string explicitLnXml = """<a:ln w="900000"><a:noFill/></a:ln>""";
        const string xfrmXml = """<a:xfrm><a:off x="914400" y="685800"/><a:ext cx="7315200" cy="5486400"/></a:xfrm>""";
        var shapeXml = FullSlideShapeStyleRefXml("StyleLnShape", fillRefXml: fillRefXml, lnRefXml: lnRefXml, explicitLnXml: explicitLnXml, xfrmXml: xfrmXml);
        using var stream = BuildRenderPackage(shapeXml, themeLnStyleListXml: lnStyleListXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 200, 200);

        // The explicit <a:ln><a:noFill/></a:ln> wins - no stroke paints, leaving the default
        // opaque-white clear at the same pixel the lnRef's own stroke would otherwise have
        // fully covered (see the sibling fact above).
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[100, 20]);
    }

    /// <summary>
    ///     Proves the fix for the reported bug: a present <c>&lt;a:ln&gt;</c> that declares a
    ///     width but no recognized fill-definition child of its own (<c>&lt;a:ln w="76200"/&gt;</c>,
    ///     the exact reported construct) keeps its own, narrower width but defers its color to the
    ///     shape's own <c>&lt;p:style&gt;/&lt;a:lnRef&gt;</c> - it no longer silently resolves to
    ///     "no stroke" (the pre-fix behavior), and it does not merge the style's own, much wider
    ///     line width either.
    /// </summary>
    [Fact]
    public void Render_ShapeWithLnWidthOnlyNoFillChildAndStyleLnRef_UsesOwnWidthAndStyleColor()
    {
        const string lnRefXml = """<a:lnRef idx="1"><a:srgbClr val="FF00FF"/></a:lnRef>""";
        const string lnStyleListXml =
            """
            <a:ln w="900000"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln>
            <a:ln w="100000"><a:solidFill><a:srgbClr val="111111"/></a:solidFill></a:ln>
            <a:ln w="100000"><a:solidFill><a:srgbClr val="222222"/></a:solidFill></a:ln>
            """;
        const string fillRefXml = """<a:fillRef idx="0"/>""";
        const string explicitLnXml = """<a:ln w="76200"/>""";
        const string xfrmXml = """<a:xfrm><a:off x="914400" y="685800"/><a:ext cx="7315200" cy="5486400"/></a:xfrm>""";
        var shapeXml = FullSlideShapeStyleRefXml("StyleLnShape", fillRefXml: fillRefXml, lnRefXml: lnRefXml, explicitLnXml: explicitLnXml, xfrmXml: xfrmXml);
        using var stream = BuildRenderPackage(shapeXml, themeLnStyleListXml: lnStyleListXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 200, 200);

        // Directly on the shape's own top edge (y=685800 EMU, pixel row 20 of 200), the stroke
        // paints the style's own magenta color - proving the color fallback fires for a
        // fill-less-but-present <a:ln>.
        Assert.Equal(new Rgba32(0xFF, 0x00, 0xFF, 255), surface[100, 20]);

        // At pixel row 25 - outside the shape's own narrow 76200 EMU-wide stroke band, but well
        // inside where the style's own much wider 900000 EMU stroke would have reached - the
        // pixel is left as the default opaque-white clear, proving width is the shape's own
        // (narrow), never merged in from the style (wide).
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[100, 25]);
    }

    /// <summary>
    ///     Proves the fix for the reported real-world bug: an ellipse shape with
    ///     <c>&lt;a:noFill/&gt;</c> and an <c>&lt;a:ln&gt;</c> that declares only a color (no
    ///     <c>w</c> attribute at all - the exact reported construct) now paints a visible stroke
    ///     at PowerPoint's own default stroke width, instead of rendering completely invisibly
    ///     (the pre-fix behavior, since an absent <c>w</c> used to collapse to the same "no
    ///     stroke" outcome as an explicit <c>w="0"</c>).
    /// </summary>
    /// <remarks>
    ///     Rendered against a deliberately small 1"x1" slide (914400x914400 EMU) at 200x200px
    ///     (scale: <c>914400 / 200 = 4572</c> EMU/pixel) so the new 9525 EMU default stroke width
    ///     (<c>~2.08</c>px at this scale) survives anti-aliased rendering as solid, easily-sampled
    ///     pixels - at the full 9144000 EMU slide width used by every other render test in this
    ///     class, 9525 EMU would be sub-pixel (<c>~0.21</c>px) and not reliably sampled.
    /// </remarks>
    [Fact]
    public void Render_EllipseWithNoFillAndLnMissingWidthButRecognizedColor_PaintsDefaultWidthStroke()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Oval 10"/>
                <p:cNvSpPr/>
                <p:nvPr/>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="152400" y="152400"/><a:ext cx="609600" cy="609600"/></a:xfrm>
                <a:prstGeom prst="ellipse"><a:avLst/></a:prstGeom>
                <a:noFill/>
                <a:ln><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:ln>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml, slideWidthEmu: 914400f, slideHeightEmu: 914400f);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 200, 200);

        // The ellipse spans x/y in [152400, 762000] EMU (center 457200,457200; radius 304800),
        // i.e. pixel columns/rows [33.33, 166.67] of 200 at this slide's 4572 EMU/pixel scale.
        // Sampled directly on the ellipse's own top-edge stroke band (pixel column 100 = the
        // ellipse's horizontal center; pixel row 33 = the ellipse's own top boundary) - a solid
        // red stroke pixel proves the default 9525 EMU width now paints visibly.
        Assert.Equal(new Rgba32(0xFF, 0x00, 0x00, 255), surface[100, 33]);

        // Sampled well inside the ellipse's interior (its own center) - still the default opaque-
        // white clear, proving <a:noFill/> still means no interior fill; this width-only fix does
        // not affect fill resolution at all.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[100, 100]);

        // Sampled well outside the ellipse's own bounding box entirely (a corner of the slide) -
        // also the default opaque-white clear, proving the stroke did not spill far beyond its own
        // narrow (~2px) band.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Proves the fix for the reported high-priority real-world regression at the ACTUAL scale
    ///     a real-world <c>.pptx</c> file uses: a full-size standard slide
    ///     (<c>9144000x6858000</c> EMU, rendered at <c>960x720</c> - exactly <c>9525</c> EMU/pixel,
    ///     i.e. PowerPoint's own default <c>&lt;a:ln&gt;</c> width), an ellipse (<c>Oval 10</c> in
    ///     the originally reported file) with <c>&lt;a:noFill/&gt;</c> and an <c>&lt;a:ln&gt;</c>
    ///     that declares only a color (no <c>w</c> attribute - PowerPoint's own default width)
    ///     must render as a thin ring - its own center must remain the slide's background color,
    ///     NOT the stroke color - rather than a solid-filled disc.
    /// </summary>
    /// <remarks>
    ///     The existing small-slide/200x200px
    ///     <see cref="Render_EllipseWithNoFillAndLnMissingWidthButRecognizedColor_PaintsDefaultWidthStroke"/>
    ///     test alone does NOT catch this regression: at that small scale, the ellipse's own
    ///     native-EMU-space Bezier curve flattens to far fewer tessellation points (its own radius
    ///     is far smaller in EMU), so the false inner-ring collapse this test guards against never
    ///     triggers there. Only at a realistic, full-size slide's own EMU magnitude (hundreds of
    ///     thousands to low millions of EMU) does <c>StrokeOutliner.BuildClosedSide</c>'s inner-ring
    ///     collapse detector false-positive (before the fix) on the resulting high tessellation
    ///     density, discarding the inner ring entirely and rendering the whole ellipse interior as
    ///     solid stroke - the exact defect reported against the real-world file.
    /// </remarks>
    [Fact]
    public void Render_FullSlideScaleEllipseWithNoFillAndLnMissingWidth_RendersThinRingNotSolidDisc()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Oval 10"/>
                <p:cNvSpPr/>
                <p:nvPr/>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="2667000" y="1524000"/><a:ext cx="3810000" cy="3810000"/></a:xfrm>
                <a:prstGeom prst="ellipse"><a:avLst/></a:prstGeom>
                <a:noFill/>
                <a:ln><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:ln>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 960, 720);

        // The ellipse spans x/y in [2667000, 6477000] EMU (center 4572000,3429000; radius
        // 1905000), i.e. pixel columns/rows [280, 680] of 960x720 at this slide's exact 9525
        // EMU/pixel scale - pixel (480, 360) is therefore the ellipse's own exact center.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[480, 360]);

        // The ellipse's own top boundary (pixel row 160) must show the stroke's red color, not
        // the background - proving the ring itself still paints (this is not simply "nothing
        // rendered at all").
        var boundaryPixel = surface[480, 160];
        Assert.True(
            boundaryPixel.R > 128 && boundaryPixel.G < 128 && boundaryPixel.B < 128,
            $"Expected a red-dominant boundary stroke pixel, but got {boundaryPixel}.");

        // A corner of the slide, well outside the ellipse's own bounding box, remains the default
        // opaque-white clear.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Proves the same fix applies generally, not only to ellipses: a full-size slide's own
    ///     <c>roundRect</c> shape with <c>&lt;a:noFill/&gt;</c> and a color-only (default-width)
    ///     <c>&lt;a:ln&gt;</c> must also render as a thin outline, not a solid-filled shape - the
    ///     investigation behind this fix confirmed <c>roundRect</c> (enough curved, Bezier-
    ///     flattened tessellation vertices to trigger the same false inner-ring collapse) is
    ///     equally affected, while a plain <c>rect</c> (too few vertices - no curves at all) is
    ///     not.
    /// </summary>
    [Fact]
    public void Render_FullSlideScaleRoundRectWithNoFillAndLnMissingWidth_RendersThinOutlineNotSolidFill()
    {
        const string spTreeInnerXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Rounded Rectangle 10"/>
                <p:cNvSpPr/>
                <p:nvPr/>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="2667000" y="1524000"/><a:ext cx="3810000" cy="3810000"/></a:xfrm>
                <a:prstGeom prst="roundRect"><a:avLst/></a:prstGeom>
                <a:noFill/>
                <a:ln><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:ln>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 960, 720);

        // The shape spans x/y in [2667000, 6477000] EMU (280..680px) at this slide's exact 9525
        // EMU/pixel scale; pixel (480, 360) is comfortably inside its interior, away from the
        // rounded corners' own geometry, and must remain the background color.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[480, 360]);

        // The shape's own top edge (pixel row 160, away from either rounded corner) must show the
        // stroke's red color.
        var boundaryPixel = surface[480, 160];
        Assert.True(
            boundaryPixel.R > 128 && boundaryPixel.G < 128 && boundaryPixel.B < 128,
            $"Expected a red-dominant boundary stroke pixel, but got {boundaryPixel}.");

        // A corner of the slide, well outside the shape's own bounding box, remains the default
        // opaque-white clear.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Proves a master's own non-placeholder <c>&lt;p:pic&gt;</c> paints on a slide using that
    ///     master even when the slide's own shape tree is empty, and that the picture's embedded-
    ///     image relationship resolves against the <strong>master's own</strong> <c>.rels</c> file
    ///     rather than the slide's - a slide-owned relationship is deliberately given the exact
    ///     same <c>rId2</c> identifier but targets a different (wrong) image, proving the
    ///     <c>ownerPartPath</c> fix (see the companion planning report) is load-bearing and not
    ///     accidentally correct.
    /// </summary>
    [Fact]
    public void Render_MasterNonPlaceholderPicture_PaintsOnEverySlideUsingThatMasterResolvingOwnRels()
    {
        var masterPngBytes = BuildPngBytes(new Rgba32(10, 20, 30, 255));
        var wrongSlidePngBytes = BuildPngBytes(new Rgba32(200, 0, 200, 255));
        const string masterShapeTreeXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="3" name="MasterPic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:pic>
            """;
        using var stream = BuildRenderPackage(
            spTreeInnerXml: string.Empty,
            masterShapeTreeXml: masterShapeTreeXml,
            masterMedia: ("png", "image/png", masterPngBytes),
            media: ("png", "image/png", wrongSlidePngBytes));
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        // If RenderPicture incorrectly resolved the master's own <a:blip r:embed="rId2"> against
        // the slide's own .rels (which also declares an rId2, deliberately targeting a different
        // image), this would instead sample the wrong (200, 0, 200) picture.
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Proves a layout's own decorative, non-placeholder autoshape paints only on slides using
    ///     that specific layout, not on a sibling slide using a different layout that shares the
    ///     same master.
    /// </summary>
    [Fact]
    public void Render_LayoutNonPlaceholderAutoshape_PaintsOnlyOnSlidesUsingThatLayout()
    {
        var layout1ShapeTreeXml = FullSlideShapeXml("LayoutAShape", "00FF00");
        using var stream = BuildTwoLayoutTwoSlideRenderPackage(layout1ShapeTreeXml, layout2ShapeTreeXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        using var surfaceUsingLayout1 = document.Render(0, 20, 20);
        using var surfaceUsingLayout2 = document.Render(1, 20, 20);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surfaceUsingLayout1[10, 10]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surfaceUsingLayout2[10, 10]);
    }

    /// <summary>Proves a slide-level shape paints on top of an overlapping master decorative shape (z-order: slide content on top).</summary>
    [Fact]
    public void Render_MasterAndSlideShapesOverlap_SlideShapePaintsOnTopOfMasterShape()
    {
        var masterShapeTreeXml = FullSlideShapeXml("MasterShape", "FF0000");
        var slideShapeXml = FullSlideShapeXml("SlideShape", "0000FF");
        using var stream = BuildRenderPackage(slideShapeXml, masterShapeTreeXml: masterShapeTreeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[10, 10]);
    }

    /// <summary>Proves a layout decorative shape paints on top of an overlapping master decorative shape (z-order: layout above master).</summary>
    [Fact]
    public void Render_MasterAndLayoutShapesOverlap_LayoutShapePaintsOnTopOfMasterShape()
    {
        var masterShapeTreeXml = FullSlideShapeXml("MasterShape", "FF0000");
        var layoutShapeXml = FullSlideShapeXml("LayoutShape", "00FF00");
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty, layoutPlaceholderXml: layoutShapeXml, masterShapeTreeXml: masterShapeTreeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Proves a master's own placeholder shape never paints its own prompt content, while a
    ///     sibling non-placeholder decorative shape on the same master still renders normally.
    /// </summary>
    [Fact]
    public void Render_MasterPlaceholderShape_DoesNotRenderItsOwnPromptContent()
    {
        const string masterShapeTreeXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="title"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
              </p:spPr>
            </p:sp>
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="3" name="Decorative"/>
                <p:cNvSpPr/>
                <p:nvPr/>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="1000000" cy="1000000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="0000FF"/></a:solidFill>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty, masterShapeTreeXml: masterShapeTreeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // The master's own title placeholder declares a full-slide red fill - it must never
        // paint, so the bottom-right corner (outside the small decorative shape's own footprint)
        // remains the cleared default white background.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[90, 90]);

        // The master's own non-placeholder decorative shape (small blue rect, top-left corner)
        // still paints normally.
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[1, 1]);
    }

    /// <summary>
    ///     Proves a layout's own placeholder shape never paints its own prompt content, while a
    ///     sibling non-placeholder decorative shape on the same layout still renders normally.
    /// </summary>
    [Fact]
    public void Render_LayoutPlaceholderShape_DoesNotRenderItsOwnPromptContent()
    {
        const string layoutShapeTreeXml =
            """
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="2" name="Title Placeholder"/>
                <p:cNvSpPr/>
                <p:nvPr><p:ph type="title"/></p:nvPr>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
              </p:spPr>
            </p:sp>
            <p:sp>
              <p:nvSpPr>
                <p:cNvPr id="3" name="Decorative"/>
                <p:cNvSpPr/>
                <p:nvPr/>
              </p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="1000000" cy="1000000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="0000FF"/></a:solidFill>
              </p:spPr>
            </p:sp>
            """;
        using var stream = BuildRenderPackage(spTreeInnerXml: string.Empty, layoutPlaceholderXml: layoutShapeTreeXml);
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 100, 100);

        // The layout's own title placeholder declares a full-slide red fill - it must never
        // paint, so the bottom-right corner (outside the small decorative shape's own footprint)
        // remains the cleared default white background.
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[90, 90]);

        // The layout's own non-placeholder decorative shape (small blue rect, top-left corner)
        // still paints normally.
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[1, 1]);
    }

    /// <summary>
    ///     Regression test (see the companion planning report's bug-fix rationale): a master's own
    ///     non-placeholder <c>&lt;p:pic&gt;</c> referencing an unsupported raster format (an EMF
    ///     vector picture) must be skipped silently - its own <see cref="PptxUnsupportedFeatureException"/>
    ///     must not abort the rest of the render - so the slide's own content still paints
    ///     normally.
    /// </summary>
    [Fact]
    public void Render_MasterNonPlaceholderPictureWithUnsupportedFormat_SkipsThatShapeAndStillRendersSlideContent()
    {
        const string masterShapeTreeXml =
            """
            <p:pic>
              <p:nvPicPr><p:cNvPr id="3" name="MasterPic"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr>
              <p:blipFill><a:blip r:embed="rId2"/></p:blipFill>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="9144000" cy="6858000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </p:spPr>
            </p:pic>
            """;
        var slideShapeXml = FullSlideShapeXml("SlideShape", "0000FF");
        using var stream = BuildRenderPackage(
            slideShapeXml,
            masterShapeTreeXml: masterShapeTreeXml,
            masterMedia: ("emf", "image/x-emf", [1, 2, 3, 4]));
        using var document = PptxDocument.Open(stream);

        using var surface = document.Render(0, 20, 20);

        // The master's own broken EMF picture shape was skipped - the slide's own shape still
        // paints over the full slide footprint.
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Regression test (see the companion planning report's bug-fix rationale): a
    ///     <strong>slide's own</strong> (not a master/layout's) <c>&lt;p:pic&gt;</c> referencing an
    ///     unsupported raster format must still hard-fail <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>
    ///     with <see cref="PptxUnsupportedFeatureException"/>, exactly as before the master/layout
    ///     graceful-skip containment was added - proving the containment is scoped to master/
    ///     layout-owned shapes only.
    /// </summary>
    [Fact]
    public void Render_SlideOwnPictureWithUnsupportedFormat_StillThrowsPptxUnsupportedFeatureException()
    {
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
        using var stream = BuildRenderPackage(spTreeInnerXml, media: ("emf", "image/x-emf", [1, 2, 3, 4]));
        using var document = PptxDocument.Open(stream);

        Assert.Throws<PptxUnsupportedFeatureException>(() => document.Render(0, 20, 20));
    }
}
