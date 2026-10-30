using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.AggregateTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the <c>window</c> transform: the ranking, offset and positional operations, the aggregate operations over
///     frames, peers, the incremental fast path of cumulative frames, partitions, the output naming, the limits, the charging of work
///     to the step budget and cancellation. The expected values are derived by hand from the public Vega documentation.
/// </summary>
public class AggregateTransformsWindowTests
{
    /// <summary>The rows of most tests: the values 10, 20, 20, 30 with ids 0 to 3 (a pair of peers in the middle).</summary>
    private static readonly string Rows4 = IdRows(10, 20, 20, 30);

    /// <summary>
    ///     Runs a window transform over rows.
    /// </summary>
    /// <param name="rows">The JSON rows.</param>
    /// <param name="window">The members of the transform object after the <c>window</c> array (for example the sort).</param>
    /// <param name="ops">The JSON array of operations.</param>
    /// <returns>The resulting table.</returns>
    private static DataTable Window(string rows, string ops, string window = "") =>
        Run(Spec(rows, "[{\"window\":" + ops + (window.Length == 0 ? string.Empty : "," + window) + "}]"));

    /// <summary>
    ///     Validates the six ranking operations on <c>[10, 20, 20, 30]</c> sorted ascending: ranks with a gap after the peers, ranks without
    ///     gap, the fraction of rows before, the cumulative distribution including peers, and the tiles.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_RankingOperationsFollowVegaOnPeers()
    {
        // Arrange
        const string ops = """
            [{"op":"row_number","as":"rn"},{"op":"rank","as":"rk"},{"op":"dense_rank","as":"dr"},{"op":"percent_rank","as":"pr"},
             {"op":"cume_dist","as":"cd"},{"op":"ntile","param":2,"as":"n2"},{"op":"ntile","param":3,"as":"n3"}]
            """;

        // Act
        var table = Window(Rows4, ops, "\"sort\":[{\"field\":\"v\"}]");

        // Assert: percent_rank = (rank - 1) / (n - 1); cume_dist = rows up to the last peer / n; ntile = ceil(tiles * cume_dist)
        Assert.Equal(["1|1|1|1|1", "2|2|2|2|3", "3|2|2|2|3", "4|4|3|2|3"], Rows(table, "rn", "rk", "dr", "n2", "n3"));
        Assert.Equal([0, 1.0 / 3, 1.0 / 3, 1], Numbers(table, "pr"), new DoubleComparer());
        Assert.Equal([0.25, 0.75, 0.75, 1], Numbers(table, "cd"), new DoubleComparer());
    }

    /// <summary>
    ///     Validates that a percent rank in a partition of one row is NaN (zero divided by zero, as in Vega) and that without a sort no
    ///     two rows are peers: the rank is the row number.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_SingleRowAndUnsortedRanks()
    {
        // Arrange
        var single = IdRows(7);

        // Act
        var one = Window(single, """[{"op":"percent_rank","as":"pr"},{"op":"rank","as":"rk"}]""", "\"sort\":[{\"field\":\"v\"}]");
        var unsorted = Window(Rows4, """[{"op":"rank","as":"rk"},{"op":"dense_rank","as":"dr"},{"op":"cume_dist","as":"cd"}]""");

        // Assert
        Assert.True(double.IsNaN(one.GetValue(0, "pr").Number));
        Assert.Equal(1, one.GetValue(0, "rk").Number);
        Assert.Equal(["1|1", "2|2", "3|3", "4|4"], Rows(unsorted, "rk", "dr"));
        Assert.Equal([0.25, 0.5, 0.75, 1], Numbers(unsorted, "cd"), new DoubleComparer());
    }

    /// <summary>
    ///     Validates that peers at the start of a partition get the cumulative distribution of their whole group (Vega under-reports the
    ///     first row; the documented meaning is used).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_CumeDistCountsPeersOfTheFirstRows()
    {
        // Act
        var table = Window(IdRows(5, 5, 9), """[{"op":"cume_dist","as":"cd"},{"op":"ntile","param":3,"as":"n"}]""", "\"sort\":[{\"field\":\"v\"}]");

        // Assert: both 5s are 2 of 3 rows
        Assert.Equal([2.0 / 3, 2.0 / 3, 1], Numbers(table, "cd"), new DoubleComparer());
        Assert.Equal([2, 2, 3], Numbers(table, "n"));
    }

    /// <summary>
    ///     Validates the default names (<c>op</c> or <c>op_field</c>), the default frame <c>[null, 0]</c> in input order without a sort, and
    ///     that an <c>as</c> replaces an existing field.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_DefaultNamesFrameAndReplacement()
    {
        // Act
        var table = Window(
            Rows4,
            """[{"op":"row_number"},{"op":"lag","field":"v"},{"op":"sum","field":"v"},{"op":"count"},{"op":"sum","field":"id","as":"v"}]""");

        // Assert: v is replaced by the running sum of the ids
        Assert.Equal(["count", "id", "lag_v", "row_number", "sum_v", "v"], table.Fields);
        Assert.Equal(["1|0", "2|1", "3|3", "4|6"], Rows(table, "count", "v"));
        Assert.Equal([10, 30, 50, 80], Numbers(table, "sum_v"));
        Assert.Equal(["null", "10", "20", "20"], Rows(table, "lag_v"));
    }

    /// <summary>
    ///     Validates that the default frame with a sort takes in the peers of the current row (sum 10, 50, 50, 80) and that
    ///     <c>ignorePeers</c> cuts it at the row (10, 30, 50, 80), with the sort stable on ties.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_FramesIncludePeersUnlessIgnored()
    {
        // Arrange
        const string ops = """[{"op":"sum","field":"v","as":"s"},{"op":"count","as":"n"},{"op":"last_value","field":"id","as":"last"}]""";

        // Act
        var withPeers = Window(Rows4, ops, "\"sort\":[{\"field\":\"v\"}]");
        var ignoring = Window(Rows4, ops, "\"sort\":[{\"field\":\"v\"}],\"ignorePeers\":true");

        // Assert: the last row of the frame of a peer is the last peer (id 2)
        Assert.Equal(["10|1|0", "50|3|2", "50|3|2", "80|4|3"], Rows(withPeers, "s", "n", "last"));
        Assert.Equal(["10|1|0", "30|2|1", "50|3|2", "80|4|3"], Rows(ignoring, "s", "n", "last"));
    }

    /// <summary>
    ///     Validates that the output keeps the input order for unsorted input and that partitions are sorted separately.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_PartitionsAreSortedSeparatelyAndOutputKeepsInputOrder()
    {
        // Arrange
        const string rows = """[{"g":"a","v":3},{"g":"b","v":1},{"g":"a","v":1},{"g":"b","v":2},{"g":"a","v":2}]""";

        // Act
        var table = Window(rows, """[{"op":"row_number","as":"n"},{"op":"sum","field":"v","as":"s"}]""", "\"groupby\":[\"g\"],\"sort\":[{\"field\":\"v\"}]");

        // Assert: a sorts 1, 2, 3 and b sorts 1, 2
        Assert.Equal(["3|6", "1|1", "1|1", "2|3", "2|3"], Rows(table, "n", "s"));
        Assert.Equal(["a", "b", "a", "b", "a"], Rows(table, "g"));
    }

    /// <summary>
    ///     Validates the sort order: null first and descending reversing it, several sort fields in order of significance, and ties
    ///     in the input order.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_SortIsStableNullFirstAndDescendingReverses()
    {
        // Arrange
        var rows = IdRows(3, null, 1);
        const string two = """[{"id":0,"a":1,"b":2},{"id":1,"a":1,"b":1},{"id":2,"a":0,"b":9},{"id":3,"a":1,"b":1}]""";

        // Act
        var ascending = Window(rows, """[{"op":"row_number","as":"n"}]""", "\"sort\":[{\"field\":\"v\"}]");
        var descending = Window(rows, """[{"op":"row_number","as":"n"}]""", "\"sort\":[{\"field\":\"v\",\"order\":\"descending\"}]");
        var multi = Window(two, """[{"op":"row_number","as":"n"}]""", "\"sort\":[{\"field\":\"a\"},{\"field\":\"b\",\"order\":\"descending\"}]");

        // Assert
        Assert.Equal([3, 1, 2], Numbers(ascending, "n"));
        Assert.Equal([1, 3, 2], Numbers(descending, "n"));
        Assert.Equal([2, 3, 1, 4], Numbers(multi, "n"));
    }

    /// <summary>
    ///     Validates <c>lag</c> and <c>lead</c>: default offset 1, explicit offsets, null beyond the partition, and an offset of the
    ///     largest integer that must not overflow.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_LagAndLeadReadNeighbors()
    {
        // Arrange
        const string ops = """
            [{"op":"lag","field":"v","as":"l1"},{"op":"lead","field":"v","as":"d1"},{"op":"lag","field":"v","param":2,"as":"l2"},
             {"op":"lead","field":"v","param":2,"as":"d2"},{"op":"lag","field":"v","param":2147483647,"as":"lh"},{"op":"lead","field":"v","param":2147483647,"as":"dh"}]
            """;

        // Act
        var table = Window(Rows4, ops, "\"sort\":[{\"field\":\"id\"}]");

        // Assert
        Assert.Equal(["null", "10", "20", "20"], Rows(table, "l1"));
        Assert.Equal(["20", "20", "30", "null"], Rows(table, "d1"));
        Assert.Equal(["null", "null", "10", "20"], Rows(table, "l2"));
        Assert.Equal(["20", "30", "null", "null"], Rows(table, "d2"));
        Assert.Equal(["null", "null", "null", "null"], Rows(table, "lh"));
        Assert.Equal(["null", "null", "null", "null"], Rows(table, "dh"));
    }

    /// <summary>
    ///     Validates <c>first_value</c>, <c>last_value</c> and <c>nth_value</c> over the frame <c>[-1, 1]</c> (rows clamped at the ends).
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_PositionalValuesReadTheFrame()
    {
        // Arrange
        const string ops = """
            [{"op":"first_value","field":"v","as":"f"},{"op":"last_value","field":"v","as":"l"},
             {"op":"nth_value","field":"v","param":2,"as":"n2"},{"op":"nth_value","field":"v","param":3,"as":"n3"}]
            """;

        // Act
        var table = Window(Rows4, ops, "\"sort\":[{\"field\":\"id\"}],\"frame\":[-1,1]");

        // Assert: the frames are rows 0-1, 0-2, 1-3, 2-3
        Assert.Equal(["10|20|20|null", "10|20|20|20", "20|30|20|30", "20|30|30|null"], Rows(table, "f", "l", "n2", "n3"));
    }

    /// <summary>
    ///     Validates <c>prev_value</c> and <c>next_value</c>: the nearest non-null value at or before / at or after the row, null at the
    ///     ends, and that the state does not carry from one partition to the next.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_PreviousAndNextValueSkipNulls()
    {
        // Arrange
        const string ops = """[{"op":"prev_value","field":"v","as":"p"},{"op":"next_value","field":"v","as":"n"}]""";
        const string grouped = """[{"g":"a","v":1},{"g":"a","v":null},{"g":"b","v":null},{"g":"b","v":2}]""";

        // Act
        var flat = Window(IdRows(null, 5, null, null, 8, null), ops, "\"sort\":[{\"field\":\"id\"}]");
        var partitions = Window(grouped, ops, "\"groupby\":[\"g\"]");

        // Assert
        Assert.Equal(["null|5", "5|5", "5|8", "5|8", "8|8", "8|null"], Rows(flat, "p", "n"));
        Assert.Equal(["1|1", "1|null", "null|2", "2|2"], Rows(partitions, "p", "n"));
    }

    /// <summary>
    ///     Validates a sliding frame <c>[-1, 1]</c> recomputed per row: the average of the neighbors.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_SlidingFrameAverages()
    {
        // Act
        var table = Window(Rows4, """[{"op":"mean","field":"v","as":"m"},{"op":"max","field":"v","as":"x"}]""", "\"sort\":[{\"field\":\"id\"}],\"frame\":[-1,1]");

        // Assert
        Assert.Equal([15, 50.0 / 3, 70.0 / 3, 25], Numbers(table, "m"), new DoubleComparer());
        Assert.Equal([20, 20, 30, 30], Numbers(table, "x"));
    }

    /// <summary>
    ///     Validates the clamping of a frame to the partition and that a frame that is empty gives no value (null sum) and a count of 0.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_FramesAreClampedAndMayBeEmpty()
    {
        // Arrange
        const string ops = """[{"op":"sum","field":"v","as":"s"},{"op":"count","as":"n"},{"op":"first_value","field":"v","as":"f"}]""";

        // Act
        var ahead = Window(Rows4, ops, "\"sort\":[{\"field\":\"id\"}],\"frame\":[2,5]");
        var behind = Window(Rows4, ops, "\"sort\":[{\"field\":\"id\"}],\"frame\":[-3,-2]");

        // Assert: [2, 5] looks two to five rows ahead; [-3, -2] two to three rows behind
        Assert.Equal(["50|2|20", "30|1|30", "null|0|null", "null|0|null"], Rows(ahead, "s", "n", "f"));
        Assert.Equal(["null|0|null", "null|0|null", "10|1|10", "30|2|10"], Rows(behind, "s", "n", "f"));
    }

    /// <summary>
    ///     Validates the whole-partition frame <c>[null, null]</c> per partition.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_WholePartitionFrame()
    {
        // Arrange
        const string rows = """[{"g":"a","v":1},{"g":"b","v":5},{"g":"a","v":2}]""";

        // Act
        var table = Window(rows, """[{"op":"sum","field":"v","as":"s"}]""", "\"groupby\":[\"g\"],\"frame\":[null,null]");

        // Assert
        Assert.Equal([3, 5, 3], Numbers(table, "s"));
    }

    /// <summary>
    ///     Validates that the incremental computation of a cumulative frame gives exactly the result of recomputing every frame from
    ///     scratch, for many operations, with and without a sort (so with and without peers), on data with nulls and repeats.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_CumulativeFastPathEqualsRecomputation()
    {
        // Arrange: 200 rows of (i * 37) % 11 - 5 with a null every 7th row (many repeated values, so many peers)
        var rows = "[" + string.Join(",", Enumerable.Range(0, 200).Select(i => "{\"id\":" + i + ",\"v\":" + (i % 7 == 3 ? "null" : (((i * 37) % 11) - 5).ToString(System.Globalization.CultureInfo.InvariantCulture)) + "}")) + "]";
        string[] names = ["sum", "mean", "count", "valid", "missing", "min", "max", "median", "q1", "variance", "stdev", "distinct", "values"];
        var ops = "[" + string.Join(",", names.Select(n => "{\"op\":\"" + n + "\",\"field\":\"v\",\"as\":\"" + n + "\"}")) + "]";

        // Act / Assert
        foreach (var sort in new[] { string.Empty, ",\"sort\":[{\"field\":\"v\"}]" })
        {
            var fast = Run(Spec(rows, "[{\"window\":" + ops + sort + ",\"frame\":[null,0]}]"));
            var slow = Run(Spec(rows, "[{\"window\":" + ops + sort + ",\"frame\":[-100000,0]}]"));
            Assert.Equal(Rows(slow, names), Rows(fast, names));
        }
    }

    /// <summary>
    ///     Validates that a <c>values</c> operation gives the frame's values in frame order, and that rows that share a frame (peers) share
    ///     one array.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_ValuesFollowTheFrameAndPeersShareTheArray()
    {
        // Act
        var table = Window(Rows4, """[{"op":"values","field":"v","as":"all"}]""", "\"sort\":[{\"field\":\"v\"}]");

        // Assert
        Assert.Equal(["10", "10,20,20", "10,20,20", "10,20,20,30"], Rows(table, "all"));
        Assert.Equal(table.GetValue(1, "all"), table.GetValue(2, "all"));
        Assert.NotEqual(table.GetValue(0, "all"), table.GetValue(3, "all"));
    }

    /// <summary>
    ///     Validates aggregate operations over the whole partition: median, maximum, distinct count and the row of the maximum.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_AggregateOperationsOverTheWholePartition()
    {
        // Act
        var table = Window(
            Rows4,
            """[{"op":"median","field":"v","as":"m"},{"op":"max","field":"v","as":"x"},{"op":"distinct","field":"v","as":"d"},{"op":"argmax","field":"v","as":"top"}]""",
            "\"frame\":[null,null]");

        // Assert
        Assert.Equal(["20|30|3", "20|30|3", "20|30|3", "20|30|3"], Rows(table, "m", "x", "d"));
        var top = table.GetValue(0, "top").ObjectValue!;
        Assert.Equal(["id", "v"], top.Keys);
        Assert.Equal(3, top.ValueAt(0).Number);
    }

    /// <summary>
    ///     Validates that the window composes with the other transforms: the rank of a descending sort drives a filter.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_ComposesWithFilter()
    {
        // Act
        var table = Run(Spec(
            IdRows(5, 9, 7, 9),
            """[{"window":[{"op":"rank","as":"rk"}],"sort":[{"field":"v","order":"descending"}]},{"filter":"datum.rk <= 2"}]"""));

        // Assert: the two 9s tie for first
        Assert.Equal(["1", "3"], Rows(table, "id"));
    }

    /// <summary>
    ///     Validates that an empty table gives an empty table with the fields added.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_EmptyInputGivesNoRows()
    {
        // Act
        var table = Window("[]", """[{"op":"row_number","as":"n"}]""");

        // Assert
        Assert.Equal(0, table.RowCount);
        Assert.Equal(["n"], table.Fields);
    }

    /// <summary>
    ///     Validates that the window does not change the row budget: the rows stay as they are.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_DoesNotChangeTheRowBudget()
    {
        // Arrange
        var (table, context, loader, tree) = Prepare(Spec(Rows4, """[{"window":[{"op":"rank","as":"r"}],"groupby":["id"]}]"""));
        var before = loader.RowsLoaded;

        // Act
        var result = RowTransforms.Apply(table, tree.Root["transform"], context);

        // Assert
        Assert.Equal(4, result.RowCount);
        Assert.Equal(before, loader.RowsLoaded);
    }

    /// <summary>
    ///     Validates that the cost of a frame that is recomputed for every row is charged to the step budget and fails closed with a
    ///     located failure, while the same cumulative frame computed incrementally fits the same budget.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_WideFramesAreChargedToTheStepBudget()
    {
        // Arrange: 3,000 rows; recomputing the frame of every row visits 4.5 million rows, charged one step per 16
        var limits = new VegaLiteLimits { MaxEvaluationSteps = 100_000 };
        var cumulative = SequenceSpec(3000, """[{"window":[{"op":"sum","field":"id","as":"s"}],"frame":[null,0]}]""");
        var sliding = SequenceSpec(3000, """[{"window":[{"op":"sum","field":"id","as":"s"}],"frame":[-3000,0]}]""");

        // Act
        var table = Run(cumulative, limits);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(sliding, limits));

        // Assert: the sum of 0..2999
        Assert.Equal(4_498_500, table.GetValue(2999, "s").Number);
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that sorting is charged (n log n elements) and that a larger budget succeeds.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_SortingIsChargedToTheStepBudget()
    {
        // Arrange: sorting 5,000 rows costs 5,000 * 13 / 64 = 1,015 steps
        var json = SequenceSpec(5000, """[{"window":[{"op":"rank","as":"r"}],"sort":[{"field":"id","order":"descending"}]}]""");

        // Act
        var table = Run(json, new VegaLiteLimits { MaxEvaluationSteps = 100_000 });
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, new VegaLiteLimits { MaxEvaluationSteps = 500 }));

        // Assert: descending ids rank from the last row
        Assert.Equal(1, table.GetValue(4999, "r").Number);
        Assert.Equal(5000, table.GetValue(0, "r").Number);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the arrays a <c>values</c> operation builds are bounded: the cumulative arrays of 100 rows hold 5,050 values,
    ///     more than a row limit of 1,000, while one shared array of the whole partition fits.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_ValuesArraysAreBounded()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxDataRows = 1000 };
        var cumulative = SequenceSpec(100, """[{"window":[{"op":"values","field":"id","as":"a"}]}]""");
        var whole = SequenceSpec(100, """[{"window":[{"op":"values","field":"id","as":"a"}],"frame":[null,null]}]""");

        // Act
        var table = Run(whole, limits);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(cumulative, limits));

        // Assert
        Assert.Equal(table.GetValue(0, "a"), table.GetValue(99, "a"));
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("(MaxDataRows)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the bounds that keep the memory of a window finite: the table of the result is bounded by the cell limit (600 operations
    ///     over 60,000 rows), and the engine bounds the values that the operations keep (rows times operations that store) before it reads a
    ///     row, located at the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_ValueStateIsBoundedUpFront()
    {
        // Arrange: 600 medians over 60,000 rows would keep 36 million values
        var ops = string.Join(",", Enumerable.Range(0, 600).Select(i => "{\"op\":\"median\",\"field\":\"id\",\"as\":\"m" + i + "\"}"));
        var (table, context, _, _) = Prepare(SequenceSpec(60_000, "[]"));
        var where = new VegaLiteLocation(null, "/transform/0", 1, 1);
        var specs = Enumerable.Range(0, 600).Select(i => new WindowSpec(null, AggregateOp.Median, "id", 0.5, "m" + i)).ToArray();

        // Act
        var byTable = Assert.Throws<VegaLiteRenderException>(() => Run(SequenceSpec(60_000, "[{\"window\":[" + ops + "]}]")));
        var byValues = Assert.Throws<VegaLiteRenderException>(() => new AggregateTransforms(context).Window(table, [], [], null, 0, false, specs, 1, where, (_, _, _) => { }));

        // Assert
        Assert.Equal("/transform/0", byTable.Location.Pointer);
        Assert.Contains("the 33554432 cells that a derived table may have", byTable.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", byValues.Location.Pointer);
        Assert.Contains("the limit of 33554432 values (TableLayout.MaxCells)", byValues.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the field limit and the string limit for names of the specification, both located at the transform.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_FieldAndStringLimitsAreLocated()
    {
        // Arrange
        var fields = SequenceSpec(3, """[{"window":[{"op":"rank","as":"a"},{"op":"rank","as":"b"},{"op":"rank","as":"c"}]}]""");
        var name = SequenceSpec(3, """[{"window":[{"op":"rank","as":"a_name_longer_than_twenty_five_chars"}]}]""");

        // Act
        var byFields = Assert.Throws<VegaLiteRenderException>(() => Run(fields, new VegaLiteLimits { MaxFieldCount = 3 }));
        var byName = Assert.Throws<VegaLiteRenderException>(() => Run(name, new VegaLiteLimits { MaxStringLength = 20 }, parseWithDefaults: true));

        // Assert
        Assert.Contains("MaxFieldCount", byFields.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", byFields.Location.Pointer);
        Assert.Contains("VegaLiteLimits.MaxStringLength", byName.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", byName.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a canceled token stops the transform, and that the loop over the rows polls it: the sink cancels after the first
    ///     result and a table of more than 1,024 rows is stopped before the end.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_CancellationIsPolled()
    {
        // Arrange
        const string json = """{"data":{"values":[{"id":0},{"id":1}]},"transform":[{"window":[{"op":"row_number","as":"n"}],"groupby":["id"]}],"mark":"point"}""";
        using var source = new CancellationTokenSource();
        var (table, context, _, _) = Prepare(SequenceSpec(3000, "[]"), null, source.Token);
        var where = new VegaLiteLocation(null, "/transform/0", 1, 1);
        var done = 0;

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => ApplyCanceled(json));
        Assert.Throws<OperationCanceledException>(() => new AggregateTransforms(context).Window(
            table,
            [],
            [],
            null,
            0,
            false,
            [new WindowSpec(WindowOp.RowNumber, default, null, 1, "n")],
            1,
            where,
            (_, _, _) =>
            {
                done++;
                source.Cancel();
            }));
        Assert.InRange(done, 1, 1100);
    }

    /// <summary>
    ///     Validates that the result is the same on a second run and under other cultures.
    /// </summary>
    [Fact]
    public void AggregateTransforms_Window_IsDeterministicAndCultureInvariant()
    {
        // Arrange
        var json = Spec(
            """[{"x":1.5,"g":"a"},{"x":2.25,"g":"b"},{"x":9.75,"g":"a"},{"x":1234.5,"g":"b"},{"x":0.1,"g":"a"}]""",
            """[{"window":[{"op":"mean","field":"x","as":"m"},{"op":"percent_rank","as":"p"},{"op":"ci1","field":"x","as":"c"}],"groupby":["g"],"sort":[{"field":"x"}]}]""");
        string Describe() => string.Join(";", Rows(Run(json), "g", "m", "p", "c"));
        var expected = Describe();

        // Act / Assert
        Assert.Equal(expected, Describe());
        Assert.Equal(expected, Expressions.ExpressionTestSupport.UnderCulture("tr-TR", Describe));
        Assert.Equal(expected, Expressions.ExpressionTestSupport.UnderCulture("de-DE", Describe));
    }

    /// <summary>Compares numbers with a tolerance of 1e-12 (and NaN with NaN).</summary>
    private sealed class DoubleComparer : IEqualityComparer<double>
    {
        /// <inheritdoc/>
        public bool Equals(double x, double y) => (double.IsNaN(x) && double.IsNaN(y)) || Math.Abs(x - y) < 1e-12;

        /// <inheritdoc/>
        public int GetHashCode(double obj) => 0;
    }
}
