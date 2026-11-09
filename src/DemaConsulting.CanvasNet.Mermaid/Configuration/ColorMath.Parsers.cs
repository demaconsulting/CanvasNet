// Portions derived from khroma (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     khroma's channel model and its four color parsers (hexadecimal, <c>rgb()</c>, <c>hsl()</c>, keyword).
/// </content>
internal static partial class ColorMath
{
    /// <summary>
    ///     khroma's keywords and their hexadecimal colors: the CSS named colors without <c>cyan</c>, <c>steelblue</c> and
    ///     <c>tomato</c>, with khroma's <c>cyanaqua</c> and <c>transparent</c>. The names are written in PascalCase and
    ///     looked up in lower case, as khroma lowers the text before the lookup.
    /// </summary>
    private static readonly FrozenDictionary<string, string> Keywords = new (string Name, string Hex)[]
    {
        ("AliceBlue", "#f0f8ff"), ("AntiqueWhite", "#faebd7"), ("Aqua", "#00ffff"), ("AquaMarine", "#7fffd4"),
        ("Azure", "#f0ffff"), ("Beige", "#f5f5dc"), ("Bisque", "#ffe4c4"), ("Black", "#000000"),
        ("BlanchedAlmond", "#ffebcd"), ("Blue", "#0000ff"), ("BlueViolet", "#8a2be2"), ("Brown", "#a52a2a"),
        ("BurlyWood", "#deb887"), ("CadetBlue", "#5f9ea0"), ("Chartreuse", "#7fff00"), ("Chocolate", "#d2691e"),
        ("Coral", "#ff7f50"), ("CornFlowerBlue", "#6495ed"), ("CornSilk", "#fff8dc"), ("Crimson", "#dc143c"),
        ("CyanAqua", "#00ffff"), ("DarkBlue", "#00008b"), ("DarkCyan", "#008b8b"), ("DarkGoldenRod", "#b8860b"),
        ("DarkGray", "#a9a9a9"), ("DarkGreen", "#006400"), ("DarkGrey", "#a9a9a9"), ("DarkKhaki", "#bdb76b"),
        ("DarkMagenta", "#8b008b"), ("DarkOliveGreen", "#556b2f"), ("DarkOrange", "#ff8c00"), ("DarkOrchid", "#9932cc"),
        ("DarkRed", "#8b0000"), ("DarkSalmon", "#e9967a"), ("DarkSeaGreen", "#8fbc8f"), ("DarkSlateBlue", "#483d8b"),
        ("DarkSlateGray", "#2f4f4f"), ("DarkSlateGrey", "#2f4f4f"), ("DarkTurquoise", "#00ced1"), ("DarkViolet", "#9400d3"),
        ("DeepPink", "#ff1493"), ("DeepSkyBlue", "#00bfff"), ("DimGray", "#696969"), ("DimGrey", "#696969"),
        ("DodgerBlue", "#1e90ff"), ("FireBrick", "#b22222"), ("FloralWhite", "#fffaf0"), ("ForestGreen", "#228b22"),
        ("Fuchsia", "#ff00ff"), ("Gainsboro", "#dcdcdc"), ("GhostWhite", "#f8f8ff"), ("Gold", "#ffd700"),
        ("GoldenRod", "#daa520"), ("Gray", "#808080"), ("Green", "#008000"), ("GreenYellow", "#adff2f"),
        ("Grey", "#808080"), ("HoneyDew", "#f0fff0"), ("HotPink", "#ff69b4"), ("IndianRed", "#cd5c5c"),
        ("Indigo", "#4b0082"), ("Ivory", "#fffff0"), ("Khaki", "#f0e68c"), ("Lavender", "#e6e6fa"),
        ("LavenderBlush", "#fff0f5"), ("LawnGreen", "#7cfc00"), ("LemonChiffon", "#fffacd"), ("LightBlue", "#add8e6"),
        ("LightCoral", "#f08080"), ("LightCyan", "#e0ffff"), ("LightGoldenRodYellow", "#fafad2"), ("LightGray", "#d3d3d3"),
        ("LightGreen", "#90ee90"), ("LightGrey", "#d3d3d3"), ("LightPink", "#ffb6c1"), ("LightSalmon", "#ffa07a"),
        ("LightSeaGreen", "#20b2aa"), ("LightSkyBlue", "#87cefa"), ("LightSlateGray", "#778899"), ("LightSlateGrey", "#778899"),
        ("LightSteelBlue", "#b0c4de"), ("LightYellow", "#ffffe0"), ("Lime", "#00ff00"), ("LimeGreen", "#32cd32"),
        ("Linen", "#faf0e6"), ("Magenta", "#ff00ff"), ("Maroon", "#800000"), ("MediumAquaMarine", "#66cdaa"),
        ("MediumBlue", "#0000cd"), ("MediumOrchid", "#ba55d3"), ("MediumPurple", "#9370db"), ("MediumSeaGreen", "#3cb371"),
        ("MediumSlateBlue", "#7b68ee"), ("MediumSpringGreen", "#00fa9a"), ("MediumTurquoise", "#48d1cc"), ("MediumVioletRed", "#c71585"),
        ("MidnightBlue", "#191970"), ("MintCream", "#f5fffa"), ("MistyRose", "#ffe4e1"), ("Moccasin", "#ffe4b5"),
        ("NavajoWhite", "#ffdead"), ("Navy", "#000080"), ("OldLace", "#fdf5e6"), ("Olive", "#808000"),
        ("OliveDrab", "#6b8e23"), ("Orange", "#ffa500"), ("OrangeRed", "#ff4500"), ("Orchid", "#da70d6"),
        ("PaleGoldenRod", "#eee8aa"), ("PaleGreen", "#98fb98"), ("PaleTurquoise", "#afeeee"), ("PaleVioletRed", "#db7093"),
        ("PapayaWhip", "#ffefd5"), ("PeachPuff", "#ffdab9"), ("Peru", "#cd853f"), ("Pink", "#ffc0cb"),
        ("Plum", "#dda0dd"), ("PowderBlue", "#b0e0e6"), ("Purple", "#800080"), ("RebeccaPurple", "#663399"),
        ("Red", "#ff0000"), ("RosyBrown", "#bc8f8f"), ("RoyalBlue", "#4169e1"), ("SaddleBrown", "#8b4513"),
        ("Salmon", "#fa8072"), ("SandyBrown", "#f4a460"), ("SeaGreen", "#2e8b57"), ("SeaShell", "#fff5ee"),
        ("Sienna", "#a0522d"), ("Silver", "#c0c0c0"), ("SkyBlue", "#87ceeb"), ("SlateBlue", "#6a5acd"),
        ("SlateGray", "#708090"), ("SlateGrey", "#708090"), ("Snow", "#fffafa"), ("SpringGreen", "#00ff7f"),
        ("Tan", "#d2b48c"), ("Teal", "#008080"), ("Thistle", "#d8bfd8"), ("Transparent", "#00000000"),
        ("Turquoise", "#40e0d0"), ("Violet", "#ee82ee"), ("Wheat", "#f5deb3"), ("White", "#ffffff"),
        ("WhiteSmoke", "#f5f5f5"), ("Yellow", "#ffff00"), ("YellowGreen", "#9acd32"),
    }.ToFrozenDictionary(pair => pair.Name.ToLowerInvariant(), pair => pair.Hex, StringComparer.Ordinal);

    /// <summary>
    ///     A parsed color: khroma's <c>Channels</c>, with the RGB and HSL channels computed lazily from each other.
    /// </summary>
    /// <remarks>
    ///     A reading of a channel family the color was not changed through is computed from the other family, and the
    ///     first change fixes the family (changing the other one afterwards throws, as in khroma). Which family is stored
    ///     decides how <see cref="Stringify"/> writes the color.
    /// </remarks>
    internal sealed class Channels
    {
        /// <summary>The red channel, 0 to 255, or null when not known yet.</summary>
        private double? _r;

        /// <summary>The green channel, 0 to 255, or null when not known yet.</summary>
        private double? _g;

        /// <summary>The blue channel, 0 to 255, or null when not known yet.</summary>
        private double? _b;

        /// <summary>The hue in degrees, or null when not known yet.</summary>
        private double? _h;

        /// <summary>The saturation, 0 to 100, or null when not known yet.</summary>
        private double? _s;

        /// <summary>The lightness, 0 to 100, or null when not known yet.</summary>
        private double? _l;

        /// <summary>The alpha channel, 0 to 1.</summary>
        private double _a;

        /// <summary>
        ///     Initializes a color.
        /// </summary>
        /// <param name="color">The text it was parsed from, or null.</param>
        private Channels(string? color)
        {
            Color = color;
        }

        /// <summary>Gets the text the color was parsed from, or null.</summary>
        public string? Color { get; }

        /// <summary>Gets a value indicating whether a channel was set since parsing.</summary>
        public bool Changed { get; private set; }

        /// <summary>Gets the family of channels the color was changed through.</summary>
        public ChannelKind Kind { get; private set; }

        /// <summary>Gets a value indicating whether the red channel is stored (khroma's <c>data.r !== undefined</c>).</summary>
        public bool HasStoredRed => _r.HasValue;

        /// <summary>Gets or sets the red channel.</summary>
        public double R
        {
            get => Kind != ChannelKind.Hsl && _r.HasValue ? _r.Value : FromHsl('r');
            set => SetChannel(ref _r, value, ChannelKind.Rgb);
        }

        /// <summary>Gets or sets the green channel.</summary>
        public double G
        {
            get => Kind != ChannelKind.Hsl && _g.HasValue ? _g.Value : FromHsl('g');
            set => SetChannel(ref _g, value, ChannelKind.Rgb);
        }

        /// <summary>Gets or sets the blue channel.</summary>
        public double B
        {
            get => Kind != ChannelKind.Hsl && _b.HasValue ? _b.Value : FromHsl('b');
            set => SetChannel(ref _b, value, ChannelKind.Rgb);
        }

        /// <summary>Gets or sets the hue.</summary>
        public double H
        {
            get => Kind != ChannelKind.Rgb && _h.HasValue ? _h.Value : FromRgb('h');
            set => SetChannel(ref _h, value, ChannelKind.Hsl);
        }

        /// <summary>Gets or sets the saturation.</summary>
        public double S
        {
            get => Kind != ChannelKind.Rgb && _s.HasValue ? _s.Value : FromRgb('s');
            set => SetChannel(ref _s, value, ChannelKind.Hsl);
        }

        /// <summary>Gets or sets the lightness.</summary>
        public double L
        {
            get => Kind != ChannelKind.Rgb && _l.HasValue ? _l.Value : FromRgb('l');
            set => SetChannel(ref _l, value, ChannelKind.Hsl);
        }

        /// <summary>Gets or sets the alpha channel; setting it does not fix the family.</summary>
        public double A
        {
            get => _a;
            set
            {
                Changed = true;
                _a = value;
            }
        }

        /// <summary>
        ///     Creates a color from RGB channels.
        /// </summary>
        /// <param name="r">The red channel.</param>
        /// <param name="g">The green channel.</param>
        /// <param name="b">The blue channel.</param>
        /// <param name="a">The alpha channel.</param>
        /// <param name="color">The text it was parsed from, or null.</param>
        /// <returns>The color.</returns>
        public static Channels FromRgb(double r, double g, double b, double a, string? color) =>
            new(color) { _r = r, _g = g, _b = b, _a = a };

        /// <summary>
        ///     Creates a color from HSL channels.
        /// </summary>
        /// <param name="h">The hue.</param>
        /// <param name="s">The saturation.</param>
        /// <param name="l">The lightness.</param>
        /// <param name="a">The alpha channel.</param>
        /// <param name="color">The text it was parsed from.</param>
        /// <returns>The color.</returns>
        public static Channels FromHsl(double h, double s, double l, double a, string color) =>
            new(color) { _h = h, _s = s, _l = l, _a = a };

        /// <summary>
        ///     Gets a channel by its khroma name.
        /// </summary>
        /// <param name="channel">One of <c>r g b h s l a</c>.</param>
        /// <returns>The channel value.</returns>
        public double Get(char channel) => channel switch
        {
            'r' => R,
            'g' => G,
            'b' => B,
            'h' => H,
            's' => S,
            'l' => L,
            _ => A,
        };

        /// <summary>
        ///     Sets a channel by its khroma name.
        /// </summary>
        /// <param name="channel">One of <c>r g b h s l a</c>.</param>
        /// <param name="value">The value.</param>
        public void Set(char channel, double value)
        {
            switch (channel)
            {
                case 'r':
                    R = value;
                    break;
                case 'g':
                    G = value;
                    break;
                case 'b':
                    B = value;
                    break;
                case 'h':
                    H = value;
                    break;
                case 's':
                    S = value;
                    break;
                case 'l':
                    L = value;
                    break;
                default:
                    A = value;
                    break;
            }
        }

        /// <summary>
        ///     Sets a channel of a family, which fixes the family of the color (khroma's <c>Type.set</c>).
        /// </summary>
        /// <param name="field">The channel field.</param>
        /// <param name="value">The value.</param>
        /// <param name="kind">The family of the channel.</param>
        /// <exception cref="InvalidOperationException">Thrown when the other family was changed already.</exception>
        private void SetChannel(ref double? field, double value, ChannelKind kind)
        {
            if (Kind != ChannelKind.All && Kind != kind)
            {
                throw new InvalidOperationException("Cannot change both RGB and HSL channels at the same time");
            }

            Kind = kind;
            Changed = true;
            field = value;
        }

        /// <summary>
        ///     Computes an RGB channel from the HSL channels (khroma's getter path through <c>_ensureHSL</c>).
        /// </summary>
        /// <param name="channel">The RGB channel.</param>
        /// <returns>The value.</returns>
        private double FromHsl(char channel)
        {
            _h ??= RgbToHsl(_r!.Value, _g!.Value, _b!.Value, 'h');
            _s ??= RgbToHsl(_r!.Value, _g!.Value, _b!.Value, 's');
            _l ??= RgbToHsl(_r!.Value, _g!.Value, _b!.Value, 'l');
            return HslToRgb(_h.Value, _s.Value, _l.Value, channel);
        }

        /// <summary>
        ///     Computes an HSL channel from the RGB channels (khroma's getter path through <c>_ensureRGB</c>).
        /// </summary>
        /// <param name="channel">The HSL channel.</param>
        /// <returns>The value.</returns>
        private double FromRgb(char channel)
        {
            _r ??= HslToRgb(_h!.Value, _s!.Value, _l!.Value, 'r');
            _g ??= HslToRgb(_h!.Value, _s!.Value, _l!.Value, 'g');
            _b ??= HslToRgb(_h!.Value, _s!.Value, _l!.Value, 'b');
            return RgbToHsl(_r.Value, _g.Value, _b.Value, channel);
        }
    }

    /// <summary>
    ///     khroma's <c>Hex.parse</c>: <c>#</c> and 3, 4, 6 or 8 hexadecimal digits (the alpha pair last).
    /// </summary>
    /// <param name="color">The text.</param>
    /// <param name="original">The text recorded as the written form (the keyword's hexadecimal text for a keyword).</param>
    /// <returns>The color, or null when the text is not in this format.</returns>
    private static Channels? ParseHex(string color, string? original = null)
    {
        if (color.Length == 0 || color[0] != '#')
        {
            return null;
        }

        var digits = color.AsSpan(1);
        if (digits.Length is not (3 or 4 or 6 or 8))
        {
            return null;
        }

        foreach (var digit in digits)
        {
            if (!char.IsAsciiHexDigit(digit))
            {
                return null;
            }
        }

        var dec = uint.Parse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var length = digits.Length;
        var hasAlpha = length % 4 == 0;
        var isFullLength = length > 4;
        var multiplier = isFullLength ? 1 : 17;
        var bits = isFullLength ? 8 : 4;
        var bitsOffset = hasAlpha ? 0 : -1;
        var mask = isFullLength ? 255u : 15u;
        double Part(int index) => ((dec >> (bits * (bitsOffset + index))) & mask) * multiplier;
        return Channels.FromRgb(
            Part(3),
            Part(2),
            Part(1),
            hasAlpha ? (dec & mask) * multiplier / 255.0 : 1,
            original ?? color);
    }

    /// <summary>
    ///     khroma's <c>RGB.parse</c>: <c>rgb()</c> or <c>rgba()</c> with numbers or percentages, as its expression
    ///     matches them.
    /// </summary>
    /// <param name="color">The text.</param>
    /// <returns>The color, or null when the text is not in this format.</returns>
    private static Channels? ParseRgb(string color)
    {
        if (color.Length == 0 || color[0] is not ('r' or 'R'))
        {
            return null;
        }

        var scanner = new ColorScanner(color);
        if (!scanner.Function("rgb") ||
            !scanner.Number(signedExponent: false, out var r) || !scanner.Percent(out var rPercent) || !scanner.Separator() ||
            !scanner.Number(signedExponent: false, out var g) || !scanner.Percent(out var gPercent) || !scanner.Separator() ||
            !scanner.Number(signedExponent: false, out var b) || !scanner.Percent(out var bPercent))
        {
            return null;
        }

        var alpha = scanner.Alpha(signedExponent: false);
        if (!scanner.Close())
        {
            return null;
        }

        static double Channel(double value, bool percent) => Clamp('r', percent ? value * 2.55 : value);
        return Channels.FromRgb(
            Channel(r, rPercent),
            Channel(g, gPercent),
            Channel(b, bPercent),
            AlphaOf(alpha),
            color);
    }

    /// <summary>
    ///     Gets the alpha channel of a parsed color function: the clamped number or percentage, or 1 when absent.
    /// </summary>
    /// <param name="alpha">The alpha component, or null.</param>
    /// <returns>The alpha channel.</returns>
    private static double AlphaOf((double Value, bool Percent)? alpha)
    {
        if (alpha is not { } a)
        {
            return 1;
        }

        return Clamp('a', a.Percent ? a.Value / 100 : a.Value);
    }

    /// <summary>
    ///     khroma's <c>HSL.parse</c>: <c>hsl()</c> or <c>hsla()</c> with a hue (optionally with a unit) and two
    ///     percentages, as its expression matches them.
    /// </summary>
    /// <param name="color">The text.</param>
    /// <returns>The color, or null when the text is not in this format.</returns>
    private static Channels? ParseHsl(string color)
    {
        if (color.Length == 0 || color[0] is not ('h' or 'H'))
        {
            return null;
        }

        var scanner = new ColorScanner(color);
        if (!scanner.Function("hsl") || !scanner.Number(signedExponent: true, out var hue))
        {
            return null;
        }

        var unit = scanner.HueUnit();
        if (!scanner.Separator() ||
            !scanner.Number(signedExponent: true, out var s) || !scanner.Percent(out var sPercent) || !sPercent || !scanner.Separator() ||
            !scanner.Number(signedExponent: true, out var l) || !scanner.Percent(out var lPercent) || !lPercent)
        {
            return null;
        }

        var alpha = scanner.Alpha(signedExponent: true);
        if (!scanner.Close())
        {
            return null;
        }

        // _hue2deg: only the lower-case unit names convert; any other unit is read as degrees
        var degrees = unit switch
        {
            "grad" => hue * .9,
            "rad" => hue * 180 / Math.PI,
            "turn" => hue * 360,
            _ => hue,
        };
        return Channels.FromHsl(
            Clamp('h', degrees),
            Clamp('s', s),
            Clamp('l', l),
            AlphaOf(alpha),
            color);
    }

    /// <summary>
    ///     khroma's <c>Keyword.parse</c>: a keyword in any letter case, read as its hexadecimal color (whose text is
    ///     what an unchanged result is written as).
    /// </summary>
    /// <param name="color">The text.</param>
    /// <returns>The color, or null when the text is not a keyword.</returns>
    private static Channels? ParseKeyword(string color) =>
        Keywords.TryGetValue(color.ToLowerInvariant(), out var hex) ? ParseHex(hex, hex) : null;

    /// <summary>
    ///     A scanner over the text of a color function with the token rules of khroma's expressions.
    /// </summary>
    /// <param name="text">The text.</param>
    private ref struct ColorScanner(string text)
    {
        /// <summary>The current position.</summary>
        private int _position;

        /// <summary>
        ///     Reads the function name (optionally followed by <c>a</c>), the opening parenthesis and the white space
        ///     after it, in any letter case.
        /// </summary>
        /// <param name="name">The name without the <c>a</c>.</param>
        /// <returns>Whether they were found.</returns>
        public bool Function(string name)
        {
            if (string.Compare(text, 0, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            _position = name.Length;
            if (_position < text.Length && text[_position] is 'a' or 'A')
            {
                _position++;
            }

            if (_position >= text.Length || text[_position] != '(')
            {
                return false;
            }

            _position++;
            SkipWhitespace();
            return true;
        }

        /// <summary>
        ///     Reads a number: <c>-?(\d+(\.\d+)?|\.\d+)</c> and an exponent (<c>e\d+</c>, or <c>e-?\d+</c> when signed).
        /// </summary>
        /// <param name="signedExponent">Whether the exponent may be negative.</param>
        /// <param name="value">The number, as <c>parseFloat</c> reads it.</param>
        /// <returns>Whether a number was found.</returns>
        public bool Number(bool signedExponent, out double value)
        {
            value = 0;
            var start = _position;
            var p = _position;
            if (p < text.Length && text[p] == '-')
            {
                p++;
            }

            var integerDigits = Digits(p);
            p += integerDigits;
            if (p + 1 < text.Length && text[p] == '.' && char.IsAsciiDigit(text[p + 1]))
            {
                p += 1 + Digits(p + 1);
            }
            else if (integerDigits == 0)
            {
                return false;
            }

            // The exponent is optional and only taken when complete
            if (p < text.Length && text[p] is 'e' or 'E')
            {
                var q = p + 1;
                if (signedExponent && q < text.Length && text[q] == '-')
                {
                    q++;
                }

                var exponentDigits = Digits(q);
                if (exponentDigits > 0)
                {
                    p = q + exponentDigits;
                }
            }

            value = double.Parse(text.AsSpan(start, p - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            _position = p;
            return true;
        }

        /// <summary>
        ///     Reads an optional percent sign.
        /// </summary>
        /// <param name="percent">Whether there was one.</param>
        /// <returns>Always true.</returns>
        public bool Percent(out bool percent)
        {
            percent = _position < text.Length && text[_position] == '%';
            if (percent)
            {
                _position++;
            }

            return true;
        }

        /// <summary>
        ///     Reads an optional hue unit (<c>deg</c>, <c>grad</c>, <c>rad</c>, <c>turn</c>, in any letter case).
        /// </summary>
        /// <returns>The unit as written, or null.</returns>
        public string? HueUnit()
        {
            foreach (var unit in (ReadOnlySpan<string>)["deg", "grad", "rad", "turn"])
            {
                if (_position + unit.Length <= text.Length &&
                    string.Compare(text, _position, unit, 0, unit.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var written = text.Substring(_position, unit.Length);
                    _position += unit.Length;
                    return written;
                }
            }

            return null;
        }

        /// <summary>
        ///     Reads a separator between two components: <c>\s*?(?:,|\s)\s*?</c>, white space with at most one comma.
        /// </summary>
        /// <returns>Whether a separator was found.</returns>
        public bool Separator()
        {
            var start = _position;
            SkipWhitespace();
            if (_position < text.Length && text[_position] == ',')
            {
                _position++;
                SkipWhitespace();
                return true;
            }

            return _position > start;
        }

        /// <summary>
        ///     Reads the optional alpha component: <c>\s*?(?:,|\/)\s*?\+?</c>, a number and an optional percent sign;
        ///     when it is not complete the position is restored, as the optional group of the expression backtracks.
        /// </summary>
        /// <param name="signedExponent">Whether the exponent may be negative.</param>
        /// <returns>The alpha and whether it is a percentage, or null.</returns>
        public (double Value, bool Percent)? Alpha(bool signedExponent)
        {
            var start = _position;
            SkipWhitespace();
            if (_position < text.Length && text[_position] is ',' or '/')
            {
                _position++;
                SkipWhitespace();
                if (_position < text.Length && text[_position] == '+')
                {
                    _position++;
                }

                if (Number(signedExponent, out var value) && Percent(out var percent))
                {
                    return (value, percent);
                }
            }

            _position = start;
            return null;
        }

        /// <summary>
        ///     Reads the closing parenthesis after optional white space, at the end of the text.
        /// </summary>
        /// <returns>Whether the text ends there.</returns>
        public bool Close()
        {
            SkipWhitespace();
            return _position == text.Length - 1 && text[_position] == ')';
        }

        /// <summary>
        ///     Counts the ASCII digits from a position.
        /// </summary>
        /// <param name="from">The position.</param>
        /// <returns>The number of digits.</returns>
        private readonly int Digits(int from)
        {
            var p = from;
            while (p < text.Length && char.IsAsciiDigit(text[p]))
            {
                p++;
            }

            return p - from;
        }

        /// <summary>
        ///     Skips JavaScript white space.
        /// </summary>
        private void SkipWhitespace()
        {
            while (_position < text.Length && ScanPatterns.IsJsWhitespace(text[_position]))
            {
                _position++;
            }
        }
    }
}
