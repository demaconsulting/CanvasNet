// cspell:ignore canvasnet
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The <c>montage</c> command: composes images, and pages of documents, into one contact sheet.
/// </summary>
/// <remarks>
///     <para>
///         Every input is one cell of the sheet, in the order given; a document contributes its <c>--page</c> (the first
///         by default), or with <c>--all-pages</c> every page in order. The cells are laid out row by row by
///         <see cref="ContactSheet"/>, which refuses a sheet that would exceed the maximum size with <c>size-limit</c>
///         before any input is loaded (with <c>--all-pages</c> only after the page counts have been read, which does not
///         decode anything). The options shared by all inputs (<c>--page</c>, <c>--dpi</c>, the options of an OpenSCAD
///         model, and so on) are skipped for an input they do not concern.
///     </para>
///     <para>
///         The sheet is composed as a stream: each input is loaded into its cell (a vector input is rendered at the cell
///         size, a raster input is fitted into it), added to the sheet, and disposed before the next one is loaded, so
///         the memory in use is the sheet and one input. With <c>--labels</c> the file name (and <c>p</c> and the page
///         number of a document page) is drawn below each cell in the bundled font. The sheet is staged and moved into
///         place only when it is complete, and it may not be the same file as any input.
///     </para>
///     <para>
///         The class is split into <c>MontageCommand.Options.cs</c> (the option declarations and the reading of the
///         settings) and this file (the run, the text result, and the JSON result).
///     </para>
/// </remarks>
internal sealed partial class MontageCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "montage";

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases => [];

    /// <inheritdoc />
    public string Summary => "Compose images and document pages into one contact sheet, optionally labeled";

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options => OptionTable;

    /// <inheritdoc />
    public string Usage =>
        "canvasnet montage <inputs...> -o <output> [options]" + Environment.NewLine
        + Environment.NewLine
        + "Reads images, drawings, or document pages like 'render' and writes one contact sheet with a cell for each," + Environment.NewLine
        + "in the order given, row by row. A document contributes its --page (the first by default), or every page" + Environment.NewLine
        + "with --all-pages. Each image is fitted into a --cell (256x256 by default; a vector input is rendered at the" + Environment.NewLine
        + "cell size) and --labels writes its file name below it (with its fragment, so cells of one file differ). The" + Environment.NewLine
        + "sheet is at most 8192 pixels on a side." + Environment.NewLine
        + "An input may end in a fragment that overrides the shared options for that input: part.scad#def.explode=0.5," + Environment.NewLine
        + "report.pdf#page=3, chart.vl.json#param.t=5, flow.mmd#theme=dark (quote it in a shell; add def.$vpd=200 to a" + Environment.NewLine
        + "model so that every cell keeps one scale). A fragment is the only way to set a chart parameter or a Mermaid" + Environment.NewLine
        + "theme here (montage has no --param or --theme). A '#' is a fragment only when no file has that exact name." + Environment.NewLine
        + "--file-access and --allow-root apply to every input (roots allows the folder of every input by default)." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet montage a.png b.png c.png -o sheet.png" + Environment.NewLine
        + "  canvasnet montage \"part.scad#def.explode=0\" \"part.scad#def.explode=1\" --labels -o explode.png" + Environment.NewLine
        + "  canvasnet montage deck.pptx --all-pages --labels -o deck-sheet.png" + Environment.NewLine
        + "  canvasnet montage *.png --columns 4 --cell 200x150 --background \"#333333\" --labels -o gallery.png" + Environment.NewLine
        + "  canvasnet montage report.pdf part.scad logo.svg --labels -o overview.jpg --json";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        var reader = ArgumentReader.Parse(Name, args, Options);
        var settings = ReadSettings(reader);

        // Without --all-pages the number of cells is the number of inputs, so a sheet that is too large is refused
        // before any input is even opened
        if (!settings.AllPages)
        {
            ContactSheet.CalculateLayout(settings.Inputs.Count, settings.Sheet);
        }

        // Report every mistake about the inputs, the pages, and the output before any pixels are decoded
        var infos = settings.Inputs.Select(text => SourceLoader.Probe(InputReference.Parse(text), settings.FileAccess)).ToList();
        if (settings.Scad != null && !infos.Any(info => info.Format.IsModel()))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {string.Join(", ", RenderCommand.ScadOptionNames)} only apply to an OpenSCAD model, but no input is one.",
                "Remove them, or add a .scad file.");
        }

        // The operation reports the mistakes about the pages and the output before any pixels are decoded, then composes
        // and writes the sheet; everything below only formats what it returns
        var montage = MontageBuilder.Execute(
            infos,
            new MontageRequest(settings.Output)
            {
                Page = settings.Page,
                AllPages = settings.AllPages,
                Dpi = settings.Dpi,
                Scad = settings.Scad,
                Strict = settings.Strict,
                AutoOrient = settings.AutoOrient,
                Sheet = settings.Sheet,
                Write = settings.Write,
                FileAccess = settings.FileAccess
            });
        var layout = montage.Layout;
        var placed = montage.Cells;
        var written = montage.Written;

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(Name, writer => WriteJsonFields(writer, settings, layout, placed, written)));
        }
        else
        {
            context.WriteLine(RenderText(layout, written));
            context.WriteDiagnostics(
                placed.SelectMany(cell => cell.Diagnostics.Select(d => (
                    (string?)cell.Info.Specifier,
                    cell.Info.Format.IsDocument() ? d with { Page = d.Page ?? cell.Page } : d))),
                placed.SelectMany(cell => cell.Echo));
        }
    }

    /// <summary>
    ///     Renders the one-line text result.
    /// </summary>
    /// <param name="layout">The geometry of the sheet.</param>
    /// <param name="written">What was written.</param>
    /// <returns>The text, for example <c>wrote sheet.png (1048x552 png, 52000 bytes): 6 images in 3 columns</c>.</returns>
    private static string RenderText(SheetLayout layout, WriteResult written)
    {
        var images = layout.Count == 1 ? "1 image" : $"{layout.Count} images";
        var columns = layout.Columns == 1 ? "1 column" : $"{layout.Columns} columns";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"wrote {written.Path} ({written.Width}x{written.Height} {written.Format.ToFormatName()}, {written.Bytes} bytes): {images} in {columns}");
    }

    /// <summary>
    ///     Writes the command's fields of the JSON document.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="layout">The geometry of the sheet.</param>
    /// <param name="cells">The cells, in order.</param>
    /// <param name="written">What was written.</param>
    /// <remarks>
    ///     <c>page</c> and <c>pageCount</c> of a cell are given for a document only, and <c>label</c> only with
    ///     <c>--labels</c>; a note of a document names its page.
    /// </remarks>
    private static void WriteJsonFields(
        Utf8JsonWriter writer, Settings settings, SheetLayout layout, IReadOnlyList<MontageCell> cells, WriteResult written)
    {
        writer.WriteStartObject("tool");
        writer.WriteString("name", "canvasnet");
        writer.WriteString("version", Program.Version);
        writer.WriteEndObject();

        writer.WriteStartObject("sheet");
        writer.WriteNumber("width", layout.Width);
        writer.WriteNumber("height", layout.Height);
        writer.WriteNumber("columns", layout.Columns);
        writer.WriteNumber("rows", layout.Rows);
        writer.WriteNumber("cellWidth", layout.CellWidth);
        writer.WriteNumber("cellHeight", layout.CellHeight);
        writer.WriteNumber("labelHeight", layout.LabelHeight);
        writer.WriteNumber("spacing", layout.Spacing);
        writer.WriteNumber("padding", layout.Padding);
        writer.WriteString("fit", settings.Sheet.Fit.ToString().ToLowerInvariant());
        writer.WriteBoolean("labels", settings.Sheet.Labels);
        writer.WriteString("background", SourceLoader.FormatColor(settings.Sheet.Background));
        writer.WriteEndObject();

        writer.WriteStartArray("cells");
        foreach (var cell in cells)
        {
            writer.WriteStartObject();
            writer.WriteNumber("index", cell.Index);
            writer.WriteNumber("column", cell.Index % layout.Columns);
            writer.WriteNumber("row", cell.Index / layout.Columns);
            writer.WriteNumber("x", cell.X);
            writer.WriteNumber("y", cell.Y);
            writer.WriteString("input", cell.Info.Path);
            if (cell.Info.Selection is { } selection)
            {
                writer.WriteString("fragment", selection.Fragment);
            }

            writer.WriteString("format", cell.Info.Format.ToFormatName());
            if (cell.Info.Format.IsDocument())
            {
                writer.WriteNumber("page", cell.Page);
                writer.WriteNumber("pageCount", cell.Info.PageCount);
            }

            writer.WriteNumber("loadedWidth", cell.LoadedWidth);
            writer.WriteNumber("loadedHeight", cell.LoadedHeight);
            if (settings.Sheet.Labels)
            {
                writer.WriteString("label", cell.Label);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("outputs");
        writer.WriteStartObject();
        writer.WriteString("path", written.Path);
        writer.WriteString("format", written.Format.ToFormatName());
        writer.WriteNumber("width", written.Width);
        writer.WriteNumber("height", written.Height);
        writer.WriteNumber("bytes", written.Bytes);
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WriteStartArray("diagnostics");
        foreach (var cell in cells)
        {
            foreach (var diagnostic in cell.Diagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("input", cell.Info.Path);
                if (cell.Info.Selection is { } fragmentSelection)
                {
                    writer.WriteString("fragment", fragmentSelection.Fragment);
                }
                if (cell.Info.Format.IsDocument())
                {
                    writer.WriteNumber("page", diagnostic.Page ?? cell.Page);
                }

                writer.WriteString("source", diagnostic.Source);
                writer.WriteString("severity", diagnostic.Severity);
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("message", diagnostic.Message);
                ResultWriter.WriteDiagnosticExtras(writer, diagnostic);
                writer.WriteEndObject();
            }
        }

        writer.WriteEndArray();

        writer.WriteStartArray("echo");
        foreach (var line in cells.SelectMany(cell => cell.Echo))
        {
            writer.WriteStringValue(line);
        }

        writer.WriteEndArray();
    }
}
