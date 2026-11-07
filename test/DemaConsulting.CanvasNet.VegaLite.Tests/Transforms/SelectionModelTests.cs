using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests of <see cref="SelectionModel"/>: the declaration of selections for the unit views, their projection over fields and encodings, the tuples of their initial value, the caller's
///     replacement of the value, the stores and the resolved value of each selection. Each expectation is written out from the rules of Vega-Lite's <c>project.ts</c> and of the selection store.
/// </summary>
public class SelectionModelTests
{
    /// <summary>The three rows that every chart reads.</summary>
    private const string Rows = """ "data": { "values": [ { "a": 1, "b": 1, "d": "2020-01-01" }, { "a": 4, "b": 2, "d": "2020-02-01" } ] } """;

    /// <summary>
    ///     Builds the model of a specification as the document does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="overrides">The values of the caller, or null.</param>
    /// <returns>The model.</returns>
    private static SelectionModel Model(string json, IReadOnlyDictionary<string, object?>? overrides = null)
    {
        var tree = SpecParser.Parse(json);
        var config = ConfigResolver.Resolve(tree, null);
        var layers = SpecNormalizer.Expand(tree, config).Layers;
        return SelectionModel.Create(tree, layers, config, overrides, TimeCalendar.Utc, new ExpressionFunctionContext(null, CancellationToken.None));
    }

    /// <summary>
    ///     Builds a point chart with one selection parameter.
    /// </summary>
    /// <param name="parameter">The parameter object text.</param>
    /// <param name="encoding">The encoding text; an x on <c>a</c> and a y on <c>b</c> when empty.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string parameter, string encoding = "")
    {
        var channels = encoding.Length == 0 ? """ "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } """ : encoding;
        return "{ \"params\": [ " + parameter + " ], " + Rows + ", \"mark\": \"point\", \"encoding\": { " + channels + " } }";
    }

    /// <summary>
    ///     Lists the projection of the first component as <c>field/channel/type</c> texts.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The texts.</returns>
    private static List<string> Projection(SelectionModel model) =>
        [.. model.Components[0].Projection.Select(p => p.Field + "/" + (p.Channel ?? "-") + "/" + p.Type)];

    /// <summary>
    ///     Writes the values of the first tuple of the first component.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The values as texts.</returns>
    private static string Values(SelectionModel model) =>
        string.Join(";", model.Components[0].Init.Select(t => string.Join(",", t.Select(v => v.IsArray ? "[" + string.Join(",", v.Elements.ToArray().Select(e => e.ToText())) + "]" : v.ToText()))));

    /// <summary>
    ///     Validates the default projection of each selection type: an interval over x and y (a range over each linear scale), a point over the identity of the rows.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_DefaultProjectionOfEachType()
    {
        // Act
        var interval = Model(Chart("""{ "name": "brush", "select": "interval" }"""));
        var point = Model(Chart("""{ "name": "pick", "select": "point" }"""));

        // Assert: the interval is projected over a and b as ranges; the point over the identity
        Assert.Equal(["a/x/R", "b/y/R"], Projection(interval));
        Assert.Equal(["_vgsid_/-/E"], Projection(point));
        Assert.True(point.Components[0].UsesRowIdentity);
        Assert.False(interval.Components[0].UsesRowIdentity);
    }

    /// <summary>
    ///     Validates the tuple types: an interval over a discrete axis stores values, a binned field of a point selection stores a range that excludes its end, an interval over a binned continuous axis
    ///     stores a range, and a time unit projects over the derived field (a range over a time axis).
    /// </summary>
    [Fact]
    public void SelectionModel_Create_TupleTypeFollowsTheScaleAndTheBin()
    {
        // Arrange
        const string nominalX = """ "x": { "field": "a", "type": "nominal" } """;
        const string binnedX = """ "x": { "field": "a", "type": "quantitative", "bin": true } """;
        const string timeX = """ "x": { "field": "d", "type": "temporal", "timeUnit": "month" } """;

        // Act
        var discrete = Model(Chart("""{ "name": "s", "select": { "type": "interval", "encodings": ["x"] } }""", nominalX));
        var pointBin = Model(Chart("""{ "name": "s", "select": { "type": "point", "encodings": ["x"] } }""", binnedX));
        var intervalBin = Model(Chart("""{ "name": "s", "select": { "type": "interval", "encodings": ["x"] } }""", binnedX));
        var time = Model(Chart("""{ "name": "s", "select": { "type": "interval", "encodings": ["x"] } }""", timeX));

        // Assert
        Assert.Equal(["a/x/E"], Projection(discrete));
        Assert.Equal(["a/x/R-RE"], Projection(pointBin));
        Assert.Equal(["a/x/R"], Projection(intervalBin));
        Assert.Equal(["month_d/x/R"], Projection(time));
    }

    /// <summary>
    ///     Validates that the keys of the initial value decide the projection when none is written: a key that is a channel is an encoding (the field of that channel), another key is a field of a point selection.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_ProjectionIsInferredFromTheKeysOfTheValue()
    {
        // Act
        var byChannel = Model(Chart("""{ "name": "s", "select": "point", "value": { "x": 4 } }"""));
        var byField = Model(Chart("""{ "name": "s", "select": "point", "value": [ { "b": 2 }, { "b": 1 } ] }"""));
        var both = Model(Chart("""{ "name": "s", "select": "point", "value": { "x": 4, "b": 2 } }"""));

        // Assert: x is the encoding of a; b is a field; the tuples hold the value of each projected field
        Assert.Equal(["a/x/E"], Projection(byChannel));
        Assert.Equal("4", Values(byChannel));
        Assert.Equal(["b/-/E"], Projection(byField));
        Assert.Equal("2;1", Values(byField));
        Assert.Equal(["a/x/E", "b/-/E"], Projection(both));
        Assert.Equal("4,2", Values(both));
    }

    /// <summary>
    ///     Validates that a scalar value applies to every projected field, and that the entry of the channel is preferred over the entry of the field.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_ScalarAndMappingValues()
    {
        // Act
        var scalar = Model(Chart("""{ "name": "s", "select": { "type": "point", "fields": ["a", "b"] }, "value": 3 }"""));
        var preferred = Model(Chart("""{ "name": "s", "select": { "type": "point", "encodings": ["x"] }, "value": { "x": 4, "a": 9 } }"""));

        // Assert
        Assert.Equal("3,3", Values(scalar));
        Assert.Equal("4", Values(preferred));
    }

    /// <summary>
    ///     Validates that a channel without a field or with an aggregate is not projected and is reported, and that an interval initialized with a key that is no channel takes the default encodings.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_UnprojectableChannelsAreReported()
    {
        // Arrange: the size channel has no field; y is aggregated
        const string encoding = """ "x": { "field": "a", "type": "quantitative" }, "y": { "aggregate": "count", "type": "quantitative" }, "size": { "value": 5 } """;

        // Act
        var model = Model(Chart("""{ "name": "s", "select": { "type": "interval", "encodings": ["x", "y", "size", "color"] } }""", encoding));
        var wrongKey = Model(Chart("""{ "name": "t", "select": "interval", "value": { "a": [1, 2] } }"""));

        // Assert: only x is projected, the other three are reported with the id of the projection
        Assert.Equal(["a/x/R"], Projection(model));
        Assert.Equal(3, model.Warnings.Count);
        Assert.All(model.Warnings, w => Assert.Equal(("vl-param-selection-projection", FeatureReportKind.Warning), (w.Id, w.Kind)));
        Assert.Contains("aggregated", model.Warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains("'size'", model.Warnings[1].Message, StringComparison.Ordinal);

        // Assert: the wrong key is reported once and the default encodings x and y are projected; the value of a is not found for them (the tuple has no value for x and y)
        Assert.Equal(["a/x/R", "b/y/R"], Projection(wrongKey));
        Assert.Single(wrongKey.Warnings);
        Assert.Contains("'a'", wrongKey.Warnings[0].Message, StringComparison.Ordinal);
        Assert.Equal("[1,2],null", Values(wrongKey));
    }

    /// <summary>
    ///     Validates the initial value of an interval: the range of each encoding, the date-time objects of the specification as milliseconds in the calendar of the call (UTC), and the stored tuple.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_IntervalValueAndDates()
    {
        // Arrange: the range from 2020-01-02 to 2020-03-01 as date-time objects (the month is written as a number from 1)
        const string encoding = """ "x": { "field": "d", "type": "temporal" } """;
        const string parameter = """{ "name": "s", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [ { "year": 2020, "month": 1, "date": 2 }, { "year": 2020, "month": 3, "date": 1 } ] } }""";

        // Act
        var model = Model(Chart(parameter, encoding));
        var store = model.Stores["s_store"];

        // Assert: 2020-01-02T00:00Z is 1577923200000 ms and 2020-03-01T00:00Z is 1583020800000 ms (the 29 days of February 2020 and 31 of January make 60 days after January 1)
        Assert.Equal("[1577923200000,1583020800000]", Values(model));
        Assert.Single(store.Entries);
        Assert.Equal("[1577923200000,1583020800000]", string.Join(",", model.Values["s"].ObjectValue!.TryGetValue("d", out var resolved) ? "[" + string.Join(",", resolved.Elements.ToArray().Select(e => e.ToText())) + "]" : "-"));
    }

    /// <summary>
    ///     Validates that the caller replaces the initial value: a map of arrays, a list of maps, dates as <see cref="DateTime"/>, and null (which clears the value); the value of the specification is not used.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_CallerValueReplacesTheValueOfTheSpecification()
    {
        // Arrange
        var interval = Chart("""{ "name": "s", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [1, 2] } }""");
        var point = Chart("""{ "name": "p", "select": { "type": "point", "fields": ["a"] }, "value": { "a": 1 } }""");
        var temporal = Chart(
            """{ "name": "s", "select": { "type": "interval", "encodings": ["x"] } }""",
            """ "x": { "field": "d", "type": "temporal" } """);

        // Act
        var map = Model(interval, new Dictionary<string, object?> { ["s"] = new Dictionary<string, object?> { ["x"] = new object[] { 5, 8 } } });
        var list = Model(point, new Dictionary<string, object?> { ["p"] = new object[] { new Dictionary<string, object?> { ["a"] = 4 }, new Dictionary<string, object?> { ["a"] = 7 } } });
        var cleared = Model(interval, new Dictionary<string, object?> { ["s"] = null });
        var dates = Model(temporal, new Dictionary<string, object?>
        {
            ["s"] = new Dictionary<string, object?> { ["x"] = new object[] { new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 3, 1, 0, 0, 0, DateTimeKind.Utc) } },
        });

        // Assert
        Assert.Equal("[5,8]", Values(map));
        Assert.Equal("4;7", Values(list));
        Assert.Empty(cleared.Components[0].Init);
        Assert.Empty(cleared.Stores["s_store"].Entries);
        Assert.Equal("[1577923200000,1583020800000]", Values(dates));
    }

    /// <summary>
    ///     Validates the stores and the resolved value of the selections: each tuple is stored with the fields, and the resolved value of an interval is its range, of a point its values and its query.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_StoresAndResolvedValues()
    {
        // Arrange
        var json = "{ \"params\": [ { \"name\": \"brush\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"value\": { \"x\": [6, 2] } }, "
            + "{ \"name\": \"pick\", \"select\": { \"type\": \"point\", \"fields\": [\"b\"] }, \"value\": [ { \"b\": 1 }, { \"b\": 2 } ] }, { \"name\": \"idle\", \"select\": \"interval\" } ], "
            + Rows + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\" } } }";

        // Act
        var model = Model(json);

        // Assert: the stores, and an empty store for the selection without a value
        Assert.Equal(["brush_store", "pick_store", "idle_store"], model.Stores.Keys);
        Assert.Single(model.Stores["brush_store"].Entries);
        Assert.Equal(2, model.Stores["pick_store"].Entries.Count);
        Assert.Empty(model.Stores["idle_store"].Entries);

        // Assert: the resolved values (the range is sorted, the point has its query, the empty selection is an empty object)
        var brush = model.Values["brush"].ObjectValue!;
        Assert.True(brush.TryGetValue("a", out var range));
        Assert.Equal([2.0, 6.0], range.Elements.ToArray().Select(e => e.Number));
        Assert.Equal(1, brush.Count);
        var pick = model.Values["pick"].ObjectValue!;
        Assert.True(pick.TryGetValue("b", out var values));
        Assert.Equal([1.0, 2.0], values.Elements.ToArray().Select(e => e.Number));
        Assert.True(pick.TryGetValue("vlPoint", out _));
        Assert.Equal(0, model.Values["idle"].ObjectValue!.Count);
    }

    /// <summary>
    ///     Validates that a top-level selection is declared for every unit and its store is that of the first unit, named by its position in the tree; that <c>views</c> narrows the units by name.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_TopLevelSelectionIsDeclaredForEveryUnitAndStoredFromTheFirst()
    {
        // Arrange
        const string unit = """{ "name": "NAME", "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }""";
        string Concat(string views) =>
            "{ \"params\": [ { \"name\": \"s\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"value\": { \"x\": [1, 2] }" + views + " } ], " + Rows
            + ", \"hconcat\": [ " + unit.Replace("NAME", "left", StringComparison.Ordinal) + ", " + unit.Replace("NAME", "right", StringComparison.Ordinal) + " ] }";

        // Act
        var all = Model(Concat(string.Empty));
        var right = Model(Concat(", \"views\": [\"right\"]"));

        // Assert: two components, the store from the first; with views only the right unit declares it
        Assert.Equal([0, 1], all.Components.Select(c => c.UnitIndex));
        Assert.Equal(["left", "right"], all.Components.Select(c => c.UnitName));
        Assert.Equal("left", all.Stores["s_store"].Entries[0].Unit);
        Assert.Single(right.Components);
        Assert.Equal((1, "right"), (right.Components[0].UnitIndex, right.Components[0].UnitName));
        Assert.Equal("right", right.Stores["s_store"].Entries[0].Unit);
    }

    /// <summary>
    ///     Validates that an unnamed unit is named by its position: the first view of a concatenation is <c>concat_0</c>, the second layer of a layered view is <c>layer_1</c>.
    /// </summary>
    [Fact]
    public void SelectionDeclarations_UnitNameOf_NamesUnitsByPosition()
    {
        // Arrange
        const string view = """{ "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }""";
        var concat = SpecNormalizer.Layers(SpecParser.Parse("{ " + Rows + ", \"vconcat\": [ " + view + ", " + view + " ] }"));
        var layer = SpecNormalizer.Layers(SpecParser.Parse("{ " + Rows + ", \"layer\": [ " + view + ", " + view + " ] }"));
        var single = SpecNormalizer.Layers(SpecParser.Parse("{ " + Rows + ", \"mark\": \"point\" }"));

        // Act / Assert
        Assert.Equal(["concat_0", "concat_1"], concat.Units.Select(SelectionDeclarations.UnitNameOf));
        Assert.Equal(["layer_0", "layer_1"], layer.Units.Select(SelectionDeclarations.UnitNameOf));
        Assert.Equal([string.Empty], single.Units.Select(SelectionDeclarations.UnitNameOf));
    }

    /// <summary>
    ///     Validates the selection of a layered view: two layers that receive the same top-level selection are duplicates that are dropped (applied as empty), naming one layer in <c>views</c> is not.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_SelectionOfTwoLayersIsDropped()
    {
        // Arrange
        const string layer = """{ "name": "NAME", "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }""";
        string Layered(string views) =>
            "{ \"params\": [ { \"name\": \"s\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"value\": { \"x\": [1, 2] }" + views + " } ], " + Rows
            + ", \"layer\": [ " + layer.Replace("NAME", "one", StringComparison.Ordinal) + ", " + layer.Replace("NAME", "two", StringComparison.Ordinal) + " ] }";

        // Act
        var both = Model(Layered(string.Empty));
        var one = Model(Layered(", \"views\": [\"two\"]"));

        // Assert: dropped components have no projection and no tuple; the narrowed selection has its tuple
        Assert.All(both.Components, c => Assert.True(c.IsDropped));
        Assert.Empty(both.Stores["s_store"].Entries);
        Assert.Equal(0, both.Values["s"].ObjectValue!.Count);
        Assert.Single(one.Components);
        Assert.False(one.Components[0].IsDropped);
        Assert.Single(one.Stores["s_store"].Entries);
    }

    /// <summary>
    ///     Validates that a selection declared in the parameters of one unit is declared for that unit only, and that a selection of a nested composition reaches the units that follow it.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_UnitSelectionBelongsToItsUnit()
    {
        // Arrange: only the second view declares the selection
        const string json = """
            { "data": { "values": [ { "a": 1 } ] },
              "hconcat": [
                { "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } },
                { "params": [ { "name": "s", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [0, 1] } } ],
                  "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } } ] }
            """;

        // Act
        var model = Model(json);

        // Assert
        Assert.Single(model.Components);
        Assert.Equal(1, model.Components[0].UnitIndex);
        Assert.Equal("concat_1", model.Components[0].UnitName);
    }

    /// <summary>
    ///     Validates the legacy <c>selection</c>: <c>single</c> and <c>multi</c> are point selections, <c>interval</c> is an interval, <c>init</c> is the value, and <c>bind</c> is kept.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_LegacySelectionsAreMapped()
    {
        // Arrange
        const string json = """
            { "selection": {
                "one": { "type": "single", "fields": ["a"], "init": { "a": 4 } },
                "many": { "type": "multi", "fields": ["a"], "init": [ { "a": 1 }, { "a": 4 } ] },
                "range": { "type": "interval", "encodings": ["x"], "init": { "x": [1, 2] }, "bind": "scales" } },
              "data": { "values": [ { "a": 1 } ] }, "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }
            """;

        // Act
        var model = Model(json);

        // Assert
        Assert.Equal([("one", "point"), ("many", "point"), ("range", "interval")], model.Components.Select(c => (c.Name, c.Type)));
        Assert.Single(model.Stores["one_store"].Entries);
        Assert.Equal(2, model.Stores["many_store"].Entries.Count);
        Assert.Equal(SelectionBind.Scales, model.Find("range")!.Bind);
        Assert.Equal(SelectionBind.None, model.Find("one")!.Bind);
    }

    /// <summary>
    ///     Validates that the kind of the binding is kept (a legend binding, a widget binding), that the resolve of a selection comes from the selection or the configuration, and that the model of a specification
    ///     without selection is empty.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_KeepsBindAndResolve()
    {
        // Act
        var legend = Model(Chart("""{ "name": "s", "select": { "type": "point", "fields": ["a"], "resolve": "intersect" }, "bind": "legend" }"""));
        var widget = Model(Chart("""{ "name": "s", "select": { "type": "point", "fields": ["a"] }, "bind": { "a": { "input": "range" } } }"""));
        var configured = Model("""{ "config": { "selection": { "interval": { "resolve": "union" } } }, "params": [ { "name": "s", "select": "interval" } ], "data": { "values": [ { "a": 1 } ] }, "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }""");
        var none = Model("""{ "data": { "values": [ { "a": 1 } ] }, "mark": "point" }""");

        // Assert
        Assert.Equal((SelectionBind.Legend, "intersect"), (legend.Components[0].Bind, legend.Components[0].Resolve));
        Assert.Equal(SelectionBind.Widget, widget.Components[0].Bind);
        Assert.Equal("union", configured.Components[0].Resolve);
        Assert.False(none.HasSelections);
        Assert.Same(SelectionModel.Empty, none);
    }

    /// <summary>
    ///     Validates that a point selection over the identity of the rows has no tuples (it is not modeled), and that a value of the caller for it is reported.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_RowIdentitySelectionHasNoTuples()
    {
        // Arrange
        var json = Chart("""{ "name": "s", "select": "point", "value": 2 }""");

        // Act
        var fromSpecification = Model(json);
        var fromCaller = Model(Chart("""{ "name": "s", "select": "point" }"""), new Dictionary<string, object?> { ["s"] = 2 });

        // Assert: the store is empty either way, and only the caller's value is a warning here (the catalog reports the value of the specification)
        Assert.Empty(fromSpecification.Stores["s_store"].Entries);
        Assert.Empty(fromSpecification.Warnings);
        Assert.Empty(fromCaller.Stores["s_store"].Entries);
        Assert.Equal(["vl-param-selection-id"], fromCaller.Warnings.Select(w => w.Id));
    }

    /// <summary>
    ///     Validates the scan for declarations, which reads the views only.
    /// </summary>
    [Fact]
    public void SelectionModel_Declares_FindsSelectionsInAnyView()
    {
        // Act / Assert
        Assert.True(SelectionModel.Declares(SpecParser.Parse(Chart("""{ "name": "s", "select": "interval" }"""))));
        Assert.True(SelectionModel.Declares(SpecParser.Parse("""{ "data": { "values": [] }, "hconcat": [ { "mark": "point", "selection": { "s": { "type": "single" } } } ] }""")));
        Assert.False(SelectionModel.Declares(SpecParser.Parse(Chart("""{ "name": "v", "value": 1 }"""))));
    }
}
