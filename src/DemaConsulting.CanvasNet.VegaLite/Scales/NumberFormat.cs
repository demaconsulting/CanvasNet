// Portions derived from d3-format (ISC, Mike Bostock); see third-party/README.md
// Portions derived from d3-scale (ISC, Mike Bostock); see third-party/README.md
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     A parsed d3 number format specifier: <c>[[fill]align][sign][symbol][0][width][,][.precision][~][type]</c>.
/// </summary>
/// <remarks>
///     The defaults are those of d3-format: fill a space, align right, sign <c>-</c>, no symbol, no zero padding, no
///     width, no grouping, no precision, no trimming and no type. The record is immutable; use <c>with</c> to derive a
///     changed specifier.
/// </remarks>
internal sealed record FormatSpecifier
{
    /// <summary>Gets the fill character used for padding (one UTF-16 character).</summary>
    public string Fill { get; init; } = " ";

    /// <summary>Gets the alignment: <c>&lt;</c>, <c>&gt;</c>, <c>^</c> (centered) or <c>=</c> (padding after the sign).</summary>
    public string Align { get; init; } = ">";

    /// <summary>Gets the sign rule: <c>-</c> (only negative numbers), <c>+</c> (always), a space, or <c>(</c> (parentheses for negative numbers).</summary>
    public string Sign { get; init; } = "-";

    /// <summary>Gets the symbol: <c>$</c> (currency), <c>#</c> (radix prefix for b, o, x, X) or empty.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the number is padded with zeros after the sign.</summary>
    public bool Zero { get; init; }

    /// <summary>Gets the minimum width in characters, or null.</summary>
    public int? Width { get; init; }

    /// <summary>Gets a value indicating whether the integer part is grouped with thousands separators.</summary>
    public bool Comma { get; init; }

    /// <summary>Gets the precision (digits after the decimal point or significant digits depending on the type), or null.</summary>
    public int? Precision { get; init; }

    /// <summary>Gets a value indicating whether insignificant trailing zeros are trimmed.</summary>
    public bool Trim { get; init; }

    /// <summary>Gets the type character, or empty.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    ///     Writes the specifier in d3's canonical text form.
    /// </summary>
    /// <returns>The text: fill, align, sign, symbol, 0, width, comma, precision, trim and type.</returns>
    public override string ToString() =>
        string.Concat(
            Fill,
            Align,
            Sign,
            Symbol,
            Zero ? "0" : string.Empty,
            Width is { } w ? Math.Max(1, w).ToString(CultureInfo.InvariantCulture) : string.Empty,
            Comma ? "," : string.Empty,
            Precision is { } p ? "." + Math.Max(0, p).ToString(CultureInfo.InvariantCulture) : string.Empty,
            Trim ? "~" : string.Empty,
            Type);
}

/// <summary>
///     A number formatter built from a d3 format specifier, with the default (en-US) locale of d3-format.
/// </summary>
/// <remarks>
///     <para>
///         This is a C# version of <c>format</c>, <c>formatPrefix</c>, <c>formatSpecifier</c>,
///         <c>precisionFixed</c>, <c>precisionPrefix</c> and <c>precisionRound</c> of d3-format 3.1.2 and of
///         <c>tickFormat</c> of d3-scale 4, for the numeric subset that axis labels need. The specifier grammar is
///         d3's. The types are <c>e f g r s % p b o d x X c n</c> and the empty type (an alias of <c>.12~g</c>, as is
///         every unknown type); the flags are <c>, ~ + - ( space $ # 0</c>, a fill and alignment, a width and a
///         precision. The default locale uses a period as the decimal point, a comma as the thousands separator with
///         groups of three, <c>$</c> as the currency prefix, the real minus sign U+2212 for negative numbers and the
///         SI prefixes <c>y z a f p n µ m k M G T P E Z Y</c>.
///     </para>
///     <para>
///         The digits come out exactly as JavaScript produces them. JavaScript's <c>toFixed</c>, <c>toExponential</c>
///         and <c>toPrecision</c> round the exact decimal value of the binary double half up (so 1.005 with two
///         decimals is 1.00, because the double is just below 1.005, and 0.5 with no decimals is 1, 2.5 is 3); the
///         digits are computed from the exact expansion of the double, not from a floating point multiplication. The
///         shortest-digits parts use the round-trip format of .NET. Negative zero is formatted like zero, and a
///         negative number that rounds to zero loses its sign. A width above 1,000 is limited to 1,000 characters.
///     </para>
///     <para>
///         Instances are immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed partial class NumberFormat
{
    /// <summary>The largest width that is honored, so a hostile specifier cannot allocate unbounded padding.</summary>
    private const int MaxWidth = 1_000;

    /// <summary>The minus sign of the default locale (U+2212).</summary>
    private const string Minus = "\u2212";

    /// <summary>The SI prefixes from yocto (index 0) to yotta (index 16); the entry for 10^0 is empty.</summary>
    private static readonly string[] Prefixes = ["y", "z", "a", "f", "p", "n", "\u00b5", "m", string.Empty, "k", "M", "G", "T", "P", "E", "Z", "Y"];

    /// <summary>The fill character.</summary>
    private readonly char _fill;

    /// <summary>The alignment.</summary>
    private readonly string _align;

    /// <summary>The sign rule.</summary>
    private readonly string _sign;

    /// <summary>Whether zero padding goes between the sign and the digits.</summary>
    private readonly bool _zero;

    /// <summary>The minimum width, or null.</summary>
    private readonly int? _width;

    /// <summary>Whether the integer part is grouped.</summary>
    private readonly bool _comma;

    /// <summary>The precision after defaulting and clamping.</summary>
    private readonly int _precision;

    /// <summary>Whether trailing zeros are trimmed.</summary>
    private readonly bool _trim;

    /// <summary>The effective type character.</summary>
    private readonly char _type;

    /// <summary>The prefix text (currency or radix prefix).</summary>
    private readonly string _prefix;

    /// <summary>The suffix text (percent sign and, for formatPrefix, the SI prefix).</summary>
    private readonly string _suffix;

    /// <summary>The factor applied to a value before it is formatted (the SI scaling of formatPrefix); 1 otherwise.</summary>
    private readonly double _valueScale;

    /// <summary>
    ///     Initializes a formatter.
    /// </summary>
    /// <param name="specifier">The parsed specifier.</param>
    /// <param name="extraSuffix">A suffix appended after the percent sign (the SI prefix of formatPrefix).</param>
    /// <param name="valueScale">The factor applied to every value before formatting.</param>
    private NumberFormat(FormatSpecifier specifier, string extraSuffix, double valueScale)
    {
        var type = specifier.Type;
        var comma = specifier.Comma;
        var trim = specifier.Trim;
        var precision = specifier.Precision;
        var fill = specifier.Fill;
        var align = specifier.Align;
        var zero = specifier.Zero;

        // The "n" type is an alias of ",g"; the empty type and every unknown type are an alias of ".12~g"
        if (type == "n")
        {
            comma = true;
            type = "g";
        }
        else if (type.Length != 1 || !IsKnownType(type[0]))
        {
            precision ??= 12;
            trim = true;
            type = "g";
        }

        // Zero padding goes after the sign and before the digits
        if (zero || (fill == "0" && align == "="))
        {
            zero = true;
            fill = "0";
            align = "=";
        }

        _type = type[0];
        _fill = fill[0];
        _align = align;
        _sign = specifier.Sign;
        _zero = zero;
        _comma = comma;
        _trim = trim;
        _width = specifier.Width is { } w ? Math.Min(w, MaxWidth) : null;
        _valueScale = valueScale;

        // The prefix and suffix: currency or radix prefix, and the percent sign
        var isCurrency = specifier.Symbol == "$";
        if (isCurrency)
        {
            _prefix = "$";
        }
        else if (specifier.Symbol == "#" && "boxX".Contains(_type, StringComparison.Ordinal))
        {
            _prefix = "0" + char.ToLowerInvariant(_type);
        }
        else
        {
            _prefix = string.Empty;
        }

        _suffix = (!isCurrency && _type is '%' or 'p' ? "%" : string.Empty) + extraSuffix;

        // The default precision is 6; significant precision is limited to 1..21, fixed precision to 0..20
        if (precision is not { } p)
        {
            _precision = 6;
        }
        else if (_type is 'g' or 'p' or 'r' or 's')
        {
            _precision = Math.Clamp(p, 1, 21);
        }
        else
        {
            _precision = Math.Clamp(p, 0, 20);
        }
    }

    /// <summary>
    ///     Parses a format specifier.
    /// </summary>
    /// <param name="specifier">The specifier text, for example <c>,.2f</c> or <c>~s</c>.</param>
    /// <returns>The parsed specifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="specifier"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the text is not a valid specifier.</exception>
    public static FormatSpecifier ParseSpecifier(string specifier)
    {
        ArgumentNullException.ThrowIfNull(specifier);
        return TryParseSpecifier(specifier, out var result)
            ? result
            : throw new FormatException("invalid format: " + specifier);
    }

    /// <summary>
    ///     Tries to parse a format specifier.
    /// </summary>
    /// <param name="specifier">The specifier text.</param>
    /// <param name="result">Receives the parsed specifier.</param>
    /// <returns><see langword="true"/> when the text is a valid specifier.</returns>
    public static bool TryParseSpecifier(string? specifier, out FormatSpecifier result)
    {
        result = new FormatSpecifier();
        if (specifier is null)
        {
            return false;
        }

        var match = SpecifierPattern().Match(specifier);
        if (!match.Success)
        {
            return false;
        }

        result = new FormatSpecifier
        {
            Fill = match.Groups[1].Success ? match.Groups[1].Value : " ",
            Align = match.Groups[2].Success ? match.Groups[2].Value : ">",
            Sign = match.Groups[3].Success ? match.Groups[3].Value : "-",
            Symbol = match.Groups[4].Success ? match.Groups[4].Value : string.Empty,
            Zero = match.Groups[5].Success,
            Width = match.Groups[6].Success ? ParseCount(match.Groups[6].Value) : null,
            Comma = match.Groups[7].Success,
            Precision = match.Groups[8].Success ? ParseCount(match.Groups[8].Value[1..]) : null,
            Trim = match.Groups[9].Success,
            Type = match.Groups[10].Success ? match.Groups[10].Value : string.Empty,
        };
        return true;
    }

    /// <summary>
    ///     Creates a formatter from a specifier text (d3's <c>format</c>).
    /// </summary>
    /// <param name="specifier">The specifier text.</param>
    /// <returns>The formatter.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="specifier"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when the text is not a valid specifier.</exception>
    public static NumberFormat Create(string specifier) => new(ParseSpecifier(specifier), string.Empty, 1);

    /// <summary>
    ///     Creates a formatter from a parsed specifier (d3's <c>format</c>).
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <returns>The formatter.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="specifier"/> is null.</exception>
    public static NumberFormat Create(FormatSpecifier specifier)
    {
        ArgumentNullException.ThrowIfNull(specifier);
        return new NumberFormat(specifier, string.Empty, 1);
    }

    /// <summary>
    ///     Creates a formatter that scales numbers by the SI prefix that suits a reference value (d3's
    ///     <c>formatPrefix</c>).
    /// </summary>
    /// <remarks>
    ///     The prefix is chosen from the magnitude of <paramref name="value"/> (from yocto, 10^-24, to yotta, 10^24);
    ///     every number is then divided by that power of ten and shown with the prefix as suffix and the specifier's
    ///     precision as the number of decimals. The specifier's type is replaced by <c>f</c>. A zero or non-finite
    ///     reference value has no prefix and formats every number as NaN, as d3 does.
    /// </remarks>
    /// <param name="specifier">The specifier text.</param>
    /// <param name="value">The reference value that decides the prefix.</param>
    /// <returns>The formatter.</returns>
    public static NumberFormat CreatePrefix(string specifier, double value) => CreatePrefix(ParseSpecifier(specifier), value);

    /// <summary>
    ///     Creates a formatter that scales numbers by the SI prefix that suits a reference value (d3's
    ///     <c>formatPrefix</c>).
    /// </summary>
    /// <param name="specifier">The specifier; its type is replaced by <c>f</c>.</param>
    /// <param name="value">The reference value that decides the prefix.</param>
    /// <returns>The formatter.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="specifier"/> is null.</exception>
    public static NumberFormat CreatePrefix(FormatSpecifier specifier, double value)
    {
        ArgumentNullException.ThrowIfNull(specifier);
        var e = Math.Max(-8, Math.Min(8, Math.Floor(Exponent(value) / 3))) * 3;
        var suffix = double.IsNaN(e) ? string.Empty : Prefixes[8 + (int)(e / 3)];
        return new NumberFormat(specifier with { Type = "f" }, suffix, Math.Pow(10, -e));
    }

    /// <summary>
    ///     Creates the formatter of the labels of a linear axis with the given domain and tick count (d3-scale's
    ///     <c>tickFormat</c>, as Vega uses it).
    /// </summary>
    /// <remarks>
    ///     The precision is chosen from the tick step so that every tick label has just enough digits to tell the ticks
    ///     apart: the default specifier is <c>,f</c> (thousands separators, fixed decimals); with a specifier of type
    ///     <c>s</c> an SI prefix suited to the largest end is used; with <c>%</c> the step is read as a percentage; types
    ///     <c>e</c>, <c>g</c>, <c>p</c>, <c>r</c> and the empty type get significant digits. A specifier that already
    ///     has a precision is used as it is.
    /// </remarks>
    /// <param name="start">The start of the domain.</param>
    /// <param name="stop">The end of the domain.</param>
    /// <param name="count">The tick count the axis uses.</param>
    /// <param name="specifier">The format specifier, or null for the default <c>,f</c>.</param>
    /// <returns>The formatter.</returns>
    /// <exception cref="FormatException">Thrown when the specifier is not valid.</exception>
    public static NumberFormat TickFormat(double start, double stop, double count, string? specifier = null)
    {
        var step = Ticks.TickStep(start, stop, count);
        var spec = ParseSpecifier(specifier ?? ",f");
        var largest = Math.Max(Math.Abs(start), Math.Abs(stop));

        // A specifier without precision gets the one that tells neighboring ticks apart
        switch (spec.Type)
        {
            case "s":
                spec = WithPrecision(spec, PrecisionPrefix(step, largest), 0);
                return CreatePrefix(spec, largest);
            case "" or "e" or "g" or "p" or "r":
                spec = WithPrecision(spec, PrecisionRound(step, largest), spec.Type == "e" ? 1 : 0);
                break;
            case "f" or "%":
                spec = WithPrecision(spec, PrecisionFixed(step), spec.Type == "%" ? 2 : 0);
                break;
            default:
                break;
        }

        return Create(spec);
    }

    /// <summary>
    ///     Gets the number of decimals that distinguishes numbers a given step apart (d3's <c>precisionFixed</c>).
    /// </summary>
    /// <param name="step">The step between the numbers.</param>
    /// <returns>The decimals for an <c>f</c> format; NaN when the step is zero or not a number.</returns>
    public static double PrecisionFixed(double step) => Math.Max(0.0, -Exponent(Math.Abs(step)));

    /// <summary>
    ///     Gets the number of decimals for an SI-prefix format of numbers a given step apart (d3's <c>precisionPrefix</c>).
    /// </summary>
    /// <param name="step">The step between the numbers.</param>
    /// <param name="value">The reference value whose SI prefix is used.</param>
    /// <returns>The decimals for an <c>s</c> format; NaN when the step or the value is zero or not a number.</returns>
    public static double PrecisionPrefix(double step, double value) =>
        Math.Max(0.0, (Math.Max(-8, Math.Min(8, Math.Floor(Exponent(value) / 3))) * 3) - Exponent(Math.Abs(step)));

    /// <summary>
    ///     Gets the number of significant digits that distinguishes numbers a given step apart up to a maximum (d3's
    ///     <c>precisionRound</c>).
    /// </summary>
    /// <param name="step">The step between the numbers.</param>
    /// <param name="max">The largest number to show.</param>
    /// <returns>The significant digits for a <c>g</c>, <c>e</c>, <c>p</c> or <c>r</c> format; NaN when an input is not a number.</returns>
    public static double PrecisionRound(double step, double max)
    {
        step = Math.Abs(step);
        max = Math.Abs(max) - step;
        return Math.Max(0.0, Exponent(max) - Exponent(step)) + 1;
    }

    /// <summary>
    ///     Formats a number.
    /// </summary>
    /// <remarks>
    ///     NaN is shown as <c>NaN</c> and the infinities as <c>Infinity</c> (or <c>-Infinity</c>), padded and signed
    ///     like any other number, as d3 does. The method has no side effects.
    /// </remarks>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public string Format(double value)
    {
        var prefix = _prefix;
        var suffix = _suffix;
        string body;
        value *= _valueScale;
        if (_type == 'c')
        {
            // The "c" type is the number's plain text as a suffix, so it is not signed or grouped
            suffix = JsNumber.ToString(value) + suffix;
            body = string.Empty;
        }
        else
        {
            // Format the magnitude; the sign is decided separately (negative zero counts as negative here)
            var negative = value < 0 || (JsNumber.IsZero(value) && double.IsNegative(value));
            int? prefixExponent = null;
            body = double.IsNaN(value) ? "NaN" : FormatMagnitude(Math.Abs(value), out prefixExponent);
            if (_trim)
            {
                body = TrimZeros(body);
            }

            // A negative number that rounds to zero is shown without a sign unless a plus sign is requested
            if (negative && JsNumber.TryParse(body, out var shown) && JsNumber.IsZero(shown) && _sign != "+")
            {
                negative = false;
            }

            string signPrefix;
            if (negative)
            {
                signPrefix = _sign == "(" ? "(" : Minus;
            }
            else
            {
                signPrefix = _sign is "-" or "(" ? string.Empty : _sign;
            }

            prefix = signPrefix + prefix;
            suffix = (_type == 's' && prefixExponent is { } pe ? Prefixes[8 + (pe / 3)] : string.Empty)
                + suffix
                + (negative && _sign == "(" ? ")" : string.Empty);

            // Move the fraction or exponent part out of the part that can be grouped
            if (_type is 'd' or 'e' or 'f' or 'g' or 'p' or 'r' or 's' or '%')
            {
                var split = body.AsSpan().IndexOfAnyExceptInRange('0', '9');
                if (split >= 0)
                {
                    var tail = body[split] == '.' ? "." + body[(split + 1)..] : body[split..];
                    suffix = tail + suffix;
                    body = body[..split];
                }
            }
        }

        // With a fill other than zero grouping comes before padding
        if (_comma && !_zero)
        {
            body = Group(body, int.MaxValue);
        }

        // Padding makes up the width
        var length = prefix.Length + body.Length + suffix.Length;
        var padding = _width is { } width && length < width ? new string(_fill, width - length) : string.Empty;

        // With zero fill the padding is grouped together with the digits
        if (_comma && _zero)
        {
            body = Group(padding + body, padding.Length > 0 ? _width!.Value - suffix.Length : int.MaxValue);
            padding = string.Empty;
        }

        // Put the parts together as aligned
        switch (_align)
        {
            case "<":
                return prefix + body + suffix + padding;
            case "=":
                return prefix + padding + body + suffix;
            case "^":
                var half = padding.Length >> 1;
                return padding[..half] + prefix + body + suffix + padding[half..];
            default:
                return padding + prefix + body + suffix;
        }
    }

    /// <summary>
    ///     Tells whether a character is one of the format types that d3 knows.
    /// </summary>
    /// <param name="type">The type character.</param>
    /// <returns><see langword="true"/> for <c>% b c d e f g o p r s X x</c>.</returns>
    private static bool IsKnownType(char type) =>
        type is '%' or 'b' or 'c' or 'd' or 'e' or 'f' or 'g' or 'o' or 'p' or 'r' or 's' or 'X' or 'x';

    /// <summary>
    ///     Gets the decimal exponent of a number's leading digit (d3's <c>exponent</c>).
    /// </summary>
    /// <param name="x">The number.</param>
    /// <returns>The exponent of the shortest scientific form of the magnitude; NaN for zero, NaN and infinities.</returns>
    private static double Exponent(double x) =>
        TryDecimalParts(Math.Abs(x), 0, out _, out var exponent) ? exponent : double.NaN;

    /// <summary>
    ///     Gives a specifier the precision a tick format derived, unless it has one or the derived one is not a number.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="precision">The derived precision; NaN when it could not be derived.</param>
    /// <param name="offset">The number subtracted from the derived precision (digits that the type adds itself).</param>
    /// <returns>The specifier with the precision set, or the specifier unchanged.</returns>
    private static FormatSpecifier WithPrecision(FormatSpecifier specifier, double precision, int offset) =>
        specifier.Precision is null && !double.IsNaN(precision)
            ? specifier with { Precision = (int)Math.Min(precision - offset, 1000) }
            : specifier;

    /// <summary>
    ///     Converts the digits of a count in a specifier to a number that is capped instead of overflowing.
    /// </summary>
    /// <param name="digits">The decimal digits.</param>
    /// <returns>The number, or <see cref="int.MaxValue"/> when it is too large.</returns>
    private static int ParseCount(string digits) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : int.MaxValue;

    /// <summary>
    ///     Formats the magnitude of a number as its type asks.
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <param name="prefixExponent">Receives the power of ten of the SI prefix for the <c>s</c> type; null otherwise.</param>
    /// <returns>The digits (with decimal point or exponent where the type has them).</returns>
    private string FormatMagnitude(double x, out int? prefixExponent)
    {
        prefixExponent = null;
        switch (_type)
        {
            case '%':
                return ToFixed(x * 100, _precision);
            case 'b':
                return ToRadix(x, 2, false);
            case 'd':
                return FormatDecimal(x);
            case 'e':
                return ToExponential(x, _precision);
            case 'f':
                return ToFixed(x, _precision);
            case 'g':
                return ToPrecision(x, _precision);
            case 'o':
                return ToRadix(x, 8, false);
            case 'p':
                return FormatRounded(x * 100, _precision);
            case 'r':
                return FormatRounded(x, _precision);
            case 's':
                return FormatPrefixAuto(x, _precision, out prefixExponent);
            case 'X':
                return ToRadix(x, 16, true);
            default:
                return ToRadix(x, 16, false);
        }
    }

    /// <summary>
    ///     Formats a number as an integer (the <c>d</c> type).
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <returns>The rounded integer without exponent; <c>∞</c> for infinity, as d3 shows it.</returns>
    private static string FormatDecimal(double x)
    {
        var rounded = JsNumber.Round(x);
        if (double.IsInfinity(rounded))
        {
            return "\u221e";
        }

        if (Math.Abs(rounded) < 1e21)
        {
            return JsNumber.ToString(rounded);
        }

        // Beyond 1e21 JavaScript's toString switches to an exponent, but d3 spells the whole integer
        JsNumber.ShortestDecimal(Math.Abs(rounded), out var digits, out var n);
        return digits + new string('0', Math.Max(0, n - digits.Length));
    }

    /// <summary>
    ///     Writes the rounded integer part of a number in a radix.
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <param name="radix">The radix.</param>
    /// <param name="upper">Whether the digits are upper case.</param>
    /// <returns>The digits; <c>Infinity</c> for infinity.</returns>
    private static string ToRadix(double x, int radix, bool upper)
    {
        var rounded = JsNumber.Round(x);
        if (double.IsInfinity(rounded))
        {
            return "Infinity";
        }

        var text = JsNumber.ToRadix(rounded, radix);
        return upper ? text.ToUpperInvariant() : text;
    }

    /// <summary>
    ///     Formats a number with a number of significant digits and no exponent where one would not be needed (d3's
    ///     <c>formatRounded</c>).
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <param name="p">The significant digits.</param>
    /// <returns>The text.</returns>
    private static string FormatRounded(double x, int p)
    {
        if (!TryDecimalParts(x, p, out var coefficient, out var exponent))
        {
            return JsNumber.ToString(x);
        }

        if (exponent < 0)
        {
            return "0." + new string('0', -exponent - 1) + coefficient;
        }

        return coefficient.Length > exponent + 1
            ? coefficient[..(exponent + 1)] + "." + coefficient[(exponent + 1)..]
            : coefficient + new string('0', exponent - coefficient.Length + 1);
    }

    /// <summary>
    ///     Formats a number with an SI prefix: the digits scaled to the prefix (d3's <c>formatPrefixAuto</c>).
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <param name="p">The significant digits.</param>
    /// <param name="prefixExponent">Receives the power of ten of the SI prefix; null for zero and non-finite numbers.</param>
    /// <returns>The scaled digits.</returns>
    private static string FormatPrefixAuto(double x, int p, out int? prefixExponent)
    {
        if (!TryDecimalParts(x, p, out var coefficient, out var exponent))
        {
            prefixExponent = null;
            return ToPrecision(x, p);
        }

        // The prefix power is the exponent rounded down to a multiple of three, within yocto to yotta
        var power = (int)(Math.Max(-8, Math.Min(8, Math.Floor(exponent / 3.0))) * 3);
        prefixExponent = power;
        var i = exponent - power + 1;
        var n = coefficient.Length;
        if (i == n)
        {
            return coefficient;
        }

        if (i > n)
        {
            return coefficient + new string('0', i - n);
        }

        if (i > 0)
        {
            return coefficient[..i] + "." + coefficient[i..];
        }

        // Smaller than one yocto: leading zeros, then the digits again with a matching precision
        TryDecimalParts(x, Math.Max(0, p + i - 1), out var rescaled, out _);
        return "0." + new string('0', -i) + rescaled;
    }

    /// <summary>
    ///     Removes insignificant trailing zeros of a formatted number, for example <c>1.2000</c> to <c>1.2</c> and
    ///     <c>1.0e+3</c> to <c>1e+3</c> (d3's <c>formatTrim</c>).
    /// </summary>
    /// <param name="s">The formatted digits.</param>
    /// <returns>The trimmed text.</returns>
    private static string TrimZeros(string s)
    {
        // Track the run of zeros after the decimal point (including the point) and cut it at the end or at an exponent
        var i0 = -1;
        var i1 = 0;
        for (var i = 1; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '.')
            {
                i0 = i1 = i;
            }
            else if (c == '0')
            {
                if (i0 == 0)
                {
                    i0 = i;
                }

                i1 = i;
            }
            else if (c is >= '1' and <= '9')
            {
                if (i0 > 0)
                {
                    i0 = 0;
                }
            }
            else
            {
                break;
            }
        }

        return i0 > 0 ? s[..i0] + s[(i1 + 1)..] : s;
    }

    /// <summary>
    ///     Groups the digits of an integer part with thousands separators (d3's <c>formatGroup</c> for groups of three).
    /// </summary>
    /// <param name="value">The digits (with padding when zero filled).</param>
    /// <param name="width">The width the grouped text may take; <see cref="int.MaxValue"/> for no limit.</param>
    /// <returns>The grouped text.</returns>
    private static string Group(string value, int width)
    {
        var i = value.Length;
        var groups = new List<string>();
        var g = 3;
        var length = 0;
        while (i > 0 && g > 0)
        {
            if (length + g + 1 > width)
            {
                g = Math.Max(1, width - length);
            }

            var start = Math.Max(0, i - g);
            groups.Add(value[start..i]);
            i -= g;
            length += g + 1;
            if (length > width)
            {
                break;
            }

            g = 3;
        }

        groups.Reverse();
        return string.Join(',', groups);
    }

    /// <summary>
    ///     Finds the significant digits of a positive finite number and the exponent of the first digit (d3's
    ///     <c>formatDecimalParts</c>).
    /// </summary>
    /// <param name="x">The number.</param>
    /// <param name="p">The number of significant digits to round to (half up on the exact value); zero for the shortest digits that identify the number.</param>
    /// <param name="coefficient">Receives the digits.</param>
    /// <param name="exponent">Receives the decimal exponent of the first digit.</param>
    /// <returns><see langword="false"/> for zero, NaN and the infinities.</returns>
    private static bool TryDecimalParts(double x, int p, out string coefficient, out int exponent)
    {
        if (!double.IsFinite(x) || JsNumber.IsZero(x))
        {
            coefficient = string.Empty;
            exponent = 0;
            return false;
        }

        if (p > 0)
        {
            ExactDecimal(x, out var digits, out var scale);
            coefficient = RoundDigits(digits, p, out var carry);
            exponent = digits.Length - 1 - scale + carry;
        }
        else
        {
            JsNumber.ShortestDecimal(x, out coefficient, out var pointPosition);
            exponent = pointPosition - 1;
        }

        return true;
    }

    /// <summary>
    ///     Writes the exact decimal expansion of a positive finite double.
    /// </summary>
    /// <remarks>
    ///     A double is an integer times a power of two; times a power of five and over a power of ten it becomes a
    ///     whole number of decimal digits, so the expansion is exact and the rounding of the callers follows the exact
    ///     value as JavaScript's formatting methods do.
    /// </remarks>
    /// <param name="x">The number.</param>
    /// <param name="digits">Receives the digits of the integer that, divided by 10^<paramref name="scale"/>, is the number.</param>
    /// <param name="scale">Receives the number of digits after the decimal point.</param>
    private static void ExactDecimal(double x, out string digits, out int scale)
    {
        var bits = BitConverter.DoubleToInt64Bits(x);
        var exponentBits = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & ((1L << 52) - 1);
        if (exponentBits == 0)
        {
            exponentBits = 1;
        }
        else
        {
            mantissa |= 1L << 52;
        }

        var exponent2 = exponentBits - 1075;
        var number = new BigInteger(mantissa);
        if (exponent2 >= 0)
        {
            number <<= exponent2;
            scale = 0;
        }
        else
        {
            number *= BigInteger.Pow(5, -exponent2);
            scale = -exponent2;
        }

        digits = number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Rounds a digit string half up to a number of significant digits.
    /// </summary>
    /// <param name="digits">The digits (no leading zeros).</param>
    /// <param name="count">The digits to keep.</param>
    /// <param name="carry">Receives 1 when rounding carried into a new leading digit (999 to 1000), otherwise 0.</param>
    /// <returns>Exactly <paramref name="count"/> digits.</returns>
    private static string RoundDigits(string digits, int count, out int carry)
    {
        carry = 0;
        if (digits.Length <= count)
        {
            return digits + new string('0', count - digits.Length);
        }

        // Rounding half up on the exact value: a first dropped digit of five or more rounds up
        var kept = digits[..count].ToCharArray();
        if (digits[count] >= '5')
        {
            var i = kept.Length - 1;
            while (i >= 0 && kept[i] == '9')
            {
                kept[i--] = '0';
            }

            if (i < 0)
            {
                carry = 1;
                return "1" + new string('0', count - 1);
            }

            kept[i]++;
        }

        return new string(kept);
    }

    /// <summary>
    ///     Formats a number like JavaScript's <c>toFixed</c>.
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <param name="fractionDigits">The digits after the decimal point.</param>
    /// <returns>The text; the plain number text for 1e21 and above, as JavaScript does.</returns>
    private static string ToFixed(double x, int fractionDigits)
    {
        if (!double.IsFinite(x) || x >= 1e21)
        {
            return JsNumber.ToString(x);
        }

        // Find the integer nearest to x times 10^f as a digit string (ties go up)
        string integer;
        if (JsNumber.IsZero(x))
        {
            integer = "0";
        }
        else
        {
            ExactDecimal(x, out var digits, out var scale);
            if (scale <= fractionDigits)
            {
                integer = digits + new string('0', fractionDigits - scale);
            }
            else
            {
                var drop = scale - fractionDigits;
                string head;
                bool up;
                if (drop > digits.Length)
                {
                    head = "0";
                    up = false;
                }
                else
                {
                    head = drop == digits.Length ? "0" : digits[..^drop];
                    up = digits[digits.Length - drop] >= '5';
                }

                integer = up ? IncrementDigits(head) : head;
            }
        }

        // Place the decimal point
        if (fractionDigits == 0)
        {
            return integer;
        }

        integer = integer.PadLeft(fractionDigits + 1, '0');
        return integer[..^fractionDigits] + "." + integer[^fractionDigits..];
    }

    /// <summary>
    ///     Adds one to a string of decimal digits.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The digits of the number plus one.</returns>
    private static string IncrementDigits(string digits)
    {
        var chars = digits.ToCharArray();
        var i = chars.Length - 1;
        while (i >= 0 && chars[i] == '9')
        {
            chars[i--] = '0';
        }

        if (i < 0)
        {
            return "1" + new string(chars);
        }

        chars[i]++;
        return new string(chars);
    }

    /// <summary>
    ///     Formats a number like JavaScript's <c>toExponential</c> with a number of fraction digits.
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <param name="fractionDigits">The digits after the decimal point of the coefficient.</param>
    /// <returns>The text, for example <c>1.50e+3</c>; <c>Infinity</c> for infinity.</returns>
    private static string ToExponential(double x, int fractionDigits)
    {
        if (double.IsInfinity(x))
        {
            return "Infinity";
        }

        string coefficient;
        int exponent;
        if (JsNumber.IsZero(x))
        {
            coefficient = new string('0', fractionDigits + 1);
            exponent = 0;
        }
        else
        {
            TryDecimalParts(x, fractionDigits + 1, out coefficient, out exponent);
        }

        var text = new StringBuilder(coefficient[..1]);
        if (fractionDigits > 0)
        {
            text.Append('.').Append(coefficient, 1, coefficient.Length - 1);
        }

        return text.Append('e').Append(exponent < 0 ? '-' : '+').Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture)).ToString();
    }

    /// <summary>
    ///     Formats a number like JavaScript's <c>toPrecision</c>.
    /// </summary>
    /// <param name="x">The non-negative number.</param>
    /// <param name="precision">The significant digits (1 to 21).</param>
    /// <returns>The text, in exponent form when the exponent is below -6 or not below the precision.</returns>
    private static string ToPrecision(double x, int precision)
    {
        if (!double.IsFinite(x))
        {
            return JsNumber.ToString(x);
        }

        if (JsNumber.IsZero(x))
        {
            return precision > 1 ? "0." + new string('0', precision - 1) : "0";
        }

        TryDecimalParts(x, precision, out var coefficient, out var e);
        if (e < -6 || e >= precision)
        {
            var text = new StringBuilder(coefficient[..1]);
            if (precision > 1)
            {
                text.Append('.').Append(coefficient, 1, coefficient.Length - 1);
            }

            return text.Append('e').Append(e < 0 ? '-' : '+').Append(Math.Abs(e).ToString(CultureInfo.InvariantCulture)).ToString();
        }

        if (e == precision - 1)
        {
            return coefficient;
        }

        return e >= 0
            ? coefficient[..(e + 1)] + "." + coefficient[(e + 1)..]
            : "0." + new string('0', -(e + 1)) + coefficient;
    }

    /// <summary>
    ///     The pattern of a format specifier: <c>[[fill]align][sign][symbol][0][width][,][.precision][~][type]</c>.
    /// </summary>
    /// <returns>The compiled regular expression.</returns>
    [GeneratedRegex(@"^(?:([^\n\r\u2028\u2029])?([<>=^]))?([+\-( ])?([$#])?(0)?([0-9]+)?(,)?(\.[0-9]+)?(~)?([a-zA-Z%])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex SpecifierPattern();
}
