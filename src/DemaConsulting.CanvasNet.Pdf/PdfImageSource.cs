// cspell:ignore pdf
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Pdf;

/// <summary>
///     The <see cref="IImageSource"/> for PDF files: it draws one page of a <c>.pdf</c> file that another
///     document links to (an SVG <c>image</c>, for example): the <b>first page</b>, unless the link has a fragment such
///     as <c>report.pdf#page=3</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Choosing the page.</b> The fragment parameter <c>page</c> (1-based, ASCII digits) selects the page; it is
///         declared through <see cref="IFragmentImageSource"/>, so any other parameter is reported by the resolver as
///         unknown. A page the document does not have throws <see cref="ImageSourceRefusedException"/> with the code
///         <see cref="FolderImageResolver.PageOutOfRangeCode"/>, and a value that is not a whole number of 1 or more
///         throws it with <see cref="FolderImageResolver.FragmentInvalidCode"/>; the resolver then draws nothing and
///         reports an <see cref="ImageDiagnosticSeverity.Unsupported"/> note. The size is that of the chosen page.
///     </para>
///     <para>
///         The natural size of the page is its displayed (rotation-adjusted) size at 96 dots per inch. The page is drawn
///         at the largest size of its aspect ratio that fits inside the requested box
///         (<see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> with <c>fitInside</c>), so it is sharp at the size of
///         the placement and never distorted, over a <b>transparent</b> background so the document that embeds it shows
///         through. A document without pages, a password-protected file and a corrupt file throw the
///         <see cref="PdfDocument"/> exceptions; the source returns no notes because the PDF library has no diagnostics
///         channel (<see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> has no effect).
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme. The PDF library cannot be canceled while it
///         draws; the request's cancellation token is checked before the page is opened and drawn.
///     </para>
/// </remarks>
public sealed class PdfImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The number of device pixels per point at 96 dots per inch (a point is 1/72 inch).
    /// </summary>
    private const double PixelsPerPoint = 96d / 72d;

    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".pdf"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     The fragment parameters this source uses.
    /// </summary>
    private static readonly ReadOnlyCollection<string> PageParameters = new(["page"]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PdfImageSource"/> class.
    /// </summary>
    public PdfImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "pdf";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters => PageParameters;

    /// <summary>
    ///     Draws the page of the PDF file of a request that its fragment names (the first page without one).
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The page on a transparent background, fitted inside the requested box (or at its natural size).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown when the page does not exist (<see cref="FolderImageResolver.PageOutOfRangeCode"/>) or the <c>page</c> value is not a whole number of 1 or more (<see cref="FolderImageResolver.FragmentInvalidCode"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a readable PDF (see <see cref="PdfDocument.Open(Stream, string?)"/>).</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The page is read before the file is, so a bad value costs nothing
        var number = request.Fragment.GetPositiveInt32("page") ?? 1;

        using var stream = request.OpenContent();
        using var document = PdfDocument.Open(stream);
        if (number > document.PageCount)
        {
            throw new ImageSourceRefusedException(
                FolderImageResolver.PageOutOfRangeCode,
                $"page {number} does not exist: the document has {document.PageCount} {(document.PageCount == 1 ? "page" : "pages")}");
        }

        // The page at 96 dots per inch is the natural size; the page is fitted inside a requested box without distortion
        var index = number - 1;
        var page = document.GetPageInfo(index);
        var (width, height) = request.SizeFor(
            Math.Max(1, (int)Math.Round(page.Width * PixelsPerPoint, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(page.Height * PixelsPerPoint, MidpointRounding.AwayFromZero)),
            fitInside: true);
        request.EnsureWithinBudget(width, height);

        request.CancellationToken.ThrowIfCancellationRequested();
        var surface = document.Render(index, width, height, new PdfRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) });
        return new ImageSourceResult(surface);
    }
}
