// cspell:ignore lanczos canvasnet deskew otsu
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The ordered image pipeline: parses the transformation options of a command line (crop, resize, fit, rotate,
///     flip, pad, the photo adjustments, deskew, trim, and grid) and applies them, in the order written, to a surface.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is shared by the <c>render</c> command, the <c>inspect</c> command, and the verb commands
///         <c>crop</c>, <c>resize</c>, <c>rotate</c>, <c>flip</c>, and <c>pad</c>: a verb is <c>render</c> with one
///         operation, taken from its positional operand, placed before the operations given as options. The
///         operations are applied left to right, so <c>--crop</c> followed by <c>--resize</c> gives a different image
///         than the reverse. One decode and one encode serve the whole chain; every intermediate surface is disposed as
///         soon as the next one exists.
///     </para>
///     <para>
///         The pixel operations themselves (flip, pad, trim, and the photo adjustments) are methods of
///         <see cref="Surface"/> in the core library; this class orders them, validates and parses the command line,
///         maps a failure to a <see cref="ToolException"/>, and describes each step in an <see cref="AppliedOperation"/>.
///         Rotation by a right angle is an exact pixel move, rotation by any other angle
///         (and the correction of a skew) is <see cref="Surface.RotateArbitrary(double, Rgba32, bool)"/> onto an expanded canvas. An
///         explicitly requested rotation or a border larger than <see cref="Surface.MaxDimension"/> is refused
///         before allocation, with the code <c>size-limit</c>. Deskew is best effort: when its automatic correction
///         would exceed that limit, it copies the input and reports why it was skipped.
///     </para>
///     <para>
///         The operations that change pixels in place (grayscale, invert, threshold, levels, brightness, contrast,
///         auto-levels, grid) work on a surface the pipeline owns: an intermediate surface, or a copy when the current
///         surface is the caller's input, which is never modified. So at most two full-size surfaces exist at a time.
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
    ///     The name of the option that sets the color that fills the corners a rotation exposes.
    /// </summary>
    public const string RotateFillOption = "--rotate-fill";

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
    ///     The name of the option that makes the image gray.
    /// </summary>
    public const string GrayscaleOption = "--grayscale";

    /// <summary>
    ///     The name of the option that complements the colors.
    /// </summary>
    public const string InvertOption = "--invert";

    /// <summary>
    ///     The name of the option that turns the image into black and white.
    /// </summary>
    public const string ThresholdOption = "--threshold";

    /// <summary>
    ///     The name of the option that maps levels through a black point, white point, and gamma.
    /// </summary>
    public const string LevelsOption = "--levels";

    /// <summary>
    ///     The name of the option that adds to the levels.
    /// </summary>
    public const string BrightnessOption = "--brightness";

    /// <summary>
    ///     The name of the option that changes the contrast.
    /// </summary>
    public const string ContrastOption = "--contrast";

    /// <summary>
    ///     The name of the option that stretches the levels to the full range.
    /// </summary>
    public const string AutoLevelsOption = "--auto-levels";

    /// <summary>
    ///     The name of the option that sets the percentage of pixels <see cref="AutoLevelsOption"/> ignores at each end.
    /// </summary>
    public const string AutoLevelsClipOption = "--auto-levels-clip";

    /// <summary>
    ///     The name of the option that straightens the image.
    /// </summary>
    public const string DeskewOption = "--deskew";

    /// <summary>
    ///     The name of the option that crops to the content.
    /// </summary>
    public const string TrimOption = "--trim";

    /// <summary>
    ///     The name of the option that sets the tolerance of <see cref="TrimOption"/>.
    /// </summary>
    public const string TrimToleranceOption = "--trim-tolerance";

    /// <summary>
    ///     The name of the option that draws a coordinate grid.
    /// </summary>
    public const string GridOption = "--grid";

    /// <summary>
    ///     The default tolerance of <see cref="TrimOption"/>.
    /// </summary>
    public const int DefaultTrimTolerance = 8;

    /// <summary>
    ///     The distance in degrees from a multiple of 90 within which a rotation is an exact pixel move.
    /// </summary>
    private const double RightAngleEpsilon = 1e-9;

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
    ///     <c>--rotate</c>, <c>--rotate-fill</c>, <c>--flip</c>, <c>--pad</c>, <c>--pad-color</c>, <c>--allow-upscale</c>,
    ///     <c>--filter</c>, <c>--grayscale</c>, <c>--invert</c>, <c>--threshold</c>, <c>--levels</c>, <c>--brightness</c>,
    ///     <c>--contrast</c>, <c>--auto-levels</c>, <c>--auto-levels-clip</c>, <c>--deskew</c>, <c>--trim</c>,
    ///     <c>--trim-tolerance</c>, and <c>--grid</c>.
    /// </param>
    /// <param name="verb">The verb command being run (one of <see cref="VerbNames"/>), or <see langword="null"/> for <c>render</c>.</param>
    /// <param name="verbOperand">The positional operand of the verb; ignored when <paramref name="verb"/> is <see langword="null"/>.</param>
    /// <returns>
    ///     The operations in the order they are applied: the operation of the verb first, then the options in
    ///     command-line order.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a value is malformed or out of range, a setting is given without
    ///     the operation it modifies (<c>--allow-upscale</c> without <c>--fit</c>, <c>--pad-color</c> without a pad,
    ///     <c>--rotate-fill</c> without a rotation or deskew, <c>--auto-levels-clip</c> without <c>--auto-levels</c>,
    ///     <c>--trim-tolerance</c> without <c>--trim</c>); and with code <c>size-limit</c> when a size or border is
    ///     larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <remarks>
    ///     <c>--filter</c>, <c>--pad-color</c>, <c>--allow-upscale</c>, <c>--rotate-fill</c>, <c>--auto-levels-clip</c>,
    ///     and <c>--trim-tolerance</c> are settings of the whole command line, not steps: the last occurrence of each
    ///     applies to every step it concerns.
    /// </remarks>
    public static IReadOnlyList<IImageOperation> Parse(ArgumentReader reader, string? verb, string? verbOperand)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var filter = reader.Get(FilterOption) is { } filterText ? ParseFilter(filterText, FilterOption) : ResamplingMode.Auto;
        var allowUpscale = reader.Has(AllowUpscaleOption);
        Rgba32? padColor = reader.Get(PadColorOption) is { } colorText ? ArgumentReader.ParseColor(colorText, PadColorOption) : null;
        Rgba32? rotateFill = reader.Get(RotateFillOption) is { } fillText ? ArgumentReader.ParseColor(fillText, RotateFillOption) : null;
        var clip = reader.GetDouble(AutoLevelsClipOption, 0, Surface.MaxAutoLevelsClipPercent);
        var trimTolerance = reader.GetInt(TrimToleranceOption, 0, Surface.MaxTrimTolerance);

        var operations = new List<IImageOperation>();
        if (verb != null)
        {
            var operand = verbOperand ?? throw new ArgumentNullException(nameof(verbOperand));
            operations.Add(ParseVerb(verb, operand, filter, padColor, rotateFill));
        }

        foreach (var (name, value) in reader.Ordered)
        {
            IImageOperation? operation = name switch
            {
                CropOption => new CropOperation(ArgumentReader.ParseGeometry(value!, name)),
                ResizeOption => ParseResize(value!, name, filter),
                FitOption => ParseFit(value!, name, allowUpscale, filter),
                RotateOption => new RotateOperation(ParseRotation(value!, name), rotateFill, filter),
                FlipOption => new FlipOperation(ArgumentReader.ParseFlip(value!, name)),
                PadOption => new PadOperation(ArgumentReader.ParsePad(value!, name), padColor),
                GrayscaleOption => new GrayscaleOperation(),
                InvertOption => new InvertOperation(),
                ThresholdOption => ParseThreshold(value!, name),
                LevelsOption => ParseLevels(value!, name),
                BrightnessOption => new BrightnessOperation(ArgumentReader.ParseInt(value!, name, -255, 255)),
                ContrastOption => new ContrastOperation(ArgumentReader.ParseNumber(value!, name, -100, 100)),
                AutoLevelsOption => new AutoLevelsOperation(clip ?? Surface.DefaultAutoLevelsClipPercent),
                DeskewOption => new DeskewOperation(rotateFill, filter),
                TrimOption => new TrimOperation(trimTolerance ?? DefaultTrimTolerance),
                GridOption => ParseGrid(value!, name),
                _ => null
            };
            if (operation != null)
            {
                operations.Add(operation);
            }
        }

        RequireOperation<FitOperation>(operations, allowUpscale, AllowUpscaleOption, FitOption, FitOption + " WxH");
        RequireOperation<PadOperation>(operations, padColor != null, PadColorOption, "a pad", $"{PadOption} N");
        RequireOperation<AutoLevelsOperation>(operations, clip != null, AutoLevelsClipOption, AutoLevelsOption, AutoLevelsOption);
        RequireOperation<TrimOperation>(operations, trimTolerance != null, TrimToleranceOption, TrimOption, TrimOption);
        if (rotateFill != null && !operations.Any(o => o is RotateOperation or DeskewOperation))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The option {RotateFillOption} only applies with a rotation or {DeskewOption}.",
                $"Add {RotateOption} <degrees> or {DeskewOption}, or remove {RotateFillOption}.");
        }

        return operations;
    }

    /// <summary>
    ///     Requires that a setting that was given has an operation to apply to.
    /// </summary>
    /// <typeparam name="T">The type of the operation the setting modifies.</typeparam>
    /// <param name="operations">The operations that were parsed.</param>
    /// <param name="given"><see langword="true"/> when the setting was given.</param>
    /// <param name="setting">The name of the setting, for the message.</param>
    /// <param name="applies">What the setting applies with, for the message.</param>
    /// <param name="suggestion">The operation to add, for the hint.</param>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the setting was given without an operation of the type.</exception>
    private static void RequireOperation<T>(List<IImageOperation> operations, bool given, string setting, string applies, string suggestion)
        where T : IImageOperation
    {
        if (given && !operations.OfType<T>().Any())
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The option {setting} only applies with {applies}.",
                $"Add {suggestion}, or remove {setting}.");
        }
    }

    /// <summary>
    ///     Applies operations, in order, to a surface.
    /// </summary>
    /// <param name="input">The surface; it is neither modified nor disposed.</param>
    /// <param name="operations">The operations, from <see cref="Parse"/>.</param>
    /// <param name="defaultPadColor">The color of a border that names none.</param>
    /// <param name="applied">What each operation did, in order: its parameters and the size of its result.</param>
    /// <param name="userBackground">
    ///     The background color the user gave, or <see langword="null"/>; the fill of a rotation that names none, ahead of
    ///     the default rule.
    /// </param>
    /// <returns>
    ///     A new surface, which the caller owns and disposes. It is always a different surface than
    ///     <paramref name="input"/>, even for an empty list of operations (then a copy).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a crop region lies entirely outside the image or is empty at that
    ///     point of the pipeline, and with code <c>size-limit</c> when a border or a rotation would make the image
    ///     larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <remarks>
    ///     Every intermediate surface is disposed as soon as the next one has been made, and also when an operation
    ///     fails, so a failure leaves nothing behind but the input. An operation that changes pixels in place is given
    ///     the current surface when the pipeline owns it and a copy of the input otherwise, and returns that surface.
    ///     The fill of a rotation (or deskew) is its own color, else <paramref name="userBackground"/>, else opaque
    ///     white when the surface being rotated has no pixel that is not opaque, else transparent.
    /// </remarks>
    public static Surface Apply(
        Surface input,
        IReadOnlyList<IImageOperation> operations,
        Rgba32 defaultPadColor,
        out IReadOnlyList<AppliedOperation> applied,
        Rgba32? userBackground = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(operations);

        var results = new List<AppliedOperation>();
        var current = input;
        try
        {
            foreach (var operation in operations)
            {
                var (next, step) = Execute(current, !ReferenceEquals(current, input), operation, defaultPadColor, userBackground);
                if (!ReferenceEquals(current, input) && !ReferenceEquals(current, next))
                {
                    current.Dispose();
                }

                current = next;
                results.Add(step);
            }

            if (ReferenceEquals(current, input))
            {
                current = input.Clone();
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
    /// <param name="rotateFill">The fill color of a rotation of the command line, or <see langword="null"/>.</param>
    /// <returns>The operation.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> or <c>size-limit</c> when the operand is not valid for the verb.</exception>
    private static IImageOperation ParseVerb(string verb, string operand, ResamplingMode filter, Rgba32? padColor, Rgba32? rotateFill)
    {
        return verb switch
        {
            "crop" => new CropOperation(ArgumentReader.ParseGeometry(operand, "<geometry>")),
            "resize" => ParseResize(operand, "<size>", filter),
            "rotate" => new RotateOperation(ParseRotation(operand, "<degrees>"), rotateFill, filter),
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
    ///     Parses a rotation by any angle.
    /// </summary>
    /// <param name="text">The angle in degrees, clockwise, optionally followed by <c>deg</c>; a negative angle turns the other way (<c>-90</c> is 270).</param>
    /// <param name="name">The option or operand name, for messages.</param>
    /// <returns>The angle from 0 up to (not including) 360; a full turn is 0.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a finite number of degrees.</exception>
    private static double ParseRotation(string text, string name)
    {
        var degrees = ArgumentReader.ParseDegrees(text, name);
        return degrees >= 360 ? 0 : degrees;
    }

    /// <summary>
    ///     Parses the value of <c>--threshold</c>.
    /// </summary>
    /// <param name="text">The level, a whole number from 0 to 255, or <c>auto</c> (any case).</param>
    /// <param name="name">The option name, for messages.</param>
    /// <returns>The operation; the level is <see langword="null"/> for <c>auto</c>.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is neither <c>auto</c> nor a level.</exception>
    private static ThresholdOperation ParseThreshold(string text, string name)
    {
        if (string.Equals(text.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
        {
            return new ThresholdOperation(null);
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var level) || level < 0 || level > 255)
        {
            throw Invalid(text, name, "an integer from 0 to 255, or auto", "auto");
        }

        return new ThresholdOperation(level);
    }

    /// <summary>
    ///     Parses the value of <c>--levels</c>.
    /// </summary>
    /// <param name="text">The levels, <c>black,white</c> or <c>black,white,gamma</c>.</param>
    /// <param name="name">The option name, for messages.</param>
    /// <returns>The operation; the gamma is 1 when it is left out.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when a value is malformed or out of range, or the black level is not below the white level.</exception>
    private static LevelsOperation ParseLevels(string text, string name)
    {
        var parts = text.Split(',');
        if (parts.Length is < 2 or > 3)
        {
            throw Invalid(text, name, "black,white or black,white,gamma", "20,230");
        }

        var black = ArgumentReader.ParseInt(parts[0].Trim(), name, 0, 254);
        var white = ArgumentReader.ParseInt(parts[1].Trim(), name, 1, 255);
        var gamma = parts.Length == 3
            ? ArgumentReader.ParseNumber(parts[2].Trim(), name, Surface.MinGamma, Surface.MaxGamma)
            : 1.0;
        if (black >= white)
        {
            throw Invalid(text, name, "a black level below the white level", "20,230");
        }

        return new LevelsOperation(black, white, gamma);
    }

    /// <summary>
    ///     Parses the value of <c>--grid</c>.
    /// </summary>
    /// <param name="text">The grid, <c>spacing</c> or <c>spacing,color</c>.</param>
    /// <param name="name">The option name, for messages.</param>
    /// <returns>The operation; the color is <see langword="null"/> when it is left out.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the spacing or the color is malformed or out of range.</exception>
    private static GridOperation ParseGrid(string text, string name)
    {
        var parts = text.Split(',', 2);
        var spacing = ArgumentReader.ParseInt(parts[0].Trim(), name, GridOverlay.MinSpacing, GridOverlay.MaxSpacing);
        Rgba32? color = parts.Length == 2 ? ArgumentReader.ParseColor(parts[1], name) : null;
        return new GridOperation(spacing, color);
    }

    /// <summary>
    ///     Creates the usage error for a malformed value.
    /// </summary>
    /// <param name="text">The text as typed.</param>
    /// <param name="option">The option name.</param>
    /// <param name="expected">What was expected.</param>
    /// <param name="example">An example value.</param>
    /// <returns>The error.</returns>
    private static ToolException Invalid(string text, string option, string expected, string example)
    {
        return new ToolException(
            ErrorCodes.UsageError,
            $"Invalid value '{text}' for {option}: expected {expected}.",
            $"Example: {option} {example}");
    }

    /// <summary>
    ///     Carries out one operation.
    /// </summary>
    /// <param name="source">The surface; it is not modified or disposed, unless the operation works in place and <paramref name="owned"/> is <see langword="true"/>.</param>
    /// <param name="owned"><see langword="true"/> when the pipeline owns <paramref name="source"/>, so an in-place operation may change it.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="defaultPadColor">The color of a border that names none.</param>
    /// <param name="userBackground">The background color the user gave, or <see langword="null"/>.</param>
    /// <returns>The new surface (the same surface for an in-place operation on an owned one) and the description of what was done.</returns>
    private static (Surface Result, AppliedOperation Applied) Execute(
        Surface source,
        bool owned,
        IImageOperation operation,
        Rgba32 defaultPadColor,
        Rgba32? userBackground)
    {
        return operation switch
        {
            CropOperation crop => ApplyCrop(source, crop),
            ResizeOperation resize => ApplyResize(source, resize),
            FitOperation fit => ApplyFit(source, fit),
            RotateOperation rotate => ApplyRotate(source, rotate, userBackground),
            FlipOperation flip => ApplyFlip(source, flip),
            PadOperation pad => ApplyPad(source, pad, defaultPadColor),
            GrayscaleOperation => InPlace(source, owned, surface =>
            {
                surface.Grayscale();
                return SourceLoader.Operation("grayscale", ("width", surface.Width), ("height", surface.Height));
            }),
            InvertOperation => InPlace(source, owned, surface =>
            {
                surface.Invert();
                return SourceLoader.Operation("invert", ("width", surface.Width), ("height", surface.Height));
            }),
            ThresholdOperation threshold => InPlace(source, owned, surface => ApplyThreshold(surface, threshold)),
            LevelsOperation levels => InPlace(source, owned, surface => ApplyLevels(surface, levels)),
            BrightnessOperation brightness => InPlace(source, owned, surface =>
            {
                surface.Brightness(brightness.Amount);
                return SourceLoader.Operation("brightness", ("amount", brightness.Amount), ("width", surface.Width), ("height", surface.Height));
            }),
            ContrastOperation contrast => InPlace(source, owned, surface =>
            {
                surface.Contrast(contrast.Amount);
                return SourceLoader.Operation("contrast", ("amount", contrast.Amount), ("width", surface.Width), ("height", surface.Height));
            }),
            AutoLevelsOperation autoLevels => InPlace(source, owned, surface => ApplyAutoLevels(surface, autoLevels)),
            DeskewOperation deskew => ApplyDeskew(source, deskew, userBackground),
            TrimOperation trim => ApplyTrim(source, trim),
            GridOperation grid => InPlace(source, owned, surface => ApplyGrid(surface, grid)),
            _ => throw new InvalidOperationException($"The image operation {operation.GetType().Name} is not handled by the pipeline.")
        };
    }

    /// <summary>
    ///     Runs an operation that changes pixels in place on a surface the pipeline owns.
    /// </summary>
    /// <param name="source">The current surface.</param>
    /// <param name="owned"><see langword="true"/> when the pipeline owns <paramref name="source"/>; otherwise (it is the caller's input) a copy is changed.</param>
    /// <param name="change">The operation: changes the surface it is given and describes what it did.</param>
    /// <returns>The changed surface and the description.</returns>
    private static (Surface, AppliedOperation) InPlace(Surface source, bool owned, Func<Surface, AppliedOperation> change)
    {
        var target = owned ? source : source.Clone();
        try
        {
            return (target, change(target));
        }
        catch
        {
            if (!owned)
            {
                target.Dispose();
            }

            throw;
        }
    }

    /// <summary>
    ///     Turns a surface into black and white.
    /// </summary>
    /// <param name="surface">The surface to change.</param>
    /// <param name="threshold">The operation.</param>
    /// <returns>The description: the level used, and whether it was chosen automatically.</returns>
    private static AppliedOperation ApplyThreshold(Surface surface, ThresholdOperation threshold)
    {
        var automatic = threshold.Level == null;
        var level = threshold.Level ?? Surface.OtsuThreshold(surface.LumaHistogram());
        surface.Threshold(level);
        return SourceLoader.Operation(
            "threshold",
            ("auto", automatic),
            ("level", level),
            ("width", surface.Width),
            ("height", surface.Height));
    }

    /// <summary>
    ///     Maps the levels of a surface through a black point, a white point, and a gamma.
    /// </summary>
    /// <param name="surface">The surface to change.</param>
    /// <param name="levels">The operation.</param>
    /// <returns>The description.</returns>
    private static AppliedOperation ApplyLevels(Surface surface, LevelsOperation levels)
    {
        surface.Levels(levels.Black, levels.White, levels.Gamma);
        return SourceLoader.Operation(
            "levels",
            ("black", levels.Black),
            ("white", levels.White),
            ("gamma", levels.Gamma),
            ("width", surface.Width),
            ("height", surface.Height));
    }

    /// <summary>
    ///     Stretches the levels of a surface to the full range.
    /// </summary>
    /// <param name="surface">The surface to change.</param>
    /// <param name="autoLevels">The operation.</param>
    /// <returns>The description: the points found, or <c>applied: false</c> for a flat image, which is left unchanged.</returns>
    private static AppliedOperation ApplyAutoLevels(Surface surface, AutoLevelsOperation autoLevels)
    {
        return surface.AutoLevels(autoLevels.ClipPercent) is { } points
            ? SourceLoader.Operation(
                "auto-levels",
                ("clip", autoLevels.ClipPercent),
                ("black", points.Black),
                ("white", points.White),
                ("applied", true),
                ("width", surface.Width),
                ("height", surface.Height))
            : SourceLoader.Operation(
                "auto-levels",
                ("clip", autoLevels.ClipPercent),
                ("applied", false),
                ("width", surface.Width),
                ("height", surface.Height));
    }

    /// <summary>
    ///     Draws a coordinate grid over a surface.
    /// </summary>
    /// <param name="surface">The surface to change.</param>
    /// <param name="grid">The operation.</param>
    /// <returns>The description: the spacing, the color, and the numbers of lines and labels drawn.</returns>
    private static AppliedOperation ApplyGrid(Surface surface, GridOperation grid)
    {
        var color = grid.Color ?? GridOverlay.DefaultColor;
        var result = GridOverlay.Draw(surface, grid.Spacing, color);
        return SourceLoader.Operation(
            "grid",
            ("spacing", grid.Spacing),
            ("color", SourceLoader.FormatColor(color)),
            ("verticalLines", result.VerticalLines),
            ("horizontalLines", result.HorizontalLines),
            ("labels", result.Labels),
            ("width", surface.Width),
            ("height", surface.Height));
    }

    /// <summary>
    ///     Crops a surface to the content inside a uniform border.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="trim">The operation.</param>
    /// <returns>
    ///     The cropped surface and its description: the box, whether anything was <c>trimmed</c>, and <c>empty</c> when the
    ///     whole image is background (then the image is copied unchanged).
    /// </returns>
    private static (Surface, AppliedOperation) ApplyTrim(Surface source, TrimOperation trim)
    {
        var bounds = source.FindTrimBounds(trim.Tolerance);
        var (x, y, width, height) = bounds ?? (0, 0, source.Width, source.Height);
        var result = source.Crop(x, y, width, height);
        var trimmed = width != source.Width || height != source.Height;
        return (result, SourceLoader.Operation(
            "trim",
            ("tolerance", trim.Tolerance),
            ("x", x),
            ("y", y),
            ("width", width),
            ("height", height),
            ("trimmed", trimmed),
            ("empty", bounds == null)));
    }

    /// <summary>
    ///     Straightens a surface by the skew its near-horizontal lines have.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="deskew">The operation.</param>
    /// <param name="userBackground">The background color the user gave, or <see langword="null"/>.</param>
    /// <returns>
    ///     The rotated surface (a copy when the skew is not corrected) and its description: <c>skewDegrees</c> (positive
    ///     when lines descend to the right), <c>rotateDegrees</c> (the signed clockwise correction, 0 when none),
    ///     <c>confidence</c>, and <c>applied</c>; when a size limit skips the correction, also <c>reason</c> (after
    ///     <c>applied</c>).
    /// </returns>
    /// <remarks>
    ///     The skew is corrected only when it was estimated, its size is at least
    ///     <see cref="SkewEstimate.MinCorrectedDegrees"/> degrees, its confidence at least
    ///     <see cref="SkewEstimate.MinConfidence"/> (together <see cref="SkewEstimate.IsCorrectable"/>), and the expanded
    ///     canvas (<see cref="Surface.RotatedSize"/>, the size <see cref="Surface.RotateArbitrary(double, Rgba32, bool)"/>
    ///     allocates) fits <see cref="Surface.MaxDimension"/>.
    ///     Otherwise the image is copied unchanged; a size-limit skip includes a <c>reason</c> in its operation report.
    /// </remarks>
    private static (Surface, AppliedOperation) ApplyDeskew(Surface source, DeskewOperation deskew, Rgba32? userBackground)
    {
        var estimate = source.EstimateSkew();
        if (!estimate.IsCorrectable)
        {
            return SkipDeskew(source, estimate, null);
        }

        var rotateDegrees = -estimate.Degrees;
        var normalized = estimate.CorrectionDegrees;
        var (rotatedWidth, rotatedHeight) = Surface.RotatedSize(source.Width, source.Height, normalized);
        if (rotatedWidth > Surface.MaxDimension || rotatedHeight > Surface.MaxDimension)
        {
            return SkipDeskew(source, estimate, "rotated image would exceed the maximum size");
        }

        var fill = ResolveFill(source, deskew.Fill, userBackground);
        var nearest = deskew.Filter == ResamplingMode.Nearest;
        var result = source.RotateArbitrary(normalized, fill, nearest);
        return (result, SourceLoader.Operation(
            "deskew",
            ("skewDegrees", estimate.Degrees),
            ("rotateDegrees", rotateDegrees),
            ("confidence", estimate.Confidence),
            ("applied", true),
            ("fill", SourceLoader.FormatColor(fill)),
            ("filter", nearest ? "nearest" : "bilinear"),
            ("width", result.Width),
            ("height", result.Height)));
    }

    /// <summary>
    ///     Copies a surface unchanged and describes a deskew that was not applied.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="estimate">The skew estimate.</param>
    /// <param name="reason">Why the correction was skipped when a limit caused it, or <see langword="null"/> when the skew simply needs no correction.</param>
    /// <returns>The copy and its description (<c>reason</c> only when one was given).</returns>
    private static (Surface, AppliedOperation) SkipDeskew(Surface source, SkewEstimate estimate, string? reason)
    {
        var copy = source.Clone();
        var fields = new List<(string, object)>
        {
            ("skewDegrees", estimate.Degrees),
            ("rotateDegrees", 0.0),
            ("confidence", estimate.Confidence),
            ("applied", false),
        };
        if (reason != null)
        {
            fields.Add(("reason", reason));
        }

        fields.Add(("width", copy.Width));
        fields.Add(("height", copy.Height));
        return (copy, SourceLoader.Operation("deskew", [.. fields]));
    }

    /// <summary>
    ///     Refuses a rotation whose expanded canvas would be larger than the maximum image size.
    /// </summary>
    /// <param name="source">The surface to be rotated.</param>
    /// <param name="degrees">The rotation in degrees, a finite number (clockwise).</param>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the rotated image would be larger than <see cref="Surface.MaxDimension"/> on a side.</exception>
    /// <remarks>
    ///     The size is checked here, before <see cref="Surface.RotateArbitrary(double, Rgba32, bool)"/> would refuse it with an
    ///     <see cref="ArgumentException"/>, so that the error carries the code <c>size-limit</c>, the size, and the hint to
    ///     shrink the image first.
    /// </remarks>
    private static void CheckRotatedSize(Surface source, double degrees)
    {
        var (width, height) = Surface.RotatedSize(source.Width, source.Height, degrees);
        if (width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            throw new ToolException(
                ErrorCodes.SizeLimit,
                $"The rotated image would be {width}x{height} pixels, more than the maximum of {Surface.MaxDimension} pixels on a side.",
                "Shrink the image first, for example --fit 4096x4096 before --rotate or --deskew.");
        }
    }

    /// <summary>
    ///     Chooses the color that fills the corners a rotation exposes.
    /// </summary>
    /// <param name="source">The surface being rotated.</param>
    /// <param name="explicitFill">The fill the operation names, or <see langword="null"/>.</param>
    /// <param name="userBackground">The background color the user gave, or <see langword="null"/>.</param>
    /// <returns>
    ///     The operation's own fill; else the user's background; else opaque white when every pixel of the surface is opaque
    ///     (a photograph or a flattened drawing: the corners look like paper); else transparent.
    /// </returns>
    private static Rgba32 ResolveFill(Surface source, Rgba32? explicitFill, Rgba32? userBackground)
    {
        if (explicitFill is { } own)
        {
            return own;
        }

        if (userBackground is { } background)
        {
            return background;
        }

        return source.HasTransparency() ? new Rgba32(0, 0, 0, 0) : new Rgba32(255, 255, 255, 255);
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
            : source.Clone();
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
    ///     Rotates a surface clockwise by any angle.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="rotate">The operation.</param>
    /// <param name="userBackground">The background color the user gave, or <see langword="null"/>.</param>
    /// <returns>
    ///     The rotated surface and its description. A right angle is an exact pixel move (for 90 and 270 degrees the width
    ///     and height are swapped) and 0 is a copy; any other angle is <see cref="Surface.RotateArbitrary(double, Rgba32, bool)"/> onto an
    ///     expanded canvas, filled with the fill of the rule at <see cref="ResolveFill"/>, and also reports <c>fill</c> and
    ///     <c>filter</c> (<c>nearest</c> or <c>bilinear</c>).
    /// </returns>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the expanded canvas would be larger than <see cref="Surface.MaxDimension"/>.</exception>
    private static (Surface, AppliedOperation) ApplyRotate(Surface source, RotateOperation rotate, Rgba32? userBackground)
    {
        var quarters = Math.Round(rotate.Degrees / 90.0);
        if (Math.Abs(rotate.Degrees - (quarters * 90.0)) >= RightAngleEpsilon)
        {
            var fill = ResolveFill(source, rotate.Fill, userBackground);
            var nearest = rotate.Filter == ResamplingMode.Nearest;
            CheckRotatedSize(source, rotate.Degrees);
            var turned = source.RotateArbitrary(rotate.Degrees, fill, nearest);
            return (turned, SourceLoader.Operation(
                "rotate",
                ("degrees", rotate.Degrees),
                ("fill", SourceLoader.FormatColor(fill)),
                ("filter", nearest ? "nearest" : "bilinear"),
                ("width", turned.Width),
                ("height", turned.Height)));
        }

        var degrees = (((int)(quarters * 90.0) % 360) + 360) % 360;
        var width = source.Width;
        var height = source.Height;

        // A quarter turn swaps the width and the height
        var rotatedWidth = height;
        var rotatedHeight = width;
        Surface result;
        switch (degrees)
        {
            case 0:
                result = source.Crop(0, 0, width, height);
                break;
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
                result = source.Rotate180();
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

        return (result, SourceLoader.Operation("rotate", ("degrees", degrees), ("width", result.Width), ("height", result.Height)));
    }

    /// <summary>
    ///     Mirrors a surface.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="flip">The operation.</param>
    /// <returns>The mirrored surface and its description.</returns>
    private static (Surface, AppliedOperation) ApplyFlip(Surface source, FlipOperation flip)
    {
        var result = flip.Axis switch
        {
            FlipAxis.Horizontal => source.FlipHorizontal(),
            FlipAxis.Vertical => source.FlipVertical(),
            _ => source.Rotate180()
        };

        return (result, SourceLoader.Operation(
            "flip",
            ("axis", flip.Axis.ToString().ToLowerInvariant()),
            ("width", result.Width),
            ("height", result.Height)));
    }

    /// <summary>
    ///     Surrounds a surface with a border.
    /// </summary>
    /// <param name="source">The surface.</param>
    /// <param name="pad">The operation.</param>
    /// <param name="defaultColor">The color of the border when the operation names none.</param>
    /// <returns>The padded surface and its description.</returns>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the result would be larger than <see cref="Surface.MaxDimension"/>.</exception>
    /// <remarks>
    ///     The size is checked here, before <see cref="Surface.Pad"/> would refuse it with an <see cref="ArgumentException"/>,
    ///     so that the error carries the code <c>size-limit</c>, the size, and the hint to use a smaller border.
    /// </remarks>
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

        var result = source.Pad(border.Left, border.Top, border.Right, border.Bottom, color);

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
