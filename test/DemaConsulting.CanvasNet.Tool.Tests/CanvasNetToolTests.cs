// cspell:ignore canvasnet
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;

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
}
