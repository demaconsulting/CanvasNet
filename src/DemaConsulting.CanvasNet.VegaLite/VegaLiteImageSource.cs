// cspell:ignore vega
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     The <see cref="IImageSource"/> for Vega-Lite charts: it draws a <c>.vl</c> or <c>.vl.json</c> file that another
///     document links to (an SVG <c>image</c>, for example) as a chart.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fragments.</b> The source claims no fragment parameter (it does not implement
///         <see cref="IFragmentImageSource"/>), so a parameter in the link, such as <c>#page=2</c>, is reported by the
///         resolver as <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and ignored.
///     </para>
///     <para>
///         The natural size of the chart is its own layout size (<see cref="VegaLiteDocument.Measure"/>); the chart is laid
///         out afresh at the size <see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> gives, so it is as sharp as the
///         placement allows. The chart keeps <b>its own background</b> (white unless the specification declares another),
///         because a chart is meant to carry its own paper. Rendering is deterministic: times are in UTC and only the
///         bundled fonts are used. The <c>data</c> files the chart names resolve against
///         <see cref="ImageSourceRequest.Directory"/> and the Vega-Lite library confines them to it.
///     </para>
///     <para>
///         Warnings and unsupported features are returned as <see cref="ImageSourceResult.Diagnostics"/> (source
///         <c>vega-lite</c>, code <c>vega-lite-warning</c> or the feature name); with
///         <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> the first unsupported feature throws
///         <see cref="VegaLiteUnsupportedFeatureException"/> instead. An invalid specification throws
///         <see cref="VegaLiteSpecException"/>, a chart that cannot be drawn <see cref="VegaLiteRenderException"/>. The
///         request's cancellation token is passed to the render.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme. Only <c>.vl.json</c> and <c>.vl</c> are
///         claimed, not <c>.json</c>: a plain JSON file is not assumed to be a chart.
///     </para>
/// </remarks>
public sealed class VegaLiteImageSource : IImageSource
{
    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".vl", ".vl.json"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteImageSource"/> class.
    /// </summary>
    public VegaLiteImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "vega-lite";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <summary>
    ///     Draws the chart of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The chart at the requested (or natural) size, on its own background, with the notes of the library.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the specification is not valid.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the chart cannot be drawn (missing data, a limit).</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown for an unsupported feature when the request asks for it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The chart is parsed from the bytes; its data files are looked up in the folder of the file and nowhere else
        VegaLiteDocument document;
        using (var stream = request.OpenContent())
        {
            document = VegaLiteDocument.Open(stream, new VegaLiteOpenOptions { BaseDirectory = request.Directory, SourceName = request.FileName });
        }

        var diagnostics = new List<ImageSourceDiagnostic>();
        var options = new VegaLiteRenderOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            Fonts = new VegaLiteFontOptions { UseSystemFonts = false },
            WarningHandler = warning => diagnostics.Add(
                new ImageSourceDiagnostic("vega-lite", ImageDiagnosticSeverity.Warning, "vega-lite-warning", OneLine(warning.ToString()))),
            UnsupportedFeatureHandler = unsupported => diagnostics.Add(
                new ImageSourceDiagnostic(
                    "vega-lite",
                    ImageDiagnosticSeverity.Unsupported,
                    unsupported.Feature,
                    OneLine(unsupported.Location.IsKnown ? $"{unsupported.Location}: {unsupported.Message}" : unsupported.Message))),
            ThrowOnUnsupportedFeature = request.ThrowOnUnsupportedFeature
        };

        // The chart lays itself out at the size it is given; its natural size is what a layout without a size gives. The
        // measure has no handlers, so a note is reported once, by the render
        var natural = document.Measure(
            new VegaLiteRenderOptions { TimeZone = TimeZoneInfo.Utc, Fonts = new VegaLiteFontOptions { UseSystemFonts = false } },
            request.CancellationToken);
        var (width, height) = request.SizeFor(natural.Width, natural.Height);
        request.EnsureWithinBudget(width, height);
        var surface = document.Render(width, height, options, request.CancellationToken);
        return new ImageSourceResult(surface) { Diagnostics = diagnostics };
    }

    /// <summary>
    ///     Joins the lines of a message into one.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns>The message on one line.</returns>
    private static string OneLine(string text)
    {
        return string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
