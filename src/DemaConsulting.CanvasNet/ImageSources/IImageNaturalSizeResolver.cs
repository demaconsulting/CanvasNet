// cspell:ignore href
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     An <see cref="IImageResolver"/> that also says whether the picture it returned is at its own pixel size.
/// </summary>
/// <remarks>
///     <para>
///         A renderer that places a picture by its natural size (the <c>image</c> mark of Vega-Lite draws a raster at its
///         pixel size, in the units of the chart) cannot tell from the returned <see cref="Surface"/> whether it is a raster
///         at its own size or a vector picture that was drawn for the requested size: a raster whose pixel size happens to
///         equal the request looks like a vector drawing. This interface carries that fact from the source
///         (<see cref="ImageSourceResult.IsNaturalSize"/>) to the renderer. <see cref="FolderImageResolver"/> implements it,
///         and so does the resolver that it hands to the files it embeds.
///     </para>
///     <para>
///         The contract of <see cref="IImageResolver"/> applies unchanged to the first overload, which is inherited. An
///         implementation that cannot tell does not implement this interface; a renderer then falls back to its own
///         judgment.
///     </para>
/// </remarks>
public interface IImageNaturalSizeResolver : IImageResolver
{
    /// <summary>
    ///     Loads the picture a link refers to and tells whether it is at its own pixel size.
    /// </summary>
    /// <param name="href">The link, as written in the document.</param>
    /// <param name="pixelWidth">The width, in device pixels, of the rectangle the picture is placed in.</param>
    /// <param name="pixelHeight">The height, in device pixels, of the rectangle the picture is placed in.</param>
    /// <param name="isNaturalSize">
    ///     Receives <see langword="true"/> when the returned picture is a raster at its own pixel size (see
    ///     <see cref="ImageSourceResult.IsNaturalSize"/>) and <see langword="false"/> when it was drawn for the request or
    ///     when nothing is returned.
    /// </param>
    /// <returns>
    ///     The picture, which the caller owns and disposes, or <see langword="null"/> when nothing should be drawn.
    /// </returns>
    Surface? Resolve(string href, int pixelWidth, int pixelHeight, out bool isNaturalSize);
}
