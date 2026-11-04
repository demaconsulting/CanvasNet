// cspell:ignore pptx
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     The <see cref="IImageSource"/> for PowerPoint files: it draws the <b>first slide</b> of a <c>.pptx</c> file that
///     another document links to (an SVG <c>image</c>, for example).
/// </summary>
/// <remarks>
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
public sealed class PptxImageSource : IImageSource
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

    /// <summary>
    ///     Draws the first slide of the presentation of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The first slide on a transparent background, fitted inside the requested box (or at its natural size).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the size to draw at does not fit in the budget of the request (<see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a readable presentation.</exception>
    /// <exception cref="PptxUnsupportedFeatureException">Thrown for an unsupported picture when the request asks for it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        using var stream = request.OpenContent();
        using var document = PptxDocument.Open(stream);

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
