// cspell:ignore canvasnet rendr convrt
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the CommandRegistry class.
/// </summary>
[Collection("Sequential")]
public class CommandRegistryTests
{
    /// <summary>
    ///     Test that CreateDefault returns a usable registry.
    /// </summary>
    [Fact]
    public void CommandRegistry_CreateDefault_ReturnsRegistry()
    {
        // Arrange / Act: create the default registry
        var registry = CommandRegistry.CreateDefault();

        // Assert: it exists, the shipped commands are registered (render also as convert), and every registered
        // command resolves by its own name
        Assert.NotNull(registry);
        Assert.Equal(
            ["formats", "info", "render", "crop", "resize", "rotate", "flip", "pad", "compare", "montage", "inspect"],
            registry.Commands.Select(c => c.Name));
        Assert.Same(registry.Find("render"), registry.Find("convert"));
        Assert.All(registry.Commands, command => Assert.Same(command, registry.Find(command.Name)));
    }

    /// <summary>
    ///     Test that Register keeps commands in registration order.
    /// </summary>
    [Fact]
    public void CommandRegistry_Register_TwoCommands_KeepsRegistrationOrder()
    {
        // Arrange: two commands
        var registry = new CommandRegistry();
        var first = new TestCommand("first");
        var second = new TestCommand("second");

        // Act: register them in order
        registry.Register(first);
        registry.Register(second);

        // Assert: the command list keeps the order
        Assert.Equal(new ICommand[] { first, second }, registry.Commands);
    }

    /// <summary>
    ///     Test that Register rejects a duplicate command name.
    /// </summary>
    [Fact]
    public void CommandRegistry_Register_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange: a registry that already holds "render"
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));

        // Act / Assert: registering the same name again (in a different case) is rejected
        var exception = Assert.Throws<InvalidOperationException>(() => registry.Register(new TestCommand("RENDER")));
        Assert.Contains("already registered", exception.Message);
    }

    /// <summary>
    ///     Test that Register rejects an alias that is already a command name.
    /// </summary>
    [Fact]
    public void CommandRegistry_Register_DuplicateAlias_ThrowsInvalidOperationException()
    {
        // Arrange: a registry that already holds "render"
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));

        // Act / Assert: a new command whose alias collides with an existing name is rejected
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new TestCommand("convert", null, "render")));
    }

    /// <summary>
    ///     Test that Register rejects a command that repeats one of its own names.
    /// </summary>
    [Fact]
    public void CommandRegistry_Register_AliasRepeatsOwnName_ThrowsInvalidOperationException()
    {
        // Arrange: an empty registry
        var registry = new CommandRegistry();

        // Act / Assert: a command whose alias equals its own name is rejected
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new TestCommand("convert", null, "convert")));
    }

    /// <summary>
    ///     Test that Register rejects the reserved help name.
    /// </summary>
    [Fact]
    public void CommandRegistry_Register_ReservedHelpName_ThrowsInvalidOperationException()
    {
        // Arrange: an empty registry
        var registry = new CommandRegistry();

        // Act / Assert: "help" is reserved, as a name and as an alias
        var exception = Assert.Throws<InvalidOperationException>(() => registry.Register(new TestCommand("help")));
        Assert.Contains("reserved", exception.Message);
        Assert.Throws<InvalidOperationException>(() => registry.Register(new TestCommand("other", null, "help")));
    }

    /// <summary>
    ///     Test that Register rejects a null command.
    /// </summary>
    [Fact]
    public void CommandRegistry_Register_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange: an empty registry
        var registry = new CommandRegistry();

        // Act / Assert: a null command is rejected
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    /// <summary>
    ///     Test that Find resolves a command by name or alias, ignoring case.
    /// </summary>
    [Fact]
    public void CommandRegistry_Find_NameOrAlias_ReturnsCommand()
    {
        // Arrange: a registry with an aliased command
        var registry = new CommandRegistry();
        var command = new TestCommand("render", null, "convert");
        registry.Register(command);

        // Act / Assert: the name and the alias resolve to the command regardless of case
        Assert.Same(command, registry.Find("render"));
        Assert.Same(command, registry.Find("CONVERT"));
    }

    /// <summary>
    ///     Test that Find returns null for an unknown name.
    /// </summary>
    [Fact]
    public void CommandRegistry_Find_UnknownName_ReturnsNull()
    {
        // Arrange: a registry with one command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));

        // Act / Assert: an unknown name resolves to nothing
        Assert.Null(registry.Find("nothing"));
    }

    /// <summary>
    ///     Test that Run dispatches to the selected command with its arguments.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_RegisteredCommand_RunsCommandWithArguments()
    {
        // Arrange: a registry with an aliased command, and a context that selects it by alias
        var registry = new CommandRegistry();
        var command = new TestCommand("render", null, "convert");
        registry.Register(command);
        using var context = Context.Create(["convert", "a.svg", "-o", "a.png"]);

        // Act: run the registry
        registry.Run(context);

        // Assert: the command received exactly the command arguments
        Assert.Equal(new[] { "a.svg", "-o", "a.png" }, command.ReceivedArguments);
    }

    /// <summary>
    ///     Test that Run without a command name throws a usage error.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_NoCommand_ThrowsUsageError()
    {
        // Arrange: a registry and a context without a command
        var registry = new CommandRegistry();
        using var context = Context.Create(["--silent"]);

        // Act / Assert: a usage error with a hint is thrown
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.NotNull(exception.Hint);
    }

    /// <summary>
    ///     Test that Run suggests the closest name for a mistyped command.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_UnknownCommandNearAName_SuggestsClosestName()
    {
        // Arrange: a registry with "render" and "info"
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));
        registry.Register(new TestCommand("info"));
        using var context = Context.Create(["rendr"]);

        // Act: run the mistyped command
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));

        // Assert: a usage error names the typo and suggests the closest command
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("Unknown command 'rendr'", exception.Message);
        Assert.Contains("Did you mean 'render'?", exception.Hint);
        Assert.Equal("rendr", exception.Command);
    }

    /// <summary>
    ///     Test that Run suggests an alias when it is the closest name.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_UnknownCommandNearAnAlias_SuggestsTheAlias()
    {
        // Arrange: a registry with an aliased command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render", null, "convert"));
        using var context = Context.Create(["convrt"]);

        // Act: run the mistyped alias
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));

        // Assert: the alias is suggested
        Assert.Contains("Did you mean 'convert'?", exception.Hint);
    }

    /// <summary>
    ///     Test that Run offers no suggestion for a name far from every command.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_UnknownCommandFarFromAll_HasNoSuggestion()
    {
        // Arrange: a registry with one command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));
        using var context = Context.Create(["zzzzzzzz"]);

        // Act: run the unrelated name
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));

        // Assert: the hint points at the help but suggests no name
        Assert.DoesNotContain("Did you mean", exception.Hint);
        Assert.Contains("--help", exception.Hint);
    }

    /// <summary>
    ///     Test that Run records the command name on a ToolException that has none.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_ToolExceptionWithoutCommand_FillsCommandName()
    {
        // Arrange: a command that fails with a ToolException that does not name a command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "render",
            (_, _) => throw new ToolException(ErrorCodes.InputNotFound, "missing"),
            "convert"));
        using var context = Context.Create(["convert"]);

        // Act: run the failing command through its alias
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));

        // Assert: the canonical command name was recorded and the original details are intact
        Assert.Equal("render", exception.Command);
        Assert.Equal(ErrorCodes.InputNotFound, exception.Code);
        Assert.Equal("missing", exception.Message);
    }

    /// <summary>
    ///     Test that Run keeps a command name that the exception already carries.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_ToolExceptionWithCommand_KeepsCommandName()
    {
        // Arrange: a command that fails with a ToolException that already names a command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand(
            "render",
            (_, _) => throw new ToolException(ErrorCodes.OperationFailed, "failed") { Command = "inner" }));
        using var context = Context.Create(["render"]);

        // Act: run the failing command
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));

        // Assert: the existing command name was not overwritten
        Assert.Equal("inner", exception.Command);
    }

    /// <summary>
    ///     Test that Run lets an unexpected exception propagate unchanged.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_UnexpectedException_PropagatesUnchanged()
    {
        // Arrange: a command with a defect
        var registry = new CommandRegistry();
        var defect = new NotSupportedException("defect");
        registry.Register(new TestCommand("render", (_, _) => throw defect));
        using var context = Context.Create(["render"]);

        // Act / Assert: the very same exception escapes
        var thrown = Assert.Throws<NotSupportedException>(() => registry.Run(context));
        Assert.Same(defect, thrown);
    }

    /// <summary>
    ///     Test that Run rejects a null context.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: a null context is rejected
        Assert.Throws<ArgumentNullException>(() => new CommandRegistry().Run(null!));
    }

    /// <summary>
    ///     Test that the built-in help command prints help for the named command.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpCommandWithName_PrintsCommandHelp()
    {
        // Arrange: a registry with one command, and a context for "help render"
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));
        using var context = Context.Create(["help", "render"]);

        // Act: run the help command
        var (stdOut, _) = ToolHost.Capture(() => registry.Run(context));

        // Assert: the help of that command was printed
        Assert.Contains("Usage: canvasnet render <input> [options]", stdOut);
    }

    /// <summary>
    ///     Test that the built-in help command without an argument prints the top-level help.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpCommandWithoutName_PrintsTopLevelHelp()
    {
        // Arrange: a registry with one command, and a context for "help"
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));
        using var context = Context.Create(["help"]);

        // Act: run the help command
        var (stdOut, _) = ToolHost.Capture(() => registry.Run(context));

        // Assert: the command list and the global options were printed
        Assert.Contains("Commands:", stdOut);
        Assert.Contains("render", stdOut);
        Assert.Contains("Options:", stdOut);
    }

    /// <summary>
    ///     Test that top-level help lists the commands with their aliases and the global options.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_NoCommand_ListsCommandsAndGlobalOptions()
    {
        // Arrange: a registry with an aliased command and a plain command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render", null, "convert"));
        registry.Register(new TestCommand("info"));

        // Act: render the top-level help
        var help = registry.RenderHelp();

        // Assert: usage, both commands with summaries, the alias, and every global option appear
        Assert.StartsWith("Usage: canvasnet <command>", help);
        Assert.Contains("render, convert", help);
        Assert.Contains("Summary of render", help);
        Assert.Contains("Summary of info", help);
        Assert.Contains("-v, --version", help);
        Assert.Contains("-?, -h, --help", help);
        Assert.Contains("--silent", help);
        Assert.Contains("--validate", help);
        Assert.Contains("--json", help);
        Assert.Contains("--result, --results <file>", help);
        Assert.Contains("--depth <#>", help);
        Assert.Contains("--log <file>", help);
        Assert.False(help.EndsWith('\n'));
    }

    /// <summary>
    ///     Test that top-level help omits the command section when no command is registered.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_EmptyRegistry_OmitsCommandSection()
    {
        // Arrange: an empty registry
        var registry = new CommandRegistry();

        // Act: render the top-level help
        var help = registry.RenderHelp();

        // Assert: the options are listed but there is no command section
        Assert.DoesNotContain("Commands:", help);
        Assert.Contains("Options:", help);
    }

    /// <summary>
    ///     Test that command help shows usage, summary, the command's options, and the global options.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Command_ShowsUsageSummaryAndOptions()
    {
        // Arrange: a registry with one command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render", null, "convert"));

        // Act: render the help of the command by its alias
        var help = registry.RenderHelp("convert");

        // Assert: usage, summary, the command's own options with value placeholders, and the global options
        Assert.StartsWith("Usage: canvasnet render <input> [options]", help);
        Assert.Contains("Summary of render", help);
        Assert.Contains("-f, --flag", help);
        Assert.Contains("--value <text>", help);
        Assert.Contains("Global options:", help);
        Assert.Contains("--silent", help);
    }

    /// <summary>
    ///     Test that asking for the help of the reserved help name gives the top-level help.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_HelpName_ReturnsTopLevelHelp()
    {
        // Arrange: an empty registry
        var registry = new CommandRegistry();

        // Act: render help for the name "help"
        var help = registry.RenderHelp("help");

        // Assert: it is the top-level help
        Assert.Equal(registry.RenderHelp(), help);
    }

    /// <summary>
    ///     Test that help for an unknown command throws a usage error.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_UnknownCommand_ThrowsUsageError()
    {
        // Arrange: a registry with one command
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render"));

        // Act / Assert: help for an unknown command is a usage error
        var exception = Assert.Throws<ToolException>(() => registry.RenderHelp("nothing"));
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
    }
}
