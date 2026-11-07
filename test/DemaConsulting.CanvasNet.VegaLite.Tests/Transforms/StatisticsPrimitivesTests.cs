using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the JavaScript-faithful building blocks of the statistical transforms: the validity rules of vega-statistics' <c>numbers</c>, the quantiles, d3-array's
///     <c>deviation</c>, vega-util's <c>extent</c> and the arithmetic helpers.
/// </summary>
public class StatisticsPrimitivesTests
{
    /// <summary>The longest text the extent comparison may build in these tests.</summary>
    private const int MaxLength = 1000;

    /// <summary>
    ///     Validates the truthiness of a number: zero and NaN are falsy, everything else (negative numbers and infinities included) is truthy.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_IsTruthy_ZeroAndNaNAreFalsy()
    {
        // Arrange / Act / Assert
        Assert.False(StatisticsPrimitives.IsTruthy(0));
        Assert.False(StatisticsPrimitives.IsTruthy(-0.0));
        Assert.False(StatisticsPrimitives.IsTruthy(double.NaN));
        Assert.True(StatisticsPrimitives.IsTruthy(-1e-300));
        Assert.True(StatisticsPrimitives.IsTruthy(double.NegativeInfinity));
    }

    /// <summary>
    ///     Validates that the minimum and the maximum of JavaScript propagate NaN, unlike the minimum of .NET for one operand only.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_JsMinAndJsMax_PropagateNaN()
    {
        // Arrange / Act / Assert
        Assert.Equal(2, StatisticsPrimitives.JsMin(2, 3));
        Assert.Equal(3, StatisticsPrimitives.JsMax(2, 3));
        Assert.True(double.IsNaN(StatisticsPrimitives.JsMin(double.NaN, 3)));
        Assert.True(double.IsNaN(StatisticsPrimitives.JsMax(2, double.NaN)));
    }

    /// <summary>
    ///     Validates the <c>numbers</c> rule: null, the empty text and NaN are dropped; numeric texts, booleans and dates are converted.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_Numbers_DropsNullEmptyAndNaNAndConvertsTheRest()
    {
        // Arrange
        DataValue[] values =
        [
            DataValue.FromNumber(1), default, DataValue.FromString(""), DataValue.FromString("x"), DataValue.FromString("7"),
            DataValue.FromBoolean(true), DataValue.FromNumber(double.NaN), DataValue.FromDate(86400000), DataValue.FromNumber(-2),
        ];

        // Act
        var numbers = StatisticsPrimitives.Numbers(values);

        // Assert
        Assert.Equal([1.0, 7.0, 1.0, 86400000.0, -2.0], numbers);
    }

    /// <summary>
    ///     Validates the quantiles of the numbers 1 to 10 (the R-7 method has the closed form <c>1 + 9p</c> for these values) and the edges: a probability at or beyond the ends, NaN,
    ///     and a list without numbers.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_Quantiles_AreLinearInterpolationOfTheSortedNumbers()
    {
        // Arrange: shuffled 1..10 with values that are not numbers mixed in
        DataValue[] values = [.. new double[] { 10, 3, 7, 1, 9, 2, 8, 4, 6, 5 }.Select(DataValue.FromNumber), default, DataValue.FromString("n/a")];

        // Act
        var quantiles = StatisticsPrimitives.Quantiles(values, [0, 0.1, 0.5, 0.9, 1, 1.5, -1, double.NaN]);
        var none = StatisticsPrimitives.Quantiles([default, DataValue.FromString("")], [0.5]);

        // Assert: 1 + 9p, clamped outside [0, 1]; NaN and the empty list have no quantile (JavaScript undefined)
        Assert.Equal(1, quantiles[0], 12);
        Assert.Equal(1.9, quantiles[1], 12);
        Assert.Equal(5.5, quantiles[2], 12);
        Assert.Equal(9.1, quantiles[3], 12);
        Assert.Equal(10, quantiles[4], 12);
        Assert.Equal(10, quantiles[5], 12);
        Assert.Equal(1, quantiles[6], 12);
        Assert.True(double.IsNaN(quantiles[7]));
        Assert.True(double.IsNaN(none[0]));
    }

    /// <summary>
    ///     Validates d3's deviation: the sample standard deviation (the closed form for 1, 2, 4, 7 is the square root of 7), the empty text counting as the number zero, and
    ///     the undefined result for fewer than two values.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_Deviation_IsTheSampleDeviationOfTheNonNullNumbers()
    {
        // Arrange / Act
        var four = StatisticsPrimitives.Deviation([.. new double[] { 1, 2, 4, 7 }.Select(DataValue.FromNumber)]);
        var withEmptyText = StatisticsPrimitives.Deviation([DataValue.FromNumber(2), DataValue.FromString(""), default, DataValue.FromString("x")]);
        var one = StatisticsPrimitives.Deviation([DataValue.FromNumber(5)]);
        var constant = StatisticsPrimitives.Deviation([DataValue.FromNumber(3), DataValue.FromNumber(3)]);

        // Assert: the mean of 1, 2, 4, 7 is 3.5 and the squared deviations sum to 21, so the variance is 7
        Assert.Equal(Math.Sqrt(7), four, 12);

        // Assert: 2 and the empty text (zero) have the mean 1 and the variance 2; null and a text that is not a number are not counted
        Assert.Equal(Math.Sqrt(2), withEmptyText, 12);
        Assert.True(double.IsNaN(one));
        Assert.Equal(0, constant);
    }

    /// <summary>
    ///     Validates vega-util's extent: it starts at the first value that is neither null nor NaN, compares like JavaScript (numbers numerically, texts lexically), and has no extent
    ///     for a list without such a value.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_Extent_FollowsJavaScriptComparison()
    {
        // Arrange
        DataValue[] numbers = [default, DataValue.FromNumber(double.NaN), DataValue.FromNumber(3), DataValue.FromNumber(-2), default, DataValue.FromNumber(9)];
        DataValue[] texts = [DataValue.FromString("pear"), DataValue.FromString("apple"), DataValue.FromString("zebra")];
        DataValue[] dates = [DataValue.FromDate(2000), DataValue.FromDate(1000)];

        // Act
        var numeric = StatisticsPrimitives.Extent(numbers, MaxLength);
        var lexical = StatisticsPrimitives.Extent(texts, MaxLength);
        var temporal = StatisticsPrimitives.Extent(dates, MaxLength);
        var empty = StatisticsPrimitives.Extent([], MaxLength);
        var allInvalid = StatisticsPrimitives.Extent([default, DataValue.FromNumber(double.NaN)], MaxLength);

        // Assert
        Assert.Equal((-2.0, 9.0), (numeric!.Value.Min.Number, numeric.Value.Max.Number));
        Assert.Equal(("apple", "zebra"), (lexical!.Value.Min.Text, lexical.Value.Max.Text));
        Assert.Equal((1000.0, 2000.0), (temporal!.Value.Min.Milliseconds, temporal.Value.Max.Milliseconds));
        Assert.Null(empty);
        Assert.Null(allInvalid);
    }

    /// <summary>
    ///     Validates the length of d3's <c>range(start, stop, step)</c>: 100 values for the default quantile step, none for a negative step or an empty interval, and zero for NaN.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_RangeCount_MatchesTheLengthOfD3Range()
    {
        // Arrange / Act / Assert
        Assert.Equal(100, StatisticsPrimitives.RangeCount(0.005, 1 - 1e-14, 0.01));
        Assert.Equal(4, StatisticsPrimitives.RangeCount(0.125, 1 - 1e-14, 0.25));
        Assert.Equal(3, StatisticsPrimitives.RangeCount(0.15, 1 - 1e-14, 0.3));
        Assert.Equal(0, StatisticsPrimitives.RangeCount(0.5, 1, -0.1));
        Assert.Equal(0, StatisticsPrimitives.RangeCount(1, 0.5, 0.1));
        Assert.Equal(0, StatisticsPrimitives.RangeCount(0, 1, double.NaN));
        Assert.Equal(long.MaxValue, StatisticsPrimitives.RangeCount(0, 1, 0));
    }

    /// <summary>
    ///     Validates that the statistics helpers reject a null argument.
    /// </summary>
    [Fact]
    public void StatisticsPrimitives_NullArguments_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => StatisticsPrimitives.Numbers(null!));
        Assert.Throws<ArgumentNullException>(() => StatisticsPrimitives.Quantiles([], null!));
        Assert.Throws<ArgumentNullException>(() => StatisticsPrimitives.Deviation(null!));
        Assert.Throws<ArgumentNullException>(() => StatisticsPrimitives.Extent(null!, MaxLength));
    }
}
