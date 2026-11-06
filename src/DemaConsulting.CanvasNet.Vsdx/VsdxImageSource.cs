// cspell:ignore vsdx visio
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Vsdx;

/// <summary>
///     The <see cref="IImageSource"/> for Visio files: it draws one page of a <c>.vsdx</c> file that another
///     document links to (an SVG <c>image</c>, for example): the <b>first page</b>, unless the link has a fragment such
///     as <c>drawing.vsdx#page=2</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Choosing the page.</b> The fragment parameter <c>page</c> (1-based, ASCII digits) selects the page; it is
///         declared through <see cref="IFragmentImageSource"/>, so any other parameter is reported by the resolver as
///         unknown. A page the drawing does not have throws <see cref="ImageSourceRefusedException"/> with the code
///         <see cref="FolderImageResolver.PageOutOfRangeCode"/>, and a value that is not a whole number of 1 or more
///         throws it with <see cref="FolderImageResolver.FragmentInvalidCode"/>; the resolver then draws nothing and
///         reports an <see cref="ImageDiagnosticSeverity.Unsupported"/> note. The size is that of the chosen page.
///     </para>
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
public sealed class VsdxImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The number of English Metric Units in one pixel at 96 dots per inch (914400 per inch).
    /// </summary>
    private const double EmuPerPixel = 9525d;

    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".vsdx"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     The fragment parameters this source uses.
    /// </summary>
    private static readonly ReadOnlyCollection<string> PageParameters = new(["page"]);

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
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters => PageParameters;

    /// <summary>
    ///     Draws the page of the drawing of a request that its fragment names (the first page without one).
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The page on a transparent background, fitted inside the requested box (or at its natural size).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown when the page does not exist (<see cref="FolderImageResolver.PageOutOfRangeCode"/>) or the <c>page</c> value is not a whole number of 1 or more (<see cref="FolderImageResolver.FragmentInvalidCode"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a readable drawing.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The page is read before the file is, so a bad value costs nothing
        var number = request.Fragment.GetPositiveInt32("page") ?? 1;

        using var stream = request.OpenContent();
        using var document = VsdxDocument.Open(stream);
        if (number > document.PageCount)
        {
            throw new ImageSourceRefusedException(
                FolderImageResolver.PageOutOfRangeCode,
                $"page {number} does not exist: the document has {document.PageCount} {(document.PageCount == 1 ? "page" : "pages")}");
        }

        // The page at 96 dots per inch is the natural size; the page is fitted inside a requested box without distortion
        var index = number - 1;
        var page = document.GetPageSize(index);
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
        var surface = document.Render(index, width, height, options);
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
