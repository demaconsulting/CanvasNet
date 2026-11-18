// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.PlantUml.Styling;

/// <content>
///     The colors of PlantUML: names, <c>#RRGGBB</c>, <c>#RGB</c>, the gray shorthands <c>#8</c> and <c>#D</c>, and
///     <c>transparent</c>.
/// </content>
internal static partial class SkinParams
{
    /// <summary>
    ///     Reads a PlantUML color.
    /// </summary>
    /// <remarks>
    ///     PlantUML writes a color as an HTML color name (<c>LightBlue</c>, with or without a leading <c>#</c>), as
    ///     <c>#RRGGBB</c> or <c>#RRGGBBAA</c>, as <c>#RGB</c>, as a single hexadecimal digit for a gray (<c>#8</c> is
    ///     <c>#888888</c>, <c>#D</c> is <c>#DDDDDD</c>), or as <c>transparent</c>. A gradient (<c>#red/blue</c>,
    ///     <c>#red|blue</c>, <c>#red\blue</c>, <c>#red-blue</c>) is read as its first color, because gradients are not
    ///     drawn. The method is pure and thread-safe.
    /// </remarks>
    /// <param name="text">The color text; null or blank is not a color.</param>
    /// <param name="color">Receives the color (fully transparent for <c>transparent</c>).</param>
    /// <returns>True when the text is a color.</returns>
    public static bool TryParseColor(string? text, out Rgba32 color)
    {
        color = default;
        var value = text?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return false;
        }

        if (TryParseSingle(value, out color))
        {
            return true;
        }

        // A gradient: the first color is drawn
        var separator = value.IndexOfAny(['/', '|', '\\', '-'], 1);
        return separator > 0 && TryParseSingle(value[..separator], out color);
    }

    /// <summary>
    ///     Reads one color without a gradient.
    /// </summary>
    /// <param name="value">The trimmed text.</param>
    /// <param name="color">Receives the color.</param>
    /// <returns>True when the text is a color.</returns>
    private static bool TryParseSingle(string value, out Rgba32 color)
    {
        color = default;
        var body = value.StartsWith('#') ? value[1..] : value;
        if (body.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Hexadecimal forms: a gray digit, #RGB, #RRGGBB, #RRGGBBAA
        if (body.Length > 0 && body.All(char.IsAsciiHexDigit))
        {
            switch (body.Length)
            {
                case 1:
                    var gray = (byte)(int.Parse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17);
                    color = new Rgba32(gray, gray, gray, 255);
                    return true;
                case 3 or 6 or 8:
                    return CssColor.TryParseHex(body, out color);
            }
        }

        // A color name, written with or without the hash
        return body.All(char.IsAsciiLetter) && CssColor.TryGetNamedColor(body.ToLowerInvariant(), out color);
    }
}
