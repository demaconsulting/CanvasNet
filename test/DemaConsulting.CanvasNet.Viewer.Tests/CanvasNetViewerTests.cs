// cspell:ignore href scad pdf embedder
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Unit tests for <see cref="CanvasNetViewer"/>: the list of sources and extensions, the creation of resolvers, and
///     rendering a file by path or from a stream with its sizes, notes, text, embedded images, failures and options.
/// </summary>
public class CanvasNetViewerTests
{
    /// <summary>
    ///     Test that the list of sources is fixed and read-only, and that the extensions are the sorted union of what the
    ///     sources claim.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_ImageSources_AreFixedAndExtensionsAreSortedUnion()
    {
        // Arrange / Act
        var sources = CanvasNetViewer.ImageSources;
        var extensions = CanvasNetViewer.Extensions;

        // Assert
        Assert.Equal(7, sources.Count);
        Assert.Same(sources, CanvasNetViewer.ImageSources);
        Assert.False(sources is IList<IImageSource> { IsReadOnly: false });
        Assert.Equal(extensions.Order(StringComparer.Ordinal), extensions);
        Assert.Equal(extensions.Distinct(), extensions);
        Assert.Contains(".vl.json", extensions);
        Assert.DoesNotContain(".json", extensions);
        Assert.Equal(new ImageSourceSet(sources).Extensions, extensions);
    }

    /// <summary>
    ///     Test that the extension and scheme lists of every built-in source cannot be changed by casting them to an
    ///     array or <see cref="IList{T}"/>.
    /// </summary>
    /// <param name="index">The position of the built-in source.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void CanvasNetViewer_ImageSources_ExtensionsAndSchemesAreReadOnly(int index)
    {
        // Arrange
        var source = CanvasNetViewer.ImageSources[index];

        // Act / Assert
        AssertReadOnly(source.Extensions, ".x");
        AssertReadOnly(source.Schemes, "x");
    }

    /// <summary>
    ///     Test that the lists of the viewer, of a resolver it creates and of a rendered image cannot be changed by
    ///     casting them to an array, a list or <see cref="IList{T}"/>.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Collections_AreReadOnly()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var path = folder.WriteText("a.svg", ViewerFixtures.Svg(string.Empty));
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act
        using var image = CanvasNetViewer.Render(path);

        // Assert
        AssertReadOnly(CanvasNetViewer.ImageSources, new RasterImageSource());
        AssertReadOnly(CanvasNetViewer.Extensions, ".x");
        AssertReadOnly(resolver.Extensions, ".x");
        AssertReadOnly(resolver.Schemes, "x");
        AssertReadOnly(image.Diagnostics, new ImageSourceDiagnostic("s", ImageDiagnosticSeverity.Info, "c", "m"));
        AssertReadOnly(image.Echo, "x");
        AssertReadOnly(image.Embedded, new EmbeddedImage("a", "b", 1, 1));
    }

    /// <summary>
    ///     Asserts that a list is not an array or a <see cref="List{T}"/>, and that when it can be cast to
    ///     <see cref="IList{T}"/> it reports read-only and refuses to add.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="sample">An element to try to add.</param>
    private static void AssertReadOnly<T>(IReadOnlyList<T> list, T sample)
    {
        Assert.False(list is T[]);
        Assert.False(list is List<T>);
        if (list is IList<T> mutable)
        {
            Assert.True(mutable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => mutable.Add(sample));
        }
    }

    /// <summary>
    ///     Test that every call creates a new resolver with the built-in extensions, that extra sources are added after
    ///     the built-in ones, and that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_CreateImageResolver_CreatesNewResolverWithBuiltInAndExtraSources()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var extra = ViewerFixtures.ColorText("mine", ".colors");
        var live = new LiveFrameSource();

        // Act
        var first = CanvasNetViewer.CreateImageResolver(folder.Path);
        var second = CanvasNetViewer.CreateImageResolver(folder.Path);
        var extended = CanvasNetViewer.CreateImageResolver(folder.Path, [extra, live], new FolderImageResolverOptions { MaxImages = 3 });

        // Assert
        Assert.NotSame(first, second);
        Assert.Equal(CanvasNetViewer.Extensions, first.Extensions);
        Assert.Empty(first.Schemes);
        Assert.Contains(".colors", extended.Extensions);
        Assert.Equal(["test-scheme"], extended.Schemes);
        Assert.Throws<ArgumentException>(() => CanvasNetViewer.CreateImageResolver(string.Empty));
        Assert.Throws<ArgumentNullException>(() => CanvasNetViewer.CreateImageResolver(folder.Path, (IEnumerable<IImageSource>)null!));
        Assert.ThrowsAny<ArgumentException>(() => CanvasNetViewer.CreateImageResolver(null!, []));
    }

    /// <summary>
    ///     Test that rendering a file by path returns it at its natural size with its format, and that disposing the
    ///     result disposes the surface.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PathNatural_ReturnsNaturalSizeAndFormat()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var path = folder.WriteBytes("logo.png", ViewerFixtures.Png(12, 8, ViewerFixtures.Green));

        // Act
        var image = CanvasNetViewer.Render(path);
        var surface = image.Surface;

        // Assert
        Assert.Equal((12, 8), (surface.Width, surface.Height));
        Assert.Equal("png", image.Format);
        Assert.Equal(ViewerFixtures.Green, surface[0, 0]);
        Assert.Empty(image.Diagnostics);
        Assert.Empty(image.Echo);
        Assert.Empty(image.Embedded);
        image.Dispose();
        Assert.Throws<ObjectDisposedException>(() => surface[0, 0]);
    }

    /// <summary>
    ///     Test that <see cref="ViewerRenderOptions.MaxDecodedPixels"/> reaches the resolver of a direct render: a null value
    ///     keeps the default (a small picture renders), a smaller cap refuses a picture over it, and a cap that fits it
    ///     lets it render.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_MaxDecodedPixels_IsPassedToTheResolver()
    {
        // Arrange: a 12 by 8 picture is 96 pixels
        using var folder = new ViewerTestFolder();
        var path = folder.WriteBytes("logo.png", ViewerFixtures.Png(12, 8, ViewerFixtures.Green));

        // Act
        using var byDefault = CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = null });
        using var fits = CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = 96 });

        // Assert
        Assert.Equal((12, 8), (byDefault.Surface.Width, byDefault.Surface.Height));
        Assert.Equal((12, 8), (fits.Surface.Width, fits.Surface.Height));
        Assert.Throws<ImageSourceBudgetException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = 95 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = -1 }));
    }

    /// <summary>
    ///     Test that a width or height that is not from 1 to <see cref="Surface.MaxDimension"/> is rejected with
    ///     <see cref="ArgumentOutOfRangeException"/> before anything is read, by both overloads: the file does not exist
    ///     and the stream is not touched, yet the exception is the one for the option.
    /// </summary>
    /// <param name="dimension">The option under test, <c>Width</c> or <c>Height</c>.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("Width", 0)]
    [InlineData("Width", -1)]
    [InlineData("Width", Surface.MaxDimension + 1)]
    [InlineData("Height", 0)]
    [InlineData("Height", -1)]
    [InlineData("Height", Surface.MaxDimension + 1)]
    public void CanvasNetViewer_Render_SizeOutOfRange_ThrowsBeforeReading(string dimension, int value)
    {
        // Arrange
        var options = dimension == "Width"
            ? new ViewerRenderOptions { Width = value }
            : new ViewerRenderOptions { Height = value };
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var byPath = Assert.Throws<ArgumentOutOfRangeException>(() => CanvasNetViewer.Render("does-not-exist.png", options));
        var byStream = Assert.Throws<ArgumentOutOfRangeException>(() => CanvasNetViewer.Render(stream, "x.png", options));

        // Assert: both name the option and the dimension, and the stream was not read
        Assert.Equal("options", byPath.ParamName);
        Assert.Equal("options", byStream.ParamName);
        Assert.Contains($"ViewerRenderOptions.{dimension}", byPath.Message, StringComparison.Ordinal);
        Assert.Contains($"ViewerRenderOptions.{dimension}", byStream.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.Position);
    }

    /// <summary>
    ///     Test that the ends of the valid range are accepted by the validation: a width and a height of 1 render, and
    ///     <see cref="Surface.MaxDimension"/> passes the validation (the render is then stopped by a small pixel budget, so
    ///     no picture of that size is allocated).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_SizeAtBoundaries_IsAccepted()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var path = folder.WriteBytes("logo.png", ViewerFixtures.Png(12, 8, ViewerFixtures.Green));

        // Act
        using var smallest = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 1, Height = 1 });
        using var stream = new MemoryStream(ViewerFixtures.Png(12, 8, ViewerFixtures.Green));
        using var byStream = CanvasNetViewer.Render(stream, "logo.png", new ViewerRenderOptions { Width = 1 });

        // Assert: past the validation the render fails for the budget, not for the argument
        Assert.Equal((1, 1), (smallest.Surface.Width, smallest.Surface.Height));
        Assert.Equal((1, 1), (byStream.Surface.Width, byStream.Surface.Height));
        Assert.Throws<ImageSourceBudgetException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = Surface.MaxDimension, MaxDecodedPixels = 96 }));
        Assert.Throws<ImageSourceBudgetException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Height = Surface.MaxDimension, MaxDecodedPixels = 96 }));
    }

    /// <summary>
    ///     Test that the final buffer of a post-processed render is charged to
    ///     <see cref="ViewerRenderOptions.MaxDecodedPixels"/> in addition to the surface of the source: a 10 by 10
    ///     picture (100 pixels) resized to 1 by 1 needs 101 pixels, so a budget of 100 refuses it and 101 lets it render.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ResizedFinalBuffer_IsChargedToTheBudget()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var path = folder.WriteBytes("logo.png", ViewerFixtures.Png(10, 10, ViewerFixtures.Green));

        // Act
        var refused = Assert.Throws<ImageSourceBudgetException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 1, MaxDecodedPixels = 100 }));
        var refusedBoxed = Assert.Throws<ImageSourceBudgetException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 1, Height = 1, MaxDecodedPixels = 100 }));
        using var fits = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 1, MaxDecodedPixels = 101 });
        using var natural = CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = 100 });

        // Assert: nothing is left of the 100 pixels the source was charged, so one more pixel is refused
        Assert.Equal((1L, 0L), (refused.RequestedPixels, refused.RemainingPixels));
        Assert.Equal(1L, refusedBoxed.RequestedPixels);
        Assert.Equal((1, 1), (fits.Surface.Width, fits.Surface.Height));
        Assert.Equal((10, 10), (natural.Surface.Width, natural.Surface.Height));
    }
    /// <summary>
    ///     Test that a vector file is drawn at the requested size, that a raster is fitted into the requested box (and
    ///     scaled for one requested dimension), so the caller gets the size it asked for.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PathSized_ReturnsRequestedSize()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var raster = folder.WriteBytes("logo.png", ViewerFixtures.Png(12, 8, ViewerFixtures.Green));
        var model = folder.WriteText("part.scad", ViewerFixtures.Cube);

        // Act
        using var vector = CanvasNetViewer.Render(model, new ViewerRenderOptions { Width = 60, Height = 40 });
        using var boxed = CanvasNetViewer.Render(raster, new ViewerRenderOptions { Width = 30, Height = 30 });
        using var widthOnly = CanvasNetViewer.Render(raster, new ViewerRenderOptions { Width = 24 });
        using var heightOnly = CanvasNetViewer.Render(raster, new ViewerRenderOptions { Height = 16 });

        // Assert: the raster keeps its aspect ratio inside the box, centered on a transparent background
        Assert.Equal((60, 40), (vector.Surface.Width, vector.Surface.Height));
        Assert.Equal((30, 30), (boxed.Surface.Width, boxed.Surface.Height));
        Assert.Equal(0, boxed.Surface[15, 2].A);
        Assert.Equal(ViewerFixtures.Green, boxed.Surface[15, 15]);
        Assert.Equal((24, 16), (widthOnly.Surface.Width, widthOnly.Surface.Height));
        Assert.Equal((24, 16), (heightOnly.Surface.Width, heightOnly.Surface.Height));
    }

    /// <summary>
    ///     Test that rendering an SVG file by path embeds its pictures from the folder of the file, and surfaces the
    ///     warnings, the text printed by models and the embedded images of the whole render.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_SvgWithEmbeds_SurfacesDiagnosticsEchoAndEmbedded()
    {
        // Arrange: a model that prints a line and uses an unsupported feature, a missing picture, and a picture
        using var folder = new ViewerTestFolder();
        folder.WriteText("part.scad", "echo(\"from-the-model\");\noffset(r = 1) square(2);\ncube(5);\n");
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var path = folder.WriteText(
            "diagram.svg",
            ViewerFixtures.Svg(ViewerFixtures.Cell("part.scad", 0) + ViewerFixtures.Cell("logo.png", 1) + ViewerFixtures.Cell("missing.png", 2)));

        // Act
        using var image = CanvasNetViewer.Render(path);

        // Assert
        Assert.Equal("svg", image.Format);
        Assert.Equal((500, 100), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(ViewerFixtures.Green, image.Surface[150, 50]);
        Assert.Contains(image.Echo, line => line.Contains("from-the-model", StringComparison.Ordinal));
        Assert.Contains(image.Diagnostics, d => d is { Source: "scad", Severity: ImageDiagnosticSeverity.Unsupported } && d.Message.StartsWith("part.scad: ", StringComparison.Ordinal));
        Assert.Contains(image.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode && d.Message.Contains("'missing.png'", StringComparison.Ordinal));
        Assert.Equal(["scad", "png"], image.Embedded.Select(e => e.Format));
    }

    /// <summary>
    ///     Test that a file rendered by path may not embed a file outside its folder, and that an SVG file that embeds
    ///     itself stops at once.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_SvgEscapingAndSelfEmbedding_AreWarnings()
    {
        // Arrange: a picture above the folder of the document, and a document that links it and itself
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("secret.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var path = folder.WriteText(
            "site/self.svg",
            ViewerFixtures.Svg(ViewerFixtures.Cell("../secret.png", 0) + ViewerFixtures.Cell("self.svg", 1)));

        // Act
        using var image = CanvasNetViewer.Render(path);

        // Assert
        Assert.Equal(ViewerFixtures.Gray, image.Surface[50, 50]);
        Assert.Equal(2, image.Diagnostics.Count(d => d.Code == FolderImageResolver.UnresolvedCode));
        Assert.Contains(image.Diagnostics, d => d.Message.Contains("cycle", StringComparison.Ordinal));
        Assert.Empty(image.Embedded);
    }

    /// <summary>
    ///     Test that a render by path fails for a missing file, an unknown extension, a corrupt file (with the original
    ///     failure inside), a file outside the folder option, and a null path.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PathFailures_ThrowSpecificExceptions()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        folder.WriteText("notes.txt", "x");
        var bad = folder.WriteBytes("bad.png", [1, 2, 3]);
        var inner = folder.WriteBytes("sub/inner.png", ViewerFixtures.Png(2, 2, ViewerFixtures.Green));

        // Act / Assert
        Assert.Throws<FileNotFoundException>(() => CanvasNetViewer.Render(folder.Combine("missing.png")));
        var unsupported = Assert.Throws<NotSupportedException>(() => CanvasNetViewer.Render(folder.Combine("notes.txt")));
        Assert.Contains(".svg", unsupported.Message, StringComparison.Ordinal);
        var corrupt = Assert.Throws<ImageSourceException>(() => CanvasNetViewer.Render(bad));
        Assert.IsType<InvalidDataException>(corrupt.InnerException);
        Assert.Null(corrupt.Embedder);
        Assert.Throws<ArgumentException>(() => CanvasNetViewer.Render(inner, new ViewerRenderOptions { Folder = folder.Combine("other") }));
        Assert.Throws<ArgumentNullException>(() => CanvasNetViewer.Render((string)null!));
        using var withFolder = CanvasNetViewer.Render(inner, new ViewerRenderOptions { Folder = folder.Path });
        Assert.Equal((2, 2), (withFolder.Surface.Width, withFolder.Surface.Height));
    }

    /// <summary>
    ///     Test that the strict option makes an unsupported feature a failure, and that without it the file is drawn with
    ///     a note.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ThrowOnUnsupportedFeature_FailsInsteadOfNote()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var path = folder.WriteText("part.scad", "offset(r = 1) square(2);\n");

        // Act
        using var lenient = CanvasNetViewer.Render(path);
        var strict = Assert.Throws<ImageSourceException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true }));

        // Assert
        Assert.Contains(lenient.Diagnostics, d => d.Severity == ImageDiagnosticSeverity.Unsupported);
        Assert.Equal("scad", lenient.Format);
        Assert.NotNull(strict.InnerException);
    }

    /// <summary>
    ///     Test that a stream is rendered by its file name, with its includes resolved only from the folder option.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_Stream_UsesFileNameAndOptionalFolder()
    {
        // Arrange: a model that includes a library, and the library in a folder
        using var folder = new ViewerTestFolder();
        folder.WriteText("lib.scad", "module part() { cube(8, center = true); }");
        var model = System.Text.Encoding.UTF8.GetBytes("include <lib.scad>\npart();\n");

        // Act
        using var withFolder = CanvasNetViewer.Render(new MemoryStream(model), "virtual/part.scad", new ViewerRenderOptions { Folder = folder.Path, Width = 40, Height = 40 });
        using var png = CanvasNetViewer.Render(new MemoryStream(ViewerFixtures.Png(5, 3, ViewerFixtures.Green)), "x.PNG");

        // Assert: the include was found in the folder; a raster needs no folder
        Assert.Equal("scad", withFolder.Format);
        Assert.Equal((40, 40), (withFolder.Surface.Width, withFolder.Surface.Height));
        Assert.DoesNotContain(withFolder.Diagnostics, d => d.Message.Contains("lib.scad", StringComparison.Ordinal));
        Assert.Equal(("png", 5, 3), (png.Format, png.Surface.Width, png.Surface.Height));
    }

    /// <summary>
    ///     Test that a stream with an unknown extension and null arguments are rejected.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StreamInvalidArguments_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<NotSupportedException>(() => CanvasNetViewer.Render(new MemoryStream(), "x.unknown"));
        Assert.Throws<ArgumentNullException>(() => CanvasNetViewer.Render((Stream)null!, "x.png"));
        Assert.Throws<ArgumentNullException>(() => CanvasNetViewer.Render(new MemoryStream(), null!));
    }

    /// <summary>
    ///     Test that a canceled token stops a render before anything is drawn.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var path = folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test that an unreadable path outside the sandbox through a symbolic link is refused as a sandbox failure when
    ///     rendered directly.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PathThroughSymbolicLink_ThrowsSandboxException()
    {
        // Arrange: a link to a picture next to it (links may need privileges on Windows)
        using var folder = new ViewerTestFolder();
        var real = folder.WriteBytes("real.png", ViewerFixtures.Png(2, 2, ViewerFixtures.Green));
        var link = folder.Combine("link.png");
        try
        {
            File.CreateSymbolicLink(link, real);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links cannot be created here.");
            return;
        }

        // Act / Assert
        Assert.Throws<SandboxException>(() => CanvasNetViewer.Render(link));
    }
}
