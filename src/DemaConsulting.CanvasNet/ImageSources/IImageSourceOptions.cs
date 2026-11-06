namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     Marker interface for caller-defined options of a <i>direct</i> render
///     (<see cref="FolderImageResolver.Render(string, int?, int?, IImageSourceOptions?)"/>): the resolver passes them to
///     the source untouched in <see cref="ImageSourceRequest.Options"/> and never reads them.
/// </summary>
/// <remarks>
///     A link inside a document carries its options in its fragment (<see cref="ImageSourceRequest.Fragment"/>); the
///     top-level file of a direct render has no link, so its caller passes the options this way. An embedded file never
///     inherits the options of the file that embeds it: <see cref="ImageSourceRequest.Options"/> is
///     <see langword="null"/> for a link. A source ignores options of a type it does not know.
/// </remarks>
public interface IImageSourceOptions
{
}
