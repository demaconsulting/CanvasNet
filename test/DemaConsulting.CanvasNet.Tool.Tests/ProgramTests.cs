// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Unit tests for the Program class.
/// </summary>
[Collection("Sequential")]
public class ProgramTests
{
    /// <summary>
    ///     Runs <see cref="Program.Run(Context, CommandRegistry)"/> for the given arguments and captures the output.
    /// </summary>
    /// <param name="registry">The command registry to dispatch against.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code of the context and the captured standard output.</returns>
    private static (int ExitCode, string StdOut) RunProgram(CommandRegistry registry, params string[] args)
    {
        var (exitCode, stdOut, _) = ToolHost.Capture(() =>
        {
            using var context = Context.Create(args);
            Program.Run(context, registry);
            return context.ExitCode;
        });
        return (exitCode, stdOut);
    }

    /// <summary>
    ///     Test that Run with version flag displays version only.
    /// </summary>
    [Fact]
    public void Program_Run_WithVersionFlag_DisplaysVersionOnly()
    {
        // Arrange: a default registry
        var registry = CommandRegistry.CreateDefault();

        // Act: run with the version flag
        var (exitCode, output) = RunProgram(registry, "--version");

        // Assert: only the version is printed - no banner
        Assert.Contains(Program.Version, output);
        Assert.DoesNotContain("Copyright", output);
        Assert.DoesNotContain("CanvasNet Tool version", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with short version flag -v displays version.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortVersionFlag_DisplaysVersion()
    {
        // Arrange: a default registry
        var registry = CommandRegistry.CreateDefault();

        // Act: run with the short version flag
        var (exitCode, output) = RunProgram(registry, "-v");

        // Assert: the version is printed
        Assert.Contains(Program.Version, output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with help flag displays usage information.
    /// </summary>
    [Fact]
    public void Program_Run_WithHelpFlag_DisplaysUsageInformation()
    {
        // Arrange: a default registry
        var registry = CommandRegistry.CreateDefault();

        // Act: run with the help flag
        var (exitCode, output) = RunProgram(registry, "--help");

        // Assert: the banner and the usage information are printed
        Assert.Contains("CanvasNet Tool version", output);
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Contains("--version", output);
        Assert.Contains("--help", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with short help flag -h displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortHelpFlag_DisplaysUsage()
    {
        // Arrange: a default registry
        var registry = CommandRegistry.CreateDefault();

        // Act: run with the short help flag
        var (exitCode, output) = RunProgram(registry, "-h");

        // Assert: the usage information is printed
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with short help flag -? displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithQuestionMarkFlag_DisplaysUsage()
    {
        // Arrange: a default registry
        var registry = CommandRegistry.CreateDefault();

        // Act: run with the question-mark help flag
        var (exitCode, output) = RunProgram(registry, "-?");

        // Assert: the usage information is printed
        Assert.Contains("Usage:", output);
        Assert.Contains("Options:", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with help flag and a command name displays the help of that command.
    /// </summary>
    [Fact]
    public void Program_Run_WithCommandAndHelpFlag_DisplaysCommandHelp()
    {
        // Arrange: a registry with one command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("greet"));

        // Act: ask for help on the command, with the flag after the command name
        var (exitCode, output) = RunProgram(registry, "greet", "--help");

        // Assert: the command's own usage and options are shown, and the command did not run
        Assert.Contains("Usage: canvasnet greet <input> [options]", output);
        Assert.Contains("--flag", output);
        Assert.Contains("Summary of greet", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with validate flag runs validation.
    /// </summary>
    [Fact]
    public void Program_Run_WithValidateFlag_RunsValidation()
    {
        // Arrange: a default registry
        var registry = CommandRegistry.CreateDefault();

        // Act: run with the validate flag
        var (exitCode, output) = RunProgram(registry, "--validate");

        // Assert: the self-validation summary is printed and succeeds
        Assert.Contains("Total Tests:", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with no arguments displays the banner and a usage hint.
    /// </summary>
    [Fact]
    public void Program_Run_NoArguments_DisplaysBannerAndUsageHint()
    {
        // Arrange: a default registry
        var registry = CommandRegistry.CreateDefault();

        // Act: run with an empty command line
        var (exitCode, output) = RunProgram(registry);

        // Assert: the banner and the hint are printed and the exit code is success
        Assert.Contains("CanvasNet Tool version", output);
        Assert.Contains("Copyright", output);
        Assert.Contains("Usage: canvasnet <command> [options] (try --help)", output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with a command name dispatches to the registered command.
    /// </summary>
    [Fact]
    public void Program_Run_WithCommand_DispatchesToRegisteredCommand()
    {
        // Arrange: a registry with a command that writes a line
        var command = new TestCommand("greet", (context, _) => context.WriteLine("hello from greet"));
        var registry = new CommandRegistry();
        registry.Register(command);

        // Act: run with the command name and one argument
        var (exitCode, output) = RunProgram(registry, "greet", "input.png");

        // Assert: the command ran with its argument and its output is shown
        Assert.Contains("hello from greet", output);
        Assert.Equal(new[] { "input.png" }, command.ReceivedArguments);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that Run with a command name prints nothing but the command's own output.
    /// </summary>
    [Fact]
    public void Program_Run_WithCommand_DoesNotPrintBanner()
    {
        // Arrange: a registry with a command that writes nothing
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("quiet"));

        // Act: run the command
        var (exitCode, output) = RunProgram(registry, "quiet");

        // Assert: no banner, no copyright, no output at all
        Assert.Equal(string.Empty, output);
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    ///     Test that version property returns non-empty version string.
    /// </summary>
    [Fact]
    public void Program_Version_ReturnsNonEmptyString()
    {
        // Act: read the version
        var version = Program.Version;

        // Assert: it is non-empty
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    /// <summary>
    ///     Test that Main with invalid arguments returns non-zero exit code.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode()
    {
        // Arrange: (none)

        // Act: invoke Main with an invalid argument, capturing the error output
        var (result, _, stdErr) = ToolHost.Capture(() => Program.Main(["--invalid-argument"]));

        // Assert: invalid arguments produce exit code 1 and name the argument
        Assert.Equal(1, result);
        Assert.Contains("Error: Unsupported argument '--invalid-argument'", stdErr);
    }

    /// <summary>
    ///     Test that Execute returns zero for a successful command.
    /// </summary>
    [Fact]
    public void Program_Execute_SuccessfulCommand_ReturnsZero()
    {
        // Arrange: a registry with a command that succeeds
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("ok"));

        // Act: execute the command
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "ok");

        // Assert: success, nothing on standard error
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stdErr);
    }

    /// <summary>
    ///     Test that Execute reports an unknown command and returns one.
    /// </summary>
    [Fact]
    public void Program_Execute_UnknownCommand_ReturnsOneWithUsageError()
    {
        // Arrange: an empty registry
        var registry = new CommandRegistry();

        // Act: execute an unknown command
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "bogus");

        // Assert: exit code 1 and a usage error naming the command
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: usage-error: Unknown command 'bogus'.", stdErr);
    }

    /// <summary>
    ///     Test that Execute reports the code, message, and hint of a ToolException.
    /// </summary>
    [Fact]
    public void Program_Execute_ToolException_ReportsCodeMessageAndHint()
    {
        // Arrange: a command that throws a ToolException with a hint
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "fail",
            (_, _) => throw new ToolException(ErrorCodes.OutputExists, "Output 'a.png' exists.", "Remove --no-clobber.")));

        // Act: execute the command
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "fail");

        // Assert: exit code 1; the error line carries the code, the hint is on its own line
        Assert.Equal(1, exitCode);
        Assert.Equal(
            $"Error: output-exists: Output 'a.png' exists.{Environment.NewLine}Hint: Remove --no-clobber.{Environment.NewLine}",
            stdErr);
    }

    /// <summary>
    ///     Test that Execute reports a ToolException without a hint on a single line.
    /// </summary>
    [Fact]
    public void Program_Execute_ToolExceptionWithoutHint_ReportsSingleLine()
    {
        // Arrange: a command that throws a ToolException with no hint
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "fail",
            (_, _) => throw new ToolException(ErrorCodes.OperationFailed, "It failed.")));

        // Act: execute the command
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "fail");

        // Assert: exit code 1 and exactly one error line
        Assert.Equal(1, exitCode);
        Assert.Equal($"Error: operation-failed: It failed.{Environment.NewLine}", stdErr);
    }

    /// <summary>
    ///     Test that Execute reports an InvalidOperationException by message only.
    /// </summary>
    [Fact]
    public void Program_Execute_InvalidOperationException_ReportsMessageOnly()
    {
        // Arrange: a command that throws a plain InvalidOperationException
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("fail", (_, _) => throw new InvalidOperationException("bad state")));

        // Act: execute the command
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "fail");

        // Assert: exit code 1 and the template-style message line
        Assert.Equal(1, exitCode);
        Assert.Equal($"Error: bad state{Environment.NewLine}", stdErr);
    }

    /// <summary>
    ///     Test that Execute reports an ArgumentException by message only.
    /// </summary>
    [Fact]
    public void Program_Execute_ArgumentException_ReportsMessageOnly()
    {
        // Arrange: a command that throws a plain ArgumentException
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("fail", (_, _) => throw new ArgumentException("bad argument")));

        // Act: execute the command
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "fail");

        // Assert: exit code 1 and the template-style message line
        Assert.Equal(1, exitCode);
        Assert.Equal($"Error: bad argument{Environment.NewLine}", stdErr);
    }

    /// <summary>
    ///     Test that Execute reports an unexpected exception and then re-throws it.
    /// </summary>
    [Fact]
    public void Program_Execute_UnexpectedException_ReportsAndRethrows()
    {
        // Arrange: a command that throws an exception that is not an expected failure
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("defect", (_, _) => throw new NotSupportedException("defect")));

        // Act: execute the command, expecting the exception to escape
        var (exception, _, stdErr) = ToolHost.InvokeExpectingThrow<NotSupportedException>(registry, "defect");

        // Assert: the same exception escaped and the message was reported first
        Assert.Equal("defect", exception.Message);
        Assert.Equal($"Unexpected error: defect{Environment.NewLine}", stdErr);
    }

    /// <summary>
    ///     Test that Execute writes the JSON failure document to standard output when --json is on the command line.
    /// </summary>
    [Fact]
    public void Program_Execute_JsonToolException_WritesErrorDocumentToStdOut()
    {
        // Arrange: a command that throws a ToolException with a hint
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "fail",
            (_, _) => throw new ToolException(ErrorCodes.OutputExists, "Output 'a.png' exists.", "Remove --no-clobber.")));

        // Act: execute the command with --json
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "fail", "--json");

        // Assert: exit code 1; the document on standard output has no exit code field; the text report is still on standard error
        Assert.Equal(1, exitCode);
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":\"fail\","
            + "\"error\":{\"code\":\"output-exists\",\"message\":\"Output 'a.png' exists.\",\"hint\":\"Remove --no-clobber.\"}}"
            + Environment.NewLine,
            stdOut);
        Assert.Contains("Error: output-exists: Output 'a.png' exists.", stdErr);
    }

    /// <summary>
    ///     Test that Execute reports a failure that happened before a command was selected, with a null command.
    /// </summary>
    [Fact]
    public void Program_Execute_JsonBadGlobalOption_WritesErrorDocumentWithNullCommand()
    {
        // Arrange: an empty registry and an unknown option before any command
        var registry = new CommandRegistry();

        // Act: execute with --json and the unknown option
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "--json", "--bogus");

        // Assert: exit code 1 and a usage-error document without a command
        Assert.Equal(1, exitCode);
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":null,"
            + "\"error\":{\"code\":\"usage-error\",\"message\":\"Unsupported argument '--bogus'\"}}"
            + Environment.NewLine,
            stdOut);
        Assert.Contains("Error: Unsupported argument '--bogus'", stdErr);
    }

    /// <summary>
    ///     Test that a plain expected exception is reported as operation-failed with the command that was running.
    /// </summary>
    [Fact]
    public void Program_Execute_JsonInvalidOperationException_ReportsOperationFailedForCommand()
    {
        // Arrange: a command that throws a plain InvalidOperationException
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("fail", (_, _) => throw new InvalidOperationException("bad state")));

        // Act: execute the command with --json
        var (exitCode, stdOut, _) = ToolHost.Invoke(registry, "--json", "fail");

        // Assert: operation-failed, naming the command taken from the command line
        Assert.Equal(1, exitCode);
        Assert.Equal(
            "{\"schemaVersion\":1,\"ok\":false,\"command\":\"fail\","
            + "\"error\":{\"code\":\"operation-failed\",\"message\":\"bad state\"}}"
            + Environment.NewLine,
            stdOut);
    }

    /// <summary>
    ///     Test that --silent suppresses the JSON failure document and the text report, and that without --json no
    ///     document is written.
    /// </summary>
    [Fact]
    public void Program_Execute_JsonSilentOrAbsent_WritesNoErrorDocument()
    {
        // Arrange: a command that fails with an expected error
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("fail", (_, _) => throw new ToolException(ErrorCodes.OperationFailed, "It failed.")));

        // Act: execute with --json and --silent, then without --json
        var (silentExit, silentOut, silentErr) = ToolHost.Invoke(registry, "fail", "--json", "--silent");
        var (plainExit, plainOut, plainErr) = ToolHost.Invoke(registry, "fail");

        // Assert: exit code 1 both times; --silent leaves both streams empty; without --json only the text report
        Assert.Equal(1, silentExit);
        Assert.Equal(string.Empty, silentOut);
        Assert.Equal(string.Empty, silentErr);
        Assert.Equal(1, plainExit);
        Assert.Equal(string.Empty, plainOut);
        Assert.Contains("Error: operation-failed: It failed.", plainErr);
    }

    /// <summary>
    ///     Test that --silent suppresses an expected error of any kind (a ToolException with a hint, an
    ///     InvalidOperationException, and an ArgumentException) but still exits with 1.
    /// </summary>
    [Fact]
    public void Program_Execute_SilentExpectedError_SuppressesConsoleOutput()
    {
        // Arrange: commands that fail in each of the expected ways
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("tool", (_, _) => throw new ToolException(ErrorCodes.OutputExists, "Exists.", "A hint.")));
        registry.Register(new TestCommand("operation", (_, _) => throw new InvalidOperationException("bad state")));
        registry.Register(new TestCommand("argument", (_, _) => throw new ArgumentException("bad argument")));

        // Act: execute each with --silent (before and after the command name)
        var results = new[]
        {
            ToolHost.Invoke(registry, "tool", "--silent"),
            ToolHost.Invoke(registry, "--silent", "operation"),
            ToolHost.Invoke(registry, "argument", "--silent")
        };

        // Assert: exit code 1 and nothing at all on either stream
        foreach (var (exitCode, stdOut, stdErr) in results)
        {
            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, stdOut);
            Assert.Equal(string.Empty, stdErr);
        }
    }

    /// <summary>
    ///     Test that --log captures an expected error exactly as it is written to standard error, including the hint.
    /// </summary>
    [Fact]
    public void Program_Execute_LogExpectedError_CapturesReport()
    {
        // Arrange: a failing command and a log file
        using var directory = new TempDirectory();
        var log = directory.Combine("tool.log");
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "fail",
            (_, _) => throw new ToolException(ErrorCodes.OutputExists, "Output 'a.png' exists.", "Remove --no-clobber.")));

        // Act: execute with --log
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "fail", "--log", log);

        // Assert: the log holds the same report as standard error, and the file is closed (readable) afterwards
        var expected = $"Error: output-exists: Output 'a.png' exists.{Environment.NewLine}Hint: Remove --no-clobber.{Environment.NewLine}";
        Assert.Equal(1, exitCode);
        Assert.Equal(expected, stdErr);
        Assert.Equal(expected, File.ReadAllText(log));
    }

    /// <summary>
    ///     Test that --silent with --log keeps the error out of the console but in the log, together with the JSON
    ///     failure document when --json is given.
    /// </summary>
    [Fact]
    public void Program_Execute_SilentLogJsonExpectedError_WritesOnlyToLog()
    {
        // Arrange: a failing command and a log file
        using var directory = new TempDirectory();
        var log = directory.Combine("tool.log");
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("fail", (_, _) => throw new ToolException(ErrorCodes.OperationFailed, "It failed.")));

        // Act: execute with --silent, --log and --json
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "fail", "--silent", "--json", "--log", log);

        // Assert: the console is silent; the log has the text report and the JSON document
        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdOut);
        Assert.Equal(string.Empty, stdErr);
        Assert.Equal(
            $"Error: operation-failed: It failed.{Environment.NewLine}"
            + "{\"schemaVersion\":1,\"ok\":false,\"command\":\"fail\","
            + "\"error\":{\"code\":\"operation-failed\",\"message\":\"It failed.\"}}"
            + Environment.NewLine,
            File.ReadAllText(log));
    }

    /// <summary>
    ///     Test that an unexpected exception is captured in the log before it is re-thrown.
    /// </summary>
    [Fact]
    public void Program_Execute_LogUnexpectedException_CapturesReportAndRethrows()
    {
        // Arrange: a command with a defect and a log file
        using var directory = new TempDirectory();
        var log = directory.Combine("tool.log");
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("defect", (_, _) => throw new NotSupportedException("defect")));

        // Act: execute with --log, expecting the exception to escape
        var (exception, _, stdErr) = ToolHost.InvokeExpectingThrow<NotSupportedException>(registry, "defect", "--log", log);

        // Assert: re-thrown; reported on standard error and in the (closed) log
        Assert.Equal("defect", exception.Message);
        Assert.Equal($"Unexpected error: defect{Environment.NewLine}", stdErr);
        Assert.Equal($"Unexpected error: defect{Environment.NewLine}", File.ReadAllText(log));
    }

    /// <summary>
    ///     Test that a log file that cannot be opened is reported on the console, because no context exists to carry
    ///     the report, even with --silent.
    /// </summary>
    [Fact]
    public void Program_Execute_LogCannotBeOpened_ReportsOnConsole()
    {
        // Arrange: a log path in a folder that does not exist
        using var directory = new TempDirectory();
        var log = Path.Combine(directory.Path, "missing", "tool.log");
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("ok"));

        // Act: execute with --log
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "ok", "--log", log);

        // Assert: exit code 1 and the failure on standard error; the command did not run
        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdOut);
        Assert.StartsWith("Error: Failed to open log file", stdErr, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an unexpected exception produces no JSON document, even with --json.
    /// </summary>
    [Fact]
    public void Program_Execute_JsonUnexpectedException_WritesNoDocumentAndRethrows()
    {
        // Arrange: a command with a defect
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("defect", (_, _) => throw new NotSupportedException("defect")));

        // Act: execute with --json, expecting the exception to escape
        var (exception, stdOut, stdErr) = ToolHost.InvokeExpectingThrow<NotSupportedException>(registry, "defect", "--json");

        // Assert: re-thrown, reported on standard error, and nothing on standard output
        Assert.Equal("defect", exception.Message);
        Assert.Equal(string.Empty, stdOut);
        Assert.Contains("Unexpected error: defect", stdErr);
    }
}
