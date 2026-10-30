using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for <see cref="StackRules"/>, which decides from the specification alone whether the marks of a view are stacked, the
///     way Vega-Lite does.
/// </summary>
public class StackRulesTests
{
    /// <summary>
    ///     Parses a one-view specification and decides whether its marks are stacked.
    /// </summary>
    /// <param name="mark">The text of the <c>mark</c> (a name or a mark definition).</param>
    /// <param name="encoding">The text of the <c>encoding</c>.</param>
    /// <returns>The decision, or null.</returns>
    private static StackDecision? Decide(string mark, string encoding)
    {
        var tree = SpecParser.Parse("{\"mark\":" + mark + ",\"encoding\":" + encoding + "}");
        var node = tree.Root["mark"]!;
        var type = node.IsString ? node.StringValue! : node["type"]!.StringValue!;
        return StackRules.Decide(type, node["orient"]?.AsString(), tree.Root["encoding"]);
    }

    /// <summary>
    ///     Validates the cases where a stack exists: the stacked channel, the offset, and the fields that split a group.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="channel">The expected stacked channel.</param>
    /// <param name="offset">The expected offset.</param>
    /// <param name="stackBy">The expected number of stack-by definitions.</param>
    [Theory]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", "y", "zero", 1)]
    [InlineData("""{"type":"bar"}""", """{"y":{"field":"k","type":"ordinal"},"x":{"field":"v","type":"quantitative"},"detail":[{"field":"g"},{"field":"h"}]}""", "x", "zero", 2)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"}}""", "y", "zero", 0)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"k","type":"nominal"}}""", "y", "zero", 0)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"value":"red"}}""", "y", "zero", 0)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":true}}""", "y", "zero", 0)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"normalize"},"color":{"field":"g"}}""", "y", "normalize", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"center"},"color":{"field":"g"}}""", "y", "center", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", "y", "zero", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", "y", "zero", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"},"order":{"field":"o"}}""", "y", "zero", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"},"color":{"field":"g"},"size":{"field":"s"},"order":{"field":"o"}}""", "y", "zero", 3)]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"},"color":{"field":"g"},"size":{"aggregate":"mean","field":"s"}}""", "y", "zero", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative","bin":true},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", "y", "zero", 1)]
    [InlineData("\"area\"", """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g"}}""", "y", "zero", 1)]
    [InlineData("\"arc\"", """{"x":{"field":"t","type":"nominal"},"y":{"field":"v","type":"quantitative"}}""", "y", "zero", 0)]
    [InlineData("\"point\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":true},"color":{"field":"g"}}""", "y", "zero", 1)]
    [InlineData("\"line\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"normalize"},"color":{"field":"g"}}""", "y", "normalize", 1)]
    [InlineData("\"tick\"", """{"x":{"field":"v","type":"quantitative","stack":"center"},"y":{"field":"k","type":"nominal"},"detail":{"field":"g"}}""", "x", "center", 1)]
    [InlineData("\"rule\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"zero"}}""", "y", "zero", 0)]
    [InlineData("\"text\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":true},"text":{"field":"v"}}""", "y", "zero", 1)]
    [InlineData("""{"type":"bar","orient":"vertical"}""", """{"x":{"field":"a","type":"quantitative"},"y":{"field":"b","type":"quantitative"}}""", "y", "zero", 0)]
    [InlineData("""{"type":"bar","orient":"horizontal"}""", """{"x":{"field":"a","type":"quantitative"},"y":{"field":"b","type":"quantitative"}}""", "x", "zero", 0)]
    [InlineData("\"bar\"", """{"x":{"aggregate":"sum","field":"a","type":"quantitative"},"y":{"field":"b","type":"quantitative"},"color":{"field":"g"}}""", "x", "zero", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative"},"y":{"aggregate":"sum","field":"b","type":"quantitative"},"color":{"field":"g"}}""", "y", "zero", 1)]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative","stack":"center"},"y":{"field":"b","type":"quantitative"}}""", "x", "center", 0)]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative"},"y":{"field":"b","type":"quantitative","stack":"normalize"}}""", "y", "normalize", 0)]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative"},"y":{"field":"b","type":"nominal"}}""", "x", "zero", 0)]
    [InlineData("""{"type":"bar","orient":"vertical"}""", """{"y":{"field":"b","type":"quantitative"}}""", "y", "zero", 0)]
    [InlineData("""{"type":"bar","orient":"horizontal"}""", """{"x":{"field":"b","type":"quantitative"}}""", "x", "zero", 0)]
    public void StackRules_Decide_FindsTheStackedChannelOffsetAndStackByFields(string mark, string encoding, string channel, string offset, int stackBy)
    {
        // Arrange / Act
        var decision = Decide(mark, encoding);

        // Assert
        Assert.NotNull(decision);
        Assert.Equal(channel, decision.Channel);
        Assert.Equal(offset, decision.Offset);
        Assert.Equal(stackBy, decision.StackBy.Count);
    }

    /// <summary>
    ///     Validates the cases where there is no stack: explicit switches, marks that do not stack, aggregates without a layer to split,
    ///     ranges, and channels that cannot be stacked.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding.</param>
    [Theory]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":null},"color":{"field":"g"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":false},"color":{"field":"g"}}""")]
    [InlineData("\"area\"", """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative","stack":false},"color":{"field":"g"}}""")]
    [InlineData("\"point\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g"}}""")]
    [InlineData("\"line\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g"}}""")]
    [InlineData("\"tick\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g"}}""")]
    [InlineData("\"rect\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":true},"color":{"field":"g"}}""")]
    [InlineData("\"geoshape\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":true}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"},"color":{"field":"k","type":"nominal"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"},"tooltip":{"field":"g"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"sum","field":"v","type":"quantitative"},"color":{"aggregate":"mean","field":"g"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"aggregate":"count","type":"quantitative"},"color":{"value":"red"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"y2":{"field":"w"},"color":{"field":"g"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"quantitative"},"y2":{"datum":0},"color":{"field":"g"}}""")]
    [InlineData("\"bar\"", """{"y":{"field":"k","type":"nominal"},"x":{"field":"v","type":"quantitative"},"x2":{"aggregate":"max","field":"w"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"k","type":"nominal"},"y":{"field":"v","type":"nominal"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative","bin":true},"y":{"field":"b","type":"nominal"}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative","bin":{"maxbins":5}},"y":{"field":"b","type":"quantitative","bin":true}}""")]
    [InlineData("\"bar\"", """{"x":{"field":"a","type":"quantitative"},"y":{"field":"b","type":"quantitative"}}""")]
    [InlineData("""{"type":"bar","orient":"vertical"}""", """{"x":{"field":"a","type":"quantitative"}}""")]
    [InlineData("""{"type":"bar","orient":"horizontal"}""", """{"y":{"field":"a","type":"quantitative"}}""")]
    [InlineData("\"bar\"", """{"color":{"field":"g","type":"nominal"}}""")]
    [InlineData("\"bar\"", """{"x":{"value":3},"y":{"value":4}}""")]
    public void StackRules_Decide_ReturnsNullWhenNothingIsStacked(string mark, string encoding)
    {
        // Arrange / Act
        var decision = Decide(mark, encoding);

        // Assert
        Assert.Null(decision);
    }

    /// <summary>
    ///     Validates that an <c>area</c> stacks by default in both orientations: the quantitative channel is stacked ascending along x or descending along y,
    ///     exactly as for a bar, and an explicit <c>stack: false</c> switches it off.
    /// </summary>
    [Fact]
    public void StackRules_Decide_AreaStacksByDefaultInBothOrientations()
    {
        // Act
        var vertical = Decide("\"area\"", """{"x":{"field":"t","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");
        var horizontal = Decide("\"area\"", """{"y":{"field":"t","type":"ordinal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");
        var horizontalBar = Decide("\"bar\"", """{"y":{"field":"t","type":"ordinal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""");
        var off = Decide("\"area\"", """{"y":{"field":"t","type":"ordinal"},"x":{"field":"v","type":"quantitative","stack":false},"color":{"field":"g","type":"nominal"}}""");

        // Assert
        Assert.Equal(("y", "zero", 1), (vertical!.Channel, vertical.Offset, vertical.StackBy.Count));
        Assert.Equal(("x", "zero", 1), (horizontal!.Channel, horizontal.Offset, horizontal.StackBy.Count));
        Assert.Equal((horizontalBar!.Channel, horizontalBar.Offset), (horizontal.Channel, horizontal.Offset));
        Assert.Null(off);
    }

    /// <summary>
    ///     Validates that the stack-by definitions are the definitions of the non-position channels in channel order, array items included.
    /// </summary>
    [Fact]
    public void StackRules_Decide_ListsTheStackByDefinitionsInChannelOrder()
    {
        // Arrange
        var tree = SpecParser.Parse(
            """
            { "mark": "bar", "encoding": {
              "order": { "field": "o" },
              "detail": [ { "field": "d1" }, { "field": "d2" } ],
              "x": { "field": "k", "type": "nominal" },
              "y": { "field": "v", "type": "quantitative" },
              "color": { "field": "c", "type": "nominal" },
              "tooltip": { "field": "t" }
            } }
            """);

        // Act
        var decision = StackRules.Decide("bar", null, tree.Root["encoding"]);

        // Assert: color first (channel order of the rules), then the order channel, then the two detail definitions
        Assert.NotNull(decision);
        Assert.Equal(["c", "o", "d1", "d2"], decision.StackBy.Select(d => d["field"]!.StringValue));
    }

    /// <summary>
    ///     Validates the null-argument handling: a missing mark is rejected, a missing or non-object encoding is not a stack.
    /// </summary>
    [Fact]
    public void StackRules_Decide_HandlesMissingArguments()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\"}}}");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => StackRules.Decide(null!, null, tree.Root["encoding"]));
        Assert.Null(StackRules.Decide("bar", null, null));
        Assert.Null(StackRules.Decide("bar", null, tree.Root["mark"]));
    }

    /// <summary>
    ///     Validates the small predicates the encoding rules share: what a field definition is, its defaulted type, and what counts as
    ///     switched on.
    /// </summary>
    [Fact]
    public void StackRules_Helpers_ClassifyDefinitionsAndSwitches()
    {
        // Arrange
        var tree = SpecParser.Parse(
            """
            { "mark": "bar", "encoding": {
              "x": { "field": "a" },
              "y": { "aggregate": "count", "type": "ordinal" },
              "color": { "value": "red" },
              "size": { "aggregate": "sum" },
              "opacity": { "field": "a", "bin": false, "stack": null, "title": "t" }
            } }
            """);
        var encoding = tree.Root["encoding"]!;

        // Act / Assert: a field definition has a field or counts rows
        Assert.True(StackRules.IsFieldDef(encoding["x"]));
        Assert.True(StackRules.IsFieldDef(encoding["y"]));
        Assert.False(StackRules.IsFieldDef(encoding["color"]));
        Assert.False(StackRules.IsFieldDef(encoding["size"]));
        Assert.False(StackRules.IsFieldDef(null));

        // Assert: a missing type is quantitative
        Assert.Equal("quantitative", StackRules.TypeOf(encoding["x"]!));
        Assert.Equal("ordinal", StackRules.TypeOf(encoding["y"]!));

        // Assert: absent, null and false are off, everything else is on
        Assert.False(StackRules.IsEnabled(null));
        Assert.False(StackRules.IsEnabled(encoding["opacity"]!["bin"]));
        Assert.False(StackRules.IsEnabled(encoding["opacity"]!["stack"]));
        Assert.True(StackRules.IsEnabled(encoding["opacity"]!["title"]));
        Assert.True(StackRules.IsEnabled(encoding["x"]));
    }
}
