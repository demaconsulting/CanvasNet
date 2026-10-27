// cspell:ignore canvasnet jpeg tif tiff RRGGBB RRGGBBAA deskew otsu midtones
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The option declarations and option parsing of <see cref="RenderCommand"/>.
/// </summary>
internal sealed partial class RenderCommand
{
    /// <summary>
    ///     The options of the command, in the order they are listed in the help.
    /// </summary>
    /// <remarks>
    ///     Source options (<c>--page</c>, <c>--all-pages</c>, <c>--dpi</c>, <c>--scale</c>, <c>--width</c>, <c>--height</c>,
    ///     <c>--size</c>, <c>--background</c>, <c>--no-auto-orient</c>, <c>--strict</c>) come first, then the options of
    ///     an OpenSCAD model (<c>--view</c>, <c>--azimuth</c>, <c>--elevation</c>, <c>--projection</c>, <c>--define</c>,
    ///     <c>--time</c>, <c>--default-color</c>, <c>--max-triangles</c>), then the image pipeline
    ///     (<c>--crop</c>, <c>--resize</c>, <c>--fit</c>, <c>--allow-upscale</c>, <c>--rotate</c>, <c>--rotate-fill</c>,
    ///     <c>--flip</c>, <c>--pad</c>, <c>--pad-color</c>, <c>--grayscale</c>, <c>--invert</c>, <c>--threshold</c>,
    ///     <c>--levels</c>, <c>--brightness</c>, <c>--contrast</c>, <c>--auto-levels</c>, <c>--auto-levels-clip</c>,
    ///     <c>--deskew</c>, <c>--trim</c>, <c>--trim-tolerance</c>, <c>--grid</c>, <c>--filter</c>), then the output
    ///     options (<c>--output</c>, <c>--format</c>, <c>--quality</c>, <c>--no-clobber</c>). The verb commands and
    ///     <c>inspect</c> share this table (<c>inspect</c> without the output options).
    /// </remarks>
    internal static readonly IReadOnlyList<OptionSpec> OptionTable =
    [
        new("--page", [], true, "The page or slide to render, counted from 1 (default 1)", "N"),
        new("--all-pages", [], false, "Render every page or slide; the output must contain {page} or be a directory"),
        new("--dpi", [], true, "Resolution of a PDF, PPTX, or VSDX page in dots per inch (default 96; VSDX needs a whole number)", "D"),
        new("--scale", [], true, "Multiply the natural size by a factor, for example 2 or 0.5", "factor"),
        new("--width", [], true, "Target width in pixels; the height keeps the aspect ratio unless --height is given", "pixels"),
        new("--height", [], true, "Target height in pixels; the width keeps the aspect ratio unless --width is given", "pixels"),
        new("--size", [], true, "Exact target size, for example 800x600 (shorthand for --width and --height)", "WxH"),
        new("--background", [], true, "Color to flatten transparency over: white (default for vector sources), black, #RRGGBB, #RRGGBBAA, transparent", "color"),
        new("--no-auto-orient", [], false, "Do not turn a JPEG upright according to its EXIF orientation"),
        new("--strict", [], false, "Fail with unsupported-feature instead of skipping a picture or shape the library cannot draw"),
        new("--view", [], true, "SCAD model: the viewpoint - iso (default), front, back, left, right, top, or bottom", "name"),
        new("--azimuth", [], true, "SCAD model: replace the azimuth of the view, in degrees about the Z axis (0 = from the front)", "degrees"),
        new("--elevation", [], true, "SCAD model: replace the elevation of the view, in degrees above the XY plane (-90 to 90)", "degrees"),
        new("--projection", [], true, "SCAD model: ortho (default) or perspective", "type"),
        new("--define", ["-D"], true, "SCAD model: set a variable, for example -D width=20 or -D label=text (repeatable)", "name=value"),
        new("--time", [], true, "SCAD model: the value of $t, the animation time (default 0)", "T"),
        new("--default-color", [], true, "SCAD model: the color of surfaces the model does not color (default gray)", "color"),
        new("--max-triangles", [], true, "SCAD model: the most triangles the model may produce (default 5000000)", "N"),
        new(ImagePipeline.CropOption, [], true, "Crop to a rectangle x,y,w,h or WxH+X+Y, each value in pixels or percent (repeatable; operations run in the order written)", "geometry"),
        new(ImagePipeline.ResizeOption, [], true, "Resample to exactly this size, for example 800x600", "WxH"),
        new(ImagePipeline.FitOption, [], true, "Shrink to fit inside this box, keeping the aspect ratio (an image that already fits is unchanged)", "WxH"),
        new(ImagePipeline.AllowUpscaleOption, [], false, "Let --fit enlarge an image that is smaller than the box"),
        new(ImagePipeline.RotateOption, [], true, "Rotate clockwise by any angle in degrees, for example 90 or -3.5; an angle that is not a multiple of 90 grows the canvas and fills the corners (see --rotate-fill)", "degrees"),
        new(ImagePipeline.RotateFillOption, [], true, "Color of the corners a --rotate or --deskew exposes (default: the --background, else white for an opaque image, else transparent)", "color"),
        new(ImagePipeline.FlipOption, [], true, "Mirror: h (left to right), v (top to bottom), or both", "axis"),
        new(ImagePipeline.PadOption, [], true, "Add a border of N pixels on every side, or l,t,r,b pixels on each side", "N|l,t,r,b"),
        new(ImagePipeline.PadColorOption, [], true, "Color of the border added by --pad (default: the --background, else white for a drawing or document, else transparent)", "color"),
        new(ImagePipeline.GrayscaleOption, [], false, "Turn every pixel gray (its luma); transparency is kept"),
        new(ImagePipeline.InvertOption, [], false, "Invert the colors; transparency is kept"),
        new(ImagePipeline.ThresholdOption, [], true, "Turn the image black and white: pixels with a luma above the level (0-255) become white; auto chooses the level with Otsu's method", "0-255|auto"),
        new(ImagePipeline.LevelsOption, [], true, "Map the levels black..white to 0..255, with an optional gamma (0.1-10; above 1 brightens the midtones), for example 20,230 or 20,230,1.2", "black,white[,gamma]"),
        new(ImagePipeline.BrightnessOption, [], true, "Add an amount (-255 to 255) to every color level", "-255..255"),
        new(ImagePipeline.ContrastOption, [], true, "Change the contrast around mid-gray by a percentage (-100 to 100)", "-100..100"),
        new(ImagePipeline.AutoLevelsOption, [], false, "Stretch the levels so the darkest and lightest pixels become black and white (ignoring 1% of the pixels at each end; see --auto-levels-clip)"),
        new(ImagePipeline.AutoLevelsClipOption, [], true, "Percentage of the pixels --auto-levels ignores at each end, 0-20 (default 1)", "percent"),
        new(ImagePipeline.DeskewOption, [], false, "Straighten the image: find the tilt of its near-horizontal lines (within 15 degrees) and rotate it level; the angle is in the --json result"),
        new(ImagePipeline.TrimOption, [], false, "Crop away the uniform border around the content (the background is the most common color of the four corners)"),
        new(ImagePipeline.TrimToleranceOption, [], true, "How far a pixel may differ from the background and still count as background, 0-255 (default 8)", "0-255"),
        new(ImagePipeline.GridOption, [], true, "Draw a labeled coordinate grid with a line every N pixels (8-4096) in the color given (default #FF00FFA0); put it last to read the final coordinates", "N[,color]"),
        new(ImagePipeline.FilterOption, [], true, "Resampling filter of a resize or fit: auto (default), nearest, bilinear, bicubic, lanczos3, or area", "name"),
        new("--output", ["-o"], true, "The output file; its extension selects the format", "file"),
        new("--format", [], true, "Output format: png, jpeg, bmp, or tiff (overrides the extension)", "format"),
        new("--quality", [], true, "JPEG quality, 1-100 (default 90)", "1-100"),
        new("--no-clobber", [], false, "Fail instead of overwriting an existing output file")
    ];

    /// <summary>
    ///     Selects option declarations of <see cref="OptionTable"/> by name, so that another command can accept the
    ///     same option with the same grammar and help text.
    /// </summary>
    /// <param name="names">The long names of the options, for example <c>--page</c>.</param>
    /// <returns>The declarations, in the order of <paramref name="names"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="names"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a name is not declared by <see cref="OptionTable"/>.</exception>
    internal static IReadOnlyList<OptionSpec> SpecsFor(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var specs = new List<OptionSpec>(names.Length);
        foreach (var name in names)
        {
            specs.Add(OptionTable.FirstOrDefault(spec => spec.Name == name)
                      ?? throw new ArgumentException($"The option {name} is not declared by the render command.", nameof(names)));
        }

        return specs;
    }

    /// <summary>
    ///     The settings of one invocation, read from the command line.
    /// </summary>
    /// <param name="Input">The input path.</param>
    /// <param name="Output">The output path.</param>
    /// <param name="Source">The options for loading the input.</param>
    /// <param name="Write">The options for writing the output.</param>
    /// <param name="AllPages"><see langword="true"/> to render every page of the input.</param>
    /// <param name="Operations">The image pipeline: the operations in the order they are applied; empty for none.</param>
    internal sealed record Settings(
        string Input,
        string Output,
        SourceOptions Source,
        WriteOptions Write,
        bool AllPages,
        IReadOnlyList<IImageOperation> Operations);

    /// <summary>
    ///     Reads the settings from the parsed command line.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <param name="verb">The verb command being run, or <see langword="null"/> for <c>render</c>.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the input, the output, or the operand of a verb is missing, a value
    ///     is malformed or out of range, or the sizing options contradict each other; and with code <c>size-limit</c>
    ///     when <c>--size</c> or an operation names a side larger than the maximum.
    /// </exception>
    private static Settings ReadSettings(ArgumentReader reader, string? verb)
    {
        if (verb == null)
        {
            reader.RequirePositionals(1, 1, "one input file");
        }
        else
        {
            reader.RequirePositionals(2, 2, $"an input file and a {VerbTexts[verb].Operand}");
        }

        var output = reader.Get("--output")
                     ?? throw new ToolException(
                         ErrorCodes.UsageError,
                         "No output file was given.",
                         verb == null
                             ? "Add -o <file>, for example: canvasnet render input.svg -o output.png"
                             : $"Add -o <file>, for example: canvasnet {verb} input.png {VerbTexts[verb].Operand} -o output.png");

        var source = ReadSourceOptions(reader);
        var quality = reader.GetInt("--quality", ImageWriter.MinQuality, ImageWriter.MaxQuality) ?? ImageWriter.DefaultQuality;

        if (reader.Has("--page") && reader.Has("--all-pages"))
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "The options --page and --all-pages cannot be used together.",
                "Use --page N for one page, or --all-pages for every page.");
        }

        // Validate the format name now so that a typo is reported before anything is loaded
        var formatOption = reader.Get("--format");
        var format = formatOption == null ? (OutputFormat?)null : ImageWriter.ResolveFormat(output, formatOption);
        var flattenBackground = source.Background is { A: > 0 } color ? color : WriteOptions.Default.FlattenBackground;
        var write = new WriteOptions(format, quality, reader.Has("--no-clobber"), flattenBackground);

        // The operand of a verb is the second positional (the first is the input); every operation is parsed now so
        // that a malformed one is reported before anything is loaded
        var operations = ImagePipeline.Parse(reader, verb, verb == null ? null : reader.Positionals[1]);
        return new Settings(reader.Positionals[0], output, source, write, reader.Has("--all-pages"), operations);
    }

    /// <summary>
    ///     Reads the options that decide how an input is loaded.
    /// </summary>
    /// <param name="reader">The parsed arguments of a command that declares the source options.</param>
    /// <returns>
    ///     The page, resolution, strictness, size (<c>--scale</c>, <c>--width</c>, <c>--height</c>, or <c>--size</c>),
    ///     background, EXIF orientation, resampling filter, and model view options.
    /// </returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a value is malformed or out of range or the sizing options contradict
    ///     each other; and with code <c>size-limit</c> when <c>--size</c> names a side larger than the maximum.
    /// </exception>
    /// <remarks>
    ///     The options of the output (<c>--output</c>, <c>--format</c>, <c>--quality</c>, <c>--no-clobber</c>,
    ///     <c>--all-pages</c>) are not read here; <c>render</c> reads them itself and <c>inspect</c> has none.
    /// </remarks>
    internal static SourceOptions ReadSourceOptions(ArgumentReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var width = reader.GetInt("--width", 1, int.MaxValue);
        var height = reader.GetInt("--height", 1, int.MaxValue);
        var scale = reader.GetDouble("--scale", 0.001, Surface.MaxDimension);
        if (reader.Get("--size") is { } sizeText)
        {
            RejectCombination(reader, "--size", "--scale", "--width", "--height");
            (width, height) = ArgumentReader.ParseSize(sizeText, "--size");
        }

        if (scale != null)
        {
            RejectCombination(reader, "--scale", "--width", "--height");
        }

        var background = reader.Get("--background") is { } colorText ? ArgumentReader.ParseColor(colorText, "--background") : (Rgba32?)null;
        return new SourceOptions
        {
            Page = reader.Get("--page") is { } pageText ? ArgumentReader.ParsePage(pageText, "--page") : 1,
            Dpi = reader.Get("--dpi") is { } dpiText ? ArgumentReader.ParseDpi(dpiText, "--dpi") : null,
            Strict = reader.Has("--strict"),
            Scale = scale,
            Width = width,
            Height = height,
            Background = background,
            AutoOrient = !reader.Has("--no-auto-orient"),
            Filter = reader.Get(ImagePipeline.FilterOption) is { } filterText
                ? ImagePipeline.ParseFilter(filterText, ImagePipeline.FilterOption)
                : ResamplingMode.Auto,
            Scad = ReadScadOptions(reader)
        };
    }

    /// <summary>
    ///     The options that only apply to an OpenSCAD model.
    /// </summary>
    internal static readonly string[] ScadOptionNames =
    [
        "--view", "--azimuth", "--elevation", "--projection", "--define", "--time", "--default-color", "--max-triangles"
    ];

    /// <summary>
    ///     Reads the options that apply to an OpenSCAD model.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <returns>The view options, or <see langword="null"/> when none of the model options was given.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the view is not one of the views, a value is malformed or out of range,
    ///     or a definition is not <c>name=value</c>.
    /// </exception>
    internal static ScadViewOptions? ReadScadOptions(ArgumentReader reader)
    {
        if (!ScadOptionNames.Any(reader.Has))
        {
            return null;
        }

        var view = reader.Get("--view") is { } viewText ? ParseScadView(viewText) : SourceLoader.DefaultScadView;

        var perspective = false;
        if (reader.Get("--projection") is { } projectionText)
        {
            perspective = projectionText.ToLowerInvariant() switch
            {
                "ortho" or "orthographic" => false,
                "perspective" => true,
                _ => throw new ToolException(
                    ErrorCodes.UsageError,
                    $"'{projectionText}' is not a projection.",
                    "The projections are: ortho, perspective.")
            };
        }

        return new ScadViewOptions
        {
            View = view,
            Azimuth = reader.Get("--azimuth") is { } azimuthText ? ArgumentReader.ParseDegrees(azimuthText, "--azimuth") : null,
            Elevation = reader.GetDouble("--elevation", -90, 90),
            Perspective = perspective,
            Defines = ArgumentReader.ParseDefines(reader.GetAll("--define")),
            Time = reader.GetDouble("--time", -1e9, 1e9),
            MaxTriangles = reader.GetInt("--max-triangles", 1, int.MaxValue),
            DefaultColor = reader.Get("--default-color") is { } colorText ? ArgumentReader.ParseColor(colorText, "--default-color") : null
        };
    }

    /// <summary>
    ///     Finds the name of a viewpoint of an OpenSCAD model.
    /// </summary>
    /// <param name="text">The name as typed; compared without regard to case.</param>
    /// <returns>The lower-case name of the view.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not one of the views.</exception>
    internal static string ParseScadView(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return SourceLoader.ScadViews.FirstOrDefault(v => string.Equals(v.Name, text, StringComparison.OrdinalIgnoreCase))?.Name
               ?? throw new ToolException(
                   ErrorCodes.UsageError,
                   $"'{text}' is not a view.",
                   $"The views are: {string.Join(", ", SourceLoader.ScadViews.Select(v => v.Name))}.");
    }

    /// <summary>
    ///     Requires that an option is not combined with others that also choose the size.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <param name="option">The option that was given.</param>
    /// <param name="others">The options it cannot be combined with.</param>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when one of the others was given as well.</exception>
    private static void RejectCombination(ArgumentReader reader, string option, params string[] others)
    {
        var conflict = others.FirstOrDefault(reader.Has);
        if (conflict != null)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The options {option} and {conflict} cannot be used together.",
                "Choose one way to set the size: --scale, --size, or --width and/or --height.");
        }
    }
}
