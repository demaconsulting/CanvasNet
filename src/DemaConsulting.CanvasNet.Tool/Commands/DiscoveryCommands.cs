// cspell:ignore canvasnet jpeg jpg tif tiff jp2 j2k j2c vsdx pptx
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The read-only discovery commands: the ones that tell a caller what the tool can do.
/// </summary>
/// <remarks>
///     Each command is a nested class implementing <see cref="ICommand"/>: <see cref="Formats"/> lists what the
///     tool can read and write, and <see cref="Info"/> describes one file. The table of formats is data owned by
///     this unit.
/// </remarks>
internal static class DiscoveryCommands
{
    /// <summary>
    ///     The input formats the tool reads, in the order they are listed.
    /// </summary>
    /// <remarks>
    ///     <c>Kind</c> is <c>raster</c> (decoded at its own size), <c>vector</c> (a drawing rendered at any
    ///     requested size), <c>document</c> (pages or slides rendered at any requested size), or <c>model</c>
    ///     (a 3D model rendered from a viewpoint).
    /// </remarks>
    internal static readonly IReadOnlyList<InputFormat> InputFormats =
    [
        new("png", "raster", [".png"], false, false),
        new("jpeg", "raster", [".jpg", ".jpeg"], false, false),
        new("bmp", "raster", [".bmp"], false, false),
        new("tiff", "raster", [".tif", ".tiff"], false, false),
        new("gif", "raster", [".gif"], false, false),
        new("jpeg2000", "raster", [".jp2", ".j2k", ".j2c"], false, false),
        new("svg", "vector", [".svg"], false, true),
        new("pdf", "document", [".pdf"], true, true),
        new("pptx", "document", [".pptx"], true, true),
        new("vsdx", "document", [".vsdx"], true, true),
        new("scad", "model", [".scad"], false, true)
    ];

    /// <summary>
    ///     The output formats the tool writes, in the order they are listed.
    /// </summary>
    internal static readonly IReadOnlyList<OutputFormatInfo> OutputFormats =
    [
        new("png", [".png"], true, false),
        new("jpeg", [".jpg", ".jpeg"], false, true),
        new("bmp", [".bmp"], true, false),
        new("tiff", [".tif", ".tiff"], true, false)
    ];

    /// <summary>
    ///     Describes an input format.
    /// </summary>
    /// <param name="Name">The format name, which is also its name in JSON output.</param>
    /// <param name="Kind">The kind: <c>raster</c>, <c>vector</c>, <c>document</c>, or <c>model</c>.</param>
    /// <param name="Extensions">The file name extensions, including the dot, in lower case.</param>
    /// <param name="MultiPage"><see langword="true"/> when one file holds several pages or slides that can be chosen.</param>
    /// <param name="Vector"><see langword="true"/> when the content is rendered at the requested size rather than decoded at its own size.</param>
    internal sealed record InputFormat(string Name, string Kind, IReadOnlyList<string> Extensions, bool MultiPage, bool Vector);

    /// <summary>
    ///     Describes an output format.
    /// </summary>
    /// <param name="Name">The format name, which is also its name in JSON output and in the <c>--format</c> option.</param>
    /// <param name="Extensions">The file name extensions, including the dot, in lower case.</param>
    /// <param name="Alpha"><see langword="true"/> when the format keeps transparency.</param>
    /// <param name="Lossy"><see langword="true"/> when the format is lossy and takes a quality setting.</param>
    internal sealed record OutputFormatInfo(string Name, IReadOnlyList<string> Extensions, bool Alpha, bool Lossy);

    /// <summary>
    ///     The <c>info</c> command: reports what a file is (its format, size, and properties) without rendering it.
    /// </summary>
    internal sealed class Info : ICommand
    {
        /// <summary>
        ///     The number of decimals the size of a page in points is rounded to.
        /// </summary>
        private const int PointDecimals = 2;

        /// <inheritdoc />
        public string Name => "info";

        /// <inheritdoc />
        public IReadOnlyList<string> Aliases => [];

        /// <inheritdoc />
        public string Summary => "Show the format, size, and properties of an image or drawing without rendering it";

        /// <inheritdoc />
        public IReadOnlyList<OptionSpec> Options =>
        [
            new("--page", [], true, "For a document, describe this page or slide, counted from 1 (default 1)", "N"),
            new("--all-pages", [], false, "For a document, describe every page or slide")
        ];

        /// <inheritdoc />
        public string Usage =>
            "canvasnet info <input> [--page N | --all-pages] [--json]" + Environment.NewLine
            + Environment.NewLine
            + "Reads only the file header: the format, the pixel size, the channels, whether it has transparency," + Environment.NewLine
            + "the number of frames of a GIF, and the EXIF orientation of a JPEG. An SVG reports its intrinsic size." + Environment.NewLine
            + "A PDF, PPTX, or VSDX document reports its number of pages and the size of a page (the first, or the" + Environment.NewLine
            + "one chosen with --page, or every page with --all-pages): in pixels at 96 dpi and in points, the" + Environment.NewLine
            + "rotation of a PDF page, and the name of a VSDX page. An OpenSCAD model is parsed (not evaluated) and" + Environment.NewLine
            + "reports the modules and functions it defines and its include and use directives." + Environment.NewLine
            + Environment.NewLine
            + "Examples:" + Environment.NewLine
            + "  canvasnet info photo.jpg" + Environment.NewLine
            + "  canvasnet info report.pdf --all-pages" + Environment.NewLine
            + "  canvasnet info part.scad --json" + Environment.NewLine
            + "  canvasnet info drawing.svg --json";

        /// <inheritdoc />
        public void Run(Context context, IReadOnlyList<string> args)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(args);

            var reader = ArgumentReader.Parse(Name, args, Options);
            reader.RequirePositionals(1, 1, "one input file");
            if (reader.Has("--page") && reader.Has("--all-pages"))
            {
                throw new ToolException(
                    ErrorCodes.UsageError,
                    "The options --page and --all-pages cannot be used together.",
                    "Use --page N for one page, or --all-pages for every page.");
            }

            var info = SourceLoader.Probe(reader.Positionals[0]);
            var page = reader.Get("--page") is { } pageText ? ArgumentReader.ParsePage(pageText, "--page") : 1;
            SourceLoader.CheckPage(info, page);
            var pages = info.Pages;
            if (info.Format.IsDocument() && !reader.Has("--all-pages"))
            {
                pages = [info.Pages[page - 1]];
            }

            context.WriteLine(context.Json
                ? ResultWriter.Success(Name, writer => WriteJsonFields(writer, info, pages))
                : RenderText(info, pages));
        }

        /// <summary>
        ///     Writes the command's fields of the JSON document.
        /// </summary>
        /// <param name="writer">The writer positioned inside the document's top-level object.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="pages">The pages of a document to describe.</param>
        private static void WriteJsonFields(Utf8JsonWriter writer, SourceInfo info, IReadOnlyList<SourcePage> pages)
        {
            writer.WriteStartObject("input");
            writer.WriteString("path", info.Path);
            writer.WriteString("format", info.Format.ToFormatName());
            writer.WriteNumber("bytes", info.Bytes);
            writer.WriteEndObject();

            if (info.Format.IsDocument())
            {
                WriteDocument(writer, info, pages);
                return;
            }

            if (info.Format.IsModel())
            {
                WriteModel(writer, info);
                return;
            }

            writer.WriteStartObject("image");
            writer.WriteNumber("width", info.Width);
            writer.WriteNumber("height", info.Height);
            writer.WriteNumber("channels", info.Channels);
            writer.WriteBoolean("hasAlpha", info.HasAlpha);
            writer.WriteBoolean("canDecode", info.CanDecode);
            writer.WriteNumber("frameCount", info.FrameCount);
            if (info.ExifOrientation is { } orientation)
            {
                writer.WriteNumber("exifOrientation", orientation);
            }

            writer.WriteEndObject();
        }

        /// <summary>
        ///     Writes the <c>document</c> object of a PDF, PPTX, or VSDX file.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="pages">The pages to describe.</param>
        /// <remarks>
        ///     A page has its number, its size in pixels at 96 dots per inch (<c>width</c>, <c>height</c>) and in
        ///     points (<c>widthPt</c>, <c>heightPt</c>), and, when the source has them, the <c>rotation</c> of a PDF
        ///     page and the <c>name</c> of a VSDX page. A slide of a presentation is a page.
        /// </remarks>
        private static void WriteDocument(Utf8JsonWriter writer, SourceInfo info, IReadOnlyList<SourcePage> pages)
        {
            writer.WriteStartObject("document");
            writer.WriteNumber("pageCount", info.PageCount);
            writer.WriteStartArray("pages");
            foreach (var page in pages)
            {
                writer.WriteStartObject();
                writer.WriteNumber("page", page.Number);
                writer.WriteNumber("width", page.Width);
                writer.WriteNumber("height", page.Height);
                writer.WriteNumber("widthPt", Math.Round(page.WidthPoints, PointDecimals));
                writer.WriteNumber("heightPt", Math.Round(page.HeightPoints, PointDecimals));
                if (page.Rotation is { } rotation)
                {
                    writer.WriteNumber("rotation", rotation);
                }

                if (page.Name != null)
                {
                    writer.WriteString("name", page.Name);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>
        ///     Writes the <c>model</c> object of an OpenSCAD file.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="info">The facts about the file.</param>
        /// <remarks>
        ///     The object lists what the source declares at file scope, in source order: the names of the <c>modules</c>
        ///     and <c>functions</c> it defines and its <c>directives</c> (<c>include</c> and <c>use</c>, each with the
        ///     <c>path</c> as written and the <c>line</c>). The model is parsed but not evaluated, so what it produces
        ///     is not reported.
        /// </remarks>
        private static void WriteModel(Utf8JsonWriter writer, SourceInfo info)
        {
            writer.WriteStartObject("model");
            writer.WriteStartArray("modules");
            foreach (var name in info.Modules)
            {
                writer.WriteStringValue(name);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("functions");
            foreach (var name in info.Functions)
            {
                writer.WriteStringValue(name);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("directives");
            foreach (var directive in info.Directives)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", directive.Kind);
                writer.WriteString("path", directive.Path);
                writer.WriteNumber("line", directive.Line);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>
        ///     Renders the text description of an OpenSCAD model.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <returns>The text: a line for the file with the counts, then a line for each module, function, and directive.</returns>
        private static string RenderModelText(SourceInfo info)
        {
            var builder = new StringBuilder();
            builder.Append(
                CultureInfo.InvariantCulture,
                $"{info.Path}: scad, {Count(info.Modules.Count, "module")}, {Count(info.Functions.Count, "function")}, {Count(info.Directives.Count, "include/use directive")}, {info.Bytes} bytes");
            foreach (var name in info.Modules)
            {
                builder.AppendLine();
                builder.Append(CultureInfo.InvariantCulture, $"  module {name}");
            }

            foreach (var name in info.Functions)
            {
                builder.AppendLine();
                builder.Append(CultureInfo.InvariantCulture, $"  function {name}");
            }

            foreach (var directive in info.Directives)
            {
                builder.AppendLine();
                builder.Append(CultureInfo.InvariantCulture, $"  {directive.Kind} <{directive.Path}> (line {directive.Line})");
            }

            return builder.ToString();
        }

        /// <summary>
        ///     Formats a count with its noun in the singular or plural.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <param name="noun">The noun in the singular.</param>
        /// <returns>The text, for example <c>1 module</c> or <c>2 modules</c>.</returns>
        private static string Count(int count, string noun)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");
        }

        /// <summary>
        ///     Renders the text description.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="pages">The pages of a document to describe.</param>
        /// <returns>
        ///     The text: one line for an image, for example
        ///     <c>photo.jpg: jpeg, 4032x3024 px, 3 channels, 2481920 bytes, EXIF orientation 6</c>; for a document one line
        ///     for the file and one for each page described.
        /// </returns>
        private static string RenderText(SourceInfo info, IReadOnlyList<SourcePage> pages)
        {
            if (info.Format.IsDocument())
            {
                return RenderDocumentText(info, pages);
            }

            if (info.Format.IsModel())
            {
                return RenderModelText(info);
            }

            var builder = new StringBuilder();
            builder.Append(CultureInfo.InvariantCulture, $"{info.Path}: {info.Format.ToFormatName()}, {info.Width}x{info.Height} px");
            builder.Append(info.Format == SourceKind.Svg ? " (intrinsic size)" : string.Empty);
            builder.Append(CultureInfo.InvariantCulture, $", {info.Channels} channel{(info.Channels == 1 ? string.Empty : "s")}");
            builder.Append(info.HasAlpha ? " with alpha" : string.Empty);
            builder.Append(CultureInfo.InvariantCulture, $", {info.Bytes} bytes");
            if (info.FrameCount > 1)
            {
                builder.Append(CultureInfo.InvariantCulture, $", {info.FrameCount} frames (the first is used)");
            }

            if (info.ExifOrientation is { } orientation)
            {
                builder.Append(CultureInfo.InvariantCulture, $", EXIF orientation {orientation}");
            }

            builder.Append(info.CanDecode ? string.Empty : ", cannot be decoded");
            return builder.ToString();
        }

        /// <summary>
        ///     Renders the text description of a document.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="pages">The pages to describe.</param>
        /// <returns>The text: a line for the file, then a line for each page.</returns>
        private static string RenderDocumentText(SourceInfo info, IReadOnlyList<SourcePage> pages)
        {
            var noun = info.Format == SourceKind.Pptx ? "slide" : "page";
            var builder = new StringBuilder();
            builder.Append(
                CultureInfo.InvariantCulture,
                $"{info.Path}: {info.Format.ToFormatName()}, {info.PageCount} {noun}{(info.PageCount == 1 ? string.Empty : "s")}, {info.Bytes} bytes");
            foreach (var page in pages)
            {
                builder.AppendLine();
                builder.Append(CultureInfo.InvariantCulture, $"  {noun} {page.Number}");
                builder.Append(page.Name == null ? string.Empty : $" \"{page.Name}\"");
                builder.Append(
                    CultureInfo.InvariantCulture,
                    $": {page.Width}x{page.Height} px at 96 dpi ({Math.Round(page.WidthPoints, PointDecimals)}x{Math.Round(page.HeightPoints, PointDecimals)} pt)");
                builder.Append(page.Rotation is { } rotation ? string.Create(CultureInfo.InvariantCulture, $", rotation {rotation}") : string.Empty);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    ///     The <c>formats</c> command: lists the input and output formats, the limits, and the versions.
    /// </summary>
    internal sealed class Formats : ICommand
    {
        /// <summary>The smallest JPEG quality setting.</summary>
        internal const int MinQuality = ImageWriter.MinQuality;

        /// <summary>The largest JPEG quality setting.</summary>
        internal const int MaxQuality = ImageWriter.MaxQuality;

        /// <summary>The JPEG quality setting used when none is given.</summary>
        internal const int DefaultQuality = ImageWriter.DefaultQuality;

        /// <summary>
        ///     The tool version to report.
        /// </summary>
        private readonly string _toolVersion;

        /// <summary>
        ///     The version of the CanvasNet library to report.
        /// </summary>
        private readonly string _libraryVersion;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Formats"/> class, reporting the versions of the running assemblies.
        /// </summary>
        public Formats()
            : this(Program.Version, GetLibraryVersion())
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Formats"/> class, reporting the given versions.
        /// </summary>
        /// <param name="toolVersion">The tool version to report.</param>
        /// <param name="libraryVersion">The CanvasNet library version to report.</param>
        /// <remarks>
        ///     Tests use this constructor to obtain output that does not depend on the build version.
        /// </remarks>
        internal Formats(string toolVersion, string libraryVersion)
        {
            _toolVersion = toolVersion;
            _libraryVersion = libraryVersion;
        }

        /// <inheritdoc />
        public string Name => "formats";

        /// <inheritdoc />
        public IReadOnlyList<string> Aliases => [];

        /// <inheritdoc />
        public string Summary => "List the image and document formats the tool reads and writes";

        /// <inheritdoc />
        public IReadOnlyList<OptionSpec> Options => [];

        /// <inheritdoc />
        public string Usage =>
            "canvasnet formats [--json]" + Environment.NewLine
            + Environment.NewLine
            + "Examples:" + Environment.NewLine
            + "  canvasnet formats" + Environment.NewLine
            + "  canvasnet formats --json";

        /// <inheritdoc />
        public void Run(Context context, IReadOnlyList<string> args)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(args);

            // The command takes no arguments of its own; anything else on the command line is a mistake
            var reader = ArgumentReader.Parse(Name, args, Options);
            reader.RequirePositionals(0, 0, "no arguments");

            context.WriteLine(context.Json
                ? ResultWriter.Success(Name, WriteJsonFields)
                : RenderText());
        }

        /// <summary>
        ///     Gets the version of the CanvasNet library the tool is built with.
        /// </summary>
        /// <returns>The informational version of the library assembly, or its assembly version.</returns>
        private static string GetLibraryVersion()
        {
            var assembly = typeof(Surface).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        }

        /// <summary>
        ///     Writes the command's fields of the JSON document.
        /// </summary>
        /// <param name="writer">The writer positioned inside the document's top-level object.</param>
        private void WriteJsonFields(Utf8JsonWriter writer)
        {
            writer.WriteStartObject("tool");
            writer.WriteString("name", "canvasnet");
            writer.WriteString("version", _toolVersion);
            writer.WriteEndObject();

            writer.WriteStartObject("library");
            writer.WriteString("name", "DemaConsulting.CanvasNet");
            writer.WriteString("version", _libraryVersion);
            writer.WriteEndObject();

            writer.WriteStartArray("inputs");
            foreach (var input in InputFormats)
            {
                writer.WriteStartObject();
                writer.WriteString("format", input.Name);
                writer.WriteString("kind", input.Kind);
                WriteExtensions(writer, input.Extensions);
                writer.WriteBoolean("multiPage", input.MultiPage);
                writer.WriteBoolean("vector", input.Vector);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("outputs");
            foreach (var output in OutputFormats)
            {
                writer.WriteStartObject();
                writer.WriteString("format", output.Name);
                WriteExtensions(writer, output.Extensions);
                writer.WriteBoolean("alpha", output.Alpha);
                writer.WriteBoolean("lossy", output.Lossy);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartObject("quality");
            writer.WriteNumber("min", MinQuality);
            writer.WriteNumber("max", MaxQuality);
            writer.WriteNumber("default", DefaultQuality);
            writer.WriteEndObject();

            writer.WriteNumber("maxDimension", Surface.MaxDimension);
        }

        /// <summary>
        ///     Writes the <c>extensions</c> array of a format.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="extensions">The extensions.</param>
        private static void WriteExtensions(Utf8JsonWriter writer, IReadOnlyList<string> extensions)
        {
            writer.WriteStartArray("extensions");
            foreach (var extension in extensions)
            {
                writer.WriteStringValue(extension);
            }

            writer.WriteEndArray();
        }

        /// <summary>
        ///     Renders the human-readable listing.
        /// </summary>
        /// <returns>The text, without a trailing newline.</returns>
        private string RenderText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Input formats:");
            AppendTable(
                builder,
                ["Format", "Kind", "Extensions", "Pages", "Scalable"],
                InputFormats.Select(f => new[]
                {
                    f.Name,
                    f.Kind,
                    string.Join(' ', f.Extensions),
                    f.MultiPage ? "multiple" : "single",
                    f.Vector ? "yes" : "no"
                }).ToList());

            builder.AppendLine();
            builder.AppendLine("Output formats:");
            AppendTable(
                builder,
                ["Format", "Extensions", "Alpha", "Quality"],
                OutputFormats.Select(f => new[]
                {
                    f.Name,
                    string.Join(' ', f.Extensions),
                    f.Alpha ? "yes" : "no",
                    f.Lossy ? "yes" : "no"
                }).ToList());

            builder.AppendLine();
            builder.AppendLine("Limits:");
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  Maximum image dimension: {Surface.MaxDimension} pixels"));
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  JPEG quality: {MinQuality}-{MaxQuality} (default {DefaultQuality})"));
            builder.AppendLine();
            builder.AppendLine("Versions:");
            builder.AppendLine($"  canvasnet tool: {_toolVersion}");
            builder.Append($"  CanvasNet library: {_libraryVersion}");
            return builder.ToString();
        }

        /// <summary>
        ///     Appends a table with aligned columns, indented by two spaces.
        /// </summary>
        /// <param name="builder">The builder to append to.</param>
        /// <param name="headers">The column headers.</param>
        /// <param name="rows">The rows, each with one cell per column.</param>
        private static void AppendTable(StringBuilder builder, string[] headers, IReadOnlyList<string[]> rows)
        {
            // Each column is as wide as its widest cell; the last column is not padded
            var widths = headers.Select((h, i) => Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();
            foreach (var row in rows.Prepend(headers))
            {
                var cells = row.Select((cell, i) => i == row.Length - 1 ? cell : cell.PadRight(widths[i]));
                builder.AppendLine("  " + string.Join("  ", cells));
            }
        }
    }
}
