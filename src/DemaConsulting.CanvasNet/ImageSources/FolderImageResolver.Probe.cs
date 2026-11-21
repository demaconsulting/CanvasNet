using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The probe of the resolver: a load that only measures a picture, whose success leaves no trace in the lists of the
///     render.
/// </summary>
public sealed partial class FolderImageResolver : FolderImageResolver.IImageProbe
{
    /// <summary>
    ///     A resolver that can load a picture only to measure it, without listing the load.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Some renderers ask for a picture twice: once to learn its natural size, which the layout needs before
    ///         anything is drawn, and once to draw it at the size the layout gave it (the Mermaid library does this for
    ///         the picture of a node). <see cref="Embedded"/> holds one entry for every image that was loaded and handed
    ///         to the renderer, so the measuring load would list the picture twice. A renderer that finds this interface
    ///         on its <see cref="IImageResolver"/> measures through <see cref="Probe(string, int, int)"/> instead, and the
    ///         picture is listed once, by the load that draws it.
    ///     </para>
    ///     <para>
    ///         The interface is internal: it is implemented by <see cref="FolderImageResolver"/> and by the scope a source
    ///         receives in <see cref="ImageSourceRequest.Resolver"/>, and only the renderers of this family (through
    ///         <c>InternalsVisibleTo</c>) use it. It does not change the public contract of the resolver.
    ///     </para>
    /// </remarks>
    internal interface IImageProbe
    {
        /// <summary>
        ///     Loads a picture only to measure it: exactly like <see cref="IImageResolver.Resolve(string, int, int)"/>,
        ///     except that a picture that is returned is not listed.
        /// </summary>
        /// <remarks>
        ///     When a picture is returned, the probe and everything it caused (the pictures the file embeds, their
        ///     requests, notes and printed lines) are removed from <see cref="Embedded"/>, <see cref="Requested"/>,
        ///     <see cref="Echo"/> and <see cref="Diagnostics"/>; the load that draws the picture lists them again. When
        ///     nothing is returned, everything is kept, so a link that is not drawn keeps its warning (and the strict
        ///     policy still sees it). The load counts against <see cref="FolderImageResolverOptions.MaxImages"/> and the
        ///     pixel budget either way, as any other load does.
        /// </remarks>
        /// <param name="href">The link as written.</param>
        /// <param name="pixelWidth">The device-pixel width to measure at; passed to the source.</param>
        /// <param name="pixelHeight">The device-pixel height to measure at; passed to the source.</param>
        /// <returns>
        ///     The picture, which the caller owns and disposes, or <see langword="null"/> when the link was not drawn (a
        ///     warning is added to <see cref="Diagnostics"/>).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="href"/> is null.</exception>
        /// <exception cref="ImageSourceException">Thrown when the file was found but its source failed to draw it.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <see cref="FolderImageResolverOptions.CancellationToken"/> is canceled.</exception>
        Surface? Probe(string href, int pixelWidth, int pixelHeight);
    }

    /// <inheritdoc />
    Surface? IImageProbe.Probe(string href, int pixelWidth, int pixelHeight)
    {
        return ProbeIn(_rootScope, href, pixelWidth, pixelHeight);
    }

    /// <summary>
    ///     Resolves a link inside a scope only to measure it, and removes a successful load from the lists of the render.
    /// </summary>
    /// <remarks>
    ///     The lists only grow during a load, and the loads of nested pictures happen inside it (the resolver is not
    ///     thread-safe, so nothing else adds to them meanwhile); cutting each list back to its length before the load
    ///     therefore removes exactly what the probe and its nested pictures added. The image count and the budget are
    ///     not given back: the picture was loaded.
    /// </remarks>
    /// <param name="scope">The scope that contains the link.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="pixelWidth">The device-pixel width to measure at.</param>
    /// <param name="pixelHeight">The device-pixel height to measure at.</param>
    /// <returns>The picture, or <see langword="null"/> when the link was not drawn (then nothing is removed).</returns>
    private Surface? ProbeIn(Scope scope, string href, int pixelWidth, int pixelHeight)
    {
        // Remember where every list ends before the load
        var embedded = _embedded.Count;
        var requested = _requested.Count;
        var echo = _echo.Count;
        var diagnostics = _diagnostics.Count;

        // Load exactly as a draw would, so the measured picture is the one the draw will get
        var surface = ResolveIn(scope, href, pixelWidth, pixelHeight);

        // A measured picture is listed by the load that draws it, so the probe leaves no entry, request, note or line
        if (surface is not null)
        {
            _embedded.RemoveRange(embedded, _embedded.Count - embedded);
            _requested.RemoveRange(requested, _requested.Count - requested);
            _echo.RemoveRange(echo, _echo.Count - echo);
            _diagnostics.RemoveRange(diagnostics, _diagnostics.Count - diagnostics);
        }

        return surface;
    }

    /// <summary>
    ///     The scope's side of the probe: a source that measures a picture of its own file measures it from this scope.
    /// </summary>
    private sealed partial class Scope : IImageProbe
    {
        /// <inheritdoc />
        public Surface? Probe(string href, int pixelWidth, int pixelHeight)
        {
            return _owner.ProbeIn(this, href, pixelWidth, pixelHeight);
        }
    }
}
