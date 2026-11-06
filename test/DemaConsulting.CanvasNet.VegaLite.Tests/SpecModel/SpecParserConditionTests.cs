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
    ///     Validates that a membership list and a composition of predicates are not bound by the expression length limit of user-written expressions, because the expression that
    ///     the renderer writes for them is not user-written: a <c>oneOf</c> of 1,000 numbers (21,891 characters as an expression) and of 1,000 strings, and an <c>or</c> of 1,000
    ///     comparisons compile with the default limits; the list is bound by <c>MaxScaleDomainSize</c> (the same entries limit as a time-aware list), and the written text by eight
    ///     times <c>MaxSourceLength</c>, so a hostile document cannot make the renderer parse text that is out of proportion to it.
    /// </summary>
    [Fact]
    public void SpecParser_Conditions_LargeOneOfAndOrPredicatesAreNotBoundByTheUserExpressionLength()
    {
        // Arrange: 1,000 numbers, 1,000 strings, and an or of 1,000 equal comparisons, each as the test of a condition
        var numbers = string.Join(", ", Enumerable.Range(1, 1000));
        var strings = string.Join(", ", Enumerable.Range(1, 1000).Select(i => "\"value" + i + "\""));
        var comparisons = string.Join(", ", Enumerable.Range(1, 1000).Select(i => "{ \"field\": \"q\", \"equal\": " + i + " }"));
        string Color(string test) => "{ \"condition\": { \"test\": " + test + ", \"value\": \"red\" }, \"value\": \"gray\" }";

        foreach (var test in new[]
        {
            "{ \"field\": \"q\", \"oneOf\": [ " + numbers + " ] }",
            "{ \"field\": \"c\", \"oneOf\": [ " + strings + " ] }",
            "{ \"or\": [ " + comparisons + " ] }",
        })
        {
            // Act
            var tree = SpecParser.Parse(Spec(Color(test)));
            var compiled = tree.Expressions.Get(tree.Root["encoding"]!["color"]!["condition"]!["test"]!);

            // Assert: the expression is compiled and longer than the limit of a user-written expression (4,096)
            Assert.NotNull(compiled);
            Assert.True(compiled.Source.Length > 4_096, $"{compiled.Source.Length} characters");
        }

        // Act / Assert: a list longer than MaxScaleDomainSize is refused at the list, one that is exactly as long as the limit is accepted
        var list = Color("{ \"field\": \"q\", \"oneOf\": [ " + numbers + " ] }");
        var tooMany = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec(list), "c.json", new VegaLiteLimits { MaxScaleDomainSize = 999 }));
        Assert.Equal("/encoding/color/condition/test/oneOf", tooMany.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", tooMany.Message, StringComparison.Ordinal);
        Assert.NotNull(SpecParser.Parse(Spec(list), "c.json", new VegaLiteLimits { MaxScaleDomainSize = 1000 }).Root);

        // Act / Assert: the written text is bound by eight times MaxSourceLength: 1,000 members of a 30 character field name are about 50,000 characters, and a document of about
        // 3,500 characters (the limit that is set) allows about 28,000
        var longField = new string('f', 30);
        var small = Spec(Color("{ \"field\": \"" + longField + "\", \"oneOf\": [ " + string.Join(", ", Enumerable.Repeat("1", 1000)) + " ] }"));
        var overBudget = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(small, "c.json", new VegaLiteLimits { MaxSourceLength = small.Length }));
        Assert.Equal("/encoding/color/condition/test", overBudget.Location.Pointer);
        Assert.Contains("MaxSourceLength", overBudget.Message, StringComparison.Ordinal);

        // Act / Assert: an expression that the user writes keeps the limit of 4,096 characters
        var written = Color("\"" + string.Join(" || ", Enumerable.Repeat("datum.q === 1", 400)) + "\"");
        var userLimit = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec(written), "c.json"));
        Assert.Equal("/encoding/color/condition/test", userLimit.Location.Pointer);
        Assert.Contains("MaxExpressionLength", userLimit.Message, StringComparison.Ordinal);
    }

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
    ///     can say, and a channel that takes no condition.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ConditionsThatAreNotEvaluatedAreNotCompiled()
    {
        // Arrange
        const string json = """
            { "params": [ { "name": "sel", "select": "interval" } ],
              "data": { "values": [ { "q": 1 } ] },
              "hconcat": [
                { "mark": "point", "encoding": {
                    "x": { "field": "q", "type": "quantitative", "condition": { "test": { "field": "q", "gt": 1 }, "value": 3 } },
                    "color": { "condition": [ { "param": "sel", "value": "red" }, { "test": { "param": "sel" }, "value": "blue" } ] } } },
                { "mark": "point", "encoding": { "color": { "condition": { "test": { "field": "q", "gt": 1 }, "value": "red" } } } } ] }
            """;

        // Act
        var tree = SpecParser.Parse(json);
        var first = tree.Root["hconcat"]!.Items[0]["encoding"]!;
        var second = tree.Root["hconcat"]!.Items[1]["encoding"]!;
        var conditions = first["color"]!["condition"]!.Items;

        // Assert: the predicate objects of the position channel and of the selection are not written as expressions; those of a view in a concatenation are, like any view's
        Assert.Null(tree.Expressions.Get(first["x"]!["condition"]!["test"]));
        Assert.Null(tree.Expressions.Get(conditions[1]["test"]));
        Assert.NotNull(tree.Expressions.Get(second["color"]!["condition"]!["test"]));
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
