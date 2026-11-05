// cspell:ignore canvasnet
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     What to compose: where to write, how the inputs are loaded, and the options of the sheet.
/// </summary>
/// <param name="Output">The path of the contact sheet; its extension selects the format.</param>
/// <param name="Page">The page of each document, or <see langword="null"/> for the first; ignored with <paramref name="AllPages"/>.</param>
/// <param name="AllPages"><see langword="true"/> to add every page of each document, in order, instead of one.</param>
/// <param name="Dpi">The resolution of a document in dots per inch, or <see langword="null"/> for the default.</param>
/// <param name="Scad">The options of an OpenSCAD model, or <see langword="null"/> when none were given.</param>
/// <param name="Strict"><see langword="true"/> to fail on a feature the library cannot draw.</param>
/// <param name="AutoOrient"><see langword="false"/> to leave a JPEG as stored.</param>
/// <param name="Sheet">The options of the sheet: columns, cell size, spacing, padding, background, labels, fit, and filter.</param>
/// <param name="Write">How the sheet is written.</param>
public sealed record MontageRequest(
    string Output,
    int? Page,
    bool AllPages,
    double? Dpi,
    ScadViewOptions? Scad,
    bool Strict,
    bool AutoOrient,
    SheetOptions Sheet,
    WriteOptions Write);

/// <summary>
///     One cell of the sheet after it was drawn.
/// </summary>
/// <param name="Index">The 0-based index of the cell.</param>
/// <param name="InputIndex">The index of the input in the list that was given.</param>
/// <param name="Info">The facts about the input.</param>
/// <param name="Page">The 1-based page of the input.</param>
/// <param name="X">The x position of the cell's image area.</param>
/// <param name="Y">The y position of the cell's image area.</param>
/// <param name="LoadedWidth">The width of the input as it was loaded into the cell.</param>
/// <param name="LoadedHeight">The height of the input as it was loaded into the cell.</param>
/// <param name="Label">The label of the cell.</param>
/// <param name="Diagnostics">The notes reported while loading.</param>
/// <param name="Echo">The text a model printed while it was evaluated.</param>
public sealed record MontageCell(
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
///     What a montage produced.
/// </summary>
/// <param name="Layout">The geometry of the sheet.</param>
/// <param name="Written">What was written.</param>
/// <param name="Cells">The cells, in order.</param>
public sealed record MontageResult(SheetLayout Layout, WriteResult Written, IReadOnlyList<MontageCell> Cells);

/// <summary>
///     The montage operation: composes images, and pages of documents, into one contact sheet - the library form of
///     <c>canvasnet montage</c>.
/// </summary>
/// <remarks>
///     <para>
///         Every input is one cell of the sheet, in the order given; a document contributes its page (the first by
///         default), or with all pages every page in order. The cells are laid out row by row by
///         <see cref="ContactSheet"/>, which refuses a sheet that would exceed the maximum size with <c>size-limit</c>
///         before any input is loaded (with all pages only after the page counts have been read, which does not decode
///         anything). The options shared by all inputs are skipped for an input they do not concern.
///     </para>
///     <para>
///         The sheet is composed as a stream: each input is loaded into its cell (a vector input is rendered at the cell
///         size, a raster input is fitted into it), added to the sheet, and disposed before the next one is loaded, so
///         the memory in use is the sheet and one input. With labels the file name (and <c>p</c> and the page number of
///         a document page) is drawn below each cell in the bundled font. The sheet is staged and moved into place only
///         when it is complete, and it may not be the same file as any input.
///     </para>
///     <para>
///         The inputs are probed by the caller (<see cref="SourceLoader.Probe"/>). The operation does file I/O, holds no
///         state, and is safe to call from several threads for different outputs.
///     </para>
/// </remarks>
public static class MontageBuilder
{
    /// <summary>
    ///     One cell of the sheet before it is loaded.
    /// </summary>
    /// <param name="InputIndex">The index of the input in the list that was given.</param>
    /// <param name="Page">The 1-based page of the input.</param>
    private sealed record PlannedCell(int InputIndex, int Page);

    /// <summary>
    ///     Composes a contact sheet.
    /// </summary>
    /// <param name="infos">The facts about the inputs, from <see cref="SourceLoader.Probe"/>, in the order they appear on the sheet.</param>
    /// <param name="request">What to compose.</param>
    /// <returns>The layout of the sheet, what was written, and what is reported about each cell.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="infos"/> is empty.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>page-out-of-range</c> when the page is not a page of a document, with code
    ///     <c>size-limit</c> when the sheet would be too large, with code <c>usage-error</c> when the output format cannot
    ///     be told, with codes <c>same-input-output</c>, <c>output-exists</c>, or <c>output-write-failed</c> when the
    ///     sheet cannot be written, and with the codes of <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> when
    ///     an input cannot be loaded.
    /// </exception>
    public static MontageResult Execute(IReadOnlyList<SourceInfo> infos, MontageRequest request)
    {
        ArgumentNullException.ThrowIfNull(infos);
        ArgumentNullException.ThrowIfNull(request);
        if (infos.Count == 0)
        {
            throw new ArgumentException("A montage needs at least one input.", nameof(infos));
        }

        // Report every mistake about the pages and the output before any pixels are decoded
        var inputs = infos.Select(info => info.Path).ToList();
        var cells = PlanCells(request, infos);
        var layout = ContactSheet.CalculateLayout(cells.Count, request.Sheet);
        ImageWriter.ResolveFormat(request.Output, null);
        ImageWriter.CheckTargets([request.Output], inputs, request.Write.NoClobber);

        var placed = new List<MontageCell>();
        ImageWriter.StagedOutput staged;
        using (var sheet = new ContactSheet(cells.Count, request.Sheet))
        {
            // Each input is opened once for all of its cells; each cell is loaded, added, and disposed in turn
            var index = 0;
            foreach (var group in cells.GroupBy(cell => cell.InputIndex))
            {
                var info = infos[group.Key];
                var options = CreateSourceOptions(request, info);
                using var source = SourceLoader.OpenPages(
                    info, SourceLoader.BoxOptions(info, options, layout.CellWidth, layout.CellHeight));
                foreach (var cell in group)
                {
                    using var loaded = source.ReadPage(cell.Page);
                    var label = CreateLabel(info, cell.Page);
                    var (x, y) = sheet.Add(loaded.Surface, label);
                    placed.Add(new MontageCell(index, cell.InputIndex, info, cell.Page, x, y, loaded.Surface.Width, loaded.Surface.Height, label, loaded.Diagnostics, loaded.Echo));
                    index++;
                }
            }

            using var surface = sheet.Detach();
            staged = ImageWriter.Stage(surface, request.Output, inputs[0], request.Write);
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

        return new MontageResult(layout, written, placed);
    }

    /// <summary>
    ///     Decides which cells the sheet has.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="infos">The facts about the inputs, in the order given.</param>
    /// <returns>One cell per single image, and one per page of a document (every page with all pages, else one).</returns>
    /// <exception cref="ViewerException">Thrown with code <c>page-out-of-range</c> when the page is not a page of a document.</exception>
    private static List<PlannedCell> PlanCells(MontageRequest request, IReadOnlyList<SourceInfo> infos)
    {
        var cells = new List<PlannedCell>();
        for (var i = 0; i < infos.Count; i++)
        {
            var info = infos[i];
            if (!info.Format.IsDocument())
            {
                cells.Add(new PlannedCell(i, 1));
            }
            else if (request.AllPages)
            {
                cells.AddRange(Enumerable.Range(1, info.PageCount).Select(page => new PlannedCell(i, page)));
            }
            else
            {
                var page = request.Page ?? 1;
                SourceLoader.CheckPage(info, page);
                cells.Add(new PlannedCell(i, page));
            }
        }

        return cells;
    }

    /// <summary>
    ///     Creates the loading options of one input.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="info">The facts about the input.</param>
    /// <returns>The options; the shared options that do not concern the kind of the input are left out.</returns>
    private static SourceOptions CreateSourceOptions(MontageRequest request, SourceInfo info)
    {
        return new SourceOptions
        {
            Dpi = info.Format.IsDocument() ? request.Dpi : null,
            Strict = request.Strict,
            Background = request.Sheet.Background,
            AutoOrient = request.AutoOrient,
            Filter = request.Sheet.Filter,
            Scad = info.Format.IsModel() ? request.Scad : null
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
}
