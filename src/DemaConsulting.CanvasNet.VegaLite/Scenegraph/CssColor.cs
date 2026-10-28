using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Parses CSS colors into <see cref="Rgba32"/> values.
/// </summary>
/// <remarks>
///     <para>
///         Charts spell colors the CSS way (the Vega-Lite defaults are <c>#4c78a8</c> and <c>#ddd</c>), and the core
///         library has no CSS parser, so this unit holds one. The accepted forms are:
///     </para>
///     <list type="bullet">
///         <item><description>hexadecimal: <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c> (a short digit is doubled: <c>#f80</c> is <c>#ff8800</c>);</description></item>
///         <item><description>the 148 CSS named colors (including <c>rebeccapurple</c> and the <c>grey</c> spellings) and <c>transparent</c>, in any letter case;</description></item>
///         <item><description><c>rgb()</c> and <c>rgba()</c> with three channels as numbers (0 to 255) or percentages, and an optional alpha as a number (0 to 1) or a percentage, written with commas or with spaces and a slash (<c>rgb(10 20 30 / 50%)</c>);</description></item>
///         <item><description><c>hsl()</c> and <c>hsla()</c> with a hue in degrees (a number, or with the unit <c>deg</c>, <c>rad</c>, <c>grad</c> or <c>turn</c>), saturation and lightness as percentages, and the same alpha forms.</description></item>
///     </list>
///     <para>
///         Out-of-range channels are clamped, as CSS specifies; fractions are rounded half away from zero. Parsing is
///         culture invariant and never throws for bad text: <see cref="TryParse"/> returns <see langword="false"/>.
///         The core pixel type holds straight (not premultiplied) alpha, so <c>rgba(255,0,0,0.5)</c> is
///         <c>(255, 0, 0, 128)</c>.
///     </para>
///     <para>
///         <b>Paints.</b> A paint is a color or no color: <see langword="null"/>, an empty string and <c>none</c> mean
///         "do not draw" (<see cref="IsNone"/>). <see cref="TryResolvePaint"/> applies that rule and an opacity in one
///         step; a text that is neither a color nor none is treated as no paint as well, which is what a canvas does with
///         a color it cannot parse.
///     </para>
/// </remarks>
internal static class CssColor
{
    // cspell:disable -- hexadecimal color values below are not words
    /// <summary>The names and values of the CSS named colors, as <c>name=rrggbb</c> entries separated by spaces.</summary>
    private const string NamedColors =
        "aliceblue=f0f8ff antiquewhite=faebd7 aqua=00ffff aquamarine=7fffd4 azure=f0ffff beige=f5f5dc bisque=ffe4c4 " +
        "black=000000 blanchedalmond=ffebcd blue=0000ff blueviolet=8a2be2 brown=a52a2a burlywood=deb887 " +
        "cadetblue=5f9ea0 chartreuse=7fff00 chocolate=d2691e coral=ff7f50 cornflowerblue=6495ed cornsilk=fff8dc " +
        "crimson=dc143c cyan=00ffff darkblue=00008b darkcyan=008b8b darkgoldenrod=b8860b darkgray=a9a9a9 " +
        "darkgreen=006400 darkgrey=a9a9a9 darkkhaki=bdb76b darkmagenta=8b008b darkolivegreen=556b2f " +
        "darkorange=ff8c00 darkorchid=9932cc darkred=8b0000 darksalmon=e9967a darkseagreen=8fbc8f " +
        "darkslateblue=483d8b darkslategray=2f4f4f darkslategrey=2f4f4f darkturquoise=00ced1 darkviolet=9400d3 " +
        "deeppink=ff1493 deepskyblue=00bfff dimgray=696969 dimgrey=696969 dodgerblue=1e90ff firebrick=b22222 " +
        "floralwhite=fffaf0 forestgreen=228b22 fuchsia=ff00ff gainsboro=dcdcdc ghostwhite=f8f8ff gold=ffd700 " +
        "goldenrod=daa520 gray=808080 green=008000 greenyellow=adff2f grey=808080 honeydew=f0fff0 hotpink=ff69b4 " +
        "indianred=cd5c5c indigo=4b0082 ivory=fffff0 khaki=f0e68c lavender=e6e6fa lavenderblush=fff0f5 " +
        "lawngreen=7cfc00 lemonchiffon=fffacd lightblue=add8e6 lightcoral=f08080 lightcyan=e0ffff " +
        "lightgoldenrodyellow=fafad2 lightgray=d3d3d3 lightgreen=90ee90 lightgrey=d3d3d3 lightpink=ffb6c1 " +
        "lightsalmon=ffa07a lightseagreen=20b2aa lightskyblue=87cefa lightslategray=778899 lightslategrey=778899 " +
        "lightsteelblue=b0c4de lightyellow=ffffe0 lime=00ff00 limegreen=32cd32 linen=faf0e6 magenta=ff00ff " +
        "maroon=800000 mediumaquamarine=66cdaa mediumblue=0000cd mediumorchid=ba55d3 mediumpurple=9370db " +
        "mediumseagreen=3cb371 mediumslateblue=7b68ee mediumspringgreen=00fa9a mediumturquoise=48d1cc " +
        "mediumvioletred=c71585 midnightblue=191970 mintcream=f5fffa mistyrose=ffe4e1 moccasin=ffe4b5 " +
        "navajowhite=ffdead navy=000080 oldlace=fdf5e6 olive=808000 olivedrab=6b8e23 orange=ffa500 " +
        "orangered=ff4500 orchid=da70d6 palegoldenrod=eee8aa palegreen=98fb98 paleturquoise=afeeee " +
        "palevioletred=db7093 papayawhip=ffefd5 peachpuff=ffdab9 peru=cd853f pink=ffc0cb plum=dda0dd " +
        "powderblue=b0e0e6 purple=800080 rebeccapurple=663399 red=ff0000 rosybrown=bc8f8f royalblue=4169e1 " +
        "saddlebrown=8b4513 salmon=fa8072 sandybrown=f4a460 seagreen=2e8b57 seashell=fff5ee sienna=a0522d " +
        "silver=c0c0c0 skyblue=87ceeb slateblue=6a5acd slategray=708090 slategrey=708090 snow=fffafa " +
        "springgreen=00ff7f steelblue=4682b4 tan=d2b48c teal=008080 thistle=d8bfd8 tomato=ff6347 " +
        "turquoise=40e0d0 violet=ee82ee wheat=f5deb3 white=ffffff whitesmoke=f5f5f5 yellow=ffff00 " +
        "yellowgreen=9acd32";
    // cspell:enable

    /// <summary>The named colors by lower-case name; built once and never changed.</summary>
    private static readonly Dictionary<string, Rgba32> Named = BuildNamedColors();

    /// <summary>Gets the number of CSS named colors that are recognized, <c>transparent</c> not included.</summary>
    public static int NamedColorCount => Named.Count - 1;

    /// <summary>
    ///     Determines whether a paint means "do not draw": null, empty, white space or <c>none</c>.
    /// </summary>
    /// <param name="text">The paint.</param>
    /// <returns><see langword="true"/> for no paint.</returns>
    public static bool IsNone(string? text) =>
        string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Parses a CSS color.
    /// </summary>
    /// <param name="text">The color text, for example <c>#4c78a8</c>, <c>steelblue</c> or <c>rgba(0, 0, 0, 0.5)</c>.</param>
    /// <param name="color">The color when the text is valid; otherwise the default value.</param>
    /// <returns><see langword="true"/> when the text is a supported color; <see langword="false"/> for null, <c>none</c> and any other text.</returns>
    public static bool TryParse(string? text, out Rgba32 color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span[0] == '#')
        {
            return TryParseHex(span[1..], out color);
        }

        var open = span.IndexOf('(');
        if (open < 0)
        {
            return Named.TryGetValue(span.ToString().ToLowerInvariant(), out color);
        }

        if (span[^1] != ')')
        {
            return false;
        }

        var name = span[..open].Trim().ToString().ToLowerInvariant();
        var arguments = span[(open + 1)..^1].ToString();
        return name switch
        {
            "rgb" or "rgba" => TryParseRgb(arguments, out color),
            "hsl" or "hsla" => TryParseHsl(arguments, out color),
            _ => false,
        };
    }

    /// <summary>
    ///     Parses a CSS color and fails loudly on bad text.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <returns>The color.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a supported color.</exception>
    public static Rgba32 Parse(string? text) =>
        TryParse(text, out var color) ? color : throw new FormatException($"'{text}' is not a supported CSS color.");

    /// <summary>
    ///     Resolves a paint and an opacity into the color to draw with.
    /// </summary>
    /// <param name="paint">The paint: a CSS color, <c>none</c> or null.</param>
    /// <param name="opacity">The opacity that multiplies the alpha of the color, clamped to 0 to 1.</param>
    /// <param name="color">The color to draw with when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when there is nothing to draw: no paint, an unparseable paint, or a fully transparent result.</returns>
    public static bool TryResolvePaint(string? paint, double opacity, out Rgba32 color)
    {
        if (IsNone(paint) || !TryParse(paint, out color))
        {
            color = default;
            return false;
        }

        color = WithOpacity(color, opacity);
        return color.A > 0;
    }

    /// <summary>
    ///     Multiplies the alpha of a color by an opacity.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The opacity; clamped to 0 to 1, and not a number counts as 1.</param>
    /// <returns>The color with the new alpha, rounded half away from zero.</returns>
    public static Rgba32 WithOpacity(Rgba32 color, double opacity)
    {
        var factor = double.IsNaN(opacity) ? 1 : Math.Clamp(opacity, 0, 1);
        if (factor >= 1)
        {
            return color;
        }

        return new Rgba32(color.R, color.G, color.B, ToByte(color.A * factor));
    }

    /// <summary>
    ///     Formats a color as a lower-case hexadecimal CSS color: <c>#rrggbb</c>, or <c>#rrggbbaa</c> when it is not opaque.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The CSS text, which <see cref="TryParse"/> reads back to the same color.</returns>
    public static string ToCss(Rgba32 color) =>
        color.A == 255
            ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}");

    /// <summary>
    ///     Builds the table of named colors.
    /// </summary>
    /// <returns>The table, with <c>transparent</c> added.</returns>
    private static Dictionary<string, Rgba32> BuildNamedColors()
    {
        var table = new Dictionary<string, Rgba32>(StringComparer.Ordinal);
        foreach (var entry in NamedColors.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.IndexOf('=', StringComparison.Ordinal);
            var value = int.Parse(entry.AsSpan(separator + 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            table[entry[..separator]] = new Rgba32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
        }

        table["transparent"] = new Rgba32(0, 0, 0, 0);
        return table;
    }

    /// <summary>
    ///     Parses the digits after the <c>#</c> of a hexadecimal color.
    /// </summary>
    /// <param name="digits">The digits: 3, 4, 6 or 8 of them.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when the digits are valid.</returns>
    private static bool TryParseHex(ReadOnlySpan<char> digits, out Rgba32 color)
    {
        color = default;
        if (digits.Length is not (3 or 4 or 6 or 8))
        {
            return false;
        }

        Span<int> values = stackalloc int[4];
        values[3] = 255;
        var isShort = digits.Length <= 4;
        var channels = digits.Length / (isShort ? 1 : 2);
        for (var i = 0; i < channels; i++)
        {
            int value;
            if (isShort)
            {
                if (!TryHexDigit(digits[i], out var digit))
                {
                    return false;
                }

                value = (digit * 16) + digit;
            }
            else
            {
                if (!TryHexDigit(digits[2 * i], out var high) || !TryHexDigit(digits[(2 * i) + 1], out var low))
                {
                    return false;
                }

                value = (high * 16) + low;
            }

            values[i] = value;
        }

        color = new Rgba32((byte)values[0], (byte)values[1], (byte)values[2], (byte)values[3]);
        return true;
    }

    /// <summary>
    ///     Converts one hexadecimal digit.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <param name="value">The value, 0 to 15.</param>
    /// <returns><see langword="true"/> for a digit.</returns>
    private static bool TryHexDigit(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
        return value >= 0;
    }

    /// <summary>
    ///     Splits the arguments of a color function into channel tokens and an optional alpha token.
    /// </summary>
    /// <param name="arguments">The text between the parentheses.</param>
    /// <param name="channels">The three channel tokens.</param>
    /// <param name="alpha">The alpha token, or null when the color has none.</param>
    /// <returns><see langword="true"/> when there are three channels, optionally followed by an alpha.</returns>
    private static bool TrySplitArguments(string arguments, out string[] channels, out string? alpha)
    {
        channels = [];
        alpha = null;
        var tokens = arguments.Replace(",", " ", StringComparison.Ordinal).Replace("/", " / ", StringComparison.Ordinal)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<string>(tokens);
        var slash = list.IndexOf("/");
        if (slash >= 0)
        {
            if (slash != 3 || list.Count != 5)
            {
                return false;
            }

            list.RemoveAt(slash);
        }

        if (list.Count is not (3 or 4))
        {
            return false;
        }

        channels = [list[0], list[1], list[2]];
        alpha = list.Count == 4 ? list[3] : null;
        return true;
    }

    /// <summary>
    ///     Parses the arguments of <c>rgb()</c> or <c>rgba()</c>.
    /// </summary>
    /// <param name="arguments">The text between the parentheses.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when the arguments are valid.</returns>
    private static bool TryParseRgb(string arguments, out Rgba32 color)
    {
        color = default;
        if (!TrySplitArguments(arguments, out var channels, out var alphaText))
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
        {
            if (!TryParseNumberOrPercent(channels[i], 255, out var value))
            {
                return false;
            }

            bytes[i] = ToByte(Math.Clamp(value, 0, 255));
        }

        if (!TryParseAlpha(alphaText, out var alpha))
        {
            return false;
        }

        color = new Rgba32(bytes[0], bytes[1], bytes[2], alpha);
        return true;
    }

    /// <summary>
    ///     Parses the arguments of <c>hsl()</c> or <c>hsla()</c>.
    /// </summary>
    /// <param name="arguments">The text between the parentheses.</param>
    /// <param name="color">The color.</param>
    /// <returns><see langword="true"/> when the arguments are valid.</returns>
    private static bool TryParseHsl(string arguments, out Rgba32 color)
    {
        color = default;
        if (!TrySplitArguments(arguments, out var channels, out var alphaText)
            || !TryParseHue(channels[0], out var hue)
            || !TryParseNumberOrPercent(channels[1], 100, out var saturation)
            || !TryParseNumberOrPercent(channels[2], 100, out var lightness)
            || !TryParseAlpha(alphaText, out var alpha))
        {
            return false;
        }

        var s = Math.Clamp(saturation, 0, 100) / 100;
        var l = Math.Clamp(lightness, 0, 100) / 100;
        var h = ((hue % 360) + 360) % 360;
        var a = s * Math.Min(l, 1 - l);
        double Channel(int n)
        {
            var k = (n + (h / 30)) % 12;
            return l - (a * Math.Max(-1, Math.Min(Math.Min(k - 3, 9 - k), 1)));
        }

        color = new Rgba32(ToByte(Channel(0) * 255), ToByte(Channel(8) * 255), ToByte(Channel(4) * 255), alpha);
        return true;
    }

    /// <summary>
    ///     Parses a hue: a number of degrees or a number with a <c>deg</c>, <c>grad</c>, <c>rad</c> or <c>turn</c> unit.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="degrees">The hue in degrees.</param>
    /// <returns><see langword="true"/> when the token is a hue.</returns>
    private static bool TryParseHue(string token, out double degrees)
    {
        var lower = token.ToLowerInvariant();
        var factor = 1.0;
        var number = lower;
        foreach (var (unit, unitFactor) in new[] { ("deg", 1.0), ("grad", 0.9), ("rad", 180 / Math.PI), ("turn", 360.0) })
        {
            if (lower.EndsWith(unit, StringComparison.Ordinal))
            {
                number = lower[..^unit.Length];
                factor = unitFactor;
                break;
            }
        }

        if (TryParseNumber(number, out var value))
        {
            degrees = value * factor;
            return true;
        }

        degrees = 0;
        return false;
    }

    /// <summary>
    ///     Parses an alpha value: absent (opaque), a number from 0 to 1 or a percentage.
    /// </summary>
    /// <param name="token">The token, or null.</param>
    /// <param name="alpha">The alpha as a byte.</param>
    /// <returns><see langword="true"/> when the token is absent or valid.</returns>
    private static bool TryParseAlpha(string? token, out byte alpha)
    {
        alpha = 255;
        if (token is null)
        {
            return true;
        }

        if (!TryParseNumberOrPercent(token, 1, out var value))
        {
            return false;
        }

        alpha = ToByte(Math.Clamp(value, 0, 1) * 255);
        return true;
    }

    /// <summary>
    ///     Parses a number, or a percentage that is scaled to a reference value (100% is <paramref name="full"/>).
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="full">The value that 100% stands for.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the token is valid.</returns>
    private static bool TryParseNumberOrPercent(string token, double full, out double value)
    {
        if (token.EndsWith('%'))
        {
            if (TryParseNumber(token[..^1], out var percent))
            {
                value = percent / 100 * full;
                return true;
            }

            value = 0;
            return false;
        }

        // CSS lets a component be the keyword none, which counts as zero
        if (string.Equals(token, "none", StringComparison.OrdinalIgnoreCase))
        {
            value = 0;
            return true;
        }

        return TryParseNumber(token, out value);
    }

    /// <summary>
    ///     Parses a finite number in the invariant culture, without thousands separators or exponent signs beyond what
    ///     <see cref="NumberStyles.Float"/> allows.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the token is a finite number.</returns>
    private static bool TryParseNumber(string token, out double value) =>
        double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>
    ///     Rounds a channel value half away from zero into a byte.
    /// </summary>
    /// <param name="value">The value, from 0 to 255.</param>
    /// <returns>The byte.</returns>
    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
}
