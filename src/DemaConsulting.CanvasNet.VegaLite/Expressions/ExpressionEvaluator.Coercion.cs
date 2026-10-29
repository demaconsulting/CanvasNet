using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The JavaScript coercion rules of the expression evaluator (the static half of the evaluator unit).
/// </summary>
/// <remarks>
///     <para>
///         The rules follow ECMAScript for the value kinds the data model has. Null stands for both JavaScript's
///         <c>null</c> and <c>undefined</c>, so it converts to zero as a number (the <c>null</c> rule) and to the text
///         <c>null</c>. A date converts to its milliseconds as a number and to its ISO 8601 text as a primitive without a
///         hint, and an array converts to its comma-joined text; objects and regular expressions convert to
///         <c>[object Object]</c> and <c>/source/flags</c>. Every conversion is culture invariant.
///     </para>
///     <para>
///         The helpers are stateless and thread-safe; text conversions take the string limit of the caller so a
///         composite value cannot expand without bound.
///     </para>
/// </remarks>
internal sealed partial class ExpressionEvaluator
{
    /// <summary>The number of values of a 32-bit integer.</summary>
    private const double TwoPow32 = 4_294_967_296.0;

    /// <summary>
    ///     Tells whether a character is JavaScript white space or a line terminator.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for the characters that JavaScript's <c>trim</c> removes.</returns>
    internal static bool IsJsWhitespace(char c) =>
        c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r' or '\u00A0' or '\u1680' or '\u2028' or '\u2029' or '\u202F'
            or '\u205F' or '\u3000' or '\uFEFF'
        || (c >= '\u2000' && c <= '\u200A');

    /// <summary>
    ///     Applies JavaScript truthiness.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>False for null, false, zero, NaN and the empty string; true for everything else.</returns>
    internal static bool IsTruthy(DataValue value) => value.Kind switch
    {
        DataValueKind.Null => false,
        DataValueKind.Boolean => value.Boolean,
        DataValueKind.Number => value.Number != 0 && !double.IsNaN(value.Number),
        DataValueKind.String => value.Text!.Length > 0,
        _ => true,
    };

    /// <summary>
    ///     Converts a value to a number the way JavaScript's unary plus does.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="context">The evaluation to charge the work of reading a text to; null to not charge.</param>
    /// <returns>The number; NaN when the value has no numeric meaning.</returns>
    internal static double ToNumber(DataValue value, ExpressionFunctionContext? context = null) => value.Kind switch
    {
        DataValueKind.Null => 0,
        DataValueKind.Boolean => value.Boolean ? 1 : 0,
        DataValueKind.Number => value.Number,
        DataValueKind.Date => value.Milliseconds,
        DataValueKind.String => ToNumber(value.Text!, context),
        DataValueKind.Array => ArrayToNumber(value.Elements, context),
        _ => double.NaN,
    };

    /// <summary>
    ///     Converts text to a number the way JavaScript's <c>Number(text)</c> does, charging the work of reading it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="context">The evaluation to charge to; null to not charge.</param>
    /// <returns>The number.</returns>
    private static double ToNumber(string text, ExpressionFunctionContext? context)
    {
        context?.ChargeSize(text.Length);
        return ToNumber(text);
    }

    /// <summary>
    ///     Converts text to a number the way JavaScript's <c>Number(text)</c> does.
    /// </summary>
    /// <remarks>
    ///     White space around the text is ignored, an empty text is zero, <c>Infinity</c> and the hexadecimal, octal and
    ///     binary prefixes are understood, and anything else that is not a decimal literal is NaN.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    internal static double ToNumber(string text)
    {
        // Trim JavaScript white space; an empty text is zero
        var start = 0;
        var end = text.Length;
        while (start < end && IsJsWhitespace(text[start]))
        {
            start++;
        }

        while (end > start && IsJsWhitespace(text[end - 1]))
        {
            end--;
        }

        var span = text.AsSpan(start, end - start);
        if (span.Length == 0)
        {
            return 0;
        }

        // The signed infinity literals and the radix prefixes have their own syntax
        if (span.SequenceEqual("Infinity") || span.SequenceEqual("+Infinity"))
        {
            return double.PositiveInfinity;
        }

        if (span.SequenceEqual("-Infinity"))
        {
            return double.NegativeInfinity;
        }

        if (span.Length > 2 && span[0] == '0' && char.ToLowerInvariant(span[1]) is 'x' or 'o' or 'b')
        {
            return ParseRadix(span[2..], char.ToLowerInvariant(span[1]) switch { 'x' => 16, 'o' => 8, _ => 2 });
        }

        // Only the characters of a decimal literal may be present; .NET would accept more (for example "infinity")
        foreach (var c in span)
        {
            if (!(c is >= '0' and <= '9' || c is '.' or '+' or '-' or 'e' or 'E'))
            {
                return double.NaN;
            }
        }

        return double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    }

    /// <summary>
    ///     Converts a value to a primitive (null, boolean, number or string).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="preferNumber">True for the number hint (relational comparison), false for no hint (addition, loose equality).</param>
    /// <param name="maxLength">The longest text allowed for an array.</param>
    /// <param name="context">The evaluation to charge the conversion to; null to not charge.</param>
    /// <returns>The primitive.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text of an array would be longer than <paramref name="maxLength"/>.</exception>
    internal static DataValue ToPrimitive(DataValue value, bool preferNumber, int maxLength, ExpressionFunctionContext? context = null) => value.Kind switch
    {
        DataValueKind.Date => preferNumber ? DataValue.FromNumber(value.Milliseconds) : DataValue.FromString(ToJsString(value, maxLength, context)),
        DataValueKind.Array or DataValueKind.Object or DataValueKind.RegExp => DataValue.FromString(ToJsString(value, maxLength, context)),
        _ => value,
    };

    /// <summary>
    ///     Converts a value to text the way JavaScript's <c>String(value)</c> does, bounded by a length.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="maxLength">The longest text allowed.</param>
    /// <param name="context">The evaluation to charge the text that is built to; null to not charge.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text would be longer than <paramref name="maxLength"/>.</exception>
    internal static string ToJsString(DataValue value, int maxLength, ExpressionFunctionContext? context = null)
    {
        if (value.IsString)
        {
            // A text from outside the evaluation (a parameter, a field of the data) has not been measured yet
            return value.Text!.Length <= maxLength
                ? value.Text
                : throw new ExpressionRuntimeException(
                    $"The expression produced a string longer than the limit of {maxLength} characters (VegaLiteLimits.MaxStringLength).");
        }

        if (!value.IsArray)
        {
            // Every conversion result is measured: a number, date, object or regular expression text can exceed a small limit
            var converted = value.ToText();
            return converted.Length <= maxLength
                ? converted
                : throw new ExpressionRuntimeException(
                    $"The expression produced a string longer than the limit of {maxLength} characters (VegaLiteLimits.MaxStringLength).");
        }

        // The text of an array repeats the content of every reference to a shared element, so the text is checked as it
        // grows (failing early) and the characters built are charged
        var text = new StringBuilder();
        if (!value.TryAppendArrayText(text, maxLength))
        {
            throw new ExpressionRuntimeException(
                $"The expression produced a string longer than the limit of {maxLength} characters (VegaLiteLimits.MaxStringLength).");
        }

        context?.ChargeSize(text.Length);
        return text.ToString();
    }

    /// <summary>
    ///     Determines JavaScript strict equality (<c>===</c>).
    /// </summary>
    /// <param name="a">The left value.</param>
    /// <param name="b">The right value.</param>
    /// <returns>True when the values have the same kind and value; NaN is not equal to itself, zero equals negative zero, composites compare by identity.</returns>
    internal static bool StrictEquals(DataValue a, DataValue b)
    {
        if (a.Kind != b.Kind)
        {
            return false;
        }

        return a.Kind == DataValueKind.Number ? NumbersEqual(a.Number, b.Number) : a.Equals(b);
    }

    /// <summary>
    ///     Compares two numbers the way JavaScript's <c>===</c> does.
    /// </summary>
    /// <param name="x">The first number.</param>
    /// <param name="y">The second number.</param>
    /// <returns>True when both are numbers of equal value (zero equals negative zero); false when either is NaN.</returns>
    internal static bool NumbersEqual(double x, double y) => !double.IsNaN(x) && !double.IsNaN(y) && x.CompareTo(y) == 0;

    /// <summary>
    ///     Determines JavaScript loose equality (<c>==</c>).
    /// </summary>
    /// <param name="a">The left value.</param>
    /// <param name="b">The right value.</param>
    /// <param name="maxLength">The longest text allowed when an array is converted to text.</param>
    /// <param name="context">The evaluation to charge the conversions to; null to not charge.</param>
    /// <returns>True when the values are loosely equal.</returns>
    internal static bool LooseEquals(DataValue a, DataValue b, int maxLength, ExpressionFunctionContext? context = null)
    {
        // Each round either decides or converts one side toward a primitive, so a few rounds always finish
        for (var round = 0; round < 4; round++)
        {
            if (a.Kind == b.Kind)
            {
                return StrictEquals(a, b);
            }

            if (a.IsNull || b.IsNull)
            {
                return false;
            }

            if (a.IsBoolean || b.IsBoolean)
            {
                a = a.IsBoolean ? DataValue.FromNumber(ToNumber(a)) : a;
                b = b.IsBoolean ? DataValue.FromNumber(ToNumber(b)) : b;
                continue;
            }

            if (IsNumberOrString(a) && IsNumberOrString(b))
            {
                return NumbersEqual(ToNumber(a, context), ToNumber(b, context));
            }

            if (IsNumberOrString(b) && !IsNumberOrString(a))
            {
                a = ToPrimitive(a, false, maxLength, context);
                continue;
            }

            if (IsNumberOrString(a) && !IsNumberOrString(b))
            {
                b = ToPrimitive(b, false, maxLength, context);
                continue;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    ///     Performs JavaScript's abstract relational comparison <c>a &lt; b</c>.
    /// </summary>
    /// <param name="a">The left value.</param>
    /// <param name="b">The right value.</param>
    /// <param name="maxLength">The longest text allowed when an array is converted to text.</param>
    /// <param name="context">The evaluation to charge the conversions to; null to not charge.</param>
    /// <returns>1 when <paramref name="a"/> is less than <paramref name="b"/>, 0 when it is not, and -1 when the comparison is undefined (a NaN is involved).</returns>
    internal static int RelationalLess(DataValue a, DataValue b, int maxLength, ExpressionFunctionContext? context = null)
    {
        var left = ToPrimitive(a, true, maxLength, context);
        var right = ToPrimitive(b, true, maxLength, context);
        if (left.IsString && right.IsString)
        {
            context?.ChargeSize(Math.Min(left.Text!.Length, right.Text!.Length));
            return string.CompareOrdinal(left.Text, right.Text) < 0 ? 1 : 0;
        }

        var x = ToNumber(left, context);
        var y = ToNumber(right, context);
        if (double.IsNaN(x) || double.IsNaN(y))
        {
            return -1;
        }

        return x < y ? 1 : 0;
    }

    /// <summary>
    ///     Converts a number to a signed 32-bit integer the way JavaScript's bitwise operators do.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The integer (NaN and infinities give zero; other values wrap modulo 2^32).</returns>
    internal static int ToInt32(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        var wrapped = Math.Truncate(value) % TwoPow32;
        if (wrapped < 0)
        {
            wrapped += TwoPow32;
        }

        return unchecked((int)(uint)wrapped);
    }

    /// <summary>
    ///     Tells whether a value is a number or a string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for numbers and strings.</returns>
    private static bool IsNumberOrString(DataValue value) => value.IsNumber || value.IsString;

    /// <summary>
    ///     Converts an array to a number through its text, as JavaScript does.
    /// </summary>
    /// <param name="elements">The elements.</param>
    /// <param name="context">The evaluation to charge the reading of a text to; null to not charge.</param>
    /// <returns>Zero for an empty array, the number of a single element, otherwise NaN.</returns>
    private static double ArrayToNumber(ReadOnlySpan<DataValue> elements, ExpressionFunctionContext? context)
    {
        if (elements.Length == 0)
        {
            return 0;
        }

        if (elements.Length > 1)
        {
            return double.NaN;
        }

        // The text of a one-element array is the text of its element, which is then read as a number
        var element = elements[0];
        return element.Kind switch
        {
            DataValueKind.Null => 0,
            DataValueKind.Number => element.Number == 0 ? 0 : element.Number,
            DataValueKind.String => ToNumber(element.Text!, context),
            DataValueKind.Array => ArrayToNumber(element.Elements, context),
            _ => double.NaN,
        };
    }

    /// <summary>
    ///     Parses the digits of a number with a radix prefix.
    /// </summary>
    /// <param name="digits">The digits after the prefix.</param>
    /// <param name="radix">The radix (2, 8 or 16).</param>
    /// <returns>The number; NaN when a digit is invalid.</returns>
    private static double ParseRadix(ReadOnlySpan<char> digits, int radix)
    {
        var value = 0.0;
        foreach (var c in digits)
        {
            var digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => int.MaxValue,
            };
            if (digit >= radix)
            {
                return double.NaN;
            }

            value = (value * radix) + digit;
        }

        return value;
    }
}
