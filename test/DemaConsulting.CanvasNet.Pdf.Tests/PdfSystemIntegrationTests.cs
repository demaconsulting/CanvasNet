// cspell:ignore xobject devicergb Zapf Nonsymbolic OTTO cidfonttype
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     System-level integration tests for PDF page-info/blank-render support via the
///     CanvasNet.Pdf package. Each test proves one <c>CanvasNetPdf-*</c> top-level requirement
///     end-to-end through the public <see cref="PdfDocument"/> API, complementing (never
///     replacing) the unit-level <c>PdfDocument_*</c> coverage in
///     <see cref="PdfDocumentTests"/>, which exercises each feature's finer-grained behavioral
///     variations.
/// </summary>
public class PdfSystemIntegrationTests
{
    private static string FixturesPath => Path.Join(AppContext.BaseDirectory, "PdfFixtures");

    private static string Fixture(string name) => Path.Join(FixturesPath, name);

    /// <summary>
    ///     The path to the real "Open Sans" TrueType font, copied to the test output directory by
    ///     this project's <c>FontFixtures\**</c> content-link item (mirroring
    ///     <c>DemaConsulting.CanvasNet.Svg.Tests</c>'s own reuse of the same shared file - see
    ///     <c>DemaConsulting.CanvasNet.Tests\FontFixtures\README.md</c> for provenance/licensing).
    /// </summary>
    private static string FontPath => Path.Join(AppContext.BaseDirectory, "FontFixtures", "OpenSans-Regular.ttf");

    /// <summary>
    ///     Opens a multi-page fixture once, asserts <see cref="PdfDocument.PageCount"/>, then calls
    ///     <see cref="PdfDocument.GetPageInfo"/> for two different pages and
    ///     <see cref="PdfDocument.Render"/> for two different pages against the <em>same</em>
    ///     opened instance - demonstrating the "parse once, reused across calls" property that no
    ///     stateless design could express. This is also the shared platform-proof test referenced
    ///     by every <c>CanvasNetPdf-Platform-*</c> requirement.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfOpen_ReturnsExpectedPageCount()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));

        // Act
        var pageCount = document.PageCount;
        var firstInfo = document.GetPageInfo(0);
        var secondInfo = document.GetPageInfo(2);
        using var firstSurface = document.Render(0, 32, 32);
        using var secondSurface = document.Render(2, 16, 16);

        // Assert
        Assert.Equal(3, pageCount);
        Assert.Equal(200, firstInfo.Width);
        Assert.Equal(300, firstInfo.Height);
        Assert.Equal(150, secondInfo.Width);
        Assert.Equal(250, secondInfo.Height);
        Assert.Equal(32, firstSurface.Width);
        Assert.Equal(16, secondSurface.Width);
    }

    /// <summary>Proves a rotated page reports swapped display dimensions and its effective rotation.</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfGetPageInfo_ReportsRotatedDimensions()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));

        // Act
        var info = document.GetPageInfo(1);

        // Assert: raw /MediaBox [0 0 400 100] with /Rotate 90 swaps to a taller-than-wide display
        Assert.Equal(100, info.Width);
        Assert.Equal(400, info.Height);
        Assert.Equal(90, info.Rotation);
    }

    /// <summary>Proves <see cref="PdfDocument.Render"/> returns a blank, correctly sized surface (Phase 1).</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_ReturnsBlankSizedSurface()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act
        using var surface = document.Render(0, 40, 20);

        // Assert
        Assert.Equal(40, surface.Width);
        Assert.Equal(20, surface.Height);
        Assert.Equal(default, surface[0, 0]);
        Assert.Equal(default, surface[39, 19]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> rasterizes real path geometry end-to-end: a
    ///     hand-authored fixture containing a filled rectangle and a stroked vertical line,
    ///     asserting specific opaque-black/transparent pixels at specific coordinates (not merely
    ///     "the surface is not blank").
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_FilledRectangleAndStrokedLine_PaintsExpectedPixels()
    {
        // Arrange: MediaBox [0 0 100 100], /Contents = "10 10 40 40 re f\n2 w 60 10 m 60 90 l S"
        // (a filled 40x40 square at user (10,10)-(50,50), plus a 2-unit-wide stroked vertical
        // line at user x=60 from y=10 to y=90).
        using var document = PdfDocument.Open(Fixture("path-construction-rect-and-line.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: interior of the filled rectangle is opaque black; a point outside it is not.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);

        // Assert: the stroked line is opaque black at its own x-position, but not a bit away.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[60, 50]);
        Assert.Equal(default, surface[70, 50]);
    }

    /// <summary>
    ///     Proves the base CTM correctly incorporates a page's effective <c>/Rotate</c>: a
    ///     hand-authored, 90-degree-rotated fixture's asymmetric filled rectangle lands at its
    ///     mathematically correct device-pixel position, not at the mirrored/opposite position a
    ///     270-degree-instead-of-90-degree rotation-sign bug would produce.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_RotatedPage_MapsGeometryToCorrectPixelPosition()
    {
        // Arrange: MediaBox [0 0 200 100], /Rotate 90, /Contents = "10 10 30 20 re f" (an
        // asymmetric rectangle near the raw MediaBox's bottom-left corner).
        using var document = PdfDocument.Open(Fixture("path-construction-rotated-page.pdf"));

        // Act: displayed size swaps to 100x200 under /Rotate 90 - render at that exact size.
        using var surface = document.Render(0, 100, 200);

        // Assert: correctly rotated, the rectangle lands at device px in [10,30), py in [10,40).
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[20, 25]);

        // Assert: a 270-instead-of-90 rotation-sign bug would instead place it near (80,175) -
        // proving this specific pixel is untouched rules out that regression.
        Assert.Equal(default, surface[80, 175]);
    }

    /// <summary>Proves a null stream is rejected before any parsing is attempted.</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfValidationNull_NullStreamThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => PdfDocument.Open((Stream)null!));
    }

    /// <summary>Proves an empty path is rejected before any file access is attempted.</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfValidationEmptyPath_EmptyPathThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => PdfDocument.Open(string.Empty));
    }

    /// <summary>
    ///     Proves a well-formed document/xref/page-tree whose <c>/Contents</c> stream is itself
    ///     lexically malformed (a <c>re</c> operator given only 2 of its 4 required operands) is
    ///     rejected with <see cref="InvalidDataException"/> end-to-end through
    ///     <see cref="PdfDocument.Render"/>, distinct from the structural malformations
    ///     (<c>malformed-startxref.pdf</c>/<c>cyclic-page-tree.pdf</c>) already covered elsewhere.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfUnsupportedFormatValidation_MalformedContentStreamThrowsInvalidDataException()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("malformed-content-stream.pdf"));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => document.Render(0, 100, 100));
    }

    /// <summary>Proves an out-of-range page index is rejected by <see cref="PdfDocument.GetPageInfo"/>.</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfGetPageInfoValidation_OutOfRangePageIndexThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPageInfo(1));
    }

    /// <summary>Proves every other public member throws once the document has been disposed.</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfDispose_ObjectDisposedExceptionAfterDispose()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act
        document.Dispose();
        document.Dispose(); // idempotent - must not throw

        // Assert
        Assert.Throws<ObjectDisposedException>(() => document.PageCount);
        Assert.Throws<ObjectDisposedException>(() => document.GetPageInfo(0));
        Assert.Throws<ObjectDisposedException>(() => document.Render(0, 10, 10));
    }

    /// <summary>Proves a document encrypted with a non-<c>/Standard</c> security handler is rejected rather than silently mis-parsed.</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfEncryptDetection_EncryptedTrailerThrowsUnsupportedImageFeatureException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => PdfDocument.Open(Fixture("encrypted-trailer.pdf")));
        Assert.Equal("pdf-encrypted-filter-Adobe.PubSec", exception.Feature);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> paints real device color end-to-end (Phase 3):
    ///     a hand-authored fixture using <c>rg</c> to fill a rectangle red, asserting a specific
    ///     interior pixel is opaque red and an exterior pixel remains transparent.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_ColoredRectangleFill_PaintsExpectedRgbPixels()
    {
        // Arrange: MediaBox [0 0 100 100], /Contents = "1 0 0 rg 10 10 80 80 re f" (an
        // 80x80 rectangle filled opaque red).
        using var document = PdfDocument.Open(Fixture("color-rgb-rectangle-fill.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: interior of the filled rectangle is opaque red; a point outside it is not.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> decodes and composites an image XObject
    ///     end-to-end (Phase 3): a hand-authored fixture placing a 2x2 <c>DeviceRGB</c>
    ///     <c>FlateDecode</c> image via <c>cm</c>/<c>Do</c>, asserting specific composited pixel
    ///     colors matching the fixture's known source pixels, and a pixel outside the placed
    ///     image's device-space footprint remains transparent.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_ImageXObjectPlacement_CompositesExpectedPixels()
    {
        // Arrange: MediaBox [0 0 100 100], /Contents = "60 0 0 60 10 10 cm /Im0 Do" places a 2x2
        // image (top-left red, top-right green, bottom-left blue, bottom-right yellow) into the
        // device-space footprint x in [10,70], y in [30,90] (PDF user y=30..90 flips to that same
        // device y range for this identity-rotation, full-page-sized MediaBox).
        using var document = PdfDocument.Open(Fixture("image-xobject-devicergb-flate.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: the four source quadrants land in their mathematically correct device pixels.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[25, 40]);
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[55, 40]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 0, 255), surface[55, 75]);

        // Assert: a pixel outside the placed image's device-space footprint remains transparent.
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> decodes an <c>LZWDecode</c>-compressed page
    ///     content stream end-to-end (Phase 7): a hand-authored fixture whose <c>/Contents</c>
    ///     stream is the PDF-variant-LZW-compressed bytes of <c>"1 0 0 rg 10 10 80 80 re f"</c>
    ///     (an 80x80 rectangle filled opaque red), proving the decoded operator text is parsed
    ///     and rendered exactly like an uncompressed content stream would be.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_LzwDecodeContentStream_PaintsExpectedPixels()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("content-stream-lzw.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: interior of the filled rectangle is opaque red; a point outside it is not.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> decodes an <c>ASCII85Decode</c>-armored page
    ///     content stream end-to-end (Phase 7): a hand-authored fixture whose <c>/Contents</c>
    ///     stream is the base-85 encoding (terminated by <c>~&gt;</c>) of the same
    ///     <c>"1 0 0 rg 10 10 80 80 re f"</c> content-stream text.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_Ascii85DecodeContentStream_PaintsExpectedPixels()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("content-stream-ascii85.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: interior of the filled rectangle is opaque red; a point outside it is not.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> decodes an <c>ASCIIHexDecode</c>-armored page
    ///     content stream end-to-end (Phase 7): a hand-authored fixture whose <c>/Contents</c>
    ///     stream is the hex-digit-pair encoding (terminated by <c>&gt;</c>) of the same
    ///     <c>"1 0 0 rg 10 10 80 80 re f"</c> content-stream text.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_AsciiHexDecodeContentStream_PaintsExpectedPixels()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("content-stream-asciihex.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: interior of the filled rectangle is opaque red; a point outside it is not.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> decodes a <c>RunLengthDecode</c>-compressed
    ///     page content stream end-to-end (Phase 7): a hand-authored fixture whose
    ///     <c>/Contents</c> stream is a single PackBits-style literal run (length byte, the
    ///     literal bytes, then the <c>128</c> EOD marker) wrapping the same
    ///     <c>"1 0 0 rg 10 10 80 80 re f"</c> content-stream text.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_RunLengthDecodeContentStream_PaintsExpectedPixels()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("content-stream-runlength.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: interior of the filled rectangle is opaque red; a point outside it is not.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> resolves an embedded simple TrueType font end
    ///     to end (Phase 4): a hand-authored fixture with a real, embedded (via
    ///     <c>/FontDescriptor/FontFile2</c>) copy of the shared <c>OpenSans-Regular.ttf</c>
    ///     production font (see <c>PdfFixtures\README.md</c> for provenance and the
    ///     <c>FontFixtures\README.md</c> in <c>DemaConsulting.CanvasNet.Tests</c> for the font's
    ///     own SIL OFL 1.1 licensing - not duplicated here), drawing <c>"HO"</c> at font size 60
    ///     with the default <c>/WinAnsiEncoding</c>. Rather than hardcoding font-specific magic
    ///     pixel numbers, this test independently re-derives the expected device-pixel positions
    ///     of real glyph ink from the font's own outline/metrics (the same production
    ///     <see cref="TrueTypeFont"/> API <see cref="PdfDocument"/> itself uses) via the
    ///     documented text-rendering-matrix formula, then asserts specific pixels: one inside
    ///     'H's left stroke (opaque), one inside 'O's hollow counter (transparent), and the
    ///     canvas corners, well outside both glyphs (transparent background).
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_EmbeddedTrueTypeFontText_PaintsGlyphStrokesNotCounters()
    {
        // Arrange: MediaBox [0 0 200 100], /Contents = "BT /F1 60 Tf 10 20 Td (HO) Tj ET" -
        // renders "HO" at font size 60, text-space origin (10, 20), using the embedded font's
        // own advance widths (the font dictionary declares no /Widths array).
        const double fontSize = 60;
        const double originX = 10;
        const double originY = 20;
        const double mediaBoxHeight = 100;

        using var document = PdfDocument.Open(Fixture("text-embedded-truetype-font.pdf"));

        // Act: render at the MediaBox's own pixel dimensions (a 1:1 user-space-to-device-pixel
        // mapping, since width/height exactly match the MediaBox), then load the same real font
        // independently to re-derive expected glyph-ink pixel positions from its own metrics.
        using var surface = document.Render(0, 200, 100);
        var font = TrueTypeFont.Load(FontPath);

        // Maps a font-design-space point (in the glyph currently being measured, whose text-space
        // origin is (originXForGlyph, originY)) to the device pixel it lands on, replicating the
        // exact Trm/base-CTM composition PdfDocument.Text.cs uses: text-space x/y scale by
        // fontSize/UnitsPerEm and offset by the glyph's own text-space origin, then flip y against
        // the MediaBox height (device x is otherwise unchanged for this identity-rotation page).
        (int X, int Y) ToDevicePixel(double originXForGlyph, double fx, double fy)
        {
            var textX = fx / font.UnitsPerEm * fontSize + originXForGlyph;
            var textY = fy / font.UnitsPerEm * fontSize + originY;
            return ((int)Math.Floor(textX), (int)Math.Floor(mediaBoxHeight - textY));
        }

        var hGlyph = font.GetGlyphIndex('H');
        var oGlyph = font.GetGlyphIndex('O');
        Assert.NotEqual(0, hGlyph);
        Assert.NotEqual(0, oGlyph);

        var hBounds = font.GetGlyphOutline(hGlyph).GetBounds(0.25f);
        var oBounds = font.GetGlyphOutline(oGlyph).GetBounds(0.25f);
        Assert.False(hBounds.IsEmpty);
        Assert.False(oBounds.IsEmpty);

        // 'O' is placed immediately after 'H', advanced by 'H's own advance width (in text
        // space) - matching ShowText's undeclared-/Widths fallback to the font's own metrics.
        var originXForO = originX + (double)font.GetAdvanceWidth(hGlyph) / font.UnitsPerEm * fontSize;

        // Assert: a point 15% in from 'H's left edge, at half its glyph height, lies on 'H's
        // solid left vertical stem (which spans 'H's full height) - real opaque ink.
        var (strokeX, strokeY) = ToDevicePixel(originX, hBounds.X + hBounds.Width * 0.15, hBounds.Y + hBounds.Height * 0.5);
        Assert.True(surface[strokeX, strokeY].A > 0, $"Expected opaque ink inside 'H's left stroke at ({strokeX},{strokeY}).");

        // Assert: the exact center of 'O's bounding box lies within its hollow counter (the
        // round hole every 'O' glyph has at its geometric center) - not painted.
        var (counterX, counterY) = ToDevicePixel(
            originXForO, oBounds.X + oBounds.Width * 0.5, oBounds.Y + oBounds.Height * 0.5);
        Assert.Equal(0, surface[counterX, counterY].A);

        // Assert: the canvas's far corners, well outside both glyphs, remain fully transparent.
        Assert.Equal(0, surface[0, 0].A);
        Assert.Equal(0, surface[199, 0].A);
        Assert.Equal(0, surface[0, 99].A);
        Assert.Equal(0, surface[199, 99].A);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> resolves a <c>/Type0</c>/<c>/Identity-H</c>
    ///     <c>CIDFontType2</c> composite font end to end (Phase 9): a hand-authored fixture with a
    ///     real, embedded (via the descendant font's <c>/FontDescriptor/FontFile2</c>) copy of the
    ///     shared <c>OpenSans-Regular.ttf</c> production font (see <c>PdfFixtures\README.md</c> for
    ///     provenance), a non-identity <c>/CIDToGIDMap</c> stream remapping CID 1 to the glyph
    ///     index of <c>'H'</c> and CID 2 to the glyph index of <c>'O'</c>, and an explicit
    ///     <c>/W</c> array declaring their advance widths - drawing the 2-byte Identity-H codes
    ///     <c>0001 0002</c> at font size 60. Rather than hardcoding font-specific magic pixel
    ///     numbers, this test independently re-derives the expected device-pixel positions of real
    ///     glyph ink from the font's own outline (looked up directly by glyph index, bypassing
    ///     <c>cmap</c> entirely - exactly as the composite code path does), combined with the
    ///     fixture's own declared <c>/W</c> advance widths (composite fonts never fall back to the
    ///     font's own metrics), then asserts specific pixels: one inside 'H's left stroke (opaque),
    ///     one inside 'O's hollow counter (transparent), and the canvas corners, well outside both
    ///     glyphs (transparent background).
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_RenderType0CompositeFont_PaintsExpectedGlyphInk()
    {
        // Arrange: MediaBox [0 0 200 100], /Contents = "BT /F1 60 Tf 10 20 Td <00010002> Tj ET" -
        // renders CID 1 then CID 2 at font size 60, text-space origin (10, 20). The descendant
        // font's /CIDToGIDMap stream remaps CID 1 -> GID 43 ('H') and CID 2 -> GID 50 ('O'); its
        // /W array declares CID 1's width as 700 and CID 2's width as 650 (both /1000 em units).
        const double fontSize = 60;
        const double originX = 10;
        const double originY = 20;
        const double mediaBoxHeight = 100;
        const int hGlyph = 43;
        const int oGlyph = 50;
        const double wH = 700.0 / 1000.0;
        const double wO = 650.0 / 1000.0;

        using var document = PdfDocument.Open(Fixture("text-composite-truetype-identity-h.pdf"));

        // Act: render at the MediaBox's own pixel dimensions (a 1:1 user-space-to-device-pixel
        // mapping), then load the same real font independently to re-derive expected glyph-ink
        // pixel positions from its own outline, looked up directly by glyph index.
        using var surface = document.Render(0, 200, 100);
        var font = TrueTypeFont.Load(FontPath);

        // Maps a font-design-space point (in the glyph currently being measured, whose text-space
        // origin is (originXForGlyph, originY)) to the device pixel it lands on, replicating the
        // exact Trm/base-CTM composition PdfDocument.Text.cs uses: text-space x/y scale by
        // fontSize/UnitsPerEm and offset by the glyph's own text-space origin, then flip y against
        // the MediaBox height (device x is otherwise unchanged for this identity-rotation page).
        (int X, int Y) ToDevicePixel(double originXForGlyph, double fx, double fy)
        {
            var textX = fx / font.UnitsPerEm * fontSize + originXForGlyph;
            var textY = fy / font.UnitsPerEm * fontSize + originY;
            return ((int)Math.Floor(textX), (int)Math.Floor(mediaBoxHeight - textY));
        }

        var hBounds = font.GetGlyphOutline(hGlyph).GetBounds(0.25f);
        var oBounds = font.GetGlyphOutline(oGlyph).GetBounds(0.25f);
        Assert.False(hBounds.IsEmpty);
        Assert.False(oBounds.IsEmpty);

        // The second glyph ('O') is placed immediately after the first ('H'), advanced by CID 1's
        // declared /W width (in text space) - never the font's own metrics, since composite fonts
        // have no such fallback.
        var originXForO = originX + wH * fontSize;

        // Assert: a point 15% in from 'H's left edge, at half its glyph height, lies on 'H's
        // solid left vertical stem (which spans 'H's full height) - real opaque ink.
        var (strokeX, strokeY) = ToDevicePixel(originX, hBounds.X + hBounds.Width * 0.15, hBounds.Y + hBounds.Height * 0.5);
        Assert.True(surface[strokeX, strokeY].A > 0, $"Expected opaque ink inside 'H's left stroke at ({strokeX},{strokeY}).");

        // Assert: the exact center of 'O's bounding box lies within its hollow counter (the
        // round hole every 'O' glyph has at its geometric center) - not painted.
        var (counterX, counterY) = ToDevicePixel(
            originXForO, oBounds.X + oBounds.Width * 0.5, oBounds.Y + oBounds.Height * 0.5);
        Assert.Equal(0, surface[counterX, counterY].A);

        // Assert: a point one full CID-2 advance width (per its declared /W entry) past 'O's own
        // origin lies well beyond 'O's right edge - transparent background, confirming CID 2's
        // declared width (not the font's own advance metric) determines how far text position
        // moves.
        var (pastOX, pastOY) = ToDevicePixel(originXForO + wO * fontSize, oBounds.X + oBounds.Width * 0.5, oBounds.Y + oBounds.Height * 0.5);
        Assert.Equal(0, surface[pastOX, pastOY].A);

        // Assert: the canvas's far corners, well outside both glyphs, remain fully transparent.
        Assert.Equal(0, surface[0, 0].A);
        Assert.Equal(0, surface[199, 0].A);
        Assert.Equal(0, surface[0, 99].A);
        Assert.Equal(0, surface[199, 99].A);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> resolves a <c>/Type0</c>/<c>/Identity-H</c>
    ///     <c>CIDFontType0</c> composite font end to end (Phase 12): a hand-authored, entirely
    ///     synthetic fixture (see <c>PdfFixtures\README.md</c>) whose descendant font's
    ///     <c>/FontDescriptor/FontFile3</c> is a synthetic, non-CID-keyed, <c>/OpenType</c>-
    ///     wrapped CFF program (built via <c>SyntheticFontBuilder.Cff</c> - no third-party font
    ///     asset), with no <c>/CIDToGIDMap</c> declared (identity CID-to-glyph-index is used
    ///     unconditionally for this subtype). The content stream draws the 2-byte Identity-H code
    ///     <c>0001</c> (CID 1, resolving to GID 1 - a filled square spanning font-design-space
    ///     <c>(100, 100)</c>-<c>(500, 500)</c> of a 1000-unit em) at font size 20, text-space
    ///     origin <c>(5, 5)</c>, on a 100x100 MediaBox - matching
    ///     <see cref="PdfDocumentTests"/>'s own <c>PdfDocument_Fonts_Type0_CidFontType0_*</c> unit
    ///     tests' pixel-position convention exactly, since both share the same synthetic glyph
    ///     design.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_RenderCidFontType0CompositeFont_PaintsExpectedGlyphInk()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("text-composite-cff-cidfonttype0-identity-h.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: text x [7, 15) holds the painted square glyph (design x [100, 500) of 1000,
        // scaled by fontSize 20, offset by originX 5, flipped against the MediaBox height for
        // originY 5) - real glyph ink, not merely "did not throw".
        Assert.NotEqual(0, surface[11, 89].A);

        // Assert: the canvas's far corners, well outside the glyph, remain fully transparent.
        Assert.Equal(0, surface[0, 0].A);
        Assert.Equal(0, surface[99, 0].A);
        Assert.Equal(0, surface[0, 99].A);
        Assert.Equal(0, surface[99, 99].A);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> resolves a <c>/Subtype /Type1</c> simple font
    ///     with an embedded classic PostScript <c>/FontDescriptor/FontFile</c> program end to end
    ///     (Phase B): a hand-authored, entirely synthetic fixture (see
    ///     <c>PdfFixtures\README.md</c>) built via <c>SyntheticFontBuilder.Type1</c> (no
    ///     third-party font asset), whose <c>A</c> glyph is a filled square spanning
    ///     font-design-space <c>(100, 100)</c>-<c>(500, 500)</c> of a 1000-unit em - deliberately
    ///     matching <see cref="CanvasNetPdf_SystemIntegration_RenderCidFontType0CompositeFont_PaintsExpectedGlyphInk"/>'s
    ///     own glyph design/placement (font size 20, text-space origin <c>(5, 5)</c>, on a 100x100
    ///     MediaBox), so both share the exact same pixel-assertion convention.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_RenderEmbeddedType1Font_PaintsExpectedGlyphInk()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("text-embedded-type1-font.pdf"));

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert: text x [7, 15) holds the painted square glyph (design x [100, 500) of 1000,
        // scaled by fontSize 20, offset by originX 5, flipped against the MediaBox height for
        // originY 5) - real glyph ink, not merely "did not throw".
        Assert.NotEqual(0, surface[11, 89].A);

        // Assert: the canvas's far corners, well outside the glyph, remain fully transparent.
        Assert.Equal(0, surface[0, 0].A);
        Assert.Equal(0, surface[99, 0].A);
        Assert.Equal(0, surface[0, 99].A);
        Assert.Equal(0, surface[99, 99].A);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> resolves a Standard-14 simple TrueType font
    ///     (<c>/BaseFont /Helvetica</c>) with no embedded <c>/FontFile2</c> end to end (Phase 6):
    ///     a synthetic, in-memory single-page document (no new binary fixture needed) drawing a
    ///     single glyph. Since the actual substitute font (a matching system font, or the bundled
    ///     Liberation Sans fallback) genuinely varies across the Windows/Linux/macOS CI matrix,
    ///     this test asserts the strongest property achievable without hardcoding a
    ///     platform-specific glyph shape: real, visible glyph ink is painted somewhere on the
    ///     canvas, proving the fallback path genuinely renders rather than merely not throwing.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_RenderStandard14HelveticaWithoutEmbeddedFont_PaintsVisibleGlyphInk()
    {
        // Arrange: a synthetic single-page PDF with a /Helvetica font resource and no /FontFile2
        var bytes = BuildSyntheticFontFallbackPdf(
            "/Type /FontDescriptor",
            "/Type /Font /Subtype /TrueType /BaseFont /Helvetica /FirstChar 65 /LastChar 65 /Widths [600]",
            "BT /F1 60 Tf 20 30 Td (A) Tj ET");

        // Act
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        using var surface = document.Render(0, 100, 100);

        // Assert: at least one visibly-painted (non-transparent) pixel proves a real substitute
        // glyph was actually rendered, not merely that no exception was thrown.
        var paintedAnyPixel = false;
        for (var y = 0; y < surface.Height && !paintedAnyPixel; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    paintedAnyPixel = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyPixel, "Expected the Standard-14 fallback-resolved font to paint visible glyph ink.");
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render"/> still fails closed end to end (Phase 6) for a
    ///     <c>/BaseFont /Symbol</c> font with no embedded <c>/FontFile2</c>: Symbol/ZapfDingbats
    ///     fonts are never substituted with an unrelated system or bundled font.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_RenderSymbolFontWithoutEmbeddedFont_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange: a synthetic single-page PDF with a /Symbol font resource and no /FontFile2
        var bytes = BuildSyntheticFontFallbackPdf(
            "/Type /FontDescriptor",
            "/Type /Font /Subtype /TrueType /BaseFont /Symbol",
            "BT /F1 20 Tf (A) Tj ET");

        // Act & Assert
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => document.Render(0, 100, 100));
        Assert.Equal("pdf-font-symbolic-not-embedded", exception.Feature);
    }

    /// <summary>
    ///     Builds a minimal, synthetic, in-memory single-page PDF (100x100 <c>/MediaBox</c>)
    ///     declaring one <c>/F1</c> simple TrueType font resource (object 5, referencing a
    ///     <c>/FontDescriptor</c> at object 6, deliberately without any <c>/FontFile2</c> entry)
    ///     and the given content stream - used by the Phase 6 font-fallback end-to-end tests, so
    ///     no new binary PDF fixture file is needed for them.
    /// </summary>
    private static byte[] BuildSyntheticFontFallbackPdf(string descriptorEntries, string fontDictEntries, string content)
    {
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        var streamBody = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream");

        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>"u8.ToArray(),
            streamBody,
            System.Text.Encoding.ASCII.GetBytes($"<< {fontDictEntries} /FontDescriptor 6 0 R >>"),
            System.Text.Encoding.ASCII.GetBytes($"<< {descriptorEntries} >>"),
        };

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n"));
        buffer.AddRange("0000000000 65535 f \n"u8.ToArray());
        foreach (var offset in offsets)
        {
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }
}
