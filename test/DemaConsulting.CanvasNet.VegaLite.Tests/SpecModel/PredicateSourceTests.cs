using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of <see cref="PredicateSource"/>: the expression text that each Vega-Lite predicate is turned into, written out by hand
///     from the meaning of the predicate.
/// </summary>
public class PredicateSourceTests
{
    /// <summary>
    ///     Parses a specification whose only transform is a filter on a predicate and returns the predicate node.
    /// </summary>
    /// <param name="predicate">The JSON text of the predicate.</param>
    /// <returns>The <c>filter</c> value in the parsed tree.</returns>
    private static SpecNode Predicate(string predicate)
    {
        var json = """{ "params": [ { "name": "threshold", "value": 1 }, { "name": "sel", "select": "interval" } ], "data": { "values": [ { "q": 1 } ] }, "transform": [ { "filter": """
            + predicate + """ } ], "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" } } }""";
        return SpecParser.Parse(json).Root["transform"]!.Items[0]["filter"]!;
    }

    /// <summary>
    ///     Validates the expression that each supported predicate is written as.
    /// </summary>
    [Theory]
    [InlineData("""{ "field": "q", "gt": 3 }""", """(datum["q"] > 3)""")]
    [InlineData("""{ "field": "q", "gte": 3 }""", """(datum["q"] >= 3)""")]
    [InlineData("""{ "field": "q", "lt": -2.5 }""", """(datum["q"] < -2.5)""")]
    [InlineData("""{ "field": "q", "lte": 0 }""", """(datum["q"] <= 0)""")]
    [InlineData("""{ "field": "c", "equal": "a" }""", """(datum["c"] === "a")""")]
    [InlineData("""{ "field": "c", "equal": true }""", """(datum["c"] === true)""")]
    [InlineData("""{ "field": "c", "equal": null }""", """(datum["c"] === null)""")]
    [InlineData("""{ "field": "q", "range": [2, 4] }""", """(datum["q"] >= 2 && datum["q"] <= 4)""")]
    [InlineData("""{ "field": "q", "range": [4, 2] }""", """(datum["q"] >= 2 && datum["q"] <= 4)""")]
    [InlineData("""{ "field": "q", "range": [null, 4] }""", """(datum["q"] <= 4)""")]
    [InlineData("""{ "field": "q", "range": [2, null] }""", """(datum["q"] >= 2)""")]
    [InlineData("""{ "field": "q", "range": [null, null] }""", "(true)")]
    [InlineData("""{ "field": "c", "oneOf": ["a", "b"] }""", """(datum["c"] === "a" || datum["c"] === "b")""")]
    [InlineData("""{ "field": "c", "oneOf": [] }""", "(false)")]
    [InlineData("""{ "field": "q", "valid": true }""", """isValid(datum["q"])""")]
    [InlineData("""{ "field": "q", "valid": false }""", """!isValid(datum["q"])""")]
    [InlineData("""{ "not": { "field": "q", "gt": 1 } }""", """!((datum["q"] > 1))""")]
    [InlineData("""{ "and": [ { "field": "q", "gt": 1 }, { "field": "c", "equal": "a" } ] }""", """((datum["q"] > 1) && (datum["c"] === "a"))""")]
    [InlineData("""{ "or": [ { "field": "q", "lt": 1 }, { "field": "q", "gt": 4 } ] }""", """((datum["q"] < 1) || (datum["q"] > 4))""")]
    [InlineData("""{ "and": [] }""", "(true)")]
    [InlineData("""{ "or": [] }""", "(false)")]
    [InlineData("\"datum.q > 1\"", "(datum.q > 1)")]
    [InlineData("""{ "param": "threshold" }""", "(threshold)")]
    public void PredicateSource_TryBuild_WritesTheExpressionThatMeansTheSame(string predicate, string expected)
    {
        // Arrange
        var node = Predicate(predicate);

        // Act
        var built = PredicateSource.TryBuild(node, out var source);

        // Assert
        Assert.True(built);
        Assert.Equal(expected, source);
        Assert.False(PredicateSource.IsUnsupported(node));
    }

    /// <summary>
    ///     Validates that a field path is written with one bracket per segment (an escaped dot stays in its segment) and that the text of
    ///     a value is quoted with the JavaScript escapes.
    /// </summary>
    [Fact]
    public void PredicateSource_TryBuild_QuotesPathsAndStrings()
    {
        // Arrange
        var path = Predicate("""{ "field": "a.b\\.c", "equal": "say \"hi\"\n\\" }""");

        // Act
        var built = PredicateSource.TryBuild(path, out var source);

        // Assert: the field is a then "b.c"; the value has its quote, newline and backslash escaped
        Assert.True(built);
        Assert.Equal("""(datum["a"]["b.c"] === "say \"hi\"\n\\")""", source);
    }

    /// <summary>
    ///     Validates the predicates that no expression can say: they are not written and the whole predicate is unsupported.
    /// </summary>
    [Theory]
    [InlineData("""{ "param": "sel" }""")]
    [InlineData("""{ "and": [ { "field": "q", "gt": 1 }, { "param": "sel" } ] }""")]
    [InlineData("""{ "not": { "param": "sel" } }""")]
    public void PredicateSource_TryBuild_RefusesWhatNoExpressionCanSay(string predicate)
    {
        // Arrange
        var node = Predicate(predicate);

        // Act
        var built = PredicateSource.TryBuild(node, out var source);

        // Assert
        Assert.False(built);
        Assert.Equal(string.Empty, source);
        Assert.True(PredicateSource.IsUnsupported(node));
    }

    /// <summary>
    ///     Validates the argument check.
    /// </summary>
    [Fact]
    public void PredicateSource_TryBuild_NullPredicateThrows() =>
        Assert.Throws<ArgumentNullException>(() => PredicateSource.TryBuild(null!, out _));
}
