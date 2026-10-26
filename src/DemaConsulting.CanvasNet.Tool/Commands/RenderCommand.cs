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
///         source (SVG, or a page of a PDF, PPTX, or VSDX document) is rendered directly at the requested size, over
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
///         The image pipeline (<see cref="ImagePipeline"/>) runs between the load and the write: the operations
///         <c>--crop</c>, <c>--resize</c>, <c>--fit</c>, <c>--rotate</c>, <c>--flip</c>, and <c>--pad</c> are applied to
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
    /// <summary>
    ///     The text in an output name that is replaced by the page number.
    /// </summary>
    private const string PagePlaceholder = "{page}";

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
        + "Reads PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, SVG, PDF, PPTX, VSDX, or SCAD and writes the format of the output" + Environment.NewLine
        + "extension. Vector sources are flattened over white; a JPEG is turned upright by its EXIF orientation." + Environment.NewLine
        + "A PDF page, PPTX slide, or VSDX page is 96 dpi by default; choose it with --page and the size with --dpi." + Environment.NewLine
        + "An OpenSCAD model is drawn from one viewpoint (--view, --azimuth, --elevation, --projection) at 800x800" + Environment.NewLine
        + "pixels unless a size is given; -D sets its variables. Its include and use directives are read from the" + Environment.NewLine
        + "folder of the model and below it only, and one that names a missing file is an error." + Environment.NewLine
        + "The operations --crop, --resize, --fit, --rotate, --flip, and --pad (repeatable) are applied in the order" + Environment.NewLine
        + "written, after the source is loaded and sized; crop, resize, rotate, flip, and pad are commands of their own." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet render drawing.svg -o drawing.png" + Environment.NewLine
        + "  canvasnet render drawing.svg --width 1200 -o drawing.png" + Environment.NewLine
        + "  canvasnet render report.pdf --page 2 --dpi 150 -o page2.png" + Environment.NewLine
        + "  canvasnet render slides.pptx --all-pages -o out/slide-{page}.png" + Environment.NewLine
        + "  canvasnet render part.scad --view top -D width=30 -o part-top.png" + Environment.NewLine
        + "  canvasnet render part.scad --azimuth 30 --elevation 20 --projection perspective -o part.png" + Environment.NewLine
        + "  canvasnet render photo.jpg --crop 10%,10%,80%,80% --resize 800x600 -o small.png" + Environment.NewLine
        + "  canvasnet render logo.png --fit 256x256 --pad 8 --pad-color white -o icon.png" + Environment.NewLine
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
        var pages = settings.AllPages ? Enumerable.Range(1, info.PageCount).ToList() : [settings.Source.Page];
        SourceLoader.CheckPage(info, pages[0]);
        var targets = PlanTargets(settings, info, pages);
        ImageWriter.ResolveFormat(targets[0].Path, settings.Write.Format?.ToFormatName());
        foreach (var (_, target) in targets)
        {
            ImageWriter.CheckTarget(target, settings.Input, settings.Write.NoClobber);
        }

        // Each page is loaded, run through the image pipeline (crop, resize, rotate, and so on), and encoded to a
        // temporary file next to its target. No target is touched until every page has been rendered and encoded, so a
        // render or encode failure on any page leaves no new or partial output and every existing output untouched.
        // The outputs are then published all or nothing: if a move fails (for example a target is no longer
        // writable), the targets already published are rolled back and the error is reported.
        var outputs = new List<PageOutput>();
        var staged = new List<ImageWriter.StagedOutput>();
        try
        {
            using (var source = SourceLoader.OpenPages(info, settings.Source))
            {
                foreach (var (page, target) in targets)
                {
                    using var loaded = source.ReadPage(page);
                    var (output, pending) = TransformAndStage(loaded, target, settings, info.Format.IsDocument());
                    staged.Add(pending);
                    outputs.Add(output);
                }
            }

            ImageWriter.PublishAll(staged);
        }
        finally
        {
            // Disposing removes the temporary file of every output that was not moved into place
            foreach (var pending in staged)
            {
                pending.Dispose();
            }
        }

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(Name, writer => WriteJsonFields(writer, info, settings, outputs)));
        }
        else
        {
            foreach (var output in outputs)
            {
                context.WriteLine(RenderText(output));
            }
        }
    }

    /// <summary>
    ///     One page that was loaded and written, with what is reported about it.
    /// </summary>
    /// <param name="Page">The 1-based page number.</param>
    /// <param name="Info">The facts about the source; the size is the natural size of the page.</param>
    /// <param name="Diagnostics">The notes reported while loading.</param>
    /// <param name="Echo">The text a model printed while it was evaluated.</param>
    /// <param name="Operations">What was done to the source to produce the output.</param>
    /// <param name="Result">What was written.</param>
    private sealed record PageOutput(
        int Page,
        SourceInfo Info,
        IReadOnlyList<ImageDiagnostic> Diagnostics,
        IReadOnlyList<string> Echo,
        IReadOnlyList<AppliedOperation> Operations,
        WriteResult Result)
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PageOutput"/> record from a loaded page.
        /// </summary>
        /// <param name="loaded">The loaded page.</param>
        /// <param name="operations">What was done to the source to produce the output.</param>
        /// <param name="result">What was written.</param>
        public PageOutput(LoadedSource loaded, IReadOnlyList<AppliedOperation> operations, WriteResult result)
            : this(loaded.Page, loaded.Info, loaded.Diagnostics, loaded.Echo, operations, result)
        {
        }
    }

    /// <summary>
    ///     Decides which output file each page is written to.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="info">The facts about the input.</param>
    /// <param name="pages">The 1-based pages to render.</param>
    /// <returns>The page and the output path of each.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when every page is rendered but the output would be the same file for
    ///     each of them.
    /// </exception>
    /// <remarks>
    ///     <c>{page}</c> in the output is replaced by the page number. With <c>--all-pages</c> an output that is an
    ///     existing directory, or ends with a directory separator, names a directory: each page is written there as
    ///     the input's file name without extension, a dash, and the page number, with the extension of the format.
    /// </remarks>
    private static List<(int Page, string Path)> PlanTargets(Settings settings, SourceInfo info, IReadOnlyList<int> pages)
    {
        var template = settings.Output;
        if (settings.AllPages && IsDirectoryOutput(template))
        {
            var extension = (settings.Write.Format ?? OutputFormat.Png) switch
            {
                OutputFormat.Jpeg => ".jpg",
                OutputFormat.Bmp => ".bmp",
                OutputFormat.Tiff => ".tif",
                _ => ".png"
            };
            template = Path.Combine(template, Path.GetFileNameWithoutExtension(settings.Input) + "-{page}" + extension);
        }
        else if (settings.AllPages && info.PageCount > 1 && !template.Contains(PagePlaceholder, StringComparison.Ordinal))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The output '{settings.Output}' would be the same file for every page.",
                "With --all-pages, put {page} in the output name, for example out-{page}.png, or name a directory.");
        }

        return pages
            .Select(page => (page, template.Replace(PagePlaceholder, page.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)))
            .ToList();
    }

    /// <summary>
    ///     Determines whether an output names a directory.
    /// </summary>
    /// <param name="output">The output path.</param>
    /// <returns><see langword="true"/> when it is an existing directory or ends with a directory separator.</returns>
    private static bool IsDirectoryOutput(string output)
    {
        return Directory.Exists(output)
               || output.EndsWith('/')
               || output.EndsWith(Path.DirectorySeparatorChar)
               || output.EndsWith(Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    ///     Runs the image pipeline over a loaded page and stages the result next to its output.
    /// </summary>
    /// <param name="loaded">The loaded page; it is not disposed.</param>
    /// <param name="target">The output path of the page.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="isDocument"><see langword="true"/> when the source is a document.</param>
    /// <returns>What was loaded, done to it, and will be written, and the staged output that publishes it.</returns>
    /// <remarks>
    ///     The pipeline makes a new surface for every step and disposes the previous one, so at most two full-size
    ///     surfaces exist at a time (plus the writer's flattened copy of a JPEG). A border that has no color of its
    ///     own is the background the user gave, else white for a vector source (which is flattened over white), else
    ///     transparent.
    /// </remarks>
    private static (PageOutput Output, ImageWriter.StagedOutput Staged) TransformAndStage(
        LoadedSource loaded, string target, Settings settings, bool isDocument)
    {
        Surface? transformed = null;
        try
        {
            var surface = loaded.Surface;
            IReadOnlyList<AppliedOperation> applied = [];
            if (settings.Operations.Count > 0)
            {
                var defaultPad = settings.Source.Background ?? (loaded.IsVector ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 0));
                transformed = ImagePipeline.Apply(surface, settings.Operations, defaultPad, out applied);
                surface = transformed;
            }

            var staged = ImageWriter.Stage(surface, target, settings.Input, settings.Write);
            var result = staged.Result;
            return (new PageOutput(loaded, CollectOperations(loaded, applied, result, settings, isDocument), result), staged);
        }
        finally
        {
            transformed?.Dispose();
        }
    }

    /// <summary>
    ///     Lists what was done to a loaded page to produce its output.
    /// </summary>
    /// <param name="loaded">The loaded page.</param>
    /// <param name="applied">What the image pipeline did, in order.</param>
    /// <param name="result">What was written.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="isDocument"><see langword="true"/> when the source is a document.</param>
    /// <returns>
    ///     The operations of the loader, then those of the pipeline, then the flattening of a JPEG when the writer did
    ///     it; when every page of a document is rendered each operation names its page.
    /// </returns>
    private static List<AppliedOperation> CollectOperations(
        LoadedSource loaded,
        IReadOnlyList<AppliedOperation> applied,
        WriteResult result,
        Settings settings,
        bool isDocument)
    {
        var operations = loaded.Operations.Concat(applied).ToList();
        if (result.Flattened)
        {
            operations.Add(SourceLoader.Operation("flatten", ("background", SourceLoader.FormatColor(settings.Write.FlattenBackground))));
        }

        if (settings.AllPages && isDocument)
        {
            return operations
                .Select(o => o.Fields.Any(f => f.Key == "page")
                    ? o
                    : o with { Fields = [.. o.Fields, new KeyValuePair<string, object>("page", loaded.Page)] })
                .ToList();
        }

        return operations;
    }

    /// <summary>
    ///     Renders the one-line text result of one page.
    /// </summary>
    /// <param name="output">The page that was written.</param>
    /// <returns>
    ///     The text, for example <c>wrote out.png (816x1056 png, 41208 bytes) from in.svg</c>; a page of a document
    ///     adds <c>page N</c>.
    /// </returns>
    private static string RenderText(PageOutput output)
    {
        var result = output.Result;
        var page = output.Info.Format.IsDocument() ? $" page {output.Page}" : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"wrote {result.Path} ({result.Width}x{result.Height} {result.Format.ToFormatName()}, {result.Bytes} bytes) from {output.Info.Path}{page}");
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
    ///     page.
    /// </remarks>
    private static void WriteJsonFields(Utf8JsonWriter writer, SourceInfo source, Settings settings, IReadOnlyList<PageOutput> outputs)
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
