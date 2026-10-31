using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of <see cref="ChannelDefinitions"/>: which channels take a condition, and which object of a conditional channel definition
///     carries its field and its values.
/// </summary>
public class ChannelDefinitionsTests
{
    /// <summary>
    ///     Parses a specification with one color channel and returns its definition.
    /// </summary>
    /// <param name="color">The JSON text of the color definition.</param>
    /// <returns>The definition node.</returns>
    private static SpecNode Color(string color) =>
        SpecParser.Parse("""{ "data": { "values": [ { "q": 1, "c": "a" } ] }, "mark": "circle", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": """ + color + " } }")
            .Root["encoding"]!["color"]!;

    /// <summary>
    ///     Validates which channels accept a condition.
    /// </summary>
    [Theory]
    [InlineData("color", true)]
    [InlineData("fill", true)]
    [InlineData("stroke", true)]
    [InlineData("opacity", true)]
    [InlineData("fillOpacity", true)]
    [InlineData("strokeOpacity", true)]
    [InlineData("strokeWidth", true)]
    [InlineData("strokeDash", true)]
    [InlineData("size", true)]
    [InlineData("shape", true)]
    [InlineData("angle", true)]
    [InlineData("x", false)]
    [InlineData("y", false)]
    [InlineData("x2", false)]
    [InlineData("order", false)]
    [InlineData("tooltip", false)]
    public void ChannelDefinitions_CanCondition_ListsTheConditionalChannels(string channel, bool expected) =>
        Assert.Equal(expected, ChannelDefinitions.CanCondition(channel));

    /// <summary>
    ///     Validates the field definition of a channel: its own field, a field in the first condition on a test, or the definition itself.
    /// </summary>
    [Fact]
    public void ChannelDefinitions_FieldDefinitionOf_FindsTheObjectThatCarriesTheField()
    {
        // Arrange
        var own = Color("""{ "field": "c", "type": "nominal", "condition": { "test": "datum.q > 1", "value": "red" } }""");
        var conditional = Color("""{ "condition": [ { "test": "datum.q > 3", "value": "red" }, { "test": "datum.q > 1", "field": "c", "type": "nominal" } ], "value": "gray" }""");
        var onParameter = Color("""{ "condition": { "param": "p", "field": "c", "type": "nominal" }, "value": "gray" }""");
        var plain = Color("""{ "value": "gray" }""");
        var aggregate = Color("""{ "aggregate": "count", "type": "quantitative" }""");

        // Act and Assert
        Assert.Same(own, ChannelDefinitions.FieldDefinitionOf(own));
        Assert.Same(ChannelDefinitions.Conditions(conditional).ElementAt(1), ChannelDefinitions.FieldDefinitionOf(conditional));
        Assert.Same(onParameter, ChannelDefinitions.FieldDefinitionOf(onParameter));
        Assert.Same(plain, ChannelDefinitions.FieldDefinitionOf(plain));
        Assert.True(ChannelDefinitions.HasField(aggregate));
        Assert.False(ChannelDefinitions.HasField(plain));
    }

    /// <summary>
    ///     Validates the lists of conditions: a single condition, an array, and the exclusion of a parameter condition from the tests.
    /// </summary>
    [Fact]
    public void ChannelDefinitions_Conditions_ListsConditionsInOrder()
    {
        // Arrange
        var single = Color("""{ "condition": { "test": "datum.q > 1", "value": "red" } }""");
        var array = Color("""{ "condition": [ { "param": "p", "value": "red" }, { "test": "datum.q > 1", "value": "blue" } ] }""");
        var none = Color("""{ "value": "gray" }""");

        // Act and Assert
        Assert.Single(ChannelDefinitions.Conditions(single));
        Assert.Equal(2, ChannelDefinitions.Conditions(array).Count());
        Assert.Single(ChannelDefinitions.TestConditions(array));
        Assert.Equal("blue", ChannelDefinitions.TestConditions(array).Single()["value"]!.StringValue);
        Assert.Empty(ChannelDefinitions.Conditions(none));
    }

    /// <summary>
    ///     Validates the constant values a legend shows for a channel that no field drives: the conditions first, then the value of the definition.
    /// </summary>
    [Fact]
    public void ChannelDefinitions_Values_ListsTheConstantsOfAChannelThatNoFieldDrives()
    {
        // Arrange
        var conditional = Color("""{ "condition": [ { "test": "datum.q > 3", "value": "red" }, { "test": "datum.q > 1", "value": "blue" } ], "value": "gray" }""");
        var driven = Color("""{ "field": "c", "type": "nominal", "condition": { "test": "datum.q > 3", "value": "red" } }""");
        var fieldCondition = Color("""{ "condition": { "test": "datum.q > 1", "field": "c", "type": "nominal" }, "value": "gray" }""");

        // Act and Assert
        Assert.Equal(["red", "blue", "gray"], ChannelDefinitions.Values(conditional).Select(v => v.StringValue));
        Assert.Equal("red", ChannelDefinitions.FirstValue(conditional)!.StringValue);
        Assert.Empty(ChannelDefinitions.Values(driven));
        Assert.Null(ChannelDefinitions.FirstValue(driven));
        Assert.Empty(ChannelDefinitions.Values(fieldCondition));
        Assert.Empty(ChannelDefinitions.Values(null));
    }
}
