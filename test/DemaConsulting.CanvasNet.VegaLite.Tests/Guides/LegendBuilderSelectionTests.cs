using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Tests of the legend that a selection is bound to (<c>"bind": "legend"</c>), laid out through the public API: the entries the selection does not hold are drawn with
///     <c>config.legend.unselectedOpacity</c> (0.35 by default), the others with the opacity of the symbol (1 for a label). Every expected opacity is what the rule of Vega-Lite's
///     <c>selectedCondition</c> says for the entries of the legend, worked out by hand.
/// </summary>
public class LegendBuilderSelectionTests
{
    /// <summary>The three categories x, y and z, with a number for the quantitative tests.</summary>
    private const string Rows = """ "data": { "values": [ { "a": 1, "b": 1, "c": "x", "n": 1, "v": 0 }, { "a": 2, "b": 2, "c": "y", "n": 2, "v": 5 }, { "a": 3, "b": 3, "c": "z", "n": 3, "v": 10 } ] } """;

    /// <summary>The selection of the entry y that is bound to the legend.</summary>
    private const string PickY = """ { "name": "pick", "select": { "type": "point", "fields": ["c"] }, "value": [ { "c": "y" } ], "bind": "legend" } """;

    /// <summary>The opacity of the symbols of a legend of point marks: the default opacity of the mark (0.7 for points, which Vega-Lite's legend symbols take over).</summary>
    private const double Mark = 0.7;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays out a point chart whose color is the field <c>c</c> (or the given channels).
    /// </summary>
    /// <param name="parameters">The <c>params</c> array (written out).</param>
    /// <param name="channels">The color channel definitions after <c>"y"</c> (written out), each starting with a comma.</param>
    /// <param name="mark">The mark (written out).</param>
    /// <param name="extra">More top-level properties, each ending with a comma.</param>
    /// <param name="reports">Receives the messages of the reports, or null.</param>
    /// <returns>The scene.</returns>
    private static Scene Layout(string parameters, string channels = """, "color": { "field": "c", "type": "nominal" }""", string mark = "\"point\"", string extra = "", List<string>? reports = null)
    {
        var json = "{ " + extra + "\"params\": " + parameters + ", \"width\": 300, \"height\": 200, " + Rows + ", \"mark\": " + mark + ", "
            + "\"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"b\", \"type\": \"quantitative\" }" + channels + " } }";
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w => reports?.Add(w.Message),
            UnsupportedFeatureHandler = u => reports?.Add(u.Feature + ": " + u.Message),
        };
        var document = VegaLiteDocument.Parse(json);
        document.Measure(options, Ct);
        return document.BuildScene(options, Ct);
    }

    /// <summary>
    ///     Lays out a complete specification.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <returns>The scene.</returns>
    private static Scene LayoutJson(string json)
    {
        var options = new VegaLiteRenderOptions { TimeZone = TimeZoneInfo.Utc };
        var document = VegaLiteDocument.Parse(json);
        document.Measure(options, Ct);
        return document.BuildScene(options, Ct);
    }

    /// <summary>
    ///     Gets the opacity of the symbols of the legend, in entry order.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The opacities.</returns>
    private static List<double> SymbolOpacities(Scene scene) => [.. scene.FindAll("legend-symbol").Select(e => e.Item.Opacity)];

    /// <summary>
    ///     Gets the opacity of the labels of the legend, in entry order.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The opacities.</returns>
    private static List<double> LabelOpacities(Scene scene) => [.. scene.FindAll("legend-label").Select(e => e.Item.Opacity)];

    /// <summary>
    ///     Validates that the entries the selection does not hold are dimmed to 0.35 (symbols and labels) and the selected entry keeps its opacity: the entries are x, y and z, the selection holds y.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_DimsTheEntriesTheSelectionDoesNotHold()
    {
        // Act
        var scene = Layout("[" + PickY + "]");

        // Assert: x and z are dimmed, y is not
        Assert.Equal([0.35, Mark, 0.35], SymbolOpacities(scene));
        Assert.Equal([0.35, 1, 0.35], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that a selection with several values keeps all of them: x and z are held, y is dimmed.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_HoldsEveryValueOfTheSelection()
    {
        // Act
        var scene = Layout("""[ { "name": "pick", "select": { "type": "point", "fields": ["c"] }, "value": [ { "c": "x" }, { "c": "z" } ], "bind": "legend" } ]""");

        // Assert
        Assert.Equal([Mark, 0.35, Mark], SymbolOpacities(scene));
        Assert.Equal([1, 0.35, 1], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that a selection without an initial value dims nothing: the store is empty (<c>!length(data(store))</c>).
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_EmptyStoreDimsNothing()
    {
        // Act
        var scene = Layout("""[ { "name": "pick", "select": { "type": "point", "fields": ["c"] }, "bind": "legend" } ]""");

        // Assert
        Assert.Equal([Mark, Mark, Mark], SymbolOpacities(scene));
        Assert.Equal([1, 1, 1], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that <c>config.legend.unselectedOpacity</c> is the opacity of the dimmed entries.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_UnselectedOpacityComesFromTheConfiguration()
    {
        // Act
        var scene = Layout("[" + PickY + "]", extra: """ "config": { "legend": { "unselectedOpacity": 0.2 } }, """);

        // Assert
        Assert.Equal([0.2, Mark, 0.2], SymbolOpacities(scene));
        Assert.Equal([0.2, 1, 0.2], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that the selected symbol keeps the opacity of the mark (<c>opacity ?? 1</c>) while the selected label has the opacity 1.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_SelectedSymbolKeepsTheOpacityOfTheMark()
    {
        // Act
        var scene = Layout("[" + PickY + "]", mark: """ { "type": "point", "opacity": 0.6 } """);

        // Assert: the symbol of y has the mark's 0.6, the others the unselected 0.35; the labels are 1 or 0.35
        Assert.Equal([0.35, 0.6, 0.35], SymbolOpacities(scene));
        Assert.Equal([0.35, 1, 0.35], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates the quirk of Vega-Lite's rule: a legend with a <c>symbolOpacity</c> gives the selected symbol the opacity 1 (the rule has no symbol opacity of its own to keep).
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_SymbolOpacityOfTheLegendGivesWayToOne()
    {
        // Act
        var scene = Layout("[" + PickY + "]", """, "color": { "field": "c", "type": "nominal", "legend": { "symbolOpacity": 0.5 } }""");

        // Assert
        Assert.Equal([0.35, 1, 0.35], SymbolOpacities(scene));
    }

    /// <summary>
    ///     Validates that the test is a strict comparison (<c>indexof</c>): the text "2" does not hold the number 2 of the entry, so every entry is dimmed.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_ComparesWithStrictEquality()
    {
        // Act: the entries are the numbers 1, 2 and 3 of an ordinal field, the selection holds the text "2"
        var scene = Layout(
            """[ { "name": "pick", "select": { "type": "point", "fields": ["n"] }, "value": [ { "n": "2" } ], "bind": "legend" } ]""",
            """, "color": { "field": "n", "type": "ordinal" }""");

        // Assert: nothing matches, and the store is not empty
        Assert.Equal([0.35, 0.35, 0.35], SymbolOpacities(scene));
        Assert.Equal([0.35, 0.35, 0.35], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that the number 2 of the selection holds the entry 2 of an ordinal field.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_NumberHoldsTheNumberEntry()
    {
        // Act
        var scene = Layout(
            """[ { "name": "pick", "select": { "type": "point", "fields": ["n"] }, "value": [ { "n": 2 } ], "bind": "legend" } ]""",
            """, "color": { "field": "n", "type": "ordinal" }""");

        // Assert
        Assert.Equal([0.35, Mark, 0.35], SymbolOpacities(scene));
    }

    /// <summary>
    ///     Validates that a selection that is not bound to the legend (no <c>bind</c>, or a resolve other than global) dims nothing, whatever it holds.
    /// </summary>
    /// <param name="selection">The selection (written out).</param>
    [Theory]
    [InlineData("""{ "name": "pick", "select": { "type": "point", "fields": ["c"] }, "value": [ { "c": "y" } ] }""")]
    [InlineData("""{ "name": "pick", "select": { "type": "point", "fields": ["c"], "resolve": "union" }, "value": [ { "c": "y" } ], "bind": "legend" }""")]
    public void LegendBuilder_UnboundSelection_DimsNothing(string selection)
    {
        // Act
        var scene = Layout("[" + selection + "]");

        // Assert
        Assert.Equal([Mark, Mark, Mark], SymbolOpacities(scene));
        Assert.Equal([1, 1, 1], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that a selection projected over the channel (<c>encodings</c>) binds the legend of that channel as one projected over its field does.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_ProjectedOverTheChannelBindsTheLegend()
    {
        // Act: the entries are x, y and z; the value is given by the channel name
        var scene = Layout("""[ { "name": "pick", "select": { "type": "point", "encodings": ["color"] }, "value": [ { "color": "z" } ], "bind": "legend" } ]""");

        // Assert: the projected field is c, held value z
        Assert.Equal([0.35, 0.35, Mark], SymbolOpacities(scene));
    }

    /// <summary>
    ///     Validates that the labels of a gradient legend are tested too: the ticks of the field v (0 to 10) are 0, 2, 4, 6, 8 and 10, and the selection holds the value 4.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_DimsTheLabelsOfAGradientLegend()
    {
        // Act
        var scene = Layout(
            """[ { "name": "pick", "select": { "type": "point", "fields": ["v"] }, "value": [ { "v": 4 } ], "bind": "legend" } ]""",
            """, "color": { "field": "v", "type": "quantitative" }""");

        // Assert: one label is selected (the tick 4), the five others are dimmed, and the gradient bar is not dimmed
        var labels = scene.FindAll("legend-label").Select(e => ((SceneText)e.Item)).ToList();
        Assert.Equal(["0", "2", "4", "6", "8", "10"], labels.Select(l => l.Text));
        Assert.Equal([0.35, 0.35, 1, 0.35, 0.35, 0.35], labels.Select(l => l.Opacity));
        Assert.Equal(1, scene.FindAll("legend-gradient").Single().Item.Opacity);
    }

    /// <summary>
    ///     Validates that a symbol legend that also explains the opacity channel keeps its symbols (Vega-Lite drops the symbol rule when the legend encodes opacity itself) while its labels are dimmed.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_LegendOfTheOpacityChannelDimsOnlyTheLabels()
    {
        // Arrange: the same legend without the selection
        var channels = """, "color": { "field": "c", "type": "nominal" }, "opacity": { "field": "c", "type": "nominal" }""";
        var plain = Layout("[]", channels);

        // Act
        var scene = Layout("[" + PickY + "]", channels);

        // Assert: the symbols are those of the legend itself, the labels follow the selection
        Assert.Equal(SymbolOpacities(plain), SymbolOpacities(scene));
        Assert.Equal([0.35, 1, 0.35], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates the quirk of Vega-Lite's rule for a projection with a time unit: the selection stores its values under the derived field (<c>month_d</c>), but the rule reads the field of the
    ///     channel definition (<c>d</c>, <c>selectedCondition</c> in <c>legend/encode.ts</c>), which the resolved selection does not have, so nothing is selected and every entry is dimmed. The selection
    ///     is still projected over the channel, so the legend is bound.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_TimeUnitProjectionDimsEveryEntry()
    {
        // Act: the entries are the months 1, 2 and 3 (the labels Jan, Feb and Mar); the selection holds the month 1 under the field month_d
        var scene = LayoutJson(
            """
            { "width": 300, "height": 200, "data": { "values": [ { "d": "2020-01-15T00:00:00", "v": 1 }, { "d": "2020-02-15T00:00:00", "v": 2 }, { "d": "2020-03-15T00:00:00", "v": 3 } ] },
              "params": [ { "name": "pick", "select": { "type": "point", "encodings": ["color"] }, "value": [ { "color": 1 } ], "bind": "legend" } ],
              "mark": "point", "encoding": { "x": { "field": "v", "type": "quantitative" }, "color": { "field": "d", "type": "ordinal", "timeUnit": "month" } } }
            """);

        // Assert: nothing is selected, though the store is not empty
        Assert.Equal([0.35, 0.35, 0.35], SymbolOpacities(scene));
        Assert.Equal([0.35, 0.35, 0.35], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that the date of a selection (a number of milliseconds, as Vega-Lite stores it) holds the entry of a discrete legend of a date field: the entries of an ordinal field of dates are
    ///     the numbers of milliseconds of the dates.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_DateOfTheSelectionHoldsTheEntryOfTheDate()
    {
        // Act: the entries are the three days; the selection holds the second
        var scene = LayoutJson(
            """
            { "width": 300, "height": 200, "data": { "values": [ { "d": "2020-01-15T00:00:00", "v": 1 }, { "d": "2020-02-15T00:00:00", "v": 2 }, { "d": "2020-03-15T00:00:00", "v": 3 } ], "format": { "parse": { "d": "date" } } },
              "params": [ { "name": "pick", "select": { "type": "point", "fields": ["d"] }, "value": [ { "d": { "year": 2020, "month": 2, "date": 15 } } ], "bind": "legend" } ],
              "mark": "point", "encoding": { "x": { "field": "v", "type": "quantitative" }, "color": { "field": "d", "type": "ordinal" } } }
            """);

        // Assert: only the second entry is selected
        Assert.Equal([0.35, Mark, 0.35], SymbolOpacities(scene));
        Assert.Equal([0.35, 1, 0.35], LabelOpacities(scene));
    }

    /// <summary>
    ///     Validates that the ticks of a time scale are dates, which a selection (it stores the milliseconds of a date as a number) never holds: the entries of a symbol legend of a temporal color are
    ///     the days 1 to 5 of January, the selection holds the third, and everything is dimmed (Vega compares the Date of the tick with the number by strict equality).
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_TickOfATimeScaleIsNotHeldByItsMilliseconds()
    {
        // Act
        var scene = LayoutJson(
            """
            { "width": 300, "height": 200, "data": { "values": [ { "d": "2020-01-01T00:00:00", "v": 1 }, { "d": "2020-01-05T00:00:00", "v": 2 } ] },
              "params": [ { "name": "pick", "select": { "type": "point", "fields": ["d"] }, "value": [ { "d": { "year": 2020, "month": 1, "date": 3 } } ], "bind": "legend" } ],
              "mark": "point", "encoding": { "x": { "field": "v", "type": "quantitative" }, "color": { "field": "d", "type": "temporal", "legend": { "type": "symbol", "tickCount": 5 } } } }
            """);

        // Assert: the third day is a tick, and it is not selected
        Assert.Equal(5, SymbolOpacities(scene).Count);
        Assert.All(SymbolOpacities(scene), o => Assert.Equal(0.35, o));
        Assert.All(LabelOpacities(scene), o => Assert.Equal(0.35, o));
    }

    /// <summary>
    ///     Validates the report of Vega-Lite's <c>LEGEND_BINDINGS_MUST_HAVE_PROJECTION</c>: a selection that is bound to the legend and projects no field names no entry, so nothing is dimmed.
    /// </summary>
    [Fact]
    public void LegendBuilder_BoundSelection_WithoutAProjectionIsReportedAndDimsNothing()
    {
        // Arrange
        var reports = new List<string>();

        // Act: no fields, no encodings, no value: the projection is the identity of the rows
        var scene = Layout("""[ { "name": "pick", "select": "point", "bind": "legend" } ]""", reports: reports);

        // Assert
        Assert.Equal([Mark, Mark, Mark], SymbolOpacities(scene));
        Assert.Contains(reports, r => r.Contains("bound to the legend", StringComparison.Ordinal));
    }
}
