// cspell:ignore canvasnet luma otsu deskew
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

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
///         pipeline), and finally analyzes the pixels with <see cref="ImageAnalysis"/>.
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
        + "default) before they are counted." + Environment.NewLine
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

        // Report every mistake about the input before any pixels are decoded
        var info = SourceLoader.Probe(settings.Input);
        if (settings.Source.Scad != null && !info.Format.IsModel())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {string.Join(", ", RenderCommand.ScadOptionNames)} only apply to an OpenSCAD model, but '{settings.Input}' is a {info.Format.ToFormatName()} file.",
                "Remove them, or inspect a .scad file.");
        }

        SourceLoader.CheckPage(info, settings.Source.Page);

        using var loaded = SourceLoader.Load(info, settings.Source);
        Surface? transformed = null;
        try
        {
            var surface = loaded.Surface;
            IReadOnlyList<AppliedOperation> applied = [];
            if (settings.Operations.Count > 0)
            {
                var defaultPad = settings.Source.Background ?? (loaded.IsVector ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 0));
                transformed = ImagePipeline.Apply(surface, settings.Operations, defaultPad, out applied, settings.Source.Background);
                surface = transformed;
            }

            var operations = loaded.Operations.Concat(applied).ToList();
            if (settings.Subcommand == "colors")
            {
                RunColors(context, settings, loaded, surface, operations);
            }
            else
            {
                RunPixel(context, settings, loaded, surface, operations);
            }
        }
        finally
        {
            transformed?.Dispose();
        }
    }

    /// <summary>
    ///     Runs <c>inspect colors</c> and reports the result.
    /// </summary>
    /// <param name="context">The context that receives the output.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="loaded">The loaded page.</param>
    /// <param name="surface">The pixels after the pipeline.</param>
    /// <param name="operations">What was done to the source to produce the pixels.</param>
    private static void RunColors(Context context, Settings settings, LoadedSource loaded, Surface surface, IReadOnlyList<AppliedOperation> operations)
    {
        var background = AnalysisBackground(settings.Source.Background);
        var statistics = ImageAnalysis.Analyze(surface, background, settings.Count);

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(CommandName, writer =>
            {
                WriteCommonFields(writer, "colors", loaded, surface, operations);
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
                WriteTail(writer, loaded);
            }));
        }
        else
        {
            var colors = string.Join(
                ", ",
                statistics.Colors.Select(c => string.Create(CultureInfo.InvariantCulture, $"{SourceLoader.FormatColor(c.Color)} {c.Fraction * 100:0.00}%")));
            context.WriteLine(string.Create(CultureInfo.InvariantCulture, $"colors of {loaded.Info.Path} ({surface.Width}x{surface.Height}): {colors}"));
            var luma = statistics.Luma;
            context.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"luma: mean {luma.Mean:0.00}, min {luma.Min}, max {luma.Max}, p1 {luma.P1}, p50 {luma.P50}, p99 {luma.P99}, otsu {luma.Otsu}"));
        }
    }

    /// <summary>
    ///     Runs <c>inspect pixel</c> and reports the result.
    /// </summary>
    /// <param name="context">The context that receives the output.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="loaded">The loaded page.</param>
    /// <param name="surface">The pixels after the pipeline.</param>
    /// <param name="operations">What was done to the source to produce the pixels.</param>
    private static void RunPixel(Context context, Settings settings, LoadedSource loaded, Surface surface, IReadOnlyList<AppliedOperation> operations)
    {
        var (x, y) = settings.Point!.Value;
        var sample = ImageAnalysis.Sample(surface, x, y, settings.Radius);
        var pixel = sample.Pixel;

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(CommandName, writer =>
            {
                WriteCommonFields(writer, "pixel", loaded, surface, operations);
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

                WriteTail(writer, loaded);
            }));
        }
        else
        {
            context.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"pixel {x},{y} of {loaded.Info.Path} ({surface.Width}x{surface.Height}): {SourceLoader.FormatColor(pixel)} rgb({pixel.R},{pixel.G},{pixel.B}) alpha {pixel.A} luma {sample.Luma}"));
            if (sample.Window is { } window)
            {
                context.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"mean of {window.Width}x{window.Height} at {window.X},{window.Y}: {SourceLoader.FormatColor(window.Mean)}"));
            }
        }
    }

    /// <summary>
    ///     Gets the color that translucent pixels are flattened over before they are analyzed.
    /// </summary>
    /// <param name="background">The <c>--background</c> color, or <see langword="null"/> when none was given.</param>
    /// <returns>
    ///     The background when it is opaque; white when there is none; and a translucent background flattened over white
    ///     (<c>(c * a + 255 * (255 - a) + 127) / 255</c> for each channel, so a transparent background is white).
    /// </returns>
    private static Rgba32 AnalysisBackground(Rgba32? background)
    {
        if (background is not { } color)
        {
            return new Rgba32(255, 255, 255, 255);
        }

        if (color.A == 255)
        {
            return color;
        }

        return new Rgba32(
            FlattenOverWhite(color.R, color.A),
            FlattenOverWhite(color.G, color.A),
            FlattenOverWhite(color.B, color.A),
            255);
    }

    /// <summary>
    ///     Flattens one channel over white.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The channel of the flattened color.</returns>
    private static byte FlattenOverWhite(int channel, int alpha)
    {
        return (byte)(((channel * alpha) + (255 * (255 - alpha)) + 127) / 255);
    }

    /// <summary>
    ///     Writes the fields that start the JSON document of both subcommands: the subcommand, the tool, the input, the
    ///     operations, and the size of the analyzed image.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="subcommand">The subcommand.</param>
    /// <param name="loaded">The loaded page.</param>
    /// <param name="surface">The pixels after the pipeline.</param>
    /// <param name="operations">What was done to the source to produce the pixels.</param>
    private static void WriteCommonFields(Utf8JsonWriter writer, string subcommand, LoadedSource loaded, Surface surface, IReadOnlyList<AppliedOperation> operations)
    {
        var info = loaded.Info;
        writer.WriteString("subcommand", subcommand);

        writer.WriteStartObject("tool");
        writer.WriteString("name", "canvasnet");
        writer.WriteString("version", Program.Version);
        writer.WriteEndObject();

        writer.WriteStartObject("input");
        writer.WriteString("path", info.Path);
        writer.WriteString("format", info.Format.ToFormatName());
        if (info.Format.IsDocument())
        {
            writer.WriteNumber("page", loaded.Page);
            writer.WriteNumber("pageCount", info.PageCount);
        }

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
                RenderCommand.WriteField(writer, name, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartObject("image");
        writer.WriteNumber("width", surface.Width);
        writer.WriteNumber("height", surface.Height);
        writer.WriteNumber("pixels", (long)surface.Width * surface.Height);
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
    /// <param name="loaded">The loaded page.</param>
    private static void WriteTail(Utf8JsonWriter writer, LoadedSource loaded)
    {
        writer.WriteStartArray("diagnostics");
        foreach (var diagnostic in loaded.Diagnostics)
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
}
