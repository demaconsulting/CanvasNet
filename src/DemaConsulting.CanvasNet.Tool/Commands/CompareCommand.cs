// cspell:ignore canvasnet psnr rmse ssim
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The <c>compare</c> command: measures how much two images differ and can write pictures of the differences.
/// </summary>
/// <remarks>
///     <para>
///         Each input is loaded by <see cref="SourceLoader"/> like the input of <c>render</c>; the options shared by
///         both inputs (<c>--page</c>, <c>--dpi</c>, the options of an OpenSCAD model, <c>--background</c>, and so on) are
///         skipped for an input they do not concern, and the per-side options (<c>--a-page</c>, <c>--b-dpi</c>,
///         <c>--a-view</c>, and so on) win over the shared value and are an error for an input of the wrong kind. Both
///         inputs are flattened over the opaque background, so the metrics are defined on opaque RGB.
///     </para>
///     <para>
///         The inputs must have the same size. <c>--resize-to</c> brings them to one size: <c>a</c> or <c>b</c> is the
///         size of that input, and <c>WxH</c> an explicit size. A vector input (SVG, a document page, a model) is
///         rendered at that size, sharply; a raster input is fitted into it (<c>--fit</c>: <c>contain</c>, the
///         default, keeps the aspect ratio; <c>stretch</c> does not). Without <c>--resize-to</c> a difference in size is
///         the error <c>size-mismatch</c>.
///     </para>
///     <para>
///         <see cref="ImageComparison"/> computes the metrics, the per-cell grid, and the pictures. The pictures are
///         encoded to temporary files first and moved into place only when every one was encoded, like the pages of
///         <c>render</c>. A threshold that is not met does not stop the result from being written: the command writes
///         the pictures and the result, and then fails with the expected error <c>threshold-failed</c> (exit code 1);
///         with <c>--json</c> the failure document carries the result fields after the <c>error</c> object.
///     </para>
///     <para>
///         The class is split into <c>CompareCommand.Options.cs</c> (the option declarations and the reading of the
///         settings) and this file (the run, the text result, and the JSON result).
///     </para>
/// </remarks>
internal sealed partial class CompareCommand : ICommand
{
    /// <summary>The number of worst grid cells that the result lists.</summary>
    private const int WorstCellCount = 3;

    /// <inheritdoc />
    public string Name => "compare";

    /// <inheritdoc />
    public IReadOnlyList<string> Aliases => [];

    /// <inheritdoc />
    public string Summary => "Measure how much two images differ (MAE, RMSE, PSNR, SSIM), and draw the differences";

    /// <inheritdoc />
    public IReadOnlyList<OptionSpec> Options => OptionTable;

    /// <inheritdoc />
    public string Usage =>
        "canvasnet compare <a> <b> [options]" + Environment.NewLine
        + Environment.NewLine
        + "Reads two images, drawings, or document pages like 'render' and reports how much they differ: the mean" + Environment.NewLine
        + "absolute error (MAE), the root mean square error (RMSE), the peak signal-to-noise ratio (PSNR), the structural" + Environment.NewLine
        + "similarity (SSIM), the fraction of pixels that differ by more than --tolerance, and the error of each cell of" + Environment.NewLine
        + "a grid, so that you can see where the images differ. The inputs must have the same size: use --resize-to a," + Environment.NewLine
        + "b, or WxH to bring them to one; a vector input is rendered at that size." + Environment.NewLine
        + "--diff, --side-by-side, and --overlay write pictures of the differences. --threshold makes the command fail" + Environment.NewLine
        + "with exit code 1 (after writing the result and the pictures) when a metric is not within its limit." + Environment.NewLine
        + "--page, --dpi, and the SCAD options apply to both inputs; --a-page, --b-page, --a-dpi, --b-dpi, --a-view," + Environment.NewLine
        + "--b-view, --a-define, and --b-define apply to one input and win." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet compare reference.png redraw.png" + Environment.NewLine
        + "  canvasnet compare reference.png redraw.svg --resize-to a --diff diff.png" + Environment.NewLine
        + "  canvasnet compare reference.png redraw.svg --resize-to a --grid 4x4 --side-by-side cmp.png --threshold ssim=0.9 --json" + Environment.NewLine
        + "  canvasnet compare deck.pptx deck.pdf --a-page 2 --b-page 2 --resize-to a --overlay blend.png" + Environment.NewLine
        + "  canvasnet compare sketch.jpg model.scad --b-view front --resize-to a --overlay blend.png";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        var reader = ArgumentReader.Parse(Name, args, Options);
        var settings = ReadSettings(reader);

        // Report every mistake about the inputs and the outputs before any pixels are decoded
        var infoA = SourceLoader.Probe(settings.A.Path);
        var infoB = SourceLoader.Probe(settings.B.Path);
        RequireSharedScadOptionsApply(settings, infoA, infoB);
        var optionsA = PrepareSide(settings, settings.A, infoA, out var pageA);
        var optionsB = PrepareSide(settings, settings.B, infoB, out var pageB);

        var requested = new List<(string Kind, string Path)>();
        if (settings.DiffPath != null)
        {
            requested.Add(("diff", settings.DiffPath));
        }

        if (settings.SideBySidePath != null)
        {
            requested.Add(("sideBySide", settings.SideBySidePath));
        }

        if (settings.OverlayPath != null)
        {
            requested.Add(("overlay", settings.OverlayPath));
        }

        foreach (var (_, path) in requested)
        {
            ImageWriter.ResolveFormat(path, null);
        }

        ImageWriter.CheckTargets(requested.Select(r => r.Path).ToList(), [settings.A.Path, settings.B.Path], settings.Write.NoClobber);

        using var a = new Side(settings.A.Label, infoA, pageA);
        using var b = new Side(settings.B.Label, infoB, pageB);
        LoadSides(settings, a, optionsA, b, optionsB);
        if (a.Width != b.Width || a.Height != b.Height)
        {
            throw new ToolException(
                ErrorCodes.SizeMismatch,
                $"The inputs have different sizes: '{a.Info.Path}' is {a.Width}x{a.Height} and '{b.Info.Path}' is {b.Width}x{b.Height}.",
                "Use --resize-to a (or b, or WxH) to bring them to one size.");
        }

        if (settings.GridColumns > a.Width || settings.GridRows > a.Height)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The grid {settings.GridColumns}x{settings.GridRows} is larger than the images ({a.Width}x{a.Height}).",
                "Use a grid with at most one cell per pixel.");
        }

        var result = ImageComparison.Compare(a.Surface!, b.Surface!, settings.Tolerance, settings.GridColumns, settings.GridRows);
        var checks = ImageComparison.Evaluate(result, settings.Thresholds);

        var outputs = WriteOutputs(settings, requested, a.Surface!, b.Surface!);

        var failed = checks.Where(check => !check.Passed).ToList();
        if (context.Json && failed.Count == 0)
        {
            context.WriteLine(ResultWriter.Success(Name, writer => WriteJsonFields(writer, settings, a, b, result, checks, outputs)));
            return;
        }

        if (!context.Json)
        {
            foreach (var line in RenderText(a, b, result, checks, outputs))
            {
                context.WriteLine(line);
            }
        }

        if (failed.Count > 0)
        {
            throw new ToolException(
                ErrorCodes.ThresholdFailed,
                string.Join("; ", failed.Select(DescribeFailure)),
                "The result and any pictures were written. See --diff, --side-by-side, and the grid for where the images differ, or relax the threshold.")
            {
                ResultFields = writer => WriteJsonFields(writer, settings, a, b, result, checks, outputs)
            };
        }
    }

    /// <summary>
    ///     One input after it was loaded, with what is reported about it.
    /// </summary>
    private sealed class Side : IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Side"/> class.
        /// </summary>
        /// <param name="label">The name of the side: <c>a</c> or <c>b</c>.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="page">The 1-based page that is loaded.</param>
        public Side(string label, SourceInfo info, int page)
        {
            Label = label;
            Info = info;
            Page = page;
        }

        /// <summary>Gets the name of the side: <c>a</c> or <c>b</c>.</summary>
        public string Label { get; }

        /// <summary>Gets the facts about the file.</summary>
        public SourceInfo Info { get; }

        /// <summary>Gets the 1-based page that is loaded.</summary>
        public int Page { get; }

        /// <summary>Gets or sets the pixels at the size that is compared; the side owns them.</summary>
        public Surface? Surface { get; set; }

        /// <summary>Gets or sets the width of <see cref="Surface"/>; it stays when the pixels are disposed, for the failure document.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the height of <see cref="Surface"/>; it stays when the pixels are disposed, for the failure document.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the width of the input as it was loaded, before it was fitted to the target size.</summary>
        public int LoadedWidth { get; set; }

        /// <summary>Gets or sets the height of the input as it was loaded, before it was fitted to the target size.</summary>
        public int LoadedHeight { get; set; }

        /// <summary>Gets or sets what was done to the input to produce the pixels.</summary>
        public IReadOnlyList<AppliedOperation> Operations { get; set; } = [];

        /// <summary>Gets or sets the notes reported while loading.</summary>
        public IReadOnlyList<ImageDiagnostic> Diagnostics { get; set; } = [];

        /// <summary>Gets or sets the text a model printed while it was evaluated.</summary>
        public IReadOnlyList<string> Echo { get; set; } = [];

        /// <inheritdoc />
        public void Dispose()
        {
            Surface?.Dispose();
            Surface = null;
        }
    }

    /// <summary>
    ///     One picture that was written.
    /// </summary>
    /// <param name="Kind">The kind used in JSON: <c>diff</c>, <c>sideBySide</c>, or <c>overlay</c>.</param>
    /// <param name="Result">What was written.</param>
    private sealed record PictureOutput(string Kind, WriteResult Result);

    /// <summary>
    ///     Requires that the options of an OpenSCAD model are not given when neither input is a model.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="infoA">The facts about the first input.</param>
    /// <param name="infoB">The facts about the second input.</param>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when a shared model option was given and no input is a model.</exception>
    private static void RequireSharedScadOptionsApply(Settings settings, SourceInfo infoA, SourceInfo infoB)
    {
        if (settings.Scad != null && !infoA.Format.IsModel() && !infoB.Format.IsModel())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {string.Join(", ", RenderCommand.ScadOptionNames)} only apply to an OpenSCAD model, but neither input is one ('{infoA.Path}' is a {infoA.Format.ToFormatName()} file, '{infoB.Path}' is a {infoB.Format.ToFormatName()} file).",
                "Remove them, or compare a .scad file.");
        }
    }

    /// <summary>
    ///     Checks the options of one input against its kind and builds its loading options.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="side">The options of the input.</param>
    /// <param name="info">The facts about the input.</param>
    /// <param name="page">The 1-based page that is compared.</param>
    /// <returns>The loading options, without a target size.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a per-side option does not suit the kind of the input, and with code
    ///     <c>page-out-of-range</c> when the page does not exist.
    /// </exception>
    private static SourceOptions PrepareSide(Settings settings, SideSettings side, SourceInfo info, out int page)
    {
        var kind = info.Format.ToFormatName();
        RequireKind(side.Page != null || side.Dpi != null, info.Format.IsDocument(), $"--{side.Label}-page and --{side.Label}-dpi", "a document (PDF, PPTX, or VSDX)", side, kind);
        RequireKind(side.View != null || side.Defines.Count > 0, info.Format.IsModel(), $"--{side.Label}-view and --{side.Label}-define", "an OpenSCAD model", side, kind);

        // Shared options are skipped for an input they do not concern
        page = info.Format.IsDocument() ? side.Page ?? settings.Page ?? 1 : 1;
        try
        {
            SourceLoader.CheckPage(info, page);
        }
        catch (ToolException ex) when (ex.Code == ErrorCodes.PageOutOfRange)
        {
            throw new ToolException(ErrorCodes.PageOutOfRange, $"Input {side.Label}: {ex.Message}", ex.Hint, ex);
        }

        return new SourceOptions
        {
            Page = page,
            Dpi = info.Format.IsDocument() ? side.Dpi ?? settings.Dpi : null,
            Strict = settings.Strict,
            Background = settings.Background,
            AutoOrient = settings.AutoOrient,
            Filter = settings.Filter,
            Scad = info.Format.IsModel() ? MergeScad(settings.Scad, side) : null
        };
    }

    /// <summary>
    ///     Requires that options which only suit one kind of input are not given for another.
    /// </summary>
    /// <param name="given"><see langword="true"/> when one of the options was given.</param>
    /// <param name="suits"><see langword="true"/> when the input is of the kind the options suit.</param>
    /// <param name="options">The names of the options, for the message.</param>
    /// <param name="what">The kind of input the options suit, for the message.</param>
    /// <param name="side">The input.</param>
    /// <param name="kind">The format name of the input, for the message.</param>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the options were given for the wrong kind.</exception>
    private static void RequireKind(bool given, bool suits, string options, string what, SideSettings side, string kind)
    {
        if (given && !suits)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {options} only apply to {what}, but '{side.Path}' is a {kind} file.",
                "Remove them, or use the shared option for both inputs.");
        }
    }

    /// <summary>
    ///     Combines the shared options of an OpenSCAD model with those of one input.
    /// </summary>
    /// <param name="shared">The shared options, or <see langword="null"/>.</param>
    /// <param name="side">The options of the input.</param>
    /// <returns>The options of the model; a variable of the input wins over a shared variable of the same name.</returns>
    private static ScadViewOptions? MergeScad(ScadViewOptions? shared, SideSettings side)
    {
        if (shared == null && side.View == null && side.Defines.Count == 0)
        {
            return null;
        }

        var options = shared ?? new ScadViewOptions();
        var defines = new Dictionary<string, object?>(options.Defines);
        foreach (var (name, value) in side.Defines)
        {
            defines[name] = value;
        }

        return options with { View = side.View ?? options.View, Defines = defines };
    }

    /// <summary>
    ///     Loads both inputs at the size that is compared.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="a">The first input; its pixels are set.</param>
    /// <param name="optionsA">The loading options of the first input.</param>
    /// <param name="b">The second input; its pixels are set.</param>
    /// <param name="optionsB">The loading options of the second input.</param>
    /// <remarks>
    ///     Without <c>--resize-to</c> both are loaded at their natural size. With <c>a</c> or <c>b</c> the reference side is
    ///     loaded first, at its natural size, and the other is loaded into that size; with <c>WxH</c> both are loaded into
    ///     the size.
    /// </remarks>
    private static void LoadSides(Settings settings, Side a, SourceOptions optionsA, Side b, SourceOptions optionsB)
    {
        var resize = settings.Resize;
        if (resize == null)
        {
            LoadSide(settings, a, optionsA, null);
            LoadSide(settings, b, optionsB, null);
        }
        else if (resize.Side == "a")
        {
            LoadSide(settings, a, optionsA, null);
            LoadSide(settings, b, optionsB, (a.Width, a.Height));
        }
        else if (resize.Side == "b")
        {
            LoadSide(settings, b, optionsB, null);
            LoadSide(settings, a, optionsA, (b.Width, b.Height));
        }
        else
        {
            LoadSide(settings, a, optionsA, (resize.Width, resize.Height));
            LoadSide(settings, b, optionsB, (resize.Width, resize.Height));
        }
    }

    /// <summary>
    ///     Loads one input, optionally into a box.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="side">The input; its pixels, sizes, operations, and notes are set.</param>
    /// <param name="options">The loading options.</param>
    /// <param name="box">The size to bring the input to, or <see langword="null"/> to keep its size.</param>
    /// <remarks>
    ///     A vector input is rendered at the box (see <see cref="SourceLoader.BoxOptions"/>); a raster input is fitted into
    ///     it with <see cref="SurfaceOperations.FitInto"/> unless it already has that size.
    /// </remarks>
    private static void LoadSide(Settings settings, Side side, SourceOptions options, (int Width, int Height)? box)
    {
        var effective = box is { } target ? SourceLoader.BoxOptions(side.Info, options, target.Width, target.Height) : options;
        using var pages = SourceLoader.OpenPages(side.Info, effective);
        var loaded = pages.ReadPage(side.Page);

        // The side takes the surface of the loaded source: it owns it from here on
        var surface = loaded.Surface;
        try
        {
            var operations = loaded.Operations.ToList();
            side.LoadedWidth = surface.Width;
            side.LoadedHeight = surface.Height;
            side.Diagnostics = loaded.Diagnostics;
            side.Echo = loaded.Echo;
            if (box is { } size && (surface.Width != size.Width || surface.Height != size.Height))
            {
                var fitted = SurfaceOperations.FitInto(surface, size.Width, size.Height, settings.Fit, settings.Background, settings.Filter);
                surface.Dispose();
                surface = fitted;
                operations.Add(settings.Fit == FitMode.Contain
                    ? SourceLoader.Operation(
                        "fit",
                        ("mode", "contain"),
                        ("width", size.Width),
                        ("height", size.Height),
                        ("background", SourceLoader.FormatColor(settings.Background)))
                    : SourceLoader.Operation(
                        "resize",
                        ("width", size.Width),
                        ("height", size.Height),
                        ("filter", ImagePipeline.FilterName(settings.Filter))));
            }

            side.Operations = operations;
            side.Width = surface.Width;
            side.Height = surface.Height;
            side.Surface = surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Creates, stages, and publishes the requested pictures.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="requested">The kind and path of each requested picture, in the order they are written.</param>
    /// <param name="a">The first image.</param>
    /// <param name="b">The second image.</param>
    /// <returns>What was written, in the order of <paramref name="requested"/>.</returns>
    /// <remarks>
    ///     Every picture is encoded to a temporary file before any is moved into place, so a failure while creating or
    ///     encoding one leaves no new or partial output and every existing output untouched. Each picture is disposed as
    ///     soon as it is encoded.
    /// </remarks>
    private static List<PictureOutput> WriteOutputs(Settings settings, List<(string Kind, string Path)> requested, Surface a, Surface b)
    {
        var outputs = new List<PictureOutput>();
        var staged = new List<ImageWriter.StagedOutput>();
        try
        {
            foreach (var (kind, path) in requested)
            {
                using var picture = CreatePicture(settings, kind, a, b);
                var pending = ImageWriter.Stage(picture, path, settings.A.Path, settings.Write);
                staged.Add(pending);
                outputs.Add(new PictureOutput(kind, pending.Result));
            }

            foreach (var pending in staged)
            {
                pending.Commit();
            }
        }
        finally
        {
            // Disposing removes the temporary file of every output that was not moved into place
            foreach (var pending in staged)
            {
                pending.Dispose();
            }
        }

        return outputs;
    }

    /// <summary>
    ///     Creates one picture.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="kind">The kind: <c>diff</c>, <c>sideBySide</c>, or <c>overlay</c>.</param>
    /// <param name="a">The first image.</param>
    /// <param name="b">The second image.</param>
    /// <returns>The picture; the caller disposes it.</returns>
    private static Surface CreatePicture(Settings settings, string kind, Surface a, Surface b)
    {
        switch (kind)
        {
            case "diff":
                return ImageComparison.CreateDiff(a, b, settings.DiffStyle, settings.Gain);
            case "overlay":
                return ImageComparison.CreateOverlay(a, b, settings.OverlayWeight);
            default:
                // The third panel of a side-by-side picture is always the heat map
                using (var heat = ImageComparison.CreateDiff(a, b, DiffStyle.Heat, settings.Gain))
                {
                    return ImageComparison.CreateSideBySide(a, b, heat);
                }
        }
    }

    /// <summary>
    ///     Describes a threshold that was not met.
    /// </summary>
    /// <param name="check">The failed check.</param>
    /// <returns>The text, for example <c>rmse 23.91 is above the limit 10</c>.</returns>
    private static string DescribeFailure(ThresholdCheck check)
    {
        return $"{ImageComparison.MetricName(check.Metric)} {FormatValue(check.Metric, check.Value)} is {(check.IsMinimum ? "below" : "above")} the limit {FormatLimit(check.Limit)}";
    }

    /// <summary>
    ///     Formats a measured value of a metric for text.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <param name="value">The value; positive infinity is written <c>inf</c>.</param>
    /// <returns>The text: SSIM and the ratio with four decimals, the maximum difference as a whole number, the others with two.</returns>
    private static string FormatValue(ThresholdMetric metric, double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "inf";
        }

        var format = metric switch
        {
            ThresholdMetric.Ssim or ThresholdMetric.DiffRatio => "0.0000",
            ThresholdMetric.MaxDiff => "0",
            _ => "0.00"
        };
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Formats the limit of a threshold for text.
    /// </summary>
    /// <param name="limit">The limit.</param>
    /// <returns>The limit with at most four decimals.</returns>
    private static string FormatLimit(double limit)
    {
        return limit.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Renders the text result.
    /// </summary>
    /// <param name="a">The first input.</param>
    /// <param name="b">The second input.</param>
    /// <param name="result">The comparison.</param>
    /// <param name="checks">The threshold checks, in the order written.</param>
    /// <param name="outputs">The pictures that were written.</param>
    /// <returns>
    ///     The lines: a summary (or <c>identical</c> when no channel of any pixel differs), the RMSE of each grid cell
    ///     (not for identical inputs), one line per picture, and one line per threshold.
    /// </returns>
    private static List<string> RenderText(
        Side a, Side b, ComparisonResult result, IReadOnlyList<ThresholdCheck> checks, IReadOnlyList<PictureOutput> outputs)
    {
        var metrics = result.Metrics;
        var lines = new List<string>();
        var names = $"{Describe(a)} with {Describe(b)} ({result.Width}x{result.Height})";
        if (metrics.Identical)
        {
            lines.Add($"compared {names}: identical");
        }
        else
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"compared {names}: MAE {metrics.Mae:0.00}, RMSE {metrics.Rmse:0.00}, PSNR {metrics.PsnrDb:0.00} dB, SSIM {metrics.Ssim:0.0000}, {metrics.DifferingPixelRatio * 100:0.00}% of pixels differ (tolerance {result.Tolerance})"));
            lines.Add($"RMSE per cell ({result.Columns}x{result.Rows}):");
            for (var row = 0; row < result.Rows; row++)
            {
                var cells = result.Cells.Skip(row * result.Columns).Take(result.Columns);
                lines.Add(string.Concat(cells.Select(cell => cell.Rmse.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(6))));
            }
        }

        foreach (var output in outputs)
        {
            var written = output.Result;
            var kind = output.Kind == "sideBySide" ? "side-by-side" : output.Kind;
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"wrote {written.Path} ({written.Width}x{written.Height} {written.Format.ToFormatName()}, {written.Bytes} bytes) [{kind}]"));
        }

        foreach (var check in checks)
        {
            lines.Add(
                $"threshold {ImageComparison.MetricName(check.Metric)} {(check.IsMinimum ? ">=" : "<=")} {FormatLimit(check.Limit)}: "
                + $"{(check.Passed ? "passed" : "failed")} ({FormatValue(check.Metric, check.Value)})");
        }

        return lines;
    }

    /// <summary>
    ///     Names an input in the text result.
    /// </summary>
    /// <param name="side">The input.</param>
    /// <returns>The path as given, followed by <c>page N</c> for a document.</returns>
    private static string Describe(Side side)
    {
        return side.Info.Format.IsDocument() ? $"{side.Info.Path} page {side.Page}" : side.Info.Path;
    }

    /// <summary>
    ///     Writes the command's fields of the JSON document.
    /// </summary>
    /// <param name="writer">The writer positioned inside the document's top-level object.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="a">The first input.</param>
    /// <param name="b">The second input.</param>
    /// <param name="result">The comparison.</param>
    /// <param name="checks">The threshold checks.</param>
    /// <param name="outputs">The pictures that were written.</param>
    /// <remarks>
    ///     The same fields follow <c>ok: true</c> in the success document and follow the <c>error</c> object in the
    ///     failure document of a threshold that was not met. Numbers are rounded to four decimals (MAE, RMSE, PSNR) or
    ///     six (the normalized RMSE, SSIM, and ratios), and the PSNR of identical images is <c>null</c>.
    /// </remarks>
    private static void WriteJsonFields(
        Utf8JsonWriter writer,
        Settings settings,
        Side a,
        Side b,
        ComparisonResult result,
        IReadOnlyList<ThresholdCheck> checks,
        IReadOnlyList<PictureOutput> outputs)
    {
        writer.WriteStartObject("tool");
        writer.WriteString("name", "canvasnet");
        writer.WriteString("version", Program.Version);
        writer.WriteEndObject();

        WriteSide(writer, "a", a);
        WriteSide(writer, "b", b);

        writer.WriteStartObject("size");
        writer.WriteNumber("width", result.Width);
        writer.WriteNumber("height", result.Height);
        writer.WriteEndObject();

        if (settings.Resize != null)
        {
            writer.WriteStartObject("resize");
            writer.WriteString("to", settings.Resize.Text);
            writer.WriteString("fit", settings.Fit.ToString().ToLowerInvariant());
            writer.WriteEndObject();
        }

        writer.WriteStartObject("options");
        writer.WriteNumber("tolerance", result.Tolerance);
        writer.WriteString("background", SourceLoader.FormatColor(settings.Background));
        writer.WriteNumber("gridColumns", result.Columns);
        writer.WriteNumber("gridRows", result.Rows);
        writer.WriteEndObject();

        var metrics = result.Metrics;
        writer.WriteStartObject("metrics");
        writer.WriteBoolean("identical", metrics.Identical);
        writer.WriteNumber("mae", Round(metrics.Mae, 4));
        writer.WriteNumber("rmse", Round(metrics.Rmse, 4));
        writer.WriteNumber("rmseNormalized", Round(metrics.Rmse / 255, 6));
        if (metrics.PsnrDb is { } psnr)
        {
            writer.WriteNumber("psnrDb", Round(psnr, 4));
        }
        else
        {
            writer.WriteNull("psnrDb");
        }

        writer.WriteNumber("ssim", Round(metrics.Ssim, 6));
        writer.WriteNumber("maxChannelDiff", metrics.MaxChannelDifference);
        writer.WriteNumber("diffPixels", metrics.DifferingPixels);
        writer.WriteNumber("diffPixelRatio", Round(metrics.DifferingPixelRatio, 6));
        writer.WriteEndObject();

        writer.WriteStartObject("grid");
        writer.WriteNumber("columns", result.Columns);
        writer.WriteNumber("rows", result.Rows);
        writer.WriteStartArray("cells");
        foreach (var cell in result.Cells)
        {
            writer.WriteStartObject();
            writer.WriteNumber("column", cell.Column);
            writer.WriteNumber("row", cell.Row);
            writer.WriteNumber("x", cell.X);
            writer.WriteNumber("y", cell.Y);
            writer.WriteNumber("width", cell.Width);
            writer.WriteNumber("height", cell.Height);
            writer.WriteNumber("mae", Round(cell.Mae, 4));
            writer.WriteNumber("rmse", Round(cell.Rmse, 4));
            writer.WriteNumber("diffPixelRatio", Round(cell.DifferingPixelRatio, 6));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteStartArray("worstCells");
        foreach (var cell in result.WorstCells(WorstCellCount))
        {
            writer.WriteStartObject();
            writer.WriteNumber("column", cell.Column);
            writer.WriteNumber("row", cell.Row);
            writer.WriteNumber("rmse", Round(cell.Rmse, 4));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartObject("thresholds");
        writer.WriteBoolean("passed", checks.All(check => check.Passed));
        writer.WriteStartArray("checks");
        foreach (var check in checks)
        {
            writer.WriteStartObject();
            writer.WriteString("metric", ImageComparison.MetricName(check.Metric));
            writer.WriteString("comparison", check.IsMinimum ? ">=" : "<=");
            writer.WriteNumber("limit", check.Limit);
            if (double.IsFinite(check.Value))
            {
                writer.WriteNumber("value", Round(check.Value, 6));
            }
            else
            {
                writer.WriteNull("value");
            }

            writer.WriteBoolean("passed", check.Passed);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteStartArray("outputs");
        foreach (var output in outputs)
        {
            var written = output.Result;
            writer.WriteStartObject();
            writer.WriteString("kind", output.Kind);
            writer.WriteString("path", written.Path);
            writer.WriteString("format", written.Format.ToFormatName());
            writer.WriteNumber("width", written.Width);
            writer.WriteNumber("height", written.Height);
            writer.WriteNumber("bytes", written.Bytes);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("diagnostics");
        foreach (var side in new[] { a, b })
        {
            foreach (var diagnostic in side.Diagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("side", side.Label);
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
        }

        writer.WriteEndArray();

        writer.WriteStartArray("echo");
        foreach (var line in a.Echo.Concat(b.Echo))
        {
            writer.WriteStringValue(line);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes the object that describes one input.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The property name: <c>a</c> or <c>b</c>.</param>
    /// <param name="side">The input.</param>
    private static void WriteSide(Utf8JsonWriter writer, string name, Side side)
    {
        writer.WriteStartObject(name);
        writer.WriteString("path", side.Info.Path);
        writer.WriteString("format", side.Info.Format.ToFormatName());
        if (side.Info.Format.IsDocument())
        {
            writer.WriteNumber("page", side.Page);
            writer.WriteNumber("pageCount", side.Info.PageCount);
        }

        writer.WriteNumber("loadedWidth", side.LoadedWidth);
        writer.WriteNumber("loadedHeight", side.LoadedHeight);
        writer.WriteNumber("width", side.Width);
        writer.WriteNumber("height", side.Height);
        writer.WriteStartArray("operations");
        foreach (var operation in side.Operations)
        {
            writer.WriteStartObject();
            writer.WriteString("op", operation.Op);
            foreach (var (field, value) in operation.Fields)
            {
                RenderCommand.WriteField(writer, field, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Rounds a number for the JSON result.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="decimals">The number of decimals.</param>
    /// <returns>The value rounded half away from zero.</returns>
    private static double Round(double value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
