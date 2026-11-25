using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.MathMl.Syntax;

/// <summary>
///     The unit of a <see cref="MathLength"/>: an absolute length in CSS pixels, or a length relative to the font.
/// </summary>
internal enum MathLengthUnit
{
    /// <summary>CSS pixels; the absolute units (<c>pt</c>, <c>pc</c>, <c>in</c>, <c>cm</c>, <c>mm</c>) are converted to it.</summary>
    Px,

    /// <summary>Multiples of the font size (<c>em</c>, and the named spaces such as <c>thinmathspace</c>).</summary>
    Em,

    /// <summary>Multiples of the x-height of the font (<c>ex</c>).</summary>
    Ex,

    /// <summary>A percentage of a reference length that depends on the attribute.</summary>
    Percent,
}

/// <summary>
///     A CSS length or percentage as written in a MathML attribute, before it is resolved against a font size.
/// </summary>
/// <param name="Value">The number (for <see cref="MathLengthUnit.Percent"/>, the percentage, so 50 means half).</param>
/// <param name="Unit">The unit.</param>
internal readonly record struct MathLength(double Value, MathLengthUnit Unit)
{
    /// <summary>The largest magnitude of a resolved length in CSS pixels; larger values are clamped to it.</summary>
    /// <remarks>
    ///     A length of an untrusted equation (an <c>mspace</c> a million em wide) must keep the arithmetic of the layout
    ///     finite; a picture this large fails the size check of the surface anyway.
    /// </remarks>
    public const double MaxMagnitude = 1_000_000;

    /// <summary>
    ///     Resolves the length to CSS pixels.
    /// </summary>
    /// <param name="fontSize">The font size in CSS pixels, the base of <c>em</c>.</param>
    /// <param name="xHeight">The x-height in CSS pixels, the base of <c>ex</c>.</param>
    /// <param name="percentBase">The reference length of a percentage, in CSS pixels.</param>
    /// <returns>The length in CSS pixels, clamped to plus or minus <see cref="MaxMagnitude"/>.</returns>
    public double Resolve(double fontSize, double xHeight, double percentBase)
    {
        var pixels = Unit switch
        {
            MathLengthUnit.Em => Value * fontSize,
            MathLengthUnit.Ex => Value * xHeight,
            MathLengthUnit.Percent => Value / 100 * percentBase,
            _ => Value,
        };
        return double.IsNaN(pixels) ? 0 : Math.Clamp(pixels, -MaxMagnitude, MaxMagnitude);
    }
}

/// <summary>
///     The parsers of MathML attribute values: CSS lengths and percentages with the legacy named spaces, booleans,
///     unsigned integers, <c>scriptlevel</c> values and colors.
/// </summary>
/// <remarks>
///     <para>
///         Every parser is a pure function that returns false for a value it does not accept, so the caller decides what
///         to report: MathML Core ignores an invalid attribute value, and so does the layout (with a
///         <see cref="MathMlDiagnosticCodes.AttributeInvalid"/> warning). Keywords are matched ASCII case-insensitively,
///         as MathML Core requires; numbers use the invariant culture.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class AttributeValues
{
    /// <summary>The legacy named spaces of MathML 3, in eighteenths of an em; the negative forms negate them.</summary>
    private static readonly Dictionary<string, int> NamedSpaces = new(StringComparer.OrdinalIgnoreCase)
    {
        ["veryverythinmathspace"] = 1,
        ["verythinmathspace"] = 2,
        ["thinmathspace"] = 3,
        ["mediummathspace"] = 4,
        ["thickmathspace"] = 5,
        ["verythickmathspace"] = 6,
        ["veryverythickmathspace"] = 7,
    };

    /// <summary>The CSS units and their unit and factor of conversion.</summary>
    private static readonly Dictionary<string, (MathLengthUnit Unit, double Factor)> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["px"] = (MathLengthUnit.Px, 1),
        ["pt"] = (MathLengthUnit.Px, 96.0 / 72),
        ["pc"] = (MathLengthUnit.Px, 16),
        ["in"] = (MathLengthUnit.Px, 96),
        ["cm"] = (MathLengthUnit.Px, 96 / 2.54),
        ["mm"] = (MathLengthUnit.Px, 96 / 25.4),
        ["em"] = (MathLengthUnit.Em, 1),
        ["ex"] = (MathLengthUnit.Ex, 1),
        ["%"] = (MathLengthUnit.Percent, 1),
    };

    /// <summary>
    ///     Parses a CSS length or percentage (<c>2em</c>, <c>-0.5ex</c>, <c>12px</c>, <c>1in</c>, <c>50%</c>), a
    ///     legacy named space (<c>thinmathspace</c>, <c>negativethickmathspace</c>) or a unitless zero.
    /// </summary>
    /// <param name="text">The attribute value; surrounding whitespace is ignored.</param>
    /// <param name="length">The length when the value is valid.</param>
    /// <returns>True when the value is a valid length.</returns>
    public static bool TryParseLength(string? text, out MathLength length)
    {
        length = default;
        if (text is null)
        {
            return false;
        }

        var value = text.Trim();

        // The named spaces of MathML 3, which browsers still accept
        var negative = value.StartsWith("negative", StringComparison.OrdinalIgnoreCase);
        if (NamedSpaces.TryGetValue(negative ? value[8..] : value, out var eighteenths))
        {
            length = new MathLength((negative ? -eighteenths : eighteenths) / 18.0, MathLengthUnit.Em);
            return true;
        }

        // A number followed by a unit; only zero may omit the unit
        var numberLength = MeasureNumber(value);
        if (numberLength == 0 || !double.TryParse(value.AsSpan(0, numberLength), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        var unit = value[numberLength..];
        if (unit.Length == 0)
        {
            if (number != 0)
            {
                return false;
            }

            length = new MathLength(0, MathLengthUnit.Px);
            return true;
        }

        if (!Units.TryGetValue(unit, out var conversion))
        {
            return false;
        }

        length = new MathLength(number * conversion.Factor, conversion.Unit);
        return true;
    }

    /// <summary>
    ///     Parses a MathML boolean (<c>true</c> or <c>false</c>, ASCII case-insensitive).
    /// </summary>
    /// <param name="text">The attribute value; surrounding whitespace is ignored.</param>
    /// <param name="value">The boolean when the value is valid.</param>
    /// <returns>True when the value is a valid boolean.</returns>
    public static bool TryParseBoolean(string? text, out bool value)
    {
        value = false;
        var trimmed = text?.Trim();
        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        return string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Parses an unsigned integer (ASCII digits only).
    /// </summary>
    /// <param name="text">The attribute value; surrounding whitespace is ignored.</param>
    /// <param name="value">The integer when the value is valid.</param>
    /// <returns>True when the value is a valid unsigned integer that fits an <see cref="int"/>.</returns>
    public static bool TryParseUnsignedInteger(string? text, out int value)
    {
        value = 0;
        var trimmed = text?.Trim();
        return !string.IsNullOrEmpty(trimmed)
            && trimmed.All(char.IsAsciiDigit)
            && int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    ///     Parses a <c>scriptlevel</c> value: <c>+U</c> and <c>-U</c> change the inherited level, <c>U</c> sets it.
    /// </summary>
    /// <param name="text">The attribute value; surrounding whitespace is ignored.</param>
    /// <param name="relative">True for <c>+U</c> and <c>-U</c> (a change), false for <c>U</c> (a level).</param>
    /// <param name="value">The change or the level; negative for <c>-U</c>.</param>
    /// <returns>True when the value is valid.</returns>
    public static bool TryParseScriptLevel(string? text, out bool relative, out int value)
    {
        relative = false;
        value = 0;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        // The sign makes the value a change of the inherited level
        var sign = trimmed[0] switch
        {
            '+' => 1,
            '-' => -1,
            _ => 0,
        };
        if (!TryParseUnsignedInteger(sign == 0 ? trimmed : trimmed[1..], out var magnitude))
        {
            return false;
        }

        relative = sign != 0;
        value = sign < 0 ? -magnitude : magnitude;
        return true;
    }

    /// <summary>
    ///     Parses a CSS color (a name, <c>transparent</c>, <c>#rgb</c>, <c>#rrggbb</c>, <c>rgb()</c>, <c>hsl()</c> and
    ///     their alpha forms).
    /// </summary>
    /// <param name="text">The attribute value; surrounding whitespace is ignored.</param>
    /// <param name="color">The color when the value is valid.</param>
    /// <returns>True when the value is a valid color.</returns>
    public static bool TryParseColor(string? text, out Rgba32 color) => CssColor.TryParse(text, out color);

    /// <summary>
    ///     Measures the leading CSS number of a text: an optional sign, digits, and an optional fraction.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number of characters of the number, or zero when the text does not start with one.</returns>
    private static int MeasureNumber(string text)
    {
        var index = 0;
        if (index < text.Length && text[index] is '+' or '-')
        {
            index++;
        }

        var digits = 0;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
            digits++;
        }

        if (index < text.Length && text[index] == '.')
        {
            index++;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
                digits++;
            }
        }

        return digits == 0 ? 0 : index;
    }
}
