using System.Collections.ObjectModel;
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
///         request and must not keep, cache or reuse it. The record itself is <b>not</b> <see cref="IDisposable"/>: do
///         not put it in a <c>using</c> statement (that does not compile). What needs disposing is
///         <see cref="Surface"/>, by whoever receives the result: a render that goes through the resolver disposes it
///         after drawing, and the caller of <c>FolderImageResolver.Render</c> disposes <c>result.Surface</c>.
///     </para>
///     <para>
///         The record is immutable once built; it is safe to share between threads (the surface is not). The
///         <see cref="Diagnostics"/> and <see cref="Echo"/> lists are copied into read-only snapshots when they are
///         assigned, so the lists a source passed in can be reused or changed afterwards without affecting the result.
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
    ///     The notes, kept as a private read-only snapshot.
    /// </summary>
    private IReadOnlyList<ImageSourceDiagnostic> _diagnostics = ReadOnlyCollection<ImageSourceDiagnostic>.Empty;

    /// <summary>
    ///     The printed lines, kept as a private read-only snapshot.
    /// </summary>
    private IReadOnlyList<string> _echo = ReadOnlyCollection<string>.Empty;

    /// <summary>
    ///     Gets the notes about the picture (unsupported features, warnings). Empty by default. Assigning copies the
    ///     list into a read-only snapshot (<see langword="null"/> becomes empty), so later changes to the list that was
    ///     passed in do not change the result, and the value cannot be modified through a cast.
    /// </summary>
    public IReadOnlyList<ImageSourceDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init => _diagnostics = value is null ? ReadOnlyCollection<ImageSourceDiagnostic>.Empty : new ReadOnlyCollection<ImageSourceDiagnostic>([.. value]);
    }

    /// <summary>
    ///     Gets the text the file printed while it was drawn (the <c>echo</c> output of an OpenSCAD model, for example).
    ///     Empty by default. Assigning copies the list into a read-only snapshot, like <see cref="Diagnostics"/>.
    /// </summary>
    public IReadOnlyList<string> Echo
    {
        get => _echo;
        init => _echo = value is null ? ReadOnlyCollection<string>.Empty : new ReadOnlyCollection<string>([.. value]);
    }

    /// <summary>
    ///     Gets a more specific name of what was drawn than <see cref="IImageSource.Name"/> (<c>png</c> or <c>jpeg</c>
    ///     for the <c>raster</c> source, for example), or <see langword="null"/> to use the name of the source.
    ///     <see cref="FolderImageResolver"/> reports it as <see cref="EmbeddedImage.Format"/>.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Surface"/> is the picture at its own pixel size (a raster image:
    ///     <see langword="true"/>) or was drawn for the size of the request (a vector source such as an SVG file, a model,
    ///     a document page or a chart: <see langword="false"/>, the default). A renderer that needs the natural size of a
    ///     picture, such as the <c>image</c> mark of Vega-Lite, reads it through
    ///     <see cref="IImageNaturalSizeResolver"/> instead of guessing from the size of the surface; a source that returns
    ///     a picture at its own size must set it.
    /// </summary>
    public bool IsNaturalSize { get; init; }
}
