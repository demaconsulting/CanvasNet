// cspell:ignore canvasnet jpeg tif tiff deskew
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The <c>render</c> command (alias <c>convert</c>): loads an image or drawing, and writes it as PNG, JPEG, BMP, or
///     TIFF.
/// </summary>
/// <remarks>
///     <para>
///         The command parses the command line, checks the options against the kind of the input, and prints the
///         result; the work - one pass of load, transform, and write - is <see cref="ImageRenderer"/> of the Viewer
///         library. Loading is <see cref="SourceLoader"/>: a vector
///         source (SVG, a Vega-Lite chart, or a page of a PDF, PPTX, or VSDX document) is rendered directly at the requested size, over
///         white unless a background is given; a raster source is decoded, turned upright by its EXIF orientation
///         (unless <c>--no-auto-orient</c>), and resized when a size is requested. Writing is
///         <see cref="ImageWriter"/>: the format comes from the extension or <c>--format</c>, JPEG is flattened over
///         the background, an existing output is overwritten unless <c>--no-clobber</c>, and the output may not be the
///         input. The result is one text line per output, or one JSON document with <c>--json</c>.
///     </para>
///     <para>
///         A document renders the page chosen with <c>--page</c> (the first by default), or every page with
///         <c>--all-pages</c>; with <c>--all-pages</c> the output names a directory or contains <c>{page}</c>, which
///         is replaced by the page number. Everything that can be checked before pixels are decoded (the input, the
///         pages, the format, and every output path) is checked first, and the document is opened once for all pages.
///     </para>
///     <para>
///         The image pipeline (<see cref="ImagePipeline"/>, read from the command line by
///         <see cref="ImagePipelineParser"/>) runs between the load and the write: the operations
///         <c>--crop</c>, <c>--resize</c>, <c>--fit</c>, <c>--rotate</c>, <c>--flip</c>, <c>--pad</c>, and the photo tools
///         <c>--grayscale</c>, <c>--invert</c>, <c>--threshold</c>, <c>--levels</c>, <c>--brightness</c>,
///         <c>--contrast</c>, <c>--auto-levels</c>, <c>--deskew</c>, <c>--trim</c>, and <c>--grid</c> are applied to
///         the loaded pixels in the order they were written, so one decode and one encode serve the whole chain. The
///         verbs <c>crop</c>, <c>resize</c>, <c>rotate</c>, <c>flip</c>, and <c>pad</c> are this same class constructed
///         with a verb: the verb's positional operand becomes the first operation. The size options of the source
///         (<c>--scale</c>, <c>--width</c>, <c>--height</c>, <c>--size</c>) are handled by the loader, so they act
///         before the first operation.
///     </para>
///     <para>
///         The class is split into <c>RenderCommand.Options.cs</c> (the option declarations and the reading of the
///         settings), <c>RenderCommand.Verbs.cs</c> (the verbs), and this file (the run).
///     </para>
/// </remarks>
internal sealed partial class RenderCommand : ICommand
{
    /// <inheritdoc />
    public string Name => _verb ?? "render";

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases => _verb == null ? ["convert"] : [];

    /// <inheritdoc />
    public string Summary => _verb == null
        ? "Render or convert an image, drawing, or document page to PNG, JPEG, BMP, or TIFF, with optional transformations"
        : VerbTexts[_verb].Summary;

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options => OptionTable;

    /// <inheritdoc />
    public string Usage => _verb == null ? RenderUsage : VerbUsage(_verb);

    /// <summary>
    ///     Gets the usage text of the <c>render</c> command.
    /// </summary>
    private static string RenderUsage =>
        "canvasnet render <input> -o <output> [options]" + Environment.NewLine
        + Environment.NewLine
        + "Reads PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, SVG, PDF, PPTX, VSDX, SCAD, or Vega-Lite (.vl.json, .vl) and" + Environment.NewLine
        + "writes the format of the output" + Environment.NewLine
        + "extension. Vector sources are flattened over white; a JPEG is turned upright by its EXIF orientation." + Environment.NewLine
        + "A PDF page, PPTX slide, or VSDX page is 96 dpi by default; choose it with --page and the size with --dpi." + Environment.NewLine
        + "An OpenSCAD model is drawn from one viewpoint (--view, --azimuth, --elevation, --projection) at 800x800" + Environment.NewLine
        + "pixels unless a size is given (a model that is entirely 2D is drawn from the top unless a view is chosen);" + Environment.NewLine
        + "-D sets its variables. Its include and use directives are read from the" + Environment.NewLine
        + "folder of the model and below it only, and one that names a missing file is an error." + Environment.NewLine
        + "A Vega-Lite chart is drawn at the size its specification lays out (--scale, --width, and --height resize it" + Environment.NewLine
        + "sharply) over its own background, white by default, with UTC dates and the bundled fonts. Its local data" + Environment.NewLine
        + "files (CSV, TSV, DSV with format.delimiter, or JSON named by a relative url) are read from" + Environment.NewLine
        + "the folder of the specification and below it only, and one that is missing is an error; there is" + Environment.NewLine
        + "no data-folder option, and nothing is read from the network. Delimited text is not type-inferred" + Environment.NewLine
        + "unless its format.parse is \"auto\". A feature that is not supported yet is reported (an error with --strict)." + Environment.NewLine
        + "An SVG drawing can embed a picture, model, chart, SVG file, or document page that sits beside it with" + Environment.NewLine
        + "<image href=\"part.scad\" .../>; run 'canvasnet help svg' for the file types, the folder rule, and an example." + Environment.NewLine
        + "The operations --crop, --resize, --fit, --rotate, --flip, --pad, and the photo tools --grayscale, --invert," + Environment.NewLine
        + "--threshold, --levels, --brightness, --contrast, --auto-levels, --deskew, --trim, and --grid (repeatable) are" + Environment.NewLine
        + "applied in the order written, after the source is loaded and sized; crop, resize, rotate, flip, and pad are" + Environment.NewLine
        + "commands of their own. --rotate takes any angle; --deskew and --json report the tilt that was found." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet render drawing.svg -o drawing.png" + Environment.NewLine
        + "  canvasnet render drawing.svg --width 1200 -o drawing.png" + Environment.NewLine
        + "  canvasnet render report.pdf --page 2 --dpi 150 -o page2.png" + Environment.NewLine
        + "  canvasnet render slides.pptx --all-pages -o out/slide-{page}.png" + Environment.NewLine
        + "  canvasnet render part.scad --view top -D width=30 -o part-top.png" + Environment.NewLine
        + "  canvasnet render part.scad --azimuth 30 --elevation 20 --projection perspective -o part.png" + Environment.NewLine
        + "  canvasnet render chart.vl.json --scale 2 -o chart.png" + Environment.NewLine
        + "  canvasnet render photo.jpg --crop 10%,10%,80%,80% --resize 800x600 -o small.png" + Environment.NewLine
        + "  canvasnet render logo.png --fit 256x256 --pad 8 --pad-color white -o icon.png" + Environment.NewLine
        + "  canvasnet render napkin.jpg --fit 2048x2048 --auto-levels --deskew --trim -o sketch.png --json" + Environment.NewLine
        + "  canvasnet render sketch.png --grayscale --threshold auto -o bw.png" + Environment.NewLine
        + "  canvasnet render sketch.png --grid 100 -o sketch-grid.png" + Environment.NewLine
        + "  canvasnet render shot.png -o shot.jpg --quality 85 --json" + Environment.NewLine
        + "  canvasnet convert scan.tif -o scan.png --no-clobber";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        var reader = ArgumentReader.Parse(Name, args, Options);
        var settings = ReadSettings(reader, _verb);

        // Report every mistake about the input, the pages, and the outputs before any pixels are decoded
        var info = SourceLoader.Probe(settings.Input);
        if (settings.Source.Scad != null && !info.Format.IsModel())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {string.Join(", ", ScadOptionNames)} only apply to an OpenSCAD model, but '{settings.Input}' is a {info.Format.ToFormatName()} file.",
                "Remove them, or render a .scad file.");
        }

        // The operation reports the mistakes about the pages and the outputs before any pixels are decoded, then
        // loads, transforms, and writes every page; everything below only formats what it returns
        var result = ImageRenderer.Execute(
            info, new RenderRequest(settings.Output, settings.AllPages, settings.Source, settings.Write, settings.Operations));
        var outputs = result.Pages;

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(Name, writer => WriteJsonFields(writer, info, settings, outputs)));
        }
        else
        {
            foreach (var output in outputs)
            {
                context.WriteLine(RenderText(output));
                foreach (var warning in RenderWarnings(output))
                {
                    context.WriteWarning(warning);
                }
            }

            // What the loader left out of the picture is not silent: it goes to standard error after the result line
            context.WriteDiagnostics(
                outputs.SelectMany(o => o.Diagnostics).Select(d => ((string?)settings.Input, d)),
                outputs.SelectMany(o => o.Echo));
        }
    }

    /// <summary>
    ///     Renders the one-line text result of one page.
    /// </summary>
    /// <param name="output">The page that was written.</param>
    /// <returns>
    ///     The text, for example <c>wrote out.png (816x1056 png, 41208 bytes) from in.svg</c>; a page of a document
    ///     adds <c>page N</c>.
    /// </returns>
    private static string RenderText(RenderedPage output)
    {
        var result = output.Result;
        var page = output.Info.Format.IsDocument() ? $" page {output.Page}" : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"wrote {result.Path} ({result.Width}x{result.Height} {result.Format.ToFormatName()}, {result.Bytes} bytes) from {output.Info.Path}{page}");
    }

    /// <summary>
    ///     Renders non-fatal warnings recorded by image operations.
    /// </summary>
    /// <param name="output">The rendered page.</param>
    /// <returns>Warning lines for operation reports that explain a skipped best-effort operation.</returns>
    private static IEnumerable<string> RenderWarnings(RenderedPage output)
    {
        foreach (var operation in output.Operations.Where(o => o.Op == "deskew"))
        {
            var reason = operation.Fields.FirstOrDefault(f => f.Key == "reason").Value;
            if (reason is string text)
            {
                yield return $"warning: deskew was not applied: {text}";
            }
        }
    }

    /// <summary>
    ///     Writes the command's fields of the JSON document.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="source">The facts about the input file.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="outputs">The pages that were written, in order.</param>
    /// <remarks>
    ///     The <c>input</c> object has <c>page</c> and <c>pageCount</c> for a document (<c>page</c> only when one page
    ///     was rendered), and <c>natural</c> is the size of the first page rendered. <c>operations</c>,
    ///     <c>outputs</c>, and <c>diagnostics</c> list every page; each output and each note of a document names its
    ///     page. For an OpenSCAD model (and only for one) <c>anchors</c> follows <c>echo</c> (see <see cref="WriteAnchors"/>).
    /// </remarks>
    private static void WriteJsonFields(Utf8JsonWriter writer, SourceInfo source, Settings settings, IReadOnlyList<RenderedPage> outputs)
    {
        var isDocument = source.Format.IsDocument();

        writer.WriteStartObject("tool");
        writer.WriteString("name", "canvasnet");
        writer.WriteString("version", Program.Version);
        writer.WriteEndObject();

        writer.WriteStartObject("input");
        writer.WriteString("path", source.Path);
        writer.WriteString("format", source.Format.ToFormatName());
        if (isDocument)
        {
            if (!settings.AllPages)
            {
                writer.WriteNumber("page", outputs[0].Page);
            }

            writer.WriteNumber("pageCount", source.PageCount);
        }

        writer.WriteStartObject("natural");
        writer.WriteNumber("width", outputs[0].Info.Width);
        writer.WriteNumber("height", outputs[0].Info.Height);
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartArray("operations");
        foreach (var operation in outputs.SelectMany(o => o.Operations))
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
        foreach (var output in outputs)
        {
            var result = output.Result;
            writer.WriteStartObject();
            writer.WriteString("path", result.Path);
            writer.WriteString("format", result.Format.ToFormatName());
            writer.WriteNumber("width", result.Width);
            writer.WriteNumber("height", result.Height);
            writer.WriteNumber("bytes", result.Bytes);
            if (isDocument)
            {
                writer.WriteNumber("page", output.Page);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("diagnostics");
        foreach (var diagnostic in outputs.SelectMany(o => o.Diagnostics))
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
        foreach (var line in outputs.SelectMany(o => o.Echo))
        {
            writer.WriteStringValue(line);
        }

        writer.WriteEndArray();

        // Only a model declares anchors, so every other kind keeps its document exactly as it was
        if (source.Format.IsModel())
        {
            WriteAnchors(writer, outputs);
        }
    }

    /// <summary>
    ///     Writes the <c>anchors</c> array: the anchors a model declared, with their positions in the written image.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="outputs">The pages that were written; a model has one.</param>
    /// <remarks>
    ///     Each anchor has <c>name</c>, <c>point</c> (the world <c>[x, y, z]</c>), <c>pixel</c> (the continuous
    ///     <c>[x, y]</c> in the written image, origin top-left, rounded to three decimals, or <see langword="null"/>
    ///     when the point cannot be located) and <c>inside</c> (whether the pixel lies within the written image). The
    ///     anchors are in echo order, duplicates included; the array is empty when the model declares none.
    /// </remarks>
    private static void WriteAnchors(Utf8JsonWriter writer, IReadOnlyList<RenderedPage> outputs)
    {
        writer.WriteStartArray("anchors");
        foreach (var output in outputs)
        {
            foreach (var anchor in output.Anchors)
            {
                writer.WriteStartObject();
                writer.WriteString("name", anchor.Name);
                writer.WriteStartArray("point");
                writer.WriteNumberValue(anchor.X);
                writer.WriteNumberValue(anchor.Y);
                writer.WriteNumberValue(anchor.Z);
                writer.WriteEndArray();
                if (anchor.PixelX is { } x && anchor.PixelY is { } y)
                {
                    writer.WriteStartArray("pixel");
                    writer.WriteNumberValue(Math.Round(x, 3));
                    writer.WriteNumberValue(Math.Round(y, 3));
                    writer.WriteEndArray();
                    writer.WriteBoolean("inside", x >= 0 && x < output.Result.Width && y >= 0 && y < output.Result.Height);
                }
                else
                {
                    writer.WriteNull("pixel");
                    writer.WriteBoolean("inside", false);
                }

                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes one parameter of an operation.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value: a string, an integer, a number, a flag, or a list of strings.</param>
    internal static void WriteField(Utf8JsonWriter writer, string name, object value)
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
            case long big:
                writer.WriteNumber(name, big);
                break;
            case bool flag:
                writer.WriteBoolean(name, flag);
                break;
            case IReadOnlyList<string> list:
                writer.WriteStartArray(name);
                foreach (var item in list)
                {
                    writer.WriteStringValue(item);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException($"The operation parameter '{name}' has an unsupported type {value.GetType().Name}.");
        }
    }
}
