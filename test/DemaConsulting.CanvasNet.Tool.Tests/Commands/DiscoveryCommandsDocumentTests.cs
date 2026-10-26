// cspell:ignore canvasnet pdf pptx vsdx dpi
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the info command of the DiscoveryCommands unit with documents (PDF, PPTX, and VSDX): the page
///     count, the size of a page, and the page options.
/// </summary>
[Collection("Sequential")]
public class DiscoveryCommandsDocumentTests
{
    /// <summary>
    ///     Runs the info command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    private static string RunInfo(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new DiscoveryCommands.Info();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Runs the info command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunInfoFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Info();
        return Assert.Throws<ToolException>(() => command.Run(context, args));
    }

    /// <summary>
    ///     Test the text description of a PDF: one line for the file and one for the first page, with the rotation.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_PdfText_DescribesFileAndFirstPage()
    {
        // Arrange: the two-page PDF
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        var text = RunInfo(false, path);

        // Assert: the file line with the page count, then page 1 with its size in pixels and points and its rotation
        var bytes = new FileInfo(path).Length;
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            [
                $"{path}: pdf, 2 pages, {bytes} bytes",
                "  page 1: 192x128 px at 96 dpi (144x96 pt), rotation 0"
            ],
            lines);
    }

    /// <summary>
    ///     Test that a page, or every page, can be chosen, and that a slide and a named page are described as such.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_PageOptions_ChoosePagesToDescribe()
    {
        // Arrange: the three documents
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var pptx = FixtureFactory.WriteSource(directory.Path, "pptx");
        var vsdx = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act
        var pdfSecond = RunInfo(false, pdf, "--page", "2");
        var pptxAll = RunInfo(false, pptx, "--all-pages");
        var vsdxAll = RunInfo(false, vsdx, "--all-pages");

        // Assert: PDF page 2 is rotated and shows its displayed size
        Assert.Contains("  page 2: 128x192 px at 96 dpi (96x144 pt), rotation 90", pdfSecond);
        Assert.DoesNotContain("page 1", pdfSecond);

        // A presentation has slides, with no names and no rotation
        Assert.Contains(", 2 slides,", pptxAll);
        Assert.Contains("  slide 1: 480x360 px at 96 dpi (360x270 pt)" + Environment.NewLine, pptxAll + Environment.NewLine);
        Assert.Contains("  slide 2: 480x360 px at 96 dpi (360x270 pt)", pptxAll);

        // A drawing has named pages of their own sizes
        Assert.Contains("  page 1 \"First\": 384x288 px at 96 dpi (288x216 pt)", vsdxAll);
        Assert.Contains("  page 2 \"Second\": 288x384 px at 96 dpi (216x288 pt)", vsdxAll);
    }

    /// <summary>
    ///     Test the JSON description of a document: the page count and the pages that were asked for.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_DocumentJson_ListsPageCountAndPages()
    {
        // Arrange: the PDF and the drawing
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var vsdx = FixtureFactory.WriteSource(directory.Path, "vsdx");

        // Act
        var pdfDefault = JsonDocument.Parse(RunInfo(true, pdf)).RootElement;
        var pdfAll = JsonDocument.Parse(RunInfo(true, pdf, "--all-pages")).RootElement;
        var vsdxSecond = JsonDocument.Parse(RunInfo(true, vsdx, "--page", "2")).RootElement;

        // Assert: no image object; the first page by default, both with --all-pages, and the one chosen
        Assert.False(pdfDefault.TryGetProperty("image", out _));
        Assert.Equal("pdf", pdfDefault.GetProperty("input").GetProperty("format").GetString());
        var document = pdfDefault.GetProperty("document");
        Assert.Equal(2, document.GetProperty("pageCount").GetInt32());
        var first = Assert.Single(document.GetProperty("pages").EnumerateArray());
        Assert.Equal(
            "{\"page\":1,\"width\":192,\"height\":128,\"widthPt\":144,\"heightPt\":96,\"rotation\":0}",
            first.GetRawText());
        var all = pdfAll.GetProperty("document").GetProperty("pages").EnumerateArray().ToList();
        Assert.Equal(
            "{\"page\":2,\"width\":128,\"height\":192,\"widthPt\":96,\"heightPt\":144,\"rotation\":90}",
            all[1].GetRawText());
        var named = Assert.Single(vsdxSecond.GetProperty("document").GetProperty("pages").EnumerateArray());
        Assert.Equal(
            "{\"page\":2,\"width\":288,\"height\":384,\"widthPt\":216,\"heightPt\":288,\"name\":\"Second\"}",
            named.GetRawText());
    }

    /// <summary>
    ///     Test that a page that does not exist, and both page options together, are errors.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_BadPageOptions_ThrowExpectedErrors()
    {
        // Arrange: a two-page document and a single image
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var png = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var outOfRange = RunInfoFailing(pdf, "--page", "3");
        var both = RunInfoFailing(pdf, "--page", "1", "--all-pages");
        var imagePage = RunInfoFailing(png, "--page", "2");

        // Assert: the codes
        Assert.Equal(ErrorCodes.PageOutOfRange, outOfRange.Code);
        Assert.Equal(ErrorCodes.UsageError, both.Code);
        Assert.Equal(ErrorCodes.PageOutOfRange, imagePage.Code);
    }

    /// <summary>
    ///     Test that a single image accepts the page options and describes itself as before.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_PageOptionsOnImage_DescribeTheImage()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var png = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        var plain = RunInfo(false, png);

        // Assert: page 1 and every page give the same description
        Assert.Equal(plain, RunInfo(false, png, "--page", "1"));
        Assert.Equal(plain, RunInfo(false, png, "--all-pages"));
    }

    /// <summary>
    ///     Test that a damaged document is an expected error.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    [Theory]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    public void DiscoveryCommands_Info_TruncatedDocument_ThrowsCorruptInput(string key)
    {
        // Arrange: a document cut after 300 bytes
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);
        File.WriteAllBytes(path, File.ReadAllBytes(path)[..300]);

        // Act / Assert: corrupt input
        Assert.Equal(ErrorCodes.CorruptInput, RunInfoFailing(path).Code);
    }
}
