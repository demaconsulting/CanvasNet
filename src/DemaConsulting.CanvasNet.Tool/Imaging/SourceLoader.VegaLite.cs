using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     Vega-Lite sources of <see cref="SourceLoader"/>: a chart drawn from its specification.
/// </summary>
/// <remarks>
///     <para>
///         A chart has no pages. Its natural size is the size the library lays the chart out at (<c>Measure</c>), and
///         <c>--scale</c>, <c>--width</c>, and <c>--height</c> are applied to it like they are to any vector source: the
///         chart is drawn sharply at the target size, fitted inside it and centered when both sides are given. The
///         background is the one of the chart (white when it has none) unless <c>--background</c> replaces it.
///     </para>
///     <para>
///         A render is static and offline: the specification is the only thing read from the file system apart from
///         the data files the library itself resolves beside it, no network is used, dates are in UTC, and only the
///         fonts bundled with the library are used, so the same chart gives the same pixels on every machine. A
///         construct the library does not draw (yet) is returned as a diagnostic (<c>vega-lite</c>, <c>unsupported</c>,
///         with its stable <c>vl-*</c> identifier as the code); with <see cref="SourceOptions.Strict"/> it is an
///         <c>unsupported-feature</c> error instead. A problem with the specification or with the chart is a
///         <c>vega-lite-error</c>; this includes a specification that nests deeper than the parser limit or has a
///         string longer than the string limit, as the SCAD parser's nesting limit is a <c>scad-error</c>.
///         A limit that the library applies while it lays out or draws the chart (rows, ticks, scene items, and so
///         on) is <c>budget-exceeded</c>; a specification file longer than the source limit is <c>corrupt-input</c>, in
///         parity with an overlong SCAD model; and a chart that lays out larger than the maximum image size is
///         <c>size-limit</c>.
///     </para>
/// </remarks>
internal static partial class SourceLoader
{
    /// <summary>
    ///     The code of a warning of the Vega-Lite library in the diagnostics of a result.
    /// </summary>
    private const string VegaLiteWarningCode = "vega-lite-warning";

    /// <summary>
    ///     Reads what a chart declares and the size it is laid out at, by parsing and measuring it; the chart is not
    ///     drawn.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>
    ///     The facts about the file: the natural size of the chart, 4 channels, and the marks, data references,
    ///     parameters, title, and schema version of the specification.
    /// </returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>vega-lite-error</c> when the specification or the chart is not valid, <c>budget-exceeded</c>
    ///     when a limit is exceeded, and <c>size-limit</c> when the chart is larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    private static SourceInfo ProbeVegaLite(string path, long bytes)
    {
        var document = Read(path, SourceKind.VegaLite, () => VegaLiteDocument.Open(path));
        var size = Read(path, SourceKind.VegaLite, () => MeasureChart(() => document.Measure(CreateVegaLiteOptions(new SourceOptions(), null)), path));
        var chart = new ChartFacts(
            [.. document.MarkTypes],
            [.. document.DataReferences.Select(r => new ChartDataReference(r.Kind.ToString().ToLowerInvariant(), r.Url, r.RowCount, r.Location.Line))],
            [.. document.ParameterNames],
            document.Title,
            document.SchemaMajorVersion);
        return new SourceInfo(path, SourceKind.VegaLite, bytes, size.Width, size.Height, 4, true, true, 1, 1, null, [], [], [])
        {
            Chart = chart
        };
    }

    /// <summary>
    ///     Measures a chart.
    /// </summary>
    /// <param name="measure">The measure of the chart, which gives its natural size.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <returns>The natural size of the chart.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>size-limit</c> when the chart lays out larger than the maximum image size, and
    ///     <c>vega-lite-error</c> when the library rejects the measure for any other argument failure.
    /// </exception>
    internal static VegaLiteSize MeasureChart(Func<VegaLiteSize> measure, string path)
    {
        try
        {
            return measure();
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "options")
        {
            // The library refuses a natural size outside 1 to Surface.MaxDimension on a side and reports it for the
            // options (their scale); this is the only argument failure that means "too large"
            throw new ToolException(
                ErrorCodes.SizeLimit,
                $"'{path}' lays out larger than the maximum of {Surface.MaxDimension} pixels per side: {OneLine(ex.Message)}",
                "Reduce the width and height of the chart.",
                ex);
        }
        catch (ArgumentException ex)
        {
            // Any other argument failure is a chart the library cannot lay out, never a size problem
            throw new ToolException(ErrorCodes.VegaLiteError, $"Vega-Lite measure error: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Draws a chart, mapping the failures of the library to a <see cref="ToolException"/>.
    /// </summary>
    /// <param name="render">The render of the chart.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <returns>The surface; the caller owns it.</returns>
    /// <exception cref="ToolException">
    ///     Thrown for the allow-listed failures of <see cref="Read{T}"/>, and with code <c>vega-lite-error</c> for an
    ///     argument failure of the library: the target size was validated before the render, so such a failure is a
    ///     chart the library cannot draw and never a usage error.
    /// </exception>
    internal static Surface RenderChart(Func<Surface> render, string path)
    {
        try
        {
            return Read(path, SourceKind.VegaLite, render);
        }
        catch (ArgumentException ex)
        {
            throw new ToolException(ErrorCodes.VegaLiteError, $"Vega-Lite render error: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Creates the options of one measure or render of a chart: the fixed, deterministic ones (UTC, bundled fonts
    ///     only) and the ones the tool options say (background, strictness).
    /// </summary>
    /// <param name="options">The loading options.</param>
    /// <param name="notes">The list the warnings and unsupported features are added to, or <see langword="null"/> to drop them.</param>
    /// <returns>The options.</returns>
    private static VegaLiteRenderOptions CreateVegaLiteOptions(SourceOptions options, List<ImageDiagnostic>? notes)
    {
        return new VegaLiteRenderOptions
        {
            Background = options.Background,
            TimeZone = TimeZoneInfo.Utc,
            Fonts = new VegaLiteFontOptions { UseSystemFonts = false },
            WarningHandler = notes == null
                ? null
                : warning => notes.Add(new ImageDiagnostic("vega-lite", "warning", VegaLiteWarningCode, OneLine(warning.ToString()))),
            UnsupportedFeatureHandler = notes == null
                ? null
                : unsupported => notes.Add(
                    new ImageDiagnostic(
                        "vega-lite",
                        "unsupported",
                        unsupported.Feature,
                        OneLine(unsupported.Location.IsKnown ? $"{unsupported.Location}: {unsupported.Message}" : unsupported.Message))),
            ThrowOnUnsupportedFeature = options.Strict
        };
    }

    /// <summary>
    ///     Maps a failure to render a chart that was read successfully to a <see cref="ToolException"/>.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>
    ///     The exception to throw: <c>budget-exceeded</c> when the chart exceeded a limit of the library (rows, fields,
    ///     ticks, scene items, and so on), otherwise <c>vega-lite-error</c> (missing or invalid data, an invalid property
    ///     value, and so on).
    /// </returns>
    /// <remarks>
    ///     The library names every limit it enforces in the message, either as <c>the limit of N</c> or with the name of
    ///     the limit (<c>VegaLiteLimits.MaxSceneItems</c>); a test pins the texts, because the library gives no other
    ///     way to tell a limit from another failure.
    /// </remarks>
    internal static ToolException MapVegaLiteRender(VegaLiteRenderException exception)
    {
        var message = OneLine(exception.Message);
        if (!IsVegaLiteLimit(message))
        {
            return new ToolException(ErrorCodes.VegaLiteError, $"Vega-Lite render error: {message}", null, exception);
        }

        return new ToolException(
            ErrorCodes.BudgetExceeded,
            $"The Vega-Lite chart exceeds a limit: {message}",
            "Simplify the chart or reduce its data; this limit cannot be changed from the command line.",
            exception);
    }

    /// <summary>
    ///     Determines whether a message of the Vega-Lite library reports that a limit was exceeded.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when the message names a limit.</returns>
    private static bool IsVegaLiteLimit(string message)
    {
        return message.Contains("the limit of ", StringComparison.Ordinal)
               || message.Contains("VegaLiteLimits.", StringComparison.Ordinal);
    }

    /// <summary>
    ///     A chart opened for drawing.
    /// </summary>
    private sealed class VegaLitePageSource : PageSource
    {
        /// <summary>
        ///     The parsed chart.
        /// </summary>
        private readonly VegaLiteDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="VegaLitePageSource"/> class.
        /// </summary>
        /// <param name="document">The parsed chart.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        private VegaLitePageSource(VegaLiteDocument document, SourceInfo info, SourceOptions options)
            : base(info, options)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a chart.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ToolException">Thrown when the file cannot be read or is not a valid specification.</exception>
        public static VegaLitePageSource Open(SourceInfo info, SourceOptions options)
        {
            var document = Read(info.Path, SourceKind.VegaLite, () => VegaLiteDocument.Open(info.Path));
            return new VegaLitePageSource(document, info, options);
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var (width, height) = ComputeTargetSize(Info.Path, Info.Width, Info.Height, Options);
            var notes = new List<ImageDiagnostic>();
            var renderOptions = CreateVegaLiteOptions(Options, notes);

            var surface = RenderChart(() => _document.Render(width, height, renderOptions), Info.Path);
            var fields = new List<(string Name, object Value)>
            {
                ("width", width),
                ("height", height),
                ("timeZone", "UTC")
            };
            if (Options.Background is { } background)
            {
                fields.Add(("background", FormatColor(background)));
            }

            return new LoadedSource(surface, Info, 1, true, notes, [], [Operation("render", [.. fields])]);
        }
    }
}
