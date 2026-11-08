namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     Conversions from <see cref="double"/> to integers that give the same answer on every runtime.
/// </summary>
/// <remarks>
///     <para>
///         A C# cast of a <see cref="double"/> that is NaN or does not fit the integer type is not defined by the language:
///         .NET 8 on x64 gives <see cref="int.MinValue"/> (or <see cref="long.MinValue"/>), .NET 9 and later saturate, and a
///         <see langword="checked"/> context throws <see cref="OverflowException"/>. A specification can steer numbers
///         into those ranges (a step of <c>1e-300</c>, a <c>limit</c> of <c>1e308</c>, a scale exponent of <c>1e300</c>), so every
///         place that converts a number that comes from the specification or from its data uses these helpers instead of a cast: the
///         result is the same on every runtime and every platform, and an out-of-range value can never wrap into a negative count.
///     </para>
///     <para>
///         The rule is saturate, truncate toward zero, and treat NaN as zero: a value above the range gives the largest integer,
///         a value below it gives the smallest, and the callers that need a tighter bound (a row budget, a size limit) apply it to
///         the saturated value before they allocate anything.
///     </para>
/// </remarks>
internal static class NumericConversions
{
    /// <summary>The smallest <see cref="double"/> above every <see cref="int"/> (2^31); the largest <see cref="int"/> is exactly representable, so this is exclusive.</summary>
    private const double Int32Ceiling = 2147483648.0;

    /// <summary>The smallest <see cref="double"/> above every <see cref="long"/> (2^63); <see cref="long.MaxValue"/> itself is not representable.</summary>
    private const double Int64Ceiling = 9223372036854775808.0;

    /// <summary>
    ///     Converts a number to a 32-bit integer by truncating toward zero and saturating at the range of the type.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>
    ///     The truncated value; <see cref="int.MaxValue"/> or <see cref="int.MinValue"/> when it is out of range
    ///     (including the infinities); zero for NaN.
    /// </returns>
    public static int ToInt32Saturating(double value)
    {
        // NaN compares false with everything, so it is handled first; the range checks then make the cast defined
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value >= Int32Ceiling)
        {
            return int.MaxValue;
        }

        return value <= int.MinValue ? int.MinValue : (int)value;
    }

    /// <summary>
    ///     Converts a number to a 64-bit integer by truncating toward zero and saturating at the range of the type.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>
    ///     The truncated value; <see cref="long.MaxValue"/> or <see cref="long.MinValue"/> when it is out of range
    ///     (including the infinities); zero for NaN.
    /// </returns>
    public static long ToInt64Saturating(double value)
    {
        // NaN compares false with everything, so it is handled first; the range checks then make the cast defined
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value >= Int64Ceiling)
        {
            return long.MaxValue;
        }

        return value <= long.MinValue ? long.MinValue : (long)value;
    }
}
