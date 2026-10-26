// cspell:ignore canvasnet jpeg jpg tif tiff exif
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool. Most tests run the tool as a real process via
///     <c>dotnet</c>; the error-policy tests run it in-process with an injected command so that
///     standard output and standard error can be examined separately.
/// </summary>
[Collection("Sequential")]
public class CanvasNetToolTests
{
    /// <summary>
    ///     The path of the tool assembly in the test output directory.
    /// </summary>
    private readonly string _dllPath;

    /// <summary>
    ///     Initialize test by locating the CanvasNet Tool DLL.
    /// </summary>
    public CanvasNetToolTests()
    {
        // The DLL should be in the same directory as the test assembly
        // because the test project references the tool project
        var baseDir = AppContext.BaseDirectory;
        _dllPath = PathHelpers.SafePathCombine(baseDir, "DemaConsulting.CanvasNet.Tool.dll");

        Assert.True(File.Exists(_dllPath), $"Could not find CanvasNet Tool DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that version flag outputs version information.
    /// </summary>
    [Fact]
    public void CanvasNetTool_VersionFlag_Provided_OutputsVersion()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the tool with version flag
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--version");

        // Assert: version string is printed; no banner or errors
        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
        Assert.DoesNotContain("Error", output);
        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that help flag outputs usage information.
    /// </summary>
    [Fact]
    public void CanvasNetTool_HelpFlag_Provided_OutputsUsageInformation()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the tool with help flag
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--help");

        // Assert: usage text contains required sections and key flags
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Contains("--version", output);
        Assert.Contains("--help", output);
    }

    /// <summary>
    ///     Test that no arguments displays the tool banner and a usage hint.
    /// </summary>
    [Fact]
    public void CanvasNetTool_NoArguments_Invoked_DisplaysBanner()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the tool with no arguments
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath);

        // Assert: banner is displayed with tool name and copyright, followed by the usage hint; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("CanvasNet Tool version", output);
        Assert.Contains("Copyright", output);
        Assert.Contains("Usage: canvasnet", output);
    }

    /// <summary>
    ///     Test that validate flag runs self-validation and outputs summary.
    /// </summary>
    [Fact]
    public void CanvasNetTool_ValidateFlag_Provided_RunsValidation()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the tool with validate flag
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate");

        // Assert: validation summary is present; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("Total Tests:", output);
        Assert.Contains("Passed:", output);
    }

    /// <summary>
    ///     Test that validate with --results flag generates a TRX file.
    /// </summary>
    [Fact]
    public void CanvasNetTool_ValidateWithTrxResults_Requested_GeneratesTrxFile()
    {
        // Arrange: temporary TRX results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_system_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with TRX results output
            var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--validate", "--results", resultsFile);

            // Assert: results file is created with valid TRX structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var trxContent = File.ReadAllText(resultsFile);
            Assert.Contains("<TestRun", trxContent);
            Assert.Contains("</TestRun>", trxContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with --result (legacy alias) flag generates a results file.
    /// </summary>
    [Fact]
    public void CanvasNetTool_ResultAlias_LegacyFlag_WritesResultsFile()
    {
        // Arrange: temporary TRX results file path; use legacy --result alias
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_system_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with legacy --result alias
            var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--validate", "--result", resultsFile);

            // Assert: results file is created; exit code is success
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created for legacy --result alias");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with an unsupported results extension returns a non-zero exit code.
    /// </summary>
    [Fact]
    public void CanvasNetTool_ValidateWithBadExtension_ExtensionInvalid_ReturnsNonZero()
    {
        // Arrange: unsupported results file extension triggers WriteError -> ExitCode 1
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_system_test_{Guid.NewGuid()}.bad");

        try
        {
            // Act: run validation with unsupported extension
            var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--validate", "--results", resultsFile);

            // Assert: non-zero exit code indicates the error path was triggered
            Assert.NotEqual(0, exitCode);
            Assert.False(File.Exists(resultsFile), "No file should be created for unsupported extension");
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that silent flag suppresses output.
    /// </summary>
    [Fact]
    public void CanvasNetTool_SilentFlag_Provided_SuppressesOutput()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the tool with --version and --silent to produce deterministic silent output
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--version", "--silent");

        // Assert: no console output in silent mode
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(output), $"Expected no output in silent mode but got: {output}");
    }

    /// <summary>
    ///     Test that log flag writes output to a file.
    /// </summary>
    [Fact]
    public void CanvasNetTool_LogFlag_Provided_WritesOutputToFile()
    {
        // Arrange: temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: run the tool with log flag
            var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--log", logFile);

            // Assert: log file is created and contains tool output; console output matches
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(logFile), "Log file was not created");

            var logContent = File.ReadAllText(logFile);
            Assert.Contains("CanvasNet Tool version", logContent);
            Assert.Contains("CanvasNet Tool version", output);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that validate with --results flag generates a JUnit XML file.
    /// </summary>
    [Fact]
    public void CanvasNetTool_ValidateWithXmlResults_Requested_GeneratesJUnitFile()
    {
        // Arrange: temporary XML results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_system_test_{Guid.NewGuid()}.xml");

        try
        {
            // Act: run validation with JUnit XML results output
            var exitCode = Runner.Run(out _, "dotnet", _dllPath, "--validate", "--results", resultsFile);

            // Assert: results file is created with valid JUnit structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var xmlContent = File.ReadAllText(resultsFile);
            Assert.Contains("<testsuites", xmlContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that an unknown argument causes an error message and non-zero exit code.
    /// </summary>
    [Fact]
    public void CanvasNetTool_UnknownArgument_Provided_ReturnsError()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the tool with an unknown argument
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--unknown");

        // Assert: non-zero exit code and error message naming the unrecognized flag
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--unknown", output);
    }

    /// <summary>
    ///     Test that an unknown command is reported as a concise usage error.
    /// </summary>
    [Fact]
    public void CanvasNetTool_UnknownCommand_Provided_ReturnsConciseUsageError()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the tool with a command that does not exist
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "bogus");

        // Assert: exit code 1, a one-line error naming the code and the command, and no stack trace
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: usage-error: Unknown command 'bogus'", output);
        Assert.DoesNotContain("   at ", output);
        Assert.DoesNotContain("Exception", output);
    }

    /// <summary>
    ///     Test that validate with depth flag outputs headings at the specified depth.
    /// </summary>
    [Fact]
    public void CanvasNetTool_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run validation with heading depth 3
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--validate", "--depth", "3");

        // Assert: output contains level-3 markdown headings
        Assert.Equal(0, exitCode);
        Assert.Contains("### DEMA Consulting CanvasNet Tool", output);
    }

    /// <summary>
    ///     Test that a command's output is exactly what the command writes, with no banner.
    /// </summary>
    [Fact]
    public void CanvasNetTool_CommandOutput_CommandRuns_HasNoBanner()
    {
        // Arrange: a registry with one command that writes a single line
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("greet", (context, _) => context.WriteLine("hello")));

        // Act: run the command in-process
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "greet");

        // Assert: standard output is exactly the command's line; nothing on standard error
        Assert.Equal(0, exitCode);
        Assert.Equal("hello" + Environment.NewLine, stdOut);
        Assert.Equal(string.Empty, stdErr);
    }

    /// <summary>
    ///     Test that a command is selected by the first non-option argument wherever the global options appear.
    /// </summary>
    [Fact]
    public void CanvasNetTool_CommandDispatch_GlobalFlagsAnywhere_RunsCommandWithItsArguments()
    {
        // Arrange: a registry with a command that records what it receives
        var command = new TestCommand("greet");
        var registry = new CommandRegistry();
        registry.Register(command);

        // Act: global --silent before the command, command arguments (including an unknown option) after it
        var (exitCode, stdOut, _) = ToolHost.Invoke(registry, "--silent", "greet", "input.png", "--width", "10");

        // Assert: the command ran with its own arguments only, and the global flag was honored
        Assert.Equal(0, exitCode);
        Assert.Equal(new[] { "input.png", "--width", "10" }, command.ReceivedArguments);
        Assert.Equal(string.Empty, stdOut);
    }

    /// <summary>
    ///     Test that an expected command failure is reported concisely on standard error with exit code 1.
    /// </summary>
    [Fact]
    public void CanvasNetTool_ExpectedFailure_CommandThrowsToolException_ReportsConciseError()
    {
        // Arrange: a command that fails with an expected, user-correctable error
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "fail",
            (_, _) => throw new ToolException(ErrorCodes.InputNotFound, "File 'x.png' was not found.", "Check the path.")));

        // Act: run the failing command
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "fail");

        // Assert: exit code 1, the code, message and hint on standard error, nothing on standard output, no stack trace
        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdOut);
        Assert.Contains("Error: input-not-found: File 'x.png' was not found.", stdErr);
        Assert.Contains("Hint: Check the path.", stdErr);
        Assert.DoesNotContain("   at ", stdErr);
    }

    /// <summary>
    ///     Test that an unexpected exception is reported and re-thrown rather than converted to an exit code.
    /// </summary>
    [Fact]
    public void CanvasNetTool_UnexpectedError_CommandThrowsUnexpectedException_IsReThrown()
    {
        // Arrange: a command with a defect (an exception type that is not an expected failure)
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("defect", (_, _) => throw new NotSupportedException("boom")));

        // Act: run the defective command, expecting the exception to escape the entry point
        var (exception, _, stdErr) = ToolHost.InvokeExpectingThrow<NotSupportedException>(registry, "defect");

        // Assert: the original exception escaped and was also reported on standard error
        Assert.Equal("boom", exception.Message);
        Assert.Contains("Unexpected error: boom", stdErr);
    }

    /// <summary>
    ///     Test that the formats command lists the formats as text, with no banner.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Formats_Requested_ListsFormats()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the formats command
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "formats");

        // Assert: the listing starts the output and names the formats and limits; no banner
        Assert.Equal(0, exitCode);
        Assert.StartsWith("Input formats:", output);
        Assert.Contains("png", output);
        Assert.Contains("pdf", output);
        Assert.Contains("Output formats:", output);
        Assert.Contains("Maximum image dimension: 8192 pixels", output);
        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that the formats command writes exactly one JSON document with the schema version when --json is given.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Formats_JsonRequested_WritesJsonDocument()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run the formats command with --json
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "formats", "--json");

        // Assert: the whole output is one parsable single-line document describing the formats
        Assert.Equal(0, exitCode);
        Assert.Single(output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("formats", root.GetProperty("command").GetString());
        Assert.Contains(root.GetProperty("inputs").EnumerateArray(), f => f.GetProperty("format").GetString() == "pdf");
        Assert.Contains(root.GetProperty("outputs").EnumerateArray(), f => f.GetProperty("format").GetString() == "png");
        Assert.Equal(8192, root.GetProperty("maxDimension").GetInt32());
    }

    /// <summary>
    ///     Test that an expected failure with --json is also written to standard output as an error document.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Json_ExpectedFailure_WritesErrorDocumentAndStderrMessage()
    {
        // Arrange: a command that fails with an expected error
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "fail",
            (_, _) => throw new ToolException(ErrorCodes.InputNotFound, "File 'x.png' was not found.", "Check the path.")));

        // Act: run the failing command with --json
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "fail", "--json");

        // Assert: exit code 1; standard output is the error document; standard error still has the message
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("fail", root.GetProperty("command").GetString());
        Assert.Equal("input-not-found", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("File 'x.png' was not found.", root.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal("Check the path.", root.GetProperty("error").GetProperty("hint").GetString());
        Assert.False(root.TryGetProperty("exitCode", out _));
        Assert.Contains("Error: input-not-found: File 'x.png' was not found.", stdErr);
    }

    /// <summary>
    ///     Test that a bad global option with --json is reported as an error document, before any command is known.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Json_BadGlobalOption_WritesErrorDocument()
    {
        // Arrange: (none)

        // Act: run the tool with --json and an unknown option before any command
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("--json", "--bogus");

        // Assert: exit code 1; a usage-error document with a null command; the text report on standard error
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("command").ValueKind);
        Assert.Equal("usage-error", root.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("Error: Unsupported argument '--bogus'", stdErr);
    }

    /// <summary>
    ///     Test that --silent suppresses the error document.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Json_SilentFlag_SuppressesErrorDocument()
    {
        // Arrange: (none)

        // Act: run a failing invocation with both --json and --silent
        var (exitCode, stdOut, _) = ToolHost.Invoke("formats", "extra", "--json", "--silent");

        // Assert: exit code 1 and nothing on standard output
        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdOut);
    }

    /// <summary>
    ///     Test that a command reads the option forms through the argument reader, in command-line order.
    /// </summary>
    [Fact]
    public void CanvasNetTool_CommandLineGrammar_OptionForms_ReadByCommand()
    {
        // Arrange: a command that echoes what the argument reader found
        OptionSpec[] specs =
        [
            new("--output", ["-o"], true, "The output file", "file"),
            new("--crop", [], true, "Crop", "geometry"),
            new("--resize", [], true, "Resize", "size"),
            new("--no-clobber", [], false, "Do not overwrite"),
            new("-D", [], true, "Define", "name=value")
        ];
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("echo", (context, args) =>
        {
            var reader = ArgumentReader.Parse("echo", args, specs);
            context.WriteLine(string.Join(' ', reader.Ordered.Select(o => $"{o.Name}={o.Value}")) + " | " + string.Join(' ', reader.Positionals));
        }));

        // Act: mix the separate, equals, short, and attached forms, a repeated option, and positional arguments
        var (exitCode, stdOut, _) = ToolHost.Invoke(
            registry,
            "echo", "in.png", "-o=a.png", "--crop", "1,2,3,4", "-Dx=1", "--resize=10x10", "--crop=5,6,7,8", "--no-clobber", "extra");

        // Assert: the options are in command-line order under their canonical names; the positionals are in order
        Assert.Equal(0, exitCode);
        Assert.Equal(
            $"--output=a.png --crop=1,2,3,4 -D=x=1 --resize=10x10 --crop=5,6,7,8 --no-clobber= | in.png extra{Environment.NewLine}",
            stdOut);
    }

    /// <summary>
    ///     Test that an unknown option of a command is reported as a concise usage error.
    /// </summary>
    [Fact]
    public void CanvasNetTool_CommandLineGrammar_UnknownOption_ReportsUsageError()
    {
        // Arrange: (none - constructor initializes _dllPath)

        // Act: run a command with an option it does not have
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "formats", "--bogus");

        // Assert: exit code 1 and a concise usage error naming the option and the command
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: usage-error: Unknown option '--bogus' for 'formats'.", output);
        Assert.DoesNotContain("   at ", output);
    }

    /// <summary>
    ///     Test that the render command renders an SVG drawing to a PNG and reports it in one line.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Render_SvgToPng_WritesImageAndResultLine()
    {
        // Arrange: the SVG fixture and an output path
        using var directory = new TempDirectory();
        var svg = FixtureFactory.FixturePath("shapes.svg");
        var output = directory.Combine("shapes.png");

        // Act: run the render command as a real process, at twice the natural size
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", svg, "--scale", "2", "-o", output);

        // Assert: success, one line naming the output, and a 200x160 image with the shapes flattened over white
        Assert.Equal(0, exitCode);
        Assert.Equal($"wrote {output} (200x160 png, {new FileInfo(output).Length} bytes) from {svg}", text.TrimEnd());
        using var image = PngCodec.Load(output);
        Assert.Equal((200, 160), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[50, 50]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[2, 2]);
    }

    /// <summary>
    ///     Test that the render command writes exactly one JSON document with the documented keys when --json is given.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Render_JsonRequested_WritesJsonDocument()
    {
        // Arrange: a PNG and an output path
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.jpg");

        // Act: run the render command as a real process with --json
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "-o", output, "--quality", "80", "--json");

        // Assert: the whole output is one document that describes the input and the output
        Assert.Equal(0, exitCode);
        Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("render", root.GetProperty("command").GetString());
        Assert.Equal("png", root.GetProperty("input").GetProperty("format").GetString());
        var written = root.GetProperty("outputs")[0];
        Assert.Equal("jpeg", written.GetProperty("format").GetString());
        Assert.Equal(new FileInfo(output).Length, written.GetProperty("bytes").GetInt64());
    }

    /// <summary>
    ///     Test that the convert alias writes every output format and each file decodes with the codec of its format.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Convert_EachOutputFormat_WritesDecodableFile()
    {
        // Arrange: a PNG
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        using var expected = FixtureFactory.CreateGradient();

        foreach (var extension in new[] { ".png", ".bmp", ".tif", ".jpg" })
        {
            var output = directory.Combine("converted" + extension);

            // Act: convert it as a real process through the alias
            var exitCode = Runner.Run(out _, "dotnet", _dllPath, "convert", input, "-o", output);

            // Assert: success, and the pixels (exactly, except for JPEG)
            Assert.Equal(0, exitCode);
            using var decoded = SurfaceAssert.Load(output);
            if (extension == ".jpg")
            {
                SurfaceAssert.CloseOnAverage(expected, decoded, 8);
            }
            else
            {
                SurfaceAssert.Equal(expected, decoded);
            }
        }
    }

    /// <summary>
    ///     Test that the info command describes a file as text and as JSON.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Info_Requested_DescribesFile()
    {
        // Arrange: a BMP
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "bmp");

        // Act: run the info command as a real process, as text and as JSON
        var textExitCode = Runner.Run(out var text, "dotnet", _dllPath, "info", input);
        var jsonExitCode = Runner.Run(out var json, "dotnet", _dllPath, "info", input, "--json");

        // Assert: the text line names the format and size; the document has the same facts
        Assert.Equal(0, textExitCode);
        Assert.StartsWith($"{input}: bmp, 12x8 px", text);
        Assert.Equal(0, jsonExitCode);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("info", document.RootElement.GetProperty("command").GetString());
        Assert.Equal("bmp", document.RootElement.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal(12, document.RootElement.GetProperty("image").GetProperty("width").GetInt32());
        Assert.Equal(8, document.RootElement.GetProperty("image").GetProperty("height").GetInt32());
    }

    /// <summary>
    ///     Test that every raster format and SVG can be read, by rendering each to a PNG.
    /// </summary>
    [Fact]
    public void CanvasNetTool_InputFormats_EveryRasterAndSvg_AreReadable()
    {
        // Arrange: a source file of every kind
        using var directory = new TempDirectory();

        foreach (var (key, _) in FixtureFactory.Sources)
        {
            var input = FixtureFactory.WriteSource(directory.Path, key);
            var output = directory.Combine($"{key}.png");

            // Act: render it in-process
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", output);

            // Assert: success, and the output has the size the source has
            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, stdErr);
            Assert.StartsWith("wrote ", stdOut);
            var info = SourceLoader.Probe(input);
            using var image = PngCodec.Load(output);
            Assert.Equal((info.Width, info.Height), (image.Width, image.Height));
        }
    }

    /// <summary>
    ///     Test that the output format follows the extension, and the format option overrides it.
    /// </summary>
    [Fact]
    public void CanvasNetTool_OutputFormats_ExtensionAndFormatOption_SelectFormat()
    {
        // Arrange: a PNG input
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        foreach (var (extension, name) in new[] { (".png", "png"), (".jpg", "jpeg"), (".jpeg", "jpeg"), (".bmp", "bmp"), (".tif", "tiff"), (".tiff", "tiff") })
        {
            // Act: render to the extension
            var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "-o", directory.Combine("out" + extension), "--json");

            // Assert: the format named in the result
            Assert.Equal(0, exitCode);
            using var document = JsonDocument.Parse(stdOut);
            Assert.Equal(name, document.RootElement.GetProperty("outputs")[0].GetProperty("format").GetString());
        }

        // Act: the format option on an output with another extension
        var (optionExitCode, optionOut, _) = ToolHost.Invoke("render", input, "-o", directory.Combine("out.png"), "--format", "bmp", "--json");

        // Assert: the option wins, and the file is a BMP
        Assert.Equal(0, optionExitCode);
        using var optionDocument = JsonDocument.Parse(optionOut);
        Assert.Equal("bmp", optionDocument.RootElement.GetProperty("outputs")[0].GetProperty("format").GetString());
        using var decoded = BmpCodec.Load(directory.Combine("out.png"));
        Assert.Equal(FixtureFactory.Width, decoded.Width);
    }

    /// <summary>
    ///     Test the overwrite defaults: an existing output is replaced, no-clobber refuses, and the input is never the output.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Defaults_ExistingOutputAndSameFile_FollowPolicy()
    {
        // Arrange: an input and an existing output
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "old");

        // Act
        var (refusedCode, _, refusedErr) = ToolHost.Invoke("render", input, "-o", output, "--no-clobber");
        var keptContent = File.ReadAllText(output);
        var (overwriteCode, _, _) = ToolHost.Invoke("render", input, "-o", output);
        var (sameCode, _, sameErr) = ToolHost.Invoke("render", input, "-o", input);

        // Assert: refused with output-exists and kept; overwritten by default; the input as output is refused
        Assert.Equal(1, refusedCode);
        Assert.Contains("Error: output-exists:", refusedErr);
        Assert.Equal("old", keptContent);
        Assert.Equal(0, overwriteCode);
        Assert.True(new FileInfo(output).Length > 3);
        Assert.Equal(1, sameCode);
        Assert.Contains("Error: same-input-output:", sameErr);
    }

    /// <summary>
    ///     Test that transparency is flattened over white for SVG sources and for JPEG output, and kept for PNG.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Defaults_Transparency_FlattenedOverWhiteForSvgAndJpeg()
    {
        // Arrange: the SVG fixture and a PNG with a fully transparent edge
        using var directory = new TempDirectory();
        var svg = FixtureFactory.FixturePath("shapes.svg");
        var translucent = directory.Combine("translucent.png");
        using (var surface = FixtureFactory.CreateTranslucent())
        {
            PngCodec.Save(surface, translucent);
        }

        // Act
        ToolHost.Invoke("render", svg, "-o", directory.Combine("svg.png"));
        ToolHost.Invoke("render", translucent, "-o", directory.Combine("kept.png"));
        ToolHost.Invoke("render", translucent, "-o", directory.Combine("flat.jpg"));

        // Assert: white for the SVG, alpha kept for the PNG output, white for the JPEG output
        using var fromSvg = PngCodec.Load(directory.Combine("svg.png"));
        using var kept = PngCodec.Load(directory.Combine("kept.png"));
        using var flat = JpegCodec.Load(directory.Combine("flat.jpg"));
        Assert.Equal(new Rgba32(255, 255, 255, 255), fromSvg[5, 5]);
        Assert.Equal(0, kept[FixtureFactory.Width - 1, 0].A);
        Assert.InRange(flat[FixtureFactory.Width - 1, 0].R, 240, 255);
        Assert.Equal(255, flat[FixtureFactory.Width - 1, 0].A);
    }

    /// <summary>
    ///     Test that a JPEG is turned upright according to its EXIF orientation by default.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Defaults_JpegWithExif_IsTurnedUpright()
    {
        // Arrange: a 12x8 JPEG with the EXIF orientation 8 (rotate 90 degrees counter-clockwise)
        using var directory = new TempDirectory();
        var input = directory.Combine("camera.jpg");
        using (var surface = FixtureFactory.CreateGradient())
        using (var stream = new MemoryStream())
        {
            JpegCodec.Save(surface, stream);
            File.WriteAllBytes(input, FixtureFactory.InsertExifOrientation(stream.ToArray(), 8, false));
        }

        // Act
        var (exitCode, _, _) = ToolHost.Invoke("render", input, "-o", directory.Combine("upright.png"));
        ToolHost.Invoke("render", input, "--no-auto-orient", "-o", directory.Combine("stored.png"));

        // Assert: 8x12 upright, 12x8 as stored, and the upright pixels are the stored ones turned
        Assert.Equal(0, exitCode);
        using var upright = PngCodec.Load(directory.Combine("upright.png"));
        using var stored = PngCodec.Load(directory.Combine("stored.png"));
        Assert.Equal((8, 12), (upright.Width, upright.Height));
        Assert.Equal((12, 8), (stored.Width, stored.Height));
        using var expected = ExifOrientation.Apply(stored, 8);
        SurfaceAssert.Equal(expected, upright);
    }

    /// <summary>
    ///     Test that an image larger than 8192 pixels is refused from its header, as an expected error.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Defaults_OversizedInput_IsRefusedBeforeDecoding()
    {
        // Arrange: a PNG that declares 9000x9000 pixels
        using var directory = new TempDirectory();
        var input = directory.Combine("huge.png");
        File.WriteAllBytes(input, FixtureFactory.CreatePngHeaderOnly(9000, 9000));
        var output = directory.Combine("out.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", output, "--json");

        // Assert: exit code 1, a size-limit error document and message, and no output
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal("size-limit", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("Error: size-limit:", stdErr);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the same input and options give the same output bytes on every run.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Determinism_SameInputTwice_WritesIdenticalBytes()
    {
        // Arrange: inputs of three kinds and the output formats
        using var directory = new TempDirectory();
        var inputs = new[]
        {
            FixtureFactory.WriteSource(directory.Path, "png"),
            FixtureFactory.WriteSource(directory.Path, "jpeg"),
            FixtureFactory.WriteSource(directory.Path, "svg")
        };

        foreach (var input in inputs)
        {
            foreach (var extension in new[] { ".png", ".jpg", ".bmp", ".tif" })
            {
                // Act: render twice with the same options
                var first = directory.Combine("first" + extension);
                var second = directory.Combine("second" + extension);
                Assert.Equal(0, ToolHost.Invoke("render", input, "--width", "20", "-o", first).ExitCode);
                Assert.Equal(0, ToolHost.Invoke("render", input, "--width", "20", "-o", second).ExitCode);

                // Assert: the same bytes
                Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
            }
        }
    }

    /// <summary>
    ///     Test that a missing, unsupported, truncated, or corrupt input of any format is an expected error, never an unexpected one.
    /// </summary>
    [Fact]
    public void CanvasNetTool_InputErrors_BadInputs_AreReportedAsExpectedErrors()
    {
        // Arrange: a missing file, a file of an unknown format, and a truncated file and garbage of every kind
        using var directory = new TempDirectory();
        var cases = new List<(string Input, string Code)>
        {
            (directory.Combine("missing.png"), "input-not-found")
        };
        var text = directory.Combine("notes.txt");
        File.WriteAllText(text, "notes");
        cases.Add((text, "unsupported-format"));
        foreach (var (key, extension) in FixtureFactory.Sources)
        {
            var truncated = FixtureFactory.WriteSource(directory.Path, key);
            File.WriteAllBytes(truncated, File.ReadAllBytes(truncated)[..16]);
            var renamed = directory.Combine($"truncated-{key}{extension}");
            File.Move(truncated, renamed);
            cases.Add((renamed, "corrupt-input"));

            var garbage = directory.Combine($"garbage-{key}{extension}");
            File.WriteAllBytes(garbage, FixtureFactory.CreateGarbage(200));
            cases.Add((garbage, "corrupt-input"));
        }

        foreach (var (input, code) in cases)
        {
            // Act: render it in-process; an unexpected exception would escape and fail the test
            var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", directory.Combine("out.png"), "--json");

            // Assert: exit code 1, the error code in the message and in the document, and no output
            Assert.Equal(1, exitCode);
            Assert.Contains($"Error: {code}:", stdErr);
            using var document = JsonDocument.Parse(stdOut);
            Assert.Equal(code, document.RootElement.GetProperty("error").GetProperty("code").GetString());
            Assert.False(File.Exists(directory.Combine("out.png")));
        }
    }
}
