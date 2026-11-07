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
    ///     Validates which conditions are compiled: a predicate object on a channel that takes no condition is not, those on a channel that does are, and a condition on a selection parameter is written
    ///     as the expression that Vega-Lite compiles for it (with or without <c>empty: false</c>).
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ConditionsThatAreNotEvaluatedAreNotCompiled()
    {
        // Arrange
        const string json = """
            { "params": [ { "name": "sel", "select": "interval" }, { "name": "flag", "value": true } ],
              "data": { "values": [ { "q": 1 } ] },
              "hconcat": [
                { "mark": "point", "encoding": {
                    "x": { "field": "q", "type": "quantitative", "condition": { "test": { "field": "q", "gt": 1 }, "value": 3 } },
                    "color": { "condition": [ { "param": "sel", "value": "red" }, { "test": { "param": "sel" }, "value": "blue" }, { "param": "sel", "empty": false, "value": "green" },
                                              { "param": "flag", "value": "gray" } ] } } },
                { "mark": "point", "encoding": { "color": { "condition": { "test": { "field": "q", "gt": 1 }, "value": "red" } } } } ] }
            """;

        // Act
        var tree = SpecParser.Parse(json);
        var first = tree.Root["hconcat"]!.Items[0]["encoding"]!;
        var second = tree.Root["hconcat"]!.Items[1]["encoding"]!;
        var conditions = first["color"]!["condition"]!.Items;

        // Assert: the predicate object of the position channel is not written as an expression; those of a view in a concatenation are, like any view's
        Assert.Null(tree.Expressions.Get(first["x"]!["condition"]!["test"]));
        Assert.NotNull(tree.Expressions.Get(second["color"]!["condition"]!["test"]));

        // Assert: a selection is the text of Vega-Lite (a selection with the default rule is empty or contains the datum, with empty: false it is not empty and contains it), a variable parameter is its truthiness
        const string Test = "vlSelectionTest(\"sel_store\", datum)";
        Assert.Equal("(!length(data(\"sel_store\")) || " + Test + ")", tree.Expressions.Get(conditions[0]["param"])!.Source);
        Assert.Equal("(!length(data(\"sel_store\")) || " + Test + ")", tree.Expressions.Get(conditions[1]["test"])!.Source);
        Assert.Equal("(length(data(\"sel_store\")) && " + Test + ")", tree.Expressions.Get(conditions[2]["param"])!.Source);
        Assert.Equal("(flag)", tree.Expressions.Get(conditions[3]["param"])!.Source);
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

    /// <summary>
    ///     Parses a specification whose only transform is a filter and returns the exception that refuses it.
    /// </summary>
    /// <param name="filter">The JSON text of the filter.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteSpecException FilterFails(string filter, VegaLiteLimits? limits = null) =>
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"mark\": \"point\", \"transform\": [ { \"filter\": " + filter + " } ] }", "c.json", limits));

    /// <summary>
    ///     Gets the description of an expression error without the location that the message starts with.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>The text from <c>Invalid expression:</c> on.</returns>
    private static string Description(VegaLiteSpecException ex) => ex.Message[ex.Message.IndexOf("Invalid expression:", StringComparison.Ordinal)..];

    /// <summary>
    ///     Validates that an expression that the user writes as the operand of a predicate (<c>{ "expr": ... }</c>) is bound by <c>MaxExpressionLength</c> like any expression that the user
    ///     writes: a 5,000 character one is refused as the comparison, as an item of a membership list, as an end of a range, in a time-aware filter, inside a composition and as the test of a
    ///     condition, with the message of a top-level expression of the same length and located at the <c>expr</c> node; one of 4,096 characters is accepted, the limit that is set applies, and
    ///     an expression that refers to a parameter that is not declared is refused at the same node.
    /// </summary>
    [Fact]
    public void SpecParser_PredicateExpressionOperands_AreBoundByTheUserExpressionLength()
    {
        // Arrange: 5,000 characters of a valid expression (a one and blanks)
        var tooLong = "1".PadRight(5_000);
        var embedded = "{ \"expr\": \"" + tooLong + "\" }";
        var reference = FilterFails("\"" + tooLong + "\"");
        Assert.Contains("the expression has 5000 characters, more than the limit of 4096 (MaxExpressionLength) at expression offset 4096.", reference.Message, StringComparison.Ordinal);

        // Act / Assert: each way of embedding the expression is refused with the same message, at the expr node
        foreach (var (filter, pointer) in new[]
        {
            ("{ \"field\": \"q\", \"equal\": " + embedded + " }", "/transform/0/filter/equal/expr"),
            ("{ \"field\": \"q\", \"gt\": " + embedded + " }", "/transform/0/filter/gt/expr"),
            ("{ \"field\": \"q\", \"oneOf\": [ 1, " + embedded + " ] }", "/transform/0/filter/oneOf/1/expr"),
            ("{ \"field\": \"q\", \"range\": [ " + embedded + ", 3 ] }", "/transform/0/filter/range/0/expr"),
            ("{ \"field\": \"q\", \"range\": [ 1, " + embedded + " ] }", "/transform/0/filter/range/1/expr"),
            ("{ \"field\": \"d\", \"timeUnit\": \"month\", \"equal\": " + embedded + " }", "/transform/0/filter/equal/expr"),
            ("{ \"field\": \"d\", \"timeUnit\": \"month\", \"range\": [ " + embedded + ", 3 ] }", "/transform/0/filter/range/0/expr"),
            ("{ \"and\": [ { \"field\": \"q\", \"gt\": 1 }, { \"field\": \"q\", \"lte\": " + embedded + " } ] }", "/transform/0/filter/and/1/lte/expr"),
        })
        {
            var ex = FilterFails(filter);
            Assert.Equal(pointer, ex.Location.Pointer);
            Assert.Equal(Description(reference), Description(ex));
        }

        // Act / Assert: the test of a condition is checked the same way
        var condition = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(
            Spec("{ \"condition\": { \"test\": { \"field\": \"q\", \"oneOf\": [ " + embedded + " ] }, \"value\": \"red\" }, \"value\": \"gray\" }"),
            "c.json"));
        Assert.Equal("/encoding/color/condition/test/oneOf/0/expr", condition.Location.Pointer);
        Assert.Equal(Description(reference), Description(condition));

        // Act / Assert: an expression of exactly 4,096 characters is accepted, and the limit that is set applies to the embedded expression
        var atLimit = "{ \"field\": \"q\", \"equal\": { \"expr\": \"" + "1".PadRight(4_096) + "\" } }";
        Assert.NotNull(SpecParser.Parse("{ \"mark\": \"point\", \"transform\": [ { \"filter\": " + atLimit + " } ] }", "c.json").Root);
        var small = FilterFails("{ \"field\": \"q\", \"equal\": { \"expr\": \"" + "1".PadRight(41) + "\" } }", new VegaLiteLimits { MaxExpressionLength = 40 });
        Assert.Equal("/transform/0/filter/equal/expr", small.Location.Pointer);
        Assert.Contains("the limit of 40 (MaxExpressionLength)", small.Message, StringComparison.Ordinal);

        // Act / Assert: an undeclared parameter in the embedded expression is refused at the expr node
        var undeclared = FilterFails("{ \"field\": \"q\", \"equal\": { \"expr\": \"nope + 1\" } }");
        Assert.Equal("/transform/0/filter/equal/expr", undeclared.Location.Pointer);
        Assert.Contains("'nope'", undeclared.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a field name that is long, with a large membership list, fails fast with the error of a predicate that is longer than the generated budget (eight times
    ///     <c>MaxSourceLength</c>), located at the test, and that the text is not allocated first: a 100,000 character field name with 100 members is 10 million characters as an
    ///     expression, over the budget of 2.4 million that a <c>MaxSourceLength</c> of 300,000 allows, and parsing it allocates a small fraction of that text.
    /// </summary>
    [Fact]
    public void SpecParser_Conditions_LongFieldTimesLargeOneOfFailsFastWithTheGeneratedBudgetError()
    {
        // Arrange: the specification (about 100,500 characters) is within the limit of 300,000
        var field = new string('f', 100_000);
        var json = Spec("{ \"condition\": { \"test\": { \"field\": \"" + field + "\", \"oneOf\": [ " + string.Join(", ", Enumerable.Repeat("1", 100)) + " ] }, \"value\": \"red\" }, \"value\": \"gray\" }");
        var limits = new VegaLiteLimits { MaxSourceLength = 300_000 };
        var expectedLength = 100 * (100_000 + 9 + 5 + 1) + (99 * 4) + 2;

        // Act: the allocations of this thread while the specification is parsed
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json, "c.json", limits));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: the error of an expression that is over the generated budget, at the test, with the exact length of the text that would have been written
        Assert.Equal("/encoding/color/condition/test", ex.Location.Pointer);
        Assert.Equal(
            $"Invalid expression: the expression that is written from the specification has {expectedLength} characters, more than 8 times the limit of 300000 (MaxSourceLength) at expression offset 2400000.",
            Description(ex));

        // Assert: the text (10 million characters, 20 MB, plus the copy of the builder) is never allocated; the document, its tree and the field text are about 2 MB
        Assert.True(allocated < 16_000_000, $"{allocated} bytes were allocated.");
    }

    /// <summary>
    ///     Validates that the text that is written for a predicate on a selection is bound by the generated budget like the text of any other predicate: a legacy <c>selection</c>
    ///     that is an <c>or</c> of 2,000 names is about 11 times the length of the document as an expression, so with the budget of eight times <c>MaxSourceLength</c> that is the
    ///     length of the document it is refused with the error of a generated expression, located at the predicate, as the test of a condition, as a condition on a legacy selection and
    ///     as a filter; the same predicates with the default limits are compiled, and a shorter one (a budget that is just enough) is accepted.
    /// </summary>
    [Fact]
    public void SpecParser_Conditions_SelectionPredicatesObeyTheGeneratedBudget()
    {
        // Arrange: the same selection name 2,000 times as a legacy composition, each name is written as about 63 characters of expression
        var names = string.Join(", ", Enumerable.Repeat("\"sel\"", 2000));
        var composition = "{ \"selection\": { \"or\": [ " + names + " ] } }";
        var sites = new[]
        {
            (Spec("{ \"condition\": { \"test\": " + composition + ", \"value\": \"red\" }, \"value\": \"gray\" }"), "/encoding/color/condition/test"),
            (Spec("{ \"condition\": { \"selection\": { \"or\": [ " + names + " ] }, \"value\": \"red\" }, \"value\": \"gray\" }"), "/encoding/color/condition"),
            (Spec("{ \"condition\": { \"test\": { \"and\": [ { \"field\": \"q\", \"gt\": 0 }, " + composition + " ] }, \"value\": \"red\" }, \"value\": \"gray\" }"), "/encoding/color/condition/test"),
            ("{ \"params\": [ { \"name\": \"sel\", \"select\": \"interval\" } ], \"mark\": \"point\", \"transform\": [ { \"filter\": " + composition + " } ] }", "/transform/0/filter"),
        };

        foreach (var (json, pointer) in sites)
        {
            // Act: the document is the limit of the source, so the budget is eight times its length
            var limits = new VegaLiteLimits { MaxSourceLength = json.Length };
            var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json, "c.json", limits));

            // Assert: the error of a generated expression that is over the budget, at the predicate
            Assert.StartsWith(pointer, ex.Location.Pointer, StringComparison.Ordinal);
            Assert.Contains($"more than 8 times the limit of {json.Length} (MaxSourceLength) at expression offset {8L * json.Length}.", Description(ex), StringComparison.Ordinal);

            // Act / Assert: with the default limits the document is accepted
            Assert.NotNull(SpecParser.Parse(json, "c.json").Root);
        }
    }
}
