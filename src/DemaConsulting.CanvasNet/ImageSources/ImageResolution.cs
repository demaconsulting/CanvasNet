using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     What <see cref="IImagePositionResolver.ResolveWithPositions"/> returns: the picture and the positions of its
///     features, from the same render.
/// </summary>
/// <remarks>
///     <para>
///         <b>Ownership.</b> The <see cref="Surface"/> belongs to the caller, which disposes it after drawing it; the
///         record itself is not <see cref="IDisposable"/>.
///     </para>
///     <para>
///         The positions are in the pixels of <see cref="Surface"/> (see <see cref="ImagePosition.Pixel"/>), so a renderer
///         maps them through the same placement it draws the surface with. The list is copied into a read-only snapshot
///         when it is assigned. The record is immutable once built (the surface is not).
///     </para>
/// </remarks>
public sealed record ImageResolution
{
    /// <summary>
    ///     The positions, kept as a private read-only snapshot.
    /// </summary>
    private readonly IReadOnlyList<ImagePosition>? _positions;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageResolution"/> record.
    /// </summary>
    /// <param name="surface">The picture. Must not be null; the caller owns and disposes it.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
    public ImageResolution(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Surface = surface;
    }

    /// <summary>
    ///     Gets the picture, as <see cref="IImageResolver.Resolve(string, int, int)"/> would return it.
    /// </summary>
    public Surface Surface { get; }

    /// <summary>
    ///     Gets the positions the source reported for the selection, in the pixels of <see cref="Surface"/> and in the
    ///     order the source delivered them, or <see langword="null"/> (the default) when the source reports no positions.
    ///     Assigning copies the list into a read-only snapshot.
    /// </summary>
    public IReadOnlyList<ImagePosition>? Positions
    {
        get => _positions;
        init => _positions = value is null ? null : new ReadOnlyCollection<ImagePosition>([.. value]);
    }

    /// <summary>
    ///     Gets a value indicating whether matching positions were left out of <see cref="Positions"/> because of the cap
    ///     of <see cref="PositionCollector.MaxPositions"/>.
    /// </summary>
    public bool PositionsTruncated { get; init; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Surface"/> is a raster at its own pixel size (see
    ///     <see cref="ImageSourceResult.IsNaturalSize"/>).
    /// </summary>
    public bool IsNaturalSize { get; init; }
}
