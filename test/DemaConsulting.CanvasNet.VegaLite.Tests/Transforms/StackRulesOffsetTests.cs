using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Tests of the offset channels in <see cref="StackRules"/> and <see cref="EncodingPlan"/>: the offset field of the dimension splits the groups of a stack (Vega-Lite's <c>stack</c>), so a field that is
///     both the offset and the color is not a layer, and the aggregate groups by it.
/// </summary>
public class StackRulesOffsetTests
{
    /// <summary>The start of a specification of a grouped bar chart over rows with a category, a group, a segment and a value.</summary>
    private const string Head = """{ "data": { "values": [ { "c": "A", "g": "x", "s": "p", "v": 1 } ] }, "mark": "bar", "encoding": """;

    /// <summary>
    ///     Parses the encoding as the encoding of a bar chart.
    /// </summary>
    /// <param name="encoding">The encoding JSON.</param>
    /// <returns>The parsed specification.</returns>
    private static SpecTree Parse(string encoding) => SpecParser.Parse(Head + encoding + " }");

    /// <summary>
    ///     Validates that the offset field is a group-by of the stack and not a stack-by: the color of the same field does not make layers.
    /// </summary>
    [Fact]
    public void StackRules_Decide_OffsetFieldSplitsTheGroupsAndIsNotAStackBy()
    {
        // Arrange
        var same = Parse("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }""");
        var other = Parse("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "s", "type": "nominal" } }""");

        // Act
        var sameDecision = StackRules.Decide("bar", null, same.Root["encoding"]);
        var otherDecision = StackRules.Decide("bar", null, other.Root["encoding"]);

        // Assert: the color that is the offset field splits nothing; a different field is the layer of the stack
        Assert.NotNull(sameDecision);
        Assert.Empty(sameDecision.StackBy);
        Assert.Equal("y", sameDecision.Channel);
        Assert.Single(otherDecision!.StackBy);
    }

    /// <summary>
    ///     Validates that an aggregated grouped bar chart has nothing to stack: the color is the offset field, so the stack-by is empty and the aggregate has one row for each group.
    /// </summary>
    [Fact]
    public void StackRules_Decide_AggregatedGroupedBarsAreNotStacked()
    {
        // Arrange
        var tree = Parse("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "aggregate": "mean", "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }""");

        // Act / Assert
        Assert.Null(StackRules.Decide("bar", null, tree.Root["encoding"]));
    }

    /// <summary>
    ///     Validates that a quantitative offset on a view without a dimension is a strip of points, not stack groups: the default stack of a bar is off, and an explicit stack is kept.
    /// </summary>
    [Fact]
    public void StackRules_Decide_QuantitativeOffsetWithoutADimensionDisablesTheDefaultStack()
    {
        // Arrange
        var plain = Parse("""{ "y": { "field": "v", "type": "quantitative" } }""");
        var offset = Parse("""{ "y": { "field": "v", "type": "quantitative" }, "xOffset": { "field": "v", "type": "quantitative" } }""");
        var explicitStack = Parse("""{ "y": { "field": "v", "type": "quantitative", "stack": true }, "xOffset": { "field": "v", "type": "quantitative" } }""");

        // Act / Assert
        Assert.NotNull(StackRules.Decide("bar", null, plain.Root["encoding"]));
        Assert.Null(StackRules.Decide("bar", null, offset.Root["encoding"]));
        Assert.NotNull(StackRules.Decide("bar", null, explicitStack.Root["encoding"]));
    }

    /// <summary>
    ///     Validates the offset channel of the dimension.
    /// </summary>
    /// <param name="channel">The stacked channel.</param>
    /// <param name="expected">The offset channel, or null.</param>
    [Theory]
    [InlineData("x", "yOffset")]
    [InlineData("y", "xOffset")]
    [InlineData("theta", null)]
    [InlineData("radius", null)]
    public void StackRules_DimensionOffsetChannel_NamesTheOffsetOfTheDimension(string channel, string? expected)
    {
        // Act / Assert
        Assert.Equal(expected, StackRules.DimensionOffsetChannel(channel));
        Assert.Throws<ArgumentNullException>(() => StackRules.DimensionOffsetChannel(null!));
    }

    /// <summary>
    ///     Validates that the plan of the stack groups by the field of the offset, and the plan of the aggregate does too.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_StackAndAggregateGroupByTheOffsetField()
    {
        // Arrange
        var stacked = Parse("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "s", "type": "nominal" } }""");
        var aggregated = Parse("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "aggregate": "sum", "field": "v", "type": "quantitative" } }""");

        // Act
        var stackPlan = EncodingPlan.Create(stacked.Root, ConfigResolver.Resolve(stacked, null));
        var aggregatePlan = EncodingPlan.Create(aggregated.Root, ConfigResolver.Resolve(aggregated, null));

        // Assert
        Assert.Equal(["c", "g"], stackPlan.Stack!.GroupBy);
        Assert.Equal(["c", "g"], aggregatePlan.Aggregate!.GroupBy);
        Assert.Null(aggregatePlan.Stack);
    }
}
