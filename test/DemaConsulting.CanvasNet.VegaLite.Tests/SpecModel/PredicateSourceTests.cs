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

    /// <summary>
    ///     Builds a membership predicate on a field name of 100,000 characters with 5,000 members, which is 500 million characters as an expression (the field is written once per member).
    /// </summary>
    /// <param name="extra">The JSON text of a part to add as the second part of an <c>and</c>, or null for the membership predicate alone.</param>
    /// <returns>The predicate node.</returns>
    private static SpecNode HugePredicate(string? extra = null)
    {
        var membership = "{ \"field\": \"" + new string('f', 100_000) + "\", \"oneOf\": [ " + string.Join(", ", Enumerable.Repeat("1", 5_000)) + " ] }";
        return Predicate(extra is null ? membership : "{ \"and\": [ " + membership + ", " + extra + " ] }");
    }

    /// <summary>
    ///     Validates that a predicate whose text is over the budget is refused while it is written, before the text is allocated: a 100,000 character field name with 5,000 members is
    ///     500,094,998 characters (the field is written once per member, <c>datum["..."] === 1</c> and a separator for each), and with a budget of one million it is reported as too long
    ///     with the exact length, no text, and without allocating more than a small fraction of it (the text would be a gigabyte).
    /// </summary>
    [Fact]
    public void PredicateSource_Build_RefusesAnOverBudgetExpansionBeforeAllocatingIt()
    {
        // Arrange
        var node = HugePredicate();

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var status = PredicateSource.Build(node, 1_000_000, out var source, out var length);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: too long, with the exact length of the text that would have been written, and nothing kept
        Assert.Equal(PredicateSource.Status.TooLong, status);
        Assert.Equal(string.Empty, source);
        Assert.Equal((5_000L * (100_009 + 5 + 1)) + (4_999 * 4) + 2, length);
        Assert.True(allocated < 16_000_000, $"{allocated} bytes were allocated.");
    }

    /// <summary>
    ///     Validates the boundary of the budget: a text of exactly the budget is written, one character less is too long, and a part that cannot be written wins over a text that is too long
    ///     (so that a predicate that is never compiled is not reported as too long).
    /// </summary>
    [Fact]
    public void PredicateSource_Build_BudgetBoundaryAndUnsupportedWins()
    {
        // Arrange
        var node = Predicate("""{ "field": "c", "oneOf": [ 1, 2 ] }""");
        const string Expected = """(datum["c"] === 1 || datum["c"] === 2)""";

        // Act
        var exact = PredicateSource.Build(node, Expected.Length, out var source, out var exactLength);
        var oneLess = PredicateSource.Build(node, Expected.Length - 1, out var refused, out var refusedLength);
        var unsupported = PredicateSource.Build(HugePredicate("""{ "param": "sel" }"""), 1_000, out var none, out _);

        // Assert
        Assert.Equal(PredicateSource.Status.Written, exact);
        Assert.Equal(Expected, source);
        Assert.Equal(Expected.Length, exactLength);
        Assert.Equal(PredicateSource.Status.TooLong, oneLess);
        Assert.Equal(string.Empty, refused);
        Assert.Equal(Expected.Length, refusedLength);
        Assert.Equal(PredicateSource.Status.Unsupported, unsupported);
        Assert.Equal(string.Empty, none);
    }

    /// <summary>
    ///     Validates that asking whether a predicate is unsupported does not write its text: the huge predicate is answered (supported, and with a part that cannot be written) while
    ///     allocating a few megabytes at most, where the text would be a gigabyte.
    /// </summary>
    [Fact]
    public void PredicateSource_IsUnsupported_DoesNotAllocateTheExpansion()
    {
        // Arrange
        var supported = HugePredicate();
        var unsupported = HugePredicate("""{ "param": "sel" }""");

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var supportedAnswer = PredicateSource.IsUnsupported(supported);
        var unsupportedAnswer = PredicateSource.IsUnsupported(unsupported);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.False(supportedAnswer);
        Assert.True(unsupportedAnswer);
        Assert.True(allocated < 16_000_000, $"{allocated} bytes were allocated.");
    }
}
