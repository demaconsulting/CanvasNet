// cspell:ignore xobject devicergb Zapf Nonsymbolic OTTO cidfonttype Noto
using System.Security.Cryptography;
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
    ///     <see cref="PdfDocument.Render(int, int, int)"/> for two different pages against the <em>same</em>
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

    /// <summary>Proves <see cref="PdfDocument.Render(int, int, int)"/> returns a blank, correctly sized surface (Phase 1).</summary>
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> rasterizes real path geometry end-to-end: a
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
    ///     <see cref="PdfDocument.Render(int, int, int)"/>, distinct from the structural malformations
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
    ///     Proves <see cref="PdfDocument.Open(Stream, string?)"/> genuinely decrypts and renders an
    ///     RC4 128-bit (<c>/V 2</c>/<c>/R 3</c>) encrypted document end-to-end when the correct,
    ///     non-empty, real user password is supplied: a synthetic, in-memory encrypted PDF (no
    ///     binary fixture) whose <c>/Contents</c> stream is RC4-encrypted with a file key derived
    ///     from a real (not empty) user password, proving the painted rectangle's device pixel is
    ///     actually opaque black (i.e. the content stream was genuinely decrypted to its real
    ///     plaintext operators and rendered, not merely that <c>Open</c> fails to throw).
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfOpen_EncryptedRc4_CorrectUserPassword_DecryptsAndRendersExpectedPixels()
    {
        // Arrange: RC4 128-bit, correct real (non-empty) user password "test".
        const int keyLengthBytes = 16;
        const int revision = 3;
        const int permissions = -3904;
        const string userPassword = "test";
        const string plaintextContent = "10 10 40 40 re f";
        byte[] idBytes = [.. Enumerable.Range(0, 16).Select(i => (byte)(0x10 + i))];

        var paddedUserPassword = EncodeAndPadRc4Password(userPassword);
        var oBytes = ComputeOwnerEntryRc4Algorithm3(keyLengthBytes, revision, PasswordPadding, paddedUserPassword);
        var fileKey = ComputeFileKeyRc4Algorithm2(paddedUserPassword, oBytes, permissions, idBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryRc4Algorithm45(fileKey, idBytes);

        var objectKey = ComputeObjectKeyRc4Algorithm1(fileKey, 4, 0);
        var encryptedContent = Rc4(objectKey, System.Text.Encoding.ASCII.GetBytes(plaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 2 /R {revision} /Length 128 /O <{Convert.ToHexString(oBytes)}> /U <{Convert.ToHexString(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, idBytes, encryptedContent);

        // Act
        using var document = PdfDocument.Open(new MemoryStream(pdfBytes), userPassword);
        using var surface = document.Render(0, 100, 100);

        // Assert: the rectangle's device footprint is painted; outside it is left blank.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> paints real device color end-to-end (Phase 3):
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> decodes and composites an image XObject
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> executes a <c>/Subtype /Form</c> XObject
    ///     placed via the <c>Do</c> operator end-to-end: a synthetic, in-memory single-page PDF
    ///     (no binary fixture) whose page content stream invokes <c>/Fm0 Do</c>, where <c>/Fm0</c>
    ///     is a genuine Form XObject (<c>/Type /XObject /Subtype /Form</c>) with its own nested
    ///     content stream filling a rectangle, proving the Form's nested content is actually
    ///     painted at the correct device pixels (not merely that <c>Do</c> fails to throw).
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfRender_FormXObject_PaintsNestedContentStream()
    {
        // Arrange: the Form's own content stream fills a centered rectangle.
        const string formContent = "10 10 80 80 re f";
        var formStreamBytes = System.Text.Encoding.ASCII.GetBytes(formContent);
        var formStreamBody = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /XObject /Subtype /Form /BBox [0 0 100 100] /Length {formStreamBytes.Length} >>\nstream\n{formContent}\nendstream");

        var bytes = BuildSyntheticPatternPdf(
            "/Fm0 Do",
            "/XObject << /Fm0 5 0 R >>",
            [formStreamBody]);

        // Act
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        using var surface = document.Render(0, 100, 100);

        // Assert: the rectangle's device footprint is painted; outside it is left blank.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> decodes an <c>LZWDecode</c>-compressed page
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> decodes an <c>ASCII85Decode</c>-armored page
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> decodes an <c>ASCIIHexDecode</c>-armored page
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> decodes a <c>RunLengthDecode</c>-compressed
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> resolves an embedded simple TrueType font end
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> resolves a <c>/Type0</c>/<c>/Identity-H</c>
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> resolves a <c>/Type0</c>/<c>/Identity-H</c>
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> resolves a <c>/Subtype /Type1</c> simple font
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> resolves a Standard-14 simple TrueType font
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
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> resolves a <c>/BaseFont /Symbol</c> font with
    ///     no embedded <c>/FontFile2</c> and no <c>/FontDescriptor</c> entries at all (PDF
    ///     32000-1 §9.6.2.2 permits an entirely absent/empty descriptor) end to end (Phase 6):
    ///     Symbol/ZapfDingbats no longer fail closed, instead resolving via the bundled Noto
    ///     substitute font union and painting real, visible glyph ink - the same
    ///     "actually painted ink, not merely did not throw" assertion style as the Standard-14
    ///     Helvetica fallback test above.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_RenderSymbolFontWithoutEmbeddedFont_PaintsVisibleGlyphInk()
    {
        // Arrange: a synthetic single-page PDF with a /Symbol font resource, no /FontFile2, and
        // an entirely empty /FontDescriptor
        var bytes = BuildSyntheticFontFallbackPdf(
            string.Empty,
            "/Type /Font /Subtype /TrueType /BaseFont /Symbol /FirstChar 97 /LastChar 97 /Widths [600]",
            "BT /F1 60 Tf 20 30 Td (a) Tj ET");

        // Act
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        using var surface = document.Render(0, 100, 100);

        // Assert: at least one visibly-painted (non-transparent) pixel proves a real Noto
        // substitute glyph was actually rendered, not merely that no exception was thrown.
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

        Assert.True(paintedAnyPixel, "Expected the Symbol Noto-substitute-resolved font to paint visible glyph ink.");
    }

    /// <summary>
    ///     Proves, via the full production round-trip (<see cref="SystemFontCatalog.LoadBundledFallback"/>'s
    ///     embedded-resource-loading path, loading <c>NotoSans-Regular.ttf</c> the same way
    ///     <c>PdfDocument.FontFallback.cs</c>'s <c>ResolveSymbolicNotoFallback</c> does for the
    ///     <c>Symbol</c> substitute's primary font) that Symbol code <c>0x61</c> ('alpha', per
    ///     PDF 32000-1 Appendix D's Symbol encoding, mapped to Unicode U+03B1) resolves to a
    ///     nonzero, valid glyph index - proving the bundled substitute font genuinely carries a
    ///     real Greek alpha glyph, not merely that the pipeline declines to throw.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_NotoSansRegularBundledFont_ResolvesSymbolAlphaToNonzeroGlyphIndex()
    {
        // Arrange: load the exact same embedded resource the Symbol fallback path loads
        using var stream = typeof(SystemFontCatalog).Assembly.GetManifestResourceStream(
            "DemaConsulting.CanvasNet.Fonts.BundledFonts.NotoSans-Regular.ttf");
        Assert.NotNull(stream);
        var font = TrueTypeFont.Load(stream);

        // Act: Symbol code 0x61 ('alpha') maps to Unicode U+03B1 per the Symbol encoding table
        var glyphIndex = font.GetGlyphIndex(0x03B1);

        // Assert: a real, non-.notdef glyph exists for Greek alpha in this substitute font
        Assert.NotEqual(0, glyphIndex);
    }

    /// <summary>
    ///     Regression guard: proves <see cref="PdfDocument.Render(int, int, int)"/> still fails closed end to
    ///     end (Phase 6) for a non-Symbol/ZapfDingbats font whose <c>/FontDescriptor/Flags</c>
    ///     declares the <c>Symbolic</c> bit without also declaring <c>Nonsymbolic</c>, and has no
    ///     embedded <c>/FontFile2</c> - proving the new Symbol/ZapfDingbats Noto-substitution
    ///     path introduced by this feature did not loosen the fail-closed policy for any other
    ///     symbolic font.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_RenderOtherSymbolicFontWithoutEmbeddedFont_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange: a synthetic single-page PDF with a non-Symbol/ZapfDingbats symbolic font
        // resource (Flags = 4, Symbolic bit only) and no /FontFile2
        var bytes = BuildSyntheticFontFallbackPdf(
            "/Type /FontDescriptor /Flags 4",
            "/Type /Font /Subtype /TrueType /BaseFont /SomeCustomSymbolFont",
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

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> paints an axial (<c>/ShadingType 2</c>) shading
    ///     pattern fill end-to-end through the public API: a synthetic, in-memory single-page PDF
    ///     (no binary fixture) declaring a <c>/Pattern</c>-color-space fill driven by a
    ///     <c>/FunctionType 2</c> function, proving the painted gradient visibly varies from
    ///     near-black at one end to near-white at the other.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_AxialShadingPatternFill_PaintsVisiblyVaryingColors()
    {
        // Arrange: axis from (0,0) to (100,0), black -> white, filling the whole 100x100 page.
        var bytes = BuildSyntheticPatternPdf(
            "/Pattern cs /P1 scn 0 0 100 100 re f",
            "/Pattern << /P1 5 0 R >>",
            [
                "<< /PatternType 2 /Shading 6 0 R >>"u8.ToArray(),
                "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] /Function 7 0 R >>"u8.ToArray(),
                BuildPatternFunctionStreamBody("/FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [1 1 1] /N 1"),
            ]);

        // Act
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        using var surface = document.Render(0, 100, 100);

        // Assert: near-black at the start coordinate, near-white at the end coordinate.
        Assert.True(surface[2, 50].R < 50);
        Assert.True(surface[97, 50].R > 200);
    }

    /// <summary>
    ///     Proves <see cref="PdfDocument.Render(int, int, int)"/> paints a colored (<c>/PaintType 1</c>) tiling
    ///     pattern fill end-to-end through the public API: a synthetic, in-memory single-page PDF
    ///     (no binary fixture) declaring a <c>/Pattern</c>-color-space fill driven by a 10x10
    ///     pattern cell (left half red, right half blue), proving the painted result repeats both
    ///     tile colors at the correctly offset device pixel columns.
    /// </summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_ColoredTilingPatternFill_PaintsRepeatingTileColors()
    {
        // Arrange: 10x10 pattern-space cell, left half red / right half blue, tiled across a
        // 100x100 fill.
        const string cellContent = "1 0 0 rg 0 0 5 10 re f 0 0 1 rg 5 0 5 10 re f";
        var patternStreamBytes = System.Text.Encoding.ASCII.GetBytes(cellContent);
        var patternStreamBody = System.Text.Encoding.ASCII.GetBytes(
            $"<< /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 10 10] /XStep 10 /YStep 10 /Length {patternStreamBytes.Length} >>\nstream\n{cellContent}\nendstream");

        var bytes = BuildSyntheticPatternPdf(
            "/Pattern cs /P1 scn 0 0 100 100 re f",
            "/Pattern << /P1 5 0 R >>",
            [patternStreamBody]);

        // Act
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        using var surface = document.Render(0, 100, 100);

        // Assert: both tile colors appear, at multiple repeated tile offsets.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[2, 50]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[7, 50]);
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[92, 50]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[97, 50]);
    }

    /// <summary>Builds a <c>/FunctionType 2</c> stream object body (no sample data - exponential functions carry no <c>/FunctionType 0</c> sample bytes) for <see cref="BuildSyntheticPatternPdf"/>'s own <paramref name="dictionaryEntries"/>-driven extra objects.</summary>
    private static byte[] BuildPatternFunctionStreamBody(string dictionaryEntries) =>
        System.Text.Encoding.ASCII.GetBytes($"<< {dictionaryEntries} /Length 0 >>\nstream\n\nendstream");

    /// <summary>
    ///     Builds a minimal, synthetic, in-memory single-page PDF (100x100 <c>/MediaBox</c>)
    ///     declaring the given content stream and <c>/Resources</c> body (object 4's own page
    ///     dictionary references object 5, 6, 7, ... in <paramref name="extraObjectBodies"/>
    ///     order), mirroring <see cref="BuildSyntheticFontFallbackPdf"/>'s own "hand-rolled
    ///     classic-xref PDF, no binary fixture" shape - used by the Pattern system-integration
    ///     tests so no new binary PDF fixture file is needed for them.
    /// </summary>
    private static byte[] BuildSyntheticPatternPdf(string content, string resourcesBody, IReadOnlyList<byte[]> extraObjectBodies)
    {
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        var streamBody = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream");

        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            System.Text.Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /Contents 4 0 R /Resources << {resourcesBody} >> >>"),
            streamBody,
        };
        bodies.AddRange(extraObjectBodies);

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

    #region RC4 encryption test-only helpers

    /// <summary>
    ///     The standard 32-byte password padding string (ISO 32000-1 7.6.3.3). Duplicated
    ///     (rather than shared) from <see cref="PdfDocumentEncryptionTests"/>'s own identical
    ///     constant/helpers below, consistent with that file's own "independently re-derived, not
    ///     copy-pasted" philosophy for test-only cryptographic helpers - this system-level test
    ///     only needs the single RC4-with-a-real-password path, not that file's full RC4/AES
    ///     matrix, so the subset is re-derived here rather than exposed as shared production- or
    ///     test-internal surface.
    /// </summary>
    private static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>A from-scratch, hand-rolled RC4 stream cipher (classic KSA/PRGA) - symmetric, so this single method both "encrypts" the test fixture and would decrypt it.</summary>
    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var state = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            state[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
        }

        var output = new byte[data.Length];
        var x = 0;
        j = 0;
        for (var n = 0; n < data.Length; n++)
        {
            x = (x + 1) & 0xFF;
            j = (j + state[x]) & 0xFF;
            (state[x], state[j]) = (state[j], state[x]);
            var keystreamByte = state[(state[x] + state[j]) & 0xFF];
            output[n] = (byte)(data[n] ^ keystreamByte);
        }

        return output;
    }

    /// <summary>Encodes (Latin-1) and pads/truncates a real password to exactly 32 bytes per ISO 32000-1 7.6.3.3.</summary>
    private static byte[] EncodeAndPadRc4Password(string password)
    {
        var encoded = System.Text.Encoding.Latin1.GetBytes(password);
        var padded = new byte[32];
        var copyLength = Math.Min(encoded.Length, 32);
        encoded.AsSpan(0, copyLength).CopyTo(padded);
        if (copyLength < 32)
        {
            PasswordPadding.AsSpan(0, 32 - copyLength).CopyTo(padded.AsSpan(copyLength));
        }

        return padded;
    }

    /// <summary>Computes the Encrypt dictionary's <c>/O</c> entry per ISO 32000-1 Algorithm 3's encrypt direction.</summary>
    private static byte[] ComputeOwnerEntryRc4Algorithm3(int keyLengthBytes, int revision, byte[] paddedOwnerPasswordBytes, byte[] paddedUserPasswordBytes)
    {
        var digest = MD5.HashData(paddedOwnerPasswordBytes);
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLengthBytes).ToArray());
            }
        }

        var ownerKey = digest.AsSpan(0, keyLengthBytes).ToArray();
        var result = Rc4(ownerKey, paddedUserPasswordBytes);

        if (revision >= 3)
        {
            for (var round = 1; round <= 19; round++)
            {
                var roundKey = new byte[ownerKey.Length];
                for (var i = 0; i < ownerKey.Length; i++)
                {
                    roundKey[i] = (byte)(ownerKey[i] ^ round);
                }

                result = Rc4(roundKey, result);
            }
        }

        return result;
    }

    /// <summary>Computes the file encryption key per ISO 32000-1 Algorithm 2, given an already-padded 32-byte password.</summary>
    private static byte[] ComputeFileKeyRc4Algorithm2(byte[] paddedPasswordBytes, byte[] oBytes, int permissions, byte[] idBytes, int keyLengthBytes, int revision)
    {
        using var input = new MemoryStream();
        input.Write(paddedPasswordBytes);
        input.Write(oBytes);
        input.Write([(byte)permissions, (byte)(permissions >> 8), (byte)(permissions >> 16), (byte)(permissions >> 24)]);
        input.Write(idBytes);

        var digest = MD5.HashData(input.ToArray());
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLengthBytes).ToArray());
            }
        }

        return digest.AsSpan(0, keyLengthBytes).ToArray();
    }

    /// <summary>Computes the Encrypt dictionary's <c>/U</c> entry per ISO 32000-1 Algorithm 5 (revision 3/4).</summary>
    private static byte[] ComputeUserEntryRc4Algorithm45(byte[] fileKey, byte[] idBytes)
    {
        using var hashInput = new MemoryStream();
        hashInput.Write(PasswordPadding);
        hashInput.Write(idBytes);
        var result = MD5.HashData(hashInput.ToArray());
        result = Rc4(fileKey, result);
        for (var round = 1; round <= 19; round++)
        {
            var roundKey = new byte[fileKey.Length];
            for (var i = 0; i < fileKey.Length; i++)
            {
                roundKey[i] = (byte)(fileKey[i] ^ round);
            }

            result = Rc4(roundKey, result);
        }

        var padded = new byte[32];
        result.CopyTo(padded, 0);
        for (var i = 16; i < 32; i++)
        {
            padded[i] = (byte)(0xAA + i);
        }

        return padded;
    }

    /// <summary>Computes a per-object encryption key per ISO 32000-1 Algorithm 1 (RC4 only).</summary>
    private static byte[] ComputeObjectKeyRc4Algorithm1(byte[] fileKey, int objectNumber, int generation)
    {
        using var input = new MemoryStream();
        input.Write(fileKey);
        input.Write([(byte)objectNumber, (byte)(objectNumber >> 8), (byte)(objectNumber >> 16)]);
        input.Write([(byte)generation, (byte)(generation >> 8)]);

        var digest = MD5.HashData(input.ToArray());
        var keyLength = Math.Min(fileKey.Length + 5, 16);
        return digest.AsSpan(0, keyLength).ToArray();
    }

    /// <summary>
    ///     Builds an in-memory, single-page (<c>/MediaBox [0 0 100 100]</c>), classic-xref,
    ///     encrypted PDF: objects 1-3 are the Catalog/Pages/Page, object 4 is the RC4-encrypted
    ///     <c>/Contents</c> stream holding <paramref name="encryptedContentBytes"/> verbatim,
    ///     object 5 is the Encrypt dictionary (<paramref name="encryptDictBody"/>), and the
    ///     trailer declares <c>/Encrypt 5 0 R</c> plus a two-element <c>/ID</c> array (both
    ///     elements set to <paramref name="idBytes"/>).
    /// </summary>
    private static byte[] BuildEncryptedPdf(string encryptDictBody, byte[] idBytes, byte[] encryptedContentBytes)
    {
        var header = System.Text.Encoding.ASCII.GetBytes($"<< /Length {encryptedContentBytes.Length} >>\nstream\n");
        var footer = "\nendstream"u8.ToArray();
        var contentStreamBody = new byte[header.Length + encryptedContentBytes.Length + footer.Length];
        header.CopyTo(contentStreamBody, 0);
        encryptedContentBytes.CopyTo(contentStreamBody, header.Length);
        footer.CopyTo(contentStreamBody, header.Length + encryptedContentBytes.Length);

        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>"u8.ToArray(),
            contentStreamBody,
            System.Text.Encoding.ASCII.GetBytes(encryptDictBody),
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

        var idHex = Convert.ToHexString(idBytes);
        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R /Encrypt 5 0 R /ID [<{idHex}> <{idHex}>] >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    #endregion
}
