using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     What to render and how, for <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/>.
/// </summary>
/// <remarks>
///     The record is immutable once built; the extra sources it references must be safe to use from the thread that
///     renders.
/// </remarks>
public sealed record ViewerRenderOptions
{
    /// <summary>
    ///     Gets the width in pixels of the picture, or <see langword="null"/> for the natural width (or, when only the
    ///     height is given, the width that keeps the aspect ratio). A vector source draws at the requested size; a raster
    ///     is returned by its source at its natural size, so the viewer resamples it: with both <see cref="Width"/> and
    ///     <see cref="Height"/> it fits the picture inside that box (keeping its aspect ratio, centered on a transparent
    ///     background) so the result is exactly that size, with one dimension it scales to keep the aspect ratio. When
    ///     given, it must be from 1 to <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/> (8192);
    ///     <see cref="CanvasNetViewer"/> throws <see cref="ArgumentOutOfRangeException"/> for any other value before it
    ///     reads the file.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    ///     Gets the height in pixels of the picture, or <see langword="null"/> for the natural height. See
    ///     <see cref="Width"/>; the same range, 1 to <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/>,
    ///     applies.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    ///     Gets the folder that confines the files the rendered file itself includes or embeds (a model's
    ///     <c>include</c>, a chart's data, the pictures of an SVG file), or <see langword="null"/>. For a render by path the
    ///     default is the folder of the file, and the file must be inside the folder; for a render of a stream the default
    ///     is none, so no file the document names is resolved.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>
    ///     Gets a value indicating whether a file that uses a feature its source does not draw fails (the source throws)
    ///     instead of being drawn without it with a note. The default is <see langword="false"/>.
    /// </summary>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>
    ///     Gets your own image sources, or <see langword="null"/> for none. They are placed after the built-in sources,
    ///     so <b>one that claims the same extension or URI scheme as a built-in source wins</b>, and a file type that only
    ///     an extra source claims can be rendered and embedded.
    /// </summary>
    public IEnumerable<IImageSource>? ExtraSources { get; init; }

    /// <summary>
    ///     Gets the most pixels the render may decode and allocate in total (the file itself and every picture it embeds), or
    ///     <see langword="null"/> for the resolver default (<see cref="FolderImageResolverOptions.MaxDecodedPixels"/>,
    ///     64,000,000). The default refuses a single 8192 x 8192 picture; set this to render one large image. A render over
    ///     the budget throws <see cref="ImageSourceBudgetException"/>; an embedded picture over it is skipped with a
    ///     <c>svg-image-too-large</c> note. A negative value throws <see cref="ArgumentOutOfRangeException"/>. The
    ///     budget contract is described in the remarks of <see cref="ImageSourceRequest"/>.
    ///     When the render is post-processed to the requested <see cref="Width"/> and <see cref="Height"/> (a raster, or
    ///     any source that returns another size), the final picture is a second buffer that must fit in what is left of
    ///     this budget after the surface the source produced (peak memory is both): one that does not fit throws
    ///     <see cref="ImageSourceBudgetException"/> as well.
    /// </summary>
    public long? MaxDecodedPixels { get; init; }

    /// <summary>
    ///     Gets the token that cancels the render. The default is <see cref="CancellationToken.None"/>.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
