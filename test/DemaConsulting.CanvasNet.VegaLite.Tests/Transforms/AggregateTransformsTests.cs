using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>aggregate</c> and <c>bin</c> transforms: grouping by structural keys in first-seen order, every
///     measure, the output naming, the row budget and cell limits, the charging of work to the step budget, cancellation,
///     determinism, and the assignment of values to bins.
/// </summary>
public class AggregateTransformsTests
{
    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops promptly.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The rows of the grouping tests: three groups, the second seen before the first is seen again.</summary>
    private const string Grouped = """[{"k":"b","v":1},{"k":"a","v":2},{"k":"b","v":3},{"k":"a","v":4},{"k":"c","v":5}]""";

    /// <summary>
    ///     Parses a specification and builds everything a transform run shares, without running any transform.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="parseWithDefaults">Whether the specification is parsed with the default limits.</param>
    /// <returns>The loaded table, the context, the loader and the parsed specification.</returns>
    private static (DataTable Table, TransformContext Context, DataLoader Loader, SpecTree Tree) Prepare(string json, VegaLiteLimits? limits = null, bool parseWithDefaults = false) =>
        PrepareWith(json, limits, Ct, parseWithDefaults);

    /// <summary>
    ///     Parses a specification and builds everything a transform run shares, with the transforms running under a given token.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="token">The token the transforms run under.</param>
    /// <param name="parseWithDefaults">Whether the specification is parsed with the default limits.</param>
    /// <returns>The loaded table, the context, the loader and the parsed specification.</returns>
    private static (DataTable Table, TransformContext Context, DataLoader Loader, SpecTree Tree) PrepareWith(string json, VegaLiteLimits? limits, CancellationToken token, bool parseWithDefaults = false)
    {
        var tree = SpecParser.Parse(json, "t.vl.json", parseWithDefaults ? null : limits);
        var effective = limits ?? VegaLiteLimits.Default;
        var loader = new DataLoader(tree.SourceName, effective, null, tree.Root["datasets"], null, Ct);
        var table = loader.Load(tree.Root["data"]);
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(effective, token, values);
        ParameterEnvironment.Populate(tree, null, values, evaluator);
        return (table, new TransformContext(tree, loader, evaluator, values, effective, token, []), loader, tree);
    }

    /// <summary>
    ///     Parses a specification, loads its data and applies its transforms the way a render does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="parseWithDefaults">Whether the specification is parsed with the default limits (so that the limits of the run can be lower than those of the parse).</param>
    /// <returns>The transformed table.</returns>
    private static DataTable Run(string json, VegaLiteLimits? limits = null, bool parseWithDefaults = false)
    {
        var (table, context, _, tree) = Prepare(json, limits, parseWithDefaults);
        return RowTransforms.Apply(table, tree.Root["transform"], context);
    }

    /// <summary>
    ///     Builds a specification with inline rows and the given transforms.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string rows, string transforms) =>
        "{\"data\":{\"values\":" + rows + "},\"transform\":" + transforms + ",\"mark\":\"point\"}";

    /// <summary>
    ///     Builds a specification whose data is a sequence of ids.
    /// </summary>
    /// <param name="count">The number of rows.</param>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <returns>The specification text.</returns>
    private static string SequenceSpec(int count, string transforms) =>
        "{\"data\":{\"sequence\":{\"start\":0,\"stop\":" + count + ",\"as\":\"id\"}},\"transform\":" + transforms + ",\"mark\":\"point\"}";

    /// <summary>
    ///     Lists the rows of a table as text: the values of the fields joined with a bar.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="fields">The fields to show.</param>
    /// <returns>One text per row.</returns>
    private static string[] Rows(DataTable table, params string[] fields) =>
        [.. Enumerable.Range(0, table.RowCount).Select(row => string.Join("|", fields.Select(f => table.GetValue(row, f).ToText())))];

    /// <summary>
    ///     Validates that groups are keyed by the group-by fields and appear in the order they are first seen, one output row each,
    ///     with the group fields first and then the measures.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_GroupsByStructuralKeysInFirstSeenOrder()
    {
        // Arrange
        var json = Spec(Grouped, """[{"aggregate":[{"op":"sum","field":"v"}],"groupby":["k"]}]""");

        // Act
        var table = Run(json);

        // Assert: b is seen first (1 + 3), then a (2 + 4), then c
        Assert.Equal(["k", "sum_v"], table.Fields);
        Assert.Equal(["b|4", "a|6", "c|5"], Rows(table, "k", "sum_v"));
    }

    /// <summary>
    ///     Validates that the key of a group is the structure of the values: a number and a text that spell the same are different
    ///     groups, true and null are groups of their own, and equal values share a group however far apart the rows are.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_KeysAreStructuralNotTextual()
    {
        // Arrange
        var json = Spec(
            """[{"k":1},{"k":"1"},{"k":1},{"k":"1"},{"k":true},{"k":null},{"k":1.0}]""",
            """[{"aggregate":[{"op":"count"}],"groupby":["k"]}]""");

        // Act
        var table = Run(json);

        // Assert: 1 (three rows, 1.0 is 1), "1" (two), true (one), null (one)
        Assert.Equal(4, table.RowCount);
        Assert.Equal(["1|3", "1|2", "true|1", "null|1"], Rows(table, "k", "count"));
        Assert.True(table.GetValue(0, "k").IsNumber);
        Assert.True(table.GetValue(1, "k").IsString);
        Assert.True(table.GetValue(2, "k").IsBoolean);
        Assert.True(table.GetValue(3, "k").IsNull);
    }

    /// <summary>
    ///     Validates that NaN is one group (NaN equals NaN as a key) and that zero and negative zero are one group.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_NaNAndSignedZeroFormOneGroupEach()
    {
        // Arrange: v = 1..5; n is NaN for odd v and a signed zero otherwise
        var json = Spec(
            Grouped,
            """
            [{"calculate":"datum.v % 2 == 1 ? NaN : (datum.v == 2 ? 0 : -0)","as":"n"},
             {"aggregate":[{"op":"count"}],"groupby":["n"]}]
            """);

        // Act
        var table = Run(json);

        // Assert: v 1, 3, 5 are NaN; v 2 and 4 are 0 and -0
        Assert.Equal(["NaN|3", "0|2"], Rows(table, "n", "count"));
    }

    /// <summary>
    ///     Validates that arrays and objects are compared by content, not by identity, and that the members of an object are
    ///     compared regardless of their order.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_ArraysAndObjectsAreComparedByContent()
    {
        // Arrange: v % 2 is 1, 0, 1, 0, 1
        var arrays = Spec(
            Grouped,
            """
            [{"calculate":"[datum.v % 2, 'x']","as":"arr"},
             {"aggregate":[{"op":"count"}],"groupby":["arr"]}]
            """);
        var objects = Spec(
            Grouped,
            """
            [{"calculate":"datum.v % 2 == 0 ? {a: 1, b: 2} : {b: 2, a: 1}","as":"obj"},
             {"aggregate":[{"op":"count"}],"groupby":["obj"]}]
            """);
        var mixed = Spec(
            Grouped,
            """
            [{"calculate":"[datum.v % 2, 'x']","as":"arr"},
             {"aggregate":[{"op":"count"}],"groupby":["k", "arr"]}]
            """);

        // Act
        var byArray = Run(arrays);
        var byObject = Run(objects);
        var byBoth = Run(mixed);

        // Assert
        Assert.Equal(["1,x|3", "0,x|2"], Rows(byArray, "arr", "count"));
        Assert.True(byArray.GetValue(0, "arr").IsArray);
        Assert.Equal(1, byObject.RowCount);
        Assert.Equal(5, byObject.GetValue(0, "count").Number);
        Assert.Equal(["b|1,x|2", "a|0,x|2", "c|1,x|1"], Rows(byBoth, "k", "arr", "count"));
    }

    /// <summary>
    ///     Validates the output names: a count of rows is <c>count</c>, other measures are <c>op_field</c> (the canonical name, so
    ///     <c>average</c> is <c>mean</c>), <c>as</c> overrides, the fields are in ordinal order, and a later write of a name wins.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_CountWithoutFieldAndAsDefaults()
    {
        // Arrange
        var named = Spec(
            Grouped,
            """[{"aggregate":[{"op":"count"},{"op":"sum","field":"v"},{"op":"average","field":"v"},{"op":"max","field":"v","as":"top"}]}]""");
        var clash = Spec(Grouped, """[{"aggregate":[{"op":"count","as":"k"}],"groupby":["k"]}]""");

        // Act
        var table = Run(named);
        var replaced = Run(clash);

        // Assert
        Assert.Equal(["count", "mean_v", "sum_v", "top"], table.Fields);
        Assert.Equal("5|3|15|5", Rows(table, "count", "mean_v", "sum_v", "top").Single());
        Assert.Equal(["k"], replaced.Fields);
        Assert.Equal(["2", "2", "1"], Rows(replaced, "k"));
    }

    /// <summary>
    ///     Validates that an input without rows has no groups (so no output row), with and without a group-by.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_EmptyInputYieldsNoRows()
    {
        // Arrange
        var plain = Spec("[]", """[{"aggregate":[{"op":"count"}]}]""");
        var grouped = Spec("[]", """[{"aggregate":[{"op":"sum","field":"v"}],"groupby":["k"]}]""");

        // Act
        var none = Run(plain);
        var noGroups = Run(grouped);

        // Assert
        Assert.Equal(0, none.RowCount);
        Assert.Equal(["count"], none.Fields);
        Assert.Equal(0, noGroups.RowCount);
        Assert.Equal(["k", "sum_v"], noGroups.Fields);
    }

    /// <summary>
    ///     Validates that without a group-by every row is in one group and the output is a single row.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_NoGroupByYieldsOneRow()
    {
        // Arrange
        var json = Spec(Grouped, """[{"aggregate":[{"op":"sum","field":"v"},{"op":"min","field":"k"},{"op":"distinct","field":"k"}]}]""");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(1, table.RowCount);
        Assert.Equal("a|3|15", Rows(table, "min_k", "distinct_k", "sum_v").Single());
    }

    /// <summary>
    ///     Validates that a nested group-by path is kept nested in the output and groups by the value at the path.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_NestedGroupByPathStaysNested()
    {
        // Arrange
        var json = Spec(
            """[{"p":{"q":"x"},"v":1},{"p":{"q":"y"},"v":2},{"p":{"q":"x"},"v":4}]""",
            """[{"aggregate":[{"op":"sum","field":"v","as":"s"}],"groupby":["p.q"]}]""");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(["p.q", "s"], table.Fields);
        Assert.Equal(["x|5", "y|2"], Rows(table, "p.q", "s"));
    }

    /// <summary>
    ///     Validates every aggregate operation through the transform over the sample 2, 4, 4, 4, 5, 5, 7, 9.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_ComputesEveryOperation()
    {
        // Arrange
        var rows = "[" + string.Join(",", new[] { 2, 4, 4, 4, 5, 5, 7, 9 }.Select(v => "{\"x\":" + v + "}")) + "]";
        var ops = new[]
        {
            "count", "valid", "missing", "distinct", "sum", "product", "mean", "variance", "variancep", "stdev", "stdevp", "stderr",
            "median", "q1", "q3", "min", "max",
        };
        var measures = string.Join(",", ops.Select(op => "{\"op\":\"" + op + "\",\"field\":\"x\",\"as\":\"" + op + "\"}"));

        // Act
        var table = Run(Spec(rows, "[{\"aggregate\":[" + measures + "]}]"));

        // Assert: the values worked out by hand (sum 40, product 201,600, variance 32/7, quartiles 4, 4.5, 5.5)
        double[] expected = [8, 8, 0, 5, 40, 201_600, 5, 32.0 / 7, 4, Math.Sqrt(32.0 / 7), 2, Math.Sqrt(32.0 / 56), 4.5, 4, 5.5, 2, 9];
        for (var i = 0; i < ops.Length; i++)
        {
            Assert.Equal(expected[i], table.GetValue(0, ops[i]).Number, 9);
        }
    }

    /// <summary>
    ///     Validates the operations over a field that no row has: the counts are the row count and zero, the others are null.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_AMissingFieldIsMissingInEveryRow()
    {
        // Arrange
        var json = Spec(
            Grouped,
            """[{"aggregate":[{"op":"missing","field":"zzz","as":"m"},{"op":"valid","field":"zzz","as":"ok"},{"op":"sum","field":"zzz","as":"s"},{"op":"min","field":"zzz","as":"lo"}]}]""");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal("5|0|null|null", Rows(table, "m", "ok", "s", "lo").Single());
    }

    /// <summary>
    ///     Validates that argmax and argmin return the row (as an object with every field) that holds the extreme value, and that
    ///     values returns the array of the field values in row order.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_ArgOperationsReturnTheRowAndValuesTheArray()
    {
        // Arrange
        var json = Spec(
            """[{"k":"a","v":1},{"k":"a","v":9},{"k":"b","v":4},{"k":"a","v":9}]""",
            """[{"aggregate":[{"op":"argmax","field":"v","as":"top"},{"op":"argmin","field":"v","as":"low"},{"op":"values","field":"v","as":"all"}],"groupby":["k"]}]""");

        // Act
        var table = Run(json);

        // Assert: group a has the maximum 9 first in row 1 and the minimum 1 in row 0; group b has one row
        var top = table.GetValue(0, "top").ObjectValue!;
        var low = table.GetValue(0, "low").ObjectValue!;
        Assert.Equal(["k", "v"], top.Keys);
        Assert.Equal(9, top.ValueAt(1).Number);
        Assert.Equal(1, low.ValueAt(1).Number);
        Assert.Equal("a", top.ValueAt(0).Text);
        Assert.Equal("1,9,9", table.GetValue(0, "all").ToText());
        Assert.Equal("4", table.GetValue(1, "all").ToText());
        Assert.Equal(4, table.GetValue(1, "top").ObjectValue!.ValueAt(1).Number);
    }

    /// <summary>
    ///     Validates the exponential operations through the transform, with the parameter of the specification and the default 0.5.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_ExponentialUsesTheParameter()
    {
        // Arrange
        var json = Spec(
            """[{"v":1},{"v":2},{"v":3}]""",
            """[{"aggregate":[{"op":"exponentialb","field":"v","param":0.5,"as":"b"},{"op":"exponential","field":"v","as":"u"},{"op":"exponential","field":"v","param":1,"as":"one"}]}]""");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(2.125, table.GetValue(0, "b").Number, 9);
        Assert.Equal(17.0 / 7, table.GetValue(0, "u").Number, 9);
        Assert.Equal(2, table.GetValue(0, "one").Number, 9);
    }

    /// <summary>
    ///     Validates that the confidence interval is deterministic for a transform and differs between two transforms (the seed is the
    ///     JSON pointer), and that it brackets the mean.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_BootstrapIntervalIsSeededByThePointer()
    {
        // Arrange: the same aggregation as transform 0 and (after a calculate) as transform 1
        var rows = "[" + string.Join(",", Enumerable.Range(1, 12).Select(v => "{\"v\":" + (v * v) + "}")) + "]";
        var measures = """[{"op":"ci0","field":"v","as":"lo"},{"op":"ci1","field":"v","as":"hi"},{"op":"mean","field":"v","as":"m"}]""";
        var first = Spec(rows, "[{\"aggregate\":" + measures + "}]");
        var second = Spec(rows, "[{\"calculate\":\"1\",\"as\":\"z\"},{\"aggregate\":" + measures + "}]");

        // Act
        var a = Run(first);
        var again = Run(first);
        var b = Run(second);

        // Assert: the mean of the squares 1..144 is 650 / 12
        Assert.Equal(a.GetValue(0, "lo").Number, again.GetValue(0, "lo").Number);
        Assert.Equal(a.GetValue(0, "hi").Number, again.GetValue(0, "hi").Number);
        Assert.NotEqual(a.GetValue(0, "lo").Number, b.GetValue(0, "lo").Number);
        Assert.InRange(a.GetValue(0, "lo").Number, 1, a.GetValue(0, "m").Number);
        Assert.InRange(a.GetValue(0, "hi").Number, a.GetValue(0, "m").Number, 144);
        Assert.Equal(650.0 / 12, a.GetValue(0, "m").Number, 9);
    }

    /// <summary>
    ///     Validates that the result of every transform is the same under other cultures and on a second run.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Apply_IsDeterministicAndCultureInvariant()
    {
        // Arrange
        var json = Spec(
            """[{"x":1.5,"g":"a"},{"x":2.25,"g":"b"},{"x":9.75,"g":"a"},{"x":1234.5,"g":"b"}]""",
            """
            [{"bin":{"maxbins":4},"field":"x","as":["lo","hi"]},
             {"aggregate":[{"op":"mean","field":"x"},{"op":"ci1","field":"x"},{"op":"median","field":"x"},{"op":"max","field":"hi"}],"groupby":["g"]}]
            """);
        string Describe() => string.Join(";", Rows(Run(json), "g", "mean_x", "ci1_x", "median_x", "max_hi"));
        var expected = Describe();

        // Act / Assert
        Assert.Equal(expected, Describe());
        Assert.Equal(expected, ExpressionTestSupport.UnderCulture("tr-TR", Describe));
        Assert.Equal(expected, ExpressionTestSupport.UnderCulture("de-DE", Describe));
    }

    /// <summary>
    ///     Validates that the groups are counted in the row budget up front: with a tiny budget the transform fails located at
    ///     the entry as soon as the budget is reached, and with the exact budget it succeeds (the input counts until the output is
    ///     complete, so four rows and four groups need eight).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_GroupLimitIsALocatedFailure()
    {
        // Arrange
        var json = Spec("""[{"id":1},{"id":2},{"id":3},{"id":4}]""", """[{"aggregate":[{"op":"count"}],"groupby":["id"]}]""");

        // Act
        var table = Run(json, new VegaLiteLimits { MaxDataRows = 8 });
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxDataRows = 7 }));

        // Assert
        Assert.Equal(4, table.RowCount);
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("(MaxDataRows)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the rows of the input are given back to the row budget when the output is complete, unless the input stays
    ///     in use.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_ReleasesTheInputRowsOnlyWhenAsked()
    {
        // Arrange
        var json = Spec(Grouped, """[{"aggregate":[{"op":"count"}],"groupby":["k"]}]""");
        var (table, context, loader, _) = Prepare(json);
        var (table2, context2, loader2, _) = Prepare(json);
        var spec = new[] { new AggregateSpec(AggregateOp.Count, null, 0.5, "n") };
        var where = new VegaLiteLocation(null, "/transform/0", 1, 1);

        // Act
        var released = new AggregateTransforms(context).Aggregate(table, ["k"], spec, where, 1, releaseInput: true);
        var kept = new AggregateTransforms(context2).Aggregate(table2, ["k"], spec, where, 1, releaseInput: false);

        // Assert: five rows were loaded; the three groups are the whole budget afterwards, or come on top of the input
        Assert.Equal(3, released.RowCount);
        Assert.Equal(3, loader.RowsLoaded);
        Assert.Equal(3, kept.RowCount);
        Assert.Equal(8, loader2.RowsLoaded);
    }

    /// <summary>
    ///     Validates that the cells of the groups are bounded as the groups appear: with many measures a group costs hundreds of
    ///     cells, so tens of thousands of groups exceed the cell limit, and the failure is located at the entry.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_GroupStateIsBoundedByTheCellLimit()
    {
        // Arrange: 120 measures cost 121 output cells and 480 state cells per group, 601 in all; 60,000 groups are 36 million
        var measures = string.Join(",", Enumerable.Range(0, 120).Select(i => "{\"op\":\"count\",\"as\":\"c" + i + "\"}"));
        var json = SequenceSpec(60_000, "[{\"aggregate\":[" + measures + "],\"groupby\":[\"id\"]}]");

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json));

        // Assert
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("the limit of 33554432 cells (TableLayout.MaxCells)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the values the operations keep in memory are bounded up front by rows times such operations.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_ValueStateIsBoundedUpFront()
    {
        // Arrange: 600 medians over 60,000 rows would keep 36 million values
        var measures = string.Join(",", Enumerable.Range(0, 600).Select(i => "{\"op\":\"median\",\"field\":\"id\",\"as\":\"m" + i + "\"}"));
        var json = SequenceSpec(60_000, "[{\"aggregate\":[" + measures + "]}]");

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json));

        // Assert
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("the limit of 33554432 values (TableLayout.MaxCells)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that more output fields than the field limit allow fail located at the entry before any row is read.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_FieldLimitIsALocatedFailure()
    {
        // Arrange
        var json = Spec(Grouped, """[{"aggregate":[{"op":"count","as":"a"},{"op":"count","as":"b"},{"op":"count","as":"c"}],"groupby":["k"]}]""");

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxFieldCount = 3 }));

        // Assert
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("MaxFieldCount", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the work of the aggregation is charged to the step budget: the rows, the sort of a median over many rows and
    ///     the bootstrap; exhaustion is a located failure that names the limit, and a larger budget succeeds.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_StepBudgetExhaustionIsLocated()
    {
        // Arrange: 5,000 rows; sorting 5,000 values costs 5,000 * 13 / 64 = 1,015 steps
        var rows = "[" + string.Join(",", Enumerable.Range(0, 5000).Select(i => "{\"v\":" + ((i * 7919) % 5003) + "}")) + "]";
        var median = Spec(rows, """[{"aggregate":[{"op":"median","field":"v"}]}]""");
        var bootstrap = Spec("[" + string.Join(",", Enumerable.Range(0, 100).Select(i => "{\"v\":" + i + "}")) + "]", """[{"aggregate":[{"op":"ci0","field":"v"}]}]""");

        // Act
        var table = Run(median, new VegaLiteLimits { MaxEvaluationSteps = 100_000 });
        var sorted = Assert.Throws<VegaLiteRenderException>(() => Run(median, new VegaLiteLimits { MaxEvaluationSteps = 500 }));
        var resampled = Assert.Throws<VegaLiteRenderException>(() => Run(bootstrap, new VegaLiteLimits { MaxEvaluationSteps = 500 }));

        // Assert
        Assert.True(table.GetValue(0, "median_v").IsNumber);
        Assert.Equal("/transform/0", sorted.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", sorted.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", resampled.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", resampled.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the per-row work is charged: a group-by pass over many rows exhausts a very small budget.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_RowWorkIsCharged()
    {
        // Arrange: each row costs two elements (one group field plus one) in the grouping pass: 20,000 elements are 312 steps
        var json = SequenceSpec(10_000, """[{"aggregate":[{"op":"count"}],"groupby":["id"]}]""");

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxEvaluationSteps = 100 }));

        // Assert
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that hashing the text of a group value is charged for every row, so many rows with a long group text exhaust a
    ///     small budget although they have few instructions and few elements.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_LongGroupTextIsChargedPerRow()
    {
        // Arrange: 200 rows whose group text has 2,000 characters cost 31 steps each (6,200 steps); the other work is 400 elements
        var group = new string('g', 2000);
        var rows = "[" + string.Join(",", Enumerable.Range(0, 200).Select(i => "{\"k\":\"" + group + "\",\"v\":" + i + "}")) + "]";
        var json = Spec(rows, """[{"aggregate":[{"op":"count"}],"groupby":["k"]}]""");

        // Act
        var table = Run(json, new VegaLiteLimits { MaxEvaluationSteps = 100_000 });
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxEvaluationSteps = 1_000 }));

        // Assert: a large budget groups the rows into one group, a small one fails with the limit named
        Assert.Equal(1, table.RowCount);
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a text of the specification longer than the string limit of the call is rejected, located at the entry:
    ///     an output name, and a group value (an array) whose structural text is too long.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_LongTextsRespectMaxStringLength()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 20 };
        var name = Spec(Grouped, """[{"aggregate":[{"op":"count","as":"a_name_longer_than_twenty_five_chars"}]}]""");
        var group = Spec(
            """[{"a":["01234567","01234567"]}]""",
            """[{"aggregate":[{"op":"count"}],"groupby":["a"]}]""");

        // Act
        var byName = Assert.Throws<VegaLiteRenderException>(() => Run(name, limits, parseWithDefaults: true));
        var byGroup = Assert.Throws<VegaLiteRenderException>(() => Run(group, limits, parseWithDefaults: true));

        // Assert
        Assert.Contains("MaxStringLength", byName.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", byName.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxStringLength", byGroup.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", byGroup.Location.Pointer);
    }

    /// <summary>
    ///     Validates that every loop of the aggregation polls the cancellation token (it is already canceled when the first
    ///     row of each pass is reached).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Aggregate_CancellationIsPolled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var json = Spec(Grouped, """[{"aggregate":[{"op":"sum","field":"v"}],"groupby":["k"]}]""");
        var (table, context, _, _) = PrepareWith(json, null, source.Token);
        source.Cancel();
        var where = new VegaLiteLocation(null, "/transform/0", 1, 1);
        var measures = new[] { new AggregateSpec(AggregateOp.Sum, "v", 0.5, "s") };

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => new AggregateTransforms(context).Aggregate(table, ["k"], measures, where, 1, true));
        Assert.Throws<OperationCanceledException>(() => new AggregateTransforms(context).Aggregate(table, [], measures, where, 1, true));
    }

    /// <summary>
    ///     Validates the lower and the upper boundary of the bin of every row for the extent 0 to 100 (step 10): the boundary of a
    ///     value on an edge, the closed last bin, and null for null, text that is not a number, and the empty text.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_AssignsBoundariesAndNullsForInvalidValues()
    {
        // Arrange
        var json = Spec(
            """[{"x":0},{"x":9.999},{"x":10},{"x":55.5},{"x":100},{"x":null},{"x":"abc"},{"x":""},{"x":25},{"x":"35"}]""",
            """[{"bin":{"maxbins":10},"field":"x","as":["lo","hi"]}]""");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(
            ["0|10", "0|10", "10|20", "50|60", "90|100", "null|null", "null|null", "null|null", "20|30", "30|40"],
            Rows(table, "lo", "hi"));
        Assert.Equal(["hi", "lo", "x"], table.Fields);
        Assert.Equal(10, table.RowCount);
    }

    /// <summary>
    ///     Validates the output names: one name gives the name and <c>name_end</c>, two names are used as they are, none gives
    ///     <c>bin0</c> and <c>bin1</c>, and an existing field of the name is replaced.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_NamesTheOutputFields()
    {
        // Arrange
        var rows = """[{"x":0},{"x":100}]""";
        var one = Spec(rows, """[{"bin":true,"field":"x","as":"b"}]""");
        var none = Spec(rows, """[{"bin":true,"field":"x"}]""");
        var replace = Spec(rows, """[{"bin":true,"field":"x","as":["x","y"]}]""");

        // Act
        var named = Run(one);
        var defaults = Run(none);
        var replaced = Run(replace);

        // Assert
        Assert.Equal(["b", "b_end", "x"], named.Fields);
        Assert.Equal(["bin0", "bin1", "x"], defaults.Fields);
        Assert.Equal(["x", "y"], replaced.Fields);
        Assert.Equal(["0|10", "90|100"], Rows(replaced, "x", "y"));
    }

    /// <summary>
    ///     Validates that a bin transform asks for ten bins by default (a hundred-wide extent has the step 10) and that an
    ///     explicit <c>maxbins</c> or <c>step</c> changes it.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_DefaultsToTenBinsAndHonorsMaxbinsAndStep()
    {
        // Arrange
        var rows = """[{"x":0},{"x":42},{"x":100}]""";

        // Act
        var byDefault = Run(Spec(rows, """[{"bin":true,"field":"x","as":"b"}]"""));
        var twenty = Run(Spec(rows, """[{"bin":{"maxbins":20},"field":"x","as":"b"}]"""));
        var stepped = Run(Spec(rows, """[{"bin":{"step":25},"field":"x","as":"b"}]"""));

        // Assert: 42 is in [40, 50) with step 10, [40, 45) with step 5 and [25, 50) with step 25
        Assert.Equal(["0|10", "40|50", "90|100"], Rows(byDefault, "b", "b_end"));
        Assert.Equal(["0|5", "40|45", "95|100"], Rows(twenty, "b", "b_end"));
        Assert.Equal(["0|25", "25|50", "75|100"], Rows(stepped, "b", "b_end"));
    }

    /// <summary>
    ///     Validates that an explicit extent decides the bins: values outside go to the bins at negative and positive infinity.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_ExplicitExtentSendsOutliersToInfinity()
    {
        // Arrange: the extent 0 to 50 with five bins is step 10
        var json = Spec(
            """[{"x":10},{"x":60},{"x":-5},{"x":50}]""",
            """[{"bin":{"extent":[0,50],"maxbins":5},"field":"x","as":["lo","hi"]}]""");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(["10|20", "Infinity|Infinity", "-Infinity|-Infinity", "40|50"], Rows(table, "lo", "hi"));
    }

    /// <summary>
    ///     Validates the anchor: with the extent 0 to 100 and the anchor 5 the bins run from 5 to 105, so 0 is below the first bin.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_AnchorShiftsTheBins()
    {
        // Arrange
        var json = Spec(
            """[{"x":0},{"x":5},{"x":14.9},{"x":100}]""",
            """[{"bin":{"maxbins":10,"anchor":5,"extent":[0,100]},"field":"x","as":["lo","hi"]}]""");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(["-Infinity|-Infinity", "5|15", "5|15", "95|105"], Rows(table, "lo", "hi"));
    }

    /// <summary>
    ///     Validates that infinite values and NaN do not enter the extent and get null boundaries, and that the extent of the rest
    ///     decides the bins (the finite values 1 and 2 with ten bins have the step 0.1).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_NonFiniteValuesAreIgnored()
    {
        // Arrange
        var json = Spec(
            """[{"x":1},{"x":2},{"x":3},{"x":4}]""",
            """
            [{"calculate":"datum.x == 3 ? 1/0 : (datum.x == 4 ? NaN : datum.x)","as":"y"},
             {"bin":true,"field":"y","as":["lo","hi"]}]
            """);

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(1, table.GetValue(0, "lo").Number, 9);
        Assert.Equal(1.1, table.GetValue(0, "hi").Number, 9);
        Assert.Equal(1.9, table.GetValue(1, "lo").Number, 9);
        Assert.Equal(2, table.GetValue(1, "hi").Number, 9);
        Assert.True(table.GetValue(2, "lo").IsNull);
        Assert.True(table.GetValue(3, "hi").IsNull);
    }

    /// <summary>
    ///     Validates that a field without any valid value (or without rows) bins to null everywhere without failing.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_NoValidValuesGiveNulls()
    {
        // Arrange
        var empty = Spec("[]", """[{"bin":true,"field":"x","as":"b"}]""");
        var none = Spec("""[{"x":null},{"y":1},{"x":"q"}]""", """[{"bin":true,"field":"x","as":"b"}]""");

        // Act
        var noRows = Run(empty);
        var nulls = Run(none);

        // Assert
        Assert.Equal(0, noRows.RowCount);
        Assert.Equal(["b", "b_end"], noRows.Fields);
        Assert.Equal(["null|null", "null|null", "null|null"], Rows(nulls, "b", "b_end"));
    }

    /// <summary>
    ///     Validates that bins that cannot be computed (a subnormal extent) and bins above the scale-domain limit are located
    ///     failures that name the limit.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_UnbinnableExtentsAndTooManyBinsFail()
    {
        // Arrange
        var tiny = Spec("""[{"x":0},{"x":5e-324}]""", """[{"bin":true,"field":"x","as":"b"}]""");
        var many = Spec("""[{"x":0},{"x":1}]""", """[{"bin":{"maxbins":60},"field":"x","as":"b"}]""");

        // Act
        var unbinnable = Assert.Throws<VegaLiteRenderException>(() => Run(tiny));
        var limited = Assert.Throws<VegaLiteRenderException>(() => Run(many, new VegaLiteLimits { MaxScaleDomainSize = 50 }));

        // Assert
        Assert.Contains("cannot be binned", unbinnable.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", unbinnable.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxScaleDomainSize", limited.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", limited.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a bin transform leaves the rows and the row budget alone, and that its work is charged and its output
    ///     name bounded.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_KeepsRowsAndChargesWork()
    {
        // Arrange
        var json = SequenceSpec(10_000, """[{"bin":true,"field":"id","as":"b"}]""");
        var (table, context, loader, tree) = Prepare(json);
        var longName = Spec("""[{"x":1}]""", """[{"bin":true,"field":"x","as":"a_name_longer_than_twenty_five_chars"}]""");

        // Act
        var binned = RowTransforms.Apply(table, tree.Root["transform"], context);
        var budget = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxEvaluationSteps = 100 }));
        var name = Assert.Throws<VegaLiteRenderException>(() => Run(longName, new VegaLiteLimits { MaxStringLength = 25 }, parseWithDefaults: true));

        // Assert: a sequence of 10,000 ids bins into ten bins of 1,000; the rows are not counted again
        Assert.Equal(10_000, binned.RowCount);
        Assert.Equal(10_000, loader.RowsLoaded);
        Assert.Equal("0|1000", Rows(binned, "b", "b_end")[0]);
        Assert.Equal("9000|10000", Rows(binned, "b", "b_end")[^1]);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", budget.Message, StringComparison.Ordinal);
        Assert.Contains("MaxStringLength", name.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the bin transform polls the cancellation token.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Bin_CancellationIsPolled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var json = Spec(Grouped, """[{"bin":true,"field":"v","as":"b"}]""");
        var (table, context, _, tree) = PrepareWith(json, null, source.Token);
        var entry = tree.Root["transform"]!.Items[0];
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => AggregateTransforms.Apply(table, entry, SpecParser.FindTransformOperation(entry)!, context));
    }

    /// <summary>
    ///     Validates the meter: it pays one step for every complete block of 64 elements, carries the remainder, and fails with the
    ///     limit named when the budget is spent.
    /// </summary>
    [Fact]
    public void AggregateTransforms_WorkMeter_PaysWholeBlocksAndCarriesTheRemainder()
    {
        // Arrange
        var context = new ExpressionFunctionContext(new VegaLiteLimits { MaxEvaluationSteps = 3 }, Ct);
        var meter = new WorkMeter(context);

        // Act
        meter.Add(63);
        var afterPartial = context.Steps;
        meter.Add(1);
        var afterCarry = context.Steps;
        meter.Add(0);
        meter.Add(-5);
        meter.Add(128);
        var afterBlocks = context.Steps;
        var exhausted = Assert.Throws<ExpressionRuntimeException>(() => meter.Add(64));

        // Assert
        Assert.Equal(0, afterPartial);
        Assert.Equal(1, afterCarry);
        Assert.Equal(3, afterBlocks);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exhausted.Message, StringComparison.Ordinal);
    }
}
