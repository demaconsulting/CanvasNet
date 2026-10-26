// cspell:ignore canvasnet
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the MontageCommand class: the sheet geometry, the fit, the labels, the result, and the errors.
/// </summary>
[Collection("Sequential")]
public class MontageCommandTests
{
    /// <summary>
    ///     The white of the default background.
    /// </summary>
    internal static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     The red of a test image.
    /// </summary>
    internal static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>
    ///     The blue of a test image.
    /// </summary>
    internal static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>
    ///     Runs the montage command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    internal static string Run(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new MontageCommand();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Runs the montage command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    internal static ToolException RunFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new MontageCommand();
        var (exception, _, _) = ToolHost.Capture(() => Assert.Throws<ToolException>(() => command.Run(context, args)));
        return exception;
    }

    /// <summary>
    ///     Test that the command declares its name, summary, usage, and options.
    /// </summary>
    [Fact]
    public void MontageCommand_Metadata_DeclaresNameSummaryUsageAndOptions()
    {
        // Arrange / Act
        var command = new MontageCommand();

        // Assert: identity and help
        Assert.Equal("montage", command.Name);
        Assert.Empty(command.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
        Assert.StartsWith("canvasnet montage <inputs...> -o <output>", command.Usage);
        Assert.Contains("Examples:", command.Usage);

        // Assert: the options, none declared twice, every one described, and -o is the alias of --output
        var names = command.Options.Select(o => o.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(command.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.Description)));
        foreach (var name in new[]
                 {
                     "--output", "--columns", "--cell", "--fit", "--spacing", "--padding", "--background", "--labels", "--page",
                     "--all-pages", "--dpi", "--no-auto-orient", "--strict", "--view", "--azimuth", "--elevation", "--projection",
                     "--define", "--time", "--default-color", "--max-triangles", "--filter", "--quality", "--no-clobber"
                 })
        {
            Assert.Contains(name, names);
        }

        Assert.Equal(["-o"], command.Options.Single(o => o.Name == "--output").Aliases);
    }

    /// <summary>
    ///     Test that two images make a one-row sheet with a cell for each and a one-line result.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_TwoImages_WritesSheetWithBothCellsAndOneLine()
    {
        // Arrange: a red 8x6 image and a blue 6x8 image
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 6, 8, Blue);
        var output = directory.Combine("sheet.png");

        // Act
        var stdOut = Run(false, a, b, "-o", output);

        // Assert: two columns of 256x256 cells with 8 pixels of padding and spacing: 536x272
        Assert.Equal($"wrote {output} (536x272 png, {new FileInfo(output).Length} bytes): 2 images in 2 columns{Environment.NewLine}", stdOut);
        using var sheet = PngCodec.Load(output);
        Assert.Equal((536, 272), (sheet.Width, sheet.Height));

        // Assert: the red image fills the middle of the first cell, the blue image the middle of the second; the margin is white
        Assert.Equal(Red, sheet[8 + 128, 8 + 128]);
        Assert.Equal(Blue, sheet[8 + 256 + 8 + 128, 8 + 128]);
        Assert.Equal(White, sheet[0, 0]);
        Assert.Equal(White, sheet[8 + 256 + 4, 100]);
    }

    /// <summary>
    ///     Test that the columns and the cell size set the geometry of the sheet.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_ColumnsAndCell_SetGeometry()
    {
        // Arrange: five images
        using var directory = new TempDirectory();
        var inputs = Enumerable.Range(0, 5).Select(i => CompareCommandTests.SaveSolid(directory, $"{i}.png", 8, 6, Red)).ToList();

        // Act: three columns of 32x24 cells, 4 pixels of spacing and 2 of padding
        var stdOut = Run(true, [.. inputs, "--columns", "3", "--cell", "32x24", "--spacing", "4", "--padding", "2", "-o", directory.Combine("sheet.png")]);

        // Assert: 2 rows; width 4 + 96 + 8 = 108, height 4 + 48 + 4 = 56; the fifth cell is in the second row
        using var document = JsonDocument.Parse(stdOut);
        var sheet = document.RootElement.GetProperty("sheet");
        Assert.Equal((108, 56, 3, 2), (sheet.GetProperty("width").GetInt32(), sheet.GetProperty("height").GetInt32(), sheet.GetProperty("columns").GetInt32(), sheet.GetProperty("rows").GetInt32()));
        Assert.Equal((32, 24, 4, 2), (sheet.GetProperty("cellWidth").GetInt32(), sheet.GetProperty("cellHeight").GetInt32(), sheet.GetProperty("spacing").GetInt32(), sheet.GetProperty("padding").GetInt32()));
        var last = document.RootElement.GetProperty("cells")[4];
        Assert.Equal((1, 1, 2 + 36, 2 + 28), (last.GetProperty("column").GetInt32(), last.GetProperty("row").GetInt32(), last.GetProperty("x").GetInt32(), last.GetProperty("y").GetInt32()));
    }

    /// <summary>
    ///     Test that the spacing, the padding, and the background are placed as written.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_SpacingPaddingBackground_ArePlaced()
    {
        // Arrange: two red squares
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 10, 10, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 10, 10, Red);
        var output = directory.Combine("sheet.png");

        // Act: 20x20 cells, 3 pixels of padding, 6 of spacing, a blue-gray background
        Run(false, a, b, "--columns", "2", "--cell", "20x20", "--spacing", "6", "--padding", "3", "--background", "#336699", "-o", output);

        // Assert: 3 + 20 + 6 + 20 + 3 = 52 wide, 26 high; red cells at 3..22 and 29..48, the background elsewhere
        using var sheet = PngCodec.Load(output);
        var background = new Rgba32(0x33, 0x66, 0x99, 255);
        Assert.Equal((52, 26), (sheet.Width, sheet.Height));
        Assert.Equal(background, sheet[0, 0]);
        Assert.Equal(background, sheet[2, 10]);
        Assert.Equal(Red, sheet[3, 3]);
        Assert.Equal(Red, sheet[22, 22]);
        Assert.Equal(background, sheet[23, 10]);
        Assert.Equal(background, sheet[28, 10]);
        Assert.Equal(Red, sheet[29, 3]);
        Assert.Equal(Red, sheet[48, 22]);
        Assert.Equal(background, sheet[49, 10]);
    }

    /// <summary>
    ///     Test that contain keeps the aspect ratio with a margin and that stretch fills the whole cell.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_FitContainAndStretch_PlaceCellContent()
    {
        // Arrange: a red 20x10 image
        using var directory = new TempDirectory();
        var input = CompareCommandTests.SaveSolid(directory, "a.png", 20, 10, Red);
        var contained = directory.Combine("contain.png");
        var stretched = directory.Combine("stretch.png");

        // Act: a 40x40 cell without padding
        Run(false, input, "--cell", "40x40", "--padding", "0", "-o", contained);
        Run(false, input, "--cell", "40x40", "--padding", "0", "--fit", "stretch", "-o", stretched);

        // Assert: contain scales to 40x20 and centers it, leaving 10 white rows above and below; stretch fills all 40 rows
        using var containPicture = PngCodec.Load(contained);
        using var stretchPicture = PngCodec.Load(stretched);
        Assert.Equal((40, 40), (containPicture.Width, containPicture.Height));
        Assert.Equal(White, containPicture[20, 5]);
        Assert.Equal(Red, containPicture[20, 10]);
        Assert.Equal(Red, containPicture[20, 29]);
        Assert.Equal(White, containPicture[20, 35]);
        Assert.Equal(Red, stretchPicture[20, 5]);
        Assert.Equal(Red, stretchPicture[20, 35]);
    }

    /// <summary>
    ///     Test that labels add a strip below each cell with text, and that without them there is no strip.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_Labels_DrawLabelStripsAndWithoutThemNone()
    {
        // Arrange: a red image
        using var directory = new TempDirectory();
        var input = CompareCommandTests.SaveSolid(directory, "photo.png", 8, 6, Red);
        var plain = directory.Combine("plain.png");
        var labeled = directory.Combine("labeled.png");

        // Act
        Run(false, input, "--cell", "64x64", "-o", plain);
        using var document = JsonDocument.Parse(Run(true, input, "--cell", "64x64", "--labels", "-o", labeled));

        // Assert: the strip adds 22 rows; everything above it is the same picture; the strip below the cell has text pixels
        using var withoutLabels = PngCodec.Load(plain);
        using var withLabels = PngCodec.Load(labeled);
        Assert.Equal((withoutLabels.Width, withoutLabels.Height + 22), (withLabels.Width, withLabels.Height));
        Assert.Equal(22, document.RootElement.GetProperty("sheet").GetProperty("labelHeight").GetInt32());
        Assert.Equal("photo.png", document.RootElement.GetProperty("cells")[0].GetProperty("label").GetString());
        for (var y = 0; y < 8 + 64; y++)
        {
            Assert.True(withoutLabels.GetRowSpan(y).SequenceEqual(withLabels.GetRowSpan(y)), $"Row {y} differs");
        }

        var textPixels = 0;
        for (var y = 8 + 64; y < 8 + 64 + 22; y++)
        {
            textPixels += withLabels.GetRowSpan(y).ToArray().Count(pixel => pixel != White);
        }

        Assert.True(textPixels > 20, $"The label has only {textPixels} pixels");
        for (var y = 8 + 64; y < withoutLabels.Height; y++)
        {
            Assert.All(withoutLabels.GetRowSpan(y).ToArray(), pixel => Assert.Equal(White, pixel));
        }
    }

    /// <summary>
    ///     Test that the JSON result lists the sheet, the cells, and the output.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_Json_ListsSheetCellsAndOutput()
    {
        // Arrange: two images
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 6, 8, Blue);
        var output = directory.Combine("sheet.png");

        // Act
        var stdOut = Run(true, a, b, "--cell", "64x64", "--labels", "-o", output);

        // Assert: the top-level fields in order, and the envelope
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.Equal(
            ["schemaVersion", "ok", "command", "tool", "sheet", "cells", "outputs", "diagnostics", "echo"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("montage", root.GetProperty("command").GetString());

        // Assert: the sheet: padding 8, spacing 8, a 64x64 cell with a 22-pixel strip, two columns: 152x102
        var sheet = root.GetProperty("sheet");
        Assert.Equal(
            ["width", "height", "columns", "rows", "cellWidth", "cellHeight", "labelHeight", "spacing", "padding", "fit", "labels", "background"],
            sheet.EnumerateObject().Select(p => p.Name));
        Assert.Equal((152, 102, 2, 1), (sheet.GetProperty("width").GetInt32(), sheet.GetProperty("height").GetInt32(), sheet.GetProperty("columns").GetInt32(), sheet.GetProperty("rows").GetInt32()));
        Assert.Equal("contain", sheet.GetProperty("fit").GetString());
        Assert.True(sheet.GetProperty("labels").GetBoolean());
        Assert.Equal("#FFFFFF", sheet.GetProperty("background").GetString());

        // Assert: the cells, with a label and no document fields
        var cells = root.GetProperty("cells");
        Assert.Equal(2, cells.GetArrayLength());
        Assert.Equal(
            ["index", "column", "row", "x", "y", "input", "format", "loadedWidth", "loadedHeight", "label"],
            cells[1].EnumerateObject().Select(p => p.Name));
        Assert.Equal((1, 1, 0, 80, 8), (cells[1].GetProperty("index").GetInt32(), cells[1].GetProperty("column").GetInt32(), cells[1].GetProperty("row").GetInt32(), cells[1].GetProperty("x").GetInt32(), cells[1].GetProperty("y").GetInt32()));
        Assert.Equal(b, cells[1].GetProperty("input").GetString());
        Assert.Equal((6, 8), (cells[1].GetProperty("loadedWidth").GetInt32(), cells[1].GetProperty("loadedHeight").GetInt32()));

        // Assert: the output and no notes
        var written = Assert.Single(root.GetProperty("outputs").EnumerateArray());
        Assert.Equal(output, written.GetProperty("path").GetString());
        Assert.Equal((152, 102), (written.GetProperty("width").GetInt32(), written.GetProperty("height").GetInt32()));
        Assert.Equal(new FileInfo(output).Length, written.GetProperty("bytes").GetInt64());
        Assert.Equal(0, root.GetProperty("diagnostics").GetArrayLength());
    }

    /// <summary>
    ///     Test that a sheet that is too large is refused before any input is opened.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_SheetTooLarge_ThrowsSizeLimitBeforeLoading()
    {
        // Arrange: a good image, and a second input that is corrupt and would fail if it were opened
        using var directory = new TempDirectory();
        var good = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var corrupt = CompareCommandTests.SaveSolid(directory, "b.png", 8, 6, Red);
        File.WriteAllBytes(corrupt, File.ReadAllBytes(corrupt)[..30]);
        var output = directory.Combine("sheet.png");

        // Act: three columns of 4000 pixels are wider than 8192
        var exception = RunFailing(good, corrupt, "--columns", "3", "--cell", "4000x100", "-o", output);

        // Assert: the size limit, not the corrupt input
        Assert.Equal(ErrorCodes.SizeLimit, exception.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that no input is a usage error.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_NoInputs_ThrowsUsageError()
    {
        // Arrange / Act
        var exception = RunFailing("-o", "sheet.png");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("one or more input files", exception.Message);
    }

    /// <summary>
    ///     Test that no output is a usage error with a hint.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_MissingOutput_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);

        // Act
        var exception = RunFailing(input);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("-o", exception.Hint);
    }

    /// <summary>
    ///     Test that a value that is malformed or out of range is a usage error and writes nothing.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">Its value.</param>
    [Theory]
    [InlineData("--columns", "0")]
    [InlineData("--columns", "65")]
    [InlineData("--columns", "two")]
    [InlineData("--cell", "8x8")]
    [InlineData("--cell", "100")]
    [InlineData("--spacing", "-1")]
    [InlineData("--padding", "-1")]
    [InlineData("--fit", "cover")]
    [InlineData("--background", "transparent")]
    [InlineData("--background", "#11223380")]
    [InlineData("--quality", "0")]
    public void MontageCommand_Run_InvalidValues_ThrowUsageError(string option, string value)
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var output = directory.Combine("sheet.png");

        // Act
        var exception = RunFailing(input, option, value, "-o", output);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the output may not be any of the inputs, and that the input is not changed.
    /// </summary>
    /// <param name="index">The index of the input that is also the output.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MontageCommand_Run_OutputIsAnInput_ThrowsSameInputOutput(int index)
    {
        // Arrange: three inputs
        using var directory = new TempDirectory();
        var inputs = Enumerable.Range(0, 3).Select(i => CompareCommandTests.SaveSolid(directory, $"{i}.png", 8, 6, Red)).ToList();
        var before = File.ReadAllBytes(inputs[index]);

        // Act
        var exception = RunFailing([.. inputs, "-o", inputs[index]]);

        // Assert
        Assert.Equal(ErrorCodes.SameInputOutput, exception.Code);
        Assert.Equal(before, File.ReadAllBytes(inputs[index]));
    }

    /// <summary>
    ///     Test that no-clobber refuses an existing output and leaves it as it was.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_NoClobberAndExistingOutput_ThrowsOutputExistsAndKeepsFile()
    {
        // Arrange: an existing output
        using var directory = new TempDirectory();
        var input = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var output = directory.Combine("sheet.png");
        File.WriteAllText(output, "existing");

        // Act
        var exception = RunFailing(input, "--no-clobber", "-o", output);
        Run(false, input, "-o", output);

        // Assert: refused with no-clobber, overwritten without
        Assert.Equal(ErrorCodes.OutputExists, exception.Code);
        using var sheet = PngCodec.Load(output);
        Assert.Equal(272, sheet.Width);
    }

    /// <summary>
    ///     Test that a corrupt input is an expected error and that no output is written.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_CorruptInput_ThrowsCorruptInputAndWritesNothing()
    {
        // Arrange: a PNG cut after 30 bytes, second of two
        using var directory = new TempDirectory();
        var good = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var corrupt = CompareCommandTests.SaveSolid(directory, "b.png", 8, 6, Red);
        File.WriteAllBytes(corrupt, File.ReadAllBytes(corrupt)[..30]);
        var output = directory.Combine("sheet.png");

        // Act
        var exception = RunFailing(good, corrupt, "-o", output);

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
        Assert.False(File.Exists(output));
        Assert.DoesNotContain(Directory.GetFiles(directory.Path), path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a missing input is an expected error.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_MissingInput_ThrowsInputNotFound()
    {
        // Arrange
        using var directory = new TempDirectory();
        var good = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);

        // Act
        var exception = RunFailing(good, directory.Combine("missing.png"), "-o", directory.Combine("sheet.png"));

        // Assert
        Assert.Equal(ErrorCodes.InputNotFound, exception.Code);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create([]);
        var command = new MontageCommand();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => command.Run(null!, []));
        Assert.Throws<ArgumentNullException>(() => command.Run(context, null!));
    }

    /// <summary>
    ///     Test that the same inputs and options give the same text, result, and bytes every time.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_SameInputsTwice_WriteIdenticalBytes()
    {
        // Arrange
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, Red);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 6, 8, Blue);
        var output = directory.Combine("sheet.png");
        string[] args = [a, b, "--cell", "48x48", "--labels", "-o", output];

        // Act
        var firstJson = Run(true, args);
        var first = File.ReadAllBytes(output);
        var secondJson = Run(true, args);
        var second = File.ReadAllBytes(output);

        // Assert
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(first, second);
    }
}
