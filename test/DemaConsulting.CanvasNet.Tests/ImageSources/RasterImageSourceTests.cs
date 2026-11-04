// cspell:ignore jpeg tiff jpeg2000 exif
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for <see cref="RasterImageSource"/>: the content (not the name) decides the format, a JPEG is turned
///     upright, the natural size is returned, and bad input fails with the codec's exception.
/// </summary>
public class RasterImageSourceTests
{
    /// <summary>
    ///     Creates a request that carries the bytes of a file.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(string name, byte[] bytes)
    {
        return new ImageSourceRequest { Href = name, FileName = name, Content = bytes };
    }

    /// <summary>
    ///     Encodes a surface with one of the codecs that can save.
    /// </summary>
    /// <param name="format">The format: png, jpeg, bmp or tiff.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Encode(string format, Surface surface)
    {
        using var stream = new MemoryStream();
        switch (format)
        {
            case "png":
                PngCodec.Save(surface, stream);
                break;
            case "jpeg":
                JpegCodec.Save(surface, stream);
                break;
            case "bmp":
                BmpCodec.Save(surface, stream);
                break;
            default:
                TiffCodec.Save(surface, stream);
                break;
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Test that every raster format is recognized by its content, whatever the file name says, and that the natural
    ///     size and the format name are returned.
    /// </summary>
    /// <param name="format">The format that is encoded.</param>
    /// <param name="misleadingName">A file name with another extension.</param>
    [Theory]
    [InlineData("png", "picture.jpg")]
    [InlineData("jpeg", "picture.PNG")]
    [InlineData("bmp", "picture.tif")]
    [InlineData("tiff", "picture.bmp")]
    public void RasterImageSource_Render_Format_IsDecidedByContentNotName(string format, string misleadingName)
    {
        // Arrange: a 12 by 8 picture encoded in the format
        using var original = ImageFixtures.CreateGradient();
        var source = new RasterImageSource();

        // Act
        var result = source.Render(Request(misleadingName, Encode(format, original)));
        using var surface = result.Surface;

        // Assert
        Assert.Equal((12, 8), (surface.Width, surface.Height));
        Assert.Equal(format, result.Format);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Echo);
    }

    /// <summary>
    ///     Test that a GIF and a JPEG 2000 file from the fixtures are decoded and named.
    /// </summary>
    /// <param name="fixture">The path of the fixture relative to the test output.</param>
    /// <param name="format">The expected format name.</param>
    [Theory]
    [InlineData("GifFixtures/sample-green-200x200.gif", "gif")]
    [InlineData("Jpeg2000Fixtures/alpha.jp2", "jpeg2000")]
    public void RasterImageSource_Render_GifAndJpeg2000Fixtures_AreDecoded(string fixture, string format)
    {
        // Arrange
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, fixture));

        // Act
        var result = new RasterImageSource().Render(Request("x.bin", bytes));
        using var surface = result.Surface;

        // Assert
        Assert.Equal(format, result.Format);
        Assert.True(surface.Width > 0 && surface.Height > 0);
    }

    /// <summary>
    ///     Test that a JPEG with an EXIF orientation of 6 (rotated a quarter turn) is returned upright, with the sides
    ///     swapped, and that an upright JPEG is returned as it is.
    /// </summary>
    [Fact]
    public void RasterImageSource_Render_JpegWithExifOrientation_IsTurnedUpright()
    {
        // Arrange: a 12 by 8 JPEG stored with orientation 6, and the same without orientation
        using var original = ImageFixtures.CreateGradient();
        var plain = Encode("jpeg", original);
        var rotated = ImageFixtures.InsertExifOrientation(plain, 6, true);
        var source = new RasterImageSource();

        // Act
        using var upright = source.Render(Request("a.jpg", rotated)).Surface;
        using var untouched = source.Render(Request("a.jpg", plain)).Surface;

        // Assert
        Assert.Equal((8, 12), (upright.Width, upright.Height));
        Assert.Equal((12, 8), (untouched.Width, untouched.Height));
    }

    /// <summary>
    ///     Test that content that is not a raster and a truncated raster fail with <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void RasterImageSource_Render_CorruptOrUnknownContent_ThrowsInvalidDataException()
    {
        // Arrange: garbage, and a PNG signature without the rest
        var source = new RasterImageSource();
        byte[] truncated = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0];

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => source.Render(Request("a.png", ImageFixtures.CreateGarbage(64))));
        Assert.Throws<InvalidDataException>(() => source.Render(Request("a.png", [])));
        Assert.Throws<InvalidDataException>(() => source.Render(Request("a.png", truncated)));
    }

    /// <summary>
    ///     Test that a request without file content, a null request and a canceled request are rejected.
    /// </summary>
    [Fact]
    public void RasterImageSource_Render_InvalidRequests_Throw()
    {
        // Arrange
        var source = new RasterImageSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => source.Render(null!));
        Assert.Throws<InvalidOperationException>(() => source.Render(new ImageSourceRequest { Href = "camera:front" }));
        Assert.Throws<OperationCanceledException>(
            () => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.png", CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test that every raster format is checked against <see cref="ImageSourceRequest.RemainingPixels"/> from its
    ///     header: a picture that exactly fits is decoded and one pixel less is refused with
    ///     <see cref="ImageSourceBudgetException"/> carrying the declared size.
    /// </summary>
    /// <param name="fixture">The path of a fixture relative to the test output, or a format name to encode.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    [InlineData("GifFixtures/sample-green-200x200.gif")]
    [InlineData("Jpeg2000Fixtures/alpha.jp2")]
    public void RasterImageSource_Render_RemainingPixels_IsCheckedFromHeaderForEveryFormat(string fixture)
    {
        // Arrange: the bytes, and the size the picture really has
        byte[] bytes;
        if (fixture.Contains('/', StringComparison.Ordinal))
        {
            bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, fixture));
        }
        else
        {
            using var original = ImageFixtures.CreateGradient();
            bytes = Encode(fixture, original);
        }

        var source = new RasterImageSource();
        var size = source.Render(new ImageSourceRequest { Href = "a", FileName = "a.bin", Content = bytes });
        var pixels = (long)size.Surface.Width * size.Surface.Height;
        size.Surface.Dispose();

        // Act
        using var fits = source.Render(new ImageSourceRequest { Href = "a", FileName = "a.bin", Content = bytes, RemainingPixels = pixels }).Surface;
        var ex = Assert.Throws<ImageSourceBudgetException>(
            () => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.bin", Content = bytes, RemainingPixels = pixels - 1 }));

        // Assert
        Assert.Equal(pixels, (long)fits.Width * fits.Height);
        Assert.Equal((pixels, pixels - 1), (ex.RequestedPixels, ex.RemainingPixels));
    }

    /// <summary>
    ///     Test that a file which only declares a huge picture is refused from its header with
    ///     <see cref="ImageSourceBudgetException"/> and never decoded (decoding it would throw
    ///     <see cref="InvalidDataException"/>, and allocate the picture first), also when the header declares more
    ///     than the codecs decode.
    /// </summary>
    [Fact]
    public void RasterImageSource_Render_HeaderOnlyHugePicture_IsRefusedWithoutDecoding()
    {
        // Arrange: files that declare 8192 by 8192 and 30000 by 30000 pixels, and a budget of 1000
        var source = new RasterImageSource();
        var huge = PngHeaderFixtures.Declaring(Surface.MaxDimension, Surface.MaxDimension);
        var beyondCodecs = PngHeaderFixtures.Declaring(30000, 30000);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var first = Assert.Throws<ImageSourceBudgetException>(
            () => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.png", Content = huge, RemainingPixels = 1000 }));
        var second = Assert.Throws<ImageSourceBudgetException>(
            () => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.png", Content = beyondCodecs, RemainingPixels = 1000 }));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(67_108_864L, first.RequestedPixels);
        Assert.Equal(900_000_000L, second.RequestedPixels);
        Assert.True(allocated < 1024 * 1024, $"{allocated} bytes were allocated for two refusals.");
        Assert.Throws<InvalidDataException>(() => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.png", Content = huge[..20] }));
    }

    /// <summary>
    ///     Test the identity of the source: its name, the extensions it claims, and that it claims no scheme.
    /// </summary>
    [Fact]
    public void RasterImageSource_Properties_NameExtensionsAndNoSchemes()
    {
        // Arrange
        var source = new RasterImageSource();

        // Act / Assert
        Assert.Equal("raster", source.Name);
        Assert.Equal(
            [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".jp2", ".j2k", ".j2c"],
            source.Extensions);
        Assert.Empty(source.Schemes);
    }

    /// <summary>
    ///     Test that the shared extension and scheme lists cannot be changed by casting them to an array or
    ///     <see cref="IList{T}"/>.
    /// </summary>
    [Fact]
    public void RasterImageSource_Properties_ExtensionsAndSchemesAreReadOnly()
    {
        // Arrange
        var source = new RasterImageSource();

        // Act / Assert
        ReadOnlyListAssert.IsReadOnly(source.Extensions, ".x");
        ReadOnlyListAssert.IsReadOnly(source.Schemes, "x");
    }
}
