using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     The pictures of one rendering: the <c>@{ img: ... }</c> nodes and the <c>&lt;img&gt;</c> of labels, loaded only
///     through <see cref="MermaidRenderOptions.ImageResolver"/>, with their natural sizes for the layout and their pixels for
///     the painter.
/// </summary>
/// <remarks>
///     <para>
///         <b>No implicit access.</b> A link is handed, as text, to the caller's resolver and to nothing else; the
///         resolver decides what a link may refer to (a remote or absolute link is refused by
///         <see cref="FolderImageResolver"/>). Without a resolver, or when the resolver gives nothing, the picture is
///         reported once per link as <see cref="MermaidDiagnosticCodes.ImageUnresolved"/> and nothing is drawn.
///     </para>
///     <para>
///         <b>Natural size.</b> Mermaid sizes an image node by the natural size of its picture (the browser's
///         <c>naturalWidth</c> and <c>naturalHeight</c>). The resolver is asked for the picture at
///         <see cref="ProbePixels"/> by <see cref="ProbePixels"/> device pixels, and the size of what it returns is the
///         natural size: a raster comes back at its own size (<see cref="IImageNaturalSizeResolver"/>), and a vector
///         picture, which has no natural size, at the size it was drawn for. The answer is kept for the rest of the
///         rendering.
///     </para>
///     <para>
///         <b>Limit.</b> Every call of the resolver counts against <see cref="MermaidLimits.MaxImages"/>; the call that
///         would pass the limit is not made and fails the rendering with a located <see cref="MermaidRenderException"/>
///         (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>). An exception of the resolver propagates unchanged.
///     </para>
///     <para>
///         An instance belongs to one call on one thread.
///     </para>
/// </remarks>
internal sealed class NodeImages : ISceneImageLoader
{
    /// <summary>The size in device pixels at which a picture is asked for to learn its natural size.</summary>
    public const int ProbePixels = 256;

    /// <summary>The most characters of a link that a message shows.</summary>
    private const int MaxShownLink = 200;

    /// <summary>The resolver of the caller, or null.</summary>
    private readonly IImageResolver? _resolver;

    /// <summary>The most resolver calls of the rendering.</summary>
    private readonly int _maxImages;

    /// <summary>The report sink of the call.</summary>
    private readonly MermaidReportSink _sink;

    /// <summary>The natural size of every link asked for (null for a link that gave nothing).</summary>
    private readonly Dictionary<string, (double Width, double Height)?> _natural = new(StringComparer.Ordinal);

    /// <summary>The location of the first element that names every link.</summary>
    private readonly Dictionary<string, MermaidLocation> _locations = new(StringComparer.Ordinal);

    /// <summary>The links already reported as unresolved.</summary>
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes the pictures of a rendering.
    /// </summary>
    /// <param name="resolver">The resolver of the caller, or null.</param>
    /// <param name="maxImages">The most resolver calls (<see cref="MermaidLimits.MaxImages"/>).</param>
    /// <param name="sink">The report sink of the call.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sink"/> is null.</exception>
    public NodeImages(IImageResolver? resolver, int maxImages, MermaidReportSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _resolver = resolver;
        _maxImages = maxImages;
        _sink = sink;
    }

    /// <summary>
    ///     Gets the number of resolver calls made so far.
    /// </summary>
    public int Calls { get; private set; }

    /// <summary>
    ///     Gets the natural size of a picture, asking the resolver once per link.
    /// </summary>
    /// <param name="href">The link, as the diagram gives it.</param>
    /// <param name="location">The location of the element that shows the picture.</param>
    /// <returns>The natural size, or null when there is no resolver or the resolver gives nothing (reported).</returns>
    /// <exception cref="MermaidRenderException">Thrown when the call would pass <see cref="MermaidLimits.MaxImages"/>.</exception>
    public (double Width, double Height)? NaturalSize(string href, MermaidLocation location)
    {
        ArgumentNullException.ThrowIfNull(href);
        _locations.TryAdd(href, location);
        if (_natural.TryGetValue(href, out var known))
        {
            return known;
        }

        (double Width, double Height)? size = null;
        using (var picture = Resolve(href, ProbePixels, ProbePixels, location))
        {
            if (picture is not null)
            {
                size = (picture.Width, picture.Height);
            }
        }

        _natural[href] = size;
        return size;
    }

    /// <summary>
    ///     Loads a picture at the device size it is drawn (for the painter).
    /// </summary>
    /// <param name="href">The link of the picture.</param>
    /// <param name="pixelWidth">The width in device pixels.</param>
    /// <param name="pixelHeight">The height in device pixels.</param>
    /// <returns>The picture, which the painter disposes, or null when nothing is drawn.</returns>
    /// <exception cref="MermaidRenderException">Thrown when the call would pass <see cref="MermaidLimits.MaxImages"/>.</exception>
    public Surface? Load(string href, int pixelWidth, int pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(href);
        var location = _locations.TryGetValue(href, out var known) ? known : default;
        return Resolve(href, pixelWidth, pixelHeight, location);
    }

    /// <summary>
    ///     Asks the resolver for a picture, counting the call and reporting a link that gives nothing.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="pixelWidth">The width in device pixels.</param>
    /// <param name="pixelHeight">The height in device pixels.</param>
    /// <param name="location">The location for the report and the failure.</param>
    /// <returns>The picture, or null.</returns>
    private Surface? Resolve(string href, int pixelWidth, int pixelHeight, MermaidLocation location)
    {
        if (_resolver is null || href.Length == 0)
        {
            Report(href, location, "no image resolver was given (MermaidRenderOptions.ImageResolver)");
            return null;
        }

        if (Calls >= _maxImages)
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram loads more than the limit of {_maxImages} pictures (MermaidLimits.MaxImages); the picture '{Shown(href)}' was not loaded"),
                location,
                "use fewer pictures or raise MermaidLimits.MaxImages");
        }

        Calls++;
        var picture = _resolver.Resolve(href, Math.Clamp(pixelWidth, 1, Surface.MaxDimension), Math.Clamp(pixelHeight, 1, Surface.MaxDimension));
        if (picture is null)
        {
            Report(href, location, "the image resolver gave no picture for it");
        }

        return picture;
    }

    /// <summary>
    ///     Reports a picture that is not drawn, once per link.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="location">The location.</param>
    /// <param name="reason">Why it is not drawn.</param>
    private void Report(string href, MermaidLocation location, string reason)
    {
        if (_reported.Add(href))
        {
            _sink.Warn(
                MermaidDiagnosticCodes.ImageUnresolved,
                $"the picture '{Shown(href)}' is not drawn: {reason}.",
                location,
                "give an image resolver that can load the link (for example a FolderImageResolver over the diagram's folder)");
        }
    }

    /// <summary>
    ///     Shortens a link for a message.
    /// </summary>
    /// <param name="href">The link.</param>
    /// <returns>The link, cut at <see cref="MaxShownLink"/> characters.</returns>
    private static string Shown(string href) =>
        href.Length <= MaxShownLink ? href : string.Concat(href.AsSpan(0, MaxShownLink), "...");
}
