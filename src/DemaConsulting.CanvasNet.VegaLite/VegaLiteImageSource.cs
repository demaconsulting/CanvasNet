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
///         <b>Fragments.</b> The source declares one family of fragment parameters (it implements
///         <see cref="IFragmentImageSource"/>): every <c>param.&lt;name&gt;=&lt;value&gt;</c> sets the parameter <c>name</c> of
///         the chart (<see cref="VegaLiteRenderOptions.Parameters"/>, like <c>--param name=value</c> of the tool), for
///         example <c>chart.vl.json#param.threshold=5&amp;amp;param.brush={"x":[2,6]}</c> (in an XML attribute the
///         <c>&amp;</c> is written <c>&amp;amp;</c>). The value is read as
///         <see cref="ImageParameterValues.TryParseParameter(string, out object?, out string?)"/> reads it (a number,
///         <c>true</c>, <c>false</c>, <c>null</c>, text, or a JSON array or object, the shapes a selection parameter takes),
///         percent-decoded after the fragment is split. A value that is not usable refuses the link with
///         <see cref="FolderImageResolver.FragmentInvalidCode"/> and the chart is not drawn; a name the chart does not
///         declare is the warning <see cref="UnknownParameterCode"/> (the chart is drawn without it). Any other parameter,
///         such as <c>#page=2</c>, is reported by the resolver as
///         <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and ignored.
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
///         <b>Pictures.</b> The <c>image</c> marks of the chart load their pictures through <see cref="ImageSourceRequest.Resolver"/>, the resolver scoped to the chart's own file: a relative <c>url</c> resolves against the
///         folder of the chart, shares the budgets, diagnostics and cycle chain of the whole render (a chart that shows itself stops at once), and is confined by the same rules as every other embedded file. A request built by
///         hand has no resolver; the <c>image</c> marks are then reported as <c>vl-mark-image</c> and draw nothing.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme. Only <c>.vl.json</c> and <c>.vl</c> are
///         claimed, not <c>.json</c>: a plain JSON file is not assumed to be a chart.
///     </para>
/// </remarks>
public sealed class VegaLiteImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The prefix of the fragment parameters that set a parameter of the chart: <c>param.</c>.
    /// </summary>
    public const string ParameterPrefix = "param.";

    /// <summary>
    ///     The code of the warning for a <c>param.</c> fragment parameter that names a parameter the chart does not declare:
    ///     <c>vl-param-unknown</c>. The chart is drawn without it.
    /// </summary>
    public const string UnknownParameterCode = "vl-param-unknown";

    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".vl", ".vl.json"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     The fragment parameter names this source declares: none (its parameters are all prefixed).
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoParameters = new([]);

    /// <summary>
    ///     The fragment parameter prefixes this source declares.
    /// </summary>
    private static readonly ReadOnlyCollection<string> Prefixes = new([ParameterPrefix]);

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

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters => NoParameters;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameterPrefixes => Prefixes;

    /// <summary>
    ///     Draws the chart of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The chart at the requested (or natural) size, on its own background, with the notes of the library.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown, with <see cref="FolderImageResolver.FragmentInvalidCode"/>, when a <c>param.</c> fragment parameter is not usable.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="VegaLiteSpecException">Thrown when the specification is not valid.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the chart cannot be drawn (missing data, a limit).</exception>
    /// <exception cref="VegaLiteUnsupportedFeatureException">Thrown for an unsupported feature when the request asks for it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The overrides come first: a link that cannot be used costs nothing
        var requested = ImageParameterValues.ReadParameters(request.Fragment, ParameterPrefix);

        // The chart is parsed from the bytes; its data files are looked up in the folder of the file and nowhere else
        VegaLiteDocument document;
        using (var stream = request.OpenContent())
        {
            document = VegaLiteDocument.Open(stream, new VegaLiteOpenOptions { BaseDirectory = request.Directory, SourceName = request.FileName });
        }

        var diagnostics = new List<ImageSourceDiagnostic>();
        var parameters = SelectDeclared(requested, document, diagnostics);
        var options = new VegaLiteRenderOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            Fonts = new VegaLiteFontOptions { UseSystemFonts = false },
            Parameters = parameters,
            WarningHandler = warning => diagnostics.Add(
                new ImageSourceDiagnostic("vega-lite", ImageDiagnosticSeverity.Warning, "vega-lite-warning", OneLine(warning.ToString()))),
            UnsupportedFeatureHandler = unsupported => diagnostics.Add(
                new ImageSourceDiagnostic(
                    "vega-lite",
                    ImageDiagnosticSeverity.Unsupported,
                    unsupported.Feature,
                    OneLine(unsupported.Location.IsKnown ? $"{unsupported.Location}: {unsupported.Message}" : unsupported.Message))),
            ThrowOnUnsupportedFeature = request.ThrowOnUnsupportedFeature,
            ImageResolver = request.Resolver
        };

        // The chart lays itself out at the size it is given; its natural size is what a layout without a size gives (with the
        // parameters, which can change the layout). The measure has no handlers, so a note is reported once, by the render
        var natural = document.Measure(
            new VegaLiteRenderOptions { TimeZone = TimeZoneInfo.Utc, Fonts = new VegaLiteFontOptions { UseSystemFonts = false }, Parameters = parameters },
            request.CancellationToken);
        var (width, height) = request.SizeFor(natural.Width, natural.Height);
        request.EnsureWithinBudget(width, height);
        var surface = document.Render(width, height, options, request.CancellationToken);
        return new ImageSourceResult(surface) { Diagnostics = diagnostics };
    }

    /// <summary>
    ///     Keeps the parameters the chart declares and reports the others.
    /// </summary>
    /// <param name="requested">The parameters the fragment of the link sets.</param>
    /// <param name="document">The chart.</param>
    /// <param name="diagnostics">The list the warning for each unknown name is added to.</param>
    /// <returns>The values of the declared parameters, or <see langword="null"/> when there are none (or none the library accepts).</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown when the library does not accept a value (for example, one nested too deeply).</exception>
    private static IReadOnlyDictionary<string, object?>? SelectDeclared(
        IReadOnlyDictionary<string, object?> requested,
        VegaLiteDocument document,
        List<ImageSourceDiagnostic> diagnostics)
    {
        var declared = new HashSet<string>(document.ParameterNames, StringComparer.Ordinal);
        var kept = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in requested)
        {
            if (declared.Contains(name))
            {
                kept[name] = value;
                continue;
            }

            var names = declared.Count == 0 ? "the chart declares none" : $"declared: {string.Join(", ", declared.Order(StringComparer.Ordinal))}";
            diagnostics.Add(
                new ImageSourceDiagnostic(
                    "vega-lite",
                    ImageDiagnosticSeverity.Warning,
                    UnknownParameterCode,
                    OneLine($"the fragment parameter '{ParameterPrefix}{name}' names no parameter of the chart and is ignored ({names})")));
        }

        if (kept.Count == 0)
        {
            return null;
        }

        try
        {
            // The library says which values it accepts, so it is asked rather than guessed
            _ = new VegaLiteRenderOptions { Parameters = kept };
        }
        catch (ArgumentException ex)
        {
            // The name of the argument that an ArgumentException appends means nothing to the author of a link
            var message = OneLine(ex.Message);
            var argument = message.LastIndexOf(" (Parameter '", StringComparison.Ordinal);
            message = argument > 0 ? message[..argument] : message;
            throw new ImageSourceRefusedException(FolderImageResolver.FragmentInvalidCode, $"a '{ParameterPrefix}' fragment parameter is not valid: {message}");
        }

        return kept;
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
