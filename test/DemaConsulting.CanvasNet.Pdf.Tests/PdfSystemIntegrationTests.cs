// cspell:ignore xobject devicergb
using DemaConsulting.CanvasNet.Codecs;

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

    /// <summary>Proves an encrypted document is rejected rather than silently mis-parsed.</summary>
    [Fact]
    public void CanvasNetPdf_SystemIntegration_PdfEncryptDetection_EncryptedTrailerThrowsUnsupportedImageFeatureException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => PdfDocument.Open(Fixture("encrypted-trailer.pdf")));
        Assert.Equal("pdf-encrypted", exception.Feature);
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
}
