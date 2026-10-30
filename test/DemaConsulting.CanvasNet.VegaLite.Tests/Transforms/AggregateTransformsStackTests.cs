using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.AggregateTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>stack</c> transform and the stack engine the encoding stage shares: the three offsets, negative values,
///     groups, sorting, the output names, IEEE handling of NaN and zero sums, the limits, charging and cancellation. The expected values are
///     derived by hand from the public Vega documentation of the stack transform.
/// </summary>
public class AggregateTransformsStackTests
{
    /// <summary>
    ///     Stacks a field of rows.
    /// </summary>
    /// <param name="rows">The JSON rows.</param>
    /// <param name="stack">The members of the stack transform after <c>stack</c>, as JSON text (for example <c>"groupby":["g"]</c>); the field is <c>v</c>.</param>
    /// <returns>The resulting table.</returns>
    private static DataTable Stack(string rows, string stack = "") =>
        Run(Spec(rows, "[{\"stack\":\"v\"" + (stack.Length == 0 ? string.Empty : "," + stack) + "}]"));

    /// <summary>
    ///     Validates that the default offset <c>zero</c> stacks <c>[3, 2, 5]</c> from 0 in input order into (0, 3), (3, 5), (5, 10), with
    ///     the default output names <c>v_start</c> and <c>v_end</c> and the rows unchanged.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_ZeroOffsetStacksFromZero()
    {
        // Act
        var table = Stack(IdRows(3, 2, 5));

        // Assert
        Assert.Equal(["id", "v", "v_end", "v_start"], table.Fields);
        Assert.Equal(["0|3", "3|5", "5|10"], Rows(table, "v_start", "v_end"));
        Assert.Equal(["3", "2", "5"], Rows(table, "v"));
    }

    /// <summary>
    ///     Validates that with the zero offset a value below 0 stacks downwards from 0 separately from the positive ones: for
    ///     <c>[3, -4, 2, -1]</c> the positives are (0, 3), (3, 5) and the negatives (0, -4), (-4, -5).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_NegativeValuesStackSeparatelyDownwards()
    {
        // Act
        var table = Stack(IdRows(3, -4, 2, -1));

        // Assert
        Assert.Equal(["0|3", "0|-4", "3|5", "-4|-5"], Rows(table, "v_start", "v_end"));
    }

    /// <summary>
    ///     Validates the <c>normalize</c> offset on <c>[3, 2, 5]</c> (sum 10): (0, 0.3), (0.3, 0.5), (0.5, 1), and that negative values are
    ///     stacked by their absolute value.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_NormalizeScalesEveryGroupToOne()
    {
        // Act
        var table = Stack(IdRows(3, 2, 5), "\"offset\":\"normalize\"");
        var negative = Stack(IdRows(-1, 3), "\"offset\":\"normalize\"");

        // Assert
        Assert.Equal([0, 0.3, 0.5], Numbers(table, "v_start"), new Within());
        Assert.Equal([0.3, 0.5, 1], Numbers(table, "v_end"), new Within());
        Assert.Equal([0, 0.25], Numbers(negative, "v_start"), new Within());
        Assert.Equal([0.25, 1], Numbers(negative, "v_end"), new Within());
    }

    /// <summary>
    ///     Validates the <c>center</c> offset: two groups with sums 10 and 4 (the widest is 10), so the narrower group starts at
    ///     (10 - 4) / 2 = 3: group A (0, 4), (4, 10); group B (3, 4), (4, 7).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_CenterOffsetCentersOnTheWidestGroup()
    {
        // Arrange
        const string rows = """[{"g":"A","v":4},{"g":"B","v":1},{"g":"A","v":6},{"g":"B","v":3}]""";

        // Act
        var table = Stack(rows, "\"groupby\":[\"g\"],\"offset\":\"center\"");

        // Assert
        Assert.Equal(["0|4", "3|4", "4|10", "4|7"], Rows(table, "v_start", "v_end"));
    }

    /// <summary>
    ///     Validates that groups stack independently (rows of different groups interleaved) and that a group of structural keys
    ///     treats the number 1 and the text "1" as two groups.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_GroupsStackIndependently()
    {
        // Arrange
        const string rows = """[{"g":1,"v":2},{"g":"1","v":3},{"g":1,"v":4},{"g":"1","v":5}]""";

        // Act
        var table = Stack(rows, "\"groupby\":[\"g\"]");

        // Assert
        Assert.Equal(["0|2", "0|3", "2|6", "3|8"], Rows(table, "v_start", "v_end"));
    }

    /// <summary>
    ///     Validates the <c>sort</c>: the first row of the sort is at the bottom (descending puts the largest key first), the output keeps
    ///     the input row order, and rows with equal sort values stack in the input order.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_SortOrdersTheStackAndTiesKeepInputOrder()
    {
        // Arrange
        const string rows = """[{"s":"a","v":1},{"s":"b","v":2},{"s":"c","v":3},{"s":"b","v":4}]""";

        // Act
        var ascending = Stack(rows, "\"sort\":[{\"field\":\"s\"}]");
        var descending = Stack(rows, "\"sort\":[{\"field\":\"s\",\"order\":\"descending\"}]");

        // Assert: ascending a, b (2), b (4), c; descending c, b (2), b (4), a
        Assert.Equal(["0|1", "1|3", "7|10", "3|7"], Rows(ascending, "v_start", "v_end"));
        Assert.Equal(["9|10", "3|5", "0|3", "5|9"], Rows(descending, "v_start", "v_end"));
    }

    /// <summary>
    ///     Validates the output names: one name gives the name and <c>name_end</c>, two names are used as they are, and an existing field of
    ///     the name is replaced.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_NamesTheOutputFields()
    {
        // Act
        var one = Stack(IdRows(1, 2), "\"as\":\"u\"");
        var two = Stack(IdRows(1, 2), "\"as\":[\"lo\",\"hi\"]");
        var replace = Stack(IdRows(1, 2), "\"as\":[\"id\",\"v\"]");

        // Assert
        Assert.Equal(["id", "u", "u_end", "v"], one.Fields);
        Assert.Equal(["1|3"], Rows(one, "u", "u_end").Skip(1));
        Assert.Equal(["0|1", "1|3"], Rows(two, "lo", "hi"));
        Assert.Equal(["0|1", "1|3"], Rows(replace, "id", "v"));
    }

    /// <summary>
    ///     Validates how values are read: null is 0, numeric text is its number, text that is not a number is NaN and poisons the
    ///     positive stack from that row on (IEEE), and a negative value is not poisoned.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_ValuesAreReadLikeUnaryPlus()
    {
        // Act
        var coerced = Stack("""[{"v":null},{"v":"5"},{"v":2}]""");
        var nan = Stack("""[{"v":1},{"v":"x"},{"v":2},{"v":-3}]""");

        // Assert
        Assert.Equal(["0|0", "0|5", "5|7"], Rows(coerced, "v_start", "v_end"));
        Assert.Equal(1, nan.GetValue(0, "v_end").Number);
        Assert.Equal(1, nan.GetValue(1, "v_start").Number);
        Assert.True(double.IsNaN(nan.GetValue(1, "v_end").Number));
        Assert.True(double.IsNaN(nan.GetValue(2, "v_start").Number));
        Assert.True(double.IsNaN(nan.GetValue(2, "v_end").Number));
        Assert.Equal(["0|-3"], Rows(nan, "v_start", "v_end").Skip(3));
    }

    /// <summary>
    ///     Validates the IEEE behavior of <c>normalize</c> and <c>center</c> that Vega has: a group that sums to 0 under <c>normalize</c>
    ///     gets NaN ends (0 times infinity), a NaN in a group makes every end of that group NaN under <c>center</c>, and the other groups are
    ///     unaffected (a NaN sum is never the widest).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_ZeroSumsAndNaNFollowIeee()
    {
        // Arrange
        const string rows = """[{"g":"a","v":0},{"g":"a","v":0},{"g":"b","v":"x"},{"g":"c","v":2},{"g":"c","v":4}]""";

        // Act
        var normalized = Stack(rows, "\"groupby\":[\"g\"],\"offset\":\"normalize\"");
        var centered = Stack(rows, "\"groupby\":[\"g\"],\"offset\":\"center\"");

        // Assert: normalize: a sums to 0 (0 * infinity = NaN), b is NaN, c is (0, 1/3), (1/3, 1)
        Assert.Equal(0, normalized.GetValue(0, "v_start").Number);
        Assert.True(double.IsNaN(normalized.GetValue(0, "v_end").Number));
        Assert.True(double.IsNaN(normalized.GetValue(1, "v_start").Number));
        Assert.True(double.IsNaN(normalized.GetValue(2, "v_end").Number));
        Assert.Equal(1.0 / 3, normalized.GetValue(3, "v_end").Number, 12);
        Assert.Equal(1, normalized.GetValue(4, "v_end").Number, 12);

        // Assert: center: the widest group is c (6); a is centered at 3, b is NaN, c starts at 0
        Assert.Equal(["3|3", "3|3"], Rows(centered, "v_start", "v_end").Take(2));
        Assert.True(double.IsNaN(centered.GetValue(2, "v_start").Number));
        Assert.Equal(["0|2", "2|6"], Rows(centered, "v_start", "v_end").Skip(3));
    }

    /// <summary>
    ///     Validates that an empty table gives an empty table with the two fields.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_EmptyInputGivesNoRows()
    {
        // Act
        var table = Stack("[]");

        // Assert
        Assert.Equal(0, table.RowCount);
        Assert.Equal(["v_end", "v_start"], table.Fields);
    }

    /// <summary>
    ///     Validates the engine entry that the encoding stage uses: group-by, a descending sort and explicit output names, with the
    ///     groups not counted in the row budget (the rows are unchanged).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_EngineStacksWithGivenNames()
    {
        // Arrange
        const string rows = """[{"g":"x","s":1,"v":1},{"g":"x","s":2,"v":2},{"g":"y","s":1,"v":4},{"g":"x","s":3,"v":3}]""";
        var (table, context, loader, _) = Prepare(Spec(rows, "[]"));
        var before = loader.RowsLoaded;
        var where = new VegaLiteLocation(null, "/encoding/y", 1, 1);

        // Act
        var result = new AggregateTransforms(context).Stack(table, "v", ["g"], [new SortField("s", true)], StackOffset.Zero, "sum_v_start", "sum_v_end", where);

        // Assert: x stacks by descending s: s3 (0, 3), s2 (3, 5), s1 (5, 6); y has one row
        Assert.Equal(["5|6", "3|5", "0|4", "0|3"], Rows(result, "sum_v_start", "sum_v_end"));
        Assert.Equal(before, loader.RowsLoaded);
    }

    /// <summary>
    ///     Validates the located failures: more output fields than the field limit, and a text longer than the string limit.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_FieldAndStringLimitsAreLocated()
    {
        // Arrange
        var rows = IdRows(1, 2);

        // Act
        var fields = Assert.Throws<VegaLiteRenderException>(() => Run(Spec(rows, """[{"stack":"v"}]"""), new VegaLiteLimits { MaxFieldCount = 3 }));
        var name = Assert.Throws<VegaLiteRenderException>(() =>
            Run(Spec(rows, """[{"stack":"v","as":"a_name_longer_than_twenty_five_chars"}]"""), new VegaLiteLimits { MaxStringLength = 20 }, parseWithDefaults: true));

        // Assert
        Assert.Contains("MaxFieldCount", fields.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", fields.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxStringLength", name.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", name.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the work is charged to the step budget (the rows and the sort) and that a larger budget succeeds.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_WorkIsChargedToTheStepBudget()
    {
        // Arrange: sorting 5,000 rows is 1,015 steps and the passes over the rows another 300
        var json = SequenceSpec(5000, """[{"stack":"id","sort":[{"field":"id","order":"descending"}]}]""");

        // Act
        var table = Run(json, new VegaLiteLimits { MaxEvaluationSteps = 100_000 });
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxEvaluationSteps = 500 }));

        // Assert: the last id is stacked first, the first id last (the sum of 1..4999 below it)
        Assert.Equal(0, table.GetValue(4999, "id_start").Number);
        Assert.Equal(12_497_500, table.GetValue(0, "id_start").Number);
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_CancellationIsPolled() =>
        Assert.Throws<OperationCanceledException>(() => ApplyCanceled(Spec(IdRows(1, 2), """[{"stack":"v","groupby":["id"]}]""")));

    /// <summary>
    ///     Validates that the result is the same on a second run and under other cultures.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Stack_IsDeterministicAndCultureInvariant()
    {
        // Arrange
        var json = Spec(
            """[{"g":"a","v":1.5},{"g":"b","v":2.25},{"g":"a","v":0.1},{"g":"b","v":-1234.5}]""",
            """[{"stack":"v","groupby":["g"],"offset":"normalize"}]""");
        string Describe() => string.Join(";", Rows(Run(json), "v_start", "v_end"));
        var expected = Describe();

        // Act / Assert
        Assert.Equal(expected, Describe());
        Assert.Equal(expected, Expressions.ExpressionTestSupport.UnderCulture("tr-TR", Describe));
        Assert.Equal(expected, Expressions.ExpressionTestSupport.UnderCulture("de-DE", Describe));
    }

    /// <summary>Compares numbers with a tolerance of 1e-12.</summary>
    private sealed class Within : IEqualityComparer<double>
    {
        /// <inheritdoc/>
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-12;

        /// <inheritdoc/>
        public int GetHashCode(double obj) => 0;
    }
}
