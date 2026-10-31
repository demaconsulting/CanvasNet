using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.AggregateTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the imputes of the implicit encoding pipeline: the <c>impute</c> property of <c>x</c> and <c>y</c> (after the aggregate, before the
///     stack), the zeros that a stack of lines or areas adds for the keys a layer lacks, and the row budget and cancellation of both. The expected rows are derived by hand
///     from the rules of Vega-Lite's <c>ImputeNode</c> and <c>StackNode</c>; the comments show the derivation.
/// </summary>
public class EncodingTransformsImputeTests
{
    /// <summary>Series a has the keys 1, 2 and 3 with the values 1, 2 and 3; series b has the keys 1 and 3 with the values 4 and 5 (it lacks the key 2).</summary>
    private const string Layers = """[{"g":"a","t":1,"v":1},{"g":"a","t":2,"v":2},{"g":"a","t":3,"v":3},{"g":"b","t":1,"v":4},{"g":"b","t":3,"v":5}]""";

    /// <summary>
    ///     Runs the pipeline up to the scales for a view.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The prepared parts.</returns>
    private static ChartParts Run(string rows, string mark, string encoding, VegaLiteLimits? limits = null) =>
        ChartTestHelpers.ResolveFull("{\"data\":{\"values\":" + rows + "},\"mark\":\"" + mark + "\",\"encoding\":" + encoding + "}", limits);

    /// <summary>
    ///     Validates that the impute of a channel runs before the stack: series b lacks the key 2, the impute (value 5) adds b/2 = 5 after the input rows, the stack
    ///     then sees it, and the stack impute finds nothing left to add. Stacked descending by g (the stacked channel is y), b is the bottom layer: at t = 1 b is (0, 4)
    ///     and a (4, 5); at t = 2 b is (0, 5) and a (5, 7); at t = 3 b is (0, 5) and a (5, 8).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputesBeforeTheStack()
    {
        // Act
        var parts = Run(
            Layers,
            "area",
            """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","impute":{"value":5}},"color":{"field":"g","type":"nominal"}}""");

        // Assert: six rows, the imputed one last; the stack starts and ends are those of the derivation
        Assert.Equal(6, parts.Data.Main.RowCount);
        Assert.Equal(
            ["a|1|4|5", "a|2|5|7", "a|3|5|8", "b|1|0|4", "b|3|0|5", "b|2|0|5"],
            Rows(parts.Data.Main, "g", "t", "v_start", "v_end"));
        Assert.Same(parts.Data.Raw, parts.Data.Main);
        Assert.Equal("v_end", parts.Scales["y"]!.FieldPath);
    }

    /// <summary>
    ///     Validates the statistical method of the impute: a window over the series in data order replaces the nulls of the imputed rows. Series a has the keys 1 and 3
    ///     (10 and 30), series b has the key 2 (5); the key domain is 1, 3, 2, so the imputed rows are a/2 (the mean of 10 and 30 is 20), b/1 (5) and b/3 (5).
    ///     A line is not stacked, so nothing else changes the rows.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputeMethodMeanUsesTheGroupWindow()
    {
        // Act
        var parts = Run(
            """[{"g":"a","t":1,"v":10},{"g":"a","t":3,"v":30},{"g":"b","t":2,"v":5}]""",
            "line",
            """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative","impute":{"method":"mean"}},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(["a|1|10", "a|3|30", "b|2|5", "a|2|20", "b|1|5", "b|3|5"], Rows(parts.Data.Main, "g", "t", "v"));
        Assert.Same(parts.Data.Raw, parts.Data.Main);
    }

    /// <summary>
    ///     Validates that the <c>keyvals</c>, <c>value</c> and <c>frame</c> of the object are passed to the engine: with the keys 1 to 4 and the value -1 the series
    ///     a (keys 1, 3) gets 2 and 4, and series b (key 2) gets 1, 3 and 4; with the method max over the frame [null, 0] the imputed values are the running maximum of the
    ///     rows before them in data order.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputeObjectParametersReachTheEngine()
    {
        // Arrange
        const string Input = """[{"g":"a","t":1,"v":10},{"g":"a","t":3,"v":30},{"g":"b","t":2,"v":5}]""";

        // Act
        var constant = Run(
            Input,
            "line",
            """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative","impute":{"keyvals":{"start":1,"stop":5},"value":-1}},"color":{"field":"g","type":"nominal"}}""");
        var running = Run(
            Input,
            "line",
            """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative","impute":{"method":"max","frame":[null,0]}},"color":{"field":"g","type":"nominal"}}""");

        // Assert: the domain is the keyvals 1, 2, 3, 4 (the observed keys are among them)
        Assert.Equal(["a|2", "a|4", "b|1", "b|3", "b|4"], Rows3(constant.Data.Main));
        Assert.Equal(["-1", "-1", "-1", "-1", "-1"], Rows(constant.Data.Main, "v").Skip(3));

        // Assert: the running maximum of a/2 over a's rows before it (10 and 30) is 30; b/1 sees b's row (5); b/3 sees 5
        Assert.Equal(["30", "5", "5"], Rows(running.Data.Main, "v").Skip(3));
    }

    /// <summary>
    ///     Lists the group and key of the rows that follow the three input rows of the table.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <returns>One text per imputed row.</returns>
    private static string[] Rows3(DataTable table) => [.. Rows(table, "g", "t").Skip(3)];

    /// <summary>
    ///     Validates that the impute of <c>x</c> uses <c>y</c> as its key: the series are the colors, the key is the y field and the imputed field the x field.
    ///     Series a has y = 1, 2 and series b has y = 1, so b lacks the key 2 and gets x = 0.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputeOnXUsesYAsTheKey()
    {
        // Act
        var parts = Run(
            """[{"g":"a","h":1,"w":7},{"g":"a","h":2,"w":8},{"g":"b","h":1,"w":9}]""",
            "line",
            """{"x":{"field":"w","type":"quantitative","impute":{}},"y":{"field":"h","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(["a|1|7", "a|2|8", "b|1|9", "b|2|0"], Rows(parts.Data.Main, "g", "h", "w"));
    }

    /// <summary>
    ///     Validates the implicit impute of a stacked area: series a has the keys 1, 2, 3 (1, 2, 3), series b the keys 1 and 3 (4 and 5), so b/2 = 0 is added
    ///     after the input rows. Stacked descending by g, b is the bottom layer: t = 1: b (0, 4), a (4, 5); t = 2: b (0, 0), a (0, 2); t = 3: b (0, 5), a (5, 8).
    ///     A stacked line (stack true) is imputed the same way, and a stacked bar is not imputed.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_StackedPathMarkImputesMissingKeysWithZero()
    {
        // Act
        var area = Run(
            Layers,
            "area",
            """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");
        var line = Run(
            Layers,
            "line",
            """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","stack":true},"color":{"field":"g","type":"nominal"}}""");
        var bar = Run(
            Layers,
            "bar",
            """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        string[] expected = ["a|1|4|5", "a|2|0|2", "a|3|5|8", "b|1|0|4", "b|3|0|5", "b|2|0|0"];
        Assert.Equal(expected, Rows(area.Data.Main, "g", "t", "v_start", "v_end"));
        Assert.Equal(expected, Rows(line.Data.Main, "g", "t", "v_start", "v_end"));
        Assert.Equal(5, bar.Data.Main.RowCount);
        Assert.Equal(["a|1|4|5", "a|2|0|2", "a|3|5|8", "b|1|0|4", "b|3|0|5"], Rows(bar.Data.Main, "g", "t", "v_start", "v_end"));

        // Assert: the scale domain spans the imputed layers: the largest top is 8
        Assert.Equal(8, area.Scales["y"]!.DomainEnd);
    }

    /// <summary>
    ///     Validates that <c>impute: null</c> on the stacked channel switches the implicit impute off (series b keeps its two rows), and that the impute follows the aggregate:
    ///     the sums are a/1 = 1 + 2 = 3, a/2 = 2, b/1 = 4, so b/2 is imputed (0) and the four rows are stacked descending by g.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputeNullDisablesTheStackImpute()
    {
        // Act
        var off = Run(
            Layers,
            "area",
            """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","impute":null},"color":{"field":"g","type":"nominal"}}""");
        var aggregated = Run(
            """[{"g":"a","t":1,"v":1},{"g":"a","t":1,"v":2},{"g":"a","t":2,"v":2},{"g":"b","t":1,"v":4}]""",
            "area",
            """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(5, off.Data.Main.RowCount);
        Assert.Equal(["b|1|0|4", "b|3|0|5"], Rows(off.Data.Main, "g", "t", "v_start", "v_end").Skip(3));
        Assert.Equal(["a|1|4|7", "a|2|0|2", "b|1|0|4", "b|2|0|0"], Rows(aggregated.Data.Main, "g", "t", "sum_v_start", "sum_v_end").Order());
    }

    /// <summary>
    ///     Validates the row budget of the impute: the rows of the table the impute replaces are given back, so 2 series of 3 keys need the 5 input rows plus the 6
    ///     combinations (11 rows) for the impute alone, and a budget of 10 fails located at the stacked channel; when a scale keeps the Raw table (a sort by a field) the
    ///     input rows stay counted, and the stacked copy is counted before it is allocated: 5 + 6 + 6 = 17, so 16 fails and 17 succeeds.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputeRespectsTheRowAndCellLimits()
    {
        // Arrange
        const string Plain = """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";
        const string Sorted = """{"x":{"field":"t","type":"ordinal","sort":{"op":"sum","field":"v"}},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";
        const string Keyed = """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative","impute":{"keyvals":{"start":0,"stop":1000}}},"color":{"field":"g","type":"nominal"}}""";

        // Act
        var fits = Run(Layers, "area", Plain, new VegaLiteLimits { MaxDataRows = 11 });
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => Run(Layers, "area", Plain, new VegaLiteLimits { MaxDataRows = 10 }));
        var kept = Run(Layers, "area", Sorted, new VegaLiteLimits { MaxDataRows = 17 });
        var keptFails = Assert.Throws<VegaLiteRenderException>(() => Run(Layers, "area", Sorted, new VegaLiteLimits { MaxDataRows = 16 }));
        var keys = Assert.Throws<VegaLiteRenderException>(() => Run(Layers, "line", Keyed, new VegaLiteLimits { MaxDataRows = 500 }));

        // Assert: the output is the six stacked rows, and the Raw table keeps the five input rows beside it
        Assert.Equal(6, fits.Data.Main.RowCount);
        Assert.Equal("/encoding/y", tooMany.Location.Pointer);
        Assert.Contains("MaxDataRows", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal(5, kept.Data.Raw.RowCount);
        Assert.Equal(6, kept.Data.Main.RowCount);
        Assert.Equal("/encoding/y", keptFails.Location.Pointer);
        Assert.Contains("MaxDataRows", keptFails.Message, StringComparison.Ordinal);

        // Assert: a thousand key values are more than the 500 rows of the budget, located at the impute object
        Assert.Equal("/encoding/y/impute", keys.Location.Pointer);
        Assert.Contains("MaxDataRows", keys.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the product of series and keys is bounded before anything is allocated: 100 series with a key of their own need 10,000 rows, which a budget of
    ///     5,000 rejects located at the stacked channel (and the default budget accepts).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputeBoundsSeriesTimesKeys()
    {
        // Arrange: 100 colors, each with one distinct key
        var rows = "[" + string.Join(",", Enumerable.Range(0, 100).Select(i => "{\"g\":\"s" + i + "\",\"t\":" + i + ",\"v\":1}")) + "]";
        const string Encoding = """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";

        // Act
        var table = Run(rows, "area", Encoding);
        var failure = Assert.Throws<VegaLiteRenderException>(() => Run(rows, "area", Encoding, new VegaLiteLimits { MaxDataRows = 5000 }));

        // Assert
        Assert.Equal(10_000, table.Data.Main.RowCount);
        Assert.Equal("/encoding/y", failure.Location.Pointer);
        Assert.Contains("MaxDataRows", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the impute polls the cancellation token and charges the step budget: a canceled token stops a plan whose only stage is the impute, and a budget of
    ///     a hundred steps fails located at the impute object and naming the limit (the 5,000 rows cost more than that).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ImputePollsCancellation()
    {
        // Arrange
        var rows = "[" + string.Join(",", Enumerable.Range(0, 5000).Select(i => "{\"g\":\"a\",\"t\":" + i + ",\"v\":1}")) + "]";
        var source = "{\"data\":{\"values\":" + rows + "},\"mark\":\"line\",\"encoding\":{\"x\":{\"field\":\"t\",\"type\":\"quantitative\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\",\"impute\":{}},\"color\":{\"field\":\"g\",\"type\":\"nominal\"}}}";
        var tree = SpecParser.Parse(source);
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var plan = EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, null));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        var steps = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.ResolveFull(source, new VegaLiteLimits { MaxEvaluationSteps = 100 }));

        // Assert
        Assert.NotNull(plan.Impute);
        Assert.ThrowsAny<OperationCanceledException>(() => EncodingTransforms.ApplyStandalone(plan, table, tree.Root, null, null, canceled.Token));
        Assert.Equal("/encoding/y/impute", steps.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", steps.Message, StringComparison.Ordinal);
    }
}
