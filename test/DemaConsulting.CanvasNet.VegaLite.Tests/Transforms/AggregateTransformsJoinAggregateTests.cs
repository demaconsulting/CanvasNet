using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.AggregateTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>joinaggregate</c> transform: the measures of a group written to every row of the group, the rows and their
///     order kept, the shared arrays of <c>values</c>, the output naming, the limits, the charging of work and cancellation.
/// </summary>
public class AggregateTransformsJoinAggregateTests
{
    /// <summary>The rows of the grouping tests: three groups, the second seen before the first is seen again.</summary>
    private const string Grouped = """[{"k":"b","v":1},{"k":"a","v":2},{"k":"b","v":3},{"k":"a","v":4},{"k":"c","v":5}]""";

    /// <summary>
    ///     Validates that every row keeps its place and gets the measures of its group: b is 1 and 3, a is 2 and 4, c is 5.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_WritesGroupMeasuresToEveryRow()
    {
        // Act
        var table = Run(Spec(
            Grouped,
            """[{"joinaggregate":[{"op":"sum","field":"v","as":"s"},{"op":"count","as":"n"},{"op":"max","field":"v","as":"x"}],"groupby":["k"]}]"""));

        // Assert
        Assert.Equal(["k", "n", "s", "v", "x"], table.Fields);
        Assert.Equal(["b|1|4|2|3", "a|2|6|2|4", "b|3|4|2|3", "a|4|6|2|4", "c|5|5|1|5"], Rows(table, "k", "v", "s", "n", "x"));
    }

    /// <summary>
    ///     Validates that without a group-by the measure covers the whole table (the mean of 1 to 5 is 3), with the default name
    ///     <c>mean_v</c>, and that an <c>as</c> replaces an existing field.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_WholeTableAndNaming()
    {
        // Act
        var table = Run(Spec(Grouped, """[{"joinaggregate":[{"op":"mean","field":"v"},{"op":"sum","field":"v","as":"v"}]}]"""));

        // Assert
        Assert.Equal(["k", "mean_v", "v"], table.Fields);
        Assert.Equal(["3|15", "3|15", "3|15", "3|15", "3|15"], Rows(table, "mean_v", "v"));
    }

    /// <summary>
    ///     Validates that the array of a <c>values</c> measure is one array shared by the rows of the group (not copied per row), and
    ///     that groups have their own.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_ValuesArraysAreSharedWithinAGroup()
    {
        // Act
        var table = Run(Spec(Grouped, """[{"joinaggregate":[{"op":"values","field":"v","as":"all"}],"groupby":["k"]}]"""));

        // Assert
        Assert.Equal(["1,3", "2,4", "1,3", "2,4", "5"], Rows(table, "all"));
        Assert.Equal(table.GetValue(0, "all"), table.GetValue(2, "all"));
        Assert.NotEqual(table.GetValue(0, "all"), table.GetValue(1, "all"));
    }

    /// <summary>
    ///     Validates the row-valued measures and the quantiles through the join (computed once per group).
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_RowMeasuresAndQuantiles()
    {
        // Act
        var table = Run(Spec(
            """[{"k":"a","v":9},{"k":"a","v":1},{"k":"a","v":5},{"k":"b","v":2}]""",
            """[{"joinaggregate":[{"op":"argmax","field":"v","as":"top"},{"op":"median","field":"v","as":"m"},{"op":"q1","field":"v","as":"q"}],"groupby":["k"]}]"""));

        // Assert: a has 1, 5, 9: median 5, first quartile 3
        Assert.Equal(["5|3", "5|3", "5|3", "2|2"], Rows(table, "m", "q"));
        Assert.Equal(9, table.GetValue(2, "top").ObjectValue!.ValueAt(1).Number);
        Assert.Equal(table.GetValue(0, "top"), table.GetValue(1, "top"));
    }

    /// <summary>
    ///     Validates that an empty table gives an empty table with the fields added.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_EmptyInputGivesNoRows()
    {
        // Act
        var table = Run(Spec("[]", """[{"joinaggregate":[{"op":"count","as":"n"}],"groupby":["k"]}]"""));

        // Assert
        Assert.Equal(0, table.RowCount);
        Assert.Equal(["n"], table.Fields);
    }

    /// <summary>
    ///     Validates that the rows are not counted again: the row budget is unchanged by the join.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_DoesNotChangeTheRowBudget()
    {
        // Arrange
        var (table, context, loader, tree) = Prepare(Spec(Grouped, """[{"joinaggregate":[{"op":"count","as":"n"}],"groupby":["k"]}]"""));
        var before = loader.RowsLoaded;

        // Act
        var result = RowTransforms.Apply(table, tree.Root["transform"], context);

        // Assert
        Assert.Equal(5, result.RowCount);
        Assert.Equal(before, loader.RowsLoaded);
    }

    /// <summary>
    ///     Validates that the state of the groups is bounded by the cell limit as the groups appear: 120 measures cost 601 cells for every
    ///     group, so 60,000 groups exceed the limit, located at the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_GroupStateIsBoundedByTheCellLimit()
    {
        // Arrange
        var measures = string.Join(",", Enumerable.Range(0, 120).Select(i => "{\"op\":\"count\",\"as\":\"c" + i + "\"}"));

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(SequenceSpec(60_000, "[{\"joinaggregate\":[" + measures + "],\"groupby\":[\"id\"]}]")));

        // Assert
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("the limit of 33554432 cells (TableLayout.MaxCells)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the up-front bound of the values that the operations keep, and the field limit.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_ValueAndFieldLimitsAreLocated()
    {
        // Arrange
        var medians = string.Join(",", Enumerable.Range(0, 600).Select(i => "{\"op\":\"median\",\"field\":\"id\",\"as\":\"m" + i + "\"}"));
        var values = Assert.Throws<VegaLiteRenderException>(() => Run(SequenceSpec(60_000, "[{\"joinaggregate\":[" + medians + "]}]")));

        // Act
        var fields = Assert.Throws<VegaLiteRenderException>(() =>
            Run(Spec(Grouped, """[{"joinaggregate":[{"op":"count","as":"a"},{"op":"count","as":"b"}]}]"""), new VegaLiteLimits { MaxFieldCount = 3 }));

        // Assert
        Assert.Contains("the limit of 33554432 values (TableLayout.MaxCells)", values.Message, StringComparison.Ordinal);
        Assert.Contains("MaxFieldCount", fields.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", fields.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the work of the join is charged to the step budget, that the sort of a median is too, and that a larger
    ///     budget succeeds; the failure is located and names the limit.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_WorkIsChargedToTheStepBudget()
    {
        // Arrange
        var rows = SequenceSpec(10_000, """[{"joinaggregate":[{"op":"median","field":"id","as":"m"}],"groupby":["id"]}]""");
        var one = SequenceSpec(5000, """[{"joinaggregate":[{"op":"median","field":"id","as":"m"}]}]""");

        // Act
        var table = Run(one, new VegaLiteLimits { MaxEvaluationSteps = 100_000 });
        var byRows = Assert.Throws<VegaLiteRenderException>(() => Run(rows, new VegaLiteLimits { MaxEvaluationSteps = 100 }));
        var bySort = Assert.Throws<VegaLiteRenderException>(() => Run(one, new VegaLiteLimits { MaxEvaluationSteps = 300 }));

        // Assert: the median of 0..4999 is 2,499.5
        Assert.Equal(2499.5, table.GetValue(0, "m").Number);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", byRows.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", bySort.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", bySort.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a text of the specification longer than the string limit of the call is rejected, located at the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_LongNamesRespectMaxStringLength()
    {
        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() =>
            Run(
                Spec(Grouped, """[{"joinaggregate":[{"op":"count","as":"a_name_longer_than_twenty_five_chars"}]}]"""),
                new VegaLiteLimits { MaxStringLength = 20 },
                parseWithDefaults: true));

        // Assert
        Assert.Contains("VegaLiteLimits.MaxStringLength", exception.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", exception.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a canceled token stops the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_JoinAggregate_CancellationIsPolled() =>
        Assert.Throws<OperationCanceledException>(() => ApplyCanceled(Spec(Grouped, """[{"joinaggregate":[{"op":"sum","field":"v"}],"groupby":["k"]}]""")));
}
