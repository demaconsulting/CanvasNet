using System.Globalization;
using DemaConsulting.CanvasNet.Tool.PandocFilter;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The <c>pandoc-filter</c> command: draws the pictures of a Pandoc document for the Lua shim
///     <c>docs/filters/canvasnet.lua</c>, in one process, and writes the shim with <c>--write-shim</c>.
/// </summary>
/// <remarks>
///     <para>
///         The shim sends the document as Pandoc's JSON on standard input, with what only Pandoc knows as options (the
///         format, the output file, the resource path, the shim's path), and reads the transformed document from standard
///         output; the warnings are in the metadata key <c>canvasnet-warnings</c>. The command is a thin front end of
///         <see cref="PandocFilterEngine"/>: it checks the protocol before reading anything, reads the input bytes, runs the
///         engine, and writes the output bytes. Standard input and output are read and written as raw UTF-8 bytes (never
///         through the console's text encoding, which would corrupt non-ASCII text), and the output is written only when
///         the run succeeded. <c>--input</c> and <c>--output</c> name files instead, for tests and debugging.
///     </para>
///     <para>
///         A failure is the tool's usual expected error: exit code 1, <c>Error: ...</c> on standard error, and with
///         <c>--json</c> the JSON failure document on standard output, which the shim turns into the build's error. The
///         warnings of the pictures before the failure are written to standard error first, as
///         <c>[WARNING] canvasnet.lua: ...</c> lines, which Pandoc passes on, so they are not lost.
///     </para>
/// </remarks>
internal sealed class PandocFilterCommand : ICommand
{
    /// <summary>
    ///     The option that writes the embedded shim.
    /// </summary>
    internal const string WriteShimOption = "--write-shim";

    /// <summary>
    ///     The options of the command, in the order they are listed in the help.
    /// </summary>
    internal static readonly IReadOnlyList<OptionSpec> OptionTable =
    [
        new("--protocol", [], true, "The protocol the shim speaks (the shim passes it)", "n"),
        new("--filter-path", [], true, "The path of the shim (PANDOC_SCRIPT_FILE): pictures are cached in generated/canvasnet beside its folder", "file"),
        new("--format", [], true, "The output format of Pandoc (FORMAT), for example html5", "format"),
        new("--pandoc-version", [], true, "The version of Pandoc, for messages", "version"),
        new("--resource-path", [], true, "A folder of the document's resource path, in order (repeatable; default: .)", "folder"),
        new("--pandoc-output", [], true, "The output file of Pandoc: an HTML picture is linked relative to its folder", "file"),
        new("--embed-resources", [], false, "Pandoc embeds the pictures, so they are linked by their full path"),
        new("--jobs", [], true, $"Pictures drawn at once, 1 to {FilterSettings.MaxJobs} (default: CANVASNET_JOBS, else the processors up to {FilterSettings.DefaultJobs})", "n"),
        new("--input", [], true, "Read the document from a file instead of standard input", "file"),
        new("--output", ["-o"], true, "Write the document to a file instead of standard output", "file"),
        new(WriteShimOption, [], true, "Write the Lua shim to a file (its folder is created) and do nothing else", "file")
    ];

    /// <inheritdoc />
    public string Name => "pandoc-filter";

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases => [];

    /// <inheritdoc />
    public string Summary => "Draw the pictures of a Pandoc document for the Pandoc filter docs/filters/canvasnet.lua, in one process";

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options => OptionTable;

    /// <inheritdoc />
    public string Usage =>
        "canvasnet pandoc-filter --write-shim <file>" + Environment.NewLine
        + "canvasnet pandoc-filter --protocol <n> --filter-path <file> [--format <format>] [--resource-path <folder>]... [options] [--json]" + Environment.NewLine
        + Environment.NewLine
        + "The Pandoc filter docs/filters/canvasnet.lua is a short Lua shim that sends the whole document to this command" + Environment.NewLine
        + "once and applies what comes back: the models, charts, diagrams, equations, drawings, and document pages the document" + Environment.NewLine
        + "names (and its mermaid, plantuml, mathml, and vega-lite code blocks, and openscad code blocks marked .draw) are drawn" + Environment.NewLine
        + "into the cache folder generated/canvasnet and the pictures point there. Write the shim with --write-shim, and" + Environment.NewLine
        + "again after each update of the tool: it lists the code-block classes and picture name ends this tool draws and" + Environment.NewLine
        + "starts the tool only for those. It names the protocol it" + Environment.NewLine
        + "speaks, and a shim this tool does not speak is an error that says how to write it again. The document is" + Environment.NewLine
        + "Pandoc's JSON on standard input (--input reads a file), and the transformed document goes to standard output" + Environment.NewLine
        + "(--output writes a file). The environment variables CANVASNET_TOOL, CANVASNET_FILE_ACCESS, CANVASNET_ROOTS, and" + Environment.NewLine
        + "CANVASNET_JOBS apply. See 'canvasnet help pandoc'." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet pandoc-filter --write-shim docs/filters/canvasnet.lua" + Environment.NewLine
        + "  canvasnet pandoc-filter --protocol 1 --filter-path docs/filters/canvasnet.lua --format html5 --input doc.json -o out.json";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        var reader = ArgumentReader.Parse(Name, args, Options);
        reader.RequirePositionals(0, 0, "no arguments");

        // Writing the shim needs nothing else
        if (reader.Get(WriteShimOption) is { } shimPath)
        {
            var bytes = PandocFilterShim.Write(shimPath);
            context.WriteLine(context.Json
                ? ResultWriter.Success(Name, writer =>
                {
                    writer.WriteStartObject("tool");
                    writer.WriteString("name", "canvasnet");
                    writer.WriteString("version", Program.Version);
                    writer.WriteEndObject();
                    writer.WriteNumber("protocol", PandocFilterShim.Protocol);
                    writer.WriteStartArray("outputs");
                    writer.WriteStartObject();
                    writer.WriteString("path", shimPath);
                    writer.WriteString("format", "lua");
                    writer.WriteNumber("bytes", bytes);
                    writer.WriteEndObject();
                    writer.WriteEndArray();
                })
                : $"wrote {shimPath} (protocol {PandocFilterShim.Protocol})");
            return;
        }

        // The protocol is checked before anything is read, so an outdated shim gets the command that fixes it
        var filterPath = reader.Get("--filter-path");
        var protocolText = reader.Get("--protocol")
                           ?? throw new ToolException(
                               ErrorCodes.UsageError,
                               "No --protocol was given.",
                               $"The Lua shim passes it; write the shim with: dotnet canvasnet pandoc-filter --write-shim {filterPath ?? PandocFilterShim.DefaultPath}");
        if (!int.TryParse(protocolText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var protocol))
        {
            throw new ToolException(ErrorCodes.UsageError, $"The option --protocol needs a whole number, but '{protocolText}' was given.", "The Lua shim passes it, for example --protocol 1.");
        }

        PandocFilterShim.CheckProtocol(protocol, filterPath);
        if (filterPath == null)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "No --filter-path was given.",
                $"Pass the path of the Lua shim (PANDOC_SCRIPT_FILE), for example --filter-path {PandocFilterShim.DefaultPath}.");
        }

        // The inputs of the run, and how many pictures are drawn at once
        var settings = new FilterSettings
        {
            FilterPath = filterPath,
            WorkingDirectory = Environment.CurrentDirectory,
            Environment = Environment.GetEnvironmentVariable,
            Format = reader.Get("--format") ?? string.Empty,
            OutputFile = reader.Get("--pandoc-output"),
            EmbedResources = reader.Has("--embed-resources"),
            ResourcePaths = reader.GetAll("--resource-path") is { Count: > 0 } paths ? paths : ["."]
        };
        var jobs = FilterSettings.ResolveJobs(reader.GetInt("--jobs", 1, FilterSettings.MaxJobs), settings.Environment);

        // Read, transform, and write; nothing is written unless the run succeeded
        var input = ReadInput(reader.Get("--input"));
        var engine = new PandocFilterEngine(settings, new ToolPictureDrawer(), jobs);
        byte[] output;
        try
        {
            output = engine.Run(input, reader.Get("--pandoc-version"));
        }
        catch
        {
            // The warnings of the pictures before the failure, in Pandoc's format, as the Lua filter printed them
            foreach (var warning in engine.WarningsBeforeFailure)
            {
                context.WriteWarning($"[WARNING] canvasnet.lua: {Context.EscapeControlCharacters(warning)}");
            }

            throw;
        }

        WriteOutput(context, reader.Get("--output"), output);
    }

    /// <summary>
    ///     Reads the document: a file, or all of standard input as raw bytes.
    /// </summary>
    /// <param name="file">The file, or <see langword="null"/> for standard input.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.InputNotFound"/> or <see cref="ErrorCodes.InputUnreadable"/> when the file cannot be read.</exception>
    private static byte[] ReadInput(string? file)
    {
        if (file == null)
        {
            using var stdin = Console.OpenStandardInput();
            using var memory = new MemoryStream();
            stdin.CopyTo(memory);
            return memory.ToArray();
        }

        try
        {
            return File.ReadAllBytes(file);
        }
        catch (FileNotFoundException ex)
        {
            throw new ToolException(ErrorCodes.InputNotFound, $"The input file '{file}' does not exist.", "Check the path.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new ToolException(ErrorCodes.InputUnreadable, $"The input file '{file}' cannot be read: {ex.Message}", "Check the path and its permissions.", ex);
        }
    }

    /// <summary>
    ///     Writes the document: to a file, or as raw bytes to standard output (unless <c>--silent</c>).
    /// </summary>
    /// <param name="context">The context, for <c>--silent</c>.</param>
    /// <param name="file">The file, or <see langword="null"/> for standard output.</param>
    /// <param name="bytes">The document.</param>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.OutputWriteFailed"/> when the file cannot be written.</exception>
    private static void WriteOutput(Context context, string? file, byte[] bytes)
    {
        if (file == null)
        {
            if (!context.Silent)
            {
                using var stdout = Console.OpenStandardOutput();
                stdout.Write(bytes);
                stdout.Flush();
            }

            return;
        }

        try
        {
            File.WriteAllBytes(file, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new ToolException(ErrorCodes.OutputWriteFailed, $"The output file '{file}' cannot be written: {ex.Message}", "Check the path and its permissions.", ex);
        }
    }
}
