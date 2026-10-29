using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the operation-name registry of <see cref="AggregateMeasures"/>: which names the aggregating transforms accept,
///     how they are spelled, and which operations need a field, a parameter or return a row.
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
}
