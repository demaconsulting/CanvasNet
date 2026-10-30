using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for <see cref="AggregateMeasures"/> and <see cref="MeasureCell"/>: the operation-name registry (which names the
///     aggregating transforms accept, how they are spelled, and which operations need a field, a parameter or return a row) and the
///     arithmetic of every aggregate operation (hand-derived values, the classification of valid and missing values, IEEE edge cases,
///     the deterministic bootstrap and the charging of the work).
/// </summary>
public class AggregateMeasuresTests
{
    /// <summary>The aggregate operation names of Vega-Lite 6.5, as written in a specification.</summary>
    private static readonly string[] AggregateNames =
    [
        "count", "valid", "missing", "distinct", "sum", "product", "mean", "average", "variance", "variancep", "stdev", "stdevp",
        "stderr", "median", "q1", "q3", "ci0", "ci1", "min", "max", "argmin", "argmax", "values", "exponential", "exponentialb",
    ];

    /// <summary>The window-only operation names of Vega.</summary>
    private static readonly string[] WindowNames =
    [
        "row_number", "rank", "dense_rank", "percent_rank", "cume_dist", "ntile", "lag", "lead", "first_value", "last_value", "nth_value",
        "prev_value", "next_value",
    ];

    /// <summary>
    ///     Validates that every aggregate operation name parses, that the canonical name round-trips, and that <c>average</c> is the
    ///     alias of <c>mean</c>.
    /// </summary>
    [Fact]
    public void AggregateMeasures_TryParse_AcceptsEveryAggregateName()
    {
        // Arrange / Act / Assert: every name is accepted and the registry lists exactly these names
        Assert.Equal(AggregateNames.Order(StringComparer.Ordinal), AggregateMeasures.GetAggregateNames().Order(StringComparer.Ordinal));
        foreach (var name in AggregateNames)
        {
            Assert.True(AggregateMeasures.TryParse(name, out var op), name);
            Assert.Equal(name == "average" ? "mean" : name, AggregateMeasures.GetName(op));
            Assert.True(AggregateMeasures.TryParse(AggregateMeasures.GetName(op), out var again));
            Assert.Equal(op, again);
        }

        Assert.True(AggregateMeasures.TryParse("average", out var average));
        Assert.Equal(AggregateOp.Mean, average);
    }

    /// <summary>
    ///     Validates that every aggregate operation has its own canonical name (the registry covers the whole enumeration).
    /// </summary>
    [Fact]
    public void AggregateMeasures_GetName_IsDistinctForEveryOperation()
    {
        // Arrange / Act
        var names = Enum.GetValues<AggregateOp>().Select(o => AggregateMeasures.GetName(o)).ToList();

        // Assert: 24 operations (the 25 names minus the alias), each with a different name that parses back
        Assert.Equal(AggregateNames.Length - 1, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.True(AggregateMeasures.TryParse(name, out _)));
    }

    /// <summary>
    ///     Validates that every window-only operation name parses and round-trips, and that the aggregate names are not window names.
    /// </summary>
    [Fact]
    public void AggregateMeasures_TryParseWindow_AcceptsEveryWindowName()
    {
        // Arrange / Act / Assert
        Assert.Equal(WindowNames.Order(StringComparer.Ordinal), AggregateMeasures.GetWindowNames().Order(StringComparer.Ordinal));
        foreach (var name in WindowNames)
        {
            Assert.True(AggregateMeasures.TryParseWindow(name, out var op), name);
            Assert.Equal(name, AggregateMeasures.GetName(op));
            Assert.False(AggregateMeasures.TryParse(name, out _), name);
        }

        Assert.Equal(WindowNames.Length, Enum.GetValues<WindowOp>().Length);
        Assert.All(AggregateNames, name => Assert.False(AggregateMeasures.TryParseWindow(name, out _), name));
    }

    /// <summary>
    ///     Validates that unknown, wrongly capitalized, padded, empty and missing names are rejected by both lookups.
    /// </summary>
    /// <param name="name">The name.</param>
    [Theory]
    [InlineData("Sum")]
    [InlineData("SUM")]
    [InlineData(" sum")]
    [InlineData("sum ")]
    [InlineData("")]
    [InlineData("rowNumber")]
    [InlineData("row-number")]
    [InlineData("Rank")]
    [InlineData("ci2")]
    [InlineData("median2")]
    [InlineData(null)]
    public void AggregateMeasures_TryParse_RejectsUnknownNames(string? name)
    {
        // Arrange / Act / Assert
        Assert.False(AggregateMeasures.TryParse(name, out var aggregate));
        Assert.Equal(default, aggregate);
        Assert.False(AggregateMeasures.TryParseWindow(name, out var window));
        Assert.Equal(default, window);
    }

    /// <summary>
    ///     Validates which aggregate operations need a field: all but <c>count</c>.
    /// </summary>
    [Fact]
    public void AggregateMeasures_RequiresField_IsTrueForEveryAggregateButCount()
    {
        // Arrange / Act / Assert
        Assert.False(AggregateMeasures.RequiresField(AggregateOp.Count));
        Assert.All(Enum.GetValues<AggregateOp>().Where(o => o != AggregateOp.Count), op => Assert.True(AggregateMeasures.RequiresField(op), op.ToString()));
    }

    /// <summary>
    ///     Validates which window-only operations need a field: the offset and positional ones, not the ranking ones.
    /// </summary>
    [Fact]
    public void AggregateMeasures_RequiresField_WindowOperationsNeedAFieldOnlyToReadValues()
    {
        // Arrange
        var needs = new[] { WindowOp.Lag, WindowOp.Lead, WindowOp.FirstValue, WindowOp.LastValue, WindowOp.NthValue, WindowOp.PrevValue, WindowOp.NextValue };

        // Act / Assert
        foreach (var op in Enum.GetValues<WindowOp>())
        {
            Assert.Equal(needs.Contains(op), AggregateMeasures.RequiresField(op));
        }
    }

    /// <summary>
    ///     Validates the classification predicates: arg operations, exponential operations, window parameters and the operations that can
    ///     order the categories of a sort.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Predicates_ClassifyTheOperations()
    {
        // Arrange / Act / Assert: arg operations
        Assert.Equal(
            [AggregateOp.ArgMin, AggregateOp.ArgMax],
            Enum.GetValues<AggregateOp>().Where(AggregateMeasures.IsArgOp));

        // Assert: exponential operations
        Assert.Equal(
            [AggregateOp.Exponential, AggregateOp.ExponentialB],
            Enum.GetValues<AggregateOp>().Where(AggregateMeasures.IsExponential));

        // Assert: window operations that read a parameter
        Assert.Equal(
            [WindowOp.Ntile, WindowOp.Lag, WindowOp.Lead, WindowOp.NthValue],
            Enum.GetValues<WindowOp>().Where(AggregateMeasures.TakesParameter));

        // Assert: a sort can use every operation that yields a number or a comparable value
        Assert.Equal(
            Enum.GetValues<AggregateOp>().Where(o => o is not (AggregateOp.ArgMin or AggregateOp.ArgMax or AggregateOp.Exponential or AggregateOp.ExponentialB or AggregateOp.Values)),
            Enum.GetValues<AggregateOp>().Where(AggregateMeasures.CanOrderCategories));
    }

    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops promptly.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The sample whose statistics are worked out by hand: mean 5, sum of squared deviations 32.</summary>
    private static readonly double[] Sample = [2, 4, 4, 4, 5, 5, 7, 9];

    /// <summary>
    ///     Creates the environment a measure works in.
    /// </summary>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The environment.</returns>
    private static MeasureEnvironment Env(VegaLiteLimits? limits = null) => EnvWith(limits, Ct);

    /// <summary>
    ///     Creates the environment a measure works in, under a given token.
    /// </summary>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The environment.</returns>
    private static MeasureEnvironment EnvWith(VegaLiteLimits? limits, CancellationToken token)
    {
        var context = new ExpressionFunctionContext(limits, token);
        return new MeasureEnvironment(context, new WorkMeter(context));
    }

    /// <summary>
    ///     Creates a number value.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The value.</returns>
    private static DataValue Num(double number) => DataValue.FromNumber(number);

    /// <summary>
    ///     Creates number values.
    /// </summary>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The values.</returns>
    private static DataValue[] Nums(params double[] numbers) => [.. numbers.Select(Num)];

    /// <summary>
    ///     Feeds values to a measure and returns its result.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="values">The values in row order.</param>
    /// <param name="param">The weighting parameter.</param>
    /// <param name="seed">The bootstrap seed.</param>
    /// <param name="rowOf">Reads a row as an object for <c>argmin</c> and <c>argmax</c>.</param>
    /// <param name="env">The environment, or null for the defaults.</param>
    /// <returns>The result.</returns>
    private static DataValue Measure(
        AggregateOp op,
        IEnumerable<DataValue> values,
        double param = 0.5,
        ulong seed = 7,
        Func<int, DataValue>? rowOf = null,
        MeasureEnvironment? env = null)
    {
        env ??= Env();
        var cell = new MeasureCell();
        var row = 0;
        foreach (var value in values)
        {
            cell.Add(op, param, value, row++, env);
        }

        return cell.Result(op, param, seed, env, rowOf);
    }

    /// <summary>
    ///     Validates every numeric operation over the sample 2, 4, 4, 4, 5, 5, 7, 9 against the values worked out by hand:
    ///     sum 40, product 201,600, mean 5, variance 32/7, population variance 4, quartiles 4, 4.5 and 5.5.
    /// </summary>
    /// <param name="name">The name of the operation.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("count", 8)]
    [InlineData("valid", 8)]
    [InlineData("missing", 0)]
    [InlineData("distinct", 5)]
    [InlineData("sum", 40)]
    [InlineData("product", 201_600)]
    [InlineData("mean", 5)]
    [InlineData("variance", 4.57143)]
    [InlineData("variancep", 4)]
    [InlineData("stdev", 2.13809)]
    [InlineData("stdevp", 2)]
    [InlineData("stderr", 0.75593)]
    [InlineData("median", 4.5)]
    [InlineData("q1", 4)]
    [InlineData("q3", 5.5)]
    [InlineData("min", 2)]
    [InlineData("max", 9)]
    public void AggregateMeasures_Cell_MatchesTheHandDerivedStatistics(string name, double expected)
    {
        // Arrange / Act
        Assert.True(AggregateMeasures.TryParse(name, out var op));
        var result = Measure(op, Nums(Sample));

        // Assert
        Assert.True(result.IsNumber, name);
        Assert.Equal(expected, result.Number, 5);
    }

    /// <summary>
    ///     Validates that null and the empty text are missing, that NaN is neither missing nor valid, and that count and distinct see
    ///     every row while the other operations see the valid values only.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_ClassifiesValidMissingAndNaNValues()
    {
        // Arrange: one valid number, null, the empty text and NaN
        DataValue[] values = [default, DataValue.FromString(string.Empty), Num(double.NaN), Num(3)];

        // Act / Assert
        Assert.Equal(4, Measure(AggregateOp.Count, values).Number);
        Assert.Equal(1, Measure(AggregateOp.Valid, values).Number);
        Assert.Equal(2, Measure(AggregateOp.Missing, values).Number);
        Assert.Equal(4, Measure(AggregateOp.Distinct, values).Number);
        Assert.Equal(3, Measure(AggregateOp.Sum, values).Number);
        Assert.Equal(3, Measure(AggregateOp.Mean, values).Number);
        Assert.Equal(3, Measure(AggregateOp.Median, values).Number);
        Assert.Equal(3, Measure(AggregateOp.Min, values).Number);
        Assert.Equal(3, Measure(AggregateOp.Max, values).Number);
        Assert.Equal(3, Measure(AggregateOp.Product, values).Number);
        Assert.True(Measure(AggregateOp.Variance, values).IsNull);
        Assert.True(Measure(AggregateOp.Stdev, values).IsNull);
        Assert.True(Measure(AggregateOp.Stderr, values).IsNull);

        // The population forms need one valid value (Vega's m.valid ? ... : undefined), so a single value has no spread
        Assert.Equal(0, Measure(AggregateOp.VarianceP, values).Number);
        Assert.Equal(0, Measure(AggregateOp.StdevP, values).Number);
    }

    /// <summary>
    ///     Validates that the population variance and deviation are defined from one valid value (zero) and null without one,
    ///     while the sample forms need two.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_PopulationSpreadOfSingleValueIsZero()
    {
        // Arrange
        DataValue[] single = [Num(7)];
        DataValue[] none = [default];

        // Act / Assert
        Assert.Equal(0, Measure(AggregateOp.VarianceP, single).Number);
        Assert.Equal(0, Measure(AggregateOp.StdevP, single).Number);
        Assert.True(Measure(AggregateOp.VarianceP, none).IsNull);
        Assert.True(Measure(AggregateOp.StdevP, none).IsNull);
        Assert.True(Measure(AggregateOp.Variance, single).IsNull);
        Assert.True(Measure(AggregateOp.Stdev, single).IsNull);
        Assert.True(Measure(AggregateOp.Stderr, single).IsNull);
    }

    /// <summary>
    ///     Validates the operations over no valid value: counts are zero and everything else is null.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_WithoutValidValuesCountsZeroAndOtherwiseIsNull()
    {
        // Arrange
        DataValue[] none = [default, DataValue.FromString(string.Empty)];

        // Act / Assert
        Assert.Equal(0, Measure(AggregateOp.Valid, none).Number);
        Assert.Equal(2, Measure(AggregateOp.Missing, none).Number);
        foreach (var op in new[]
                 {
                     AggregateOp.Sum, AggregateOp.Product, AggregateOp.Mean, AggregateOp.Median, AggregateOp.Q1, AggregateOp.Q3,
                     AggregateOp.Ci0, AggregateOp.Ci1, AggregateOp.Min, AggregateOp.Max, AggregateOp.ArgMin, AggregateOp.ArgMax,
                     AggregateOp.Exponential, AggregateOp.ExponentialB,
                 })
        {
            Assert.True(Measure(op, none, rowOf: row => Num(row)).IsNull, op.ToString());
        }

        Assert.Equal(0, new MeasureCell().Valid);
        Assert.Equal(0, new MeasureCell().Count);
        Assert.Empty(Measure(AggregateOp.Values, []).Elements.ToArray());
        Assert.Equal(0, Measure(AggregateOp.Distinct, []).Number);
    }

    /// <summary>
    ///     Validates that the variance is accumulated with Welford's update, which keeps the exact value of values with a large
    ///     common offset (a naive sum of squares would lose it): the deviations of 4, 7, 13 and 16 around 10 give 90.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_VarianceIsNumericallyStable()
    {
        // Arrange
        var values = Nums(1e9 + 4, 1e9 + 7, 1e9 + 13, 1e9 + 16);

        // Act
        var variance = Measure(AggregateOp.Variance, values).Number;
        var variancep = Measure(AggregateOp.VarianceP, values).Number;
        var mean = Measure(AggregateOp.Mean, values).Number;

        // Assert: the mean is 1e9 + 10; 90 / 3 = 30 and 90 / 4 = 22.5
        Assert.Equal(1e9 + 10, mean, 6);
        Assert.Equal(30, variance, 6);
        Assert.Equal(22.5, variancep, 6);
    }

    /// <summary>
    ///     Validates the quartiles by linear interpolation (R-7) on small samples, whatever the order of the rows.
    /// </summary>
    /// <param name="values">The values in row order.</param>
    /// <param name="q1">The first quartile.</param>
    /// <param name="median">The median.</param>
    /// <param name="q3">The third quartile.</param>
    [Theory]
    [InlineData(new double[] { 1, 2, 3, 4, 5 }, 2, 3, 4)]
    [InlineData(new double[] { 1, 2 }, 1.25, 1.5, 1.75)]
    [InlineData(new double[] { 7 }, 7, 7, 7)]
    [InlineData(new double[] { 9, 1, 5, 3 }, 2.5, 4, 6)]
    public void AggregateMeasures_Cell_QuartilesInterpolateBetweenRanks(double[] values, double q1, double median, double q3)
    {
        // Arrange / Act / Assert
        Assert.Equal(q1, Measure(AggregateOp.Q1, Nums(values)).Number, 9);
        Assert.Equal(median, Measure(AggregateOp.Median, Nums(values)).Number, 9);
        Assert.Equal(q3, Measure(AggregateOp.Q3, Nums(values)).Number, 9);
    }

    /// <summary>
    ///     Validates that text that is not a number is a valid value that poisons a sum, while the order statistics leave it out
    ///     (the quantile of d3 ignores NaN).
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_NumericTextIsReadAsANumber()
    {
        // Arrange
        DataValue[] numeric = [DataValue.FromString("1"), DataValue.FromString(" 2 "), DataValue.FromBoolean(true), DataValue.FromDate(4)];
        DataValue[] poisoned = [Num(1), DataValue.FromString("abc"), Num(3)];

        // Act / Assert: "1" + 2 + true (1) + the date 4 (its milliseconds)
        Assert.Equal(8, Measure(AggregateOp.Sum, numeric).Number);
        Assert.Equal(3, Measure(AggregateOp.Valid, poisoned).Number);
        Assert.True(double.IsNaN(Measure(AggregateOp.Sum, poisoned).Number));
        Assert.True(double.IsNaN(Measure(AggregateOp.Mean, poisoned).Number));
        Assert.Equal(2, Measure(AggregateOp.Median, poisoned).Number);
    }

    /// <summary>
    ///     Validates that sums and products follow IEEE 754: they overflow to infinity and an infinity is kept.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_OverflowGivesInfinity()
    {
        // Arrange
        var big = Nums(1.7e308, 1.7e308);

        // Act / Assert
        Assert.Equal(double.PositiveInfinity, Measure(AggregateOp.Sum, big).Number);
        Assert.Equal(double.PositiveInfinity, Measure(AggregateOp.Product, big).Number);
        Assert.Equal(double.NegativeInfinity, Measure(AggregateOp.Min, Nums(double.NegativeInfinity, 1)).Number);
        Assert.Equal(double.PositiveInfinity, Measure(AggregateOp.Max, Nums(double.PositiveInfinity, 1)).Number);
        Assert.Equal(double.PositiveInfinity, Measure(AggregateOp.Sum, Nums(double.PositiveInfinity, 5)).Number);
    }

    /// <summary>
    ///     Validates that min and max compare like JavaScript's relational operators and return the original value: numbers
    ///     numerically, texts by code unit (so "10" is below "9"), dates by instant, and mixed kinds by conversion to numbers.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_MinAndMaxCompareLikeJavaScript()
    {
        // Arrange
        DataValue[] texts = [DataValue.FromString("10"), DataValue.FromString("9"), DataValue.FromString("2")];
        DataValue[] words = [DataValue.FromString("b"), DataValue.FromString("a"), DataValue.FromString("c")];
        DataValue[] dates = [DataValue.FromDate(5000), DataValue.FromDate(1000), DataValue.FromDate(3000)];
        DataValue[] mixed = [Num(5), DataValue.FromString("20"), Num(3)];

        // Act / Assert
        Assert.Equal("10", Measure(AggregateOp.Min, texts).Text);
        Assert.Equal("9", Measure(AggregateOp.Max, texts).Text);
        Assert.Equal("a", Measure(AggregateOp.Min, words).Text);
        Assert.Equal("c", Measure(AggregateOp.Max, words).Text);
        Assert.True(Measure(AggregateOp.Min, dates).IsDate);
        Assert.Equal(1000, Measure(AggregateOp.Min, dates).Milliseconds);
        Assert.Equal(5000, Measure(AggregateOp.Max, dates).Milliseconds);
        Assert.Equal(3, Measure(AggregateOp.Min, mixed).Number);
        Assert.Equal("20", Measure(AggregateOp.Max, mixed).Text);
    }

    /// <summary>
    ///     Validates that argmin and argmax return the row that holds the extreme value, the first one of a tie, through the row reader.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_ArgMinAndArgMaxKeepTheFirstRowOfATie()
    {
        // Arrange: the minimum 3 is in rows 1 and 2, the maximum 9 in rows 3 and 4
        var values = Nums(5, 3, 3, 9, 9);

        // Act
        var argMin = Measure(AggregateOp.ArgMin, values, rowOf: row => Num(row));
        var argMax = Measure(AggregateOp.ArgMax, values, rowOf: row => Num(row));
        var withoutReader = Measure(AggregateOp.ArgMax, values);

        // Assert
        Assert.Equal(1, argMin.Number);
        Assert.Equal(3, argMax.Number);
        Assert.True(withoutReader.IsNull);
    }

    /// <summary>
    ///     Validates the exponential operations: for r = 0.5 over 1, 2, 3 the weighted sum is 3 + 2·0.5 + 1·0.25 = 4.25, the biased
    ///     form multiplies it by 1 − r, the unbiased form divides by the sum of the weights 1.75; at r = 1 the weights are equal.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_ExponentialOperationsWeightRecentValues()
    {
        // Arrange
        var values = Nums(1, 2, 3);

        // Act / Assert
        Assert.Equal(17.0 / 7, Measure(AggregateOp.Exponential, values, 0.5).Number, 9);
        Assert.Equal(2.125, Measure(AggregateOp.ExponentialB, values, 0.5).Number, 9);
        Assert.Equal(2, Measure(AggregateOp.Exponential, values, 1).Number, 9);
        Assert.Equal(0, Measure(AggregateOp.ExponentialB, values, 1).Number, 9);
        Assert.Equal(3, Measure(AggregateOp.Exponential, Nums(3), 0.25).Number, 9);
    }

    /// <summary>
    ///     Validates that distinct counts the texts of the values (so 1 and "1" are the same) and that values keeps every value,
    ///     missing ones included, in row order.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_DistinctCountsTextsAndValuesKeepsEveryValue()
    {
        // Arrange
        DataValue[] values = [Num(1), DataValue.FromString("1"), Num(1.0), DataValue.FromBoolean(true), default, default];

        // Act
        var distinct = Measure(AggregateOp.Distinct, values);
        var kept = Measure(AggregateOp.Values, values);

        // Assert: "1", "1", "1", "true", "null", "null" are three different texts
        Assert.Equal(3, distinct.Number);
        Assert.True(kept.IsArray);
        Assert.Equal(6, kept.Elements.Length);
        Assert.Equal(1, kept.Elements[0].Number);
        Assert.Equal("1", kept.Elements[1].Text);
        Assert.True(kept.Elements[4].IsNull);
    }

    /// <summary>
    ///     Validates the bootstrap interval of the mean: it is deterministic for a seed, brackets the mean inside the range of the
    ///     values, has ci0 not above ci1, collapses for constant values, and changes with the seed.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_BootstrapCiIsDeterministicAndBounded()
    {
        // Arrange
        var values = Nums(10, 20, 30, 40, 50);

        // Act
        var low = Measure(AggregateOp.Ci0, values, seed: 11).Number;
        var high = Measure(AggregateOp.Ci1, values, seed: 11).Number;
        var again = Measure(AggregateOp.Ci0, values, seed: 11).Number;
        double[] spread = [1.5, 2.25, 9.75, 4, 8, 0.3, 77, 5.5, 6.125, 3.3];
        var first = Measure(AggregateOp.Ci0, Nums(spread), seed: 11).Number;
        var second = Measure(AggregateOp.Ci0, Nums(spread), seed: 12).Number;
        var constant = Nums(4, 4, 4);

        // Assert: the mean of the values is 30 and every resample mean lies between 10 and 50
        Assert.Equal(low, again);
        Assert.NotEqual(first, second);
        Assert.InRange(low, 10, 30);
        Assert.InRange(high, 30, 50);
        Assert.True(low <= high);
        Assert.Equal(4, Measure(AggregateOp.Ci0, constant).Number);
        Assert.Equal(4, Measure(AggregateOp.Ci1, constant).Number);
        Assert.Equal(7, Measure(AggregateOp.Ci0, Nums(7)).Number);
    }

    /// <summary>
    ///     Validates that the bootstrap does not depend on the culture of the thread.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_BootstrapCiIgnoresTheCulture()
    {
        // Arrange
        var values = Nums(1.5, 2.5, 9.25, 4, 8);
        var expected = Measure(AggregateOp.Ci1, values, seed: 3).Number;

        // Act
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", () => Measure(AggregateOp.Ci1, values, seed: 3).Number);
        var german = ExpressionTestSupport.UnderCulture("de-DE", () => Measure(AggregateOp.Ci1, values, seed: 3).Number);

        // Assert
        Assert.Equal(expected, turkish);
        Assert.Equal(expected, german);
    }

    /// <summary>
    ///     Validates that the group number is mixed into the seed, so two groups draw different resamples, and that the same group
    ///     and transform always give the same seed.
    /// </summary>
    [Fact]
    public void AggregateMeasures_GroupSeed_DiffersByGroupAndIsStable()
    {
        // Arrange / Act / Assert
        Assert.Equal(MeasureCell.GroupSeed(5, 0), MeasureCell.GroupSeed(5, 0));
        Assert.NotEqual(MeasureCell.GroupSeed(5, 0), MeasureCell.GroupSeed(5, 1));
        Assert.NotEqual(MeasureCell.GroupSeed(5, 0), MeasureCell.GroupSeed(6, 0));
    }

    /// <summary>
    ///     Validates the quantile function (R-7): the ends, the interpolation, and the empty and NaN cases.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Quantile_InterpolatesLikeD3()
    {
        // Arrange
        double[] sorted = [10, 20, 30, 40];

        // Act / Assert: position (4 - 1) * 0.5 = 1.5 is halfway between 20 and 30
        Assert.Equal(25, AggregateMeasures.Quantile(sorted, 0.5), 9);
        Assert.Equal(10, AggregateMeasures.Quantile(sorted, 0), 9);
        Assert.Equal(10, AggregateMeasures.Quantile(sorted, -1), 9);
        Assert.Equal(40, AggregateMeasures.Quantile(sorted, 1), 9);
        Assert.Equal(40, AggregateMeasures.Quantile(sorted, 2), 9);
        Assert.Equal(17.5, AggregateMeasures.Quantile(sorted, 0.25), 9);
        Assert.Equal(5, AggregateMeasures.Quantile([5], 0.9), 9);
        Assert.True(double.IsNaN(AggregateMeasures.Quantile([], 0.5)));
        Assert.True(double.IsNaN(AggregateMeasures.Quantile(sorted, double.NaN)));
    }

    /// <summary>
    ///     Validates the default output names: <c>count</c> without a field, otherwise the canonical operation name, an underscore and the field.
    /// </summary>
    [Fact]
    public void AggregateMeasures_DefaultName_FollowsOpUnderscoreField()
    {
        // Arrange / Act / Assert
        Assert.Equal("count", AggregateSpec.DefaultName(AggregateOp.Count, null));
        Assert.Equal("sum_price", AggregateSpec.DefaultName(AggregateOp.Sum, "price"));
        Assert.Equal("mean_a.b", AggregateSpec.DefaultName(AggregateOp.Mean, "a.b"));
        Assert.Equal("argmax_x", AggregateSpec.DefaultName(AggregateOp.ArgMax, "x"));
        Assert.Equal(4, MeasureCell.StateCells);
        Assert.Equal(
            [AggregateOp.Distinct, AggregateOp.Median, AggregateOp.Q1, AggregateOp.Q3, AggregateOp.Ci0, AggregateOp.Ci1, AggregateOp.Values],
            Enum.GetValues<AggregateOp>().Where(MeasureCell.StoresValues).Order());
    }

    /// <summary>
    ///     Validates that the work of the operations is charged to the step budget: sorting for the quartiles, the bootstrap,
    ///     long distinct keys and the conversion of long text to a number all fail when the budget is too small, with the limit named.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_WorkIsChargedToTheStepBudget()
    {
        // Arrange: 100,000 values cost about 1,600 steps to keep and 26,000 to sort; 100 values cost 1,562 steps to bootstrap
        var many = Enumerable.Range(0, 100_000).Select(i => Num(i)).ToArray();
        var few = Enumerable.Range(0, 100).Select(i => Num(i)).ToArray();
        var longText = DataValue.FromString(new string('7', 64_000));
        var limits = new VegaLiteLimits { MaxEvaluationSteps = 10_000 };
        var small = new VegaLiteLimits { MaxEvaluationSteps = 500 };

        // Act
        var sort = Assert.Throws<ExpressionRuntimeException>(() => Measure(AggregateOp.Median, many, env: Env(limits)));
        var bootstrap = Assert.Throws<ExpressionRuntimeException>(() => Measure(AggregateOp.Ci0, few, env: Env(small)));
        var distinct = Assert.Throws<ExpressionRuntimeException>(() => Measure(AggregateOp.Distinct, [longText], env: Env(small)));
        var number = Assert.Throws<ExpressionRuntimeException>(() => Measure(AggregateOp.Sum, [longText], env: Env(small)));
        var fine = Measure(AggregateOp.Sum, few, env: Env(small));

        // Assert
        Assert.All(new[] { sort, bootstrap, distinct, number }, ex => Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", ex.Message, StringComparison.Ordinal));
        Assert.Equal(4950, fine.Number);
    }

    /// <summary>
    ///     Validates that a text longer than the string limit is rejected by the operations that convert values to text or compare them.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_LongTextRespectsTheStringLimit()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 10 };
        var text = DataValue.FromString("0123456789a");

        // Act
        var exception = Assert.Throws<ExpressionRuntimeException>(() => Measure(AggregateOp.Distinct, [text], env: Env(limits)));

        // Assert
        Assert.Contains("VegaLiteLimits.MaxStringLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the bootstrap polls the cancellation token.
    /// </summary>
    [Fact]
    public void AggregateMeasures_Cell_BootstrapPollsCancellation()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var env = EnvWith(null, source.Token);
        var cell = new MeasureCell();
        cell.Add(AggregateOp.Ci0, 0.5, Num(1), 0, env);
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => cell.Result(AggregateOp.Ci0, 0.5, 1, env, null));
    }
}
