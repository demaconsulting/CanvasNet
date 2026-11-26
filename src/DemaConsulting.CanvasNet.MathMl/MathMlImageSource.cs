using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     The <see cref="IImageSource"/> for MathML equations: it draws a <c>.mml</c> file that another document links to (an
///     SVG <c>image</c>, a picture of a diagram) as an equation.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fragments.</b> The source declares one fragment parameter (it implements <see cref="IFragmentImageSource"/>):
///         <c>display=block</c> or <c>display=inline</c> sets the display mode (<see cref="MathMlRenderOptions.Display"/>),
///         for example <c>sum.mml#display=block</c> draws the equation in display style even when its <c>math</c> element
///         says otherwise. The value is matched ASCII case-insensitively, as the <c>display</c> attribute; any other value
///         refuses the link with <see cref="FolderImageResolver.FragmentInvalidCode"/> before the file is read, and the
///         equation is not drawn. Any other parameter, such as <c>#page=2</c>, is reported by the resolver as
///         <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and ignored.
///     </para>
///     <para>
///         The natural size of the equation is its own at the default font size of 16 CSS pixels (the size that
///         <see cref="MathMlDocument.Measure"/> gives at scale 1, which here may be larger than
///         <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/>); the equation is drawn afresh at the size
///         <see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> gives (each side at most
///         <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/>, the equation scaled uniformly to fit and
///         centered), so it is as sharp as the placement allows, on a transparent background. The surface is checked
///         against the pixel budget of the request before it is allocated. Rendering is deterministic: only the bundled
///         fonts are used.
///     </para>
///     <para>
///         <b>Notes.</b> Warnings are returned as <see cref="ImageSourceResult.Diagnostics"/> with the source <c>mathml</c>,
///         the code <c>mathml-warning</c>, the specific <c>mathml-*</c> code in <see cref="ImageSourceDiagnostic.Detail"/>
///         and the line and column in <see cref="ImageSourceDiagnostic.Location"/>; unsupported features use the feature
///         code as the code (for example <see cref="MathMlDiagnosticCodes.ElementUnsupported"/>). With
///         <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> the first unsupported feature throws
///         <see cref="MathMlUnsupportedFeatureException"/> instead. Invalid text throws <see cref="MathMlSyntaxException"/>,
///         an equation that cannot be drawn <see cref="MathMlRenderException"/>. The request's cancellation token is passed
///         to the render.
///     </para>
///     <para>
///         <b>Files.</b> MathML Core has no external references, so the source reads only the content of the request
///         and never uses <see cref="ImageSourceRequest.Resolver"/> or the network.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme.
///     </para>
/// </remarks>
public sealed class MathMlImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The fragment parameter that sets the display mode: <c>display</c>.
    /// </summary>
    public const string DisplayParameter = "display";

    /// <summary>
    ///     The code of a warning note; the specific <c>mathml-*</c> code is in <see cref="ImageSourceDiagnostic.Detail"/>.
    /// </summary>
    public const string WarningCode = "mathml-warning";

    /// <summary>The extensions this source claims.</summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".mml"]);

    /// <summary>The schemes this source claims: none.</summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>The fragment parameters this source declares.</summary>
    private static readonly ReadOnlyCollection<string> Parameters = new([DisplayParameter]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="MathMlImageSource"/> class.
    /// </summary>
    public MathMlImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "mathml";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters => Parameters;

    /// <summary>
    ///     Draws the equation of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The equation at the requested (or natural) size, on a transparent background, with the notes of the library.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown, with <see cref="FolderImageResolver.FragmentInvalidCode"/>, when the <c>display</c> fragment parameter is not <c>block</c> or <c>inline</c>.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="MathMlSyntaxException">Thrown when the text is not a valid equation.</exception>
    /// <exception cref="MathMlRenderException">Thrown when the equation cannot be drawn (a limit, a layout failure).</exception>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown for an unsupported feature when the request asks for it.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than the limits allow.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The display mode comes first: a link that cannot be used costs nothing
        var value = request.Fragment.GetValue(DisplayParameter);
        MathMlDisplay? display = null;
        if (value is not null)
        {
            display = value.Trim() switch
            {
                var text when text.Equals("block", StringComparison.OrdinalIgnoreCase) => MathMlDisplay.Block,
                var text when text.Equals("inline", StringComparison.OrdinalIgnoreCase) => MathMlDisplay.Inline,
                _ => throw new ImageSourceRefusedException(
                    FolderImageResolver.FragmentInvalidCode,
                    $"the '{DisplayParameter}' fragment parameter must be block or inline, not '{value}'"),
            };
        }

        MathMlDocument document;
        using (var stream = request.OpenContent())
        {
            document = MathMlDocument.Open(stream, new MathMlOpenOptions { SourceName = request.FileName });
        }

        // The notes keep the specific code and the location of every report
        var diagnostics = new List<ImageSourceDiagnostic>();
        var options = new MathMlRenderOptions
        {
            Display = display,
            ThrowOnUnsupportedFeature = request.ThrowOnUnsupportedFeature,
            WarningHandler = warning => diagnostics.Add(
                new ImageSourceDiagnostic(Name, ImageDiagnosticSeverity.Warning, WarningCode, OneLine(warning.Message))
                {
                    Detail = warning.Code,
                    Location = ToLocation(warning.Location),
                }),
            UnsupportedFeatureHandler = unsupported => diagnostics.Add(
                new ImageSourceDiagnostic(Name, ImageDiagnosticSeverity.Unsupported, unsupported.Feature, OneLine(unsupported.Message))
                {
                    Location = ToLocation(unsupported.Location),
                }),
        };

        // The natural size comes from a measure without handlers, so every note is reported once, by the render; it may be
        // larger than the largest surface, as only its aspect ratio matters for a smaller requested size
        var natural = document.MeasureNatural(new MathMlRenderOptions { Display = display }, request.CancellationToken);
        var (width, height) = request.SizeFor(natural.Width, natural.Height);
        request.EnsureWithinBudget(width, height);
        var surface = document.Render(width, height, options, request.CancellationToken);
        return new ImageSourceResult(surface) { Diagnostics = diagnostics };
    }

    /// <summary>
    ///     Converts a location for a note.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>The note location, or null when the location is unknown.</returns>
    private static ImageDiagnosticLocation? ToLocation(MathMlLocation location) =>
        location.IsKnown ? new ImageDiagnosticLocation(location.SourceName, location.Line, location.Column, null) : null;

    /// <summary>
    ///     Joins the lines of a message into one.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns>The message on one line.</returns>
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
