// Portions derived from js-yaml (MIT) as bundled by mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Numerics;
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     A value read by <see cref="YamlSubsetParser"/>, with the location of its first character in the original text.
/// </summary>
/// <remarks>
///     <para>
///         The tree has the shape of the JavaScript value that js-yaml's <c>load</c> with the <c>JSON_SCHEMA</c> gives
///         Mermaid: a mapping (an object whose keys are strings), a sequence (an array) or a scalar (<see langword="null"/>,
///         a boolean, a number or a string). <see cref="IsTruthy"/> and <see cref="ToJsString"/> give the JavaScript
///         truthiness and <c>String(value)</c> conversion that Mermaid applies to frontmatter values.
///     </para>
///     <para>
///         Nodes are immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Location">The location of the first character of the value in the original text.</param>
internal abstract record YamlNode(MermaidLocation Location)
{
    /// <summary>
    ///     Gets a value indicating whether the value is truthy in JavaScript (objects and arrays always are; <c>null</c>,
    ///     <c>false</c>, <c>0</c>, <c>NaN</c> and the empty string are not).
    /// </summary>
    public abstract bool IsTruthy { get; }

    /// <summary>
    ///     Converts the value to text as JavaScript's <c>String(value)</c> does.
    /// </summary>
    /// <returns>The text: <c>null</c>, <c>true</c>, a number in JavaScript's shortest form, the string itself, the items of a sequence joined with commas, or <c>[object Object]</c>.</returns>
    public abstract string ToJsString();
}

/// <summary>
///     A scalar value: <see langword="null"/>, a <see cref="bool"/>, a <see cref="double"/> or a <see cref="string"/>.
/// </summary>
/// <param name="Value">The value.</param>
/// <param name="Location">The location of the value.</param>
internal sealed record YamlScalar(object? Value, MermaidLocation Location) : YamlNode(Location)
{
    /// <inheritdoc />
    public override bool IsTruthy => Value switch
    {
        null => false,
        bool b => b,
        double d => d != 0 && !double.IsNaN(d),
        string s => s.Length > 0,
        _ => true,
    };

    /// <inheritdoc />
    public override string ToJsString() => Value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        double d => YamlSubsetParser.FormatJsNumber(d),
        _ => (string)Value,
    };
}

/// <summary>
///     A sequence (a JavaScript array).
/// </summary>
/// <param name="Items">The items in order.</param>
/// <param name="Location">The location of the sequence.</param>
internal sealed record YamlSequence(IReadOnlyList<YamlNode> Items, MermaidLocation Location) : YamlNode(Location)
{
    /// <inheritdoc />
    public override bool IsTruthy => true;

    /// <inheritdoc />
    public override string ToJsString() =>
        string.Join(",", Items.Select(item => item is YamlScalar { Value: null } ? string.Empty : item.ToJsString()));
}

/// <summary>
///     One key and value of a <see cref="YamlMapping"/>.
/// </summary>
/// <param name="Key">The key, converted to a string as a JavaScript object key.</param>
/// <param name="Value">The value.</param>
/// <param name="KeyLocation">The location of the key.</param>
internal sealed record YamlEntry(string Key, YamlNode Value, MermaidLocation KeyLocation);

/// <summary>
///     A mapping (a JavaScript object) with its entries in the order they were written; keys are unique.
/// </summary>
/// <param name="Entries">The entries in document order.</param>
/// <param name="Location">The location of the mapping.</param>
internal sealed record YamlMapping(IReadOnlyList<YamlEntry> Entries, MermaidLocation Location) : YamlNode(Location)
{
    /// <inheritdoc />
    public override bool IsTruthy => true;

    /// <summary>
    ///     Gets the value of a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/> when the mapping has no such key.</returns>
    public YamlNode? Get(string key) => Entries.FirstOrDefault(entry => entry.Key == key)?.Value;

    /// <inheritdoc />
    public override string ToJsString() => "[object Object]";
}

/// <content>
///     The scalar types of js-yaml's <c>JSON_SCHEMA</c> and the JavaScript number formatting.
/// </content>
internal sealed partial class YamlSubsetParser
{
    /// <summary>
    ///     Resolves the text of a plain (unquoted) scalar to a value with the implicit types of js-yaml's
    ///     <c>JSON_SCHEMA</c>, tried in order: null, boolean, integer, float; anything else is a string.
    /// </summary>
    /// <param name="text">The text of the scalar.</param>
    /// <returns><see langword="null"/>, a <see cref="bool"/>, a <see cref="double"/> or the text.</returns>
    internal static object? ResolvePlain(string text)
    {
        // null: ~, null, Null, NULL
        if (text is "~" or "null" or "Null" or "NULL")
        {
            return null;
        }

        // bool: true/True/TRUE and false/False/FALSE
        if (text is "true" or "True" or "TRUE")
        {
            return true;
        }

        if (text is "false" or "False" or "FALSE")
        {
            return false;
        }

        // int, then float; a value that overflows to infinity stays text
        if (TryResolveInteger(text, out var integer))
        {
            return integer;
        }

        return TryResolveFloat(text, out var real) ? real : text;
    }

    /// <summary>
    ///     Formats a number as JavaScript's <c>Number.prototype.toString()</c> does: the shortest digits that round-trip,
    ///     in positional form for exponents from -7 to 20 and in exponential form otherwise.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text, for example <c>1.5</c>, <c>1e+21</c>, <c>1e-7</c>, <c>NaN</c> or <c>-Infinity</c>.</returns>
    internal static string FormatJsNumber(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "Infinity" : "-Infinity";
        }

        if (value == 0)
        {
            return "0";
        }

        // Take the shortest round-trip digits from .NET and work out the decimal exponent n (value = 0.d1d2... x 10^n)
        var sign = value < 0 ? "-" : string.Empty;
        var round = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
        var exponentIndex = round.IndexOfAny(['E', 'e']);
        var exponent = exponentIndex < 0 ? 0 : int.Parse(round[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var mantissa = exponentIndex < 0 ? round : round[..exponentIndex];
        var point = mantissa.IndexOf('.');
        var integerDigits = point < 0 ? mantissa.Length : point;
        var digits = mantissa.Replace(".", string.Empty, StringComparison.Ordinal);
        var leading = digits.Length - digits.TrimStart('0').Length;
        digits = digits.Trim('0');
        var k = digits.Length;
        var n = integerDigits - leading + exponent;

        // ECMAScript Number::toString: positional for -6 < n <= 21, exponential otherwise
        if (k <= n && n <= 21)
        {
            return sign + digits + new string('0', n - k);
        }

        if (0 < n && n <= 21)
        {
            return sign + digits[..n] + "." + digits[n..];
        }

        if (-6 < n && n <= 0)
        {
            return sign + "0." + new string('0', -n) + digits;
        }

        var e = n - 1;
        var exponentText = (e < 0 ? "-" : "+") + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
        return k == 1 ? sign + digits + "e" + exponentText : sign + digits[..1] + "." + digits[1..] + "e" + exponentText;
    }

    /// <summary>
    ///     Resolves js-yaml's integer type: decimal digits, or <c>0b</c>, <c>0x</c> or <c>0o</c> digits, with an optional sign.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value when the text is an integer.</param>
    /// <returns>Whether the text is a finite integer.</returns>
    private static bool TryResolveInteger(string text, out double value)
    {
        value = 0;
        if (text.Length == 0)
        {
            return false;
        }

        // Optional sign
        var index = 0;
        var negative = false;
        if (text[0] is '-' or '+')
        {
            negative = text[0] == '-';
            index++;
        }

        if (index >= text.Length)
        {
            return false;
        }

        // A lone zero, or a radix prefix after a leading zero
        if (text[index] == '0')
        {
            if (index + 1 == text.Length)
            {
                value = 0;
                return true;
            }

            var radix = text[index + 1] switch { 'b' => 2, 'x' => 16, 'o' => 8, _ => 0 };
            if (radix != 0)
            {
                return TryParseRadix(text[(index + 2)..], radix, negative, out value);
            }
        }

        // Decimal digits only
        var body = text[index..];
        if (!body.All(char.IsAsciiDigit))
        {
            return false;
        }

        value = double.Parse(body, NumberStyles.None, CultureInfo.InvariantCulture) * (negative ? -1 : 1);
        return double.IsFinite(value);
    }

    /// <summary>
    ///     Parses digits of a radix as JavaScript's <c>parseInt</c> does for js-yaml's prefixed integers.
    /// </summary>
    /// <param name="digits">The digits after the prefix.</param>
    /// <param name="radix">2, 8 or 16.</param>
    /// <param name="negative">Whether the sign is negative.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the digits are valid and the value is finite.</returns>
    private static bool TryParseRadix(string digits, int radix, bool negative, out double value)
    {
        value = 0;
        if (digits.Length == 0)
        {
            return false;
        }

        var accumulated = BigInteger.Zero;
        foreach (var c in digits)
        {
            var digit = -1;
            if (char.IsAsciiDigit(c))
            {
                digit = c - '0';
            }
            else if (char.IsAsciiHexDigit(c))
            {
                digit = char.ToLowerInvariant(c) - 'a' + 10;
            }

            if (digit < 0 || digit >= radix)
            {
                return false;
            }

            accumulated = (accumulated * radix) + digit;
        }

        value = (double)accumulated * (negative ? -1 : 1);
        return double.IsFinite(value);
    }

    /// <summary>
    ///     Resolves js-yaml's float type: a decimal number with an optional fraction and exponent, or <c>.inf</c> and
    ///     <c>.nan</c> in their three spellings.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value when the text is a float.</param>
    /// <returns>Whether the text is a float (finite, or one of the special values).</returns>
    private static bool TryResolveFloat(string text, out double value)
    {
        value = 0;

        // The special values first: [-+]?.inf and .nan
        var unsigned = text.Length > 0 && text[0] is '-' or '+' ? text[1..] : text;
        if (unsigned is ".inf" or ".Inf" or ".INF")
        {
            value = text[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (text is ".nan" or ".NaN" or ".NAN")
        {
            value = double.NaN;
            return true;
        }

        // [-+]?[0-9]+(\.[0-9]*)?([eE][-+]?[0-9]+)? or \.[0-9]+([eE][-+]?[0-9]+)? (no sign before a leading point)
        if (!IsFloatSyntax(text))
        {
            return false;
        }

        value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        return double.IsFinite(value);
    }

    /// <summary>
    ///     Checks the finite-number syntax of js-yaml's <c>YAML_FLOAT_PATTERN</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether the text has the syntax.</returns>
    private static bool IsFloatSyntax(string text)
    {
        var i = 0;
        var leadingPoint = text.StartsWith('.');
        if (!leadingPoint && i < text.Length && text[i] is '-' or '+')
        {
            i++;
        }

        // Integer digits (required unless the number starts with the point)
        var start = i;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
        }

        if (!leadingPoint && i == start)
        {
            return false;
        }

        // Fraction: after a leading point at least one digit is required
        if (i < text.Length && text[i] == '.')
        {
            i++;
            var fractionStart = i;
            while (i < text.Length && char.IsAsciiDigit(text[i]))
            {
                i++;
            }

            if (leadingPoint && i == fractionStart)
            {
                return false;
            }
        }

        // Exponent
        if (i < text.Length && text[i] is 'e' or 'E')
        {
            i++;
            if (i < text.Length && text[i] is '-' or '+')
            {
                i++;
            }

            var exponentStart = i;
            while (i < text.Length && char.IsAsciiDigit(text[i]))
            {
                i++;
            }

            if (i == exponentStart)
            {
                return false;
            }
        }

        return i == text.Length;
    }

    /// <summary>
    ///     Checks a captured segment for the characters js-yaml refuses: non-printable characters in a plain or block
    ///     scalar, and control characters other than the tab in a quoted scalar.
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <param name="quoted">Whether the segment comes from a quoted scalar (js-yaml's JSON check).</param>
    /// <returns>Whether the segment is acceptable.</returns>
    private static bool IsPrintable(ReadOnlySpan<char> segment, bool quoted)
    {
        for (var i = 0; i < segment.Length; i++)
        {
            var c = segment[i];
            if (quoted)
            {
                if (c != '\t' && c < 0x20)
                {
                    return false;
                }

                continue;
            }

            // js-yaml's PATTERN_NON_PRINTABLE: the C0 controls except tab and line breaks, DEL and the C1 controls except
            // NEL, the two non-characters at the end of the basic plane, and unpaired surrogates
            if (c <= 0x08 || c is '\v' or '\f' || (c >= 0x0E && c <= 0x1F) || (c >= 0x7F && c <= 0x84) || (c >= 0x86 && c <= 0x9F) || c >= 0xFFFE)
            {
                return false;
            }

            if (char.IsHighSurrogate(c) && (i + 1 >= segment.Length || !char.IsLowSurrogate(segment[i + 1])))
            {
                return false;
            }

            if (char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(segment[i - 1])))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Appends the escaped character of a double-quoted scalar for a simple escape (js-yaml's <c>simpleEscapeSequence</c>).
    /// </summary>
    /// <param name="c">The character after the backslash.</param>
    /// <returns>The character it stands for, or null when it is not a simple escape.</returns>
    private static char? SimpleEscape(char c) => c switch
    {
        '0' => '\0',
        'a' => '\a',
        'b' => '\b',
        't' or '\t' => '\t',
        'n' => '\n',
        'v' => '\v',
        'f' => '\f',
        'r' => '\r',
        'e' => '\u001b',
        ' ' => ' ',
        '"' => '"',
        '/' => '/',
        '\\' => '\\',
        'N' => '\u0085',
        '_' => '\u00a0',
        'L' => '\u2028',
        'P' => '\u2029',
        _ => null,
    };

    /// <summary>
    ///     Appends a code point to a builder, as a surrogate pair when it is above the basic plane.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="codePoint">The code point.</param>
    private static void AppendCodePoint(StringBuilder builder, long codePoint)
    {
        if (codePoint <= 0xFFFF)
        {
            builder.Append((char)codePoint);
            return;
        }

        // js-yaml's charFromCodepoint: no range check, the surrogates are computed as JavaScript computes them
        builder.Append((char)(((codePoint - 0x10000) >> 10) + 0xD800));
        builder.Append((char)(((codePoint - 0x10000) & 0x3FF) + 0xDC00));
    }
}
