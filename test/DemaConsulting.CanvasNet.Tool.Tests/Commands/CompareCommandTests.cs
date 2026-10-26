// cspell:ignore canvasnet psnr rmse ssim
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the CompareCommand class: the metrics, the grid, the thresholds, the result, and the errors.
/// </summary>
[Collection("Sequential")]
public class CompareCommandTests
{
    /// <summary>
    ///     The gray of the first image of most tests.
    /// </summary>
    internal static readonly Rgba32 Gray = new(100, 100, 100, 255);

    /// <summary>
    ///     The first image's gray with the blue channel raised by 10: every channel difference is 0 except one of 10.
    /// </summary>
    internal static readonly Rgba32 BluishGray = new(100, 100, 110, 255);

    /// <summary>
    ///     Runs the compare command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    internal static string Run(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new CompareCommand();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Runs the compare command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    internal static ToolException RunFailing(params string[] args)
    {
        return RunFailingWithOutput(false, args).Exception;
    }

    /// <summary>
    ///     Runs the compare command, which fails, and captures what it wrote before it failed.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception and the captured standard output.</returns>
    internal static (ToolException Exception, string StdOut) RunFailingWithOutput(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new CompareCommand();
        var (exception, stdOut, _) = ToolHost.Capture(() => Assert.Throws<ToolException>(() => command.Run(context, args)));
        return (exception, stdOut);
    }

    /// <summary>
    ///     Creates an opaque image of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>A new surface.</returns>
    internal static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <summary>
    ///     Writes an image of one color as a PNG file.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The path of the file.</returns>
    internal static string SaveSolid(TempDirectory directory, string name, int width, int height, Rgba32 color)
    {
        using var surface = Solid(width, height, color);
        var path = directory.Combine(name);
        PngCodec.Save(surface, path);
        return path;
    }

    /// <summary>
    ///     Writes the two images of most tests: 8x6, gray and gray with a blue channel 10 higher.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The paths of the two files.</returns>
    internal static (string A, string B) SavePair(TempDirectory directory)
    {
        return (SaveSolid(directory, "a.png", 8, 6, Gray), SaveSolid(directory, "b.png", 8, 6, BluishGray));
    }

    /// <summary>
    ///     Test that the command declares its name, summary, usage, and options.
    /// </summary>
    [Fact]
    public void CompareCommand_Metadata_DeclaresNameSummaryUsageAndOptions()
    {
        // Arrange / Act
        var command = new CompareCommand();

        // Assert: identity and help
        Assert.Equal("compare", command.Name);
        Assert.Empty(command.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
        Assert.StartsWith("canvasnet compare <a> <b>", command.Usage);
        Assert.Contains("Examples:", command.Usage);

        // Assert: the options, none declared twice, every one described, the shared ones with the render grammar
        var names = command.Options.Select(o => o.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(command.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.Description)));
        foreach (var name in new[]
                 {
                     "--page", "--dpi", "--a-page", "--b-page", "--a-dpi", "--b-dpi", "--background", "--no-auto-orient", "--strict",
                     "--view", "--azimuth", "--elevation", "--projection", "--define", "--time", "--default-color", "--max-triangles",
                     "--a-view", "--b-view", "--a-define", "--b-define", "--resize-to", "--fit", "--filter", "--tolerance", "--grid",
                     "--threshold", "--diff", "--diff-style", "--gain", "--side-by-side", "--overlay", "--overlay-weight", "--quality",
                     "--no-clobber"
                 })
        {
            Assert.Contains(name, names);
        }

        Assert.Same(
            RenderCommand.OptionTable.Single(o => o.Name == "--page"),
            command.Options.Single(o => o.Name == "--page"));
    }

    /// <summary>
    ///     Test that two identical images are reported as identical, in one line and without a grid.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_IdenticalImages_ReportsIdentical()
    {
        // Arrange: the same gray image twice
        using var directory = new TempDirectory();
        var a = SaveSolid(directory, "a.png", 8, 6, Gray);
        var b = SaveSolid(directory, "b.png", 8, 6, Gray);

        // Act
        var stdOut = Run(false, a, b);

        // Assert: one line, naming both inputs and the size
        Assert.Equal($"compared {a} with {b} (8x6): identical{Environment.NewLine}", stdOut);
    }

    /// <summary>
    ///     Test that different images are reported with the metrics and the error of each grid cell.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_DifferentImages_ReportsMetricsAndGridInText()
    {
        // Arrange: gray against gray with one channel 10 higher: MAE 10/3, RMSE sqrt(100/3), every pixel above the tolerance
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act: a 2x2 grid
        var lines = Run(false, a, b, "--grid", "2x2").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Assert: the summary, the grid heading and rows
        var psnr = 20 * Math.Log10(255 / Math.Sqrt(100.0 / 3));
        Assert.Equal(4, lines.Length);
        Assert.StartsWith($"compared {a} with {b} (8x6): MAE 3.33, RMSE 5.77, PSNR {psnr:0.00} dB, SSIM ", lines[0]);
        Assert.EndsWith(", 100.00% of pixels differ (tolerance 8)", lines[0]);
        Assert.Equal("RMSE per cell (2x2):", lines[1]);
        Assert.Equal("   5.8   5.8", lines[2]);
        Assert.Equal("   5.8   5.8", lines[3]);
    }

    /// <summary>
    ///     Test that the JSON result has every field of the schema, in order.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_Json_WritesDocumentWithMetricsGridWorstCellsAndThresholds()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act
        var stdOut = Run(true, a, b, "--grid", "2x2");

        // Assert: the top-level fields in order, and the envelope
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.Equal(
            ["schemaVersion", "ok", "command", "tool", "a", "b", "size", "options", "metrics", "grid", "worstCells", "thresholds", "outputs", "diagnostics", "echo"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("compare", root.GetProperty("command").GetString());
        Assert.Equal("canvasnet", root.GetProperty("tool").GetProperty("name").GetString());

        // Assert: the inputs, the size, and the options
        var side = root.GetProperty("b");
        Assert.Equal(["path", "format", "loadedWidth", "loadedHeight", "width", "height", "operations"], side.EnumerateObject().Select(p => p.Name));
        Assert.Equal(b, side.GetProperty("path").GetString());
        Assert.Equal("png", side.GetProperty("format").GetString());
        Assert.Equal((8, 6), (side.GetProperty("width").GetInt32(), side.GetProperty("height").GetInt32()));
        Assert.Equal((8, 6), (root.GetProperty("size").GetProperty("width").GetInt32(), root.GetProperty("size").GetProperty("height").GetInt32()));
        var options = root.GetProperty("options");
        Assert.Equal(8, options.GetProperty("tolerance").GetInt32());
        Assert.Equal("#FFFFFF", options.GetProperty("background").GetString());
        Assert.Equal((2, 2), (options.GetProperty("gridColumns").GetInt32(), options.GetProperty("gridRows").GetInt32()));
        Assert.False(root.TryGetProperty("resize", out _));

        // Assert: the metrics, rounded
        var metrics = root.GetProperty("metrics");
        Assert.Equal(
            ["identical", "mae", "rmse", "rmseNormalized", "psnrDb", "ssim", "maxChannelDiff", "diffPixels", "diffPixelRatio"],
            metrics.EnumerateObject().Select(p => p.Name));
        Assert.False(metrics.GetProperty("identical").GetBoolean());
        Assert.Equal(3.3333, metrics.GetProperty("mae").GetDouble());
        Assert.Equal(5.7735, metrics.GetProperty("rmse").GetDouble());
        Assert.Equal(0.022641, metrics.GetProperty("rmseNormalized").GetDouble());
        Assert.Equal(Math.Round(20 * Math.Log10(255 / Math.Sqrt(100.0 / 3)), 4), metrics.GetProperty("psnrDb").GetDouble());
        Assert.InRange(metrics.GetProperty("ssim").GetDouble(), 0.9999, 1.0);
        Assert.Equal(10, metrics.GetProperty("maxChannelDiff").GetInt32());
        Assert.Equal(48, metrics.GetProperty("diffPixels").GetInt64());
        Assert.Equal(1.0, metrics.GetProperty("diffPixelRatio").GetDouble());

        // Assert: the grid cells partition the image, and the worst cells are the first in row-major order (all equal)
        var grid = root.GetProperty("grid");
        Assert.Equal(2, grid.GetProperty("columns").GetInt32());
        var cells = grid.GetProperty("cells");
        Assert.Equal(4, cells.GetArrayLength());
        var last = cells[3];
        Assert.Equal(
            ["column", "row", "x", "y", "width", "height", "mae", "rmse", "diffPixelRatio"],
            last.EnumerateObject().Select(p => p.Name));
        Assert.Equal((1, 1, 4, 3, 4, 3), (last.GetProperty("column").GetInt32(), last.GetProperty("row").GetInt32(), last.GetProperty("x").GetInt32(), last.GetProperty("y").GetInt32(), last.GetProperty("width").GetInt32(), last.GetProperty("height").GetInt32()));
        var worst = root.GetProperty("worstCells");
        Assert.Equal(3, worst.GetArrayLength());
        Assert.Equal(["column", "row", "rmse"], worst[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal((0, 0), (worst[0].GetProperty("column").GetInt32(), worst[0].GetProperty("row").GetInt32()));
        Assert.Equal((1, 0), (worst[1].GetProperty("column").GetInt32(), worst[1].GetProperty("row").GetInt32()));
        Assert.Equal((0, 1), (worst[2].GetProperty("column").GetInt32(), worst[2].GetProperty("row").GetInt32()));

        // Assert: no thresholds, outputs, notes, or echo
        Assert.True(root.GetProperty("thresholds").GetProperty("passed").GetBoolean());
        Assert.Equal(0, root.GetProperty("thresholds").GetProperty("checks").GetArrayLength());
        Assert.Equal(0, root.GetProperty("outputs").GetArrayLength());
        Assert.Equal(0, root.GetProperty("diagnostics").GetArrayLength());
        Assert.Equal(0, root.GetProperty("echo").GetArrayLength());
    }

    /// <summary>
    ///     Test that the PSNR of identical images is null in JSON, and that no cell is a worst cell.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_JsonIdenticalImages_PsnrIsNull()
    {
        // Arrange: the same image twice
        using var directory = new TempDirectory();
        var a = SaveSolid(directory, "a.png", 8, 6, Gray);
        var b = SaveSolid(directory, "b.png", 8, 6, Gray);

        // Act
        var stdOut = Run(true, a, b, "--threshold", "psnr=100");

        // Assert: identical, null PSNR (infinite), a similarity of one, no worst cell, and the PSNR threshold passes
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        var metrics = root.GetProperty("metrics");
        Assert.True(metrics.GetProperty("identical").GetBoolean());
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("psnrDb").ValueKind);
        Assert.Equal(1.0, metrics.GetProperty("ssim").GetDouble());
        Assert.Equal(0.0, metrics.GetProperty("mae").GetDouble());
        Assert.Equal(0, root.GetProperty("worstCells").GetArrayLength());
        var check = Assert.Single(root.GetProperty("thresholds").GetProperty("checks").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, check.GetProperty("value").ValueKind);
        Assert.True(check.GetProperty("passed").GetBoolean());
    }

    /// <summary>
    ///     Test that the tolerance changes the count of differing pixels and nothing else.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_Tolerance_ChangesDiffPixelRatioOnly()
    {
        // Arrange: every pixel differs by 10
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act: the default tolerance (8) and a tolerance of exactly the difference (10)
        using var defaults = JsonDocument.Parse(Run(true, a, b));
        using var tolerant = JsonDocument.Parse(Run(true, a, b, "--tolerance", "10"));

        // Assert: a difference equal to the tolerance does not count; every other number is the same
        var strict = defaults.RootElement.GetProperty("metrics");
        var lenient = tolerant.RootElement.GetProperty("metrics");
        Assert.Equal(48, strict.GetProperty("diffPixels").GetInt64());
        Assert.Equal(0, lenient.GetProperty("diffPixels").GetInt64());
        Assert.Equal(0.0, lenient.GetProperty("diffPixelRatio").GetDouble());
        Assert.Equal(10, tolerant.RootElement.GetProperty("options").GetProperty("tolerance").GetInt32());
        Assert.Equal(strict.GetProperty("mae").GetDouble(), lenient.GetProperty("mae").GetDouble());
        Assert.Equal(strict.GetProperty("rmse").GetDouble(), lenient.GetProperty("rmse").GetDouble());
        Assert.Equal(strict.GetProperty("maxChannelDiff").GetInt32(), lenient.GetProperty("maxChannelDiff").GetInt32());
        Assert.False(lenient.GetProperty("identical").GetBoolean());
    }

    /// <summary>
    ///     Test that the grid option sets the number of cells reported.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_GridOption_ReportsRequestedCells()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act: a grid of 3 columns and 2 rows, then the default
        using var requested = JsonDocument.Parse(Run(true, a, b, "--grid", "3x2"));
        using var defaults = JsonDocument.Parse(Run(true, a, b));

        // Assert
        Assert.Equal(6, requested.RootElement.GetProperty("grid").GetProperty("cells").GetArrayLength());
        Assert.Equal(3, requested.RootElement.GetProperty("grid").GetProperty("columns").GetInt32());
        Assert.Equal(2, requested.RootElement.GetProperty("grid").GetProperty("rows").GetInt32());
        Assert.Equal(16, defaults.RootElement.GetProperty("grid").GetProperty("cells").GetArrayLength());
    }

    /// <summary>
    ///     Test that a grid that is malformed, out of range, or larger than the image is a usage error.
    /// </summary>
    /// <param name="grid">The value of <c>--grid</c>.</param>
    [Theory]
    [InlineData("0x3")]
    [InlineData("100x100")]
    [InlineData("4")]
    [InlineData("65x1")]
    [InlineData("x4")]
    [InlineData("4x")]
    [InlineData("9x1")]
    [InlineData("1x7")]
    public void CompareCommand_Run_InvalidGrid_ThrowsUsageError(string grid)
    {
        // Arrange: 8x6 images
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act
        var exception = RunFailing(a, b, "--grid", grid);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
    }

    /// <summary>
    ///     Test that a threshold that is met leaves the command successful and is listed in the text result.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ThresholdMet_Succeeds()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act
        var stdOut = Run(false, a, b, "--threshold", "rmse=10", "--threshold", "max-diff=10");

        // Assert: one line per threshold
        var lines = stdOut.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("threshold rmse <= 10: passed (5.77)", lines[^2]);
        Assert.Equal("threshold max-diff <= 10: passed (10)", lines[^1]);
    }

    /// <summary>
    ///     Test that a threshold that is not met fails after the result and the pictures were written.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ThresholdNotMet_WritesResultThenThrowsThresholdFailed()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);
        var diff = directory.Combine("diff.png");

        // Act
        var (exception, stdOut) = RunFailingWithOutput(false, a, b, "--diff", diff, "--threshold", "rmse=1", "--threshold", "ssim=0.5");

        // Assert: the expected error names the failed check only; the result and the picture were written first
        Assert.Equal(ErrorCodes.ThresholdFailed, exception.Code);
        Assert.Equal("rmse 5.77 is above the limit 1", exception.Message);
        Assert.NotNull(exception.Hint);
        var lines = stdOut.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("compared ", lines[0]);
        Assert.Contains(lines, line => line.StartsWith($"wrote {diff} (8x6 png, ", StringComparison.Ordinal) && line.EndsWith("[diff]", StringComparison.Ordinal));
        Assert.Equal("threshold rmse <= 1: failed (5.77)", lines[^2]);
        Assert.StartsWith("threshold ssim >= 0.5: passed (", lines[^1]);
        Assert.True(File.Exists(diff));
    }

    /// <summary>
    ///     Test that in JSON mode a missed threshold writes no success document and carries the result on the exception.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ThresholdNotMetWithJson_ThrowsWithResultFieldsAndNoSuccessDocument()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);
        var overlay = directory.Combine("overlay.png");

        // Act
        var (exception, stdOut) = RunFailingWithOutput(true, a, b, "--overlay", overlay, "--threshold", "psnr=60");

        // Assert: nothing was written to standard output, and the picture was written
        Assert.Equal(ErrorCodes.ThresholdFailed, exception.Code);
        Assert.Equal(string.Empty, stdOut);
        Assert.True(File.Exists(overlay));
        Assert.NotNull(exception.ResultFields);

        // Assert: the failure document has the error and then the result fields
        exception.Command = "compare";
        using var document = JsonDocument.Parse(ResultWriter.Failure("compare", exception));
        var root = document.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal(ErrorCodes.ThresholdFailed, root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            ["schemaVersion", "ok", "command", "error", "tool", "a", "b", "size", "options", "metrics", "grid", "worstCells", "thresholds", "outputs", "diagnostics", "echo"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.False(root.GetProperty("thresholds").GetProperty("passed").GetBoolean());
        var check = Assert.Single(root.GetProperty("thresholds").GetProperty("checks").EnumerateArray());
        Assert.Equal("psnr", check.GetProperty("metric").GetString());
        Assert.Equal(">=", check.GetProperty("comparison").GetString());
        Assert.Equal(60, check.GetProperty("limit").GetDouble());
        Assert.False(check.GetProperty("passed").GetBoolean());
        Assert.Equal(48, root.GetProperty("metrics").GetProperty("diffPixels").GetInt64());
        Assert.Equal("overlay", Assert.Single(root.GetProperty("outputs").EnumerateArray()).GetProperty("kind").GetString());
    }

    /// <summary>
    ///     Test that every metric passes on its own side of the limit and fails on the other.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="passes"><see langword="true"/> when the measured value is within the limit.</param>
    [Theory]
    [InlineData("mae", "3.4", true)]
    [InlineData("mae", "3.3", false)]
    [InlineData("rmse", "5.8", true)]
    [InlineData("rmse", "5.7", false)]
    [InlineData("max-diff", "10", true)]
    [InlineData("max-diff", "9", false)]
    [InlineData("diff-ratio", "1", true)]
    [InlineData("diff-ratio", "0.99", false)]
    [InlineData("cell-rmse", "5.8", true)]
    [InlineData("cell-rmse", "5.7", false)]
    [InlineData("psnr", "32", true)]
    [InlineData("psnr", "33.5", false)]
    [InlineData("ssim", "0.9999", true)]
    [InlineData("ssim", "0.99999", false)]
    public void CompareCommand_Run_Thresholds_EachMetricDirection(string metric, string limit, bool passes)
    {
        // Arrange: MAE 3.33, RMSE 5.77, maximum 10, every pixel differs, PSNR 32.9, SSIM 0.99995, worst cell RMSE 5.77
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act
        (ToolException? Exception, string StdOut) outcome = passes
            ? (null, Run(true, a, b, "--grid", "2x2", "--threshold", $"{metric}={limit}"))
            : RunFailingWithOutput(false, a, b, "--grid", "2x2", "--threshold", $"{metric}={limit}");

        // Assert: success, or the expected failure
        if (passes)
        {
            using var document = JsonDocument.Parse(outcome.StdOut);
            Assert.True(document.RootElement.GetProperty("thresholds").GetProperty("passed").GetBoolean());
        }
        else
        {
            Assert.Equal(ErrorCodes.ThresholdFailed, outcome.Exception!.Code);
            Assert.StartsWith($"{metric} ", outcome.Exception.Message);
        }
    }

    /// <summary>
    ///     Test that a malformed threshold is a usage error.
    /// </summary>
    /// <param name="threshold">The value of <c>--threshold</c>.</param>
    [Theory]
    [InlineData("bogus=1")]
    [InlineData("rmse")]
    [InlineData("rmse=")]
    [InlineData("=1")]
    [InlineData("rmse=abc")]
    [InlineData("rmse=300")]
    [InlineData("mae=-1")]
    [InlineData("ssim=2")]
    [InlineData("diff-ratio=1.5")]
    [InlineData("psnr=-1")]
    public void CompareCommand_Run_InvalidThreshold_ThrowsUsageError(string threshold)
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act
        var exception = RunFailing(a, b, "--threshold", threshold);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
    }

    /// <summary>
    ///     Test that giving the same metric twice is a usage error.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_DuplicateThreshold_ThrowsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);

        // Act
        var exception = RunFailing(a, b, "--threshold", "rmse=1", "--threshold", "RMSE=2");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("more than once", exception.Message);
    }

    /// <summary>
    ///     Test that anything other than two inputs is a usage error.
    /// </summary>
    /// <param name="count">The number of inputs given.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void CompareCommand_Run_WrongNumberOfInputs_ThrowsUsageError(int count)
    {
        // Arrange: some inputs
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);
        var inputs = new[] { a, b, a }.Take(count).ToArray();

        // Act
        var exception = RunFailing(inputs);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("two input files", exception.Message);
    }

    /// <summary>
    ///     Test that a missing input is an expected error.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_MissingInput_ThrowsInputNotFound()
    {
        // Arrange: one input only
        using var directory = new TempDirectory();
        var (a, _) = SavePair(directory);

        // Act
        var exception = RunFailing(a, directory.Combine("missing.png"));

        // Assert
        Assert.Equal(ErrorCodes.InputNotFound, exception.Code);
    }

    /// <summary>
    ///     Test that a corrupt second input is an expected error, and that no picture is written.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_CorruptSecondInput_ThrowsCorruptInputAndWritesNothing()
    {
        // Arrange: a PNG cut after 30 bytes
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);
        File.WriteAllBytes(b, File.ReadAllBytes(b)[..30]);
        var diff = directory.Combine("diff.png");

        // Act
        var exception = RunFailing(a, b, "--diff", diff);

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
        Assert.False(File.Exists(diff));
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create([]);
        var command = new CompareCommand();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => command.Run(null!, []));
        Assert.Throws<ArgumentNullException>(() => command.Run(context, null!));
    }

    /// <summary>
    ///     Test that the same inputs and options give the same result, text, and picture bytes every time.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_SameInputsTwice_WriteIdenticalJsonAndBytes()
    {
        // Arrange: a pair and every picture
        using var directory = new TempDirectory();
        var (a, b) = SavePair(directory);
        string[] args = [a, b, "--diff", directory.Combine("diff.png"), "--side-by-side", directory.Combine("sbs.png"), "--overlay", directory.Combine("overlay.png"), "--grid", "2x2"];

        // Act
        var firstJson = Run(true, args);
        var first = args.Where((_, i) => i is 3 or 5 or 7).Select(File.ReadAllBytes).ToList();
        var secondJson = Run(true, args);
        var second = args.Where((_, i) => i is 3 or 5 or 7).Select(File.ReadAllBytes).ToList();
        var firstText = Run(false, args);
        var secondText = Run(false, args);

        // Assert
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(firstText, secondText);
        Assert.Equal(3, first.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i], second[i]);
        }
    }
}
