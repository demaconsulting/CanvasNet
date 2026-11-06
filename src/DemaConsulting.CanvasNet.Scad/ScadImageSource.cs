// cspell:ignore scad openscad
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The <see cref="IImageSource"/> for OpenSCAD models: it draws a <c>.scad</c> file that another document links to
///     (an SVG <c>image</c>, for example) as a picture of the model.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fragments.</b> The source claims no fragment parameter (it does not implement
///         <see cref="IFragmentImageSource"/>), so a parameter in the link, such as <c>#page=2</c>, is reported by the
///         resolver as <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and ignored. (Choosing the viewpoint with <c>#view=front</c> is a feature of the built-in sources of <c>CanvasNetViewer</c>, which draw with the loader of the viewer; this stand-alone source takes none.)
///     </para>
///     <para>
///         The model is drawn with the view the library itself chooses for it (see
///         <see cref="ScadDocument.Render(int, int, ScadRenderOptions?, CancellationToken)"/>): the fitted isometric
///         view for a model with 3D objects, a flat top-down view for a model that is entirely 2D, or the view of the
///         model's own <c>$vpr</c>, <c>$vpt</c>, <c>$vpd</c> and <c>$vpf</c> assignments; the projection is orthographic.
///         The picture has the size of the placement (<see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> with a natural size of
///         <see cref="NaturalSize"/> pixels square), over a <b>transparent</b> background so the document that embeds it
///         shows through. The <c>include</c> and <c>use</c> directives of the model resolve against
///         <see cref="ImageSourceRequest.Directory"/>, the folder of the file, and the Scad library confines them to it;
///         a directive whose file is missing from that folder is reported as a <c>scad-warning</c> note, not an error. A
///         request without a <see cref="ImageSourceRequest.Directory"/> has no place to look, so a model that uses
///         <c>include</c> or <c>use</c> then fails with <see cref="ScadEvaluationException"/>.
///     </para>
///     <para>
///         The warnings, unsupported features and <c>echo</c> output of the model are returned as
///         <see cref="ImageSourceResult.Diagnostics"/> (source <c>scad</c>; the code of an unsupported feature is the
///         feature name) and <see cref="ImageSourceResult.Echo"/>; with
///         <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> the first unsupported feature throws
///         <see cref="ScadUnsupportedFeatureException"/> instead. A syntax error throws <see cref="ScadSyntaxException"/>,
///         an evaluation failure <see cref="ScadEvaluationException"/>. The request's cancellation token is passed to the
///         render.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme.
///     </para>
/// </remarks>
public sealed class ScadImageSource : IImageSource
{
    /// <summary>
    ///     The width and height in pixels of a model when no size is requested: a model has no natural size.
    /// </summary>
    public const int NaturalSize = 800;

    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".scad"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScadImageSource"/> class.
    /// </summary>
    public ScadImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "scad";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <summary>
    ///     Draws the model of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The picture of the model on a transparent background, with the notes and echo lines of the model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="ScadSyntaxException">Thrown when the model has a syntax error.</exception>
    /// <exception cref="ScadEvaluationException">Thrown when the model cannot be evaluated (a failed <c>assert</c>, a limit).</exception>
    /// <exception cref="ScadUnsupportedFeatureException">Thrown for an unsupported feature when the request asks for it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        var (width, height) = request.SizeFor(NaturalSize, NaturalSize);
        request.EnsureWithinBudget(width, height);
        var diagnostics = new List<ImageSourceDiagnostic>();
        var echo = new List<string>();

        // The model is parsed from the bytes; its includes are looked up in the folder of the file and nowhere else
        ScadDocument document;
        using (var stream = request.OpenContent())
        {
            document = ScadDocument.Open(stream, new ScadOpenOptions { BaseDirectory = request.Directory, SourceName = request.FileName });
        }

        // No view is chosen here: the library picks it (isometric for 3D, top-down for flat 2D, or the model's own $vp*
        // variables). The picture is drawn straight onto a transparent background so the embedding document shows through
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions
            {
                EchoHandler = echo.Add,
                WarningHandler = warning => diagnostics.Add(
                    new ImageSourceDiagnostic("scad", ImageDiagnosticSeverity.Warning, "scad-warning", OneLine(warning.ToString()))),
                UnsupportedFeatureHandler = unsupported => diagnostics.Add(
                    new ImageSourceDiagnostic(
                        "scad",
                        ImageDiagnosticSeverity.Unsupported,
                        unsupported.Feature,
                        OneLine($"{unsupported.Location}: {unsupported.Message}"))),
                ThrowOnUnsupportedFeature = request.ThrowOnUnsupportedFeature
            },
            Projection = CameraProjection.Orthographic,
            SceneOptions = new SceneRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) }
        };

        var surface = document.Render(width, height, options, request.CancellationToken);
        return new ImageSourceResult(surface) { Diagnostics = diagnostics, Echo = echo };
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
