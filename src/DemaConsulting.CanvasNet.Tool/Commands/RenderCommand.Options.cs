// cspell:ignore canvasnet jpeg tif tiff RRGGBB RRGGBBAA
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
    ///     Source options (<c>--scale</c>, <c>--width</c>, <c>--height</c>, <c>--size</c>, <c>--background</c>,
    ///     <c>--no-auto-orient</c>) come first, then the output options (<c>--output</c>, <c>--format</c>,
    ///     <c>--quality</c>, <c>--no-clobber</c>). Options of further source kinds and of the image pipeline are added
    ///     to this table as they are implemented.
    /// </remarks>
    internal static readonly IReadOnlyList<OptionSpec> OptionTable =
    [
        new("--scale", [], true, "Multiply the natural size by a factor, for example 2 or 0.5", "factor"),
        new("--width", [], true, "Target width in pixels; the height keeps the aspect ratio unless --height is given", "pixels"),
        new("--height", [], true, "Target height in pixels; the width keeps the aspect ratio unless --width is given", "pixels"),
        new("--size", [], true, "Exact target size, for example 800x600 (shorthand for --width and --height)", "WxH"),
        new("--background", [], true, "Color to flatten transparency over: white (default for SVG), black, #RRGGBB, #RRGGBBAA, transparent", "color"),
        new("--no-auto-orient", [], false, "Do not turn a JPEG upright according to its EXIF orientation"),
        new("--output", ["-o"], true, "The output file; its extension selects the format", "file"),
        new("--format", [], true, "Output format: png, jpeg, bmp, or tiff (overrides the extension)", "format"),
        new("--quality", [], true, "JPEG quality, 1-100 (default 90)", "1-100"),
        new("--no-clobber", [], false, "Fail instead of overwriting an existing output file")
    ];

    /// <summary>
    ///     The settings of one invocation, read from the command line.
    /// </summary>
    /// <param name="Input">The input path.</param>
    /// <param name="Output">The output path.</param>
    /// <param name="Source">The options for loading the input.</param>
    /// <param name="Write">The options for writing the output.</param>
    internal sealed record Settings(string Input, string Output, SourceOptions Source, WriteOptions Write);

    /// <summary>
    ///     Reads the settings from the parsed command line.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the input or the output is missing, a value is malformed or out of
    ///     range, or the sizing options contradict each other; and with code <c>size-limit</c> when <c>--size</c> names a
    ///     side larger than the maximum.
    /// </exception>
    private static Settings ReadSettings(ArgumentReader reader)
    {
        reader.RequirePositionals(1, 1, "one input file");

        var output = reader.Get("--output")
                     ?? throw new ToolException(
                         ErrorCodes.UsageError,
                         "No output file was given.",
                         "Add -o <file>, for example: canvasnet render input.svg -o output.png");

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
        var quality = reader.GetInt("--quality", ImageWriter.MinQuality, ImageWriter.MaxQuality) ?? ImageWriter.DefaultQuality;

        var source = new SourceOptions
        {
            Scale = scale,
            Width = width,
            Height = height,
            Background = background,
            AutoOrient = !reader.Has("--no-auto-orient")
        };

        // Validate the format name now so that a typo is reported before anything is loaded
        var formatOption = reader.Get("--format");
        var format = formatOption == null ? (OutputFormat?)null : ImageWriter.ResolveFormat(output, formatOption);
        var flattenBackground = background is { A: > 0 } color ? color : WriteOptions.Default.FlattenBackground;
        var write = new WriteOptions(format, quality, reader.Has("--no-clobber"), flattenBackground);
        return new Settings(reader.Positionals[0], output, source, write);
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
