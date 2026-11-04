// cspell:ignore vsdx visio
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Vsdx;

/// <summary>
///     The <see cref="IImageSource"/> for Visio files: it draws the <b>first page</b> of a <c>.vsdx</c> file that another
///     document links to (an SVG <c>image</c>, for example).
/// </summary>
/// <remarks>
///     <para>
///         The natural size of the page is its size at 96 dots per inch. The page is drawn at the largest size of its
///         aspect ratio that fits inside the requested box
///         (<see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> with <c>fitInside</c>), over a <b>transparent</b>
///         background so the document that embeds it shows through. A feature of the drawing that the library does not
///         draw is reported as an <see cref="ImageDiagnosticSeverity.Unsupported"/> note (source <c>vsdx</c>, code the
///         feature name); with <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> it throws instead. A file that is
///         not a readable drawing throws the <see cref="VsdxDocument"/> exceptions.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme. The VSDX library cannot be canceled while it
///         draws; the request's cancellation token is checked before the page is opened and drawn.
///     </para>
/// </remarks>
public sealed class VsdxImageSource : IImageSource
{
    /// <summary>
    ///     The number of English Metric Units in one pixel at 96 dots per inch (914400 per inch).
    /// </summary>
    private const double EmuPerPixel = 9525d;

    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly string[] ClaimedExtensions = [".vsdx"];

    /// <summary>
    ///     Initializes a new instance of the <see cref="VsdxImageSource"/> class.
    /// </summary>
    public VsdxImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "vsdx";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => [];

    /// <summary>
    ///     Draws the first page of the drawing of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The first page on a transparent background, fitted inside the requested box (or at its natural size).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the size to draw at does not fit in the budget of the request (<see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a readable drawing.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        using var stream = request.OpenContent();
        using var document = VsdxDocument.Open(stream);

        // The page at 96 dots per inch is the natural size; the page is fitted inside a requested box without distortion
        var page = document.GetPageSize(0);
        var (width, height) = request.SizeFor(
            Math.Max(1, (int)Math.Round(page.WidthEmu / EmuPerPixel, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(page.HeightEmu / EmuPerPixel, MidpointRounding.AwayFromZero)),
            fitInside: true);
        request.EnsureWithinBudget(width, height);

        var diagnostics = new List<ImageSourceDiagnostic>();
        var options = new VsdxRenderOptions
        {
            BackgroundColor = new Rgba32(0, 0, 0, 0),
            ThrowOnUnsupportedFeature = request.ThrowOnUnsupportedFeature,
            UnsupportedFeatureHandler = feature => diagnostics.Add(
                new ImageSourceDiagnostic("vsdx", ImageDiagnosticSeverity.Unsupported, feature.Feature, OneLine(feature.Message)))
        };

        request.CancellationToken.ThrowIfCancellationRequested();
        var surface = document.Render(0, width, height, options);
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
