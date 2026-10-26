// cspell:ignore canvasnet
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

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
        + "cell size) and --labels writes its file name below it. The sheet is at most 8192 pixels on a side." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet montage a.png b.png c.png -o sheet.png" + Environment.NewLine
        + "  canvasnet montage deck.pptx --all-pages --labels -o deck-sheet.png" + Environment.NewLine
        + "  canvasnet montage *.png --columns 4 --cell 200x150 --background #333333 --labels -o gallery.png" + Environment.NewLine
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
        var infos = settings.Inputs.Select(SourceLoader.Probe).ToList();
        if (settings.Scad != null && !infos.Any(info => info.Format.IsModel()))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {string.Join(", ", RenderCommand.ScadOptionNames)} only apply to an OpenSCAD model, but no input is one.",
                "Remove them, or add a .scad file.");
        }

        var cells = PlanCells(settings, infos);
        var layout = ContactSheet.CalculateLayout(cells.Count, settings.Sheet);
        ImageWriter.ResolveFormat(settings.Output, null);
        ImageWriter.CheckTargets([settings.Output], settings.Inputs, settings.Write.NoClobber);

        var placed = new List<PlacedCell>();
        ImageWriter.StagedOutput staged;
        using (var sheet = new ContactSheet(cells.Count, settings.Sheet))
        {
            // Each input is opened once for all of its cells; each cell is loaded, added, and disposed in turn
            var index = 0;
            foreach (var group in cells.GroupBy(cell => cell.InputIndex))
            {
                var info = infos[group.Key];
                var options = CreateSourceOptions(settings, info);
                using var source = SourceLoader.OpenPages(
                    info, SourceLoader.BoxOptions(info, options, layout.CellWidth, layout.CellHeight));
                foreach (var cell in group)
                {
                    using var loaded = source.ReadPage(cell.Page);
                    var label = CreateLabel(info, cell.Page);
                    var (x, y) = sheet.Add(loaded.Surface, label);
                    placed.Add(new PlacedCell(index, cell.InputIndex, info, cell.Page, x, y, loaded.Surface.Width, loaded.Surface.Height, label, loaded.Diagnostics, loaded.Echo));
                    index++;
                }
            }

            using var surface = sheet.Detach();
            staged = ImageWriter.Stage(surface, settings.Output, settings.Inputs[0], settings.Write);
        }

        WriteResult written;
        try
        {
            written = staged.Commit();
        }
        finally
        {
            staged.Dispose();
        }

        if (context.Json)
        {
            context.WriteLine(ResultWriter.Success(Name, writer => WriteJsonFields(writer, settings, layout, placed, written)));
        }
        else
        {
            context.WriteLine(RenderText(layout, written));
        }
    }

    /// <summary>
    ///     One cell of the sheet before it is loaded.
    /// </summary>
    /// <param name="InputIndex">The index of the input in the command line.</param>
    /// <param name="Page">The 1-based page of the input.</param>
    private sealed record PlannedCell(int InputIndex, int Page);

    /// <summary>
    ///     One cell of the sheet after it was drawn.
    /// </summary>
    /// <param name="Index">The 0-based index of the cell.</param>
    /// <param name="InputIndex">The index of the input in the command line.</param>
    /// <param name="Info">The facts about the input.</param>
    /// <param name="Page">The 1-based page of the input.</param>
    /// <param name="X">The x position of the cell's image area.</param>
    /// <param name="Y">The y position of the cell's image area.</param>
    /// <param name="LoadedWidth">The width of the input as it was loaded into the cell.</param>
    /// <param name="LoadedHeight">The height of the input as it was loaded into the cell.</param>
    /// <param name="Label">The label of the cell.</param>
    /// <param name="Diagnostics">The notes reported while loading.</param>
    /// <param name="Echo">The text a model printed while it was evaluated.</param>
    private sealed record PlacedCell(
        int Index,
        int InputIndex,
        SourceInfo Info,
        int Page,
        int X,
        int Y,
        int LoadedWidth,
        int LoadedHeight,
        string Label,
        IReadOnlyList<ImageDiagnostic> Diagnostics,
        IReadOnlyList<string> Echo);

    /// <summary>
    ///     Decides which cells the sheet has.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="infos">The facts about the inputs, in the order given.</param>
    /// <returns>One cell per single image, and one per page of a document (every page with <c>--all-pages</c>, else one).</returns>
    /// <exception cref="ToolException">Thrown with code <c>page-out-of-range</c> when <c>--page</c> is not a page of a document.</exception>
    private static List<PlannedCell> PlanCells(Settings settings, IReadOnlyList<SourceInfo> infos)
    {
        var cells = new List<PlannedCell>();
        for (var i = 0; i < infos.Count; i++)
        {
            var info = infos[i];
            if (!info.Format.IsDocument())
            {
                cells.Add(new PlannedCell(i, 1));
            }
            else if (settings.AllPages)
            {
                cells.AddRange(Enumerable.Range(1, info.PageCount).Select(page => new PlannedCell(i, page)));
            }
            else
            {
                var page = settings.Page ?? 1;
                SourceLoader.CheckPage(info, page);
                cells.Add(new PlannedCell(i, page));
            }
        }

        return cells;
    }

    /// <summary>
    ///     Creates the loading options of one input.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="info">The facts about the input.</param>
    /// <returns>The options; the shared options that do not concern the kind of the input are left out.</returns>
    private static SourceOptions CreateSourceOptions(Settings settings, SourceInfo info)
    {
        return new SourceOptions
        {
            Dpi = info.Format.IsDocument() ? settings.Dpi : null,
            Strict = settings.Strict,
            Background = settings.Sheet.Background,
            AutoOrient = settings.AutoOrient,
            Filter = settings.Sheet.Filter,
            Scad = info.Format.IsModel() ? settings.Scad : null
        };
    }

    /// <summary>
    ///     Creates the label of a cell.
    /// </summary>
    /// <param name="info">The facts about the input.</param>
    /// <param name="page">The 1-based page.</param>
    /// <returns>The file name, followed by <c> p</c> and the page number for a page of a document.</returns>
    private static string CreateLabel(SourceInfo info, int page)
    {
        var name = Path.GetFileName(info.Path);
        return info.Format.IsDocument() ? string.Create(CultureInfo.InvariantCulture, $"{name} p{page}") : name;
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
        Utf8JsonWriter writer, Settings settings, SheetLayout layout, IReadOnlyList<PlacedCell> cells, WriteResult written)
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
                if (cell.Info.Format.IsDocument())
                {
                    writer.WriteNumber("page", diagnostic.Page ?? cell.Page);
                }

                writer.WriteString("source", diagnostic.Source);
                writer.WriteString("severity", diagnostic.Severity);
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("message", diagnostic.Message);
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
