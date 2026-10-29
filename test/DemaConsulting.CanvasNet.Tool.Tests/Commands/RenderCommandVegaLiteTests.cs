using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the RenderCommand class with Vega-Lite charts: the result, the sizing, the diagnostics, strict
///     mode, and the errors.
/// </summary>
[Collection("Sequential")]
public class RenderCommandVegaLiteTests
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
    ///     Test that a chart renders to a PNG of its natural size, with a result that names the format, the natural
    ///     size, and the render operation, and with the bars where the layout puts them.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLite_WritesPngAndReportsFormat()
    {
        // Arrange: the two-bar chart of the tests
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "bars.vl.json", FixtureFactory.TwoBarChart);
        var output = directory.Combine("bars.png");

        // Act
        var stdOut = Run(true, input, "-o", output);

        // Assert: the document names the format and the natural size, which is the size of the file
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("vega-lite", root.GetProperty("input").GetProperty("format").GetString());
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.True(image.Width > 200 && image.Height > 100);
        Assert.Equal(["render"], root.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("op").GetString()));
        Assert.Equal("UTC", root.GetProperty("operations")[0].GetProperty("timeZone").GetString());
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());

        // The corner is the white background; the second bar is the default mark color
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.Equal(new Rgba32(0x4C, 0x78, 0xA8, 255), image[image.Width - 5 - 50, 15]);
    }

    /// <summary>
    ///     Test that the size options fit a chart: a scale multiplies the natural size, a width keeps the aspect ratio,
    ///     and both sides give the exact size with the chart centered.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteSizeOptions_FollowTheNaturalSize()
    {
        // Arrange: the fixture chart and its natural size
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "vega-lite");
        var natural = DiscoveryCommandsChartTests.NaturalSize(input);

        // Act
        Run(false, input, "--scale", "2", "-o", directory.Combine("scaled.png"));
        Run(false, input, "--width", "100", "-o", directory.Combine("width.png"));
        Run(false, input, "--size", "90x50", "-o", directory.Combine("size.png"));

        // Assert
        var expectedHeight = (int)Math.Round(100.0 * natural.Height / natural.Width, MidpointRounding.AwayFromZero);
        foreach (var (name, expected) in new[]
                 {
                     ("scaled", (natural.Width * 2, natural.Height * 2)),
                     ("width", (100, expectedHeight)),
                     ("size", (90, 50))
                 })
        {
            using var image = PngCodec.Load(directory.Combine(name + ".png"));
            Assert.Equal(expected, (image.Width, image.Height));
        }
    }

    /// <summary>
    ///     Test that every output format can be written from a chart, and that a transparent background is flattened
    ///     over white for a JPEG.
    /// </summary>
    /// <param name="extension">The extension of the output.</param>
    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".bmp")]
    [InlineData(".tif")]
    public void RenderCommand_Run_VegaLiteToEachOutputFormat_WritesDecodableFile(string extension)
    {
        // Arrange: the fixture chart
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "vega-lite");
        var output = directory.Combine("out" + extension);

        // Act
        Run(false, input, "--background", "transparent", "-o", output);

        // Assert: a decodable file; a transparent PNG keeps its clear corner, the others are flattened over white
        using var image = SurfaceAssert.Load(output);
        if (extension is ".png" or ".bmp" or ".tif")
        {
            Assert.Equal(0, image[0, 0].A);
        }
        else
        {
            Assert.InRange(image[0, 0].R, 240, 255);
        }
    }

    /// <summary>
    ///     Test that the same chart and options give the same bytes on every run.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteTwice_WritesIdenticalBytes()
    {
        // Arrange: the fixture chart
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "vega-lite");

        // Act
        foreach (var name in new[] { "a", "b" })
        {
            Run(false, input, "--scale", "1.5", "-o", directory.Combine(name + ".png"));
        }

        // Assert
        Assert.Equal(File.ReadAllBytes(directory.Combine("a.png")), File.ReadAllBytes(directory.Combine("b.png")));
    }

    /// <summary>
    ///     Test that the warnings and unsupported features of a chart are in the JSON result, and that strict mode
    ///     makes the unsupported feature an error and writes nothing.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteDiagnosticsAndStrict_AreReportedOrFail()
    {
        // Arrange: a chart with an unknown property and a mark the library does not draw
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(
            directory.Path,
            "noisy.vl.json",
            "{\"bogus\": 4, \"data\": {\"values\": [{\"v\": 1}]}, \"mark\": \"arc\", \"encoding\": {\"theta\": {\"field\": \"v\", \"type\": \"quantitative\"}}}");
        var output = directory.Combine("o.png");

        // Act
        var stdOut = Run(true, input, "-o", output);
        var strict = RunFailing(input, "--strict", "-o", directory.Combine("strict.png"));

        // Assert: the warning, the unsupported mark, and its unsupported channel, in the documented shape, and the strict failure writes nothing
        using var document = JsonDocument.Parse(stdOut);
        var notes = document.RootElement.GetProperty("diagnostics").EnumerateArray().ToList();
        Assert.Equal(3, notes.Count);
        Assert.Equal(("warning", "vega-lite", "vega-lite-warning"), (
            notes[0].GetProperty("severity").GetString(), notes[0].GetProperty("source").GetString(), notes[0].GetProperty("code").GetString()));
        Assert.Equal(("unsupported", "vega-lite", "vl-mark-arc"), (
            notes[1].GetProperty("severity").GetString(), notes[1].GetProperty("source").GetString(), notes[1].GetProperty("code").GetString()));
        Assert.False(notes[1].TryGetProperty("page", out _));
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.False(File.Exists(directory.Combine("strict.png")));
    }

    /// <summary>
    ///     Test that a chart with an error is a <c>vega-lite-error</c> with the location, through the entry point, and
    ///     that no output is written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteError_UsesStableErrorCode()
    {
        // Arrange: a specification that is not valid JSON, and plain Vega named like a chart
        using var directory = new TempDirectory();
        var broken = FixtureFactory.WriteText(directory.Path, "broken.vl.json", "{\"mark\": ");
        var vega = FixtureFactory.WriteText(directory.Path, "vega.vl.json", "{\"$schema\": \"https://vega.github.io/schema/vega/v5.json\"}");
        var output = directory.Combine("o.png");

        // Act: in-process through the command, and through the entry point for the document
        var direct = RunFailing(broken, "-o", output);
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", vega, "-o", output, "--json");

        // Assert: the code, one line with the file, and no output
        Assert.Equal(ErrorCodes.VegaLiteError, direct.Code);
        Assert.Contains(broken, direct.Message);
        Assert.DoesNotContain('\n', direct.Message);
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: vega-lite-error:", stdErr);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal("vega-lite-error", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a page option beyond the single page of a chart is out of range, and that an option of a model is
    ///     refused for a chart.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteWithPageTwoOrModelOption_Fails()
    {
        // Arrange: the fixture chart
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "vega-lite");

        // Act
        var page = RunFailing(input, "--page", "2", "-o", directory.Combine("o.png"));
        var model = RunFailing(input, "-D", "x=1", "-o", directory.Combine("o.png"));

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, page.Code);
        Assert.Equal(ErrorCodes.UsageError, model.Code);
        Assert.Contains("is a vega-lite file", model.Message);
    }

    /// <summary>
    ///     Test that the usage text of the render command names Vega-Lite charts.
    /// </summary>
    [Fact]
    public void RenderCommand_Usage_NamesVegaLite()
    {
        // Arrange / Act
        var usage = new RenderCommand().Usage;

        // Assert
        Assert.Contains("Vega-Lite", usage);
        Assert.Contains("canvasnet render chart.vl.json", usage);

        // Assert: the help says where chart data comes from, and that there is no option for another folder
        Assert.Contains("folder of the specification", usage);
        Assert.Contains("no data-folder option", usage);
        Assert.Contains("nothing is read from the", usage);
    }

    /// <summary>
    ///     Test that <c>render</c> reads the data file of a chart from the folder of the specification (not the current
    ///     directory) and writes the same picture as the chart with inline rows, that a missing file is a
    ///     <c>vega-lite-error</c> that writes nothing, that remote data is only a diagnostic (an error with
    ///     <c>--strict</c>), and that there is no option for another data folder.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteLocalDataResolvesRelativeToSpecFolder()
    {
        // Arrange: a specification and its CSV in a sub folder, the inline chart as the reference, and two broken charts
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "charts/bars.vl.json", FixtureFactory.TwoBarFileChart);
        FixtureFactory.WriteText(directory.Path, "charts/bars.csv", FixtureFactory.TwoBarCsv);
        var inline = FixtureFactory.WriteText(directory.Path, "inline.vl.json", FixtureFactory.TwoBarChart);
        var missing = FixtureFactory.WriteText(directory.Path, "charts/missing.vl.json", FixtureFactory.TwoBarFileChart.Replace("bars.csv", "gone.csv", StringComparison.Ordinal));
        var remote = FixtureFactory.WriteText(directory.Path, "charts/remote.vl.json", FixtureFactory.TwoBarFileChart.Replace("bars.csv", "http://example.invalid/bars.csv", StringComparison.Ordinal));

        // Act
        Run(false, input, "-o", directory.Combine("file.png"));
        Run(false, inline, "-o", directory.Combine("inline.png"));
        var notFound = RunFailing(missing, "-o", directory.Combine("missing.png"));
        var stdOut = Run(true, remote, "-o", directory.Combine("remote.png"));
        var strict = RunFailing(remote, "--strict", "-o", directory.Combine("strict.png"));
        var dataDir = RunFailing(input, "--data-dir", directory.Path, "-o", directory.Combine("dir.png"));

        // Assert: the chart of the file is the chart of the rows, byte for byte
        Assert.Equal(File.ReadAllBytes(directory.Combine("inline.png")), File.ReadAllBytes(directory.Combine("file.png")));

        // Assert: a missing file is an error of the chart, on one line, and nothing is written
        Assert.Equal(ErrorCodes.VegaLiteError, notFound.Code);
        Assert.Contains("was not found", notFound.Message);
        Assert.DoesNotContain('\n', notFound.Message);
        Assert.False(File.Exists(directory.Combine("missing.png")));

        // Assert: remote data is a diagnostic and an empty chart; strict makes it an error and writes nothing
        using var document = JsonDocument.Parse(stdOut);
        var codes = document.RootElement.GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("code").GetString());
        Assert.Contains("vl-data-remote-url", codes);
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.False(File.Exists(directory.Combine("strict.png")));

        // Assert: this phase adds no option for another data folder
        Assert.Equal(ErrorCodes.UsageError, dataDir.Code);
    }
}
