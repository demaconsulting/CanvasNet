using System.Collections;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Conversion of host (.NET) objects to <see cref="ScadValue"/>, used for variable overrides supplied by the caller.
/// </summary>
internal readonly partial struct ScadValue
{
    /// <summary>The maximum number of values (every nested element counts) converted from one object.</summary>
    public const int MaxConvertedElements = 1_000_000;

    /// <summary>
    ///     Converts a host object to a value.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Supported: <see langword="null"/> (<c>undef</c>), <see cref="bool"/>, every built-in integer and
    ///         floating-point type and <see cref="decimal"/> (a number; non-finite values are allowed),
    ///         <see cref="string"/>, and any other <see cref="IEnumerable"/> of supported objects (a vector).
    ///     </para>
    ///     <para>
    ///         The conversion is bounded so a hostile object cannot hang it or overflow the stack: nesting deeper than
    ///         <see cref="MaxNestingDepth"/> and more than <see cref="MaxConvertedElements"/> converted values in total
    ///         (which also stops endless and self-referential enumerables) are rejected, as is any unsupported
    ///         object. Enumerables are enumerated exactly once.
    ///     </para>
    /// </remarks>
    /// <param name="value">The host object.</param>
    /// <param name="result">The converted value when the conversion succeeds; otherwise <c>undef</c>.</param>
    /// <returns><see langword="true"/> when the object could be converted.</returns>
    public static bool TryFromObject(object? value, out ScadValue result)
    {
        var budget = MaxConvertedElements;
        return TryConvert(value, 1, ref budget, out result);
    }

    /// <summary>
    ///     Converts one object, charging the shared element budget.
    /// </summary>
    /// <param name="value">The object.</param>
    /// <param name="depth">The nesting depth of this object (one for the root).</param>
    /// <param name="budget">The remaining number of values that may be converted.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><see langword="true"/> on success.</returns>
    private static bool TryConvert(object? value, int depth, ref int budget, out ScadValue result)
    {
        result = Undef;
        if (--budget < 0 || depth > MaxNestingDepth + 1)
        {
            return false;
        }

        if (TryConvertScalar(value, out result))
        {
            return true;
        }

        // Strings were handled as scalars above, so any remaining enumerable is a vector
        return value is IEnumerable sequence && TryConvertSequence(sequence, depth, ref budget, out result);
    }

    /// <summary>
    ///     Converts the non-collection kinds: null, booleans, strings and numbers.
    /// </summary>
    /// <param name="value">The object.</param>
    /// <param name="result">The converted value when the object is one of those kinds.</param>
    /// <returns><see langword="true"/> when the object was converted.</returns>
    private static bool TryConvertScalar(object? value, out ScadValue result)
    {
        switch (value)
        {
            case null:
                result = Undef;
                return true;
            case bool flag:
                result = FromBoolean(flag);
                return true;
            case string text:
                result = FromString(text);
                return true;
            default:
                var converted = TryConvertNumber(value, out var number);
                result = converted ? FromNumber(number) : Undef;
                return converted;
        }
    }

    /// <summary>
    ///     Converts a numeric host object to a double.
    /// </summary>
    /// <param name="value">The object.</param>
    /// <param name="number">The number when the object is numeric.</param>
    /// <returns><see langword="true"/> for the built-in integer and floating-point types and decimal.</returns>
    private static bool TryConvertNumber(object value, out double number)
    {
        switch (value)
        {
            case double d:
                number = d;
                return true;
            case float f:
                number = f;
                return true;
            case decimal m:
                number = (double)m;
                return true;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            default:
                number = 0;
                return false;
        }
    }

    /// <summary>
    ///     Converts an enumerable to a vector.
    /// </summary>
    /// <param name="sequence">The enumerable.</param>
    /// <param name="depth">The nesting depth of the enumerable.</param>
    /// <param name="budget">The remaining element budget.</param>
    /// <param name="result">The vector.</param>
    /// <returns><see langword="true"/> when every element converted within the limits.</returns>
    private static bool TryConvertSequence(IEnumerable sequence, int depth, ref int budget, out ScadValue result)
    {
        result = Undef;

        // A vector nested deeper than the language limit is rejected even when it is empty
        if (depth > MaxNestingDepth)
        {
            return false;
        }

        var items = new List<ScadValue>();
        foreach (var element in sequence)
        {
            // Every element is converted one level deeper; the budget also bounds endless sequences
            if (!TryConvert(element, depth + 1, ref budget, out var converted))
            {
                return false;
            }

            items.Add(converted);
        }

        result = FromVector(items.ToArray());
        return true;
    }
}
