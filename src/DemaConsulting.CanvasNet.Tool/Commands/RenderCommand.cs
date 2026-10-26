// cspell:ignore canvasnet jpeg tif tiff
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The <c>render</c> command (alias <c>convert</c>): loads an image or drawing, and writes it as PNG, JPEG, BMP, or
///     TIFF.
/// </summary>
/// <remarks>
///     <para>
///         The command is one pass of load, transform, and write. Loading is <see cref="SourceLoader"/>: a vector
///         source (SVG) is rendered directly at the requested size, over white unless a background is given; a raster
///         source is decoded, turned upright by its EXIF orientation (unless <c>--no-auto-orient</c>), and resized when
///         a size is requested. Writing is <see cref="ImageWriter"/>: the format comes from the extension or
///         <c>--format</c>, JPEG is flattened over the background, an existing output is overwritten unless
///         <c>--no-clobber</c>, and the output may not be the input. The result is one text line, or one JSON
///         document with <c>--json</c>.
///     </para>
///     <para>
///         The class is split into <c>RenderCommand.Options.cs</c> (the option declarations and the reading of the
///         settings) and this file (the run).
///     </para>
/// </remarks>
internal sealed partial class RenderCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "render";

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases => ["convert"];

    /// <inheritdoc />
    public string Summary => "Render or convert an image or drawing to PNG, JPEG, BMP, or TIFF";

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options => OptionTable;

    /// <inheritdoc />
    public string Usage =>
        "canvasnet render <input> -o <output> [options]" + Environment.NewLine
        + Environment.NewLine
        + "Reads PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, or SVG and writes the format of the output extension." + Environment.NewLine
        + "SVG is flattened over white; a JPEG is turned upright by its EXIF orientation." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet render drawing.svg -o drawing.png" + Environment.NewLine
        + "  canvasnet render drawing.svg --width 1200 -o drawing.png" + Environment.NewLine
        + "  canvasnet render shot.png -o shot.jpg --quality 85 --json" + Environment.NewLine
        + "  canvasnet convert scan.tif -o scan.png --no-clobber";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        var reader = ArgumentReader.Parse(Name, args, Options);
        var settings = ReadSettings(reader);

        // Report every mistake about the input and the output before any pixels are decoded
        var info = SourceLoader.Probe(settings.Input);
        ImageWriter.ResolveFormat(settings.Output, settings.Write.Format?.ToFormatName());
        ImageWriter.CheckTarget(settings.Output, settings.Input, settings.Write.NoClobber);

        using var loaded = SourceLoader.Load(info, settings.Source);

        // The image pipeline (crop, resize, rotate, and so on) is applied here, between the load and the write
        var result = ImageWriter.Write(loaded.Surface, settings.Output, settings.Input, settings.Write);

        var operations = result.Flattened
            ? loaded.Operations
                .Append(SourceLoader.Operation("flatten", ("background", SourceLoader.FormatColor(settings.Write.FlattenBackground))))
                .ToList()
            : loaded.Operations;
        context.WriteLine(context.Json
            ? ResultWriter.Success(Name, writer => WriteJsonFields(writer, loaded, operations, result))
            : RenderText(loaded, result));
    }

    /// <summary>
    ///     Renders the one-line text result.
    /// </summary>
    /// <param name="loaded">The loaded source.</param>
    /// <param name="result">What was written.</param>
    /// <returns>The text, for example <c>wrote out.png (816x1056 png, 41208 bytes) from in.svg</c>.</returns>
    private static string RenderText(LoadedSource loaded, WriteResult result)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"wrote {result.Path} ({result.Width}x{result.Height} {result.Format.ToFormatName()}, {result.Bytes} bytes) from {loaded.Info.Path}");
    }

    /// <summary>
    ///     Writes the command's fields of the JSON document.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="loaded">The loaded source.</param>
    /// <param name="operations">What was done to the source, in order.</param>
    /// <param name="result">What was written.</param>
    private static void WriteJsonFields(Utf8JsonWriter writer, LoadedSource loaded, IReadOnlyList<AppliedOperation> operations, WriteResult result)
    {
        var info = loaded.Info;

        writer.WriteStartObject("tool");
        writer.WriteString("name", "canvasnet");
        writer.WriteString("version", Program.Version);
        writer.WriteEndObject();

        writer.WriteStartObject("input");
        writer.WriteString("path", info.Path);
        writer.WriteString("format", info.Format.ToFormatName());
        writer.WriteStartObject("natural");
        writer.WriteNumber("width", info.Width);
        writer.WriteNumber("height", info.Height);
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartArray("operations");
        foreach (var operation in operations)
        {
            writer.WriteStartObject();
            writer.WriteString("op", operation.Op);
            foreach (var (name, value) in operation.Fields)
            {
                WriteField(writer, name, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("outputs");
        writer.WriteStartObject();
        writer.WriteString("path", result.Path);
        writer.WriteString("format", result.Format.ToFormatName());
        writer.WriteNumber("width", result.Width);
        writer.WriteNumber("height", result.Height);
        writer.WriteNumber("bytes", result.Bytes);
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WriteStartArray("diagnostics");
        foreach (var diagnostic in loaded.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("source", diagnostic.Source);
            writer.WriteString("severity", diagnostic.Severity);
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("message", diagnostic.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("echo");
        foreach (var line in loaded.Echo)
        {
            writer.WriteStringValue(line);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes one parameter of an operation.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value: a string, an integer, a number, or a flag.</param>
    private static void WriteField(Utf8JsonWriter writer, string name, object value)
    {
        switch (value)
        {
            case string text:
                writer.WriteString(name, text);
                break;
            case int number:
                writer.WriteNumber(name, number);
                break;
            case double real:
                writer.WriteNumber(name, real);
                break;
            case bool flag:
                writer.WriteBoolean(name, flag);
                break;
            default:
                throw new InvalidOperationException($"The operation parameter '{name}' has an unsupported type {value.GetType().Name}.");
        }
    }
}
