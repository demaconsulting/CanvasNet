using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.AggregateTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for <see cref="EncodingTransforms"/>: the implicit bin, aggregate and stack of an encoding on the rows of a view, in Vega-Lite's
///     order, with the Raw and Main tables, the row budget, the step budget, cancellation and determinism. The expected numbers are derived by
///     hand from the public Vega-Lite and Vega rules (comments show the derivation).
/// </summary>
public class EncodingTransformsTests
{
    /// <summary>
    ///     Builds a view specification.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="mark">The mark (a name).</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string rows, string mark, string encoding) =>
        "{\"data\":{\"values\":" + rows + "},\"mark\":\"" + mark + "\",\"encoding\":" + encoding + "}";

    /// <summary>
    ///     Runs the pipeline up to the scales for a view.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The prepared parts.</returns>
    private static ChartParts Run(string rows, string mark, string encoding, VegaLiteLimits? limits = null) =>
        ChartTestHelpers.ResolveFull(Spec(rows, mark, encoding), limits);

    /// <summary>
    ///     Validates the implicit aggregate: one row per category in first-seen order with the sum, the derived field names, and the Raw table being
    ///     the Main table when no scale needs the rows before the aggregate.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_AggregatesPerCategoryInFirstSeenOrder()
    {
        // Act: a has 1 and 3, b has 2
        var parts = Run(
            """[{"c":"a","v":1},{"c":"b","v":2},{"c":"a","v":3}]""",
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"}}""");

        // Assert
        Assert.Equal(["c", "sum_v"], parts.Data.Main.Fields);
        Assert.Equal(["a|4", "b|2"], Rows(parts.Data.Main, "c", "sum_v"));
        Assert.Same(parts.Data.Main, parts.Data.Raw);
        Assert.Equal("sum_v", parts.Scales["y"]!.FieldPath);
        Assert.Equal("Sum of v", parts.Scales["y"]!.Title);
        Assert.Equal("c", parts.Scales["x"]!.Title);
    }

    /// <summary>
    ///     Validates that an aggregate without a dimension is a single row, that an aggregate of no rows has no rows, and that the count does not read a field.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_AggregateWithoutDimensionsIsOneRowAndEmptyInputHasNone()
    {
        // Act
        var counted = Run("""[{"v":1},{"v":2},{"v":3}]""", "bar", """{"y":{"aggregate":"count","type":"quantitative"}}""");
        var empty = Run("[]", "bar", """{"x":{"field":"c","type":"nominal"},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert
        Assert.Equal(["3"], Rows(counted.Data.Main, "__count"));
        Assert.Equal(0, empty.Data.Main.RowCount);
        Assert.Equal(["__count", "c"], empty.Data.Main.Fields);
        Assert.Empty(empty.Scales["x"]!.DiscreteDomain);
    }

    /// <summary>
    ///     Validates the stack of an aggregated bar chart with layers: the rows are aggregated per (category, layer) and the layers are stacked
    ///     descending by the layer field (the stacked channel is y, so the last layer is at the bottom): for a = {x: 1, y: 2} the layer y is (0, 2)
    ///     and x is (2, 3); b = {x: 3} is (0, 3).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_StacksTheAggregatedLayersDescendingOnY()
    {
        // Act
        var parts = Run(
            """[{"c":"a","g":"x","v":1},{"c":"a","g":"y","v":2},{"c":"b","g":"x","v":3}]""",
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(["c", "g", "sum_v", "sum_v_end", "sum_v_start"], parts.Data.Main.Fields);
        Assert.Equal(["a|x|2|3", "a|y|0|2", "b|x|0|3"], Rows(parts.Data.Main, "c", "g", "sum_v_start", "sum_v_end"));
        Assert.Equal("sum_v_end", parts.Scales["y"]!.FieldPath);
        Assert.Equal("sum_v_start", parts.Scales["y"]!.Field2Path);

        // Assert: the domain of the stacked scale spans the starts and the ends: [0, 3], with zero for a bar
        Assert.Equal(0, parts.Scales["y"]!.DomainStart);
        Assert.Equal(3, parts.Scales["y"]!.DomainEnd);
    }

    /// <summary>
    ///     Validates the stack of plain rows with positive and negative values: the negative value stacks downwards from 0 separately. The layers are
    ///     z (1), y (-2), x (3) from the bottom; x sits on top of z at (1, 4), y is (0, -2), z is (0, 1). The rows keep their order.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_StacksNegativeValuesDownwards()
    {
        // Act
        var parts = Run(
            """[{"c":"a","g":"x","v":3},{"c":"a","g":"y","v":-2},{"c":"a","g":"z","v":1}]""",
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(["x|1|4", "y|0|-2", "z|0|1"], Rows(parts.Data.Main, "g", "v_start", "v_end"));
        Assert.Equal(-2, parts.Scales["y"]!.DomainStart);
        Assert.Equal(4, parts.Scales["y"]!.DomainEnd);
    }

    /// <summary>
    ///     Validates that signed values stack separately from zero on a horizontal measure, in ascending layer order, and that aggregated sums that are
    ///     negative stack downwards on a vertical one. Horizontal: the layers x (3), y (-2), z (1) are stacked ascending, so the positives are x (0, 3) then
    ///     z (3, 4) and the negative y is (0, -2). Vertical: the sums are x = -1 + -3 = -4 and y = 2, stacked descending: y is (0, 2) and x is (0, -4).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_SignedStacksOnXAndWithAggregatedNegativeSums()
    {
        // Act
        var horizontal = Run(
            """[{"c":"a","g":"x","v":3},{"c":"a","g":"y","v":-2},{"c":"a","g":"z","v":1}]""",
            "bar",
            """{"y":{"field":"c","type":"nominal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");
        var aggregated = Run(
            """[{"c":"a","g":"x","v":-1},{"c":"a","g":"y","v":2},{"c":"a","g":"x","v":-3}]""",
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""");

        // Assert: the rows keep their order; the domain spans the lowest negative end and the highest positive end
        Assert.Equal(["x|0|3", "y|0|-2", "z|3|4"], Rows(horizontal.Data.Main, "g", "v_start", "v_end"));
        Assert.Equal((-2, 4), (horizontal.Scales["x"]!.DomainStart, horizontal.Scales["x"]!.DomainEnd));
        Assert.Equal(["x|0|-4", "y|0|2"], Rows(aggregated.Data.Main, "g", "sum_v_start", "sum_v_end"));
        Assert.Equal((-4, 2), (aggregated.Scales["y"]!.DomainStart, aggregated.Scales["y"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates the <c>normalize</c> offset (the layers y = 3 and x = 1 of a total of 4 become (0, 0.75) and (0.75, 1)) with the domain [0, 1], and
    ///     the <c>center</c> offset (group a of total 4 is the widest and starts at 0; group b of total 2 starts at (4 - 2) / 2 = 1).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_NormalizeAndCenterOffsets()
    {
        // Act
        var normalized = Run(
            """[{"c":"a","g":"x","v":1},{"c":"a","g":"y","v":3}]""",
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"normalize"},"color":{"field":"g","type":"nominal"}}""");
        var centered = Run(
            """[{"c":"a","g":"x","v":1},{"c":"a","g":"y","v":3},{"c":"b","g":"x","v":2}]""",
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"center"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(["x|0.75|1", "y|0|0.75"], Rows(normalized.Data.Main, "g", "v_start", "v_end"));
        Assert.Equal((0, 1), (normalized.Scales["y"]!.DomainStart, normalized.Scales["y"]!.DomainEnd));
        Assert.Equal(["a|x|3|4", "a|y|0|3", "b|x|1|3"], Rows(centered.Data.Main, "c", "g", "v_start", "v_end"));
        Assert.Equal((0, 4), (centered.Scales["y"]!.DomainStart, centered.Scales["y"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that a horizontal stack orders the layers ascending (x then y from the left: x is (0, 1), y is (1, 3)).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_StacksAscendingOnX()
    {
        // Act
        var parts = Run(
            """[{"c":"a","g":"x","v":1},{"c":"a","g":"y","v":2}]""",
            "bar",
            """{"y":{"field":"c","type":"nominal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(["x|0|1", "y|1|3"], Rows(parts.Data.Main, "g", "v_start", "v_end"));
        Assert.Equal("v_end", parts.Scales["x"]!.FieldPath);
    }

    /// <summary>
    ///     Validates that the <c>order</c> channel replaces the default layer order: with the order field descending the layer with the larger order
    ///     is at the bottom (q is (0, 2), p is (2, 3)), and with the default direction the smaller one is (p is (0, 1), q is (1, 3)).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_OrderChannelOrdersTheLayers()
    {
        // Arrange
        const string Rows3 = """[{"c":"a","g":"p","v":1,"o":1},{"c":"a","g":"q","v":2,"o":2}]""";

        // Act
        var descending = Run(
            Rows3,
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"},"order":{"field":"o","type":"quantitative","sort":"descending"}}""");
        var ascending = Run(
            Rows3,
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"},"order":{"field":"o","type":"quantitative"}}""");

        // Assert
        Assert.Equal(["p|2|3", "q|0|2"], Rows(descending.Data.Main, "g", "v_start", "v_end"));
        Assert.Equal(["p|0|1", "q|1|3"], Rows(ascending.Data.Main, "g", "v_start", "v_end"));
    }

    /// <summary>
    ///     Validates the implicit bin of a histogram: the values 1 to 10 with <c>maxbins</c> 10 have the extent [1, 10], the step 1 and the bins
    ///     [1, 2) to [9, 10] (the last bin is closed, so 9 and 10 share it); the count per bin is 1 for the first eight bins and 2 for the last.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_BinsAndCountsAHistogram()
    {
        // Act
        var rows = "[" + string.Join(",", Enumerable.Range(1, 10).Select(n => "{\"n\":" + n + "}")) + "]";
        var parts = Run(rows, "bar", """{"x":{"field":"n","type":"quantitative","bin":true},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert
        var spec = parts.Data.Bins["bin_maxbins_10_n"];
        Assert.Equal((1.0, 10.0, 1.0), (spec.Start, spec.Stop, spec.Step));
        Assert.Equal(
            ["1|2|1", "2|3|1", "3|4|1", "4|5|1", "5|6|1", "6|7|1", "7|8|1", "8|9|1", "9|10|2"],
            Rows(parts.Data.Main, "bin_maxbins_10_n", "bin_maxbins_10_n_end", "__count"));
        var x = parts.Scales["x"]!;
        Assert.Equal(BinKind.Binning, x.Bin);
        Assert.Equal("bin_maxbins_10_n", x.FieldPath);
        Assert.Equal("bin_maxbins_10_n_end", x.Field2Path);
        Assert.Equal("n (binned)", x.Title);
        Assert.Equal("Count of Records", parts.Scales["y"]!.Title);

        // Assert: the domain of a binned scale is [start, stop] with no zero and no nice
        Assert.Equal((1, 10), (x.DomainStart, x.DomainEnd));
        Assert.Null(x.NiceCount);
        Assert.Equal(spec, x.BinSpec);
    }

    /// <summary>
    ///     Validates that a bin without an aggregate keeps every row and adds the start and end of its bin, and that a value that is not a finite number
    ///     (null, text) gets null in both: with the values 0 and 100 the bins are 0 to 100 by 10, so 0 is [0, 10), 100 is [90, 100], null and "abc" have no bin.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_BinOnlyKeepsRowsAndLeavesInvalidValuesWithoutABin()
    {
        // Act
        var parts = Run(
            """[{"n":0},{"n":100},{"n":null},{"n":"abc"}]""",
            "point",
            """{"x":{"field":"n","type":"quantitative","bin":true},"y":{"field":"n","type":"quantitative"}}""");

        // Assert
        Assert.Equal(["0|10", "90|100", "null|null", "null|null"], Rows(parts.Data.Main, "bin_maxbins_10_n", "bin_maxbins_10_n_end"));
        Assert.Same(parts.Data.Raw, parts.Data.Main);
        Assert.Equal(new BinSpec(0, 100, 10), parts.Data.Bins["bin_maxbins_10_n"]);
    }

    /// <summary>
    ///     Validates that an aggregate groups the rows that have no valid value together, in the first-seen order: 0 is [0, 10), 100 is [90, 100], and the
    ///     null and the text form one group with no bin.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_InvalidValuesFormOneGroupWithoutBounds()
    {
        // Act
        var parts = Run(
            """[{"n":0},{"n":100},{"n":null},{"n":"abc"}]""",
            "bar",
            """{"x":{"field":"n","type":"quantitative","bin":true},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert
        Assert.Equal(["0|10|1", "90|100|1", "null|null|2"], Rows(parts.Data.Main, "bin_maxbins_10_n", "bin_maxbins_10_n_end", "__count"));
    }

    /// <summary>
    ///     Validates the bin parameters: an exact <c>step</c> of 25 over 0 to 100 gives the bins starting at 0, 25, 50 and 75 (100 is in the last, closed bin);
    ///     the counts of the values 0, 24, 25, 99, 100 are 2, 1, 2.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_BinStepAndExtentParameters()
    {
        // Act
        var stepped = Run(
            """[{"n":0},{"n":24},{"n":25},{"n":99},{"n":100}]""",
            "bar",
            """{"x":{"field":"n","type":"quantitative","bin":{"step":25}},"y":{"aggregate":"count","type":"quantitative"}}""");
        var extent = Run(
            """[{"n":0},{"n":24},{"n":25},{"n":99},{"n":100}]""",
            "bar",
            """{"x":{"field":"n","type":"quantitative","bin":{"step":25,"extent":[0,50]}},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert: 0 and 24 are in [0, 25); 25 is in [25, 50); 99 and 100 are in the closed last bin [75, 100]
        Assert.Equal(["0|25|2", "25|50|1", "75|100|2"], Rows(stepped.Data.Main, "bin_step_25_n", "bin_step_25_n_end", "__count"));
        Assert.Equal(new BinSpec(0, 100, 25), stepped.Data.Bins["bin_step_25_n"]);

        // Assert: with the extent [0, 50] the bins are [0, 25) and [25, 50]; the values above the extent are in the bin at +infinity
        Assert.Equal(new BinSpec(0, 50, 25), extent.Data.Bins["bin_step_25_extent_0_50_n"]);
        Assert.Equal((0, 50), (extent.Scales["x"]!.DomainStart, extent.Scales["x"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that a discrete (ordinal) bin shows the text of the bin (the boundaries joined with an en dash, ordered by where the bin starts) as
    ///     the categories of the scale: the values 0, 10 ... 100 have the bins 0 to 100 by 10.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_OrdinalBinsAreTextCategoriesOrderedByStart()
    {
        // Act: the rows come in the reverse order, so the domain must be sorted by the bin start, not by appearance
        var rows = "[" + string.Join(",", Enumerable.Range(0, 11).Reverse().Select(n => "{\"n\":" + (n * 10) + "}")) + "]";
        var parts = Run(rows, "bar", """{"x":{"field":"n","type":"ordinal","bin":true},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert
        var domain = parts.Scales["x"]!.DiscreteDomain.Select(v => v.ToText()).ToArray();
        Assert.Equal(
            ["0 \u2013 10", "10 \u2013 20", "20 \u2013 30", "30 \u2013 40", "40 \u2013 50", "50 \u2013 60", "60 \u2013 70", "70 \u2013 80", "80 \u2013 90", "90 \u2013 100"],
            domain);
        Assert.Equal("bin_maxbins_10_n_range", parts.Scales["x"]!.FieldPath);
        Assert.Equal(ScaleKind.Band, parts.Scales["x"]!.Kind);
        Assert.Equal(["2", "1"], Rows(parts.Data.Main, "__count").Take(2).ToArray());
    }

    /// <summary>
    ///     Validates that a discrete (ordinal) bin leaves the category of a value that is not a finite number (null, text) null: the bin text is null, not
    ///     the text "null", so no category named "null" is made up, and the valid values keep their bin text.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_OrdinalBinOfInvalidValuesKeepsANullCategory()
    {
        // Act: 0 and 100 give the bins 0 to 100 by 10; null and "abc" have no bin
        var parts = Run(
            """[{"n":0},{"n":100},{"n":null},{"n":"abc"}]""",
            "bar",
            """{"x":{"field":"n","type":"ordinal","bin":true},"y":{"aggregate":"count","type":"quantitative"}}""");
        var plain = Run(
            """[{"n":0},{"n":100},{"n":null},{"n":"abc"}]""",
            "point",
            """{"x":{"field":"n","type":"ordinal","bin":true},"y":{"field":"n","type":"quantitative"}}""");

        // Assert: the cell of the invalid rows is a null value, never the string "null"
        var cells = Enumerable.Range(0, plain.Data.Main.RowCount).Select(row => plain.Data.Main.GetValue(row, "bin_maxbins_10_n_range")).ToArray();
        Assert.Equal(["0 \u2013 10", "90 \u2013 100"], cells.Take(2).Select(v => v.ToText()).ToArray());
        Assert.All(cells.Skip(2), v => Assert.True(v.IsNull));
        Assert.DoesNotContain(
            Enumerable.Range(0, parts.Data.Main.RowCount).Select(row => parts.Data.Main.GetValue(row, "bin_maxbins_10_n_range")),
            v => v.IsString && v.Text == "null");
        Assert.DoesNotContain(parts.Scales["x"]!.DiscreteDomain, v => v.IsString && v.Text == "null");
    }

    /// <summary>
    ///     Validates that an <c>argmax</c> definition reads the field of the row that has the largest compared value: in group a the row with t = 5 has v = 9,
    ///     in group b the only row has v = 2.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_ArgMaxReadsTheFieldOfTheWinningRow()
    {
        // Act
        var parts = Run(
            """[{"c":"a","v":1,"t":1},{"c":"a","v":9,"t":5},{"c":"b","v":2,"t":2}]""",
            "point",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":{"argmax":"t"}}}""");

        // Assert
        Assert.Equal(["a|9", "b|2"], Rows(parts.Data.Main, "c", "argmax_t.v"));
        Assert.Equal("argmax_t.v", parts.Scales["y"]!.FieldPath);
        Assert.Equal("v for max t", parts.Scales["y"]!.Title);
        Assert.Equal((0, 9), (parts.Scales["y"]!.DomainStart, parts.Scales["y"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that the Raw table is kept next to the aggregated one when a scale sorts by a field: Raw has the three input rows and their fields, Main
    ///     the two groups; and that without the sort the two are one table.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_KeepsTheRawRowsWhenAScaleNeedsThem()
    {
        // Arrange
        const string Rows3 = """[{"c":"a","v":1},{"c":"b","v":2},{"c":"a","v":3}]""";

        // Act
        var parts = Run(
            Rows3,
            "bar",
            """{"x":{"field":"c","type":"nominal","sort":{"op":"max","field":"v"}},"y":{"field":"v","type":"quantitative","aggregate":"sum"}}""");

        // Assert
        Assert.Equal(["c", "v"], parts.Data.Raw.Fields);
        Assert.Equal(3, parts.Data.Raw.RowCount);
        Assert.Equal(2, parts.Data.Main.RowCount);
        Assert.NotSame(parts.Data.Raw, parts.Data.Main);
    }

    /// <summary>
    ///     Validates the row budget of the implicit aggregate: the three input rows stay counted while the two groups are added (five in all), so a
    ///     budget of 4 fails located at the aggregated channel and a budget of 5 succeeds.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_CountsTheGroupsInTheRowBudget()
    {
        // Arrange
        const string Rows3 = """[{"c":"a","v":1},{"c":"b","v":2},{"c":"a","v":3}]""";
        const string Encoding = """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"}}""";

        // Act
        var parts = Run(Rows3, "bar", Encoding, new VegaLiteLimits { MaxDataRows = 5 });
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(Rows3, "bar", Encoding, new VegaLiteLimits { MaxDataRows = 4 }));

        // Assert
        Assert.Equal(2, parts.Data.Main.RowCount);
        Assert.Equal("/encoding/y", exception.Location.Pointer);
        Assert.Contains("MaxDataRows", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a stack that keeps the Raw table next to it counts its rows too (3 + 3), while a stack that replaces its input does not.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_StackNextToRawRowsCountsTheRows()
    {
        // Arrange
        const string Rows3 = """[{"c":"a","g":"x","v":1},{"c":"b","g":"x","v":2},{"c":"a","g":"y","v":3}]""";
        const string Sorted = """{"x":{"field":"c","type":"nominal","sort":{"op":"sum","field":"v"}},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";
        const string Plain = """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";

        // Act
        var kept = Run(Rows3, "bar", Sorted, new VegaLiteLimits { MaxDataRows = 6 });
        var failure = Assert.Throws<VegaLiteRenderException>(() => Run(Rows3, "bar", Sorted, new VegaLiteLimits { MaxDataRows = 5 }));
        var replaced = Run(Rows3, "bar", Plain, new VegaLiteLimits { MaxDataRows = 3 });

        // Assert: Raw is the unstacked table, Main the stacked one; the sort by the sum of v puts b (2) before a (1 + 3 = 4)
        Assert.Equal(["c", "g", "v"], kept.Data.Raw.Fields);
        Assert.Equal(["c", "g", "v", "v_end", "v_start"], kept.Data.Main.Fields);
        Assert.Equal(["b", "a"], kept.Scales["x"]!.DiscreteDomain.Select(v => v.ToText()).ToArray());
        Assert.Equal("/encoding/y", failure.Location.Pointer);
        Assert.Same(replaced.Data.Raw, replaced.Data.Main);
    }

    /// <summary>
    ///     Validates the row budget when an aggregate and a stack run next to the rows a scale still reads: the 3 input rows stay (3), the 3 groups of the
    ///     aggregate are added (6), and the stacked copy is counted before it is allocated (9) and then replaces the aggregated rows (back to 6). So a budget
    ///     of 9 succeeds and a budget of 8 fails located at the stacked channel instead of allocating the copy unchecked.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_StackOfAggregateNextToRawRowsCountsTheStackedRows()
    {
        // Arrange: three rows, three (category, layer) groups; the sort by an operation on the field keeps the unaggregated rows (Raw)
        const string Rows3 = """[{"c":"a","g":"x","v":1},{"c":"b","g":"x","v":2},{"c":"a","g":"y","v":3}]""";
        const string Encoding = """{"x":{"field":"c","type":"nominal","sort":{"op":"max","field":"v"}},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""";

        // Act
        var kept = Run(Rows3, "bar", Encoding, new VegaLiteLimits { MaxDataRows = 9 });
        var failure = Assert.Throws<VegaLiteRenderException>(() => Run(Rows3, "bar", Encoding, new VegaLiteLimits { MaxDataRows = 8 }));

        // Assert
        Assert.Equal(3, kept.Data.Raw.RowCount);
        Assert.Equal(3, kept.Data.Main.RowCount);
        Assert.Contains("sum_v_start", kept.Data.Main.Fields);
        Assert.NotSame(kept.Data.Raw, kept.Data.Main);
        Assert.Equal("/encoding/y", failure.Location.Pointer);
        Assert.Contains("MaxDataRows", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the limits of the bins: more bins than <see cref="VegaLiteLimits.MaxScaleDomainSize"/> is a located failure at the <c>bin</c>, and the step budget
    ///     is charged for the rows (a located failure that names <c>MaxEvaluationSteps</c>).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_BoundsTheBinsAndChargesTheRows()
    {
        // Arrange
        const string Sequence = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":5000,\"as\":\"id\"}},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"id\",\"type\":\"quantitative\",\"bin\":{\"maxbins\":1000}},\"y\":{\"aggregate\":\"count\",\"type\":\"quantitative\"}}}";
        const string Histogram = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":5000,\"as\":\"id\"}},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"id\",\"type\":\"quantitative\",\"bin\":true},\"y\":{\"aggregate\":\"count\",\"type\":\"quantitative\"}}}";

        // Act
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.ResolveFull(Sequence, new VegaLiteLimits { MaxScaleDomainSize = 100 }));
        var steps = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.ResolveFull(Histogram, new VegaLiteLimits { MaxEvaluationSteps = 100 }));
        var fine = ChartTestHelpers.ResolveFull(Histogram);

        // Assert
        Assert.Equal("/encoding/x/bin", tooMany.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", tooMany.Message, StringComparison.Ordinal);
        Assert.StartsWith("/encoding/", steps.Location.Pointer, StringComparison.Ordinal);
        Assert.Contains("MaxEvaluationSteps", steps.Message, StringComparison.Ordinal);
        Assert.True(fine.Data.Main.RowCount > 0);
    }

    /// <summary>
    ///     Validates that a canceled token stops the stage (the standalone entry polls the token while it reads the rows).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_PollsTheCancellationToken()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"data\":{\"sequence\":{\"start\":0,\"stop\":5000,\"as\":\"id\"}},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"id\",\"type\":\"quantitative\",\"bin\":true},\"y\":{\"aggregate\":\"count\",\"type\":\"quantitative\"}}}");
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var plan = EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, null));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => EncodingTransforms.ApplyStandalone(plan, table, tree.Root, null, null, source.Token));
    }

    /// <summary>
    ///     Validates that an encoding that asks for nothing gives the input table itself as both tables, and the argument checks.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_IdentityReturnsTheInputAndChecksArguments()
    {
        // Arrange
        var tree = SpecParser.Parse(Spec("""[{"c":"a","v":1}]""", "point", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"}}"""));
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var plan = EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, null));

        // Act
        var data = EncodingTransforms.ApplyStandalone(plan, table, tree.Root, null, null, Ct);

        // Assert
        Assert.Same(table, data.Raw);
        Assert.Same(table, data.Main);
        Assert.Empty(data.Bins);
        Assert.Throws<ArgumentNullException>(() => EncodingTransforms.Apply(null!, table, null!));
        Assert.Throws<ArgumentNullException>(() => EncodingTransforms.ApplyStandalone(plan, null!, tree.Root, null, null, Ct));
        Assert.Throws<ArgumentNullException>(() => EncodingTransforms.ApplyStandalone(plan, table, null!, null, null, Ct));
    }

    /// <summary>
    ///     Validates that the stage is deterministic: two runs, and runs under other cultures, give the same tables, domains and labels.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Apply_IsDeterministicAndCultureInvariant()
    {
        // Arrange
        var rows = "[" + string.Join(",", Enumerable.Range(0, 40).Select(n => "{\"n\":" + (n * 1.25).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"g\":\"" + (n % 3) + "\"}")) + "]";
        var encoding = """{"x":{"field":"n","type":"ordinal","bin":{"maxbins":7}},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";
        string Describe()
        {
            var parts = Run(rows, "bar", encoding);
            return string.Join(
                ";",
                Rows(parts.Data.Main, "bin_maxbins_7_n_range", "g", "__count_start", "__count_end").Concat(parts.Scales["x"]!.DiscreteDomain.Select(v => v.ToText())));
        }

        var expected = Describe();

        // Act / Assert
        Assert.Equal(expected, Describe());
        Assert.Equal(expected, ExpressionTestSupport.UnderCulture("tr-TR", Describe));
        Assert.Equal(expected, ExpressionTestSupport.UnderCulture("de-DE", Describe));
    }
}
