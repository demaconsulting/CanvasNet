using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Imaging;

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
        Assert.Equal(9, sources.Count);
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
    [InlineData(7)]
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
        Assert.Throws<ArgumentException>(() => CanvasNetViewer.CreateImageResolver("   "));
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
        var refused = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = 95 }));
        Assert.Equal(ErrorCodes.BudgetExceeded, refused.Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = -1 }));
    }

    /// <summary>
    ///     Test that a Mermaid diagram whose picture node without its <c>w</c> and <c>h</c> enlarges the layout is held to
    ///     <see cref="ViewerRenderOptions.MaxDecodedPixels"/> at the size it is drawn at, not at the smaller size probed
    ///     without the picture.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_MermaidPictureEnlargingTheLayout_IsChargedAtTheDrawnSize()
    {
        // Arrange: a 400 by 300 picture (120 000 pixels, loaded twice: once for its natural size, once to draw it) in a
        // node that takes its size, so the diagram is drawn at about 416 by 316 (about 131 000 pixels) while the probe
        // measures it at a few hundred pixels
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("big.png", ViewerFixtures.Png(400, 300, ViewerFixtures.Green));
        var path = folder.WriteText("big.mmd", "flowchart LR\n    p@{ img: \"big.png\" }\n");
        var probe = Viewer.Imaging.SourceLoader.Probe(path);

        // Act: 300 000 pays for both picture loads and the probed size but not the drawn diagram; 400 000 pays for all
        var refused = Assert.IsType<ImageSourceBudgetException>(
            Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = 300_000 })).InnerException);
        using var drawn = CanvasNetViewer.Render(path, new ViewerRenderOptions { MaxDecodedPixels = 400_000 });

        // Assert: the refusal is for the drawn pixels, and the diagram that fits is drawn larger than the probe measured
        Assert.Equal((long)drawn.Surface.Width * drawn.Surface.Height, refused.RequestedPixels);
        Assert.True(refused.RequestedPixels > refused.RemainingPixels);
        Assert.True(drawn.Surface.Width > probe.Width && drawn.Surface.Height > probe.Height);
        Assert.InRange(drawn.Surface.Width, 400, 440);
        Assert.InRange(drawn.Surface.Height, 300, 340);
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
        Assert.IsType<ImageSourceBudgetException>(Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = Surface.MaxDimension, MaxDecodedPixels = 96 })).InnerException);
        Assert.IsType<ImageSourceBudgetException>(Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Height = Surface.MaxDimension, MaxDecodedPixels = 96 })).InnerException);
    }

    /// <summary>
    ///     Test the chart of issue #165 through the stream entry point in strict mode: a field without a type is drawn, and the image carries one informational note
    ///     (<c>vega-lite-info</c>, detail <c>vl-type-inferred</c>, located at the channel) instead of no diagnostic at all.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ChartFieldWithoutType_ReportsInfoNoteInStrictMode()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{ "data": {"values":[{"a":1}]}, "mark": "bar", "encoding": { "x": { "field": "a" } } }"""));

        // Act
        using var image = CanvasNetViewer.Render(stream, "chart.vl.json", new ViewerRenderOptions { ThrowOnUnsupportedFeature = true });

        // Assert: the note names the field and the inferred type, at the channel
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(("vega-lite", ImageDiagnosticSeverity.Info, "vega-lite-info", "vl-type-inferred"), (note.Source, note.Severity, note.Code, note.Detail));
        Assert.Equal("/encoding/x", note.Location?.Pointer);
        Assert.Contains("The field 'a' of the 'x' channel has no type; it is read as nominal", note.Message, StringComparison.Ordinal);
        Assert.True(image.Surface.Width > 0);
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
        var refused = Assert.IsType<ImageSourceBudgetException>(Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 1, MaxDecodedPixels = 100 })).InnerException);
        var refusedBoxed = Assert.IsType<ImageSourceBudgetException>(Assert.Throws<ViewerException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 1, Height = 1, MaxDecodedPixels = 100 })).InnerException);
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
    ///     Test that a Mermaid diagram rendered by path lists each picture its nodes show once in
    ///     <see cref="ViewerImage.Embedded"/>, although the diagram asks for each picture twice (once to learn its natural
    ///     size, once to draw it).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_MermaidNodePictures_AreListedOnceEach()
    {
        // Arrange: a raster and a vector picture, each shown by one node of a flowchart
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText("inner.svg", ViewerFixtures.Svg(string.Empty));
        var path = folder.WriteText(
            "flow.mmd",
            "flowchart LR\n    a@{ img: \"logo.png\", w: 40, h: 40 } --> b@{ img: \"inner.svg\", w: 40, h: 40 }\n");

        // Act
        using var image = CanvasNetViewer.Render(path);

        // Assert: each picture is listed once, at the top level of the diagram, and nothing is reported
        Assert.Equal("mermaid", image.Format);
        Assert.Equal([("logo.png", 0), ("inner.svg", 0)], image.Embedded.Select(e => (e.Href, e.Depth)));
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that an SVG document that shows a Mermaid diagram, whose node shows a picture, lists the picture once (one
    ///     level down) and the diagram once in <see cref="ViewerImage.Embedded"/>.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_SvgEmbeddingMermaidWithPicture_ListsThePictureOnce()
    {
        // Arrange: an SVG cell that shows a flowchart, whose single node shows a raster
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        folder.WriteText("flow.mmd", "flowchart LR\n    a@{ img: \"logo.png\", w: 40, h: 40 }\n");
        var path = folder.WriteText("doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("flow.mmd", 0)));

        // Act
        using var image = CanvasNetViewer.Render(path);

        // Assert: the picture is listed once inside the diagram, then the diagram itself, and nothing is reported
        Assert.Equal([("logo.png", 1), ("flow.mmd", 0)], image.Embedded.Select(e => (e.Href, e.Depth)));
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that, under a policy confined to its folder, a file rendered by path may not embed a file outside it (an
    ///     access-denied warning), and that an SVG file that embeds itself stops at once.
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
        var options = new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(folder.Combine("site")) };

        // Act
        using var image = CanvasNetViewer.Render(path, options);

        // Assert
        Assert.Equal(ViewerFixtures.Gray, image.Surface[50, 50]);
        Assert.Single(image.Diagnostics, d => d.Code == FolderImageResolver.AccessDeniedCode);
        var cycle = Assert.Single(image.Diagnostics, d => d.Code == FolderImageResolver.UnresolvedCode);
        Assert.Contains("cycle", cycle.Message, StringComparison.Ordinal);
        Assert.Empty(image.Embedded);
    }

    /// <summary>
    ///     Test that, under the default policy, a file rendered by path embeds a picture of its parent folder.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_SvgParentFolderLink_IsDrawnByDefault()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("secret.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var path = folder.WriteText("site/doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("../secret.png", 0)));

        // Act
        using var image = CanvasNetViewer.Render(path);

        // Assert
        Assert.Equal(ViewerFixtures.Green, image.Surface[50, 50]);
        Assert.Empty(image.Diagnostics);
        Assert.Single(image.Embedded);
    }

    /// <summary>
    ///     Test that a link of the rendered file is denied with the error <c>file-access-denied</c> under the strict
    ///     option, and is a warning without it.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StrictDeniedLink_ThrowsFileAccessDenied()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("secret.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var path = folder.WriteText("site/doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("../secret.png", 0)));
        var confined = new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(folder.Combine("site")) };

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, confined with { ThrowOnUnsupportedFeature = true }));
        using var lenient = CanvasNetViewer.Render(path, confined);

        // Assert
        Assert.Equal(ErrorCodes.FileAccessDenied, ex.Code);
        Assert.Contains("../secret.png", ex.Message, StringComparison.Ordinal);
        Assert.Equal(FolderImageResolver.AccessDeniedCode, Assert.Single(lenient.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that the policy of the render reaches the includes of a model an SVG file embeds: a confined policy fails
    ///     the render with <c>file-access-denied</c>, whose chain of inner exceptions holds the denial and which names both
    ///     files, and the default draws the model.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_NestedModelInclude_IsCheckedByTheRenderPolicy()
    {
        // Arrange: site/doc.svg embeds site/part.scad, which includes lib.scad of the parent folder
        using var folder = new ViewerTestFolder();
        folder.WriteText("lib.scad", "module part() { cube(8, center = true); }\n");
        folder.WriteText("site/part.scad", "include <../lib.scad>\npart();\n");
        var path = folder.WriteText("site/doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("part.scad", 0)));
        var confined = new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(folder.Combine("site")) };

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, confined));
        using var allowed = CanvasNetViewer.Render(path);

        // Assert
        Assert.Equal(ErrorCodes.FileAccessDenied, ex.Code);
        Assert.StartsWith("'doc.svg' embeds 'part.scad': ", ex.Message, StringComparison.Ordinal);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, FindDenial(ex)?.Kind);
        Assert.Single(allowed.Embedded);
    }

    /// <summary>
    ///     Test that the policy of the render reaches the data of a chart an SVG file embeds: a confined policy fails the
    ///     render with <c>file-access-denied</c>, whose chain of inner exceptions holds the denial, and the default draws
    ///     the chart.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_NestedChartData_IsCheckedByTheRenderPolicy()
    {
        // Arrange: site/doc.svg embeds site/chart.vl.json, whose data is in the parent folder
        using var folder = new ViewerTestFolder();
        folder.WriteText("data.csv", "a,b\nA,3\nB,5\n");
        folder.WriteText(
            "site/chart.vl.json",
            "{\"data\":{\"url\":\"../data.csv\"},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}");
        var path = folder.WriteText("site/doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("chart.vl.json", 0)));
        var confined = new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(folder.Combine("site")) };

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, confined));
        using var allowed = CanvasNetViewer.Render(path);

        // Assert
        Assert.Equal(ErrorCodes.FileAccessDenied, ex.Code);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, FindDenial(ex)?.Kind);
        Assert.Single(allowed.Embedded);
    }

    /// <summary>
    ///     Test that the no-symlinks policy works through a nested source: a model an SVG file embeds reads a plain
    ///     include under the policy (judged from the folder of the outer document, whose real location the nested
    ///     resolver captures before its open), while an include through a directory link (a junction on Windows, so the
    ///     test needs no privilege) is denied.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_NestedModelInclude_NoSymlinks_ReadsPlainIncludeAndDeniesLink()
    {
        // Arrange: site/doc.svg embeds site/part.scad (plain include) and site/linked.scad (include through site/libs,
        // a directory link to a folder beside the site)
        using var folder = new ViewerTestFolder();
        folder.WriteText("site/lib.scad", "module part() { cube(8, center = true); }\n");
        folder.WriteText("real/lib.scad", "module part() { cube(8, center = true); }\n");
        folder.WriteText("site/part.scad", "include <lib.scad>\npart();\n");
        folder.WriteText("site/linked.scad", "include <libs/lib.scad>\npart();\n");
        folder.CreateDirectoryLinkOrSkip("site/libs", "real");
        var plain = folder.WriteText("site/doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("part.scad", 0)));
        var linked = folder.WriteText("site/linked.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("linked.scad", 0)));
        var noLinks = new ViewerRenderOptions { FileAccess = FileAccessPolicy.NoSymlinks };

        // Act
        using var drawn = CanvasNetViewer.Render(plain, noLinks);
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(linked, noLinks));

        // Assert
        Assert.Single(drawn.Embedded);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, FindDenial(ex)?.Kind);
    }

    /// <summary>
    ///     Test that the file-access option of a render is the allow-all policy by default and refuses null.
    /// </summary>
    [Fact]
    public void ViewerRenderOptions_FileAccess_DefaultsToAllowAllAndRejectsNull()
    {
        // Act
        var options = new ViewerRenderOptions();
        var confined = options with { FileAccess = FileAccessPolicy.NoSymlinks };

        // Assert
        Assert.Same(FileAccessPolicy.AllowAll, options.FileAccess);
        Assert.Same(FileAccessPolicy.NoSymlinks, confined.FileAccess);
        Assert.Throws<ArgumentNullException>(() => new ViewerRenderOptions { FileAccess = null! });
    }

    /// <summary>
    ///     Finds the file-access refusal in the chain of inner exceptions of a failure.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The refusal, or <see langword="null"/> when the chain has none.</returns>
    private static FileReferenceException? FindDenial(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is FileReferenceException denial)
            {
                return denial;
            }
        }

        return null;
    }

    /// <summary>
    ///     Test that a render by path fails with the codes of the tool for a missing file, an unknown extension, and a
    ///     corrupt file (with the original failure inside), with an argument exception for a null path, and that a folder
    ///     option that holds the file works.
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
        var missing = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(folder.Combine("missing.png")));
        Assert.Equal(ErrorCodes.InputNotFound, missing.Code);
        Assert.IsType<FileNotFoundException>(missing.InnerException);
        var unsupported = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(folder.Combine("notes.txt")));
        Assert.Equal(ErrorCodes.UnsupportedFormat, unsupported.Code);
        Assert.Contains(".svg", unsupported.Message, StringComparison.Ordinal);
        Assert.IsType<NotSupportedException>(unsupported.InnerException);
        var corrupt = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(bad));
        Assert.Equal(ErrorCodes.CorruptInput, corrupt.Code);
        Assert.IsType<InvalidDataException>(corrupt.InnerException);
        Assert.Null(corrupt.Location);
        Assert.Throws<ArgumentNullException>(() => CanvasNetViewer.Render((string)null!));
        using var withFolder = CanvasNetViewer.Render(inner, new ViewerRenderOptions { Folder = folder.Path });
        Assert.Equal((2, 2), (withFolder.Surface.Width, withFolder.Surface.Height));
    }

    /// <summary>
    ///     Test that a file outside the folder option is rendered (the folder is the base of its links, not a boundary,
    ///     and the file itself is never judged by the policy), even under a policy that denies everything.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_FileOutsideTheFolderOption_IsRendered()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var inner = folder.WriteBytes("sub/inner.png", ViewerFixtures.Png(2, 2, ViewerFixtures.Green));
        Directory.CreateDirectory(folder.Combine("other"));
        var denyAll = FileAccessPolicy.FromDelegate("deny-all", _ => FileAccessDecision.Deny("nothing may be read"));

        // Act
        using var image = CanvasNetViewer.Render(inner, new ViewerRenderOptions { Folder = folder.Combine("other"), FileAccess = denyAll });

        // Assert
        Assert.Equal((2, 2), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(ViewerFixtures.Green, image.Surface[0, 0]);
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
        var strict = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true }));

        // Assert
        Assert.Contains(lenient.Diagnostics, d => d.Severity == ImageDiagnosticSeverity.Unsupported);
        Assert.Equal("scad", lenient.Format);
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.IsType<ScadUnsupportedFeatureException>(strict.InnerException);
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
    ///     Test that a stream with an unknown extension, a stream that cannot be read, and null arguments are rejected.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StreamInvalidArguments_Throw()
    {
        // Arrange: a stream that is closed cannot be read
        var closed = new MemoryStream(ViewerFixtures.Png(2, 2, ViewerFixtures.Green));
        closed.Dispose();

        // Act / Assert
        Assert.Equal(ErrorCodes.UnsupportedFormat, Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(new MemoryStream(), "x.unknown")).Code);
        Assert.Throws<ArgumentNullException>(() => CanvasNetViewer.Render((Stream)null!, "x.png"));
        Assert.Throws<ArgumentNullException>(() => CanvasNetViewer.Render(new MemoryStream(), null!));
        Assert.Equal("content", Assert.Throws<ArgumentException>(() => CanvasNetViewer.Render(closed, "x.png")).ParamName);
    }

    /// <summary>
    ///     Test the Mermaid row of issue #166: a syntax error is a <see cref="ViewerException"/> with the code of the tool,
    ///     the line and column in <see cref="ViewerException.Location"/>, the library's code in
    ///     <see cref="ViewerException.DetailCode"/>, and the library exception inside.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_MermaidSyntaxError_ThrowsViewerExceptionWithLocationAndDetailCode()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("flowchart LR\n  A -> B\n"));

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(stream, "diagram.mmd"));

        // Assert
        Assert.Equal((ErrorCodes.MermaidError, "mermaid-syntax-error"), (ex.Code, ex.DetailCode));
        Assert.Equal(new ImageDiagnosticLocation("diagram.mmd", 2, 5, null), ex.Location);
        Assert.IsType<MermaidSyntaxException>(ex.InnerException);
        Assert.Contains("diagram.mmd:2:5", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a PlantUML syntax error is a <see cref="ViewerException"/> with the code of the tool, the line in
    ///     <see cref="ViewerException.Location"/>, the library's code in <see cref="ViewerException.DetailCode"/>, and the
    ///     library exception inside.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PlantUmlSyntaxError_ThrowsViewerExceptionWithLocationAndDetailCode()
    {
        // Arrange: the message line of issue #164, which has no target
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("@startuml\nparticipant Agent\nAgent -> : call tool\n@enduml\n"));

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(stream, "flow.puml"));

        // Assert
        var inner = Assert.IsType<PlantUmlSyntaxException>(ex.InnerException);
        Assert.Equal((ErrorCodes.PlantUmlError, inner.Code), (ex.Code, ex.DetailCode));
        Assert.Equal("plantuml-syntax-error", ex.DetailCode);
        Assert.Equal(("flow.puml", 3), (ex.Location?.File, ex.Location?.Line));
    }

    /// <summary>
    ///     Test the OpenSCAD row of issue #166: a syntax error is <c>scad-error</c> with the line and column of the library
    ///     and its code <c>scad-syntax-error</c>.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadSyntaxError_ThrowsViewerExceptionWithLocation()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("cube([10, 10, 10]\nsphere(5);\n"));

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(stream, "model.scad"));

        // Assert
        Assert.Equal((ErrorCodes.ScadError, "scad-syntax-error"), (ex.Code, ex.DetailCode));
        Assert.Equal(new ImageDiagnosticLocation("model.scad", 2, 1, null), ex.Location);
        Assert.IsType<ScadSyntaxException>(ex.InnerException);
    }

    /// <summary>
    ///     Test that a Vega-Lite chart cut short (issue #163) is <c>vega-lite-error</c> at the end of the text, not a depth
    ///     error, and that an invalid property is located by its line and JSON pointer, both without a detail code: the
    ///     library gives a chart error none.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ChartErrors_ThrowVegaLiteErrorWithLocation()
    {
        // Arrange
        using var truncated = new MemoryStream(Encoding.UTF8.GetBytes("{\n  \"mark\": \"bar\",\n  \"encoding\": {\n"));
        using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("{\n  \"data\": {\"values\": [{\"a\": 1}]},\n  \"mark\": \"bar\",\n  \"encoding\": {\"x\": {\"field\": \"a\", \"type\": \"bogus\"}}\n}\n"));

        // Act
        var cut = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(truncated, "chart.vl.json"));
        var wrong = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(invalid, "chart.vl.json"));

        // Assert: the truncated chart is located in its text, is not a depth error, and has no detail code
        Assert.Equal(ErrorCodes.VegaLiteError, cut.Code);
        Assert.IsType<VegaLiteSpecException>(cut.InnerException);
        Assert.DoesNotContain("deeper", cut.Message, StringComparison.Ordinal);
        Assert.Equal("chart.vl.json", cut.Location?.File);
        Assert.NotNull(cut.Location?.Line);
        Assert.Null(cut.DetailCode);

        // Assert: the invalid type is located by its line and pointer; the library gives a chart error no detail code
        Assert.Equal(ErrorCodes.VegaLiteError, wrong.Code);
        Assert.Equal("/encoding/x/type", wrong.Location?.Pointer);
        Assert.Equal(4, wrong.Location?.Line);
        Assert.Null(wrong.DetailCode);
    }

    /// <summary>
    ///     Test the SVG row of issue #166: malformed markup is <c>corrupt-input</c> with the line and column of the XML
    ///     failure in <see cref="ViewerException.Location"/> and no detail code.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_MalformedSvg_ThrowsCorruptInputWithXmlLineAndColumn()
    {
        // Arrange: the rect is never closed, so the end tag of the root does not match (line 2)
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\">\n  <rect width=\"5\"></svg>\n"));

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(stream, "figure.svg"));

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, ex.Code);
        Assert.Null(ex.DetailCode);
        Assert.Equal(("figure.svg", 2), (ex.Location?.File, ex.Location?.Line));
        Assert.NotNull(ex.Location?.Column);
        Assert.IsType<InvalidDataException>(ex.InnerException);
    }

    /// <summary>
    ///     Test the file-access rows of issue #166: a chart's data, a PlantUML include and a model's include outside the
    ///     policy are each <c>file-access-denied</c>, located in the source that names the file, with the denial in the
    ///     chain of inner exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_DeniedReferences_ThrowFileAccessDenied()
    {
        // Arrange: three sources in site/ that read a file of the parent folder
        using var folder = new ViewerTestFolder();
        folder.WriteText("data.csv", "a,b\nA,3\n");
        folder.WriteText("lib.scad", "module part() { cube(8, center = true); }\n");
        folder.WriteText("common.iuml", "Alice -> Bob : hi\n");
        var chart = folder.WriteText(
            "site/chart.vl.json",
            "{\n  \"mark\": \"bar\",\n  \"data\": {\"url\": \"../data.csv\"},\n  \"encoding\": {\"x\": {\"field\": \"a\", \"type\": \"nominal\"}}\n}\n");
        var diagram = folder.WriteText("site/flow.puml", "@startuml\n!include ../common.iuml\n@enduml\n");
        var model = folder.WriteText("site/part.scad", "include <../lib.scad>\npart();\n");
        var confined = new ViewerRenderOptions { FileAccess = FileAccessPolicy.UnderRoots(folder.Combine("site")) };

        // Act
        var chartDenied = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(chart, confined));
        var diagramDenied = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(diagram, confined));
        var modelDenied = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(model, confined));

        // Assert: one code for every kind of reference, with the place that names the file
        Assert.All(new[] { chartDenied, diagramDenied, modelDenied }, ex => Assert.Equal(ErrorCodes.FileAccessDenied, ex.Code));
        Assert.All(new[] { chartDenied, diagramDenied, modelDenied }, ex => Assert.Equal(FileReferenceFailureKind.AccessDenied, FindDenial(ex)?.Kind));
        Assert.IsType<VegaLiteRenderException>(chartDenied.InnerException);
        Assert.Equal("/data/url", chartDenied.Location?.Pointer);
        Assert.IsType<PlantUmlSyntaxException>(diagramDenied.InnerException);
        Assert.Equal(2, diagramDenied.Location?.Line);
        Assert.IsType<ScadEvaluationException>(modelDenied.InnerException);
        Assert.Equal(1, modelDenied.Location?.Line);
    }

    /// <summary>
    ///     Test that the failures of the input that are not in its source are <see cref="ViewerException"/>s with the codes
    ///     of the tool and the original exception inside: a missing file, an unknown extension, a picture over the pixel
    ///     budget, and a stream longer than 16 MiB.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_InputFailures_ThrowViewerExceptionWithCode()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var logo = folder.WriteBytes("logo.png", ViewerFixtures.Png(12, 8, ViewerFixtures.Green));
        using var huge = new MemoryStream(new byte[(16 * 1024 * 1024) + 1]);

        // Act
        var missing = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(folder.Combine("missing.png")));
        var unknown = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(new MemoryStream([1]), "notes.txt"));
        var budget = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(logo, new ViewerRenderOptions { MaxDecodedPixels = 10 }));
        var tooLong = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(huge, "big.png"));

        // Assert
        Assert.Equal(ErrorCodes.InputNotFound, missing.Code);
        Assert.IsType<FileNotFoundException>(missing.InnerException);
        Assert.Equal(ErrorCodes.UnsupportedFormat, unknown.Code);
        Assert.IsType<NotSupportedException>(unknown.InnerException);
        Assert.Equal(ErrorCodes.BudgetExceeded, budget.Code);
        Assert.Equal(96, Assert.IsType<ImageSourceBudgetException>(budget.InnerException).RequestedPixels);
        Assert.Contains("MaxDecodedPixels", budget.Hint, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.InputUnreadable, tooLong.Code);
        Assert.IsType<InvalidDataException>(tooLong.InnerException);
        Assert.All(new[] { missing, unknown, budget, tooLong }, ex => Assert.Null(ex.Location));
    }

    /// <summary>
    ///     Test that a caller's stream whose reading fails is <c>input-unreadable</c> with the failure inside, also when
    ///     the stream refuses with a <see cref="NotSupportedException"/>, and that a name no source claims is
    ///     <c>unsupported-format</c> before the stream is read.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StreamReadFailures_AreInputUnreadable()
    {
        // Arrange: streams that fail when they are read
        using var broken = new FailingReadStream(new IOException("the device is gone"));
        using var refusing = new FailingReadStream(new NotSupportedException("the stream cannot be read now"));
        using var unknownName = new FailingReadStream(new IOException("not expected"));

        // Act
        var io = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(broken, "a.png"));
        var refused = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(refusing, "a.png"));
        var unknown = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(unknownName, "x.unknown"));

        // Assert
        Assert.Equal(ErrorCodes.InputUnreadable, io.Code);
        Assert.IsType<IOException>(io.InnerException);
        Assert.Equal(ErrorCodes.InputUnreadable, refused.Code);
        Assert.IsType<NotSupportedException>(refused.InnerException);
        Assert.Contains("'a.png' cannot be read", refused.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.UnsupportedFormat, unknown.Code);
        Assert.Equal(0, unknownName.Reads);
    }

    /// <summary>
    ///     A readable stream whose every read fails with the exception it was given, and that counts the reads.
    /// </summary>
    /// <param name="failure">The exception every read throws.</param>
    private sealed class FailingReadStream(Exception failure) : MemoryStream
    {
        /// <summary>
        ///     Gets the number of reads that were tried.
        /// </summary>
        public int Reads { get; private set; }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++;
            throw failure;
        }
    }

    /// <summary>
    ///     Test that the messages and hints of the failures of a render name no option of the command-line tool: the
    ///     strict failures of a model, a chart, both diagrams, an SVG link and a position selector that matched nothing,
    ///     the limits of a model, and a picture larger than the largest image.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_Failures_HaveNoCommandLineWording()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        var strict = new ViewerRenderOptions { ThrowOnUnsupportedFeature = true };
        var sources = new (string Name, string Text, ViewerRenderOptions Options)[]
        {
            ("offset.scad", "offset(r = 1) square(2);\n", strict),
            ("geo.vl.json", "{\"data\": {\"values\": []}, \"mark\": \"geoshape\"}", strict),
            ("journey.mmd", "journey\n    title Feed the ducks\n", strict),
            ("legacy.pu", "@startuml\n(*) --> First\nFirst --> Second\n@enduml\n", strict),
            ("links.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("missing.png", 0)), strict),
            ("big.scad", "cube(1);\n", new ViewerRenderOptions { Scad = new ScadViewOptions { MaxTriangles = 1 } }),
            ("wide.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20000\" height=\"10\"/>", new ViewerRenderOptions()),
            ("flow.mmd", "flowchart LR\n  A --> B\n", new ViewerRenderOptions { Positions = PositionSelection.Parse(["node:Zed"]), ThrowOnUnsupportedFeature = true }),
        };

        // Act
        var failures = sources
            .Select(s => Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(folder.WriteText(s.Name, s.Text), s.Options)))
            .ToList();

        // Assert: the expected codes, and no command-line option in any message or hint
        Assert.Equal(
            [ErrorCodes.UnsupportedFeature, ErrorCodes.UnsupportedFeature, ErrorCodes.UnsupportedFeature, ErrorCodes.UnsupportedFeature,
             ErrorCodes.UnsupportedFeature, ErrorCodes.BudgetExceeded, ErrorCodes.SizeLimit, ErrorCodes.PositionsUnmatched],
            failures.Select(f => f.Code));
        Assert.All(failures, f => Assert.NotNull(f.Hint));
        Assert.All(failures, f => Assert.DoesNotMatch(CommandLineOption, f.Message));
        Assert.All(failures, f => Assert.DoesNotMatch(CommandLineOption, f.Hint!));
        Assert.Contains("strict mode", failures[0].Hint, StringComparison.Ordinal);
        Assert.Contains("ScadViewOptions.MaxTriangles", failures[5].Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The pattern of an option of the command-line tool (<c>--strict</c>, <c>--max-triangles</c>), which no failure of
    ///     the library may name; an arrow of a diagram (<c>--&gt;</c>) is not one.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex CommandLineOption = new("(?<![\\w-])--[a-z]");

    /// <summary>
    ///     Test that an extra source that fails is mapped like the built-in sources: a library exception of the shared
    ///     mapping gets its code, and any other failure is <c>operation-failed</c>, with the original exception inside.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ExtraSourceFailure_IsMappedOrOperationFailed()
    {
        // Arrange: one source that reports bad data and one that fails in its own way
        var options = new ViewerRenderOptions
        {
            ExtraSources =
            [
                new FailingSource("corrupt", ".bad", new InvalidDataException("the header is damaged")),
                new FailingSource("broken", ".oops", new ArithmeticException("a defect"))
            ]
        };

        // Act
        var corrupt = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(new MemoryStream([1]), "x.bad", options));
        var failed = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(new MemoryStream([1]), "x.oops", options));

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, corrupt.Code);
        Assert.IsType<InvalidDataException>(corrupt.InnerException);
        Assert.Equal(ErrorCodes.OperationFailed, failed.Code);
        Assert.IsType<ArithmeticException>(failed.InnerException);
    }

    /// <summary>
    ///     Test that a picture an SVG file embeds that fails in its source is mapped with the same codes, location and detail
    ///     code as the file itself, with a message that names both files.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_EmbeddedSourceError_KeepsCodeLocationAndDetailCode()
    {
        // Arrange
        using var folder = new ViewerTestFolder();
        folder.WriteText("flow.mmd", "flowchart LR\n  A -> B\n");
        var path = folder.WriteText("doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("flow.mmd", 0)));

        // Act
        var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path));

        // Assert
        Assert.Equal((ErrorCodes.MermaidError, "mermaid-syntax-error"), (ex.Code, ex.DetailCode));
        Assert.Equal(new ImageDiagnosticLocation("flow.mmd", 2, 5, null), ex.Location);
        Assert.StartsWith("'doc.svg' embeds 'flow.mmd': ", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A source that always fails with the exception it was given, as a source of an application might.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <param name="extension">The extension it claims.</param>
    /// <param name="failure">The exception it throws.</param>
    private sealed class FailingSource(string name, string extension, Exception failure) : IImageSource
    {
        /// <inheritdoc />
        public string Name { get; } = name;

        /// <inheritdoc />
        public IReadOnlyList<string> Extensions { get; } = [extension];

        /// <inheritdoc />
        public IReadOnlyList<string> Schemes => [];

        /// <inheritdoc />
        public ImageSourceResult Render(ImageSourceRequest request) => throw failure;
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
    ///     Test that a file rendered directly through a file symbolic link is rendered whatever the policy, because the
    ///     rendered file is the caller's choice. The test skips where file links cannot be created (Windows without the
    ///     privilege for symbolic links; the directory-link test covers links there).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PathThroughSymbolicLink_IsRenderedWhateverThePolicy()
    {
        // Arrange: a link to a picture next to it
        using var folder = new ViewerTestFolder();
        var real = folder.WriteBytes("real.png", ViewerFixtures.Png(2, 2, ViewerFixtures.Green));
        var link = folder.Combine("link.png");
        try
        {
            File.CreateSymbolicLink(link, real);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("File symbolic links cannot be created here (on Windows they need a privilege): " + ex.Message);
            return;
        }

        // Act
        using var byDefault = CanvasNetViewer.Render(link);
        using var noLinks = CanvasNetViewer.Render(link, new ViewerRenderOptions { FileAccess = FileAccessPolicy.NoSymlinks });

        // Assert
        Assert.Equal(ViewerFixtures.Green, byDefault.Surface[0, 0]);
        Assert.Equal(ViewerFixtures.Green, noLinks.Surface[0, 0]);
    }

    /// <summary>
    ///     Test that, under the no-symlinks policy, a link of the rendered file through a directory link (a junction on
    ///     Windows, so the test needs no privilege) is denied, while the default policy draws it.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_LinkThroughDirectoryLink_NoSymlinks_IsAccessDenied()
    {
        // Arrange: site/lib is a directory link to a folder beside the site
        using var folder = new ViewerTestFolder();
        folder.WriteBytes("real/a.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));
        var path = folder.WriteText("site/doc.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("lib/a.png", 0)));
        folder.CreateDirectoryLinkOrSkip("site/lib", "real");

        // Act
        using var denied = CanvasNetViewer.Render(path, new ViewerRenderOptions { FileAccess = FileAccessPolicy.NoSymlinks });
        using var followed = CanvasNetViewer.Render(path);

        // Assert
        Assert.Equal(FolderImageResolver.AccessDeniedCode, Assert.Single(denied.Diagnostics).Code);
        Assert.Equal(ViewerFixtures.Gray, denied.Surface[50, 50]);
        Assert.Equal(ViewerFixtures.Green, followed.Surface[50, 50]);
    }
}
