using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the row transforms (calculate, filter, fold, flatten, sample, pivot and lookup), the static parameter
///     environment that their expressions read, the deterministic sampler and the sort of discrete scales over transformed rows.
/// </summary>
public class RowTransformsTests
{
    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops promptly.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Parses a specification, loads its data, evaluates its parameters and applies its transforms the way a render does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="warnings">Receives the warnings of the transforms.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="overrides">The parameter values of the caller, or null.</param>
    /// <param name="open">The open options (where data files are), or null.</param>
    /// <param name="canceled">Whether the transforms run under a token that is already canceled (the loader never is).</param>
    /// <returns>The transformed table.</returns>
    private static DataTable Run(
        string json,
        out List<FeatureReport> warnings,
        VegaLiteLimits? limits = null,
        IReadOnlyDictionary<string, object?>? overrides = null,
        VegaLiteOpenOptions? open = null,
        bool canceled = false)
    {
        var tree = SpecParser.Parse(json, "t.vl.json", limits);
        var effective = limits ?? VegaLiteLimits.Default;
        var loader = new DataLoader(tree.SourceName, effective, open, tree.Root["datasets"], null, Ct);
        var table = loader.Load(tree.Root["data"]);
        using var source = new CancellationTokenSource();
        if (canceled)
        {
            source.Cancel();
        }

        var token = canceled ? source.Token : Ct;
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(effective, token, values);
        ParameterEnvironment.Populate(tree, overrides, values, evaluator);
        warnings = [];
        return RowTransforms.Apply(table, tree.Root["transform"], new TransformContext(tree, loader, evaluator, values, effective, token, warnings));
    }

    /// <summary>
    ///     Runs a specification and ignores the warnings.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The transformed table.</returns>
    private static DataTable Run(string json) => Run(json, out _);

    /// <summary>
    ///     Builds a specification with inline rows and the given transforms.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="transforms">The JSON array of transforms.</param>
    /// <param name="extra">Extra top-level members, each followed by a comma, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string rows, string transforms, string extra = "") =>
        "{" + extra + "\"data\":{\"values\":" + rows + "},\"transform\":" + transforms + ",\"mark\":\"point\"}";

    /// <summary>
    ///     Lists the rows of a table as text: the values of the fields joined with a bar, <c>null</c> for null.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="fields">The fields to show.</param>
    /// <returns>One text per row.</returns>
    private static string[] Rows(DataTable table, params string[] fields) =>
        [.. Enumerable.Range(0, table.RowCount).Select(row => string.Join("|", fields.Select(f => table.GetValue(row, f).ToText())))];

    /// <summary>The rows used by the calculate and filter tests.</summary>
    private const string Numbers = "[{\"a\":1,\"b\":2},{\"a\":3,\"b\":4},{\"a\":5,\"b\":null}]";

    /// <summary>
    ///     Validates that calculate adds a field, replaces an existing one by name, and keeps the other fields and the order of the rows.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_CalculateAddsAndReplacesFields()
    {
        // Arrange
        var json = Spec(Numbers, "[{\"calculate\":\"datum.a * 10\",\"as\":\"c\"},{\"calculate\":\"datum.c + 1\",\"as\":\"c\"}]");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(["a", "b", "c"], table.Fields);
        Assert.Equal(["1|11", "3|31", "5|51"], Rows(table, "a", "c"));
        Assert.Equal(["2", "4", "null"], Rows(table, "b"));
    }

    /// <summary>
    ///     Validates that a calculate result may be an array or an object, that a dotted output name is literal, and that
    ///     replacing a nested object by a value drops the fields of the old object.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_CalculateStoresCompositeValuesAndLiteralNames()
    {
        // Arrange
        var json = Spec(
            "[{\"p\":{\"q\":1,\"r\":2}}]",
            "[{\"calculate\":\"[datum.p.q, datum.p.r]\",\"as\":\"pair\"},{\"calculate\":\"{x: datum.p.q}\",\"as\":\"obj\"},{\"calculate\":\"datum.p.q + datum.p.r\",\"as\":\"p\"},{\"calculate\":\"1\",\"as\":\"a.b\"}]");

        // Act
        var table = Run(json);

        // Assert: p was an object (p.q, p.r) and is now the number 3; a dotted name is one field
        Assert.Equal(["a\\.b", "obj", "p", "pair"], table.Fields);
        Assert.True(table.GetValue(0, "pair").IsArray);
        Assert.Equal("1,2", table.GetValue(0, "pair").ToText());
        Assert.True(table.GetValue(0, "obj").IsObject);
        Assert.Equal(3, table.GetValue(0, "p").Number);
        Assert.Equal(1, table.GetValue(0, "a\\.b").Number);
    }

    /// <summary>
    ///     Validates that a regular expression result cannot be stored: it becomes null and one warning counts the rows.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_CalculateOfARegularExpressionWarnsAndStoresNull()
    {
        // Arrange
        var json = Spec(Numbers, "[{\"calculate\":\"regexp('a')\",\"as\":\"r\"}]");

        // Act
        var table = Run(json, out var warnings);

        // Assert
        Assert.All(Enumerable.Range(0, 3), row => Assert.True(table.GetValue(row, "r").IsNull));
        var warning = Assert.Single(warnings);
        Assert.Equal("vl-expr-runtime", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("/transform/0/calculate", warning.Location.Pointer);
        Assert.Contains("3 rows, the first at row index 0", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an expression filter keeps the rows whose result is truthy under JavaScript rules (null is falsy).
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FilterExpressionKeepsTruthyRows()
    {
        // Arrange / Act
        var truthy = Run(Spec(Numbers, "[{\"filter\":\"datum.b\"}]"));
        var comparison = Run(Spec(Numbers, "[{\"filter\":\"datum.a > 1 && datum.a < 5\"}]"));

        // Assert
        Assert.Equal(["1", "3"], Rows(truthy, "a"));
        Assert.Equal(["3"], Rows(comparison, "a"));
    }

    /// <summary>
    ///     Validates every predicate operator and the logical composition, with the comparison rules of JavaScript (null
    ///     converts to zero in an ordered comparison).
    /// </summary>
    /// <param name="predicate">The predicate text.</param>
    /// <param name="expected">The values of x of the rows kept, joined with a comma.</param>
    [Theory]
    [InlineData("{\"field\":\"x\",\"equal\":2}", "2")]
    [InlineData("{\"field\":\"x\",\"equal\":\"2\"}", "")]
    [InlineData("{\"field\":\"x\",\"lt\":3}", "1,2,null")]
    [InlineData("{\"field\":\"x\",\"lte\":2}", "1,2,null")]
    [InlineData("{\"field\":\"x\",\"gt\":1}", "2,3")]
    [InlineData("{\"field\":\"x\",\"gte\":2}", "2,3")]
    [InlineData("{\"field\":\"x\",\"range\":[2,3]}", "2,3")]
    [InlineData("{\"field\":\"x\",\"range\":[3,2]}", "2,3")]
    [InlineData("{\"field\":\"x\",\"range\":[2,null]}", "2,3")]
    [InlineData("{\"field\":\"x\",\"range\":[null,2]}", "1,2,null")]
    [InlineData("{\"field\":\"x\",\"oneOf\":[1,3]}", "1,3")]
    [InlineData("{\"field\":\"x\",\"valid\":true}", "1,2,3")]
    [InlineData("{\"field\":\"x\",\"valid\":false}", "null")]
    [InlineData("{\"and\":[{\"field\":\"x\",\"gt\":1},{\"field\":\"x\",\"lt\":3}]}", "2")]
    [InlineData("{\"or\":[{\"field\":\"x\",\"equal\":1},\"datum.x === 3\"]}", "1,3")]
    [InlineData("{\"not\":{\"field\":\"x\",\"valid\":true}}", "null")]
    [InlineData("{\"not\":{\"or\":[{\"field\":\"x\",\"equal\":1},{\"field\":\"x\",\"equal\":2}]}}", "3,null")]
    [InlineData("{\"field\":\"x\",\"equal\":2,\"timeUnit\":\"year\"}", "1,2,3,null")]
    [InlineData("{\"not\":{\"field\":\"x\",\"equal\":2,\"timeUnit\":\"year\"}}", "")]
    [InlineData("{\"and\":[{\"field\":\"x\",\"equal\":2,\"timeUnit\":\"year\"},{\"field\":\"x\",\"gt\":1}]}", "2,3")]
    [InlineData("{\"field\":\"x\",\"equal\":{\"year\":2020}}", "1,2,3,null")]
    public void RowTransforms_Apply_FilterPredicatesAndCompositions(string predicate, string expected)
    {
        // Arrange
        var json = Spec("[{\"x\":1},{\"x\":2},{\"x\":3},{\"x\":null}]", "[{\"filter\":" + predicate + "}]");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(expected, string.Join(",", Rows(table, "x")));
    }

    /// <summary>
    ///     Validates that a filter on a variable parameter keeps every row or none, that the caller's value replaces the
    ///     static default, and that a selection parameter keeps every row.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FilterOnAParameterReadsItsTruthiness()
    {
        // Arrange
        var json = Spec(Numbers, "[{\"filter\":{\"param\":\"show\"}}]", "\"params\":[{\"name\":\"show\",\"value\":true}],");
        var selection = Spec(Numbers, "[{\"filter\":{\"param\":\"brush\"}}]", "\"params\":[{\"name\":\"brush\",\"select\":\"interval\"}],");
        var legacy = Spec(Numbers, "[{\"filter\":{\"selection\":\"brush\"}}]", "\"params\":[{\"name\":\"brush\",\"select\":\"interval\"}],");

        // Act
        var shown = Run(json);
        var hidden = Run(json, out _, overrides: new Dictionary<string, object?> { ["show"] = false });
        var zero = Run(json, out _, overrides: new Dictionary<string, object?> { ["show"] = 0 });

        // Assert
        Assert.Equal(3, shown.RowCount);
        Assert.Equal(0, hidden.RowCount);
        Assert.Equal(0, zero.RowCount);
        Assert.Equal(3, Run(selection).RowCount);
        Assert.Equal(3, Run(legacy).RowCount);
    }

    /// <summary>
    ///     Validates the static parameters: a default value, an expression that reads other parameters whatever the order of
    ///     the text, a caller override of a value and of an expression, composite values, and use in a transform expression.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_ParametersAreEvaluatedInDependencyOrderAndOverridden()
    {
        // Arrange: b depends on a, c depends on b, written in the opposite order
        const string Params = "\"params\":[{\"name\":\"c\",\"expr\":\"b * 2\"},{\"name\":\"b\",\"expr\":\"a + 1\"},{\"name\":\"a\",\"value\":2},{\"name\":\"list\",\"value\":[\"x\",\"y\"]}],";
        var json = Spec("[{\"i\":1}]", "[{\"calculate\":\"c + list[1]\",\"as\":\"r\"},{\"calculate\":\"a\",\"as\":\"a2\"},{\"calculate\":\"b\",\"as\":\"b2\"}]", Params);

        // Act
        var defaults = Run(json);
        var overridden = Run(json, out _, overrides: new Dictionary<string, object?> { ["a"] = 10 });
        var replacedExpression = Run(json, out _, overrides: new Dictionary<string, object?> { ["b"] = 100, ["list"] = "q" });

        // Assert: 6 + "y"; then 22 + "y"; then the overridden b replaces its expression (200) and "q"[1] is null, which adds as zero
        Assert.Equal(["6y"], Rows(defaults, "r"));
        Assert.Equal(["2"], Rows(defaults, "a2"));
        Assert.Equal(["3"], Rows(defaults, "b2"));
        Assert.Equal(["22y"], Rows(overridden, "r"));
        Assert.Equal(["11"], Rows(overridden, "b2"));
        Assert.Equal(["200"], Rows(replacedExpression, "r"));
        Assert.Equal(["100"], Rows(replacedExpression, "b2"));
    }

    /// <summary>
    ///     Validates that a long chain of parameters, each defined by the one declared after it, is ordered and evaluated
    ///     correctly (the dependency order is computed once for the whole list, not searched for every parameter).
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_LongParameterChainsAreOrderedByDependency()
    {
        // Arrange: p0 = p1 + 1, p1 = p2 + 1, ..., p1999 = 0, so p0 is 1999
        const int Count = 2000;
        var parameters = string.Join(",", Enumerable.Range(0, Count).Select(i => i == Count - 1
            ? "{\"name\":\"p" + i + "\",\"value\":0}"
            : "{\"name\":\"p" + i + "\",\"expr\":\"p" + (i + 1) + " + 1\"}"));
        var json = Spec("[{\"a\":1}]", "[{\"calculate\":\"p0\",\"as\":\"r\"}]", "\"params\":[" + parameters + "],");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(["1999"], Rows(table, "r"));
    }

    /// <summary>
    ///     Validates the specification errors of parameters: a cycle, a self reference, an unknown reference, a repeated name
    ///     and a reserved name, each located at the offending value.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_ParameterErrorsAreLocatedSpecificationErrors()
    {
        // Arrange / Act
        var cycle = Assert.Throws<VegaLiteSpecException>(() => Run(Spec("[]", "[]", "\"params\":[{\"name\":\"a\",\"expr\":\"b\"},{\"name\":\"b\",\"expr\":\"a\"}],")));
        var self = Assert.Throws<VegaLiteSpecException>(() => Run(Spec("[]", "[]", "\"params\":[{\"name\":\"ok\",\"value\":1},{\"name\":\"a\",\"expr\":\"a + 1\"}],")));
        var unknown = Assert.Throws<VegaLiteSpecException>(() => Run(Spec("[]", "[]", "\"params\":[{\"name\":\"a\",\"expr\":\"1 + zzz\"}],")));
        var repeated = Assert.Throws<VegaLiteSpecException>(() => Run(Spec("[]", "[]", "\"params\":[{\"name\":\"a\",\"value\":1},{\"name\":\"a\",\"value\":2}],")));
        var reserved = Assert.Throws<VegaLiteSpecException>(() => Run(Spec("[]", "[]", "\"params\":[{\"name\":\"datum\",\"value\":1}],")));
        var transformUnknown = Assert.Throws<VegaLiteSpecException>(() => Run(Spec("[]", "[{\"calculate\":\"nothing + 1\",\"as\":\"x\"}]")));

        // Assert
        Assert.Equal("/params/0/expr", cycle.Location.Pointer);
        Assert.Contains("cycle", cycle.Message, StringComparison.Ordinal);
        Assert.Equal("/params/1/expr", self.Location.Pointer);
        Assert.Equal("/params/0/expr", unknown.Location.Pointer);
        Assert.Contains("'zzz'", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("expression offset 4", unknown.Message, StringComparison.Ordinal);
        Assert.Equal("/params/1/name", repeated.Location.Pointer);
        Assert.Equal("/params/0/name", reserved.Location.Pointer);
        Assert.Equal("/transform/0/calculate", transformUnknown.Location.Pointer);
        Assert.StartsWith("t.vl.json:", unknown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that fold produces one row per input row and folded field with the default and the given names, that the
    ///     folded fields stay, and that a missing field folds to null.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FoldProducesKeyAndValueRows()
    {
        // Arrange
        const string Rows3 = "[{\"id\":\"p\",\"u\":1,\"v\":2},{\"id\":\"q\",\"u\":3,\"v\":4}]";

        // Act
        var folded = Run(Spec(Rows3, "[{\"fold\":[\"u\",\"v\"]}]"));
        var named = Run(Spec(Rows3, "[{\"fold\":[\"u\",\"zz\"],\"as\":[\"name\",\"amount\"]}]"));
        var keyOnly = Run(Spec(Rows3, "[{\"fold\":[\"v\"],\"as\":[\"which\"]}]"));

        // Assert
        Assert.Equal(["id", "key", "u", "v", "value"], folded.Fields);
        Assert.Equal(["p|u|1", "p|v|2", "q|u|3", "q|v|4"], Rows(folded, "id", "key", "value"));
        Assert.Equal(["p|u|1", "p|zz|null", "q|u|3", "q|zz|null"], Rows(named, "id", "name", "amount"));
        Assert.Equal(["p|v|2", "q|v|4"], Rows(keyOnly, "id", "which", "value"));
    }

    /// <summary>
    ///     Validates that flatten expands arrays in row order, treats a scalar as one element, drops empty, null and missing
    ///     arrays, and replaces the array fields by the elements.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FlattenExpandsArraysAndScalars()
    {
        // Arrange
        const string Tagged = "[{\"id\":1,\"tags\":[\"a\",\"b\"]},{\"id\":2,\"tags\":[]},{\"id\":3,\"tags\":\"c\"},{\"id\":4,\"tags\":null},{\"id\":5}]";

        // Act
        var table = Run(Spec(Tagged, "[{\"flatten\":[\"tags\"]}]"));

        // Assert: the array fields tags.0 and tags.1 are replaced by the field tags
        Assert.Equal(["id", "tags"], table.Fields);
        Assert.Equal(["1|a", "1|b", "3|c"], Rows(table, "id", "tags"));
    }

    /// <summary>
    ///     Validates the parallel expansion of several array fields (shorter ones padded with null) and the output names.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FlattenExpandsSeveralFieldsInParallel()
    {
        // Arrange
        var json = Spec("[{\"k\":1,\"a\":[10,20,30],\"b\":[\"x\"]},{\"k\":2,\"a\":[],\"b\":[\"y\",\"z\"]}]", "[{\"flatten\":[\"a\",\"b\"],\"as\":[\"n\",\"s\"]}]");

        // Act
        var table = Run(json);

        // Assert: the longest array of each row decides, and the fields a and b stay as they were
        Assert.Equal(["1|10|x", "1|20|null", "1|30|null", "2|null|y", "2|null|z"], Rows(table, "k", "n", "s"));
        Assert.Equal(5, table.RowCount);
    }

    /// <summary>
    ///     Validates the sampler against a hand-computed vector: the FNV-1a seed of a pointer, the SplitMix64 output for a
    ///     known state, the reservoir for ten rows, and that the same sample is selected in source order.
    /// </summary>
    [Fact]
    public void RowSampler_Select_MatchesTheHandComputedReservoir()
    {
        // Arrange / Act / Assert: the FNV-1a 64-bit offset basis for the empty text and the known value for "a"
        Assert.Equal(14695981039346656037UL, RowSampler.Seed(string.Empty));
        Assert.Equal(12638187200555641996UL, RowSampler.Seed("a"));
        Assert.Equal(9609140692196152753UL, RowSampler.Seed("/transform/0"));

        // Assert: SplitMix64 started at state 0 gives the published first output
        var state = 0UL;
        Assert.Equal(16294208416658607535UL, RowSampler.Next(ref state));

        // Assert: from the seed of "/transform/0" the draws for indexes 4 to 9 are, modulo i + 1, the slots 0, 0, 1, 0, 4, 0
        var seed = RowSampler.Seed("/transform/0");
        ulong[] draws = [15123139727152546690UL, 4042705570471441650UL, 17835413592778900504UL, 12511710870284562112UL, 14693908099680327109UL, 5643946291216382080UL];
        ulong[] slots = [0, 0, 1, 0, 4, 0];
        for (var i = 0; i < draws.Length; i++)
        {
            var draw = RowSampler.Next(ref seed);
            Assert.Equal(draws[i], draw);
            Assert.Equal(slots[i], draw % (ulong)(i + 5));
        }

        // Assert: reservoir [0,1,2,3] becomes [4,..] [5,..] [..6..] [7,..] (slot 4 is outside) [9,..], sorted: 2, 3, 6, 9
        Assert.Equal([2, 3, 6, 9], RowSampler.Select(10, 4, "/transform/0", Ct));
        Assert.Equal([3, 5, 8, 9], RowSampler.Select(10, 4, "/transform/1", Ct));
        Assert.Equal([0, 6, 11, 13, 16], RowSampler.Select(20, 5, "/transform/2", Ct));
    }

    /// <summary>
    ///     Validates the sample transform through a specification: the position of the transform seeds it, a size of at
    ///     least the number of rows is a stable copy and a size of zero is empty.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_SampleUsesThePointerAsSeed()
    {
        // Arrange: ten rows with the index i; the same size at two positions of the transform array
        var rows = "[" + string.Join(",", Enumerable.Range(0, 10).Select(i => "{\"i\":" + i + "}")) + "]";

        // Act
        var first = Run(Spec(rows, "[{\"sample\":4}]"));
        var second = Run(Spec(rows, "[{\"sample\":10},{\"sample\":4}]"));
        var all = Run(Spec(rows, "[{\"sample\":10}]"));
        var more = Run(Spec(rows, "[{\"sample\":999}]"));
        var none = Run(Spec(rows, "[{\"sample\":0}]"));

        // Assert
        Assert.Equal(["2", "3", "6", "9"], Rows(first, "i"));
        Assert.Equal(["3", "5", "8", "9"], Rows(second, "i"));
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)), Rows(all, "i"));
        Assert.Equal(10, more.RowCount);
        Assert.Equal(0, none.RowCount);
    }

    /// <summary>
    ///     Validates that the reservoir sampler polls the cancellation token while it fills the reservoir, not only in the
    ///     sampling loop: a sample whose size is below the first polling interval after it must still stop.
    /// </summary>
    [Fact]
    public void RowSampler_Select_ReservoirInitializationObservesCancellation()
    {
        // Arrange: size 700 of 800 rows, so no row index of the sampling loop (700 to 799) is a multiple of 1,024
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => RowSampler.Select(800, 700, "/transform/0", source.Token));
        Assert.Equal(700, RowSampler.Select(800, 700, "/transform/0", Ct).Length);
    }

    /// <summary>
    ///     Validates that the whole row that a lookup copies is an object even when its names are the indexes 0 to n-1, and
    ///     that a reader of the whole row of an empty row gives the empty object while a reader of a path gives null.
    /// </summary>
    [Fact]
    public void FieldReader_WholeRow_IsAlwaysAnObject()
    {
        // Arrange: the secondary row has the field names 0 and 1
        var json = Spec(
            "[{\"id\":2}]",
            "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":[{\"0\":2,\"1\":\"x\"}]},\"key\":\"0\"},\"as\":\"hit\"}]");
        var empty = DataTable.Create([], [new List<DataValue>()]);

        // Act
        var table = Run(json);
        var emptyWhole = new FieldReader(empty, string.Empty).Read(0);
        var emptyPath = new FieldReader(empty, "a").Read(0);

        // Assert
        var hit = table.GetValue(0, "hit");
        Assert.True(hit.IsObject);
        Assert.Equal(["0", "1"], hit.ObjectValue!.Keys);
        Assert.True(emptyWhole.IsObject);
        Assert.Empty(emptyWhole.ObjectValue!.Keys);
        Assert.True(emptyPath.IsNull);
    }

    /// <summary>
    ///     Validates that repeated and concurrent runs select identical rows, whatever the degree of parallelism.
    /// </summary>
    [Fact]
    public async Task RowTransforms_Apply_SampleIsIdenticalWhenRepeatedAndConcurrent()
    {
        // Arrange
        var rows = "[" + string.Join(",", Enumerable.Range(0, 2000).Select(i => "{\"i\":" + i + "}")) + "]";
        var json = Spec(rows, "[{\"sample\":100}]");
        var reference = Rows(Run(json), "i");

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Rows(Run(json), "i"), Ct)));

        // Assert
        Assert.Equal(100, reference.Length);
        Assert.All(results, result => Assert.Equal(reference, result));
        Assert.Equal(reference, Rows(Run(json), "i"));
        Assert.Equal(100, reference.Distinct().Count());

        // Assert: the rows come in source order
        var indexes = reference.Select(text => int.Parse(text, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(indexes.Order().ToList(), indexes);
    }

    /// <summary>The rows used by the pivot tests.</summary>
    private const string PivotRows = "[{\"g\":\"a\",\"k\":\"x\",\"v\":1},{\"g\":\"a\",\"k\":\"y\",\"v\":2},{\"g\":\"b\",\"k\":\"x\",\"v\":3},{\"g\":\"a\",\"k\":\"x\",\"v\":4}]";

    /// <summary>
    ///     Validates that pivot sums the value per group in the first-seen order of the groups, with zero for a missing
    ///     combination, and keeps only the group fields and the pivot columns.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotSumsValuesPerGroup()
    {
        // Arrange / Act
        var table = Run(Spec(PivotRows, "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"]}]"));

        // Assert
        Assert.Equal(["g", "x", "y"], table.Fields);
        Assert.Equal(["a|5|2", "b|3|0"], Rows(table, "g", "x", "y"));
    }

    /// <summary>
    ///     Validates the count operation, a pivot without group fields (one row) and that an operation that is not supported
    ///     falls back to the sum.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotCountsAndFallsBackToSum()
    {
        // Arrange / Act
        var counted = Run(Spec(PivotRows, "[{\"pivot\":\"k\",\"value\":\"v\",\"op\":\"count\"}]"));
        var mean = Run(Spec(PivotRows, "[{\"pivot\":\"k\",\"value\":\"v\",\"op\":\"mean\",\"groupby\":[\"g\"]}]"));

        // Assert
        Assert.Equal(["3|1"], Rows(counted, "x", "y"));
        Assert.Equal(["a|5|2", "b|3|0"], Rows(mean, "g", "x", "y"));
    }

    /// <summary>
    ///     Validates the pivot limit (the first values in ordinal order are kept, with a warning) and that rows whose pivot
    ///     value is null are skipped with a warning.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotLimitAndNullValuesAreWarnings()
    {
        // Arrange
        var withNull = PivotRows.Replace("]", ",{\"g\":\"a\",\"k\":null,\"v\":9}]", StringComparison.Ordinal);

        // Act
        var limited = Run(Spec(withNull, "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"],\"limit\":1}]"), out var warnings);

        // Assert
        Assert.Equal(["g", "x"], limited.Fields);
        Assert.Equal(["a|5", "b|3"], Rows(limited, "g", "x"));
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, w => Assert.Equal(("vl-transform-runtime", "/transform/0"), (w.Id, w.Location.Pointer)));
        Assert.Contains("1 row, the first at row index 4", warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("only the first 1 in ordinal order", warnings[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a pivot with more columns than the field limit allows is a located failure.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotColumnsAreBoundedByTheFieldLimit()
    {
        // Arrange: one group field and three distinct pivot values against a limit of three fields
        var json = Spec("[{\"g\":1,\"k\":\"a\",\"v\":1},{\"g\":1,\"k\":\"b\",\"v\":1},{\"g\":1,\"k\":\"c\",\"v\":1}]", "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"]}]");

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(json, out _, new VegaLiteLimits { MaxFieldCount = 3 }));

        // Assert
        Assert.Equal("/transform/0", ex.Location.Pointer);
        Assert.Contains("MaxFieldCount", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the pivot limit is applied before the field bound: many distinct values with a small limit give
    ///     exactly the first values in ordinal order, even when the distinct values alone would exceed the field limit.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotLimitIsAppliedBeforeTheFieldBound()
    {
        // Arrange: one group field, 50 distinct pivot values (in descending order, so the kept ones arrive last) and a field
        // limit of 10 that the 50 values alone would exceed
        var rows = string.Join(",", Enumerable.Range(0, 50).Select(i => $"{{\"g\":1,\"k\":\"k{49 - i:D2}\",\"v\":{i}}}"));
        var json = Spec($"[{rows}]", "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"],\"limit\":3}]");

        // Act
        var table = Run(json, out var warnings, new VegaLiteLimits { MaxFieldCount = 10 });

        // Assert: exactly three columns, the first in ordinal order, and one warning for the cut
        Assert.Equal(["g", "k00", "k01", "k02"], table.Fields);
        Assert.Equal(["1|49|48|47"], Rows(table, "g", "k00", "k01", "k02"));
        var warning = Assert.Single(warnings);
        Assert.Equal(("vl-transform-runtime", "/transform/0"), (warning.Id, warning.Location.Pointer));
        Assert.Contains("only the first 3 in ordinal order", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the field bound still stops a pivot without a limit, or with a limit above the bound, and that a
    ///     limit that is not reached does not hide it.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotWithoutLimitIsStillBoundedByTheFieldLimit()
    {
        // Arrange: 50 distinct values against a field limit of 10
        var rows = string.Join(",", Enumerable.Range(0, 50).Select(i => $"{{\"g\":1,\"k\":\"k{i:D2}\",\"v\":1}}"));
        var unlimited = Spec($"[{rows}]", "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"]}]");
        var tooHigh = Spec($"[{rows}]", "[{\"pivot\":\"k\",\"value\":\"v\",\"groupby\":[\"g\"],\"limit\":40}]");
        var limits = new VegaLiteLimits { MaxFieldCount = 10 };

        // Act
        var first = Assert.Throws<VegaLiteRenderException>(() => Run(unlimited, out _, limits));
        var second = Assert.Throws<VegaLiteRenderException>(() => Run(tooHigh, out _, limits));

        // Assert
        Assert.All([first, second], ex =>
        {
            Assert.Equal("/transform/0", ex.Location.Pointer);
            Assert.Contains("MaxFieldCount", ex.Message, StringComparison.Ordinal);
        });
    }

    /// <summary>
    ///     Validates that a text of a parameter that comes from outside the evaluation (the value of the caller or a value of
    ///     the specification under a lower limit) is bounded by <see cref="VegaLiteLimits.MaxStringLength"/>, so it cannot be
    ///     stored by <c>calculate</c> or returned by <c>toString</c>.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_ParameterTextLongerThanMaxStringLength_IsLocatedRenderError()
    {
        // Arrange: a parameter that calculate stores and toString converts, with a limit of 20 characters
        var limits = new VegaLiteLimits { MaxStringLength = 20 };
        var json = Spec(
            "[{\"i\":1}]",
            "[{\"calculate\":\"p\",\"as\":\"stored\"},{\"calculate\":\"toString(p)\",\"as\":\"converted\"}]",
            "\"params\":[{\"name\":\"p\",\"value\":\"short\"}],");

        // Act
        var fits = Run(json, out _, limits, new Dictionary<string, object?> { ["p"] = new string('x', 20) });
        var tooLong = Assert.Throws<VegaLiteRenderException>(
            () => Run(json, out _, limits, new Dictionary<string, object?> { ["p"] = new string('x', 21) }));

        // Assert: at the limit the text is kept; over it the failure is located at the parameter and names the limit
        Assert.Equal(20, fits.GetValue(0, "stored").Text!.Length);
        Assert.Equal("/params/0", tooLong.Location.Pointer);
        Assert.Contains("the limit of 20 characters (VegaLiteLimits.MaxStringLength)", tooLong.Message, StringComparison.Ordinal);
        Assert.Contains("'p'", tooLong.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the texts of the values that the specification gives a parameter are checked against the limit of
    ///     the render call, which can be lower than the limit the specification was parsed with, also inside a composite.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_StaticParameterTextLongerThanRenderLimit_IsLocatedRenderError()
    {
        // Arrange: parsed under the default limit, evaluated under a limit of 10
        var tree = SpecParser.Parse(Spec("[]", "[]", "\"params\":[{\"name\":\"s\",\"value\":\"0123456789a\"},{\"name\":\"l\",\"value\":[\"ok\",{\"k\":\"0123456789a\"}]}],"), "t.vl.json");
        var limits = new VegaLiteLimits { MaxStringLength = 10 };
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(limits, Ct, values);

        // Act
        var scalar = Assert.Throws<VegaLiteRenderException>(() => ParameterEnvironment.Populate(tree, null, values, evaluator));
        var composite = SpecParser.Parse(Spec("[]", "[]", "\"params\":[{\"name\":\"l\",\"value\":[\"ok\",{\"k\":\"0123456789a\"}]}],"), "t.vl.json");
        var nested = Assert.Throws<VegaLiteRenderException>(() => ParameterEnvironment.Populate(composite, null, values, evaluator));

        // Assert
        Assert.Equal("/params/0", scalar.Location.Pointer);
        Assert.Contains("the limit of 10 characters (VegaLiteLimits.MaxStringLength)", scalar.Message, StringComparison.Ordinal);
        Assert.Equal("/params/0", nested.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a calculated field that is the text of a long parameter is not stored when the text is over the
    ///     limit of the call, whichever way the text got to the expression.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_CalculateOfLongTextFieldIsLocatedRenderError()
    {
        // Arrange: a row whose text is over a limit of 10 only when the render limit is lower than the one used to parse it
        var tree = SpecParser.Parse(Spec("[{\"s\":\"0123456789a\"}]", "[{\"calculate\":\"datum.s\",\"as\":\"copy\"}]"), "t.vl.json");
        var limits = new VegaLiteLimits { MaxStringLength = 10 };
        var loader = new DataLoader(tree.SourceName, VegaLiteLimits.Default, null, tree.Root["datasets"], null, Ct);
        var table = loader.Load(tree.Root["data"]);
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(limits, Ct, values);

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(
            () => RowTransforms.Apply(table, tree.Root["transform"], new TransformContext(tree, loader, evaluator, values, limits, Ct, [])));

        // Assert
        Assert.Equal("/transform/0/calculate", exception.Location.Pointer);
        Assert.Contains("the limit of 10 characters (VegaLiteLimits.MaxStringLength)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the array value of a pivot field is converted to the text of its column name within the string
    ///     limit and that a text that is too long is a located failure before the column is created.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_PivotArrayKeyTextIsBoundedByMaxStringLength()
    {
        // Arrange: the key is the array ["aaaaaa", "bbbbbb"], whose text has 13 characters
        var json = Spec("[{\"k\":[\"aaaaaa\",\"bbbbbb\"],\"v\":1},{\"k\":[\"aaaaaa\",\"bbbbbb\"],\"v\":2}]", "[{\"pivot\":\"k\",\"value\":\"v\"}]");

        // Act
        var table = Run(json);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Run(json, out _, new VegaLiteLimits { MaxStringLength = 12 }));

        // Assert: within the limit the column is named by the JavaScript text of the array
        Assert.Equal(["aaaaaa,bbbbbb"], table.Fields);
        Assert.Equal("3", table.GetValue(0, "aaaaaa,bbbbbb").ToText());
        Assert.Equal("/transform/0", exception.Location.Pointer);
        Assert.Contains("the limit of 12 characters (VegaLiteLimits.MaxStringLength)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'k'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a text of the specification that a transform copies into its output (the default of a lookup and the field names a fold turns into keys) is checked against the limit of the call, which
    ///     can be lower than the limit the specification was parsed with.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_SpecTextLongerThanRenderLimit_IsLocatedRenderError()
    {
        // Arrange: parsed under the default limit, applied under a limit of 10
        var limits = new VegaLiteLimits { MaxStringLength = 10 };
        var lookupScalar = "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":" + Secondary + "},\"key\":\"k\",\"fields\":[\"n\"]},\"default\":\"0123456789a\"}]";
        var lookupFits = "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":" + Secondary + "},\"key\":\"k\",\"fields\":[\"n\"]},\"default\":\"0123456789\"}]";
        var fold = "[{\"fold\":[\"0123456789a\"]}]";

        // Act
        var scalar = Assert.Throws<VegaLiteRenderException>(() => RunUnder(Spec("[{\"id\":9}]", lookupScalar), limits));
        var folded = Assert.Throws<VegaLiteRenderException>(() => RunUnder(Spec("[{\"0123456789a\":1}]", fold), limits));
        var fits = RunUnder(Spec("[{\"id\":9}]", lookupFits), limits);

        // Assert: located at the transform, naming the limit; a text at the limit is kept
        foreach (var exception in new[] { scalar, folded })
        {
            Assert.Equal("/transform/0", exception.Location.Pointer);
            Assert.Contains("the limit of 10 characters (VegaLiteLimits.MaxStringLength)", exception.Message, StringComparison.Ordinal);
        }

        Assert.Equal("0123456789", fits.GetValue(0, "n").Text);
    }

    /// <summary>
    ///     Validates that a calculated array or object whose texts are over the limit is not stored in a field.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_CalculateOfCompositeWithLongText_IsLocatedRenderError()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 5 };
        var array = Spec("[{\"i\":1}]", "[{\"calculate\":\"['0123456789']\",\"as\":\"a\"}]");
        var obj = Spec("[{\"i\":1}]", "[{\"calculate\":\"{k: '0123456789'}\",\"as\":\"o\"}]");

        // Act
        var first = Assert.Throws<VegaLiteRenderException>(() => RunUnder(array, limits));
        var second = Assert.Throws<VegaLiteRenderException>(() => RunUnder(obj, limits));

        // Assert
        Assert.Equal("/transform/0/calculate", first.Location.Pointer);
        Assert.Equal("/transform/0/calculate", second.Location.Pointer);
        Assert.Contains("the limit of 5 characters (VegaLiteLimits.MaxStringLength)", first.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the shared text check: it finds a text or object key over the limit at any depth and passes the rest.
    /// </summary>
    [Fact]
    public void DataValue_HasTextLongerThan_FindsLongTextAtAnyDepth()
    {
        // Arrange
        var deep = DataValue.FromString("abcdef");
        for (var i = 0; i < 1000; i++)
        {
            deep = DataValue.FromObject(DataObject.Create([new KeyValuePair<string, DataValue>("k", deep)]));
        }

        var longKey = DataValue.FromObject(DataObject.Create([new KeyValuePair<string, DataValue>("abcdef", DataValue.FromNumber(1))]));

        // Act and assert
        Assert.True(deep.HasTextLongerThan(5));
        Assert.False(deep.HasTextLongerThan(6));
        Assert.True(longKey.HasTextLongerThan(5));
        Assert.False(longKey.HasTextLongerThan(6));
        Assert.False(DataValue.FromNumber(1).HasTextLongerThan(0));
        Assert.False(DataValue.Null.HasTextLongerThan(0));
    }

    /// <summary>
    ///     Parses a specification under the default limits and applies it under the limits of the call.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits of the call.</param>
    /// <returns>The transformed table.</returns>
    private static DataTable RunUnder(string json, VegaLiteLimits limits)
    {
        var tree = SpecParser.Parse(json, "t.vl.json");
        var loader = new DataLoader(tree.SourceName, VegaLiteLimits.Default, null, tree.Root["datasets"], null, Ct);
        var table = loader.Load(tree.Root["data"]);
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(limits, Ct, values);
        return RowTransforms.Apply(table, tree.Root["transform"], new TransformContext(tree, loader, evaluator, values, limits, Ct, []));
    }

    /// <summary>The secondary rows used by the lookup tests.</summary>
    private const string Secondary = "[{\"k\":1,\"n\":\"one\",\"m\":10},{\"k\":2,\"n\":\"two\",\"m\":20},{\"k\":1,\"n\":\"uno\",\"m\":99}]";

    /// <summary>
    ///     Validates lookup against an inline source: the first match wins, the fields go to the names of <c>as</c>, and a
    ///     row without a match gets the default.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_LookupCopiesFieldsOfTheFirstMatch()
    {
        // Arrange
        var json = Spec(
            "[{\"id\":1},{\"id\":2},{\"id\":3},{\"id\":null}]",
            "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":" + Secondary + "},\"key\":\"k\",\"fields\":[\"n\",\"m\"]},\"as\":[\"name\",\"size\"],\"default\":\"?\"}]");

        // Act
        var table = Run(json);

        // Assert
        Assert.Equal(["1|one|10", "2|two|20", "3|?|?", "null|?|?"], Rows(table, "id", "name", "size"));
    }

    /// <summary>
    ///     Validates the default output names (the field names), the match by text (a number finds the same text) and the whole-row
    ///     form, which stores one object in the field <c>as</c> (<c>data</c> by default).
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_LookupDefaultNamesTextKeysAndWholeRows()
    {
        // Arrange
        var named = Spec("[{\"id\":\"2\"}]", "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":" + Secondary + "},\"key\":\"k\",\"fields\":[\"n\"]}}]");
        var whole = Spec("[{\"id\":2},{\"id\":9}]", "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":" + Secondary + "},\"key\":\"k\"}},{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":" + Secondary + "},\"key\":\"k\"},\"as\":\"hit\",\"default\":0}]");

        // Act
        var byName = Run(named);
        var rows = Run(whole);

        // Assert: the string "2" finds the number 2 and the output field has the name of the looked-up field
        Assert.Equal(["2|two"], Rows(byName, "id", "n"));
        Assert.True(rows.GetValue(0, "data").IsObject);
        Assert.True(rows.GetValue(0, "data").ObjectValue!.TryGetValue("n", out var n));
        Assert.Equal("two", n.Text);
        Assert.True(rows.GetValue(1, "data").IsNull);
        Assert.Equal(["0", "20"], new[] { rows.GetValue(1, "hit").ToText(), rows.GetValue(0, "hit").ObjectValue!.TryGetValue("m", out var m) ? m.ToText() : string.Empty });
    }

    /// <summary>
    ///     Validates lookup in a named dataset of the specification, and that a lookup in the data of a parameter is not applied.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_LookupInNamedDatasetsAndParamSourceIsSkipped()
    {
        // Arrange
        var named = Spec(
            "[{\"id\":2}]",
            "[{\"lookup\":\"id\",\"from\":{\"data\":{\"name\":\"names\"},\"key\":\"k\",\"fields\":[\"n\"]}}]",
            "\"datasets\":{\"names\":" + Secondary + "},");
        var param = Spec("[{\"id\":2}]", "[{\"lookup\":\"id\",\"from\":{\"param\":\"brush\"}}]");

        // Act
        var table = Run(named);
        var skipped = Run(param);

        // Assert
        Assert.Equal(["2|two"], Rows(table, "id", "n"));
        Assert.Equal(["id"], skipped.Fields);
    }

    /// <summary>
    ///     Validates lookup in a file beside the specification: the file is read through the sandbox of the call, once, and a path
    ///     that escapes the sandbox is a located failure.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_LookupInFilesUsesTheSandboxAndTheFileCache()
    {
        // Arrange
        using var directory = new DataTempDirectory();
        directory.Write("root/names.csv", "k,n\n1,one\n2,two\n");
        directory.Write("outside.csv", "k,n\n1,leak\n");
        var open = new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") };
        var from = "{\"data\":{\"url\":\"names.csv\"},\"key\":\"k\",\"fields\":[\"n\"]}";
        var json = Spec("[{\"id\":2},{\"id\":1}]", "[{\"lookup\":\"id\",\"from\":" + from + "},{\"lookup\":\"id\",\"from\":" + from + ",\"as\":[\"again\"]}]");
        var escape = Spec("[{\"id\":1}]", "[{\"lookup\":\"id\",\"from\":{\"data\":{\"url\":\"../outside.csv\"},\"key\":\"k\",\"fields\":[\"n\"]}}]");

        // Act: two lookups of the same file need a file limit of one
        var table = Run(json, out _, new VegaLiteLimits { MaxDataFiles = 1 }, open: open);
        var blocked = Assert.Throws<VegaLiteRenderException>(() => Run(escape, out _, open: open));

        // Assert
        Assert.Equal(["2|two|two", "1|one|one"], Rows(table, "id", "n", "again"));
        Assert.Contains("outside the allowed directories", blocked.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0/from/data/url", blocked.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a lookup source counts against the same row budget as the main data, so that a total over the limit
    ///     is a located failure at the source and a total at the limit works.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_LookupSourceSharesTheRowBudget()
    {
        // Arrange: three main rows and three secondary rows against limits of five and six
        var json = Spec(
            "[{\"id\":1},{\"id\":2},{\"id\":3}]",
            "[{\"lookup\":\"id\",\"from\":{\"data\":{\"values\":" + Secondary + "},\"key\":\"k\",\"fields\":[\"n\"]}}]");

        // Act
        var ok = Run(json, out _, new VegaLiteLimits { MaxDataRows = 6 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(json, out _, new VegaLiteLimits { MaxDataRows = 5 }));

        // Assert
        Assert.Equal(3, ok.RowCount);
        Assert.Equal("/transform/0/from/data/values", ex.Location.Pointer);
        Assert.Contains("MaxDataRows", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that fold counts its output rows before it allocates them, with the input counted until the output is
    ///     complete: three rows folded over two fields need room for nine rows.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FoldOutputIsBoundedByTheRowLimit()
    {
        // Arrange
        var json = Spec("[{\"u\":1,\"v\":2},{\"u\":3,\"v\":4},{\"u\":5,\"v\":6}]", "[{\"fold\":[\"u\",\"v\"]}]");

        // Act
        var ok = Run(json, out _, new VegaLiteLimits { MaxDataRows = 9 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(json, out _, new VegaLiteLimits { MaxDataRows = 8 }));

        // Assert
        Assert.Equal(6, ok.RowCount);
        Assert.Equal("/transform/0", ex.Location.Pointer);
        Assert.Contains("fold transform output has 6 rows", ex.Message, StringComparison.Ordinal);
        Assert.Contains("with the 3 rows read before", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that flatten counts its output rows before it allocates them, and that rows given back by a filter make room.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FlattenOutputIsBoundedByTheRowLimitAndFiltersReleaseRows()
    {
        // Arrange: two rows of three elements make six rows with two loaded
        const string Tags = "[{\"id\":1,\"t\":[1,2,3]},{\"id\":2,\"t\":[4,5,6]}]";
        var json = Spec(Tags, "[{\"flatten\":[\"t\"]}]");
        var afterFilter = Spec(Tags, "[{\"filter\":\"datum.id === 1\"},{\"flatten\":[\"t\"]}]");

        // Act
        var ok = Run(json, out _, new VegaLiteLimits { MaxDataRows = 8 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(json, out _, new VegaLiteLimits { MaxDataRows = 7 }));
        var released = Run(afterFilter, out _, new VegaLiteLimits { MaxDataRows = 4 });

        // Assert: the filter gave one row back, so three output rows fit beside the one that is left
        Assert.Equal(6, ok.RowCount);
        Assert.Contains("flatten transform output", ex.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", ex.Location.Pointer);
        Assert.Equal(["1", "2", "3"], Rows(released, "t"));
    }

    /// <summary>
    ///     Validates the field, string and step limits of the transforms and the cell bound of a derived table.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FieldStringAndStepLimitsAreLocatedFailures()
    {
        // Arrange
        var fields = Spec("[{\"a\":1,\"b\":2}]", "[{\"calculate\":\"1\",\"as\":\"c\"},{\"calculate\":\"2\",\"as\":\"d\"}]");
        var text = Spec("[{\"s\":\"abcdefghij\"}]", "[{\"calculate\":\"datum.s+datum.s+datum.s\",\"as\":\"t\"}]");
        var steps = Spec("[{\"a\":1},{\"a\":2},{\"a\":3}]", "[{\"calculate\":\"datum.a + datum.a + datum.a\",\"as\":\"t\"}]");

        // Act
        var fieldEx = Assert.Throws<VegaLiteRenderException>(() => Run(fields, out _, new VegaLiteLimits { MaxFieldCount = 3 }));
        var stringEx = Assert.Throws<VegaLiteRenderException>(() => Run(text, out _, new VegaLiteLimits { MaxStringLength = 24 }));
        var stepEx = Assert.Throws<VegaLiteRenderException>(() => Run(steps, out _, new VegaLiteLimits { MaxEvaluationSteps = 10 }));

        // Assert
        Assert.Equal("/transform/1", fieldEx.Location.Pointer);
        Assert.Contains("MaxFieldCount", fieldEx.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0/calculate", stringEx.Location.Pointer);
        Assert.Contains("expression offset", stringEx.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0/calculate", stepEx.Location.Pointer);

        // Assert: a table of more than the cell bound is refused before it is allocated
        var table = DataTable.Create(["a"], [[DataValue.FromNumber(1)]]);
        var layout = new TableLayout(table, ["b"], keepInput: true, VegaLiteLimits.Default, default);
        var cells = Assert.Throws<VegaLiteRenderException>(() => layout.Allocate(TableLayout.MaxCells));
        Assert.Contains("cells", cells.Message, StringComparison.Ordinal);
        Assert.Equal(["a", "b"], layout.Fields);
    }

    /// <summary>
    ///     Validates that filter and sample build their result through the table layout of the other transforms: the layout
    ///     of a selection keeps every field of the input and refuses, before it allocates, a result of more than the cell
    ///     bound, reporting the location of the transform.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_FilterAndSampleRouteThroughTheCellBound()
    {
        // Arrange: the layout a selection uses (no added fields), over a table of one field
        var table = DataTable.Create(["a"], [[DataValue.FromNumber(1)]]);
        var location = new VegaLiteLocation(null, "/transform/0/filter", 1, 1);
        var layout = new TableLayout(table, [], keepInput: true, VegaLiteLimits.Default, location);

        // Act
        var atBound = layout.Allocate(0);
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => layout.Allocate(TableLayout.MaxCells + 1));
        var filtered = Run(Spec(Numbers, "[{\"filter\":\"datum.a > 1\"}]"));
        var sampled = Run(Spec(Numbers, "[{\"sample\":2}]"));

        // Assert: refused before allocation, with the location; the results keep every field
        Assert.Empty(atBound);
        Assert.Equal(["a"], layout.Fields);
        Assert.Equal("/transform/0/filter", tooMany.Location.Pointer);
        Assert.Contains("cells", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal(["a", "b"], filtered.Fields);
        Assert.Equal(2, filtered.RowCount);
        Assert.Equal(["a", "b"], sampled.Fields);
        Assert.Equal(2, sampled.RowCount);
    }

    /// <summary>
    ///     Validates that the transforms stop on a canceled token before and between transforms and every batch of rows, and
    ///     that an evaluation under a canceled token throws.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_CanceledTokenStopsTheTransforms()
    {
        // Arrange: 5000 rows and two transforms; the transforms run under a canceled token
        var rows = "[" + string.Join(",", Enumerable.Range(0, 5000).Select(i => "{\"a\":" + i + "}")) + "]";
        var json = Spec(rows, "[{\"calculate\":\"datum.a + 1\",\"as\":\"b\"},{\"filter\":\"datum.b > 1\"}]");

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => Run(json, out _, canceled: true));
        Assert.Equal(4999, Run(json).RowCount);
    }

    /// <summary>
    ///     Validates the argument checks and the identity of an input without transforms.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_ChecksArgumentsAndReturnsTheInputWithoutTransforms()
    {
        // Arrange
        var tree = SpecParser.Parse(Spec("[{\"a\":1}]", "[]"));
        var loader = new DataLoader(null, null, null, null, null, Ct);
        var table = loader.Load(tree.Root["data"]);
        var evaluator = new ExpressionEvaluator(null, Ct);
        var context = new TransformContext(tree, loader, evaluator, new Dictionary<string, DataValue>(), VegaLiteLimits.Default, Ct, []);

        // Act / Assert
        Assert.Same(table, RowTransforms.Apply(table, tree.Root["transform"], context));
        Assert.Same(table, RowTransforms.Apply(table, null, context));
        Assert.Throws<ArgumentNullException>(() => RowTransforms.Apply(null!, null, context));
        Assert.Throws<ArgumentNullException>(() => RowTransforms.Apply(table, null, null!));
    }

    /// <summary>
    ///     Validates that a transform that a later phase owns (an aggregate) is skipped by the executor and leaves the rows as
    ///     they are.
    /// </summary>
    [Fact]
    public void RowTransforms_Apply_TransformsOfLaterPhasesAreSkipped()
    {
        // Arrange / Act
        var table = Run(Spec(Numbers, "[{\"aggregate\":[{\"op\":\"count\",\"as\":\"n\"}]},{\"calculate\":\"datum.a\",\"as\":\"z\"}]"));

        // Assert: only the calculate ran
        Assert.Equal(["a", "b", "z"], table.Fields);
        Assert.Equal(3, table.RowCount);
    }

    /// <summary>
    ///     Validates that a discrete sort by an aggregate of a field is resolved against the transformed rows, is stable for
    ///     equal aggregates, supports the order, and that an unsupported form keeps the ascending default.
    /// </summary>
    /// <param name="sort">The text of the <c>sort</c> property.</param>
    /// <param name="expected">The expected domain, joined with a comma.</param>
    [Theory]
    [InlineData("{\"op\":\"sum\",\"field\":\"w\"}", "c,a,b")]
    [InlineData("{\"op\":\"sum\",\"field\":\"w\",\"order\":\"descending\"}", "b,a,c")]
    [InlineData("{\"op\":\"count\"}", "b,c,a")]
    [InlineData("{\"op\":\"max\",\"field\":\"w\"}", "c,a,b")]
    [InlineData("{\"op\":\"min\",\"field\":\"w\",\"order\":\"descending\"}", "b,a,c")]
    [InlineData("{\"op\":\"mean\",\"field\":\"w\"}", "c,a,b")]
    [InlineData("{\"op\":\"average\",\"field\":\"w\",\"order\":\"descending\"}", "b,a,c")]
    [InlineData("{\"op\":\"median\",\"field\":\"w\"}", "c,a,b")]
    [InlineData("{\"op\":\"valid\",\"field\":\"w\"}", "a,b,c")]
    [InlineData("{\"encoding\":\"x\"}", "a,b,c")]
    public void ScaleResolver_Resolve_SortByFieldUsesTheTransformedRows(string sort, string expected)
    {
        // Arrange: the rows of a (2 and 3), b (10) and c (1) get their weight w from a calculate; the domain starts in the
        // order of first appearance (a, b, c) and is then sorted by the aggregate of w
        var json = "{\"data\":{\"values\":[{\"k\":\"a\",\"p\":2,\"q\":0},{\"k\":\"b\",\"p\":10,\"q\":0},{\"k\":\"a\",\"p\":0,\"q\":3},{\"k\":\"c\",\"p\":1,\"q\":0}]},"
            + "\"transform\":[{\"calculate\":\"datum.p + datum.q\",\"as\":\"w\"}],\"mark\":\"bar\","
            + "\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"sort\":" + sort + "},\"y\":{\"field\":\"w\",\"type\":\"quantitative\"}}}";
        var tree = SpecParser.Parse(json);
        var loader = new DataLoader(null, null, null, null, null, Ct);
        var values = new Dictionary<string, DataValue>();
        var evaluator = new ExpressionEvaluator(null, Ct, values);
        var transformed = RowTransforms.Apply(loader.Load(tree.Root["data"]), tree.Root["transform"], new TransformContext(tree, loader, evaluator, values, VegaLiteLimits.Default, Ct, []));

        // Act
        var scales = ScaleResolver.Resolve(tree.Root, transformed, ConfigResolver.Resolve(tree, null), null, null, Ct);

        // Assert: the aggregate of a is 5 (sum), 3 (max), 2 (min), 2.5 (mean and median) and 2 (count); b is 10 and 1; c is 1 and 1
        Assert.Equal(expected, string.Join(",", scales["x"]!.DiscreteDomain.Select(v => v.ToText())));
    }

    /// <summary>
    ///     Validates that categories with equal aggregates keep the order of first appearance in both directions, and that the
    ///     comparison does not depend on the culture.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_SortByFieldIsStableForTies()
    {
        // Arrange: every category has the same sum
        const string Rows3 = "[{\"k\":\"z\",\"w\":1},{\"k\":\"m\",\"w\":1},{\"k\":\"a\",\"w\":1}]";
        string Domain(string order)
        {
            var tree = SpecParser.Parse("{\"data\":{\"values\":" + Rows3 + "},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"sort\":{\"op\":\"sum\",\"field\":\"w\"" + order + "}}}}");
            var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
            var scales = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), null, null, Ct);
            return string.Join(",", scales["x"]!.DiscreteDomain.Select(v => v.ToText()));
        }

        // Act / Assert: the data order, not the alphabet, decides among ties
        Assert.Equal("z,m,a", Domain(string.Empty));
        Assert.Equal("z,m,a", Domain(",\"order\":\"descending\""));
        Assert.True(ScaleResolver.IsSupportedFieldSort(SpecParser.Parse("{\"s\":{\"op\":\"sum\"}}").Root["s"]!));
        Assert.False(ScaleResolver.IsSupportedFieldSort(SpecParser.Parse("{\"s\":{\"op\":\"sum\",\"order\":\"up\"}}").Root["s"]!));
        Assert.False(ScaleResolver.IsSupportedFieldSort(SpecParser.Parse("{\"s\":\"x\"}").Root["s"]!));
    }

    /// <summary>
    ///     Validates that a category without a valid value (a null aggregate) sorts first in both directions for the
    ///     operations that have no value for an empty category, and that only the values reverse.
    /// </summary>
    /// <param name="operation">The aggregate operation.</param>
    [Theory]
    [InlineData("mean")]
    [InlineData("average")]
    [InlineData("median")]
    [InlineData("min")]
    [InlineData("max")]
    public void ScaleResolver_Resolve_SortByFieldPutsCategoriesWithoutValuesFirstInBothDirections(string operation)
    {
        // Arrange: m has no valid number; z has 1 and a has 3
        const string Data = "[{\"k\":\"z\",\"w\":1},{\"k\":\"m\",\"w\":null},{\"k\":\"a\",\"w\":3}]";
        string Domain(string order)
        {
            var tree = SpecParser.Parse("{\"data\":{\"values\":" + Data + "},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"sort\":{\"op\":\"" + operation + "\",\"field\":\"w\"" + order + "}}}}");
            var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
            var scales = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), null, null, Ct);
            return string.Join(",", scales["x"]!.DiscreteDomain.Select(v => v.ToText()));
        }

        // Act / Assert: the null aggregate is first, then the values ascend or descend
        Assert.Equal("m,z,a", Domain(string.Empty));
        Assert.Equal("m,z,a", Domain(",\"order\":\"ascending\""));
        Assert.Equal("m,a,z", Domain(",\"order\":\"descending\""));
    }
}
