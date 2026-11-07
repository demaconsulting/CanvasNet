using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>quantile</c> and <c>extent</c> transforms and the shared behavior of the statistical transform unit (the dispatch, the row accounting and the argument
///     checks): the quantiles of the numbers 1 to 10 have the closed form <c>1 + 9p</c> (the R-7 method), the probabilities of the default step are those of d3's <c>range</c>, and the extent
///     of a field is the minimum and the maximum of its numbers, stored in a parameter that the later transforms read.
/// </summary>
public class StatTransformsQuantileTests
{
    /// <summary>The rows <c>v = 1..10</c>.</summary>
    private const string OneToTen = "[{\"v\":1},{\"v\":2},{\"v\":3},{\"v\":4},{\"v\":5},{\"v\":6},{\"v\":7},{\"v\":8},{\"v\":9},{\"v\":10}]";

    /// <summary>
    ///     Runs a transform array over inline rows the way a render does.
    /// </summary>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The table and the warnings of the transforms.</returns>
    private static (DataTable Table, List<FeatureReport> Warnings) Run(string transforms, string rows, VegaLiteLimits? limits = null)
    {
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(rows, transforms), limits);
        var result = RowTransforms.Apply(table, tree.Root["transform"], context);
        return (result, context.Warnings);
    }

    /// <summary>
    ///     Validates the default probabilities: step 0.01 gives the 100 probabilities 0.005, 0.015, ... 0.995 (d3's range), and each value is the closed form 1 + 9p; the output fields are
    ///     <c>prob</c> and <c>value</c>.
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_DefaultStepGivesOneHundredProbabilities()
    {
        // Arrange / Act
        var (table, warnings) = Run("[{\"quantile\":\"v\"}]", OneToTen);

        // Assert
        Assert.Equal(["prob", "value"], table.Fields);
        Assert.Equal(100, table.RowCount);
        var probability = AggregateTestSupport.Numbers(table, "prob");
        var value = AggregateTestSupport.Numbers(table, "value");
        Assert.Equal(0.005, probability[0], 15);
        Assert.Equal(0.995, probability[99], 12);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(1 + (9 * probability[i]), value[i], 12);
        }

        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates <c>step</c>: 0.25 gives the probabilities 0.125, 0.375, 0.625 and 0.875 (the values 2.125, 4.375, 6.625 and 8.875), and 0.3 gives three probabilities
    ///     (the range stops below 1 - 1e-14).
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_StepGivesTheRangeOfHalfStepsUpToOne()
    {
        // Arrange / Act
        var (quarter, _) = Run("[{\"quantile\":\"v\",\"step\":0.25}]", OneToTen);
        var (third, _) = Run("[{\"quantile\":\"v\",\"step\":0.3}]", OneToTen);

        // Assert
        Assert.Equal([0.125, 0.375, 0.625, 0.875], AggregateTestSupport.Numbers(quarter, "prob"));
        Assert.Equal([2.125, 4.375, 6.625, 8.875], AggregateTestSupport.Numbers(quarter, "value"));
        Assert.Equal(3, third.RowCount);
        Assert.Equal(7.75, third.GetValue(2, "value").Number, 12);
    }

    /// <summary>
    ///     Validates <c>probs</c>: the given probabilities are used in their order, with the interpolated values and the clamping beyond the ends (the reference gives 1, 1.9, 5.5, 9.1, 10, 10, 1).
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_ProbsAreUsedAsGiven()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"quantile\":\"v\",\"probs\":[0,0.1,0.5,0.9,1,1.5,-1]}]", OneToTen);

        // Assert
        Assert.Equal([0.0, 0.1, 0.5, 0.9, 1, 1.5, -1], AggregateTestSupport.Numbers(table, "prob"));
        var value = AggregateTestSupport.Numbers(table, "value");
        double[] expected = [1, 1.9, 5.5, 9.1, 10, 10, 1];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], value[i], 12);
        }
    }

    /// <summary>
    ///     Validates the groups: the quantiles of each group, with the group field, in the order of first appearance (a: {1, 5} has the quartile 2 and the median 3; b: {2, 4, 9} has 3 and 4).
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_GroupbyComputesEveryGroupSeparately()
    {
        // Arrange
        const string rows = "[{\"g\":\"a\",\"v\":1},{\"g\":\"a\",\"v\":5},{\"g\":\"b\",\"v\":2},{\"g\":\"b\",\"v\":4},{\"g\":\"b\",\"v\":9}]";

        // Act
        var (table, _) = Run("[{\"quantile\":\"v\",\"groupby\":[\"g\"],\"probs\":[0.25,0.5]}]", rows);

        // Assert
        Assert.Equal(["g", "prob", "value"], table.Fields);
        Assert.Equal(["a|0.25|2", "a|0.5|3", "b|0.25|3", "b|0.5|4"], AggregateTestSupport.Rows(table, "g", "prob", "value"));
    }

    /// <summary>
    ///     Validates the values that are not numbers: null, the empty text and text that is not a number are left out of the order statistics, a numeric text is converted (the reference
    ///     gives the median 3 and the first quartile 2 of 1, 3 and "7").
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_IgnoresValuesThatAreNotNumbers()
    {
        // Arrange
        const string rows = "[{\"v\":1},{\"v\":null},{\"v\":\"x\"},{\"v\":3},{\"v\":\"\"},{\"v\":\"7\"}]";

        // Act
        var (table, _) = Run("[{\"quantile\":\"v\",\"probs\":[0.5,0.25]}]", rows);

        // Assert
        Assert.Equal([3.0, 2], AggregateTestSupport.Numbers(table, "value"));
    }

    /// <summary>
    ///     Validates that the quantile of nothing is undefined in Vega: an empty group (the one group of an empty table, or a column without numbers) has the probability and no value (null).
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_OfNothingHasNoValue()
    {
        // Arrange / Act
        var (empty, _) = Run("[{\"quantile\":\"v\",\"probs\":[0.5]}]", "[]");
        var (nulls, _) = Run("[{\"quantile\":\"v\",\"probs\":[0.5]}]", "[{\"v\":null}]");

        // Assert
        Assert.Equal(["0.5|null"], AggregateTestSupport.Rows(empty, "prob", "value"));
        Assert.Equal(["0.5|null"], AggregateTestSupport.Rows(nulls, "prob", "value"));
    }

    /// <summary>
    ///     Validates the <c>as</c> names.
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_AsRenamesTheOutputFields()
    {
        // Arrange / Act
        var (table, _) = Run("[{\"quantile\":\"v\",\"probs\":[0.5],\"as\":[\"p\",\"q\"]}]", OneToTen);

        // Assert
        Assert.Equal(["p", "q"], table.Fields);
        Assert.Equal(["0.5|5.5"], AggregateTestSupport.Rows(table, "p", "q"));
    }

    /// <summary>
    ///     Validates the row budget: a step so small that the probabilities would exceed <see cref="VegaLiteLimits.MaxDataRows"/> is refused before they are generated, with an error located at the transform.
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_TinyStepBeyondTheRowBudgetFailsAtTheTransform()
    {
        // Arrange / Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("[{\"quantile\":\"v\",\"step\":1e-9}]", OneToTen));

        // Assert
        Assert.Equal("/transform/0", error.Location.Pointer);
        Assert.Contains("MaxDataRows", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the cancellation: a token that is already canceled stops the quantile.
    /// </summary>
    [Fact]
    public void StatTransforms_Quantile_StopsWhenCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(OneToTen, "[{\"quantile\":\"v\"}]"), null, source.Token);
        source.Cancel();
        var entry = tree.Root["transform"]!.Items[0];

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => StatTransforms.Apply(table, entry, SpecParser.FindTransformOperation(entry)!, context));
    }

    /// <summary>
    ///     Validates the extent: the minimum and the maximum of the numbers of the field are stored in the parameter as an array, the table is the same table, null and the empty text are skipped, NaN
    ///     (a text that is not a number) is ignored and a numeric text is converted (the reference gives [-2, 9]).
    /// </summary>
    [Fact]
    public void StatTransforms_Extent_StoresTheMinimumAndMaximumInTheParameter()
    {
        // Arrange
        const string rows = "[{\"v\":3},{\"v\":null},{\"v\":\"x\"},{\"v\":9},{\"v\":\"4\"},{\"v\":-2},{\"v\":\"\"}]";
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(rows, "[{\"extent\":\"v\",\"param\":\"ext\"}]"));

        // Act
        var result = RowTransforms.Apply(table, tree.Root["transform"], context);

        // Assert
        Assert.Same(table, result);
        var extent = context.Parameters["ext"];
        Assert.Equal([-2.0, 9.0], extent.Elements.ToArray().Select(e => e.Number));
        Assert.Empty(context.Warnings);
    }

    /// <summary>
    ///     Validates the extent of nothing: a table without rows, without numbers, or with an infinite value has no finite extent, so the parameter is <c>[null, null]</c> and a warning is
    ///     recorded (Vega warns "Infinite extent" and the signal is <c>[undefined, undefined]</c>).
    /// </summary>
    /// <param name="rows">The rows.</param>
    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"v\":null},{\"v\":\"x\"}]")]
    [InlineData("[{\"v\":1},{\"v\":\"Infinity\"}]")]
    public void StatTransforms_Extent_WithoutAFiniteExtentSetsNullsAndWarns(string rows)
    {
        // Arrange
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(rows, "[{\"extent\":\"v\",\"param\":\"ext\"}]"));

        // Act
        RowTransforms.Apply(table, tree.Root["transform"], context);

        // Assert
        var extent = context.Parameters["ext"].Elements.ToArray();
        Assert.Equal(2, extent.Length);
        Assert.All(extent, e => Assert.True(e.IsNull));
        var warning = Assert.Single(context.Warnings);
        Assert.Equal(("vl-transform-runtime", "/transform/0"), (warning.Id, warning.Location.Pointer));
        Assert.Contains("'ext'", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the parameter of an extent is read by the transforms that follow it: a calculate that divides by the maximum gives the shares of the maximum (the table was not changed by the
    ///     extent, so the original fields are there).
    /// </summary>
    [Fact]
    public void StatTransforms_Extent_IsReadByTheFollowingTransforms()
    {
        // Arrange
        const string rows = "[{\"v\":2},{\"v\":5},{\"v\":10}]";

        // Act
        var (table, _) = Run("[{\"extent\":\"v\",\"param\":\"ext\"},{\"calculate\":\"(datum.v - ext[0]) / (ext[1] - ext[0])\",\"as\":\"unit\"}]", rows);

        // Assert: 2 and 10 are the ends of the extent
        Assert.Equal(["2|0", "5|0.375", "10|1"], AggregateTestSupport.Rows(table, "v", "unit"));
    }

    /// <summary>
    ///     Validates the accounting of the rows: the input of a generating transform is given back to the row budget when its output is complete, so only the output counts afterwards
    ///     (six rows in, two out), and an extent keeps its input.
    /// </summary>
    [Fact]
    public void StatTransforms_Apply_ReleasesTheInputRowsWhenTheOutputIsComplete()
    {
        // Arrange
        const string rows = "[{\"x\":0,\"y\":1},{\"x\":1,\"y\":3},{\"x\":2,\"y\":5},{\"x\":3,\"y\":7},{\"x\":4,\"y\":9},{\"x\":5,\"y\":11}]";
        var (table, context, loader, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(rows, "[{\"regression\":\"y\",\"on\":\"x\"}]"));
        var (extentTable, extentContext, extentLoader, extentTree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(rows, "[{\"extent\":\"y\",\"param\":\"e\"}]"));

        // Act
        var regression = RowTransforms.Apply(table, tree.Root["transform"], context);
        RowTransforms.Apply(extentTable, extentTree.Root["transform"], extentContext);

        // Assert
        Assert.Equal(2, regression.RowCount);
        Assert.Equal(2, loader.RowsLoaded);
        Assert.Equal(6, extentLoader.RowsLoaded);
    }

    /// <summary>
    ///     Validates the argument checks and that a transform that is not a statistical one is returned unchanged.
    /// </summary>
    [Fact]
    public void StatTransforms_Apply_RejectsNullArgumentsAndIgnoresOtherTransforms()
    {
        // Arrange
        var (table, context, _, tree) = AggregateTestSupport.Prepare(AggregateTestSupport.Spec(OneToTen, "[{\"calculate\":\"datum.v\",\"as\":\"w\"}]"));
        var entry = tree.Root["transform"]!.Items[0];
        var operation = SpecParser.FindTransformOperation(entry)!;

        // Act / Assert
        Assert.Same(table, StatTransforms.Apply(table, entry, operation, context));
        Assert.Throws<ArgumentNullException>(() => StatTransforms.Apply(null!, entry, operation, context));
        Assert.Throws<ArgumentNullException>(() => StatTransforms.Apply(table, null!, operation, context));
        Assert.Throws<ArgumentNullException>(() => StatTransforms.Apply(table, entry, null!, context));
        Assert.Throws<ArgumentNullException>(() => StatTransforms.Apply(table, entry, operation, null!));
    }
}
