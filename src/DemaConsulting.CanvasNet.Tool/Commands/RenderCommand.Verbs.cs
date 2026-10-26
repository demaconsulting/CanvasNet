// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The verb commands of <see cref="RenderCommand"/>: <c>crop</c>, <c>resize</c>, <c>rotate</c>, <c>flip</c>, and <c>pad</c>.
/// </summary>
/// <remarks>
///     A verb is the <c>render</c> command with one operation placed first in the image pipeline. The operation
///     comes from a positional operand after the input, so <c>canvasnet crop in.png 10,10,100,100 -o out.png</c>
///     does exactly what <c>canvasnet render in.png --crop 10,10,100,100 -o out.png</c> does. The verb accepts every
///     option of <c>render</c>; operations given as options are applied after the verb's own.
/// </remarks>
internal sealed partial class RenderCommand
{
    /// <summary>
    ///     The verb this instance runs, or <see langword="null"/> for <c>render</c> itself.
    /// </summary>
    private readonly string? _verb;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RenderCommand"/> class as the <c>render</c> command.
    /// </summary>
    public RenderCommand()
        : this(null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="RenderCommand"/> class.
    /// </summary>
    /// <param name="verb">
    ///     One of <see cref="ImagePipeline.VerbNames"/> to run that verb, or <see langword="null"/> for the
    ///     <c>render</c> command.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="verb"/> is not a verb.</exception>
    public RenderCommand(string? verb)
    {
        if (verb != null && !VerbTexts.ContainsKey(verb))
        {
            throw new ArgumentException($"'{verb}' is not an image verb.", nameof(verb));
        }

        _verb = verb;
    }

    /// <summary>
    ///     The words that describe one verb in the help.
    /// </summary>
    /// <param name="Operand">The operand placeholder, for example <c>&lt;geometry&gt;</c>.</param>
    /// <param name="Summary">The one-line summary.</param>
    /// <param name="Description">What the verb does and what its operand may be.</param>
    /// <param name="Examples">The example command lines.</param>
    private sealed record VerbText(string Operand, string Summary, string Description, string[] Examples);

    /// <summary>
    ///     The help text of each verb.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, VerbText> VerbTexts = new Dictionary<string, VerbText>
    {
        ["crop"] = new(
            "<geometry>",
            "Cut a rectangle out of an image, drawing, or document page",
            "Cuts out the rectangle x,y,w,h or WxH+X+Y (origin and size), each value in pixels (10 or 10px) or percent (10%)."
            + Environment.NewLine
            + "A part that lies outside the image is cut off at its edge; a rectangle that starts outside the image is an error.",
            [
                "canvasnet crop photo.png 10,10,200,100 -o part.png",
                "canvasnet crop photo.png 200x100+10+10 -o part.png",
                "canvasnet crop slide.pdf 0,0,50%,100% --page 2 --dpi 150 -o left-half.png"
            ]),
        ["resize"] = new(
            "<WxH>",
            "Resample an image, drawing, or document page to an exact size",
            "Resamples to exactly the width and height given, for example 800x600; the aspect ratio is not kept." + Environment.NewLine
            + "To keep it, use render with --fit WxH (shrink into a box) or --width / --height.",
            [
                "canvasnet resize photo.png 800x600 -o small.png",
                "canvasnet resize photo.png 64x64 --filter nearest -o icon.png"
            ]),
        ["rotate"] = new(
            "<degrees>",
            "Rotate an image, drawing, or document page by 90, 180, or 270 degrees",
            "Rotates clockwise by 90, 180, or 270 degrees (-90 is 270). Other angles are not supported yet.",
            [
                "canvasnet rotate scan.png 90 -o upright.png",
                "canvasnet rotate scan.jpg 180 -o flipped.jpg --quality 85"
            ]),
        ["flip"] = new(
            "<axis>",
            "Mirror an image, drawing, or document page",
            "Mirrors left to right (h), top to bottom (v), or both (both, the same as rotating by 180 degrees).",
            [
                "canvasnet flip photo.png h -o mirrored.png",
                "canvasnet flip photo.png v -o upside-down.png"
            ]),
        ["pad"] = new(
            "<border>",
            "Add a border around an image, drawing, or document page",
            "Adds a border of N pixels on every side, or of l,t,r,b pixels on the left, top, right, and bottom." + Environment.NewLine
            + "--pad-color sets its color; by default it is the --background color, else white for a drawing or document, else transparent.",
            [
                "canvasnet pad logo.png 16 -o padded.png",
                "canvasnet pad logo.png 0,0,32,32 --pad-color #336699 -o padded.png"
            ])
    };

    /// <summary>
    ///     Builds the usage text of a verb.
    /// </summary>
    /// <param name="verb">The verb.</param>
    /// <returns>The usage line, the description, and the examples.</returns>
    private static string VerbUsage(string verb)
    {
        var text = VerbTexts[verb];
        return $"canvasnet {verb} <input> {text.Operand} -o <output> [options]" + Environment.NewLine
               + Environment.NewLine
               + text.Description + Environment.NewLine
               + $"'{verb}' is 'render' with this one operation first, so it takes every option of render (--page, --dpi," + Environment.NewLine
               + "--background, --format, --quality, --no-clobber, --json, ...). The operations given as options" + Environment.NewLine
               + "(--crop, --resize, --fit, --rotate, --flip, --pad) are applied after it, in the order written." + Environment.NewLine
               + Environment.NewLine
               + "Examples:" + Environment.NewLine
               + string.Join(Environment.NewLine, text.Examples.Select(e => "  " + e));
    }
}
