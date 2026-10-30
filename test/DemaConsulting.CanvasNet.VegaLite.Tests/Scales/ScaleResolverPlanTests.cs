using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Unit tests for the scale resolver's use of the encoding plan: the encoding <c>sort</c> forms (a channel shorthand, an <c>encoding</c> object, an operation
///     with a field) computed over the Raw table, the domains of ranges, bins and unaggregated measures, the titles and the plan facts a scale carries, and the
///     limits of the sort. The expected orders are derived by hand from the data in the comments.
/// </summary>
public class ScaleResolverPlanTests
{
    /// <summary>The rows used by the sort tests: c once (1), b once (10) and a twice (2 and 3), in that order of first appearance.</summary>
    private const string SortRows = """[{"k":"c","w":1},{"k":"b","w":10},{"k":"a","w":2},{"k":"a","w":3}]""";

    /// <summary>
    ///     Builds a specification and resolves it.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The JSON text of the encoding.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The prepared parts.</returns>
    private static ChartParts Parts(string rows, string mark, string encoding, VegaLiteLimits? limits = null) =>
        ChartTestHelpers.ResolveFull("{\"data\":{\"values\":" + rows + "},\"mark\":\"" + mark + "\",\"encoding\":" + encoding + "}", limits);

    /// <summary>
    ///     Gets the categories of the x scale in domain order, joined with a comma.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The categories.</returns>
    private static string XDomain(ChartParts parts) => string.Join(",", parts.Scales["x"]!.DiscreteDomain.Select(v => v.ToText()));

    /// <summary>
    ///     Validates the sort by another channel (the shorthands <c>y</c> and <c>-y</c> and the <c>encoding</c> object) when that channel aggregates: the
    ///     operation is the channel's aggregate over the Raw rows. The sums of w are c 1, a 5 (2 + 3), b 10; the counts are a 2, c 1, b 1 (c before b by appearance).
    /// </summary>
    /// <param name="aggregate">The aggregate of the <c>y</c> channel (field w, or a count).</param>
    /// <param name="sort">The <c>sort</c> of the <c>x</c> channel.</param>
    /// <param name="expected">The expected domain.</param>
    [Theory]
    [InlineData("""{"aggregate":"sum","field":"w","type":"quantitative"}""", "\"y\"", "c,a,b")]
    [InlineData("""{"aggregate":"sum","field":"w","type":"quantitative"}""", "\"-y\"", "b,a,c")]
    [InlineData("""{"aggregate":"sum","field":"w","type":"quantitative"}""", """{"encoding":"y"}""", "c,a,b")]
    [InlineData("""{"aggregate":"sum","field":"w","type":"quantitative"}""", """{"encoding":"y","order":"descending"}""", "b,a,c")]
    [InlineData("""{"aggregate":"count","type":"quantitative"}""", "\"y\"", "c,b,a")]
    [InlineData("""{"aggregate":"count","type":"quantitative"}""", "\"-y\"", "a,c,b")]
    [InlineData("""{"aggregate":"max","field":"w","type":"quantitative"}""", "\"-y\"", "b,a,c")]
    [InlineData("""{"aggregate":"mean","field":"w","type":"quantitative"}""", "\"y\"", "c,a,b")]
    public void ScaleResolver_Resolve_SortsByAnotherChannelsAggregate(string aggregate, string sort, string expected)
    {
        // Act
        var parts = Parts(SortRows, "bar", "{\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"sort\":" + sort + "},\"y\":" + aggregate + "}");

        // Assert
        Assert.Equal(expected, XDomain(parts));
    }

    /// <summary>
    ///     Validates the sort by a channel that does not aggregate: its operation is <c>min</c> for a mark that does not stack (the minimum of w is c 1, a 2, b 10) and
    ///     <c>sum</c> for the stacked channel of a bar (the sums are c 1, a 5, b 10), so a descending sort puts b first in both cases and the orders differ only by the
    ///     second place: for min and descending the order is b (10), a (2), c (1); for sum it is b (10), a (5), c (1).
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_SortByAPlainChannelUsesMinOrSumWhenStacked()
    {
        // Act
        var point = Parts(SortRows, "point", """{"x":{"field":"k","type":"nominal","sort":"-y"},"y":{"field":"w","type":"quantitative"}}""");
        var bar = Parts(SortRows, "bar", """{"x":{"field":"k","type":"nominal","sort":"-y"},"y":{"field":"w","type":"quantitative"}}""");

        // Assert
        Assert.Equal("b,a,c", XDomain(point));
        Assert.Equal("b,a,c", XDomain(bar));
        Assert.Null(point.Plan.Stack);
        Assert.NotNull(bar.Plan.Stack);
    }

    /// <summary>
    ///     Validates the operations of a sort object: the operation defaults to <c>min</c> and the field to the channel's own field; every aggregate that yields a value
    ///     orders the categories. The values are: q3 of a is 2.75 (R-7 interpolation of 2 and 3), b is 10, c is 1; the sample variance of a is 0.5 and is null
    ///     for the single-value categories (null sorts first); the number of valid values is a 2, b 1, c 1; the distinct count of w is a 2, b 1, c 1.
    /// </summary>
    /// <param name="sort">The sort object.</param>
    /// <param name="expected">The expected domain.</param>
    [Theory]
    [InlineData("""{"field":"w"}""", "c,a,b")]
    [InlineData("""{"field":"w","order":"descending"}""", "b,a,c")]
    [InlineData("""{"op":"q3","field":"w"}""", "c,a,b")]
    [InlineData("""{"op":"variance","field":"w"}""", "c,b,a")]
    [InlineData("""{"op":"variance","field":"w","order":"descending"}""", "c,b,a")]
    [InlineData("""{"op":"valid","field":"w"}""", "c,b,a")]
    [InlineData("""{"op":"distinct","field":"w","order":"descending"}""", "a,c,b")]
    [InlineData("""{"op":"missing","field":"w"}""", "c,b,a")]
    [InlineData("""{"op":"count"}""", "c,b,a")]
    [InlineData("""{"order":"descending"}""", "c,b,a")]
    public void ScaleResolver_Resolve_SortsByAnOperationOfAField(string sort, string expected)
    {
        // Act
        var parts = Parts(SortRows, "point", "{\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"sort\":" + sort + "},\"y\":{\"field\":\"w\",\"type\":\"quantitative\"}}");

        // Assert: {"order": "descending"} alone sorts the values themselves: c, b, a
        Assert.Equal(expected, XDomain(parts));
    }

    /// <summary>
    ///     Validates that the sort by a field reads the Raw rows and not the aggregated ones: the aggregated table has no field w, so a sort by the sum of w
    ///     only works on the rows before the aggregate (a has 100 + 100, b has 1).
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_SortOfAnAggregatedViewReadsTheRawRows()
    {
        // Act
        var parts = Parts(
            """[{"k":"a","v":1,"w":100},{"k":"b","v":50,"w":1},{"k":"a","v":1,"w":100}]""",
            "bar",
            """{"x":{"field":"k","type":"nominal","sort":{"op":"sum","field":"w"}},"y":{"field":"v","type":"quantitative","aggregate":"sum"}}""");

        // Assert
        Assert.Equal("b,a", XDomain(parts));
        Assert.Equal(["a|2", "b|50"], AggregateTestSupport.Rows(parts.Data.Main, "k", "sum_v"));
        Assert.DoesNotContain("w", parts.Data.Main.Fields);
    }

    /// <summary>
    ///     Validates that a sort that cannot be resolved keeps the ascending default: a shorthand naming a channel that is not in the encoding, and an
    ///     operation that cannot order categories; and that the plain forms (ascending, descending, null, an array) are unchanged.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_UnresolvableSortsAndPlainFormsKeepTheirOrder()
    {
        // Act
        string Sorted(string sort) => XDomain(Parts(SortRows, "point", "{\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"sort\":" + sort + "},\"y\":{\"field\":\"w\",\"type\":\"quantitative\"}}"));

        // Assert
        Assert.Equal("a,b,c", Sorted("\"-color\""));
        Assert.Equal("a,b,c", Sorted("\"ascending\""));
        Assert.Equal("c,b,a", Sorted("\"descending\""));
        Assert.Equal("c,b,a", Sorted("null"));
        Assert.Equal("c,b,a", Sorted("[\"b\",\"a\"]"));
    }

    /// <summary>
    ///     Validates that the sort by the aggregated, stacked channel reads the Raw sums: a is 1 + 2 + 4 = 7 and b is 9, so the descending sort puts b first
    ///     (the aggregated rows are per color and category, 3 of them, and would give the wrong totals).
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_SortByAnAggregatedStackedChannelUsesTheRawSums()
    {
        // Act
        var parts = Parts(
            """[{"k":"a","g":"p","v":1},{"k":"a","g":"q","v":2},{"k":"b","g":"p","v":9},{"k":"a","g":"p","v":4}]""",
            "bar",
            """{"x":{"field":"k","type":"nominal","sort":"-y"},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal("b,a", XDomain(parts));
        Assert.NotNull(parts.Plan.Stack);
    }

    /// <summary>
    ///     Validates that the text categories of an ordinal bin are ordered by where the bin starts and that <c>descending</c> reverses them: the values 0 to 100 by 10.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OrdinalBinsSortByStartAndDescendingReverses()
    {
        // Arrange
        var rows = "[" + string.Join(",", Enumerable.Range(0, 11).Select(n => "{\"n\":" + (n * 10) + "}")) + "]";

        // Act
        var ascending = Parts(rows, "bar", """{"x":{"field":"n","type":"ordinal","bin":true},"y":{"aggregate":"count","type":"quantitative"}}""");
        var descending = Parts(rows, "bar", """{"x":{"field":"n","type":"ordinal","bin":true,"sort":"descending"},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert
        Assert.Equal("0 \u2013 10", ascending.Scales["x"]!.DiscreteDomain[0].ToText());
        Assert.Equal("90 \u2013 100", ascending.Scales["x"]!.DiscreteDomain[^1].ToText());
        Assert.Equal("90 \u2013 100", descending.Scales["x"]!.DiscreteDomain[0].ToText());
        Assert.Equal(10, descending.Scales["x"]!.DiscreteDomain.Count);
    }

    /// <summary>
    ///     Validates that the number format of the axis formats the boundaries in the text of an ordinal bin: the bins of 0 to 1 by 0.1 with the format <c>.0%</c>.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OrdinalBinTextUsesTheAxisFormat()
    {
        // Act
        var rows = "[" + string.Join(",", Enumerable.Range(0, 11).Select(n => "{\"n\":" + (n / 10.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + "}")) + "]";
        var parts = Parts(rows, "bar", """{"x":{"field":"n","type":"ordinal","bin":true,"axis":{"format":".0%"}},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert: [0, 1] with step 0.1
        Assert.Equal("0% \u2013 10%", parts.Scales["x"]!.DiscreteDomain[0].ToText());
        Assert.Equal("90% \u2013 100%", parts.Scales["x"]!.DiscreteDomain[^1].ToText());
    }

    /// <summary>
    ///     Validates the domain of a range: the extent over both fields (x is 1 to 5 and x2 is 3 to 9, so the domain is 1 to 9), with the title merged.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_RangeDomainIsTheUnionOfBothFields()
    {
        // Act
        var parts = Parts(
            """[{"a":1,"b":3,"c":"p"},{"a":5,"b":9,"c":"q"}]""",
            "bar",
            """{"x":{"field":"a","type":"quantitative","scale":{"zero":false}},"x2":{"field":"b"},"y":{"field":"c","type":"nominal"}}""");

        // Assert
        var x = parts.Scales["x"]!;
        Assert.Equal((1, 9), (x.DomainStart, x.DomainEnd));
        Assert.Equal("a, b", x.Title);
        Assert.Equal("b", x.Field2Path);
        Assert.DoesNotContain(parts.Scales.Scales, s => s.Channel == "x2");
    }

    /// <summary>
    ///     Validates the domain of pre-binned data (<c>bin: "binned"</c>): the union of the start and the end fields (the starts 0 and 10 and the ends 10 and 20 give 0 to 20),
    ///     with no zero rule and no nice, and that the scale is marked as binned without computed bins.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PreBinnedDomainIsTheUnionOfStartAndEnd()
    {
        // Act
        var parts = Parts(
            """[{"lo":5,"hi":10,"n":3},{"lo":10,"hi":20,"n":4}]""",
            "bar",
            """{"x":{"field":"lo","type":"quantitative","bin":"binned"},"x2":{"field":"hi"},"y":{"field":"n","type":"quantitative"}}""");

        // Assert: the data starts at 5, so a zero rule would give 0; none applies to a binned scale
        var x = parts.Scales["x"]!;
        Assert.Equal((5, 20), (x.DomainStart, x.DomainEnd));
        Assert.Equal(BinKind.Binned, x.Bin);
        Assert.Null(x.BinSpec);
        Assert.Null(x.NiceCount);
    }

    /// <summary>
    ///     Validates that a binned scale has no <c>nice</c> by default but honors an explicit one, and that an explicit domain is used as given.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_BinnedScalesHaveNoNiceUnlessAsked()
    {
        // Arrange
        const string Rows = """[{"n":3},{"n":18},{"n":47}]""";

        // Act
        var plain = Parts(Rows, "bar", """{"x":{"field":"n","type":"quantitative","bin":true},"y":{"aggregate":"count","type":"quantitative"}}""");
        var nice = Parts(Rows, "bar", """{"x":{"field":"n","type":"quantitative","bin":true,"scale":{"nice":true}},"y":{"aggregate":"count","type":"quantitative"}}""");
        var domain = Parts(Rows, "bar", """{"x":{"field":"n","type":"quantitative","bin":true,"scale":{"domain":[0,100]}},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert: the extent is 3 to 47 with 10 bins: span 44, the first step 10^(round(log10 44) - 1) = 10 fits (5 bins), and the divisor 2 (step 5, 8.8 bins) fits too, so nice gives 0 to 50
        Assert.Equal((0, 50), (plain.Scales["x"]!.DomainStart, plain.Scales["x"]!.DomainEnd));
        Assert.Equal(new BinSpec(0, 50, 5), plain.Data.Bins["bin_maxbins_10_n"]);
        Assert.Null(plain.Scales["x"]!.NiceCount);
        Assert.Equal(10, nice.Scales["x"]!.NiceCount);
        Assert.Equal((0, 100), (domain.Scales["x"]!.DomainStart, domain.Scales["x"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates the <c>unaggregated</c> domain: the mean of v per category is 2 for a (1 and 3) and 8 for b (4 and 12), the rows before the aggregate span 1 to 12;
    ///     the unaggregated domain reads the Raw rows (1 to 12), the normal one the means (2 to 8). A sum has no unaggregated domain and uses the normal one.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_UnaggregatedDomainReadsTheRawRows()
    {
        // Arrange
        const string Rows = """[{"k":"a","v":1},{"k":"a","v":3},{"k":"b","v":4},{"k":"b","v":12}]""";

        // Act
        var unaggregated = Parts(Rows, "point", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"mean","scale":{"domain":"unaggregated","zero":false}}}""");
        var normal = Parts(Rows, "point", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"mean","scale":{"zero":false}}}""");
        var sum = Parts(Rows, "point", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum","scale":{"domain":"unaggregated","zero":false}}}""");

        // Assert
        Assert.Equal((1, 12), (unaggregated.Scales["y"]!.DomainStart, unaggregated.Scales["y"]!.DomainEnd));
        Assert.Equal((2, 8), (normal.Scales["y"]!.DomainStart, normal.Scales["y"]!.DomainEnd));
        Assert.Equal((4, 16), (sum.Scales["y"]!.DomainStart, sum.Scales["y"]!.DomainEnd));
        Assert.True(unaggregated.Plan.NeedsRaw);
        Assert.NotSame(unaggregated.Data.Raw, unaggregated.Data.Main);
    }

    /// <summary>
    ///     Validates the facts a scale carries from the plan: the field and its second end, the title, the bin kind and the stack offset; a datum has neither a field nor a title.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ScalesCarryThePlanFacts()
    {
        // Act
        var parts = Parts(
            """[{"c":"a","g":"p","v":1},{"c":"a","g":"q","v":2}]""",
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum","stack":"normalize"},"color":{"field":"g","type":"nominal"}}""");
        var datum = Parts("""[{"a":1}]""", "point", """{"x":{"datum":3,"type":"quantitative"}}""");

        // Assert
        var y = parts.Scales["y"]!;
        Assert.Equal("sum_v_end", y.FieldPath);
        Assert.Equal("sum_v_start", y.Field2Path);
        Assert.Equal("Sum of v", y.Title);
        Assert.Equal(StackOffset.Normalize, y.Stack);
        Assert.Equal(BinKind.None, y.Bin);
        Assert.Null(parts.Scales["x"]!.Stack);
        Assert.Same(parts.Plan, parts.Scales.Plan);
        Assert.Null(datum.Scales["x"]!.FieldPath);
        Assert.Null(datum.Scales["x"]!.Title);
    }

    /// <summary>
    ///     Validates the cost of a sort by an aggregate: the rows are charged to the step budget (a located failure at the channel that names
    ///     <c>MaxEvaluationSteps</c>), the categories are bounded by <see cref="VegaLiteLimits.MaxScaleDomainSize"/>, and the cancellation token is polled.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_SortIsChargedBoundedAndCancellable()
    {
        // Arrange: 5,000 rows, each its own category
        const string Spec = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":5000,\"as\":\"id\"}},\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"id\",\"type\":\"nominal\",\"sort\":{\"op\":\"median\",\"field\":\"id\"}},\"y\":{\"field\":\"id\",\"type\":\"quantitative\"}}}";
        var tree = SpecParser.Parse(Spec);
        var table = DataLoader.Load(tree.Root["data"], null, null, TestContext.Current.CancellationToken);
        var config = ConfigResolver.Resolve(tree, null);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        var charged = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.ResolveFull(Spec, new VegaLiteLimits { MaxEvaluationSteps = 40 }));
        var bounded = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.ResolveFull(Spec, new VegaLiteLimits { MaxScaleDomainSize = 100 }));
        var cancellation = Assert.ThrowsAny<OperationCanceledException>(() => ScaleResolver.Resolve(tree.Root, table, config, null, null, canceled.Token));
        var fine = ChartTestHelpers.ResolveFull(Spec);

        // Assert
        Assert.Equal("/encoding/x", charged.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", charged.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/x", bounded.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", bounded.Message, StringComparison.Ordinal);
        Assert.NotNull(cancellation);
        Assert.Equal(5000, fine.Scales["x"]!.DiscreteDomain.Count);
    }

    /// <summary>
    ///     Validates the supported field sorts that the feature catalog shares: any orderable operation, a valid field and order, no <c>encoding</c>; the argument check.
    /// </summary>
    [Fact]
    public void ScaleResolver_IsSupportedFieldSort_AcceptsOrderableOperations()
    {
        // Arrange
        static bool Supported(string sort) => ScaleResolver.IsSupportedFieldSort(SpecParser.Parse("{\"s\":" + sort + "}").Root["s"]!);

        // Act / Assert
        Assert.True(Supported("""{"op":"q1","field":"w"}"""));
        Assert.True(Supported("""{"op":"distinct","order":"descending"}"""));
        Assert.False(Supported("""{"op":"sum","order":"up"}"""));
        Assert.False(Supported("""{"encoding":"y"}"""));
        Assert.False(Supported("\"x\""));
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.IsSupportedFieldSort(null!));
    }
}
