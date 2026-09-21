using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Geometry;

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
    ///     Proves that the system can composite a semi-transparent Surface over another Surface
    ///     through the public API, producing the expected Porter-Duff "over" result.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_CompositeSurfaceOverSurface_ReturnsExpectedPixel()
    {
        // Arrange: construct an opaque blue background surface, and a semi-transparent yellow
        // foreground surface, both through the public API
        var background = new Surface(2, 2);
        background[0, 0] = new Rgba32(0, 0, 255, 255);
        var foreground = new Surface(2, 2);
        foreground[0, 0] = new Rgba32(255, 255, 0, 128);

        // Act: composite the foreground surface over the background surface in place
        background.CompositeOver(foreground);

        // Assert: expected value independently computed (Porter-Duff "over", normalized [0, 1]
        // math, round-half-away-from-zero, clamped) - not copied from the CompositeOver(Rgba32)
        // test above, since fgA=128/255, bgA=1 gives outA=1 exactly, outR=outG=255*128/255=128,
        // and outB=255*(1-128/255)=127
        Assert.Equal(new Rgba32(128, 128, 127, 255), background[0, 0]);
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

    /// <summary>
    ///     Row widths, in pixels, that exercise Surface's internal row-padding boundary (padding
    ///     is applied in multiples of 16 pixels): widths at, just below, and just above each
    ///     boundary, plus a couple of larger "normal" widths.
    /// </summary>
    public static TheoryData<int> BoundaryWidths =>
    [
        1, 15, 16, 17, 31, 32, 33, 100, 257
    ];

    /// <summary>
    ///     Proves that a PNG save/load round-trip remains byte-exact at Surface's internal
    ///     row-padding boundary widths, confirming Surface's stride/padding storage detail is not
    ///     observable through the PNG codec. This is a system-level test (not a Surface unit
    ///     test) because it exercises the Codecs -> Surface integration boundary rather than
    ///     Surface in isolation: Surface's unit tests must only depend on Surface itself, and
    ///     Codecs depend on Surface (not vice versa), so a codec round-trip belongs here.
    /// </summary>
    [Theory]
    [MemberData(nameof(BoundaryWidths))]
    public void CanvasNet_SystemIntegration_PngCodecRoundTrip_BoundaryWidths_ReturnsExpectedPixels(int width)
    {
        // Arrange: build a surface at a boundary width with distinct, non-trivial pixel values
        var surface = new Surface(width, 3);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                surface[x, y] = new Rgba32((byte)(x * 17 + 1), (byte)(y * 23 + 2), (byte)(x + y + 3), (byte)(200 - (x % 200)));
            }
        }

        using var stream = new MemoryStream();

        // Act: save and reload the surface through the PNG codec
        PngCodec.Save(surface, stream, PngColorType.Rgba);
        stream.Position = 0;
        var reloaded = PngCodec.Load(stream);

        // Assert: every pixel must round-trip exactly
        Assert.Equal(surface.Width, reloaded.Width);
        Assert.Equal(surface.Height, reloaded.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(surface[x, y], reloaded[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that the system can construct a Path via PathBuilder using a MoveTo/
    ///     CubicBezierTo/ArcTo/Close sequence, flatten it to a polyline, and compute its
    ///     axis-aligned bounding Rect - exercising the Geometry namespace's public types end to
    ///     end through the public API.
    /// </summary>
    [Fact]
    public void CanvasNet_SystemIntegration_BuildFlattenAndBoundPath_ReturnsExpectedBounds()
    {
        // Arrange: build a path with a move, a cubic Bezier, an SVG-style arc, and a close
        var builder = new PathBuilder();
        var path = builder
            .MoveTo(new Vector2(0, 0))
            .CubicBezierTo(new Vector2(0, 50), new Vector2(50, 50), new Vector2(50, 0))
            .ArcTo(new Vector2(25, 25), 0, largeArc: false, sweep: true, new Vector2(0, 0))
            .Close()
            .Build();

        // Act: flatten the cubic segment directly through the public API, and compute the path's
        // conservative bounding rectangle
        var flattened = new List<Vector2>();
        BezierFlattening.FlattenCubic(
            new Vector2(0, 0),
            new Vector2(0, 50),
            new Vector2(50, 50),
            new Vector2(50, 0),
            0.5f,
            flattened);
        var bounds = path.GetBounds();

        // Assert: the system produces one closed subpath, a non-empty flattened polyline ending
        // at the curve's true end point, and a bounding rectangle enclosing every command
        var subpath = Assert.Single(path.Subpaths);
        Assert.True(subpath.IsClosed);
        Assert.NotEmpty(flattened);
        Assert.Equal(new Vector2(50, 0), flattened[^1]);
        Assert.False(bounds.IsEmpty);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }
}
