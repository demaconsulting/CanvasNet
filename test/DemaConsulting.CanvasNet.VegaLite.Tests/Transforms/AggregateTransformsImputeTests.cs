using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.AggregateTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>impute</c> transform: the key domain (observed keys and <c>keyvals</c>), the appended rows, the constant and
///     the statistical methods with frames, structural keys, the size bounds that apply before anything is allocated, the row budget,
///     charging and cancellation. The expected values are derived by hand from the public Vega and Vega-Lite documentation.
/// </summary>
public class AggregateTransformsImputeTests
{
    /// <summary>Two series: <c>a</c> has the keys 1 and 3, <c>b</c> has the key 2.</summary>
    private const string Series = """[{"x":1,"g":"a","y":10},{"x":3,"g":"a","y":30},{"x":2,"g":"b","y":5}]""";

    /// <summary>
    ///     Imputes the field <c>y</c> of rows by the key <c>x</c> within <c>g</c>.
    /// </summary>
    /// <param name="rows">The JSON rows.</param>
    /// <param name="members">More members of the impute transform as JSON text (for example <c>"method":"mean"</c>), or empty.</param>
    /// <param name="groupBy">Whether the series are grouped by <c>g</c>.</param>
    /// <returns>The resulting table.</returns>
    private static DataTable Impute(string rows, string members = "", bool groupBy = true) =>
        Run(Spec(rows, "[{\"impute\":\"y\",\"key\":\"x\"" + (groupBy ? ",\"groupby\":[\"g\"]" : string.Empty) + (members.Length == 0 ? string.Empty : "," + members) + "}]"));

    /// <summary>
    ///     Validates the default method: the rows are followed by one row of value 0 for every group that lacks a key, in group order and key
    ///     order. The keys are 1, 3, 2 (first seen), so <c>a</c> lacks 2 and <c>b</c> lacks 1 and 3; the other fields of an imputed row are null.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_AppendsMissingKeysAfterTheInputRows()
    {
        // Act
        var table = Impute("""[{"x":1,"g":"a","y":10,"z":1},{"x":3,"g":"a","y":30,"z":1},{"x":2,"g":"b","y":5,"z":1}]""");

        // Assert
        Assert.Equal(["a|1|10|1", "a|3|30|1", "b|2|5|1", "a|2|0|null", "b|1|0|null", "b|3|0|null"], Rows(table, "g", "x", "y", "z"));
    }

    /// <summary>
    ///     Validates that <c>keyvals</c> extend the domain (they come first, then the observed keys not among them), as an array and
    ///     as a sequence with a start, a stop, a step, a negative step, and a sequence that is empty because the step points away.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_KeyValuesExtendTheDomain()
    {
        // Act
        var array = Impute(Series, "\"keyvals\":[1,2,3,4]");
        var sequence = Impute(Series, "\"keyvals\":{\"start\":1,\"stop\":4}");
        var stepped = Impute(Series, "\"keyvals\":{\"start\":0,\"stop\":10,\"step\":5}");
        var backwards = Impute(Series, "\"keyvals\":{\"start\":3,\"stop\":0,\"step\":-1}");
        var away = Impute(Series, "\"keyvals\":{\"start\":0,\"stop\":3,\"step\":-1}");

        // Assert: a lacks 2 and 4; b lacks 1, 3 and 4
        Assert.Equal(["a|2", "a|4", "b|1", "b|3", "b|4"], Rows(array, "g", "x").Skip(3));
        Assert.Equal(["a|2", "b|1", "b|3"], Rows(sequence, "g", "x").Skip(3));
        Assert.Equal(["a|0", "a|5", "a|2", "b|0", "b|5", "b|1", "b|3"], Rows(stepped, "g", "x").Skip(3));
        Assert.Equal(["a|2", "b|3", "b|1"], Rows(backwards, "g", "x").Skip(3));
        Assert.Equal(["a|2", "b|1", "b|3"], Rows(away, "g", "x").Skip(3));
    }

    /// <summary>
    ///     Validates that without a group-by every row belongs to one series.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_WithoutGroupByThereIsOneSeries()
    {
        // Act
        var table = Impute("""[{"x":1,"y":10},{"x":3,"y":30}]""", "\"keyvals\":[1,2,3]", groupBy: false);

        // Assert
        Assert.Equal(["1|10", "3|30", "2|0"], Rows(table, "x", "y"));
    }

    /// <summary>
    ///     Validates that a field that no row has is added: the imputed row has it, the others have null.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_AddsAMissingField()
    {
        // Act
        var table = Impute("""[{"x":1},{"x":3}]""", "\"keyvals\":[1,2,3]", groupBy: false);

        // Assert
        Assert.Equal(["x", "y"], table.Fields);
        Assert.Equal(["1|null", "3|null", "2|0"], Rows(table, "x", "y"));
    }

    /// <summary>
    ///     Validates the constant: a text, a number, and an explicit null (which is not replaced by the default 0).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_ConstantValueForms()
    {
        // Act
        var text = Impute(Series, "\"value\":\"n/a\"");
        var number = Impute(Series, "\"value\":-2.5");
        var none = Impute(Series, "\"value\":null");

        // Assert
        Assert.Equal(["n/a", "n/a", "n/a"], Rows(text, "y").Skip(3));
        Assert.Equal(["-2.5", "-2.5", "-2.5"], Rows(number, "y").Skip(3));
        Assert.True(none.GetValue(3, "y").IsNull);
        Assert.Equal(6, none.RowCount);
    }

    /// <summary>
    ///     Validates the statistical methods over the group (the default frame is the whole group): series <c>a</c> has 10 and 30, series
    ///     <c>b</c> has 5; the imputed rows are a/2, b/1 and b/3.
    /// </summary>
    [Theory]
    [InlineData("mean", new double[] { 20, 5, 5 })]
    [InlineData("median", new double[] { 20, 5, 5 })]
    [InlineData("max", new double[] { 30, 5, 5 })]
    [InlineData("min", new double[] { 10, 5, 5 })]
    public void AggregateTransforms_Impute_StatisticalMethodsUseTheGroup(string method, double[] expected)
    {
        // Act
        var table = Impute(Series, "\"method\":\"" + method + "\"");

        // Assert
        Assert.Equal(expected, Numbers(table, "y").Skip(3));
        Assert.Equal([10, 30, 5], Numbers(table, "y").Take(3));
    }

    /// <summary>
    ///     Validates that the frame is over the group in data order with the imputed rows (null until imputed) after the input rows:
    ///     with <c>[-1, 0]</c> a/2 sees the row before it (30), b/1 sees the only row of b (5), and b/3 sees only imputed (null) rows, so it is
    ///     null; with <c>[null, 0]</c> a/2 is the mean of 10 and 30.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_FrameIsOverTheGroupInDataOrder()
    {
        // Act
        var previous = Impute(Series, "\"method\":\"mean\",\"frame\":[-1,0]");
        var running = Impute(Series, "\"method\":\"mean\",\"frame\":[null,0]");

        // Assert
        Assert.Equal(["30", "5", "null"], Rows(previous, "y").Skip(3));
        Assert.Equal(["20", "5", "5"], Rows(running, "y").Skip(3));
    }

    /// <summary>
    ///     Validates that with a statistical method a null in an existing row is replaced as well (as Vega-Lite does), and that with the
    ///     constant method an existing null is left alone.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_StatisticalMethodsReplaceExistingNulls()
    {
        // Arrange
        const string rows = """[{"x":1,"y":10},{"x":2,"y":null}]""";

        // Act
        var mean = Impute(rows, "\"method\":\"mean\"", groupBy: false);
        var constant = Impute(rows, string.Empty, groupBy: false);

        // Assert
        Assert.Equal([10, 10], Numbers(mean, "y"));
        Assert.True(constant.GetValue(1, "y").IsNull);
        Assert.Equal(2, constant.RowCount);
    }

    /// <summary>
    ///     Validates that keys are structural (the number 1 and the text "1" are different keys), that a key twice in a group counts once,
    ///     and that nothing is added when every group has every key.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_KeysAreStructuralAndDuplicatesCountOnce()
    {
        // Act
        var mixed = Impute("""[{"g":"a","x":1,"y":1},{"g":"b","x":"1","y":2}]""");
        var complete = Impute("""[{"g":"a","x":1,"y":1},{"g":"a","x":1,"y":2},{"g":"a","x":2,"y":3}]""");

        // Assert: a lacks the text key and b lacks the number key
        Assert.Equal(4, mixed.RowCount);
        Assert.True(mixed.GetValue(2, "x").IsString);
        Assert.Equal("a", mixed.GetValue(2, "g").Text);
        Assert.True(mixed.GetValue(3, "x").IsNumber);
        Assert.Equal("b", mixed.GetValue(3, "g").Text);
        Assert.Equal(3, complete.RowCount);
    }

    /// <summary>
    ///     Validates that an empty table is returned as it is.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_EmptyInputGivesNoRows() =>
        Assert.Equal(0, Impute("[]", "\"keyvals\":[1,2]").RowCount);

    /// <summary>
    ///     Validates the row budget: the result has six rows, so the input and the output together (3 + 6) need a budget of nine rows
    ///     (the input counts until the output is complete), and eight fail located at the transform; afterwards only the output is counted.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_OutputRowsAreBoundedByTheRowBudget()
    {
        // Arrange
        var json = Spec(Series, """[{"impute":"y","key":"x","groupby":["g"]}]""");
        var (table, context, loader, tree) = Prepare(json, new VegaLiteLimits { MaxDataRows = 9 });

        // Act
        var result = RowTransforms.Apply(table, tree.Root["transform"], context);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxDataRows = 8 }));

        // Assert
        Assert.Equal(6, result.RowCount);
        Assert.Equal(6, loader.RowsLoaded);
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("(MaxDataRows)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the cross product of groups and keys is bounded before it is allocated: 100 groups of one row, each with its own
    ///     key, need 10,000 rows, which a limit of 5,000 rejects (and the default limit accepts).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_GroupsTimesKeysIsBoundedBeforeAllocation()
    {
        // Arrange
        var json = SequenceSpec(100, """[{"impute":"id","key":"id","groupby":["id"]}]""");

        // Act
        var table = Run(json);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxDataRows = 5000 }));

        // Assert
        Assert.Equal(10_000, table.RowCount);
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("(MaxDataRows)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a sequence of key values that is far too long is rejected before it is materialized, located at the transform.
    /// </summary>
    [Theory]
    [InlineData("{\"start\":0,\"stop\":1e12}")]
    [InlineData("{\"stop\":1e300,\"step\":1e-300}")]
    public void AggregateTransforms_Impute_HugeKeySequencesAreRejectedUpFront(string keyvals)
    {
        // Arrange
        var json = Spec(Series, "[{\"impute\":\"y\",\"key\":\"x\",\"keyvals\":" + keyvals + "}]");

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json));

        // Assert
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("the limit of 1000000 (MaxDataRows)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a list of key values longer than the row limit is rejected too.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_LongKeyListsAreRejected()
    {
        // Arrange
        var json = Spec(Series, "[{\"impute\":\"y\",\"key\":\"x\",\"keyvals\":{\"start\":0,\"stop\":2000}}]");

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxDataRows = 1000 }));

        // Assert
        Assert.Contains("(MaxDataRows)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the field limit and the string limit for the value, located at the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_FieldAndStringLimitsAreLocated()
    {
        // Arrange
        var json = Spec("""[{"x":1,"g":"a","y":1},{"x":2,"g":"b","y":1}]""", """[{"impute":"w","key":"k","groupby":["g"]}]""");
        var text = Spec(Series, """[{"impute":"y","key":"x","value":"a_value_longer_than_twenty_five"}]""");

        // Act
        var fields = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxFieldCount = 4 }));
        var value = Assert.Throws<VegaLiteRenderException>(() => Run(text, new VegaLiteLimits { MaxStringLength = 20 }, parseWithDefaults: true));

        // Assert: g, w, k, x and y would be five fields
        Assert.Contains("MaxFieldCount", fields.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", fields.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxStringLength", value.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", value.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the work is charged to the step budget (a failure is located and names the limit; a larger budget succeeds).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_WorkIsChargedToTheStepBudget()
    {
        // Arrange
        var json = SequenceSpec(10_000, """[{"impute":"id","key":"id","groupby":["id"]}]""");
        var mean = SequenceSpec(10_000, """[{"impute":"id","key":"id","method":"mean"}]""");

        // Act
        var table = Run(mean);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxDataRows = 20_000_000, MaxEvaluationSteps = 100 }));

        // Assert: one series with every key present adds nothing
        Assert.Equal(10_000, table.RowCount);
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Impute_CancellationIsPolled() =>
        Assert.Throws<OperationCanceledException>(() => ApplyCanceled(Spec(Series, """[{"impute":"y","key":"x","groupby":["g"],"keyvals":[1,2,3]}]""")));

    /// <summary>
    ///     Validates that every grouped transform gives the same result on a second run and under other cultures, in a pipeline that
    ///     feeds each into the next.
    /// </summary>
    [Fact]
    public void AggregateTransforms_GroupedTransforms_AreDeterministicAndCultureInvariant()
    {
        // Arrange
        var json = Spec(
            """[{"x":1.5,"g":"a","k":1},{"x":2.25,"g":"b","k":1},{"x":9.75,"g":"a","k":3},{"x":1234.5,"g":"b","k":2},{"x":0.1,"g":"a","k":2}]""",
            """
            [{"impute":"x","key":"k","groupby":["g"],"method":"mean","keyvals":[1,2,3,4]},
             {"joinaggregate":[{"op":"mean","field":"x","as":"m"},{"op":"ci0","field":"x","as":"c"}],"groupby":["g"]},
             {"window":[{"op":"cume_dist","as":"cd"},{"op":"sum","field":"x","as":"run"}],"groupby":["g"],"sort":[{"field":"k"}]},
             {"stack":"x","groupby":["k"],"offset":"normalize","sort":[{"field":"g"}]}]
            """);
        string Describe() => string.Join(";", Rows(Run(json), "g", "k", "m", "c", "cd", "run", "x_start", "x_end"));
        var expected = Describe();

        // Act / Assert
        Assert.Equal(expected, Describe());
        Assert.Equal(expected, Expressions.ExpressionTestSupport.UnderCulture("tr-TR", Describe));
        Assert.Equal(expected, Expressions.ExpressionTestSupport.UnderCulture("de-DE", Describe));
    }
}
