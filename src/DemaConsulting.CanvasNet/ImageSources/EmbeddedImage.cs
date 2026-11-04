// cspell:ignore href
namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     One picture a <see cref="FolderImageResolver"/> loaded and handed to the renderer.
/// </summary>
/// <param name="Href">The link as written in the document.</param>
/// <param name="Format">
///     The format that was drawn: <see cref="ImageSourceResult.Format"/>, or <see cref="IImageSource.Name"/> when the
///     source reported none.
/// </param>
/// <param name="Width">The width in pixels of the surface that was returned (the natural width of a raster).</param>
/// <param name="Height">The height in pixels of the surface that was returned.</param>
public sealed record EmbeddedImage(string Href, string Format, int Width, int Height)
{
    /// <summary>
    ///     Gets how many files deep the image was embedded: <c>0</c> for an image embedded by the top-level document,
    ///     <c>1</c> for one embedded by a file that the top-level document embeds, and so on.
    /// </summary>
    public int Depth { get; init; }
}
