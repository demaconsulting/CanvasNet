using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="LegendPlan"/> for the views of a composition: the legend of a channel that the children of a concatenation share is owned by the concatenation, a
///     legend that a view makes independent stays with the view below, and a plan for one owner holds only that owner's legends, so a view whose channels have different owners is
///     planned once for each.
/// </summary>
public class LegendPlanCompositionTests
{
    /// <summary>The rows: categories b, a, c, a in the field k, numbers in q and r.</summary>
    private const string Data = """
        "data": { "values": [ { "k": "b", "q": 3, "r": 10 }, { "k": "a", "q": 7, "r": 20 }, { "k": "c", "q": 12, "r": 35 }, { "k": "a", "q": 5, "r": 15 } ] }
        """;

    /// <summary>A point view colored by <c>k</c>.</summary>
    private const string Points = """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""";

    /// <summary>A bar view colored by <c>k</c>.</summary>
    private const string Bars = """{ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""";

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Prepares the legend layers of the unit views of a composition (every view reads the top-level data).
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The layers with their views, the view tree and the configuration.</returns>
    private static (List<LegendLayer> Layers, LayerTree Tree, ResolvedConfig Config) Prepare(string json)
    {
        var tree = SpecParser.Parse(json);
        var config = ConfigResolver.Resolve(tree, null);
        var layers = SpecNormalizer.Layers(tree, null, config);
        var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, Ct);
        var inputs = new List<LayerScaleInput>();
        foreach (var node in layers.Units)
        {
            var plan = EncodingPlan.Create(node.Unit!, config);
            inputs.Add(new LayerScaleInput(node, plan, EncodingTransforms.ApplyStandalone(plan, table, node.Unit!, null, null, Ct)));
        }

        var scales = ScaleResolver.ResolveLayers(inputs, config, null, null, Ct);
        var legendLayers = new List<LegendLayer>();
        for (var i = 0; i < inputs.Count; i++)
        {
            legendLayers.Add(new LegendLayer(inputs[i].Node.Unit!, scales.PerLayer[i], null, inputs[i].Node));
        }

        return (legendLayers, layers, config);
    }

    /// <summary>
    ///     Builds a horizontal concatenation over the rows.
    /// </summary>
    /// <param name="top">More top-level properties, each with a trailing comma, or empty.</param>
    /// <param name="children">The views.</param>
    /// <returns>The specification text.</returns>
    private static string HConcat(string top, params string[] children) =>
        "{" + top + Data + ", \"hconcat\": [" + string.Join(", ", children) + "]}";

    /// <summary>
    ///     Validates that the children of a concatenation that color by the same field share one legend, owned by the concatenation (the highest view that shares the legend), and
    ///     that the legend takes the title of the field and the mark of the first child.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_OwnersAreTheHighestSharingAncestor()
    {
        // Arrange: the default of a concatenation shares the color scale and so its legend
        var (layers, tree, config) = Prepare(HConcat(string.Empty, Points, Bars));

        // Act
        var legends = LegendPlan.Create(layers, config, null);
        var owners = LegendPlan.Owners(layers);

        // Assert: one legend with one member, owned by the concatenation, the root of the view tree
        var legend = Assert.Single(legends);
        Assert.Equal(["color"], legend.Members.Select(m => m.Channel));
        Assert.Same(tree.Root, legend.Owner);
        Assert.Equal(["k"], legend.Title);
        Assert.Equal("point", legend.Mark.MarkType);
        Assert.Same(tree.Root, Assert.Single(owners));
        Assert.Equal(ViewKind.Concat, tree.Root.Kind);

        // Assert: the layer reports the owner for the channels it has and for those it has not (the view is the same)
        Assert.Same(tree.Root, layers[0].OwnerOf("color"));
        Assert.Same(tree.Root, layers[1].OwnerOf("shape"));
    }

    /// <summary>
    ///     Validates that a legend that the <c>resolve</c> of the concatenation makes independent is owned by the child view: a legend for each child, each with its own mark.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_IndependentLegendStaysWithTheLeaf()
    {
        // Arrange
        var (layers, tree, config) = Prepare(HConcat("\"resolve\": { \"legend\": { \"color\": \"independent\" } },", Points, Bars));

        // Act
        var legends = LegendPlan.Create(layers, config, null);
        var owners = LegendPlan.Owners(layers);

        // Assert: two legends, one for each child, which owns it
        Assert.Equal(2, legends.Count);
        Assert.Same(tree.Units[0], legends[0].Owner);
        Assert.Same(tree.Units[1], legends[1].Owner);
        Assert.Equal(("point", "bar"), (legends[0].Mark.MarkType, legends[1].Mark.MarkType));
        Assert.Equal([tree.Units[0], tree.Units[1]], owners);
    }

    /// <summary>
    ///     Validates that a plan for one owner holds the legends of that owner only: a child that has one channel owned by the concatenation and another owned by itself is planned once
    ///     for each owner, and the two owners' legends never merge even when their scales are the same.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_ChannelFilterPlansEachOwnerOnce()
    {
        // Arrange: the first child colors and shapes by k, the legend of the shape is independent; the second child colors by k
        const string shaped = """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" }, "shape": { "field": "k", "type": "nominal" } } }""";
        var (layers, tree, config) = Prepare(HConcat("\"resolve\": { \"legend\": { \"shape\": \"independent\" } },", shaped, Bars));

        // Act
        var owners = LegendPlan.Owners(layers);
        var all = LegendPlan.Create(layers, config, null);
        var ofConcat = LegendPlan.Create(layers, config, null, tree.Root);
        var ofLeaf = LegendPlan.Create(layers, config, null, tree.Units[0]);
        var ofOther = LegendPlan.Create(layers, config, null, tree.Units[1]);

        // Assert: the owners in channel order: the concatenation (color), then the first child (shape)
        Assert.Equal([tree.Root, tree.Units[0]], owners);

        // Assert: the concatenation's plan has the color of both children only; the child's plan has the shape only
        var color = Assert.Single(ofConcat);
        Assert.Equal(["color"], color.Members.Select(m => m.Channel));
        Assert.Same(tree.Root, color.Owner);
        var shape = Assert.Single(ofLeaf);
        Assert.Equal(["shape"], shape.Members.Select(m => m.Channel));
        Assert.Same(tree.Units[0], shape.Owner);
        Assert.Empty(ofOther);

        // Assert: without a filter both are planned, as two legends although the scales of color and shape are the same
        Assert.Equal(["color", "shape"], all.Select(l => l.Members.Single().Channel));
        Assert.Equal([tree.Root, tree.Units[0]], all.Select(l => l.Owner));
    }

    /// <summary>
    ///     Validates that the children of a concatenation contribute to one legend, whose title joins the titles of the fields of the children and which is built from the first
    ///     child's mark.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_MergedLegendUnionsMembersAcrossConcatChildren()
    {
        // Arrange: the second child colors by the number q as a nominal field
        const string byNumber = """{ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative" }, "color": { "field": "q", "type": "nominal" } } }""";
        var (layers, tree, config) = Prepare(HConcat(string.Empty, Points, byNumber));

        // Act
        var legend = Assert.Single(LegendPlan.Create(layers, config, null));

        // Assert: one member for the two children, titled with both fields, owned by the concatenation
        var member = Assert.Single(legend.Members);
        Assert.Equal(["k, q"], legend.Title);
        Assert.NotNull(member.MergedTitle);
        Assert.Same(tree.Root, legend.Owner);
        Assert.Equal("point", legend.Mark.MarkType);
    }

    /// <summary>
    ///     Validates the nesting: a concatenation that makes the legend independent keeps it with its children, while a sibling of that concatenation still shares it with the root.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_NestedIndependentConcatKeepsItsChildrenLegends()
    {
        // Arrange: a vertical concatenation of an inner concatenation (independent color legends) and a bar
        var json = "{" + Data + ", \"vconcat\": [ { \"resolve\": { \"legend\": { \"color\": \"independent\" } }, \"hconcat\": [" + Points + ", " + Bars + "] }, " + Bars + " ] }";
        var (layers, tree, config) = Prepare(json);

        // Act
        var owners = LegendPlan.Owners(layers);
        var ofRoot = LegendPlan.Create(layers, config, null, tree.Root);

        // Assert: the two children of the inner concatenation own their legends; the bar below shares its legend with the root and is the only member the root plans
        Assert.Equal([tree.Units[0], tree.Units[1], tree.Root], owners);
        var legend = Assert.Single(ofRoot);
        Assert.Equal("bar", legend.Mark.MarkType);
        Assert.Same(tree.Root, legend.Owner);
        Assert.Equal(3, LegendPlan.Create(layers, config, null).Count);
    }

    /// <summary>
    ///     Validates that layers without a view node have no owner: the owner is null, nothing is listed, and a plan with an owner finds nothing, so the plan of a single view is unchanged.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_LayerWithoutNodeHasNoOwner()
    {
        // Arrange
        var parts = ChartTestHelpers.Parts(Points.Replace("{ \"mark\"", "{ " + Data + ", \"mark\"", StringComparison.Ordinal));
        var layers = new List<LegendLayer> { new(parts.Tree.Root, parts.Scales) };
        var (_, tree, _) = Prepare(HConcat(string.Empty, Points));

        // Act
        var legends = LegendPlan.Create(layers, parts.Config, null);

        // Assert
        Assert.Null(Assert.Single(legends).Owner);
        Assert.Null(layers[0].OwnerOf("color"));
        Assert.Empty(LegendPlan.Owners(layers));
        Assert.Empty(LegendPlan.Create(layers, parts.Config, null, tree.Root));
    }

    /// <summary>
    ///     Validates the arguments of the owner functions.
    /// </summary>
    [Fact]
    public void LegendPlan_Owners_ChecksItsArguments()
    {
        // Arrange
        var parts = ChartTestHelpers.Parts(Points.Replace("{ \"mark\"", "{ " + Data + ", \"mark\"", StringComparison.Ordinal));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => LegendPlan.Owners(null!));
        Assert.Throws<ArgumentNullException>(() => new LegendLayer(parts.Tree.Root, parts.Scales).OwnerOf(null!));
    }
}
