// cspell:ignore canvasnet pdf pptx vsdx Visio dpi emf FutureRowKind docx
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the page-based sources (PDF, PPTX, and VSDX) of the SourceLoader class: detection of a package,
///     probing the pages, page selection, sizing, diagnostics, strict mode, and damaged documents.
/// </summary>
public class SourceLoaderVectorTests
{
    /// <summary>
    ///     The red the fixtures paint.
    /// </summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     The blue the fixtures paint.
    /// </summary>
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>
    ///     The white the pages are rendered over.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Gets the keys of the document kinds, as theory data.
    /// </summary>
    public static TheoryData<string> DocumentKeys => ["pdf", "pptx", "vsdx"];

    /// <summary>
    ///     Gets every combination of a document kind and a way of damaging it, as theory data.
    /// </summary>
    public static TheoryData<string, string> DamagedDocuments
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var key in new[] { "pdf", "pptx", "vsdx" })
            {
                foreach (var cut in new[] { 1, 16, 64, 200, 500, 1000, 2000, 3000 })
                {
                    data.Add(key, $"cut-{cut}");
                }

                foreach (var damage in new[] { "tail-100", "tail-1000", "middle-garbage", "zeroed-middle" })
                {
                    data.Add(key, damage);
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     Writes a document and damages it.
    /// </summary>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="key">The kind of document.</param>
    /// <param name="damage">How to damage it: <c>cut-N</c> keeps the first N bytes, <c>tail-N</c> drops the last N, and the others overwrite the middle.</param>
    /// <returns>The path of the damaged file.</returns>
    private static string WriteDamaged(TempDirectory directory, string key, string damage)
    {
        var path = FixtureFactory.WriteSource(directory.Path, key);
        var bytes = File.ReadAllBytes(path);
        var parts = damage.Split('-');
        byte[] damaged;
        switch (parts[0])
        {
            case "cut":
                damaged = bytes[..Math.Min(bytes.Length, int.Parse(parts[1]))];
                break;
            case "tail":
                damaged = bytes[..Math.Max(0, bytes.Length - int.Parse(parts[1]))];
                break;
            default:
                damaged = [.. bytes];
                var garbage = FixtureFactory.CreateGarbage(64);
                for (var i = 0; i < garbage.Length; i++)
                {
                    damaged[(bytes.Length / 2) + i] = damage == "zeroed-middle" ? (byte)0 : garbage[i];
                }

                break;
        }

        File.WriteAllBytes(path, damaged);
        return path;
    }

    /// <summary>
    ///     Test that the entries of a ZIP package, not its extension, decide whether it is a presentation or a drawing.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_ZipPackages_UsesEntriesOverExtension()
    {
        // Arrange: a presentation and a drawing under each other's extension and under none, and a Word-like archive
        using var directory = new TempDirectory();
        var pptx = directory.Combine("deck.vsdx");
        File.WriteAllBytes(pptx, FixtureFactory.CreatePptx());
        var vsdx = directory.Combine("drawing.pptx");
        File.WriteAllBytes(vsdx, FixtureFactory.CreateVsdx());
        var bare = directory.Combine("package");
        File.WriteAllBytes(bare, FixtureFactory.CreatePptx());
        var word = directory.Combine("letter.pptx");
        File.WriteAllBytes(word, FixtureFactory.CreateZip([("word/document.xml", "<w:document/>"u8.ToArray())]));

        // Act / Assert: the entries decide; an archive that is neither is not supported, whatever its extension
        Assert.Equal(SourceKind.Pptx, SourceLoader.Detect(pptx));
        Assert.Equal(SourceKind.Vsdx, SourceLoader.Detect(vsdx));
        Assert.Equal(SourceKind.Pptx, SourceLoader.Detect(bare));
        var unsupported = Assert.Throws<ToolException>(() => SourceLoader.Detect(word));
        Assert.Equal(ErrorCodes.UnsupportedFormat, unsupported.Code);
        Assert.Contains("ZIP archive", unsupported.Message);
    }

    /// <summary>
    ///     Test that probing a PDF lists every page with its size, its rotation, and no name.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_Pdf_ListsPagesWithSizeAndRotation()
    {
        // Arrange: the two-page PDF, whose second page is rotated by 90 degrees
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        var info = SourceLoader.Probe(path);

        // Assert: two pages; the size in points and in pixels at 96 dpi, with the rotation already applied to page 2
        Assert.Equal(SourceKind.Pdf, info.Format);
        Assert.Equal(2, info.PageCount);
        Assert.Equal(new SourcePage(1, 192, 128, null, 144, 96, 0), info.Pages[0]);
        Assert.Equal(new SourcePage(2, 128, 192, null, 96, 144, 90), info.Pages[1]);
        Assert.Equal((192, 128), (info.Width, info.Height));
        Assert.False(info.HasAlpha);
    }

    /// <summary>
    ///     Test that probing a presentation lists its slides with the size of the presentation.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_Pptx_ListsSlidesWithPresentationSize()
    {
        // Arrange: the two-slide presentation of 5 by 3.75 inches
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pptx");

        // Act
        var info = SourceLoader.Probe(path);

        // Assert: both slides are 360x270 points, which is 480x360 pixels at 96 dpi
        Assert.Equal(2, info.PageCount);
        Assert.All(info.Pages, p => Assert.Equal((480, 360, 360d, 270d, null, null), (p.Width, p.Height, p.WidthPoints, p.HeightPoints, p.Name, p.Rotation)));
        Assert.Equal([1, 2], info.Pages.Select(p => p.Number));
    }

    /// <summary>
    ///     Test that probing a drawing lists its pages with their names and their own sizes.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_Vsdx_ListsPagesWithNamesAndSizes()
    {
        // Arrange: the drawing with a 4x3 inch page named First and a 3x4 inch page named Second
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act
        var info = SourceLoader.Probe(path);

        // Assert: names, and sizes in points and pixels at 96 dpi
        Assert.Equal(new SourcePage(1, 384, 288, "First", 288, 216, null), info.Pages[0]);
        Assert.Equal(new SourcePage(2, 288, 384, "Second", 216, 288, null), info.Pages[1]);
    }

    /// <summary>
    ///     Test that every document renders its first page by default, over white, at 96 dpi, with the pixels the
    ///     library draws.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_FirstPage_RendersOverWhiteAt96Dpi()
    {
        // Arrange: the three documents
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var pptx = FixtureFactory.WriteSource(directory.Path, "pptx");
        var vsdx = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act
        using var fromPdf = SourceLoader.Load(pdf, new SourceOptions());
        using var fromPptx = SourceLoader.Load(pptx, new SourceOptions());
        using var fromVsdx = SourceLoader.Load(vsdx, new SourceOptions());

        // Assert: the size, and the shapes where the document puts them (PDF: top-left red, bottom-right blue)
        Assert.Equal((192, 128), (fromPdf.Surface.Width, fromPdf.Surface.Height));
        Assert.Equal(Red, fromPdf.Surface[20, 20]);
        Assert.Equal(Blue, fromPdf.Surface[170, 110]);
        Assert.Equal(White, fromPdf.Surface[170, 20]);
        Assert.Equal(White, fromPdf.Surface[20, 110]);

        // PPTX: the left half red; VSDX: the top-left quarter red
        Assert.Equal((480, 360), (fromPptx.Surface.Width, fromPptx.Surface.Height));
        Assert.Equal(Red, fromPptx.Surface[100, 180]);
        Assert.Equal(White, fromPptx.Surface[400, 180]);
        Assert.Equal((384, 288), (fromVsdx.Surface.Width, fromVsdx.Surface.Height));
        Assert.Equal(Red, fromVsdx.Surface[50, 50]);
        Assert.Equal(White, fromVsdx.Surface[300, 250]);

        Assert.All(new[] { fromPdf, fromPptx, fromVsdx }, l =>
        {
            Assert.True(l.IsVector);
            Assert.Equal(1, l.Page);
            Assert.Empty(l.Diagnostics);
            Assert.Empty(l.Echo);
            Assert.Equal(["render"], l.Operations.Select(o => o.Op));
        });
    }

    /// <summary>
    ///     Test that a page is chosen by its 1-based number and that the loaded source says which page it is.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SecondPage_RendersThatPage()
    {
        // Arrange: the three documents
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var pptx = FixtureFactory.WriteSource(directory.Path, "pptx");
        var vsdx = FixtureFactory.WriteSource(directory.Path, "vsdx");
        var options = new SourceOptions { Page = 2 };

        // Act
        using var fromPdf = SourceLoader.Load(pdf, options);
        using var fromPptx = SourceLoader.Load(pptx, options);
        using var fromVsdx = SourceLoader.Load(vsdx, options);

        // Assert: PDF page 2 is displayed rotated (96x144 pt = 128x192 px), green with the yellow square at top-left
        Assert.Equal((128, 192), (fromPdf.Surface.Width, fromPdf.Surface.Height));
        Assert.Equal(new Rgba32(255, 255, 0, 255), fromPdf.Surface[10, 10]);
        Assert.Equal(new Rgba32(0, 153, 0, 255), fromPdf.Surface[100, 150]);

        // PPTX slide 2: top half blue; VSDX page 2 is 288x384 px with the bottom half blue
        Assert.Equal(Blue, fromPptx.Surface[240, 50]);
        Assert.Equal(White, fromPptx.Surface[240, 300]);
        Assert.Equal((288, 384), (fromVsdx.Surface.Width, fromVsdx.Surface.Height));
        Assert.Equal(Blue, fromVsdx.Surface[100, 300]);
        Assert.Equal(White, fromVsdx.Surface[100, 50]);
        Assert.All(new[] { fromPdf, fromPptx, fromVsdx }, l => Assert.Equal(2, l.Page));

        // The facts of a loaded page give its own size at the requested resolution
        Assert.Equal((128, 192), (fromPdf.Info.Width, fromPdf.Info.Height));
        Assert.Equal(2, fromPdf.Info.PageCount);
    }

    /// <summary>
    ///     Test that a page that does not exist is reported with the page-out-of-range code, for a document and for a
    ///     single image.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    /// <param name="page">The page.</param>
    [Theory]
    [InlineData("pdf", 0)]
    [InlineData("pdf", 3)]
    [InlineData("pptx", 3)]
    [InlineData("vsdx", 3)]
    [InlineData("png", 2)]
    [InlineData("svg", 2)]
    public void SourceLoader_Load_PageThatDoesNotExist_ThrowsPageOutOfRange(string key, int page)
    {
        // Arrange: a source and a page number that is not one of its pages
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Page = page }));

        // Assert: the code, and a message that names the page and the file
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
        Assert.Contains($"Page {page} does not exist", exception.Message);
        Assert.Contains(path, exception.Message);
    }

    /// <summary>
    ///     Test that the resolution sizes a page: twice the dots per inch is twice the pixels.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    /// <param name="dpi">The resolution.</param>
    /// <param name="expectedWidth">The expected width.</param>
    /// <param name="expectedHeight">The expected height.</param>
    [Theory]
    [InlineData("pdf", 192.0, 384, 256)]
    [InlineData("pdf", 72.0, 144, 96)]
    [InlineData("pdf", 150.0, 300, 200)]
    [InlineData("pptx", 48.0, 240, 180)]
    [InlineData("pptx", 100.5, 503, 377)]
    [InlineData("vsdx", 192.0, 768, 576)]
    [InlineData("vsdx", 50.0, 200, 150)]
    public void SourceLoader_Load_Dpi_SizesThePage(string key, double dpi, int expectedWidth, int expectedHeight)
    {
        // Arrange: a document
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);

        // Act: load its first page at the resolution
        using var loaded = SourceLoader.Load(path, new SourceOptions { Dpi = dpi });

        // Assert: the size, the same size in the facts, and the resolution in the render operation
        Assert.Equal((expectedWidth, expectedHeight), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.Equal((expectedWidth, expectedHeight), (loaded.Info.Width, loaded.Info.Height));
        Assert.Equal(dpi, loaded.Operations[0].Fields.Single(f => f.Key == "dpi").Value);
    }

    /// <summary>
    ///     Test that the page is rendered at the same size as the library renders it at that resolution.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Dpi_MatchesTheLibraryRender()
    {
        // Arrange: the PDF
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act: the tool at 120 dpi and the library at 120 dpi
        using var loaded = SourceLoader.Load(path, new SourceOptions { Dpi = 120 });
        using var document = DemaConsulting.CanvasNet.Pdf.PdfDocument.Open(path);
        using var expected = document.Render(0, 120f);

        // Assert: the same pixels
        SurfaceAssert.Equal(expected, loaded.Surface);
    }

    /// <summary>
    ///     Test that a resolution that is not usable is refused.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_DpiNotUsable_ThrowsUsageError()
    {
        // Arrange: a PDF and a drawing
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var vsdx = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act / Assert: zero and not-a-number dots per inch are refused; the drawing also refuses a fraction
        Assert.Equal(ErrorCodes.UsageError, Assert.Throws<ToolException>(() => SourceLoader.Load(pdf, new SourceOptions { Dpi = 0 })).Code);
        Assert.Equal(ErrorCodes.UsageError, Assert.Throws<ToolException>(() => SourceLoader.Load(pdf, new SourceOptions { Dpi = double.NaN })).Code);
        var fraction = Assert.Throws<ToolException>(() => SourceLoader.Load(vsdx, new SourceOptions { Dpi = 96.5 }));
        Assert.Equal(ErrorCodes.UsageError, fraction.Code);
        Assert.Contains("whole number", fraction.Message);
    }

    /// <summary>
    ///     Test that the sizing options act on the page's natural size, as for an image.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SizeOptions_ActOnTheNaturalSize()
    {
        // Arrange: the PDF, whose first page is 192x128 pixels at 96 dpi
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        using var scaled = SourceLoader.Load(path, new SourceOptions { Scale = 2 });
        using var wide = SourceLoader.Load(path, new SourceOptions { Width = 96 });
        using var tall = SourceLoader.Load(path, new SourceOptions { Height = 64 });
        using var denser = SourceLoader.Load(path, new SourceOptions { Dpi = 192, Scale = 0.5 });

        // Assert: scale multiplies, one side keeps the aspect ratio, and the scale applies to the size at the resolution
        Assert.Equal((384, 256), (scaled.Surface.Width, scaled.Surface.Height));
        Assert.Equal((96, 64), (wide.Surface.Width, wide.Surface.Height));
        Assert.Equal((96, 64), (tall.Surface.Width, tall.Surface.Height));
        Assert.Equal((192, 128), (denser.Surface.Width, denser.Surface.Height));
        Assert.Equal(Red, scaled.Surface[40, 40]);
    }

    /// <summary>
    ///     Test that a width and a height together name a box that the page is fitted inside and centered in, never
    ///     stretched, with the margin in the background color.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_WidthAndHeight_FitsThePageInsideTheBox()
    {
        // Arrange: the PDF page of 192x128 (3:2), asked for a 200x200 box
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act: with the default background, and with a green one
        using var white = SourceLoader.Load(path, new SourceOptions { Width = 200, Height = 200 });
        using var green = SourceLoader.Load(path, new SourceOptions { Width = 200, Height = 200, Background = new Rgba32(0, 255, 0, 255) });

        // Assert: the box size; the page is 200x133 in the middle, with white or green above and below
        Assert.Equal((200, 200), (white.Surface.Width, white.Surface.Height));
        Assert.Equal(White, white.Surface[100, 5]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), green.Surface[100, 5]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), green.Surface[100, 195]);
        Assert.Equal(Red, white.Surface[20, 70]);
        Assert.Equal(["render", "fit"], white.Operations.Select(o => o.Op));
        Assert.Equal(200, white.Operations[1].Fields.Single(f => f.Key == "width").Value);
        Assert.Equal(133, white.Operations[0].Fields.Single(f => f.Key == "height").Value);

        // A box with the aspect ratio of the page needs no fitting
        using var exact = SourceLoader.Load(path, new SourceOptions { Width = 96, Height = 64 });
        Assert.Equal(["render"], exact.Operations.Select(o => o.Op));
    }

    /// <summary>
    ///     Test that the background is what the page is rendered over, including no background at all.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    /// <param name="x">The x of a pixel the document does not paint.</param>
    /// <param name="y">The y of a pixel the document does not paint.</param>
    [Theory]
    [InlineData("pdf", 170, 20)]
    [InlineData("pptx", 400, 180)]
    [InlineData("vsdx", 300, 250)]
    public void SourceLoader_Load_Background_IsWhatThePageIsRenderedOver(string key, int x, int y)
    {
        // Arrange: a document
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);

        // Act: transparent, black, and a translucent color
        using var transparent = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(0, 0, 0, 0) });
        using var black = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(0, 0, 0, 255) });

        // Assert: the unpainted pixel shows the background, and the operation names it
        Assert.Equal(0, transparent.Surface[x, y].A);
        Assert.Equal(new Rgba32(0, 0, 0, 255), black.Surface[x, y]);
        Assert.Equal("#00000000", transparent.Operations[0].Fields.Single(f => f.Key == "background").Value);
        Assert.Equal("#000000", black.Operations[0].Fields.Single(f => f.Key == "background").Value);
    }

    /// <summary>
    ///     Test that a size beyond the maximum is refused before the page is rendered.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SizeBeyondMaximum_ThrowsSizeLimit()
    {
        // Arrange: the PDF page of 192x128 pixels
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act: 9600 dpi is 12800 pixels wide, and a width of 9000 is too large too
        var byDpi = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Dpi = 9600 }));
        var byWidth = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Width = 9000 }));

        // Assert: size-limit
        Assert.Equal(ErrorCodes.SizeLimit, byDpi.Code);
        Assert.Equal(ErrorCodes.SizeLimit, byWidth.Code);
    }

    /// <summary>
    ///     Test that one opened source loads several pages, and that the pages are the same as when each is loaded alone.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    [Theory]
    [MemberData(nameof(DocumentKeys))]
    public void SourceLoader_OpenPages_EveryPage_MatchesLoadingEachAlone(string key)
    {
        // Arrange: a document
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);
        var info = SourceLoader.Probe(path);

        // Act: load both pages from one opened source, in reverse order
        using var pages = SourceLoader.OpenPages(info, new SourceOptions());
        using var second = pages.ReadPage(2);
        using var first = pages.ReadPage(1);

        // Assert: each is what a load of that page alone gives
        using var firstAlone = SourceLoader.Load(path, new SourceOptions { Page = 1 });
        using var secondAlone = SourceLoader.Load(path, new SourceOptions { Page = 2 });
        SurfaceAssert.Equal(firstAlone.Surface, first.Surface);
        SurfaceAssert.Equal(secondAlone.Surface, second.Surface);
        Assert.Equal(ErrorCodes.PageOutOfRange, Assert.Throws<ToolException>(() => pages.ReadPage(3)).Code);
    }

    /// <summary>
    ///     Test that a picture the presentation renderer cannot draw is skipped and reported, with the page it is on.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PptxMetafilePicture_IsSkippedAndReported()
    {
        // Arrange: a presentation with an EMF picture on slide 1
        using var directory = new TempDirectory();
        var path = directory.Combine("deck.pptx");
        File.WriteAllBytes(path, FixtureFactory.CreatePptx(withMetafile: true));

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert: the rest of the slide is drawn, and the note says what was skipped
        Assert.Equal(Red, loaded.Surface[100, 180]);
        Assert.Equal(White, loaded.Surface[400, 180]);
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("pptx", "unsupported", "pptx-image-metafile", 1), (note.Source, note.Severity, note.Code, note.Page));
        Assert.False(string.IsNullOrWhiteSpace(note.Message));
        Assert.DoesNotContain('\n', note.Message);

        // Slide 2 has no such picture, so it has no notes
        using var second = SourceLoader.Load(path, new SourceOptions { Page = 2 });
        Assert.Empty(second.Diagnostics);
    }

    /// <summary>
    ///     Test that strict mode turns the skipped picture into an unsupported-feature error.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PptxMetafilePictureStrict_ThrowsUnsupportedFeature()
    {
        // Arrange: a presentation with an EMF picture on slide 1
        using var directory = new TempDirectory();
        var path = directory.Combine("deck.pptx");
        File.WriteAllBytes(path, FixtureFactory.CreatePptx(withMetafile: true));

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert: an expected error that names the file
        Assert.Equal(ErrorCodes.UnsupportedFeature, exception.Code);
        Assert.Contains(path, exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
    }

    /// <summary>
    ///     Test that a shape the presentation renderer does not implement fails the slide with unsupported-feature, with
    ///     or without strict mode.
    /// </summary>
    /// <param name="strict">The strict setting.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceLoader_Load_PptxUnsupportedShape_ThrowsUnsupportedFeature(bool strict)
    {
        // Arrange: a presentation with a preset shape that does not exist
        using var directory = new TempDirectory();
        var path = directory.Combine("deck.pptx");
        File.WriteAllBytes(path, FixtureFactory.CreatePptx(withUnsupportedShape: true));

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Strict = strict }));

        // Assert: reported as an expected error, not a library exception
        Assert.Equal(ErrorCodes.UnsupportedFeature, exception.Code);
        Assert.Contains("not supported", exception.Message);
        Assert.Contains("(pptx-preset-geometry)", exception.Message);
    }

    /// <summary>
    ///     Test that a drawing feature that is approximated is reported, and that strict mode refuses it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VsdxUnsupportedRow_IsReportedAndStrictRefuses()
    {
        // Arrange: a drawing with a geometry row of a kind that does not exist
        using var directory = new TempDirectory();
        var path = directory.Combine("drawing.vsdx");
        File.WriteAllBytes(path, FixtureFactory.CreateVsdx(withUnsupportedShape: true));

        // Act
        using var tolerant = SourceLoader.Load(path, new SourceOptions());
        var strict = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert: the page is drawn and one note names the feature; strict mode refuses it
        Assert.Equal(Red, tolerant.Surface[50, 50]);
        var note = Assert.Single(tolerant.Diagnostics);
        Assert.Equal(("vsdx", "unsupported", "vsdx-geometry-row-FutureRowKind", 1), (note.Source, note.Severity, note.Code, note.Page));
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.Contains("vsdx-geometry-row-FutureRowKind", strict.Message);
    }

    /// <summary>
    ///     Test that a PDF reports no notes and that strict mode changes nothing, because the library has no
    ///     diagnostics for PDF.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PdfStrict_HasNoDiagnostics()
    {
        // Arrange: the PDF
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        using var tolerant = SourceLoader.Load(path, new SourceOptions());
        using var strict = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert: the same pixels and no notes
        SurfaceAssert.Equal(tolerant.Surface, strict.Surface);
        Assert.Empty(strict.Diagnostics);
    }

    /// <summary>
    ///     Test that a PDF the library cannot read is an expected error: malformed content, and encryption it does not
    ///     support.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PdfWithBadContentOrEncryption_ThrowsExpectedErrors()
    {
        // Arrange: a page whose rectangle operator has too few operands, and an unsupported encryption handler
        using var directory = new TempDirectory();
        var badContent = directory.Combine("bad-content.pdf");
        File.WriteAllBytes(badContent, FixtureFactory.CreatePdf("1 0 0 rg 0 48 72 re f"));
        var encrypted = directory.Combine("encrypted.pdf");
        File.WriteAllBytes(encrypted, FixtureFactory.CreatePdf(encrypted: true));

        // Act
        var content = Assert.Throws<ToolException>(() => SourceLoader.Load(badContent, new SourceOptions()));
        var encryption = Assert.Throws<ToolException>(() => SourceLoader.Load(encrypted, new SourceOptions()));

        // Assert: corrupt input and an unsupported feature
        Assert.Equal(ErrorCodes.CorruptInput, content.Code);
        Assert.Equal(ErrorCodes.UnsupportedFeature, encryption.Code);
    }

    /// <summary>
    ///     Test that a package that is a valid archive but has broken parts is corrupt input.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PackageWithBrokenPart_ThrowsCorruptInput()
    {
        // Arrange: a presentation whose slide is not XML, and a drawing whose page is not XML
        using var directory = new TempDirectory();
        var pptx = directory.Combine("deck.pptx");
        File.WriteAllBytes(pptx, FixtureFactory.ReplaceZipEntry(FixtureFactory.CreatePptx(), "ppt/slides/slide1.xml", "this is <not xml"));
        var vsdx = directory.Combine("drawing.vsdx");
        File.WriteAllBytes(vsdx, FixtureFactory.ReplaceZipEntry(FixtureFactory.CreateVsdx(), "visio/pages/page1.xml", "this is <not xml"));
        var noSlides = directory.Combine("empty.pptx");
        File.WriteAllBytes(noSlides, FixtureFactory.ReplaceZipEntry(FixtureFactory.CreatePptx(), "ppt/presentation.xml", "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>"));

        // Act / Assert: each is corrupt input, wherever the damage is found (probing or rendering)
        foreach (var path in new[] { pptx, vsdx, noSlides })
        {
            var exception = Assert.Throws<ToolException>(() =>
            {
                using var loaded = SourceLoader.Load(path, new SourceOptions());
            });
            Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
            Assert.Contains(path, exception.Message);
        }
    }

    /// <summary>
    ///     Test that a document damaged in any way is reported as an expected error, never as an unexpected exception:
    ///     every page is probed and loaded.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    /// <param name="damage">How the file is damaged.</param>
    [Theory]
    [MemberData(nameof(DamagedDocuments))]
    public void SourceLoader_Load_DamagedDocument_ThrowsToolExceptionOrLoads(string key, string damage)
    {
        // Arrange: a document, damaged
        using var directory = new TempDirectory();
        var path = WriteDamaged(directory, key, damage);

        // Act: probe it and load every page; any exception that is not a ToolException fails the test
        var outcome = Record.Exception(() =>
        {
            var info = SourceLoader.Probe(path);
            using var pages = SourceLoader.OpenPages(info, new SourceOptions());
            for (var page = 1; page <= info.PageCount; page++)
            {
                pages.ReadPage(page).Dispose();
            }
        });

        // Assert: it loads (damage that does not matter) or is an expected error
        if (outcome != null)
        {
            var exception = Assert.IsType<ToolException>(outcome);
            Assert.Contains(
                exception.Code,
                new[] { ErrorCodes.CorruptInput, ErrorCodes.UnsupportedFeature, ErrorCodes.UnsupportedFormat });
        }
    }

    /// <summary>
    ///     Test that every public member rejects null arguments.
    /// </summary>
    [Fact]
    public void SourceLoader_OpenPages_NullArguments_ThrowArgumentNullException()
    {
        // Arrange / Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => SourceLoader.OpenPages(null!, new SourceOptions()));
        Assert.Throws<ArgumentNullException>(() => SourceLoader.CheckPage(null!, 1));
    }
}
