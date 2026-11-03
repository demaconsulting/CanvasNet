using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the scales of layered views: shared scales with the combined domain, the owner by scale-kind precedence, titles, independent scales from
///     <c>resolve</c> (also in nested layers), conflicts, explicit domains and the limit on the combined categories, with numbers derived by hand from the sample data.
/// </summary>
public class ScaleResolverLayerTests
{
    /// <summary>The data of most tests: categories b, a, c, a with q 3, 7, 12, 5 and r 10, 20, 35, 15.</summary>
    private const string SampleData = """
        "data": { "values": [
          { "k": "b", "q": 3, "r": 10 },
          { "k": "a", "q": 7, "r": 20 },
          { "k": "c", "q": 12, "r": 35 },
          { "k": "a", "q": 5, "r": 15 }
        ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Resolves the scales of the layers of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The scales of the layers.</returns>
    private static LayeredScales Resolve(string json, VegaLiteLimits? limits = null)
    {
        var tree = SpecParser.Parse(json);
        var config = ConfigResolver.Resolve(tree, null);
        var layers = SpecNormalizer.Layers(tree, limits);
        var shared = DataLoader.Load(tree.Root["data"], tree.SourceName, limits, Ct);
        var inputs = new List<LayerScaleInput>();
        foreach (var node in layers.Units)
        {
            var unit = node.Unit!;
            var table = node.Data is { } own ? DataLoader.Load(own, tree.SourceName, limits, Ct) : shared;
            var plan = EncodingPlan.Create(unit, config);
            inputs.Add(new LayerScaleInput(node, plan, EncodingTransforms.ApplyStandalone(plan, table, unit, null, limits, Ct)));
        }

        return ScaleResolver.ResolveLayers(inputs, config, null, limits, Ct);
    }

    /// <summary>
    ///     Builds a layered specification over the sample data.
    /// </summary>
    /// <param name="top">More top-level properties, each with a trailing comma, or empty.</param>
    /// <param name="layers">The layers (the text of the elements of the <c>layer</c> array).</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string top, params string[] layers) =>
        "{" + top + SampleData + ", \"layer\": [" + string.Join(", ", layers) + "]}";

    /// <summary>
    ///     Creates a string category.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    private static DataValue S(string text) => DataValue.FromString(text);

    /// <summary>
    ///     Validates that a single layer is resolved exactly like a single view and that the merged scales are the layer's scales, not a copy.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_SingleLayerIsTheSingleViewResult()
    {
        // Arrange / Act
        var result = Resolve(Spec(string.Empty, """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "y": { "field": "k", "type": "nominal" } } }"""));

        // Assert
        Assert.Same(result.PerLayer[0], result.Merged);
        Assert.Equal(ScaleKind.Linear, result.Merged["x"]!.Kind);
        Assert.Equal(ScaleKind.Point, result.Merged["y"]!.Kind);
        Assert.Equal([[0]], result.Groups["x"]);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the shared continuous scale spans the domains of all the layers (after each layer's zero rule), that every layer holds the same kind
    ///     and domain bound to its own field, and that the merged title joins the layers' titles.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_SharedContinuousDomainIsTheUnion()
    {
        // Arrange / Act: q spans [3, 12] and r spans [10, 35] without the zero rule
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative", "scale": { "zero": false } } } }""",
            """{ "mark": "line", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative", "scale": { "zero": false } } } }"""));

        // Assert: one scale for the view, with the union [3, 35], titled by both fields
        var merged = result.Merged["y"]!;
        Assert.Equal(ScaleKind.Linear, merged.Kind);
        Assert.Equal((3.0, 35.0), (merged.DomainStart, merged.DomainEnd));
        Assert.Equal("q, r", merged.Title);
        Assert.Equal([[0, 1]], result.Groups["y"]);

        // Assert: both layers map with the union domain but read their own field and keep their own title
        var (first, second) = (result.PerLayer[0]["y"]!, result.PerLayer[1]["y"]!);
        Assert.Equal(((3.0, 35.0), (3.0, 35.0)), ((first.DomainStart, first.DomainEnd), (second.DomainStart, second.DomainEnd)));
        Assert.Equal(("q", "r"), (first.FieldPath, second.FieldPath));
        Assert.Equal(("q", "r"), (first.Title, second.Title));
        Assert.Equal(first.CreateModel(100).Map(7), second.CreateModel(100).Map(7));
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that each layer's zero rule applies before the union: a bar's domain reaches zero, so the union does.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_UnionIncludesTheZeroOfALayer()
    {
        // Arrange / Act: the bar's domain is [0, 12] (zero rule), the rule's is [10, 35] without it
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative", "scale": { "zero": false } } } }"""));

        // Assert
        Assert.Equal((0.0, 35.0), (result.Merged["y"]!.DomainStart, result.Merged["y"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that a channel that only one layer carries keeps that layer's scale as the scale of the view.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ChannelOfOneLayerIsTheViewsScale()
    {
        // Arrange / Act
        var result = Resolve(Spec(
            "\"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" } },",
            """{ "mark": "line" }""",
            """{ "mark": "point", "encoding": { "color": { "field": "k", "type": "nominal" } } }"""));

        // Assert
        Assert.Same(result.PerLayer[1]["color"], result.Merged["color"]);
        Assert.Equal([[1]], result.Groups["color"]);
        Assert.Null(result.PerLayer[0]["color"]);
        Assert.Equal([[0, 1]], result.Groups["x"]);
        Assert.Equal((0.0, 12.0), (result.Merged["x"]!.DomainStart, result.Merged["x"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that the categories of a discrete scale are combined without repeats and sorted when every layer sorts ascending, that every
    ///     layer sees the combined categories, and that an ordinal color scale is built for the combined domain.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_DiscreteDomainIsTheSortedUnion()
    {
        // Arrange / Act: the second layer has its own data with a new category
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""",
            """{ "data": { "values": [ { "k": "d", "q": 1 }, { "k": "a", "q": 2 } ] }, "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }"""));

        // Assert
        var expected = new[] { S("a"), S("b"), S("c"), S("d") };
        Assert.Equal(expected, result.Merged["color"]!.DiscreteDomain);
        Assert.Equal(expected, result.PerLayer[0]["color"]!.DiscreteDomain);
        Assert.Equal(expected, result.PerLayer[1]["color"]!.DiscreteDomain);
        Assert.Equal(ScaleKind.Ordinal, result.Merged["color"]!.Kind);
        Assert.Equal(result.PerLayer[0]["color"]!.ColorRange, result.PerLayer[1]["color"]!.ColorRange);
        Assert.Equal("k", result.Merged["color"]!.Title);
    }

    /// <summary>
    ///     Validates the order of the combined categories: descending when every layer sorts descending, first appearance when the layers disagree.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_DiscreteOrderFollowsTheLayersSorts()
    {
        // Arrange
        const string Other = "\"data\": { \"values\": [ { \"k\": \"d\", \"q\": 1 }, { \"k\": \"a\", \"q\": 2 } ] },";
        static string Layer(string data, string sort) =>
            "{" + data + """ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal", "sort": """ + sort + " } } }";

        // Act
        var descending = Resolve(Spec(string.Empty, Layer(string.Empty, "\"descending\""), Layer(Other, "\"descending\"")));
        var mixed = Resolve(Spec(string.Empty, Layer(string.Empty, "null"), Layer(Other, "\"ascending\"")));

        // Assert: the data order of the first layer (b, a, c) then the new category
        Assert.Equal([S("d"), S("c"), S("b"), S("a")], descending.Merged["color"]!.DiscreteDomain);
        Assert.Equal([S("b"), S("a"), S("c"), S("d")], mixed.Merged["color"]!.DiscreteDomain);
    }

    /// <summary>
    ///     Validates that an explicit discrete domain beats the implicit categories of the other layers: the shared discrete scale has the explicit categories only.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ExplicitDiscreteDomainWins()
    {
        // Arrange / Act
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal", "scale": { "domain": ["c", "b", "z"] } } } }"""));

        // Assert
        Assert.Equal([S("c"), S("b"), S("z")], result.Merged["color"]!.DiscreteDomain);
        Assert.Equal([S("c"), S("b"), S("z")], result.PerLayer[0]["color"]!.DiscreteDomain);
    }

    /// <summary>
    ///     Validates that the explicit domain of one layer decides the shared continuous domain, wherever the layer is.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ExplicitContinuousDomainWins()
    {
        // Arrange / Act
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative", "scale": { "domain": [0, 100] } } } }"""));

        // Assert
        Assert.Equal((0.0, 100.0), (result.PerLayer[0]["y"]!.DomainStart, result.PerLayer[0]["y"]!.DomainEnd));
        Assert.Equal((0.0, 100.0), (result.PerLayer[1]["y"]!.DomainStart, result.PerLayer[1]["y"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates Vega-Lite's merge of explicit domains (<c>mergeValuesWithExplicit</c> with <c>domainsTieBreaker</c>): explicit domains that differ are all kept and
    ///     Vega takes the extent of their union for a continuous scale; equal explicit domains count once (a piecewise one stays piecewise); an implicit domain is ignored
    ///     next to an explicit one.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ExplicitContinuousDomainsAreMerged()
    {
        // Arrange
        static string Layer(string domain) =>
            $$"""{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative", "scale": { "domain": {{domain}} } } } }""";
        const string Implicit = """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative" } } }""";

        // Act
        var differing = Resolve(Spec(string.Empty, Layer("[0, 100]"), Implicit, Layer("[50, 200]")));
        var reversed = Resolve(Spec(string.Empty, Layer("[100, 0]"), Layer("[20, 250]")));
        var equal = Resolve(Spec(string.Empty, Layer("[0, 100]"), Layer("[0, 100]")));
        var piecewise = Resolve(Spec(string.Empty, Layer("[0, 50, 100]"), Layer("[0, 50, 100]")));

        // Assert: the extent of the union, for every layer, whichever layers have no explicit domain
        foreach (var scale in new[] { differing.PerLayer[0]["y"]!, differing.PerLayer[1]["y"]!, differing.PerLayer[2]["y"]!, differing.Merged["y"]! })
        {
            Assert.Equal((0.0, 200.0), (scale.DomainStart, scale.DomainEnd));
            Assert.Null(scale.Domain);
        }

        // Assert: a reversed domain does not matter next to another one (the extent is taken)
        Assert.Equal((0.0, 250.0), (reversed.Merged["y"]!.DomainStart, reversed.Merged["y"]!.DomainEnd));

        // Assert: equal domains are one domain; a piecewise one is kept
        Assert.Equal((0.0, 100.0), (equal.Merged["y"]!.DomainStart, equal.Merged["y"]!.DomainEnd));
        Assert.Equal([0.0, 50.0, 100.0], piecewise.Merged["y"]!.Domain);
        Assert.Equal([0.0, 50.0, 100.0], piecewise.PerLayer[1]["y"]!.Domain);
    }

    /// <summary>
    ///     Validates that explicit discrete domains of several layers are merged in order of first appearance, and that the categories of a layer without an explicit domain
    ///     are ignored next to them.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ExplicitDiscreteDomainsAreMerged()
    {
        // Arrange / Act
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal", "scale": { "domain": ["c", "b", "z"] } } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal", "scale": { "domain": ["b", "a", "c", "y"] } } } }"""));

        // Assert: c, b, z from the first explicit domain, then the new categories of the second (a, y)
        var expected = new[] { S("c"), S("b"), S("z"), S("a"), S("y") };
        Assert.Equal(expected, result.Merged["color"]!.DiscreteDomain);
        Assert.Equal(expected, result.PerLayer[0]["color"]!.DiscreteDomain);
        Assert.Equal(expected, result.PerLayer[2]["color"]!.DiscreteDomain);
    }

    /// <summary>
    ///     Validates the precedence of the scale kinds: a band scale of a bar beats the point scale of a line whichever comes first, so both layers share the
    ///     band scale (with the bar's padding and the step), and the combined categories size the view.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_BandBeatsPointAndSizesTheView()
    {
        // Arrange / Act: the line comes first and has the category d that the bar lacks
        var result = Resolve(Spec(
            string.Empty,
            """{ "data": { "values": [ { "k": "d", "q": 1 }, { "k": "a", "q": 2 } ] }, "mark": "line", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }"""));

        // Assert: both layers have the band scale of the bar with four categories
        foreach (var scale in new[] { result.PerLayer[0]["x"]!, result.PerLayer[1]["x"]!, result.Merged["x"]! })
        {
            Assert.Equal(ScaleKind.Band, scale.Kind);
            Assert.Equal(0.1, scale.PaddingInner);
            Assert.Equal(4, scale.DiscreteDomain.Count);
        }

        // Assert: the default step of 20 pixels sizes the view for all four categories
        Assert.Equal(20 * ScaleModel.BandSpace(4, 0.1, 0.05), result.Merged.DiscreteWidth!.Value, 9);
        Assert.Equal(result.Merged.DiscreteWidth, result.PerLayer[0].DiscreteWidth);
    }

    /// <summary>
    ///     Validates that an explicit step through the layered view sizes the union of the categories.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_StepAppliesToTheCombinedCategories()
    {
        // Arrange / Act: the line reads three categories, the point layer's own data adds a fourth
        var result = Resolve(Spec(
            "\"width\": { \"step\": 30 },",
            """{ "mark": "line", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "data": { "values": [ { "k": "z", "q": 1 } ] }, "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }"""));

        // Assert: point scale, one inner padding step and half a step of outer padding each side
        var x = result.Merged["x"]!;
        Assert.Equal(ScaleKind.Point, x.Kind);
        Assert.Equal(4, x.DiscreteDomain.Count);
        Assert.Equal(30 * ScaleModel.BandSpace(4, 1, 0.5), result.Merged.DiscreteWidth!.Value, 9);
    }

    /// <summary>
    ///     Validates that <c>resolve.scale</c> independent keeps one scale for each layer, with no merged scale and no merged domain, while the other channels
    ///     stay shared.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_IndependentScaleStaysPerLayer()
    {
        // Arrange / Act
        var result = Resolve(Spec(
            "\"resolve\": { \"scale\": { \"y\": \"independent\" } },",
            """{ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "mark": "line", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative" } } }"""));

        // Assert
        Assert.Null(result.Merged["y"]);
        Assert.Equal([[0], [1]], result.Groups["y"]);
        Assert.Equal((0.0, 12.0), (result.PerLayer[0]["y"]!.DomainStart, result.PerLayer[0]["y"]!.DomainEnd));
        Assert.Equal((0.0, 35.0), (result.PerLayer[1]["y"]!.DomainStart, result.PerLayer[1]["y"]!.DomainEnd));
        Assert.Equal("q", result.PerLayer[0]["y"]!.Title);
        Assert.NotNull(result.Merged["x"]);
        Assert.Equal([[0, 1]], result.Groups["x"]);
    }

    /// <summary>
    ///     Validates that an independent scale in a nested layered view separates only the layers below it, and that an independent scale above a shared nested
    ///     view still lets the layers of the nested view share.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_NestedResolveDecidesWhichLayersShare()
    {
        // Arrange
        const string Point = """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "FIELD", "type": "quantitative" } } }""";
        var a = Point.Replace("FIELD", "q", StringComparison.Ordinal);
        var b = Point.Replace("FIELD", "r", StringComparison.Ordinal);

        // Act: the inner view is independent: its two layers do not share, and neither joins the outer layer
        var innerIndependent = Resolve(Spec(
            string.Empty,
            a,
            """{ "resolve": { "scale": { "y": "independent" } }, "layer": [""" + b + "," + a + "] }"));

        // Act: the outer view is independent and the inner one shared: the inner layers share with each other only
        var outerIndependent = Resolve(Spec(
            "\"resolve\": { \"scale\": { \"y\": \"independent\" } },",
            a,
            """{ "layer": [""" + b + "," + a + "] }"));

        // Assert
        Assert.Equal([[0], [1], [2]], innerIndependent.Groups["y"]);
        Assert.Null(innerIndependent.Merged["y"]);
        Assert.Equal([[0], [1, 2]], outerIndependent.Groups["y"]);
        Assert.Null(outerIndependent.Merged["y"]);
        Assert.Equal((0.0, 35.0), (outerIndependent.PerLayer[1]["y"]!.DomainStart, outerIndependent.PerLayer[1]["y"]!.DomainEnd));
        Assert.Equal((0.0, 35.0), (outerIndependent.PerLayer[2]["y"]!.DomainStart, outerIndependent.PerLayer[2]["y"]!.DomainEnd));
        Assert.Equal((0.0, 12.0), (outerIndependent.PerLayer[0]["y"]!.DomainStart, outerIndependent.PerLayer[0]["y"]!.DomainEnd));
        Assert.Equal((0.0, 35.0), (innerIndependent.PerLayer[1]["y"]!.DomainStart, innerIndependent.PerLayer[1]["y"]!.DomainEnd));
        Assert.Equal((0.0, 12.0), (innerIndependent.PerLayer[2]["y"]!.DomainStart, innerIndependent.PerLayer[2]["y"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that layers whose scales cannot be merged keep their own scale and are reported: a linear scale and a logarithmic one, a continuous
    ///     scale and a discrete one.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_IncompatibleScalesKeepTheirOwnAndAreReported()
    {
        // Arrange / Act
        var logarithmic = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative", "scale": { "type": "log" } } } }"""));
        var discrete = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "k", "type": "nominal" } } }"""));

        // Assert: the second layer keeps its own scale; the warning is located at its channel
        var warning = Assert.Single(logarithmic.Warnings);
        Assert.Equal("vl-layer-scale-conflict", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("/layer/1/encoding/y", warning.Location.Pointer);
        Assert.Equal(ScaleKind.Linear, logarithmic.PerLayer[0]["y"]!.Kind);
        Assert.Equal(ScaleTransformKind.Log, logarithmic.PerLayer[1]["y"]!.Transform.Kind);
        Assert.Null(logarithmic.Merged["y"]);
        Assert.Equal([[0], [1]], logarithmic.Groups["y"]);
        Assert.Equal("vl-layer-scale-conflict", Assert.Single(discrete.Warnings).Id);
        Assert.Equal(ScaleKind.Point, discrete.PerLayer[1]["y"]!.Kind);
    }

    /// <summary>
    ///     Validates that a discretizing scale is not combined: the first layer's scale (domain and intervals) is used for all the layers that share it.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_DiscretizingScaleIsTheFirstLayersOwn()
    {
        // Arrange / Act: the second layer's own data reaches 100
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "q", "type": "quantitative", "scale": { "type": "quantize" } } } }""",
            """{ "data": { "values": [ { "q": 1 }, { "q": 100 } ] }, "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "q", "type": "quantitative", "scale": { "type": "quantize" } } } }"""));

        // Assert
        var (first, second) = (result.PerLayer[0]["color"]!, result.PerLayer[1]["color"]!);
        Assert.Equal(ScaleKind.Quantize, second.Kind);
        Assert.Equal((first.DomainStart, first.DomainEnd), (second.DomainStart, second.DomainEnd));
        Assert.True(first.DomainEnd < 100);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the limit on the categories of a scale applies to the combined categories.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_CombinedCategoriesAreLimited()
    {
        // Arrange: three categories in each layer, six together, a limit of five
        var limits = new VegaLiteLimits { MaxScaleDomainSize = 5 };
        var json = Spec(
            string.Empty,
            """{ "data": { "values": [ { "k": "a", "q": 1 }, { "k": "b", "q": 2 }, { "k": "c", "q": 3 } ] }, "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""",
            """{ "data": { "values": [ { "k": "d", "q": 1 }, { "k": "e", "q": 2 }, { "k": "f", "q": 3 } ] }, "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""");

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Resolve(json, limits));

        // Assert
        Assert.Contains("MaxScaleDomainSize", ex.Message, StringComparison.Ordinal);
        Assert.Equal("/layer/0/encoding/color", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates the arguments of the layer resolution.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ChecksItsArguments()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "mark": "point" }""");
        var config = ConfigResolver.Resolve(tree, null);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.ResolveLayers(null!, config, null, null, Ct));
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.ResolveLayers([], null!, null, null, Ct));
        Assert.Throws<ArgumentException>(() => ScaleResolver.ResolveLayers([], config, null, null, Ct));
    }

    /// <summary>
    ///     Validates that a layer without a mark or encoding has no scales and does not disturb the others.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_LayerWithoutEncodingHasNoScales()
    {
        // Arrange / Act
        var result = Resolve(Spec(
            string.Empty,
            """{ "mark": "rule" }""",
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" } } }"""));

        // Assert
        Assert.Empty(result.PerLayer[0].Scales);
        Assert.Equal([[1]], result.Groups["x"]);
        Assert.Same(result.PerLayer[1]["x"], result.Merged["x"]);
    }
}
