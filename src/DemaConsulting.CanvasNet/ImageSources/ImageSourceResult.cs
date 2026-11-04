using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     What an <see cref="IImageSource"/> drew for one request: the picture, the notes about it, and the text it
///     printed.
/// </summary>
/// <remarks>
///     <para>
///         <b>Ownership.</b> The <see cref="Surface"/> belongs to whoever receives the result: the resolver hands it to
///         the renderer, which disposes it after drawing it. A source must therefore return a surface it created for this
///         request and must not keep, cache or reuse it.
///     </para>
///     <para>
///         The record is immutable once built; it is safe to share between threads (the surface is not).
///     </para>
/// </remarks>
public sealed record ImageSourceResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageSourceResult"/> record.
    /// </summary>
    /// <param name="surface">The picture. Must not be null; the receiver of the result owns and disposes it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
    public ImageSourceResult(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Surface = surface;
    }

    /// <summary>
    ///     Gets the picture. Its size is up to the source: a vector source draws at the size
    ///     <see cref="ImageSourceRequest.SizeFor(int, int, bool)"/> gives, a raster returns its natural size and lets
    ///     the renderer resample it once. The renderer fits any size into the placement.
    /// </summary>
    public Surface Surface { get; }

    /// <summary>
    ///     Gets the notes about the picture (unsupported features, warnings). Empty by default.
    /// </summary>
    public IReadOnlyList<ImageSourceDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    ///     Gets the text the file printed while it was drawn (the <c>echo</c> output of an OpenSCAD model, for example).
    ///     Empty by default.
    /// </summary>
    public IReadOnlyList<string> Echo { get; init; } = [];

    /// <summary>
    ///     Gets a more specific name of what was drawn than <see cref="IImageSource.Name"/> (<c>png</c> or <c>jpeg</c>
    ///     for the <c>raster</c> source, for example), or <see langword="null"/> to use the name of the source.
    ///     <see cref="FolderImageResolver"/> reports it as <see cref="EmbeddedImage.Format"/>.
    /// </summary>
    public string? Format { get; init; }
}
