using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The recording resolver of <see cref="SvgFlattener"/>: it learns from the SVG library which links the drawing
///     shows and at what size, and draws each picture to replace.
/// </summary>
public static partial class SvgFlattener
{
    /// <summary>
    ///     The formats of the pictures every SVG reader shows: the rasters and SVG itself.
    /// </summary>
    private static readonly HashSet<string> PlainFormats = new(
        new[] { SourceKind.Png, SourceKind.Jpeg, SourceKind.Bmp, SourceKind.Tiff, SourceKind.Gif, SourceKind.Jpeg2000, SourceKind.Svg }
            .Select(kind => kind.ToFormatName()),
        StringComparer.Ordinal);

    /// <summary>
    ///     A picture that replaces a link.
    /// </summary>
    /// <param name="Format">The format of the linked file.</param>
    /// <param name="Width">The width in pixels of the picture.</param>
    /// <param name="Height">The height in pixels of the picture.</param>
    /// <param name="PngBytes">The size in bytes of the PNG picture.</param>
    /// <param name="DataUri">The <c>data:image/png;base64,...</c> URI of the picture.</param>
    internal sealed record Capture(string Format, int Width, int Height, long PngBytes, string DataUri);

    /// <summary>
    ///     The resolver the drawing is drawn with once: it records every link the SVG library asks for with its largest
    ///     placement, and afterwards (<see cref="DrawRecorded"/>) draws the links to replace through the resolver of
    ///     <c>canvasnet render</c>, keeping them as PNG pictures. It returns nothing to the SVG library, because the
    ///     drawing itself is not needed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The SVG library asks for each link that is shown with the size of its placement in device pixels, through
    ///         every transform and the <c>viewBox</c> (and never for a <c>data:</c> URI or a <c>#</c> link). A link to a
    ///         file only CanvasNet draws is drawn at that size times the enlargement of the canvas; a link to an SVG
    ///         file is drawn the same way and kept only when the pictures drawn for it include such a file at any depth.
    ///         A raster or an unknown link is not drawn at all.
    ///     </para>
    ///     <para>
    ///         A link that is shown more than once is drawn once, at its largest placement (the most pixels; the first of
    ///         equal ones), so the image count and the pixel budget are charged once for it. The links are drawn in the
    ///         order the drawing first shows them. The resolver applies its sandbox, its limits (64 pictures, 4 levels,
    ///         cycles, the pixel budget), and reports what it could not draw; the sum of the kept pictures is limited to
    ///         the output limit as they are drawn. Not thread-safe: one per flatten.
    ///     </para>
    /// </remarks>
    private sealed class Recorder : IImageResolver
    {
        /// <summary>
        ///     The resolver of <c>canvasnet render</c> that draws the pictures.
        /// </summary>
        private readonly FolderImageResolver _resolver;

        /// <summary>
        ///     The factor that turns a placement on the canvas into the size to draw.
        /// </summary>
        private readonly double _enlarge;

        /// <summary>
        ///     The output limit, which the kept pictures may not exceed together.
        /// </summary>
        private readonly long _maxOutputBytes;

        /// <summary>
        ///     The kept pictures by link.
        /// </summary>
        private readonly Dictionary<string, Capture> _captures = new(StringComparer.Ordinal);

        /// <summary>
        ///     The links to draw, in the order the drawing first shows them, with the kind of the linked file.
        /// </summary>
        private readonly List<(string Href, SourceKind Kind)> _order = [];

        /// <summary>
        ///     The largest placement of each link to draw, as the size to draw.
        /// </summary>
        private readonly Dictionary<string, (int Width, int Height)> _largest = new(StringComparer.Ordinal);

        /// <summary>
        ///     Every link the SVG library asked for.
        /// </summary>
        private readonly HashSet<string> _requested = new(StringComparer.Ordinal);

        /// <summary>
        ///     The total length of the data URIs of the kept pictures.
        /// </summary>
        private long _dataBytes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Recorder"/> class.
        /// </summary>
        /// <param name="resolver">The resolver of <c>canvasnet render</c> that draws the pictures.</param>
        /// <param name="enlarge">The factor that turns a placement on the canvas into the size to draw.</param>
        /// <param name="maxOutputBytes">The output limit.</param>
        public Recorder(FolderImageResolver resolver, double enlarge, long maxOutputBytes)
        {
            _resolver = resolver;
            _enlarge = enlarge;
            _maxOutputBytes = maxOutputBytes;
        }

        /// <summary>
        ///     Gets every link the SVG library asked for, as it was written (trimmed).
        /// </summary>
        public IReadOnlySet<string> Requested => _requested;

        /// <summary>
        ///     Gets the kept pictures by link.
        /// </summary>
        public IReadOnlyDictionary<string, Capture> Captures => _captures;

        /// <summary>
        ///     Records a link the drawing shows, and the size of its placement when it is one to replace; nothing is drawn
        ///     yet (see <see cref="DrawRecorded"/>).
        /// </summary>
        /// <param name="href">The link, trimmed.</param>
        /// <param name="pixelWidth">The width of the placement on the canvas.</param>
        /// <param name="pixelHeight">The height of the placement on the canvas.</param>
        /// <returns>Always <see langword="null"/>: the drawing is not needed.</returns>
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            ArgumentNullException.ThrowIfNull(href);
            _requested.Add(href);

            // Only a file only CanvasNet draws, or an SVG file that may link one, is drawn; a raster is left to the reader
            var kind = SourceLoader.SvgImages.KindOfName(href);
            if (kind is not { } known || (known != SourceKind.Svg && !IsCanvasNetOnly(known)))
            {
                return null;
            }

            // Only the largest placement of a link is kept (the first of equal ones)
            var width = Enlarge(pixelWidth);
            var height = Enlarge(pixelHeight);
            if (!_largest.TryGetValue(href, out var largest))
            {
                _order.Add((href, known));
                _largest[href] = (width, height);
            }
            else if ((long)width * height > (long)largest.Width * largest.Height)
            {
                _largest[href] = (width, height);
            }

            return null;
        }

        /// <summary>
        ///     Draws each recorded link once, at its largest placement, in the order the drawing first showed them, and
        ///     keeps the pictures to replace.
        /// </summary>
        /// <exception cref="ImageSourceException">Thrown when a linked file is found but broken.</exception>
        /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when the kept pictures exceed the output limit.</exception>
        public void DrawRecorded()
        {
            foreach (var (href, kind) in _order)
            {
                var (width, height) = _largest[href];

                // The resolver lists what it drew, nested pictures before the one that embeds them
                var before = _resolver.Embedded.Count;
                using var surface = _resolver.Resolve(href, width, height);
                if (surface is null)
                {
                    continue;
                }

                // An SVG file is kept only when it links a file only CanvasNet draws at any depth
                var composite = _resolver.Embedded.Skip(before).Any(e => e.Depth > 0 && !PlainFormats.Contains(e.Format));
                if (kind == SourceKind.Svg && !composite)
                {
                    continue;
                }

                Keep(href, kind, surface);
            }
        }

        /// <summary>
        ///     Turns a placement size on the canvas into the size to draw.
        /// </summary>
        /// <param name="pixels">The placement size on the canvas.</param>
        /// <returns>The size to draw, from 1 to <see cref="Surface.MaxDimension"/>.</returns>
        private int Enlarge(int pixels)
        {
            // A tolerance keeps a whole number of pixels up to rounding noise from gaining a pixel, as in the SVG library
            return (int)Math.Clamp(Math.Ceiling((pixels * _enlarge) - 1e-3), 1, Surface.MaxDimension);
        }

        /// <summary>
        ///     Keeps a picture as a PNG data URI.
        /// </summary>
        /// <param name="href">The link.</param>
        /// <param name="kind">The kind of the linked file.</param>
        /// <param name="surface">The picture.</param>
        /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when the kept pictures exceed the output limit.</exception>
        private void Keep(string href, SourceKind kind, Surface surface)
        {
            using var png = new MemoryStream();
            PngCodec.Save(surface, png);

            // The size is checked before the text of the URI is made
            var length = DataUriPrefix.Length + (4 * ((png.Length + 2) / 3));
            CheckSize(_dataBytes + length, _maxOutputBytes);
            _dataBytes += length;

            var dataUri = DataUriPrefix + Convert.ToBase64String(png.GetBuffer(), 0, (int)png.Length);
            _captures[href] = new Capture(kind.ToFormatName(), surface.Width, surface.Height, png.Length, dataUri);
        }
    }
}
