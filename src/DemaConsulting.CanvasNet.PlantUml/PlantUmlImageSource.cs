using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     The <see cref="IImageSource"/> for PlantUML diagrams: it draws a <c>.puml</c>, <c>.plantuml</c> or <c>.pu</c> file
///     that another document links to (an SVG <c>image</c>, a Mermaid node picture, a Markdown picture) as a picture of
///     the diagram.
/// </summary>
/// <remarks>
///     <para>
///         The diagram is drawn at the size of the placement (<see cref="ImageSourceRequest.SizeFor(int, int, bool)"/>
///         with the diagram's natural size, the size <see cref="PlantUmlDocument.Measure"/> gives), over a
///         <b>transparent</b> background so the document that embeds it shows through. The source takes no fragment
///         parameters: a link such as <c>flow.puml#theme=dark</c> is drawn and its parameter is reported by the resolver
///         as unknown. The <c>!include</c> directives of the diagram resolve against
///         <see cref="ImageSourceRequest.Directory"/>, the folder of the file, and are read under
///         <see cref="ImageSourceRequest.FileAccess"/>, the file-access policy of the render; a request without a folder
///         has no place to look, so a diagram that includes a file then fails with a <see cref="PlantUmlSyntaxException"/>.
///     </para>
///     <para>
///         The warnings of the diagram are returned as <see cref="ImageSourceResult.Diagnostics"/> with the source
///         <c>plantuml</c>, the code <see cref="WarningCode"/>, and the specific <c>plantuml-*</c> code in
///         <see cref="ImageSourceDiagnostic.Detail"/>; an unsupported feature is returned with its code as the code of
///         the note. With <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> the first unsupported feature throws
///         <see cref="PlantUmlUnsupportedFeatureException"/> instead. A diagram of a type this version does not draw is an
///         empty transparent picture with a <see cref="PlantUmlDiagnosticCodes.DiagramUnsupported"/> note. The request's
///         cancellation token is passed to the render.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme.
///     </para>
/// </remarks>
public sealed class PlantUmlImageSource : IImageSource
{
    /// <summary>
    ///     The code of a warning note; the specific <c>plantuml-*</c> code is in <see cref="ImageSourceDiagnostic.Detail"/>.
    /// </summary>
    public const string WarningCode = "plantuml-warning";

    /// <summary>The extensions this source claims.</summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".puml", ".plantuml", ".pu"]);

    /// <summary>The schemes this source claims: none.</summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>The transparent background of an embedded picture.</summary>
    private static readonly Rgba32 Transparent = new(0, 0, 0, 0);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PlantUmlImageSource"/> class.
    /// </summary>
    public PlantUmlImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "plantuml";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <summary>
    ///     Draws the diagram of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The diagram at the requested (or natural) size, on a transparent background, with the notes of the library.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the text is not a valid diagram or an included file cannot be read.</exception>
    /// <exception cref="PlantUmlRenderException">Thrown when the diagram cannot be drawn (a limit, a layout failure).</exception>
    /// <exception cref="PlantUmlUnsupportedFeatureException">Thrown for an unsupported feature when the request asks for it.</exception>
    /// <exception cref="InvalidDataException">Thrown when the text is longer than the limits allow.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The includes of the diagram are looked up in the folder of the file and nowhere else
        PlantUmlDocument document;
        using (var stream = request.OpenContent())
        {
            document = PlantUmlDocument.Open(
                stream,
                new PlantUmlOpenOptions { SourceName = request.FileName, BaseDirectory = request.Directory, FileAccess = request.FileAccess });
        }

        // The notes keep the specific code and the location of every report
        var diagnostics = new List<ImageSourceDiagnostic>();
        var options = new PlantUmlRenderOptions
        {
            Background = Transparent,
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
        var natural = document.MeasureNatural(new PlantUmlRenderOptions(), request.CancellationToken);
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
    private static ImageDiagnosticLocation? ToLocation(PlantUmlLocation location) =>
        location.IsKnown ? new ImageDiagnosticLocation(location.File, location.Line, location.Column, null) : null;

    /// <summary>
    ///     Joins the lines of a message into one.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns>The message on one line.</returns>
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
