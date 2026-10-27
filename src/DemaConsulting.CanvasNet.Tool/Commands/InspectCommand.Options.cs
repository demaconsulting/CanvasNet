// cspell:ignore canvasnet
using System.Globalization;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The option declarations and option parsing of <see cref="InspectCommand"/>.
/// </summary>
internal sealed partial class InspectCommand
{
    /// <summary>The names of the subcommands, in the order they are listed.</summary>
    internal static readonly IReadOnlyList<string> SubcommandNames = ["colors", "pixel"];

    /// <summary>The options of <see cref="RenderCommand.OptionTable"/> that write a file, and so do not apply to a command that writes none.</summary>
    private static readonly string[] OutputOptionNames = ["--all-pages", "--output", "--format", "--quality", "--no-clobber"];

    /// <summary>
    ///     The options of the command, in the order they are listed in the help.
    /// </summary>
    /// <remarks>
    ///     Every source option and every pipeline option of <see cref="RenderCommand.OptionTable"/> (all of them but the
    ///     options of the output and <c>--all-pages</c>, because <c>inspect</c> reads one page and writes no file), then
    ///     <c>--count</c> of <c>inspect colors</c> and <c>--radius</c> of <c>inspect pixel</c>. The declarations are
    ///     those of the render command, so the grammar and the help text cannot drift; the options of the other
    ///     subcommand are refused with a usage error.
    /// </remarks>
    internal static readonly IReadOnlyList<OptionSpec> OptionTable =
    [
        .. RenderCommand.OptionTable.Where(spec => !OutputOptionNames.Contains(spec.Name)),
        new(CountOption, [], true, $"colors: the number of dominant colors to report, {ImageAnalysis.MinColors}-{ImageAnalysis.MaxColors} (default {ImageAnalysis.DefaultColors})", "N"),
        new(RadiusOption, [], true, $"pixel: also report the pixels within this radius of the point and their mean, 0-{ImageAnalysis.MaxRadius} (default 0: the pixel only)", "R")
    ];

    /// <summary>
    ///     The settings of one invocation, read from the command line.
    /// </summary>
    /// <param name="Subcommand">The subcommand: <c>colors</c> or <c>pixel</c>.</param>
    /// <param name="Input">The input path.</param>
    /// <param name="Source">The options for loading the input.</param>
    /// <param name="Operations">The image pipeline: the operations in the order they are applied; empty for none.</param>
    /// <param name="Count">The number of dominant colors for <c>colors</c>.</param>
    /// <param name="Radius">The radius of the window for <c>pixel</c>.</param>
    /// <param name="Point">The point for <c>pixel</c>, or <see langword="null"/> for <c>colors</c>.</param>
    internal sealed record Settings(
        string Subcommand,
        string Input,
        SourceOptions Source,
        IReadOnlyList<IImageOperation> Operations,
        int Count,
        int Radius,
        (int X, int Y)? Point);

    /// <summary>
    ///     Reads the settings from the parsed command line.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the subcommand is missing or unknown, an option belongs to the other
    ///     subcommand, the input or the point is missing or malformed, or a value is malformed or out of range; and with
    ///     code <c>size-limit</c> when <c>--size</c> or an operation names a side larger than the maximum.
    /// </exception>
    private static Settings ReadSettings(ArgumentReader reader)
    {
        var subcommand = ReadSubcommand(reader);

        // An option of the other subcommand is a mistake, not something to ignore
        if (subcommand == "colors" && reader.Has(RadiusOption))
        {
            throw OtherSubcommand(RadiusOption, "pixel");
        }

        if (subcommand == "pixel" && reader.Has(CountOption))
        {
            throw OtherSubcommand(CountOption, "colors");
        }

        (int X, int Y)? point = null;
        if (subcommand == "colors")
        {
            RequireOperands(reader, 1, "an input file", "canvasnet inspect colors photo.jpg");
        }
        else
        {
            RequireOperands(reader, 2, "an input file and a point x,y", "canvasnet inspect pixel sketch.png 412,230");
            point = ParsePoint(reader.Positionals[2]);
        }

        var count = reader.GetInt(CountOption, ImageAnalysis.MinColors, ImageAnalysis.MaxColors) ?? ImageAnalysis.DefaultColors;
        var radius = reader.GetInt(RadiusOption, 0, ImageAnalysis.MaxRadius) ?? 0;
        var source = RenderCommand.ReadSourceOptions(reader);

        // Every operation is parsed now so that a malformed one is reported before anything is loaded
        var operations = ImagePipeline.Parse(reader, null, null);
        return new Settings(subcommand, reader.Positionals[1], source, operations, count, radius, point);
    }

    /// <summary>
    ///     Reads the subcommand, the first positional argument.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <returns>The subcommand in lower case.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when there is none or it is not a subcommand; the hint suggests the closest name.</exception>
    private static string ReadSubcommand(ArgumentReader reader)
    {
        if (reader.Positionals.Count == 0)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                "'inspect' needs a subcommand: colors or pixel.",
                "Examples: canvasnet inspect colors photo.jpg, canvasnet inspect pixel sketch.png 412,230");
        }

        var typed = reader.Positionals[0];
        var match = SubcommandNames.FirstOrDefault(name => string.Equals(name, typed, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return match;
        }

        var closest = SubcommandNames
            .Select(name => (Name: name, Distance: CommandRegistry.EditDistance(typed.ToLowerInvariant(), name)))
            .Where(candidate => candidate.Distance <= MaxSuggestionDistance)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Name)
            .FirstOrDefault();
        throw new ToolException(
            ErrorCodes.UsageError,
            $"Unknown inspect subcommand '{typed}'.",
            closest != null
                ? $"Did you mean '{closest}'? The subcommands are: {string.Join(", ", SubcommandNames)}."
                : $"The subcommands are: {string.Join(", ", SubcommandNames)}.");
    }

    /// <summary>
    ///     Requires the operands that follow the subcommand.
    /// </summary>
    /// <param name="reader">The parsed arguments.</param>
    /// <param name="count">The number of operands the subcommand takes.</param>
    /// <param name="expected">What the operands are, for the message.</param>
    /// <param name="example">An example command line, for the hint.</param>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the number of operands is not <paramref name="count"/>.</exception>
    private static void RequireOperands(ArgumentReader reader, int count, string expected, string example)
    {
        var given = reader.Positionals.Count - 1;
        if (given != count)
        {
            var found = given == 0 ? "none were given" : $"{given} were given";
            throw new ToolException(
                ErrorCodes.UsageError,
                $"'inspect {reader.Positionals[0].ToLowerInvariant()}' expects {expected}, but {found}.",
                $"Example: {example}");
        }
    }

    /// <summary>
    ///     Creates the error for an option that belongs to the other subcommand.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="owner">The subcommand the option belongs to.</param>
    /// <returns>The error.</returns>
    private static ToolException OtherSubcommand(string option, string owner)
    {
        return new ToolException(
            ErrorCodes.UsageError,
            $"The option {option} only applies to 'inspect {owner}'.",
            $"Remove {option}, or use 'inspect {owner}'.");
    }

    /// <summary>
    ///     Parses a point.
    /// </summary>
    /// <param name="text">The point, <c>x,y</c>: two whole numbers separated by a comma.</param>
    /// <returns>The x and y position; a position outside the image is refused when the point is sampled.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not two whole numbers separated by a comma.</exception>
    internal static (int X, int Y) ParsePoint(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parts = text.Split(',');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y))
        {
            return (x, y);
        }

        throw new ToolException(
            ErrorCodes.UsageError,
            $"Invalid value '{text}' for <x,y>: expected two whole numbers separated by a comma.",
            "Example: canvasnet inspect pixel sketch.png 412,230");
    }
}
