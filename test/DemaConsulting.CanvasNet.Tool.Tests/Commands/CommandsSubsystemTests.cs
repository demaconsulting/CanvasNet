// cspell:ignore canvasnet rmse deskew scadd
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

        // Assert: exit code 1 and a usage error that names the subject
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: usage-error: Unknown help subject 'nothing'.", stdErr);
    }

    /// <summary>
    ///     Test that a help topic is shown by name, by alias, and by the --help flag, all with the same text.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_HelpTopics_SubjectsResolve_ByNameAliasAndFlag()
    {
        // Arrange / Act: ask for the scad topic in the four ways
        var (byName, byNameOut, _) = ToolHost.Invoke("help", "scad");
        var (byAlias, byAliasOut, _) = ToolHost.Invoke("help", "OpenSCAD");
        var (byFlag, byFlagOut, _) = ToolHost.Invoke("scad", "--help");
        var (byTopics, byTopicsOut, _) = ToolHost.Invoke("help", "topics");

        // Assert: the topic text is the same (the flag form adds the banner), and 'help topics' is the index with the topic list
        Assert.Equal([0, 0, 0, 0], [byName, byAlias, byFlag, byTopics]);
        Assert.StartsWith("Topic: OpenSCAD models (.scad)", byNameOut);
        Assert.Equal(byNameOut, byAliasOut);
        Assert.EndsWith(byNameOut, byFlagOut);
        Assert.Contains("Help topics:", byTopicsOut);
        Assert.All(HelpTopics.TopicNames, name => Assert.Contains(name, byTopicsOut));
    }

    /// <summary>
    ///     Test that help with --json is one JSON line with no banner and no escape sequences, however it is asked for.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_HelpTopics_Json_IsOneLineWithoutBannerOrAnsi()
    {
        // Arrange / Act: ask for JSON help in the three ways
        var results = new[]
        {
            ToolHost.Invoke("help", "scad", "--json"),
            ToolHost.Invoke("--help", "--json"),
            ToolHost.Invoke("scad", "--help", "--json")
        };

        // Assert: each is exactly one JSON document of the help command on one line
        var kinds = new[] { "topic", "index", "topic" };
        for (var i = 0; i < results.Length; i++)
        {
            var (exitCode, stdOut, stdErr) = results[i];
            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, stdErr);
            Assert.DoesNotContain('\u001b', stdOut);
            Assert.Single(stdOut.TrimEnd().Split('\n'));
            using var document = System.Text.Json.JsonDocument.Parse(stdOut);
            Assert.Equal("help", document.RootElement.GetProperty("command").GetString());
            Assert.Equal(kinds[i], document.RootElement.GetProperty("kind").GetString());
        }
    }

    /// <summary>
    ///     Test that --example prints only the source of the example on standard output.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_HelpTopics_Example_PrintsOnlyTheSource()
    {
        // Arrange: the plate example
        var expected = HelpTopics.RenderExample(HelpTopics.Find("scad")!, "plate");

        // Act: print it
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("help", "scad", "--example", "plate");

        // Assert: stdout is the source and one newline, and nothing else was written
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stdErr);
        Assert.Equal(expected + "\n", stdOut.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that an unknown help subject is a usage error that lists the valid subjects, as text and as JSON.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_HelpTopics_UnknownSubject_ReportsUsageErrorWithValidSubjects()
    {
        // Arrange / Act: ask for an unknown subject, as text and with --json
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("help", "scadd");
        var (jsonExit, jsonOut, _) = ToolHost.Invoke("help", "nothing", "--json");

        // Assert: exit code 1, the suggestion and the valid subjects on stderr, and a failure document in JSON
        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdOut);
        Assert.Contains("Error: usage-error: Unknown help subject 'scadd'.", stdErr);
        Assert.Contains("Did you mean 'scad'?", stdErr);
        Assert.Contains("Valid subjects:", stdErr);
        Assert.Equal(1, jsonExit);
        using var document = System.Text.Json.JsonDocument.Parse(jsonOut);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("help", document.RootElement.GetProperty("command").GetString());
        Assert.Equal("usage-error", document.RootElement.GetProperty("error").GetProperty("code").GetString());
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
        Assert.Contains("Render or convert an image, drawing, or document page", topLevel);
        Assert.Contains("Show the format, size, and properties", topLevel);
        Assert.Contains("Usage: canvasnet render <input> -o <output> [options]", renderHelp);
        Assert.Contains("--no-clobber", renderHelp);
        Assert.Contains("-o, --output <file>", renderHelp);
        Assert.Contains("Usage: canvasnet render <input> -o <output> [options]", convertHelp);
        Assert.Contains("Usage: canvasnet info <input> [--page N | --all-pages] [--json]", infoHelp);
    }

    /// <summary>
    ///     Test that the default registry runs the transform verbs and lists them in the help.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_TransformVerbs_DefaultRegistry_RunAndAreListedInHelp()
    {
        // Arrange: a PNG in a temporary directory
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act: run each verb through the default registry, and ask for the help
        var crop = ToolHost.Invoke("crop", input, "0,0,6,4", "-o", directory.Combine("crop.png"));
        var resize = ToolHost.Invoke("resize", input, "6x4", "-o", directory.Combine("resize.png"));
        var rotate = ToolHost.Invoke("rotate", input, "90", "-o", directory.Combine("rotate.png"));
        var flip = ToolHost.Invoke("flip", input, "h", "-o", directory.Combine("flip.png"));
        var pad = ToolHost.Invoke("pad", input, "2", "-o", directory.Combine("pad.png"));
        var help = ToolHost.Invoke("help", "pad");
        var top = ToolHost.Invoke("--help");

        // Assert: each succeeds with one result line and nothing on standard error; the help names the verb
        Assert.All(new[] { crop, resize, rotate, flip, pad }, result =>
        {
            Assert.Equal(0, result.ExitCode);
            Assert.StartsWith("wrote ", result.StdOut);
            Assert.Equal(string.Empty, result.StdErr);
        });
        Assert.Contains("(6x4 png,", crop.StdOut);
        Assert.Contains("(8x12 png,", rotate.StdOut);
        Assert.Contains("(16x12 png,", pad.StdOut);
        Assert.Contains("Usage: canvasnet pad <input> <border> -o <output> [options]", help.StdOut);
        Assert.Contains("Add a border around an image", top.StdOut);
    }

    /// <summary>
    ///     Test that the default registry runs the compare and montage commands.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Comparison_DefaultRegistry_RunsCompareAndMontage()
    {
        // Arrange: two PNG images in a temporary directory
        using var directory = new TempDirectory();
        var (a, b) = CompareCommandTests.SavePair(directory);
        var sheet = directory.Combine("sheet.png");

        // Act: run each command through the default registry, then a failing compare
        var compare = ToolHost.Invoke("compare", a, b, "--threshold", "rmse=10");
        var montage = ToolHost.Invoke("montage", a, b, "--cell", "32x32", "-o", sheet);
        var failing = ToolHost.Invoke("compare", a, b, "--threshold", "rmse=1");

        // Assert: each succeeds with nothing on standard error; the failing compare is an expected error naming its command
        Assert.Equal(0, compare.ExitCode);
        Assert.StartsWith("compared ", compare.StdOut);
        Assert.Equal(string.Empty, compare.StdErr);
        Assert.Equal(0, montage.ExitCode);
        Assert.Equal($"wrote {sheet} (88x48 png, {new FileInfo(sheet).Length} bytes): 2 images in 2 columns{Environment.NewLine}", montage.StdOut);
        Assert.Equal(string.Empty, montage.StdErr);
        Assert.Equal(1, failing.ExitCode);
        Assert.Contains("Error: threshold-failed:", failing.StdErr);
    }

    /// <summary>
    ///     Test that the default registry lists the compare and montage commands in the help, each with its own help.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Comparison_DefaultRegistry_ListedInHelp()
    {
        // Act
        var top = ToolHost.Invoke("--help");
        var compare = ToolHost.Invoke("help", "compare");
        var montage = ToolHost.Invoke("montage", "--help");

        // Assert: both are listed with their summaries, and each has its usage and options
        Assert.Contains("Measure how much two images differ", top.StdOut);
        Assert.Contains("Compose images and document pages into one contact sheet", top.StdOut);
        Assert.Contains("Usage: canvasnet compare <a> <b> [options]", compare.StdOut);
        Assert.Contains("--resize-to <a|b|WxH>", compare.StdOut);
        Assert.Contains("Usage: canvasnet montage <inputs...> -o <output> [options]", montage.StdOut);
        Assert.Contains("-o, --output <file>", montage.StdOut);
    }

    /// <summary>
    ///     Test that the default registry runs the inspect command through the entry point and lists it in the help.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Inspect_DefaultRegistry_RunsInspectAndIsListedInHelp()
    {
        // Arrange: a PNG in a temporary directory
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "png");

        // Act: both subcommands through the default registry, a failing one, the help, and the help by name
        var colors = ToolHost.Invoke("inspect", "colors", input, "--count", "2");
        var pixel = ToolHost.Invoke("inspect", "pixel", input, "1,1", "--json");
        var failing = ToolHost.Invoke("inspect", "palette", input);
        var top = ToolHost.Invoke("--help");
        var help = ToolHost.Invoke("inspect", "--help");
        var byName = ToolHost.Invoke("help", "inspect");

        // Assert: success with nothing on standard error; the failing one is an expected error naming the command
        Assert.Equal(0, colors.ExitCode);
        Assert.StartsWith($"colors of {input} (12x8): ", colors.StdOut);
        Assert.Equal(string.Empty, colors.StdErr);
        Assert.Equal(0, pixel.ExitCode);
        Assert.Contains("\"command\":\"inspect\",\"subcommand\":\"pixel\"", pixel.StdOut);
        Assert.Equal(1, failing.ExitCode);
        Assert.Contains("Error: usage-error:", failing.StdErr);
        Assert.Contains("Unknown inspect subcommand 'palette'", failing.StdErr);

        // Assert: listed with its summary, with both usages and the options of render
        Assert.Contains("Report the dominant colors of an image, or the pixel at a point", top.StdOut);
        Assert.Contains("Usage: canvasnet inspect colors <input> [--count N] [options]", help.StdOut);
        Assert.Contains("canvasnet inspect pixel <input> <x,y> [--radius R] [options]", help.StdOut);
        Assert.Contains("--radius <R>", help.StdOut);
        Assert.Contains("--deskew", help.StdOut);
        Assert.Contains("Usage: canvasnet inspect colors <input> [--count N] [options]", byName.StdOut);
    }

    /// <summary>
    ///     Test that the default registry runs the flatten command through the entry point and lists it in the help.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_Flatten_DefaultRegistry_RunsFlattenAndListsItInHelp()
    {
        // Arrange: a drawing that links a Mermaid diagram
        using var directory = new TempDirectory();
        var input = FlattenCommandTests.WriteDrawing(directory, "<image href='flow.mmd' width='50' height='30'/>");
        var output = directory.Combine("flat.svg");

        // Act: the command, a failing one, the help, and the help by name
        var flatten = ToolHost.Invoke("flatten", input, "-o", output);
        var failing = ToolHost.Invoke("flatten", input);
        var top = ToolHost.Invoke("--help");
        var byName = ToolHost.Invoke("help", "flatten");

        // Assert: success with nothing on standard error; the failing one is an expected error
        Assert.Equal(0, flatten.ExitCode);
        Assert.StartsWith($"wrote {output} (", flatten.StdOut, StringComparison.Ordinal);
        Assert.Equal(string.Empty, flatten.StdErr);
        Assert.Equal(1, failing.ExitCode);
        Assert.Contains("Error: usage-error:", failing.StdErr, StringComparison.Ordinal);

        // Assert: listed with its summary and its usage
        Assert.Contains("only CanvasNet can draw with PNG pictures", top.StdOut, StringComparison.Ordinal);
        Assert.Contains("Usage: canvasnet flatten <input.svg> -o <output.svg>", byName.StdOut, StringComparison.Ordinal);
        Assert.Contains("--scale <factor>", byName.StdOut, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every command that reads an input (render, a verb, inspect, compare, montage, and flatten) accepts
    ///     <c>--file-access</c> and applies it: a picture outside the folder of the drawing is denied with a warning.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_FileAccess_EveryReadingCommand_AcceptsTheOptionsAndDeniesAnOutsidePicture()
    {
        // Arrange: a drawing in a sub folder that links a Mermaid diagram in the folder above it
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), directory.Combine("flow.mmd"));
        var input = FixtureFactory.WriteText(
            directory.Path,
            "site/diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><image href='../flow.mmd' width='60' height='40'/></svg>");
        string[][] commands =
        [
            ["render", input, "-o", directory.Combine("render.png")],
            ["rotate", input, "90", "-o", directory.Combine("rotate.png")],
            ["inspect", "colors", input],
            ["compare", input, input],
            ["montage", input, "-o", directory.Combine("montage.png")],
            ["flatten", input, "-o", directory.Combine("flat.svg")]
        ];

        foreach (var command in commands)
        {
            // Act
            var (exitCode, stdOut, _) = ToolHost.Invoke([.. command, "--file-access", "roots", "--json"]);

            // Assert: success, with the denial among the notes
            Assert.True(exitCode == 0, $"{command[0]} failed: {stdOut}");
            Assert.Contains("\"code\":\"image-access-denied\"", stdOut, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Test that <c>info</c> has no file-access option: it measures under the default mode.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_FileAccess_Info_HasNoFileAccessOption()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "svg");

        // Act
        var (exitCode, _, stdErr) = ToolHost.Invoke("info", input, "--file-access", "roots");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: usage-error: Unknown option '--file-access' for 'info'.", stdErr, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that, in the source of the tool, every probe of an input and every set of load, flatten, and montage
    ///     options names a file-access policy, so no command can read a reference under a policy it did not choose.
    /// </summary>
    [Fact]
    public void CommandsSubsystem_FileAccess_SourceTree_EveryProbeAndRequestCarriesThePolicy()
    {
        // Arrange: every C# file of the tool
        var source = Path.Combine(FindRepositoryRoot(), "src", "DemaConsulting.CanvasNet.Tool");
        var files = Directory.GetFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        string[] patterns = ["SourceLoader.Probe(", "new SourceOptions", "new FlattenRequest(", "new MontageRequest("];

        // Act: each statement that starts with a pattern, up to the semicolon that ends it
        var statements = new List<(string File, string Text)>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var pattern in patterns)
            {
                for (var index = text.IndexOf(pattern, StringComparison.Ordinal); index >= 0; index = text.IndexOf(pattern, index + 1, StringComparison.Ordinal))
                {
                    var end = text.IndexOf(';', index);
                    statements.Add((Path.GetFileName(file), text[index..end]));
                }
            }
        }

        // Assert: the seven probes and the four sets of options were found, and each names a policy
        Assert.True(statements.Count >= 11, $"Only {statements.Count} statements were found.");
        Assert.All(
            statements,
            statement => Assert.True(
                statement.Text.Contains("FileAccess", StringComparison.OrdinalIgnoreCase),
                $"{statement.File}: '{statement.Text}' does not name a file-access policy."));
    }

    /// <summary>
    ///     Finds the root of the repository: the folder that holds <c>CanvasNet.slnx</c>.
    /// </summary>
    /// <returns>The full path of the folder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when there is no such folder above the test assembly.</exception>
    private static string FindRepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "CanvasNet.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException(
            $"The repository root (the folder with CanvasNet.slnx) was not found above {AppContext.BaseDirectory}.");
    }
}
