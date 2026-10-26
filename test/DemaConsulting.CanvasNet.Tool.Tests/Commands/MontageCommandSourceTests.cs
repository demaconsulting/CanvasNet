// cspell:ignore canvasnet pptx scad pdf svg
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the MontageCommand class with different kinds of input: the pages of documents, mixed kinds,
///     vector inputs, models, and notes.
/// </summary>
[Collection("Sequential")]
public class MontageCommandSourceTests
{
    /// <summary>
    ///     Writes the two-slide presentation of the fixtures.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="withMetafile"><see langword="true"/> for a slide with a picture the renderer skips with a note.</param>
    /// <returns>The path.</returns>
    private static string WritePptx(TempDirectory directory, bool withMetafile = false)
    {
        var path = directory.Combine("deck.pptx");
        File.WriteAllBytes(path, FixtureFactory.CreatePptx(withMetafile: withMetafile));
        return path;
    }

    /// <summary>
    ///     Determines whether a pixel is the red of the first slide.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns><see langword="true"/> for a strong red.</returns>
    private static bool IsRed(Rgba32 pixel)
    {
        return pixel.R > 200 && pixel.G < 60 && pixel.B < 60;
    }

    /// <summary>
    ///     Determines whether a pixel is the blue of the second slide.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns><see langword="true"/> for a strong blue.</returns>
    private static bool IsBlue(Rgba32 pixel)
    {
        return pixel.B > 200 && pixel.R < 60 && pixel.G < 60;
    }

    /// <summary>
    ///     Test that every page of a document is added in order, after the inputs that precede it.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_AllPages_AddsEveryPageInOrderAfterPrecedingInputs()
    {
        // Arrange: a PNG and the two-slide presentation
        using var directory = new TempDirectory();
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, MontageCommandTests.Blue);
        var deck = WritePptx(directory);
        var output = directory.Combine("sheet.png");

        // Act
        var stdOut = MontageCommandTests.Run(true, png, deck, "--all-pages", "-o", output);

        // Assert: three cells, in order: the PNG, slide 1, slide 2; the documents' cells name their page and page count
        using var document = JsonDocument.Parse(stdOut);
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToList();
        Assert.Equal([png, deck, deck], cells.Select(c => c.GetProperty("input").GetString()));
        Assert.False(cells[0].TryGetProperty("page", out _));
        Assert.Equal([1, 2], cells.Skip(1).Select(c => c.GetProperty("page").GetInt32()));
        Assert.Equal([2, 2], cells.Skip(1).Select(c => c.GetProperty("pageCount").GetInt32()));
        Assert.Equal(2, document.RootElement.GetProperty("sheet").GetProperty("columns").GetInt32());

        // Assert: slide 1 has its red left half and a white right half; slide 2 has its blue top half and a white bottom half
        using var sheet = PngCodec.Load(output);
        var (x1, y1) = (cells[1].GetProperty("x").GetInt32(), cells[1].GetProperty("y").GetInt32());
        var (x2, y2) = (cells[2].GetProperty("x").GetInt32(), cells[2].GetProperty("y").GetInt32());
        Assert.True(IsRed(sheet[x1 + 40, y1 + 128]));
        Assert.Equal(MontageCommandTests.White, sheet[x1 + 220, y1 + 128]);
        Assert.True(IsBlue(sheet[x2 + 128, y2 + 64]));
        Assert.Equal(MontageCommandTests.White, sheet[x2 + 128, y2 + 160]);
    }

    /// <summary>
    ///     Test that a page number applies to every document, and is ignored by a single image.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_PageOption_UsesThatPageOfEachDocument()
    {
        // Arrange: a presentation, a PDF, and a PNG
        using var directory = new TempDirectory();
        var deck = WritePptx(directory);
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, MontageCommandTests.Red);

        // Act
        using var document = JsonDocument.Parse(MontageCommandTests.Run(true, deck, pdf, png, "--page", "2", "--cell", "64x64", "-o", directory.Combine("sheet.png")));

        // Assert
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToList();
        Assert.Equal(2, cells[0].GetProperty("page").GetInt32());
        Assert.Equal(2, cells[1].GetProperty("page").GetInt32());
        Assert.False(cells[2].TryGetProperty("page", out _));
    }

    /// <summary>
    ///     Test that a page and every page together are a usage error.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_PageAndAllPages_ThrowUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var deck = WritePptx(directory);

        // Act
        var exception = MontageCommandTests.RunFailing(deck, "--page", "1", "--all-pages", "-o", directory.Combine("sheet.png"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
    }

    /// <summary>
    ///     Test that a page beyond the end of a document is an error that names the file.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_PageBeyondDocument_ThrowsPageOutOfRangeNamingTheFile()
    {
        // Arrange
        using var directory = new TempDirectory();
        var deck = WritePptx(directory);
        var output = directory.Combine("sheet.png");

        // Act
        var exception = MontageCommandTests.RunFailing(deck, "--page", "3", "-o", output);

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
        Assert.Contains(deck, exception.Message);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a page number is ignored by an input that is a single image.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_PageForSingleImage_IsIgnored()
    {
        // Arrange
        using var directory = new TempDirectory();
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, MontageCommandTests.Red);
        var output = directory.Combine("sheet.png");

        // Act
        MontageCommandTests.Run(false, png, "--page", "3", "--dpi", "300", "-o", output);

        // Assert
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that images, drawings, documents, and models can be mixed, each drawing its cell.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_MixedKinds_PngSvgPdfPptxScad_EachCellDrawn()
    {
        // Arrange: one input of each kind that is not a raster other than PNG
        using var directory = new TempDirectory();
        var inputs = new[] { "png", "svg", "pdf", "pptx", "scad" }.Select(key => FixtureFactory.WriteSource(directory.Path, key)).ToList();
        var output = directory.Combine("sheet.png");

        // Act
        var stdOut = MontageCommandTests.Run(true, [.. inputs, "--cell", "64x64", "-o", output]);

        // Assert: five cells of five formats; every cell holds pixels other than the background
        using var document = JsonDocument.Parse(stdOut);
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToList();
        Assert.Equal(["png", "svg", "pdf", "pptx", "scad"], cells.Select(c => c.GetProperty("format").GetString()));
        using var sheet = PngCodec.Load(output);
        foreach (var cell in cells)
        {
            var (x, y) = (cell.GetProperty("x").GetInt32(), cell.GetProperty("y").GetInt32());
            var drawn = false;
            for (var row = y; row < y + 64 && !drawn; row++)
            {
                drawn = sheet.GetRowSpan(row).Slice(x, 64).ToArray().Any(pixel => pixel != MontageCommandTests.White);
            }

            Assert.True(drawn, $"The cell of {cell.GetProperty("input").GetString()} is empty");
        }
    }

    /// <summary>
    ///     Test that a vector input is rendered at the size of the cell, and a raster input is loaded at its own size.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_VectorCell_IsRenderedNativelyAtCellSize()
    {
        // Arrange: an SVG, a presentation, and a small PNG
        using var directory = new TempDirectory();
        var svg = FixtureFactory.WriteSource(directory.Path, "svg");
        var deck = WritePptx(directory);
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, MontageCommandTests.Red);

        // Act
        using var document = JsonDocument.Parse(MontageCommandTests.Run(true, svg, deck, png, "--cell", "96x72", "-o", directory.Combine("sheet.png")));

        // Assert: the vector inputs were rendered at 96x72, the raster input was loaded at 8x6 and fitted by the sheet
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToList();
        Assert.Equal((96, 72), (cells[0].GetProperty("loadedWidth").GetInt32(), cells[0].GetProperty("loadedHeight").GetInt32()));
        Assert.Equal((96, 72), (cells[1].GetProperty("loadedWidth").GetInt32(), cells[1].GetProperty("loadedHeight").GetInt32()));
        Assert.Equal((8, 6), (cells[2].GetProperty("loadedWidth").GetInt32(), cells[2].GetProperty("loadedHeight").GetInt32()));
    }

    /// <summary>
    ///     Test that model options are a usage error when no input is a model, and apply to a model when there is one.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_ScadOptionsWithoutModel_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, MontageCommandTests.Red);
        var scad = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var exception = MontageCommandTests.RunFailing(png, "--view", "top", "-o", directory.Combine("sheet.png"));
        MontageCommandTests.Run(false, png, scad, "--view", "top", "--cell", "64x64", "-o", directory.Combine("sheet.png"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("only apply to an OpenSCAD model", exception.Message);
        Assert.True(File.Exists(directory.Combine("sheet.png")));
    }

    /// <summary>
    ///     Test that a note about an unsupported feature is listed with its input and page.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_Diagnostics_AreListedWithTheirInputAndPage()
    {
        // Arrange: a presentation whose first slide has a picture the renderer skips
        using var directory = new TempDirectory();
        var deck = WritePptx(directory, withMetafile: true);
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, MontageCommandTests.Red);

        // Act
        using var document = JsonDocument.Parse(MontageCommandTests.Run(true, png, deck, "--cell", "64x64", "-o", directory.Combine("sheet.png")));

        // Assert
        var note = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(deck, note.GetProperty("input").GetString());
        Assert.Equal(1, note.GetProperty("page").GetInt32());
        Assert.Equal("pptx", note.GetProperty("source").GetString());
        Assert.Equal("unsupported", note.GetProperty("severity").GetString());
        Assert.False(string.IsNullOrWhiteSpace(note.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(note.GetProperty("message").GetString()));
    }

    /// <summary>
    ///     Test that the labels of document pages and of images are the file name, and the page of a document.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_AllPagesWithLabels_LabelsNamePages()
    {
        // Arrange
        using var directory = new TempDirectory();
        var deck = WritePptx(directory);
        var png = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, MontageCommandTests.Red);

        // Act
        using var document = JsonDocument.Parse(MontageCommandTests.Run(true, png, deck, "--all-pages", "--labels", "--cell", "64x64", "-o", directory.Combine("sheet.png")));

        // Assert
        Assert.Equal(
            ["a.png", "deck.pptx p1", "deck.pptx p2"],
            document.RootElement.GetProperty("cells").EnumerateArray().Select(c => c.GetProperty("label").GetString()));
    }
}
