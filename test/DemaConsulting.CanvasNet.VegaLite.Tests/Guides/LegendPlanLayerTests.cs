using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="LegendPlan"/> for the layers of a layered view: the legend of a channel that several layers share is planned once (the first layer decides the
///     scale, the properties and the facts of the symbols, the titles of the fields join), layers in different scopes keep legends of their own, and a single layer plans exactly what the
///     view overload plans.
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
}
