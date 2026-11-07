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
        var json = """{ "params": [ { "name": "threshold", "value": 1 }, { "name": "bad name", "value": 1 }, { "name": "sel", "select": "interval" }, { "name": "ptid", "select": "point" }, """
            + """{ "name": "multi", "select": { "type": "interval", "resolve": "union" } }, { "name": "my-sel", "select": { "type": "point", "fields": ["q"] } } ], """
            + """ "selection": { "leg": { "type": "single", "empty": "none" } }, "data": { "values": [ { "q": 1 } ] }, "transform": [ { "filter": """
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
    ///     Validates the text that Vega-Lite compiles for a predicate on a selection: empty or in the selection, or (with <c>empty: false</c>) not empty and in it; a point selection
    ///     over the identity of the rows uses the identity test; a resolve other than <c>global</c> is the third argument; the store is named with the variable form of the name; the
    ///     legacy <c>selection</c> property names a selection too (a legacy <c>empty: "none"</c> is <c>empty: false</c>).
    /// </summary>
    [Theory]
    [InlineData("""{ "param": "sel" }""", """(!length(data("sel_store")) || vlSelectionTest("sel_store", datum))""")]
    [InlineData("""{ "param": "sel", "empty": true }""", """(!length(data("sel_store")) || vlSelectionTest("sel_store", datum))""")]
    [InlineData("""{ "param": "sel", "empty": false }""", """(length(data("sel_store")) && vlSelectionTest("sel_store", datum))""")]
    [InlineData("""{ "param": "ptid" }""", """(!length(data("ptid_store")) || vlSelectionIdTest("ptid_store", datum))""")]
    [InlineData("""{ "param": "multi" }""", """(!length(data("multi_store")) || vlSelectionTest("multi_store", datum, "union"))""")]
    [InlineData("""{ "param": "my-sel" }""", """(!length(data("my_sel_store")) || vlSelectionTest("my_sel_store", datum))""")]
    [InlineData("""{ "not": { "param": "sel" } }""", """!((!length(data("sel_store")) || vlSelectionTest("sel_store", datum)))""")]
    [InlineData(
        """{ "and": [ { "field": "q", "gt": 1 }, { "param": "sel", "empty": false } ] }""",
        """((datum["q"] > 1) && (length(data("sel_store")) && vlSelectionTest("sel_store", datum)))""")]
    [InlineData("""{ "selection": "leg" }""", """(length(data("leg_store")) && vlSelectionIdTest("leg_store", datum))""")]
    [InlineData("""{ "selection": "sel", "empty": false }""", """(!length(data("sel_store")) || vlSelectionTest("sel_store", datum))""")]
    [InlineData("""{ "selection": "leg", "empty": true }""", """(length(data("leg_store")) && vlSelectionIdTest("leg_store", datum))""")]
    [InlineData(
        """{ "selection": { "or": [ "leg", { "not": "sel" } ] } }""",
        """((length(data("leg_store")) && vlSelectionIdTest("leg_store", datum)) || !((!length(data("sel_store")) || vlSelectionTest("sel_store", datum))))""")]
    public void PredicateSource_TryBuild_WritesTheSelectionTestOfVegaLite(string predicate, string expected)
    {
        // Arrange
        var node = Predicate(predicate);

        // Act
        var built = PredicateSource.TryBuild(node, out var source);

        // Assert
        Assert.True(built);
        Assert.Equal(expected, source);
    }

    /// <summary>
    ///     Validates that the legacy form takes its emptiness from the declaration of the selection only: Vega-Lite's <c>selectioncompat</c> turns <c>{"selection": name}</c> into
    ///     <c>{param, empty}</c> where <c>empty</c> is <c>empty !== "none"</c> of the declaration (default true), and does not read an <c>empty</c> of the predicate itself.
    /// </summary>
    /// <remarks>The cases are part of <see cref="PredicateSource_TryBuild_WritesTheSelectionTestOfVegaLite"/>; this only names the rule.</remarks>
    [Fact]
    public void PredicateSource_TryBuild_LegacyPredicateIgnoresItsOwnEmpty()
    {
        // Arrange: the declaration says "none", the predicate says true; and the other way round for a selection that is not legacy
        var none = Predicate("""{ "selection": "leg", "empty": true }""");
        var all = Predicate("""{ "selection": "sel", "empty": false }""");

        // Act
        PredicateSource.TryBuild(none, out var noneSource);
        PredicateSource.TryBuild(all, out var allSource);

        // Assert
        Assert.Equal("""(length(data("leg_store")) && vlSelectionIdTest("leg_store", datum))""", noneSource);
        Assert.Equal("""(!length(data("sel_store")) || vlSelectionTest("sel_store", datum))""", allSource);
    }

    /// <summary>
    ///     Parses a specification with one selection and returns the text of the predicate on it.
    /// </summary>
    /// <param name="declaration">The selection (the <c>params</c> entry, or <c>null</c> to use the legacy entry given in <paramref name="legacy"/>).</param>
    /// <param name="config">The <c>config</c> object (written out), or null.</param>
    /// <param name="legacy">The legacy <c>selection</c> object (written out), or null.</param>
    /// <returns>The expression source of <c>{"param": "p"}</c> (<c>{"selection": "p"}</c> for a legacy entry).</returns>
    private static string SelectionPredicate(string? declaration, string? config = null, string? legacy = null)
    {
        var json = "{ " + (config is null ? string.Empty : "\"config\": " + config + ", ")
            + (declaration is null ? string.Empty : "\"params\": [ " + declaration + " ], ")
            + (legacy is null ? string.Empty : "\"selection\": " + legacy + ", ")
            + """ "data": { "values": [ { "q": 1 } ] }, "transform": [ { "filter": """ + (legacy is null ? """{ "param": "p" }""" : """{ "selection": "p" }""")
            + """ } ], "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" } } }""";
        var node = SpecParser.Parse(json).Root["transform"]!.Items[0]["filter"]!;
        Assert.True(PredicateSource.TryBuild(node, out var source));
        return source;
    }

    /// <summary>
    ///     Validates the choice of the test function from the projection that Vega-Lite's <c>project.ts</c> gives the selection: the presence of <c>fields</c> or <c>encodings</c> decides (an empty
    ///     list is an empty projection, so the field test), then the keys of the value, then the default projection of the configuration (<c>selection.point.fields</c>), then the row identity.
    /// </summary>
    /// <param name="declaration">The selection (written out).</param>
    /// <param name="config">The <c>config</c> object (written out), or null.</param>
    /// <param name="identity">Whether the predicate is the identity test.</param>
    [Theory]
    [InlineData("""{ "name": "p", "select": { "type": "point", "fields": [] } }""", null, false)]
    [InlineData("""{ "name": "p", "select": { "type": "point", "encodings": [] } }""", null, false)]
    [InlineData("""{ "name": "p", "select": { "type": "point", "fields": [] }, "value": [ { "q": 1 } ] }""", null, false)]
    [InlineData("""{ "name": "p", "select": { "type": "point", "fields": ["q"] } }""", null, false)]
    [InlineData("""{ "name": "p", "select": { "type": "point", "encodings": ["x"] } }""", null, false)]
    [InlineData("""{ "name": "p", "select": "point" }""", null, true)]
    [InlineData("""{ "name": "p", "select": "point", "value": 3 }""", null, true)]
    [InlineData("""{ "name": "p", "select": "point", "value": [ { "q": 3 } ] }""", null, false)]
    [InlineData("""{ "name": "p", "select": "point", "value": [ { "x": 3 } ] }""", null, false)]
    [InlineData("""{ "name": "p", "select": { "type": "point", "fields": ["_vgsid_"] } }""", null, true)]
    [InlineData("""{ "name": "p", "select": "point" }""", """{ "selection": { "point": { "fields": ["q"] } } }""", false)]
    [InlineData("""{ "name": "p", "select": "point", "value": 3 }""", """{ "selection": { "point": { "fields": ["q"] } } }""", false)]
    [InlineData("""{ "name": "p", "select": "point" }""", """{ "selection": { "point": { "fields": [] } } }""", false)]
    [InlineData("""{ "name": "p", "select": "point" }""", """{ "selection": { "point": { "encodings": ["x"] } } }""", true)]
    [InlineData("""{ "name": "p", "select": "point" }""", """{ "selection": { "interval": { "encodings": ["x"] } } }""", true)]
    [InlineData("""{ "name": "p", "select": { "type": "point", "fields": [] } }""", """{ "selection": { "point": { "fields": ["_vgsid_"] } } }""", false)]
    [InlineData("""{ "name": "p", "select": "interval" }""", null, false)]
    public void PredicateSource_TryBuild_ChoosesTheTestFromTheEffectiveProjection(string declaration, string? config, bool identity)
    {
        // Act
        var source = SelectionPredicate(declaration, config);

        // Assert
        Assert.Equal(
            identity ? """(!length(data("p_store")) || vlSelectionIdTest("p_store", datum))""" : """(!length(data("p_store")) || vlSelectionTest("p_store", datum))""",
            source);
    }

    /// <summary>
    ///     Validates that the legacy selection object projects as a parameter does (an empty <c>fields</c> is an empty projection).
    /// </summary>
    [Fact]
    public void PredicateSource_TryBuild_LegacySelectionWithEmptyFieldsIsAFieldTest()
    {
        // Act
        var empty = SelectionPredicate(null, legacy: """{ "p": { "type": "multi", "fields": [] } }""");
        var identity = SelectionPredicate(null, legacy: """{ "p": { "type": "multi" } }""");

        // Assert
        Assert.Equal("""(!length(data("p_store")) || vlSelectionTest("p_store", datum))""", empty);
        Assert.Equal("""(!length(data("p_store")) || vlSelectionIdTest("p_store", datum))""", identity);
    }

    /// <summary>
    ///     Validates the predicates that no expression can say: they are not written and the whole predicate is unsupported (a variable parameter whose name is not an identifier).
    /// </summary>
    [Theory]
    [InlineData("""{ "param": "bad name" }""")]
    [InlineData("""{ "and": [ { "field": "q", "gt": 1 }, { "param": "bad name" } ] }""")]
    [InlineData("""{ "not": { "param": "bad name" } }""")]
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
        var unsupported = PredicateSource.Build(HugePredicate("""{ "param": "bad name" }"""), 1_000, out var none, out _);

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
        var unsupported = HugePredicate("""{ "param": "bad name" }""");

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

    /// <summary>
    ///     Validates that the text of a predicate on a selection is counted and refused by the budget like any other: an <c>or</c> of 100 selection predicates is written within a budget
    ///     that holds it, is reported as too long with the exact length (and no text) below it, and a legacy composition behaves the same.
    /// </summary>
    [Fact]
    public void PredicateSource_Build_SelectionPredicatesAreCountedAgainstTheBudget()
    {
        // Arrange
        const string One = """(!length(data("sel_store")) || vlSelectionTest("sel_store", datum))""";
        var modern = Predicate("{ \"or\": [ " + string.Join(", ", Enumerable.Repeat("{ \"param\": \"sel\" }", 100)) + " ] }");
        var legacy = Predicate("{ \"selection\": { \"or\": [ " + string.Join(", ", Enumerable.Repeat("\"sel\"", 100)) + " ] } }");
        var expected = (100L * One.Length) + (99 * 4) + 2;

        foreach (var node in new[] { modern, legacy })
        {
            // Act
            var exact = PredicateSource.Build(node, expected, out var source, out var exactLength);
            var oneLess = PredicateSource.Build(node, expected - 1, out var refused, out var refusedLength);
            var counted = PredicateSource.Build(node, 0, out _, out var countedLength);

            // Assert
            Assert.Equal(PredicateSource.Status.Written, exact);
            Assert.Equal(expected, source.Length);
            Assert.Equal(expected, exactLength);
            Assert.Equal(PredicateSource.Status.TooLong, oneLess);
            Assert.Equal(string.Empty, refused);
            Assert.Equal(expected, refusedLength);
            Assert.Equal(PredicateSource.Status.TooLong, counted);
            Assert.Equal(expected, countedLength);
            Assert.False(PredicateSource.IsUnsupported(node));
        }
    }
}
