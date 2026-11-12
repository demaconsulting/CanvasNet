// Portions derived from khroma (MIT); see third-party/README.md
using System.Globalization;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     A color value that khroma cannot read: it is not a string, or a string in none of its formats.
/// </summary>
/// <remarks>
///     Mermaid's themes derive their colors with khroma, which throws for such a value; the theme resolver catches this
///     exception to drop the theme variable that caused it.
/// </remarks>
internal sealed class ColorFormatException : FormatException
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ColorFormatException"/> class.
    /// </summary>
    /// <param name="value">The value that is not a color.</param>
    /// <param name="message">The message (khroma's own, for a string).</param>
    public ColorFormatException(object? value, string message)
        : base(message)
    {
        Value = value;
    }

    /// <summary>
    ///     Gets the value that is not a color.
    /// </summary>
    public object? Value { get; }
}

/// <summary>
///     The subset of the khroma color library that Mermaid's themes and styles use, with khroma's exact parsing,
///     channel conversions, clamping, rounding and output formats.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> Mermaid derives most theme colors with khroma (<c>adjust</c>, <c>darken</c>, <c>lighten</c>,
///         <c>invert</c>, <c>rgba</c>, <c>isDark</c>, <c>channel</c>), and the text of a derived color is what reaches
///         the style sheet, so the port keeps every detail that shows in the text: the parsers of the hexadecimal,
///         <c>rgb()</c>, <c>hsl()</c> and keyword forms (khroma's own keyword table), the lazy RGB/HSL channel model that
///         decides whether a result is written as <c>#rrggbb</c>, <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> or
///         <c>hsla()</c>, an unchanged color returned as it was written, the rounding to ten decimals, JavaScript's
///         number formatting, and the arithmetic quirks (a missing amount gives <c>NaN</c>, a hue is reduced with the
///         sign-preserving remainder, the luminance threshold compares the unscaled channel).
///     </para>
///     <para>
///         The regular expressions of khroma's parsers are replaced by scanners with the same matches, so parsing is
///         linear in the length of the text. The class is stateless and safe for concurrent use: where khroma reuses one
///         channel object, every call here creates its own, which gives the same results for every call the themes make.
///     </para>
/// </remarks>
internal static partial class ColorMath
{
    /// <summary>The value of an amount that is missing in JavaScript (<c>undefined</c> in arithmetic gives <c>NaN</c>).</summary>
    public const double Missing = double.NaN;

    /// <summary>
    ///     Which family of channels a color has been changed through (khroma's <c>TYPE</c>).
    /// </summary>
    internal enum ChannelKind
    {
        /// <summary>Not changed yet: both families can be read.</summary>
        All = 0,

        /// <summary>Changed through red, green or blue.</summary>
        Rgb = 1,

        /// <summary>Changed through hue, saturation or lightness.</summary>
        Hsl = 2,
    }

    /// <summary>
    ///     Parses a color as khroma's <c>Color.parse</c> does: hexadecimal, then <c>rgb()</c>, then <c>hsl()</c>, then a
    ///     keyword.
    /// </summary>
    /// <param name="color">The color text.</param>
    /// <returns>The channels.</returns>
    /// <exception cref="ColorFormatException">Thrown when the text is in none of the formats.</exception>
    public static Channels Parse(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        return ParseHex(color) ?? ParseRgb(color) ?? ParseHsl(color) ?? ParseKeyword(color)
            ?? throw new ColorFormatException(color, $"Unsupported color format: \"{color}\"");
    }

    /// <summary>
    ///     Gets whether a text is a color khroma can read.
    /// </summary>
    /// <param name="color">The text.</param>
    /// <returns>Whether it parses.</returns>
    public static bool IsColor(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        return ParseHex(color) is not null || ParseRgb(color) is not null || ParseHsl(color) is not null || ParseKeyword(color) is not null;
    }

    /// <summary>
    ///     Writes a color as khroma's <c>Color.stringify</c> does: unchanged colors as they were written, HSL-changed
    ///     colors as <c>hsl()</c>/<c>hsla()</c>, colors with alpha or fractional channels as <c>rgb()</c>/<c>rgba()</c>,
    ///     and the rest as <c>#rrggbb</c>.
    /// </summary>
    /// <param name="channels">The channels.</param>
    /// <returns>The text.</returns>
    public static string Stringify(Channels channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        if (!channels.Changed && !string.IsNullOrEmpty(channels.Color))
        {
            return channels.Color;
        }

        if (channels.Kind == ChannelKind.Hsl || !channels.HasStoredRed)
        {
            return StringifyHsl(channels);
        }

        if (channels.A < 1 || !IsInteger(channels.R) || !IsInteger(channels.G) || !IsInteger(channels.B))
        {
            return StringifyRgb(channels);
        }

        return "#" + ToHex(channels.R) + ToHex(channels.G) + ToHex(channels.B);
    }

    /// <summary>
    ///     khroma's <c>adjust</c>: adds amounts to channels (zero amounts are skipped) and writes the result.
    /// </summary>
    /// <param name="color">The color: a string (or parsed channels).</param>
    /// <param name="h">The hue change.</param>
    /// <param name="s">The saturation change.</param>
    /// <param name="l">The lightness change.</param>
    /// <param name="r">The red change.</param>
    /// <param name="g">The green change.</param>
    /// <param name="b">The blue change.</param>
    /// <returns>The adjusted color.</returns>
    /// <exception cref="ColorFormatException">Thrown when the color cannot be read.</exception>
    /// <exception cref="InvalidOperationException">Thrown when both RGB and HSL channels are changed.</exception>
    public static string Adjust(object? color, double h = 0, double s = 0, double l = 0, double r = 0, double g = 0, double b = 0)
    {
        // The changes are computed from one reading of the color, then applied to a fresh reading (khroma's change)
        var parsed = ParseValue(color);
        var changes = new List<(char Channel, double Value)>();
        foreach (var (channel, amount) in new[] { ('h', h), ('s', s), ('l', l), ('r', r), ('g', g), ('b', b) })
        {
            // A zero or NaN amount is falsy in JavaScript and skipped
            if (IsTruthy(amount))
            {
                changes.Add((channel, parsed.Get(channel) + amount));
            }
        }

        var target = ParseValue(color);
        foreach (var (channel, value) in changes)
        {
            target.Set(channel, Clamp(channel, value));
        }

        return Stringify(target);
    }

    /// <summary>
    ///     khroma's <c>darken</c>: lowers the lightness.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="amount">The amount; <see cref="Missing"/> gives khroma's <c>NaN</c> lightness.</param>
    /// <returns>The darker color.</returns>
    public static string Darken(object? color, double amount = Missing) => AdjustChannel(color, 'l', -amount);

    /// <summary>
    ///     khroma's <c>lighten</c>: raises the lightness.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="amount">The amount; <see cref="Missing"/> gives khroma's <c>NaN</c> lightness.</param>
    /// <returns>The lighter color.</returns>
    public static string Lighten(object? color, double amount = Missing) => AdjustChannel(color, 'l', amount);

    /// <summary>
    ///     khroma's <c>invert</c> with its default weight of 100: the RGB complement, mixed with the original color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The inverted color.</returns>
    public static string Invert(object? color)
    {
        var inverse = ParseValue(color);
        inverse.R = 255 - inverse.R;
        inverse.G = 255 - inverse.G;
        inverse.B = 255 - inverse.B;
        return Mix(inverse, color, 100);
    }

    /// <summary>
    ///     khroma's <c>mix</c> (dart-sass's weighted mix).
    /// </summary>
    /// <param name="color1">The first color (a string or parsed channels).</param>
    /// <param name="color2">The second color.</param>
    /// <param name="weight">The weight of the first color in percent.</param>
    /// <returns>The mixed color.</returns>
    public static string Mix(object? color1, object? color2, double weight = 50)
    {
        var first = ParseValue(color1);
        var (r1, g1, b1, a1) = (first.R, first.G, first.B, first.A);
        var second = ParseValue(color2);
        var (r2, g2, b2, a2) = (second.R, second.G, second.B, second.A);
        var weightScale = weight / 100;
        var weightNormalized = (weightScale * 2) - 1;
        var alphaDelta = a1 - a2;
        var weight1Combined = Same(weightNormalized * alphaDelta, -1)
            ? weightNormalized
            : (weightNormalized + alphaDelta) / (1 + (weightNormalized * alphaDelta));
        var weight1 = (weight1Combined + 1) / 2;
        var weight2 = 1 - weight1;
        var r = (r1 * weight1) + (r2 * weight2);
        var g = (g1 * weight1) + (g2 * weight2);
        var b = (b1 * weight1) + (b2 * weight2);
        var a = (a1 * weightScale) + (a2 * (1 - weightScale));
        return Rgba(r, g, b, a);
    }

    /// <summary>
    ///     khroma's <c>rgba(r, g, b, a)</c>: a color from clamped channel numbers.
    /// </summary>
    /// <param name="r">The red channel.</param>
    /// <param name="g">The green channel.</param>
    /// <param name="b">The blue channel.</param>
    /// <param name="a">The alpha channel.</param>
    /// <returns>The color text.</returns>
    public static string Rgba(double r, double g, double b, double a = 1) =>
        Stringify(Channels.FromRgb(Clamp('r', r), Clamp('g', g), Clamp('b', b), Clamp('a', a), null));

    /// <summary>
    ///     khroma's <c>rgba(color, alpha)</c>: a color with another alpha channel.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="alpha">The alpha channel.</param>
    /// <returns>The color text.</returns>
    public static string Rgba(object? color, double alpha)
    {
        var channels = ParseValue(color);
        channels.A = Clamp('a', alpha);
        return Stringify(channels);
    }

    /// <summary>
    ///     khroma's <c>channel</c>: one channel of a color, rounded to ten decimals.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="channel">One of <c>r g b h s l a</c>.</param>
    /// <returns>The channel value.</returns>
    public static double Channel(object? color, char channel) => Round(ParseValue(color).Get(channel));

    /// <summary>
    ///     khroma's <c>luminance</c>, rounded to ten decimals (with khroma's threshold on the unscaled channel).
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The relative luminance.</returns>
    public static double Luminance(object? color)
    {
        var channels = ParseValue(color);
        var value = (.2126 * ToLinear(channels.R)) + (.7152 * ToLinear(channels.G)) + (.0722 * ToLinear(channels.B));
        return Round(value);
    }

    /// <summary>
    ///     khroma's <c>isDark</c>: the luminance is not at least one half.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>Whether the color is dark.</returns>
    public static bool IsDark(object? color) => !(Luminance(color) >= .5);

    /// <summary>
    ///     Rounds to ten decimals as khroma's <c>lang.round</c>.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The rounded number.</returns>
    public static double Round(double number) => JsRound(number * 10000000000) / 10000000000;

    /// <summary>
    ///     JavaScript's <c>Math.round</c>: the nearest integer, halves toward positive infinity.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The rounded number.</returns>
    public static double JsRound(double number)
    {
        if (!double.IsFinite(number))
        {
            return number;
        }

        // The difference from the floor is exact, so halves are recognized without the error of floor(x + 0.5)
        var floor = Math.Floor(number);
        return number - floor >= 0.5 ? floor + 1 : floor;
    }

    /// <summary>
    ///     Formats a number as JavaScript's <c>String(number)</c> does: the shortest round-trip digits, the exponent form
    ///     below 1e-6 and from 1e21, <c>NaN</c>, <c>Infinity</c>, and <c>0</c> for negative zero.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The text.</returns>
    public static string FormatNumber(double number)
    {
        if (double.IsNaN(number))
        {
            return "NaN";
        }

        if (double.IsInfinity(number))
        {
            return number > 0 ? "Infinity" : "-Infinity";
        }

        if (Same(number, 0))
        {
            return "0";
        }

        // The shortest round-trip digits and the decimal exponent, laid out as ECMAScript's Number::toString does
        var sign = number < 0 ? "-" : string.Empty;
        var (digits, exponent) = Decompose(Math.Abs(number).ToString("R", CultureInfo.InvariantCulture));
        var k = digits.Length;
        var n = exponent + 1;
        string body;
        if (k <= n && n <= 21)
        {
            body = digits + new string('0', n - k);
        }
        else if (n > 0 && n <= 21)
        {
            body = digits[..n] + "." + digits[n..];
        }
        else if (n > -6 && n <= 0)
        {
            body = "0." + new string('0', -n) + digits;
        }
        else
        {
            var mantissa = k == 1 ? digits : digits[..1] + "." + digits[1..];
            var e = n - 1;
            body = mantissa + "e" + (e < 0 ? "-" : "+") + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
        }

        return sign + body;
    }

    /// <summary>
    ///     Gets whether two numbers are equal as JavaScript's <c>===</c> compares them (NaN is never equal).
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>Whether they are equal.</returns>
    internal static bool Same(double a, double b)
    {
        // Bit patterns: equal bits are equal numbers, and both zeros are equal; NaN is never equal
        const long MagnitudeMask = long.MaxValue;
        if (double.IsNaN(a) || double.IsNaN(b))
        {
            return false;
        }

        var bitsA = BitConverter.DoubleToInt64Bits(a);
        var bitsB = BitConverter.DoubleToInt64Bits(b);
        return bitsA == bitsB || ((bitsA & MagnitudeMask) == 0 && (bitsB & MagnitudeMask) == 0);
    }

    /// <summary>
    ///     Gets whether a number is truthy in JavaScript (not zero and not NaN).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>Whether it is truthy.</returns>
    internal static bool IsTruthy(double value) => !double.IsNaN(value) && !Same(value, 0);

    /// <summary>
    ///     Splits a round-trip number text into its significant digits (no leading or trailing zeros) and the decimal
    ///     exponent of the first digit.
    /// </summary>
    /// <param name="text">The text of a positive number, plain or in exponent form.</param>
    /// <returns>The digits and the exponent.</returns>
    private static (string Digits, int Exponent) Decompose(string text)
    {
        var exponent = 0;
        var e = text.IndexOfAny(['E', 'e']);
        if (e >= 0)
        {
            exponent = int.Parse(text[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            text = text[..e];
        }

        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var integerPart = dot >= 0 ? text[..dot] : text;
        var all = dot >= 0 ? integerPart + text[(dot + 1)..] : text;
        var leading = 0;
        while (leading < all.Length - 1 && all[leading] == '0')
        {
            leading++;
        }

        var digits = all[leading..].TrimEnd('0');
        if (digits.Length == 0)
        {
            digits = "0";
        }

        // The first significant digit is integerPart.Length - 1 - leading places left of the point
        return (digits, exponent + integerPart.Length - 1 - leading);
    }

    /// <summary>
    ///     khroma's <c>adjustChannel</c>: changes one channel by an amount, clamped; an unchanged channel keeps the color
    ///     as it was written.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="amount">The amount.</param>
    /// <returns>The color text.</returns>
    private static string AdjustChannel(object? color, char channel, double amount)
    {
        var channels = ParseValue(color);
        var current = channels.Get(channel);
        var next = Clamp(channel, current + amount);

        // JavaScript's !== is true for NaN, so a NaN result is always stored
        if (!Same(current, next))
        {
            channels.Set(channel, next);
        }

        return Stringify(channels);
    }

    /// <summary>
    ///     Parses a color value: a string is parsed, parsed channels are used as they are, anything else cannot be read.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The channels.</returns>
    /// <exception cref="ColorFormatException">Thrown when the value is not a color.</exception>
    private static Channels ParseValue(object? value) => value switch
    {
        string text => Parse(text),
        Channels channels => channels,
        _ => throw new ColorFormatException(value, "Unsupported color value: it is not a string"),
    };

    /// <summary>
    ///     Clamps a channel as khroma's <c>channel.clamp</c>: RGB to 0 to 255, saturation and lightness to 0 to 100,
    ///     alpha to 0 to 1, and the hue reduced with the sign-preserving remainder of 360; NaN stays NaN.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="value">The value.</param>
    /// <returns>The clamped value.</returns>
    private static double Clamp(char channel, double value) => channel switch
    {
        'r' or 'g' or 'b' => Limit(value, 255),
        'h' => value % 360,
        's' or 'l' => Limit(value, 100),
        _ => Limit(value, 1),
    };

    /// <summary>
    ///     Limits a value to 0 and a maximum as khroma does (NaN passes through).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="max">The maximum.</param>
    /// <returns>The limited value.</returns>
    private static double Limit(double value, double max)
    {
        if (value >= max)
        {
            return max;
        }

        return value < 0 ? 0 : value;
    }

    /// <summary>
    ///     khroma's <c>channel.hsl2rgb</c>.
    /// </summary>
    /// <param name="h">The hue.</param>
    /// <param name="s">The saturation.</param>
    /// <param name="l">The lightness.</param>
    /// <param name="channel">The RGB channel.</param>
    /// <returns>The channel value.</returns>
    private static double HslToRgb(double h, double s, double l, char channel)
    {
        // A zero or NaN saturation is falsy: achromatic
        if (!IsTruthy(s))
        {
            return l * 2.55;
        }

        h /= 360;
        s /= 100;
        l /= 100;
        var q = l < .5 ? l * (1 + s) : l + s - (l * s);
        var p = (2 * l) - q;
        return channel switch
        {
            'r' => HueToRgb(p, q, h + (1.0 / 3)) * 255,
            'g' => HueToRgb(p, q, h) * 255,
            _ => HueToRgb(p, q, h - (1.0 / 3)) * 255,
        };
    }

    /// <summary>
    ///     khroma's <c>channel.hue2rgb</c>.
    /// </summary>
    /// <param name="p">The p term.</param>
    /// <param name="q">The q term.</param>
    /// <param name="t">The hue term.</param>
    /// <returns>The channel fraction.</returns>
    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6)
        {
            return p + ((q - p) * 6 * t);
        }

        if (t < 1.0 / 2)
        {
            return q;
        }

        if (t < 2.0 / 3)
        {
            return p + ((q - p) * ((2.0 / 3) - t) * 6);
        }

        return p;
    }

    /// <summary>
    ///     khroma's <c>channel.rgb2hsl</c>.
    /// </summary>
    /// <param name="r">The red channel.</param>
    /// <param name="g">The green channel.</param>
    /// <param name="b">The blue channel.</param>
    /// <param name="channel">The HSL channel.</param>
    /// <returns>The channel value.</returns>
    private static double RgbToHsl(double r, double g, double b, char channel)
    {
        r /= 255;
        g /= 255;
        b /= 255;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (channel == 'l')
        {
            return l * 100;
        }

        if (Same(max, min))
        {
            return 0;
        }

        var d = max - min;
        var s = l > .5 ? d / (2 - max - min) : d / (max + min);
        if (channel == 's')
        {
            return s * 100;
        }

        // The first channel equal to the maximum wins, as in khroma's switch
        if (Same(max, r))
        {
            return (((g - b) / d) + (g < b ? 6 : 0)) * 60;
        }

        if (Same(max, g))
        {
            return (((b - r) / d) + 2) * 60;
        }

        return Same(max, b) ? (((r - g) / d) + 4) * 60 : -1;
    }

    /// <summary>
    ///     khroma's <c>channel.toLinear</c>, with its threshold compared to the unscaled channel.
    /// </summary>
    /// <param name="c">The channel, 0 to 255.</param>
    /// <returns>The linear value.</returns>
    private static double ToLinear(double c)
    {
        var n = c / 255;
        return c > .03928 ? Math.Pow((n + .055) / 1.055, 2.4) : n / 12.92;
    }

    /// <summary>
    ///     JavaScript's <c>Number.isInteger</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether it is a finite integer.</returns>
    private static bool IsInteger(double value) => double.IsFinite(value) && Same(Math.Floor(value), value);

    /// <summary>
    ///     khroma's <c>HSL.stringify</c>.
    /// </summary>
    /// <param name="channels">The channels.</param>
    /// <returns>The text.</returns>
    private static string StringifyHsl(Channels channels)
    {
        var (h, s, l, a) = (channels.H, channels.S, channels.L, channels.A);
        return a < 1
            ? $"hsla({FormatNumber(Round(h))}, {FormatNumber(Round(s))}%, {FormatNumber(Round(l))}%, {FormatNumber(a)})"
            : $"hsl({FormatNumber(Round(h))}, {FormatNumber(Round(s))}%, {FormatNumber(Round(l))}%)";
    }

    /// <summary>
    ///     khroma's <c>RGB.stringify</c>.
    /// </summary>
    /// <param name="channels">The channels.</param>
    /// <returns>The text.</returns>
    private static string StringifyRgb(Channels channels)
    {
        var (r, g, b, a) = (channels.R, channels.G, channels.B, channels.A);
        return a < 1
            ? $"rgba({FormatNumber(Round(r))}, {FormatNumber(Round(g))}, {FormatNumber(Round(b))}, {FormatNumber(Round(a))})"
            : $"rgb({FormatNumber(Round(r))}, {FormatNumber(Round(g))}, {FormatNumber(Round(b))})";
    }

    /// <summary>
    ///     khroma's <c>DEC2HEX</c> of a rounded channel: two lower-case digits.
    /// </summary>
    /// <param name="value">The channel, 0 to 255.</param>
    /// <returns>The digits.</returns>
    private static string ToHex(double value) =>
        ((int)JsRound(value)).ToString("x2", CultureInfo.InvariantCulture);
}
