using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     An <see cref="IImageResolver"/> that can also report where the features of the picture it drew are, from the same
///     render that produced the picture.
/// </summary>
/// <remarks>
///     <para>
///         A document that points at a feature of an embedded picture (an SVG position binding such as
///         <c>cn:x2y2="#flow node:send"</c>) needs the position of the feature in the pixels of the very surface that is
///         drawn; asking the picture's source twice could give another layout. This optional interface returns the
///         surface and its positions together (<see cref="ImageResolution"/>). It is opt-in metadata: a renderer asks for
///         positions only for the pictures a binding targets, with the union of their selectors, and the request is
///         independent of the <c>--positions</c> option of a direct render.
///     </para>
///     <para>
///         The contract of <see cref="IImageResolver"/> applies unchanged: the caller owns and disposes the surface, a
///         fresh result is returned on every call, and <see langword="null"/> means nothing should be drawn (the reason
///         is the resolver's own diagnostics). A resolver that does not implement this interface keeps working; a
///         renderer then reports the binding as unsupported. <see cref="FolderImageResolver"/> implements it, and so does
///         the resolver it hands to the files it embeds. The interface does not require thread safety.
///     </para>
/// </remarks>
public interface IImagePositionResolver : IImageResolver
{
    /// <summary>
    ///     Loads the picture a link refers to and the positions of the features its source reports for a selection.
    /// </summary>
    /// <param name="href">The link, as written in the document (trimmed); never empty, a <c>data:</c> URI or a <c>#</c> link.</param>
    /// <param name="pixelWidth">The width, in device pixels, of the placement rectangle, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="pixelHeight">The height, in device pixels, of the placement rectangle, from 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="positions">The selectors of the positions wanted; must not be null.</param>
    /// <returns>
    ///     The picture and its positions, which the caller owns (it disposes <see cref="ImageResolution.Surface"/>), or
    ///     <see langword="null"/> when nothing should be drawn. <see cref="ImageResolution.Positions"/> is
    ///     <see langword="null"/> when the source of the picture reports no positions (a raster, a document page).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="href"/> or <paramref name="positions"/> is null.</exception>
    ImageResolution? ResolveWithPositions(string href, int pixelWidth, int pixelHeight, PositionSelection positions);
}
