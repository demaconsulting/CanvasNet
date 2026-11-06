// cspell:ignore deskew
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <content>
///     The validation of the values of the operation records, before any operation is applied.
/// </content>
public static partial class ImagePipeline
{
    /// <summary>
    ///     Checks every operation of a list, so that a record built by hand is refused with the same code a command
    ///     line value would be.
    /// </summary>
    /// <param name="operations">The operations, in the order they would be applied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operations"/> or one of its elements is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>usage-error</c> when a value is out of range, not finite, or not a member of its
    ///     enumeration, and with code <c>size-limit</c> when a size or border is larger than
    ///     <see cref="Surface.MaxDimension"/>. The message names the position (counted from 1) and the name of the
    ///     operation.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         The records are plain data, so nothing stops code from building one with a value that makes no sense,
    ///         copying one with a <c>with</c> expression, or taking the <see langword="default"/> of a value type. Checking
    ///         here, in one place and before the first operation runs, means no image is decoded, copied, or partly
    ///         changed for a list that cannot be applied, and every caller (the renderer, the inspector, the tool, and
    ///         code that calls <see cref="Apply"/> itself) gets the same answer. The rules are the ones the command
    ///         line parser of the tool already enforces, so a list that came from a command line is never refused here.
    ///     </para>
    ///     <para>
    ///         What depends on the image (a crop region outside it, a border or rotation that would make it too
    ///         large) is still decided when the operation is applied. Any finite angle is a valid rotation. An
    ///         implementation of <see cref="IImageOperation"/> that this class does not know is not checked.
    ///     </para>
    /// </remarks>
    internal static void Validate(IReadOnlyList<IImageOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        // Check the elements in order, so the first problem is the one reported
        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i]
                ?? throw new ArgumentNullException(nameof(operations), $"Operation {i + 1} of the list must not be null.");
            ValidateOperation(i + 1, operation);
        }
    }

    /// <summary>
    ///     Checks the values of one operation.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <exception cref="ViewerException">Thrown when a value is invalid; see <see cref="Validate"/>.</exception>
    private static void ValidateOperation(int number, IImageOperation operation)
    {
        switch (operation)
        {
            case CropOperation crop:
                ValidateCrop(number, crop);
                break;
            case ResizeOperation resize:
                ValidateBox(number, resize, resize.Width, resize.Height, resize.Filter);
                break;
            case FitOperation fit:
                ValidateBox(number, fit, fit.Width, fit.Height, fit.Filter);
                break;
            case RotateOperation rotate:
                RequireFinite(number, rotate, "the angle", rotate.Degrees);
                RequireDefined(number, rotate, "the filter", rotate.Filter);
                break;
            case DeskewOperation deskew:
                RequireDefined(number, deskew, "the filter", deskew.Filter);
                break;
            case FlipOperation flip:
                RequireDefined(number, flip, "the axis", flip.Axis);
                break;
            case PadOperation pad:
                ValidatePad(number, pad);
                break;
            case ThresholdOperation { Level: { } level } threshold:
                RequireInRange(number, threshold, "the level", level, 0, 255);
                break;
            case LevelsOperation levels:
                ValidateLevels(number, levels);
                break;
            case BrightnessOperation brightness:
                RequireInRange(number, brightness, "the amount", brightness.Amount, -255, 255);
                break;
            case ContrastOperation contrast:
                RequireInRange(number, contrast, "the amount", contrast.Amount, -100, 100);
                break;
            case AutoLevelsOperation autoLevels:
                RequireInRange(number, autoLevels, "the clip percentage", autoLevels.ClipPercent, 0, Surface.MaxAutoLevelsClipPercent);
                break;
            case TrimOperation trim:
                RequireInRange(number, trim, "the tolerance", trim.Tolerance, 0, Surface.MaxTrimTolerance);
                break;
            case GridOperation grid:
                RequireInRange(number, grid, "the spacing", grid.Spacing, GridOverlay.MinSpacing, GridOverlay.MaxSpacing);
                break;
        }
    }

    /// <summary>
    ///     Checks the region of a crop.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="crop">The operation.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when a value of the region is invalid.</exception>
    private static void ValidateCrop(int number, CropOperation crop)
    {
        var region = crop.Region;

        // The origin may be 0; the size must be positive, so a default region is refused
        CheckDimension(number, crop, "the x offset", region.X, positive: false);
        CheckDimension(number, crop, "the y offset", region.Y, positive: false);
        CheckDimension(number, crop, "the width", region.Width, positive: true);
        CheckDimension(number, crop, "the height", region.Height, positive: true);
    }

    /// <summary>
    ///     Checks one length of a crop region.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="dimension">The length.</param>
    /// <param name="positive"><see langword="true"/> when the length must be greater than 0; otherwise 0 is allowed.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when the length is invalid.</exception>
    private static void CheckDimension(int number, IImageOperation operation, string what, Dimension dimension, bool positive)
    {
        var value = dimension.Value;
        if (!double.IsFinite(value))
        {
            throw Invalid(number, operation, $"{what} must be a finite number", "Use a number of pixels or a percentage from 0 to 100.");
        }

        if (positive ? value <= 0 : value < 0)
        {
            throw Invalid(
                number,
                operation,
                $"{what} must be {(positive ? "greater than 0" : "0 or more")}, not {Format(value)}",
                "Use a region that starts inside the image and has a width and height of at least 1.");
        }

        if (dimension.IsPercent && value > 100)
        {
            throw Invalid(number, operation, $"{what} must be 100% or less, not {Format(value)}%", "Use a percentage from 0 to 100.");
        }
    }

    /// <summary>
    ///     Checks the size and filter of a resize or a fit.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="filter">The resampling filter.</param>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>usage-error</c> when a side is less than 1 or the filter is undefined, and with code
    ///     <c>size-limit</c> when a side is larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    private static void ValidateBox(int number, IImageOperation operation, int width, int height, ResamplingMode filter)
    {
        // Every usage error first, then the size limit, as the command line parser orders them
        RequireAtLeastOne(number, operation, "the width", width);
        RequireAtLeastOne(number, operation, "the height", height);
        RequireDefined(number, operation, "the filter", filter);
        RequireWithinLimit(number, operation, "the width", width);
        RequireWithinLimit(number, operation, "the height", height);
    }

    /// <summary>
    ///     Checks the border of a pad.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="pad">The operation.</param>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>usage-error</c> when a side is negative, and with code <c>size-limit</c> when a side is
    ///     larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    private static void ValidatePad(int number, PadOperation pad)
    {
        var sides = new (string Name, int Value)[]
        {
            ("the left border", pad.Border.Left),
            ("the top border", pad.Border.Top),
            ("the right border", pad.Border.Right),
            ("the bottom border", pad.Border.Bottom)
        };

        // The total size of the padded image depends on the image and is checked when the border is applied
        foreach (var (name, value) in sides)
        {
            if (value < 0)
            {
                throw Invalid(number, pad, $"{name} must be 0 or more, not {Format(value)}", "Use a border width of 0 or more pixels.");
            }
        }

        foreach (var (name, value) in sides)
        {
            RequireWithinLimit(number, pad, name, value);
        }
    }

    /// <summary>
    ///     Checks the black point, white point, and gamma of a levels adjustment.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="levels">The operation.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when a value is out of range or the black point is not below the white point.</exception>
    private static void ValidateLevels(int number, LevelsOperation levels)
    {
        RequireInRange(number, levels, "the black level", levels.Black, 0, 254);
        RequireInRange(number, levels, "the white level", levels.White, 1, 255);
        if (levels.Black >= levels.White)
        {
            throw Invalid(
                number,
                levels,
                $"the black level ({levels.Black}) must be below the white level ({levels.White})",
                "Use a black level below the white level.");
        }

        RequireInRange(number, levels, "the gamma", levels.Gamma, Surface.MinGamma, Surface.MaxGamma);
    }

    /// <summary>
    ///     Requires a finite number.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when the value is NaN or infinite.</exception>
    private static void RequireFinite(int number, IImageOperation operation, string what, double value)
    {
        if (!double.IsFinite(value))
        {
            throw Invalid(number, operation, $"{what} must be a finite number, not {Format(value)}", "Use a finite number of degrees.");
        }
    }

    /// <summary>
    ///     Requires a number within a range; NaN and the infinities are outside every range.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="value">The value.</param>
    /// <param name="minimum">The smallest allowed value.</param>
    /// <param name="maximum">The largest allowed value.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when the value is not from <paramref name="minimum"/> to <paramref name="maximum"/>.</exception>
    private static void RequireInRange(int number, IImageOperation operation, string what, double value, double minimum, double maximum)
    {
        if (!(value >= minimum && value <= maximum))
        {
            throw Invalid(
                number,
                operation,
                $"{what} must be from {Format(minimum)} to {Format(maximum)}, not {Format(value)}",
                $"Use a value from {Format(minimum)} to {Format(maximum)}.");
        }
    }

    /// <summary>
    ///     Requires a pixel count of at least 1.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when the value is less than 1.</exception>
    private static void RequireAtLeastOne(int number, IImageOperation operation, string what, int value)
    {
        if (value < 1)
        {
            throw Invalid(number, operation, $"{what} must be at least 1 pixel, not {Format(value)}", "Use a size of at least 1 pixel.");
        }
    }

    /// <summary>
    ///     Requires a pixel count not above <see cref="Surface.MaxDimension"/>.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when the value is larger than the maximum.</exception>
    private static void RequireWithinLimit(int number, IImageOperation operation, string what, int value)
    {
        if (value > Surface.MaxDimension)
        {
            throw new ViewerException(
                ErrorCodes.SizeLimit,
                $"Operation {number} ({operation.Name}): {what} is {Format(value)} pixels, more than the maximum of {Surface.MaxDimension} pixels.",
                $"Use a size of {Surface.MaxDimension} pixels or less.");
        }
    }

    /// <summary>
    ///     Requires a value that is a member of its enumeration.
    /// </summary>
    /// <typeparam name="TEnum">The enumeration.</typeparam>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when the value is not a member of <typeparamref name="TEnum"/>.</exception>
    private static void RequireDefined<TEnum>(int number, IImageOperation operation, string what, TEnum value)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw Invalid(number, operation, $"{what} is not a known {typeof(TEnum).Name} value ({Convert.ToInt64(value, CultureInfo.InvariantCulture)})", $"Use one of the members of {typeof(TEnum).Name}.");
        }
    }

    /// <summary>
    ///     Creates the exception for an invalid value.
    /// </summary>
    /// <param name="number">The position of the operation in the list, counted from 1.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="problem">What is wrong, without the position and the final period.</param>
    /// <param name="hint">How to correct it.</param>
    /// <returns>The exception to throw, with code <c>usage-error</c>.</returns>
    private static ViewerException Invalid(int number, IImageOperation operation, string problem, string hint)
    {
        return new ViewerException(ErrorCodes.UsageError, $"Operation {number} ({operation.Name}): {problem}.", hint);
    }

    /// <summary>
    ///     Formats a number for a message, independent of the current culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The shortest text that reads back as the same number.</returns>
    private static string Format(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
