using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The <see cref="IImageSource"/> for Mermaid diagrams: it draws a <c>.mmd</c> or <c>.mermaid</c> file that another
///     document links to (an SVG <c>image</c>, a picture of a Markdown document) as a diagram.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fragments.</b> The source declares one fragment parameter (it implements <see cref="IFragmentImageSource"/>):
///         <c>theme=&lt;name&gt;</c> sets the site-level theme (<see cref="MermaidRenderOptions.Theme"/>, like <c>mmdc -t</c>),
///         for example <c>flow.mmd#theme=dark</c>; a theme the text sets itself still wins. A value that is not one of
///         <see cref="MermaidRenderOptions.ThemeNames"/> refuses the link with <see cref="FolderImageResolver.FragmentInvalidCode"/>
///         and the diagram is not drawn. Any other parameter, such as <c>#page=2</c>, is reported by the resolver as
///         <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and ignored.
///     </para>
///     <para>
///         The natural size of the diagram is its own (<see cref="MermaidDocument.Measure"/>); the diagram is drawn afresh at
///         the size <see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> gives, so it is as sharp as the placement
///         allows, on a transparent background (as Mermaid's SVG). Rendering is deterministic: only the bundled fonts are used.
///     </para>
///     <para>
///         <b>Notes.</b> Warnings are returned as <see cref="ImageSourceResult.Diagnostics"/> with the source <c>mermaid</c>,
///         the code <c>mermaid-warning</c>, the specific <c>mermaid-*</c> code in <see cref="ImageSourceDiagnostic.Detail"/>
///         and the line and column in <see cref="ImageSourceDiagnostic.Location"/>; unsupported features use the feature name
///         as the code (for example <see cref="MermaidDiagnosticCodes.DiagramUnsupported"/>). With
///         <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> the first unsupported feature throws
///         <see cref="MermaidUnsupportedFeatureException"/> instead. Invalid text throws <see cref="MermaidSyntaxException"/>,
///         a diagram that cannot be drawn <see cref="MermaidRenderException"/>. The request's cancellation token is passed to
///         the render.
///     </para>
///     <para>
///         <b>Pictures.</b> The pictures of the diagram (<c>@{ img }</c> nodes) load through
///         <see cref="ImageSourceRequest.Resolver"/>, the resolver scoped to the diagram's own file, so they share the budgets,
///         notes and cycle chain of the whole render and are confined like every other embedded file.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme.
///     </para>
/// </remarks>
public sealed class MermaidImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The fragment parameter that sets the theme: <c>theme</c>.
    /// </summary>
    public const string ThemeParameter = "theme";

    /// <summary>
    ///     The code of a warning note; the specific <c>mermaid-*</c> code is in <see cref="ImageSourceDiagnostic.Detail"/>.
    /// </summary>
    public const string WarningCode = "mermaid-warning";

    /// <summary>The extensions this source claims.</summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".mmd", ".mermaid"]);

    /// <summary>The schemes this source claims: none.</summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>The fragment parameters this source declares.</summary>
    private static readonly ReadOnlyCollection<string> Parameters = new([ThemeParameter]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="MermaidImageSource"/> class.
    /// </summary>
    public MermaidImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "mermaid";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters => Parameters;

    /// <summary>
    ///     Draws the diagram of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The diagram at the requested (or natural) size, on a transparent background, with the notes of the library.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown, with <see cref="FolderImageResolver.FragmentInvalidCode"/>, when the <c>theme</c> fragment parameter is not a Mermaid theme.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="MermaidSyntaxException">Thrown when the text is not a valid diagram.</exception>
    /// <exception cref="MermaidRenderException">Thrown when the diagram cannot be drawn (a limit, a layout failure).</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown for an unsupported feature when the request asks for it.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than the limits allow.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The theme comes first: a link that cannot be used costs nothing
        var theme = request.Fragment.GetValue(ThemeParameter);
        if (theme is not null && !MermaidRenderOptions.ThemeNames.Contains(theme, StringComparer.Ordinal))
        {
            throw new ImageSourceRefusedException(
                FolderImageResolver.FragmentInvalidCode,
                $"the '{ThemeParameter}' fragment parameter must be one of {string.Join(", ", MermaidRenderOptions.ThemeNames)}, not '{theme}'");
        }

        MermaidDocument document;
        using (var stream = request.OpenContent())
        {
            document = MermaidDocument.Open(stream, new MermaidOpenOptions { SourceName = request.FileName });
        }

        // The notes keep the specific code and the location of every report
        var diagnostics = new List<ImageSourceDiagnostic>();
        var options = new MermaidRenderOptions
        {
            Theme = theme,
            ImageResolver = request.Resolver,
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

        // The natural size comes from a measure without handlers, so every note is reported once, by the render
        var natural = document.Measure(new MermaidRenderOptions { Theme = theme }, request.CancellationToken);
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
    private static ImageDiagnosticLocation? ToLocation(MermaidLocation location) =>
        location.IsKnown ? new ImageDiagnosticLocation(location.File, location.Line, location.Column, null) : null;

    /// <summary>
    ///     Joins the lines of a message into one.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns>The message on one line.</returns>
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
