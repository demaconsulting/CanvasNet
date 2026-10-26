// cspell:ignore canvasnet scad openscad jpeg jpg tif tiff Dlabel
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the RenderCommand class with OpenSCAD models: the views, the model options, the result,
///     the diagnostics, and the errors.
/// </summary>
[Collection("Sequential")]
public class RenderCommandScadTests
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
    ///     Test that a model renders at 800 by 800 pixels by default, with a text line like any other source.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadDefaults_Writes800SquareImage()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");
        var output = directory.Combine("out.png");

        // Act
        var stdOut = Run(false, input, "-o", output);

        // Assert: one line, no page, an 800x800 file with the model in the middle over white
        Assert.Equal($"wrote {output} (800x800 png, {new FileInfo(output).Length} bytes) from {input}{Environment.NewLine}", stdOut);
        using var image = PngCodec.Load(output);
        Assert.Equal((800, 800), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[2, 2]);
        Assert.NotEqual(new Rgba32(255, 255, 255, 255), image[400, 400]);
    }

    /// <summary>
    ///     Test that each named view can be chosen, and that the views differ.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_EachView_WritesItsOwnImage()
    {
        // Arrange: the asymmetric model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "model.scad", FixtureFactory.AsymmetricModel);
        var files = new List<byte[]>();

        foreach (var view in new[] { "iso", "front", "back", "left", "right", "top", "bottom" })
        {
            // Act
            var output = directory.Combine($"{view}.png");
            Run(false, input, "--view", view, "--size", "48x48", "-o", output);
            files.Add(File.ReadAllBytes(output));
        }

        // Assert: seven different files
        Assert.Equal(7, files.Select(Convert.ToBase64String).Distinct().Count());
    }

    /// <summary>
    ///     Test that the view option takes its value after an equals sign too, in any case, and that an override is reported.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ViewAzimuthElevationProjection_AreReportedInTheOperation()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act: a view with an azimuth and a negative elevation override, and a perspective projection
        var stdOut = Run(true, input, "--view=TOP", "--azimuth", "30", "--elevation=-20", "--projection", "perspective", "--size", "32x32", "-o", directory.Combine("o.png"));

        // Assert: the resolved view, angles, and projection
        using var document = JsonDocument.Parse(stdOut);
        var render = document.RootElement.GetProperty("operations")[0];
        Assert.Equal("render", render.GetProperty("op").GetString());
        Assert.Equal("top", render.GetProperty("view").GetString());
        Assert.Equal(30, render.GetProperty("azimuth").GetDouble());
        Assert.Equal(-20, render.GetProperty("elevation").GetDouble());
        Assert.Equal("perspective", render.GetProperty("projection").GetString());
        Assert.Equal(32, render.GetProperty("width").GetInt32());
        Assert.Equal("#FFFFFF", render.GetProperty("background").GetString());
        Assert.False(render.TryGetProperty("defines", out _));
    }

    /// <summary>
    ///     Test that repeated definitions set variables, that the later one of a name wins, and that they change the image.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_Defines_ChangeTheImageAndAreReported()
    {
        // Arrange: the fixture model, whose width is a variable
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");
        var plain = directory.Combine("plain.png");
        var wide = directory.Combine("wide.png");
        Run(false, input, "--view", "front", "--size", "40x40", "-o", plain);

        // Act: two definitions of the same name, in the two spellings, and one of another kind
        var stdOut = Run(true, input, "--view", "front", "--size", "40x40", "-D", "width=5", "--define", "width=40", "-Dlabel=a b", "-o", wide);

        // Assert: a different picture, and one entry per name in name order
        Assert.NotEqual(File.ReadAllBytes(plain), File.ReadAllBytes(wide));
        using var document = JsonDocument.Parse(stdOut);
        var defines = document.RootElement.GetProperty("operations")[0].GetProperty("defines");
        Assert.Equal(["label=\"a b\"", "width=40"], defines.EnumerateArray().Select(e => e.GetString()));
    }

    /// <summary>
    ///     Test that the time, the default color, and the triangle limit are accepted and reported.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_TimeDefaultColorAndMaxTriangles_AreReported()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var stdOut = Run(true, input, "--time", "0.5", "--default-color", "#336699", "--max-triangles", "100000", "--size", "32x32", "-o", directory.Combine("o.png"));

        // Assert
        using var document = JsonDocument.Parse(stdOut);
        var render = document.RootElement.GetProperty("operations")[0];
        Assert.Equal(0.5, render.GetProperty("time").GetDouble());
        Assert.Equal("#336699", render.GetProperty("defaultColor").GetString());
        Assert.Equal(100000, render.GetProperty("maxTriangles").GetInt64());
    }

    /// <summary>
    ///     Test that the echo output, the warnings, and the unsupported features of the model are in the JSON result.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadDiagnosticsAndEcho_AreInTheJsonResult()
    {
        // Arrange: a model that echoes, warns, and uses an unsupported module
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "noisy.scad", "echo(\"hello\");\nmissing();\ntext(\"A\");\ncube(1);\n");

        // Act
        var stdOut = Run(true, input, "--size", "32x32", "-o", directory.Combine("o.png"));

        // Assert: the echo line and the two notes, in the documented shape
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(["ECHO: \"hello\""], document.RootElement.GetProperty("echo").EnumerateArray().Select(e => e.GetString()));
        var notes = document.RootElement.GetProperty("diagnostics").EnumerateArray().ToList();
        Assert.Equal(2, notes.Count);
        Assert.Equal("warning", notes[0].GetProperty("severity").GetString());
        Assert.Equal("scad", notes[0].GetProperty("source").GetString());
        Assert.Equal("scad-warning", notes[0].GetProperty("code").GetString());
        Assert.Equal("unsupported", notes[1].GetProperty("severity").GetString());
        Assert.Equal("scad-module-text", notes[1].GetProperty("code").GetString());
        Assert.False(notes[1].TryGetProperty("page", out _));
    }

    /// <summary>
    ///     Test that strict mode turns an unsupported feature into an error and writes nothing.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_StrictWithUnsupportedFeature_FailsWithoutOutput()
    {
        // Arrange: a model that uses an unsupported module
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "text.scad", "text(\"A\");\ncube(1);\n");
        var output = directory.Combine("o.png");

        // Act
        var exception = RunFailing(input, "--strict", "--size", "16x16", "-o", output);

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, exception.Code);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that exceeding the triangle limit is a concise budget error and writes nothing.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MaxTrianglesExceeded_FailsWithBudgetExceeded()
    {
        // Arrange: the fixture model and a limit of one triangle
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");
        var output = directory.Combine("o.png");

        // Act
        var exception = RunFailing(input, "--max-triangles", "1", "-o", output);

        // Assert: the code, one line that says which limit, and no file
        Assert.Equal(ErrorCodes.BudgetExceeded, exception.Code);
        Assert.Contains("Triangle limit exceeded (limit 1)", exception.Message);
        Assert.Contains("--max-triangles", exception.Hint);
        Assert.DoesNotContain('\n', exception.Message);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the size options apply to a model: the natural size is 800 square, a scale multiplies it.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadSizeOptions_FollowTheNaturalSize()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        Run(false, input, "--scale", "0.25", "-o", directory.Combine("scaled.png"));
        Run(false, input, "--width", "100", "-o", directory.Combine("width.png"));
        Run(false, input, "--height", "60", "-o", directory.Combine("height.png"));
        Run(false, input, "--size", "90x50", "-o", directory.Combine("size.png"));

        // Assert
        foreach (var (name, expected) in new[] { ("scaled", (200, 200)), ("width", (100, 100)), ("height", (60, 60)), ("size", (90, 50)) })
        {
            using var image = PngCodec.Load(directory.Combine(name + ".png"));
            Assert.Equal(expected, (image.Width, image.Height));
        }
    }

    /// <summary>
    ///     Test that the background option applies to a model: a color fills the background and transparent keeps it clear in a PNG.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadBackground_IsAppliedAndFlattenedForJpeg()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        Run(false, input, "--size", "32x32", "--background", "#0000FF", "-o", directory.Combine("blue.png"));
        Run(false, input, "--size", "32x32", "--background", "transparent", "-o", directory.Combine("clear.png"));
        var stdOut = Run(true, input, "--size", "32x32", "--background", "transparent", "-o", directory.Combine("clear.jpg"));

        // Assert: blue corner, clear corner, and a white corner in the JPEG, which is flattened by the writer
        using var blue = PngCodec.Load(directory.Combine("blue.png"));
        using var clear = PngCodec.Load(directory.Combine("clear.png"));
        using var jpeg = JpegCodec.Load(directory.Combine("clear.jpg"));
        Assert.Equal(new Rgba32(0, 0, 255, 255), blue[0, 0]);
        Assert.Equal(0, clear[0, 0].A);
        Assert.InRange(jpeg[0, 0].R, 240, 255);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(["render", "flatten"], document.RootElement.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("op").GetString()));
    }

    /// <summary>
    ///     Test that every output format can be written from a model.
    /// </summary>
    /// <param name="extension">The extension of the output.</param>
    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".bmp")]
    [InlineData(".tif")]
    public void RenderCommand_Run_ScadToEachOutputFormat_WritesDecodableFile(string extension)
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");
        var output = directory.Combine("out" + extension);

        // Act
        Run(false, input, "--size", "64x64", "-o", output);

        // Assert: a decodable file of the size, with the model in the middle
        using var image = SurfaceAssert.Load(output);
        Assert.Equal((64, 64), (image.Width, image.Height));
        Assert.NotEqual(new Rgba32(255, 255, 255, 255), image[32, 32]);
    }

    /// <summary>
    ///     Test that the same model and options give the same bytes on every run.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadTwice_WritesIdenticalBytes()
    {
        // Arrange: the asymmetric model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "model.scad", FixtureFactory.AsymmetricModel);

        // Act
        foreach (var name in new[] { "a", "b" })
        {
            Run(false, input, "--view", "back", "--projection", "perspective", "-D", "x=3", "--size", "100x80", "-o", directory.Combine(name + ".png"));
        }

        // Assert
        Assert.Equal(File.ReadAllBytes(directory.Combine("a.png")), File.ReadAllBytes(directory.Combine("b.png")));
    }

    /// <summary>
    ///     Test that a bad value of a model option is a usage error that names the option.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="value">The bad value.</param>
    [Theory]
    [InlineData("--view", "side")]
    [InlineData("--projection", "isometric")]
    [InlineData("--azimuth", "left")]
    [InlineData("--elevation", "95")]
    [InlineData("--elevation", "-91")]
    [InlineData("--elevation", "up")]
    [InlineData("--time", "soon")]
    [InlineData("--time", "1e12")]
    [InlineData("--default-color", "not-a-color")]
    [InlineData("--max-triangles", "0")]
    [InlineData("--max-triangles", "many")]
    [InlineData("--max-triangles", "3000000000")]
    [InlineData("--define", "width")]
    [InlineData("--define", "1x=3")]
    [InlineData("--define", "v=[1,2]")]
    public void RenderCommand_Run_BadModelOption_ThrowsUsageError(string option, string value)
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var exception = RunFailing(input, option, value, "-o", directory.Combine("o.png"));

        // Assert: a usage error that mentions the option or its value
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.True(
            exception.Message.Contains(option, StringComparison.Ordinal) || exception.Message.Contains(value, StringComparison.Ordinal)
            || exception.Message.Contains("-D", StringComparison.Ordinal),
            exception.Message);
        Assert.False(File.Exists(directory.Combine("o.png")));
    }

    /// <summary>
    ///     Test that an unknown view is reported with the list of views.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_UnknownView_ListsTheViews()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var exception = RunFailing(input, "--view", "diagonal", "-o", directory.Combine("o.png"));

        // Assert
        Assert.Equal("'diagonal' is not a view.", exception.Message);
        Assert.Equal("The views are: iso, front, back, left, right, top, bottom.", exception.Hint);
    }

    /// <summary>
    ///     Test that a model option on an input that is not a model is refused, rather than silently ignored.
    /// </summary>
    /// <param name="key">The kind of input.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("svg")]
    [InlineData("pdf")]
    public void RenderCommand_Run_ModelOptionOnAnotherKind_ThrowsUsageError(string key)
    {
        // Arrange: an input that is not a model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, key);

        // Act
        var exception = RunFailing(input, "-D", "x=1", "-o", directory.Combine("o.png"));

        // Assert: a usage error that names the kind
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains($"is a {key} file", exception.Message);
        Assert.Contains("only apply to an OpenSCAD model", exception.Message);
    }

    /// <summary>
    ///     Test that a page option beyond the single page of a model is out of range.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PageTwoOfAModel_ThrowsPageOutOfRange()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var exception = RunFailing(input, "--page", "2", "-o", directory.Combine("o.png"));

        // Assert
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
    }

    /// <summary>
    ///     Test that a model with a syntax error is a scad error that names the line, and that no output remains.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ModelWithSyntaxError_ThrowsScadError()
    {
        // Arrange: a model with a syntax error
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "broken.scad", "cube(1);\ncube(;\n");
        var output = directory.Combine("o.png");

        // Act
        var exception = RunFailing(input, "-o", output);

        // Assert
        Assert.Equal(ErrorCodes.ScadError, exception.Code);
        Assert.Contains("broken.scad:2:6", exception.Message);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the help of the command lists the model options and says how include and use are resolved.
    /// </summary>
    [Fact]
    public void RenderCommand_Usage_DescribesModelOptionsAndIncludeRule()
    {
        // Arrange / Act: the help of the command
        var help = CommandRegistry.CreateDefault().RenderHelp("render");

        // Assert: the options, the default size, and the include rule
        foreach (var option in new[] { "--view", "--azimuth", "--elevation", "--projection", "--define", "-D", "--time", "--default-color", "--max-triangles" })
        {
            Assert.Contains(option, help);
        }

        Assert.Contains("800x800", help);
        Assert.Contains("include and use directives are read from the", help);
        Assert.Contains("folder of the model and below it only", help);
    }
}
