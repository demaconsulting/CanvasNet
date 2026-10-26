// cspell:ignore canvasnet tif jpg jpeg tiff
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the CompareCommand class with its pictures: the difference image, the side-by-side image, the
///     overlay, the formats, and the checks of the output paths.
/// </summary>
[Collection("Sequential")]
public class CompareCommandOutputTests
{
    /// <summary>
    ///     The black of a pixel that does not differ in a difference picture.
    /// </summary>
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>
    ///     Writes a gray 8x6 image and a copy of it whose pixel (3,2) has a blue channel 50 higher.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The paths of the two files.</returns>
    private static (string A, string B) SaveOnePixelPair(TempDirectory directory)
    {
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, CompareCommandTests.Gray);
        using var changed = CompareCommandTests.Solid(8, 6, CompareCommandTests.Gray);
        changed[3, 2] = new Rgba32(100, 100, 150, 255);
        var b = directory.Combine("b.png");
        PngCodec.Save(changed, b);
        return (a, b);
    }

    /// <summary>
    ///     Test that the difference picture is a heat map: black where the images agree, and the hot ramp where they differ.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_DiffOutput_WritesHeatmapOfDifferences()
    {
        // Arrange: one pixel differs by 50 in one channel
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var diff = directory.Combine("diff.png");

        // Act
        var stdOut = CompareCommandTests.Run(false, a, b, "--diff", diff);

        // Assert: the picture has the size of the images; delta 50 at gain 4 is v = 200, which the ramp makes (255, 255, 90)
        using var picture = PngCodec.Load(diff);
        Assert.Equal((8, 6), (picture.Width, picture.Height));
        Assert.Equal(new Rgba32(255, 255, 90, 255), picture[3, 2]);
        Assert.Equal(Black, picture[0, 0]);
        Assert.Equal(Black, picture[7, 5]);
        Assert.Contains($"wrote {diff} (8x6 png, {new FileInfo(diff).Length} bytes) [diff]", stdOut);
    }

    /// <summary>
    ///     Test that the style and the gain change the difference picture.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_DiffStyleAbsAndGain_ChangePixels()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var heat = directory.Combine("heat.png");
        var abs = directory.Combine("abs.png");
        var gain = directory.Combine("gain.png");

        // Act: the default, the per-channel style at gain 2, and the heat style at gain 1
        CompareCommandTests.Run(false, a, b, "--diff", heat);
        CompareCommandTests.Run(false, a, b, "--diff", abs, "--diff-style", "abs", "--gain", "2");
        CompareCommandTests.Run(false, a, b, "--diff", gain, "--gain", "1");

        // Assert: each channel's own difference times two; delta 50 at gain 1 is v = 50, which the ramp makes (150, 0, 0)
        using var heatPicture = PngCodec.Load(heat);
        using var absPicture = PngCodec.Load(abs);
        using var gainPicture = PngCodec.Load(gain);
        Assert.Equal(new Rgba32(0, 0, 100, 255), absPicture[3, 2]);
        Assert.Equal(Black, absPicture[0, 0]);
        Assert.Equal(new Rgba32(150, 0, 0, 255), gainPicture[3, 2]);
        Assert.NotEqual(heatPicture[3, 2], gainPicture[3, 2]);
    }

    /// <summary>
    ///     Test that the side-by-side picture has the first image, the second image, and the differences, with gaps between.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_SideBySide_WritesThreePanels()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var output = directory.Combine("sbs.png");

        // Act
        var stdOut = CompareCommandTests.Run(false, a, b, "--side-by-side", output);

        // Assert: 3 panels of 8 pixels and 2 gaps of 8 pixels
        using var picture = PngCodec.Load(output);
        Assert.Equal((40, 6), (picture.Width, picture.Height));
        Assert.Equal(CompareCommandTests.Gray, picture[3, 2]);
        Assert.Equal(new Rgba32(100, 100, 150, 255), picture[16 + 3, 2]);
        Assert.Equal(new Rgba32(255, 255, 90, 255), picture[32 + 3, 2]);
        Assert.Equal(Black, picture[32, 0]);
        Assert.Equal(new Rgba32(128, 128, 128, 255), picture[8, 0]);
        Assert.Equal(new Rgba32(128, 128, 128, 255), picture[31, 5]);
        Assert.Contains("(40x6 png, ", stdOut);
        Assert.Contains("[side-by-side]", stdOut);
    }

    /// <summary>
    ///     Test that the overlay blends the images and that the weight changes the blend.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_Overlay_WritesBlendAndWeightChangesIt()
    {
        // Arrange: a gray of 100 and a gray of 200
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, new Rgba32(100, 100, 100, 255));
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 8, 6, new Rgba32(200, 200, 200, 255));
        var half = directory.Combine("half.png");
        var none = directory.Combine("none.png");
        var all = directory.Combine("all.png");

        // Act: the default weight (a half), and the weights 0 and 1
        CompareCommandTests.Run(false, a, b, "--overlay", half);
        CompareCommandTests.Run(false, a, b, "--overlay", none, "--overlay-weight", "0");
        CompareCommandTests.Run(false, a, b, "--overlay", all, "--overlay-weight", "1");

        // Assert: the integer blend of the design (weight 128 of 255), and the images themselves at the ends
        var expected = (byte)(((100 * 127) + (200 * 128) + 127) / 255);
        using var halfPicture = PngCodec.Load(half);
        using var nonePicture = PngCodec.Load(none);
        using var allPicture = PngCodec.Load(all);
        Assert.Equal(new Rgba32(expected, expected, expected, 255), halfPicture[4, 3]);
        Assert.Equal(new Rgba32(100, 100, 100, 255), nonePicture[4, 3]);
        Assert.Equal(new Rgba32(200, 200, 200, 255), allPicture[4, 3]);
    }

    /// <summary>
    ///     Test that all three pictures are written together and listed in the JSON result.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_AllOutputs_WrittenTogetherAndListedInJson()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var diff = directory.Combine("out", "diff.png");
        var sideBySide = directory.Combine("out", "sbs.png");
        var overlay = directory.Combine("out", "overlay.png");

        // Act
        var stdOut = CompareCommandTests.Run(true, a, b, "--overlay", overlay, "--side-by-side", sideBySide, "--diff", diff);

        // Assert: in the fixed order diff, side by side, overlay, each with its size and size on disk
        using var document = JsonDocument.Parse(stdOut);
        var outputs = document.RootElement.GetProperty("outputs").EnumerateArray().ToList();
        Assert.Equal(["diff", "sideBySide", "overlay"], outputs.Select(o => o.GetProperty("kind").GetString()));
        Assert.Equal([diff, sideBySide, overlay], outputs.Select(o => o.GetProperty("path").GetString()));
        Assert.Equal([8, 40, 8], outputs.Select(o => o.GetProperty("width").GetInt32()));
        Assert.Equal(["png", "png", "png"], outputs.Select(o => o.GetProperty("format").GetString()));
        Assert.Equal(
            outputs.Select(o => new FileInfo(o.GetProperty("path").GetString()!).Length),
            outputs.Select(o => o.GetProperty("bytes").GetInt64()));
    }

    /// <summary>
    ///     Test that the format of a picture comes from the extension of its file.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <param name="format">The expected format name.</param>
    [Theory]
    [InlineData(".png", "png")]
    [InlineData(".bmp", "bmp")]
    [InlineData(".tif", "tiff")]
    [InlineData(".jpg", "jpeg")]
    public void CompareCommand_Run_OutputFormatFromExtension_WritesDecodableFile(string extension, string format)
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var output = directory.Combine("diff" + extension);

        // Act
        using var document = JsonDocument.Parse(CompareCommandTests.Run(true, a, b, "--diff", output));

        // Assert: the format is reported and the file decodes at the size of the images
        Assert.Equal(format, document.RootElement.GetProperty("outputs")[0].GetProperty("format").GetString());
        using var picture = SurfaceAssert.Load(output);
        Assert.Equal((8, 6), (picture.Width, picture.Height));
    }

    /// <summary>
    ///     Test that an output that is the first input is refused, and that nothing is written.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_OutputIsInputA_ThrowsSameInputOutputAndWritesNothing()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var before = File.ReadAllBytes(a);
        var overlay = directory.Combine("overlay.png");

        // Act: the overlay would be fine, the diff would overwrite the first input
        var exception = CompareCommandTests.RunFailing(a, b, "--overlay", overlay, "--diff", a);

        // Assert
        Assert.Equal(ErrorCodes.SameInputOutput, exception.Code);
        Assert.Equal(before, File.ReadAllBytes(a));
        Assert.False(File.Exists(overlay));
    }

    /// <summary>
    ///     Test that an output that is the second input is refused, and that nothing is written.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_OutputIsInputB_ThrowsSameInputOutputAndWritesNothing()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var before = File.ReadAllBytes(b);
        var diff = directory.Combine("diff.png");

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, "--diff", diff, "--side-by-side", b);

        // Assert
        Assert.Equal(ErrorCodes.SameInputOutput, exception.Code);
        Assert.Equal(before, File.ReadAllBytes(b));
        Assert.False(File.Exists(diff));
    }

    /// <summary>
    ///     Test that two pictures cannot be written to the same file.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_TwoOutputsSameFile_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var output = directory.Combine("both.png");

        // Act: the second spelling of the path is the same file
        var exception = CompareCommandTests.RunFailing(a, b, "--diff", output, "--overlay", directory.Combine(".", "both.png"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that no-clobber refuses an existing output and leaves it as it was.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_NoClobberAndExistingOutput_ThrowsOutputExistsAndKeepsFile()
    {
        // Arrange: an existing diff file
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var diff = directory.Combine("diff.png");
        File.WriteAllText(diff, "existing");
        var overlay = directory.Combine("overlay.png");

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, "--overlay", overlay, "--diff", diff, "--no-clobber");

        // Assert: the file is untouched and nothing else was written
        Assert.Equal(ErrorCodes.OutputExists, exception.Code);
        Assert.Equal("existing", File.ReadAllText(diff));
        Assert.False(File.Exists(overlay));
    }

    /// <summary>
    ///     Test that an existing output is overwritten by default.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ExistingOutput_IsOverwritten()
    {
        // Arrange: an existing diff file
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var diff = directory.Combine("diff.png");
        File.WriteAllText(diff, "existing");

        // Act
        CompareCommandTests.Run(false, a, b, "--diff", diff);

        // Assert: it is a PNG now, and no temporary file is left
        using var picture = PngCodec.Load(diff);
        Assert.Equal(8, picture.Width);
        Assert.DoesNotContain(Directory.GetFiles(directory.Path), path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that an output path that cannot be written is found before any picture is written.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_SecondOutputPathInvalid_LeavesNoFirstOutput()
    {
        // Arrange: the second output would be inside a file
        using var directory = new TempDirectory();
        var (a, b) = SaveOnePixelPair(directory);
        var diff = directory.Combine("diff.png");
        var blocked = directory.Combine("a.png", "sub", "overlay.png");

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, "--diff", diff, "--overlay", blocked);

        // Assert
        Assert.Equal(ErrorCodes.OutputWriteFailed, exception.Code);
        Assert.False(File.Exists(diff));
    }

    /// <summary>
    ///     Test that a value that is out of range is a usage error.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">Its value.</param>
    [Theory]
    [InlineData("--quality", "0")]
    [InlineData("--tolerance", "256")]
    [InlineData("--tolerance", "-1")]
    [InlineData("--gain", "0")]
    [InlineData("--gain", "300")]
    [InlineData("--overlay-weight", "2")]
    [InlineData("--diff-style", "glow")]
    [InlineData("--filter", "sharp")]
    public void CompareCommand_Run_InvalidOptionValues_ThrowUsageError(string option, string value)
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = CompareCommandTests.SavePair(directory);

        // Act
        var exception = CompareCommandTests.RunFailing(a, b, option, value);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
    }
}
