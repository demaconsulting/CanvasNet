// cspell:ignore href hrefs
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     Turns the link of a picture (the <c>href</c> of an SVG <c>image</c> element, for example) into a
///     <see cref="Surface"/>.
/// </summary>
/// <remarks>
///     <para>
///         This is the only thing a document renderer needs from the host to draw linked pictures, so the renderer (the
///         SVG library) depends on this contract and on no format. <see cref="FolderImageResolver"/> is the
///         implementation that resolves links relative to the document under its
///         <see cref="FolderImageResolverOptions.FileAccess"/> policy
///         (<see cref="DemaConsulting.CanvasNet.Files.FileAccessPolicy.AllowAll"/>, every regular local file, by
///         default); an application may implement the interface itself (for example to serve pictures from memory or
///         from a network allow-list).
///     </para>
///     <para>
///         <b>What an implementation must do.</b> Return a surface the caller owns (the caller disposes it after drawing,
///         so never return a surface that is kept or reused), or <see langword="null"/> when nothing should be drawn
///         (the renderer then draws nothing and carries on). Return a fresh surface on every call: nothing in the
///         contract allows a caller to cache results, and a live source (a video frame) depends on being asked again. The
///         <c>href</c> is untrusted text from a document, so an implementation that touches the file system
///         or the network must decide what an untrusted link may reach (a
///         <see cref="DemaConsulting.CanvasNet.Files.FileAccessPolicy"/>, for example). Exceptions propagate to the caller of the renderer unchanged. An
///         implementation need not be <see cref="IDisposable"/>; <see cref="FolderImageResolver"/> is not.
///     </para>
///     <para>
///         <b>Thread safety.</b> The contract does not require an implementation to be thread-safe; a renderer calls it
///         from the thread that renders. <see cref="FolderImageResolver"/> is not thread-safe.
///     </para>
/// </remarks>
public interface IImageResolver
{
    /// <summary>
    ///     Loads the picture a link refers to.
    /// </summary>
    /// <param name="href">
    ///     The link, as written in the document (trimmed of surrounding white space by the SVG codec). Never empty,
    ///     never a <c>data:</c> URI and never a same-document <c>#fragment</c> when called by the SVG codec.
    /// </param>
    /// <param name="pixelWidth">
    ///     The width, in device pixels, of the rectangle the picture is placed in, from 1 to
    ///     <see cref="Surface.MaxDimension"/>. A vector source should draw at this size to be sharp; the renderer still
    ///     fits whatever is returned into the placement rectangle.
    /// </param>
    /// <param name="pixelHeight">
    ///     The height, in device pixels, of the placement rectangle, from 1 to <see cref="Surface.MaxDimension"/>.
    /// </param>
    /// <returns>
    ///     The picture, which the caller owns and disposes, or <see langword="null"/> when nothing should be drawn.
    /// </returns>
    Surface? Resolve(string href, int pixelWidth, int pixelHeight);
}
