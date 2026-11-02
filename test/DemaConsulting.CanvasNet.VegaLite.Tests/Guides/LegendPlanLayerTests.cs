using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="LegendPlan"/> for the layers of a layered view: the legend of a channel that several layers share is planned once (the first layer decides the
///     scale and the facts of the symbols, the legend properties of all the layers merge as Vega-Lite's <c>mergeLegendComponent</c> does, the titles of the fields join), layers that give
///     different orients or are in different scopes keep legends of their own, and a single layer plans exactly what the view overload plans.
/// </summary>
public class LegendPlanLayerTests
{
    /// <summary>The rows: categories A, B and C with the fields a and b (the same categories under two names), and a value v.</summary>
    private const string Data = """
        "data": { "values": [ { "a": "A", "b": "A", "c": "X", "v": 4 }, { "a": "B", "b": "B", "c": "Y", "v": 8 }, { "a": "C", "b": "C", "c": "X", "v": 6 } ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Plans the legends of a layered specification.
    /// </summary>
    /// <param name="layers">The layers (the text of the elements of the <c>layer</c> array).</param>
    /// <param name="scope">Tells the scope of the legend of a channel for each layer index, or null to share everything.</param>
    /// <param name="top">More top-level properties, each with a trailing comma, or empty.</param>
    /// <returns>The planned legends.</returns>
    private static IReadOnlyList<LegendSpec> Plan(string[] layers, Func<int, string, string>? scope = null, string top = "")
    {
        var tree = SpecParser.Parse("{" + top + Data + ", \"layer\": [" + string.Join(", ", layers) + "]}");
        var config = ConfigResolver.Resolve(tree, null);
        var normalized = SpecNormalizer.Layers(tree);
        var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, Ct);
        var inputs = new List<LayerScaleInput>();
        foreach (var node in normalized.Units)
        {
            var plan = EncodingPlan.Create(node.Unit!, config);
            inputs.Add(new LayerScaleInput(node, plan, EncodingTransforms.ApplyStandalone(plan, table, node.Unit!, null, null, Ct)));
        }

        var scales = ScaleResolver.ResolveLayers(inputs, config, null, null, Ct);
        var legendLayers = new List<LegendLayer>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var index = i;
            legendLayers.Add(new LegendLayer(inputs[i].Node.Unit!, scales.PerLayer[i], scope is null ? null : channel => scope(index, channel)));
        }

        return LegendPlan.Create(legendLayers, config, null);
    }

    /// <summary>
    ///     Validates that layers that color by the same field share one legend with one member and the title of the field.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_SameFieldInTwoLayersIsOneLegend()
    {
        // Arrange / Act
        var legends = Plan([
            """{ "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "color": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "a", "type": "nominal" }, "color": { "field": "a", "type": "nominal" } } }""",
        ]);

        // Assert
        var legend = Assert.Single(legends);
        Assert.Equal(["color"], legend.Members.Select(m => m.Channel));
        Assert.Equal(["a"], legend.Title);
        Assert.Equal("bar", legend.Mark.MarkType);
    }

    /// <summary>
    ///     Validates that the titles of different fields on the shared scale join in layer order, and that an explicit legend title of the first layer wins.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_TitlesOfDifferentFieldsJoin()
    {
        // Arrange / Act
        var joined = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal" } } }""",
            """{ "mark": "tick", "encoding": { "color": { "field": "a", "type": "nominal" } } }""",
        ]);
        var titled = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal", "title": "Mine" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal" } } }""",
        ]);

        // Assert
        Assert.Equal(["a, b"], Assert.Single(joined).Title);
        Assert.Equal(["Mine"], Assert.Single(titled).Title);
    }

    /// <summary>
    ///     Validates that a written title wins over a derived one wherever the layer is (Vega-Lite's explicit-over-implicit merge), for the title of the channel definition and for
    ///     <c>legend.title</c>, and that the derived titles of the other layers do not join it.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_WrittenTitleOfAnyLayerWinsOverDerivedTitles()
    {
        // Arrange / Act: the written title is on the last layer, in the middle, and on the legend object
        var last = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal" } } }""",
            """{ "mark": "tick", "encoding": { "color": { "field": "a", "type": "nominal", "title": "Kind" } } }""",
        ]);
        var middle = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal", "legend": { "title": "Group" } } } }""",
        ]);

        // Assert
        Assert.Equal(["Kind"], Assert.Single(last).Title);
        Assert.Equal(["Group"], Assert.Single(middle).Title);
    }

    /// <summary>
    ///     Validates that different written titles of the layers join as <c>a, b</c> in layer order, that the same written title is one, and that a written <c>null</c> title
    ///     switches the title of the legend off.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_WrittenTitlesJoinWhenDifferentAndMergeWhenEqual()
    {
        // Arrange / Act
        var different = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal", "title": "One" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal", "title": "Two" } } }""",
        ]);
        var same = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal", "title": "One" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal", "title": "One" } } }""",
        ]);
        var off = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "b", "type": "nominal", "title": null } } }""",
        ]);

        // Assert
        Assert.Equal(["One, Two"], Assert.Single(different).Title);
        Assert.Equal(["One"], Assert.Single(same).Title);
        Assert.Null(Assert.Single(off).Title);
    }

    /// <summary>
    ///     Validates that a channel that only a later layer has is planned from that layer, with the facts of its mark.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_ChannelOfALaterLayerUsesThatLayersMark()
    {
        // Arrange / Act
        var legends = Plan([
            """{ "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "c", "type": "nominal" } } }""",
        ]);

        // Assert
        var legend = Assert.Single(legends);
        Assert.Equal("point", legend.Mark.MarkType);
        Assert.Equal(["c"], legend.Title);
    }

    /// <summary>
    ///     Validates that channels of different layers that explain the same domain share one legend, as channels of one view do.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_ChannelsOfDifferentLayersWithTheSameDomainMerge()
    {
        // Arrange / Act: color of the bars and shape of the points, both by the field c
        var legends = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "c", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "shape": { "field": "c", "type": "nominal" } } }""",
        ]);

        // Assert
        var legend = Assert.Single(legends);
        Assert.Equal(["color", "shape"], legend.Members.Select(m => m.Channel));
        Assert.Equal(["c"], legend.Title);
    }

    /// <summary>
    ///     Validates that layers in different scopes have a legend each, in layer order, and that a scope can differ by channel.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_LayersInDifferentScopesKeepTheirOwnLegends()
    {
        // Arrange
        string[] layers =
        [
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" }, "size": { "field": "v", "type": "quantitative" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "a", "type": "nominal" }, "size": { "field": "v", "type": "quantitative" } } }""",
        ];

        // Act: the colors are independent, the sizes are shared
        var legends = Plan(layers, (layer, channel) => channel == "color" ? "L" + layer : string.Empty);

        // Assert: a legend for each layer's color and one for the shared size, in the order of the first channel
        Assert.Equal(["color", "color", "size"], legends.Select(l => l.Members[0].Channel));
        Assert.Equal(["bar", "point", "bar"], legends.Select(l => l.Mark.MarkType));
    }

    /// <summary>
    ///     Validates that a single layer plans exactly what the view overload plans for the same view, and that the legends can be switched off.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_SingleLayerMatchesTheViewOverloadAndDisableIsHonored()
    {
        // Arrange
        const string json = """
            { "data": { "values": [ { "a": "A", "v": 1 }, { "a": "B", "v": 2 } ] }, "mark": "point",
              "encoding": { "color": { "field": "a", "type": "nominal" }, "size": { "field": "v", "type": "quantitative" } } }
            """;
        var parts = ChartTestHelpers.Parts(json);

        // Act
        var view = LegendPlan.Create(parts.Tree.Root, parts.Scales, parts.Config, null);
        var layered = LegendPlan.Create([new LegendLayer(parts.Tree.Root, parts.Scales)], parts.Config, null);
        var disabled = ChartTestHelpers.Parts(json, "{\"legend\":{\"disable\":true}}");

        // Assert
        Assert.Equal(view.Select(l => (string.Join("+", l.Members.Select(m => m.Channel)), l.Title?[0], l.Kind, l.Orient)), layered.Select(l => (string.Join("+", l.Members.Select(m => m.Channel)), l.Title?[0], l.Kind, l.Orient)));
        Assert.Equal(2, layered.Count);
        Assert.Empty(LegendPlan.Create([new LegendLayer(disabled.Tree.Root, disabled.Scales)], disabled.Config, null));
        Assert.Empty(LegendPlan.Create([], parts.Config, null));
        Assert.Throws<ArgumentNullException>(() => LegendPlan.Create((IReadOnlyList<LegendLayer>)null!, parts.Config, null));
        Assert.Throws<ArgumentNullException>(() => LegendPlan.Create([new LegendLayer(parts.Tree.Root, parts.Scales)], null!, null));
    }

    /// <summary>
    ///     Validates Vega-Lite's merge of the properties of the legends of layers: a property that a later layer gives wins over the silence of the first layer, and when several
    ///     layers give a property differently the first layer's value wins, while two explicit types that differ give symbols (<c>mergeLegendComponent</c>).
    /// </summary>
    [Fact]
    public void LegendPlan_Create_PropertiesOfALaterLayerAreKeptWhenTheFirstIsSilent()
    {
        // Arrange / Act: the first layer is silent, the second gives the orient and the values; two layers give the number of columns differently; two layers give different types
        var later = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "a", "type": "nominal", "legend": { "orient": "bottom", "values": ["A", "B"] } } } }""",
        ]);
        var columns = Plan([
            """{ "mark": "bar", "encoding": { "color": { "field": "a", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "color": { "field": "a", "type": "nominal", "legend": { "columns": 2 } } } }""",
            """{ "mark": "tick", "encoding": { "color": { "field": "a", "type": "nominal", "legend": { "columns": 3 } } } }""",
        ]);
        string Continuous(string type) => $$"""{ "mark": "point", "encoding": { "color": { "field": "v", "type": "quantitative", "legend": { "type": "{{type}}" } } } }""";
        var gradient = Plan([Continuous("gradient"), """{ "mark": "bar", "encoding": { "color": { "field": "v", "type": "quantitative" } } }"""]);
        var different = Plan([Continuous("gradient"), Continuous("symbol")]);

        // Assert: the orient and values of the later layer, with the mark of the first layer; the first layer's columns; symbols for different types
        var legend = Assert.Single(later);
        Assert.Equal("bottom", legend.Orient);
        Assert.True(legend.ExplicitOrient);
        Assert.False(legend.Vertical);
        Assert.Equal(2, legend.Settings.ExplicitNode("values")?.Items.Count);
        Assert.Equal("bar", legend.Mark.MarkType);
        Assert.Equal(2, Assert.Single(columns).Settings.Number("columns"));
        Assert.Equal(LegendKind.Gradient, Assert.Single(gradient).Kind);
        Assert.Equal(LegendKind.Symbol, Assert.Single(different).Kind);
    }

    /// <summary>
    ///     Validates that layers that give different <c>orient</c>s explicitly are not merged (Vega-Lite makes the legend of the channel independent: a legend for each layer, each with
    ///     the mark of its layer), while an explicit orient with a silent layer or the same orient in both layers is one legend.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_ExplicitOrientConflictMakesTheLegendsIndependent()
    {
        // Arrange
        string Layer(string mark, string orient)
        {
            var legend = orient.Length == 0 ? string.Empty : ", \"legend\": { \"orient\": \"" + orient + "\" }";
            return "{ \"mark\": \"" + mark + "\", \"encoding\": { \"color\": { \"field\": \"a\", \"type\": \"nominal\"" + legend + " } } }";
        }

        // Act
        var conflict = Plan([Layer("bar", "left"), Layer("point", "right")]);
        var conflictAfterSilent = Plan([Layer("bar", string.Empty), Layer("point", "left"), Layer("tick", "right")]);
        var silent = Plan([Layer("bar", string.Empty), Layer("point", "left")]);
        var same = Plan([Layer("bar", "left"), Layer("point", "left")]);

        // Assert
        Assert.Equal(["left", "right"], conflict.Select(l => l.Orient));
        Assert.Equal(["bar", "point"], conflict.Select(l => l.Mark.MarkType));
        Assert.Equal(["a", "a"], conflict.Select(l => l.Title![0]));
        Assert.Equal(["right", "left", "right"], conflictAfterSilent.Select(l => l.Orient));
        Assert.Equal(["bar", "point", "tick"], conflictAfterSilent.Select(l => l.Mark.MarkType));
        Assert.Equal("left", Assert.Single(silent).Orient);
        Assert.Equal("left", Assert.Single(same).Orient);
    }


    /// <param name="json">The JSON text of the title value.</param>
    /// <returns>The written title.</returns>
    private static GuideTitle Written(string json) => GuideTitle.FromWritten(SpecParser.Parse("{\"t\": " + json + "}").Root["t"]!);

    /// <summary>
    ///     Validates the merge of two titles that differ in being written or derived: the written title wins in either order and the derived one does not join it, a written title that
    ///     switches the title off or has no text still wins over a derived one.
    /// </summary>
    [Fact]
    public void LegendPlan_GuideTitle_WrittenWinsOverDerivedInEitherOrder()
    {
        // Arrange
        var written = Written("\"Crates\"");
        var derived = GuideTitle.FromField("Mean of crates");

        // Act / Assert
        Assert.Equal(["Crates"], GuideTitle.Merge(written, derived).Resolve());
        Assert.Equal(["Crates"], GuideTitle.Merge(derived, written).Resolve());
        Assert.Null(GuideTitle.Merge(derived, Written("null")).Resolve());
        Assert.Null(GuideTitle.Merge(Written("\"\""), derived).Resolve());
    }

    /// <summary>
    ///     Validates that two derived titles join as the distinct titles in order, and that a derived title without a field adds nothing.
    /// </summary>
    [Fact]
    public void LegendPlan_GuideTitle_DerivedTitlesJoinDistinctInOrder()
    {
        // Arrange
        var a = GuideTitle.FromField("week");
        var b = GuideTitle.FromField("Mean of yield");

        // Act
        var joined = GuideTitle.Merge(GuideTitle.Merge(a, b), a);

        // Assert
        Assert.Equal(["week, Mean of yield"], joined.Resolve());
        Assert.Equal(["week"], GuideTitle.Merge(a, GuideTitle.FromField(null)).Resolve());
        Assert.Null(GuideTitle.Merge(GuideTitle.FromField(string.Empty), GuideTitle.FromField(null)).Resolve());
    }

    /// <summary>
    ///     Validates that two written titles join with a comma, that equal ones merge into one, that a title without text is neutral, that a <c>null</c> title switches the merged
    ///     title off, and that the lines of a written title are kept when it stands alone.
    /// </summary>
    [Fact]
    public void LegendPlan_GuideTitle_WrittenTitlesJoinWhenDifferent()
    {
        // Arrange
        var one = Written("\"One\"");
        var two = Written("[\"Two\", \"lines\"]");

        // Act / Assert
        Assert.Equal(["One, Two, lines"], GuideTitle.Merge(one, two).Resolve());
        Assert.Equal(["One"], GuideTitle.Merge(one, Written("\"One\"")).Resolve());
        Assert.Equal(["One"], GuideTitle.Merge(one, Written("\"\"")).Resolve());
        Assert.Equal(["One"], GuideTitle.Merge(Written("[]"), one).Resolve());
        Assert.Null(GuideTitle.Merge(one, Written("null")).Resolve());
        Assert.Null(GuideTitle.Merge(Written("null"), one).Resolve());
        Assert.Equal(["Two", "lines"], two.Resolve());
        Assert.Equal(["A", "B"], Written("\"A\\nB\"").Resolve());
        Assert.Null(Written("7").Resolve());
    }
}
