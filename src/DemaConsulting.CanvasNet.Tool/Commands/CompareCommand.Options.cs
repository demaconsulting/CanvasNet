// cspell:ignore canvasnet psnr rmse ssim
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The option declarations and option parsing of <see cref="CompareCommand"/>.
/// </summary>
internal sealed partial class CompareCommand
{
    /// <summary>
    ///     The options of the command, in the order they are listed in the help.
    /// </summary>
    /// <remarks>
    ///     The options of the shared source are the declarations of <see cref="RenderCommand.OptionTable"/> (so the
    ///     grammar and the help text cannot drift), except <c>--background</c>, which must be opaque here. They apply to
    ///     both inputs; the per-side options (<c>--a-page</c>, <c>--b-page</c>, <c>--a-dpi</c>, <c>--b-dpi</c>,
    ///     <c>--a-view</c>, <c>--b-view</c>, <c>--a-define</c>, <c>--b-define</c>) apply to one input and win over the
    ///     shared value.
    /// </remarks>
    internal static readonly IReadOnlyList<OptionSpec> OptionTable =
    [
        .. RenderCommand.SpecsFor("--page", "--dpi"),
        new("--a-page", [], true, "The page or slide of the first input, counted from 1 (a document only; overrides --page)", "N"),
        new("--b-page", [], true, "The page or slide of the second input, counted from 1 (a document only; overrides --page)", "N"),
        new("--a-dpi", [], true, "Resolution of the first input in dots per inch (a document only; overrides --dpi)", "D"),
        new("--b-dpi", [], true, "Resolution of the second input in dots per inch (a document only; overrides --dpi)", "D"),
        new("--background", [], true, "Opaque color that transparency is flattened over: white (default), black, #RRGGBB, or a color name", "color"),
        .. RenderCommand.SpecsFor("--no-auto-orient", "--strict"),
        .. RenderCommand.SpecsFor(RenderCommand.ScadOptionNames),
        new("--a-view", [], true, "SCAD model: the viewpoint of the first input (overrides --view)", "name"),
        new("--b-view", [], true, "SCAD model: the viewpoint of the second input (overrides --view)", "name"),
        new("--a-define", [], true, "SCAD model: set a variable of the first input (repeatable; wins over -D of the same name)", "name=value"),
        new("--b-define", [], true, "SCAD model: set a variable of the second input (repeatable; wins over -D of the same name)", "name=value"),
        new("--resize-to", [], true, "Bring both inputs to one size: a (the size of the first input), b (the second), or WxH", "a|b|WxH"),
        new("--fit", [], true, "How a raster input is fitted to --resize-to: contain (default; keeps the aspect ratio, with margins) or stretch", "mode"),
        .. RenderCommand.SpecsFor(ImagePipeline.FilterOption),
        new("--tolerance", [], true, $"A pixel differs when a channel differs by more than this, 0-255 (default {ImageComparison.DefaultTolerance})", "0-255"),
        new("--grid", [], true, $"Report the error of each cell of a grid, columns x rows, each 1-{ComparisonResult.MaxGridSide} (default {ImageComparison.DefaultGridColumns}x{ImageComparison.DefaultGridRows})", "CxR"),
        new("--threshold", [], true, "Fail (exit code 1) unless a metric is within a limit: mae, rmse, max-diff, diff-ratio, or cell-rmse at most; psnr or ssim at least (repeatable)", "metric=value"),
        new("--diff", [], true, "Write a picture of the differences to this file", "file"),
        new("--diff-style", [], true, "How --diff draws a difference: heat (default) or abs; --side-by-side always uses heat", "style"),
        new("--gain", [], true, $"Amplification of a difference in the pictures, 0.1-255 (default {ImageComparison.DefaultGain})", "factor"),
        new("--side-by-side", [], true, "Write the first input, the second input, and the differences side by side to this file", "file"),
        new("--overlay", [], true, "Write a blend of both inputs to this file", "file"),
        new("--overlay-weight", [], true, $"Weight of the second input in --overlay, 0-1 (default {ImageComparison.DefaultOverlayWeight})", "0-1"),
        .. RenderCommand.SpecsFor("--quality", "--no-clobber")
    ];

    /// <summary>
    ///     The settings of one input, as given by its own options.
    /// </summary>
    /// <param name="Label">The name of the side: <c>a</c> or <c>b</c>.</param>
    /// <param name="Path">The path of the input.</param>
    /// <param name="Page">The page chosen for this input alone, or <see langword="null"/>.</param>
    /// <param name="Dpi">The resolution chosen for this input alone, or <see langword="null"/>.</param>
    /// <param name="View">The SCAD view chosen for this input alone, or <see langword="null"/>.</param>
    /// <param name="Defines">The SCAD variables chosen for this input alone; empty for none.</param>
    internal sealed record SideSettings(
        string Label,
        string Path,
        int? Page,
        float? Dpi,
        string? View,
        IReadOnlyDictionary<string, object?> Defines);

    /// <summary>
    ///     How the two inputs are brought to one size.
    /// </summary>
    /// <param name="Text">What was written: <c>a</c>, <c>b</c>, or <c>WxH</c> (with the numbers as parsed).</param>
    /// <param name="Side">The side whose size is the target (<c>a</c> or <c>b</c>), or <see langword="null"/> for an explicit size.</param>
    /// <param name="Width">The target width for an explicit size; 0 otherwise.</param>
    /// <param name="Height">The target height for an explicit size; 0 otherwise.</param>
    internal sealed record ResizeTarget(string Text, string? Side, int Width, int Height);

    /// <summary>
    ///     The settings of one invocation, read from the command line.
    /// </summary>
    /// <param name="A">The first input.</param>
    /// <param name="B">The second input.</param>
    /// <param name="Page">The page chosen for both inputs, or <see langword="null"/>.</param>
    /// <param name="Dpi">The resolution chosen for both inputs, or <see langword="null"/>.</param>
    /// <param name="Scad">The shared options of an OpenSCAD model, or <see langword="null"/> when none was given.</param>
    /// <param name="Background">The opaque color transparency is flattened over.</param>
    /// <param name="Strict"><see langword="true"/> to fail on a feature the library cannot draw.</param>
    /// <param name="AutoOrient"><see langword="false"/> to leave a JPEG as stored.</param>
    /// <param name="Filter">The resampling filter of a fit.</param>
    /// <param name="Resize">How the inputs are brought to one size, or <see langword="null"/> for none.</param>
    /// <param name="Fit">How a raster input is fitted to the size.</param>
    /// <param name="Tolerance">The largest channel difference that does not make a pixel differ.</param>
    /// <param name="GridColumns">The number of grid columns.</param>
    /// <param name="GridRows">The number of grid rows.</param>
    /// <param name="Thresholds">The requirements, in the order written.</param>
    /// <param name="DiffPath">The output of the difference picture, or <see langword="null"/>.</param>
    /// <param name="SideBySidePath">The output of the side-by-side picture, or <see langword="null"/>.</param>
    /// <param name="OverlayPath">The output of the overlay, or <see langword="null"/>.</param>
    /// <param name="DiffStyle">How a difference is drawn.</param>
    /// <param name="Gain">The amplification of a difference.</param>
    /// <param name="OverlayWeight">The weight of the second input in the overlay.</param>
    /// <param name="Write">How the pictures are written.</param>
    internal sealed record Settings(
        SideSettings A,
        SideSettings B,
        int? Page,
        float? Dpi,
        ScadViewOptions? Scad,
        Rgba32 Background,
        bool Strict,
        bool AutoOrient,
        ResamplingMode Filter,
        ResizeTarget? Resize,
        FitMode Fit,
        int Tolerance,
        int GridColumns,
        int GridRows,
        IReadOnlyList<ComparisonThreshold> Thresholds,
        string? DiffPath,
        string? SideBySidePath,
        string? OverlayPath,
        DiffStyle DiffStyle,
        double Gain,
        double OverlayWeight,
        WriteOptions Write);

    /// <summary>
    ///     Parses a fit mode.
    /// </summary>
    /// <param name="text">The text: <c>contain</c> or <c>stretch</c>, in any case.</param>
    /// <param name="option">The option name, for messages.</param>
    /// <returns>The mode.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is neither.</exception>
    internal static FitMode ParseFit(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        return text.ToLowerInvariant() switch
        {
            "contain" => FitMode.Contain,
            "stretch" => FitMode.Stretch,
            _ => throw new ToolException(
                ErrorCodes.UsageError,
                $"'{text}' is not a value of {option}.",
                "Use contain (keep the aspect ratio) or stretch (fill the box).")
        };
    }

    /// <summary>
    ///     Reads the settings from the parsed command line.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when there are not exactly two inputs, a value is malformed or out of
    ///     range, the background is not opaque, a threshold is repeated, or two options contradict each other; and with
    ///     code <c>size-limit</c> when <c>--resize-to</c> names a side larger than the maximum.
    /// </exception>
    private static Settings ReadSettings(ArgumentReader reader)
    {
        reader.RequirePositionals(2, 2, "two input files");

        var background = reader.Get("--background") is { } colorText
            ? ArgumentReader.ParseColor(colorText, "--background")
            : new Rgba32(255, 255, 255, 255);
        if (background.A != 255)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "The --background of a comparison must be opaque.",
                "Use a color without transparency, for example white or #808080.");
        }

        var (columns, rows) = reader.Get("--grid") is { } gridText
            ? ParseGrid(gridText)
            : (ImageComparison.DefaultGridColumns, ImageComparison.DefaultGridRows);

        var thresholds = new List<ComparisonThreshold>();
        foreach (var text in reader.GetAll("--threshold"))
        {
            var threshold = ParseThreshold(text);
            if (thresholds.Any(t => t.Metric == threshold.Metric))
            {
                throw new ToolException(
                    ErrorCodes.UsageError,
                    $"The threshold {ImageComparison.MetricName(threshold.Metric)} was given more than once.",
                    "Give each metric one limit.");
            }

            thresholds.Add(threshold);
        }

        var write = new WriteOptions(
            null,
            reader.GetInt("--quality", ImageWriter.MinQuality, ImageWriter.MaxQuality) ?? ImageWriter.DefaultQuality,
            reader.Has("--no-clobber"),
            background);

        return new Settings(
            ReadSide(reader, "a", reader.Positionals[0]),
            ReadSide(reader, "b", reader.Positionals[1]),
            reader.Get("--page") is { } pageText ? ArgumentReader.ParsePage(pageText, "--page") : null,
            reader.Get("--dpi") is { } dpiText ? ArgumentReader.ParseDpi(dpiText, "--dpi") : null,
            RenderCommand.ReadScadOptions(reader),
            background,
            reader.Has("--strict"),
            !reader.Has("--no-auto-orient"),
            reader.Get(ImagePipeline.FilterOption) is { } filterText
                ? ImagePipeline.ParseFilter(filterText, ImagePipeline.FilterOption)
                : ResamplingMode.Auto,
            reader.Get("--resize-to") is { } resizeText ? ParseResizeTo(resizeText) : null,
            reader.Get("--fit") is { } fitText ? ParseFit(fitText, "--fit") : FitMode.Contain,
            reader.GetInt("--tolerance", 0, 255) ?? ImageComparison.DefaultTolerance,
            columns,
            rows,
            thresholds,
            reader.Get("--diff"),
            reader.Get("--side-by-side"),
            reader.Get("--overlay"),
            reader.Get("--diff-style") is { } styleText ? ParseDiffStyle(styleText) : DiffStyle.Heat,
            reader.GetDouble("--gain", 0.1, 255) ?? ImageComparison.DefaultGain,
            reader.GetDouble("--overlay-weight", 0, 1) ?? ImageComparison.DefaultOverlayWeight,
            write);
    }

    /// <summary>
    ///     Reads the options that belong to one input.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <param name="label">The side: <c>a</c> or <c>b</c>.</param>
    /// <param name="path">The path of the input.</param>
    /// <returns>The settings of the input.</returns>
    private static SideSettings ReadSide(ArgumentReader reader, string label, string path)
    {
        var page = $"--{label}-page";
        var dpi = $"--{label}-dpi";
        var view = $"--{label}-view";
        var define = $"--{label}-define";
        return new SideSettings(
            label,
            path,
            reader.Get(page) is { } pageText ? ArgumentReader.ParsePage(pageText, page) : null,
            reader.Get(dpi) is { } dpiText ? ArgumentReader.ParseDpi(dpiText, dpi) : null,
            reader.Get(view) is { } viewText ? RenderCommand.ParseScadView(viewText) : null,
            ArgumentReader.ParseDefines(reader.GetAll(define)));
    }

    /// <summary>
    ///     Parses the grid size written <c>CxR</c>.
    /// </summary>
    /// <param name="text">The text: the number of columns and rows, each 1 to 64.</param>
    /// <returns>The columns and rows.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not of that form.</exception>
    private static (int Columns, int Rows) ParseGrid(string text)
    {
        var separator = text.IndexOfAny(['x', 'X']);
        if (separator <= 0 || separator == text.Length - 1)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'{text}' is not a value of --grid.",
                $"Write the columns and rows as CxR, each 1 to {ComparisonResult.MaxGridSide}, for example 4x4.");
        }

        return (
            ArgumentReader.ParseInt(text[..separator], "--grid", 1, ComparisonResult.MaxGridSide),
            ArgumentReader.ParseInt(text[(separator + 1)..], "--grid", 1, ComparisonResult.MaxGridSide));
    }

    /// <summary>
    ///     Parses the value of <c>--resize-to</c>.
    /// </summary>
    /// <param name="text">The text: <c>a</c>, <c>b</c>, or a size written <c>WxH</c>.</param>
    /// <returns>The target.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the text is none of these, and <c>size-limit</c> when a side is larger
    ///     than the maximum.
    /// </exception>
    private static ResizeTarget ParseResizeTo(string text)
    {
        if (string.Equals(text, "a", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "b", StringComparison.OrdinalIgnoreCase))
        {
            var side = text.ToLowerInvariant();
            return new ResizeTarget(side, side, 0, 0);
        }

        var (width, height) = ArgumentReader.ParseSize(text, "--resize-to");
        return new ResizeTarget(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{width}x{height}"), null, width, height);
    }

    /// <summary>
    ///     Parses a difference style.
    /// </summary>
    /// <param name="text">The text: <c>heat</c> or <c>abs</c>, in any case.</param>
    /// <returns>The style.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is neither.</exception>
    private static DiffStyle ParseDiffStyle(string text)
    {
        return text.ToLowerInvariant() switch
        {
            "heat" => DiffStyle.Heat,
            "abs" => DiffStyle.Abs,
            _ => throw new ToolException(
                ErrorCodes.UsageError,
                $"'{text}' is not a value of --diff-style.",
                "Use heat (a hot ramp of the largest difference) or abs (the difference of each channel).")
        };
    }

    /// <summary>
    ///     Parses one <c>--threshold</c> value written <c>metric=value</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The threshold.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when there is no <c>=</c>, the metric is unknown, or the value is not a
    ///     number in the range of the metric.
    /// </exception>
    private static ComparisonThreshold ParseThreshold(string text)
    {
        var metrics = string.Join(", ", Enum.GetValues<ThresholdMetric>().Select(ImageComparison.MetricName));
        var separator = text.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0 || separator == text.Length - 1)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'{text}' is not a value of --threshold.",
                $"Write a metric and a limit as metric=value, for example ssim=0.95. The metrics are: {metrics}.");
        }

        var name = text[..separator];
        if (!ImageComparison.TryParseMetric(name, out var metric))
        {
            throw new ToolException(ErrorCodes.UsageError, $"'{name}' is not a metric of --threshold.", $"The metrics are: {metrics}.");
        }

        var (min, max) = ImageComparison.Range(metric);
        var limit = ArgumentReader.ParseNumber(text[(separator + 1)..], $"--threshold {ImageComparison.MetricName(metric)}", min, max);
        return new ComparisonThreshold(metric, limit);
    }
}
