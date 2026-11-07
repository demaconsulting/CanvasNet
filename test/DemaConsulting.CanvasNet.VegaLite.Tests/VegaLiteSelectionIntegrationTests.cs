using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of selection parameters in their initial state (Phase 12, selection semantics): charts of three circles (a is 1, 4 and 7; b is 1, 2 and 3) are laid out through the
///     public API and the fill of each circle is checked against what the selection says by hand: an interval selection includes both ends of its range, a point selection matches a value by
///     strict equality, an empty selection selects everything (or nothing with <c>empty: false</c>).
/// </summary>
public class VegaLiteSelectionIntegrationTests
{
    /// <summary>The fill of a circle that is in the selection.</summary>
    private const string Picked = "#d73027";

    /// <summary>The fill of a circle that is not.</summary>
    private const string Other = "#4575b4";

    /// <summary>The three rows of the charts: a is 1, 4 and 7.</summary>
    private const string Rows = """ "data": { "values": [ { "a": 1, "b": 1 }, { "a": 4, "b": 2 }, { "a": 7, "b": 3 } ] } """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a circle chart of the three rows with a color condition on a selection parameter.
    /// </summary>
    /// <param name="parameters">The <c>params</c> array (written out).</param>
    /// <param name="condition">The condition object of the fill (written out), such as <c>{ "param": "brush", "value": "#d73027" }</c>.</param>
    /// <param name="extra">More top-level properties, each ending with a comma.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string parameters, string condition, string extra = "") =>
        "{ " + extra + "\"params\": " + parameters + ", \"width\": 300, \"height\": 200, " + Rows + ", \"mark\": \"circle\", "
        + "\"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\" }, "
        + "\"fill\": { \"condition\": " + condition + ", \"value\": \"" + Other + "\" } } }";

    /// <summary>
    ///     Lays out a chart and returns the scene and the reports of the call.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="parameters">The values of the caller, or null.</param>
    /// <param name="reports">Receives the ids of the unsupported features and warnings, in the order delivered.</param>
    /// <returns>The scene.</returns>
    private static Scene Layout(string json, IReadOnlyDictionary<string, object?>? parameters, List<string> reports)
    {
        var options = new VegaLiteRenderOptions
        {
            Parameters = parameters,
            WarningHandler = w => reports.Add(w.Message),
            UnsupportedFeatureHandler = u => reports.Add(u.Feature + ": " + u.Message),
        };
        var document = VegaLiteDocument.Parse(json);
        document.Measure(options, Ct);
        return document.BuildScene(options, Ct);
    }

    /// <summary>
    ///     Gets the fill of the circles of a scene, in row order.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The fills.</returns>
    private static List<string?> Fills(Scene scene) => [.. scene.FindAll("mark-circle").Select(e => ((SceneSymbol)e.Item).Fill)];

    /// <summary>
    ///     Validates that an interval selection includes both ends of its range: the circles at 1 and 4 are in [1, 4] and the circle at 7 is not.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_IntervalIncludesBothEnds()
    {
        // Arrange: x projected over the field a, range 1 to 4
        var json = Chart(
            """[ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [1, 4] } } ]""",
            """{ "param": "brush", "value": "#d73027" }""");
        var reports = new List<string>();

        // Act
        var fills = Fills(Layout(json, null, reports));

        // Assert: 1 and 4 are on the ends, 7 is outside; nothing is reported
        Assert.Equal([Picked, Picked, Other], fills);
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a reversed range selects what the sorted range does: the ends are swapped.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_ReversedIntervalIsSwapped()
    {
        // Arrange
        var json = Chart(
            """[ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [7, 4] } } ]""",
            """{ "param": "brush", "value": "#d73027" }""");

        // Act
        var fills = Fills(Layout(json, null, []));

        // Assert: the range is [4, 7]
        Assert.Equal([Other, Picked, Picked], fills);
    }

    /// <summary>
    ///     Validates the <c>empty</c> rule: without an initial value every circle is in the selection by default, and none is with <c>empty: false</c>.
    /// </summary>
    /// <param name="empty">The text of the <c>empty</c> property of the condition.</param>
    /// <param name="selected">Whether the circles are in the selection.</param>
    [Theory]
    [InlineData("", true)]
    [InlineData(", \"empty\": true", true)]
    [InlineData(", \"empty\": false", false)]
    public void CanvasNetVegaLite_SystemIntegration_Selection_EmptySelectionFollowsTheEmptyRule(string empty, bool selected)
    {
        // Arrange: an interval selection with no value
        var json = Chart(
            """[ { "name": "brush", "select": "interval" } ]""",
            "{ \"param\": \"brush\"" + empty + ", \"value\": \"#d73027\" }");

        // Act
        var fills = Fills(Layout(json, null, []));

        // Assert
        Assert.Equal(Enumerable.Repeat(selected ? Picked : Other, 3), fills);
    }

    /// <summary>
    ///     Validates that a point selection matches a value by strict equality: the number 4 selects the circle at 4, and the text "4" selects nothing (so with a non-empty store nothing is in the selection).
    /// </summary>
    /// <param name="value">The initial value.</param>
    /// <param name="expected">The expected fills.</param>
    [Theory]
    [InlineData("""[ { "a": 4 } ]""", new[] { Other, Picked, Other })]
    [InlineData("""[ { "a": 1 }, { "a": 7 } ]""", new[] { Picked, Other, Picked })]
    [InlineData("""[ { "a": "4" } ]""", new[] { Other, Other, Other })]
    public void CanvasNetVegaLite_SystemIntegration_Selection_PointMatchesByStrictEquality(string value, string[] expected)
    {
        // Arrange: a point selection over the field a
        var json = Chart(
            """[ { "name": "pick", "select": { "type": "point", "fields": ["a"] }, "value": """ + value + " } ]",
            """{ "param": "pick", "value": "#d73027" }""");

        // Act
        var fills = Fills(Layout(json, null, []));

        // Assert
        Assert.Equal(expected, fills);
    }

    /// <summary>
    ///     Validates that the projection is inferred from the keys of the initial value: a key that is not a channel is a field of a point selection, a key that is a channel is an encoding.
    /// </summary>
    /// <param name="select">The <c>select</c> value.</param>
    /// <param name="value">The initial value.</param>
    [Theory]
    [InlineData("\"point\"", """{ "a": 4 }""")]
    [InlineData("\"point\"", """{ "x": 4 }""")]
    [InlineData("\"interval\"", """{ "x": [3, 5] }""")]
    public void CanvasNetVegaLite_SystemIntegration_Selection_ProjectionIsInferredFromTheValue(string select, string value)
    {
        // Arrange: only the circle at 4 is in the selection
        var json = Chart(
            "[ { \"name\": \"pick\", \"select\": " + select + ", \"value\": " + value + " } ]",
            """{ "param": "pick", "value": "#d73027" }""");
        var reports = new List<string>();

        // Act
        var fills = Fills(Layout(json, null, reports));

        // Assert
        Assert.Equal([Other, Picked, Other], fills);
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the filter transform on a selection: the rows in the selection stay (the circles at 4 and 7), and the others are dropped.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_FilterKeepsTheSelectedRows()
    {
        // Arrange: the selection [4, 7] filters the data
        var json = Chart(
            """[ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [4, 7] } } ]""",
            """{ "param": "brush", "value": "#d73027" }""",
            """ "transform": [ { "filter": { "param": "brush" } } ], """);

        // Act
        var scene = Layout(json, null, []);

        // Assert: two circles remain; the x axis then has the domain [0, 7] over 300 pixels, so they are at 4 * 300 / 7 and 300
        var circles = scene.FindAll("mark-circle").Select(e => (SceneSymbol)e.Item).ToList();
        Assert.Equal(2, circles.Count);
        Assert.Equal(1200.0 / 7, circles[0].X, 9);
        Assert.Equal(300.0, circles[1].X, 9);
    }

    /// <summary>
    ///     Validates that a filter on an empty selection with <c>empty: false</c> drops every row and with the default keeps them all.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_FilterOfAnEmptySelectionFollowsTheEmptyRule()
    {
        // Arrange
        const string selection = """[ { "name": "brush", "select": "interval" } ]""";
        var keepAll = Chart(selection, """{ "param": "brush", "value": "#d73027" }""", """ "transform": [ { "filter": { "param": "brush" } } ], """);
        var keepNone = Chart(selection, """{ "param": "brush", "value": "#d73027" }""", """ "transform": [ { "filter": { "param": "brush", "empty": false } } ], """);

        // Act
        var all = Layout(keepAll, null, []).FindAll("mark-circle").Count();
        var none = Layout(keepNone, null, []).FindAll("mark-circle").Count();

        // Assert
        Assert.Equal(3, all);
        Assert.Equal(0, none);
    }

    /// <summary>
    ///     Validates the logical compositions of a <c>test</c> with a selection parameter: <c>not</c> inverts it, and <c>and</c> with a field predicate narrows it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_TestCompositionsUseTheSelection()
    {
        // Arrange: the selection [1, 4] and the tests not(brush), and brush and a > 1
        const string selection = """[ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [1, 4] } } ]""";
        var negated = Chart(selection, """{ "test": { "not": { "param": "brush" } }, "value": "#d73027" }""");
        var narrowed = Chart(selection, """{ "test": { "and": [ { "param": "brush" }, { "field": "a", "gt": 1 } ] }, "value": "#d73027" }""");

        // Act
        var negatedFills = Fills(Layout(negated, null, []));
        var narrowedFills = Fills(Layout(narrowed, null, []));

        // Assert
        Assert.Equal([Other, Other, Picked], negatedFills);
        Assert.Equal([Other, Picked, Other], narrowedFills);
    }

    /// <summary>
    ///     Validates that the caller replaces the initial value of a selection: the interval [4, 7] given as a map of arrays selects the circles at 4 and 7, a null clears it, and the specification's value is not used.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_CallerValueReplacesTheInitialValue()
    {
        // Arrange: the specification says [1, 4]
        var json = Chart(
            """[ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [1, 4] } } ]""",
            """{ "param": "brush", "value": "#d73027" }""");
        var replaced = new Dictionary<string, object?> { ["brush"] = new Dictionary<string, object?> { ["x"] = new object[] { 4, 7 } } };
        var cleared = new Dictionary<string, object?> { ["brush"] = null };

        // Act
        var replacedFills = Fills(Layout(json, replaced, []));
        var clearedFills = Fills(Layout(json, cleared, []));

        // Assert: [4, 7]; an empty selection selects everything
        Assert.Equal([Other, Picked, Picked], replacedFills);
        Assert.Equal([Picked, Picked, Picked], clearedFills);
    }

    /// <summary>
    ///     Validates that a selection parameter is a value for expressions: the resolved selection <c>{a: [1, 4]}</c> is read by a variable parameter and an expression reference.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_ExpressionsReadTheResolvedSelection()
    {
        // Arrange: the title is 'From 1 to 4'
        var json = Chart(
            """[ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [1, 4] } }, { "name": "lo", "expr": "brush.a[0]" } ]""",
            """{ "param": "brush", "value": "#d73027" }""",
            """ "title": { "text": { "expr": "'From ' + lo + ' to ' + brush.a[1]" } }, """);

        // Act
        var scene = Layout(json, null, []);

        // Assert
        var title = Assert.IsType<SceneText>(scene.Find("title-text")!.Value.Item);
        Assert.Equal("From 1 to 4", title.Text);
    }

    /// <summary>
    ///     Validates the legacy <c>selection</c> property: a <c>single</c> selection with an <c>init</c> and a condition that names it with <c>selection</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_LegacySelectionIsMappedToAParameter()
    {
        // Arrange
        const string json = """
            { "selection": { "pick": { "type": "single", "fields": ["a"], "init": { "a": 4 } } },
              "width": 300, "height": 200,
              "data": { "values": [ { "a": 1, "b": 1 }, { "a": 4, "b": 2 }, { "a": 7, "b": 3 } ] }, "mark": "circle",
              "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" },
                "fill": { "condition": { "selection": "pick", "value": "#d73027" }, "value": "#4575b4" } } }
            """;
        var reports = new List<string>();

        // Act
        var fills = Fills(Layout(json, null, reports));

        // Assert
        Assert.Equal([Other, Picked, Other], fills);
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a legacy selection with <c>empty: "none"</c> selects nothing while it is empty.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_LegacyEmptyNoneSelectsNothing()
    {
        // Arrange
        const string json = """
            { "selection": { "pick": { "type": "multi", "fields": ["a"], "empty": "none" } },
              "width": 300, "height": 200,
              "data": { "values": [ { "a": 1, "b": 1 }, { "a": 4, "b": 2 } ] }, "mark": "circle",
              "encoding": { "x": { "field": "a", "type": "quantitative" },
                "fill": { "condition": { "selection": "pick", "value": "#d73027" }, "value": "#4575b4" } } }
            """;

        // Act
        var fills = Fills(Layout(json, null, []));

        // Assert
        Assert.Equal([Other, Other], fills);
    }

    /// <summary>
    ///     Validates that a point selection without fields or encodings (a row-identity selection) is supported while it is empty, and unsupported, applied as empty, with an initial value.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_RowIdentitySelectionWithAValueIsReported()
    {
        // Arrange
        var plain = Chart("""[ { "name": "pick", "select": "point" } ]""", """{ "param": "pick", "value": "#d73027" }""");
        var withValue = Chart("""[ { "name": "pick", "select": "point", "value": 2 } ]""", """{ "param": "pick", "value": "#d73027" }""");
        var plainReports = new List<string>();
        var valueReports = new List<string>();

        // Act
        var plainFills = Fills(Layout(plain, null, plainReports));
        var valueFills = Fills(Layout(withValue, null, valueReports));

        // Assert: both select everything (an empty selection); only the second is reported
        Assert.Equal([Picked, Picked, Picked], plainFills);
        Assert.Equal([Picked, Picked, Picked], valueFills);
        Assert.Empty(plainReports);
        Assert.Single(valueReports);
        Assert.StartsWith("vl-param-selection-id", valueReports[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the tuple type of an interval over a discrete axis: the values at the two ends are what is stored, so only those two categories are in the selection (not the one between).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_IntervalOverACategoricalAxisSelectsItsEndsOnly()
    {
        // Arrange: three categories, the interval from x to z
        const string json = """
            { "params": [ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": ["x", "z"] } } ],
              "width": 300, "height": 200,
              "data": { "values": [ { "c": "x", "b": 1 }, { "c": "y", "b": 2 }, { "c": "z", "b": 3 } ] }, "mark": "circle",
              "encoding": { "x": { "field": "c", "type": "nominal" }, "y": { "field": "b", "type": "quantitative" },
                "fill": { "condition": { "param": "brush", "value": "#d73027" }, "value": "#4575b4" } } }
            """;

        // Act
        var fills = Fills(Layout(json, null, []));

        // Assert: membership of the two ends
        Assert.Equal([Picked, Other, Picked], fills);
    }

    /// <summary>
    ///     Validates that a selection that a concatenation declares at the top level is one selection for all its views: the store comes from the first view (its fields), and the second view is tested
    ///     against the same field, even though its own x channel is another field.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_TopLevelSelectionOfAConcatIsSharedByItsViews()
    {
        // Arrange: the interval [1, 4] over x, which is the field a in the first view and the field b in the second; both color by the selection
        const string json = """
            { "params": [ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] }, "value": { "x": [1, 4] } } ],
              "data": { "values": [ { "a": 1, "b": 10 }, { "a": 4, "b": 20 }, { "a": 7, "b": 4 } ] },
              "hconcat": [
                { "width": 100, "height": 100, "mark": "circle", "encoding": { "x": { "field": "a", "type": "quantitative" },
                    "fill": { "condition": { "param": "brush", "value": "#d73027" }, "value": "#4575b4" } } },
                { "width": 100, "height": 100, "mark": "square", "encoding": { "x": { "field": "b", "type": "quantitative" },
                    "fill": { "condition": { "param": "brush", "value": "#d73027" }, "value": "#4575b4" } } } ] }
            """;

        // Act
        var scene = Layout(json, null, []);
        var circles = scene.FindAll("mark-circle").Select(e => ((SceneSymbol)e.Item).Fill).ToList();
        var squares = scene.FindAll("mark-square").Select(e => ((SceneSymbol)e.Item).Fill).ToList();

        // Assert: both views test the field a (the first unit's projection): 1 and 4 are in [1, 4]; 7 is not
        Assert.Equal([Picked, Picked, Other], circles);
        Assert.Equal([Picked, Picked, Other], squares);
    }

    /// <summary>
    ///     Validates a temporal interval whose ends are date-time objects: the instants are read in UTC, so the row of 2020-02-01 is inside [2020-01-15, 2020-02-15] and the other two are not; the caller's
    ///     <see cref="DateTime"/> values select the same rows.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_TemporalIntervalSelectsTheRowsInsideTheDates()
    {
        // Arrange
        const string json = """
            { "params": [ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] },
                            "value": { "x": [ { "year": 2020, "month": 1, "date": 15 }, { "year": 2020, "month": 2, "date": 15 } ] } } ],
              "width": 300, "height": 200,
              "data": { "values": [ { "d": "2020-01-01", "b": 1 }, { "d": "2020-02-01", "b": 2 }, { "d": "2020-03-01", "b": 3 } ] }, "mark": "circle",
              "encoding": { "x": { "field": "d", "type": "temporal" }, "y": { "field": "b", "type": "quantitative" },
                "fill": { "condition": { "param": "brush", "value": "#d73027" }, "value": "#4575b4" } } }
            """;
        var caller = new Dictionary<string, object?>
        {
            ["brush"] = new Dictionary<string, object?>
            {
                ["x"] = new object[] { new DateTime(2020, 2, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 3, 20, 0, 0, 0, DateTimeKind.Utc) },
            },
        };

        // Act
        var fromSpecification = Fills(Layout(json, null, []));
        var fromCaller = Fills(Layout(json, caller, []));

        // Assert: the second row only (2020-02-01 is inside Jan 15 to Feb 15); with the caller's range (Feb 10 to Mar 20) the third row only (Mar 1), as Feb 1 is before Feb 10
        Assert.Equal([Other, Picked, Other], fromSpecification);
        Assert.Equal([Other, Other, Picked], fromCaller);
    }

    /// <summary>
    ///     Validates that a condition that holds a field takes the scale and the legend of that field, and that the rows outside the selection take the value of the channel: the point selection on the
    ///     category b gives that row the color of its category (the second of the palette) and the others the gray value.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_FieldConditionColorsTheSelectedRowsByTheirCategory()
    {
        // Arrange
        const string json = """
            { "params": [ { "name": "pick", "select": { "type": "point", "fields": ["c"] }, "value": [ { "c": "b" } ] } ],
              "width": 300, "height": 200,
              "data": { "values": [ { "c": "a", "x": 1 }, { "c": "b", "x": 2 }, { "c": "c", "x": 3 } ] }, "mark": "circle",
              "encoding": { "x": { "field": "x", "type": "quantitative" },
                "fill": { "condition": { "param": "pick", "field": "c", "type": "nominal" }, "value": "#808080" } } }
            """;

        // Act
        var scene = Layout(json, null, []);

        // Assert: the default categorical palette gives the second category the second color; a legend lists the three categories
        Assert.Equal(["#808080", "#f58518", "#808080"], Fills(scene));
        Assert.Equal(3, scene.FindAll("legend-label").Count());
    }

    /// <summary>
    ///     Validates that the interaction properties are ignored without a report: <c>on</c>, <c>clear</c>, <c>toggle</c>, <c>nearest</c>, <c>translate</c>, <c>zoom</c> and the input bindings change nothing in the chart.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_InteractionPropertiesAreIgnoredWithoutAReport()
    {
        // Arrange: the same point selection with and without interaction properties and a binding
        const string plain = """[ { "name": "pick", "select": { "type": "point", "fields": ["a"] }, "value": [ { "a": 4 } ] } ]""";
        const string busy = """
            [ { "name": "pick", "select": { "type": "point", "fields": ["a"], "on": "dblclick", "clear": false, "toggle": true, "nearest": true },
                "value": [ { "a": 4 } ], "bind": { "a": { "input": "range", "min": 1, "max": 7 } } } ]
            """;
        var reports = new List<string>();

        // Act
        var expected = Fills(Layout(Chart(plain, """{ "param": "pick", "value": "#d73027" }"""), null, reports));
        var actual = Fills(Layout(Chart(busy, """{ "param": "pick", "value": "#d73027" }"""), null, reports));

        // Assert
        Assert.Equal([Other, Picked, Other], expected);
        Assert.Equal(expected, actual);
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that the selection of a faceted unit view is one selection for all the cells: each cell colors its rows by the same point selection.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_FacetCellsShareTheSelection()
    {
        // Arrange: the value 4 is in both cells
        const string json = """
            { "params": [ { "name": "pick", "select": { "type": "point", "fields": ["a"] }, "value": [ { "a": 4 } ] } ],
              "width": 100, "height": 100,
              "data": { "values": [ { "g": "u", "a": 1, "b": 1 }, { "g": "u", "a": 4, "b": 2 }, { "g": "v", "a": 4, "b": 3 }, { "g": "v", "a": 7, "b": 4 } ] }, "mark": "circle",
              "encoding": { "column": { "field": "g", "type": "nominal" }, "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" },
                "fill": { "condition": { "param": "pick", "value": "#d73027" }, "value": "#4575b4" } } }
            """;
        var reports = new List<string>();

        // Act
        var fills = Fills(Layout(json, null, reports));

        // Assert: cell u holds the rows 1 and 4, cell v the rows 4 and 7
        Assert.Equal([Other, Picked, Picked, Other], fills);
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a legacy selection is a parameter for the caller too: its name is listed and a value of the caller replaces its <c>init</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_CallerCanReplaceTheInitOfALegacySelection()
    {
        // Arrange
        const string json = """
            { "selection": { "pick": { "type": "single", "fields": ["a"], "init": { "a": 4 } } },
              "width": 300, "height": 200,
              "data": { "values": [ { "a": 1, "b": 1 }, { "a": 4, "b": 2 }, { "a": 7, "b": 3 } ] }, "mark": "circle",
              "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" },
                "fill": { "condition": { "selection": "pick", "value": "#d73027" }, "value": "#4575b4" } } }
            """;
        var caller = new Dictionary<string, object?> { ["pick"] = new Dictionary<string, object?> { ["a"] = 1 } };

        // Act
        var document = VegaLiteDocument.Parse(json);
        var fills = Fills(Layout(json, caller, []));

        // Assert
        Assert.Equal(["pick"], document.ParameterNames);
        Assert.Equal([Picked, Other, Other], fills);
    }

    /// <summary>
    ///     Validates that a top-level selection of a layered view with two units is reported and applied as empty; naming one layer in <c>views</c> makes it valid.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Selection_TopLevelSelectionOfTwoLayersIsReported()
    {
        // Arrange: a layer of two circle views; the selection [1, 4] would select the first two circles
        string Layered(string views) =>
            "{ \"params\": [ { \"name\": \"brush\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\"] }, \"value\": { \"x\": [1, 4] }" + views + " } ], \"width\": 300, \"height\": 200, "
            + Rows + ", \"layer\": [ { \"name\": \"first\", \"mark\": \"circle\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, "
            + "\"fill\": { \"condition\": { \"param\": \"brush\", \"value\": \"#d73027\" }, \"value\": \"#4575b4\" } } }, "
            + "{ \"mark\": \"square\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } } ] }";
        var rejected = new List<string>();
        var accepted = new List<string>();

        // Act
        var rejectedFills = Fills(Layout(Layered(string.Empty), null, rejected));
        var acceptedFills = Fills(Layout(Layered(", \"views\": [\"first\"]"), null, accepted));

        // Assert: applied as an empty selection, everything is selected, and the report names the feature
        Assert.Equal([Picked, Picked, Picked], rejectedFills);
        Assert.Contains(rejected, r => r.StartsWith("vl-param-selection-layer", StringComparison.Ordinal));
        Assert.Equal([Picked, Picked, Other], acceptedFills);
        Assert.Empty(accepted);
    }
}
