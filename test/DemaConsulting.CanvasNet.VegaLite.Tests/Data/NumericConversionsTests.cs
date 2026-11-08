using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests for <see cref="NumericConversions"/>: the saturating, NaN-defined conversions from <see cref="double"/> to integers that replace the casts whose result for a value that does not fit
///     differs between .NET 8 (the minimum value) and .NET 9 and later (saturation).
/// </summary>
public class NumericConversionsTests
{
    /// <summary>
    ///     Validates the 32-bit conversion: values in range are truncated toward zero, values beyond the range (and the infinities) saturate, and NaN is zero.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="expected">The expected integer.</param>
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(1.9, 1)]
    [InlineData(-1.9, -1)]
    [InlineData(2147483647.0, int.MaxValue)]
    [InlineData(2147483647.9, int.MaxValue)]
    [InlineData(2147483648.0, int.MaxValue)]
    [InlineData(4294967296.0, int.MaxValue)]
    [InlineData(1e300, int.MaxValue)]
    [InlineData(double.MaxValue, int.MaxValue)]
    [InlineData(double.PositiveInfinity, int.MaxValue)]
    [InlineData(-2147483648.0, int.MinValue)]
    [InlineData(-2147483649.0, int.MinValue)]
    [InlineData(-1e300, int.MinValue)]
    [InlineData(double.MinValue, int.MinValue)]
    [InlineData(double.NegativeInfinity, int.MinValue)]
    [InlineData(double.NaN, 0)]
    [InlineData(5e-324, 0)]
    public void NumericConversions_ToInt32Saturating_TruncatesSaturatesAndMapsNaNToZero(double value, int expected)
    {
        // Arrange / Act
        var result = NumericConversions.ToInt32Saturating(value);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Validates the 64-bit conversion: values in range are truncated toward zero, 2^63 and beyond saturate to the largest value (the largest long is not representable as a double, so 2^63 is the first
    ///     value out of range), values below the range saturate to the smallest, and NaN is zero.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="expected">The expected integer.</param>
    [Theory]
    [InlineData(0.0, 0L)]
    [InlineData(3.99, 3L)]
    [InlineData(-3.99, -3L)]
    [InlineData(4294967296.0, 4294967296L)]
    [InlineData(9007199254740992.0, 9007199254740992L)]
    [InlineData(9.2233720368547748E+18, 9223372036854774784L)]
    [InlineData(9.2233720368547758E+18, long.MaxValue)]
    [InlineData(9.3e18, long.MaxValue)]
    [InlineData(1e300, long.MaxValue)]
    [InlineData(double.MaxValue, long.MaxValue)]
    [InlineData(double.PositiveInfinity, long.MaxValue)]
    [InlineData(-9.2233720368547758E+18, long.MinValue)]
    [InlineData(-9.3e18, long.MinValue)]
    [InlineData(-1e300, long.MinValue)]
    [InlineData(double.NegativeInfinity, long.MinValue)]
    [InlineData(double.NaN, 0L)]
    public void NumericConversions_ToInt64Saturating_TruncatesSaturatesAndMapsNaNToZero(double value, long expected)
    {
        // Arrange / Act
        var result = NumericConversions.ToInt64Saturating(value);

        // Assert
        Assert.Equal(expected, result);
    }
}
