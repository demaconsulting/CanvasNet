using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     One step of the image pipeline: a transformation of the pixels that is applied after the source has been loaded.
/// </summary>
/// <remarks>
///     The steps are parsed from the command line by <see cref="ImagePipeline.Parse"/> and carried out, in the
///     order they were written, by <see cref="ImagePipeline.Apply"/>. A step is a plain description of what to do;
///     sizes that depend on the image (percentages) are resolved when the step is applied.
/// </remarks>
internal interface IImageOperation
{
    /// <summary>
    ///     Gets the name of the operation as it appears in the <c>operations</c> list of a result, for example <c>crop</c>.
    /// </summary>
    string Name { get; }
}

/// <summary>
///     Cuts a rectangle out of the image.
/// </summary>
/// <param name="Region">The rectangle: an origin and a size, each in pixels or percent of the image at that point of the pipeline.</param>
internal sealed record CropOperation(GeometrySpec Region) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "crop";
}

/// <summary>
///     Resamples the image to an exact size.
/// </summary>
/// <param name="Width">The width in pixels, 1 to <see cref="Surface.MaxDimension"/>.</param>
/// <param name="Height">The height in pixels, 1 to <see cref="Surface.MaxDimension"/>.</param>
/// <param name="Filter">The resampling filter.</param>
internal sealed record ResizeOperation(int Width, int Height, ResamplingMode Filter) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "resize";
}

/// <summary>
///     Resamples the image, keeping its aspect ratio, to the largest size that fits inside a box.
/// </summary>
/// <param name="Width">The width of the box in pixels, 1 to <see cref="Surface.MaxDimension"/>.</param>
/// <param name="Height">The height of the box in pixels, 1 to <see cref="Surface.MaxDimension"/>.</param>
/// <param name="AllowUpscale"><see langword="true"/> to enlarge an image that is smaller than the box; otherwise it is only ever shrunk.</param>
/// <param name="Filter">The resampling filter.</param>
internal sealed record FitOperation(int Width, int Height, bool AllowUpscale, ResamplingMode Filter) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "fit";
}

/// <summary>
///     Rotates the image clockwise by a right angle.
/// </summary>
/// <param name="Degrees">The angle: 90, 180, or 270.</param>
internal sealed record RotateOperation(int Degrees) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "rotate";
}

/// <summary>
///     Mirrors the image.
/// </summary>
/// <param name="Axis">The axis, or axes, to mirror about.</param>
internal sealed record FlipOperation(FlipAxis Axis) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "flip";
}

/// <summary>
///     Adds a border of a solid color around the image.
/// </summary>
/// <param name="Border">The width of the border on each side, in pixels.</param>
/// <param name="Color">The color of the border, or <see langword="null"/> for the default that the pipeline is given.</param>
internal sealed record PadOperation(PadSpec Border, Rgba32? Color) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "pad";
}
