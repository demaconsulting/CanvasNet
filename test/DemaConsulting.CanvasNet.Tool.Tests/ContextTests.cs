namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Unit tests for the Context class.
/// </summary>
[Collection("Sequential")]
public class ContextTests
{
    /// <summary>
    ///     Test that Create with no arguments returns the default context.
    /// </summary>
    [Fact]
    public void Context_Create_NoArguments_ReturnsDefaultContext()
    {
        // Arrange: (none)

        // Act: create a context from an empty argument list
        using var context = Context.Create([]);

        // Assert: every flag is off, the defaults apply, and no command was captured
        Assert.False(context.Version);
        Assert.False(context.Help);
        Assert.False(context.Silent);
        Assert.False(context.Validate);
        Assert.False(context.Json);
        Assert.Null(context.ResultsFile);
        Assert.Equal(1, context.HeadingDepth);
        Assert.Null(context.Command);
        Assert.Empty(context.CommandArguments);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that Create with --version sets the version flag.
    /// </summary>
    [Fact]
    public void Context_Create_VersionFlag_SetsVersionTrue()
    {
        // Arrange / Act: create a context with the long version flag
        using var context = Context.Create(["--version"]);

        // Assert: the version flag is set and is not mistaken for a command
        Assert.True(context.Version);
        Assert.Null(context.Command);
    }

    /// <summary>
    ///     Test that Create with -v sets the version flag.
    /// </summary>
    [Fact]
    public void Context_Create_ShortVersionFlag_SetsVersionTrue()
    {
        // Arrange / Act: create a context with the short version flag
        using var context = Context.Create(["-v"]);

        // Assert: the version flag is set
        Assert.True(context.Version);
    }

    /// <summary>
    ///     Test that Create with --help sets the help flag.
    /// </summary>
    [Fact]
    public void Context_Create_HelpFlag_SetsHelpTrue()
    {
        // Arrange / Act: create a context with the long help flag
        using var context = Context.Create(["--help"]);

        // Assert: the help flag is set
        Assert.True(context.Help);
    }

    /// <summary>
    ///     Test that Create with -h sets the help flag.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_H_SetsHelpTrue()
    {
        // Arrange / Act: create a context with -h
        using var context = Context.Create(["-h"]);

        // Assert: the help flag is set
        Assert.True(context.Help);
    }

    /// <summary>
    ///     Test that Create with -? sets the help flag.
    /// </summary>
    [Fact]
    public void Context_Create_ShortHelpFlag_Question_SetsHelpTrue()
    {
        // Arrange / Act: create a context with -?
        using var context = Context.Create(["-?"]);

        // Assert: the help flag is set
        Assert.True(context.Help);
    }

    /// <summary>
    ///     Test that Create with --silent sets the silent flag.
    /// </summary>
    [Fact]
    public void Context_Create_SilentFlag_SetsSilentTrue()
    {
        // Arrange / Act: create a context with --silent
        using var context = Context.Create(["--silent"]);

        // Assert: the silent flag is set
        Assert.True(context.Silent);
    }

    /// <summary>
    ///     Test that Create with --json sets the JSON flag, and that it is not mistaken for a command.
    /// </summary>
    [Fact]
    public void Context_Create_JsonFlag_SetsJsonTrue()
    {
        // Arrange / Act: create a context with --json
        using var context = Context.Create(["--json"]);

        // Assert: the JSON flag is set and there is no command
        Assert.True(context.Json);
        Assert.Null(context.Command);
    }

    /// <summary>
    ///     Test that --json is honored after the command name and removed from the command arguments.
    /// </summary>
    [Fact]
    public void Context_Create_JsonFlagAfterCommand_IsHonoredAndRemovedFromArguments()
    {
        // Arrange / Act: create a context with --json between command arguments
        using var context = Context.Create(["render", "a.svg", "--json", "-o", "a.png"]);

        // Assert: the flag is set and the command saw only its own tokens
        Assert.True(context.Json);
        Assert.Equal("render", context.Command);
        Assert.Equal(new[] { "a.svg", "-o", "a.png" }, context.CommandArguments);
    }

    /// <summary>
    ///     Test that Create with --validate sets the validate flag.
    /// </summary>
    [Fact]
    public void Context_Create_ValidateFlag_SetsValidateTrue()
    {
        // Arrange / Act: create a context with --validate
        using var context = Context.Create(["--validate"]);

        // Assert: the validate flag is set
        Assert.True(context.Validate);
    }

    /// <summary>
    ///     Test that Create with --results sets the results file.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_SetsResultsFile()
    {
        // Arrange / Act: create a context with --results
        using var context = Context.Create(["--results", "output.trx"]);

        // Assert: the results file is captured
        Assert.Equal("output.trx", context.ResultsFile);
    }

    /// <summary>
    ///     Test that Create with the legacy --result alias sets the results file.
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_SetsResultsFile()
    {
        // Arrange / Act: create a context with the legacy alias
        using var context = Context.Create(["--result", "output.trx"]);

        // Assert: the alias behaves exactly like --results
        Assert.Equal("output.trx", context.ResultsFile);
    }

    /// <summary>
    ///     Test that Create with --log opens the log file.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_OpensLogFile()
    {
        // Arrange: a temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: create a context with --log and write a line
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                context.WriteLine("log test message");
            }

            // Assert: the log file contains the message even though the console is silent
            Assert.Contains("log test message", File.ReadAllText(logFile));
        }
        finally
        {
            File.Delete(logFile);
        }
    }

    /// <summary>
    ///     Test that Create with --depth sets the heading depth.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_SetsHeadingDepth()
    {
        // Arrange / Act: create a context with --depth 3
        using var context = Context.Create(["--depth", "3"]);

        // Assert: the heading depth is captured
        Assert.Equal(3, context.HeadingDepth);
    }

    /// <summary>
    ///     Test that Create without --depth uses the default heading depth.
    /// </summary>
    [Fact]
    public void Context_Create_NoDepthFlag_ReturnsDefaultHeadingDepth()
    {
        // Arrange / Act: create a context without --depth
        using var context = Context.Create([]);

        // Assert: the default depth is 1
        Assert.Equal(1, context.HeadingDepth);
    }

    /// <summary>
    ///     Test that Create captures the first non-option token as the command.
    /// </summary>
    [Fact]
    public void Context_Create_CommandName_CapturesCommand()
    {
        // Arrange / Act: create a context with a command name
        using var context = Context.Create(["render"]);

        // Assert: the command is captured and no arguments remain
        Assert.Equal("render", context.Command);
        Assert.Empty(context.CommandArguments);
    }

    /// <summary>
    ///     Test that Create without a command leaves the command null.
    /// </summary>
    [Fact]
    public void Context_Create_NoCommand_CommandIsNull()
    {
        // Arrange / Act: create a context with only global flags
        using var context = Context.Create(["--silent", "--depth", "2"]);

        // Assert: option values are not mistaken for the command
        Assert.Null(context.Command);
    }

    /// <summary>
    ///     Test that Create passes the tokens after the command to the command in their original order.
    /// </summary>
    [Fact]
    public void Context_Create_CommandWithArguments_PassesArgumentsInOrder()
    {
        // Arrange / Act: create a context with a command and several arguments
        using var context = Context.Create(["render", "in.svg", "-o", "out.png", "positional"]);

        // Assert: the arguments follow the command in order, including option-like tokens and values
        Assert.Equal("render", context.Command);
        Assert.Equal(new[] { "in.svg", "-o", "out.png", "positional" }, context.CommandArguments);
    }

    /// <summary>
    ///     Test that an unknown option after the command is passed through to the command.
    /// </summary>
    [Fact]
    public void Context_Create_UnknownOptionAfterCommand_PassesThroughToCommand()
    {
        // Arrange / Act: create a context with an option the global parser does not know, after the command
        using var context = Context.Create(["render", "--width", "100", "--unknown-to-globals"]);

        // Assert: nothing is rejected; the tokens are left for the command to interpret
        Assert.Equal("render", context.Command);
        Assert.Equal(new[] { "--width", "100", "--unknown-to-globals" }, context.CommandArguments);
    }

    /// <summary>
    ///     Test that a global flag after the command is honored.
    /// </summary>
    [Fact]
    public void Context_Create_GlobalFlagAfterCommand_IsHonored()
    {
        // Arrange / Act: create a context with global flags after the command name
        using var context = Context.Create(["render", "--silent", "--help"]);

        // Assert: the flags were consumed by the context, not passed to the command
        Assert.Equal("render", context.Command);
        Assert.True(context.Silent);
        Assert.True(context.Help);
        Assert.Empty(context.CommandArguments);
    }

    /// <summary>
    ///     Test that a global option between command arguments is removed from the command arguments.
    /// </summary>
    [Fact]
    public void Context_Create_GlobalFlagBetweenCommandArguments_IsRemovedFromArguments()
    {
        // Arrange / Act: create a context with a global option with a value in the middle of the command arguments
        using var context = Context.Create(["info", "a.png", "--depth", "4", "--pages"]);

        // Assert: the global option and its value are consumed; the other tokens keep their order
        Assert.Equal(4, context.HeadingDepth);
        Assert.Equal(new[] { "a.png", "--pages" }, context.CommandArguments);
    }

    /// <summary>
    ///     Test that the value of a global option is not mistaken for the command.
    /// </summary>
    [Fact]
    public void Context_Create_OptionValueBeforeCommand_IsNotTheCommand()
    {
        // Arrange / Act: create a context where a global option value precedes the command name
        using var context = Context.Create(["--results", "out.trx", "info"]);

        // Assert: the value belongs to the option and the command is the following token
        Assert.Equal("out.trx", context.ResultsFile);
        Assert.Equal("info", context.Command);
    }

    /// <summary>
    ///     Test that an unknown option before the command throws.
    /// </summary>
    [Fact]
    public void Context_Create_UnknownArgument_ThrowsArgumentException()
    {
        // Arrange / Act: create a context with an unknown option and no command yet
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--unknown"]));

        // Assert: the message names the unsupported argument
        Assert.Contains("Unsupported argument '--unknown'", exception.Message);
    }

    /// <summary>
    ///     Test that --log without a value throws.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_WithoutValue_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: the missing filename is rejected
        Assert.Throws<ArgumentException>(() => Context.Create(["--log"]));
    }

    /// <summary>
    ///     Test that --results without a value throws.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsFlag_WithoutValue_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: the missing filename is rejected
        Assert.Throws<ArgumentException>(() => Context.Create(["--results"]));
    }

    /// <summary>
    ///     Test that --result without a value throws.
    /// </summary>
    [Fact]
    public void Context_Create_ResultAliasFlag_WithoutValue_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: the missing filename is rejected
        Assert.Throws<ArgumentException>(() => Context.Create(["--result"]));
    }

    /// <summary>
    ///     Test that --depth without a value throws.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_WithoutValue_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: the missing depth is rejected
        Assert.Throws<ArgumentException>(() => Context.Create(["--depth"]));
    }

    /// <summary>
    ///     Test that a non-integer --depth value throws.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_NonIntegerValue_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: a non-numeric depth is rejected
        Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "abc"]));
    }

    /// <summary>
    ///     Test that a --depth value of zero throws.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ZeroValue_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: a depth below the minimum is rejected
        Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "0"]));
    }

    /// <summary>
    ///     Test that a --depth value above the maximum throws.
    /// </summary>
    [Fact]
    public void Context_Create_DepthFlag_ExceedsMaxValue_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: a depth above the maximum is rejected
        Assert.Throws<ArgumentException>(() => Context.Create(["--depth", "7"]));
    }

    /// <summary>
    ///     Test that Create with a null argument array throws.
    /// </summary>
    [Fact]
    public void Context_Create_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: a null argument array is rejected
        Assert.Throws<ArgumentNullException>(() => Context.Create(null!));
    }

    /// <summary>
    ///     Test that Create with a log file that cannot be opened throws.
    /// </summary>
    [Fact]
    public void Context_Create_LogFlag_InvalidPath_ThrowsInvalidOperationException()
    {
        // Arrange: a log path whose directory does not exist, so the file cannot be opened
        var logFile = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid()}", "x.log");

        // Act / Assert: the failure is reported as an expected operation failure that names the file
        var exception = Assert.Throws<InvalidOperationException>(() => Context.Create(["--log", logFile]));
        Assert.Contains("Failed to open log file", exception.Message);
    }

    /// <summary>
    ///     Test that WriteLine writes to standard output when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_NotSilent_WritesToConsole()
    {
        // Arrange: a non-silent context
        using var context = Context.Create([]);

        // Act: write a line
        var (stdOut, stdErr) = ToolHost.Capture(() => context.WriteLine("hello"));

        // Assert: it appears on standard output only
        Assert.Contains("hello", stdOut);
        Assert.Equal(string.Empty, stdErr);
    }

    /// <summary>
    ///     Test that WriteLine writes nothing when silent.
    /// </summary>
    [Fact]
    public void Context_WriteLine_Silent_DoesNotWriteToConsole()
    {
        // Arrange: a silent context
        using var context = Context.Create(["--silent"]);

        // Act: write a line
        var (stdOut, _) = ToolHost.Capture(() => context.WriteLine("hello"));

        // Assert: nothing appears on standard output
        Assert.Equal(string.Empty, stdOut);
    }

    /// <summary>
    ///     Test that WriteError writes to standard error when not silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_NotSilent_WritesToConsole()
    {
        // Arrange: a non-silent context
        using var context = Context.Create([]);

        // Act: write an error
        var (stdOut, stdErr) = ToolHost.Capture(() => context.WriteError("something failed"));

        // Assert: it appears on standard error only
        Assert.Contains("something failed", stdErr);
        Assert.Equal(string.Empty, stdOut);
    }

    /// <summary>
    ///     Test that WriteWarning writes to standard error when not silent, without setting the error exit code.
    /// </summary>
    [Fact]
    public void Context_WriteWarning_NotSilent_WritesToConsoleWithoutError()
    {
        // Arrange: a non-silent context
        using var context = Context.Create([]);

        // Act: write a warning
        var (stdOut, stdErr) = ToolHost.Capture(() => context.WriteWarning("something was skipped"));

        // Assert: it appears on standard error only and the exit code stays 0
        Assert.Contains("something was skipped", stdErr);
        Assert.Equal(string.Empty, stdOut);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that WriteWarning writes nothing to the console when silent, but still reaches the log file.
    /// </summary>
    [Fact]
    public void Context_WriteWarning_SilentWithLog_WritesOnlyToLogFile()
    {
        // Arrange: a temporary log file path and a silent context that logs
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: write a warning
            string stdErr;
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                (_, stdErr) = ToolHost.Capture(() => context.WriteWarning("logged warning"));
            }

            // Assert: nothing is on standard error, and the log file holds the warning
            Assert.Equal(string.Empty, stdErr);
            Assert.Contains("logged warning", File.ReadAllText(logFile));
        }
        finally
        {
            File.Delete(logFile);
        }
    }

    /// <summary>
    ///     Test that WriteError writes nothing when silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_Silent_DoesNotWriteToConsole()
    {
        // Arrange: a silent context
        using var context = Context.Create(["--silent"]);

        // Act: write an error
        var (_, stdErr) = ToolHost.Capture(() => context.WriteError("something failed"));

        // Assert: nothing appears on standard error
        Assert.Equal(string.Empty, stdErr);
    }

    /// <summary>
    ///     Test that WriteError sets the error exit code.
    /// </summary>
    [Fact]
    public void Context_WriteError_SetsErrorExitCode()
    {
        // Arrange: a silent context so the test output stays clean
        using var context = Context.Create(["--silent"]);
        Assert.Equal(0, context.ExitCode);

        // Act: report an error
        context.WriteError("something failed");

        // Assert: the exit code is now 1, even though the message was suppressed
        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>
    ///     Test that WriteError writes to the log file even when silent.
    /// </summary>
    [Fact]
    public void Context_WriteError_WritesToLogFile()
    {
        // Arrange: a temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: report an error on a silent context that logs
            using (var context = Context.Create(["--silent", "--log", logFile]))
            {
                context.WriteError("logged failure");
            }

            // Assert: the log file holds the error
            Assert.Contains("logged failure", File.ReadAllText(logFile));
        }
        finally
        {
            File.Delete(logFile);
        }
    }
}
