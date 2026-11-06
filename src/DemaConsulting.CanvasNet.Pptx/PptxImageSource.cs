// cspell:ignore pptx
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     The <see cref="IImageSource"/> for PowerPoint files: it draws one slide of a <c>.pptx</c> file that
///     another document links to (an SVG <c>image</c>, for example): the <b>first slide</b>, unless the link has a
///     fragment such as <c>deck.pptx#slide=3</c> (or <c>#page=3</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Choosing the slide.</b> The fragment parameters <c>slide</c> and <c>page</c> (aliases; 1-based, ASCII
///         digits; the last one of either in the link wins) select the slide; they are declared through
///         <see cref="IFragmentImageSource"/>, so any other parameter is reported by the resolver as unknown. A slide the
///         presentation does not have throws <see cref="ImageSourceRefusedException"/> with the code
///         <see cref="FolderImageResolver.PageOutOfRangeCode"/>, and a value that is not a whole number of 1 or more
///         throws it with <see cref="FolderImageResolver.FragmentInvalidCode"/>; the resolver then draws nothing and
///         reports an <see cref="ImageDiagnosticSeverity.Unsupported"/> note.
///     </para>
///     <para>
///         The natural size of the slide is the size of the presentation at 96 dots per inch. The slide is drawn at the
///         largest size of its aspect ratio that fits inside the requested box
///         (<see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> with <c>fitInside</c>), over a <b>transparent</b>
///         background so the document that embeds it shows through. A picture on the slide that the library cannot decode
///         is skipped and reported as an <see cref="ImageDiagnosticSeverity.Unsupported"/> note (source <c>pptx</c>, code
///         the feature name); with <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> it throws
///         <see cref="PptxUnsupportedFeatureException"/> instead. A file that is not a readable presentation throws the
///         <see cref="PptxDocument"/> exceptions.
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme. The PPTX library cannot be canceled while it
///         draws; the request's cancellation token is checked before the slide is opened and drawn.
///     </para>
/// </remarks>
public sealed class PptxImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The number of English Metric Units in one pixel at 96 dots per inch (914400 per inch).
    /// </summary>
    private const double EmuPerPixel = 9525d;

    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".pptx"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     The fragment parameters this source uses.
    /// </summary>
    private static readonly ReadOnlyCollection<string> SlideParameters = new(["page", "slide"]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="PptxImageSource"/> class.
    /// </summary>
    public PptxImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "pptx";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters => SlideParameters;

    /// <summary>
    ///     Draws the slide of the presentation of a request that its fragment names (the first slide without one).
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The slide on a transparent background, fitted inside the requested box (or at its natural size).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown when the slide does not exist (<see cref="FolderImageResolver.PageOutOfRangeCode"/>) or the <c>slide</c> or <c>page</c> value is not a whole number of 1 or more (<see cref="FolderImageResolver.FragmentInvalidCode"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a readable presentation.</exception>
    /// <exception cref="PptxUnsupportedFeatureException">Thrown for an unsupported picture when the request asks for it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The slide is read before the file is, so a bad value costs nothing
        var number = request.Fragment.GetPositiveInt32("page", "slide") ?? 1;

        using var stream = request.OpenContent();
        using var document = PptxDocument.Open(stream);
        if (number > document.SlideCount)
        {
            throw new ImageSourceRefusedException(
                FolderImageResolver.PageOutOfRangeCode,
                $"slide {number} does not exist: the presentation has {document.SlideCount} {(document.SlideCount == 1 ? "slide" : "slides")}");
        }

        // The slide at 96 dots per inch is the natural size; the slide is fitted inside a requested box without distortion
        var size = document.SlideSize;
        var (width, height) = request.SizeFor(
            Math.Max(1, (int)Math.Round(size.WidthEmu / EmuPerPixel, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(size.HeightEmu / EmuPerPixel, MidpointRounding.AwayFromZero)),
            fitInside: true);
        request.EnsureWithinBudget(width, height);

        var diagnostics = new List<ImageSourceDiagnostic>();
        var options = new PptxRenderOptions
        {
            BackgroundColor = new Rgba32(0, 0, 0, 0),
            StrictUnsupportedPictures = request.ThrowOnUnsupportedFeature,
            OnUnsupportedPictureSkipped = skipped => diagnostics.Add(
                new ImageSourceDiagnostic("pptx", ImageDiagnosticSeverity.Unsupported, skipped.Feature, OneLine(skipped.Message)))
        };

        request.CancellationToken.ThrowIfCancellationRequested();
        var surface = document.Render(number - 1, width, height, options);
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
