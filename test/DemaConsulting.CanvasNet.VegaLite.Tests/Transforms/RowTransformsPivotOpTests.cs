using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the aggregate operations of the <c>pivot</c> transform: every operation of Vega's aggregate engine is available, the cell of a group and a pivot value
///     holds the rows of that value, a combination without rows has the empty result of the operation (null for a sum, 0 for a count), and the quirks of Vega's pivot (the count of
///     the values that are not NaN, a distinct count that sees the NaN of the other columns, the NaN of the exponential operations) are reproduced. The expected cells are derived by hand
///     from the rows below and agree with the reference implementation.
/// </summary>
public class RowTransformsPivotOpTests
{
    /// <summary>
    ///     The rows: group a has x = 1, 4 and the empty text, and y = 10, null and the text "q"; group b has x = 2, 6 and 8; group c has y = 3.
    /// </summary>
    private const string Rows =
        "[{\"g\":\"a\",\"k\":\"x\",\"v\":1},{\"g\":\"a\",\"k\":\"x\",\"v\":4},{\"g\":\"a\",\"k\":\"y\",\"v\":10},{\"g\":\"a\",\"k\":\"y\",\"v\":null},{\"g\":\"a\",\"k\":\"y\",\"v\":\"q\"},"
        + "{\"g\":\"b\",\"k\":\"x\",\"v\":2},{\"g\":\"b\",\"k\":\"x\",\"v\":6},{\"g\":\"b\",\"k\":\"x\",\"v\":8},{\"g\":\"c\",\"k\":\"y\",\"v\":3},{\"g\":\"a\",\"k\":\"x\",\"v\":\"\"}]";

    /// <summary>
    ///     Pivots the rows with an operation.
    /// </summary>
    /// <param name="op">The operation, or null for the default.</param>
    /// <param name="extra">More members of the transform, each preceded by a comma.</param>
    /// <returns>The pivoted table.</returns>
    private static DataTable Pivot(string? op, string extra = "") =>
        AggregateTestSupport.Run(AggregateTestSupport.Spec(Rows, "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"]" + (op is null ? string.Empty : ",\"op\":\"" + op + "\"") + extra + "}]"));

    /// <summary>
    ///     Validates the result of each operation for the groups a, b and c, derived by hand (a missing combination is the empty result of the operation; a text that is not a number
    ///     poisons the sums, the products and the means of its cell with NaN; the median and the quartiles use the numbers only; the minimum and maximum compare like JavaScript).
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="a">The cells of the group a (x and y).</param>
    /// <param name="b">The cells of the group b.</param>
    /// <param name="c">The cells of the group c.</param>
    [Theory]
    [InlineData("sum", "5|NaN", "16|null", "null|3")]
    [InlineData("count", "3|3", "3|0", "0|1")]
    [InlineData("mean", "2.5|NaN", "5.333333333333333|null", "null|3")]
    [InlineData("average", "2.5|NaN", "5.333333333333333|null", "null|3")]
    [InlineData("variance", "4.5|NaN", "9.333333333333334|null", "null|null")]
    [InlineData("variancep", "2.25|NaN", "6.222222222222222|null", "null|0")]
    [InlineData("stdev", "2.1213203435596424|NaN", "3.0550504633038935|null", "null|null")]
    [InlineData("stdevp", "1.5|NaN", "2.494438257849294|null", "null|0")]
    [InlineData("stderr", "1.5|NaN", "1.7638342073763937|null", "null|null")]
    [InlineData("median", "2.5|10", "6|null", "null|3")]
    [InlineData("q1", "1.75|10", "4|null", "null|3")]
    [InlineData("q3", "3.25|10", "7|null", "null|3")]
    [InlineData("min", "1|10", "2|null", "null|3")]
    [InlineData("max", "4|10", "8|null", "null|3")]
    [InlineData("product", "4|NaN", "96|null", "null|3")]
    [InlineData("valid", "2|2", "3|0", "0|1")]
    [InlineData("missing", "1|1", "0|0", "0|0")]
    [InlineData("distinct", "4|4", "3|1", "1|1")]
    [InlineData("exponential", "NaN|NaN", "NaN|null", "null|NaN")]
    [InlineData("exponentialb", "NaN|NaN", "NaN|null", "null|NaN")]
    public void RowTransforms_Apply_PivotComputesTheOperation(string op, string a, string b, string c)
    {
        // Arrange / Act
        var table = Pivot(op);

        // Assert
        Assert.Equal(["g", "x", "y"], table.Fields);
        Assert.Equal(["a|" + a, "b|" + b, "c|" + c], AggregateTestSupport.Rows(table, "g", "x", "y"));
    }

    /// <summary>
    ///     Validates that the default operation is the sum, and that <c>limit</c> keeps working with an operation: only the first pivot value in ordinal order is a column, and a
    ///     group without a row of that value has the empty result.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotDefaultsToTheSumAndAppliesTheLimit()
    {
        // Arrange / Act
        var defaulted = Pivot(null);
        var limited = Pivot("max", ",\"limit\":1");

        // Assert: the oracle gives {a: 4}, {b: 8}, {c: no value} for the maximum of the first value only
        Assert.Equal(["a|5|NaN", "b|16|null", "c|null|3"], AggregateTestSupport.Rows(defaulted, "g", "x", "y"));
        Assert.Equal(["g", "x"], limited.Fields);
        Assert.Equal(["a|4", "b|8", "c|null"], AggregateTestSupport.Rows(limited, "g", "x"));
    }

    /// <summary>
    ///     Validates defect B2: a <c>limit</c> that no 64-bit integer can hold (9.3e18, 1.79e308) means "every pivot value" on every runtime. The plain cast gave the smallest long on .NET 8 and the largest
    ///     one on .NET 9 and later (and an <see cref="OverflowException"/> in a checked build), so the same specification gave a different table on two runtimes.
    /// </summary>
    /// <param name="limit">The limit as JSON text.</param>
    [Theory]
    [InlineData("9.3e18")]
    [InlineData("1e30")]
    [InlineData("1.7976931348623157E+308")]
    public void RowTransforms_Apply_PivotLimitBeyondLong_KeepsEveryPivotValue(string limit)
    {
        // Arrange / Act
        var huge = Pivot(null, ",\"limit\":" + limit);
        var unlimited = Pivot(null);

        // Assert
        Assert.Equal(unlimited.Fields, huge.Fields);
        Assert.Equal(AggregateTestSupport.Rows(unlimited, "g", "x", "y"), AggregateTestSupport.Rows(huge, "g", "x", "y"));
    }

    /// <summary>
    ///     Validates the pivot without group fields: one row of the operation over every row of each value (the maximum of x is 8 and of y is 10).
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotWithoutGroupsAggregatesEveryRowOfAValue()
    {
        // Arrange / Act
        var table = AggregateTestSupport.Run(AggregateTestSupport.Spec(Rows, "[{\"pivot\":\"k\",\"value\":\"v\",\"op\":\"max\"}]"));

        // Assert
        Assert.Equal(["8|10"], AggregateTestSupport.Rows(table, "x", "y"));
    }

    /// <summary>
    ///     Validates that the operations that keep tuples see the whole group in Vega's pivot: <c>values</c> is the array of every row of the group (six for group a, three for b, one for c)
    ///     in every column.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotValuesAreTheRowsOfTheGroup()
    {
        // Arrange / Act
        var table = Pivot("values");

        // Assert
        Assert.Equal([6, 3, 1], Enumerable.Range(0, 3).Select(r => table.GetValue(r, "x").Elements.Length));
        Assert.Equal([6, 3, 1], Enumerable.Range(0, 3).Select(r => table.GetValue(r, "y").Elements.Length));
        Assert.True(table.GetValue(0, "x").Elements[0].IsObject);
    }

    /// <summary>
    ///     Validates the rows that the extreme operations return: the row of the smallest and of the largest value of the cell, as an object with the fields of the row.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotArgMinAndArgMaxReturnTheRows()
    {
        // Arrange / Act
        var smallest = Pivot("argmin");
        var largest = Pivot("argmax");

        // Assert: group b has x = 2, 6 and 8
        Assert.Equal(2, Member(smallest.GetValue(1, "x"), "v").Number);
        Assert.Equal(8, Member(largest.GetValue(1, "x"), "v").Number);
        Assert.Equal("b", Member(largest.GetValue(1, "x"), "g").Text);
        Assert.True(smallest.GetValue(1, "y").IsNull);
    }

    /// <summary>
    ///     Reads a member of an object value.
    /// </summary>
    /// <param name="value">The object value.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The member.</returns>
    private static DataValue Member(DataValue value, string name)
    {
        Assert.True(value.ObjectValue!.TryGetValue(name, out var member));
        return member;
    }

    /// <summary>
    ///     Validates that the bootstrap confidence interval is deterministic (the same table every run), lies within the values of the cell, and has the lower bound at or below the upper bound.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotConfidenceIntervalIsDeterministicAndOrdered()
    {
        // Arrange / Act
        var lowerA = Pivot("ci0");
        var lowerB = Pivot("ci0");
        var upper = Pivot("ci1");

        // Assert: group b has x = 2, 6 and 8
        Assert.Equal(AggregateTestSupport.Rows(lowerA, "g", "x", "y"), AggregateTestSupport.Rows(lowerB, "g", "x", "y"));
        var low = lowerA.GetValue(1, "x").Number;
        var high = upper.GetValue(1, "x").Number;
        Assert.InRange(low, 2, 8);
        Assert.InRange(high, 2, 8);
        Assert.True(low <= high);
    }

    /// <summary>
    ///     Validates that the pivot operation is read from the transform even when the numbers are texts: a numeric text is converted by the sum as JavaScript's unary plus does, so "2" and 3 sum to 5.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotSumConvertsNumericTexts()
    {
        // Arrange
        const string rows = "[{\"k\":\"x\",\"v\":\"2\"},{\"k\":\"x\",\"v\":3}]";

        // Act
        var table = AggregateTestSupport.Run(AggregateTestSupport.Spec(rows, "[{\"pivot\":\"k\",\"value\":\"v\"}]"));

        // Assert
        Assert.Equal(["5"], AggregateTestSupport.Rows(table, "x"));
    }

    /// <summary>
    ///     Validates the budget of the cells: a pivot whose cells (groups times columns) would exceed the cell limit is refused with an error located at the transform, before the cells are allocated.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotCellsBeyondTheLimitFailAtTheTransform()
    {
        // Arrange: 9,000 groups of 1,000 columns are 9 million states of four cells, which is beyond the limit of 33 million cells
        var rows = StatTestSupport.PivotRows(9000, 1000);
        var limits = new VegaLiteLimits { MaxFieldCount = 2000, MaxDataRows = 10_000_000 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => AggregateTestSupport.Run(AggregateTestSupport.Spec(rows, "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"]}]"), limits));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("TableLayout.MaxCells", error.Message, StringComparison.Ordinal);
    }
}
