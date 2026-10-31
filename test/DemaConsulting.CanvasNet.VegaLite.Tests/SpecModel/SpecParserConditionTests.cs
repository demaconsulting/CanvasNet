using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the validation of conditional encodings in <see cref="SpecParser"/>: the shape of a condition, the compilation of its
///     test (an expression text, or a predicate object written as an expression), and the located errors.
/// </summary>
public class SpecParserConditionTests
{
    /// <summary>
    ///     Builds a specification with a point mark whose color channel is the given definition.
    /// </summary>
    /// <param name="color">The JSON text of the color definition.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string color) =>
        """{ "params": [ { "name": "t", "value": 1 }, { "name": "sel", "select": "interval" } ], "data": { "values": [ { "q": 1, "c": "a" } ] }, "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": """
        + color + " } }";

    /// <summary>
    ///     Validates that a malformed condition is a specification error at the offending value.
    /// </summary>
    [Theory]
    [InlineData("""{ "condition": 3 }""", "/encoding/color/condition")]
    [InlineData("""{ "condition": [ 3 ] }""", "/encoding/color/condition/0")]
    [InlineData("""{ "condition": { "value": "red" } }""", "/encoding/color/condition")]
    [InlineData("""{ "condition": [ { "test": "datum.q > 1", "value": "red" }, { "value": "blue" } ] }""", "/encoding/color/condition/1")]
    [InlineData("""{ "condition": { "test": "datum.q >", "value": "red" } }""", "/encoding/color/condition/test")]
    [InlineData("""{ "condition": { "test": "datum.q > missing", "value": "red" } }""", "/encoding/color/condition/test")]
    [InlineData("""{ "condition": { "test": 5, "value": "red" } }""", "/encoding/color/condition/test")]
    [InlineData("""{ "condition": { "test": { "and": 3 }, "value": "red" } }""", "/encoding/color/condition/test/and")]
    [InlineData("""{ "condition": { "test": "datum.q > 1", "value": { "expr": "(" } } }""", "/encoding/color/condition/value/expr")]
    [InlineData("""{ "condition": { "test": "datum.q > 1", "value": { "expr": "nothing" } } }""", "/encoding/color/condition/value/expr")]
    public void SpecParser_Parse_MalformedConditionReportsThePointer(string color, string pointer)
    {
        // Arrange and Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec(color), "c.json"));

        // Assert: the error is located in the file at the offending value
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Equal("c.json", ex.Location.File);
        Assert.True(ex.Location.Line >= 1 && ex.Location.Column >= 1);
    }

    /// <summary>
    ///     Validates that the test of a condition on a drawn channel is compiled once: an expression text as written, a predicate object as the
    ///     expression that means the same, and that the value expression of a condition is compiled too.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ConditionTestsAreCompiled()
    {
        // Arrange
        const string color = """
            { "condition": [
                { "test": "datum.q > t", "value": "red" },
                { "test": { "field": "q", "range": [3, 2] }, "value": { "expr": "'blue'" } },
                { "test": { "param": "t" }, "value": "green" } ], "value": "gray" }
            """;

        // Act
        var tree = SpecParser.Parse(Spec(color));
        var conditions = tree.Root["encoding"]!["color"]!["condition"]!.Items;

        // Assert: the text is kept as written, the predicates are written as expressions, the value expression is compiled
        Assert.Equal("datum.q > t", tree.Expressions.Get(conditions[0]["test"])!.Source);
        Assert.Equal("""(datum["q"] >= 2 && datum["q"] <= 3)""", tree.Expressions.Get(conditions[1]["test"])!.Source);
        Assert.Equal("'blue'", tree.Expressions.GetReference(conditions[1]["value"])!.Source);
        Assert.Equal("(t)", tree.Expressions.Get(conditions[2]["test"])!.Source);
    }

    /// <summary>
    ///     Validates that the conditions that are never evaluated are not compiled: a condition on a parameter, a predicate that no expression
    ///     can say, a channel that takes no condition, and a view that is not drawn.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ConditionsThatAreNotEvaluatedAreNotCompiled()
    {
        // Arrange
        const string json = """
            { "params": [ { "name": "sel", "select": "interval" } ],
              "data": { "values": [ { "q": 1 } ] },
              "layer": [
                { "mark": "point", "encoding": {
                    "x": { "field": "q", "type": "quantitative", "condition": { "test": { "field": "q", "gt": 1 }, "value": 3 } },
                    "color": { "condition": [ { "param": "sel", "value": "red" }, { "test": { "param": "sel" }, "value": "blue" } ] } } },
                { "mark": "point", "encoding": { "color": { "condition": { "test": { "field": "q", "gt": 1 }, "value": "red" } } } } ] }
            """;

        // Act
        var tree = SpecParser.Parse(json);
        var first = tree.Root["layer"]!.Items[0]["encoding"]!;
        var second = tree.Root["layer"]!.Items[1]["encoding"]!;
        var conditions = first["color"]!["condition"]!.Items;

        // Assert: the predicate objects of the position channel, of the selection and of the view that is not drawn are not written as expressions
        Assert.Null(tree.Expressions.Get(first["x"]!["condition"]!["test"]));
        Assert.Null(tree.Expressions.Get(conditions[1]["test"]));
        Assert.Null(tree.Expressions.Get(second["color"]!["condition"]!["test"]));
    }

    /// <summary>
    ///     Validates that a field condition is checked for the properties of a field definition, including the scale options.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_FieldConditionScaleIsValidated()
    {
        // Arrange
        const string color = """{ "condition": { "test": "datum.q > 1", "field": "c", "type": "nominal", "scale": { "padding": "wide" } }, "value": "gray" }""";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec(color), "c.json"));

        // Assert
        Assert.StartsWith("/encoding/color/condition/scale", ex.Location.Pointer, StringComparison.Ordinal);
    }
}
