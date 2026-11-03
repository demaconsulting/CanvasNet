// cspell:ignore canvasnet rendr convrt scadd
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
    ///     Test that help for a topic name returns the topic, with its supported features and examples.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Topic_ReturnsTopicHelp()
    {
        // Arrange: a registry with one command
        var registry = CreateHelpRegistry();

        // Act: render the help of the svg topic
        var help = registry.RenderHelp("svg");

        // Assert: it is the topic, not a command help
        Assert.StartsWith("Topic: SVG drawings (.svg)", help);
        Assert.Contains("Supported:", help);
        Assert.Contains("Unsupported:", help);
        Assert.Contains("Examples:", help);
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
    ///     Test that the topics subject lists the commands and the topics, like the top-level help.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Topics_ListsCommandsAndTopics()
    {
        // Arrange: a registry with one aliased command
        var registry = CreateHelpRegistry();

        // Act: render help for 'topics'
        var help = registry.RenderHelp("topics");

        // Assert: it is the index
        Assert.Equal(registry.RenderHelp(), help);
        Assert.Contains("render, convert", help);
        Assert.All(HelpTopics.TopicNames, name => Assert.Contains($"  {name}", help));
    }

    /// <summary>
    ///     Test that the top-level help lists the help topics, with their aliases, and how to read them.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_TopLevel_ListsHelpTopics()
    {
        // Arrange: a registry with one command
        var registry = CreateHelpRegistry();

        // Act: render the top-level help
        var help = registry.RenderHelp();

        // Assert: the usage line of help, the topic section, an alias, and the pointer
        Assert.Contains("canvasnet help [<command>|<topic>] [--example <name> | --examples] [--json]", help);
        Assert.Contains("Help topics:", help);
        Assert.Contains("scad = openscad", help);
        Assert.Contains("canvasnet help <topic>", help);
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
    ///     Test that the index as JSON lists the commands and the topics.
    /// </summary>
    [Fact]
    public void CommandRegistry_RenderHelp_Json_Index_ListsCommandsAndTopics()
    {
        // Arrange: a registry with an aliased command
        var registry = CreateHelpRegistry();

        // Act: render the index as JSON, for no subject and for 'topics'
        var json = registry.RenderHelp(null, json: true);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        // Assert: the index has the usage, the commands, all topics with aliases, and the global options
        Assert.Equal(json, registry.RenderHelp("topics", json: true));
        Assert.Equal("index", root.GetProperty("kind").GetString());
        Assert.Equal("topics", root.GetProperty("topic").GetString());
        Assert.Contains("canvasnet help", root.GetProperty("usage").GetString());
        Assert.Equal(["render"], root.GetProperty("commands").EnumerateArray().Select(c => c.GetProperty("name").GetString()));
        var topics = root.GetProperty("topics").EnumerateArray().ToList();
        Assert.Equal(HelpTopics.TopicNames, topics.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal(["openscad"], topics[0].GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.False(string.IsNullOrEmpty(topics[0].GetProperty("summary").GetString()));
        Assert.NotEmpty(root.GetProperty("globalOptions").EnumerateArray());
    }

    /// <summary>
    ///     Test that <c>help &lt;topic&gt; --example</c> writes only the source of the example.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExample_WritesOnlyTheSource()
    {
        // Arrange: a registry, and the expected source and JSON
        var registry = CreateHelpRegistry();
        var topic = HelpTopics.Find("svg")!;

        // Act: print the shapes example as text and as JSON
        var text = RunHelp(registry, false, "svg", "--example", "shapes");
        var json = RunHelp(registry, true, "svg", "--example=shapes");

        // Assert: the text is the source and one line end; the JSON is the example document
        Assert.Equal(HelpTopics.RenderExample(topic, "shapes") + "\n", text.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(HelpTopics.RenderExampleJson(topic, "shapes"), json.TrimEnd());
    }

    /// <summary>
    ///     Test that <c>help &lt;topic&gt; --examples</c> lists the examples of the topic, as text and as JSON.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExamples_ListsNames()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();
        var topic = HelpTopics.Find("svg")!;

        // Act: list the examples
        var text = RunHelp(registry, false, "svg", "--examples");
        var json = RunHelp(registry, true, "svg", "--examples");

        // Assert: every example name is listed, and the JSON is the list document
        Assert.All(topic.Examples, example => Assert.Contains(example.Name, text));
        Assert.Equal(HelpTopics.RenderExampleListJson(topic), json.TrimEnd());
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
        var exception = RunHelpFailing(registry, "svg", "--example", "nothing");

        // Assert: a usage error naming the topic, the example, and the valid examples
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("Topic 'svg' has no example 'nothing'.", exception.Message);
        Assert.Contains("shapes", exception.Hint);
    }

    /// <summary>
    ///     Test that examples of a command (which has usage text, not examples) are a usage error.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExampleOnCommand_ThrowsUsageError()
    {
        // Arrange: a registry with a command
        var registry = CreateHelpRegistry();

        // Act / Assert: --example and --examples on a command or on 'topics' are usage errors
        Assert.Equal(ErrorCodes.UsageError, RunHelpFailing(registry, "render", "--example", "x").Code);
        Assert.Contains("has no examples", RunHelpFailing(registry, "render", "--examples").Message);
        Assert.Equal(ErrorCodes.UsageError, RunHelpFailing(registry, "topics", "--examples").Code);
    }

    /// <summary>
    ///     Test that --example without a topic, and --example together with --examples, are usage errors.
    /// </summary>
    [Fact]
    public void CommandRegistry_Run_HelpExampleWithoutSubject_ThrowsUsageError()
    {
        // Arrange: a registry
        var registry = CreateHelpRegistry();

        // Act / Assert: no topic, and both options together, are usage errors
        Assert.Contains("need a topic", RunHelpFailing(registry, "--example", "x").Message);
        Assert.Contains("need a topic", RunHelpFailing(registry, "--examples").Message);
        Assert.Contains("cannot be combined", RunHelpFailing(registry, "svg", "--example", "shapes", "--examples").Message);
        Assert.Equal(ErrorCodes.UsageError, RunHelpFailing(registry, "svg", "extra").Code);
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
        Assert.Contains("topics", exception.Hint);
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
