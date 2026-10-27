// cspell:ignore canvasnet jpeg jpg tiff tif exif qualty deskew
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the RenderCommand class (the render command and its alias convert).
/// </summary>
[Collection("Sequential")]
public class RenderCommandTests
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
    ///     Writes a PNG with transparency, the input of several tests.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The path of the file.</returns>
    private static string WriteTranslucentPng(TempDirectory directory)
    {
        var path = directory.Combine("translucent.png");
        using var surface = FixtureFactory.CreateTranslucent();
        PngCodec.Save(surface, path);
        return path;
    }

    /// <summary>
    ///     Test that the command describes itself, with its alias and every option the plan declares.
    /// </summary>
    [Fact]
    public void RenderCommand_Metadata_DescribesCommandAndOptions()
    {
        // Arrange / Act: create the command
        var command = new RenderCommand();

        // Assert: name, alias, summary, usage with examples, and the declared options
        Assert.Equal("render", command.Name);
        Assert.Equal(["convert"], command.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
        Assert.StartsWith("canvasnet render <input> -o <output> [options]", command.Usage);
        Assert.Contains("Examples:", command.Usage);
        Assert.Equal(
            [
                "--page", "--all-pages", "--dpi", "--scale", "--width", "--height", "--size", "--background", "--no-auto-orient",
                "--strict", "--view", "--azimuth", "--elevation", "--projection", "--define", "--time", "--default-color",
                "--max-triangles", "--crop", "--resize", "--fit", "--allow-upscale", "--rotate", "--rotate-fill", "--flip", "--pad",
                "--pad-color", "--grayscale", "--invert", "--threshold", "--levels", "--brightness", "--contrast", "--auto-levels",
                "--auto-levels-clip", "--deskew", "--trim", "--trim-tolerance", "--grid", "--filter", "--output", "--format",
                "--quality", "--no-clobber"
            ],
            command.Options.Select(o => o.Name));
        Assert.Equal(["-o"], command.Options.Single(o => o.Name == "--output").Aliases);
        Assert.All(command.Options, o => Assert.Equal(o.TakesValue, o.ValueName != null));
        Assert.All(command.Options, o => Assert.False(string.IsNullOrWhiteSpace(o.Description)));
    }

    /// <summary>
    ///     Test that the default registry offers the command under both names.
    /// </summary>
    [Fact]
    public void RenderCommand_DefaultRegistry_RegistersRenderAndConvert()
    {
        // Arrange / Act: the default registry
        var registry = CommandRegistry.CreateDefault();

        // Assert: both names resolve to the render command
        Assert.IsType<RenderCommand>(registry.Find("render"));
        Assert.Same(registry.Find("render"), registry.Find("convert"));
    }

    /// <summary>
    ///     Test that a successful run writes the output and one text line naming it.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PngToPng_WritesFileAndTextLine()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act: render it
        var stdOut = Run(false, input, "-o", output);

        // Assert: one line naming the output, its size, format, bytes, and the input; the file holds the pixels
        var bytes = new FileInfo(output).Length;
        Assert.Equal($"wrote {output} (12x8 png, {bytes} bytes) from {input}{Environment.NewLine}", stdOut);
        using var expected = FixtureFactory.CreateGradient();
        using var decoded = PngCodec.Load(output);
        SurfaceAssert.Equal(expected, decoded);
    }

    /// <summary>
    ///     Test that the JSON document has the documented shape for a raster.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_Json_HasDocumentedShape()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.bmp");

        // Act: render it with JSON output
        var stdOut = Run(true, input, "-o", output);

        // Assert: the envelope, the tool, the input, no operations, the output, and empty diagnostics and echo
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("render", root.GetProperty("command").GetString());
        Assert.Equal("canvasnet", root.GetProperty("tool").GetProperty("name").GetString());
        Assert.Equal(Program.Version, root.GetProperty("tool").GetProperty("version").GetString());
        var inputElement = root.GetProperty("input");
        Assert.Equal(input, inputElement.GetProperty("path").GetString());
        Assert.Equal("png", inputElement.GetProperty("format").GetString());
        Assert.Equal(12, inputElement.GetProperty("natural").GetProperty("width").GetInt32());
        Assert.Equal(8, inputElement.GetProperty("natural").GetProperty("height").GetInt32());
        Assert.Equal(0, root.GetProperty("operations").GetArrayLength());
        var written = Assert.Single(root.GetProperty("outputs").EnumerateArray());
        Assert.Equal(output, written.GetProperty("path").GetString());
        Assert.Equal("bmp", written.GetProperty("format").GetString());
        Assert.Equal(12, written.GetProperty("width").GetInt32());
        Assert.Equal(8, written.GetProperty("height").GetInt32());
        Assert.Equal(new FileInfo(output).Length, written.GetProperty("bytes").GetInt64());
        Assert.Equal(0, root.GetProperty("diagnostics").GetArrayLength());
        Assert.Equal(0, root.GetProperty("echo").GetArrayLength());
    }

    /// <summary>
    ///     Test that the operations of an SVG render are listed in order with their parameters.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgJson_ListsRenderAndFlattenOperations()
    {
        // Arrange: the SVG fixture
        using var directory = new TempDirectory();
        var output = directory.Combine("out.png");

        // Act: render it at a width of 50
        var stdOut = Run(true, FixtureFactory.FixturePath("shapes.svg"), "--width", "50", "-o", output);

        // Assert: the operations are exactly the render at 50x40 and the flatten over white
        using var document = JsonDocument.Parse(stdOut);
        Assert.Contains(
            "\"operations\":[{\"op\":\"render\",\"width\":50,\"height\":40},{\"op\":\"flatten\",\"background\":\"#FFFFFF\"}]",
            stdOut);
        Assert.Equal("svg", document.RootElement.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal(100, document.RootElement.GetProperty("input").GetProperty("natural").GetProperty("width").GetInt32());
    }

    /// <summary>
    ///     Test that rendering a transparent image to JPEG flattens it, reports the flatten, and uses a given background.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_TransparentToJpeg_FlattensOverBackground()
    {
        // Arrange: a PNG with transparency
        using var directory = new TempDirectory();
        var input = WriteTranslucentPng(directory);

        // Act: render to JPEG by default (over white), and over a given color
        var defaultJson = Run(true, input, "-o", directory.Combine("white.jpg"));
        var colorJson = Run(true, input, "--background", "#336699", "-o", directory.Combine("blue.jpg"));

        // Assert: both report a flatten; the pixel at the transparent edge is white, then the color
        Assert.Contains("{\"op\":\"flatten\",\"background\":\"#FFFFFF\"}", defaultJson);
        Assert.Contains("{\"op\":\"flatten\",\"background\":\"#336699\"}", colorJson);
        using var white = JpegCodec.Load(directory.Combine("white.jpg"));
        using var blue = JpegCodec.Load(directory.Combine("blue.jpg"));
        Assert.InRange(white[FixtureFactory.Width - 1, 0].R, 240, 255);
        Assert.InRange(blue[FixtureFactory.Width - 1, 0].B, 130, 175);
        Assert.InRange(blue[FixtureFactory.Width - 1, 0].R, 30, 70);
    }

    /// <summary>
    ///     Test that a PNG output keeps transparency by default and a transparent background keeps an SVG's alpha.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_BackgroundTransparent_KeepsAlpha()
    {
        // Arrange: the SVG fixture
        using var directory = new TempDirectory();
        var svg = FixtureFactory.FixturePath("shapes.svg");

        // Act: render over white (the default) and with a transparent background
        Run(false, svg, "-o", directory.Combine("white.png"));
        Run(false, svg, "--background", "transparent", "-o", directory.Combine("clear.png"));

        // Assert: white where the drawing is empty, then transparent
        using var white = PngCodec.Load(directory.Combine("white.png"));
        using var clear = PngCodec.Load(directory.Combine("clear.png"));
        Assert.Equal(new Rgba32(255, 255, 255, 255), white[5, 5]);
        Assert.Equal(0, clear[5, 5].A);
    }

    /// <summary>
    ///     Test the size options: scale, width, height, and size.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">Its value.</param>
    /// <param name="expectedWidth">The expected output width.</param>
    /// <param name="expectedHeight">The expected output height.</param>
    [Theory]
    [InlineData("--scale", "2", 24, 16)]
    [InlineData("--width", "6", 6, 4)]
    [InlineData("--height", "4", 6, 4)]
    [InlineData("--size", "5x7", 5, 7)]
    public void RenderCommand_Run_SizeOption_ResizesRaster(string option, string value, int expectedWidth, int expectedHeight)
    {
        // Arrange: the 12x8 PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act: render with the size option
        Run(false, input, option, value, "-o", output);

        // Assert: the requested size
        using var decoded = PngCodec.Load(output);
        Assert.Equal((expectedWidth, expectedHeight), (decoded.Width, decoded.Height));
    }

    /// <summary>
    ///     Test that width and height together are exact and that the options are accepted as <c>--name=value</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_WidthAndHeightWithEquals_AreExact()
    {
        // Arrange: the 12x8 PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act: render with both options written with an equals sign
        Run(false, input, "--width=10", "--height=3", $"--output={output}");

        // Assert: exactly 10x3
        using var decoded = PngCodec.Load(output);
        Assert.Equal((10, 3), (decoded.Width, decoded.Height));
    }

    /// <summary>
    ///     Test that contradicting or malformed sizing options are usage errors.
    /// </summary>
    /// <param name="args">The extra arguments.</param>
    [Theory]
    [InlineData("--scale", "2", "--width", "10")]
    [InlineData("--scale", "2", "--height", "10")]
    [InlineData("--size", "10x10", "--width", "10")]
    [InlineData("--size", "10x10", "--height", "10")]
    [InlineData("--size", "10x10", "--scale", "2")]
    [InlineData("--size", "10")]
    [InlineData("--width", "0")]
    [InlineData("--width", "ten")]
    [InlineData("--height", "-5")]
    [InlineData("--scale", "0")]
    [InlineData("--scale", "fast")]
    [InlineData("--background", "not-a-color")]
    public void RenderCommand_Run_InvalidSizeOrBackground_ThrowsUsageError(params string[] args)
    {
        // Arrange: a PNG and an output that must not be created
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act
        var exception = RunFailing([input, .. args, "-o", output]);

        // Assert: a usage error and no output
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a requested size beyond the maximum is a size-limit error and writes nothing.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SizeBeyondMaximum_ThrowsSizeLimit()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");

        // Act
        var viaSize = RunFailing(input, "--size", "9000x10", "-o", output);
        var viaWidth = RunFailing(input, "--width", "9000", "-o", output);

        // Assert: size-limit, and no output
        Assert.Equal(ErrorCodes.SizeLimit, viaSize.Code);
        Assert.Equal(ErrorCodes.SizeLimit, viaWidth.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the quality option changes a JPEG and is checked.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_Quality_ChangesJpegSizeAndIsValidated()
    {
        // Arrange: a PNG of some size
        using var directory = new TempDirectory();
        var input = directory.Combine("big.png");
        using (var surface = FixtureFactory.CreateGradient(64, 64))
        {
            PngCodec.Save(surface, input);
        }

        // Act: render at two qualities
        Run(false, input, "--quality", "5", "-o", directory.Combine("low.jpg"));
        Run(false, input, "--quality=100", "-o", directory.Combine("high.jpg"));

        // Assert: the higher quality is larger; values outside 1-100 and non-numbers are usage errors
        Assert.True(new FileInfo(directory.Combine("high.jpg")).Length > new FileInfo(directory.Combine("low.jpg")).Length);
        foreach (var bad in new[] { "0", "101", "high" })
        {
            Assert.Equal(ErrorCodes.UsageError, RunFailing(input, "--quality", bad, "-o", directory.Combine("bad.jpg")).Code);
        }

        Assert.False(File.Exists(directory.Combine("bad.jpg")));
    }

    /// <summary>
    ///     Test that the format option overrides the extension and is checked before anything is loaded.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_FormatOption_OverridesExtension()
    {
        // Arrange: a PNG, and an output with an extension that means nothing
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.dat");

        // Act: render with --format jpeg
        var stdOut = Run(true, input, "--format", "jpeg", "-o", output);

        // Assert: a JPEG was written; an unknown format and an unknown extension are usage errors
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal("jpeg", document.RootElement.GetProperty("outputs")[0].GetProperty("format").GetString());
        Assert.Equal(ErrorCodes.UsageError, RunFailing(input, "--format", "gif", "-o", directory.Combine("x.png")).Code);
        Assert.Equal(ErrorCodes.UsageError, RunFailing(input, "-o", directory.Combine("x.xyz")).Code);
        Assert.Equal(ErrorCodes.UsageError, RunFailing(input, "-o", directory.Combine("no-extension")).Code);
    }

    /// <summary>
    ///     Test that an existing output is overwritten by default and kept with no-clobber.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ExistingOutput_OverwrittenUnlessNoClobber()
    {
        // Arrange: an input and an existing output
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "old");

        // Act: refuse with --no-clobber, then overwrite by default
        var refused = RunFailing(input, "-o", output, "--no-clobber");
        var keptContent = File.ReadAllText(output);
        Run(false, input, "-o", output);

        // Assert: output-exists with the file untouched, then a real image
        Assert.Equal(ErrorCodes.OutputExists, refused.Code);
        Assert.Equal("old", keptContent);
        using var decoded = PngCodec.Load(output);
        Assert.Equal(FixtureFactory.Width, decoded.Width);
    }

    /// <summary>
    ///     Test that output mistakes are reported before the input is decoded.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_OutputProblemWithHugeInput_ReportsOutputProblemFirst()
    {
        // Arrange: an input that would fail with size-limit when decoded, and an existing output
        using var directory = new TempDirectory();
        var input = directory.Combine("huge.png");
        File.WriteAllBytes(input, FixtureFactory.CreatePngHeaderOnly(9000, 9000));
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "old");

        // Act: render with --no-clobber
        var exception = RunFailing(input, "-o", output, "--no-clobber");

        // Assert: the output problem is reported, not the input's size
        Assert.Equal(ErrorCodes.OutputExists, exception.Code);
    }

    /// <summary>
    ///     Test that an output that is the input is refused and the input is untouched.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_OutputIsInput_ThrowsSameInputOutput()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var before = File.ReadAllBytes(input);

        // Act
        var exception = RunFailing(input, "-o", input);

        // Assert: refused, input untouched
        Assert.Equal(ErrorCodes.SameInputOutput, exception.Code);
        Assert.Equal(before, File.ReadAllBytes(input));
    }

    /// <summary>
    ///     Test that a missing output directory is created.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_OutputInNewDirectory_CreatesDirectory()
    {
        // Arrange: a PNG and an output in a directory that does not exist
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = Path.Combine(directory.Path, "new", "folder", "out.png");

        // Act
        Run(false, input, "-o", output);

        // Assert: written
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that a JPEG is turned upright by default and left alone with no-auto-orient.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_JpegWithExif_AutoOrientUnlessDisabled()
    {
        // Arrange: a 12x8 JPEG with the EXIF orientation 6
        using var directory = new TempDirectory();
        var input = directory.Combine("camera.jpg");
        using (var surface = FixtureFactory.CreateGradient())
        using (var stream = new MemoryStream())
        {
            JpegCodec.Save(surface, stream);
            File.WriteAllBytes(input, FixtureFactory.InsertExifOrientation(stream.ToArray(), 6, true));
        }

        // Act
        var upright = Run(true, input, "-o", directory.Combine("upright.png"));
        Run(false, input, "--no-auto-orient", "-o", directory.Combine("stored.png"));

        // Assert: 8x12 upright with the operation reported, and 12x8 as stored
        using var uprightImage = PngCodec.Load(directory.Combine("upright.png"));
        using var storedImage = PngCodec.Load(directory.Combine("stored.png"));
        Assert.Equal((8, 12), (uprightImage.Width, uprightImage.Height));
        Assert.Equal((12, 8), (storedImage.Width, storedImage.Height));
        Assert.Contains("{\"op\":\"auto-orient\",\"orientation\":6}", upright);
    }

    /// <summary>
    ///     Test that a missing, corrupt, or unsupported input is an expected error and writes no output.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_BadInput_ThrowsToolExceptionAndWritesNothing()
    {
        // Arrange: a missing file, a truncated PNG, a text file, and a directory
        using var directory = new TempDirectory();
        var truncated = FixtureFactory.WriteSource(directory.Path, "png");
        File.WriteAllBytes(truncated, File.ReadAllBytes(truncated)[..30]);
        var text = directory.Combine("notes.txt");
        File.WriteAllText(text, "notes");
        var output = directory.Combine("out.png");

        // Act
        var missing = RunFailing(directory.Combine("missing.png"), "-o", output);
        var corrupt = RunFailing(truncated, "-o", output);
        var unsupported = RunFailing(text, "-o", output);
        var folder = RunFailing(directory.Path, "-o", output);

        // Assert: the codes, and no output
        Assert.Equal(ErrorCodes.InputNotFound, missing.Code);
        Assert.Equal(ErrorCodes.CorruptInput, corrupt.Code);
        Assert.Equal(ErrorCodes.UnsupportedFormat, unsupported.Code);
        Assert.Equal(ErrorCodes.InputUnreadable, folder.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that command-line mistakes are usage errors.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_CommandLineMistakes_ThrowUsageError()
    {
        // Arrange / Act
        var noOutput = RunFailing("in.png");
        var noInput = RunFailing("-o", "out.png");
        var twoInputs = RunFailing("a.png", "b.png", "-o", "out.png");
        var unknown = RunFailing("in.png", "-o", "out.png", "--qualty", "50");
        var flagWithValue = RunFailing("in.png", "-o", "out.png", "--no-clobber=yes");
        var missingValue = RunFailing("in.png", "-o");

        // Assert: usage errors with specific messages
        Assert.All(new[] { noOutput, noInput, twoInputs, unknown, flagWithValue, missingValue }, e => Assert.Equal(ErrorCodes.UsageError, e.Code));
        Assert.Contains("No output file was given", noOutput.Message);
        Assert.Contains("-o <file>", noOutput.Hint);
        Assert.Contains("expects one input file", noInput.Message);
        Assert.Contains("expects one input file", twoInputs.Message);
        Assert.Contains("Unknown option '--qualty'", unknown.Message);
        Assert.Contains("--quality", unknown.Hint);
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange: the command and a context
        using var context = Context.Create([]);
        var command = new RenderCommand();

        // Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => command.Run(null!, []));
        Assert.Throws<ArgumentNullException>(() => command.Run(context, null!));
    }
}
