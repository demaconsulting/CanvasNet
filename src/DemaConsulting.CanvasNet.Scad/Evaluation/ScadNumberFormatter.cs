using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Formats numbers for <c>str()</c> and <c>echo</c> deterministically: invariant culture, six significant
///     digits, <c>%g</c>-style choice between fixed and scientific notation.
/// </summary>
/// <remarks>
///     <para>
///         The text of a number appears in echo output, in diagnostics and in golden test files, so it must be
///         identical on every platform and under every current culture. The algorithm therefore never uses the
///         culture-sensitive <c>ToString()</c> and relies only on the invariant-culture <c>E5</c> format, which
///         .NET computes exactly (correctly rounded from the binary value) on every supported runtime. An exact
///         decimal tie (for example 1234565, halfway between two six-digit results) is resolved by that exact
///         digit generation to the even digit (<c>1.23456e+06</c>); this is pinned by a test.
///     </para>
///     <para>
///         Rules: <c>NaN</c> is <c>nan</c>, the infinities are <c>inf</c> and <c>-inf</c>, zero is <c>0</c> and
///         negative zero is <c>-0</c>. Otherwise the six significant digits are taken and trailing zeros are
///         removed; an exponent below -4 or at least 6 selects scientific notation (<c>1e+06</c>,
///         <c>1.5e-07</c>, at least two exponent digits), anything else is written in fixed notation.
///     </para>
/// </remarks>
internal static class ScadNumberFormatter
{
    /// <summary>The number of significant digits written.</summary>
    private const int SignificantDigits = 6;

    /// <summary>The smallest decimal exponent that is still written in fixed notation.</summary>
    private const int MinFixedExponent = -4;

    /// <summary>The first decimal exponent that is written in scientific notation.</summary>
    private const int MinScientificExponent = 6;

    /// <summary>
    ///     Formats a number.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The deterministic text form.</returns>
    public static string Format(double value)
    {
        // Non-finite values and zero have fixed spellings (negative zero keeps its sign)
        if (double.IsNaN(value))
        {
            return "nan";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "inf" : "-inf";
        }

        if (value == 0)
        {
            return double.IsNegative(value) ? "-0" : "0";
        }

        // Obtain the rounded digits and the decimal exponent from the exact E5 rendering, then lay them out
        var scientific = Math.Abs(value).ToString("E5", CultureInfo.InvariantCulture);
        var exponentIndex = scientific.IndexOf('E', StringComparison.Ordinal);
        var exponent = int.Parse(scientific.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var digits = (scientific[0].ToString() + scientific.Substring(2, SignificantDigits - 1)).TrimEnd('0');

        // The first digit of a non-zero value is never zero, so at least one digit always remains
        var body = exponent < MinFixedExponent || exponent >= MinScientificExponent
            ? LayOutScientific(digits, exponent)
            : LayOutFixed(digits, exponent);
        return value < 0 ? "-" + body : body;
    }

    /// <summary>
    ///     Lays out digits in scientific notation.
    /// </summary>
    /// <param name="digits">The significant digits without trailing zeros (at least one).</param>
    /// <param name="exponent">The decimal exponent of the first digit.</param>
    /// <returns>For example <c>1.5e-07</c>.</returns>
    private static string LayOutScientific(string digits, int exponent)
    {
        // One digit before the point, the rest after it (when present), then a signed exponent of at least two digits
        var text = new StringBuilder(digits.Length + 6);
        text.Append(digits[0]);
        if (digits.Length > 1)
        {
            text.Append('.').Append(digits, 1, digits.Length - 1);
        }

        text.Append('e').Append(exponent < 0 ? '-' : '+');
        text.Append(Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    /// <summary>
    ///     Lays out digits in fixed notation.
    /// </summary>
    /// <param name="digits">The significant digits without trailing zeros (at least one).</param>
    /// <param name="exponent">The decimal exponent of the first digit, from -4 to 5.</param>
    /// <returns>For example <c>12345.7</c> or <c>0.0001</c>.</returns>
    private static string LayOutFixed(string digits, int exponent)
    {
        // A negative exponent means a leading "0." followed by zeros before the first digit
        if (exponent < 0)
        {
            return "0." + new string('0', -exponent - 1) + digits;
        }

        // Otherwise the point falls after position exponent + 1: pad short digit runs with zeros, split long ones
        var integerLength = exponent + 1;
        return digits.Length <= integerLength
            ? digits + new string('0', integerLength - digits.Length)
            : digits[..integerLength] + "." + digits[integerLength..];
    }
}
