// cspell:ignore lanczos canvasnet
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The ordered image pipeline: parses the transformation options of a command line (crop, resize, fit, rotate,
///     flip, pad) and applies them, in the order written, to a surface.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is shared by the <c>render</c> command and the verb commands <c>crop</c>, <c>resize</c>,
///         <c>rotate</c>, <c>flip</c>, and <c>pad</c>: a verb is <c>render</c> with one operation, taken from its
///         positional operand, placed before the operations given as options. The operations are applied left to
///         right, so <c>--crop</c> followed by <c>--resize</c> gives a different image than the reverse. One decode
///         and one encode serve the whole chain; every intermediate surface is disposed as soon as the next one
///         exists.
///     </para>
///     <para>
///         The transformations are written over <see cref="Surface"/> rows, because the surface itself offers only
///         crop, resize, and composite. Rotation is limited to the right angles, which are exact pixel moves. A
///         result larger than <see cref="Surface.MaxDimension"/> is refused before it is allocated, with the code
///         <c>size-limit</c>.
///     </para>
///     <para>
///         The loader's own sizing (<c>--scale</c>, <c>--width</c>, <c>--height</c>, <c>--size</c> of a raster,
///         and the page size of a vector source) happens before the first operation of the pipeline.
///     </para>
/// </remarks>
internal static class ImagePipeline
{
    /// <summary>
    ///     The name of the option that crops.
    /// </summary>
    public const string CropOption = "--crop";

    /// <summary>
    ///     The name of the option that resizes to an exact size.
    /// </summary>
    public const string ResizeOption = "--resize";

    /// <summary>
    ///     The name of the option that fits the image inside a box.
    /// </summary>
    public const string FitOption = "--fit";

    /// <summary>
    ///     The name of the option that rotates.
    /// </summary>
    public const string RotateOption = "--rotate";

    /// <summary>
    ///     The name of the option that flips.
    /// </summary>
    public const string FlipOption = "--flip";

    /// <summary>
    ///     The name of the option that adds a border.
    /// </summary>
    public const string PadOption = "--pad";

    /// <summary>
    ///     The name of the option that sets the color of a border.
    /// </summary>
    public const string PadColorOption = "--pad-color";

    /// <summary>
    ///     The name of the option that lets <see cref="FitOption"/> enlarge an image.
    /// </summary>
    public const string AllowUpscaleOption = "--allow-upscale";

    /// <summary>
    ///     The name of the option that chooses the resampling filter.
    /// </summary>
    public const string FilterOption = "--filter";

    /// <summary>
    ///     The names of the verb commands, each of which is the pipeline with one operation taken from an operand.
    /// </summary>
    public static readonly IReadOnlyList<string> VerbNames = ["crop", "resize", "rotate", "flip", "pad"];

    /// <summary>
    ///     The filter names, in the order they are listed in messages, with the filter each selects.
    /// </summary>
    private static readonly (string Name, ResamplingMode Mode)[] Filters =
    [
        ("auto", ResamplingMode.Auto),
        ("nearest", ResamplingMode.Nearest),
        ("bilinear", ResamplingMode.Bilinear),
        ("bicubic", ResamplingMode.Bicubic),
        ("lanczos3", ResamplingMode.Lanczos3),
        ("area", ResamplingMode.Area)
    ];

    /// <summary>
    ///     Parses a resampling filter name.
    /// </summary>
    /// <param name="text">The name: <c>auto</c>, <c>nearest</c>, <c>bilinear</c>, <c>bicubic</c>, <c>lanczos3</c>, or <c>area</c> (any case).</param>
    /// <param name="option">The option name, for messages.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a filter name.</exception>
    public static ResamplingMode ParseFilter(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        foreach (var (name, mode) in Filters)
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                return mode;
            }
        }

        throw new ToolException(
            ErrorCodes.UsageError,
            $"Invalid value '{text}' for {option}: expected one of {string.Join(", ", Filters.Select(f => f.Name))}.",
            $"Example: {option} lanczos3");
    }

    /// <summary>
    ///     Gets the name of a resampling filter as it is written on the command line and in the result.
    /// </summary>
    /// <param name="mode">The filter.</param>
    /// <returns>The lower-case name.</returns>
    public static string FilterName(ResamplingMode mode)
    {
        return Filters.First(f => f.Mode == mode).Name;
    }

    /// <summary>
    ///     Reads the operations of a command line.
    /// </summary>
    /// <param name="reader">
    ///     The parsed arguments of a command that declares <c>--crop</c>, <c>--resize</c>, <c>--fit</c>,
    ///     <c>--rotate</c>, <c>--flip</c>, <c>--pad</c>, <c>--pad-color</c>, <c>--allow-upscale</c>, and <c>--filter</c>.
    /// </param>
    /// <param name="verb">The verb command being run (one of <see cref="VerbNames"/>), or <see langword="null"/> for <c>render</c>.</param>
    /// <param name="verbOperand">The positional operand of the verb; ignored when <paramref name="verb"/> is <see langword="null"/>.</param>
    /// <returns>
    ///     The operations in the order they are applied: the operation of the verb first, then the options in
    ///     command-line order.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a value is malformed, a rotation is not a right angle,
    ///     <c>--allow-upscale</c> is given without <c>--fit</c>, or <c>--pad-color</c> without a pad; and with code
    ///     <c>size-limit</c> when a size or border is larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <remarks>
    ///     <c>--filter</c>, <c>--pad-color</c>, and <c>--allow-upscale</c> are settings of the whole command line,
    ///     not steps: the last occurrence of each applies to every step it concerns.
    /// </remarks>
    public static IReadOnlyList<IImageOperation> Parse(ArgumentReader reader, string? verb, string? verbOperand)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var filter = reader.Get(FilterOption) is { } filterText ? ParseFilter(filterText, FilterOption) : ResamplingMode.Auto;
        var allowUpscale = reader.Has(AllowUpscaleOption);
        Rgba32? padColor = reader.Get(PadColorOption) is { } colorText ? ArgumentReader.ParseColor(colorText, PadColorOption) : null;

        var operations = new List<IImageOperation>();
        if (verb != null)
        {
            var operand = verbOperand ?? throw new ArgumentNullException(nameof(verbOperand));
            operations.Add(ParseVerb(verb, operand, filter, padColor));
        }

        foreach (var (name, value) in reader.Ordered)
        {
            IImageOperation? operation = name switch
            {
                CropOption => new CropOperation(ArgumentReader.ParseGeometry(value!, name)),
                ResizeOption => ParseResize(value!, name, filter),
                FitOption => ParseFit(value!, name, allowUpscale, filter),
                RotateOption => new RotateOperation(ParseRotation(value!, name)),
                FlipOption => new FlipOperation(ArgumentReader.ParseFlip(value!, name)),
                PadOption => new PadOperation(ArgumentReader.ParsePad(value!, name), padColor),
                _ => null
            };
            if (operation != null)
            {
                operations.Add(operation);
            }
        }

        if (allowUpscale && !operations.OfType<FitOperation>().Any())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The option {AllowUpscaleOption} only applies with {FitOption}.",
                $"Add {FitOption} WxH, or remove {AllowUpscaleOption}.");
        }

        if (padColor != null && !operations.OfType<PadOperation>().Any())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The option {PadColorOption} only applies with a pad.",
                $"Add {PadOption} N, or remove {PadColorOption}.");
        }

        return operations;
    }

    /// <summary>
    ///     Applies operations, in order, to a surface.
    /// </summary>
    /// <param name="input">The surface; it is neither modified nor disposed.</param>
    /// <param name="operations">The operations, from <see cref="Parse"/>.</param>
    /// <param name="defaultPadColor">The color of a border that names none.</param>
    /// <param name="applied">What each operation did, in order: its parameters and the size of its result.</param>
    /// <returns>
    ///     A new surface, which the caller owns and disposes. It is always a different surface than
    ///     <paramref name="input"/>, even for an empty list of operations (then a copy).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a crop region lies entirely outside the image or is empty at that
    ///     point of the pipeline, and with code <c>size-limit</c> when a border would make the image larger than
    ///     <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <remarks>
    ///     Every intermediate surface is disposed as soon as the next one has been made, and also when an operation
    ///     fails, so a failure leaves nothing behind but the input.
    /// </remarks>
    public static Surface Apply(
        Surface input,
        IReadOnlyList<IImageOperation> operations,
        Rgba32 defaultPadColor,
        out IReadOnlyList<AppliedOperation> applied)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(operations);

        var results = new List<AppliedOperation>();
        var current = input;
        try
        {
            foreach (var operation in operations)
            {
                var (next, step) = Execute(current, operation, defaultPadColor);
                if (!ReferenceEquals(current, input))
                {
                    current.Dispose();
                }

                current = next;
                results.Add(step);
            }

            if (ReferenceEquals(current, input))
            {
                current = input.Crop(0, 0, input.Width, input.Height);
            }
        }
        catch
        {
            if (!ReferenceEquals(current, input))
            {
                current.Dispose();
            }

            throw;
        }

        applied = results;
        return current;
    }

    /// <summary>
    ///     Parses the operand of a verb command into its operation.
    /// </summary>
    /// <param name="verb">The verb.</param>
    /// <param name="operand">The operand as typed.</param>
    /// <param name="filter">The resampling filter of the command line.</param>
    /// <param name="padColor">The border color of the command line, or <see langword="null"/>.</param>
    /// <returns>The operation.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> or <c>size-limit</c> when the operand is not valid for the verb.</exception>
    private static IImageOperation ParseVerb(string verb, string operand, ResamplingMode filter, Rgba32? padColor)
    {
        return verb switch
        {
            "crop" => new CropOperation(ArgumentReader.ParseGeometry(operand, "<geometry>")),
            "resize" => ParseResize(operand, "<size>", filter),
            "rotate" => new RotateOperation(ParseRotation(operand, "<degrees>")),
            "flip" => new FlipOperation(ArgumentReader.ParseFlip(operand, "<axis>")),
            "pad" => new PadOperation(ArgumentReader.ParsePad(operand, "<border>"), padColor),
            _ => throw new ArgumentException($"'{verb}' is not an image verb.", nameof(verb))
        };
    }

    /// <summary>
    ///     Parses an exact size.
    /// </summary>
    /// <param name="text">The size, <c>WxH</c>.</param>
    /// <param name="name">The option or operand name, for messages.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>The operation.</returns>
    private static ResizeOperation ParseResize(string text, string name, ResamplingMode filter)
    {
        var (width, height) = ArgumentReader.ParseSize(text, name);
        return new ResizeOperation(width, height, filter);
    }

    /// <summary>
    ///     Parses a fit box.
    /// </summary>
    /// <param name="text">The box, <c>WxH</c>.</param>
    /// <param name="name">The option name, for messages.</param>
    /// <param name="allowUpscale"><see langword="true"/> when an image smaller than the box is enlarged.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>The operation.</returns>
    private static FitOperation ParseFit(string text, string name, bool allowUpscale, ResamplingMode filter)
    {
        var (width, height) = ArgumentReader.ParseSize(text, name);
        return new FitOperation(width, height, allowUpscale, filter);
    }

    /// <summary>
    ///     Parses a rotation, which must be a right angle.
    /// </summary>
    /// <param name="text">The angle in degrees, clockwise; a negative angle turns the other way (<c>-90</c> is 270).</param>
    /// <param name="name">The option or operand name, for messages.</param>
    /// <returns>90, 180, or 270.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not an angle, or the angle is not a right angle.</exception>
    private static int ParseRotation(string text, string name)
    {
        var degrees = ArgumentReader.ParseDegrees(text, name);
        if (degrees is 90 or 180 or 270)
        {
            return (int)degrees;
        }

        throw new ToolException(
            ErrorCodes.UsageError,
            $"Rotation by {degrees.ToString("0.##", CultureInfo.InvariantCulture)} degrees is not supported yet; only 90, 180, and 270 degrees are.",
            "Use 90, 180, or 270 (clockwise).");
    }

    /// <summary>
    ///     Carries out one operation.
    /// </summary>
    /// <param name="source">The surface; it is not modified or disposed.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="defaultPadColor">The color of a border that names none.</param>
    /// <returns>The new surface and the description of what was done.</returns>
    private static (Surface Result, AppliedOperation Applied) Execute(Surface source, IImageOperation operation, Rgba32 defaultPadColor)
    {
        return operation switch
        {
            CropOperation crop => ApplyCrop(source, crop),
            ResizeOperation resize => ApplyResize(source, resize),
            FitOperation fit => ApplyFit(source, fit),
            RotateOperation rotate => ApplyRotate(source, rotate),
            FlipOperation flip => ApplyFlip(source, flip),
            PadOperation pad => ApplyPad(source, pad, defaultPadColor),
            _ => throw new InvalidOperationException($"The image operation {operation.GetType().Name} is not handled by the pipeline.")
        };
    }

    /// <summary>
    ///     Cuts a region out of a surface.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="crop">The operation.</param>
    /// <returns>The cropped surface and its description.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the region starts outside the image or is smaller than one pixel.</exception>
    /// <remarks>
    ///     A percentage is of the size of the image at this point of the pipeline. The four edges of the region are
    ///     rounded to the nearest pixel (half away from zero). A region that extends past the right or bottom edge is
    ///     cut off at the edge (reported as <c>clamped</c>); one that starts at or beyond an edge has nothing left and
    ///     is a usage error.
    /// </remarks>
    private static (Surface, AppliedOperation) ApplyCrop(Surface source, CropOperation crop)
    {
        var region = crop.Region;
        var x = Resolve(region.X, source.Width);
        var y = Resolve(region.Y, source.Height);
        var left = Round(x);
        var top = Round(y);
        var right = Round(x + Resolve(region.Width, source.Width));
        var bottom = Round(y + Resolve(region.Height, source.Height));

        if (left >= source.Width || top >= source.Height)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The crop region starts at {left.ToString(CultureInfo.InvariantCulture)},{top.ToString(CultureInfo.InvariantCulture)}, outside the {source.Width}x{source.Height} image.",
                "Start the region inside the image; a region that extends past an edge is cut off at that edge.");
        }

        var clampedRight = Math.Min(right, source.Width);
        var clampedBottom = Math.Min(bottom, source.Height);
        if (clampedRight - left < 1 || clampedBottom - top < 1)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The crop region is smaller than one pixel in the {source.Width}x{source.Height} image.",
                "Use a larger width and height.");
        }

        var width = (int)(clampedRight - left);
        var height = (int)(clampedBottom - top);
        var clamped = right > source.Width || bottom > source.Height;
        var result = source.Crop((int)left, (int)top, width, height);
        return (result, SourceLoader.Operation(
            "crop",
            ("x", (int)left),
            ("y", (int)top),
            ("width", width),
            ("height", height),
            ("clamped", clamped)));
    }

    /// <summary>
    ///     Resamples a surface to an exact size.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="resize">The operation.</param>
    /// <returns>The resized surface and its description.</returns>
    private static (Surface, AppliedOperation) ApplyResize(Surface source, ResizeOperation resize)
    {
        var result = source.Resize(resize.Width, resize.Height, resize.Filter);
        return (result, SourceLoader.Operation(
            "resize",
            ("width", result.Width),
            ("height", result.Height),
            ("filter", FilterName(resize.Filter))));
    }

    /// <summary>
    ///     Fits a surface inside a box, keeping its aspect ratio.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="fit">The operation.</param>
    /// <returns>The fitted surface and its description.</returns>
    /// <remarks>
    ///     The core library's fit also enlarges; the pipeline only shrinks (an image that already fits is copied
    ///     unchanged and reported as not <c>scaled</c>) unless the operation allows upscaling.
    /// </remarks>
    private static (Surface, AppliedOperation) ApplyFit(Surface source, FitOperation fit)
    {
        var scaled = fit.AllowUpscale || source.Width > fit.Width || source.Height > fit.Height;
        var result = scaled
            ? source.ResizeToFit(fit.Width, fit.Height, fit.Filter)
            : source.Crop(0, 0, source.Width, source.Height);
        return (result, SourceLoader.Operation(
            "fit",
            ("width", result.Width),
            ("height", result.Height),
            ("boxWidth", fit.Width),
            ("boxHeight", fit.Height),
            ("filter", FilterName(fit.Filter)),
            ("allowUpscale", fit.AllowUpscale),
            ("scaled", scaled)));
    }

    /// <summary>
    ///     Rotates a surface clockwise by a right angle.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="rotate">The operation.</param>
    /// <returns>The rotated surface and its description; for 90 and 270 degrees the width and height are swapped.</returns>
    private static (Surface, AppliedOperation) ApplyRotate(Surface source, RotateOperation rotate)
    {
        var width = source.Width;
        var height = source.Height;

        // A quarter turn swaps the width and the height
        var rotatedWidth = height;
        var rotatedHeight = width;
        Surface result;
        switch (rotate.Degrees)
        {
            case 90:
                // A pixel at (x, y) of the source lands at (height - 1 - y, x): the top row becomes the right column
                result = new Surface(rotatedWidth, rotatedHeight);
                for (var y = 0; y < height; y++)
                {
                    var row = source.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        result[height - 1 - y, x] = row[x];
                    }
                }

                break;
            case 180:
                result = MirrorBoth(source);
                break;
            default:
                // A pixel at (x, y) of the source lands at (y, width - 1 - x): the top row becomes the left column
                result = new Surface(rotatedWidth, rotatedHeight);
                for (var y = 0; y < height; y++)
                {
                    var row = source.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        result[y, width - 1 - x] = row[x];
                    }
                }

                break;
        }

        return (result, SourceLoader.Operation("rotate", ("degrees", rotate.Degrees), ("width", result.Width), ("height", result.Height)));
    }

    /// <summary>
    ///     Mirrors a surface.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="flip">The operation.</param>
    /// <returns>The mirrored surface and its description.</returns>
    private static (Surface, AppliedOperation) ApplyFlip(Surface source, FlipOperation flip)
    {
        Surface result;
        switch (flip.Axis)
        {
            case FlipAxis.Horizontal:
                result = new Surface(source.Width, source.Height);
                for (var y = 0; y < source.Height; y++)
                {
                    var row = result.GetRowSpan(y);
                    source.GetRowSpan(y).CopyTo(row);
                    row.Reverse();
                }

                break;
            case FlipAxis.Vertical:
                result = new Surface(source.Width, source.Height);
                for (var y = 0; y < source.Height; y++)
                {
                    source.GetRowSpan(source.Height - 1 - y).CopyTo(result.GetRowSpan(y));
                }

                break;
            default:
                result = MirrorBoth(source);
                break;
        }

        return (result, SourceLoader.Operation(
            "flip",
            ("axis", flip.Axis.ToString().ToLowerInvariant()),
            ("width", result.Width),
            ("height", result.Height)));
    }

    /// <summary>
    ///     Mirrors a surface about both axes, which is a rotation by 180 degrees.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <returns>A new surface.</returns>
    private static Surface MirrorBoth(Surface source)
    {
        var result = new Surface(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            var row = result.GetRowSpan(y);
            source.GetRowSpan(source.Height - 1 - y).CopyTo(row);
            row.Reverse();
        }

        return result;
    }

    /// <summary>
    ///     Surrounds a surface with a border.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="pad">The operation.</param>
    /// <param name="defaultColor">The color of the border when the operation names none.</param>
    /// <returns>The padded surface and its description.</returns>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the result would be larger than <see cref="Surface.MaxDimension"/>.</exception>
    private static (Surface, AppliedOperation) ApplyPad(Surface source, PadOperation pad, Rgba32 defaultColor)
    {
        var border = pad.Border;
        var color = pad.Color ?? defaultColor;
        var width = (long)source.Width + border.Left + border.Right;
        var height = (long)source.Height + border.Top + border.Bottom;
        if (width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            throw new ToolException(
                ErrorCodes.SizeLimit,
                $"The padded image would be {width}x{height} pixels, which is larger than the maximum of {Surface.MaxDimension} pixels per side.",
                "Use a smaller --pad, or crop or shrink the image first.");
        }

        var result = new Surface((int)width, (int)height);
        if (color != new Rgba32(0, 0, 0, 0))
        {
            result.Clear(color);
        }

        for (var y = 0; y < source.Height; y++)
        {
            source.GetRowSpan(y).CopyTo(result.GetRowSpan(y + border.Top)[border.Left..]);
        }

        return (result, SourceLoader.Operation(
            "pad",
            ("left", border.Left),
            ("top", border.Top),
            ("right", border.Right),
            ("bottom", border.Bottom),
            ("color", SourceLoader.FormatColor(color)),
            ("width", result.Width),
            ("height", result.Height)));
    }

    /// <summary>
    ///     Converts a length that is in pixels or percent to pixels.
    /// </summary>
    /// <param name="dimension">The length.</param>
    /// <param name="total">The size of the image along that axis, which a percentage is of.</param>
    /// <returns>The length in pixels, not rounded.</returns>
    private static double Resolve(Dimension dimension, int total)
    {
        return dimension.IsPercent ? dimension.Value * total / 100.0 : dimension.Value;
    }

    /// <summary>
    ///     Rounds a pixel position to the nearest pixel, half away from zero.
    /// </summary>
    /// <param name="value">The position.</param>
    /// <returns>The rounded position.</returns>
    private static double Round(double value)
    {
        return Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
