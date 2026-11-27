using DemaConsulting.CanvasNet.ImageSources;
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
            ["formats", "info", "render", "crop", "resize", "rotate", "flip", "pad", "compare", "montage", "inspect", "flatten", "pandoc-filter"],
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
    ///     Test that Run reports a failure of the library as a ToolException with the same code, message, location, detail
    ///     code and cause, naming the command, and with the hint in the words of the command line.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_ViewerException_KeepsLocationAndDetailCode()
    {
        // Arrange: a command that fails as the loader does for a model that uses an unsupported feature in strict mode
        var registry = new CommandRegistry();
        var cause = new FormatException("syntax");
        var where = new ImageDiagnosticLocation("model.scad", 2, 1, null);
        registry.Register(new TestCommand(
            "render",
            (_, _) => throw new ViewerException(
                ErrorCodes.UnsupportedFeature,
                "The SCAD model uses a feature that is not supported (offset).",
                "Without strict mode the feature is left out and reported in the diagnostics.",
                cause)
            {
                Location = where,
                DetailCode = "offset"
            }));
        using var context = Context.Create(["render"]);

        // Act
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));

        // Assert: the facts of the library are kept, and the hint names the option of the tool
        Assert.Equal(("render", ErrorCodes.UnsupportedFeature), (exception.Command, exception.Code));
        Assert.Equal("The SCAD model uses a feature that is not supported (offset).", exception.Message);
        Assert.Equal(where, exception.Location);
        Assert.Equal("offset", exception.DetailCode);
        Assert.Same(cause, exception.InnerException);
        Assert.Equal("Without --strict the feature is left out and reported in the diagnostics.", exception.Hint);
    }

    /// <summary>
    ///     Test that the render command run through the registry reports the failures of a model with the codes, location
    ///     and detail code of the library's shared mapping, and with hints that name the options of the tool: a syntax
    ///     error, an unsupported feature with <c>--strict</c>, and the triangle limit with <c>--max-triangles</c>.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_RenderOfFailingModels_ReportsLibraryCodesInCommandLineWords()
    {
        // Arrange
        using var directory = new TempDirectory();
        var broken = FixtureFactory.WriteText(directory.Path, "broken.scad", "cube([10, 10, 10]\nsphere(5);\n");
        var surface = FixtureFactory.WriteText(directory.Path, "surface.scad", "surface(\"A\");\ncube(1);\n");
        var model = FixtureFactory.WriteSource(directory.Path, "scad");
        var output = directory.Combine("o.png");
        var registry = CommandRegistry.CreateDefault();

        // Act
        var syntax = RunRenderFailing(registry, broken, "-o", output);
        var strict = RunRenderFailing(registry, surface, "--strict", "--size", "16x16", "-o", output);
        var limit = RunRenderFailing(registry, model, "--max-triangles", "1", "-o", output);

        // Assert: the same codes as the library, with its location and detail code
        Assert.Equal((ErrorCodes.ScadError, "scad-syntax-error", 2), (syntax.Code, syntax.DetailCode, syntax.Location?.Line));
        Assert.Equal((ErrorCodes.UnsupportedFeature, 1), (strict.Code, strict.Location?.Line));
        Assert.NotNull(strict.DetailCode);
        Assert.Equal(ErrorCodes.BudgetExceeded, limit.Code);

        // Assert: the hints in the words of the command line
        Assert.Contains("Without --strict", strict.Hint, StringComparison.Ordinal);
        Assert.Contains("Raise --max-triangles", limit.Hint, StringComparison.Ordinal);
        Assert.All(new[] { syntax, strict, limit }, ex => Assert.Equal("render", ex.Command));
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the render command run through the registry reports a picture larger than the largest image with the
    ///     size hints of the library in the words of the command line: the scale that fits when the size follows the
    ///     natural size, and a smaller <c>--scale</c>, <c>--width</c>, or <c>--height</c> when a size option was given.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_RenderOfOversizePictures_NamesTheSizeOptions()
    {
        // Arrange: a picture wider than the largest image, and a tall one made too tall by the width asked for
        using var directory = new TempDirectory();
        var wide = FixtureFactory.WriteText(directory.Path, "wide.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20000\" height=\"10\"/>");
        var tall = FixtureFactory.WriteText(directory.Path, "tall.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"100\"/>");
        var output = directory.Combine("o.png");
        var registry = CommandRegistry.CreateDefault();

        // Act
        var natural = RunRenderFailing(registry, wide, "-o", output);
        var sized = RunRenderFailing(registry, tall, "--width", "4000", "-o", output);

        // Assert: the size limit, with the hints of the library in the words of the command line
        Assert.Equal(ErrorCodes.SizeLimit, natural.Code);
        Assert.Equal("Use --scale 0.4 or less, or a smaller --width or --height.", natural.Hint);
        Assert.Equal(ErrorCodes.SizeLimit, sized.Code);
        Assert.Equal("Use a smaller --scale, --width, or --height.", sized.Hint);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Runs the render command through a registry and returns the error it fails with.
    /// </summary>
    /// <param name="registry">The registry.</param>
    /// <param name="args">The arguments of the render command.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunRenderFailing(CommandRegistry registry, params string[] args)
    {
        using var context = Context.Create(["render", .. args]);
        return Assert.Throws<ToolException>(() => ToolHost.Capture(() => registry.Run(context)));
    }

    /// <summary>
    ///     Test that the hints of the library are worded with the options of the tool: strict mode, the triangle limit, and
    ///     a smaller size; a hint without such a phrase and no hint are kept as they are.
    /// </summary>
    /// <param name="library">The hint of the library.</param>
    /// <param name="commandLine">The hint the tool reports.</param>
    [Theory]
    [InlineData("Without strict mode the image is left out.", "Without --strict the image is left out.")]
    [InlineData(
        "Raise ScadViewOptions.MaxTriangles (the default is 2000000) or simplify the model.",
        "Raise --max-triangles (the default is 2000000) or simplify the model.")]
    [InlineData("Use a smaller scale, width, or height.", "Use a smaller --scale, --width, or --height.")]
    [InlineData("Use a scale of 0.4 or less, or a smaller width or height.", "Use --scale 0.4 or less, or a smaller --width or --height.")]
    [InlineData("Check the path.", "Check the path.")]
    [InlineData(null, null)]
    public void CommandRegistry_CommandLineHint_LibraryPhrases_NameTheOptions(string? library, string? commandLine)
    {
        // Act
        var hint = CommandRegistry.CommandLineHint(library);

        // Assert
        Assert.Equal(commandLine, hint);
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

    /// <summary>
    ///     Creates a registry with one aliased command, for the help-subject tests.
    /// </summary>
    /// <returns>The registry.</returns>
    private static CommandRegistry CreateHelpRegistry()
    {
        var registry = new CommandRegistry();
        registry.Register(new TestCommand("render", null, "convert"));
        return registry;
    }

    /// <summary>
    ///     Runs the help command with arguments and captures its standard output.
    /// </summary>
    /// <param name="registry">The registry.</param>
    /// <param name="json">Whether the global <c>--json</c> option is on.</param>
    /// <param name="args">The arguments after <c>help</c>.</param>
    /// <returns>The captured standard output.</returns>
    private static string RunHelp(CommandRegistry registry, bool json, params string[] args)
    {
        string[] arguments = json ? ["--json", "help", .. args] : ["help", .. args];
        using var context = Context.Create(arguments);
        var (stdOut, _) = ToolHost.Capture(() => registry.Run(context));
        return stdOut;
    }

    /// <summary>
    ///     Runs the help command with arguments that HelpCards refuses, and captures what it writes.
    /// </summary>
    /// <param name="registry">The registry.</param>
    /// <param name="json">Whether the global <c>--json</c> option is on.</param>
    /// <param name="args">The arguments after <c>help</c>.</param>
    /// <returns>The captured standard output and standard error, and the exit code of the context.</returns>
    private static (string StdOut, string StdErr, int ExitCode) RunHelpFailed(CommandRegistry registry, bool json, params string[] args)
    {
        string[] arguments = json ? ["--json", "help", .. args] : ["help", .. args];
        using var context = Context.Create(arguments);
        var (stdOut, stdErr) = ToolHost.Capture(() => registry.Run(context));
        return (stdOut, stdErr, context.ExitCode);
    }

    /// <summary>
    ///     Runs the help command with arguments and returns the error it fails with.
    /// </summary>
    /// <param name="registry">The registry.</param>
    /// <param name="args">The arguments after <c>help</c>.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunHelpFailing(CommandRegistry registry, params string[] args)
    {
        using var context = Context.Create(["help", .. args]);
        return Assert.Throws<ToolException>(() => registry.Run(context));
    }

    /// <summary>
    ///     Test that help for a topic name returns its root card, with the cards below it and its See also links.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Topic_ReturnsTopicHelp()
    {
        // Arrange: a registry with one command
        var registry = CreateHelpRegistry();

        // Act: render the help of the svg topic
        var help = registry.RenderHelp("svg");

        // Assert: it is the root card, not a command help
        Assert.Equal(HelpView.RenderText(HelpView.Get("svg")), help);
        Assert.StartsWith("# SVG drawings (.svg)", help);
        Assert.Contains("## Cards", help);
        Assert.Contains("- canvasnet help svg embedding", help);
        Assert.Contains("## See also", help);
    }

    /// <summary>
    ///     Test that a help path of several words prints the card it names, by names and aliases, as text and as JSON.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpCardPath_PrintsTheCard()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();
        var card = HelpView.Get("mermaid/gantt");

        // Act: print the card by its path, by aliases, and as JSON
        var text = RunHelp(registry, false, "mermaid", "gantt");
        var aliased = RunHelp(registry, false, "MMD", "gantt");
        var slashed = RunHelp(registry, false, "mermaid/gantt");
        var json = RunHelp(registry, true, "mermaid", "gantt");

        // Assert: the card text and document
        Assert.Equal(HelpView.RenderText(card) + "\n", text.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(text, aliased);
        Assert.Equal(text, slashed);
        Assert.Equal(HelpView.RenderJson(card), json.TrimEnd());
        Assert.Equal(registry.RenderHelp(["mermaid", "gantt"]), HelpView.RenderText(card));
    }

    /// <summary>
    ///     Test that an unknown word below a card is a usage error that lists the cards at that level: on standard error
    ///     as <c>Error: usage-error:</c> and <c>Hint:</c> lines with exit code 1, and with <c>--json</c> also as the
    ///     failure document of the tool on standard output.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpUnknownCard_ListsChildren()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act: ask for a mistyped card, as text and as JSON
        var (textOut, textErr, textExit) = RunHelpFailed(registry, false, "mermaid", "giant");
        var (jsonOut, jsonErr, jsonExit) = RunHelpFailed(registry, true, "mermaid", "giant");

        // Assert: the error lines suggest the card and list the cards under mermaid, and the exit code is 1
        Assert.Equal(1, textExit);
        Assert.Empty(textOut);
        var lines = textErr.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        Assert.Equal("Error: usage-error: Unknown help card 'giant' under 'mermaid'.", lines[0]);
        Assert.StartsWith("Hint: Did you mean 'gantt'? Cards under 'mermaid': flowchart,", lines[1]);
        Assert.Equal(2, lines.Length);

        // Assert: the JSON failure document has the tool's shape: no kind, and the error object
        Assert.Equal(1, jsonExit);
        Assert.Equal(textErr, jsonErr);
        using var document = System.Text.Json.JsonDocument.Parse(jsonOut);
        var root = document.RootElement;
        Assert.Equal(["schemaVersion", "ok", "command", "error"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("help", root.GetProperty("command").GetString());
        var error = root.GetProperty("error");
        Assert.Equal(["code", "message", "hint"], error.EnumerateObject().Select(p => p.Name));
        Assert.Equal(ErrorCodes.UsageError, error.GetProperty("code").GetString());
        Assert.Equal("Unknown help card 'giant' under 'mermaid'.", error.GetProperty("message").GetString());
        Assert.Equal(lines[1]["Hint: ".Length..], error.GetProperty("hint").GetString());

        // Assert: the convenience method throws the same error
        var exception = Assert.Throws<ToolException>(() => registry.RenderHelp(["mermaid", "giant"]));
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("Unknown help card 'giant' under 'mermaid'.", exception.Message);
        Assert.Equal("help", exception.Command);
    }

    /// <summary>
    ///     Test that a word after a command, <c>help</c>, or <c>topics</c> is a usage error, because they have no cards
    ///     below them.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpPathAfterCommand_ThrowsUsageError()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act
        var command = RunHelpFailing(registry, "render", "gantt");
        var topics = RunHelpFailing(registry, "topics", "mermaid");
        var help = RunHelpFailing(registry, "help", "help", "mermaid");

        // Assert: each is a usage error that says so
        Assert.Equal(ErrorCodes.UsageError, command.Code);
        Assert.Equal("'render' has no help cards below it.", command.Message);
        Assert.Equal("'topics' has no help cards below it.", topics.Message);
        Assert.Equal("'help' has no help cards below it.", help.Message);
    }

    /// <summary>
    ///     Test that a leading <c>help</c> word is ignored, so <c>help help mermaid</c> prints the mermaid card.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_LeadingHelpWord_IsIgnored()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act / Assert
        Assert.Equal(registry.RenderHelp("mermaid"), registry.RenderHelp(["help", "mermaid"]));
    }

    /// <summary>
    ///     Test that help for a topic alias equals help for the canonical topic.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_TopicAlias_EqualsCanonical()
    {
        // Arrange: a registry with one command
        var registry = CreateHelpRegistry();

        // Act / Assert: aliases, in any case, give the canonical text and document
        Assert.Equal(registry.RenderHelp("vega-lite"), registry.RenderHelp("VL"));
        Assert.Equal(registry.RenderHelp("pptx"), registry.RenderHelp("powerpoint"));
        Assert.Equal(registry.RenderHelp("raster", json: true), registry.RenderHelp("jpg", json: true));
    }

    /// <summary>
    ///     Test that the topics subject lists every help card, depth first, as text and as JSON.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Topics_ListsEveryCard()
    {
        // Arrange: a registry with one aliased command
        var registry = CreateHelpRegistry();

        // Act: render help for 'topics'
        var help = registry.RenderHelp("topics");
        var json = registry.RenderHelp("TOPICS", json: true);

        // Assert: it is the tree, not the index
        Assert.Equal(HelpTopics.RenderTopics(false), help);
        Assert.Equal(HelpTopics.RenderTopics(true), json);
        Assert.NotEqual(registry.RenderHelp(), help);
        Assert.Contains("canvasnet help mermaid gantt settings", help);
        Assert.All(HelpView.All, card => Assert.Contains(card.Command, help));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(["schemaVersion", "ok", "command", "kind"], document.RootElement.EnumerateObject().Select(p => p.Name).Take(4));
        Assert.Equal("topics", document.RootElement.GetProperty("kind").GetString());
        var cardKeys = document.RootElement.GetProperty("cards")[0].EnumerateObject().Select(p => p.Name).ToList();
        Assert.All(["id", "aliases", "tags", "summary", "command"], key => Assert.Contains(key, cardKeys));
    }

    /// <summary>
    ///     Test that the top-level help lists the usage, the commands, the options, the help options, and the help topics
    ///     with the command that prints each, and how to read a card.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_TopLevel_ListsHelpTopics()
    {
        // Arrange: a registry with one command
        var registry = CreateHelpRegistry();

        // Act: render the top-level help
        var help = registry.RenderHelp();
        var flat = HelpCardText.Flatten(help.Replace("\r\n", "\n", StringComparison.Ordinal));

        // Assert: the usage line of help, the sections, every root, and the pointers
        Assert.Contains("canvasnet help [<command> | <topic> [<card> ...] | topics] [<help options>] [--json]", help);
        Assert.Contains("\nHelp options:\n", help);
        Assert.Contains("--example <name>", help);
        Assert.Contains("--search <text>", help);
        Assert.Contains("\nHelp topics:\n", help);
        Assert.All(HelpTopics.TopicNames, name => Assert.Contains($"canvasnet help {name} ", help));
        Assert.Contains("'canvasnet help topics' to list every help card", flat);
        Assert.Contains("'canvasnet help style'", flat);
        Assert.Contains("'canvasnet help composition'", flat);
        Assert.EndsWith("Run 'canvasnet help' for topics and examples.", help);
    }

    /// <summary>
    ///     Test that command help as JSON carries the usage and the options of the command.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Json_Command_HasOptions()
    {
        // Arrange: a registry with an aliased command
        var registry = CreateHelpRegistry();

        // Act: render the help of the command by its alias as JSON
        using var document = System.Text.Json.JsonDocument.Parse(registry.RenderHelp("convert", json: true));
        var root = document.RootElement;

        // Assert: the command document has the canonical name, aliases, usage, and both option lists
        Assert.Equal("command", root.GetProperty("kind").GetString());
        Assert.Equal("render", root.GetProperty("topic").GetString());
        Assert.Equal(["convert"], root.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("Summary of render", root.GetProperty("summary").GetString());
        Assert.Equal("canvasnet render <input> [options]", root.GetProperty("usage").GetString());
        var options = root.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal(["--flag", "--value"], options.Select(o => o.GetProperty("name").GetString()));
        Assert.Equal(["-f"], options[0].GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.False(options[0].GetProperty("takesValue").GetBoolean());
        Assert.False(options[0].TryGetProperty("valueName", out _));
        Assert.True(options[1].GetProperty("takesValue").GetBoolean());
        Assert.Equal("text", options[1].GetProperty("valueName").GetString());
        Assert.Contains(root.GetProperty("globalOptions").EnumerateArray(), o => o.GetProperty("name").GetString() == "--json");
    }

    /// <summary>
    ///     Test that the usage of every real command in JSON help uses only line feeds, so the document is the same on every
    ///     platform.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Json_UsageUsesLineFeedsOnly()
    {
        // Arrange
        var registry = CommandRegistry.CreateDefault();

        // Act / Assert: no command's usage carries a carriage return
        foreach (var name in new[] { "render", "info", "compare", "montage", "inspect" })
        {
            using var document = System.Text.Json.JsonDocument.Parse(registry.RenderHelp(name, json: true));
            var usage = document.RootElement.GetProperty("usage").GetString()!;
            Assert.DoesNotContain('\r', usage);
            Assert.Contains('\n', usage);
        }
    }

    /// <summary>
    ///     Test that the index as JSON lists the usage, the commands, the topics, and the global options in the tool's
    ///     envelope and order, followed by the fields HelpCards adds.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Json_Index_ListsCommandsAndTopics()
    {
        // Arrange: a registry with an aliased command
        var registry = CreateHelpRegistry();

        // Act: render the index as JSON, for no subject and for 'help'
        var json = registry.RenderHelp(json: true);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        // Assert: the index has the usage, the commands, all topics with aliases, and the global options, first
        Assert.Equal(json, registry.RenderHelp("help", json: true));
        Assert.Equal(
            ["schemaVersion", "ok", "command", "kind", "usage", "commands", "topics", "globalOptions"],
            root.EnumerateObject().Select(p => p.Name).Take(8));
        Assert.Equal("index", root.GetProperty("kind").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.String, root.GetProperty("usage").ValueKind);
        Assert.Contains("canvasnet help", root.GetProperty("usage").GetString());
        Assert.Equal(["render"], root.GetProperty("commands").EnumerateArray().Select(c => c.GetProperty("name").GetString()));
        var topics = root.GetProperty("topics").EnumerateArray().ToList();
        Assert.Equal(HelpTopics.TopicNames, topics.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal(["openscad"], topics[0].GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.False(string.IsNullOrEmpty(topics[0].GetProperty("summary").GetString()));
        Assert.NotEmpty(root.GetProperty("globalOptions").EnumerateArray());
    }

    /// <summary>
    ///     Test that <c>help &lt;topic&gt; --example</c> writes only the source of the example, and the example document
    ///     with <c>--json</c>.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExample_WritesOnlyTheSource()
    {
        // Arrange: a registry, and the expected source
        var registry = CreateHelpRegistry();
        var topic = HelpView.Get("svg");

        // Act: print the shapes example as text and as JSON
        var text = RunHelp(registry, false, "svg", "--example", "shapes");
        var json = RunHelp(registry, true, "svg", "--example", "shapes");

        // Assert: the text is the source and one line end; the JSON is the example document, whose id is the card that
        // was asked for and whose example names the card that holds it
        Assert.Equal(HelpView.RenderExample(topic, "shapes") + "\n", text.Replace("\r\n", "\n", StringComparison.Ordinal));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("example", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal("svg", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("svg/supported", document.RootElement.GetProperty("example").GetProperty("card").GetString());
        Assert.Equal(HelpView.RenderExample(topic, "shapes"), document.RootElement.GetProperty("example").GetProperty("content").GetString());
    }

    /// <summary>
    ///     Test that the document of an example found below the requested card keeps the requested card as its
    ///     <c>id</c>, and differs from the document asked for at the card that holds the example only in that value.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_ExampleBelowTheCard_KeepsTheRequestedCardAsId()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act: ask for the shapes example at the topic and at the card that holds it
        var viaTopic = registry.RenderHelp(["svg", "--example", "shapes"], json: true);
        var viaOwner = registry.RenderHelp(["svg", "supported", "--example", "shapes"], json: true);

        // Assert: the keys of the document in order, the two ids, the card that holds the example, and the same bytes
        // apart from the id
        using var topicDocument = System.Text.Json.JsonDocument.Parse(viaTopic);
        using var ownerDocument = System.Text.Json.JsonDocument.Parse(viaOwner);
        Assert.Equal(
            ["schemaVersion", "ok", "command", "kind", "id", "example"],
            topicDocument.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("svg", topicDocument.RootElement.GetProperty("id").GetString());
        Assert.Equal("svg/supported", ownerDocument.RootElement.GetProperty("id").GetString());
        Assert.Equal("svg/supported", topicDocument.RootElement.GetProperty("example").GetProperty("card").GetString());
        Assert.Equal("svg/supported", ownerDocument.RootElement.GetProperty("example").GetProperty("card").GetString());
        Assert.Equal(viaOwner.Replace("\"id\":\"svg/supported\"", "\"id\":\"svg\"", StringComparison.Ordinal), viaTopic);
    }

    /// <summary>
    ///     Test that the value of a help option may follow an equals sign, as for every other command, but not after
    ///     <c>--</c>.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_HelpOptionValueAfterEqualsSign_IsAccepted()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act / Assert: each form with an equals sign gives the same answer as the form with two words
        Assert.Equal(
            registry.RenderHelp(["svg", "--example", "shapes"]),
            registry.RenderHelp(["svg", "--example=shapes"]));
        Assert.Equal(
            registry.RenderHelp(["svg", "--example", "shapes"], json: true),
            registry.RenderHelp(["svg", "--example=shapes"], json: true));
        Assert.Equal(
            RunHelp(registry, true, "svg", "--example", "shapes"),
            RunHelp(registry, true, "svg", "--example=shapes"));
        Assert.Equal(
            registry.RenderHelp(["--search", "gantt"], json: true),
            registry.RenderHelp(["--search=gantt"], json: true));
        Assert.Equal(
            registry.RenderHelp(["--tag", "mathml"], json: true),
            registry.RenderHelp(["--tag=mathml"], json: true));
        Assert.Equal(
            registry.RenderHelp(["mermaid"], json: true),
            registry.RenderHelp(["mermaid", "--format=json"]));
        Assert.Equal(
            registry.RenderHelp(["mermaid", "--width", "60"]),
            registry.RenderHelp(["mermaid", "--width=60"]));

        // Act / Assert: after '--' the word is a path word, and so an unknown subject
        var exception = Assert.Throws<ToolException>(() => registry.RenderHelp(["--", "--example=shapes"]));
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("'--example=shapes'", exception.Message);
    }

    /// <summary>
    ///     Test that <c>help &lt;card&gt; --examples</c> prints every example of the card in full and lists the cards
    ///     below it that have examples, as text and as JSON.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExamples_PrintsTheCardsExamplesInFull()
    {
        // Arrange: a registry, the svg topic, and its flatten card
        var registry = CreateHelpRegistry();
        var topic = HelpView.Get("svg");
        var flatten = HelpView.Get("svg/flatten");

        // Act: print the examples of the topic and of one card
        var text = RunHelp(registry, false, "svg", "--examples").Replace("\r\n", "\n", StringComparison.Ordinal);
        var json = RunHelp(registry, true, "svg", "--examples");
        var one = RunHelp(registry, false, "svg", "flatten", "--examples").Replace("\r\n", "\n", StringComparison.Ordinal);

        // Assert: the card prints its own examples in full; the topic points at the cards below with examples
        Assert.All(flatten.Examples, example => Assert.Contains(example.Content, one));
        Assert.All(HelpView.CardsBelowWithExamples(topic), card => Assert.Contains($"{card.Command} --examples", text));
        Assert.DoesNotContain(flatten.Examples[0].Content, text);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("examples", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(
            HelpView.CardsBelowWithExamples(topic).Select(c => c.Id),
            document.RootElement.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetString()));
    }

    /// <summary>
    ///     Test that <c>--examples</c> on a card without examples, of its own or below it, succeeds with empty lists.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_ExamplesOnCardWithoutExamples_GivesEmptyLists()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act
        var text = registry.RenderHelp(["mermaid", "differences", "--examples"]);
        using var document = System.Text.Json.JsonDocument.Parse(registry.RenderHelp(["mermaid", "differences", "--examples"], json: true));

        // Assert
        Assert.Contains("has no examples of its own", text);
        Assert.Empty(document.RootElement.GetProperty("examples").EnumerateArray());
        Assert.Empty(document.RootElement.GetProperty("cards").EnumerateArray());
    }

    /// <summary>
    ///     Test that an unknown example name is a usage error that lists the valid names.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExampleUnknown_ThrowsUsageErrorWithNames()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act: ask for an example that does not exist
        var exception = Assert.Throws<ToolException>(() => registry.RenderHelp(["svg", "--example", "nothing"]));

        // Assert: a usage error naming the topic, the example, and the valid examples
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("The help card 'svg' has no example 'nothing'.", exception.Message);
        Assert.Contains("shapes", exception.Hint);
    }

    /// <summary>
    ///     Test that the wrong uses of the help options are usage errors: examples of a command or of the tree, an option
    ///     that needs a card without one, two modes at once, an unknown option, an empty value after an equals sign, and a
    ///     flag with a value.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_WrongHelpOptions_AreUsageErrors()
    {
        // Arrange: a registry with a command
        var registry = CreateHelpRegistry();

        // Act / Assert: each is a usage error of the help command
        foreach (var arguments in new[]
                 {
                     new[] { "render", "--example", "x" },
                     ["render", "--examples"],
                     ["topics", "--examples"],
                     ["--example", "x"],
                     ["--examples"],
                     ["svg", "--example", "shapes", "--examples"],
                     ["svg", "extra"],
                     ["--bogus"],
                     ["svg", "--example="],
                     ["svg", "--examples=x"],
                     ["mermaid", "--lang", "de"]
                 })
        {
            var exception = Assert.Throws<ToolException>(() => registry.RenderHelp(arguments));
            Assert.Equal(ErrorCodes.UsageError, exception.Code);
            Assert.Equal("help", exception.Command);
        }
    }

    /// <summary>
    ///     Test that the argument errors that HelpCards reports without a hint (an option without its value,
    ///     <c>--example</c> or <c>--examples</c> without a card path, and two modes together) get the tool's hint, in
    ///     text, with <c>--json</c>, with <c>--format json</c>, and from <c>RenderHelp</c>.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpArgumentErrorsWithoutAHint_GetTheHelpOptionsHint()
    {
        // Arrange: a registry with a command, and the hint of the tool
        var registry = CreateHelpRegistry();
        const string hint = "Run 'canvasnet help' for the help options, or 'canvasnet help topics' to list the help cards.";

        foreach (var (arguments, message) in new[]
                 {
                     (new[] { "--example", "x" }, "option '--example' needs a card path."),
                     (["--examples"], "option '--examples' needs a card path."),
                     (["help", "--examples"], "option '--examples' needs a card path."),
                     (["svg", "--examples", "--example", "x"], "options '--examples' and '--example' cannot be combined."),
                     (["--search", "gantt", "--examples"], "options '--examples' and '--search' cannot be combined."),
                     (["svg", "--example"], "option '--example' needs a value."),
                     (["--tag"], "option '--tag' needs a value.")
                 })
        {
            // Act: as text, with the global --json, and with '--format json' before the options (after a leading 'help',
            // which names the index again only as the first word), where a trailing option would read it as its value
            string[] formatArguments = arguments[0] == "help"
                ? ["help", "--format", "json", .. arguments[1..]]
                : ["--format", "json", .. arguments];
            var (textOut, textErr, textExit) = RunHelpFailed(registry, false, arguments);
            var (jsonOut, jsonErr, jsonExit) = RunHelpFailed(registry, true, arguments);
            var (formatOut, _, formatExit) = RunHelpFailed(registry, false, formatArguments);

            // Assert: the Error and Hint lines, exit code 1, and no text output
            Assert.Equal(1, textExit);
            Assert.Empty(textOut);
            var lines = textErr.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            Assert.Equal([$"Error: usage-error: {message}", $"Hint: {hint}"], lines);

            // Assert: the failure document has the hint, the same for --json and '--format json'
            Assert.Equal(1, jsonExit);
            Assert.Equal(1, formatExit);
            Assert.Equal(textErr, jsonErr);
            Assert.Equal(jsonOut, formatOut);
            using var document = System.Text.Json.JsonDocument.Parse(jsonOut);
            var error = document.RootElement.GetProperty("error");
            Assert.Equal(["code", "message", "hint"], error.EnumerateObject().Select(p => p.Name));
            Assert.Equal(ErrorCodes.UsageError, error.GetProperty("code").GetString());
            Assert.Equal(message, error.GetProperty("message").GetString());
            Assert.Equal(hint, error.GetProperty("hint").GetString());

            // Assert: the convenience method throws the same error with the hint
            var exception = Assert.Throws<ToolException>(() => registry.RenderHelp(arguments));
            Assert.Equal(message, exception.Message);
            Assert.Equal(hint, exception.Hint);
        }
    }

    /// <summary>
    ///     Test that <c>--format json</c>, with the value in any case, gives the same documents as <c>--json</c> for the
    ///     subjects the tool answers itself (a command and <c>topics</c>) and for a card.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpFormatJsonInAnyCase_GivesTheToolsOwnDocuments()
    {
        // Arrange: a registry with a command
        var registry = CreateHelpRegistry();

        // Act: ask with '--format' in other cases and with the global --json
        var command = RunHelp(registry, false, "render", "--format", "JSON");
        var topics = RunHelp(registry, false, "topics", "--format", "Json");
        var card = RunHelp(registry, false, "svg", "--format", "JSON");

        // Assert: each is the document of --json, with the kind of the tool
        Assert.Equal(RunHelp(registry, true, "render"), command);
        Assert.Equal(RunHelp(registry, true, "topics"), topics);
        Assert.Equal(RunHelp(registry, true, "svg"), card);
        using var commandDocument = System.Text.Json.JsonDocument.Parse(command);
        Assert.Equal("command", commandDocument.RootElement.GetProperty("kind").GetString());
        Assert.Equal("render", commandDocument.RootElement.GetProperty("topic").GetString());
        using var topicsDocument = System.Text.Json.JsonDocument.Parse(topics);
        Assert.Equal("topics", topicsDocument.RootElement.GetProperty("kind").GetString());
    }

    /// <summary>
    ///     Test that a failure of a subject the tool answers itself, asked with <c>--format json</c>, writes the
    ///     <c>Error:</c> and <c>Hint:</c> lines and the failure document once, with exit code 1, while the global
    ///     <c>--json</c> path still leaves the document to <c>Program</c>.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpFormatJsonFailureOfOwnSubject_WritesTheFailureDocument()
    {
        // Arrange: a registry with a command
        var registry = CreateHelpRegistry();

        foreach (var path in new[] { new[] { "nosuch" }, ["render", "extra"] })
        {
            // Act: ask with '--format json', and without it for the expected error
            var (stdOut, stdErr, exitCode) = RunHelpFailed(registry, false, [.. path, "--format", "json"]);
            var expected = RunHelpFailing(registry, path);

            // Assert: the error lines, exit code 1, and the failure document of the same error
            Assert.Equal(1, exitCode);
            var lines = stdErr.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            Assert.Equal([$"Error: usage-error: {expected.Message}", $"Hint: {expected.Hint}"], lines);
            Assert.Equal(ResultWriter.Failure("help", expected), stdOut.TrimEnd());
            using var document = System.Text.Json.JsonDocument.Parse(stdOut);
            var root = document.RootElement;
            Assert.Equal(["schemaVersion", "ok", "command", "error"], root.EnumerateObject().Select(p => p.Name));
            Assert.Equal(["code", "message", "hint"], root.GetProperty("error").EnumerateObject().Select(p => p.Name));
        }

        Assert.Equal("Unknown help subject 'nosuch'.", RunHelpFailing(registry, "nosuch").Message);
        Assert.Equal("'render' has no help cards below it.", RunHelpFailing(registry, "render", "extra").Message);

        // Assert: with the global --json the registry still throws and writes nothing itself
        using var context = Context.Create(["--json", "help", "nosuch"]);
        var (jsonOut, _) = ToolHost.Capture(() =>
        {
            Assert.Throws<ToolException>(() => registry.Run(context));
        });
        Assert.Empty(jsonOut);

        // Assert: the convenience method throws the error
        var exception = Assert.Throws<ToolException>(() => registry.RenderHelp(["nosuch", "--format", "json"]));
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("help", exception.Command);
        Assert.Equal("Unknown help subject 'nosuch'.", exception.Message);
    }

    /// <summary>
    ///     Test that the examples of a command, an alias, or <c>topics</c> are a usage error that says only help cards
    ///     have examples, as text and as JSON, while an option error still comes first.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExamplesOfOwnSubject_SaysOnlyHelpCardsHaveExamples()
    {
        // Arrange: a registry with an aliased command
        var registry = CreateHelpRegistry();

        foreach (var (arguments, subject) in new[]
                 {
                     (new[] { "render", "--examples" }, "render"),
                     (["convert", "--example", "x"], "convert"),
                     (["topics", "--examples"], "topics")
                 })
        {
            // Act: ask as text, with the global --json, and with '--format json'
            var (textOut, textErr, textExit) = RunHelpFailed(registry, false, arguments);
            var (jsonOut, jsonErr, jsonExit) = RunHelpFailed(registry, true, arguments);
            var (formatOut, _, formatExit) = RunHelpFailed(registry, false, [.. arguments, "--format", "json"]);

            // Assert: the tool's message and hint, exit code 1, and no text output
            var message = $"'{subject}' has no examples; only help cards do.";
            var hint = $"Run 'canvasnet help {subject}' for its usage, or 'canvasnet help topics' to list the help cards.";
            Assert.Equal(1, textExit);
            Assert.Empty(textOut);
            var lines = textErr.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            Assert.Equal([$"Error: usage-error: {message}", $"Hint: {hint}"], lines);

            // Assert: the failure document carries the same error, identical for --json and '--format json'
            Assert.Equal(1, jsonExit);
            Assert.Equal(1, formatExit);
            Assert.Equal(textErr, jsonErr);
            Assert.Equal(jsonOut, formatOut);
            using var document = System.Text.Json.JsonDocument.Parse(jsonOut);
            var error = document.RootElement.GetProperty("error");
            Assert.Equal(ErrorCodes.UsageError, error.GetProperty("code").GetString());
            Assert.Equal(message, error.GetProperty("message").GetString());
            Assert.Equal(hint, error.GetProperty("hint").GetString());
        }

        // Assert: the convenience method throws the message
        var exception = Assert.Throws<ToolException>(() => registry.RenderHelp(["render", "--examples"]));
        Assert.Equal("'render' has no examples; only help cards do.", exception.Message);
        Assert.Equal("help", exception.Command);

        // Assert: option errors keep their precedence
        Assert.Equal(
            "Unknown option '--bogus'.",
            Assert.Throws<ToolException>(() => registry.RenderHelp(["render", "--examples", "--bogus"])).Message);
        Assert.NotEqual(
            "'render' has no examples; only help cards do.",
            Assert.Throws<ToolException>(() => registry.RenderHelp(["render", "--example", "x", "--examples"])).Message);
    }

    /// <summary>
    ///     Test that the search, tag, and whole-tree options of HelpCards answer in the tool's envelope.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_SearchTagAndAll_AnswerInTheToolEnvelope()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act
        using var search = System.Text.Json.JsonDocument.Parse(registry.RenderHelp(["--search", "gantt"], json: true));
        using var tag = System.Text.Json.JsonDocument.Parse(registry.RenderHelp(["--tag", "mathml"], json: true));
        using var all = System.Text.Json.JsonDocument.Parse(registry.RenderHelp(["--all"], json: true));
        var text = registry.RenderHelp(["--search", "gantt"]);

        // Assert: the envelope and kind of each, and the cards they find
        foreach (var (document, kind) in new[] { (search, "search"), (tag, "cards"), (all, "index") })
        {
            var root = document.RootElement;
            Assert.Equal(["schemaVersion", "ok", "command", "kind"], root.EnumerateObject().Select(p => p.Name).Take(4));
            Assert.Equal("help", root.GetProperty("command").GetString());
            Assert.Equal(kind, root.GetProperty("kind").GetString());
        }

        Assert.Contains(search.RootElement.GetProperty("results").EnumerateArray(), r => r.GetProperty("id").GetString() == "mermaid/gantt");
        Assert.All(tag.RootElement.GetProperty("cards").EnumerateArray(), c => Assert.Contains("mathml", c.GetProperty("tags").EnumerateArray().Select(t => t.GetString())));
        Assert.Equal(HelpView.All.Select(c => c.Id), all.RootElement.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetString()));
        Assert.Contains("canvasnet help mermaid gantt", text);
    }

    /// <summary>
    ///     Test that an unknown help subject near a name suggests it and always lists the valid subjects.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpUnknownSubject_SuggestsClosestAndListsValidSubjects()
    {
        // Arrange: a registry with one command
        var registry = CreateHelpRegistry();

        // Act: ask for a mistyped topic
        var exception = RunHelpFailing(registry, "scadd");

        // Assert: the message names the subject; the hint suggests the topic and lists the subjects
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("Unknown help subject 'scadd'.", exception.Message);
        Assert.Equal("help", exception.Command);
        Assert.Contains("Did you mean 'scad'?", exception.Hint);
        Assert.Contains("Valid subjects: render, scad, vega-lite,", exception.Hint);
        Assert.Contains("topics, help.", exception.Hint);
    }

    /// <summary>
    ///     Test that an unknown help subject far from every name has no suggestion but still lists the valid subjects.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpUnknownSubjectFarFromAll_HasNoSuggestionButListsSubjects()
    {
        // Arrange: a registry with one command
        var registry = CreateHelpRegistry();

        // Act: ask for an unrelated subject
        var exception = RunHelpFailing(registry, "zzzzzzzz");

        // Assert: no suggestion, but the valid subjects
        Assert.DoesNotContain("Did you mean", exception.Hint);
        Assert.StartsWith("Valid subjects:", exception.Hint);
    }

    /// <summary>
    ///     Test that the text error of HelpCards splits into its message and hint, and that other text is the message.
    /// </summary>
    [Fact]
    public void CommandRegistry_SplitTextError_DocumentedFormat_GivesMessageAndHint()
    {
        // Act / Assert
        Assert.Equal(("Bad.", "Try this."), CommandRegistry.SplitTextError("Error: Bad.\nTry this.\n"));
        Assert.Equal(("Bad.", (string?)null), CommandRegistry.SplitTextError("Error: Bad.\r\n"));
        Assert.Equal(("Something else", (string?)null), CommandRegistry.SplitTextError("Something else"));
    }
    /// <summary>
    ///     Test that a topic name typed as a command points at <c>help</c>.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_TopicNameAsCommand_HintsAtHelp()
    {
        // Arrange: a registry and a context that runs the topic name as a command
        var registry = CreateHelpRegistry();
        using var context = Context.Create(["openscad"]);

        // Act: run it
        var exception = Assert.Throws<ToolException>(() => registry.Run(context));

        // Assert: still the unknown-command error, with a hint to the topic
        Assert.Equal("Unknown command 'openscad'.", exception.Message);
        Assert.Contains("'openscad' is a help topic: run 'canvasnet help scad'", exception.Hint);
    }
}
