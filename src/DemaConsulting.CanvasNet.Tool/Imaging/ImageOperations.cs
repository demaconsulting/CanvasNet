// cspell:ignore deskew otsu midtones
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
///     Rotates the image clockwise by any angle.
/// </summary>
/// <param name="Degrees">The angle in degrees from 0 up to (not including) 360, clockwise; 90, 180, and 270 are exact pixel moves, 0 is a copy.</param>
/// <param name="Fill">The color that fills the corners an oblique angle exposes, or <see langword="null"/> for the pipeline's default rule.</param>
/// <param name="Filter">The resampling filter: <c>nearest</c> copies pixels, every other value interpolates bilinearly.</param>
internal sealed record RotateOperation(double Degrees, Rgba32? Fill = null, ResamplingMode Filter = ResamplingMode.Auto) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "rotate";
}

/// <summary>
///     Replaces the color of every pixel with its luma.
/// </summary>
internal sealed record GrayscaleOperation : IImageOperation
{
    /// <inheritdoc />
    public string Name => "grayscale";
}

/// <summary>
///     Complements the color of every pixel.
/// </summary>
internal sealed record InvertOperation : IImageOperation
{
    /// <inheritdoc />
    public string Name => "invert";
}

/// <summary>
///     Turns every pixel into opaque black or opaque white by its luma.
/// </summary>
/// <param name="Level">The threshold from 0 to 255, or <see langword="null"/> to choose it automatically (Otsu's method).</param>
internal sealed record ThresholdOperation(int? Level) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "threshold";
}

/// <summary>
///     Maps the levels of the color channels through a black point, a white point, and a gamma.
/// </summary>
/// <param name="Black">The level that becomes 0, 0 to 254.</param>
/// <param name="White">The level that becomes 255, 1 to 255 and above the black level.</param>
/// <param name="Gamma">The gamma, 0.1 to 10; above 1 brightens the midtones.</param>
internal sealed record LevelsOperation(int Black, int White, double Gamma) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "levels";
}

/// <summary>
///     Adds an amount to every level of the color channels.
/// </summary>
/// <param name="Amount">The amount, -255 to 255.</param>
internal sealed record BrightnessOperation(int Amount) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "brightness";
}

/// <summary>
///     Spreads or squeezes the levels of the color channels around mid-gray.
/// </summary>
/// <param name="Amount">The contrast, -100 to 100; 0 changes nothing.</param>
internal sealed record ContrastOperation(double Amount) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "contrast";
}

/// <summary>
///     Stretches the levels so that the darkest and lightest levels of the image become black and white.
/// </summary>
/// <param name="ClipPercent">The percentage of the pixels ignored at each end, 0 to 20.</param>
internal sealed record AutoLevelsOperation(double ClipPercent) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "auto-levels";
}

/// <summary>
///     Estimates the skew of the near-horizontal lines of the image and rotates it level when the correction fits.
/// </summary>
/// <param name="Fill">The color that fills the exposed corners, or <see langword="null"/> for the pipeline's default rule.</param>
/// <param name="Filter">The resampling filter of the rotation.</param>
internal sealed record DeskewOperation(Rgba32? Fill = null, ResamplingMode Filter = ResamplingMode.Auto) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "deskew";
}

/// <summary>
///     Crops the image to the content inside a uniform border.
/// </summary>
/// <param name="Tolerance">The largest distance from the background color that is still background, 0 to 255.</param>
internal sealed record TrimOperation(int Tolerance) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "trim";
}

/// <summary>
///     Draws a labeled coordinate grid over the image.
/// </summary>
/// <param name="Spacing">The distance in pixels between two lines, 8 to 4096.</param>
/// <param name="Color">The color of the lines and labels, or <see langword="null"/> for <see cref="GridOverlay.DefaultColor"/>.</param>
internal sealed record GridOperation(int Spacing, Rgba32? Color) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "grid";
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
