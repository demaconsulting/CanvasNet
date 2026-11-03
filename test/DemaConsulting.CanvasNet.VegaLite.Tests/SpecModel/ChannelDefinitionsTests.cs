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

    /// <summary>
    ///     Parses a specification with one encoding channel and returns its definition.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="definition">The JSON text of the channel definition.</param>
    /// <returns>The encoding object and the definition node.</returns>
    private static (SpecNode Encoding, SpecNode Definition) Channel(string channel, string definition)
    {
        var encoding = SpecParser.Parse(
            """{ "data": { "values": [ { "q": 1, "c": "a" } ] }, "mark": "point", "encoding": { """
            + $"\"{channel}\": {definition} }} }}").Root["encoding"]!;
        return (encoding, encoding[channel]!);
    }

    /// <summary>
    ///     Validates the type of a field definition without a <c>type</c>, rule by rule as Vega-Lite's <c>defaultType</c> gives it: the channel rules first (<c>shape</c> and
    ///     <c>strokeDash</c> are nominal, <c>order</c> ordinal), then a <c>sort</c> array (ordinal), a <c>timeUnit</c> (temporal), a <c>bin</c> or an <c>aggregate</c> other than
    ///     argmin and argmax (quantitative), the scale type, and otherwise nominal, on every channel (<c>x</c>, <c>y</c>, <c>color</c>, <c>text</c> and the others).
    /// </summary>
    [Theory]
    [InlineData("text", """{ "field": "c" }""", "nominal")]
    [InlineData("color", """{ "field": "c" }""", "nominal")]
    [InlineData("shape", """{ "field": "c" }""", "nominal")]
    [InlineData("strokeDash", """{ "field": "c", "timeUnit": "year" }""", "nominal")]
    [InlineData("x", """{ "field": "c" }""", "nominal")]
    [InlineData("y", """{ "field": "c" }""", "nominal")]
    [InlineData("size", """{ "field": "c" }""", "nominal")]
    [InlineData("theta", """{ "field": "c" }""", "nominal")]
    [InlineData("order", """{ "field": "c" }""", "ordinal")]
    [InlineData("order", """{ "aggregate": "count" }""", "ordinal")]
    [InlineData("x", """{ "field": "c", "sort": ["b", "a"] }""", "ordinal")]
    [InlineData("x", """{ "field": "c", "sort": ["b", "a"], "timeUnit": "year" }""", "ordinal")]
    [InlineData("x", """{ "field": "c", "sort": "descending" }""", "nominal")]
    [InlineData("x", """{ "field": "c", "timeUnit": "year" }""", "temporal")]
    [InlineData("color", """{ "field": "c", "timeUnit": "month" }""", "temporal")]
    [InlineData("x", """{ "field": "q", "bin": true }""", "quantitative")]
    [InlineData("color", """{ "field": "q", "bin": { "maxbins": 5 } }""", "quantitative")]
    [InlineData("x", """{ "field": "q", "bin": false }""", "nominal")]
    [InlineData("y", """{ "aggregate": "count" }""", "quantitative")]
    [InlineData("y", """{ "aggregate": "valid", "field": "c" }""", "quantitative")]
    [InlineData("y", """{ "aggregate": "missing", "field": "c" }""", "quantitative")]
    [InlineData("y", """{ "aggregate": "distinct", "field": "c" }""", "quantitative")]
    [InlineData("y", """{ "aggregate": "mean", "field": "q" }""", "quantitative")]
    [InlineData("text", """{ "aggregate": "sum", "field": "q" }""", "quantitative")]
    [InlineData("y", """{ "aggregate": { "argmax": "q" }, "field": "c" }""", "nominal")]
    [InlineData("x", """{ "field": "q", "scale": { "type": "log" } }""", "quantitative")]
    [InlineData("color", """{ "field": "q", "scale": { "type": "quantize" } }""", "quantitative")]
    [InlineData("x", """{ "field": "c", "scale": { "type": "time" } }""", "temporal")]
    [InlineData("x", """{ "field": "c", "scale": { "type": "utc" } }""", "temporal")]
    [InlineData("x", """{ "field": "c", "scale": { "type": "band" } }""", "nominal")]
    [InlineData("x", """{ "field": "c", "scale": { "type": "ordinal" } }""", "nominal")]
    [InlineData("x", """{ "field": "q", "type": "ordinal", "bin": true }""", "ordinal")]
    [InlineData("x", """{ "field": "c", "type": "quantitative" }""", "quantitative")]
    [InlineData("color", """{ "field": "c", "type": "temporal" }""", "temporal")]
    public void ChannelDefinitions_TypeOf_InfersTheMissingTypeLikeVegaLite(string channel, string definition, string expected)
    {
        // Arrange
        var (encoding, node) = Channel(channel, definition);

        // Act and Assert
        Assert.Equal(expected, ChannelDefinitions.TypeOf(node, channel, encoding));
    }

    /// <summary>
    ///     Validates the types of the rest: a datum takes the type of its value whatever the channel is, the position of a map (<c>latitude</c>, <c>longitude</c>) is quantitative,
    ///     and a secondary channel takes the type of its main channel (the written one, or the inferred one) and, when it has none to follow, is inferred as its main channel would be.
    /// </summary>
    [Fact]
    public void ChannelDefinitions_TypeOf_DatumLatitudeAndSecondaryChannels()
    {
        // Arrange
        var number = SpecParser.Parse("""{ "data": { "values": [] }, "mark": "point", "encoding": { "x": { "datum": 3 }, "y": { "datum": "label" }, "color": { "datum": true }, "shape": { "datum": 2 } } }""")
            .Root["encoding"]!;
        var dated = SpecParser.Parse("""{ "data": { "values": [] }, "mark": "bar", "encoding": { "x": { "field": "d", "timeUnit": "year" }, "x2": { "field": "e" }, "y": { "field": "q", "type": "quantitative" }, "y2": { "field": "r" } } }""")
            .Root["encoding"]!;
        var written = SpecParser.Parse("""{ "data": { "values": [] }, "mark": "bar", "encoding": { "x": { "field": "d", "type": "temporal" }, "x2": { "field": "e" }, "y": { "aggregate": "count" }, "y2": { "field": "r", "aggregate": "sum" } } }""")
            .Root["encoding"]!;
        var node = SpecParser.Parse("""{ "data": { "values": [] }, "mark": "bar", "encoding": { "x2": { "field": "e" } } }""").Root["encoding"]!;

        // Act and Assert: a datum is typed by its value
        Assert.Equal("quantitative", ChannelDefinitions.TypeOf(number["x"]!, "x"));
        Assert.Equal("nominal", ChannelDefinitions.TypeOf(number["y"]!, "y"));
        Assert.Equal("nominal", ChannelDefinitions.TypeOf(number["color"]!, "color"));
        Assert.Equal("quantitative", ChannelDefinitions.TypeOf(number["shape"]!, "shape"));

        // Assert: a map position is quantitative
        Assert.Equal("quantitative", ChannelDefinitions.TypeOf(number["x"]!, "latitude"));
        Assert.Equal("quantitative", ChannelDefinitions.TypeOf(dated["y2"]!, "longitude"));

        // Assert: a secondary channel takes the type of its main channel, written or inferred
        Assert.Equal("temporal", ChannelDefinitions.TypeOf(dated["x2"]!, "x2", dated));
        Assert.Equal("quantitative", ChannelDefinitions.TypeOf(dated["y2"]!, "y2", dated));
        Assert.Equal("temporal", ChannelDefinitions.TypeOf(written["x2"]!, "x2", written));
        Assert.Equal("quantitative", ChannelDefinitions.TypeOf(written["y2"]!, "y2", written));

        // Assert: without a main channel the secondary channel is inferred as its main channel is, and the names of the main channels are the Vega-Lite ones
        Assert.Equal("nominal", ChannelDefinitions.TypeOf(node["x2"]!, "x2", node));
        Assert.Equal(
            ["x", "y", "theta", "radius", "latitude", "longitude", null, null],
            new[] { "x2", "y2", "theta2", "radius2", "latitude2", "longitude2", "x", "xError" }.Select(ChannelDefinitions.MainChannelOf));
    }
}
