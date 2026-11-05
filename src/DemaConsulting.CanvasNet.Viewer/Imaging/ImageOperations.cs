// cspell:ignore deskew otsu midtones
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     One step of the image pipeline: a transformation of the pixels that is applied after the source has been loaded.
/// </summary>
/// <remarks>
///     The steps are carried out, in the order they are given, by <see cref="ImagePipeline.Apply"/>; the
///     <c>canvasnet</c> tool builds them from its command line. A step is a plain description of what to do;
///     sizes that depend on the image (percentages) are resolved when the step is applied.
/// </remarks>
public interface IImageOperation
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
/// <remarks>
///     <para>
///         The region is always an origin and a size; there is no "inset" or negative value. A negative or non-finite
///         number is refused with the code <c>usage-error</c> (the origin must be 0 or more, the size greater than 0, and a
///         percentage at most 100). A percentage is of the size of the image <b>at this step of the pipeline</b>, so after
///         a resize it is of the resized image; the four edges are rounded to the nearest pixel (half away from zero). A
///         region that runs past the right or bottom edge is cut off at the edge (the applied operation reports
///         <c>clamped</c>); one that starts at or beyond the right or bottom edge is a <c>usage-error</c>.
///     </para>
///     <para>
///         To take 20 pixels off every side of a <c>w</c> x <c>h</c> image, start at 20, 20 and keep
///         <c>w - 40</c> x <c>h - 40</c>; when the size is not known, 5%, 5%, 90%, 90% takes 5% off every side.
///     </para>
/// </remarks>
/// <example>
///     Crop 20 pixels off every side of an image whose size is known from its facts (the first step of a pipeline), or 5%
///     off every side without knowing it:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer;
///     using DemaConsulting.CanvasNet.Viewer.Imaging;
///
///     var info = SourceLoader.Probe("photo.png");
///     var inPixels = new CropOperation(new GeometrySpec(
///         Dimension.Pixels(20), Dimension.Pixels(20),
///         Dimension.Pixels(info.Width - 40), Dimension.Pixels(info.Height - 40)));
///     var inPercent = new CropOperation(new GeometrySpec(
///         Dimension.Percent(5), Dimension.Percent(5), Dimension.Percent(90), Dimension.Percent(90)));
///
///     var request = new RenderRequest("cropped.png") { Operations = [inPixels] };
///     ImageRenderer.Execute(info, request);
///     </code>
/// </example>
public sealed record CropOperation(GeometrySpec Region) : IImageOperation
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
public sealed record ResizeOperation(int Width, int Height, ResamplingMode Filter = ResamplingMode.Auto) : IImageOperation
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
public sealed record FitOperation(int Width, int Height, bool AllowUpscale = false, ResamplingMode Filter = ResamplingMode.Auto) : IImageOperation
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
public sealed record RotateOperation(double Degrees, Rgba32? Fill = null, ResamplingMode Filter = ResamplingMode.Auto) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "rotate";
}

/// <summary>
///     Replaces the color of every pixel with its luma.
/// </summary>
public sealed record GrayscaleOperation : IImageOperation
{
    /// <inheritdoc />
    public string Name => "grayscale";
}

/// <summary>
///     Complements the color of every pixel.
/// </summary>
public sealed record InvertOperation : IImageOperation
{
    /// <inheritdoc />
    public string Name => "invert";
}

/// <summary>
///     Turns every pixel into opaque black or opaque white by its luma.
/// </summary>
/// <param name="Level">The threshold from 0 to 255, or <see langword="null"/> to choose it automatically (Otsu's method).</param>
public sealed record ThresholdOperation(int? Level) : IImageOperation
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
public sealed record LevelsOperation(int Black, int White, double Gamma) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "levels";
}

/// <summary>
///     Adds an amount to every level of the color channels.
/// </summary>
/// <param name="Amount">The amount, -255 to 255.</param>
public sealed record BrightnessOperation(int Amount) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "brightness";
}

/// <summary>
///     Spreads or squeezes the levels of the color channels around mid-gray.
/// </summary>
/// <param name="Amount">The contrast, -100 to 100; 0 changes nothing.</param>
public sealed record ContrastOperation(double Amount) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "contrast";
}

/// <summary>
///     Stretches the levels so that the darkest and lightest levels of the image become black and white.
/// </summary>
/// <param name="ClipPercent">
///     The percentage of the pixels ignored at each end of the histogram, 0 to <see cref="Surface.MaxAutoLevelsClipPercent"/>
///     (20); the default is <see cref="Surface.DefaultAutoLevelsClipPercent"/>, 1 percent, which is also the default of the
///     <c>auto-levels</c> command.
/// </param>
/// <remarks>
///     A flat image (one with a single level) has nothing to stretch and is left unchanged; the applied operation then
///     reports <c>applied: false</c>.
/// </remarks>
public sealed record AutoLevelsOperation(double ClipPercent = Surface.DefaultAutoLevelsClipPercent) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "auto-levels";
}

/// <summary>
///     Estimates the skew of the near-horizontal lines of the image and rotates it level when the correction fits.
/// </summary>
/// <param name="Fill">The color that fills the exposed corners, or <see langword="null"/> for the pipeline's default rule.</param>
/// <param name="Filter">The resampling filter of the rotation.</param>
public sealed record DeskewOperation(Rgba32? Fill = null, ResamplingMode Filter = ResamplingMode.Auto) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "deskew";
}

/// <summary>
///     Crops the image to the content inside a uniform border.
/// </summary>
/// <param name="Tolerance">The largest distance from the background color that is still background, 0 to 255.</param>
public sealed record TrimOperation(int Tolerance) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "trim";
}

/// <summary>
///     Draws a labeled coordinate grid over the image.
/// </summary>
/// <param name="Spacing">The distance in pixels between two lines, 8 to 4096.</param>
/// <param name="Color">The color of the lines and labels, or <see langword="null"/> for <see cref="GridOverlay.DefaultColor"/>.</param>
public sealed record GridOperation(int Spacing, Rgba32? Color) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "grid";
}

/// <summary>
///     Mirrors the image.
/// </summary>
/// <param name="Axis">The axis, or axes, to mirror about.</param>
public sealed record FlipOperation(FlipAxis Axis) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "flip";
}

/// <summary>
///     Adds a border of a solid color around the image.
/// </summary>
/// <param name="Border">The width of the border on each side, in pixels.</param>
/// <param name="Color">The color of the border, or <see langword="null"/> for the default that the pipeline is given.</param>
public sealed record PadOperation(PadSpec Border, Rgba32? Color) : IImageOperation
{
    /// <inheritdoc />
    public string Name => "pad";
}

/// <summary>
///     A length that is either an absolute number of pixels or a percentage of the image's size.
/// </summary>
/// <remarks>
///     A length is never negative: a crop refuses a negative value (and a size of 0) with the code <c>usage-error</c>, and
///     a percentage is from 0 to 100. <see cref="Pixels(double)"/> and <see cref="Percent(double)"/> are the readable ways
///     to create one.
/// </remarks>
/// <param name="Value">The number: pixels when <see cref="IsPercent"/> is <see langword="false"/>, percent (0-100) otherwise.</param>
/// <param name="IsPercent"><see langword="true"/> when <paramref name="Value"/> is a percentage.</param>
public readonly record struct Dimension(double Value, bool IsPercent)
{
    /// <summary>
    ///     Creates a length in pixels.
    /// </summary>
    /// <param name="value">The number of pixels; the operation that uses it checks its range.</param>
    /// <returns>The length, with <see cref="IsPercent"/> <see langword="false"/>.</returns>
    public static Dimension Pixels(double value) => new(value, false);

    /// <summary>
    ///     Creates a length that is a percentage of the image's size.
    /// </summary>
    /// <param name="value">The percentage, 0 to 100; the operation that uses it checks its range.</param>
    /// <returns>The length, with <see cref="IsPercent"/> <see langword="true"/>.</returns>
    public static Dimension Percent(double value) => new(value, true);
}

/// <summary>
///     A rectangle given as an origin and a size, each component in pixels or percent.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <remarks>
///     Percentages of <paramref name="X"/> and <paramref name="Width"/> are relative to the image width;
///     percentages of <paramref name="Y"/> and <paramref name="Height"/> to the image height, in both cases the size at
///     that step of the pipeline. Nothing here is negative: the origin is 0 or more and the size is greater than 0, so
///     a margin is expressed by its complement (for 20 pixels off every side of a <c>w</c> x <c>h</c> image:
///     <c>20, 20, w - 40, h - 40</c>). See <see cref="CropOperation"/> for the full rules.
/// </remarks>
public readonly record struct GeometrySpec(Dimension X, Dimension Y, Dimension Width, Dimension Height);

/// <summary>
///     The widths of a border, in pixels, on each side of an image.
/// </summary>
/// <param name="Left">The left border width.</param>
/// <param name="Top">The top border width.</param>
/// <param name="Right">The right border width.</param>
/// <param name="Bottom">The bottom border width.</param>
public readonly record struct PadSpec(int Left, int Top, int Right, int Bottom);

/// <summary>
///     The axis (or axes) an image is mirrored about.
/// </summary>
public enum FlipAxis
{
    /// <summary>Mirror left to right (about the vertical axis).</summary>
    Horizontal,

    /// <summary>Mirror top to bottom (about the horizontal axis).</summary>
    Vertical,

    /// <summary>Mirror about both axes, which is a rotation by 180 degrees.</summary>
    Both
}
