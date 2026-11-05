// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the MontageBuilder unit: composing images and document pages into one contact sheet.
/// </summary>
public class MontageBuilderTests
{
    /// <summary>
    ///     Writes a gradient PNG and probes it.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo CreatePng(TempDirectory directory, string name)
    {
        var path = directory.Combine(name);
        using var surface = FixtureFactory.CreateGradient();
        PngCodec.Save(surface, path);
        return SourceLoader.Probe(path);
    }

    /// <summary>
    ///     Creates a request with small cells.
    /// </summary>
    /// <param name="output">The output path.</param>
    /// <param name="labels"><see langword="true"/> to label the cells.</param>
    /// <param name="columns">The number of columns, or 0 for as square as possible.</param>
    /// <param name="cell">The width and height of a cell.</param>
    /// <returns>The request.</returns>
    private static MontageRequest CreateRequest(string output, bool labels = false, int columns = 0, int cell = 32)
    {
        var sheet = new SheetOptions(
            columns, cell, cell, 4, 4, new Rgba32(255, 255, 255, 255), labels, FitMode.Contain, ResamplingMode.Auto);
        return new MontageRequest(output, null, false, null, null, false, true, sheet, WriteOptions.Default);
    }

    /// <summary>
    ///     Test that images are laid out row by row and the sheet is written at the size of the layout.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_ThreeImages_LaysOutRowByRowAndWritesTheSheet()
    {
        // Arrange
        using var directory = new TempDirectory();
        SourceInfo[] infos = [CreatePng(directory, "a.png"), CreatePng(directory, "b.png"), CreatePng(directory, "c.png")];
        var output = directory.Combine("sheet.png");

        // Act
        var result = MontageBuilder.Execute(infos, CreateRequest(output));

        // Assert: 2 columns by 2 rows of 32 pixel cells with 4 pixels of spacing and padding
        Assert.Equal((3, 2, 2), (result.Layout.Count, result.Layout.Columns, result.Layout.Rows));
        Assert.Equal((4 + 32 + 4 + 32 + 4, 4 + 32 + 4 + 32 + 4), (result.Layout.Width, result.Layout.Height));
        Assert.Equal((result.Layout.Width, result.Layout.Height), (result.Written.Width, result.Written.Height));
        Assert.Equal(output, result.Written.Path);
        Assert.True(File.Exists(output));

        // The second cell is in the first row, to the right of the first
        Assert.Equal([0, 1, 2], result.Cells.Select(c => c.Index));
        Assert.Equal(result.Cells[0].Y, result.Cells[1].Y);
        Assert.True(result.Cells[1].X > result.Cells[0].X);
        Assert.True(result.Cells[2].Y > result.Cells[0].Y);
    }

    /// <summary>
    ///     Test that the label of a cell is the file name, and that the labels make the cells taller.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_Labels_AreTheFileNames()
    {
        // Arrange
        using var directory = new TempDirectory();
        SourceInfo[] infos = [CreatePng(directory, "first.png"), CreatePng(directory, "second.png")];

        // Act
        var plain = MontageBuilder.Execute(infos, CreateRequest(directory.Combine("plain.png")));
        var labeled = MontageBuilder.Execute(infos, CreateRequest(directory.Combine("labeled.png"), true));

        // Assert
        Assert.Equal(["first.png", "second.png"], labeled.Cells.Select(c => c.Label));
        Assert.Equal(ContactSheet.LabelHeight, labeled.Layout.LabelHeight);
        Assert.True(labeled.Layout.Height > plain.Layout.Height);
    }

    /// <summary>
    ///     Test that a page of a document is labeled with its page number and reported with its page.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_DocumentPage_IsLabeledWithThePageNumber()
    {
        // Arrange: a PDF, whose first page is the cell
        using var directory = new TempDirectory();
        var info = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "pdf"));

        // Act
        var result = MontageBuilder.Execute([info], CreateRequest(directory.Combine("sheet.png"), true));

        // Assert
        var cell = Assert.Single(result.Cells);
        Assert.Equal("source.pdf p1", cell.Label);
        Assert.Equal(1, cell.Page);
        Assert.Equal(0, cell.InputIndex);
    }

    /// <summary>
    ///     Test that a sheet that would exceed the maximum size is refused before any input is decoded.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_SheetTooLarge_ThrowsSizeLimit()
    {
        // Arrange: 64 columns of 200 pixel cells is wider than the maximum
        using var directory = new TempDirectory();
        var infos = Enumerable.Range(0, 64).Select(_ => CreatePng(directory, "a.png")).ToList();

        // Act
        var error = Assert.Throws<ViewerException>(() => MontageBuilder.Execute(infos, CreateRequest(directory.Combine("sheet.png"), false, 64, 200)));

        // Assert
        Assert.Equal(ErrorCodes.SizeLimit, error.Code);
        Assert.False(File.Exists(directory.Combine("sheet.png")));
    }

    /// <summary>
    ///     Test that a sheet which would overwrite an input is refused.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_OutputIsAnInput_ThrowsSameInputOutput()
    {
        // Arrange
        using var directory = new TempDirectory();
        var info = CreatePng(directory, "a.png");

        // Act
        var error = Assert.Throws<ViewerException>(() => MontageBuilder.Execute([info], CreateRequest(info.Path)));

        // Assert
        Assert.Equal(ErrorCodes.SameInputOutput, error.Code);
    }

    /// <summary>
    ///     Test that a montage of no inputs is a caller error.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_NoInputs_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => MontageBuilder.Execute([], CreateRequest("sheet.png")));
        Assert.Throws<ArgumentNullException>(() => MontageBuilder.Execute(null!, CreateRequest("sheet.png")));
    }

    /// <summary>
    ///     Test that an explicit format writes a sheet to an output without an extension.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_ExplicitFormatAndExtensionlessOutput_WritesThatFormat()
    {
        // Arrange: a sheet named without an extension, with JPEG asked for in the write options
        using var directory = new TempDirectory();
        var output = directory.Combine("sheet");
        var request = CreateRequest(output) with { Write = WriteOptions.Default with { Format = OutputFormat.Jpeg } };

        // Act
        var result = MontageBuilder.Execute([CreatePng(directory, "a.png")], request);

        // Assert: the sheet is a JPEG, which starts with FF D8
        Assert.Equal(output, result.Written.Path);
        Assert.Equal(OutputFormat.Jpeg, result.Written.Format);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, File.ReadAllBytes(output).Take(2));
    }

    /// <summary>
    ///     Test that an output whose format cannot be told is refused before anything is written.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_NoFormatAndNoExtension_ThrowsUsageError()
    {
        // Arrange: no explicit format and no extension
        using var directory = new TempDirectory();
        var output = directory.Combine("sheet");

        // Act
        var error = Assert.Throws<ViewerException>(() => MontageBuilder.Execute([CreatePng(directory, "a.png")], CreateRequest(output)));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, error.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a request with a missing member is a caller error, and nothing is written.
    /// </summary>
    /// <param name="member">The member that is <see langword="null"/>.</param>
    [Theory]
    [InlineData("Output")]
    [InlineData("Sheet")]
    [InlineData("Write")]
    public void MontageBuilder_Execute_NullRequestMember_ThrowsArgumentNullException(string member)
    {
        // Arrange: a valid request with one member removed
        using var directory = new TempDirectory();
        var info = CreatePng(directory, "a.png");
        var request = CreateRequest(directory.Combine("sheet.png"));
        request = member switch
        {
            "Output" => request with { Output = null! },
            "Sheet" => request with { Sheet = null! },
            _ => request with { Write = null! }
        };

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => MontageBuilder.Execute([info], request));

        // Assert: the member is named, and nothing was written
        Assert.Contains(member, error.ParamName);
        Assert.False(File.Exists(directory.Combine("sheet.png")));
    }

    /// <summary>
    ///     Test that a missing input in the list is a caller error, and nothing is written.
    /// </summary>
    [Fact]
    public void MontageBuilder_Execute_NullInfoElement_ThrowsArgumentNullException()
    {
        // Arrange: a list whose second element is missing
        using var directory = new TempDirectory();
        SourceInfo[] infos = [CreatePng(directory, "a.png"), null!];

        // Act
        var error = Assert.Throws<ArgumentNullException>(() => MontageBuilder.Execute(infos, CreateRequest(directory.Combine("sheet.png"))));

        // Assert: the list is named with the position, and nothing was written
        Assert.Equal("infos", error.ParamName);
        Assert.Contains("2", error.Message);
        Assert.False(File.Exists(directory.Combine("sheet.png")));
    }
}
