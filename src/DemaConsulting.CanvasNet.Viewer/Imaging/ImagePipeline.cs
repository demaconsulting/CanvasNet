// cspell:ignore lanczos canvasnet deskew otsu
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The ordered image pipeline: applies a list of <see cref="IImageOperation"/> records (crop, resize, fit, rotate,
///     flip, pad, the photo adjustments, deskew, trim, and grid), in the order given, to a surface.
/// </summary>
/// <remarks>
///     <para>
///         The pipeline is shared by the <c>render</c> and <c>inspect</c> operations (<see cref="ImageRenderer"/> and
///         <see cref="ImageInspector"/>) and by the verb commands of the <c>canvasnet</c> tool: a verb is <c>render</c>
///         with one operation placed before the others. The operations are applied left to right, so a crop followed by
///         a resize gives a different image than the reverse. One decode and one encode serve the whole chain; every
///         intermediate surface is disposed as soon as the next one exists. Reading the operations from a command line
///         is the job of the tool, not of this class.
///     </para>
///     <para>
///         The pixel operations themselves (flip, pad, trim, and the photo adjustments) are methods of
///         <see cref="Surface"/> in the core library; this class orders them, maps a failure to a
///         <see cref="ViewerException"/>, and describes each step in an <see cref="AppliedOperation"/>.
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
///         The loader's own sizing (the scale, width, height, and size of a raster, and the page size of a vector
///         source) happens before the first operation of the pipeline.
///     </para>
/// </remarks>
public static partial class ImagePipeline
{
    /// <summary>
    ///     The default tolerance of a trim, 0 to 255: how far a pixel may differ from the background and still count as
    ///     background.
    /// </summary>
    public const int DefaultTrimTolerance = 8;

    /// <summary>
    ///     The distance in degrees from a multiple of 90 within which a rotation is an exact pixel move.
    /// </summary>
    private const double RightAngleEpsilon = 1e-9;

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
    ///     Gets the names of the resampling filters, in the order they are listed in messages.
    /// </summary>
    /// <remarks>
    ///     The names are the stable spelling used by <see cref="TryParseFilter"/>, by <see cref="FilterName"/>, and in
    ///     the <c>operations</c> of a result: <c>auto</c>, <c>nearest</c>, <c>bilinear</c>, <c>bicubic</c>,
    ///     <c>lanczos3</c>, and <c>area</c>.
    /// </remarks>
    public static IReadOnlyList<string> FilterNames { get; } = Filters.Select(f => f.Name).ToArray();

    /// <summary>
    ///     Parses a resampling filter name.
    /// </summary>
    /// <param name="text">The name: <c>auto</c>, <c>nearest</c>, <c>bilinear</c>, <c>bicubic</c>, <c>lanczos3</c>, or <c>area</c> (any case).</param>
    /// <param name="mode">The filter, when the text is a filter name; otherwise <see cref="ResamplingMode.Auto"/>.</param>
    /// <returns><see langword="true"/> when the text is a filter name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static bool TryParseFilter(string text, out ResamplingMode mode)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var (name, filter) in Filters)
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                mode = filter;
                return true;
            }
        }

        mode = ResamplingMode.Auto;
        return false;
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
    ///     Applies operations, in order, to a surface.
    /// </summary>
    /// <param name="input">The surface; it is neither modified nor disposed.</param>
    /// <param name="operations">The operations, in the order they are applied.</param>
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
    /// <exception cref="ViewerException">
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
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when the rotated image would be larger than <see cref="Surface.MaxDimension"/> on a side.</exception>
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
            throw new ViewerException(
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
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> when the region starts outside the image or is smaller than one pixel.</exception>
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
            throw new ViewerException(
                ErrorCodes.UsageError,
                $"The crop region starts at {left.ToString(CultureInfo.InvariantCulture)},{top.ToString(CultureInfo.InvariantCulture)}, outside the {source.Width}x{source.Height} image.",
                "Start the region inside the image; a region that extends past an edge is cut off at that edge.");
        }

        var clampedRight = Math.Min(right, source.Width);
        var clampedBottom = Math.Min(bottom, source.Height);
        if (clampedRight - left < 1 || clampedBottom - top < 1)
        {
            throw new ViewerException(
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
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when the expanded canvas would be larger than <see cref="Surface.MaxDimension"/>.</exception>
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
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when the result would be larger than <see cref="Surface.MaxDimension"/>.</exception>
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
            throw new ViewerException(
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
