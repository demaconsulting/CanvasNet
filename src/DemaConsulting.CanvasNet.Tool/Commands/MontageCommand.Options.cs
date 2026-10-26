// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The option declarations and option parsing of <see cref="MontageCommand"/>.
/// </summary>
internal sealed partial class MontageCommand
{
    /// <summary>The default width and height of the image area of a cell.</summary>
    internal const int DefaultCellSize = 256;

    /// <summary>The default space between cells.</summary>
    internal const int DefaultSpacing = 8;

    /// <summary>The default space around the sheet.</summary>
    internal const int DefaultPadding = 8;

    /// <summary>
    ///     The options of the command, in the order they are listed in the help.
    /// </summary>
    /// <remarks>
    ///     The source options (<c>--page</c>, <c>--dpi</c>, the options of an OpenSCAD model, <c>--no-auto-orient</c>,
    ///     <c>--strict</c>, <c>--filter</c>) and the output options are the declarations of
    ///     <see cref="RenderCommand.OptionTable"/>, so the grammar and the help text cannot drift; they apply to every
    ///     input an option concerns and are skipped for the others.
    /// </remarks>
    internal static readonly IReadOnlyList<OptionSpec> OptionTable =
    [
        new("--output", ["-o"], true, "The file the contact sheet is written to; its extension selects the format", "file"),
        new("--columns", [], true, $"Number of columns, 1-{ContactSheet.MaxColumns} (default: as square as possible)", "N"),
        new("--cell", [], true, $"Size of the image area of each cell (default {DefaultCellSize}x{DefaultCellSize}, each side at least {ContactSheet.MinCell})", "WxH"),
        new("--fit", [], true, "How an image is fitted into its cell: contain (default; keeps the aspect ratio) or stretch", "mode"),
        new("--spacing", [], true, $"Space between cells in pixels (default {DefaultSpacing})", "N"),
        new("--padding", [], true, $"Space around the sheet in pixels (default {DefaultPadding})", "N"),
        new("--background", [], true, "Opaque color of the sheet: white (default), black, #RRGGBB, or a color name", "color"),
        new("--labels", [], false, "Write the file name (and the page of a document) below each cell"),
        .. RenderCommand.SpecsFor("--page"),
        new("--all-pages", [], false, "Add every page or slide of a document, in order, instead of one"),
        .. RenderCommand.SpecsFor("--dpi", "--no-auto-orient", "--strict"),
        .. RenderCommand.SpecsFor(RenderCommand.ScadOptionNames),
        .. RenderCommand.SpecsFor(ImagePipeline.FilterOption, "--quality", "--no-clobber")
    ];

    /// <summary>
    ///     The settings of one invocation, read from the command line.
    /// </summary>
    /// <param name="Inputs">The input paths, in the order given.</param>
    /// <param name="Output">The output path.</param>
    /// <param name="Page">The page of each document, or <see langword="null"/> for the first.</param>
    /// <param name="AllPages"><see langword="true"/> to add every page of each document.</param>
    /// <param name="Dpi">The resolution of a document, or <see langword="null"/> for the default.</param>
    /// <param name="Scad">The options of an OpenSCAD model, or <see langword="null"/> when none was given.</param>
    /// <param name="Strict"><see langword="true"/> to fail on a feature the library cannot draw.</param>
    /// <param name="AutoOrient"><see langword="false"/> to leave a JPEG as stored.</param>
    /// <param name="Sheet">The options of the sheet.</param>
    /// <param name="Write">How the sheet is written.</param>
    internal sealed record Settings(
        IReadOnlyList<string> Inputs,
        string Output,
        int? Page,
        bool AllPages,
        float? Dpi,
        ScadViewOptions? Scad,
        bool Strict,
        bool AutoOrient,
        SheetOptions Sheet,
        WriteOptions Write);

    /// <summary>
    ///     Reads the settings from the parsed command line.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when there is no input or no output, a value is malformed or out of range,
    ///     the background is not opaque, or <c>--page</c> and <c>--all-pages</c> are combined.
    /// </exception>
    private static Settings ReadSettings(ArgumentReader reader)
    {
        reader.RequirePositionals(1, int.MaxValue, "one or more input files");

        var output = reader.Get("--output")
                     ?? throw new ToolException(
                         ErrorCodes.UsageError,
                         "No output file was given.",
                         "Add -o <file>, for example: canvasnet montage a.png b.png -o sheet.png");

        if (reader.Has("--page") && reader.Has("--all-pages"))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "The options --page and --all-pages cannot be used together.",
                "Use --page N for one page of each document, or --all-pages for every page.");
        }

        var background = reader.Get("--background") is { } colorText
            ? ArgumentReader.ParseColor(colorText, "--background")
            : new Rgba32(255, 255, 255, 255);
        if (background.A != 255)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "The --background of a contact sheet must be opaque.",
                "Use a color without transparency, for example white or #808080.");
        }

        var (cellWidth, cellHeight) = (DefaultCellSize, DefaultCellSize);
        if (reader.Get("--cell") is { } cellText)
        {
            (cellWidth, cellHeight) = ArgumentReader.ParseSize(cellText, "--cell");
            if (cellWidth < ContactSheet.MinCell || cellHeight < ContactSheet.MinCell)
            {
                throw new ToolException(
                    ErrorCodes.UsageError,
                    $"'{cellText}' is not a value of --cell: each side must be at least {ContactSheet.MinCell} pixels.",
                    "Use a size such as 256x256.");
            }
        }

        var filter = reader.Get(ImagePipeline.FilterOption) is { } filterText
            ? ImagePipeline.ParseFilter(filterText, ImagePipeline.FilterOption)
            : ResamplingMode.Auto;
        var sheet = new SheetOptions(
            reader.GetInt("--columns", 1, ContactSheet.MaxColumns) ?? 0,
            cellWidth,
            cellHeight,
            reader.GetInt("--spacing", 0, Surface.MaxDimension) ?? DefaultSpacing,
            reader.GetInt("--padding", 0, Surface.MaxDimension) ?? DefaultPadding,
            background,
            reader.Has("--labels"),
            reader.Get("--fit") is { } fitText ? CompareCommand.ParseFit(fitText, "--fit") : FitMode.Contain,
            filter);
        var quality = reader.GetInt("--quality", ImageWriter.MinQuality, ImageWriter.MaxQuality) ?? ImageWriter.DefaultQuality;

        return new Settings(
            reader.Positionals,
            output,
            reader.Get("--page") is { } pageText ? ArgumentReader.ParsePage(pageText, "--page") : null,
            reader.Has("--all-pages"),
            reader.Get("--dpi") is { } dpiText ? ArgumentReader.ParseDpi(dpiText, "--dpi") : null,
            RenderCommand.ReadScadOptions(reader),
            reader.Has("--strict"),
            !reader.Has("--no-auto-orient"),
            sheet,
            new WriteOptions(null, quality, reader.Has("--no-clobber"), background));
    }
}
