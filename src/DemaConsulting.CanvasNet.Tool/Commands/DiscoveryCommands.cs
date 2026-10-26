// cspell:ignore canvasnet jpeg jpg tif tiff jp2 j2k j2c vsdx pptx
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The read-only discovery commands: the ones that tell a caller what the tool can do.
/// </summary>
/// <remarks>
///     Each command is a nested class implementing <see cref="ICommand"/>. As of this release the only
///     discovery command is <see cref="Formats"/>; commands that describe a given file are added here as they
///     are implemented. The table of formats is data owned by this unit.
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
    ///     The <c>formats</c> command: lists the input and output formats, the limits, and the versions.
    /// </summary>
    internal sealed class Formats : ICommand
    {
        /// <summary>The smallest JPEG quality setting.</summary>
        internal const int MinQuality = 1;

        /// <summary>The largest JPEG quality setting.</summary>
        internal const int MaxQuality = 100;

        /// <summary>The JPEG quality setting used when none is given.</summary>
        internal const int DefaultQuality = 90;

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
