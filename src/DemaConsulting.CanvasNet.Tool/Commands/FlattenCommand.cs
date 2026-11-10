using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The <c>flatten</c> command: writes a copy of an SVG drawing in which every picture that only CanvasNet can draw is
///     a PNG picture, so that any SVG reader shows it.
/// </summary>
/// <remarks>
///     The command is a thin front end of <see cref="SvgFlattener"/>: it reads the options, probes the input, and formats
///     what the operation returns as one text line (and the notes on standard error) or as the JSON document. When there
///     is nothing to flatten nothing is written and the exit code is still 0, so a caller can tell the two cases apart by
///     the text or by <c>flattened</c> in the JSON document.
/// </remarks>
internal sealed class FlattenCommand : ICommand
{
    /// <summary>
    ///     The options of the command, in the order they are listed in the help.
    /// </summary>
    /// <remarks>
    ///     <c>--strict</c> and <c>--no-clobber</c> are the declarations of <see cref="RenderCommand.OptionTable"/>, so the
    ///     grammar and the help text cannot drift.
    /// </remarks>
    internal static readonly IReadOnlyList<OptionSpec> OptionTable =
    [
        new("--output", ["-o"], true, "The SVG file the flattened copy is written to", "file"),
        new("--scale", [], true, "Multiply the size of each replaced picture, for example 2 for sharper pictures in print", "factor"),
        .. RenderCommand.SpecsFor("--strict", "--no-clobber")
    ];

    /// <inheritdoc />
    public string Name => "flatten";

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases => [];

    /// <inheritdoc />
    public string Summary => "Replace the pictures of an SVG drawing that only CanvasNet can draw with PNG pictures, so any SVG reader shows them";

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options => OptionTable;

    /// <inheritdoc />
    public string Usage =>
        "canvasnet flatten <input.svg> -o <output.svg> [--scale <factor>] [--strict] [--no-clobber]" + Environment.NewLine
        + Environment.NewLine
        + "Writes a copy of an SVG drawing in which every <image> link to a file only CanvasNet draws (a Mermaid diagram," + Environment.NewLine
        + "an OpenSCAD model, a Vega-Lite chart, a PDF, PowerPoint, or Visio page, with or without a fragment, and a nested" + Environment.NewLine
        + "SVG file that links one) is a PNG picture of its placement size, as a data: URI. Nothing else in the file changes;" + Environment.NewLine
        + "relative links that are left are rewritten when the copy is in another folder. When the drawing links no such" + Environment.NewLine
        + "picture, nothing is written and the exit code is 0. See 'canvasnet help svg' for the rules." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet flatten dashboard.svg -o dashboard-flat.svg" + Environment.NewLine
        + "  canvasnet flatten dashboard.svg -o print/dashboard.svg --scale 2" + Environment.NewLine
        + "  canvasnet flatten dashboard.svg -o dashboard-flat.svg --strict --json";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        // Read the options; the operation checks the input, the scale, and the output before anything is drawn
        var reader = ArgumentReader.Parse(Name, args, Options);
        reader.RequirePositionals(1, 1, "one SVG file");
        var output = reader.Get("--output")
                     ?? throw new ToolException(
                         ErrorCodes.UsageError,
                         "No output file was given.",
                         "Add -o <file>, for example: canvasnet flatten dashboard.svg -o dashboard-flat.svg");
        var scale = reader.GetDouble("--scale", 0.001, Surface.MaxDimension) ?? 1;
        var info = SourceLoader.Probe(reader.Positionals[0]);
        var result = SvgFlattener.Execute(
            info,
            new FlattenRequest(output) { Scale = scale, Strict = reader.Has("--strict"), NoClobber = reader.Has("--no-clobber") });

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(Name, writer => WriteJsonFields(writer, result, scale, output)));
            return;
        }

        context.WriteLine(RenderText(result, output));
        context.WriteDiagnostics(result.Diagnostics.Select(d => ((string?)info.Path, d)), result.Echo);
    }

    /// <summary>
    ///     Renders the one-line text result.
    /// </summary>
    /// <param name="result">What the operation returned.</param>
    /// <param name="output">The output path as given.</param>
    /// <returns>
    ///     For example <c>wrote flat.svg (61234 bytes): 2 pictures replaced (flow.mmd 640x360 mermaid, part.scad 400x400 scad)</c>,
    ///     or <c>nothing to flatten: a.svg links no picture that only CanvasNet draws; flat.svg was not written</c>.
    /// </returns>
    internal static string RenderText(FlattenResult result, string output)
    {
        if (!result.Flattened)
        {
            return $"nothing to flatten: {result.Info.Path} links no picture that only CanvasNet draws; {output} was not written";
        }

        var count = result.Replaced.Count == 1 ? "1 picture" : $"{result.Replaced.Count} pictures";
        var list = string.Join(", ", result.Replaced.Select(r => $"{r.Href} {r.Width}x{r.Height} {r.Format}"));
        return string.Create(CultureInfo.InvariantCulture, $"wrote {output} ({result.Bytes} bytes): {count} replaced ({list})");
    }

    /// <summary>
    ///     Writes the command's fields of the JSON document.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="result">What the operation returned.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="output">The output path as given.</param>
    /// <remarks>
    ///     The keys are always written, in this order; <c>outputs</c>, <c>replaced</c>, and <c>rebased</c> are empty when
    ///     nothing was flattened.
    /// </remarks>
    private static void WriteJsonFields(Utf8JsonWriter writer, FlattenResult result, double scale, string output)
    {
        writer.WriteStartObject("tool");
        writer.WriteString("name", "canvasnet");
        writer.WriteString("version", Program.Version);
        writer.WriteEndObject();

        writer.WriteStartObject("input");
        writer.WriteString("path", result.Info.Path);
        writer.WriteString("format", result.Info.Format.ToFormatName());
        writer.WriteStartObject("natural");
        writer.WriteNumber("width", result.Info.Width);
        writer.WriteNumber("height", result.Info.Height);
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteNumber("scale", scale);
        writer.WriteBoolean("flattened", result.Flattened);

        writer.WriteStartArray("replaced");
        foreach (var image in result.Replaced)
        {
            writer.WriteStartObject();
            writer.WriteString("href", image.Href);
            writer.WriteString("format", image.Format);
            writer.WriteNumber("width", image.Width);
            writer.WriteNumber("height", image.Height);
            writer.WriteNumber("elements", image.Elements);
            writer.WriteNumber("bytes", image.Bytes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("rebased");
        foreach (var link in result.Rebased)
        {
            writer.WriteStartObject();
            writer.WriteString("href", link.Href);
            writer.WriteString("newHref", link.NewHref);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("outputs");
        if (result.Flattened)
        {
            writer.WriteStartObject();
            writer.WriteString("path", output);
            writer.WriteString("format", "svg");
            writer.WriteNumber("bytes", result.Bytes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("diagnostics");
        foreach (var diagnostic in result.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("source", diagnostic.Source);
            writer.WriteString("severity", diagnostic.Severity);
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("message", diagnostic.Message);
            ResultWriter.WriteDiagnosticExtras(writer, diagnostic);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("echo");
        foreach (var line in result.Echo)
        {
            writer.WriteStringValue(line);
        }

        writer.WriteEndArray();
    }
}
