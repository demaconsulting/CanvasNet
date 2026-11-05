// cspell:ignore pptx emf
using System.IO.Compression;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

/// <summary>
///     Tests of <see cref="PptxImageSource"/>: the first slide of a PowerPoint file as an embedded picture - drawn at the
///     size of its placement (fitted without distortion) over a transparent background, with skipped pictures as
///     diagnostics.
/// </summary>
public class PptxImageSourceTests
{
    /// <summary>The namespace declarations of the parts.</summary>
    private const string Namespaces =
        "xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" "
        + "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" "
        + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";

    /// <summary>The namespace of package relationships.</summary>
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>The prefix of the relationship types.</summary>
    private const string RelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    ///     Creates the bytes of a one-slide presentation of 480 by 360 pixels at 96 dpi (5 by 3.75 inches) whose slide has a
    ///     red rectangle over its left half and, optionally, a picture of a metafile that the renderer cannot draw.
    /// </summary>
    /// <param name="withMetafile"><see langword="true"/> to add the picture the renderer skips.</param>
    /// <returns>The bytes of the package.</returns>
    private static byte[] CreatePptx(bool withMetafile = false)
    {
        var shapes =
            """
            <p:sp>
              <p:nvSpPr><p:cNvPr id="2" name="Left half"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="0" y="0"/><a:ext cx="2286000" cy="3429000"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                <a:solidFill><a:srgbClr val="FF0000"/></a:solidFill>
              </p:spPr>
            </p:sp>
            """;
        if (withMetafile)
        {
            shapes +=
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

        var picture = withMetafile
            ? $"<Relationship Id=\"rId2\" Type=\"{RelationshipType}/image\" Target=\"../media/image1.emf\" />"
            : string.Empty;
        var entries = new List<(string Name, byte[] Content)>
        {
            Utf8(
                "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\" />"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\" />"
                + (withMetafile ? "<Default Extension=\"emf\" ContentType=\"image/x-emf\" />" : string.Empty)
                + "</Types>"),
            Utf8(
                "_rels/.rels",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{RelationshipsNamespace}\">"
                + $"<Relationship Id=\"rId1\" Type=\"{RelationshipType}/officeDocument\" Target=\"ppt/presentation.xml\" /></Relationships>"),
            Utf8(
                "ppt/presentation.xml",
                $"<p:presentation {Namespaces}><p:sldSz cx=\"4572000\" cy=\"3429000\"/>"
                + "<p:sldIdLst><p:sldId id=\"256\" r:id=\"rId2\"/></p:sldIdLst></p:presentation>"),
            Utf8(
                "ppt/_rels/presentation.xml.rels",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{RelationshipsNamespace}\">"
                + $"<Relationship Id=\"rId2\" Type=\"{RelationshipType}/slide\" Target=\"slides/slide1.xml\" /></Relationships>"),
            Utf8("ppt/slides/slide1.xml", $"<p:sld {Namespaces}><p:cSld><p:spTree>{shapes}</p:spTree></p:cSld></p:sld>"),
            Utf8(
                "ppt/slides/_rels/slide1.xml.rels",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{RelationshipsNamespace}\">"
                + $"<Relationship Id=\"rId1\" Type=\"{RelationshipType}/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\" />{picture}</Relationships>"),
            Utf8("ppt/slideLayouts/slideLayout1.xml", $"<p:sldLayout {Namespaces}><p:cSld><p:spTree></p:spTree></p:cSld></p:sldLayout>"),
            Utf8(
                "ppt/slideLayouts/_rels/slideLayout1.xml.rels",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{RelationshipsNamespace}\">"
                + $"<Relationship Id=\"rId1\" Type=\"{RelationshipType}/slideMaster\" Target=\"../slideMasters/slideMaster1.xml\" /></Relationships>"),
            Utf8(
                "ppt/slideMasters/slideMaster1.xml",
                $"<p:sldMaster {Namespaces}><p:cSld><p:spTree></p:spTree></p:cSld>"
                + "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\"/><p:txStyles></p:txStyles></p:sldMaster>"),
            Utf8(
                "ppt/slideMasters/_rels/slideMaster1.xml.rels",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"{RelationshipsNamespace}\">"
                + $"<Relationship Id=\"rId1\" Type=\"{RelationshipType}/theme\" Target=\"../theme/theme1.xml\" /></Relationships>"),
            Utf8(
                "ppt/theme/theme1.xml",
                "<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" name=\"Test\"><a:themeElements>"
                + "<a:clrScheme name=\"Test\"><a:dk1><a:srgbClr val=\"101010\"/></a:dk1><a:lt1><a:srgbClr val=\"F0F0F0\"/></a:lt1>"
                + "<a:dk2><a:srgbClr val=\"202020\"/></a:dk2><a:lt2><a:srgbClr val=\"E0E0E0\"/></a:lt2>"
                + "<a:accent1><a:srgbClr val=\"4472C4\"/></a:accent1><a:accent2><a:srgbClr val=\"ED7D31\"/></a:accent2>"
                + "<a:accent3><a:srgbClr val=\"A5A5A5\"/></a:accent3><a:accent4><a:srgbClr val=\"FFC000\"/></a:accent4>"
                + "<a:accent5><a:srgbClr val=\"5B9BD5\"/></a:accent5><a:accent6><a:srgbClr val=\"70AD47\"/></a:accent6>"
                + "<a:hlink><a:srgbClr val=\"0563C1\"/></a:hlink><a:folHlink><a:srgbClr val=\"954F72\"/></a:folHlink></a:clrScheme>"
                + "<a:fontScheme name=\"Test\"><a:majorFont><a:latin typeface=\"Calibri Light\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont>"
                + "<a:minorFont><a:latin typeface=\"Calibri\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>"
                + "</a:themeElements></a:theme>")
        };
        if (withMetafile)
        {
            // Not a real metafile: the renderer decides by the extension that it cannot draw it
            entries.Add(("ppt/media/image1.emf", [1, 2, 3, 4]));
        }

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entryStream = archive.CreateEntry(name).Open();
                entryStream.Write(content, 0, content.Length);
            }
        }

        return stream.ToArray();
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

    /// <summary>
    ///     Creates a request for a presentation.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="width">The requested width, or <see langword="null"/>.</param>
    /// <param name="height">The requested height, or <see langword="null"/>.</param>
    /// <param name="strict">Whether an unsupported feature must throw.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(byte[] bytes, int? width = null, int? height = null, bool strict = false, long remaining = long.MaxValue)
    {
        return new ImageSourceRequest
        {
            Href = "slides.pptx",
            FileName = "slides.pptx",
            Content = bytes,
            PixelWidth = width,
            PixelHeight = height,
            ThrowOnUnsupportedFeature = strict,
            Budget = new ImageBudget(remaining)
        };
    }

    /// <summary>
    ///     Test that the first slide is drawn at its natural size at 96 dots per inch over a transparent background.
    /// </summary>
    [Fact]
    public void PptxImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnTransparentBackground()
    {
        // Arrange
        var source = new PptxImageSource();

        // Act
        var result = source.Render(Request(CreatePptx()));
        using var surface = result.Surface;

        // Assert: 480 by 360 pixels, red over the left half, nothing painted on the right half
        Assert.Equal((480, 360), (surface.Width, surface.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[100, 100]);
        Assert.Equal(0, surface[400, 100].A);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that the slide is fitted inside the requested box without distortion, and that one requested dimension
    ///     keeps the aspect ratio.
    /// </summary>
    [Fact]
    public void PptxImageSource_Render_RequestedSize_FitsSlideInsideBoxKeepingAspectRatio()
    {
        // Arrange
        var bytes = CreatePptx();
        var source = new PptxImageSource();

        // Act
        using var boxed = source.Render(Request(bytes, 240, 240)).Surface;
        using var widthOnly = source.Render(Request(bytes, 240)).Surface;

        // Assert: the 4 to 3 slide fits a square box as 240 by 180
        Assert.Equal((240, 180), (boxed.Width, boxed.Height));
        Assert.Equal((240, 180), (widthOnly.Width, widthOnly.Height));
    }

    /// <summary>
    ///     Test that a size beyond <see cref="ImageSourceRequest.RemainingPixels"/> is refused with
    ///     <see cref="ImageSourceBudgetException"/> before the slide is drawn (the allocation stays far below the 64 MB
    ///     of the picture), and that a size that exactly fits is drawn.
    /// </summary>
    [Fact]
    public void PptxImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing()
    {
        // Arrange
        var bytes = CreatePptx();
        var source = new PptxImageSource();

        // Act: a 4000 by 4000 box (the slide is fitted to 4000 by 3000) against a budget of 1000 pixels
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<ImageSourceBudgetException>(() => source.Render(Request(bytes, 4000, 4000, remaining: 1000)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using var fits = source.Render(Request(bytes, 240, 240, remaining: 240 * 180)).Surface;

        // Assert
        Assert.Equal((4000L * 3000, 1000L), (ex.RequestedPixels, ex.RemainingPixels));
        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated} bytes were allocated before the refusal.");
        Assert.Equal((240, 180), (fits.Width, fits.Height));
    }

    /// <summary>
    ///     Test that a picture the renderer skips is returned as an unsupported note with the source <c>pptx</c>, and that
    ///     the strict request throws instead.
    /// </summary>
    [Fact]
    public void PptxImageSource_Render_SkippedPicture_ReturnsDiagnosticOrThrowsWhenStrict()
    {
        // Arrange
        var bytes = CreatePptx(withMetafile: true);
        var source = new PptxImageSource();

        // Act
        var result = source.Render(Request(bytes));
        using var surface = result.Surface;

        // Assert
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal(("pptx", ImageDiagnosticSeverity.Unsupported), (note.Source, note.Severity));
        Assert.NotEmpty(note.Code);
        Assert.Throws<PptxUnsupportedFeatureException>(() => source.Render(Request(bytes, strict: true)));
    }

    /// <summary>
    ///     Test that content that is not a presentation, a request without file content, a null request and a canceled
    ///     request are rejected.
    /// </summary>
    [Fact]
    public void PptxImageSource_Render_InvalidInput_Throws()
    {
        // Arrange
        var source = new PptxImageSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => source.Render(Request([1, 2, 3])));
        Assert.Throws<InvalidOperationException>(() => source.Render(new ImageSourceRequest { Href = "camera:front" }));
        Assert.Throws<ArgumentNullException>(() => source.Render(null!));
        Assert.Throws<OperationCanceledException>(() => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.pptx", CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test the identity of the source: its name, its extension, and that it claims no scheme.
    /// </summary>
    [Fact]
    public void PptxImageSource_Properties_NameExtensionAndNoSchemes()
    {
        // Arrange
        var source = new PptxImageSource();

        // Act / Assert
        Assert.Equal("pptx", source.Name);
        Assert.Equal([".pptx"], source.Extensions);
        Assert.Empty(source.Schemes);
    }
}
