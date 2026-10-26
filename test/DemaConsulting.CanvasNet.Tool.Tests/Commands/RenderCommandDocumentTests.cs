// cspell:ignore canvasnet pdf pptx vsdx dpi emf jpeg jpg tif tiff
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the RenderCommand class with documents (PDF, PPTX, and VSDX): page selection, every page,
///     resolution, strict mode, and the result.
/// </summary>
[Collection("Sequential")]
public class RenderCommandDocumentTests
{
    /// <summary>
    ///     Runs the render command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    private static string Run(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new RenderCommand();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Runs the render command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new RenderCommand();
        return Assert.Throws<ToolException>(() => command.Run(context, args));
    }

    /// <summary>
    ///     Test that a document renders its first page by default, with a text line that names the page.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    /// <param name="width">The expected width.</param>
    /// <param name="height">The expected height.</param>
    [Theory]
    [InlineData("pdf", 192, 128)]
    [InlineData("pptx", 480, 360)]
    [InlineData("vsdx", 384, 288)]
    public void RenderCommand_Run_DocumentDefaults_WritesFirstPage(string key, int width, int height)
    {
        // Arrange: a document
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, key);
        var output = directory.Combine("out.png");

        // Act
        var stdOut = Run(false, input, "-o", output);

        // Assert: one line that names the output, its size, and the page; the file has that size
        var bytes = new FileInfo(output).Length;
        Assert.Equal($"wrote {output} ({width}x{height} png, {bytes} bytes) from {input} page 1{Environment.NewLine}", stdOut);
        using var image = PngCodec.Load(output);
        Assert.Equal((width, height), (image.Width, image.Height));
    }

    /// <summary>
    ///     Test that the JSON result of a document names the page, the page count, and the operations.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_DocumentJson_DescribesPageAndOperations()
    {
        // Arrange: the PDF
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pdf");
        var output = directory.Combine("out.png");

        // Act: render page 2 at 120 dpi
        var stdOut = Run(true, input, "--page", "2", "--dpi", "120", "-o", output);

        // Assert: the document's own fields
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        var inputObject = root.GetProperty("input");
        Assert.Equal("pdf", inputObject.GetProperty("format").GetString());
        Assert.Equal(2, inputObject.GetProperty("page").GetInt32());
        Assert.Equal(2, inputObject.GetProperty("pageCount").GetInt32());
        Assert.Equal(160, inputObject.GetProperty("natural").GetProperty("width").GetInt32());
        Assert.Equal(240, inputObject.GetProperty("natural").GetProperty("height").GetInt32());
        var render = root.GetProperty("operations")[0];
        Assert.Equal("render", render.GetProperty("op").GetString());
        Assert.Equal(2, render.GetProperty("page").GetInt32());
        Assert.Equal(120, render.GetProperty("dpi").GetDouble());
        Assert.Equal(160, render.GetProperty("width").GetInt32());
        Assert.Equal("#FFFFFF", render.GetProperty("background").GetString());
        var written = root.GetProperty("outputs")[0];
        Assert.Equal(2, written.GetProperty("page").GetInt32());
        Assert.Equal(160, written.GetProperty("width").GetInt32());
        Assert.Equal(0, root.GetProperty("diagnostics").GetArrayLength());
    }

    /// <summary>
    ///     Test that a raster image does not get document fields in its result.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_RasterJson_HasNoPageFields()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var stdOut = Run(true, input, "-o", directory.Combine("out.png"));

        // Assert: neither page nor pageCount in the input, nor a page in the output
        using var document = JsonDocument.Parse(stdOut);
        Assert.False(document.RootElement.GetProperty("input").TryGetProperty("page", out _));
        Assert.False(document.RootElement.GetProperty("input").TryGetProperty("pageCount", out _));
        Assert.False(document.RootElement.GetProperty("outputs")[0].TryGetProperty("page", out _));
    }

    /// <summary>
    ///     Test that every page can be rendered with a page number in the output name, in one run.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_AllPagesWithPlaceholder_WritesOneFilePerPage()
    {
        // Arrange: the two-page PDF
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pdf");
        var template = directory.Combine("sub", "page-{page}.png");

        // Act
        var stdOut = Run(true, input, "--all-pages", "-o", template);

        // Assert: one file per page, each of its own size and colors
        using var first = PngCodec.Load(directory.Combine("sub", "page-1.png"));
        using var second = PngCodec.Load(directory.Combine("sub", "page-2.png"));
        Assert.Equal((192, 128), (first.Width, first.Height));
        Assert.Equal((128, 192), (second.Width, second.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), first[20, 20]);
        Assert.Equal(new Rgba32(255, 255, 0, 255), second[10, 10]);

        // The result lists both outputs with their pages, and every operation names its page
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.False(root.GetProperty("input").TryGetProperty("page", out _));
        Assert.Equal(2, root.GetProperty("input").GetProperty("pageCount").GetInt32());
        var outputs = root.GetProperty("outputs").EnumerateArray().ToList();
        Assert.Equal([1, 2], outputs.Select(o => o.GetProperty("page").GetInt32()));
        Assert.EndsWith("page-2.png", outputs[1].GetProperty("path").GetString());
        Assert.All(root.GetProperty("operations").EnumerateArray(), o => Assert.True(o.TryGetProperty("page", out _)));
    }

    /// <summary>
    ///     Test that every page can be written into a directory, with names made from the input name and the page number.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_AllPagesToDirectory_NamesFilesAfterInputAndPage()
    {
        // Arrange: a presentation, and a directory that exists
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pptx");
        var existing = directory.Combine("existing");
        Directory.CreateDirectory(existing);

        // Act: one existing directory, and one that is named with a trailing separator and does not exist yet
        Run(false, input, "--all-pages", "-o", existing);
        Run(false, input, "--all-pages", "--format", "bmp", "-o", directory.Combine("made") + Path.DirectorySeparatorChar);

        // Assert: files named source-1 and source-2 with the extension of the format
        Assert.True(File.Exists(Path.Combine(existing, "source-1.png")));
        Assert.True(File.Exists(Path.Combine(existing, "source-2.png")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "made", "source-1.bmp")));
        Assert.True(File.Exists(Path.Combine(directory.Path, "made", "source-2.bmp")));
    }

    /// <summary>
    ///     Test that every page needs a page number or a directory in the output name, and that nothing is written
    ///     when it does not have one.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_AllPagesWithoutPlaceholder_ThrowsUsageError()
    {
        // Arrange: a two-page document
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "vsdx");
        var output = directory.Combine("out.png");

        // Act
        var exception = RunFailing(input, "--all-pages", "-o", output);

        // Assert: a usage error that says what to do, and no output
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("{page}", exception.Hint);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a single image accepts every page without a page number in the output name, because it has one page.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_AllPagesOfSingleImage_WritesTheOutputAsGiven()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act
        Run(false, input, "--all-pages", "-o", output);

        // Assert: one file, exactly where it was asked for
        Assert.True(File.Exists(output));
        Assert.Equal(2, Directory.GetFiles(directory.Path).Length);
    }

    /// <summary>
    ///     Test that a placeholder is replaced for a single page too.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PlaceholderWithPage_IsReplacedByThatPage()
    {
        // Arrange: a document
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        Run(false, input, "--page", "2", "-o", directory.Combine("p{page}.png"));

        // Assert: the file for page 2 only
        Assert.True(File.Exists(directory.Combine("p2.png")));
        Assert.False(File.Exists(directory.Combine("p1.png")));
    }

    /// <summary>
    ///     Test that a mistake in one of the outputs is found before any page is written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_AllPagesWithOneExistingOutputAndNoClobber_WritesNothing()
    {
        // Arrange: a two-page document whose second output exists
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pdf");
        File.WriteAllText(directory.Combine("out-2.png"), "keep me");

        // Act
        var exception = RunFailing(input, "--all-pages", "--no-clobber", "-o", directory.Combine("out-{page}.png"));

        // Assert: output-exists, the existing file untouched, and no file for page 1
        Assert.Equal(ErrorCodes.OutputExists, exception.Code);
        Assert.Equal("keep me", File.ReadAllText(directory.Combine("out-2.png")));
        Assert.False(File.Exists(directory.Combine("out-1.png")));
    }

    /// <summary>
    ///     Test that a page that does not exist, a page and every page together, and a page of zero are refused.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_BadPageOptions_ThrowExpectedErrors()
    {
        // Arrange: a two-page document and an output
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pptx");
        var output = directory.Combine("out.png");

        // Act
        var outOfRange = RunFailing(input, "--page", "3", "-o", output);
        var both = RunFailing(input, "--page", "1", "--all-pages", "-o", output);
        var zero = RunFailing(input, "--page", "0", "-o", output);
        var notNumber = RunFailing(input, "--page", "first", "-o", output);

        // Assert: the codes, and nothing written
        Assert.Equal(ErrorCodes.PageOutOfRange, outOfRange.Code);
        Assert.Equal(ErrorCodes.UsageError, both.Code);
        Assert.Equal(ErrorCodes.UsageError, zero.Code);
        Assert.Equal(ErrorCodes.UsageError, notNumber.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the resolution and the size options apply to a document page, and that a bad resolution is refused.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_DpiAndSizeOptions_SizeThePage()
    {
        // Arrange: the PDF and the drawing
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var vsdx = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act
        Run(false, pdf, "--dpi", "192", "-o", directory.Combine("dpi.png"));
        Run(false, pdf, "--width", "96", "-o", directory.Combine("width.png"));
        Run(false, pdf, "--scale", "0.5", "-o", directory.Combine("scale.png"));
        Run(false, pdf, "--size", "200x200", "--background", "#00FF00", "-o", directory.Combine("size.png"));
        var fraction = RunFailing(vsdx, "--dpi", "96.5", "-o", directory.Combine("bad.png"));
        var tooLow = RunFailing(pdf, "--dpi", "0", "-o", directory.Combine("bad.png"));
        var tooLarge = RunFailing(pdf, "--dpi", "2400", "--scale", "2", "-o", directory.Combine("bad.png"));
        var tooHigh = RunFailing(pdf, "--dpi", "9000", "-o", directory.Combine("bad.png"));

        // Assert: the sizes; the box of --size is filled with the background around the fitted page
        Assert.Equal((384, 256), Size(directory.Combine("dpi.png")));
        Assert.Equal((96, 64), Size(directory.Combine("width.png")));
        Assert.Equal((96, 64), Size(directory.Combine("scale.png")));
        Assert.Equal((200, 200), Size(directory.Combine("size.png")));
        using (var boxed = PngCodec.Load(directory.Combine("size.png")))
        {
            Assert.Equal(new Rgba32(0, 255, 0, 255), boxed[100, 2]);
            Assert.Equal(new Rgba32(255, 0, 0, 255), boxed[20, 70]);
        }

        Assert.Equal(ErrorCodes.UsageError, fraction.Code);
        Assert.Equal(ErrorCodes.UsageError, tooLow.Code);
        Assert.Equal(ErrorCodes.UsageError, tooHigh.Code);
        Assert.Equal(ErrorCodes.SizeLimit, tooLarge.Code);
    }

    /// <summary>
    ///     Test that a transparent background keeps the transparency of a page in PNG, and that JPEG flattens it.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_TransparentBackground_KeepsAlphaInPngAndFlattensJpeg()
    {
        // Arrange: the PDF
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        Run(false, input, "--background", "transparent", "-o", directory.Combine("t.png"));
        Run(false, input, "--background", "transparent", "-o", directory.Combine("t.jpg"));

        // Assert: the unpainted corner is transparent in the PNG and white in the JPEG
        using var png = PngCodec.Load(directory.Combine("t.png"));
        using var jpeg = JpegCodec.Load(directory.Combine("t.jpg"));
        Assert.Equal(0, png[170, 20].A);
        Assert.InRange(jpeg[170, 20].R, 240, 255);
    }

    /// <summary>
    ///     Test that a skipped picture is reported in the result with its page, and that strict mode fails instead.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    [Theory]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    public void RenderCommand_Run_UnsupportedFeature_IsReportedOrFailsWhenStrict(string key)
    {
        // Arrange: a document with something the renderer cannot draw on page 1
        using var directory = new TempDirectory();
        var input = directory.Combine("doc." + key);
        File.WriteAllBytes(input, key == "pptx" ? FixtureFactory.CreatePptx(withMetafile: true) : FixtureFactory.CreateVsdx(withUnsupportedShape: true));
        var output = directory.Combine("out.png");

        // Act
        var stdOut = Run(true, input, "-o", output);
        var strict = RunFailing(input, "--strict", "-o", directory.Combine("strict.png"));

        // Assert: the note is in the result, with its source and page; strict mode is an unsupported-feature error
        using var document = JsonDocument.Parse(stdOut);
        var note = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(key, note.GetProperty("source").GetString());
        Assert.Equal("unsupported", note.GetProperty("severity").GetString());
        Assert.StartsWith(key + "-", note.GetProperty("code").GetString());
        Assert.Equal(1, note.GetProperty("page").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(note.GetProperty("message").GetString()));
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.False(File.Exists(directory.Combine("strict.png")));
    }

    /// <summary>
    ///     Test that every page of a document is rendered into the same bytes on a second run.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    [Theory]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    public void RenderCommand_Run_AllPagesTwice_WritesIdenticalBytes(string key)
    {
        // Arrange: a document
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, key);

        // Act: render every page twice, to PNG and JPEG
        foreach (var extension in new[] { "png", "jpg" })
        {
            Run(false, input, "--all-pages", "--dpi", "72", "-o", directory.Combine("a", "p{page}." + extension));
            Run(false, input, "--all-pages", "--dpi", "72", "-o", directory.Combine("b", "p{page}." + extension));

            // Assert: identical bytes for each page
            foreach (var page in new[] { 1, 2 })
            {
                Assert.Equal(
                    File.ReadAllBytes(directory.Combine("a", $"p{page}.{extension}")),
                    File.ReadAllBytes(directory.Combine("b", $"p{page}.{extension}")));
            }
        }
    }

    /// <summary>
    ///     Test that a damaged document is an expected error and leaves no output.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    [Theory]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    public void RenderCommand_Run_TruncatedDocument_ThrowsCorruptInputAndWritesNothing(string key)
    {
        // Arrange: a document cut after 300 bytes
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, key);
        File.WriteAllBytes(input, File.ReadAllBytes(input)[..300]);
        var output = directory.Combine("out.png");

        // Act
        var exception = RunFailing(input, "-o", output);

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Reads the size of a PNG file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The width and height.</returns>
    private static (int Width, int Height) Size(string path)
    {
        using var image = PngCodec.Load(path);
        return (image.Width, image.Height);
    }
}
