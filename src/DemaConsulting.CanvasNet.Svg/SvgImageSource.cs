// cspell:ignore href
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Svg;

/// <summary>
///     The <see cref="IImageSource"/> for SVG files: it draws an <c>.svg</c> file that another document links to.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fragments.</b> The source claims no fragment parameter (it does not implement
///         <see cref="IFragmentImageSource"/>), so a parameter in the link, such as <c>#page=2</c>, is reported by the
///         resolver as <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and ignored.
///     </para>
///     <para>
///         The file is drawn by <see cref="SvgCodec"/> at the size <see cref="ImageSourceRequest.SizeFor(int, int, bool)"/>
///         gives from the natural size of the SVG file, so a vector picture is as sharp as its placement allows; when both
///         dimensions are requested the surface is exactly that box and the codec fits the drawing into it by the
///         <c>preserveAspectRatio</c> of the file's own root element. The background stays transparent so the document
///         that embeds it shows through.
///     </para>
///     <para>
///         The <c>image</c> elements of the linked file are resolved through <see cref="ImageSourceRequest.Resolver"/>,
///         the resolver scoped to this file: its relative links resolve against the folder of the file itself, and the
///         depth, cycle and count limits of the whole render apply, so SVG files that embed each other terminate with a
///         warning. When the request has no resolver the file's own external links are not drawn.
///     </para>
///     <para>
///         The SVG library neither reports unsupported features nor has a switch to fail on them, so
///         <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> has no effect and the result carries no
///         diagnostics. Text uses the fonts installed on the host (the default of <see cref="SvgLoadOptions"/>).
///     </para>
///     <para>
///         The source is stateless and thread-safe and claims no URI scheme.
///     </para>
/// </remarks>
public sealed class SvgImageSource : IImageSource
{
    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions = new([".svg"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="SvgImageSource"/> class.
    /// </summary>
    public SvgImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "svg";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <summary>
    ///     Draws the SVG file of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The picture at the requested (or natural) size, with a transparent background and no notes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the surface to allocate does not fit in the pixel budget (see <see cref="ImageSourceRequest"/>).</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="InvalidDataException">Thrown when the content is not a valid SVG document.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is already canceled.</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();

        // The natural size comes from the header (the root element), so the file is parsed once for it and once to draw
        ImageInfo info;
        using (var stream = request.OpenContent())
        {
            info = SvgCodec.GetInfo(stream);
        }

        // The SVG canvas is charged here, BEFORE the nested links are loaded (the codec allocates its own canvas before it
        // resolves any image), so the nested pictures see only what is left of the live budget
        var (width, height) = request.SizeFor(info.Width, info.Height);
        request.EnsureWithinBudget(width, height);

        // The nested links go through the scoped resolver, which shares the live budget, so recursion is bounded
        using var content = request.OpenContent();
        var surface = SvgCodec.LoadWithFontFaces(content, width, height, null, new SvgLoadOptions { ImageResolver = request.Resolver });
        return new ImageSourceResult(surface);
    }
}
