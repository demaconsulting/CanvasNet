// cspell:ignore href hrefs
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for the natural-size fact that a source reports (<see cref="ImageSourceResult.IsNaturalSize"/>) and that
///     <see cref="FolderImageResolver"/> and the resolver it gives to embedded files carry to a renderer
///     (<see cref="IImageNaturalSizeResolver"/>): a raster is at its own size, a picture drawn for the request is not, and
///     the size of the surface is never used to tell the two apart.
/// </summary>
public class FolderImageResolverNaturalSizeTests
{
    /// <summary>The extension the stubs claim.</summary>
    private const string Ext = ".stub";

    /// <summary>
    ///     Encodes a solid picture as a PNG file.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bytes.</returns>
    private static byte[] SolidPng(int width, int height)
    {
        using var surface = StubImageSource.Solid(width, height, StubImageSource.Green);
        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        return stream.ToArray();
    }

    /// <summary>
    ///     Test that the raster source says its picture is at its own size, and a result says nothing by default.
    /// </summary>
    [Fact]
    public void ImageSourceResult_IsNaturalSize_IsSetByTheRasterSourceAndFalseByDefault()
    {
        // Arrange / Act
        var raster = new RasterImageSource().Render(new ImageSourceRequest { Href = "a.png", FileName = "a.png", Content = SolidPng(5, 3) });
        using var rasterSurface = raster.Surface;
        using var plain = StubImageSource.Solid(2, 2, StubImageSource.Green);

        // Assert
        Assert.True(raster.IsNaturalSize);
        Assert.False(new ImageSourceResult(plain).IsNaturalSize);
    }

    /// <summary>
    ///     Test that the resolver reports a raster as natural size even when its pixel size equals the requested size, and a
    ///     source that draws for the request as not natural size, whatever size it returns.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ReportsTheKindOfThePictureNotItsSize()
    {
        // Arrange: a 40 by 40 PNG asked for at exactly 40 by 40, and a stub that draws for the request
        using var folder = new ImageTestFolder();
        folder.WriteBytes("same.png", SolidPng(40, 40));
        folder.WriteText("drawn" + Ext, "x");
        var stub = new StubImageSource("stub", [Ext], null, request => new ImageSourceResult(StubImageSource.Solid(request.PixelWidth ?? 7, request.PixelHeight ?? 7, StubImageSource.Blue)));
        var resolver = new FolderImageResolver(folder.Path, [new RasterImageSource(), stub]);

        // Act
        using var raster = resolver.Resolve("same.png", 40, 40, out var rasterNatural);
        using var drawn = resolver.Resolve("drawn" + Ext, 40, 40, out var drawnNatural);
        using var missing = resolver.Resolve("missing.png", 40, 40, out var missingNatural);

        // Assert: the raster has the request's size and is still natural; the drawn picture is not; nothing is not
        Assert.NotNull(raster);
        Assert.Equal((40, 40), (raster.Width, raster.Height));
        Assert.True(rasterNatural);
        Assert.NotNull(drawn);
        Assert.Equal((40, 40), (drawn.Width, drawn.Height));
        Assert.False(drawnNatural);
        Assert.Null(missing);
        Assert.False(missingNatural);
    }

    /// <summary>
    ///     Test that the resolver that an embedded file receives reports the same fact, so a chart that is embedded in another
    ///     document sees it too, and that the flag of the plain overload does not leak into the next call.
    /// </summary>
    [Fact]
    public void FolderImageResolver_ScopedResolver_ReportsTheKindOfThePicture()
    {
        // Arrange: a stub file that, while it is drawn, asks its own resolver for a PNG and for a picture drawn for the request
        using var folder = new ImageTestFolder();
        folder.WriteBytes("inner.png", SolidPng(30, 30));
        folder.WriteText("inner" + Ext, "x");
        folder.WriteText("outer" + Ext, "x");
        var seen = new List<(string Href, bool Natural)>();
        var inner = new StubImageSource("inner", [".inner"], null, request => new ImageSourceResult(StubImageSource.Solid(request.PixelWidth ?? 7, request.PixelHeight ?? 7, StubImageSource.Blue)));
        var outer = new StubImageSource("outer", [Ext], null, request =>
        {
            var scoped = Assert.IsAssignableFrom<IImageNaturalSizeResolver>(request.Resolver);
            using var png = scoped.Resolve("inner.png", 30, 30, out var pngNatural);
            seen.Add(("inner.png", pngNatural));
            using var drawn = scoped.Resolve("part.inner", 30, 30, out var drawnNatural);
            seen.Add(("part.inner", drawnNatural));
            return new ImageSourceResult(StubImageSource.Solid(4, 4, StubImageSource.Green));
        });
        folder.WriteText("part.inner", "x");
        var resolver = new FolderImageResolver(folder.Path, [new RasterImageSource(), inner, outer]);

        // Act: the outer file is drawn by a call of the resolver itself, which reports the outer picture as not natural
        using var surface = resolver.Resolve("outer" + Ext, 4, 4, out var outerNatural);

        // Assert: the embedded PNG at its own size is natural, the embedded drawn picture is not, and the outer one is not
        Assert.NotNull(surface);
        Assert.Equal([("inner.png", true), ("part.inner", false)], seen);
        Assert.False(outerNatural);
    }
}
