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
///         <b>Fragments.</b> The source declares one family of fragment parameters (it implements
///         <see cref="IFragmentImageSource"/>): every <c>def.&lt;name&gt;=&lt;value&gt;</c> sets the variable <c>name</c> of the
///         model, exactly like <c>-D name=value</c> of the tool: it replaces every top-level assignment to the name and
///         creates the variable when the model has none (see <see cref="ScadEvaluationOptions.Variables"/>). The value is a
///         number, <c>true</c>, <c>false</c> or text (one pair of quotes is removed; see
///         <see cref="ImageParameterValues.TryParseVariable(string, out object?, out string?)"/>), percent-decoded after the
///         fragment is split. A value that is not usable, or a name the evaluator does not accept, refuses the link with
///         <see cref="FolderImageResolver.FragmentInvalidCode"/> and the picture is not drawn. Any other parameter, such as
///         <c>#page=2</c>, is reported by the resolver as <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and
///         ignored. (Choosing the viewpoint with <c>#view=front</c> is a feature of the built-in sources of
///         <c>CanvasNetViewer</c>, which draw with the loader of the viewer; this stand-alone source takes none.)
///     </para>
///     <para>
///         The model is drawn with the view the library itself chooses for it (see
///         <see cref="ScadDocument.Render(int, int, ScadRenderOptions?, CancellationToken)"/>): the fitted isometric
///         view for a model with 3D objects, a flat top-down view for a model that is entirely 2D, or the view of the
///         model's own <c>$vpr</c>, <c>$vpt</c>, <c>$vpd</c> and <c>$vpf</c> assignments; the projection is orthographic.
///         The picture has the size of the placement (<see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> with a natural size of
///         <see cref="NaturalSize"/> pixels square), over a <b>transparent</b> background so the document that embeds it
///         shows through. The <c>include</c> and <c>use</c> directives of the model resolve against
///         <see cref="ImageSourceRequest.Directory"/>, the folder of the file, and are read under
///         <see cref="ImageSourceRequest.FileAccess"/>, the file-access policy of the render (a denied file is a
///         <see cref="ScadEvaluationException"/>); a directive whose file is missing is reported as a <c>scad-warning</c>
///         note, not an error. A
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
public sealed class ScadImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The prefix of the fragment parameters that set a variable of the model: <c>def.</c>.
    /// </summary>
    public const string DefinePrefix = "def.";

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
    ///     The fragment parameter names this source declares: none (its parameters are all prefixed).
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoParameters = new([]);

    /// <summary>
    ///     The fragment parameter prefixes this source declares.
    /// </summary>
    private static readonly ReadOnlyCollection<string> Prefixes = new([DefinePrefix]);

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

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters => NoParameters;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameterPrefixes => Prefixes;

    /// <summary>
    ///     Draws the model of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The picture of the model on a transparent background, with the notes and echo lines of the model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown, with <see cref="FolderImageResolver.FragmentInvalidCode"/>, when a <c>def.</c> fragment parameter is not usable.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="ScadSyntaxException">Thrown when the model has a syntax error.</exception>
    /// <exception cref="ScadEvaluationException">Thrown when the model cannot be evaluated (a failed <c>assert</c>, a limit).</exception>
    /// <exception cref="ScadUnsupportedFeatureException">Thrown for an unsupported feature when the request asks for it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The overrides come first: a link that cannot be used costs nothing
        var variables = ReadVariables(request.Fragment);
        var (width, height) = request.SizeFor(NaturalSize, NaturalSize);
        request.EnsureWithinBudget(width, height);
        var diagnostics = new List<ImageSourceDiagnostic>();
        var echo = new List<string>();

        // The model is parsed from the bytes; its includes are looked up in the folder of the file and nowhere else
        ScadDocument document;
        using (var stream = request.OpenContent())
        {
            document = ScadDocument.Open(
                stream,
                new ScadOpenOptions { BaseDirectory = request.Directory, SourceName = request.FileName, FileAccess = request.FileAccess });
        }

        // No view is chosen here: the library picks it (isometric for 3D, top-down for flat 2D, or the model's own $vp*
        // variables). The picture is drawn straight onto a transparent background so the embedding document shows through
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions
            {
                Variables = variables.Count > 0 ? variables : null,
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
    ///     Reads the variables the fragment of a link sets.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    /// <returns>The values by variable name; empty when the link sets none.</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown when a value is not usable or the evaluator does not accept a name.</exception>
    private static IReadOnlyDictionary<string, object?> ReadVariables(ImageFragment fragment)
    {
        var variables = ImageParameterValues.ReadVariables(fragment, DefinePrefix);
        if (variables.Count > 0)
        {
            try
            {
                // The evaluator says which names it accepts, so it is asked rather than guessed
                _ = new ScadEvaluationOptions { Variables = variables };
            }
            catch (ArgumentException ex)
            {
                var message = OneLine(ex.Message);

                // The name of the argument that an ArgumentException appends means nothing to the author of a link
                var argument = message.LastIndexOf(" (Parameter '", StringComparison.Ordinal);
                message = argument > 0 ? message[..argument] : message;
                throw new ImageSourceRefusedException(
                    FolderImageResolver.FragmentInvalidCode,
                    $"a '{DefinePrefix}' fragment parameter is not valid: {(message.Length > 160 ? message[..160] + "..." : message)}");
            }
        }

        return variables;
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
