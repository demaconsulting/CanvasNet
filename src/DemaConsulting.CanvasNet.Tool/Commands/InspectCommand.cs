// cspell:ignore canvasnet luma otsu deskew
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The <c>inspect</c> command: reports what an image contains - <c>inspect colors</c> the dominant colors and the
///     luma statistics, <c>inspect pixel</c> the pixel at a point and, optionally, its neighborhood.
/// </summary>
/// <remarks>
///     <para>
///         The command is read-only: it writes no file, so it has no output option, no format, and no overwrite policy.
///         It loads one page of its input exactly as <c>render</c> does (the same source options, so an EXIF
///         orientation, a document page, a view of a model, a size, and a background all work), then runs the same image
///         pipeline (every operation of <c>render</c>, so <c>inspect colors photo.jpg --crop 100,100,50,50</c> answers
///         "what color is this region" and the coordinates of <c>inspect pixel</c> are those of the image after the
///         pipeline), and finally analyzes the pixels with <see cref="Surface.Analyze"/> and <see cref="Surface.Sample"/>.
///     </para>
///     <para>
///         One registered command has two subcommands, because the registry is flat: the first positional argument is
///         the subcommand. A missing or unknown subcommand, and an option of the other subcommand, are usage errors. The
///         result is one or two text lines, or one JSON document with <c>--json</c>.
///     </para>
///     <para>
///         The class is split into <c>InspectCommand.Options.cs</c> (the option declarations and the reading of the
///         settings) and this file (the run and the results).
///     </para>
/// </remarks>
internal sealed partial class InspectCommand : ICommand
{
    /// <summary>The name of the command.</summary>
    private const string CommandName = "inspect";

    /// <summary>The name of the option that sets the number of colors.</summary>
    internal const string CountOption = "--count";

    /// <summary>The name of the option that sets the radius of the window around a point.</summary>
    internal const string RadiusOption = "--radius";

    /// <summary>The largest edit distance at which a subcommand name is suggested.</summary>
    private const int MaxSuggestionDistance = 2;

    /// <inheritdoc />
    public string Name => CommandName;

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases => [];

    /// <inheritdoc />
    public string Summary => "Report the dominant colors of an image, or the pixel at a point (colors, pixel)";

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options => OptionTable;

    /// <inheritdoc />
    public string Usage =>
        "canvasnet inspect colors <input> [--count N] [options]" + Environment.NewLine
        + "canvasnet inspect pixel <input> <x,y> [--radius R] [options]" + Environment.NewLine
        + Environment.NewLine
        + "Reads an image, drawing, or document page like 'render', applies the same operations (--crop, --deskew," + Environment.NewLine
        + "--auto-levels, ... in the order written), and reports what the pixels contain; no file is written." + Environment.NewLine
        + "colors reports the dominant colors (shades within about 32 levels are one color) with their share of the" + Environment.NewLine
        + "pixels, and the luma statistics (mean, range, percentiles, the level --threshold auto would choose, and a" + Environment.NewLine
        + "16-bin histogram) that help to choose --levels and --threshold values. pixel reports the color and luma of" + Environment.NewLine
        + "one pixel; with --radius also the pixels around it and their mean. Coordinates are those of the image after" + Environment.NewLine
        + "the operations, from 0,0 at the top left. Transparent pixels are flattened over the --background (white by" + Environment.NewLine
        + "default) before they are counted. The input may end in a fragment (part.scad#view=top, report.pdf#page=2) as" + Environment.NewLine
        + "for 'render'." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet inspect colors napkin.jpg --count 6" + Environment.NewLine
        + "  canvasnet inspect colors photo.jpg --crop 100,100,50,50 --json" + Environment.NewLine
        + "  canvasnet inspect pixel sketch.png 412,230" + Environment.NewLine
        + "  canvasnet inspect pixel sketch.png 412,230 --radius 2 --json";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        var reader = ArgumentReader.Parse(Name, args, Options);
        var settings = ReadSettings(reader);

        // Report every mistake about the input before any pixels are decoded; a '#' fragment of the input is read here too
        var info = SourceLoader.Probe(InputReference.Parse(settings.Input), settings.Source.FileAccess);
        if (settings.Source.Scad != null && !info.Format.IsModel())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {string.Join(", ", RenderCommand.ScadOptionNames)} only apply to an OpenSCAD model, but '{settings.Input}' is a {info.Format.ToFormatName()} file.",
                "Remove them, or inspect a .scad file.");
        }

        if (settings.Source.VegaLite != null && !info.Format.IsChart())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The option {string.Join(", ", RenderCommand.ChartOptionNames)} only applies to a Vega-Lite chart, but '{settings.Input}' is a {info.Format.ToFormatName()} file.",
                "Remove it, or inspect a .vl.json file.");
        }

        if (settings.Source.Mermaid != null && !info.Format.IsDiagram())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The option {string.Join(", ", RenderCommand.DiagramOptionNames)} only applies to a Mermaid diagram, but '{settings.Input}' is a {info.Format.ToFormatName()} file.",
                "Remove it, or inspect a .mmd file.");
        }

        // The operation reports the mistakes about the page before any pixels are decoded, then loads the page, runs the
        // pipeline, and analyzes the pixels; everything below only formats what it returns
        var result = ImageInspector.Execute(
            info,
            new InspectRequest(settings.Subcommand == "colors" ? InspectKind.Colors : InspectKind.Pixel)
            {
                Source = settings.Source,
                Operations = settings.Operations,
                Count = settings.Count,
                Radius = settings.Radius,
                Point = settings.Point is { } point ? new PixelPoint(point.X, point.Y) : null
            });
        if (settings.Subcommand == "colors")
        {
            RunColors(context, result);
        }
        else
        {
            RunPixel(context, result);
        }

        context.WriteDiagnostics(result.Diagnostics.Select(d => ((string?)settings.Input, d)), result.Echo);
    }

    /// <summary>
    ///     Reports <c>inspect colors</c>.
    /// </summary>
    /// <param name="context">The context that receives the output.</param>
    /// <param name="result">What the inspection found.</param>
    private static void RunColors(Context context, InspectResult result)
    {
        var background = result.Background;
        var statistics = result.Colors!;

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(CommandName, writer =>
            {
                WriteCommonFields(writer, "colors", result);
                writer.WriteString("background", SourceLoader.FormatColor(background));
                writer.WriteStartArray("colors");
                foreach (var color in statistics.Colors)
                {
                    writer.WriteStartObject();
                    writer.WriteString("hex", SourceLoader.FormatColor(color.Color));
                    writer.WriteNumber("r", color.Color.R);
                    writer.WriteNumber("g", color.Color.G);
                    writer.WriteNumber("b", color.Color.B);
                    writer.WriteNumber("pixels", color.Pixels);
                    writer.WriteNumber("fraction", color.Fraction);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                WriteLuma(writer, statistics.Luma);
                WriteTail(writer, result);
            }));
        }
        else
        {
            var colors = string.Join(
                ", ",
                statistics.Colors.Select(c => string.Create(CultureInfo.InvariantCulture, $"{SourceLoader.FormatColor(c.Color)} {c.Fraction * 100:0.00}%")));
            context.WriteLine(string.Create(CultureInfo.InvariantCulture, $"colors of {result.Info.Specifier} ({result.Width}x{result.Height}): {colors}"));
            var luma = statistics.Luma;
            context.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"luma: mean {luma.Mean:0.00}, min {luma.Min}, max {luma.Max}, p1 {luma.P1}, p50 {luma.P50}, p99 {luma.P99}, otsu {luma.Otsu}"));
        }
    }

    /// <summary>
    ///     Reports <c>inspect pixel</c>.
    /// </summary>
    /// <param name="context">The context that receives the output.</param>
    /// <param name="result">What the inspection found.</param>
    private static void RunPixel(Context context, InspectResult result)
    {
        var sample = result.Pixel!;
        var (x, y) = (sample.X, sample.Y);
        var pixel = sample.Pixel;

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(CommandName, writer =>
            {
                WriteCommonFields(writer, "pixel", result);
                writer.WriteStartObject("point");
                writer.WriteNumber("x", x);
                writer.WriteNumber("y", y);
                writer.WriteEndObject();
                writer.WriteStartObject("pixel");
                writer.WriteNumber("r", pixel.R);
                writer.WriteNumber("g", pixel.G);
                writer.WriteNumber("b", pixel.B);
                writer.WriteNumber("a", pixel.A);
                writer.WriteString("hex", SourceLoader.FormatColor(pixel));
                writer.WriteNumber("luma", sample.Luma);
                writer.WriteEndObject();
                if (sample.Window is { } window)
                {
                    writer.WriteStartObject("neighborhood");
                    writer.WriteNumber("x", window.X);
                    writer.WriteNumber("y", window.Y);
                    writer.WriteNumber("width", window.Width);
                    writer.WriteNumber("height", window.Height);
                    writer.WriteString("mean", SourceLoader.FormatColor(window.Mean));
                    writer.WriteStartArray("pixels");
                    for (var row = 0; row < window.Height; row++)
                    {
                        writer.WriteStartArray();
                        for (var column = 0; column < window.Width; column++)
                        {
                            writer.WriteStringValue(SourceLoader.FormatColor(window.Pixels[(row * window.Width) + column]));
                        }

                        writer.WriteEndArray();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                WriteTail(writer, result);
            }));
        }
        else
        {
            context.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"pixel {x},{y} of {result.Info.Specifier} ({result.Width}x{result.Height}): {SourceLoader.FormatColor(pixel)} rgb({pixel.R},{pixel.G},{pixel.B}) alpha {pixel.A} luma {sample.Luma}"));
            if (sample.Window is { } window)
            {
                context.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"mean of {window.Width}x{window.Height} at {window.X},{window.Y}: {SourceLoader.FormatColor(window.Mean)}"));
            }
        }
    }

    /// <summary>
    ///     Writes the fields that start the JSON document of both subcommands: the subcommand, the tool, the input, the
    ///     operations, and the size of the analyzed image.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="subcommand">The subcommand.</param>
    /// <param name="result">What the inspection found.</param>
    private static void WriteCommonFields(Utf8JsonWriter writer, string subcommand, InspectResult result)
    {
        var info = result.Info;
        writer.WriteString("subcommand", subcommand);

        writer.WriteStartObject("tool");
        writer.WriteString("name", "canvasnet");
        writer.WriteString("version", Program.Version);
        writer.WriteEndObject();

        writer.WriteStartObject("input");
        writer.WriteString("path", info.Path);
        if (info.Selection is { } selection)
        {
            writer.WriteString("fragment", selection.Fragment);
        }

        writer.WriteString("format", info.Format.ToFormatName());
        if (info.Format.IsDocument())
        {
            writer.WriteNumber("page", result.Page);
            writer.WriteNumber("pageCount", info.PageCount);
        }

        writer.WriteStartObject("natural");
        writer.WriteNumber("width", info.Width);
        writer.WriteNumber("height", info.Height);
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartArray("operations");
        foreach (var operation in result.Operations)
        {
            writer.WriteStartObject();
            writer.WriteString("op", operation.Op);
            foreach (var (name, value) in operation.Fields)
            {
                RenderCommand.WriteField(writer, name, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartObject("image");
        writer.WriteNumber("width", result.Width);
        writer.WriteNumber("height", result.Height);
        writer.WriteNumber("pixels", (long)result.Width * result.Height);
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes the luma statistics.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="luma">The statistics.</param>
    private static void WriteLuma(Utf8JsonWriter writer, LumaStatistics luma)
    {
        writer.WriteStartObject("luma");
        writer.WriteNumber("mean", luma.Mean);
        writer.WriteNumber("min", luma.Min);
        writer.WriteNumber("max", luma.Max);
        writer.WriteNumber("p1", luma.P1);
        writer.WriteNumber("p50", luma.P50);
        writer.WriteNumber("p99", luma.P99);
        writer.WriteNumber("otsu", luma.Otsu);
        writer.WriteStartArray("histogram");
        foreach (var count in luma.Histogram)
        {
            writer.WriteNumberValue(count);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes the diagnostics and the echo, the last fields of the JSON document of both subcommands.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="result">What the inspection found.</param>
    private static void WriteTail(Utf8JsonWriter writer, InspectResult result)
    {
        writer.WriteStartArray("diagnostics");
        foreach (var diagnostic in result.Diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("source", diagnostic.Source);
            writer.WriteString("severity", diagnostic.Severity);
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("message", diagnostic.Message);
            if (diagnostic.Page is { } page)
            {
                writer.WriteNumber("page", page);
            }

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
