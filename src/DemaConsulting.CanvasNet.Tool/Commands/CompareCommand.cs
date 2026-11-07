// cspell:ignore canvasnet psnr rmse ssim
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

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
///         <see cref="Surface.Compare"/> computes the metrics and the per-cell grid and <see cref="Surface.CreateDiff"/>,
///         <see cref="Surface.CreateOverlay"/>, and <see cref="Surface.CreateSideBySide"/> the pictures;
///         <see cref="ImageComparison"/> checks the thresholds. The pictures are
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
        + "--b-view, --a-define, and --b-define apply to one input and win. Either input may end in a fragment" + Environment.NewLine
        + "(a.scad#view=top&def.width=20, report.pdf#page=2; quote it in a shell), which wins over all of them for that" + Environment.NewLine
        + "input." + Environment.NewLine
        + Environment.NewLine
        + "Examples:" + Environment.NewLine
        + "  canvasnet compare reference.png redraw.png" + Environment.NewLine
        + "  canvasnet compare reference.png redraw.svg --resize-to a --diff diff.png" + Environment.NewLine
        + "  canvasnet compare reference.png redraw.svg --resize-to a --grid 4x4 --side-by-side cmp.png --threshold ssim=0.9 --json" + Environment.NewLine
        + "  canvasnet compare deck.pptx deck.pdf --a-page 2 --b-page 2 --resize-to a --overlay blend.png" + Environment.NewLine
        + "  canvasnet compare sketch.jpg model.scad --b-view front --resize-to a --overlay blend.png" + Environment.NewLine
        + "  canvasnet compare \"a.scad#view=top\" \"b.scad#view=top\" --diff diff.png";

    /// <inheritdoc />
    public void Run(Context context, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);

        var reader = ArgumentReader.Parse(Name, args, Options);
        var settings = ReadSettings(reader);

        // Report every mistake about the inputs and the outputs before any pixels are decoded
        var infoA = SourceLoader.Probe(InputReference.Parse(settings.A.Path));
        var infoB = SourceLoader.Probe(InputReference.Parse(settings.B.Path));
        RequireSharedScadOptionsApply(settings, infoA, infoB);
        var optionsA = PrepareSide(settings, settings.A, infoA, out var pageA);
        var optionsB = PrepareSide(settings, settings.B, infoB, out var pageB);

        // The operation reports the mistakes about the outputs before any pixels are decoded, then loads both inputs,
        // compares them, and writes the pictures; everything below only formats what it returns
        // Every value is passed explicitly, so the command never depends on the defaults of the request
        var comparison = ImageComparer.Execute(new CompareRequest(
            new CompareInput(settings.A.Label, infoA) { Page = pageA, Source = optionsA },
            new CompareInput(settings.B.Label, infoB) { Page = pageB, Source = optionsB })
        {
            Resize = settings.Resize,
            Fit = settings.Fit,
            Filter = settings.Filter,
            Background = settings.Background,
            Tolerance = settings.Tolerance,
            GridColumns = settings.GridColumns,
            GridRows = settings.GridRows,
            Thresholds = settings.Thresholds,
            DiffPath = settings.DiffPath,
            SideBySidePath = settings.SideBySidePath,
            OverlayPath = settings.OverlayPath,
            DiffStyle = settings.DiffStyle,
            Gain = settings.Gain,
            OverlayWeight = settings.OverlayWeight,
            Write = settings.Write
        });
        var a = comparison.A;
        var b = comparison.B;
        var result = comparison.Comparison;
        var checks = comparison.Checks;
        var outputs = comparison.Pictures;

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

            context.WriteDiagnostics(
                new[] { a, b }.SelectMany(side => side.Diagnostics.Select(d => ((string?)$"input {side.Label}", d))),
                a.Echo.Concat(b.Echo));
        }

        if (failed.Count > 0)
        {
            throw new ToolException(
                ErrorCodes.ThresholdFailed,
                string.Join("; ", failed.Select(DescribeFailure)),
                outputs.Count > 0
                    ? "The result and the pictures were written. See the pictures and the grid for where the images differ, or relax the threshold."
                    : "The result was written. Add --diff or --side-by-side, and see the grid, for where the images differ, or relax the threshold.")
            {
                ResultFields = writer => WriteJsonFields(writer, settings, a, b, result, checks, outputs)
            };
        }
    }

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

        // Shared options are skipped for an input they do not concern; a page that the fragment of the input names wins
        // over --a-page, --b-page, and --page
        page = info.Format.IsDocument() ? info.Selection?.Page ?? side.Page ?? settings.Page ?? 1 : 1;
        try
        {
            SourceLoader.CheckPage(info, page);
        }
        catch (ViewerException ex) when (ex.Code == ErrorCodes.PageOutOfRange)
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

        return options with { View = side.View ?? options.View, ViewGiven = options.ViewGiven || side.View != null, Defines = defines };
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
        CompareSide a, CompareSide b, ComparisonResult result, IReadOnlyList<ThresholdCheck> checks, IReadOnlyList<ComparedPicture> outputs)
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
    /// <returns>The input as written (the path, and <c>#</c> and the fragment when it has one), followed by <c>page N</c> for a document.</returns>
    private static string Describe(CompareSide side)
    {
        return side.Info.Format.IsDocument() ? $"{side.Info.Specifier} page {side.Page}" : side.Info.Specifier;
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
        CompareSide a,
        CompareSide b,
        ComparisonResult result,
        IReadOnlyList<ThresholdCheck> checks,
        IReadOnlyList<ComparedPicture> outputs)
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
            writer.WriteString("to", settings.Resize.Side ?? string.Create(CultureInfo.InvariantCulture, $"{settings.Resize.Width}x{settings.Resize.Height}"));
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

                ResultWriter.WriteDiagnosticExtras(writer, diagnostic);
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
    private static void WriteSide(Utf8JsonWriter writer, string name, CompareSide side)
    {
        writer.WriteStartObject(name);
        writer.WriteString("path", side.Info.Path);
        if (side.Info.Selection is { } selection)
        {
            writer.WriteString("fragment", selection.Fragment);
        }

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
