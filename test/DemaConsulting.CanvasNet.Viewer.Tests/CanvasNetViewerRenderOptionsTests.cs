// cspell:ignore pptx vsdx scad
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Tests of the options of <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/> beyond the size: the
///     page, the resolution, the background and the options of a model, the new members of <see cref="ViewerImage"/>
///     (<c>Page</c>, <c>PageCount</c>, <c>Anchors</c>), the validation before the file is read, the error shapes, and the
///     parity with <see cref="SourceLoader"/>.
/// </summary>
public class CanvasNetViewerRenderOptionsTests
{
    /// <summary>
    ///     A stream that fails when it is read, to prove that a render is refused before the content is touched.
    /// </summary>
    private sealed class UnreadableStream : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("The stream must not be read.");
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    ///     Gets the kinds of source that are compared with the loader of the tool.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string> ParityKeys()
    {
        return ["png", "svg", "pdf", "pptx", "vsdx", "scad", "vega-lite"];
    }

    /// <summary>
    ///     Test that <c>Page</c> and <c>Dpi</c> draw the chosen page at the resolution, by path, and that the result
    ///     reports the page and the page count.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PageAndDpi_DrawsThePageAtTheResolution()
    {
        // Arrange: page 2 of the PDF is displayed 96 by 144 points
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { Page = 2, Dpi = 192 });
        using var first = CanvasNetViewer.Render(path);

        // Assert: 2 times the 96 dpi size of the page, green, over a transparent background
        Assert.Equal((256, 384), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(new Rgba32(0, 153, 0, 255), image.Surface[128, 192]);
        Assert.Equal((2, 2), (image.Page, image.PageCount));
        Assert.Equal("pdf", image.Format);
        Assert.Equal((192, 128), (first.Surface.Width, first.Surface.Height));
        Assert.Equal((1, 2), (first.Page, first.PageCount));
        Assert.Equal(0, first.Surface[170, 20].A);
        Assert.Empty(image.Anchors);
    }

    /// <summary>
    ///     Test that a page can be chosen when the file is given as a stream, with the same result as by path.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StreamWithPage_DrawsThePage()
    {
        // Arrange
        using var stream = new MemoryStream(FixtureFactory.CreatePptx());

        // Act
        using var image = CanvasNetViewer.Render(stream, "deck.pptx", new ViewerRenderOptions { Page = 2 });

        // Assert: slide 2 is blue over its top half
        Assert.Equal((FixtureFactory.PptxSlideWidth, FixtureFactory.PptxSlideHeight), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(new Rgba32(0, 0, 255, 255), image.Surface[240, 90]);
        Assert.Equal(0, image.Surface[240, 270].A);
        Assert.Equal((2, 2), (image.Page, image.PageCount));
    }

    /// <summary>
    ///     Test that a page the file does not have is a <see cref="ViewerException"/> with the page-out-of-range code, for
    ///     a document and for a single-image file.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PageBeyondTheLast_ThrowsViewerExceptionPageOutOfRange()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var png = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var document = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(pdf, new ViewerRenderOptions { Page = 3 }));
        var image = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(png, new ViewerRenderOptions { Page = 2 }));

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, document.Code);
        Assert.Contains("page 3 does not exist: the document has 2 pages", document.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.PageOutOfRange, image.Code);
        Assert.Contains("page 2 does not exist: the file has 1 page", image.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that options that cannot be used are refused with <see cref="ArgumentOutOfRangeException"/> before the
    ///     stream is read.
    /// </summary>
    /// <param name="label">What is wrong.</param>
    /// <param name="options">The options.</param>
    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void CanvasNetViewer_Render_InvalidOptions_ThrowsBeforeReadingTheContent(string label, ViewerRenderOptions options)
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CanvasNetViewer.Render(new UnreadableStream(), "a.pdf", options));

        // Assert
        Assert.True(exception.ParamName == "options", label);
    }

    /// <summary>
    ///     Gets the render options that cannot be used, with a label.
    /// </summary>
    /// <returns>The test data.</returns>
    public static TheoryData<string, ViewerRenderOptions> InvalidOptions()
    {
        return new TheoryData<string, ViewerRenderOptions>
        {
            { "page 0", new ViewerRenderOptions { Page = 0 } },
            { "page negative", new ViewerRenderOptions { Page = -1 } },
            { "dpi 0", new ViewerRenderOptions { Dpi = 0 } },
            { "dpi NaN", new ViewerRenderOptions { Dpi = double.NaN } },
            { "dpi above the maximum", new ViewerRenderOptions { Dpi = 2401 } },
            { "width 0", new ViewerRenderOptions { Width = 0 } },
            { "unknown view", new ViewerRenderOptions { Scad = new ScadViewOptions { View = "sideways" } } },
            { "elevation 91", new ViewerRenderOptions { Scad = new ScadViewOptions { Elevation = 91 } } },
            { "time too large", new ViewerRenderOptions { Scad = new ScadViewOptions { Time = 2e9 } } },
            { "triangle limit 0", new ViewerRenderOptions { Scad = new ScadViewOptions { MaxTriangles = 0 } } }
        };
    }

    /// <summary>
    ///     Test that a fractional resolution for a Visio drawing is a usage error of the loader, as for the tool.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_FractionalDpiForVsdx_ThrowsViewerExceptionUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act
        var exception = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { Dpi = 100.5 }));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
    }

    /// <summary>
    ///     Test that a background flattens a transparent page, an SVG file and a raster with transparency, and that the
    ///     defaults stay transparent for the vector kinds.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_Background_FlattensTransparentAreas()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var svg = FixtureFactory.WriteText(directory.Path, "empty.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>");
        var red = new Rgba32(255, 0, 0, 255);

        // Act
        using var pdfDefault = CanvasNetViewer.Render(pdf);
        using var pdfRed = CanvasNetViewer.Render(pdf, new ViewerRenderOptions { Background = red });
        using var svgDefault = CanvasNetViewer.Render(svg);
        using var svgRed = CanvasNetViewer.Render(svg, new ViewerRenderOptions { Background = red });

        // Assert: the top-right quarter of the page is transparent by default and red with a background
        Assert.Equal(0, pdfDefault.Surface[170, 20].A);
        Assert.Equal(red, pdfRed.Surface[170, 20]);
        Assert.Equal(0, svgDefault.Surface[5, 5].A);
        Assert.Equal(red, svgRed.Surface[5, 5]);
    }

    /// <summary>
    ///     Test that a chart keeps its own background by default and that a background replaces it.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_VegaLiteBackground_DefaultIsTheChartsOwn()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "vega-lite");
        var red = new Rgba32(255, 0, 0, 255);

        // Act
        using var chart = CanvasNetViewer.Render(path);
        using var replaced = CanvasNetViewer.Render(path, new ViewerRenderOptions { Background = red });

        // Assert: opaque by default; the corner is red when the background is replaced
        Assert.Equal(255, chart.Surface[0, 0].A);
        Assert.Equal(red, replaced.Surface[0, 0]);
        Assert.Equal("vega-lite", chart.Format);
    }

    /// <summary>
    ///     Test that the options of a model choose its view and its variables, that its anchors and printed text come
    ///     back, and that a malformed anchor is a note with the finer code and the position.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadOptions_ChooseViewDefinesAndReportAnchorsEchoAndDetail()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(
            directory.Path,
            "model.scad",
            "size = 4;\ncube([12, 4, size]);\ntranslate([0, 0, size]) cube([4, 4, 8]);\necho(\"anchor\", \"top\", [0, 0, 4]);\necho(\"anchor\", \"bad\", [1, 2]);\necho(\"hello\");\n");
        var small = new ViewerRenderOptions { Width = 64, Height = 64 };

        // Act
        using var front = CanvasNetViewer.Render(model, small with { Scad = new ScadViewOptions { View = "front" } });
        using var top = CanvasNetViewer.Render(model, small with { Scad = new ScadViewOptions { View = "top" } });
        using var big = CanvasNetViewer.Render(
            model,
            small with { Scad = new ScadViewOptions { View = "front", Defines = new Dictionary<string, object?> { ["size"] = 30.0 } } });

        // Assert: different views and different variables are different pictures; the anchors, echo and notes come back
        SurfaceAssertDifferent(front.Surface, top.Surface);
        SurfaceAssertDifferent(front.Surface, big.Surface);
        Assert.Equal(["top"], front.Anchors.Select(a => a.Name));
        Assert.Equal((0, 0, 4), (front.Anchors[0].X, front.Anchors[0].Y, front.Anchors[0].Z));
        Assert.NotNull(front.Anchors[0].PixelX);
        Assert.Contains("ECHO: \"hello\"", front.Echo);
        var warning = Assert.Single(front.Diagnostics, d => d.Severity == ImageDiagnosticSeverity.Warning);
        Assert.Equal("scad-warning", warning.Code);
        Assert.Equal("scad-anchor-invalid", warning.Detail);
        Assert.Equal(5, warning.Location?.Line);
    }

    /// <summary>
    ///     Test that a model whose include is missing is a <see cref="ViewerException"/> with the scad-error code, as for
    ///     the tool, and that a corrupt raster stays an <see cref="ImageSourceException"/> whose inner exception is the
    ///     codec's.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_Failures_HaveTheDocumentedShapes()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "model.scad", "include <missing.scad>\ncube(1);\n");
        var corrupt = Path.Combine(directory.Path, "broken.png");
        File.WriteAllBytes(corrupt, FixtureFactory.CreatePngHeaderOnly(4, 4));
        var chart = FixtureFactory.WriteText(directory.Path, "broken.vl.json", "{ \"mark\": ");

        // Act
        var missingInclude = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(model));
        var broken = Assert.Throws<ImageSourceException>(() => CanvasNetViewer.Render(corrupt));
        var badChart = Assert.Throws<ImageSourceException>(() => CanvasNetViewer.Render(chart));

        // Assert
        Assert.Equal(ErrorCodes.ScadError, missingInclude.Code);
        Assert.IsAssignableFrom<InvalidDataException>(broken.InnerException);
        Assert.NotNull(badChart.InnerException);
        Assert.IsNotType<ViewerException>(badChart.InnerException);
    }

    /// <summary>
    ///     Test that rendering a file with the viewer gives the pixels the loader of the tool gives for the same file and
    ///     the equivalent options (the defaults of the two differ only in the background of a vector source, which is
    ///     given here).
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [MemberData(nameof(ParityKeys))]
    public void CanvasNetViewer_Render_MatchesTheLoaderOfTheTool(string key)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);
        var white = new Rgba32(255, 255, 255, 255);

        // Act
        using var viewer = CanvasNetViewer.Render(path, new ViewerRenderOptions { Background = white });
        using var loader = SourceLoader.Load(path, new SourceOptions { Background = white });

        // Assert: a chart keeps its own background (also white here) in both
        SurfaceAssert.Equal(loader.Surface, viewer.Surface);
        Assert.Equal(loader.Info.Format.ToFormatName(), viewer.Format);
        Assert.Equal(loader.Diagnostics.Select(d => (d.Code, d.Message)), viewer.Diagnostics.Select(d => (d.Code, d.Message)));
    }

    /// <summary>
    ///     Test that a raster is still fitted inside a requested box over a transparent margin and charged to the budget,
    ///     as before the loader was used.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_RasterWithBox_IsFittedAndCharged()
    {
        // Arrange: a 4 by 4 picture in a 10 by 6 box, and a budget that cannot hold the final buffer next to the source
        using var directory = new TempDirectory();
        var path = Path.Combine(directory.Path, "a.png");
        File.WriteAllBytes(path, ViewerFixtures.Png(4, 4, ViewerFixtures.Green));

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 10, Height = 6 });
        var refused = Assert.Throws<ImageSourceBudgetException>(
            () => CanvasNetViewer.Render(path, new ViewerRenderOptions { Width = 10, Height = 6, MaxDecodedPixels = 16 + 59 }));

        // Assert
        Assert.Equal((10, 6), (image.Surface.Width, image.Surface.Height));
        Assert.Equal(0, image.Surface[0, 3].A);
        Assert.Equal(ViewerFixtures.Green, image.Surface[5, 3]);
        Assert.Equal(60, refused.RequestedPixels);
    }

    /// <summary>
    ///     Test that a canceled token stops a render of a document.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_CanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Test that the new members of the render options have the documented defaults.
    /// </summary>
    [Fact]
    public void ViewerRenderOptions_Defaults_PageOneAndNoDpiBackgroundOrScad()
    {
        // Act
        var options = new ViewerRenderOptions();

        // Assert
        Assert.Equal(1, options.Page);
        Assert.Null(options.Dpi);
        Assert.Null(options.Background);
        Assert.Null(options.Scad);
    }

    /// <summary>
    ///     Asserts that two surfaces of the same size are not the same picture.
    /// </summary>
    /// <param name="a">The first surface.</param>
    /// <param name="b">The second surface.</param>
    private static void SurfaceAssertDifferent(Surface a, Surface b)
    {
        var different = false;
        for (var y = 0; y < a.Height && !different; y++)
        {
            different = !a.GetRowSpan(y).SequenceEqual(b.GetRowSpan(y));
        }

        Assert.True(different, "The two pictures are the same.");
    }
}
