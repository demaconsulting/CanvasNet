// cspell:ignore scad
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the input of <see cref="SourceLoader"/>: a source in memory loads exactly like the same file, the
///     hooks of an embedding resolver (the pixel budget, cancellation, the resolver of nested pictures) and the page
///     fitting of an embedded document.
/// </summary>
public class SourceLoaderInputTests
{
    /// <summary>
    ///     Gets the keys of every kind of source the fixture factory writes.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string> AllKeys()
    {
        return [.. FixtureFactory.Sources.Select(s => s.Key)];
    }

    /// <summary>
    ///     A resolver that records the links it is asked for and draws a small green picture for each.
    /// </summary>
    private sealed class RecordingResolver : IImageResolver
    {
        /// <summary>
        ///     Gets the links that were resolved, in order.
        /// </summary>
        public List<string> Links { get; } = [];

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            Links.Add(href);
            return StubImage();
        }

        /// <summary>
        ///     Creates the picture.
        /// </summary>
        /// <returns>A new 4 by 4 green surface.</returns>
        private static Surface StubImage()
        {
            var surface = new Surface(4, 4);
            surface.Clear(ViewerFixtures.Green);
            return surface;
        }
    }

    /// <summary>
    ///     Test that a source in memory is probed and loaded exactly as the same file: the facts, the pixels and the
    ///     diagnostics are the same.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [MemberData(nameof(AllKeys))]
    public void SourceLoader_InputFromMemory_LoadsLikeTheFile(string key)
    {
        // Arrange: a file, and the same bytes in memory under the same name and folder
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);
        var options = new SourceOptions { Scad = new ScadViewOptions { View = "front", ViewGiven = true } };
        using var fromFile = SourceLoader.Load(path, options);
        var memory = SourceLoader.SourceInput.FromMemory(File.ReadAllBytes(path), path, directory.Path);

        // Act
        var info = SourceLoader.ProbeInput(memory, SourceLoader.Probe(path).Format, FileAccessPolicy.AllowAll);
        using var pages = SourceLoader.OpenInputPages(memory, info, options, SourceLoader.LoadContext.Default);
        using var fromMemory = pages.ReadPage(1);

        // Assert
        Assert.Equal(fromFile.Info.Format, info.Format);
        Assert.Equal(fromFile.Info.Bytes, info.Bytes);
        Assert.Equal((fromFile.Info.Width, fromFile.Info.Height, fromFile.Info.PageCount), (info.Width, info.Height, info.PageCount));
        SurfaceAssert.Equal(fromFile.Surface, fromMemory.Surface);
        Assert.Equal(fromFile.Diagnostics, fromMemory.Diagnostics);
        Assert.Equal(fromFile.Echo, fromMemory.Echo);
    }

    /// <summary>
    ///     Test that the charge hook is called once, with the size of the surface that is allocated, for a page of a
    ///     document and for a raster.
    /// </summary>
    [Fact]
    public void SourceLoader_LoadContext_Charge_IsCalledOnceWithTheSizeOfTheSurface()
    {
        // Arrange
        var charges = new List<(int Width, int Height)>();
        var context = new SourceLoader.LoadContext { Charge = (w, h) => charges.Add((w, h)) };
        var pdf = SourceLoader.SourceInput.FromMemory(FixtureFactory.CreatePdf(), "a.pdf", null);
        var png = SourceLoader.SourceInput.FromMemory(ViewerFixtures.Png(7, 5, ViewerFixtures.Green), "a.png", null);

        // Act
        using (var pdfPages = SourceLoader.OpenInputPages(pdf, SourceLoader.ProbeInput(pdf, SourceKind.Pdf, FileAccessPolicy.AllowAll), new SourceOptions { Width = 100, Height = 100 }, context))
        {
            pdfPages.ReadPage(1).Dispose();
        }

        using (var pngPages = SourceLoader.OpenInputPages(png, SourceLoader.ProbeInput(png, SourceKind.Png, FileAccessPolicy.AllowAll), new SourceOptions(), context))
        {
            pngPages.ReadPage(1).Dispose();
        }

        // Assert: the PDF page is charged at the size it is drawn at (fitted inside the box), the raster at its declared size
        Assert.Equal((100, 67), charges[0]);
        Assert.Equal((7, 5), charges[^1]);
    }

    /// <summary>
    ///     Test that a charge that is refused stops the load before anything is decoded: a header that declares a huge
    ///     picture and has no pixel data is never decoded.
    /// </summary>
    [Fact]
    public void SourceLoader_LoadContext_ChargeRefused_StopsBeforeDecoding()
    {
        // Arrange: a PNG of 8192 by 8192 that is only a header, and a hook that refuses everything
        var input = SourceLoader.SourceInput.FromMemory(ViewerFixtures.PngDeclaring(Surface.MaxDimension, Surface.MaxDimension), "huge.png", null);
        var context = new SourceLoader.LoadContext { Charge = (w, h) => throw new ImageSourceBudgetException((long)w * h, 0) };
        var info = SourceLoader.ProbeInput(input, SourceKind.Png, FileAccessPolicy.AllowAll);
        using var pages = SourceLoader.OpenInputPages(input, info, new SourceOptions(), context);

        // Act / Assert: the refusal, not an error of decoding the missing pixels or of the size limit
        var exception = Assert.Throws<ImageSourceBudgetException>(() => pages.ReadPage(1));
        Assert.Equal((long)Surface.MaxDimension * Surface.MaxDimension, exception.RequestedPixels);
    }

    /// <summary>
    ///     Test that a canceled token stops the load of a page.
    /// </summary>
    [Fact]
    public void SourceLoader_LoadContext_CanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var input = SourceLoader.SourceInput.FromMemory(ViewerFixtures.Png(4, 4, ViewerFixtures.Green), "a.png", null);
        var info = SourceLoader.ProbeInput(input, SourceKind.Png, FileAccessPolicy.AllowAll);
        using var pages = SourceLoader.OpenInputPages(input, info, new SourceOptions(), new SourceLoader.LoadContext { CancellationToken = cts.Token });

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => pages.ReadPage(1));
    }

    /// <summary>
    ///     Test that a resolver supplied by the caller loads the pictures an SVG source links to, and that the loader then
    ///     adds no note and no embed operation of its own.
    /// </summary>
    [Fact]
    public void SourceLoader_LoadContext_ScopedResolver_LoadsNestedPicturesAndAddsNoNotes()
    {
        // Arrange
        var svg = "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><image href='a.png' x='0' y='0' width='10' height='10'/></svg>";
        var input = SourceLoader.SourceInput.FromMemory(System.Text.Encoding.UTF8.GetBytes(svg), "outer.svg", null);
        var resolver = new RecordingResolver();
        var context = new SourceLoader.LoadContext { UseScopedResolver = true, ScopedResolver = resolver };
        var info = SourceLoader.ProbeInput(input, SourceKind.Svg, FileAccessPolicy.AllowAll);

        // Act
        using var pages = SourceLoader.OpenInputPages(input, info, new SourceOptions { Background = new Rgba32(0, 0, 0, 0) }, context);
        using var loaded = pages.ReadPage(1);

        // Assert
        Assert.Equal(["a.png"], resolver.Links);
        Assert.Equal(ViewerFixtures.Green, loaded.Surface[5, 5]);
        Assert.Empty(loaded.Diagnostics);
        Assert.DoesNotContain(loaded.Operations, o => o.Op == "embed");
        Assert.Equal(0, loaded.Surface[15, 15].A);
    }

    /// <summary>
    ///     Test that a caller that supplies no resolver at all gets an SVG whose links are not resolved, and not a
    ///     resolver of the loader's own.
    /// </summary>
    [Fact]
    public void SourceLoader_LoadContext_ScopedResolverNull_LeavesLinksUnresolved()
    {
        // Arrange: a link to a file that would resolve beside the SVG if the loader made its own resolver
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("a.png"), ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var svg = "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><image href='a.png' x='0' y='0' width='10' height='10'/></svg>";
        var input = SourceLoader.SourceInput.FromMemory(System.Text.Encoding.UTF8.GetBytes(svg), "outer.svg", directory.Path);
        var info = SourceLoader.ProbeInput(input, SourceKind.Svg, FileAccessPolicy.AllowAll);

        // Act
        using var pages = SourceLoader.OpenInputPages(input, info, new SourceOptions { Background = new Rgba32(0, 0, 0, 0) }, new SourceLoader.LoadContext { UseScopedResolver = true });
        using var loaded = pages.ReadPage(1);

        // Assert
        Assert.Equal(0, loaded.Surface[5, 5].A);
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that a document page is returned at the size it was drawn at when the context says so, and centered in
    ///     a box of the requested size otherwise.
    /// </summary>
    [Fact]
    public void SourceLoader_LoadContext_ReturnFittedPage_SkipsTheMargin()
    {
        // Arrange
        var input = SourceLoader.SourceInput.FromMemory(FixtureFactory.CreatePdf(), "a.pdf", null);
        var info = SourceLoader.ProbeInput(input, SourceKind.Pdf, FileAccessPolicy.AllowAll);
        var options = new SourceOptions { Width = 100, Height = 100 };

        // Act
        using var fittedPages = SourceLoader.OpenInputPages(input, info, options, new SourceLoader.LoadContext { ReturnFittedPage = true });
        using var fitted = fittedPages.ReadPage(1);
        using var boxedPages = SourceLoader.OpenInputPages(input, info, options, SourceLoader.LoadContext.Default);
        using var boxed = boxedPages.ReadPage(1);

        // Assert
        Assert.Equal((100, 67), (fitted.Surface.Width, fitted.Surface.Height));
        Assert.Equal((100, 100), (boxed.Surface.Width, boxed.Surface.Height));
    }

    /// <summary>
    ///     Test the identity of an input: the name and folder of a file, and of memory, and that a file opens as a stream.
    /// </summary>
    [Fact]
    public void SourceLoader_SourceInput_FileAndMemory_ExposeNameFolderLengthAndStream()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "a.bin");
        File.WriteAllBytes(path, [1, 2, 3]);

        // Act
        var file = SourceLoader.SourceInput.FromFile(path);
        var memory = SourceLoader.SourceInput.FromMemory(new byte[] { 4, 5 }, "m.bin", "folder");
        using var fileStream = file.Open();
        using var memoryStream = memory.Open();

        // Assert
        Assert.Equal(path, file.Name);
        Assert.Equal(directory.Path, file.Directory);
        Assert.Equal(3, file.Length);
        Assert.Equal(3, fileStream.Length);
        Assert.Equal("m.bin", memory.Name);
        Assert.Equal("folder", memory.Directory);
        Assert.Equal(2, memory.Length);
        Assert.Equal(2, memoryStream.Length);
        Assert.False(memoryStream.CanWrite);
        Assert.True(memoryStream.CanSeek);
    }

    /// <summary>
    ///     Test that the facts of a probe refuse a null file-access policy, so malformed facts fail at once rather than
    ///     with a null reference when an operation checks the probe policy.
    /// </summary>
    [Fact]
    public void SourceInfo_FileAccess_Null_ThrowsArgumentNullException()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "a.png");
        FixtureFactory.WriteDeclaredPng(path, 4, 4);
        var info = SourceLoader.Probe(path);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => info with { FileAccess = null! });
    }
}
