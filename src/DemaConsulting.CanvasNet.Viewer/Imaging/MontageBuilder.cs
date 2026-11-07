// cspell:ignore canvasnet
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     What to compose: where to write, how the inputs are loaded, and the options of the sheet.
/// </summary>
/// <remarks>
///     The output path is the only required member; every other member is an optional init property whose default is
///     what the <c>canvasnet montage</c> command uses when the option is not given.
/// </remarks>
/// <example>
///     A sheet of three inputs in two columns with the file names as labels:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer.Imaging;
///
///     string[] files = ["logo.png", "diagram.svg", "deck.pptx"];
///     var infos = files.Select(SourceLoader.Probe).ToList();
///     var request = new MontageRequest("sheet.png") { Sheet = new SheetOptions { Columns = 2, Labels = true } };
///     var result = MontageBuilder.Execute(infos, request);
///     Console.WriteLine($"{result.Cells.Count} cells: {string.Join(", ", result.Cells.Select(cell => cell.Label))}");
///     </code>
/// </example>
/// <param name="Output">The path of the contact sheet; its extension selects the format unless <c>Write.Format</c> is set.</param>
public sealed record MontageRequest(string Output)
{
    /// <summary>
    ///     Gets the 1-based page of each document (the first page is 1), or <see langword="null"/> (the default) for page 1;
    ///     ignored with <see cref="AllPages"/>. A page that a document does not have is refused with the code
    ///     <c>page-out-of-range</c>. The format packages count from 0; do not pass them this number.
    /// </summary>
    public int? Page { get; init; }

    /// <summary>
    ///     Gets a value indicating whether every page of each document is added, in order, instead of one;
    ///     <see langword="false"/> by default.
    /// </summary>
    public bool AllPages { get; init; }

    /// <summary>
    ///     Gets the resolution of a document in dots per inch, or <see langword="null"/> (the default) for the library
    ///     default.
    /// </summary>
    public double? Dpi { get; init; }

    /// <summary>
    ///     Gets the options of an OpenSCAD model, or <see langword="null"/> (the default) when none were given.
    /// </summary>
    public ScadViewOptions? Scad { get; init; }

    /// <summary>
    ///     Gets a value indicating whether a feature the library cannot draw is an error; <see langword="false"/> by
    ///     default.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    ///     Gets a value indicating whether a JPEG is rotated to its EXIF orientation; <see langword="true"/> by default,
    ///     <see langword="false"/> leaves a JPEG as stored.
    /// </summary>
    public bool AutoOrient { get; init; } = true;

    /// <summary>
    ///     Gets the options of the sheet: columns, cell size, spacing, padding, background, labels, fit, and filter; a
    ///     <see cref="SheetOptions"/> with its defaults unless set.
    /// </summary>
    public SheetOptions Sheet { get; init; } = new();

    /// <summary>
    ///     Gets how the sheet is written (<see cref="WriteOptions.Format"/>, JPEG <see cref="WriteOptions.Quality"/>,
    ///     <see cref="WriteOptions.NoClobber"/>, <see cref="WriteOptions.FlattenBackground"/>);
    ///     <see cref="WriteOptions.Default"/> unless set, for example
    ///     <c>Write = WriteOptions.Default with { Quality = 85 }</c>.
    /// </summary>
    public WriteOptions Write { get; init; } = WriteOptions.Default;
}

/// <summary>
///     One cell of the sheet after it was drawn.
/// </summary>
/// <param name="Index">The 0-based index of the cell, in row-major order (the first cell is 0).</param>
/// <param name="InputIndex">The 0-based index of the input in the list that was given to <see cref="MontageBuilder.Execute"/>; a document with all pages gives several cells the same input index.</param>
/// <param name="Info">The facts about the input.</param>
/// <param name="Page">The 1-based page of the input (the first page is 1; 1 for a single image).</param>
/// <param name="X">The x position of the cell's image area.</param>
/// <param name="Y">The y position of the cell's image area.</param>
/// <param name="LoadedWidth">The width of the input as it was loaded into the cell.</param>
/// <param name="LoadedHeight">The height of the input as it was loaded into the cell.</param>
/// <param name="Label">The label of the cell: the file name of the input, followed by <c>#</c> and the fragment when the input had one, and by <c> p</c> and the 1-based page number for a page of a document (unless the fragment names the page). It is drawn only when <see cref="SheetOptions.Labels"/> is set.</param>
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
/// <remarks>
///     The sheet is written to the output; <paramref name="Written"/> says where. The result holds no pixels and has
///     nothing to dispose.
/// </remarks>
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
///         The inputs are probed by the caller (<see cref="SourceLoader.Probe(string)"/>, or <see cref="SourceLoader.Probe(InputReference)"/> for an input with a fragment). The operation does file I/O, holds no
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
    /// <param name="infos">The facts about the inputs, from <see cref="SourceLoader.Probe(string)"/> or <see cref="SourceLoader.Probe(InputReference)"/>, in the order they appear on the sheet; the fragment of an input overrides the request for that input.</param>
    /// <param name="request">What to compose.</param>
    /// <returns>The layout of the sheet, what was written, and what is reported about each cell.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when an argument is <see langword="null"/>, when <see cref="MontageRequest.Output"/>,
    ///     <see cref="MontageRequest.Sheet"/>, or <see cref="MontageRequest.Write"/> is <see langword="null"/>, or when an
    ///     element of <paramref name="infos"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="infos"/> is empty.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>page-out-of-range</c> when the page is not a page of a document, with code
    ///     <c>size-limit</c> when the sheet would be too large, with code <c>usage-error</c> when the output format cannot
    ///     be told (<see cref="WriteOptions.Format"/> wins over the extension of the output), with codes
    ///     <c>same-input-output</c>, <c>output-exists</c>, or <c>output-write-failed</c> when the
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

        // Refuse a request with a missing member, or a missing input, before anything is planned or loaded
        ArgumentNullException.ThrowIfNull(request.Output);
        ArgumentNullException.ThrowIfNull(request.Sheet);
        ArgumentNullException.ThrowIfNull(request.Write);
        for (var i = 0; i < infos.Count; i++)
        {
            if (infos[i] is null)
            {
                throw new ArgumentNullException(nameof(infos), $"Input {i + 1} of the list must not be null.");
            }
        }

        // Report every mistake about the pages and the output before any pixels are decoded
        var inputs = infos.Select(info => info.Path).ToList();
        var cells = PlanCells(request, infos);
        var layout = ContactSheet.CalculateLayout(cells.Count, request.Sheet);
        ImageWriter.ResolveFormat(request.Output, request.Write.Format?.ToFormatName());
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
            else if (info.Selection?.Page is { } named)
            {
                // A page that the fragment of the input names is the one cell of the input, whatever the request says
                SourceLoader.CheckPage(info, named);
                cells.Add(new PlannedCell(i, named));
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
    /// <returns>
    ///     The file name, followed by <c>#</c> and the fragment of the input when it had one (so the cells of one file are
    ///     told apart), and by <c> p</c> and the page number for a page of a document unless the fragment names the page.
    /// </returns>
    private static string CreateLabel(SourceInfo info, int page)
    {
        var name = Path.GetFileName(info.Path);
        if (info.Selection is { } selection)
        {
            name = $"{name}#{selection.Fragment}";
        }

        return info.Format.IsDocument() && info.Selection?.Page is null ? string.Create(CultureInfo.InvariantCulture, $"{name} p{page}") : name;
    }
}
