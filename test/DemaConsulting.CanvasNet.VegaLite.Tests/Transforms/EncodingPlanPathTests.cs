using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the path half of <see cref="EncodingPlan"/>: the fields that group the rows of a line, area or trail into paths, the fields of the
///     <c>order</c> channel, the plan of the <c>impute</c> property of <c>x</c> and <c>y</c> and the plan of the impute that precedes the stack of a line or area.
///     The expectations are derived by hand from the grouping rules of Vega-Lite's <c>pathGroupingFields</c> and the data flow of its <c>ImputeNode</c> and <c>StackNode</c>.
/// </summary>
public class EncodingPlanPathTests
{
    /// <summary>
    ///     Creates the plan of a view.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <returns>The plan.</returns>
    private static EncodingPlan Create(string mark, string encoding)
    {
        var tree = SpecParser.Parse("{\"data\":{\"values\":[{\"a\":1}]},\"mark\":\"" + mark + "\",\"encoding\":" + encoding + "}");
        return EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, null));
    }

    /// <summary>
    ///     Validates the grouping rules: the positions, polar channels, <c>text</c>, <c>shape</c>, <c>angle</c> and the link channels never group; the paint channels,
    ///     <c>detail</c> and <c>key</c> do (in channel order and without duplicates); <c>order</c> groups an area but not a line or trail; <c>size</c> groups a line
    ///     but not a trail; a definition that aggregates never groups; the field of a bin or a time unit is the derived one.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="expected">The expected fields, joined with a comma.</param>
    [Theory]
    [InlineData("line", """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative"}}""", "")]
    [InlineData("line", """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", "g")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"color":{"field":"g"},"detail":[{"field":"d1"},{"field":"d2"}],"order":{"field":"o"}}""", "g,d1,d2")]
    [InlineData("area", """{"x":{"field":"t"},"y":{"field":"v"},"color":{"field":"g"},"detail":[{"field":"d1"},{"field":"d2"}],"order":{"field":"o"}}""", "g,d1,d2,o")]
    [InlineData("trail", """{"x":{"field":"t"},"y":{"field":"v"},"size":{"field":"s"},"order":{"field":"o"},"key":{"field":"k"}}""", "k")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"size":{"field":"s"},"key":{"field":"k"}}""", "s,k")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"color":{"field":"g"},"detail":{"field":"g"}}""", "g")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"stroke":{"field":"a"},"fill":{"field":"b"},"opacity":{"field":"c"},"fillOpacity":{"field":"d"},"strokeOpacity":{"field":"e"},"strokeDash":{"field":"f"},"strokeWidth":{"field":"h"}}""", "a,b,c,d,e,f,h")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"x2":{"field":"u"},"y2":{"field":"w"},"theta":{"field":"p"},"theta2":{"field":"q"},"radius":{"field":"r"},"radius2":{"field":"s"}}""", "")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"text":{"field":"a"},"shape":{"field":"b"},"angle":{"field":"c"},"tooltip":{"field":"d"},"href":{"field":"e"},"description":{"field":"f"},"url":{"field":"h"}}""", "")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"color":{"value":"red"},"detail":{"value":1},"order":{"value":null}}""", "")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"aggregate":"sum","field":"v"},"color":{"aggregate":"mean","field":"g"},"detail":{"field":"d"}}""", "d")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"color":{"field":"c","type":"quantitative","bin":true}}""", "bin_maxbins_6_c")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"color":{"field":"c","type":"nominal","bin":true}}""", "bin_maxbins_6_c_range")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"detail":{"field":"d","timeUnit":"year"}}""", "year_d")]
    [InlineData("line", """{"x":{"field":"t"},"y":{"field":"v"},"detail":{"field":"a.b"},"color":{"field":"a\\.b"}}""", "a.b,a\\.b")]
    [InlineData("area", """{"x":{"field":"t"},"y":{"field":"v"},"order":{"aggregate":"max","field":"o"},"color":{"field":"g"}}""", "g")]
    public void EncodingPlan_PathGroupFields_FollowVegaLiteGroupingRules(string mark, string encoding, string expected)
    {
        // Act
        var plan = Create(mark, encoding);

        // Assert
        Assert.Equal(expected, string.Join(",", plan.PathGroupFields));
    }

    /// <summary>
    ///     Validates the order fields: one entry for every definition that reads a field, in order, with its direction; an order that is a value, an aggregated order
    ///     (read through its derived name) and an encoding without an order.
    /// </summary>
    [Fact]
    public void EncodingPlan_OrderFields_ListTheOrderChannelWithDirections()
    {
        // Act
        var single = Create("line", """{"x":{"field":"t"},"y":{"field":"v"},"order":{"field":"o"}}""");
        var several = Create("line", """{"x":{"field":"t"},"y":{"field":"v"},"order":[{"field":"a"},{"field":"b","sort":"descending"},{"field":"c","sort":"ascending"}]}""");
        var constant = Create("line", """{"x":{"field":"t"},"y":{"field":"v"},"order":{"value":null}}""");
        var aggregated = Create("line", """{"x":{"field":"t"},"y":{"field":"v"},"order":{"aggregate":"max","field":"o","sort":"descending"}}""");
        var none = Create("line", """{"x":{"field":"t"},"y":{"field":"v"}}""");

        // Assert
        Assert.Equal([new SortField("o", false)], single.OrderFields);
        Assert.Equal([new SortField("a", false), new SortField("b", true), new SortField("c", false)], several.OrderFields);
        Assert.Empty(constant.OrderFields);
        Assert.Equal([new SortField("max_o", true)], aggregated.OrderFields);
        Assert.Empty(none.OrderFields);
        Assert.Empty(Create("line", "{}").OrderFields);
    }

    /// <summary>
    ///     Validates the plan of the <c>impute</c> property of a position channel: the imputed field is the one of the channel that has the object, the key is the
    ///     other position channel, the series are the path groups, and the plan needs a field definition on both channels (the <c>x</c> channel wins when both impute).
    /// </summary>
    [Fact]
    public void EncodingPlan_Impute_PlansFieldKeyAndPathGroups()
    {
        // Act
        var onY = Create("line", """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative","impute":{"value":0}},"color":{"field":"g","type":"nominal"},"order":{"field":"o"}}""");
        var onX = Create("line", """{"x":{"field":"t","type":"quantitative","impute":{"method":"mean"}},"y":{"field":"v","type":"quantitative"}}""");
        var both = Create("line", """{"x":{"field":"t","impute":{}},"y":{"field":"v","impute":{"value":1}}}""");
        var nullImpute = Create("line", """{"x":{"field":"t"},"y":{"field":"v","impute":null}}""");
        var noKey = Create("line", """{"y":{"field":"v","impute":{}}}""");
        var valueKey = Create("line", """{"x":{"value":3},"y":{"field":"v","impute":{}}}""");
        var other = Create("line", """{"x":{"field":"t"},"y":{"field":"v"},"color":{"field":"g","impute":{}}}""");
        var aggregated = Create("line", """{"x":{"field":"t"},"y":{"field":"v","aggregate":"sum","impute":{}},"color":{"field":"g"}}""");
        var counted = Create("line", """{"x":{"field":"t"},"y":{"aggregate":"count","impute":{}}}""");

        // Assert: the order does not group a line, so the series are the colors
        Assert.Equal("v", onY.Impute!.Field);
        Assert.Equal("t", onY.Impute.Key);
        Assert.Equal(["g"], onY.Impute.GroupBy);
        Assert.Same(onY.Channel("y")!.Definition, onY.Impute.Definition);
        Assert.False(onY.IsIdentity);
        Assert.Equal(("t", "v"), (onX.Impute!.Field, onX.Impute.Key));
        Assert.Empty(onX.Impute.GroupBy);
        Assert.Equal(("t", "v"), (both.Impute!.Field, both.Impute.Key));
        Assert.Null(nullImpute.Impute);
        Assert.Null(noKey.Impute);
        Assert.Null(valueKey.Impute);
        Assert.Null(other.Impute);
        Assert.True(other.IsIdentity);

        // Assert: an aggregated measure is imputed through its derived name after the aggregate, and a count through the count field
        Assert.Equal(("sum_v", "t"), (aggregated.Impute!.Field, aggregated.Impute.Key));
        Assert.Equal(("__count", "t"), (counted.Impute!.Field, counted.Impute.Key));
    }

    /// <summary>
    ///     Validates the plan of the impute of a stacked path mark: it exists for a stacked line or area with stack-by fields and an unbinned dimension, its series are the
    ///     stack-by fields, its key the dimension and its field the stacked measure; an <c>impute: null</c>, a missing stack-by field, a mark that is not a path mark and a binned
    ///     dimension have none (the last is also the case the catalog warns about).
    /// </summary>
    [Fact]
    public void EncodingPlan_StackImpute_SkipsABinnedDimension()
    {
        // Arrange
        const string Layers = """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";

        // Act
        var area = Create("area", Layers);
        var line = Create("line", """{"x":{"field":"t","type":"quantitative"},"y":{"field":"v","type":"quantitative","stack":true},"color":{"field":"g","type":"nominal"},"detail":{"field":"d"}}""");
        var noImpute = Create("area", """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","impute":null},"color":{"field":"g"}}""");
        var noLayers = Create("area", """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"}}""");
        var bar = Create("bar", Layers);
        var binned = Create("area", """{"x":{"field":"t","type":"quantitative","bin":true},"y":{"field":"v","type":"quantitative"},"color":{"field":"g"}}""");
        var horizontal = Create("area", """{"y":{"field":"t","type":"ordinal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g"}}""");
        var aggregated = Create("area", """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g"}}""");
        var timeUnit = Create("area", """{"x":{"field":"t","type":"temporal","timeUnit":"year"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g"}}""");
        var impute = Create("area", """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","impute":{"value":9}},"color":{"field":"g"}}""");

        // Assert: the stack-by fields are the series (the line also has its detail), the dimension is the key
        Assert.Equal(("v", "t", "g"), (area.StackImpute!.Field, area.StackImpute.Key, string.Join(",", area.StackImpute.GroupBy)));
        Assert.Same(area.Stack!.Definition, area.StackImpute.Definition);
        Assert.Equal("g,d", string.Join(",", line.StackImpute!.GroupBy));
        Assert.Null(noImpute.StackImpute);
        Assert.NotNull(noImpute.Stack);
        Assert.Null(noLayers.StackImpute);
        Assert.Null(bar.StackImpute);
        Assert.NotNull(bar.Stack);

        // Assert: a binned dimension is stacked but not imputed
        Assert.Null(binned.StackImpute);
        Assert.NotNull(binned.Stack);

        // Assert: a horizontal area stacks x, the key is y; an aggregate stacks its derived field; a time unit is imputed through its start field
        Assert.Equal(("v", "t"), (horizontal.StackImpute!.Field, horizontal.StackImpute.Key));
        Assert.Equal(("sum_v", "t"), (aggregated.StackImpute!.Field, aggregated.StackImpute.Key));
        Assert.Equal("year_t", timeUnit.StackImpute!.Key);

        // Assert: an impute object on the stacked channel does not switch the stack impute off
        Assert.NotNull(impute.StackImpute);
        Assert.NotNull(impute.Impute);
    }
}
