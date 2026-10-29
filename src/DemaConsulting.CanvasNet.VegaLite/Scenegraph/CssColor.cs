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
    /// <summary>
    ///     The CSS named colors as PascalCase names and <c>0xRRGGBB</c> values; the table is built from numbers and
    ///     camel-case word parts so that no unusual all-lower-case spelling needs special handling by the spell checker.
    ///     The names are lower-cased when the lookup table is built.
    /// </summary>
    private static readonly (string Name, int Rgb)[] NamedColors =
    [
        ("AliceBlue", 0xF0F8FF),
        ("AntiqueWhite", 0xFAEBD7),
        ("Aqua", 0x00FFFF),
        ("AquaMarine", 0x7FFFD4),
        ("Azure", 0xF0FFFF),
        ("Beige", 0xF5F5DC),
        ("Bisque", 0xFFE4C4),
        ("Black", 0x000000),
        ("BlanchedAlmond", 0xFFEBCD),
        ("Blue", 0x0000FF),
        ("BlueViolet", 0x8A2BE2),
        ("Brown", 0xA52A2A),
        ("BurlyWood", 0xDEB887),
        ("CadetBlue", 0x5F9EA0),
        ("Chartreuse", 0x7FFF00),
        ("Chocolate", 0xD2691E),
        ("Coral", 0xFF7F50),
        ("CornFlowerBlue", 0x6495ED),
        ("CornSilk", 0xFFF8DC),
        ("Crimson", 0xDC143C),
        ("Cyan", 0x00FFFF),
        ("DarkBlue", 0x00008B),
        ("DarkCyan", 0x008B8B),
        ("DarkGoldenRod", 0xB8860B),
        ("DarkGray", 0xA9A9A9),
        ("DarkGreen", 0x006400),
        ("DarkGrey", 0xA9A9A9),
        ("DarkKhaki", 0xBDB76B),
        ("DarkMagenta", 0x8B008B),
        ("DarkOliveGreen", 0x556B2F),
        ("DarkOrange", 0xFF8C00),
        ("DarkOrchid", 0x9932CC),
        ("DarkRed", 0x8B0000),
        ("DarkSalmon", 0xE9967A),
        ("DarkSeaGreen", 0x8FBC8F),
        ("DarkSlateBlue", 0x483D8B),
        ("DarkSlateGray", 0x2F4F4F),
        ("DarkSlateGrey", 0x2F4F4F),
        ("DarkTurquoise", 0x00CED1),
        ("DarkViolet", 0x9400D3),
        ("DeepPink", 0xFF1493),
        ("DeepSkyBlue", 0x00BFFF),
        ("DimGray", 0x696969),
        ("DimGrey", 0x696969),
        ("DodgerBlue", 0x1E90FF),
        ("FireBrick", 0xB22222),
        ("FloralWhite", 0xFFFAF0),
        ("ForestGreen", 0x228B22),
        ("Fuchsia", 0xFF00FF),
        ("Gainsboro", 0xDCDCDC),
        ("GhostWhite", 0xF8F8FF),
        ("Gold", 0xFFD700),
        ("GoldenRod", 0xDAA520),
        ("Gray", 0x808080),
        ("Green", 0x008000),
        ("GreenYellow", 0xADFF2F),
        ("Grey", 0x808080),
        ("HoneyDew", 0xF0FFF0),
        ("HotPink", 0xFF69B4),
        ("IndianRed", 0xCD5C5C),
        ("Indigo", 0x4B0082),
        ("Ivory", 0xFFFFF0),
        ("Khaki", 0xF0E68C),
        ("Lavender", 0xE6E6FA),
        ("LavenderBlush", 0xFFF0F5),
        ("LawnGreen", 0x7CFC00),
        ("LemonChiffon", 0xFFFACD),
        ("LightBlue", 0xADD8E6),
        ("LightCoral", 0xF08080),
        ("LightCyan", 0xE0FFFF),
        ("LightGoldenRodYellow", 0xFAFAD2),
        ("LightGray", 0xD3D3D3),
        ("LightGreen", 0x90EE90),
        ("LightGrey", 0xD3D3D3),
        ("LightPink", 0xFFB6C1),
        ("LightSalmon", 0xFFA07A),
        ("LightSeaGreen", 0x20B2AA),
        ("LightSkyBlue", 0x87CEFA),
        ("LightSlateGray", 0x778899),
        ("LightSlateGrey", 0x778899),
        ("LightSteelBlue", 0xB0C4DE),
        ("LightYellow", 0xFFFFE0),
        ("Lime", 0x00FF00),
        ("LimeGreen", 0x32CD32),
        ("Linen", 0xFAF0E6),
        ("Magenta", 0xFF00FF),
        ("Maroon", 0x800000),
        ("MediumAquaMarine", 0x66CDAA),
        ("MediumBlue", 0x0000CD),
        ("MediumOrchid", 0xBA55D3),
        ("MediumPurple", 0x9370DB),
        ("MediumSeaGreen", 0x3CB371),
        ("MediumSlateBlue", 0x7B68EE),
        ("MediumSpringGreen", 0x00FA9A),
        ("MediumTurquoise", 0x48D1CC),
        ("MediumVioletRed", 0xC71585),
        ("MidnightBlue", 0x191970),
        ("MintCream", 0xF5FFFA),
        ("MistyRose", 0xFFE4E1),
        ("Moccasin", 0xFFE4B5),
        ("NavajoWhite", 0xFFDEAD),
        ("Navy", 0x000080),
        ("OldLace", 0xFDF5E6),
        ("Olive", 0x808000),
        ("OliveDrab", 0x6B8E23),
        ("Orange", 0xFFA500),
        ("OrangeRed", 0xFF4500),
        ("Orchid", 0xDA70D6),
        ("PaleGoldenRod", 0xEEE8AA),
        ("PaleGreen", 0x98FB98),
        ("PaleTurquoise", 0xAFEEEE),
        ("PaleVioletRed", 0xDB7093),
        ("PapayaWhip", 0xFFEFD5),
        ("PeachPuff", 0xFFDAB9),
        ("Peru", 0xCD853F),
        ("Pink", 0xFFC0CB),
        ("Plum", 0xDDA0DD),
        ("PowderBlue", 0xB0E0E6),
        ("Purple", 0x800080),
        ("RebeccaPurple", 0x663399),
        ("Red", 0xFF0000),
        ("RosyBrown", 0xBC8F8F),
        ("RoyalBlue", 0x4169E1),
        ("SaddleBrown", 0x8B4513),
        ("Salmon", 0xFA8072),
        ("SandyBrown", 0xF4A460),
        ("SeaGreen", 0x2E8B57),
        ("SeaShell", 0xFFF5EE),
        ("Sienna", 0xA0522D),
        ("Silver", 0xC0C0C0),
        ("SkyBlue", 0x87CEEB),
        ("SlateBlue", 0x6A5ACD),
        ("SlateGray", 0x708090),
        ("SlateGrey", 0x708090),
        ("Snow", 0xFFFAFA),
        ("SpringGreen", 0x00FF7F),
        ("SteelBlue", 0x4682B4),
        ("Tan", 0xD2B48C),
        ("Teal", 0x008080),
        ("Thistle", 0xD8BFD8),
        ("Tomato", 0xFF6347),
        ("Turquoise", 0x40E0D0),
        ("Violet", 0xEE82EE),
        ("Wheat", 0xF5DEB3),
        ("White", 0xFFFFFF),
        ("WhiteSmoke", 0xF5F5F5),
        ("Yellow", 0xFFFF00),
        ("YellowGreen", 0x9ACD32),
    ];

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
        foreach (var (name, value) in NamedColors)
        {
            table[name.ToLowerInvariant()] = new Rgba32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
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
