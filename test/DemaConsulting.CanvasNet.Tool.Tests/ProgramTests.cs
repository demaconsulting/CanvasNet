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
}
