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
}
