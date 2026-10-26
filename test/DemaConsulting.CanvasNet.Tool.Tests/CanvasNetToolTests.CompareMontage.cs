// cspell:ignore canvasnet pptx psnr rmse ssim
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Imaging;
using DemaConsulting.CanvasNet.Tool.Tests.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the <c>compare</c> and <c>montage</c> commands: most run the tool as a real process; the
///     tests that examine standard output and standard error separately run it in-process.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     The gray of the first image of the comparison tests.
    /// </summary>
    private static readonly Rgba32 CompareGray = new(100, 100, 100, 255);

    /// <summary>
    ///     Writes the two 8x6 images of the comparison tests: a gray, and the same gray with a blue channel 10 higher.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The paths of the two files.</returns>
    private static (string A, string B) WriteComparisonPair(TempDirectory directory)
    {
        return (
            CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, CompareGray),
            CompareCommandTests.SaveSolid(directory, "b.png", 8, 6, new Rgba32(100, 100, 110, 255)));
    }

    /// <summary>
    ///     Test that comparing identical images reports no difference and succeeds.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_IdenticalImages_ReportsZeroDifference()
    {
        // Arrange: the same image twice
        using var directory = new TempDirectory();
        var a = CompareCommandTests.SaveSolid(directory, "a.png", 8, 6, CompareGray);
        var b = CompareCommandTests.SaveSolid(directory, "b.png", 8, 6, CompareGray);

        // Act: run the compare command as a real process, with a threshold that only identical images meet
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "compare", a, b, "--threshold", "rmse=0");
        var json = Runner.Run(out var jsonText, "dotnet", _dllPath, "compare", a, b, "--json");

        // Assert: one line that says identical, then the threshold; in JSON zero error, similarity one, and no PSNR
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [$"compared {a} with {b} (8x6): identical", "threshold rmse <= 0: passed (0.00)"],
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')));
        Assert.Equal(0, json);
        using var document = JsonDocument.Parse(jsonText);
        var metrics = document.RootElement.GetProperty("metrics");
        Assert.True(metrics.GetProperty("identical").GetBoolean());
        Assert.Equal(0.0, metrics.GetProperty("rmse").GetDouble());
        Assert.Equal(1.0, metrics.GetProperty("ssim").GetDouble());
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("psnrDb").ValueKind);
    }

    /// <summary>
    ///     Test that comparing different images reports the metrics and the grid as one JSON document.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_DifferentImages_ReportsMetricsAndGridAsJson()
    {
        // Arrange: gray against gray with one channel 10 higher
        using var directory = new TempDirectory();
        var (a, b) = WriteComparisonPair(directory);

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "compare", a, b, "--grid", "2x2", "--json");

        // Assert: exactly one document, with the metrics of the design and a grid that covers the image
        Assert.Equal(0, exitCode);
        Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("compare", root.GetProperty("command").GetString());
        var metrics = root.GetProperty("metrics");
        Assert.Equal(3.3333, metrics.GetProperty("mae").GetDouble());
        Assert.Equal(5.7735, metrics.GetProperty("rmse").GetDouble());
        Assert.Equal(10, metrics.GetProperty("maxChannelDiff").GetInt32());
        Assert.Equal(48, metrics.GetProperty("diffPixels").GetInt64());
        Assert.Equal(4, root.GetProperty("grid").GetProperty("cells").GetArrayLength());
        Assert.Equal(3, root.GetProperty("worstCells").GetArrayLength());
    }

    /// <summary>
    ///     Test that a vector drawing is rendered at the size of the reference, so that it equals the raster made from it.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_VectorAgainstReference_RendersVectorAtReferenceSize()
    {
        // Arrange: the SVG fixture and a 200x160 raster rendering of it as the reference
        using var directory = new TempDirectory();
        var svg = FixtureFactory.FixturePath("shapes.svg");
        var reference = directory.Combine("reference.png");
        using (var loaded = SourceLoader.Load(svg, new SourceOptions { Width = 200, Height = 160 }))
        {
            PngCodec.Save(loaded.Surface, reference);
        }

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "compare", reference, svg, "--resize-to", "a", "--json");

        // Assert: the drawing was rendered at 200x160, not resampled, and the images are identical
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.Equal((200, 160), (root.GetProperty("b").GetProperty("loadedWidth").GetInt32(), root.GetProperty("b").GetProperty("loadedHeight").GetInt32()));
        Assert.True(root.GetProperty("metrics").GetProperty("identical").GetBoolean());
    }

    /// <summary>
    ///     Test that the difference, side-by-side, and overlay pictures are written together.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_ImageOutputs_WriteDiffSideBySideAndOverlay()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = WriteComparisonPair(directory);
        var diff = directory.Combine("diff.png");
        var sideBySide = directory.Combine("sbs.png");
        var overlay = directory.Combine("overlay.jpg");

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "compare", a, b, "--diff", diff, "--side-by-side", sideBySide, "--overlay", overlay);

        // Assert: success, a line for each picture, and decodable files of the sizes of the design
        Assert.Equal(0, exitCode);
        Assert.Contains("[diff]", text);
        Assert.Contains("[side-by-side]", text);
        Assert.Contains("[overlay]", text);
        using var diffPicture = PngCodec.Load(diff);
        using var sheetPicture = PngCodec.Load(sideBySide);
        using var overlayPicture = JpegCodec.Load(overlay);
        Assert.Equal((8, 6), (diffPicture.Width, diffPicture.Height));
        Assert.Equal((40, 6), (sheetPicture.Width, sheetPicture.Height));
        Assert.Equal((8, 6), (overlayPicture.Width, overlayPicture.Height));
        // The difference of 10 at the default gain of 4 is v = 40, which the hot ramp makes (120, 0, 0)
        Assert.Equal(new Rgba32(120, 0, 0, 255), diffPicture[0, 0]);
    }

    /// <summary>
    ///     Test that a threshold that is met exits with zero.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_ThresholdMet_ExitsZero()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = WriteComparisonPair(directory);

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "compare", a, b, "--threshold", "rmse=10", "--threshold", "ssim=0.99");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("threshold rmse <= 10: passed (5.77)", text);
        Assert.Contains("threshold ssim >= 0.99: passed (", text);
        Assert.DoesNotContain("Error", text);
    }

    /// <summary>
    ///     Test that a threshold that is not met exits with one, after the result and the pictures were written.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_ThresholdNotMet_ExitsOneAfterWritingResultAndImages()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = WriteComparisonPair(directory);
        var diff = directory.Combine("diff.png");

        // Act: run as a real process
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "compare", a, b, "--diff", diff, "--threshold", "rmse=1");

        // Assert: exit code 1, the result lines, the failed check, the concise error with its hint, no stack trace, and the picture
        Assert.Equal(1, exitCode);
        Assert.Contains("compared ", text);
        Assert.Contains("threshold rmse <= 1: failed (5.77)", text);
        Assert.Contains("Error: threshold-failed: rmse 5.77 is above the limit 1", text);
        Assert.Contains("Hint:", text);
        Assert.DoesNotContain("   at ", text);
        Assert.True(File.Exists(diff));
    }

    /// <summary>
    ///     Test that with --json a threshold that is not met writes one failure document that also carries the metrics.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_ThresholdNotMetWithJson_WritesFailureDocumentWithMetrics()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = WriteComparisonPair(directory);
        var overlay = directory.Combine("overlay.png");

        // Act: in-process, so that standard output and standard error are separate
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("compare", a, b, "--overlay", overlay, "--threshold", "ssim=1", "--threshold", "mae=1", "--json");

        // Assert: exit code 1; standard error has the concise error; standard output is one document with ok false,
        // the error code, and the metrics, the thresholds, and the output
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: threshold-failed:", stdErr);
        Assert.Single(stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("compare", root.GetProperty("command").GetString());
        Assert.Equal("threshold-failed", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(3.3333, root.GetProperty("metrics").GetProperty("mae").GetDouble());
        Assert.False(root.GetProperty("thresholds").GetProperty("passed").GetBoolean());
        var checks = root.GetProperty("thresholds").GetProperty("checks").EnumerateArray().ToList();
        Assert.Equal([false, false], checks.Select(c => c.GetProperty("passed").GetBoolean()));
        Assert.Equal(overlay, Assert.Single(root.GetProperty("outputs").EnumerateArray()).GetProperty("path").GetString());
        Assert.True(File.Exists(overlay));
    }

    /// <summary>
    ///     Test that the mistakes of the compare command are expected errors with an exit code of one and no stack trace.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_Errors_AreReportedAsExpectedErrors()
    {
        // Arrange: images of two sizes, and the output of each mistake
        using var directory = new TempDirectory();
        var (a, b) = WriteComparisonPair(directory);
        var small = CompareCommandTests.SaveSolid(directory, "small.png", 4, 3, CompareGray);
        var output = directory.Combine("out.png");
        var cases = new (string[] Args, string Code)[]
        {
            ([a, small], "size-mismatch"),
            ([a, b, "--threshold", "rmse"], "usage-error"),
            ([a, b, "--threshold", "bogus=1"], "usage-error"),
            ([a, b, "--diff", a], "same-input-output"),
            ([a, directory.Combine("missing.png")], "input-not-found"),
            ([a, b, "--overlay", output, "--diff", output], "usage-error")
        };

        foreach (var (args, code) in cases)
        {
            // Act: in-process; an unexpected exception would escape and fail the test
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke(["compare", .. args, "--json"]);

            // Assert: exit code 1, the code in the message and in the document, no stack trace, and no picture
            Assert.Equal(1, exitCode);
            Assert.Contains($"Error: {code}:", stdErr);
            Assert.DoesNotContain("   at ", stdErr);
            using var document = JsonDocument.Parse(stdOut);
            Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(code, document.RootElement.GetProperty("error").GetProperty("code").GetString());
            Assert.False(File.Exists(output));
        }
    }

    /// <summary>
    ///     Test that the same comparison twice writes the same text, result, and picture bytes.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Compare_SameInputsTwice_WriteIdenticalBytes()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = WriteComparisonPair(directory);
        var diff = directory.Combine("diff.png");
        var sideBySide = directory.Combine("sbs.png");
        var overlay = directory.Combine("overlay.png");
        string[] args = ["compare", a, b, "--diff", diff, "--side-by-side", sideBySide, "--overlay", overlay, "--json"];

        // Act
        Assert.Equal(0, Runner.Run(out var firstText, "dotnet", [_dllPath, .. args]));
        var first = new[] { diff, sideBySide, overlay }.Select(File.ReadAllBytes).ToList();
        Assert.Equal(0, Runner.Run(out var secondText, "dotnet", [_dllPath, .. args]));
        var second = new[] { diff, sideBySide, overlay }.Select(File.ReadAllBytes).ToList();

        // Assert
        Assert.Equal(firstText, secondText);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i], second[i]);
        }
    }

    /// <summary>
    ///     Test that the montage command writes a contact sheet of images.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Montage_Images_WritesContactSheet()
    {
        // Arrange: a red image and a blue image
        using var directory = new TempDirectory();
        var red = CompareCommandTests.SaveSolid(directory, "red.png", 8, 6, new Rgba32(255, 0, 0, 255));
        var blue = CompareCommandTests.SaveSolid(directory, "blue.png", 6, 8, new Rgba32(0, 0, 255, 255));
        var output = directory.Combine("sheet.png");

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "montage", red, blue, "-o", output);

        // Assert: one line naming the sheet, its size, and the layout; a cell for each image
        Assert.Equal(0, exitCode);
        Assert.Equal($"wrote {output} (536x272 png, {new FileInfo(output).Length} bytes): 2 images in 2 columns", text.TrimEnd());
        using var sheet = PngCodec.Load(output);
        Assert.Equal((536, 272), (sheet.Width, sheet.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), sheet[8 + 128, 8 + 128]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), sheet[8 + 256 + 8 + 128, 8 + 128]);
    }

    /// <summary>
    ///     Test that every page of a document is a cell, with labels that name the pages.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Montage_DocumentAllPagesWithLabels_WritesCellPerPage()
    {
        // Arrange: the two-slide presentation
        using var directory = new TempDirectory();
        var deck = FixtureFactory.WriteSource(directory.Path, "pptx");
        var output = directory.Combine("deck-sheet.png");

        // Act
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "montage", deck, "--all-pages", "--labels", "--cell", "96x72", "-o", output, "--json");

        // Assert: two cells, one for each slide, labeled with the file name and the page
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(text);
        var cells = document.RootElement.GetProperty("cells").EnumerateArray().ToList();
        Assert.Equal(["source.pptx p1", "source.pptx p2"], cells.Select(c => c.GetProperty("label").GetString()));
        Assert.Equal([1, 2], cells.Select(c => c.GetProperty("page").GetInt32()));
        using var sheet = PngCodec.Load(output);
        Assert.Equal(
            (document.RootElement.GetProperty("sheet").GetProperty("width").GetInt32(), document.RootElement.GetProperty("sheet").GetProperty("height").GetInt32()),
            (sheet.Width, sheet.Height));
        Assert.Equal(22, document.RootElement.GetProperty("sheet").GetProperty("labelHeight").GetInt32());
    }

    /// <summary>
    ///     Test that the mistakes of the montage command are expected errors with an exit code of one and no stack trace.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Montage_Errors_AreReportedAsExpectedErrors()
    {
        // Arrange: a good input, a corrupt input, and the output of each mistake
        using var directory = new TempDirectory();
        var good = CompareCommandTests.SaveSolid(directory, "good.png", 8, 6, CompareGray);
        var corrupt = CompareCommandTests.SaveSolid(directory, "corrupt.png", 8, 6, CompareGray);
        File.WriteAllBytes(corrupt, File.ReadAllBytes(corrupt)[..30]);
        var output = directory.Combine("sheet.png");
        var cases = new (string[] Args, string Code)[]
        {
            ([good, "-o", good], "same-input-output"),
            ([good, corrupt, "-o", output], "corrupt-input"),
            ([good, directory.Combine("missing.png"), "-o", output], "input-not-found"),
            ([good, "--cell", "8x8", "-o", output], "usage-error"),
            ([good, corrupt, "--columns", "3", "--cell", "4000x100", "-o", output], "size-limit"),
            ([good], "usage-error")
        };

        foreach (var (args, code) in cases)
        {
            // Act: in-process; an unexpected exception would escape and fail the test
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke(["montage", .. args, "--json"]);

            // Assert: exit code 1, the code in the message and in the document, no stack trace, and no sheet
            Assert.Equal(1, exitCode);
            Assert.Contains($"Error: {code}:", stdErr);
            Assert.DoesNotContain("   at ", stdErr);
            using var document = JsonDocument.Parse(stdOut);
            Assert.Equal(code, document.RootElement.GetProperty("error").GetProperty("code").GetString());
            Assert.False(File.Exists(output));
        }
    }

    /// <summary>
    ///     Test that the same montage twice writes the same text and sheet bytes.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Montage_SameInputsTwice_WriteIdenticalBytes()
    {
        // Arrange: two images, labeled
        using var directory = new TempDirectory();
        var red = CompareCommandTests.SaveSolid(directory, "red.png", 8, 6, new Rgba32(255, 0, 0, 255));
        var blue = CompareCommandTests.SaveSolid(directory, "blue.png", 6, 8, new Rgba32(0, 0, 255, 255));
        var output = directory.Combine("sheet.png");
        string[] args = ["montage", red, blue, "--labels", "--cell", "64x64", "-o", output, "--json"];

        // Act
        Assert.Equal(0, Runner.Run(out var firstText, "dotnet", [_dllPath, .. args]));
        var first = File.ReadAllBytes(output);
        Assert.Equal(0, Runner.Run(out var secondText, "dotnet", [_dllPath, .. args]));
        var second = File.ReadAllBytes(output);

        // Assert
        Assert.Equal(firstText, secondText);
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Test that the compare and montage commands are listed in the help of the tool and have their own help.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Help_ListsCompareAndMontage()
    {
        // Act
        var top = ToolHost.Invoke("--help");
        var compare = ToolHost.Invoke("compare", "--help");
        var montage = ToolHost.Invoke("montage", "--help");

        // Assert
        Assert.Equal(0, top.ExitCode);
        Assert.Contains("  compare", top.StdOut);
        Assert.Contains("  montage", top.StdOut);
        Assert.Equal(0, compare.ExitCode);
        Assert.Contains("canvasnet compare <a> <b> [options]", compare.StdOut);
        Assert.Contains("--threshold", compare.StdOut);
        Assert.Contains("--resize-to", compare.StdOut);
        Assert.Equal(0, montage.ExitCode);
        Assert.Contains("canvasnet montage <inputs...> -o <output> [options]", montage.StdOut);
        Assert.Contains("--all-pages", montage.StdOut);
        Assert.Contains("--labels", montage.StdOut);
    }
}
