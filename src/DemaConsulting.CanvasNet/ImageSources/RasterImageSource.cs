// cspell:ignore jpeg tiff jpeg2000 exif
using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The <see cref="IImageSource"/> for raster pictures: PNG, JPEG, BMP, TIFF, GIF and JPEG 2000 files.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fragments.</b> The source claims no fragment parameter (it does not implement
///         <see cref="IFragmentImageSource"/>), so a parameter in the link, such as <c>#page=2</c>, is reported by the
///         resolver as <see cref="FolderImageResolver.FragmentUnknownParameterCode"/> and ignored.
///     </para>
///     <para>
///         The format is decided from the <b>content</b> of the file by <see cref="ImageDecoder"/> (the file name
///         extension only selects this source), so a PNG that is named <c>.jpg</c> still loads. A JPEG is turned upright
///         by its Exif orientation, so the picture looks the way a viewer shows it. The source returns the <b>natural
///         size</b> of the picture and does not resample it: a raster has no sharper rendering at another size, and the
///         renderer that draws the picture resamples it once, with a filter chosen for the placement. Transparency is
///         kept.
///     </para>
///     <para>
///         <b>Memory.</b> A decoded picture takes four bytes per pixel whatever the size of the file, so the source reads
///         the declared size from the header first and charges it (<see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/>;
///         see <see cref="ImageSourceRequest"/> for the budget contract), refusing
///         (<see cref="ImageSourceBudgetException"/>) a picture that does not fit before it decodes anything.
///     </para>
///     <para>
///         The source is stateless and thread-safe; one instance can serve any number of resolvers. It claims no URI
///         scheme. Claim <c>.png</c> (or any of its extensions) in a source of your own, placed after this one, to
///         replace the way a raster is loaded.
///     </para>
/// </remarks>
public sealed class RasterImageSource : IImageSource
{
    /// <summary>
    ///     The extensions this source claims.
    /// </summary>
    private static readonly ReadOnlyCollection<string> ClaimedExtensions =
        new([".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".jp2", ".j2k", ".j2c"]);

    /// <summary>
    ///     The schemes this source claims: none. Read-only, so a caller cannot change what is shared.
    /// </summary>
    private static readonly ReadOnlyCollection<string> NoSchemes = new([]);

    /// <summary>
    ///     Initializes a new instance of the <see cref="RasterImageSource"/> class.
    /// </summary>
    public RasterImageSource()
    {
    }

    /// <inheritdoc />
    public string Name => "raster";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => ClaimedExtensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => NoSchemes;

    /// <summary>
    ///     Decodes the file of a request.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>
    ///     The decoded, upright picture at its natural size, with <see cref="ImageSourceResult.Format"/> set to the
    ///     detected format (<c>png</c>, <c>jpeg</c>, <c>gif</c>, <c>bmp</c>, <c>tiff</c> or <c>jpeg2000</c>). The result has
    ///     no notes and no echo.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content (it was handed over by scheme).</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the content is not a recognized raster format, or the codec rejects it (corrupt or larger than
    ///     <see cref="Surface.MaxDimension"/>).
    /// </exception>
    /// <exception cref="ImageSourceBudgetException">
    ///     Thrown, before anything is decoded, when the size the header declares does not fit in the budget of the request
    ///     (<see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/>).
    /// </exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();
        if (!request.HasContent)
        {
            throw new InvalidOperationException($"The request for '{request.Href}' has no file content; a raster is read from a file.");
        }

        // The content decides the format, not the name, so a mislabeled file still loads
        var format = ImageFormats.Detect(request.Content.Span[..Math.Min(request.Content.Length, ImageFormats.HeaderLength)])
                     ?? throw new InvalidDataException("The data is not a recognized image format.");

        // The declared size is read from the header and checked against the budget BEFORE anything is decoded, so a small
        // file that declares a huge picture never allocates it
        var (declaredWidth, declaredHeight) = ReadDeclaredSize(request, format);
        request.EnsureWithinBudget(declaredWidth, declaredHeight);

        Surface surface;
        using (var stream = request.OpenContent())
        {
            surface = ImageDecoder.Load(stream, format);
        }

        // A JPEG that was stored sideways is turned upright; the orientation is read from the same bytes
        if (format == ImageFormat.Jpeg)
        {
            int? orientation;
            using (var stream = request.OpenContent())
            {
                orientation = JpegCodec.ReadExifOrientation(stream);
            }

            if (orientation is > 1)
            {
                var upright = surface.ApplyExifOrientation(orientation.Value);
                surface.Dispose();
                surface = upright;
            }
        }

        return new ImageSourceResult(surface) { Format = format.ToString().ToLowerInvariant() };
    }

    /// <summary>
    ///     Reads the size the file declares in its header, without decoding any pixel data.
    /// </summary>
    /// <param name="request">The request that carries the file.</param>
    /// <param name="format">The detected format.</param>
    /// <returns>The declared width and height (as the header says, before any limit of the decoder is applied).</returns>
    /// <exception cref="InvalidDataException">Thrown when the header is malformed.</exception>
    /// <remarks>
    ///     The codecs' own <c>GetInfo</c> methods are used because they read the header only and never allocate the
    ///     picture. The exception is GIF: <c>GifCodec.GetInfo</c> also decodes the first frame (to report whether it can be
    ///     decoded), which would allocate the picture it is meant to guard, so the width and height of the Logical Screen
    ///     Descriptor (bytes 6 to 9, little-endian), which is the size <c>GifCodec.Load</c> allocates, are read directly.
    /// </remarks>
    private static (int Width, int Height) ReadDeclaredSize(ImageSourceRequest request, ImageFormat format)
    {
        if (format == ImageFormat.Gif)
        {
            var span = request.Content.Span;
            if (span.Length < 10)
            {
                throw new InvalidDataException("The GIF data is truncated.");
            }

            return (span[6] | (span[7] << 8), span[8] | (span[9] << 8));
        }

        using var stream = request.OpenContent();
        var info = format switch
        {
            ImageFormat.Png => PngCodec.GetInfo(stream),
            ImageFormat.Jpeg => JpegCodec.GetInfo(stream),
            ImageFormat.Bmp => BmpCodec.GetInfo(stream),
            ImageFormat.Tiff => TiffCodec.GetInfo(stream),
            ImageFormat.Jpeg2000 => Jpeg2000Codec.GetInfo(stream),
            _ => throw new InvalidDataException("The data is not a recognized image format.")
        };
        return (info.Width, info.Height);
    }
}
