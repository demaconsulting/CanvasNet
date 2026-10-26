// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Subsystem tests for the Commands subsystem: command resolution, dispatch, and help, exercised
///     through the program's entry point with the command registry.
/// </summary>
[Collection("Sequential")]
public class CommandsSubsystemTests
{
    /// <summary>
    ///     Creates a registry holding two commands: "render" (alias "convert") and "info".
    /// </summary>
    /// <param name="render">Receives the "render" command.</param>
    /// <returns>The registry.</returns>
    private static CommandRegistry CreateRegistry(out TestCommand render)
    {
        render = new TestCommand("render", (context, args) => context.WriteLine($"rendered {string.Join(' ', args)}"), "convert");
        var registry = new CommandRegistry();
        registry.Register(render);
        registry.Register(new TestCommand("info"));
        return registry;
    }

    /// <summary>
    ///     Test that a registered command is run, by name or alias, with its own arguments.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Dispatch_RegisteredCommand_RunsWithItsArguments()
    {
        // Arrange: a registry with an aliased command
        var registry = CreateRegistry(out var render);

        // Act: run the command through its alias, with an argument
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "convert", "a.svg");

        // Assert: the command ran once with its argument and wrote its output
        Assert.Equal(0, exitCode);
        Assert.Equal($"rendered a.svg{Environment.NewLine}", stdOut);
        Assert.Equal(string.Empty, stdErr);
        Assert.Equal(new[] { "a.svg" }, render.ReceivedArguments);
    }

    /// <summary>
    ///     Test that global options are not passed on to the command.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Dispatch_GlobalOptionsMixedIn_AreNotPassedToCommand()
    {
        // Arrange: a registry with a command
        var registry = CreateRegistry(out var render);

        // Act: mix global options (a flag and an option with a value) among the command arguments
        var (exitCode, stdOut, _) = ToolHost.Invoke(registry, "render", "a.svg", "--silent", "-o", "a.png", "--depth", "2");

        // Assert: the command saw only its own tokens, and --silent suppressed its output
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stdOut);
        Assert.Equal(new[] { "a.svg", "-o", "a.png" }, render.ReceivedArguments);
    }

    /// <summary>
    ///     Test that a failing command's error carries the command name.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Dispatch_CommandThrowsToolException_RecordsCommandName()
    {
        // Arrange: a registry whose command fails with an expected error
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "render",
            (_, _) => throw new ToolException(ErrorCodes.CorruptInput, "The file is corrupt.")));
        using var context = Context.Create(["render"]);

        // Act: run the command through the program
        var exception = Assert.Throws<ToolException>(() => Program.Run(context, registry));

        // Assert: the registry named the command on the error
        Assert.Equal("render", exception.Command);
        Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
    }

    /// <summary>
    ///     Test that an unknown command is a usage error with a suggestion and exit code 1.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_UnknownCommand_Mistyped_ReportsUsageErrorWithSuggestion()
    {
        // Arrange: a registry with commands
        var registry = CreateRegistry(out _);

        // Act: run a mistyped command
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(registry, "inf");

        // Assert: exit code 1, nothing on stdout, and a usage error with a suggestion on stderr
        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdOut);
        Assert.Contains("Error: usage-error: Unknown command 'inf'.", stdErr);
        Assert.Contains("Hint: Did you mean 'info'?", stdErr);
    }

    /// <summary>
    ///     Test that top-level help lists the registered commands.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Help_TopLevel_ListsRegisteredCommands()
    {
        // Arrange: a registry with commands
        var registry = CreateRegistry(out _);

        // Act: ask for top-level help
        var (exitCode, stdOut, _) = ToolHost.Invoke(registry, "--help");

        // Assert: the commands, their aliases and summaries, and the global options are listed
        Assert.Equal(0, exitCode);
        Assert.Contains("render, convert", stdOut);
        Assert.Contains("Summary of render", stdOut);
        Assert.Contains("Summary of info", stdOut);
        Assert.Contains("Options:", stdOut);
    }

    /// <summary>
    ///     Test that a command's help is shown by both the --help flag and the help command, without running it.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Help_CommandHelp_ShownByFlagAndByHelpCommand()
    {
        // Arrange: a registry with commands
        var registry = CreateRegistry(out var render);

        // Act: ask for the command's help both ways
        var (flagExit, flagOut, _) = ToolHost.Invoke(registry, "render", "--help");
        var (commandExit, commandOut, _) = ToolHost.Invoke(registry, "help", "render");

        // Assert: both show the command's usage and options, the command itself never ran
        Assert.Equal(0, flagExit);
        Assert.Equal(0, commandExit);
        Assert.Contains("Usage: canvasnet render <input> [options]", flagOut);
        Assert.Contains("--value <text>", flagOut);
        Assert.Contains("Usage: canvasnet render <input> [options]", commandOut);
        Assert.Null(render.ReceivedArguments);
    }

    /// <summary>
    ///     Test that help for an unknown command is a usage error.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Help_UnknownCommand_ReportsUsageError()
    {
        // Arrange: a registry with commands
        var registry = CreateRegistry(out _);

        // Act: ask for help on a command that does not exist
        var (exitCode, _, stdErr) = ToolHost.Invoke(registry, "help", "nothing");

        // Assert: exit code 1 and a usage error
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: usage-error: Unknown command 'nothing'.", stdErr);
    }

    /// <summary>
    ///     Test that the default registry runs the formats command.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Formats_DefaultRegistry_RunsFormatsCommand()
    {
        // Arrange: the registry the tool ships with (through the default ToolHost registry)

        // Act: run the formats command as text and as JSON
        var (textExit, textOut, textErr) = ToolHost.Invoke("formats");
        var (jsonExit, jsonOut, _) = ToolHost.Invoke("formats", "--json");

        // Assert: the text listing has no banner; the JSON is the formats document
        Assert.Equal(0, textExit);
        Assert.StartsWith("Input formats:", textOut);
        Assert.Equal(string.Empty, textErr);
        Assert.Equal(0, jsonExit);
        Assert.StartsWith("{\"schemaVersion\":1,\"ok\":true,\"command\":\"formats\",", jsonOut);
    }

    /// <summary>
    ///     Test that the default registry lists the formats command in the top-level help and has help for it.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Formats_DefaultRegistry_ListedInHelp()
    {
        // Arrange: the registry the tool ships with

        // Act: ask for the top-level help and the command's help
        var (_, topLevel, _) = ToolHost.Invoke("--help");
        var (_, commandHelp, _) = ToolHost.Invoke("formats", "--help");

        // Assert: the command is listed with its summary, and its help shows the usage and the JSON example
        Assert.Contains("formats", topLevel);
        Assert.Contains("List the image and document formats", topLevel);
        Assert.Contains("Usage: canvasnet formats [--json]", commandHelp);
        Assert.Contains("canvasnet formats --json", commandHelp);
    }

    /// <summary>
    ///     Test that the default registry runs the info and render commands.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Imaging_DefaultRegistry_RunsInfoAndRender()
    {
        // Arrange: a PNG in a temporary directory
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");
        var output = directory.Combine("out.jpg");

        // Act: run info, render, and the convert alias through the default registry
        var (infoExit, infoOut, infoErr) = ToolHost.Invoke("info", input);
        var (renderExit, renderOut, renderErr) = ToolHost.Invoke("render", input, "-o", output);
        var (convertExit, convertOut, _) = ToolHost.Invoke("convert", input, "-o", directory.Combine("out.bmp"));

        // Assert: each succeeds with its own output, no banner and nothing on standard error
        Assert.Equal(0, infoExit);
        Assert.StartsWith($"{input}: png, 12x8 px", infoOut);
        Assert.Equal(string.Empty, infoErr);
        Assert.Equal(0, renderExit);
        Assert.StartsWith($"wrote {output} (12x8 jpeg,", renderOut);
        Assert.Equal(string.Empty, renderErr);
        Assert.Equal(0, convertExit);
        Assert.StartsWith("wrote ", convertOut);
    }

    /// <summary>
    ///     Test that the default registry lists the info and render commands, with the alias, and has help for them.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Imaging_DefaultRegistry_ListedInHelp()
    {
        // Arrange: the registry the tool ships with

        // Act: ask for the top-level help and the help of the commands
        var (_, topLevel, _) = ToolHost.Invoke("--help");
        var (_, renderHelp, _) = ToolHost.Invoke("render", "--help");
        var (_, convertHelp, _) = ToolHost.Invoke("help", "convert");
        var (_, infoHelp, _) = ToolHost.Invoke("info", "--help");

        // Assert: both commands are listed with their summaries and the alias; their help shows usage and options
        Assert.Contains("render, convert", topLevel);
        Assert.Contains("Render or convert an image or drawing", topLevel);
        Assert.Contains("Show the format, size, and properties", topLevel);
        Assert.Contains("Usage: canvasnet render <input> -o <output> [options]", renderHelp);
        Assert.Contains("--no-clobber", renderHelp);
        Assert.Contains("-o, --output <file>", renderHelp);
        Assert.Contains("Usage: canvasnet render <input> -o <output> [options]", convertHelp);
        Assert.Contains("Usage: canvasnet info <input> [--json]", infoHelp);
    }
}
