using CanvasNet.Canvas;
using CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests;

/// <summary>
///     System-level integration tests for the CanvasNet system.
/// </summary>
public class CanvasNetTests
{
    /// <summary>
    ///     Proves that the system can construct a Surface and set a pixel through the public
    ///     API, and that the pixel is observable through subsequent reads.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CanvasConstructAndSetPixel_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API
        var surface = new Surface(2, 2);
        var pixel = new Rgba32(12, 34, 56, 78);

        // Act: set a pixel and read it back
        surface[1, 1] = pixel;
        var result = surface[1, 1];

        // Assert: the system produces the expected integrated pixel value
        Assert.Equal(pixel, result);
    }

    /// <summary>
    ///     Proves that the system can crop a Surface to an independent sub-region, and that the
    ///     result and source do not share storage.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CanvasCrop_ReturnsIndependentSubRegion()
    {
        // Arrange: construct a surface and set a distinct pixel in the region to be cropped
        var surface = new Surface(3, 3);
        surface[1, 1] = new Rgba32(1, 2, 3, 4);

        // Act: crop the region containing the pixel, then mutate the source
        var cropped = surface.Crop(1, 1, 2, 2);
        surface[1, 1] = new Rgba32(9, 9, 9, 9);

        // Assert: the cropped result retains the original pixel, independent of the source
        Assert.Equal(new Rgba32(1, 2, 3, 4), cropped[0, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to BMP and load it back through the public
    ///     API, preserving pixel values end to end.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_BmpSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(2, 2);
        surface[1, 0] = new Rgba32(11, 22, 33, 255);
        using var stream = new MemoryStream();

        // Act: save the surface to BMP and load it back through the public API
        BmpCodec.Save(surface, stream);
        stream.Position = 0;
        var loaded = BmpCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value
        Assert.Equal(surface[1, 0], loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to PNG and load it back through the public
    ///     API, preserving pixel values end to end.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_PngSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(2, 2);
        surface[1, 0] = new Rgba32(11, 22, 33, 200);
        using var stream = new MemoryStream();

        // Act: save the surface to PNG and load it back through the public API
        PngCodec.Save(surface, stream);
        stream.Position = 0;
        var loaded = PngCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value
        Assert.Equal(surface[1, 0], loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to TIFF and load it back through the public
    ///     API, preserving pixel values end to end.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_TiffSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(2, 2);
        surface[1, 0] = new Rgba32(11, 22, 33, 200);
        using var stream = new MemoryStream();

        // Act: save the surface to TIFF and load it back through the public API
        TiffCodec.Save(surface, stream, TiffCompression.Lzw);
        stream.Position = 0;
        var loaded = TiffCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value
        Assert.Equal(surface[1, 0], loaded[1, 0]);
    }

    /// <summary>
    ///     Proves that the system can save a Surface to JPEG and load it back through the public
    ///     API, reproducing pixel values within JPEG's lossy compression tolerance.
    /// </summary>
    /// <remarks>
    ///     Unlike the BMP/PNG/TIFF system-integration tests above, this test does not assert
    ///     exact pixel equality: JPEG is a lossy format, so a per-channel tolerance is used
    ///     instead, matching the tolerance-based approach used throughout
    ///     <c>JpegCodecTests</c>/<c>JpegFixtureTests</c>.
    /// </remarks>
    [Fact]
    public void CanvasNet_SystemIntegration_JpegSaveThenLoad_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a distinct pixel
        var surface = new Surface(16, 16);
        surface[1, 0] = new Rgba32(11, 22, 33, 255);
        using var stream = new MemoryStream();

        // Act: save the surface to JPEG and load it back through the public API
        JpegCodec.Save(surface, stream, 90);
        stream.Position = 0;
        var loaded = JpegCodec.Load(stream);

        // Assert: the system produces the expected integrated round-trip pixel value, within
        // JPEG's lossy compression tolerance
        var expected = surface[1, 0];
        var actual = loaded[1, 0];
        // Note: a single isolated distinct pixel against an otherwise uniform background induces
        // DCT ringing across its entire 8x8 block, producing a larger per-channel delta than the
        // gradient/flat-region round-trips exercised in JpegCodecTests; a generous tolerance of
        // 40 is used here to accommodate that worst case while still detecting a functionally
        // broken round-trip.
        const int tolerance = 40;
        Assert.True(Math.Abs(expected.R - actual.R) <= tolerance, $"R delta {Math.Abs(expected.R - actual.R)} exceeded tolerance");
        Assert.True(Math.Abs(expected.G - actual.G) <= tolerance, $"G delta {Math.Abs(expected.G - actual.G)} exceeded tolerance");
        Assert.True(Math.Abs(expected.B - actual.B) <= tolerance, $"B delta {Math.Abs(expected.B - actual.B)} exceeded tolerance");
    }

    /// <summary>
    ///     Proves that the system can composite a semi-transparent constant color over a Surface
    ///     through the public API, producing the expected Porter-Duff "over" result.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CompositeColorOverSurface_ReturnsExpectedPixel()
    {
        // Arrange: construct an opaque green background surface through the public API
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(0, 255, 0, 255);

        // Act: composite a semi-transparent red overlay over the surface in place
        surface.CompositeOver(new Rgba32(255, 0, 0, 128));

        // Assert: the system produces the expected integrated compositing result
        Assert.Equal(new Rgba32(128, 127, 0, 255), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that the system can convert a Surface's pixel buffer from straight to
    ///     premultiplied alpha through the public API, producing the expected rounded result.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_PremultiplyAlpha_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a straight-alpha pixel
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(200, 100, 50, 128);

        // Act: premultiply the surface's alpha in place
        surface.PremultiplyAlpha();

        // Assert: the system produces the expected integrated premultiplied pixel value
        // (round(200*128/255)=100, round(100*128/255)=50, round(50*128/255)=25; alpha unchanged)
        Assert.Equal(new Rgba32(100, 50, 25, 128), surface[0, 0]);
    }

    /// <summary>
    ///     Proves that the system can convert a Surface's pixel buffer from premultiplied back to
    ///     straight alpha through the public API, including the load-bearing clamp when the
    ///     division overshoots 255.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_UnpremultiplyAlpha_ReturnsExpectedPixel()
    {
        // Arrange: construct a surface through the public API and set a premultiplied pixel whose
        // red channel overshoots 255 when unpremultiplied, exercising the documented clamp
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(100, 10, 0, 50);

        // Act: unpremultiply the surface's alpha in place
        surface.UnpremultiplyAlpha();

        // Assert: the system produces the expected integrated unpremultiplied pixel value
        // (round(100*255/50)=510, clamped to 255; round(10*255/50)=51; 0 stays 0; alpha unchanged)
        Assert.Equal(new Rgba32(255, 51, 0, 50), surface[0, 0]);
    }
}
