using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Loads the pictures of the <see cref="SceneImage"/> items of one painting: asks the host's <see cref="IImageResolver"/>, remembers what it returned for the rest of the
///     painting, counts the calls against <see cref="VegaLiteLimits.MaxImages"/> and owns every surface it hands out.
/// </summary>
/// <remarks>
///     <para>
///         <b>One painting.</b> An instance serves one paint of one chart on one thread and must be disposed when the painting ends; it disposes every surface the resolver returned.
///         The contract of <see cref="IImageResolver"/> says that nothing may assume a cache (a live source is asked again), and the resolver returns a fresh surface for every call.
///         A chart that draws the same picture for every row (an icon for each point) would call the resolver for every row, so within one painting the result of a call is kept and shared by
///         the items that ask for the same link at the same device size. This is a deliberate decision of a static renderer: one painting shows one state of the picture.
///     </para>
///     <para>
///         <b>Limit.</b> A call that is not answered from this painting's cache counts as one image (whatever the resolver returns, null included); the call that would be number
///         <see cref="VegaLiteLimits.MaxImages"/> plus one is not made and fails the render with a <see cref="VegaLiteRenderException"/> located at the mark. A resolver that answers
///         <see langword="null"/> is reported once per link as <c>vl-mark-image-unresolved</c> (a warning); the answer is cached, so the resolver is not asked again for it. An
///         exception of the resolver propagates unchanged.
///     </para>
///     <para>
///         <b>No implicit file access.</b> Without a resolver nothing is loaded and nothing is reported by this class (the feature catalog reports <c>vl-mark-image</c>).
///     </para>
/// </remarks>
internal sealed class SceneImageSource : IDisposable
{
    /// <summary>The identifier of the warning for a link that the resolver did not resolve.</summary>
    public const string UnresolvedId = "vl-mark-image-unresolved";

    /// <summary>The most characters of a link that a message shows.</summary>
    private const int MaxShownUrl = 200;

    /// <summary>The resolver of the host, or null.</summary>
    private readonly IImageResolver? _resolver;

    /// <summary>The most resolver calls of one painting.</summary>
    private readonly int _maxImages;

    /// <summary>Records a warning (message with identifier, location, key).</summary>
    private readonly Action<string, VegaLiteLocation, string?> _warn;

    /// <summary>What the resolver returned for each link and device size (null for a link it did not resolve).</summary>
    private readonly Dictionary<(string Url, int Width, int Height), (Surface? Surface, bool? Natural)> _cache = [];

    /// <summary>The number of resolver calls made so far.</summary>
    private int _calls;

    /// <summary>
    ///     Initializes a source of pictures.
    /// </summary>
    /// <param name="resolver">The resolver of the host, or null when the host gave none.</param>
    /// <param name="maxImages">The most resolver calls (<see cref="VegaLiteLimits.MaxImages"/>).</param>
    /// <param name="warn">Records a warning: the message that starts with the identifier, the location, and the key that tells two warnings with one message apart (or null).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="warn"/> is null.</exception>
    public SceneImageSource(IImageResolver? resolver, int maxImages, Action<string, VegaLiteLocation, string?> warn)
    {
        ArgumentNullException.ThrowIfNull(warn);
        _resolver = resolver;
        _maxImages = maxImages;
        _warn = warn;
    }

    /// <summary>
    ///     Gets a value indicating whether the host gave a resolver.
    /// </summary>
    public bool HasResolver => _resolver is not null;

    /// <summary>
    ///     Gets the picture of a link at a device size.
    /// </summary>
    /// <param name="url">The link, as the chart gives it.</param>
    /// <param name="pixelWidth">The width in device pixels of the rectangle the picture is drawn in, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="pixelHeight">The height in device pixels of the rectangle, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="location">The location of the mark, for the report and the failure.</param>
    /// <returns>The surface, which belongs to this instance (the caller must not dispose or change it); null when there is no resolver or the resolver gave nothing.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, located at the mark, when the call would be more than <see cref="VegaLiteLimits.MaxImages"/>.</exception>
    public Surface? Get(string url, int pixelWidth, int pixelHeight, VegaLiteLocation location) =>
        Get(url, pixelWidth, pixelHeight, location, out _);

    /// <summary>
    ///     Gets the picture of a link at a device size, and whether it is at its own pixel size.
    /// </summary>
    /// <param name="url">The link, as the chart gives it.</param>
    /// <param name="pixelWidth">The width in device pixels of the rectangle the picture is drawn in, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="pixelHeight">The height in device pixels of the rectangle, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="location">The location of the mark, for the report and the failure.</param>
    /// <param name="isNaturalSize">
    ///     Receives <see langword="true"/> for a raster at its own pixel size and <see langword="false"/> for a picture drawn for the request, as the resolver says
    ///     (<see cref="IImageNaturalSizeResolver"/>); <see langword="null"/> when the resolver does not say (a resolver of the host that only implements <see cref="IImageResolver"/>)
    ///     or nothing was returned.
    /// </param>
    /// <returns>The surface, which belongs to this instance (the caller must not dispose or change it); null when there is no resolver or the resolver gave nothing.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, located at the mark, when the call would be more than <see cref="VegaLiteLimits.MaxImages"/>.</exception>
    public Surface? Get(string url, int pixelWidth, int pixelHeight, VegaLiteLocation location, out bool? isNaturalSize)
    {
        isNaturalSize = null;
        if (_resolver is null)
        {
            return null;
        }

        var key = (url, pixelWidth, pixelHeight);
        if (_cache.TryGetValue(key, out var known))
        {
            isNaturalSize = known.Natural;
            return known.Surface;
        }

        if (_calls >= _maxImages)
        {
            throw new VegaLiteRenderException(
                $"The chart draws more than the limit of {_maxImages} pictures (MaxImages); the picture '{Shown(url)}' was not loaded.",
                location);
        }

        _calls++;
        Surface? surface;
        bool? natural = null;
        if (_resolver is IImageNaturalSizeResolver aware)
        {
            surface = aware.Resolve(url, pixelWidth, pixelHeight, out var flag);
            natural = surface is null ? null : flag;
        }
        else
        {
            surface = _resolver.Resolve(url, pixelWidth, pixelHeight);
        }

        _cache[key] = (surface, natural);
        if (surface is null)
        {
            _warn(
                $"{UnresolvedId}: the image resolver gave no picture for '{Shown(url)}'; the picture is not drawn.",
                location,
                url);
        }

        isNaturalSize = natural;
        return surface;
    }

    /// <summary>
    ///     Disposes every surface that the resolver returned.
    /// </summary>
    public void Dispose()
    {
        foreach (var entry in _cache.Values)
        {
            entry.Surface?.Dispose();
        }

        _cache.Clear();
    }

    /// <summary>
    ///     Shortens a link for a message.
    /// </summary>
    /// <param name="url">The link.</param>
    /// <returns>The link, cut at <see cref="MaxShownUrl"/> characters.</returns>
    private static string Shown(string url) =>
        url.Length <= MaxShownUrl ? url : string.Concat(url.AsSpan(0, MaxShownUrl), "...");
}
