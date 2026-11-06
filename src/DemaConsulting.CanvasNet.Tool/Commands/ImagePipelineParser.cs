// cspell:ignore canvasnet deskew
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     Reads the image pipeline of a command line: the transformation options of a command (crop, resize, fit, rotate,
///     flip, pad, the photo adjustments, deskew, trim, and grid) become the <see cref="IImageOperation"/> records that
///     <see cref="ImagePipeline.Apply"/> of the Viewer library carries out.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is shared by the <c>render</c> command, the <c>inspect</c> command, and the verb commands
///         <c>crop</c>, <c>resize</c>, <c>rotate</c>, <c>flip</c>, and <c>pad</c>: a verb is <c>render</c> with one
///         operation, taken from its positional operand, placed before the operations given as options. The
///         operations are kept in the order they were written, so <c>--crop</c> followed by <c>--resize</c> gives a
///         different image than the reverse.
///     </para>
///     <para>
///         Parsing is the command-line half of the pipeline and so stays in the tool: it owns the option names, the
///         value grammars (through <see cref="ArgumentReader"/>), and the wording of every usage error. Applying the
///         operations is the library half and lives in <see cref="ImagePipeline"/>.
///     </para>
/// </remarks>
internal static class ImagePipelineParser
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
    ///     The names of the verb commands, each of which is the pipeline with one operation taken from an operand.
    /// </summary>
    public static readonly IReadOnlyList<string> VerbNames = ["crop", "resize", "rotate", "flip", "pad"];

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

        if (ImagePipeline.TryParseFilter(text, out var mode))
        {
            return mode;
        }

        throw new ToolException(
            ErrorCodes.UsageError,
            $"Invalid value '{text}' for {option}: expected one of {string.Join(", ", ImagePipeline.FilterNames)}.",
            $"Example: {option} lanczos3");
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
                TrimOption => new TrimOperation(trimTolerance ?? ImagePipeline.DefaultTrimTolerance),
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
}
